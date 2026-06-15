using Autodesk.Revit.DB;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Models
{
    public class CadImportItem
    {
        public ImportInstance ImportInstance { get; }
        public string DisplayName { get; }

        public CadImportItem(ImportInstance import)
        {
            ImportInstance = import;

            string name =
                import.get_Parameter(BuiltInParameter.IMPORT_SYMBOL_NAME)
                ?.AsString() ?? import.Name;

            string type = import.IsLinked ? "Link" : "Import";

            DisplayName = $"{name} [{type}] (Id {import.Id.Value})";
        }

        public override string ToString() => DisplayName;
    }
}
