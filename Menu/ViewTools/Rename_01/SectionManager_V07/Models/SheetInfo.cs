namespace Revit26_Plugin.SectionManager_V07.Models
{
    public class SheetInfo
    {
        public string SheetNumber { get; }
        public string SheetName { get; }

        public SheetInfo(string number, string name)
        {
            SheetNumber = number;
            SheetName = name;
        }
    }
}
