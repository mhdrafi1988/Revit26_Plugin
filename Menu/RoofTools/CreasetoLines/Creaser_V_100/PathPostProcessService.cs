using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V100.Helpers;
using Revit26_Plugin.Creaser_V100.Models;

namespace Revit26_Plugin.Creaser_V100.Services
{
    public class PathPostProcessService
    {
        private readonly View _view;
        private readonly ILogService _log;

        private const double MinLength = 1e-4;

        public PathPostProcessService(View view, ILogService log)
        {
            _view = view;
            _log = log;
        }

        public IList<ProcessedLine> ProcessPaths(
            IList<PathResult> paths)
        {
            using (_log.Scope(nameof(PathPostProcessService), "ProcessPaths"))
            {
                var result = new List<ProcessedLine>();

                foreach (PathResult path in paths)
                {
                    _log.Info(nameof(PathPostProcessService),
                        $"Processing path: Length={path.TotalLength:F3}");

                    for (int i = 0; i < path.PathPoints.Count - 1; i++)
                    {
                        XYZ p3dA = path.PathPoints[i];
                        XYZ p3dB = path.PathPoints[i + 1];

                        // ------------------------------
                        // 1. Project to 2D
                        // ------------------------------
                        XYZ p2dA = ProjectionHelper.ProjectToViewPlane(p3dA, _view);
                        XYZ p2dB = ProjectionHelper.ProjectToViewPlane(p3dB, _view);

                        double length2D = p2dA.DistanceTo(p2dB);
                        if (length2D < MinLength)
                        {
                            _log.Warning(nameof(PathPostProcessService),
                                "Zero-length segment skipped.");
                            continue;
                        }

                        _log.Info(nameof(PathPostProcessService),
                            $"Before ordering: A={p3dA}, B={p3dB}");

                        // ------------------------------
                        // 2. Order by ORIGINAL Z (critical)
                        // ------------------------------
                        XYZ finalP1;
                        XYZ finalP2;
                        double z1;
                        double z2;

                        if (p3dA.Z >= p3dB.Z)
                        {
                            finalP1 = p2dA;
                            finalP2 = p2dB;
                            z1 = p3dA.Z;
                            z2 = p3dB.Z;
                        }
                        else
                        {
                            finalP1 = p2dB;
                            finalP2 = p2dA;
                            z1 = p3dB.Z;
                            z2 = p3dA.Z;
                        }

                        _log.Info(nameof(PathPostProcessService),
                            $"After ordering: P1(Z={z1:F3}), P2(Z={z2:F3})");

                        result.Add(new ProcessedLine(
                            finalP1,
                            finalP2,
                            z1,
                            z2));
                    }
                }

                _log.Info(nameof(PathPostProcessService),
                    $"Final 2D line count = {result.Count}");

                return result;
            }
        }
    }
}
