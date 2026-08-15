using Autodesk.Revit.DB;
using Revit26_Plugin.CalloutCOP.V018.Helpers;

namespace Revit26_Plugin.CalloutCOP.V018.Services
{
    public static class ReferenceCalloutService
    {
        private const double MmToFeet = 1.0 / 304.8;

        /// <summary>
        /// Creates a single reference callout on parentView pointing at referenceView.
        /// offsetMm shifts the callout center along the view's sheet-space horizontal
        /// axis (negative = left, 0 = center, positive = right), scaled by the parent
        /// view's print scale before conversion to feet.
        /// </summary>
        public static void CreateReferenceCallout(
            Document doc,
            View parentView,
            View referenceView,
            double sizeMm,
            double offsetMm)
        {
            RevitViewValidator.ValidateParentForReferenceCallout(parentView);
            RevitViewValidator.ValidateReferenceTargetView(referenceView);
            RevitViewValidator.ValidatePlacementContext(doc, parentView, referenceView);

            var section = (ViewSection)parentView;
            var box = section.CropBox;
            var tf = box.Transform;

            var baseCenter = tf.OfPoint((box.Min + box.Max) * 0.5);
            var right = tf.BasisX.Normalize();
            var up = tf.BasisY.Normalize();

            var offsetFeet = (offsetMm * parentView.Scale) * MmToFeet;
            var center = baseCenter + right * offsetFeet;

            var half = (sizeMm * MmToFeet) / 2.0;

            var p1 = center - right * half - up * half;
            var p2 = center + right * half + up * half;

            ViewSection.CreateReferenceCallout(
                doc,
                section.Id,
                referenceView.Id,
                p1,
                p2);
        }
    }
}
