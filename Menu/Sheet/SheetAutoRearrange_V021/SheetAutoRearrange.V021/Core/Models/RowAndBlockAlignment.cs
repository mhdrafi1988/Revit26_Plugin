namespace Revit26_Plugin.SheetAutoRearrange.V021.Core.Models
{
    /// <summary>Per-row alignment for Sheet Order Rearrange — how shorter views in a row align against the row's tallest view.</summary>
    public enum RowAlignment { Top, Center, Bottom }

    /// <summary>Whole-block horizontal alignment against the titleblock usable area (Sheet Order Rearrange).</summary>
    public enum BlockAlignmentH { Left, Center, Right }

    /// <summary>Whole-block vertical alignment against the titleblock usable area (Sheet Order Rearrange).</summary>
    public enum BlockAlignmentV { Top, Center, Bottom }
}
