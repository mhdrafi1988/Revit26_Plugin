// File: PointPickViewAdapter.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Views.Adapters

using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

using Revit26_Plugin.RoofRidgeLines_V06.Models;
using Revit26_Plugin.RoofRidgeLines_V06.Services.Context;
using Revit26_Plugin.RoofRidgeLines_V06.ViewModels.Steps;

namespace Revit26_Plugin.RoofRidgeLines_V06.Views.Adapters
{
    public class PointPickViewAdapter
    {
        private readonly RevitContextService _context;

        public PointPickViewAdapter(RevitContextService context)
        {
            _context = context;
        }

        public void PickTwoPoints(PointPickStepViewModel vm)
        {
            XYZ p1 = _context.Selection.PickPoint("Pick first roof point");
            XYZ p2 = _context.Selection.PickPoint("Pick second roof point");

            vm.Point1 = new PickedPointData(p1.X, p1.Y, p1.Z);
            vm.Point2 = new PickedPointData(p2.X, p2.Y, p2.Z);
        }
    }
}
