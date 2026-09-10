using Revit26_Plugin.CombinedRoofTools.V001.Core;
using System;

namespace Revit26_Plugin.CombinedRoofTools.V001.Infrastructure.ExternalEvents
{
    public class ChangeRoofPayload
    {
        public Action<CombinedRoofToolsInitResult> OnCompleted { get; set; }
        public Action OnCancelled { get; set; }
        public Action<string> OnFailed { get; set; }
    }
}
