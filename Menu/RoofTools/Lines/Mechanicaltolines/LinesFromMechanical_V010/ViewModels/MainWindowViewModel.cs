using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.LinesFromMechanical.V010.Models;
using Revit26_Plugin.LinesFromMechanical.V010.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using RevitColor = Autodesk.Revit.DB.Color;
using WpfColor   = System.Windows.Media.Color;

// LinkInfo, FloorFamilyInfo, ColorOption moved to Models/ (V009.Models)

namespace Revit26_Plugin.LinesFromMechanical.V010.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    public const string VersionString = "V009";
    public string Version => VersionString;

    // Valid input ranges (mm)
    private const double MinRadiusMm =    1.0;
    private const double MaxRadiusMm = 10000.0;
    private const double MinOffsetMm = -10000.0;
    private const double MaxOffsetMm =  10000.0;

    private readonly UIDocument _uiDoc;
    private readonly Document   _doc;
    private readonly ViewPlan   _activePlanView;
    private readonly LinkedMechanicalCircleService _circleService;
    private readonly LinkedMechanicalFloorService  _floorService;
    private readonly LinkedMechanicalSourceLoader  _sourceLoader;
    private readonly List<Element> _currentlyHighlightedElements = [];

    // ── Observable properties ──────────────────────────────────────────────────

    [ObservableProperty] private string   _activeViewName  = string.Empty;
    [ObservableProperty] private bool     _isValidView;
    [ObservableProperty] private string   _viewWarning     = string.Empty;
    [ObservableProperty] private bool     _isProcessing;
    [ObservableProperty] private int      _previewCount;
    [ObservableProperty] private string   _radiusText      = "400";
    [ObservableProperty] private string   _floorOffsetText = "0";
    [ObservableProperty] private string   _inputWarning    = string.Empty;
    [ObservableProperty] private WpfColor _selectedColor;
    [ObservableProperty] private bool     _isDetailLineMode = true;
    [ObservableProperty] private bool     _isFloorMode;
    [ObservableProperty] private bool     _showNoFloorTypeWarning;
    [ObservableProperty] private OperationSummary? _lastSummary;

    [ObservableProperty] private ObservableCollection<LinkInfo>        _availableLinks         = [];
    [ObservableProperty] private ObservableCollection<string>          _availableFamilies      = [];
    [ObservableProperty] private ObservableCollection<FloorFamilyInfo> _availableFloorFamilies = [];
    [ObservableProperty] private ObservableCollection<FloorType>       _availableFloorTypes    = [];
    [ObservableProperty] private ObservableCollection<ColorOption>     _colorOptions           = [];
    [ObservableProperty] private ObservableCollection<LogEntry>        _logEntries             = [];

    private LinkInfo?        _selectedLink;
    private string?          _selectedFamily;
    private FloorFamilyInfo? _selectedFloorFamily;
    private FloorType?       _selectedFloorType;
    private ColorOption?     _selectedColorOption;

    public LinkInfo? SelectedLink
    {
        get => _selectedLink;
        set { SetProperty(ref _selectedLink, value); LoadFamiliesForSelectedLink(); UpdatePreviewCount(); UpdateCanProcess(); }
    }

    public string? SelectedFamily
    {
        get => _selectedFamily;
        set { SetProperty(ref _selectedFamily, value); UpdatePreviewCount(); UpdateCanProcess(); }
    }

    public FloorFamilyInfo? SelectedFloorFamily
    {
        get => _selectedFloorFamily;
        set { SetProperty(ref _selectedFloorFamily, value); LoadFloorTypesForFamily(); UpdateCanProcess(); }
    }

    public FloorType? SelectedFloorType
    {
        get => _selectedFloorType;
        set { SetProperty(ref _selectedFloorType, value); UpdateCanProcess(); }
    }

    public ColorOption? SelectedColorOption
    {
        get => _selectedColorOption;
        set { SetProperty(ref _selectedColorOption, value); if (value != null) SelectedColor = value.Color; }
    }

    // Derived
    public bool ShowViewWarning  => !IsValidView && !string.IsNullOrEmpty(ViewWarning);
    public bool ShowInputWarning => !string.IsNullOrEmpty(InputWarning);
    public bool IsNotProcessing  => !IsProcessing;
    public bool ShowSummary      => LastSummary != null;

    // ── Constructor ────────────────────────────────────────────────────────────

    public MainWindowViewModel(UIDocument uiDoc, Document doc, ViewPlan activePlanView)
    {
        _uiDoc          = uiDoc;
        _doc            = doc;
        _activePlanView = activePlanView;

        _circleService = new LinkedMechanicalCircleService();
        _circleService.OnLog += AddLog;

        _floorService = new LinkedMechanicalFloorService();
        _floorService.OnLog += AddLog;

        _sourceLoader = new LinkedMechanicalSourceLoader(doc, activePlanView);
        _sourceLoader.OnLog += AddLog;

        InitializeColorOptions();
        SelectedColorOption = ColorOptions.First();
        SelectedColor       = ColorOptions.First().Color;

        ActiveViewName = _activePlanView.Name;
        IsValidView    = true;

        LoadVisibleLinks();
        LoadFloorTypes();
    }

    // ── Property change hooks ──────────────────────────────────────────────────

    partial void OnIsProcessingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotProcessing));
        UpdateCanProcess();
    }

    partial void OnIsDetailLineModeChanged(bool value)
    {
        if (value) IsFloorMode = false;
        UpdatePreviewCount();
        UpdateCanProcess();
    }

    partial void OnIsFloorModeChanged(bool value)
    {
        if (value) IsDetailLineMode = false;
        UpdatePreviewCount();
        UpdateCanProcess();
    }

    partial void OnRadiusTextChanged(string value)      { ValidateInputs(); UpdateCanProcess(); }
    partial void OnFloorOffsetTextChanged(string value) { ValidateInputs(); UpdateCanProcess(); }
    partial void OnPreviewCountChanged(int value)       => UpdateCanProcess();
    partial void OnViewWarningChanged(string value)     => OnPropertyChanged(nameof(ShowViewWarning));
    partial void OnIsValidViewChanged(bool value)       => OnPropertyChanged(nameof(ShowViewWarning));
    partial void OnInputWarningChanged(string value)    => OnPropertyChanged(nameof(ShowInputWarning));
    partial void OnLastSummaryChanged(OperationSummary? value) => OnPropertyChanged(nameof(ShowSummary));

    // ── Input validation (item 7) ──────────────────────────────────────────────

    private bool TryGetRadiusMm(out double radiusMm)
        => TryParseRange(RadiusText, MinRadiusMm, MaxRadiusMm, out radiusMm);

    private bool TryGetOffsetMm(out double offsetMm)
        => TryParseRange(FloorOffsetText, MinOffsetMm, MaxOffsetMm, out offsetMm);

    private static bool TryParseRange(string text, double min, double max, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && value >= min && value <= max)
            return true;
        value = 0;
        return false;
    }

    private void ValidateInputs()
    {
        if (!TryGetRadiusMm(out _))
        {
            InputWarning = $"Radius must be a number between {MinRadiusMm:0} and {MaxRadiusMm:0} mm.";
            return;
        }
        if (IsFloorMode && !TryGetOffsetMm(out _))
        {
            InputWarning = $"Level offset must be a number between {MinOffsetMm:0} and {MaxOffsetMm:0} mm.";
            return;
        }
        InputWarning = string.Empty;
    }

    // ── Commands ───────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanHighlight))]
    private void Highlight()
    {
        ClearHighlight();
        if (SelectedLink?.Instance == null || string.IsNullOrEmpty(SelectedFamily)) return;

        try
        {
            var elements = _circleService.GetPreviewElements(_doc, _activePlanView, SelectedLink.Instance, SelectedFamily);
            _currentlyHighlightedElements.AddRange(elements);

            if (elements.Count > 0)
            {
                _uiDoc.Selection.SetElementIds(elements.Select(e => e.Id).ToList());
                AddLog(LogLevel.Info, $"Highlighted {elements.Count} elements in the active view.");
            }
            else AddLog(LogLevel.Warning, "No elements found to highlight.");
        }
        catch (Exception ex) { AddLog(LogLevel.Error, $"Error highlighting elements: {ex.Message}"); }
    }

    private bool CanHighlight()
        => !IsProcessing && SelectedLink != null && !string.IsNullOrEmpty(SelectedFamily) && PreviewCount > 0;

    [RelayCommand(CanExecute = nameof(CanProcess))]
    private void Process()
    {
        if (SelectedLink?.Instance == null || string.IsNullOrEmpty(SelectedFamily)) return;
        if (!TryGetRadiusMm(out double radiusMm)) { AddLog(LogLevel.Error, "Invalid radius."); return; }

        IsProcessing = true;
        LogEntries.Clear();
        LastSummary  = null;
        AddLog(LogLevel.Info, "=== Operation Started ===");

        try
        {
            OperationSummary summary;
            if (IsDetailLineMode)
            {
                var revitColor = new RevitColor(SelectedColor.R, SelectedColor.G, SelectedColor.B);
                summary = _circleService.CreateDetailLines(
                    _doc, _activePlanView, SelectedLink.Instance, SelectedFamily, radiusMm, revitColor);
            }
            else
            {
                if (SelectedFloorType == null) { AddLog(LogLevel.Error, "No floor type selected."); return; }
                if (!TryGetOffsetMm(out double offsetMm)) { AddLog(LogLevel.Error, "Invalid offset."); return; }

                summary = _floorService.CreateFloors(
                    _doc, _activePlanView, SelectedLink.Instance, SelectedFamily,
                    radiusMm, SelectedFloorType, offsetMm);
            }

            LastSummary = summary;
            AddLog(LogLevel.Success, "=== Operation Complete ===");
            foreach (var line in summary.ToDisplayText().Split('\n'))
                AddLog(LogLevel.Info, line.Trim());
        }
        catch (Exception ex) { AddLog(LogLevel.Error, ex.Message); }
        finally
        {
            IsProcessing = false;
            ClearHighlight();
            UpdatePreviewCount();
        }
    }

    private bool CanProcess()
    {
        if (!IsValidView || IsProcessing || SelectedLink == null
            || string.IsNullOrEmpty(SelectedFamily) || PreviewCount <= 0
            || !string.IsNullOrEmpty(InputWarning))
            return false;

        if (!TryGetRadiusMm(out _)) return false;

        return IsDetailLineMode
            || (SelectedFloorType != null && _activePlanView.GenLevel != null && !ShowNoFloorTypeWarning && TryGetOffsetMm(out _));
    }

    [RelayCommand]
    private void Cancel()
    {
        ClearHighlight();
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this)?.Close();
    }

    [RelayCommand(CanExecute = nameof(CanCopyLog))]
    private void CopyLog()
    {
        try
        {
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString())));
            AddLog(LogLevel.Info, "Log copied to clipboard.");
        }
        catch (Exception ex) { AddLog(LogLevel.Error, $"Failed to copy log: {ex.Message}"); }
    }

    private bool CanCopyLog() => LogEntries.Count > 0;

    [RelayCommand]
    private void ClearLog() => LogEntries.Clear();

    // ── Private helpers ─────────────────────────────────────────────────────────

    private void UpdateCanProcess()
    {
        HighlightCommand.NotifyCanExecuteChanged();
        ProcessCommand.NotifyCanExecuteChanged();
        CopyLogCommand.NotifyCanExecuteChanged();
    }

    private void InitializeColorOptions()
    {
        ColorOptions =
        [
            new ColorOption { Name = "Red",    Color = System.Windows.Media.Colors.Red    },
            new ColorOption { Name = "Blue",   Color = System.Windows.Media.Colors.Blue   },
            new ColorOption { Name = "Green",  Color = System.Windows.Media.Colors.Green  },
            new ColorOption { Name = "Yellow", Color = System.Windows.Media.Colors.Yellow },
            new ColorOption { Name = "Orange", Color = System.Windows.Media.Colors.Orange },
            new ColorOption { Name = "Purple", Color = System.Windows.Media.Colors.Purple },
            new ColorOption { Name = "Cyan",   Color = System.Windows.Media.Colors.Cyan   },
        ];
    }

    private void LoadVisibleLinks()
    {
        AvailableLinks.Clear();

        foreach (var link in _sourceLoader.LoadVisibleLinks())
            AvailableLinks.Add(link);

        if (AvailableLinks.Count > 0) SelectedLink = AvailableLinks[0];

        UpdateCanProcess();
    }

    private void LoadFamiliesForSelectedLink()
    {
        AvailableFamilies.Clear();
        SelectedFamily = null;

        foreach (var name in LinkedMechanicalSourceLoader.LoadFamiliesForLink(SelectedLink?.Instance))
            AvailableFamilies.Add(name);

        if (AvailableFamilies.Count > 0) SelectedFamily = AvailableFamilies[0];
    }

    private void LoadFloorTypes()
    {
        AvailableFloorFamilies.Clear();
        AvailableFloorTypes.Clear();
        SelectedFloorFamily = null;
        SelectedFloorType   = null;

        bool noFloorTypesFound;
        foreach (var family in _sourceLoader.LoadFloorFamilies(out noFloorTypesFound))
            AvailableFloorFamilies.Add(family);

        ShowNoFloorTypeWarning = noFloorTypesFound;
        if (noFloorTypesFound) return;

        if (AvailableFloorFamilies.Count > 0) SelectedFloorFamily = AvailableFloorFamilies[0];
    }

    private void LoadFloorTypesForFamily()
    {
        AvailableFloorTypes.Clear();
        SelectedFloorType = null;

        foreach (var ft in LinkedMechanicalSourceLoader.LoadFloorTypesForFamily(SelectedFloorFamily))
            AvailableFloorTypes.Add(ft);

        if (AvailableFloorTypes.Count > 0) SelectedFloorType = AvailableFloorTypes[0];
    }

    private void UpdatePreviewCount()
    {
        if (SelectedLink?.Instance == null || string.IsNullOrEmpty(SelectedFamily))
        { PreviewCount = 0; return; }

        try
        {
            PreviewCount = IsDetailLineMode
                ? _circleService.GetPreviewCount(_doc, _activePlanView, SelectedLink.Instance, SelectedFamily)
                : _floorService.GetPreviewCount(_doc, _activePlanView, SelectedLink.Instance, SelectedFamily);
        }
        catch (Exception ex)
        {
            AddLog(LogLevel.Error, $"Error getting preview count: {ex.Message}");
            PreviewCount = 0;
        }
    }

    private void ClearHighlight()
    {
        _uiDoc.Selection.SetElementIds([]);
        _currentlyHighlightedElements.Clear();
    }

    private void AddLog(LogLevel level, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogEntries.Add(new LogEntry(level, message));
            CopyLogCommand.NotifyCanExecuteChanged();
        });
    }

    public void Dispose()
    {
        _circleService.OnLog -= AddLog;
        _floorService.OnLog  -= AddLog;
        _sourceLoader.OnLog  -= AddLog;
        ClearHighlight();
    }
}
