using Autodesk.Revit.DB;
using Revit26_Plugin.RoofPointComparison.V001.Core.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofPointComparison.V001.Core.Engine
{
    /// <summary>
    /// Reads shape-editing points from a roof's SlabShapeEditor. Read-only — never
    /// enables shape editing on the roof, since this tool must not mutate either
    /// roof it is comparing. A roof with shape editing not enabled simply has 0
    /// shape-editing points.
    /// </summary>
    public static class RoofPointExtractor
    {
        public static List<RoofPointSample> Extract(RoofBase roof, Action<LogEntry> log)
        {
            var samples = new List<RoofPointSample>();
            if (roof == null) return samples;

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null || !editor.IsValidObject)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    $"'{roof.Name}': slab shape editor is not available — treated as 0 shape-editing points."));
                return samples;
            }

            if (!editor.IsEnabled)
            {
                log?.Invoke(new LogEntry(LogLevel.Info,
                    $"'{roof.Name}': shape editing is not enabled — treated as 0 shape-editing points."));
                return samples;
            }

            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
            {
                samples.Add(new RoofPointSample
                {
                    Position = v.Position,
                    ElevationMm = UnitUtils.ConvertFromInternalUnits(v.Position.Z, UnitTypeId.Millimeters)
                });
            }

            return samples;
        }
    }
}
