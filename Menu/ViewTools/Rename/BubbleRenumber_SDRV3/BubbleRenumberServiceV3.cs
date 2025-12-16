using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Revit22_Plugin.SDRV3.Services;

namespace Revit22_Plugin.SDRV3.Renumber
{
    public static class BubbleRenumberServiceV4
    {
        public static RenumberSummaryV3 Run(Document doc, ViewSheet sheet, int startNum, double thresholdFt)
        {
            var summary = new RenumberSummaryV3
            {
                SheetName = sheet.Name,
                SheetNumber = sheet.SheetNumber
            };

            var items = CollectSections(doc, sheet);
            if (!items.Any())
            {
                summary.LogMessage = "No section views found on the sheet.";
                return summary;
            }

            // preload existing used numbers from sheet
            var used = CollectExistingNumbers(doc, sheet);

            // PERFECT ROW ORDERING (BOTTOM ALIGNMENT)
            var ordered = OrderByRows(items, thresholdFt);

            using (Transaction t = new Transaction(doc, "Renumber Sections V3"))
            {
                t.Start();

                int current = startNum;

                foreach (var e in ordered)
                {
                    string baseNum = current.ToString();
                    string uniq = MakeUnique(baseNum, used);

                    try
                    {
                        AssignDetailNumber(e.View, uniq);
                        summary.Success++;
                        summary.LogMessage += $"{e.View.Name} → {uniq}\n";
                    }
                    catch (Exception ex)
                    {
                        string fail = $"{e.View.Name}: {ex.Message}";
                        summary.Failed.Add(fail);
                        summary.LogMessage += fail + "\n";
                    }

                    current++;
                }

                summary.Total = items.Count;
                t.Commit();
            }

            return summary;
        }


        // --------------------------------------------------------------------
        // GET EXISTING DETAIL NUMBERS IN SHEET  (IMPORTANT FIX)
        // --------------------------------------------------------------------

        private static HashSet<string> CollectExistingNumbers(Document doc, ViewSheet sheet)
        {
            var list = new HashSet<string>();

            var vps = new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            foreach (var vp in vps)
            {
                var view = doc.GetElement(vp.ViewId) as View;
                if (view == null) continue;

                var p = view.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (p != null)
                {
                    string num = p.AsString();
                    if (!string.IsNullOrWhiteSpace(num))
                        list.Add(num);
                }
            }

            return list;
        }

        // --------------------------------------------------------------------
        // ROW ORDERING (BOTTOM-ALIGNMENT)
        // --------------------------------------------------------------------

        private static List<SectionEntry> OrderByRows(List<SectionEntry> items, double thresholdFt)
        {
            var rows = new List<List<SectionEntry>>();
            var rowBands = new List<double>(); // store bottom Y of each row

            foreach (var e in items)
            {
                double bottomY = e.Box.MinimumPoint.Y;   // BOTTOM OF VIEWPORT

                bool placed = false;

                for (int i = 0; i < rows.Count; i++)
                {
                    if (Math.Abs(bottomY - rowBands[i]) <= thresholdFt)
                    {
                        rows[i].Add(e);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    rows.Add(new List<SectionEntry> { e });
                    rowBands.Add(bottomY);
                }
            }

            // Sort rows by bottom Y descending = top → bottom visually
            rows = rows
                .OrderByDescending(r => r.Average(x => x.Box.MinimumPoint.Y))
                .ToList();

            // Sort inside each row by X ascending (left → right)
            foreach (var r in rows)
                r.Sort((a, b) => a.Center.X.CompareTo(b.Center.X));

            return rows.SelectMany(r => r).ToList();
        }


        // --------------------------------------------------------------------
        // SECTION COLLECTION
        // --------------------------------------------------------------------

        private static List<SectionEntry> CollectSections(Document doc, ViewSheet sheet)
        {
            var vps = new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            return vps
                .Select(vp => new SectionEntry
                {
                    Vp = vp,
                    View = doc.GetElement(vp.ViewId) as ViewSection,
                    Center = GetCenter(vp),
                    Box = vp.GetBoxOutline()
                })
                .Where(x => x.View != null && x.View.ViewType == ViewType.Section)
                .ToList();
        }

        private static XYZ GetCenter(Viewport vp)
        {
            var box = vp.GetBoxOutline();
            return (box.MinimumPoint + box.MaximumPoint) / 2.0;
        }


        // --------------------------------------------------------------------
        // UNIQUE NUMBER LOGIC — (D), (D2), (D3)
        // --------------------------------------------------------------------

        private static string MakeUnique(string baseNum, HashSet<string> used)
        {
            // FIRST TRY → base number
            if (!used.Contains(baseNum))
            {
                used.Add(baseNum);
                return baseNum;
            }

            // SECOND TRY → "(D)"
            string d1 = $"{baseNum} (D)";
            if (!used.Contains(d1))
            {
                used.Add(d1);
                return d1;
            }

            // NEXT → "(D2)", "(D3)", ...
            int i = 2;
            while (true)
            {
                string attempt = $"{baseNum} (D{i})";
                if (!used.Contains(attempt))
                {
                    used.Add(attempt);
                    return attempt;
                }
                i++;
            }
        }

        private static void AssignDetailNumber(ViewSection view, string number)
        {
            var p = view.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
            if (p == null || p.IsReadOnly)
                throw new Exception("Detail number is read-only.");

            p.Set(number);
        }


        // --------------------------------------------------------------------
        // PRIVATE ENTRY MODEL
        // --------------------------------------------------------------------

        private class SectionEntry
        {
            public Viewport Vp { get; set; }
            public ViewSection View { get; set; }
            public XYZ Center { get; set; }
            public Outline Box { get; set; }
        }
    }
}
