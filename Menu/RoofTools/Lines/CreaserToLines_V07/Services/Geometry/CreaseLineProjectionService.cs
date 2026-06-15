using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V00_701.Services.Logging;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V00_701.Services.Geometry
{
    public class CreaseLineProjectionService
    {
        private readonly LoggingService _log;

        public CreaseLineProjectionService(LoggingService log)
        {
            _log = log;
        }

        public IList<Line> ProjectToPlan(IList<Line> lines)
        {
            var result = new List<Line>();

            foreach (var l in lines)
            {
                var a = l.GetEndPoint(0);
                var b = l.GetEndPoint(1);
                var z = a.Z > b.Z ? a.Z : b.Z;

                result.Add(Line.CreateBound(
                    new XYZ(a.X, a.Y, z),
                    new XYZ(b.X, b.Y, z)));
            }

            _log.Info($"Projected lines: {result.Count}");
            return result;
        }
    }
}
