using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.LinkedDetailLineGenerator.VA010.Core.Engine;
using Revit26_Plugin.LinkedDetailLineGenerator.VA010.Core.Models;
using Revit26_Plugin.LinkedDetailLineGenerator.VA010.Core.Services;
using Revit26_Plugin.LinkedDetailLineGenerator.VA010.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA010.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ── Section 3: Mapping Grid ─────────────────────────────────────────
        public ObservableCollection<ElementMapping> Mappings { get; } = new();
        public ICollectionView MappingsGrouped { get; }
        public int TotalMappingCount => Mappings.Count;

        [ObservableProperty]
        private string _mappingFilterText = string.Empty;

        public ObservableCollection<string> AvailableLineStyleNames { get; } = new();
        public ObservableCollection<string> AvailableColorNames { get; } = new();
        public GlobalOverrideSettings GlobalOverride { get; } = new();

        // ── Mapping grid commands ──────────────────────────────────────────

        [RelayCommand]
        private void SelectAllMappings()
        {
            foreach (var m in Mappings) m.IsEnabled = true;
        }

        [RelayCommand]
        private void ClearAllMappings()
        {
            foreach (var m in Mappings) m.IsEnabled = false;
        }

        [RelayCommand]
        private void RemoveSelectedMappings()
        {
            var toRemove = Mappings.Where(m => !m.IsEnabled).ToList();
            foreach (var m in toRemove) Mappings.Remove(m);
            AddLog(LogLevel.Info, $"Removed {toRemove.Count} mapping(s)");
        }

        // ── Footer commands ────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanClear))]
        private void Clear()
        {
            Mappings.Clear();
            foreach (var link in LinkedModels) link.IsSelected = false;
            AddLog(LogLevel.Info, "Selections cleared");
        }
        private bool CanClear() => RunState != ToolRunState.Complete;

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void CreateDetailLines()
        {
            RunState = ToolRunState.Running;

            var enabledMappings = Mappings.Where(m => m.IsEnabled).ToList();
            AddLog(LogLevel.Info, $"Run started — {enabledMappings.Count} mapping(s) enabled.");

            ElementsFound = 0;
            ElementsProcessed = 0;
            DetailLinesCreated = 0;
            FilledRegionsCreated = 0;
            ElementsSkipped = 0;
            CriticalErrors = 0;

            var (boundary, isExact) = GetActiveProcessingBoundary();

            if (!ProcessingScope.LimitToActiveView)
                AddLog(LogLevel.Info, "'Limit to active view' is off — processing boundary still computed the same way per performance requirement (Section 32); this toggle governs candidate pre-filtering scope, not boundary shape.");

            var availableStyles = _lineStyleService.GetAvailableLineStyles(_uiApp.ActiveUIDocument.Document);
            foreach (var m in enabledMappings.Where(m => string.IsNullOrWhiteSpace(m.DetailLineStyleName)))
            {
                if (availableStyles.Count > 0)
                {
                    m.DetailLineStyleName = availableStyles[0].Name;
                    AddLog(LogLevel.Info, $"Mapping '{m.TypeName}' had no Detail Line Style selected — defaulted to '{m.DetailLineStyleName}'.");
                }
            }

            _eventHandler.PendingRequest = new CreateDetailLinesRequest
            {
                EnabledMappings = enabledMappings,
                ProcessingBoundary = boundary,
                ProcessingScope = BuildProcessingScopeSnapshot(),
                ComplexCurveSettings = new ComplexCurveSettings
                {
                    ReplaceWithFallback = ComplexCurve.ReplaceWithFallback,
                    FallbackShape = ComplexCurve.FallbackShape
                },
                CircleMarkerSettings = new CircleMarkerSettings { DiameterMm = CircleMarker.DiameterMm },
                RectangleMarkerSettings = new RectangleMarkerSettings
                {
                    WidthMm = RectangleMarker.WidthMm,
                    HeightMm = RectangleMarker.HeightMm,
                    AlignmentMode = RectangleMarker.AlignmentMode,
                    ManualAngleDegrees = RectangleMarker.ManualAngleDegrees
                },
                ActualProfileSettings = new ActualProfileSettings { FallbackShape = ActualProfile.FallbackShape },
                GlobalOverride = new GlobalOverrideSettings
                {
                    IsEnabled = GlobalOverride.IsEnabled,
                    LineStyleName = GlobalOverride.LineStyleName,
                    ColorName = GlobalOverride.ColorName
                },
                OnLog = (msg, sev) => AddLog(MapSeverity(sev), msg)
            };

            _externalEvent.Raise();
        }

        private static LogLevel MapSeverity(LogSeverity sev) => sev switch
        {
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Success => LogLevel.Success,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Info
        };

        private bool CanRun() => RunState == ToolRunState.Configuring && Mappings.Any(m => m.IsEnabled);

        private (List<XYZ> boundary, bool isExact) GetActiveProcessingBoundary()
        {
            if (RestrictToBoundary)
                return _elementBoundaryService.GetProcessingBoundary(_selectedBoundaryElement, ComplexCurve, msg => AddLog(LogLevel.Info, msg));

            Autodesk.Revit.DB.View activeView = _uiApp.ActiveUIDocument.ActiveView;
            return _viewBoundaryService.GetProcessingBoundary(activeView, msg => AddLog(LogLevel.Info, msg));
        }

        [RelayCommand]
        private void ResetSession()
        {
            RunState = ToolRunState.Configuring;
            ElementsFound = 0;
            ElementsProcessed = 0;
            DetailLinesCreated = 0;
            FilledRegionsCreated = 0;
            ElementsSkipped = 0;
            CriticalErrors = 0;
            AddLog(LogLevel.Info, "New session started");
        }

        private ProcessingScope BuildProcessingScopeSnapshot()
        {
            var snapshot = new ProcessingScope
            {
                LimitToActiveView = ProcessingScope.LimitToActiveView,
                TrimToBoundary = ProcessingScope.TrimToBoundary,
                RemoveEngulfedOnly = ProcessingScope.RemoveEngulfedOnly,
                MergePartialOverlaps = ProcessingScope.MergePartialOverlaps,
                JoinCollinearLines = ProcessingScope.JoinCollinearLines,
                LineJoinToleranceMm = ProcessingScope.LineJoinToleranceMm
            };
            snapshot.OuterLoopClosing.CloseOpenLoops = ProcessingScope.OuterLoopClosing.CloseOpenLoops;
            snapshot.OuterLoopClosing.CapOpenEnds = ProcessingScope.OuterLoopClosing.CapOpenEnds;
            snapshot.InnerLoopClosing.CloseOpenLoops = ProcessingScope.InnerLoopClosing.CloseOpenLoops;
            snapshot.InnerLoopClosing.CapOpenEnds = ProcessingScope.InnerLoopClosing.CapOpenEnds;
            return snapshot;
        }

        // ── Mapping add/remove helpers ─────────────────────────────────────

        private void AddMappingIfMissing(LinkTreeNode node, CategoryTreeItem cat, FamilyTreeItem fam, TypeTreeItem typeItem)
        {
            if (Mappings.Any(m => m.LinkInstanceId == node.LinkInstanceId && m.TypeId == typeItem.TypeId))
                return;

            bool isColumnCategory = cat.CategoryName is "Structural Columns" or "Columns";
            var defaultRepresentation = cat.Group switch
            {
                RepresentationGroup.Profile => RepresentationMode.Boundary,
                RepresentationGroup.Linear => RepresentationMode.Centerline,
                RepresentationGroup.Point => isColumnCategory ? RepresentationMode.ActualProfile : RepresentationMode.Circle,
                _ => RepresentationMode.Boundary
            };

            var mapping = new ElementMapping
            {
                LinkInstanceId = node.LinkInstanceId,
                LinkDisplayName = node.LinkDisplayName,
                CategoryName = cat.CategoryName.TrimEnd('s'),
                FamilyName = fam.FamilyName,
                TypeName = typeItem.TypeName,
                TypeId = typeItem.TypeId,
                Group = cat.Group,
                Representation = defaultRepresentation,
                DetailLineStyleName = string.Empty,
                ColorName = "None"
            };
            mapping.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ElementMapping.IsEnabled))
                    CreateDetailLinesCommand.NotifyCanExecuteChanged();
            };
            Mappings.Add(mapping);
            CreateDetailLinesCommand.NotifyCanExecuteChanged();
        }

        private void RemoveMapping(long linkInstanceId, long typeId)
        {
            var existing = Mappings.FirstOrDefault(m => m.LinkInstanceId == linkInstanceId && m.TypeId == typeId);
            if (existing != null)
            {
                Mappings.Remove(existing);
                CreateDetailLinesCommand.NotifyCanExecuteChanged();
            }
        }

        // ── Processing complete callback ───────────────────────────────────

        private void OnProcessingComplete(ProcessingResult result)
        {
            ElementsFound = result.ElementsFound;
            ElementsProcessed = result.ElementsProcessed;
            DetailLinesCreated = result.DetailLinesCreated;
            FilledRegionsCreated = result.FilledRegionsCreated;
            ElementsSkipped = result.ElementsSkipped;
            CriticalErrors = result.CriticalErrors;

            foreach (var err in result.Errors)
                AddLog(err.Level, $"Element {err.ElementId} ({err.CategoryName}): {err.Reason}");

            RunState = ToolRunState.Complete;
            SaveSettings();

            bool autoSaved = _logExportService.AutoSave(LogEntries, out var path);
            AddLog(autoSaved ? LogLevel.Success : LogLevel.Warning,
                autoSaved ? $"Log auto-saved to {path}" : "Log auto-save failed");
        }
    }
}
