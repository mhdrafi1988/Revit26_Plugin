using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using Revit22_Plugin.copv3.Models;

namespace Revit22_Plugin.copv3.Services
{
    public static class CalloutCOPV3PlacementService
    {
        public static void PlaceCallouts(
            Document doc,
            UIDocument uidoc,
            List<CalloutCOPV3Item> items,
            View draftingView,
            IList<string> log)
        {
            using (TransactionGroup tg = new TransactionGroup(doc, "COPV3 Callout Placement"))
            {
                tg.Start();

                foreach (var i in items)
                {
                    try
                    {
                        ViewSection sec = doc.GetElement(i.ViewId) as ViewSection;
                        if (sec == null)
                        {
                            log.Add($"❌ {i.SectionName}: Invalid view");
                            continue;
                        }

                        if (!sec.CropBoxActive)
                        {
                            log.Add($"⚠️ {i.SectionName}: CropBox inactive");
                            continue;
                        }

                        var box = sec.CropBox;
                        var tf = box.Transform;
                        XYZ center = tf.OfPoint((box.Min + box.Max) * 0.5);

                        XYZ p1 = center - new XYZ(2, 2, 0);
                        XYZ p2 = center + new XYZ(2, 2, 0);

                        using (Transaction tx = new Transaction(doc, "Place Callout"))
                        {
                            tx.Start();

                            ViewSection.CreateReferenceCallout(
                                doc,
                                sec.Id,
                                draftingView.Id,
                                p1,
                                p2);

                            tx.Commit();
                        }

                        log.Add($"✅ {i.SectionName}: Callout placed");
                    }
                    catch (Exception ex)
                    {
                        log.Add($"❌ {i.SectionName}: {ex.Message}");
                    }
                }

                tg.Assimilate();
            }
        }
    }
}
