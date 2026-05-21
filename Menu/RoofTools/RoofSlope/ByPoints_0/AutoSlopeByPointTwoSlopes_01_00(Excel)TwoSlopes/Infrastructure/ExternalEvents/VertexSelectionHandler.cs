using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AutoSlopeByPointTwoSlopes_01_00.UI.ViewModels;
using System;
using System.Collections.Generic;

namespace AutoSlopeByPointTwoSlopes_01_00.Infrastructure.ExternalEvents
{
    public class VertexSelectionHandler : IExternalEventHandler
    {
        // ViewModel is set via property after construction so the handler can be
        // created (and its ExternalEvent registered) inside IExternalCommand.Execute()
        // BEFORE the ViewModel and window exist.
        private AutoSlopeViewModel _viewModel;
        private HashSet<int> _selectedIndices;
        private List<XYZ> _vertexPositions;

        public VertexSelectionHandler(AutoSlopeViewModel viewModel)
        {
            _viewModel = viewModel;
            _selectedIndices = new HashSet<int>();
        }

        /// <summary>
        /// Called by AutoSlopeViewModel constructor to wire itself into the
        /// handler after both objects exist.
        /// </summary>
        public void SetViewModel(AutoSlopeViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            bool completedNormally = false;

            try
            {
                // Guard: ViewModel must be wired before Execute runs.
                if (_viewModel == null)
                {
                    System.Diagnostics.Debug.WriteLine("VertexSelectionHandler: ViewModel is null — aborting.");
                    return;
                }

                // Step 1: Get roof from stored ID — no re-confirmation PickObject needed.
                // (PickObject from a handler while a modeless WPF window exists does not
                //  work reliably in Revit and was the original cause of the issue.)
                RoofBase roof = doc.GetElement(_viewModel.RoofId) as RoofBase;
                if (roof == null)
                {
                    _viewModel.AddLog(LogColorHelper.Red("Error: Stored roof not found in document."));
                    return; // → finally → CancelVertexSelection
                }

                _viewModel.AddLog(LogColorHelper.Green($"✓ Roof confirmed (ID: {roof.Id.Value})"));

                // Step 2: Ensure shape editing is enabled.
                _viewModel.AddLog("Ensuring shape editing is enabled...");
                SlabShapeEditor editor = roof.GetSlabShapeEditor();
                if (editor == null)
                {
                    _viewModel.AddLog(LogColorHelper.Red("Error: Cannot access roof shape editor."));
                    return;
                }

                if (!editor.IsEnabled)
                {
                    using (Transaction tx = new Transaction(doc, "Enable Shape Editing for Vertex Selection"))
                    {
                        tx.Start();
                        try
                        {
                            editor.Enable();
                            tx.Commit();
                            _viewModel.AddLog("Shape editing enabled.");
                        }
                        catch (Exception ex)
                        {
                            tx.RollBack();
                            _viewModel.AddLog(LogColorHelper.Red($"Error enabling shape editing: {ex.Message}"));
                            return;
                        }
                    }

                    editor = roof.GetSlabShapeEditor();
                    if (editor == null || !editor.IsValidObject)
                    {
                        _viewModel.AddLog(LogColorHelper.Red("Error: Shape editor not available after enabling."));
                        return;
                    }
                }

                // Step 3: Read vertices.
                _vertexPositions = GetVertexPositions(editor);
                if (_vertexPositions == null || _vertexPositions.Count == 0)
                {
                    _viewModel.AddLog(LogColorHelper.Red("Error: No vertices found on the roof."));
                    return;
                }

                _viewModel.AddLog(LogColorHelper.Cyan($"Found {_vertexPositions.Count} unique vertices on the roof."));
                _viewModel.AddLog("Click on vertices to select them for special slope.");
                _viewModel.AddLog("Press Finish (✓) on the Revit toolbar when done, or ESC to cancel.");

                _selectedIndices.Clear();

                // Step 4: Vertex selection loop.
                while (true)
                {
                    try
                    {
                        IList<Reference> selectedRefs = uidoc.Selection.PickObjects(
                            ObjectType.PointOnElement,
                            new VertexSelectionFilter(),
                            $"Select special vertices ({_selectedIndices.Count} selected so far). Click Finish when done.");

                        if (selectedRefs == null || selectedRefs.Count == 0)
                            break;

                        foreach (var refe in selectedRefs)
                        {
                            if (refe == null || refe.GlobalPoint == null)
                                continue;

                            int closestIndex = FindClosestVertex(refe.GlobalPoint);
                            if (closestIndex >= 0)
                            {
                                if (_selectedIndices.Add(closestIndex))
                                    _viewModel.AddLog(LogColorHelper.Green($"✓ Added vertex #{closestIndex} to special selection."));
                                else
                                    _viewModel.AddLog(LogColorHelper.Yellow($"⚠ Vertex #{closestIndex} already selected."));
                            }
                            else
                            {
                                _viewModel.AddLog(LogColorHelper.Yellow("✗ No matching vertex found near clicked point."));
                            }
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        _viewModel.AddLog("Vertex selection finished.");
                        break;
                    }
                }

                _viewModel.AddLog(LogColorHelper.Cyan($"Total selected special vertices: {_selectedIndices.Count}"));
                completedNormally = true;
            }
            catch (Exception ex)
            {
                _viewModel?.AddLog(LogColorHelper.Red($"Error during vertex selection: {ex.Message}"));
                System.Diagnostics.Debug.WriteLine($"Vertex Selection Error: {ex.StackTrace}");
            }
            finally
            {
                // Always restore the window — no code path can leave it hidden.
                if (completedNormally)
                    _viewModel?.CompleteVertexSelection(_selectedIndices);
                else
                    _viewModel?.CancelVertexSelection();
            }
        }

        // ── Private helpers ────────────────────────────────────────────────

        private List<XYZ> GetVertexPositions(SlabShapeEditor editor)
        {
            var positions = new List<XYZ>();
            try
            {
                for (int i = 0; i < editor.SlabShapeVertices.Size; i++)
                {
                    SlabShapeVertex vertex = editor.SlabShapeVertices.get_Item(i);
                    if (vertex != null && vertex.IsValidObject)
                        positions.Add(vertex.Position);
                }
            }
            catch (Exception ex)
            {
                _viewModel?.AddLog(LogColorHelper.Red($"Error reading vertices: {ex.Message}"));
                return null;
            }

            var unique = RemoveDuplicates(positions, 0.5);
            if (unique.Count != positions.Count)
                _viewModel?.AddLog(LogColorHelper.Yellow($"Removed {positions.Count - unique.Count} duplicate vertex position(s)."));

            return unique;
        }

        private List<XYZ> RemoveDuplicates(List<XYZ> positions, double toleranceMm)
        {
            if (positions == null || positions.Count <= 1)
                return positions ?? new List<XYZ>();

            double toleranceFt = UnitUtils.ConvertToInternalUnits(toleranceMm, UnitTypeId.Millimeters);
            var unique = new List<XYZ>();
            var seen = new HashSet<string>();

            foreach (var pos in positions)
            {
                if (pos == null) continue;
                double bucket = Math.Max(toleranceFt * 0.5, 1e-9);
                string key = $"{Math.Round(pos.X / bucket) * bucket:F6}," +
                             $"{Math.Round(pos.Y / bucket) * bucket:F6}," +
                             $"{Math.Round(pos.Z / bucket) * bucket:F6}";
                if (seen.Add(key))
                    unique.Add(pos);
            }
            return unique;
        }

        private int FindClosestVertex(XYZ point)
        {
            int closest = -1;
            double minDist = double.MaxValue;
            double maxMatch = UnitUtils.ConvertToInternalUnits(610, UnitTypeId.Millimeters); // 610 mm ≈ 2 ft

            for (int i = 0; i < _vertexPositions.Count; i++)
            {
                double dist = _vertexPositions[i].DistanceTo(point);
                if (dist < minDist && dist < maxMatch)
                {
                    minDist = dist;
                    closest = i;
                }
            }
            return closest;
        }

        public string GetName() => "VertexSelectionHandler";
    }

    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => true;
    }

    public class VertexSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => true;
    }

    internal static class LogColorHelper
    {
        private static string Stamp => DateTime.Now.ToString("HH:mm:ss");
        private static string Wrap(string color, string msg) => $"<color={color}>[{Stamp}] {msg}</color>";

        public static string Green(string m) => Wrap("#2ECC71", m);
        public static string Yellow(string m) => Wrap("#F1C40F", m);
        public static string Red(string m) => Wrap("#E74C3C", m);
        public static string Cyan(string m) => Wrap("#1ABC9C", m);
    }
}