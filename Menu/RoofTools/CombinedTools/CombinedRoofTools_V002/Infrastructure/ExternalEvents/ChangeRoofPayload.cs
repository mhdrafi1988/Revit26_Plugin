using Revit26_Plugin.CombinedRoofTools.V002.Core;
using System;

namespace Revit26_Plugin.CombinedRoofTools.V002.Infrastructure.ExternalEvents
{
    public class ChangeRoofPayload
    {
        public Action<CombinedRoofToolsInitResult> OnCompleted { get; set; }
        public Action OnCancelled { get; set; }
        public Action<string> OnFailed { get; set; }
    }
}
