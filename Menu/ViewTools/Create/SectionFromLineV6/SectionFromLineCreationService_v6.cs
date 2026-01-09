using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.PlanSections.ViewModels;

namespace Revit22_Plugin.PlanSections.Services
{
    public class SectionFromLineCreationService_v6
    {
        private readonly Document _doc;
        private readonly ViewPlan _plan;
        private readonly SectionDialogDtlLineViewModel _vm;

        private readonly SectionOrientationService _orientationService;
        private readonly ElementSearchService_v6 _searchService;
        private readonly SectionNamingService _namingService;

        public SectionFromLineCreationService_v6(Document doc, ViewPlan plan, SectionDialogDtlLineViewModel vm)
        {
            _doc = doc;
            _plan = plan;
            _vm = vm;

            _orientationService = new SectionOrientationService();
            _searchService = new ElementSearchService_v6(doc);
            _namingService = new SectionNamingService(doc);
        }

        public void CreateSectionsFromLines(IList<Reference> lineRefs)
        {
            int success = 0, fail = 0;
            int hostHits = 0, linkHits = 0, duplicateCount = 0;

            var createdSections = new List<ViewSection>();
            var linesToDelete = new List<ElementId>();

            using (TransactionGroup tg = new TransactionGroup(_doc, "Create Sections From Lines (v6.5)"))
            {
                tg.Start();

                foreach (Reference r in lineRefs)
                {
                    DetailLine dl = _doc.GetElement(r) as DetailLine;
                    Line line = dl?.GeometryCurve as Line;

                    if (line == null)
                    {
                        fail++;
                        continue;
                    }

                    try
                    {
                        var sec = CreateSingleSection(line, dl, ref hostHits, ref linkHits, ref duplicateCount);

                        if (sec != null)
                        {
                            createdSections.Add(sec);
                            linesToDelete.Add(dl.Id);
                            success++;
                        }
                        else fail++;
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Error", $"Section creation failed:\n{ex.Message}");
                        fail++;
                    }
                }

                if (success > 0) tg.Assimilate();
                else tg.RollBack();

                ShowSummary(success, fail, hostHits, linkHits, duplicateCount);
            }
        }

        private ViewSection CreateSingleSection(
            Line line,
            DetailLine dl,
            ref int hostHits,
            ref int linkHits,
            ref int dupCount)
        {
            double thresholdFt = UnitUtils.ConvertToInternalUnits(_vm.SearchThresholdMm, UnitTypeId.Millimeters);
            double topPadFt = UnitUtils.ConvertToInternalUnits(_vm.TopPaddingMm, UnitTypeId.Millimeters);
            double farClipFt = UnitUtils.ConvertToInternalUnits(_vm.FarClipMm, UnitTypeId.Millimeters);
            double bottom500Ft = UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters);

            // orientation
            var orient = _orientationService.CalculateOrientation(line);
            if (!orient.Success) return null;

            XYZ p0 = orient.StartPoint;
            XYZ p1 = orient.EndPoint;
            XYZ mid = (p0 + p1) / 2.0;
            double halfLen = line.Length / 2.0;

            // ⬇ Collect candidate elements
            var bbAll = _searchService.CollectBounds(mid, mid.Z, thresholdFt, _vm.SelectedSnapSource);

            if (!bbAll.FoundAny)
                return null;

            // -----------------------------------------
            // REAL FIX: Find closest element to the line
            // -----------------------------------------
            var candidates = GetCandidateElements(mid, thresholdFt);

            if (!candidates.Any())
                return null;

            // pick closest element
            var chosen = candidates.OrderBy(c => c.Distance).First();

            double baseZ = chosen.BBox.Min.Z;
            double topZ = chosen.BBox.Max.Z;

            double minZ = baseZ - bottom500Ft;
            double maxZ = topZ + topPadFt;

            double centerZ = (minZ + maxZ) / 2.0;
            double halfHeightFt = (maxZ - minZ) / 2.0;

            // Build transform
            Transform t = Transform.Identity;
            t.Origin = new XYZ(mid.X, mid.Y, centerZ);
            t.BasisX = orient.XDir;
            t.BasisY = orient.YDir;
            t.BasisZ = orient.ZDir;

            BoundingBoxXYZ box = new BoundingBoxXYZ()
            {
                Transform = t,
                Min = new XYZ(-halfLen, -halfHeightFt, -farClipFt),
                Max = new XYZ(halfLen, halfHeightFt, farClipFt)
            };

            using (Transaction tx = new Transaction(_doc, "Create Section View (v6.5)"))
            {
                tx.Start();

                var sec = ViewSection.CreateSection(_doc, _vm.SelectedSectionType.Id, box);
                if (sec == null) return null;

                string baseName = _namingService.GenerateBaseName(
                    _plan, _vm.SectionPrefix, dl.Id, _vm.IncludePlanLevelInName);

                sec.Name = _namingService.EnsureUniqueName(baseName, ref dupCount);

                if (_vm.SelectedTemplate != null)
                    sec.ViewTemplateId = _vm.SelectedTemplate.Id;

                sec.CropBoxActive = true;
                sec.CropBoxVisible = true;

                tx.Commit();
                return sec;
            }
        }

        // ---------------------------------------------
        // 🔥 FIXED OBJECT SELECTION
        // ---------------------------------------------
        private List<(Element Element, BoundingBoxXYZ BBox, double Distance)>
            GetCandidateElements(XYZ mid, double thresholdFt)
        {
            var result = new List<(Element, BoundingBoxXYZ, double)>();

            var collector = new FilteredElementCollector(_doc)
                .WherePasses(new LogicalOrFilter(
                    new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                    new ElementCategoryFilter(BuiltInCategory.OST_Roofs)))
                .WhereElementIsNotElementType();

            foreach (var el in collector)
            {
                var bb = el.get_BoundingBox(null);
                if (bb == null) continue;

                // vertical filtering
                if (Math.Abs(bb.Min.Z - mid.Z) > thresholdFt &&
                    Math.Abs(bb.Max.Z - mid.Z) > thresholdFt) continue;

                // compute horizontal distance
                double dx = Math.Max(bb.Min.X - mid.X, 0.0);
                dx = Math.Max(dx, mid.X - bb.Max.X);

                double dy = Math.Max(bb.Min.Y - mid.Y, 0.0);
                dy = Math.Max(dy, mid.Y - bb.Max.Y);

                double dist = Math.Sqrt(dx * dx + dy * dy);

                result.Add((el, bb, dist));
            }

            return result;
        }

        private void ShowSummary(int success, int fail, int hostHits, int linkHits, int dupCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Section Creation Summary (v6.5)");
            sb.AppendLine($"Success: {success}");
            sb.AppendLine($"Failed:  {fail}");
            TaskDialog.Show("Summary", sb.ToString());
        }
    }
}
