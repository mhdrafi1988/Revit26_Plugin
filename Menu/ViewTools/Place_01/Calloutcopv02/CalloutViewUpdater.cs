using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.copv2.Models;

namespace Revit22_Plugin.copv2.Helpers
{
    public static class CalloutViewUpdater
    {
        public static void InsertReferences(
            Document doc,
            UIDocument uidoc,
            List<CalloutViewModelCall> views,
            double calloutSizeFt,
            ElementId draftingViewId,
            ElementId selectedSheetId)
        {
            int success = 0, skipped = 0, failed = 0;
            List<string> messages = new List<string>();

            using (TransactionGroup tg = new TransactionGroup(doc, "Batch Reference Callouts"))
            {
                tg.Start();

                foreach (var vm in views)
                {
                    try
                    {
                        // ---------------------------------------------------------------------
                        // 1) Enforce OPTION B: Only sections on the selected sheet
                        // ---------------------------------------------------------------------
                        if (vm.SheetId == null ||
                            vm.SheetId == ElementId.InvalidElementId ||
                            vm.SheetId != selectedSheetId)
                        {
                            skipped++;
                            messages.Add($"⚠️ [{vm.SectionName}] - Skipped (Not on selected sheet)");
                            continue;
                        }

                        // ---------------------------------------------------------------------
                        // 2) Validate section view
                        // ---------------------------------------------------------------------
                        var sectionView = doc.GetElement(vm.ViewId) as ViewSection;
                        if (sectionView == null)
                        {
                            failed++;
                            messages.Add($"❌ [{vm.SectionName}] - Not a valid section view.");
                            continue;
                        }

                        // ---------------------------------------------------------------------
                        // 3) Validate crop box
                        // ---------------------------------------------------------------------
                        if (!sectionView.CropBoxActive || !sectionView.CropBoxVisible)
                        {
                            failed++;
                            messages.Add($"❌ [{vm.SectionName}] - CropBox not active or not visible.");
                            continue;
                        }

                        // ---------------------------------------------------------------------
                        // 4) Compute center of crop box
                        // ---------------------------------------------------------------------
                        BoundingBoxXYZ cropBox = sectionView.CropBox;
                        Transform tf = cropBox.Transform;

                        XYZ min = cropBox.Min;
                        XYZ max = cropBox.Max;

                        XYZ centerLocal = (min + max) * 0.5;
                        XYZ centerWorld = tf.OfPoint(centerLocal);

                        double offset = (calloutSizeFt > 0 ? calloutSizeFt : 3.0) / 2.0;

                        XYZ right = tf.BasisX.Normalize();
                        XYZ up = tf.BasisY.Normalize();

                        XYZ pt1 = centerWorld - right * offset - up * offset;
                        XYZ pt2 = centerWorld + right * offset + up * offset;

                        // ---------------------------------------------------------------------
                        // 5) Create callout
                        // ---------------------------------------------------------------------
                        using (Transaction tx = new Transaction(doc, "Place Reference Callout"))
                        {
                            tx.Start();
                            ViewSection.CreateReferenceCallout(
                                doc,
                                sectionView.Id,
                                draftingViewId,
                                pt1,
                                pt2);

                            tx.Commit();
                        }

                        success++;
                        messages.Add($"✅ [{vm.SectionName}] - Callout placed.");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        messages.Add($"❌ [{vm.SectionName}] - Error: {ex.Message}");
                    }
                }

                // Final decision: Assimilate if no fatal errors
                tg.Assimilate();
            }

            // ---------------------------------------------------------------------
            // Display Summary
            // ---------------------------------------------------------------------
            string summary =
                $"📌 Reference Callout Summary\n\n" +
                $"Total Selected: {views.Count}\n" +
                $"🟩 Placed: {success}\n" +
                $"🟨 Skipped: {skipped}\n" +
                $"🟥 Failed: {failed}\n\n" +
                string.Join("\n", messages);

            TaskDialog.Show("Callout Summary", summary);
        }
    }
}
