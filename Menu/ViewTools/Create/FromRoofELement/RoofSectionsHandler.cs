using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.AutoRoofSections.Models;
using Revit22_Plugin.AutoRoofSections.Services;
using System;

namespace Revit22_Plugin.AutoRoofSections.MVVM
{
    public class RoofSectionsHandler : IExternalEventHandler
    {
        public SectionSettings Payload { get; set; }

        public string GetName() => "Auto Roof Edge Sections Handler";

        public void Execute(UIApplication uiapp)
        {
            if (Payload == null)
            {
                TaskDialog.Show("Error", "No settings payload found.");
                return;
            }

            UIDocument uidoc = Payload.Uidoc;
            Document doc = uidoc.Document;

            // Logging helper
            Action<string> log = Payload.LogAction;

            try
            {
                log("Selecting roof...");

                // STEP 1 — Pick ONE roof
                Reference r = uidoc.Selection.PickObject(
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    "Select a Roof");
                Element roof = doc.GetElement(r);
                if (!(roof is RoofBase roofElement))
                {
                    log("Selected element is not a roof.");
                    return;
                }

                log($"Roof selected: {roofElement.Id}");

                // STEP 2 — Extract roof edges
                var extractor = new RoofEdgeExtractor(doc, log);
                var edges = extractor.GetValidEdges(roofElement, Payload.MinEdgeLengthMm);

                if (edges.Count == 0)
                {
                    log("No valid edges found. Aborting.");
                    return;
                }

                log($"Valid edges: {edges.Count}");

                // STEP 3 — Direction resolver
                var dirResolver = new SectionDirectionResolver(log);

                // STEP 4 — Section creator
                var creator = new SectionCreator(doc, log);

                // STEP 5 — Naming
                var namer = new SectionNaming(Payload);

                // STEP 6 — View configuration
                var viewConfig = new ViewConfigurator(Payload);

                int serial = 1;

                using (TransactionGroup tg = new TransactionGroup(doc, "Auto Roof Edge Sections"))
                {
                    tg.Start();

                    foreach (var edge in edges)
                    {
                        log($"Processing edge #{serial}...");

                        XYZ midpoint = edge.Midpoint;
                        XYZ direction = dirResolver.ResolveDirection(edge.Direction, Payload.DirectionMode);

                        using (Transaction tx = new Transaction(doc, "Create Section"))
                        {
                            tx.Start();
                            try
                            {
                                // Create section
                                var section = creator.CreateSectionAt(
                                    midpoint,
                                    direction,
                                    Payload.Scale);

                                // Generate name
                                string name = namer.BuildName(
                                    roofElement,
                                    serial);

                                section.Name = name;

                                // Apply view template + scale
                                viewConfig.Apply(section, Payload.Scale);

                                tx.Commit();
                                log($" ✓ Section created: {name}");
                            }
                            catch (Exception ex)
                            {
                                tx.RollBack();
                                log($" ✗ Error creating section #{serial}: {ex.Message}");
                            }
                        }

                        serial++;
                    }

                    tg.Assimilate();
                }

                log("=== ALL SECTIONS COMPLETED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                Payload.LogAction("FATAL ERROR: " + ex.Message);
            }
        }
    }
}
