// File: WizardState.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Models
//
// Responsibility:
// - Represents the high-level wizard execution state
// - Used for UI flow control and execution safety
// - Contains NO Revit API references

namespace Revit26_Plugin.RoofRidgeLines_V06.Models
{
    /// <summary>
    /// Defines the lifecycle states of the wizard.
    /// </summary>
    public enum WizardState
    {
        /// <summary>
        /// Wizard initialized but no action taken yet.
        /// </summary>
        Idle,

        /// <summary>
        /// Roof has been selected and validated.
        /// </summary>
        RoofValidated,

        /// <summary>
        /// Two valid points have been picked on the roof.
        /// </summary>
        PointsPicked,

        /// <summary>
        /// Execution is currently running.
        /// </summary>
        Executing,

        /// <summary>
        /// Execution completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// User cancelled the process.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Execution failed due to an error.
        /// </summary>
        Failed
    }
}
