// ==================================
// File: DetailItemPlacementService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V004_00
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V004_00.Services
{
    /// <summary>
    /// Places a line-based detail component along each supplied plan line.
    /// Must be called inside an active <see cref="Transaction"/>.
    /// Returns a (placed, failed) tuple for the run summary.
    /// </summary>
    public class DetailItemPlacementService
    {
        private readonly Document _doc;
        private readonly ViewPlan _view;

        public DetailItemPlacementService(Document doc, ViewPlan view)
        {
            _doc  = doc  ?? throw new ArgumentNullException(nameof(doc));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        /// <summary>
        /// Places detail items along <paramref name="lines"/>.
        /// </summary>
        /// <returns>(placed count, failed count)</returns>
        public (int Placed, int Failed) PlaceAlongLines(
            IList<Line>    lines,
            FamilySymbol   symbol,
            LoggingService log)
        {
            if (lines == null || lines.Count == 0)
            {
                log.Warning("No lines provided for placement.");
                return (0, 0);
            }

            if (symbol == null)
            {
                log.Warning("Detail item symbol is null.");
                return (0, 0);
            }

            if (!symbol.IsActive)
            {
                symbol.Activate();
                _doc.Regenerate();
            }

            double tol    = _doc.Application.ShortCurveTolerance;
            int    placed = 0;
            int    failed = 0;

            foreach (Line line in lines)
            {
                if (line == null || line.Length < tol)
                {
                    log.Warning("Skipped a line: null or shorter than tolerance.");
                    failed++;
                    continue;
                }

                try
                {
                    var instance = _doc.Create.NewFamilyInstance(line, symbol, _view);
                    if (instance != null)
                        placed++;
                    else
                        failed++;
                }
                catch (Exception ex)
                {
                    log.Error($"Placement failed: {ex.Message}");
                    failed++;
                }
            }

            log.Info($"Detail items placed: {placed}  |  failed: {failed}");
            return (placed, failed);
        }
    }
}
