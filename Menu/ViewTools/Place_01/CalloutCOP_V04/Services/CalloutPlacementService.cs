using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using Revit26_Plugin.CalloutCOP_V04.Models;

namespace Revit26_Plugin.CalloutCOP_V04.Services
{
    public static class CalloutPlacementService
    {
        public static void Place(
            Document doc,
            UIDocument uidoc,
            IEnumerable<CalloutItem> items,
            ViewDrafting target,
            LoggerService log)
        {
            using var tg = new TransactionGroup(doc, "CalloutCOP Placement");
            tg.Start();

            foreach (var item in items)
            {
                var section = doc.GetElement(item.ViewId) as ViewSection;
                if (section == null || !section.CropBoxActive)
                {
                    log.Warn($"{item.SectionName}: Invalid crop box");
                    continue;
                }

                var box = section.CropBox;
                var center = box.Transform.OfPoint((box.Min + box.Max) * 0.5);

                double offset = target.Scale * doc.Application.ShortCurveTolerance;
                var p1 = center - new XYZ(offset, offset, 0);
                var p2 = center + new XYZ(offset, offset, 0);

                using var tx = new Transaction(doc, "Place Callout");
                tx.Start();

                ViewSection.CreateReferenceCallout(
                    doc,
                    section.Id,
                    target.Id,
                    p1,
                    p2);

                tx.Commit();
                log.Success($"{item.SectionName} placed");
            }

            tg.Assimilate();
        }
    }
}
