using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Revit_26.CornertoDrainArrow_V05
{
    public sealed class PlaceLineDetailEvent : IExternalEventHandler
    {
        public ElementId SymbolId { get; set; }
        public XYZ Start { get; set; }
        public XYZ End { get; set; }

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            var doc = uidoc?.Document;
            var view = doc?.ActiveView;

            if (doc == null || view == null) return;

            var symbol = doc.GetElement(SymbolId) as FamilySymbol;
            if (symbol == null) return;

            using var tx = new Transaction(doc, "Place Line-Based Detail Item");
            tx.Start();

            if (!symbol.IsActive)
                symbol.Activate();

            var line = Line.CreateBound(Start, End);

            doc.Create.NewFamilyInstance(
                line,
                symbol,
                view
            );

            tx.Commit();
        }

        public string GetName() => "Place Line Detail Item";
    }
}
