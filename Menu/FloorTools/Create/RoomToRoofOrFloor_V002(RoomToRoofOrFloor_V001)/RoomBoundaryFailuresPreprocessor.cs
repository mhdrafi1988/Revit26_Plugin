using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoomToRoofOrFloor.V002.Core.Services
{
    /// <summary>
    /// Suppresses expected, non-critical warnings raised when creating a
    /// roof/floor from a room boundary (e.g. "not fully contained",
    /// room-bounding warnings). Only Warning-severity messages are
    /// swallowed — true errors still roll back the SubTransaction so
    /// the floor fallback can run.
    /// </summary>
    public class RoomBoundaryFailuresPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            foreach (var failure in failuresAccessor.GetFailureMessages())
            {
                if (failure.GetSeverity() == FailureSeverity.Warning)
                    failuresAccessor.DeleteWarning(failure);
            }

            return FailureProcessingResult.Continue;
        }
    }
}
