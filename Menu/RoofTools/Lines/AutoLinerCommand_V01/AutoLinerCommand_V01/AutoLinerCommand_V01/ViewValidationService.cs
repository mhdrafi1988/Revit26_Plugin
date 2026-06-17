using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoLiner_V01.Services
{
    public static class ViewValidationService
    {
        public static bool IsValidPlanView(
            View view,
            out string reason)
        {
            if (view == null)
            {
                reason = "No active view.";
                return false;
            }

            if (view.ViewType != ViewType.FloorPlan &&
                view.ViewType != ViewType.EngineeringPlan &&
                view.ViewType != ViewType.CeilingPlan)
            {
                reason = "Active view is not a Plan View.";
                return false;
            }

            reason = "Valid Plan View detected.";
            return true;
        }
    }
}
