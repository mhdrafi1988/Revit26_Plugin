namespace Revit26_Plugin.TaperedEpsPattern2D.V006.Core.Pattern
{
    public enum BoardShape
    {
        /// <summary>Separate length (along the row) and width (up the slope).</summary>
        Rectangular,

        /// <summary>Square boards: one size; length = width. Laying is chosen with <see cref="SquarePattern"/>.</summary>
        Square
    }
}
