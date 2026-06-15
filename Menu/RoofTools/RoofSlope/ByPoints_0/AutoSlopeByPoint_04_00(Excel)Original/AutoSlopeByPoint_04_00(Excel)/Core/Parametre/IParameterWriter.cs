using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Models;

namespace Revit26_Plugin.AutoSlopeByPoint_04.Core.Parameters
{
    public interface IParameterWriter
    {
        ParameterWriteResult WriteAll(
            Document doc,
            RoofBase roof,
            AutoSlopePayload data,
            double highestElevation_mm,
            double longestPath_m,
            int processed,
            int skipped,
            int runDuration_sec);
    }

    public struct ParameterWriteResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public bool HasFailures => FailCount > 0;
    }
}