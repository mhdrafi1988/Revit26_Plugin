namespace Revit26_Plugin.ParaManager.V001.Models
{
    /// <summary>
    /// The two supported Revit shared-parameter binding types.
    /// Chosen once per Run click and applied to every parameter in the batch.
    /// </summary>
    public enum BindingChoice
    {
        Instance,
        Type
    }

    /// <summary>
    /// Outcome summary of one Run — feeds the completion summary line
    /// ("X assigned | Y skipped | Z failed") and the final log entry.
    /// </summary>
    public class AssignmentRunResult
    {
        public int AssignedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
    }
}
