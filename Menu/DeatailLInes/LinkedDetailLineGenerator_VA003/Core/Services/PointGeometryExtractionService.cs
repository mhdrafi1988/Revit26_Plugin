using System;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>Outcome of extracting a Point-group element's location.</summary>
    public class PointExtractionResult
    {
        public ElementLocationKind Kind { get; set; }
        public XYZ? Point { get; set; }
        public Curve? Curve { get; set; }
    }

    /// <summary>
    /// Extracts the insertion point for Point-group elements (spec Section 19), with
    /// mandatory Section 21 classification: an element checked under a Point-group
    /// category is not automatically treated as Point-based. ElementLocationClassifier
    /// determines the true location kind; PointProcessingEngine acts on
    /// PointExtractionResult.Kind, routing Curve-classified elements to the same
    /// Linear rendering path as Walls/Beams, and reporting Unsupported elements as
    /// skipped rather than guessing.
    /// </summary>
    public class PointGeometryExtractionService
    {
        private readonly ElementLocationClassifier _classifier = new();

        public PointExtractionResult Extract(Element element, Action<string, ElementId?>? onWarning = null)
        {
            var kind = _classifier.Classify(element);

            switch (kind)
            {
                case ElementLocationKind.Point:
                {
                    var loc = (LocationPoint)element.Location;
                    return new PointExtractionResult { Kind = kind, Point = loc.Point };
                }
                case ElementLocationKind.Curve:
                {
                    var loc = (LocationCurve)element.Location;
                    return new PointExtractionResult { Kind = kind, Curve = loc.Curve };
                }
                default:
                    onWarning?.Invoke(
                        "Element was skipped because no supported V1 representation was found (no reliable Point or Curve location).",
                        element.Id);
                    return new PointExtractionResult { Kind = ElementLocationKind.Unsupported };
            }
        }
    }
}
