using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit22_Plugin.RoofTagV4.Helpers
{
    /// <summary>
    /// Handles selection of a RoofBase element for RoofTagV4.
    /// V4-safe and does not conflict with V3 selection helper.
    /// </summary>
    public static class SelectionHelperV4
    {
        /// <summary>
        /// Prompts user to select ONE roof element.
        /// Ensures returned element is a RoofBase.
        /// </summary>
        public static RoofBase SelectRoof(UIDocument uiDoc)
        {
            try
            {
                Selection sel = uiDoc.Selection;

                Reference picked = sel.PickObject(
                    ObjectType.Element,
                    new RoofOnlyFilterV4(),
                    "Select a Roof");

                if (picked == null)
                    return null;

                Element e = uiDoc.Document.GetElement(picked);
                return e as RoofBase;
            }
            catch
            {
                // Cancelled or wrong selection
                return null;
            }
        }

        /// <summary>
        /// Enables SlabShapeEditor if disabled.
        /// </summary>
        public static void EnsureShapeEditorEnabled(Document doc, RoofBase roof)
        {
            if (roof == null) return;
            var slabShapeEditor = roof.GetSlabShapeEditor();
            if (slabShapeEditor == null) return;

            if (!slabShapeEditor.IsEnabled)
            {
                using (Transaction tx = new Transaction(doc, "Enable Slab Shape Editor"))
                {
                    tx.Start();
                    slabShapeEditor.Enable();
                    tx.Commit();
                }
            }
        }
    }

    /// <summary>
    /// Filters selection to RoofBase only.
    /// </summary>
    public class RoofOnlyFilterV4 : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is RoofBase;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
