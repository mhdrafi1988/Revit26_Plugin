using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V03_03.Helpers;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V03_03.Services
{
    public static class SlabShapeService
    {
        public static void EnableEditing(
            Document doc,
            RoofBase roof,
            UiLogService log)
        {
            log.Log("Enabling slab shape editing");

            using Transaction tx = new(doc, "Enable Slab Shape Editing");
            tx.Start();

            var editor = roof.GetSlabShapeEditor();
            editor.Enable();

            log.Log($"Crease count: {editor.Creases.Count}");

            tx.Commit();
        }
    }
}
