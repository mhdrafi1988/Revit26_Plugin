using System;
using Autodesk.Revit.UI;
using Revit26_Plugin.AddPointOnintersections.Models;
using Revit26_Plugin.AddPointOnintersections.Services;
using Revit26_Plugin.AddPointOnintersections.ViewModels;

namespace Revit26_Plugin.AddPointOnintersections.ExternalEvents
{
    public class AddPointsExternalEventHandler : IExternalEventHandler
    {
        private MainViewModel _viewModel;
        private RoofSelectionContext _context;

        public void Initialize(MainViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void SetRequest(RoofSelectionContext context)
        {
            _context = context;
        }

        public void Execute(UIApplication app)
        {
            if (_viewModel == null || _context == null)
            {
                return;
            }

            try
            {
                _viewModel.IsBusy = true;
                _viewModel.Log("Execute requested.");
                _viewModel.Log("Starting Transaction 01: Enable shape editing.");

                RoofIntersectionService service = new RoofIntersectionService();
                AddPointsExecutionResult result = service.Execute(_context, _viewModel.Log);

                _viewModel.ShapePointsAddedCount = result.AddedPointsCount;
                _viewModel.ZeroElevationConfirmed = result.ZeroElevationConfirmed ? "Yes" : "No";

                _viewModel.Log($"Execution completed. Total shape editing points added: {result.AddedPointsCount}.");
                _viewModel.Log($"Every added point created with 0 elevation difference: {_viewModel.ZeroElevationConfirmed}.");
            }
            catch (Exception ex)
            {
                _viewModel.Log($"Execution failed: {ex.Message}");
                TaskDialog.Show("Add Points On Intersections", $"Execution failed:\n{ex.Message}");
            }
            finally
            {
                _viewModel.IsBusy = false;
            }
        }

        public string GetName()
        {
            return "Add Points On Intersections External Event Handler";
        }
    }
}