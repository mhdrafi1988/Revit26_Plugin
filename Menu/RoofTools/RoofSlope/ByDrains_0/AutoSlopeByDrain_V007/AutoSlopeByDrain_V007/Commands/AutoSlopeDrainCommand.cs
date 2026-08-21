// File: AutoSlopeDrainCommand.cs
// Location: Commands/
// Base: ported from AutoSlopeByDrain V004 — roof pick, enable shape editing,
// reset vertices to zero, initial drain detection all happen here
// synchronously BEFORE the window is shown (safe since the window hasn't
// opened yet). Window shown modeless via window.Show().
//
// FIX (V005): neither AutoSlopeByPoint V018 nor AutoSlopeByDrain V004
// actually set the window's Owner via WindowInteropHelper, despite that
// being the project's stated standing convention — confirmed as a real gap
// during the merge audit. Fixed here per Rafi's confirmed decision
// (2026-07-21), even though neither prior tool does this.

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.AutoSlopeByDrain.V007.Core.Models;
using Revit26_Plugin.AutoSlopeByDrain.V007.Core.Services;
using Revit26_Plugin.AutoSlopeByDrain.V007.UI.ViewModels;
using Revit26_Plugin.AutoSlopeByDrain.V007.UI.Views;
using System.Collections.Generic;
using System.Windows.Interop;

namespace Revit26_Plugin.AutoSlopeByDrain.V007.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoSlopeByDrain : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;
                Document doc = uidoc.Document;

                Reference roofRef;
                try
                {
                    roofRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        new RoofFilter(),
                        "Select a roof (FootPrintRoof only — required for sketch-based opening detection)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                var roof = doc.GetElement(roofRef) as FootPrintRoof;
                if (roof == null)
                {
                    // Defensive fallback only — RoofFilter already restricts PickObject
                    // to FootPrintRoof, so this should not be reachable in normal use.
                    TaskDialog.Show("AutoSlope By Drain", "Selected element is not a FootPrintRoof. This tool requires a sketch-based roof for opening detection.");
                    return Result.Cancelled;
                }

                var roofData = new RoofData { Roof = roof };

                InitializeRoofGeometry(roof, doc);
                AnalyzeRoofGeometry(roofData);

                // Buffer diagnostic (DRAIN-DEBUG) entries here — the log panel doesn't
                // exist yet at this point, so we collect them and flush into the
                // ViewModel's log once it's constructed below. Detection results
                // (detectedDrains) are completely unaffected by this.
                var diagnosticLog = new List<Revit26_Plugin.Shared.Models.LogEntry>();

                var detectionService = new DrainDetectionService();
                var detectedDrains = detectionService.DetectDrainsFromRoof(
                    roof, roofData.TopFace, roofData.Vertices, entry => diagnosticLog.Add(entry));
                roofData.DetectedDrains = detectedDrains;

                var viewModel = new AutoSlopeDrainViewModel(uidoc, uiApp, roofData);
                foreach (var entry in diagnosticLog)
                    viewModel.LogEntries.Add(entry);

                var window = new AutoSlopeByDrainWindow(viewModel);

                // FIX (V005): parent to Revit's main window per standing convention —
                // never Application.Current.MainWindow, which is null/unreliable in
                // an add-in context.
                new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

                window.Show(); // modeless

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = $"Failed to initialize plugin: {ex.Message}";
                return Result.Failed;
            }
        }

        private void InitializeRoofGeometry(RoofBase roof, Document doc)
        {
            using (Transaction tx = new Transaction(doc, "AutoSlope By Drain - Initialize Geometry"))
            {
                tx.Start();

                var slabShapeEditor = roof.GetSlabShapeEditor();
                if (!slabShapeEditor.IsEnabled)
                {
                    slabShapeEditor.Enable();
                }

                foreach (SlabShapeVertex vertex in slabShapeEditor.SlabShapeVertices)
                {
                    slabShapeEditor.ModifySubElement(vertex, 0.0);
                }

                tx.Commit();
            }
        }

        private void AnalyzeRoofGeometry(RoofData roofData)
        {
            var roof = roofData.Roof;

            roofData.TopFace = GetTopFace(roof);
            if (roofData.TopFace == null)
                throw new System.Exception("Could not find top face of the roof.");

            roofData.Vertices.Clear();
            var slabShapeEditor = roof.GetSlabShapeEditor();
            foreach (SlabShapeVertex vertex in slabShapeEditor.SlabShapeVertices)
            {
                roofData.Vertices.Add(vertex);
            }
        }

        private Face GetTopFace(RoofBase roof)
        {
            GeometryElement geomElem = roof.get_Geometry(new Options());
            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face == null) continue;
                        BoundingBoxUV bb = face.GetBoundingBox();
                        if (bb == null) continue;

                        UV midpointUV = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                        XYZ midpoint = face.Evaluate(midpointUV);

                        if (midpoint != null && midpoint.Z > maxZ)
                        {
                            maxZ = midpoint.Z;
                            topFace = face;
                        }
                    }
                }
            }
            return topFace;
        }
    }

    /// <summary>
    /// Restricts roof selection to FootPrintRoof only — confirmed by Rafi
    /// (2026-07-22) since opening detection is now Sketch-based, and only
    /// FootPrintRoof carries a dependent Sketch element. ExtrusionRoof and any
    /// other RoofBase subtype are not selectable with this tool.
    /// </summary>
    public class RoofFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is FootPrintRoof;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
