namespace Revit26_Plugin.Creaser_V08.Commands.Models
{
    public class CornerSummary
    {
        public int TotalCorners { get; }

        public CornerSummary(int totalCorners)
        {
            TotalCorners = totalCorners;
        }
    }
}
