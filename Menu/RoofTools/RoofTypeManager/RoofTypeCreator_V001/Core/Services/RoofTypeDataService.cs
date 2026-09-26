using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTypeCreator.V001.Core.Models;

namespace Revit26_Plugin.RoofTypeCreator.V001.Core.Services
{
    public static class RoofTypeDataService
    {
        public static List<RoofTypeItem> LoadFromDocument(Document doc)
        {
            var items = new List<RoofTypeItem>();

            var roofTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .Cast<RoofType>()
                .OrderBy(t => t.Name);

            foreach (var rt in roofTypes)
            {
                var cs            = rt.GetCompoundStructure();
                int layerCount    = cs?.LayerCount ?? 0;
                double thicknessMm = cs != null
                    ? Math.Round(UnitUtils.ConvertFromInternalUnits(cs.GetWidth(), UnitTypeId.Millimeters), 1)
                    : 0.0;

                string typeMark = rt.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)?.AsString() ?? "";
                string function = rt.get_Parameter(BuiltInParameter.FUNCTION_PARAM)?.AsValueString() ?? "Exterior";

                var layers = new List<LayerData>();
                if (cs != null)
                {
                    int varIdx = cs.VariableLayerIndex;
                    for (int li = 0; li < cs.LayerCount; li++)
                    {
                        var layer = cs.GetLayer(li);
                        string mat = layer.MaterialId != ElementId.InvalidElementId
                            ? (doc.GetElement(layer.MaterialId) as Material)?.Name ?? ""
                            : "";
                        double thick = Math.Round(
                            UnitUtils.ConvertFromInternalUnits(layer.Width, UnitTypeId.Millimeters), 1);
                        layers.Add(new LayerData
                        {
                            MaterialName  = mat,
                            ThicknessMm   = thick,
                            LayerFunction = layer.Function.ToString(),
                            Priority      = layer.Priority,
                            IsVariable    = li == varIdx
                        });
                    }
                }

                items.Add(new RoofTypeItem
                {
                    TypeId           = rt.Id,
                    TypeName         = rt.Name,
                    TypeMark         = typeMark,
                    Function         = function,
                    LayerCount       = layerCount,
                    TotalThicknessMm = thicknessMm,
                    Layers           = layers,
                    IsSelected       = false
                });
            }

            return items;
        }

        public static HashSet<string> GetExistingTypeNames(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .Cast<RoofType>()
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
