using Autodesk.Revit.DB;

namespace BatchDwgFamilyLinker.V002.Services
{
    public static class FamilySaveService
    {
        public static void Save(Document famDoc)
        {
            if (!famDoc.IsReadOnly)
                famDoc.Save();
        }
    }
}
