using Autodesk.Revit.DB;
using System.Collections.Generic;
using Revit26_Plugin.SectionManager.V008.Models;

namespace Revit26_Plugin.SectionManager.V008.Services
{
    public class SectionRenameService
    {
        public RenameResult Rename(Document doc, IEnumerable<SectionInfo> sections)
        {
            int renamed = 0;

            using (Transaction tx = new Transaction(doc, "Rename Sections"))
            {
                tx.Start();

                foreach (var s in sections)
                {
                    var view = doc.GetElement(s.ElementId) as ViewSection;
                    if (view == null) continue;

                    if (view.Name != s.NewName)
                    {
                        view.Name = s.NewName;
                        renamed++;
                    }
                }

                tx.Commit();
            }

            return new RenameResult(renamed);
        }
    }
}
