using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Core.Services
{
    /// <summary>Groups newly-drawn detail lines, resolving duplicate group names with an incremental numeric suffix. Must run inside an open Transaction.</summary>
    public static class GroupCreationService
    {
        public static Group CreateGroup(Document doc, List<ElementId> elementIds, string desiredName, out string finalName)
        {
            finalName = ResolveUniqueName(doc, desiredName);

            Group group = doc.Create.NewGroup(elementIds);
            RenameGroupType(group, finalName);

            return group;
        }

        private static string ResolveUniqueName(Document doc, string desiredName)
        {
            string baseName = string.IsNullOrWhiteSpace(desiredName) ? "ClosedLoop Group" : desiredName.Trim();

            var existingNames = new HashSet<string>(
                new FilteredElementCollector(doc).OfClass(typeof(GroupType)).Cast<GroupType>().Select(gt => gt.Name),
                StringComparer.OrdinalIgnoreCase);

            if (!existingNames.Contains(baseName))
                return baseName;

            int suffix = 2;
            string candidate;
            do
            {
                candidate = $"{baseName} {suffix}";
                suffix++;
            } while (existingNames.Contains(candidate));

            return candidate;
        }

        private static void RenameGroupType(Group group, string name)
        {
            try
            {
                group.GroupType.Name = name;
            }
            catch (Exception)
            {
                // Another GroupType claimed this name between ResolveUniqueName and here — fall back to a short unique tail.
                group.GroupType.Name = $"{name} {Guid.NewGuid().ToString("N").Substring(0, 4)}";
            }
        }
    }
}
