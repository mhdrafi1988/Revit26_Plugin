namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V002.Core.Pattern
{
    /// <summary>One board (full or cut) of the grid, with its thickness range.</summary>
    public sealed record BoardPiece
    {
        public string Id { get; init; } = string.Empty;
        public int Row { get; init; }
        public int Column { get; init; }
        public Polygon2D Polygon { get; init; } = Polygon2D.Empty;
        public bool IsFull { get; init; }
        public double MinThicknessMm { get; init; }
        public double MaxThicknessMm { get; init; }
        public string Code { get; init; } = string.Empty;

        /// <summary>Roof edge whose grid this board belongs to ("each edge" mode), otherwise null.</summary>
        public int? EdgeIndex { get; init; }
    }
}
