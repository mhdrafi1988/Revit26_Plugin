using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V100.Models;

namespace Revit26_Plugin.Creaser_V100.Services
{
    public class RoofGeometryService
    {
        private readonly Document _doc;
        private readonly ILogService _log;

        private const double ZTolerance = 1e-6;

        public RoofGeometryService(Document document, ILogService log)
        {
            _doc = document;
            _log = log;
        }

        // ------------------------------------------------------------
        // Corner Points
        // ------------------------------------------------------------
        public IList<XYZ> GetCornerPoints(RoofBase roof)
        {
            using (_log.Scope(nameof(RoofGeometryService), "GetCornerPoints"))
            {
                var points = new List<XYZ>();

                Options options = new Options
                {
                    ComputeReferences = false,
                    IncludeNonVisibleObjects = false
                };

                GeometryElement geom = roof.get_Geometry(options);
                if (geom == null)
                {
                    _log.Error(nameof(RoofGeometryService),
                        "Roof geometry is null.");
                    return points;
                }

                foreach (GeometryObject obj in geom)
                {
                    if (obj is Solid solid)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face is PlanarFace pf &&
                                pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                            {
                                foreach (EdgeArray loop in pf.EdgeLoops)
                                {
                                    foreach (Edge edge in loop)
                                    {
                                        XYZ p = edge.AsCurve().GetEndPoint(0);
                                        points.Add(p);
                                        _log.Info(nameof(RoofGeometryService),
                                            $"Corner XYZ: {p}");
                                    }
                                }
                            }
                        }
                    }
                }

                return points;
            }
        }

        // ------------------------------------------------------------
        // Drain Points (Lowest Z)
        // ------------------------------------------------------------
        public IList<XYZ> GetDrainPoints(IEnumerable<XYZ> points)
        {
            using (_log.Scope(nameof(RoofGeometryService), "GetDrainPoints"))
            {
                var list = points.ToList();
                if (!list.Any())
                {
                    _log.Warning(nameof(RoofGeometryService),
                        "No points provided.");
                    return new List<XYZ>();
                }

                double minZ = list.Min(p => p.Z);

                var drains = list
                    .Where(p => Math.Abs(p.Z - minZ) < ZTolerance)
                    .ToList();

                foreach (XYZ drain in drains)
                {
                    _log.Info(nameof(RoofGeometryService),
                        $"Drain XYZ: {drain}");
                }

                return drains;
            }
        }

        // ------------------------------------------------------------
        // Crease Lines (Revit 2026)
        // ------------------------------------------------------------
        public IList<CreaseSegment> GetCreaseSegments(SlabShapeEditor editor)
        {
            using (_log.Scope(nameof(RoofGeometryService), "GetCreaseSegments"))
            {
                var segments = new List<CreaseSegment>();

                if (editor == null)
                {
                    _log.Error(nameof(RoofGeometryService),
                        "SlabShapeEditor is null.");
                    return segments;
                }

                foreach (SlabShapeCrease crease in editor.SlabShapeCreases)
                {
                    Curve curve = crease.Curve;
                    XYZ a = curve.GetEndPoint(0);
                    XYZ b = curve.GetEndPoint(1);

                    segments.Add(new CreaseSegment(a, b));

                    _log.Info(nameof(RoofGeometryService),
                        $"Crease: A={a}, B={b}");
                }

                return segments;
            }
        }
    }
}
