namespace Revit26_Plugin.SectionManager_V07.Models
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
