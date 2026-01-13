using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.SectionPlacement
{
    public static class SectionUtils
    {
        public static List<ViewSection> CollectUnplacedSectionsInPlan(Document doc, View activeView)
        {
            List<ViewSection> sections = new List<ViewSection>();

            var markers = new FilteredElementCollector(doc, activeView.Id)
                            .OfCategory(BuiltInCategory.OST_Viewers)
                            .ToElements();

            foreach (var marker in markers)
            {
                var dependents = marker.GetDependentElements(null);
                foreach (ElementId id in dependents)
                {
                    if (doc.GetElement(id) is ViewSection vs && !IsPlaced(doc, vs))
                        sections.Add(vs);
                }
            }

            var viewsInPlan = new FilteredElementCollector(doc, activeView.Id)
                                .OfClass(typeof(ViewSection))
                                .Cast<ViewSection>()
                                .Where(v => !IsPlaced(doc, v));

            sections.AddRange(viewsInPlan);
            return sections.Distinct(new ElementIdComparer<ViewSection>()).ToList();
        }

        public static List<ViewSection> CollectUnplacedSectionsInProject(Document doc)
        {
            return new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSection))
                        .Cast<ViewSection>()
                        .Where(v => !IsPlaced(doc, v))
                        .ToList();
        }

        public static bool IsPlaced(Document doc, View view)
        {
            return new FilteredElementCollector(doc)
                        .OfClass(typeof(Viewport))
                        .Cast<Viewport>()
                        .Any(vp => vp.ViewId == view.Id);
        }

        public static void PlaceSectionsOnSheets(Document doc, List<ViewSection> sections, SectionPlacementViewModel vm)
        {
            int capacity = vm.Rows * vm.Columns;
            int sectionIndex = 0;

            foreach (var sheet in vm.SelectedSheets)
            {
                for (int r = 0; r < vm.Rows; r++)
                {
                    for (int c = 0; c < vm.Columns; c++)
                    {
                        if (sectionIndex >= sections.Count) return;

                        PlaceOneSection(doc, sections[sectionIndex], sheet, vm, r, c);
                        sectionIndex++;
                    }
                }
            }

            if (sectionIndex < sections.Count && vm.AutoCreateNewSheets)
            {
                while (sectionIndex < sections.Count)
                {
                    ViewSheet newSheet = ViewSheet.Create(doc, vm.SelectedTitleBlock.Id);

                    for (int r = 0; r < vm.Rows; r++)
                    {
                        for (int c = 0; c < vm.Columns; c++)
                        {
                            if (sectionIndex >= sections.Count) return;

                            PlaceOneSection(doc, sections[sectionIndex], newSheet, vm, r, c);
                            sectionIndex++;
                        }
                    }
                }
            }
        }

        private static void PlaceOneSection(Document doc, ViewSection vs, ViewSheet sheet, SectionPlacementViewModel vm, int row, int col)
        {
            if (vm.SelectedViewTemplate != null)
                vs.ViewTemplateId = vm.SelectedViewTemplate.Id;

            XYZ location = new XYZ(col * vm.XGap, -row * vm.YGap, 0);
            Viewport.Create(doc, sheet.Id, vs.Id, location);
        }
    }

    public class ElementIdComparer<T> : IEqualityComparer<T> where T : Element
    {
        public bool Equals(T x, T y) => x.Id == y.Id;
        public int GetHashCode(T obj) => obj.Id.IntegerValue;
    }
}
