// File: RoofBoundaryService.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Services.Geometry
//
// Responsibility:
// - Extracts roof outline curves
// - Uses sketch/footprint when available
// - Falls back to solid geometry edges
// - Returns curves projected to XY plane
//
// IMPORTANT:
// - No transactions
// - Deterministic output

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

using Revit26_Plugin.RoofRidgeLines_V06.Services.Context;

namespace Revit26_Plugin.RoofRidgeLines_V06.Services.Geometry
{
    public class RoofBoundaryService
    {
        private readonly RevitContextService _context;

        public RoofBoundaryService(RevitContextService context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Gets roof boundary curves using the best available method.
        /// </summary>
        public IList<Curve> GetRoofBoundaryCurves(RoofBase roof)
        {
            // Try sketch-based boundary first
            IList<Curve> sketchCurves = TryGetSketchBoundary(roof);
            if (sketchCurves.Any())
                return sketchCurves;

            // Fallback to geometry-based boundary
            return GetGeometryBoundary(roof);
        }

        private IList<Curve> TryGetSketchBoundary(RoofBase roof)
        {
            if (roof is not FootPrintRoof footprintRoof)
                return new List<Curve>();

            ModelCurveArray profile = footprintRoof.GetProfiles();
            List<Curve> result = new();

            foreach (ModelCurve curve in profile)
            {
                result.Add(ProjectCurveToXY(curve.GeometryCurve));
            }

            return result;
        }

        private IList<Curve> GetGeometryBoundary(RoofBase roof)
        {
            List<Curve> curves = new();

            Options options = new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geomElem = roof.get_Geometry(options);
            if (geomElem == null)
                return curves;

            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        foreach (EdgeArray edgeLoop in face.EdgeLoops)
                        {
                            foreach (Edge edge in edgeLoop)
                            {
                                Curve curve = edge.AsCurve();
                                curves.Add(ProjectCurveToXY(curve));
                            }
                        }
                    }
                }
            }

            return curves;
        }

        private Curve ProjectCurveToXY(Curve curve)
        {
            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            return Line.CreateBound(
                new XYZ(p0.X, p0.Y, 0),
                new XYZ(p1.X, p1.Y, 0));
        }
    }
}
