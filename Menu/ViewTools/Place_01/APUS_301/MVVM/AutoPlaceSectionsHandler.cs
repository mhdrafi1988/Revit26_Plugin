using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
//using Revit22_Plugin.SectionPlacer.Services;
//using Revit22_Plugin.SectionPlacer.ViewModels;
using Revit26_Plugin.APUS_301.Services;
using Revit26_Plugin.APUS_301.ViewModels;

namespace Revit26_Plugin.APUS_301.MVVM
{
    public class AutoPlaceSectionsHandler : IExternalEventHandler
    {
        public AutoPlaceSectionsViewModel Payload { get; set; }

        public void Execute(UIApplication app)
        {
            if (Payload == null) return;

            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            ViewSheet lastSheet = null;

            using (Transaction tx = new Transaction(doc, "APUS Auto Place Sections"))
            {
                tx.Start();
                var service = new SectionPlacementService(doc, app);
                lastSheet = service.ExecutePlacement(Payload);
                tx.Commit();
            }

            if (lastSheet != null)
                uidoc.ActiveView = lastSheet;
        }

        public string GetName() => "APUS_301_AutoPlaceSections";
    }
}
