using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit_26.CornertoDrainArrow_V05
{
    public sealed class CollectDetailFamiliesEvent : IExternalEventHandler
    {
        public Action<List<DetailFamilyOptionDto>> OnCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null) return;

            var symbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .Cast<FamilySymbol>()
                .Where(s => s.Family != null && s.Family.FamilyPlacementType == FamilyPlacementType.CurveBased); // ✅ LINE-BASED ONLY

            var result = symbols
                .Select(s => new DetailFamilyOptionDto
                {
                    SymbolId = s.Id,
                    DisplayName = $"{s.Family.Name} : {s.Name}",
                    IsLineBased = true
                })
                .ToList();

            OnCompleted?.Invoke(result);
        }

        public string GetName() => "Collect Line-Based Detail Families";
    }
}
