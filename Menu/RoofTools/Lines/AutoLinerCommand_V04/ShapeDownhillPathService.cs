using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoLiner_V04.Services
{
    public class ShapeDownhillPathService
    {
        private readonly UiLogService _log;
        private const double ZTol = 1e-4;

        public ShapeDownhillPathService(UiLogService log)
        {
            _log = log;
        }

        public IList<IList<XYZ>> GeneratePaths(SlabShapeEditor editor)
        {
            _log.Info("Reading slab shape vertices");

            var vertices =
                editor.SlabShapeVertices
                    .Cast<SlabShapeVertex>()
                    .Select(v => v.Position)
                    .ToList();

            _log.Info($"Vertices count: {vertices.Count}");

            var adjacency =
                vertices.ToDictionary(v => v, _ => new List<XYZ>());

            foreach (SlabShapeCrease crease in editor.SlabShapeCreases)
            {
                if (crease.EndPoints.Size != 2)
                    continue;

                XYZ p0 = crease.EndPoints.get_Item(0).Position;
                XYZ p1 = crease.EndPoints.get_Item(1).Position;

                adjacency[p0].Add(p1);
                adjacency[p1].Add(p0);
            }

            double minZ = vertices.Min(v => v.Z);

            var drains =
                vertices.Where(v => Math.Abs(v.Z - minZ) < ZTol).ToHashSet();

            _log.Info($"Drain points: {drains.Count}");

            var sources =
                adjacency
                    .Where(kv => kv.Value.All(n => n.Z < kv.Key.Z - ZTol))
                    .Select(kv => kv.Key)
                    .ToList();

            _log.Info($"Source peaks: {sources.Count}");

            var result = new List<IList<XYZ>>();

            foreach (XYZ source in sources)
            {
                var path = new List<XYZ> { source };
                XYZ current = source;

                while (!drains.Contains(current))
                {
                    XYZ next =
                        adjacency[current]
                            .Where(n => n.Z < current.Z - ZTol)
                            .OrderByDescending(n => current.Z - n.Z)
                            .ThenBy(n => current.DistanceTo(n))
                            .FirstOrDefault();

                    if (next == null || path.Any(p => p.IsAlmostEqualTo(next)))
                        break;

                    path.Add(next);
                    current = next;
                }

                if (path.Count > 1)
                {
                    result.Add(path);
                    _log.Info($"Path created ({path.Count} points)");
                }
            }

            _log.Info($"Total paths generated: {result.Count}");
            return result;
        }
    }
}
