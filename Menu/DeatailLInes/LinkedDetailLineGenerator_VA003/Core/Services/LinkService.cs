using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// Queries the host document for placed RevitLinkInstances, and queries each
    /// linked document for available Category → Family → Type hierarchy. Phase 3
    /// scope: Floors, Roofs (Profile group), Walls, Structural Framing (Linear
    /// group). Point group (Columns, point-based families) arrives in Phase 4.
    /// </summary>
    public class LinkService
    {
        /// <summary>Returns all RevitLinkInstance elements in the host document,
        /// wrapped as LinkedModelItem for UI binding. Unloaded links are included
        /// (IsLoaded = false) so they show in the list but stay unselectable.</summary>
        public List<LinkedModelItem> GetLinkedModels(Document hostDoc, Action<string>? onLog = null)
        {
            var result = new List<LinkedModelItem>();

            var linkInstances = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            onLog?.Invoke($"Found {linkInstances.Count} RevitLinkInstance element(s) in host document");

            foreach (var li in linkInstances)
            {
                Document? linkedDoc = null;
                try
                {
                    linkedDoc = li.GetLinkDocument();
                }
                catch (Exception ex)
                {
                    onLog?.Invoke($"Link instance {li.Id.Value} — failed to access linked document: {ex.Message}");
                }

                string docTitle = linkedDoc?.Title ?? (li.Name ?? "Unknown");
                string instanceName = li.Name ?? docTitle;

                result.Add(new LinkedModelItem
                {
                    LinkInstanceId = li.Id.Value,
                    InstanceName = instanceName,
                    DocumentTitle = docTitle.EndsWith(".rvt") ? docTitle : docTitle + ".rvt",
                    IsLoaded = linkedDoc != null
                });
            }

            return result;
        }

        /// <summary>
        /// Builds the merged Category → Family → Type tree for all currently-loaded
        /// links whose LinkInstanceId is in selectedLinkIds. Phase 3 scans Floors,
        /// Roofs (Profile group), Walls, Structural Framing (Linear group). Point
        /// group (Columns, point-based families) is added in Phase 4 without
        /// changing this method's shape.
        /// </summary>
        public List<LinkTreeNode> BuildElementTree(
            Document hostDoc,
            IEnumerable<long> selectedLinkIds,
            Action<string>? onLog = null)
        {
            var nodes = new List<LinkTreeNode>();
            var selectedSet = selectedLinkIds.ToHashSet();

            var linkInstances = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(li => selectedSet.Contains(li.Id.Value))
                .ToList();

            foreach (var li in linkInstances)
            {
                Document? linkedDoc;
                try
                {
                    linkedDoc = li.GetLinkDocument();
                }
                catch
                {
                    continue;
                }
                if (linkedDoc == null) continue;

                var node = new LinkTreeNode
                {
                    LinkInstanceId = li.Id.Value,
                    LinkDisplayName = (li.Name ?? linkedDoc.Title) + (linkedDoc.Title.EndsWith(".rvt") ? "" : ".rvt")
                };

                node.Categories.Add(BuildCategoryNode(linkedDoc, BuiltInCategory.OST_Floors, "Floors", RepresentationGroup.Profile, onLog));
                node.Categories.Add(BuildCategoryNode(linkedDoc, BuiltInCategory.OST_Roofs, "Roofs", RepresentationGroup.Profile, onLog));

                // Phase 3: Linear group — Walls and Structural Framing (Beams).
                // "Other reliable LocationCurve elements" (spec Section 18) is left
                // for a future phase rather than guessing which additional categories
                // Rafi wants swept in — Walls/Beams cover the two explicitly named
                // V1 categories.
                node.Categories.Add(BuildCategoryNode(linkedDoc, BuiltInCategory.OST_Walls, "Walls", RepresentationGroup.Linear, onLog));
                node.Categories.Add(BuildCategoryNode(linkedDoc, BuiltInCategory.OST_StructuralFraming, "Structural Framing", RepresentationGroup.Linear, onLog));

                // Phase 4: Point group — Structural Columns, Architectural Columns,
                // Mechanical Equipment. Mechanical is included here for selection
                // purposes only; its actual per-element representation (Point vs
                // Linear) is determined at PROCESSING time by
                // ElementLocationClassifier, per spec Section 21 — an element showing
                // up under this category in the tree is not a guarantee it will be
                // rendered as a point marker.
                node.Categories.Add(BuildCategoryNode(linkedDoc, BuiltInCategory.OST_StructuralColumns, "Structural Columns", RepresentationGroup.Point, onLog));
                node.Categories.Add(BuildCategoryNode(linkedDoc, BuiltInCategory.OST_Columns, "Columns", RepresentationGroup.Point, onLog));
                node.Categories.Add(BuildCategoryNode(linkedDoc, BuiltInCategory.OST_MechanicalEquipment, "Mechanical Equipment", RepresentationGroup.Point, onLog));

                // Only keep categories that actually had matching Types in this link
                node.Categories = new System.Collections.ObjectModel.ObservableCollection<CategoryTreeItem>(
                    node.Categories.Where(c => c.Families.Count > 0));

                if (node.Categories.Count > 0)
                    nodes.Add(node);
            }

            return nodes;
        }

        private CategoryTreeItem BuildCategoryNode(
            Document linkedDoc, BuiltInCategory bic, string displayName,
            RepresentationGroup group, Action<string>? onLog)
        {
            var catItem = new CategoryTreeItem { CategoryName = displayName, Group = group };

            List<Element> instances;
            try
            {
                instances = new FilteredElementCollector(linkedDoc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .ToList();
            }
            catch (Exception ex)
            {
                onLog?.Invoke($"Failed to collect {displayName} from linked doc '{linkedDoc.Title}': {ex.Message}");
                return catItem;
            }

            onLog?.Invoke($"{displayName}: {instances.Count} instance(s) found in '{linkedDoc.Title}'");

            // Group by (FamilyName, TypeName) — one TypeTreeItem per distinct type,
            // one FamilyTreeItem per distinct family name.
            //
            // System families (Wall, Structural Framing when hosted as system types)
            // have no SYMBOL_FAMILY_NAME_PARAM — WallType exposes no family symbol,
            // so those fall back to the category name as a single family bucket
            // (e.g. all wall types grouped under "Walls"), matching how Revit's own
            // Project Browser groups system-family types. This is intentional, not
            // an oversight — loadable families (most floors/roofs, and beams that
            // use structural framing families) still group by their true family name.
            var familyGroups = instances
                .Select(e => new
                {
                    Element = e,
                    TypeId = e.GetTypeId(),
                })
                .Where(x => x.TypeId != ElementId.InvalidElementId)
                .Select(x => new
                {
                    x.TypeId,
                    TypeElem = linkedDoc.GetElement(x.TypeId)
                })
                .Where(x => x.TypeElem != null)
                .GroupBy(x => x.TypeElem!.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)?.AsString()
                              ?? x.TypeElem!.Category?.Name ?? "Unknown Family")
                .ToList();

            foreach (var famGroup in familyGroups)
            {
                var famItem = new FamilyTreeItem { FamilyName = famGroup.Key };

                var distinctTypes = famGroup
                    .GroupBy(x => x.TypeId.Value)
                    .Select(g => g.First())
                    .ToList();

                foreach (var t in distinctTypes)
                {
                    famItem.Types.Add(new TypeTreeItem
                    {
                        TypeId = t.TypeId.Value,
                        TypeName = t.TypeElem!.Name
                    });
                }

                if (famItem.Types.Count > 0)
                    catItem.Families.Add(famItem);
            }

            return catItem;
        }
    }
}
