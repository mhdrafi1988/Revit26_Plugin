namespace Revit26_Plugin.SectionManager.V008.Models
{
    public class RenameResult
    {
        public int RenamedCount { get; }

        public RenameResult(int renamed)
        {
            RenamedCount = renamed;
        }
    }
}
