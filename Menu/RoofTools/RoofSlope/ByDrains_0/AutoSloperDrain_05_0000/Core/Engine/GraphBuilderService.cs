using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Engine
{
    public class GraphBuilderService
    {
        private readonly double _edgeThresholdFt;

        public GraphBuilderService(double thresholdMeters = 50)
        {
            _edgeThresholdFt = thresholdMeters * 3.28084;
        }

        public Dictionary<int, List<int>> BuildGraph(
            List<SlabShapeVertex> vertices,
            Face topFace)
        {
            var graph = new Dictionary<int, List<int>>();
            int n = vertices.Count;

            for (int i = 0; i < n; i++)
                graph[i] = new List<int>();

            for (int i = 0; i < n; i++)
            {
                XYZ a = vertices[i]?.Position;
                if (a == null) continue;

                for (int j = i + 1; j < n; j++)
                {
                    XYZ b = vertices[j]?.Position;
                    if (b == null) continue;

                    double dist = a.DistanceTo(b);
                    if (dist < 0.5 || dist > _edgeThresholdFt) continue;

                    if (!IsValidEdge(a, b, topFace)) continue;

                    graph[i].Add(j);
                    graph[j].Add(i);
                }
            }

            return graph;
        }

        private bool IsValidEdge(XYZ a, XYZ b, Face face)
        {
            try
            {
                Line ln = Line.CreateBound(a, b);
                double len = a.DistanceTo(b);
                int samples = Math.Max(10, (int)(len * 4));
                double step = 1.0 / samples;

                for (double t = step; t < 1.0; t += step)
                {
                    XYZ p = ln.Evaluate(t, true);
                    if (!GeometryHelper.IsPointOnFace(p, face))
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}