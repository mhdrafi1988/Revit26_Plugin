// =======================================================
// File: CombinedRoofToolsInitResult.cs
// Location: Core/
// Bag of the 5 tool ViewModels (or a friendly "unavailable" reason per
// tool, when the picked roof can't be used by that particular tool —
// e.g. Auto Slope By Drain requires a FootPrintRoof) built for one
// picked roof. Produced by CombinedRoofToolsInitializer.BuildAll, used
// by both the initial Command and the "Change Roof" ExternalEvent.
// =======================================================

using Revit26_Plugin.AutoSlopeByDrain.V007.UI.ViewModels;
using Revit26_Plugin.CreaserAdv.V009.ViewModels;
using Revit26_Plugin.InnerLoopDivider.V009.UI.ViewModels;
using Revit26_Plugin.InnerLoopsAndPerpendicular.V005.UI.ViewModels;
using Revit26_Plugin.OuterCurveDivider.V004.UI.ViewModels;

namespace Revit26_Plugin.CombinedRoofTools.V001.Core
{
    public class CombinedRoofToolsInitResult
    {
        public string RoofLabel { get; set; }

        public InnerLoopDividerViewModel InnerLoopDividerVm { get; set; }
        public string InnerLoopDividerUnavailableReason { get; set; }

        public InnerLoopsAndPerpendicularViewModel InnerLoopsAndPerpendicularVm { get; set; }
        public string InnerLoopsAndPerpendicularUnavailableReason { get; set; }

        public CurveDividerViewModel OuterCurveDividerVm { get; set; }
        public string OuterCurveDividerUnavailableReason { get; set; }

        public AutoSlopeDrainViewModel AutoSlopeByDrainVm { get; set; }
        public string AutoSlopeByDrainUnavailableReason { get; set; }

        public CreaserAdvViewModel CreaserAdvVm { get; set; }
        public string CreaserAdvUnavailableReason { get; set; }
    }
}
