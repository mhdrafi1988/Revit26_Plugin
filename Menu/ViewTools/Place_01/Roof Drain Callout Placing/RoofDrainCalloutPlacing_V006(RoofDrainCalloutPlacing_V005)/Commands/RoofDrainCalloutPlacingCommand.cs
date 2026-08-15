using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofDrainCalloutPlacing.V006.Helpers;
using Revit26_Plugin.RoofDrainCalloutPlacing.V006.Models;
using Revit26_Plugin.RoofDrainCalloutPlacing.V006.Services;
using Revit26_Plugin.RoofDrainCalloutPlacing.V006.ViewModels;
using Revit26_Plugin.RoofDrainCalloutPlacing.V006.Views;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V006.Commands
{
    /// <summary>
    /// V005: roof pick and drain-point pick now happen synchronously here,
    /// inside Execute()'s valid API context, BEFORE the window is constructed
    /// or shown — confirmed with Rafi. This replaces V004's ExternalEvent-driven
    /// PickRoofCommand/PickPointsCommand (both removed from the ViewModel);
    /// the window is now display + parameters + Run only, no pick buttons.
    ///
    /// Drain-point picking uses Selection.PickObjects(ObjectType.PointOnElement, ...)
    /// rather than a PickPoint loop — PickObjects shows real "Finish"/"Cancel"
    /// buttons on the dialog bar (confirmed against the Revit API docs), unlike
    /// PickPoint which only supports Esc with no on-screen affordance. Shape
    /// editing is enabled on the roof before picking starts, so Revit natively
    /// draws the SlabShapeVertex markers in the view during the pick session —
    /// no custom marker-drawing code needed.
    ///
    /// Cancel behavior (confirmed):
    ///   - Esc during roof pick -> TaskDialog, command aborts, window never opens.
    ///   - Cancel/Esc during drain-point pick (at any point, including
    ///     immediately, 0 points picked) -> proceeds to open the window
    ///     anyway, with whatever points were picked (possibly zero). Run
    ///     stays disabled until PickedPoints.Count > 0 (existing CanRun gate
    ///     in the ViewModel).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofDrainCalloutPlacingCommand : IExternalCommand
    {
        private readonly RoofPointCollectionService _pointService = new RoofPointCollectionService();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;

            // Load settings before picking so the persisted snap tolerance is
            // honored during the point-picking loop, not just after the window
            // opens. Passed into the ViewModel constructor below to avoid a
            // second, redundant load.
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            double snapToleranceFeet = double.TryParse(settings.SnapToleranceMmText, out var snapMm)
                ? snapMm / 304.8
                : 150.0 / 304.8;

            RoofBase roof;
            try
            {
                var pickedRef = uiDoc.Selection.PickObject(
                    ObjectType.Element, new RoofSelectionFilter(), "Select a roof.");
                roof = doc.GetElement(pickedRef) as RoofBase;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                TaskDialog.Show("RoofDrainCalloutPlacing", "No roof was selected. Command cancelled.");
                return Result.Cancelled;
            }

            if (roof == null)
            {
                // RoofSelectionFilter should prevent this, but guard anyway —
                // filters can be bypassed in some edge cases (e.g. pre-highlighted
                // selection at pick start).
                TaskDialog.Show("RoofDrainCalloutPlacing", "The picked element is not a roof. Command cancelled.");
                return Result.Cancelled;
            }

            try
            {
                using (var t = new Transaction(doc, "Enable shape editing for picked roof"))
                {
                    t.Start();
                    t.SetFailureHandlingOptions(
                        t.GetFailureHandlingOptions()
                         .SetFailuresPreprocessor(new RoofDrainFailuresPreprocessor()));

                    _pointService.EnsureShapeEditingEnabled(doc, roof);

                    var status = t.Commit();
                    if (status != TransactionStatus.Committed)
                    {
                        TaskDialog.Show("RoofDrainCalloutPlacing",
                            $"Could not enable shape editing on the selected roof ({status}). Command cancelled.");
                        return Result.Cancelled;
                    }
                }
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("RoofDrainCalloutPlacing", $"Shape editing failed: {ex.Message}. Command cancelled.");
                return Result.Cancelled;
            }

            var pickedPoints = new List<CandidatePoint>();
            try
            {
                using (var t = new Transaction(doc, "Pick drain points"))
                {
                    t.Start();
                    t.SetFailureHandlingOptions(
                        t.GetFailureHandlingOptions()
                         .SetFailuresPreprocessor(new RoofDrainFailuresPreprocessor()));

                    IList<Reference> refs;
                    try
                    {
                        // PickObjects(ObjectType.PointOnElement, ...) gives a real
                        // "Finish"/"Cancel" button pair on the dialog bar — unlike
                        // PickPoint, which only supports Esc-to-cancel with no
                        // on-screen affordance. Shape editing was already enabled
                        // above, so Revit natively draws the roof's SlabShapeVertex
                        // markers in the view during this pick session (confirmed:
                        // SlabShapeEditor.Enable() alone makes vertices visible in
                        // plan/3D — no custom drawing needed). RoofFaceSelectionFilter
                        // restricts picks to references on this specific roof only.
                        // Each Reference.GlobalPoint is then snapped to the nearest
                        // actual SlabShapeVertex by our own FindNearestVertex code,
                        // same as before — Revit's PointOnElement picking targets
                        // face/surface geometry, not SlabShapeVertex points
                        // specifically, so the snap step is still ours to do.
                        refs = uiDoc.Selection.PickObjects(
                            ObjectType.PointOnElement,
                            new RoofFaceSelectionFilter(roof.Id),
                            "Pick drain points on the roof, then click Finish.");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // Cancel button or Esc — confirmed: proceeds to open the
                        // window regardless of how many points were picked,
                        // including zero. refs stays empty; pickedPoints stays empty.
                        refs = new List<Reference>();
                    }

                    foreach (var pickedRef in refs)
                    {
                        var rawPick = pickedRef.GlobalPoint;
                        var candidate = _pointService.FindNearestVertex(doc, roof, rawPick, snapToleranceFeet);

                        if (candidate != null)
                        {
                            pickedPoints.Add(candidate);
                        }
                        else
                        {
                            pickedPoints.Add(new CandidatePoint
                            {
                                RoofId = roof.Id,
                                Position = rawPick,
                                SnapDeltaFeet = null,
                                IsSelected = true
                            });
                        }
                    }

                    var status = t.Commit();
                    if (status != TransactionStatus.Committed)
                    {
                        TaskDialog.Show("RoofDrainCalloutPlacing",
                            $"Drain point picking transaction failed ({status}). Command cancelled.");
                        return Result.Cancelled;
                    }
                }
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("RoofDrainCalloutPlacing", $"Drain point picking failed: {ex.Message}. Command cancelled.");
                return Result.Cancelled;
            }

            // ViewModel constructor calls ExternalEvent.Create() for the Run event —
            // must happen here, inside the valid API execution context, not lazily
            // from UI interaction. Roof, picked points, and pre-loaded settings are
            // passed in directly since picking already happened above.
            var viewModel = new RoofDrainCalloutPlacingViewModel(uiApp, roof, pickedPoints, settings, settingsService);
            var window = new RoofDrainCalloutPlacingWindow(viewModel);

            new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;

            // Show(), not ShowDialog() — required because IExternalEventHandler needs
            // the Revit message loop to keep pumping while this window is open.
            window.Show();

            return Result.Succeeded;
        }
    }
}
