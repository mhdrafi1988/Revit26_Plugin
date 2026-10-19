using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin_Revised.TaperedEpsPattern2D.V005.Core.Pattern;

namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V005.Revit
{
    /// <summary>Builds a <see cref="RoofContext"/>; checks 3 (footprint roof) and 4 (visible in view).</summary>
    public static class RoofContextBuilder
    {
        public const string ExtrusionRoofMessage =
            "The selected roof is an extrusion roof. This tool works on footprint roofs only.";

        public const string NotVisibleMessage =
            "The selected roof is not visible in the active view. Make it visible (view range, visibility/graphics, crop) and run the tool again.";

        /// <summary>Returns false with a message when the roof is not usable at all (tool should exit).</summary>
        public static bool TryCheckRoof(RoofBase roof, ViewPlan view, out string message)
        {
            if (roof is ExtrusionRoof || roof is not FootPrintRoof)
            {
                message = ExtrusionRoofMessage;
                return false;
            }

            if (!IsVisibleInView(roof, view))
            {
                message = NotVisibleMessage;
                return false;
            }

            message = string.Empty;
            return true;
        }

        public static bool IsVisibleInView(Element element, View view) =>
            new FilteredElementCollector(element.Document, view.Id)
                .OfCategory(BuiltInCategory.OST_Roofs)
                .WhereElementIsNotElementType()
                .ToElementIds()
                .Contains(element.Id);

        /// <summary>Reads the roof and view. Geometry problems end up in <see cref="RoofContext.Outline"/>.</summary>
        public static RoofContext Build(Document doc, ElementId viewId, ElementId roofId)
        {
            var view = doc.GetElement(viewId) as ViewPlan;
            var roof = doc.GetElement(roofId) as FootPrintRoof;

            if (view == null || !view.IsValidObject || roof == null || !roof.IsValidObject)
            {
                return new RoofContext(doc, viewId, roofId)
                {
                    ViewName = view?.Name ?? "(view deleted)",
                    RoofName = roof == null ? "(roof deleted)" : Describe(roof),
                    Outline = Invalid("The roof or the view no longer exists. Close the tool and run it again.")
                };
            }

            Level? level = view.GenLevel;
            OutlineValidationResult outline = RoofFootprintReader.Read(roof, out int edges);
            if (outline.IsValid && !IsVisibleInView(roof, view))
            {
                outline = Invalid(NotVisibleMessage);
            }

            return new RoofContext(doc, viewId, roofId)
            {
                ViewName = view.Name,
                RoofName = Describe(roof),
                LevelName = level?.Name ?? "(no level)",
                LevelElevationFeet = level?.ProjectElevation ?? 0.0,
                ShortCurveToleranceFeet = doc.Application.ShortCurveTolerance,
                MinSegmentLengthMetres = System.Math.Max(
                    GeometryTolerance.MinSegmentLength,
                    UnitConverter.FeetToMetres(doc.Application.ShortCurveTolerance) * 1.01),
                SketchEdgeCount = edges,
                Outline = outline
            };
        }

        private static string Describe(RoofBase roof)
        {
            string type = roof.Document.GetElement(roof.GetTypeId())?.Name ?? "Roof";
            return $"{type}  [Id {roof.Id.Value}]";
        }

        private static OutlineValidationResult Invalid(string message) =>
            new OutlineValidationResult { Errors = new[] { message }, ShapeName = "Invalid" };
    }
}
