using Autodesk.Revit.DB;
using Revit26_Plugin.AutoLiner_V04.Services;
using Revit26_Plugin.Creaser_V32.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V32.Services
{
    public class RoofGeometryService
    {
        private readonly UiLogService _log;

        public RoofGeometryService(UiLogService log)
        {
            _log = log;
        }

        public RoofGeometryData Extract(Document doc, RoofBase roof)
        {
            _log.Write("Extracting roof geometry…");

            var shapeMgr = roof.GetSlabShapeEditor();
            var vertices = shapeMgr.SlabShapeVertices.Select(v => v.Position).ToList();
            _log.Write($"Shape vertices: {vertices.Count}");

            var creases = shapeMgr.SlabShapeCreases
                .Select(c => c.Curve)
                .ToList();

            _log.Write($"Creases: {creases.Count}");

            var footprint = roof.GetProfiles()
                .First()
                .Select(c => c.GetEndPoint(0))
                .ToList();

            return new RoofGeometryData(vertices, footprint, creases);
        }
    }

    public record RoofGeometryData(
        List<XYZ> ShapePoints,
        List<XYZ> CornerPoints,
        List<Curve> Creases);
}
