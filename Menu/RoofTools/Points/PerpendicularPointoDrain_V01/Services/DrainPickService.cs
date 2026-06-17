using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.PerpendicularPointoDrain.V01.Services
{
    /// <summary>
    /// Drain identification — three runtime-selectable methods.
    /// </summary>
    public class DrainPickService
    {
        /// <summary>
        /// Method 1: pick points on the roof face using the same pattern as AutoSlopeCommand —
        /// PickObjects(PointOnElement) gives a native Finish button instead of relying on Escape,
        /// and each picked Reference.GlobalPoint is already snapped to the element geometry.
        /// </summary>
        public List<XYZ> PickOnScreen(UIDocument uidoc)
        {
            var pts = new List<XYZ>();
            try
            {
                IList<Reference> picks = uidoc.Selection.PickObjects(
                    ObjectType.PointOnElement, "Pick drain points and click Finish");

                foreach (var r in picks)
                    pts.Add(r.GlobalPoint);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Esc pressed before clicking Finish — return whatever was captured (none).
            }
            return pts;
        }

        /// <summary>
        /// Method 2: select existing drain family instances directly, take their location point.
        /// </summary>
        public List<XYZ> PickFamilyInstances(UIDocument uidoc, Document doc)
        {
            var pts = new List<XYZ>();
            try
            {
                var refs = uidoc.Selection.PickObjects(ObjectType.Element, "Select drain family instances (Esc to finish)");
                foreach (var r in refs)
                {
                    if (doc.GetElement(r) is FamilyInstance fi && fi.Location is LocationPoint lp)
                        pts.Add(lp.Point);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Esc pressed before selecting anything — return whatever was captured (none).
            }
            return pts;
        }

        /// <summary>
        /// Method 3: auto-detect drains near the roof.
        /// NOTE: this is a best-effort reimplementation (category + name-contains-"drain" filter
        /// within the roof's bounding box) since the original AutoSlopeByPoint detection source
        /// wasn't available when this was written. Swap in your exact original filter logic here
        /// if it differs — e.g. specific family/type names, hosting checks, or parameter flags.
        /// </summary>
        public List<XYZ> AutoDetectNearRoof(Document doc, RoofBase roof)
        {
            var pts = new List<XYZ>();

            BoundingBoxXYZ bb = roof.get_BoundingBox(null);
            if (bb == null) return pts;

            Outline outline = new Outline(bb.Min, bb.Max);
            var bbFilter = new BoundingBoxIntersectsFilter(outline);

            var candidates = new FilteredElementCollector(doc)
                .WherePasses(bbFilter)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi =>
                    (fi.Symbol?.Family?.Name?.IndexOf("drain", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (fi.Name?.IndexOf("drain", StringComparison.OrdinalIgnoreCase) >= 0));

            foreach (var fi in candidates)
            {
                if (fi.Location is LocationPoint lp)
                    pts.Add(lp.Point);
            }

            return pts;
        }
    }
}