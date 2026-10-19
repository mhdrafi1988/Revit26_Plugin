using System;
using System.Collections.Generic;

namespace Revit26_Plugin.TaperedEpsPattern2D.V002.Core.Pattern
{
    /// <summary>Grid lines and boards of all slope faces in "each edge" mode.</summary>
    public sealed class EdgeGridSet
    {
        public IReadOnlyList<PatternSegment> GridLines { get; init; } = Array.Empty<PatternSegment>();
        public IReadOnlyList<BoardPiece> Boards { get; init; } = Array.Empty<BoardPiece>();
    }
}
