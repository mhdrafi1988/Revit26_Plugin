// File: RoofSelectionService.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Services.Selection
//
// Responsibility:
// - Handles user roof selection
// - Validates slab shape editing capability
// - Returns API-agnostic RoofInfo model
//
// IMPORTANT:
// - Revit 2026 compatible
// - NO imaginary API calls
// - NO UI logic

using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI.Selection;

using Revit26_Plugin.RoofRidgeLines_V06.Models;
using Revit26_Plugin.RoofRidgeLines_V06.Services.Context;

namespace Revit26_Plugin.RoofRidgeLines_V06.Services.Selection
{
    public class RoofSelectionService
    {
        private readonly RevitContextService _context;

        public RoofSelectionService(RevitContextService context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Prompts the user to select a single roof and validates it.
        /// </summary>
        public RoofInfo SelectAndValidateRoof()
        {
            _context.ValidatePlanView();

            Reference reference = _context.Selection.PickObject(
                ObjectType.Element,
                new RoofSelectionFilter(),
                "Select a roof");

            if (reference == null)
                throw new InvalidOperationException("No roof selected.");

            Element element = _context.Document.GetElement(reference);

            if (element is not RoofBase roof)
                throw new InvalidOperationException("Selected element is not a roof.");

            // Revit 2026–correct shape editor check
            SlabShapeEditor editor = roof.SlabShapeEditor;
            bool isShapeEditable = editor != null;

            if (!isShapeEditable)
                throw new InvalidOperationException(
                    "Selected roof does not support shape editing.");

            return new RoofInfo(
                roof.Id.Value,     // Revit 2026 correct
                roof.Name,
                isShapeEditable);
        }

        /// <summary>
        /// Selection filter allowing only roof elements.
        /// </summary>
        private class RoofSelectionFilter : ISelectionFilter
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
}
