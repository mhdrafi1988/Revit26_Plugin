using Autodesk.Revit.DB;
using Revit26_Plugin.AutoLiner_V04.Services;
using Revit26_Plugin.Creaser_V32.Helpers;
using Revit26_Plugin.Creaser_V32.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V32.Services
{
    public class DetailItemPlacementService
    {
        private readonly UiLogService _log;

        public DetailItemPlacementService(UiLogService log)
        {
            _log = log;
        }

        public void Place(Document doc, List<DirectedLine> lines, ElementType detailType)
        {
            using Transaction tx = new(doc, "Place Crease Detail Items");
            tx.Start();

            if (!detailType.IsActive)
                detailType.Activate();

            foreach (var line in lines)
            {
                Line geom = Line.CreateBound(line.P1, line.P2);
                var inst = doc.Create.NewFamilyInstance(
                    geom,
                    detailType,
                    doc.ActiveView);

                _log.Write($"Created detail item {inst.Id.IntegerValue}");
            }

            tx.Commit();
        }
    }
}
