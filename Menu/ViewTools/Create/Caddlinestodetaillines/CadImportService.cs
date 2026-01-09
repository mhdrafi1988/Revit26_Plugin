using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit22_Plugin.ImportCadLines.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.ImportCadLines.Services
{
    public class CadImportService
    {
        private readonly Document _doc;
        private readonly View _view;
        public ImportInstance SelectedCadLink { get; private set; }

        public CadImportService(Document doc, View view)
        {
            _doc = doc;
            _view = view;
        }

        public bool TryGetCadLayers(out List<string> layers)
        {
            layers = new List<string>();
            SelectedCadLink = PickLinkedCad();

            if (SelectedCadLink == null)
            {
                TaskDialog.Show("CAD Import", "No CAD link selected.");
                return false;
            }

            try
            {
                Options options = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = true
                };

                GeometryElement geoElement = SelectedCadLink.get_Geometry(options);

                if (geoElement == null)
                {
                    TaskDialog.Show("CAD Import", "CAD geometry is null.");
                    return false;
                }

                var extracted = new HashSet<string>();
                ExtractLayerNamesRecursive(geoElement, extracted);

                if (extracted.Count == 0)
                {
                    TaskDialog.Show("CAD Import", "No layers found in CAD geometry.");
                    return false;
                }

                layers.AddRange(extracted);
                return true;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error extracting CAD layers: {ex.Message}");
                return false;
            }
        }

        private void ExtractLayerNamesRecursive(GeometryElement geometry, HashSet<string> layers)
        {
            foreach (var obj in geometry)
            {
                if (obj is GeometryInstance instance)
                {
                    GeometryElement nested = instance.GetInstanceGeometry();
                    ExtractLayerNamesRecursive(nested, layers);
                }
                else if (obj.GraphicsStyleId != ElementId.InvalidElementId)
                {
                    GraphicsStyle style = _doc.GetElement(obj.GraphicsStyleId) as GraphicsStyle;
                    if (style != null)
                    {
                        layers.Add(style.Name);
                        System.Diagnostics.Debug.WriteLine($"Found CAD layer: {style.Name}");
                    }
                }
            }
        }

        public void ImportLinesFromSelectedLayer(string layerName)
        {
            var lines = new List<Line>();

            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true
            };

            var geo = SelectedCadLink.get_Geometry(options);
            foreach (var obj in geo)
            {
                if (obj is GeometryInstance geoInstance)
                {
                    var instanceGeo = geoInstance.GetInstanceGeometry();
                    lines.AddRange(ExtractLines(instanceGeo, layerName));
                }
                else
                {
                    lines.AddRange(ExtractLines(geo, layerName));
                }
            }

            using (Transaction tx = new Transaction(_doc, "Import CAD Detail Lines"))
            {
                tx.Start();
                foreach (var line in lines)
                {
                    try
                    {
                        _doc.Create.NewDetailCurve(_view, line);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to create line: {ex.Message}");
                    }
                }
                tx.Commit();
            }

            TaskDialog.Show("CAD Import", $"Imported {lines.Count} lines from layer '{layerName}'.");
        }

        private List<Line> ExtractLines(GeometryElement geometry, string layer)
        {
            var lines = new List<Line>();

            foreach (var obj in geometry)
            {
                if (obj is GeometryInstance nestedInstance)
                {
                    var nestedGeo = nestedInstance.GetInstanceGeometry();
                    lines.AddRange(ExtractLines(nestedGeo, layer));
                }
                else if (obj.GraphicsStyleId != ElementId.InvalidElementId)
                {
                    var style = _doc.GetElement(obj.GraphicsStyleId) as GraphicsStyle;
                    if (style == null || !style.Name.Equals(layer, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (obj is Line l)
                    {
                        lines.Add(l);
                    }
                    else if (obj is PolyLine pl)
                    {
                        var pts = pl.GetCoordinates();
                        for (int i = 0; i < pts.Count - 1; i++)
                        {
                            try
                            {
                                lines.Add(Line.CreateBound(pts[i], pts[i + 1]));
                            }
                            catch
                            {
                                // Skip invalid segment
                            }
                        }
                    }
                }
            }

            return lines;
        }

        private ImportInstance PickLinkedCad()
        {
            var links = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .Where(i => i.IsLinked)
                .ToList();

            if (!links.Any())
            {
                TaskDialog.Show("CAD Import", "No linked CAD files found.");
                return null;
            }

            if (links.Count == 1)
                return links.First();

            var ids = links.Select(l => l.Id).ToList();

            try
            {
                var selection = new UIDocument(_doc).Selection.PickObject(
                    ObjectType.Element,
                    new SelectionFilterByElementIds(ids),
                    "Select a CAD link");

                return _doc.GetElement(selection.ElementId) as ImportInstance;
            }
            catch
            {
                return null;
            }
        }
    }

    public class SelectionFilterByElementIds : ISelectionFilter
    {
        private readonly HashSet<ElementId> _ids;
        public SelectionFilterByElementIds(IEnumerable<ElementId> ids)
        {
            _ids = new HashSet<ElementId>(ids);
        }

        public bool AllowElement(Element elem) => _ids.Contains(elem.Id);
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
