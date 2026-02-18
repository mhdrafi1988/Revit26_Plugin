using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlope.V5_00.Core.Models;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Parameters
{
    public interface IParameterWriter
    {
        ParameterWriteResult WriteAll(
            Document doc,
            AutoSlopePayload payload,
            AutoSlopeMetrics metrics);
    }

    public struct ParameterWriteResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public bool HasFailures => FailCount > 0;
    }
}