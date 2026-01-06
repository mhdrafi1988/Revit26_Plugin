using Autodesk.Revit.DB;
using Revit22_Plugin.V4_02.Domain.Models;

namespace Revit22_Plugin.V4_02.Infrastructure.Revit
{
    public class AutoSlopeParameterWriter
    {
        public void WriteResults(
            RoofBase roof,
            SlopeResult result)
        {
            WriteInt(roof, "AS_VerticesModified", result.VerticesModified);
            WriteDouble(roof, "AS_MaxElevationMM", result.MaxElevationMm);
            WriteDouble(roof, "AS_LongestPathM", result.LongestPathMeters);
        }

        private void WriteInt(Element e, string paramName, int value)
        {
            Parameter p = e.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly)
                p.Set(value);
        }

        private void WriteDouble(Element e, string paramName, double value)
        {
            Parameter p = e.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly)
                p.Set(value);
        }
    }
}
