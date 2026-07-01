using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.RoofTag_V006
{
    public partial class RoofTagViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly Document _doc;

        // ── Tag Mode ────────────────────────────────────────────────────
        [ObservableProperty]
        private bool _useManualMode = false;

        // ── Angle ────────────────────────────────────────────────────────
        [ObservableProperty]
        private bool _isAngle45 = true;

        [ObservableProperty]
        private bool _isAngle30 = false;

        public double SelectedAngle => IsAngle45 ? 45.0 : 30.0;

        partial void OnIsAngle45Changed(bool value)
        {
            if (value) IsAngle30 = false;
            OnPropertyChanged(nameof(SelectedAngle));
        }

        partial void OnIsAngle30Changed(bool value)
        {
            if (value) IsAngle45 = false;
            OnPropertyChanged(nameof(SelectedAngle));
        }

        // ── Bend Direction ───────────────────────────────────────────────
        [ObservableProperty]
        private bool _bendInward = true;

        [ObservableProperty]
        private bool _bendOutward = false;

        partial void OnBendInwardChanged(bool value)
        {
            if (value) BendOutward = false;
        }

        partial void OnBendOutwardChanged(bool value)
        {
            if (value) BendInward = false;
        }

        // ── Offsets (mm) ─────────────────────────────────────────────────
        [ObservableProperty]
        private double _bendOffset = 1000.0;

        [ObservableProperty]
        private double _endOffset = 2000.0;

        // ── Leader ───────────────────────────────────────────────────────
        [ObservableProperty]
        private bool _useLeader = true;

        // ── Tag Types ────────────────────────────────────────────────────
        public ObservableCollection<SpotTagTypeWrapper> SpotTagTypes { get; } = new();

        [ObservableProperty]
        private SpotTagTypeWrapper _selectedSpotTagType;

        // ── Log ──────────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        [ObservableProperty]
        private int _placedCount;

        [ObservableProperty]
        private int _fallbackCount;

        [ObservableProperty]
        private int _failedCount;

        // ── Constructor ──────────────────────────────────────────────────
        public RoofTagViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _doc   = uiApp.ActiveUIDocument.Document;
            LoadTagTypes();
        }

        // ── Commands ─────────────────────────────────────────────────────
        [RelayCommand]
        private void CopyLog()
        {
            if (LogEntries.Count == 0) return;
            var text = string.Join(System.Environment.NewLine,
                LogEntries.Select(e => e.ToString()));
            Clipboard.SetText(text);
        }

        // ── Helpers ──────────────────────────────────────────────────────
        private void LoadTagTypes()
        {
            var types = new FilteredElementCollector(_doc)
                .OfClass(typeof(SpotDimensionType))
                .Cast<SpotDimensionType>()
                .Select(t => new SpotTagTypeWrapper(t));

            foreach (var t in types)
                SpotTagTypes.Add(t);

            SelectedSpotTagType = SpotTagTypes.FirstOrDefault();
        }

        public void AddLog(LogEntry entry)
        {
            LogEntries.Add(entry);

            if (entry.Level == LogLevel.Success) PlacedCount++;
            else if (entry.Level == LogLevel.Warning) FallbackCount++;
            else if (entry.Level == LogLevel.Error) FailedCount++;
        }

        public void ResetCounters()
        {
            PlacedCount   = 0;
            FallbackCount = 0;
            FailedCount   = 0;
            LogEntries.Clear();
        }
    }

    // ── Tag Type Wrapper ─────────────────────────────────────────────────
    public class SpotTagTypeWrapper
    {
        public SpotDimensionType TagType { get; }
        public string Name => TagType.Name;

        public SpotTagTypeWrapper(SpotDimensionType type)
        {
            TagType = type;
        }

        public override string ToString() => Name;
    }
}
