// File: RoofSelectionViewAdapter.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Views.Adapters
//
// Responsibility:
// - Bridges UI interaction to Revit selection service
// - Updates ViewModel with API-safe data
//
// IMPORTANT:
// - Revit API allowed here
// - NO business logic
// - NO geometry logic

using Autodesk.Revit.DB.Architecture;

using Revit26_Plugin.RoofRidgeLines_V06.Models;
using Revit26_Plugin.RoofRidgeLines_V06.Services.Selection;
using Revit26_Plugin.RoofRidgeLines_V06.ViewModels.Steps;

namespace Revit26_Plugin.RoofRidgeLines_V06.Views.Adapters
{
    public class RoofSelectionViewAdapter
    {
        private readonly RoofSelectionService _service;

        public RoofSelectionViewAdapter(RoofSelectionService service)
        {
            _service = service;
        }

        public void SelectRoof(RoofSelectionStepViewModel viewModel)
        {
            RoofInfo info = _service.SelectAndValidateRoof();
            viewModel.SelectedRoof = info;
        }
    }
}
