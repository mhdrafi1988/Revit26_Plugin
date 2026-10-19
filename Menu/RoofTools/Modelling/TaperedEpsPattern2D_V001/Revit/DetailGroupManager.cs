using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V001.Revit
{
    /// <summary>
    /// Finds, deletes and creates the result detail group. The "EPS2D_&lt;roofId&gt;_&lt;viewId&gt;" name is the
    /// GroupType name; deleting the type removes every instance. Call inside an open transaction.
    /// </summary>
    public static class DetailGroupManager
    {
        public static GroupType? FindType(Document doc, string name) =>
            new FilteredElementCollector(doc)
                .OfClass(typeof(GroupType))
                .Cast<GroupType>()
                .FirstOrDefault(t => t.Name == name);

        /// <summary>Deletes the group type with this name (and its instances). Returns the number of instances removed.</summary>
        public static int DeleteExisting(Document doc, string name)
        {
            GroupType? type = FindType(doc, name);
            if (type == null)
            {
                return 0;
            }

            int instances = type.Groups.Size;
            doc.Delete(type.Id);
            return instances;
        }

        /// <summary>Groups the elements; when the name is taken a suffix _2, _3 … is added.</summary>
        public static Group Create(Document doc, ICollection<ElementId> elementIds, string name)
        {
            Group group = doc.Create.NewGroup(elementIds);
            string unique = name;
            for (int n = 2; FindType(doc, unique) != null; n++)
            {
                unique = $"{name}_{n}";
            }

            group.GroupType.Name = unique;
            return group;
        }
    }
}
