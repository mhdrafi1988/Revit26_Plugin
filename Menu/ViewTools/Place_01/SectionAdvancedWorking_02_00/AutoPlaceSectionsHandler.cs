using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Revit22_Plugin.SectionPlacer.Services;
using Revit22_Plugin.SectionPlacer.ViewModels;

namespace Revit22_Plugin.SectionPlacer.MVVM
{
    /// <summary>
    /// Executes the section placement workflow inside Revit's API thread.
    /// Triggered by ExternalEvent from the ViewModel.
    /// </summary>
    public class AutoPlaceSectionsHandler : IExternalEventHandler
    {
        public AutoPlaceSectionsViewModel Payload { get; set; }

        public void Execute(UIApplication uiapp)
        {
            if (Payload == null)
            {
                TaskDialog.Show("Error", "No payload found for Auto Section Placement.");
                return;
            }

            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            ViewSheet lastSheet = null;

            try
            {
                using (Transaction tx = new Transaction(doc, "Auto Place Sections"))
                {
                    tx.Start();

                    // ✅ Run the placement logic — returns the last created sheet
                    var service = new SectionPlacementService(doc, uiapp);
                    lastSheet = service.ExecutePlacement(Payload);

                    tx.Commit();
                }

                // ✅ Switch to the final sheet AFTER transaction commits
                if (lastSheet != null)
                {
                    uidoc.ActiveView = lastSheet;
                }
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Error", $"Auto Place Sections failed:\n{ex.Message}");
            }
        }

        public string GetName() => "Auto Place Sections Event";
    }
}
