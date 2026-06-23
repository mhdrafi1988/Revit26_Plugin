// ==================================
// File: RoofSelectionService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V003_01
// ==================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;

namespace Revit26_Plugin.CreaserAdv_V003_01.Services
{
    /// <summary>
    /// Prompts the user to pick a single roof element.
    /// Throws <see cref="Autodesk.Revit.Exceptions.OperationCanceledException"/>
    /// if the user presses Escape — callers should handle it explicitly.
    /// </summary>
    public class RoofSelectionService
    {
        public Element SelectSingleRoof(UIDocument uiDoc)
        {
            if (uiDoc == null) throw new ArgumentNullException(nameof(uiDoc));

            Reference reference = uiDoc.Selection.PickObject(
                ObjectType.Element,
                new RoofSelectionFilter(),
                "Select one roof");

            return uiDoc.Document.GetElement(reference);
        }

        // --------------------------------------------------
        // Selection filter — roofs only
        // --------------------------------------------------

        private sealed class RoofSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
                => elem.Category?.Id.Value == (int)BuiltInCategory.OST_Roofs;

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
