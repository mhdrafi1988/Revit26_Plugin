namespace Revit26_Plugin.SectionManager.V008.Models
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
