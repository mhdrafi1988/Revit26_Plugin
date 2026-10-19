using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.TaperedEpsPattern2D.V004.Core.Pattern;

namespace Revit26_Plugin.TaperedEpsPattern2D.V004.Revit
{
    /// <summary>
    /// Reads FootPrintRoof.GetProfiles() into plan segments (metres) and validates them
    /// with the Core <see cref="OutlineValidator"/>. Never throws for bad geometry.
    /// </summary>
    public static class RoofFootprintReader
    {
        public static OutlineValidationResult Read(FootPrintRoof roof, out int sketchEdgeCount)
        {
            var loops = new List<IReadOnlyList<Segment2D>>();
            int nonLinear = 0;
            sketchEdgeCount = 0;

            ModelCurveArrArray profiles = roof.GetProfiles();
            foreach (ModelCurveArray loop in profiles)
            {
                var segments = new List<Segment2D>();
                foreach (ModelCurve modelCurve in loop)
                {
                    sketchEdgeCount++;
                    Curve curve = modelCurve.GeometryCurve;
                    if (curve is Line line && line.IsBound)
                    {
                        segments.Add(new Segment2D(
                            UnitConverter.ToPlanMetres(line.GetEndPoint(0)),
                            UnitConverter.ToPlanMetres(line.GetEndPoint(1))));
                    }
                    else
                    {
                        nonLinear++;
                    }
                }

                loops.Add(segments);
            }

            return OutlineValidator.Validate(loops, nonLinear);
        }
    }
}
