using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.CalloutCOP_V06.Helpers
{
    public static class RevitViewValidator
    {
        /// <summary>
        /// Determines whether a view can appear in the selectable target list.
        /// </summary>
        public static bool IsSelectableTargetView(View view)
        {
            if (view == null)
                return false;

            if (view.IsTemplate)
                return false;

            if (!view.CanBePrinted)
                return false;

            return view.ViewType == ViewType.Section
                || view.ViewType == ViewType.Elevation;
        }

        /// <summary>
        /// Validates that the parent view can host a reference callout.
        /// </summary>
        public static void ValidateParentForReferenceCallout(View parent)
        {
            if (parent is not ViewSection section)
                throw new InvalidOperationException(
                    "Parent view must be a section view.");

            if (!section.CropBoxActive)
                throw new InvalidOperationException(
                    $"Crop box is not active in view '{section.Name}'.");

            if (!section.CropBoxVisible)
                throw new InvalidOperationException(
                    $"Crop box is not visible in view '{section.Name}'.");
        }

        /// <summary>
        /// Validates that the referenced view is a Drafting View.
        /// </summary>
        public static void ValidateReferenceTargetView(View view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            if (view.IsTemplate)
                throw new InvalidOperationException(
                    $"Referenced view '{view.Name}' is a template.");

            if (view.ViewType != ViewType.DraftingView)
                throw new InvalidOperationException(
                    $"Referenced view '{view.Name}' is not a Drafting View.");
        }

        /// <summary>
        /// Cross-checks document and view relationships.
        /// </summary>
        public static void ValidatePlacementContext(
            Document doc,
            View parent,
            View reference)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));

            if (!doc.IsModifiable)
                throw new InvalidOperationException(
                    "Document is not in a modifiable state.");

            if (parent.Id == reference.Id)
                throw new InvalidOperationException(
                    "A view cannot reference itself.");
        }
    }
}
