using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofDrainCalloutPlacing.V001.Models;
using Revit26_Plugin.RoofDrainCalloutPlacing.V001.Services;
using Revit26_Plugin.RoofDrainCalloutPlacing.V001.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V001.ExternalEvents
{
    /// <summary>
    /// All Revit API access for this tool happens here, triggered via ExternalEvent.Raise()
    /// from the ViewModel's RunCommand. Never called directly from the UI thread.
    /// </summary>
    public class RoofDrainCalloutRunHandler : IExternalEventHandler
    {
        private readonly RoofDrainCalloutPlacingViewModel _viewModel;
        private readonly RoofPointCollectionService _pointService = new RoofPointCollectionService();
        private readonly PointClusteringService _clusterService = new PointClusteringService();
        private readonly CalloutPlacementService _calloutService = new CalloutPlacementService();

        public RoofDrainCalloutRunHandler(RoofDrainCalloutPlacingViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public string GetName() => "RoofDrainCalloutPlacing Run Handler";

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var log = _viewModel.Logs;

            int roofCount = 0, zeroPointCount = 0, groupCount = 0, placedCount = 0, failedCount = 0;

            try
            {
                var parentView = _viewModel.SelectedPlanView;
                var draftingViewId = _viewModel.SelectedDraftingView.Id;
                double toleranceFeet = _viewModel.GroupingToleranceMm / 304.8;
                double calloutSizeFeet = _viewModel.CalloutSizeMm / 304.8;

                var roofs = _pointService.CollectRoofsInView(doc, parentView);
                roofCount = roofs.Count;

                var allGroups = new List<ZeroOffsetPointGroup>();

                using (var group = new TransactionGroup(doc, "Roof Drain Callout Placement"))
                {
                    group.Start();

                    // 1. Enable shape editing where needed + read zero-offset points.
                    foreach (var roof in roofs)
                    {
                        using (var t = new Transaction(doc, "Enable shape editing"))
                        {
                            t.Start();
                            t.SetFailureHandlingOptions(
                                t.GetFailureHandlingOptions()
                                 .SetFailuresPreprocessor(new RoofDrainFailuresPreprocessor()));

                            List<XYZ> zeroPoints;
                            try
                            {
                                zeroPoints = _pointService.GetZeroOffsetPoints(doc, roof, log);
                            }
                            catch (Exception ex)
                            {
                                log.Add(new LogEntry(LogLevel.Warning, $"Roof {roof.Id}: skipped — {ex.Message}"));
                                t.RollBack();
                                continue;
                            }

                            var status = t.Commit();
                            if (status != TransactionStatus.Committed)
                            {
                                log.Add(new LogEntry(LogLevel.Error, $"Roof {roof.Id}: shape-edit transaction failed ({status})"));
                                continue;
                            }

                            zeroPointCount += zeroPoints.Count;
                            if (zeroPoints.Count == 0) continue;

                            var groups = _clusterService.Cluster(roof.Id, zeroPoints, toleranceFeet);
                            allGroups.AddRange(groups);
                        }
                    }

                    groupCount = allGroups.Count;

                    // 2. Place callouts, one transaction for all placements.
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
                                _calloutService.PlaceReferenceCallout(
                                    doc, parentView.Id, draftingViewId, g.Centroid, calloutSizeFeet);
                                placedCount++;
                                log.Add(new LogEntry(LogLevel.Success,
                                    $"Roof {g.RoofId}: cluster of {g.Points.Count} pts -> callout placed"));
                            }
                            catch (CalloutPlacementSkippedException ex)
                            {
                                failedCount++;
                                log.Add(new LogEntry(LogLevel.Warning, $"Roof {g.RoofId}: {ex.Message}"));
                            }
                            catch (Exception ex)
                            {
                                failedCount++;
                                log.Add(new LogEntry(LogLevel.Warning, $"Roof {g.RoofId}: callout failed — {ex.Message}"));
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
                    $"Done. {roofCount} roofs, {zeroPointCount} zero-offset points, {groupCount} groups, {placedCount} callouts placed, {failedCount} skipped"));
            }
            catch (Exception ex)
            {
                log.Add(new LogEntry(LogLevel.Error, $"Run failed: {ex.Message}"));
            }
            finally
            {
                _viewModel.ReportResults(roofCount, zeroPointCount, groupCount, placedCount);
            }
        }
    }
}
