// File: IParameterWriter.cs
// Location: Revit26_Plugin.Asd_19.Core.Parameters

using Autodesk.Revit.DB;
using Revit26_Plugin.Asd_19.Models;

namespace Revit26_Plugin.Asd_19.Core.Parameters
{
    public interface IParameterWriter
    {
        ParameterWriteResult WriteAll(
            Document doc,
            RoofBase roof,
            DrainExportMetrics metrics,
            double slopePercent,
            double thresholdMeters,
            System.Action<string> logAction = null);
    }

    public struct ParameterWriteResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public bool HasFailures => FailCount > 0;
    }
}