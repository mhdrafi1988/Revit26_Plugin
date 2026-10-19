namespace Revit26_Plugin.TaperedEpsPattern2D.V002.Core.Pattern
{
    /// <summary>A text label to place at <see cref="Position"/> (board centroid).</summary>
    public sealed class PatternLabel
    {
        public PatternLabel(Vec2 position, string text, string boardId)
        {
            Position = position;
            Text = text;
            BoardId = boardId;
        }

        public Vec2 Position { get; }
        public string Text { get; }
        public string BoardId { get; }
    }
}
