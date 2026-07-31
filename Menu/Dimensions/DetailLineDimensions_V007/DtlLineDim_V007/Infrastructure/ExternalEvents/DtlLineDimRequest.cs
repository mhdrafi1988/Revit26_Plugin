namespace Revit26_Plugin.DtlLineDim.V007.Infrastructure.ExternalEvents
{
    /// <summary>
    /// Identifies which Revit-side action the external event handler should perform.
    /// </summary>
    public enum DtlLineDimRequest
    {
        None,
        Initialize,
        GenerateDimensions
    }
}
