using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// Places curve-based detail components along drainage paths.
    /// </summary>
    public class DetailItemPlacementService
    {
        private readonly Document _doc;
        private readonly View _view;

        public DetailItemPlacementService(Document doc, View view)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public IList<ElementId> Place(
            IList<DetailCurve> detailCurves,
            FamilySymbol symbol)
        {
            var placedIds = new List<ElementId>();

            if (detailCurves == null || detailCurves.Count == 0)
                return placedIds;

            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            // 🔴 REQUIRED: activate symbol
            if (!symbol.IsActive)
            {
                symbol.Activate();
                _doc.Regenerate();
            }

            foreach (DetailCurve dc in detailCurves)
            {
                Curve curve = dc.GeometryCurve;
                if (curve == null)
                    continue;

                // Only place detail items on straight lines
                if (curve is Line line)
                {
                    try
                    {
                        FamilyInstance inst =
                            _doc.Create.NewFamilyInstance(
                                line,
                                symbol,
                                _view);

                        if (inst != null)
                            placedIds.Add(inst.Id);
                    }
                    catch
                    {
                        // Skip failed segments, continue others
                    }
                }
            }

            return placedIds;
        }
    }
}
