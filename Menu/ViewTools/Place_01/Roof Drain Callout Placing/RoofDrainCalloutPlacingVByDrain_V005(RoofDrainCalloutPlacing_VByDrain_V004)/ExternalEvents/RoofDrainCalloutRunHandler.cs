using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.Models;
using Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.Services;
using Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.ExternalEvents
{
    /// <summary>
    /// Executes callout placement inside the Revit API context.
    ///
    /// V004 changes from V002:
    /// - No more single global margin/floor — each opening now carries its
    ///   group's (Circle/Rectangle/Other) resolved sizing.
    /// - Each SELECTED OPENING gets its own callout box (never a shared
    ///   group-wide box — confirmed with Rafi). Auto mode sizes the box from
    ///   that single opening's own geometry + margin; Fixed mode places a
    ///   FixedSize square centered on that opening.
    /// - Parent view is still the current active view (resolved at Run time).
    /// </summary>
    public class RoofDrainCalloutRunHandler : IExternalEventHandler
    {
        private readonly RoofDrainCalloutPlacingViewModel _viewModel;

        public RoofDrainCalloutRunHandler(RoofDrainCalloutPlacingViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public void Execute(UIApplication app)
        {
            int placed = 0, skipped = 0, failed = 0;

            try
            {
                var doc = app.ActiveUIDocument.Document;
                var selectedWithSizing = _viewModel.GetSelectedOpeningsWithSizing();

                if (selectedWithSizing.Count == 0)
                {
                    _viewModel.AddLog(LogLevel.Warning, "No openings selected.");
                    _viewModel.OnRunCompleted(0, 0, 0);
                    return;
                }

                var draftingView = _viewModel.SelectedDraftingView;
                if (draftingView == null)
                {
                    _viewModel.AddLog(LogLevel.Error, "No drafting view selected.");
                    _viewModel.OnRunCompleted(0, selectedWithSizing.Count, 1);
                    return;
                }

                var parentViewId = ResolveParentViewId(app);
                if (parentViewId == ElementId.InvalidElementId)
                {
                    _viewModel.AddLog(LogLevel.Error, "No active view available to place callouts in.");
                    _viewModel.OnRunCompleted(0, selectedWithSizing.Count, 1);
                    return;
                }

                double duplicateToleranceFeet = 0.5 / 304.8; // 0.5mm tolerance (configurable if needed)

                // New instance per run so duplicate check starts fresh
                var calloutService = new CalloutPlacementService();

                using (var txGroup = new TransactionGroup(doc, "Roof Drain Callout Placement"))
                {
                    txGroup.Start();

                    using (var tx = new Transaction(doc, "Place opening callouts"))
                    {
                        tx.Start();
                        tx.SetFailureHandlingOptions(
                            tx.GetFailureHandlingOptions()
                             .SetFailuresPreprocessor(new RoofDrainFailuresPreprocessor()));

                        foreach (var (opening, sizing) in selectedWithSizing)
                        {
                            try
                            {
                                if (opening.LoopGeometry == null || opening.CenterPoint == null)
                                {
                                    skipped++;
                                    _viewModel.AddLog(LogLevel.Warning, $"Opening {opening.LoopIdentifier}: no geometry — skipped.");
                                    continue;
                                }

                                calloutService.PlaceReferenceCallout(
                                    doc,
                                    parentViewId,
                                    draftingView.Id,
                                    opening,
                                    sizing,
                                    duplicateToleranceFeet);

                                placed++;
                                _viewModel.AddLog(LogLevel.Success, $"Callout placed for {opening.LoopIdentifier} ({sizing.Mode})");
                            }
                            catch (CalloutPlacementSkippedException ex)
                            {
                                skipped++;
                                _viewModel.AddLog(LogLevel.Warning, $"Opening {opening.LoopIdentifier}: {ex.Message}");
                            }
                            catch (Exception ex)
                            {
                                failed++;
                                _viewModel.AddLog(LogLevel.Error, $"Opening {opening.LoopIdentifier}: {ex.Message}");
                            }
                        }

                        var status = tx.Commit();
                        if (status != TransactionStatus.Committed)
                        {
                            _viewModel.AddLog(LogLevel.Error, $"Transaction failed: {status}");
                        }
                    }

                    txGroup.Assimilate();
                }

                _viewModel.OnRunCompleted(placed, skipped, failed);
            }
            catch (Exception ex)
            {
                _viewModel.AddLog(LogLevel.Error, $"Run failed: {ex.Message}");
                _viewModel.OnRunCompleted(0, 0, 1);
            }
        }

        /// <summary>
        /// Resolve the parent view for callout placement: the Revit UI's currently
        /// active view at Run time. This is the view in which the reference callout
        /// box will be drawn.
        /// </summary>
        private ElementId ResolveParentViewId(UIApplication app)
        {
            var activeView = app.ActiveUIDocument?.ActiveView;
            if (activeView != null)
                return activeView.Id;

            return ElementId.InvalidElementId;
        }

        public string GetName() => "RoofDrainCalloutRunHandler";
    }
}
