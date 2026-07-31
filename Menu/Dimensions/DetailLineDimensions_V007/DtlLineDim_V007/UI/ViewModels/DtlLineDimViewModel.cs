using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Interop;
using Revit26_Plugin.DtlLineDim.V007.Core.Models;
using Revit26_Plugin.DtlLineDim.V007.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.DtlLineDim.V007.UI.ViewModels
{
    public partial class DtlLineDimViewModel : ObservableObject
    {
        private const string ToolFolderName = "DtlLineDim";

        private readonly ExternalEvent _externalEvent;
        private readonly DtlLineDimExternalEventHandler _handler;
        private readonly DtlLineDimSettings _settings;

        /// <summary>
        /// Owner window for any secondary dialogs raised from this ViewModel
        /// (e.g. the export-folder picker). Set by the View immediately after
        /// construction — see DtlLineDimWindow.xaml.cs.
        /// </summary>
        public System.Windows.Window OwnerWindow { get; set; }

        public ObservableCollection<ComboItem> DetailItemTypes { get; } = new();
        public ObservableCollection<ComboItem> DimensionTypes { get; } = new();
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        [ObservableProperty] private ComboItem selectedDetailItemType;
        [ObservableProperty] private ComboItem selectedDimensionType;

        [ObservableProperty] private string tickSizeText = "50";
        [ObservableProperty] private string offsetText = "300";

        [ObservableProperty] private bool isViewValid;
        [ObservableProperty] private string viewValidationMessage = string.Empty;

        [ObservableProperty] private bool isBusy;

        [ObservableProperty] private string runSummary = "Ready.";

        /// <summary>
        /// Resolved tick size for the run currently in flight — set by
        /// ResolveEffectiveInputs() immediately before raising GenerateDimensions,
        /// so parsing happens exactly once per run rather than being re-evaluated
        /// independently by every consumer.
        /// </summary>
        public double EffectiveTickSizeMm { get; private set; }
        public double EffectiveOffsetMm { get; private set; }

        public DtlLineDimViewModel(UIApplication uiApp)
        {
            _settings = SettingsService<DtlLineDimSettings>.Load(ToolFolderName);
            TickSizeText = _settings.TickSizeMm.ToString(CultureInfo.InvariantCulture);
            OffsetText = _settings.OffsetMm.ToString(CultureInfo.InvariantCulture);

            _handler = new DtlLineDimExternalEventHandler(this);
            _externalEvent = ExternalEvent.Create(_handler);

            RaiseRequest(DtlLineDimRequest.Initialize);
        }

        private bool CanGenerate =>
            !IsBusy &&
            IsViewValid &&
            SelectedDetailItemType != null &&
            SelectedDimensionType != null;

        [RelayCommand(CanExecute = nameof(CanGenerate))]
        private void GenerateDimensions()
        {
            ResolveEffectiveInputs();
            RaiseRequest(DtlLineDimRequest.GenerateDimensions);
        }

        [RelayCommand]
        private void CopyAllLogs()
        {
            string text = string.Join(System.Environment.NewLine, LogEntries.Select(e => e.ToString()));
            System.Windows.Clipboard.SetText(text);
        }

        [RelayCommand]
        private void ClearLogs() => LogEntries.Clear();

        [RelayCommand]
        private void ExportLogs()
        {
            string folder = PromptForFolder(_settings.LastExportFolder, OwnerWindow);
            if (string.IsNullOrWhiteSpace(folder))
                return;

            _settings.LastExportFolder = folder;
            LogExportService.Export(ToolFolderName, LogEntries, folder);
            Log(LogLevel.Info, $"Log exported to {folder}");
        }

        /// <summary>
        /// Minimal folder-path prompt with no external dependency (no
        /// System.Windows.Forms reference required). Pre-fills the last-used
        /// folder. Owner-parented to the tool window (ownerWindow) rather than
        /// left unset — fixed in V007; previously the dialog had no owner and
        /// could appear behind Revit / in the taskbar. ASSUMPTION FLAGGED:
        /// replace with a native folder-browser dialog (e.g.
        /// System.Windows.Forms.FolderBrowserDialog) if/when UseWindowsForms is
        /// confirmed enabled in the .csproj — see chat for context.
        /// </summary>
        private static string PromptForFolder(string lastFolder, System.Windows.Window ownerWindow)
        {
            var dialog = new System.Windows.Window
            {
                Title = "Select Export Folder",
                Width = 480,
                Height = 140,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                SizeToContent = System.Windows.SizeToContent.Manual
            };

            if (ownerWindow != null)
            {
                dialog.Owner = ownerWindow;
            }
            else
            {
                // Fallback: no WPF owner available (should not normally happen —
                // DtlLineDimWindow sets OwnerWindow right after construction).
                // Parent to the Revit main window via interop so the dialog is
                // never ownerless.
                dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            }

            var stack = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(14) };
            var label = new System.Windows.Controls.TextBlock { Text = "Folder path:", Margin = new System.Windows.Thickness(0, 0, 0, 4) };
            var textBox = new System.Windows.Controls.TextBox { Text = lastFolder, Padding = new System.Windows.Thickness(4), MaxLength = 260 };
            var buttonRow = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new System.Windows.Thickness(0, 12, 0, 0)
            };
            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 70, Margin = new System.Windows.Thickness(0, 0, 6, 0), IsDefault = true };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 70, IsCancel = true };

            string result = null;
            okButton.Click += (_, _) => { result = textBox.Text; dialog.Close(); };
            cancelButton.Click += (_, _) => dialog.Close();

            buttonRow.Children.Add(okButton);
            buttonRow.Children.Add(cancelButton);
            stack.Children.Add(label);
            stack.Children.Add(textBox);
            stack.Children.Add(buttonRow);
            dialog.Content = stack;

            dialog.ShowDialog();
            return result;
        }

        private void RaiseRequest(DtlLineDimRequest request)
        {
            IsBusy = true;
            _handler.PendingRequest = request;
            _externalEvent.Raise();
        }

        private void ResolveEffectiveInputs()
        {
            if (TryParsePositiveDouble(TickSizeText, out double tick))
            {
                EffectiveTickSizeMm = tick;
            }
            else
            {
                EffectiveTickSizeMm = _settings.TickSizeMm;
                Log(LogLevel.Info, $"Tick size fallback to default ({_settings.TickSizeMm}mm) — invalid input.");
            }

            if (TryParsePositiveDouble(OffsetText, out double offset))
            {
                EffectiveOffsetMm = offset;
            }
            else
            {
                EffectiveOffsetMm = _settings.OffsetMm;
                Log(LogLevel.Info, $"Offset fallback to default ({_settings.OffsetMm}mm) — invalid input.");
            }
        }

        private static bool TryParsePositiveDouble(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;

            return value > 0;
        }

        // ── Called by the external event handler (same UI thread) ──────────

        public void Log(LogLevel level, string message) => LogEntries.Add(new LogEntry(level, message));

        public void LogError(string message) => Log(LogLevel.Error, message);

        public void ApplyViewValidation(bool valid, string reason)
        {
            IsViewValid = valid;
            ViewValidationMessage = reason;
            GenerateDimensionsCommand.NotifyCanExecuteChanged();
        }

        public void ApplyRunSummary(int created, int failed)
        {
            RunSummary = $"{created} placed | {failed} failed";
            PersistSelections();
        }

        public void SetRunSummary(string text) => RunSummary = text;

        public void RestoreLastSelections()
        {
            if (!string.IsNullOrEmpty(_settings.LastDetailItemTypeName))
                SelectedDetailItemType = DetailItemTypes.FirstOrDefault(i => i.Name == _settings.LastDetailItemTypeName);

            if (!string.IsNullOrEmpty(_settings.LastDimensionTypeName))
                SelectedDimensionType = DimensionTypes.FirstOrDefault(i => i.Name == _settings.LastDimensionTypeName);
        }

        public void PersistSelections()
        {
            // Only overwrite tick/offset if a run has actually resolved them
            // (EffectiveTickSizeMm/EffectiveOffsetMm default to 0 until then) —
            // otherwise closing the window without running would zero out
            // previously saved values.
            if (EffectiveTickSizeMm > 0)
                _settings.TickSizeMm = EffectiveTickSizeMm;
            if (EffectiveOffsetMm > 0)
                _settings.OffsetMm = EffectiveOffsetMm;

            _settings.LastDetailItemTypeName = SelectedDetailItemType?.Name ?? string.Empty;
            _settings.LastDimensionTypeName = SelectedDimensionType?.Name ?? string.Empty;
            SettingsService<DtlLineDimSettings>.Save(ToolFolderName, _settings);
        }

        partial void OnSelectedDetailItemTypeChanged(ComboItem value) =>
            GenerateDimensionsCommand.NotifyCanExecuteChanged();

        partial void OnSelectedDimensionTypeChanged(ComboItem value) =>
            GenerateDimensionsCommand.NotifyCanExecuteChanged();

        partial void OnIsBusyChanged(bool value) =>
            GenerateDimensionsCommand.NotifyCanExecuteChanged();
    }
}
