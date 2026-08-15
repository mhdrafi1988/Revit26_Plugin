using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004
{
    /// <summary>
    /// ASSUMPTION (flagged): the GUID list below is a starting point copied from similar
    /// slab/floor-creation tools in the suite. Confirm the actual warning GUIDs you see
    /// during testing on a real model before relying on this — wrong GUIDs mean warnings
    /// you actually want to see could get silently deleted.
    /// </summary>
    public class FloorFailuresPreprocessor : IFailuresPreprocessor
    {
        private static readonly HashSet<Guid> AllowedToResolve = new()
        {
            // TODO: confirm against real warning GUIDs from test models before shipping.
            new Guid("2ba1d33f-1252-4b28-8b91-b62b0857b3aa"), // "Room boundary slightly adjusted" (placeholder)
        };

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failures = failuresAccessor.GetFailureMessages();
            foreach (var failure in failures)
            {
                if (failure.GetSeverity() == FailureSeverity.Warning
                    && AllowedToResolve.Contains(failure.GetFailureDefinitionId().Guid))
                {
                    failuresAccessor.DeleteWarning(failure);
                }
            }
            return FailureProcessingResult.Continue;
        }
    }
}
