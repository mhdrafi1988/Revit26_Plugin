using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V009.Services
{
    /// <summary>
    /// Handles safe cleanup actions after successful creation.
    /// Unchanged from V07.
    /// </summary>
    public class PostCreationCleanupService
    {
        private readonly Document _doc;

        public PostCreationCleanupService(Document doc)
        {
            _doc = doc;
        }

        public void DeleteDetailLines(
            IList<ElementId> lineIds,
            bool allowDelete)
        {
            if (!allowDelete || lineIds.Count == 0)
                return;

            using Transaction tx = new(_doc, "Delete Source Detail Lines");
            tx.Start();
            _doc.Delete(lineIds);
            tx.Commit();
        }
    }
}
