// File: DetailLineTransactionService.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Services.Transactions
//
// Responsibility:
// - Creates detail lines in ONE transaction
// - Creates base line + two perpendicular lines
// - Returns created geometry data
//
// IMPORTANT:
// - No shape editing
// - No UI logic

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

using Revit26_Plugin.RoofRidgeLines_V06.Services.Context;

namespace Revit26_Plugin.RoofRidgeLines_V06.Services.Transactions
{
    public class DetailLineTransactionService
    {
        private readonly RevitContextService _context;

        public DetailLineTransactionService(RevitContextService context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Creates detail lines inside a single transaction.
        /// </summary>
        public IList<DetailCurve> CreateDetailLines(
            Line baseLine,
            Line perpLine1,
            Line perpLine2)
        {
            List<DetailCurve> createdLines = new();

            using Transaction tx = new Transaction(
                _context.Document,
                "Create Roof Ridge Detail Lines");

            tx.Start();

            createdLines.Add(
                _context.Document.Create.NewDetailCurve(
                    _context.ActiveView, baseLine));

            createdLines.Add(
                _context.Document.Create.NewDetailCurve(
                    _context.ActiveView, perpLine1));

            createdLines.Add(
                _context.Document.Create.NewDetailCurve(
                    _context.ActiveView, perpLine2));

            tx.Commit();

            return createdLines;
        }
    }
}
