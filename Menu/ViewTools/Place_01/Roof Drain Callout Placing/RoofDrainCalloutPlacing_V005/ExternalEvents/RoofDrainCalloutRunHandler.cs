using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.Models;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.Services;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V005.ExternalEvents
{
    /// <summary>
    /// All Revit API access for the Run action happens here, triggered via
    /// ExternalEvent.Raise() from the ViewModel's RunCommand. Never called
    /// directly from the UI thread.
    ///
    /// V005: Run only groups the points already picked and places one callout
    /// per group. Roof selection and drain-point picking now happen
    /// synchronously in RoofDrainCalloutPlacingCommand.Execute(), before this
    /// ViewModel/window even exist — this handler does not re-collect or
    /// re-detect points on its own, it only clusters + places.
    /// </summary>
    public class RoofDrainCalloutRunHandler : IExternalEventHandler
    {
        private readonly RoofDrainCalloutPlacingViewModel _viewModel;
        private readonly PointClusteringService _clusterService = new PointClusteringService();

        public RoofDrainCalloutRunHandler(RoofDrainCalloutPlacingViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public string GetName() => "RoofDrainCalloutPlacing Run Handler";

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var log = _viewModel.Logs;

            // New instance per run so the duplicate-callout check's _placedCentroids
            // list always starts empty for this run only (confirmed scope).
            var calloutService = new CalloutPlacementService();

            int roofCount = 0, sourcePointCount = 0, groupCount = 0, placedCount = 0, failedCount = 0;

            try
            {
                var roof = _viewModel.SelectedRoof;
                if (roof == null)
                {
                    log.Add(new LogEntry(LogLevel.Error, "Run failed: no roof selected."));
                    return;
                }

                var draftingViewId = _viewModel.SelectedDraftingView.Id;
                double toleranceFeet = _viewModel.GroupingToleranceMm / 304.8;
                double fixedSizeFeet = _viewModel.CalloutFloorMm / 304.8;
                double duplicateToleranceFeet = _viewModel.DuplicateToleranceMm / 304.8;

                var points = _viewModel.PickedPoints.Select(p => p.Position).ToList();
                sourcePointCount = points.Count;
                roofCount = 1;

                var allGroups = _clusterService.Cluster(roof.Id, points, toleranceFeet);
                groupCount = allGroups.Count;

                using (var group = new TransactionGroup(doc, "Roof Drain Callout Placement"))
                {
                    group.Start();

                    // Callout parent view: resolved via ResolveParentViewId below,
                    // which currently uses Revit's active view at Run time (this
                    // tool has no Plan View dropdown to pin it to otherwise —
                    // see the ASSUMPTION note on that method).
                    var parentViewId = ResolveParentViewId(app, roof, log);

                    using (var t = new Transaction(doc, "Place drain callouts"))
                    {
                        t.Start();
                        t.SetFailureHandlingOptions(
                            t.GetFailureHandlingOptions()
                             .SetFailuresPreprocessor(new RoofDrainFailuresPreprocessor()));

                        foreach (var g in allGroups)
                        {
                            try
                            {
                                calloutService.PlaceReferenceCallout(
                                    doc, parentViewId, draftingViewId, g.Centroid, g.Points, fixedSizeFeet, duplicateToleranceFeet);
                                placedCount++;
                                log.Add(new LogEntry(LogLevel.Success,
                                    $"Cluster of {g.Points.Count} pts -> callout placed"));
                            }
                            catch (CalloutPlacementSkippedException ex)
                            {
                                failedCount++;
                                log.Add(new LogEntry(LogLevel.Warning, ex.Message));
                            }
                            catch (Exception ex)
                            {
                                failedCount++;
                                log.Add(new LogEntry(LogLevel.Warning, $"Callout failed — {ex.Message}"));
                            }
                        }

                        var status = t.Commit();
                        if (status != TransactionStatus.Committed)
                        {
                            log.Add(new LogEntry(LogLevel.Error, $"Callout placement transaction failed ({status})"));
                        }
                    }

                    group.Assimilate();
                }

                log.Add(new LogEntry(LogLevel.Info,
                    $"Done. {sourcePointCount} points, {groupCount} groups, {placedCount} callouts placed, {failedCount} skipped"));
            }
            catch (Exception ex)
            {
                log.Add(new LogEntry(LogLevel.Error, $"Run failed: {ex.Message}"));
            }
            finally
            {
                _viewModel.ReportResults(roofCount, sourcePointCount, groupCount, placedCount);
            }
        }

        /// <summary>
        /// ASSUMPTION (flagged, not confirmed): the callout's parent view is the
        /// Revit UI's currently active view at Run time, not necessarily the view
        /// the roof was originally picked in — this tool has no Plan View dropdown
        /// to pin the callout's parent view to, so the active view is the only
        /// available anchor. If the active view changes between picking the roof
        /// and clicking Run, the callout will be parented to whatever view is
        /// active at Run time.
        /// </summary>
        private ElementId ResolveParentViewId(UIApplication app, RoofBase roof, IList<LogEntry> log)
        {
            var activeView = app.ActiveUIDocument?.ActiveView;
            if (activeView != null)
                return activeView.Id;

            log.Add(new LogEntry(LogLevel.Error, "No active view available to place callouts in."));
            return ElementId.InvalidElementId;
        }
    }
}
