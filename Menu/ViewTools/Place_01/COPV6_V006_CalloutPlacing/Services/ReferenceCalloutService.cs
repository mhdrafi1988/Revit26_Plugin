using Autodesk.Revit.DB;
using Revit26_Plugin.CalloutCOP_V06.Helpers;

namespace Revit26_Plugin.CalloutCOP_V06.Services
{
    public static class ReferenceCalloutService
    {
        public static void CreateReferenceCallout(
            Document doc,
            View parentView,
            View referenceView,
            double sizeMm)
        {
            RevitViewValidator.ValidateParentForReferenceCallout(parentView);
            RevitViewValidator.ValidateReferenceTargetView(referenceView);
            RevitViewValidator.ValidatePlacementContext(doc, parentView, referenceView);

            var section = (ViewSection)parentView;
            var box = section.CropBox;
            var tf = box.Transform;

            var center = tf.OfPoint((box.Min + box.Max) * 0.5);
            var right = tf.BasisX.Normalize();
            var up = tf.BasisY.Normalize();

            var half = (sizeMm / 304.8) / 2.0;

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
