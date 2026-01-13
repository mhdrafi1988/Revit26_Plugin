using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.callout.Models;
using Revit22_Plugin.callout.Commands;
using Revit22_Plugin.callout.Helpers;
using Revit22_Plugin.callout.Models;
using Revit22_Plugin.Relay;

namespace Revit22_Plugin.callout.Helpers
{
    public static class CalloutViewUpdater
    {
        public static void InsertReferences(Document doc, List<CalloutViewModelCall> views, double calloutSize, ElementId draftingViewId)
        {
            int success = 0, skipped = 0, failed = 0;
            List<string> messages = new List<string>();

            using (Transaction tx = new Transaction(doc, "Place Reference Callouts"))
            {
                tx.Start();

                foreach (var vm in views)
                {
                    try
                    {
                        var sectionView = doc.GetElement(vm.ViewId) as ViewSection;
                        if (sectionView == null)
                        {
                            failed++;
                            messages.Add($"❌ [{vm.SectionName}] - Not a valid section view.");
                            continue;
                        }

                        if (AlreadyHasReference(doc, sectionView, draftingViewId))
                        {
                            skipped++;
                            messages.Add($"⚠️ [{vm.SectionName}] - Already has reference.");
                            continue;
                        }

                        // Ensure the section has a valid crop box
                        if (!sectionView.CropBoxActive || !sectionView.CropBoxVisible)
                        {
                            failed++;
                            messages.Add($"❌ [{vm.SectionName}] - CropBox not active/visible.");
                            continue;
                        }

                        BoundingBoxXYZ cropBox = sectionView.CropBox;
                        Transform tf = sectionView.CropBox.Transform;

                        // Compute box center in model space
                        XYZ min = cropBox.Min;
                        XYZ max = cropBox.Max;
                        XYZ centerLocal = (min + max) * 0.5;
                        XYZ centerWorld = tf.OfPoint(centerLocal);

                        // Use normalized square size (e.g., 3 feet)
                        double offset = (calloutSize > 0 ? calloutSize : 3.0) / 2.0;

                        XYZ right = tf.BasisX.Normalize();
                        XYZ up = tf.BasisY.Normalize();

                        // Define rectangular box around center in section plane
                        XYZ pt1 = centerWorld - right * offset - up * offset;
                        XYZ pt2 = centerWorld + right * offset + up * offset;

                        ViewSection.CreateReferenceCallout(doc, sectionView.Id, draftingViewId, pt1, pt2);
                        success++;
                        messages.Add($"✅ [{vm.SectionName}] - Reference placed.");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        messages.Add($"❌ [{vm.SectionName}] - Error: {ex.Message}");
                    }
                }

                tx.Commit();
            }

            // Summary Dialog
            string summary =
                $"📌 Reference Callout Summary\n\n" +
                $"Total Selected: {views.Count}\n" +
                $"✅ Placed: {success}\n" +
                $"⚠️ Skipped: {skipped}\n" +
                $"❌ Failed: {failed}\n\n" +
                string.Join("\n", messages);

            TaskDialog.Show("Callout Summary", summary);
        }

        private static bool AlreadyHasReference(Document doc, ViewSection sectionView, ElementId draftingViewId)
        {
            // Not yet implemented – always allow placement
            return false;
        }
    }
}
