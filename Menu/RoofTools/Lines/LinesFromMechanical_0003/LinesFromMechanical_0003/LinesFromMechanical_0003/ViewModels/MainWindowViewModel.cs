using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.LinesFromMechanical.V003.Models;
using Revit26_Plugin.LinesFromMechanical.V003.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RevitColor = Autodesk.Revit.DB.Color;
using WpfColor = System.Windows.Media.Color;

namespace Revit26_Plugin.LinesFromMechanical.V003.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly UIDocument _uiDoc;
    private readonly Document _doc;
    private readonly ViewPlan _activePlanView;
    private readonly LinkedMechanicalCircleService _circleService;
    private readonly LinkedMechanicalFloorService _floorService;
    private readonly List<Element> _currentlyHighlightedElements;

    private bool _isProcessing;
    private string _activeViewName;
    private bool _isValidView;
    private string _viewWarning;
    private ObservableCollection<LinkInfo> _availableLinks;
    private LinkInfo _selectedLink;
    private ObservableCollection<string> _availableFamilies;
    private string _selectedFamily;
    private int _previewCount;
    private double _radiusMm;
    private WpfColor _selectedColor;
    private ObservableCollection<string> _logMessages;
    private bool _canProcess;

    // Mode selection
    private bool _isDetailLineMode = true;
    private bool _isFloorMode;

    // Floor properties
    private ObservableCollection<FloorFamilyInfo> _availableFloorFamilies;
    private FloorFamilyInfo _selectedFloorFamily;
    private ObservableCollection<FloorType> _availableFloorTypes;
    private FloorType _selectedFloorType;
    private double _floorOffsetMm;
    private bool _showNoFloorTypeWarning;

    // Color options for combobox
    private ObservableCollection<ColorOption> _colorOptions;
    private ColorOption _selectedColorOption;

    public MainWindowViewModel(UIDocument uiDoc, Document doc, ViewPlan activePlanView)
    {
        _uiDoc = uiDoc;
        _doc = doc;
        _activePlanView = activePlanView;
        _circleService = new LinkedMechanicalCircleService();
        _circleService.OnLogMessage += AddLogMessage;
        _floorService = new LinkedMechanicalFloorService();
        _floorService.OnLogMessage += AddLogMessage;
        _currentlyHighlightedElements = new List<Element>();

        AvailableLinks = new ObservableCollection<LinkInfo>();
        AvailableFamilies = new ObservableCollection<string>();
        AvailableFloorFamilies = new ObservableCollection<FloorFamilyInfo>();
        AvailableFloorTypes = new ObservableCollection<FloorType>();
        LogMessages = new ObservableCollection<string>();

        RadiusMm = 400.0;
        FloorOffsetMm = 0.0;

        // Initialize color options
        InitializeColorOptions();
        SelectedColor = Colors.Red;
        SelectedColorOption = ColorOptions.FirstOrDefault(c => c.Color == Colors.Red);

        HighlightCommand = new RelayCommand(_ => ExecuteHighlight(), _ => CanExecuteHighlight());
        ProcessCommand = new RelayCommand(_ => ExecuteProcess(), _ => CanExecuteProcess());
        CancelCommand = new RelayCommand(_ => ExecuteCancel());
        CopyLogCommand = new RelayCommand(_ => ExecuteCopyLog(), _ => LogMessages.Count > 0);

        InitializeViewValidation();
        LoadVisibleLinks();
        LoadFloorTypes();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Properties
    public string ActiveViewName
    {
        get => _activeViewName;
        set { _activeViewName = value; OnPropertyChanged(); }
    }

    public bool IsValidView
    {
        get => _isValidView;
        set { _isValidView = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowViewWarning)); }
    }

    public string ViewWarning
    {
        get => _viewWarning;
        set { _viewWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowViewWarning)); }
    }

    public bool ShowViewWarning => !IsValidView && !string.IsNullOrEmpty(ViewWarning);

    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotProcessing)); UpdateCanProcess(); }
    }

    public bool IsNotProcessing => !IsProcessing;

    public ObservableCollection<LinkInfo> AvailableLinks
    {
        get => _availableLinks;
        set { _availableLinks = value; OnPropertyChanged(); }
    }

    public LinkInfo SelectedLink
    {
        get => _selectedLink;
        set
        {
            _selectedLink = value;
            OnPropertyChanged();
            LoadFamiliesForSelectedLink();
            UpdatePreviewCount();
            UpdateCanProcess();
        }
    }

    public ObservableCollection<string> AvailableFamilies
    {
        get => _availableFamilies;
        set { _availableFamilies = value; OnPropertyChanged(); }
    }

    public string SelectedFamily
    {
        get => _selectedFamily;
        set
        {
            _selectedFamily = value;
            OnPropertyChanged();
            UpdatePreviewCount();
            UpdateCanProcess();
        }
    }

    public int PreviewCount
    {
        get => _previewCount;
        set { _previewCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowPreviewCount)); }
    }

    public bool ShowPreviewCount => PreviewCount > 0;

    public double RadiusMm
    {
        get => _radiusMm;
        set
        {
            if (value > 0 && value <= 10000)
            {
                _radiusMm = value;
                OnPropertyChanged();
                UpdateCanProcess();
            }
            else if (value > 10000)
            {
                AddLogMessage("Warning: Radius limited to 10000mm (10m) for performance reasons.");
                _radiusMm = 10000;
                OnPropertyChanged();
            }
        }
    }

    public WpfColor SelectedColor
    {
        get => _selectedColor;
        set
        {
            _selectedColor = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ColorOption> ColorOptions
    {
        get => _colorOptions;
        set { _colorOptions = value; OnPropertyChanged(); }
    }

    public ColorOption SelectedColorOption
    {
        get => _selectedColorOption;
        set
        {
            _selectedColorOption = value;
            if (value != null)
            {
                SelectedColor = value.Color;
            }
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> LogMessages
    {
        get => _logMessages;
        set { _logMessages = value; OnPropertyChanged(); }
    }

    public bool CanProcess
    {
        get => _canProcess;
        set { _canProcess = value; OnPropertyChanged(); }
    }

    // Mode Properties
    public bool IsDetailLineMode
    {
        get => _isDetailLineMode;
        set
        {
            _isDetailLineMode = value;
            if (value) _isFloorMode = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFloorMode));
            UpdatePreviewCount();
            UpdateCanProcess();
        }
    }

    public bool IsFloorMode
    {
        get => _isFloorMode;
        set
        {
            _isFloorMode = value;
            if (value) _isDetailLineMode = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDetailLineMode));
            UpdatePreviewCount();
            UpdateCanProcess();
        }
    }

    // Floor Properties
    public ObservableCollection<FloorFamilyInfo> AvailableFloorFamilies
    {
        get => _availableFloorFamilies;
        set { _availableFloorFamilies = value; OnPropertyChanged(); }
    }

    public FloorFamilyInfo SelectedFloorFamily
    {
        get => _selectedFloorFamily;
        set
        {
            _selectedFloorFamily = value;
            OnPropertyChanged();
            LoadFloorTypesForFamily();
            UpdateCanProcess();
        }
    }

    public ObservableCollection<FloorType> AvailableFloorTypes
    {
        get => _availableFloorTypes;
        set { _availableFloorTypes = value; OnPropertyChanged(); }
    }

    public FloorType SelectedFloorType
    {
        get => _selectedFloorType;
        set
        {
            _selectedFloorType = value;
            OnPropertyChanged();
            UpdateCanProcess();
        }
    }

    public double FloorOffsetMm
    {
        get => _floorOffsetMm;
        set
        {
            _floorOffsetMm = value;
            OnPropertyChanged();
            UpdateCanProcess();
        }
    }

    public bool ShowNoFloorTypeWarning
    {
        get => _showNoFloorTypeWarning;
        set { _showNoFloorTypeWarning = value; OnPropertyChanged(); }
    }

    // Commands
    public ICommand HighlightCommand { get; }
    public ICommand ProcessCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CopyLogCommand { get; }

    private void InitializeColorOptions()
    {
        ColorOptions = new ObservableCollection<ColorOption>
        {
            new ColorOption { Name = "Red", Color = Colors.Red },
            new ColorOption { Name = "Blue", Color = Colors.Blue },
            new ColorOption { Name = "Green", Color = Colors.Green },
            new ColorOption { Name = "Yellow", Color = Colors.Yellow },
            new ColorOption { Name = "Orange", Color = Colors.Orange },
            new ColorOption { Name = "Purple", Color = Colors.Purple },
            new ColorOption { Name = "Cyan", Color = Colors.Cyan }
        };
    }

    private void InitializeViewValidation()
    {
        ActiveViewName = _activePlanView.Name;
        IsValidView = true;
        ViewWarning = string.Empty;
    }

    private void LoadVisibleLinks()
    {
        AvailableLinks.Clear();

        var collector = new FilteredElementCollector(_doc, _activePlanView.Id);
        var linkInstances = collector.OfClass(typeof(RevitLinkInstance))
            .WhereElementIsNotElementType()
            .Cast<RevitLinkInstance>()
            .ToList();

        foreach (var link in linkInstances)
        {
            var linkDoc = link.GetLinkDocument();
            if (linkDoc != null)
            {
                AvailableLinks.Add(new LinkInfo
                {
                    Id = link.Id,
                    Name = link.Name,
                    Instance = link
                });
            }
        }

        if (AvailableLinks.Count > 0 && SelectedLink == null)
        {
            SelectedLink = AvailableLinks[0];
        }

        if (AvailableLinks.Count == 0 && IsValidView)
        {
            AddLogMessage("Warning: No visible loaded links found in active view.");
        }

        UpdateCanProcess();
    }

    private void LoadFamiliesForSelectedLink()
    {
        AvailableFamilies.Clear();
        SelectedFamily = null;

        if (SelectedLink?.Instance == null)
            return;

        var linkDoc = SelectedLink.Instance.GetLinkDocument();
        if (linkDoc == null)
            return;

        var familyNames = new HashSet<string>();

        var collector = new FilteredElementCollector(linkDoc)
            .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>();

        foreach (var instance in collector)
        {
            var familyName = instance.Symbol?.Family?.Name;
            if (!string.IsNullOrEmpty(familyName))
                familyNames.Add(familyName);
        }

        foreach (var name in familyNames.OrderBy(n => n))
        {
            AvailableFamilies.Add(name);
        }

        if (AvailableFamilies.Count > 0 && string.IsNullOrEmpty(SelectedFamily))
        {
            SelectedFamily = AvailableFamilies[0];
        }
    }

    private void LoadFloorTypes()
    {
        AvailableFloorFamilies.Clear();
        AvailableFloorTypes.Clear();
        SelectedFloorFamily = null;
        SelectedFloorType = null;

        var floorTypes = new FilteredElementCollector(_doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .Where(ft => ft != null)
            .ToList();

        if (!floorTypes.Any())
        {
            ShowNoFloorTypeWarning = true;
            AddLogMessage("Warning: No active floor types found in document.");
            return;
        }

        ShowNoFloorTypeWarning = false;

        var familyGroups = floorTypes
            .GroupBy(ft => GetFloorFamilyName(ft))
            .OrderBy(g => g.Key);

        foreach (var group in familyGroups)
        {
            var familyInfo = new FloorFamilyInfo
            {
                Name = group.Key,
                FloorTypes = group.ToList()
            };
            AvailableFloorFamilies.Add(familyInfo);
        }

        if (AvailableFloorFamilies.Count > 0)
        {
            SelectedFloorFamily = AvailableFloorFamilies[0];
        }
    }

    private string GetFloorFamilyName(FloorType floorType)
    {
        var familyParam = floorType.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME);
        if (familyParam != null && !string.IsNullOrEmpty(familyParam.AsString()))
            return familyParam.AsString();

        return "Floor Types";
    }

    private void LoadFloorTypesForFamily()
    {
        AvailableFloorTypes.Clear();
        SelectedFloorType = null;

        if (SelectedFloorFamily == null || SelectedFloorFamily.FloorTypes == null)
            return;

        foreach (var floorType in SelectedFloorFamily.FloorTypes.OrderBy(ft => ft.Name))
        {
            AvailableFloorTypes.Add(floorType);
        }

        if (AvailableFloorTypes.Count > 0)
        {
            SelectedFloorType = AvailableFloorTypes[0];
        }
    }

    private void UpdatePreviewCount()
    {
        if (SelectedLink?.Instance == null || string.IsNullOrEmpty(SelectedFamily))
        {
            PreviewCount = 0;
            return;
        }

        try
        {
            if (IsDetailLineMode)
            {
                PreviewCount = _circleService.GetPreviewCount(_doc, _activePlanView, SelectedLink.Instance, SelectedFamily);
            }
            else
            {
                // For floor mode, count without checking existing floors (performance)
                var elements = _circleService.GetPreviewElements(_doc, _activePlanView, SelectedLink.Instance, SelectedFamily);
                PreviewCount = elements.Count;
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error getting preview count: {ex.Message}");
            PreviewCount = 0;
        }
    }

    private void ExecuteHighlight()
    {
        ClearHighlight();

        if (SelectedLink?.Instance == null || string.IsNullOrEmpty(SelectedFamily))
            return;

        try
        {
            var elements = _circleService.GetPreviewElements(_doc, _activePlanView, SelectedLink.Instance, SelectedFamily);
            _currentlyHighlightedElements.AddRange(elements);

            if (elements.Any())
            {
                _uiDoc.Selection.SetElementIds(elements.Select(e => e.Id).ToList());
                AddLogMessage($"Highlighted {elements.Count} elements in the active view.");
            }
            else
            {
                AddLogMessage("No elements found to highlight.");
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error highlighting elements: {ex.Message}");
        }
    }

    private void ClearHighlight()
    {
        _uiDoc.Selection.SetElementIds(new List<ElementId>());
        _currentlyHighlightedElements.Clear();
    }

    private bool CanExecuteHighlight()
    {
        return !IsProcessing && SelectedLink != null && !string.IsNullOrEmpty(SelectedFamily) && PreviewCount > 0;
    }

    private void ExecuteProcess()
    {
        if (SelectedLink?.Instance == null || string.IsNullOrEmpty(SelectedFamily))
            return;

        IsProcessing = true;
        ClearLog();

        AddLogMessage("=== Operation Started ===");
        AddLogMessage($"Mode: {(IsDetailLineMode ? "Detail Lines" : "Floors")}");
        AddLogMessage($"Selected Link: {SelectedLink.Name}");
        AddLogMessage($"Selected Family: {SelectedFamily}");
        AddLogMessage($"Radius: {RadiusMm} mm");

        try
        {
            if (IsDetailLineMode)
            {
                var revitColor = new RevitColor(SelectedColor.R, SelectedColor.G, SelectedColor.B);
                var summary = _circleService.CreateDetailLines(
                    _doc,
                    _activePlanView,
                    SelectedLink.Instance,
                    SelectedFamily,
                    RadiusMm,
                    revitColor);
                AddLogMessage("");
                AddLogMessage("=== Operation Complete ===");
                AddLogMessage(summary.ToDisplayText());
            }
            else
            {
                if (SelectedFloorType == null)
                {
                    AddLogMessage("ERROR: No floor type selected.");
                    return;
                }

                if (_activePlanView.GenLevel == null)
                {
                    AddLogMessage("ERROR: Active view has no associated level.");
                    return;
                }

                var summary = _floorService.CreateFloors(
                    _doc,
                    _activePlanView,
                    SelectedLink.Instance,
                    SelectedFamily,
                    RadiusMm,
                    SelectedFloorType,
                    FloorOffsetMm);
                AddLogMessage("");
                AddLogMessage("=== Operation Complete ===");
                AddLogMessage(summary.ToDisplayText());
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"ERROR: {ex.Message}");
            AddLogMessage($"Stack Trace: {ex.StackTrace}");
        }
        finally
        {
            IsProcessing = false;
            ClearHighlight();
            UpdatePreviewCount();
        }
    }

    private bool CanExecuteProcess()
    {
        if (!IsValidView || IsProcessing || SelectedLink == null || string.IsNullOrEmpty(SelectedFamily) || PreviewCount <= 0 || RadiusMm <= 0)
            return false;

        if (IsDetailLineMode)
        {
            return true;
        }
        else
        {
            return SelectedFloorType != null && _activePlanView.GenLevel != null && !ShowNoFloorTypeWarning;
        }
    }

    private void UpdateCanProcess()
    {
        CanProcess = CanExecuteProcess();
    }

    private void ExecuteCancel()
    {
        ClearHighlight();
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this)?.Close();
    }

    private void ExecuteCopyLog()
    {
        if (LogMessages.Count > 0)
        {
            var logText = string.Join(Environment.NewLine, LogMessages);
            try
            {
                Clipboard.SetText(logText);
                AddLogMessage("Log copied to clipboard.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"Failed to copy log: {ex.Message}");
            }
        }
    }

    private void AddLogMessage(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogMessages.Add(message);
        });
    }

    private void ClearLog()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogMessages.Clear();
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        _circleService.OnLogMessage -= AddLogMessage;
        _floorService.OnLogMessage -= AddLogMessage;
        ClearHighlight();
    }
}

public class LinkInfo
{
    public ElementId Id { get; set; }
    public string Name { get; set; }
    public RevitLinkInstance Instance { get; set; }

    public override string ToString() => Name;
}

public class FloorFamilyInfo
{
    public string Name { get; set; }
    public List<FloorType> FloorTypes { get; set; }

    public override string ToString() => Name;
}

public class ColorOption
{
    public string Name { get; set; }
    public System.Windows.Media.Color Color { get; set; }

    public override string ToString() => Name;
}