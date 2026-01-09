using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.RoofTagV4.Helpers;
using Revit22_Plugin.RoofTagV4.Models;
using Revit22_Plugin.RoofTagV4.Services;
using Revit22_Plugin.RoofTagV4.ViewModels;
using System.Collections.Generic;

namespace Revit22_Plugin.RoofTagV4
{
    /// <summary>
    /// Executes the full tagging workflow using the already-selected roof.
    /// NO picking happens here. UI receives geometry instantly.
    /// Results are written back into the ViewModel for UI display.
    /// </summary>
    public static class RoofTagExecutionV4
    {
        public static void Execute(
            UIApplication uiApp,
            RoofTagViewModelV4 vm,
            RoofBase roof,
            RoofLoopsModel geom)
        {
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            // -------------------------------------------------------
            // SAFETY CHECK
            // -------------------------------------------------------
            if (roof == null)
            {
                vm.ResultMessage = "❌ No roof was supplied to execution.";
                return;
            }

            if (geom == null || geom.AllVertices.Count == 0)
            {
                vm.ResultMessage = "❌ Invalid or empty geometry.";
                return;
            }

            // Ensure shape editing enabled
            SelectionHelperV4.EnsureShapeEditorEnabled(doc, roof);

            // -------------------------------------------------------
            // 1️⃣ FILTER POINTS (V4 rules)
            // -------------------------------------------------------
            List<TagPoint> finalPoints = PointSelectionServiceV4.GetFinalTagPoints(
                roof,
                geom.AllVertices,
                geom.Boundary,
                vm.ClutterThreshold,
                vm.DrainThreshold);

            if (finalPoints == null || finalPoints.Count == 0)
            {
                vm.ResultMessage = "⚠ No valid points selected by filter rules.";
                vm.SuccessCount = 0;
                vm.FailCount = 0;
                return;
            }

            // -------------------------------------------------------
            // 2️⃣ VALIDATE VIEW (MUST BE PLAN)
            // -------------------------------------------------------
            View activeView = uiDoc.ActiveView;

            if (activeView.ViewType != ViewType.FloorPlan &&
                activeView.ViewType != ViewType.CeilingPlan)
            {
                vm.ResultMessage = "❌ Tagging works only in PLAN views.";
                vm.SuccessCount = 0;
                vm.FailCount = finalPoints.Count;
                return;
            }

            // -------------------------------------------------------
            // 3️⃣ RUN TAGGING SERVICE
            // -------------------------------------------------------
            var result = TaggingServiceV4.PlaceTags(
                uiDoc,
                roof,
                geom,
                finalPoints,
                activeView,
                vm);

            vm.SuccessCount = result.success;
            vm.FailCount = result.fail;

            // -------------------------------------------------------
            // 4️⃣ WRITE FEEDBACK INTO UI
            // -------------------------------------------------------
            vm.ResultMessage =
                $"🏁 Tagging Completed\n" +
                $"Total Points: {finalPoints.Count}\n" +
                $"✔ Success: {result.success}\n" +
                $"✘ Failed: {result.fail}";
        }
    }
}
