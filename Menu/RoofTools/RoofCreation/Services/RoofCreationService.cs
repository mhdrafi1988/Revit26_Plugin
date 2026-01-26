using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofFromFloor.Services
{
    public static class RoofCreationService
    {
        public static bool TryCreateFootprintRoof(
            Document doc,
            List<CurveLoop> loops,
            RoofType roofType,
            Level level,
            Action<string> log)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                log($"Attempt {attempt}/3 : Creating FootPrintRoof...");

                try
                {
                    using (Transaction tx = new Transaction(doc, "Create Roof From Floor"))
                    {
                        tx.Start();

                        CurveArray footprint = new CurveArray();

                        foreach (var loop in loops)
                        {
                            foreach (var curve in loop)
                            {
                                footprint.Append(curve);
                            }
                        }

                        FootPrintRoof newRoof = doc.Create.NewFootPrintRoof(
                            footprint,
                            level,
                            roofType,
                            out ModelCurveArray _);

                        tx.Commit();
                    }

                    log("? Roof created successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    log($"? Attempt {attempt} failed: {ex.Message}");
                }
            }

            log("? Roof creation failed after 3 attempts.");
            return false;
        }
    }
}
