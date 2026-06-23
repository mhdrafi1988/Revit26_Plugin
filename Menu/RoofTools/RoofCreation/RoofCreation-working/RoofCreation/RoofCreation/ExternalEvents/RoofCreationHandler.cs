// ==================================================
// File: RoofCreationHandler.cs
// ==================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.V006.Services;
using Revit26_Plugin.RoofFromFloor.V006.ViewModels;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.V006.ExternalEvents
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

                if (ViewModel.SelectedRoof == null ||
                    ViewModel.RoofContext == null)
                {
                    ViewModel.LogFromExternal("? ABORT: Missing roof selection or roof context.");
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

                // -----------------------------------------
                // CREATE ROOF (AUTHORITATIVE FOOTPRINT ONLY)
                // -----------------------------------------
                bool success = RoofCreationService.TryCreateFootprintRoof(
                    doc,
                    ViewModel.RoofContext,
                    roofType,
                    ViewModel.RoofContext.RoofLevel,
                    ViewModel.LogFromExternal
                );

                if (!success && ViewModel.DebugMode)
                {
                    ViewModel.LogFromExternal("?? DEBUG MODE: Dumping geometry");

                    View view = uidoc.ActiveView;

                    CurveDumpService.DumpCurves(
                        doc,
                        view,
                        ViewModel.RoofContext.RoofFootprintCurves,
                        "DEBUG_Roof_Footprint");

                    CurveDumpService.DumpCurves(
                        doc,
                        view,
                        ViewModel.FloorProfiles.SelectMany(p => p.Curves),
                        "DEBUG_Floor_Profiles");
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
