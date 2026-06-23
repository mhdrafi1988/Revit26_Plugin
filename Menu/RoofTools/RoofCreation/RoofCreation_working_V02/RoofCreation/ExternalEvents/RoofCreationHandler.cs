using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.V02.Services;
using Revit26_Plugin.RoofFromFloor.V02.ViewModels;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.V02.ExternalEvents
{
    public class RoofCreationHandler : IExternalEventHandler
    {
        public RoofFromFloorViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                ViewModel.LogFromExternal("=== HANDLER INPUT CHECK ===");

                ViewModel.LogFromExternal(
                    $"SelectedRoof: {(ViewModel.SelectedRoof == null ? "NULL" : ViewModel.SelectedRoof.Id.Value.ToString())}");

                ViewModel.LogFromExternal(
                    $"RoofContext: {(ViewModel.RoofContext == null ? "NULL" : "OK")}");

                ViewModel.LogFromExternal(
                    $"RoofLevel: {(ViewModel.RoofContext?.RoofLevel == null ? "NULL" : ViewModel.RoofContext.RoofLevel.Name)}");

                ViewModel.LogFromExternal(
                    $"CleanLoops: {(ViewModel.CleanLoops == null ? "NULL" : ViewModel.CleanLoops.Count.ToString())}");

                if (ViewModel.RoofContext == null ||
                    ViewModel.CleanLoops == null ||
                    ViewModel.CleanLoops.Count == 0)
                {
                    ViewModel.LogFromExternal("? ABORT: Missing roof context or cleaned loops.");
                    return;
                }

                if (ViewModel.SelectedRoof == null)
                {
                    ViewModel.LogFromExternal("? ABORT: SelectedRoof is NULL.");
                    return;
                }

                RoofType roofType =
                    doc.GetElement(ViewModel.SelectedRoof.GetTypeId()) as RoofType;

                ViewModel.LogFromExternal(
                    $"RoofType: {(roofType == null ? "NULL" : roofType.Name)}");

                if (roofType == null)
                {
                    ViewModel.LogFromExternal("? ABORT: RoofType resolution failed.");
                    return;
                }

                double sourceBaseOffset =
                    ViewModel.SelectedRoof
                        .get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM)
                        ?.AsDouble() ?? 0.0;

                ViewModel.LogFromExternal(
                    $"Source roof base offset (ft): {sourceBaseOffset:F6}");

                bool success = RoofCreationService.TryCreateFootprintRoof(
                    doc,
                    ViewModel.CleanLoops,
                    roofType,
                    ViewModel.RoofContext.RoofLevel,
                    sourceBaseOffset,
                    ViewModel.LogFromExternal
                );

                if (!success && ViewModel.DebugMode)
                {
                    ViewModel.LogFromExternal("?? DEBUG MODE: Dumping geometry");

                    View view = uidoc.ActiveView;

                    CurveDumpService.DumpCleanGroupedCurves(
                        doc,
                        view,
                        ViewModel.RoofContext.RoofFootprintCurves,
                        "RoofFootprintCurves");

                    CurveDumpService.DumpCleanGroupedCurves(
                        doc,
                        view,
                        ViewModel.FloorProfiles.SelectMany(p => p.Curves),
                        "FloorProfiles");

                    CurveDumpService.DumpCleanGroupedCurves(
                        doc,
                        view,
                        ViewModel.CleanLoops.SelectMany(l => l),
                        "CleanLoops");
                }
            }
            catch (System.Exception ex)
            {
                ViewModel.LogFromExternal($"? HANDLER EXCEPTION: {ex.GetType().Name}");
                ViewModel.LogFromExternal(ex.Message);
            }
            finally
            {
                ViewModel.ShowWindow();
            }
        }

        public string GetName() => "Roof Creation Handler";
    }
}
