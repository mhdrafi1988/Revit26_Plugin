using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Engine;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.Infrastructure.ExternalEvents;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.UI.ViewModels
{
    /// <summary>
    /// PHASE 4 SCOPE (feature-complete for V1's three representation groups): Live
    /// Revit data for Linked Models and all three Element Tree branches — Profile
    /// (Floor/Roof), Linear (Wall/Structural Framing), and Point (Structural/
    /// Architectural Columns, Mechanical Equipment). "Create Detail Lines" runs the
    /// real ProfileProcessingEngine, LinearProcessingEngine, and PointProcessingEngine
    /// via CreateDetailLinesEventHandler / ExternalEvent for enabled mappings in any
    /// group. Mechanical Equipment elements are classified per-element (Point vs
    /// Curve location) at processing time, not assumed from category, per spec
    /// Section 21.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly SettingsService _settingsService = new();
        private readonly LogExportService _logExportService = new();
        private readonly LinkService _linkService = new();
        private readonly ViewBoundaryService _viewBoundaryService = new();
        private readonly DetailLineStyleService _lineStyleService = new();
        private readonly GraphicsOverrideService _graphicsOverrideService = new();

        private readonly CreateDetailLinesEventHandler _eventHandler = new();
        private readonly ExternalEvent _externalEvent;

        /// <summary>Set by MainWindow's constructor (SetOwnerWindow) — hidden/shown
        /// around PickAxisLine's Selection.PickObject call, same pattern as
        /// RoofRidgeViewModel's SelectRoof across the suite.</summary>
        private Window? _ownerWindow;

        public void SetOwnerWindow(Window window) => _ownerWindow = window;

        // ── Header / Settings state ──────────────────────────────────────
        [ObservableProperty]

        private bool _settingsLoaded;

        // ── Run state (drives gray-out/inactive UI) ──────────────────────
        [ObservableProperty]
        private ToolRunState _runState = ToolRunState.Idle;

        /// <summary>True while RunState == Running — used to disable the Run button
        /// during the operation per suite convention.</summary>
        public bool IsRunning => RunState == ToolRunState.Running;

        /// <summary>True once RunState == Complete — bound to the form's IsEnabled
        /// (inverted) to produce the gray/inactive post-run state.</summary>
        public bool IsFormLocked => RunState == ToolRunState.Complete;

        // ── Metrics (Section: top metrics row) ────────────────────────────
        [ObservableProperty]
        private int _elementsFound;

        [ObservableProperty]
        private int _elementsProcessed;

        [ObservableProperty]
        private int _detailLinesCreated;

        [ObservableProperty]
        private int _elementsSkipped;

        [ObservableProperty]
        private int _criticalErrors;

        // ── Section 1: Linked Models & Instances (expander, default expanded) ──
        [ObservableProperty]
        private bool _isLinkedModelsExpanded = true;

        public ObservableCollection<LinkedModelItem> LinkedModels { get; } = new();

        public int SelectedLinkCount => LinkedModels.Count(l => l.IsSelected);

        // ── Section 2: Element Selection (Phase 5: three always-visible
        //    per-group expanders — Profile / Linear / Point — replacing the old
        //    single-tree-plus-tab-switcher layout, which never actually filtered
        //    the tree by tab; see LinkTreeNode.*Categories) ─────────────────
        [ObservableProperty]
        private bool _isProfileSelectionExpanded = true;

        [ObservableProperty]
        private bool _isLinearSelectionExpanded = true;

        [ObservableProperty]
        private bool _isPointSelectionExpanded = true;

        public ObservableCollection<LinkTreeNode> ElementTree { get; } = new();

        [ObservableProperty]
        private string _treeSearchText = string.Empty;

        // ── Section 3: Mapping Grid ─────────────────────────────────────
        public ObservableCollection<ElementMapping> Mappings { get; } = new();

        /// <summary>Grouped view of Mappings, grouped by RepresentationGroup, for the
        /// expander-per-group mapping grid display. XAML binds to this instead of
        /// Mappings directly so ItemsControl.GroupStyle has a live grouping to render.</summary>
        public ICollectionView MappingsGrouped { get; }

        /// <summary>Grand total badge shown at the bottom of the Mapping card —
        /// sum of all mapping rows across all groups. (Flagged assumption: confirm
        /// this is the intended meaning vs. total Detail Lines, which is only known
        /// post-run and already surfaced via DetailLinesCreated above.)</summary>
        public int TotalMappingCount => Mappings.Count;

        [ObservableProperty]
        private string _mappingFilterText = string.Empty;

        /// <summary>Per-type Detail Line Style options for the Mapping Grid's style
        /// selector — host project's Lines subcategories, populated in LoadLiveData.
        /// Each ElementMapping row picks independently via its own DetailLineStyleName,
        /// so different Types can be assigned different line types.</summary>
        public ObservableCollection<string> AvailableLineStyleNames { get; } = new();

        /// <summary>Per-type color override options for the Mapping Grid's color
        /// selector. "None" (no override) plus the fixed named palette from
        /// GraphicsOverrideService. Each ElementMapping row picks independently via
        /// its own ColorName, so different Types can be assigned different colors.</summary>
        public ObservableCollection<string> AvailableColorNames { get; } = new();

        /// <summary>Mapping Grid master override (Section 3): when enabled, every
        /// enabled mapping's Detail Lines are created with THIS Line Style/Color
        /// instead of its own row's selection — applied at generation time only
        /// (CreateDetailLinesEventHandler), never overwriting the per-row values
        /// themselves. Per-row Line Style/Color combos are disabled in the UI while
        /// this is on, so it's visually clear they're not taking effect.</summary>
        public GlobalOverrideSettings GlobalOverride { get; } = new();

        // ── Section 4a/4b: Point Marker settings ───────────────────────
        public CircleMarkerSettings CircleMarker { get; } = new();
        public RectangleMarkerSettings RectangleMarker { get; } = new();

        // ── Section 5: Processing Scope ─────────────────────────────────
        public ProcessingScope ProcessingScope { get; } = new();

        // ── Section 6: Complex Curve Handling ───────────────────────────
        public ComplexCurveSettings ComplexCurve { get; } = new();

        // ── Log panel ────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        // ── Current view / scope info line ──────────────────────────────
        [ObservableProperty]
        private string _currentViewName = string.Empty;

        public MainViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;

            // ExternalEvent.Create() called here — MainViewModel is constructed from
            // MainWindow's constructor, which runs in the active API context triggered
            // by the PushButton command (per suite convention: never lazily inside
            // Execute()).
            _externalEvent = ExternalEvent.Create(_eventHandler);
            _eventHandler.OnComplete = OnProcessingComplete;

            MappingsGrouped = CollectionViewSource.GetDefaultView(Mappings);
            MappingsGrouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ElementMapping.Group)));

            LoadSettings();
            LoadLiveData();

            Mappings.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TotalMappingCount));
            LinkedModels.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SelectedLinkCount));

            RunState = ToolRunState.Configuring;
        }

        // ── Settings load/save ──────────────────────────────────────────

        private void LoadSettings()
        {
            var settings = _settingsService.Load(msg => AddLog(LogLevel.Info, msg));

            CircleMarker.DiameterMm = settings.CircleMarker.DiameterMm;

            RectangleMarker.WidthMm = settings.RectangleMarker.WidthMm;
            RectangleMarker.HeightMm = settings.RectangleMarker.HeightMm;
            RectangleMarker.AlignmentMode = Enum.TryParse<RectangleAlignmentMode>(
                settings.RectangleMarker.AlignmentMode, out var alignment) ? alignment : RectangleAlignmentMode.InstanceRotation;
            RectangleMarker.ManualAngleDegrees = settings.RectangleMarker.ManualAngleDegrees;

            ProcessingScope.LimitToActiveView = settings.ProcessingScope.LimitToActiveView;
            ProcessingScope.TrimToBoundary = settings.ProcessingScope.TrimToBoundary;
            ProcessingScope.OuterLoopClosing.CloseOpenLoops = settings.ProcessingScope.OuterLoopClosing.CloseOpenLoops;
            ProcessingScope.OuterLoopClosing.CapOpenEnds = settings.ProcessingScope.OuterLoopClosing.CapOpenEnds;
            ProcessingScope.InnerLoopClosing.CloseOpenLoops = settings.ProcessingScope.InnerLoopClosing.CloseOpenLoops;
            ProcessingScope.InnerLoopClosing.CapOpenEnds = settings.ProcessingScope.InnerLoopClosing.CapOpenEnds;
            ProcessingScope.RemoveEngulfedOnly = settings.ProcessingScope.RemoveEngulfedOnly;
            ProcessingScope.MergePartialOverlaps = settings.ProcessingScope.MergePartialOverlaps;
            ProcessingScope.JoinCollinearLines = settings.ProcessingScope.JoinCollinearLines;
            ProcessingScope.LineJoinToleranceMm = settings.ProcessingScope.LineJoinToleranceMm;

            ComplexCurve.ReplaceWithFallback = settings.ComplexCurve.ReplaceWithFallback;
            ComplexCurve.FallbackShape = Enum.TryParse<SplineFallbackShape>(
                settings.ComplexCurve.FallbackShape, out var shape) ? shape : SplineFallbackShape.StraightChord;

            GlobalOverride.IsEnabled = settings.GlobalOverride.IsEnabled;
            GlobalOverride.LineStyleName = settings.GlobalOverride.LineStyleName;
            GlobalOverride.ColorName = settings.GlobalOverride.ColorName;

            SettingsLoaded = true;
        }

        private ToolSettings BuildSettingsSnapshot() => new()
        {
            CircleMarker = new CircleMarkerSettingsDto
            {
                DiameterMm = CircleMarker.DiameterMm
            },
            RectangleMarker = new RectangleMarkerSettingsDto
            {
                WidthMm = RectangleMarker.WidthMm,
                HeightMm = RectangleMarker.HeightMm,
                AlignmentMode = RectangleMarker.AlignmentMode.ToString(),
                ManualAngleDegrees = RectangleMarker.ManualAngleDegrees
            },
            ProcessingScope = new ProcessingScopeDto
            {
                LimitToActiveView = ProcessingScope.LimitToActiveView,
                TrimToBoundary = ProcessingScope.TrimToBoundary,
                OuterLoopClosing = new LoopClosingSettingsDto
                {
                    CloseOpenLoops = ProcessingScope.OuterLoopClosing.CloseOpenLoops,
                    CapOpenEnds = ProcessingScope.OuterLoopClosing.CapOpenEnds
                },
                InnerLoopClosing = new LoopClosingSettingsDto
                {
                    CloseOpenLoops = ProcessingScope.InnerLoopClosing.CloseOpenLoops,
                    CapOpenEnds = ProcessingScope.InnerLoopClosing.CapOpenEnds
                },
                RemoveEngulfedOnly = ProcessingScope.RemoveEngulfedOnly,
                MergePartialOverlaps = ProcessingScope.MergePartialOverlaps,
                JoinCollinearLines = ProcessingScope.JoinCollinearLines,
                LineJoinToleranceMm = ProcessingScope.LineJoinToleranceMm
            },
            ComplexCurve = new ComplexCurveSettingsDto
            {
                ReplaceWithFallback = ComplexCurve.ReplaceWithFallback,
                FallbackShape = ComplexCurve.FallbackShape.ToString()
            },
            GlobalOverride = new GlobalOverrideSettingsDto
            {
                IsEnabled = GlobalOverride.IsEnabled,
                LineStyleName = GlobalOverride.LineStyleName,
                ColorName = GlobalOverride.ColorName
            }
        };

        [RelayCommand]
        private void SaveSettings()
        {
            _settingsService.Save(BuildSettingsSnapshot(), msg => AddLog(LogLevel.Info, msg));
        }

        /// <summary>"Pick Line" alignment option (4B): lets the user define the
        /// Rectangle marker's rotation axis by picking an existing Detail Line in
        /// the host view, instead of typing a Manual angle or relying on one of the
        /// automatic modes.
        ///   1. Hide this window so it doesn't block the view.
        ///   2. Single-selection PickObject restricted to Detail Lines (DetailLineSelectionFilter).
        ///   3. Show this window again (finally — runs on cancel/error too).
        ///   4. Extract the line's direction as an angle and feed it into the SAME
        ///      alignment logic Manual mode already uses — ManualAngleDegrees is the
        ///      one place PointProcessingEngine reads a fixed angle from, so setting
        ///      AlignmentMode to Manual here reuses that path rather than adding a
        ///      parallel one.</summary>
        [RelayCommand]
        private void PickAxisLine()
        {
            UIDocument uiDoc = _uiApp.ActiveUIDocument;
            Document hostDoc = uiDoc.Document;

            try
            {
                _ownerWindow?.Hide();

                Reference pickedRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new DetailLineSelectionFilter(),
                    "Select a Detail Line to define the Rectangle marker's alignment axis");

                if (hostDoc.GetElement(pickedRef) is not DetailLine detailLine)
                {
                    AddLog(LogLevel.Warning, "Pick Line: selected element was not a Detail Line — alignment unchanged.");
                    return;
                }

                Curve curve = detailLine.GeometryCurve;
                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                XYZ direction = (end - start).Normalize();

                double angleDegrees = Math.Atan2(direction.Y, direction.X) * 180.0 / Math.PI;

                RectangleMarker.ManualAngleDegrees = angleDegrees;
                RectangleMarker.AlignmentMode = RectangleAlignmentMode.Manual;

                string curveNote = curve is Line ? string.Empty : " (non-straight geometry — used its start→end chord direction)";
                AddLog(LogLevel.Info, $"Pick Line: axis set to {angleDegrees:F2}° from Detail Line {detailLine.Id.Value}{curveNote} — Rectangle alignment switched to Manual.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                AddLog(LogLevel.Info, "Pick Line: selection cancelled.");
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Pick Line: failed — {ex.Message}");
            }
            finally
            {
                _ownerWindow?.Show();
            }
        }

        /// <summary>Snapshots the live ProcessingScope for a run request. A plain
        /// object initializer can't set OuterLoopClosing/InnerLoopClosing (get-only
        /// properties, each holding its own mutual-exclusivity wiring), so those are
        /// copied field-by-field after construction instead.</summary>
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

        // ── Logging helper ──────────────────────────────────────────────

        private void AddLog(LogLevel level, string message)
        {
            LogEntries.Add(new LogEntry(level, message));
        }

        [RelayCommand]
        private void ClearLog() => LogEntries.Clear();

        // ── Section 3: Mapping grid commands ────────────────────────────

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

        // ── Footer commands ──────────────────────────────────────────────

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
            ElementsSkipped = 0;
            CriticalErrors = 0;

            View activeView = _uiApp.ActiveUIDocument.ActiveView;
            var (boundary, isExact) = _viewBoundaryService.GetProcessingBoundary(activeView, msg => AddLog(LogLevel.Info, msg));

            if (!ProcessingScope.LimitToActiveView)
            {
                AddLog(LogLevel.Info, "'Limit to active view' is off — processing boundary still computed from crop/view extent per performance requirement (Section 32); this toggle governs candidate pre-filtering scope, not boundary shape.");
            }

            // Assign Detail Line Style default if a mapping has none set yet — falls
            // back to the first available host project line style, logged so it's
            // visible rather than silently applied.
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
                CircleMarkerSettings = new CircleMarkerSettings
                {
                    DiameterMm = CircleMarker.DiameterMm
                },
                RectangleMarkerSettings = new RectangleMarkerSettings
                {
                    WidthMm = RectangleMarker.WidthMm,
                    HeightMm = RectangleMarker.HeightMm,
                    AlignmentMode = RectangleMarker.AlignmentMode,
                    ManualAngleDegrees = RectangleMarker.ManualAngleDegrees
                },
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

        private static LogLevel MapSeverity(Core.Engine.LogSeverity sev) => sev switch
        {
            Core.Engine.LogSeverity.Warning => LogLevel.Warning,
            Core.Engine.LogSeverity.Error => LogLevel.Error,
            Core.Engine.LogSeverity.Success => LogLevel.Success,
            Core.Engine.LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Info
        };

        private bool CanRun() => RunState == ToolRunState.Configuring && Mappings.Any(m => m.IsEnabled);

        [RelayCommand]
        private void ResetSession()
        {
            RunState = ToolRunState.Configuring;
            ElementsFound = 0;
            ElementsProcessed = 0;
            DetailLinesCreated = 0;
            ElementsSkipped = 0;
            CriticalErrors = 0;
            AddLog(LogLevel.Info, "New session started");
        }

        partial void OnRunStateChanged(ToolRunState value)
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsFormLocked));
            CreateDetailLinesCommand.NotifyCanExecuteChanged();
            ClearCommand.NotifyCanExecuteChanged();
        }

        // ── Live data (Phase 2): real LinkService queries, reactive tree→mapping ──

        private void LoadLiveData()
        {
            Document hostDoc = _uiApp.ActiveUIDocument.Document;
            View activeView = _uiApp.ActiveUIDocument.ActiveView;
            CurrentViewName = activeView.Name;

            AvailableLineStyleNames.Clear();
            foreach (var style in _lineStyleService.GetAvailableLineStyles(hostDoc))
                AvailableLineStyleNames.Add(style.Name);

            AvailableColorNames.Clear();
            AvailableColorNames.Add("None");
            foreach (var colorName in _graphicsOverrideService.AvailableColorNames)
                AvailableColorNames.Add(colorName);

            var links = _linkService.GetLinkedModels(hostDoc, msg => AddLog(LogLevel.Info, msg));
            foreach (var link in links)
            {
                link.PropertyChanged += OnLinkSelectionChanged;
                LinkedModels.Add(link);
            }

            // Auto-select loaded links so the tree has something to show on open
            // (matches the mockup's default state); user can uncheck freely.
            foreach (var link in LinkedModels.Where(l => l.IsLoaded))
                link.IsSelected = true;

            RebuildElementTree();
        }

        private void OnLinkSelectionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LinkedModelItem.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedLinkCount));
                RebuildElementTree();
            }
        }

        private void RebuildElementTree()
        {
            Document hostDoc = _uiApp.ActiveUIDocument.Document;
            var selectedIds = LinkedModels.Where(l => l.IsSelected).Select(l => l.LinkInstanceId);

            // Detach old TypeTreeItem handlers before rebuilding to avoid leaking
            // subscriptions across tree rebuilds.
            foreach (var node in ElementTree)
                foreach (var cat in node.Categories)
                    foreach (var fam in cat.Families)
                        foreach (var t in fam.Types)
                            t.PropertyChanged -= OnTypeCheckedChanged;

            ElementTree.Clear();

            var nodes = _linkService.BuildElementTree(hostDoc, selectedIds, msg => AddLog(LogLevel.Info, msg));
            foreach (var node in nodes)
            {
                foreach (var cat in node.Categories)
                    foreach (var fam in cat.Families)
                        foreach (var t in fam.Types)
                        {
                            // Reflect current mapping state if a mapping already exists
                            // for this type (e.g. after a link is unchecked and rechecked).
                            t.IsChecked = Mappings.Any(m => m.LinkInstanceId == node.LinkInstanceId && m.TypeId == t.TypeId);
                            t.PropertyChanged += OnTypeCheckedChanged;
                        }

                ElementTree.Add(node);
            }

            // Remove mappings whose link is no longer selected (link unchecked).
            var toRemove = Mappings.Where(m => !selectedIds.Contains(m.LinkInstanceId)).ToList();
            foreach (var m in toRemove) Mappings.Remove(m);

            AddLog(LogLevel.Info, $"{LinkedModels.Count(l => l.IsSelected)} linked instance(s) selected — element tree loaded ({nodes.Sum(n => n.Categories.Sum(c => c.Families.Sum(f => f.Types.Count)))} type(s) available).");
        }

        /// <summary>Checking/unchecking a Type in Section 2's tree adds/removes the
        /// corresponding Mapping Grid row, per spec Section 7: "Only explicitly
        /// selected Types should be added to the Mapping Grid."</summary>
        private void OnTypeCheckedChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TypeTreeItem.IsChecked)) return;
            if (sender is not TypeTreeItem typeItem) return;

            // Find which link/category/family this type belongs to by walking the tree.
            foreach (var node in ElementTree)
                foreach (var cat in node.Categories)
                    foreach (var fam in cat.Families)
                        if (fam.Types.Contains(typeItem))
                        {
                            if (typeItem.IsChecked)
                                AddMappingIfMissing(node, cat, fam, typeItem);
                            else
                                RemoveMapping(node.LinkInstanceId, typeItem.TypeId);
                            return;
                        }
        }

        /// <summary>Checks every Type under the given Category (all its Families) —
        /// each toggle cascades through OnTypeCheckedChanged exactly as a manual click
        /// would, so mappings are created the same way.</summary>
        [RelayCommand]
        private void SelectAllInCategory(CategoryTreeItem category)
        {
            foreach (var fam in category.Families)
                foreach (var t in fam.Types)
                    t.IsChecked = true;
        }

        /// <summary>Unchecks every Type under the given Category, removing their
        /// mappings via the same cascade OnTypeCheckedChanged already handles.</summary>
        [RelayCommand]
        private void SelectNoneInCategory(CategoryTreeItem category)
        {
            foreach (var fam in category.Families)
                foreach (var t in fam.Types)
                    t.IsChecked = false;
        }

        private void AddMappingIfMissing(LinkTreeNode node, CategoryTreeItem cat, FamilyTreeItem fam, TypeTreeItem typeItem)
        {
            if (Mappings.Any(m => m.LinkInstanceId == node.LinkInstanceId && m.TypeId == typeItem.TypeId))
                return;

            var defaultRepresentation = cat.Group switch
            {
                RepresentationGroup.Profile => RepresentationMode.Boundary,
                RepresentationGroup.Linear => RepresentationMode.Centerline,
                RepresentationGroup.Point => RepresentationMode.Circle,
                _ => RepresentationMode.Boundary
            };

            var mapping = new ElementMapping
            {
                LinkInstanceId = node.LinkInstanceId,
                LinkDisplayName = node.LinkDisplayName,
                CategoryName = cat.CategoryName.TrimEnd('s'), // "Floors" -> "Floor" to match existing display convention
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

        // ── Real processing (Phase 2: Profile group only) ──────────────────

        private void OnProcessingComplete(ProcessingResult result)
        {
            ElementsFound = result.ElementsFound;
            ElementsProcessed = result.ElementsProcessed;
            DetailLinesCreated = result.DetailLinesCreated;
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

