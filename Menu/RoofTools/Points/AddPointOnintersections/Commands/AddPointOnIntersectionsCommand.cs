using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.AddPointOnintersections.ExternalEvents;
using Revit26_Plugin.AddPointOnintersections.Helpers;
using Revit26_Plugin.AddPointOnintersections.Models;
using Revit26_Plugin.AddPointOnintersections.ViewModels;
using Revit26_Plugin.AddPointOnintersections.Views;

namespace Revit26_Plugin.AddPointOnintersections.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AddPointOnIntersectionsCommand : IExternalCommand
    {
        private static MainWindow _window;
        private static MainViewModel _viewModel;
        private static AddPointsExternalEventHandler _handler;
        private static ExternalEvent _externalEvent;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                if (doc.ActiveView is not ViewPlan viewPlan)
                {
                    TaskDialog.Show(
                        "Add Points On Intersections",
                        "The active view must be a Plan View.\n\nCommand cancelled.");

                    return Result.Cancelled;
                }

                if (_window == null || !_window.IsLoaded)
                {
                    _handler = new AddPointsExternalEventHandler();
                    _externalEvent = ExternalEvent.Create(_handler);
                }

                Reference roofReference = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new FootPrintRoofSelectionFilter(),
                    "Select one footprint roof");

                if (roofReference == null)
                {
                    return Result.Cancelled;
                }

                FootPrintRoof roof = doc.GetElement(roofReference.ElementId) as FootPrintRoof;
                if (roof == null)
                {
                    TaskDialog.Show(
                        "Add Points On Intersections",
                        "Selected element is not a valid FootPrintRoof.\n\nCommand cancelled.");

                    return Result.Cancelled;
                }

                IList<Reference> detailLineReferences = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    new DetailLineSelectionFilter(),
                    "Select one or more detail lines");

                if (detailLineReferences == null || detailLineReferences.Count == 0)
                {
                    TaskDialog.Show(
                        "Add Points On Intersections",
                        "No detail lines were selected.\n\nCommand cancelled.");

                    return Result.Cancelled;
                }

                List<ElementId> detailLineIds = detailLineReferences
                    .Select(x => x.ElementId)
                    .Distinct()
                    .ToList();

                RoofSelectionContext context = new RoofSelectionContext(
                    uiDoc,
                    viewPlan.Id,
                    roof.Id,
                    detailLineIds);

                if (_window == null || !_window.IsLoaded)
                {
                    _viewModel = new MainViewModel();
                    _handler.Initialize(_viewModel);

                    _viewModel.Initialize(
                        context,
                        _externalEvent,
                        _handler);

                    _window = new MainWindow
                    {
                        DataContext = _viewModel
                    };

                    _window.Closed += (_, _) =>
                    {
                        _window = null;
                    };

                    _window.Show();
                }
                else
                {
                    _viewModel.Initialize(
                        context,
                        _externalEvent,
                        _handler);

                    _window.Activate();
                }

                _viewModel.Log("Plugin started.");
                _viewModel.Log($"Active plan view validated: {doc.ActiveView.Name} (Id: {doc.ActiveView.Id.Value}).");
                _viewModel.Log($"Roof selected: ElementId {roof.Id.Value}.");
                _viewModel.Log($"Detail lines selected: {detailLineIds.Count}.");
                _viewModel.Log("Selections completed. UI loaded.");

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;

                TaskDialog.Show(
                    "Add Points On Intersections",
                    $"Unexpected error:\n{ex.Message}");

                return Result.Failed;
            }
        }
    }
}