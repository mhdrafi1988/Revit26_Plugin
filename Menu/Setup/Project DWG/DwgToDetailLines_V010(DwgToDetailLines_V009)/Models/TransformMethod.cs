namespace Revit26_Plugin.DwgToDetailLines.V010.Models
{
    /// <summary>
    /// TEMPORARY DIAGNOSTIC (V008) — lets Rafi try each candidate transform
    /// composition against a real DWG in Revit and see which one lands
    /// converted geometry exactly on the source linework. Once the correct
    /// method is confirmed, this enum, the radio-button UI, and the
    /// branching in CadGeometryExtractor should be removed and the winning
    /// method hard-coded — this is not meant to ship as a permanent option.
    /// </summary>
    public enum TransformMethod
    {
        /// <summary>gi.Transform alone — no ImportInstance-level transform composed.</summary>
        None,

        /// <summary>import.GetTransform().Multiply(gi.Transform) — basic placement transform.</summary>
        GetTransform,

        /// <summary>import.GetTotalTransform().Multiply(gi.Transform) — placement + true north.</summary>
        GetTotalTransform
    }
}
