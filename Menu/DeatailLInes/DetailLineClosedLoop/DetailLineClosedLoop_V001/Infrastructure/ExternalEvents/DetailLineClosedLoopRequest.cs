namespace Revit26_Plugin.DetailLineClosedLoop.V001.Infrastructure.ExternalEvents
{
    /// <summary>Identifies which Revit-side action the external event handler should perform.</summary>
    public enum DetailLineClosedLoopRequest
    {
        None,
        SelectLines,
        Run,
        DeleteSelectedLines,
        RefreshCreatedLines
    }
}
