using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V03_03.Helpers;

namespace Revit26_Plugin.Creaser_V03_03.Services
{
    public static class SlabShapeService
    {
        public static void EnableEditing(
            Document doc,
            RoofBase roof,
            UiLogService log)
        {
            log.Log("Enable slab shape editing");

            using Transaction tx =
                new Transaction(doc, "Enable Slab Shape Editing");

            tx.Start();

            SlabShapeEditor editor = roof.GetSlabShapeEditor();

            if (!editor.IsEnabled)
            {
                editor.Enable();
                log.Log("Slab shape enabled");
            }
            else
            {
                log.Log("Slab shape already enabled");
            }

            tx.Commit();
        }
    }
}
