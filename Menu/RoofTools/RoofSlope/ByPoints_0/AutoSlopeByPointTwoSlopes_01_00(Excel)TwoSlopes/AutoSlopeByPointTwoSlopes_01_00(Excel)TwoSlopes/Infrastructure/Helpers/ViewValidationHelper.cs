using Autodesk.Revit.DB;

namespace AutoSlopeByPointTwoSlopes_01_00.Infrastructure.Helpers
{
    public static class ViewValidationHelper
    {
        /// <summary>
        /// Checks if the active view is a Plan View type.
        /// </summary>
        /// <param name="view">The view to validate</param>
        /// <returns>True if the view is a Plan View, false otherwise</returns>
        public static bool IsPlanView(View view)
        {
            if (view == null)
                return false;

            foreach (ViewType planType in AppConstants.PlanViewTypes)
            {
                if (view.ViewType == planType)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a user-friendly description of why a view is invalid.
        /// </summary>
        public static string GetInvalidViewMessage(View view)
        {
            if (view == null)
                return "No active view found.";

            return $"Current view type is '{view.ViewType}'. " +
                   $"AutoSlope requires a Plan View (Floor Plan, Ceiling Plan, Area Plan, or Engineering Plan).";
        }
    }
}