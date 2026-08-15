namespace Revit26_Plugin.SectionViewAutoTagger.V003
{
    /// <summary>
    /// Global leader-end condition applied to every tag placed in a Run.
    /// Maps to Revit's LeaderEndCondition enum (Autodesk.Revit.DB) when
    /// applied to the created IndependentTag in the engine.
    /// Confirmed: global setting (not per-category) — default is Free.
    /// </summary>
    public enum LeaderEndCondition
    {
        Free,
        Attached
    }
}
