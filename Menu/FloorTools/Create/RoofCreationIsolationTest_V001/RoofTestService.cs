using Autodesk.Revit.DB;
using Revit26_Plugin.RoofCreationIsolationTest.V001.Core.Models;
using Revit26_Plugin.RoofCreationIsolationTest.V001.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofCreationIsolationTest.V001.Core.Services
{
    public class RoofTestService
    {
        private const double FootprintSideMeters = 4.0;

        public RoofTestResult RunTest(Document doc, ThreadSafeLogSink log)
        {
            var result = new RoofTestResult();
            log.Add(LogLevel.Info, "=== RoofTestService.RunTest() entered ===");

            try
            {
                // ── Step 1: Get first Level ──
                log.Add(LogLevel.Info, "Collecting Levels...");
                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                if (levels.Count == 0)
                {
                    log.Add(LogLevel.Error, "No Level found.");
                    result.Success = false;
                    result.ExceptionMessage = "No Level elements found.";
                    return result;
                }

                var level = levels.First();
                result.LevelId = level.Id;
                result.LevelName = level.Name;
                result.LevelElevationFt = level.Elevation;
                log.Add(LogLevel.Success, $"Selected Level: \"{level.Name}\" Elev={level.Elevation:F2} ft");

                // ── Step 2: Get first valid RoofType ──
                log.Add(LogLevel.Info, "Collecting RoofTypes...");
                var roofTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(RoofType))
                    .Cast<RoofType>()
                    .Where(rt => rt.GetCompoundStructure() != null)
                    .ToList();

                if (roofTypes.Count == 0)
                {
                    log.Add(LogLevel.Error, "No RoofType with compound structure found.");
                    result.Success = false;
                    result.ExceptionMessage = "No valid RoofType found.";
                    return result;
                }

                var roofType = roofTypes.First();
                result.RoofTypeId = roofType.Id;
                result.RoofTypeName = roofType.Name;
                log.Add(LogLevel.Success, $"Selected RoofType: \"{roofType.Name}\"");

                // ── Step 3: Build footprint (CurveLoop, not CurveArray) ──
                double sideFt = UnitUtils.ConvertToInternalUnits(FootprintSideMeters, UnitTypeId.Meters);
                double z = level.Elevation;   // critical: use level's elevation

                XYZ p1 = new(0, 0, z);
                XYZ p2 = new(sideFt, 0, z);
                XYZ p3 = new(sideFt, sideFt, z);
                XYZ p4 = new(0, sideFt, z);

                result.FootprintPointsFt = new[] { p1, p2, p3, p4 };

                var curveLoop = new CurveLoop();
                curveLoop.Append(Line.CreateBound(p1, p2));
                curveLoop.Append(Line.CreateBound(p2, p3));
                curveLoop.Append(Line.CreateBound(p3, p4));
                curveLoop.Append(Line.CreateBound(p4, p1));

                log.Add(LogLevel.Info, $"CurveLoop created, side={sideFt:F3} ft, Z={z:F3} ft");

                // ── Step 4: Validate the loop is closed (IsOpen() is available) ──
                if (curveLoop.IsOpen())
                {
                    log.Add(LogLevel.Error, "CurveLoop is NOT closed.");
                    result.Success = false;
                    result.ExceptionMessage = "Footprint loop is not closed.";
                    return result;
                }
                log.Add(LogLevel.Info, "CurveLoop is closed.");

                // ── Step 5: Call the original API method (which you know compiles) ──
                log.Add(LogLevel.Info, "Calling doc.Create.NewFootPrintRoof(...)");

                try
                {
                    // Convert CurveLoop to CurveArray (the old API expects CurveArray)
                    var curveArray = new CurveArray();
                    foreach (Curve c in curveLoop)
                        curveArray.Append(c);

                    ModelCurveArray mapping = new();
                    FootPrintRoof roof = doc.Create.NewFootPrintRoof(curveArray, level, roofType, out mapping);

                    result.Success = true;
                    result.CreatedRoofId = roof.Id;
                    result.CreatedRoofName = roof.Name;
                    log.Add(LogLevel.Success, $"Roof created! Id={roof.Id.Value}, Name=\"{roof.Name}\"");
                }
                catch (Exception apiEx)
                {
                    result.Success = false;
                    CaptureException(apiEx, result);
                    LogFullException(log, "NewFootPrintRoof", apiEx);
                }
            }
            catch (Exception outerEx)
            {
                result.Success = false;
                CaptureException(outerEx, result);
                LogFullException(log, "RunTest (outer)", outerEx);
            }

            log.Add(LogLevel.Info, "=== RoofTestService.RunTest() exited ===");
            return result;
        }

        private static void CaptureException(Exception ex, RoofTestResult result)
        {
            result.ExceptionTypeName = ex.GetType().FullName;
            result.ExceptionMessage = ex.Message;
            result.ExceptionStackTrace = ex.StackTrace;
            result.ExceptionSource = ex.Source;

            var inner = ex.InnerException;
            while (inner != null)
            {
                result.InnerExceptionChain.Add(
                    $"Type: {inner.GetType().FullName}\nMessage: {inner.Message}\nSource: {inner.Source}\nStackTrace:\n{inner.StackTrace}");
                inner = inner.InnerException;
            }
        }

        private static void LogFullException(ThreadSafeLogSink log, string context, Exception ex)
        {
            string text = $"EXCEPTION in {context}:\nType: {ex.GetType().FullName}\nMessage: {ex.Message}\nSource: {ex.Source}\nStackTrace:\n{ex.StackTrace}";
            var inner = ex.InnerException;
            int depth = 1;
            while (inner != null)
            {
                text += $"\n--- Inner #{depth} ---\nType: {inner.GetType().FullName}\nMessage: {inner.Message}\nSource: {inner.Source}\nStackTrace:\n{inner.StackTrace}";
                inner = inner.InnerException;
                depth++;
            }
            log.Add(LogLevel.Error, text);
        }
    }
}