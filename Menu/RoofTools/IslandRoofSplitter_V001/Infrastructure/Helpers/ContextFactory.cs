// IslandRoofSplitter V001 — builds the Revit-free SplitterContext from a picked roof + detail curves.
// Used by the Command and by the handler's Re-pick requests.
#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin_Revised.IslandRoofSplitter.V001.Core.Models;

namespace Revit26_Plugin_Revised.IslandRoofSplitter.V001.Infrastructure.Helpers
{
    public static class ContextFactory
    {
        public static SplitterContext Build(UIDocument uidoc, FootPrintRoof roof, List<DetailCurve> curves)
        {
            Document doc = uidoc.Document;
            var segments = DetailCurveExtractor.Extract(curves, out int nLines, out int nArcs, out int nOther);
            var mesh = RoofSurfaceExtractor.Extract(roof);

            var level = doc.GetElement(roof.LevelId) as Level;
            double offset = roof.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM)?.AsDouble() ?? 0;
            var type = doc.GetElement(roof.GetTypeId()) as RoofType;

            int slopeEdges = 0;
            foreach (ModelCurveArray loop in roof.GetProfiles())
                foreach (ModelCurve mc in loop)
                    if (roof.get_DefinesSlope(mc)) slopeEdges++;

            var parts = new List<string>();
            if (nLines > 0) parts.Add($"{nLines} straight");
            if (nArcs > 0) parts.Add($"{nArcs} arc{(nArcs == 1 ? "" : "s")}");
            if (nOther > 0) parts.Add($"{nOther} other");

            return new SplitterContext
            {
                RoofId = roof.Id.Value,
                RoofTitle = $"{type?.FamilyName ?? "Roof"} : {type?.Name ?? roof.Name}",
                RoofDetail = $"ID {roof.Id.Value} · {level?.Name ?? "no level"} · base offset {offset * LengthUnits.MmPerFoot:0} mm · " +
                             $"{slopeEdges} slope-defining edge{(slopeEdges == 1 ? "" : "s")}" +
                             (mesh.Triangles.Count == 0 ? " · no top faces found" : ""),
                RoofTypeId = roof.GetTypeId().Value,
                LevelId = roof.LevelId.Value,
                BaseOffsetFt = offset,
                Mesh = mesh,
                Segments = segments,
                LinesTitle = $"{curves.Count} detail curve{(curves.Count == 1 ? "" : "s")} ({string.Join(" · ", parts)})",
                LinesDetail = $"View: {uidoc.ActiveView.Name}",
                RoofTypes = new FilteredElementCollector(doc).OfClass(typeof(RoofType)).Cast<RoofType>()
                    .OrderBy(t => t.FamilyName).ThenBy(t => t.Name)
                    .Select(t => new NamedId(t.Id.Value, $"{t.FamilyName} : {t.Name}")).ToList(),
                Levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .Select(l => new NamedId(l.Id.Value, l.Name)).ToList(),
                ShortCurveTolFt = doc.Application.ShortCurveTolerance
            };
        }
    }
}
