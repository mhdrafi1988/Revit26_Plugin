using Autodesk.Revit.DB;

namespace Revit26_Plugin.DtlLineDim_V03.Services
{
    public static class ViewValidationService
    {
        public static bool ValidatePlanView(View view, out string reason)
        {
            if (view == null)
            {
                reason = "No active view.";
                return false;
            }

            if (view.ViewType != ViewType.FloorPlan &&
                view.ViewType != ViewType.CeilingPlan &&
                view.ViewType != ViewType.EngineeringPlan)
            {
                reason = "Active view is not a Plan View.";
                return false;
            }

            reason = "Valid Plan View detected.";
            return true;
        }
    }
}
