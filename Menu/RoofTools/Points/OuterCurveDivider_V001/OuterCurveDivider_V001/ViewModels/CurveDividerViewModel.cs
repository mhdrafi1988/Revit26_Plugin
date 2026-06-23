using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.OuterCurveDivider.V001.Models;
using Revit26_Plugin.OuterCurveDivider.V001.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Revit26_Plugin.OuterCurveDivider.V001.ViewModels
{
    public class CurveDividerViewModel : ObservableObject
    {
        private readonly Document             _doc;
        private readonly RoofBase             _roof;
        private readonly CurveDivisionService _service;

        public ObservableCollection<EdgeTypeSetting> TypeSettings { get; } = new ObservableCollection<EdgeTypeSetting>();
        public ObservableCollection<CurveEdgeModel>  Edges        { get; } = new ObservableCollection<CurveEdgeModel>();

        /// <summary>Shared log rows (timestamp + level + message), colored by LogLevelToColorConverter.</summary>
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        public string[] ModeOptions { get; } = { "Distance", "Count" };

        public IRelayCommand ApplyCommand      { get; }
        public IRelayCommand SelectAllCommand  { get; }
        public IRelayCommand SelectNoneCommand { get; }

        public CurveDividerViewModel(Document doc, RoofBase roof)
        {
            _doc     = doc;
            _roof    = roof;
            _service = new CurveDivisionService();

            ApplyCommand      = new RelayCommand(ApplyDivisions);
            SelectAllCommand  = new RelayCommand(() => SetAllSelected(true));
            SelectNoneCommand = new RelayCommand(() => SetAllSelected(false));

            LoadEdges();
        }

        private void LoadEdges()
        {
            Edges.Clear();
            TypeSettings.Clear();

            List<CurveEdgeModel> found;
            int filteredLines;
            try { found = _service.ExtractNonLinearEdges(_roof, out filteredLines); }
            catch (Exception ex) { Log(LogLevel.Error, $"Edge extraction failed: {ex.Message}"); return; }

            var settingByType = new Dictionary<string, EdgeTypeSetting>();
            foreach (var typeName in found.Select(e => e.CurveTypeName).Distinct())
            {
                var setting = new EdgeTypeSetting { TypeName = typeName };
                setting.PropertyChanged += OnTypeSettingChanged;
                settingByType[typeName] = setting;
                TypeSettings.Add(setting);
            }

            // Sort by arc length, ascending
            var sorted = found.OrderBy(e => e.LengthM).ToList();
            foreach (var edge in sorted)
            {
                edge.TypeSetting = settingByType[edge.CurveTypeName];
                Edges.Add(edge);
            }

            Log(LogLevel.Info, $"Roof loaded — {Edges.Count} non-linear edge(s) across {TypeSettings.Count} type(s).");
            Log(LogLevel.Info, "Per-edge point counts seeded from edge length (4 / 8 / 12 / L÷2).");
            if (filteredLines > 0) Log(LogLevel.Info, $"{filteredLines} straight Line edge(s) filtered out.");
            if (Edges.Count == 0)  Log(LogLevel.Warning, "No curved edges on this roof's top face.");
        }

        private void OnTypeSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is EdgeTypeSetting setting)) return;

            foreach (var edge in Edges.Where(x => x.TypeSetting == setting && !x.HasOverride))
            {
                bool bulkCount = e.PropertyName == nameof(EdgeTypeSetting.FixedCount)
                                 && setting.Mode == DivisionMode.ByCount
                                 && !edge.IsManual;
                if (bulkCount) edge.PointCount = setting.FixedCount;

                edge.NotifyInheritedRuleChanged();
            }
        }

        private void SetAllSelected(bool selected)
        {
            foreach (var edge in Edges) edge.IsSelected = selected;
        }

        private void ApplyDivisions()
        {
            var selected = Edges.Where(e => e.IsSelected).ToList();
            if (!selected.Any()) { Log(LogLevel.Warning, "Apply skipped — no edges selected."); return; }

            int manual    = selected.Count(e => e.IsCountDriven && e.IsManual);
            int overrides  = selected.Count(e => e.HasOverride);
            int totalPts   = selected.Sum(e => e.FinalPointCount);
            Log(LogLevel.Info, $"Applying to {selected.Count} edge(s): {totalPts} point(s) ({overrides} override, {manual} manual)...");

            foreach (var entry in _service.ApplyDivisions(_doc, _roof, selected))
                LogEntries.Add(entry);
        }

        private void Log(LogLevel level, string message) => LogEntries.Add(new LogEntry(level, message));
    }
}
