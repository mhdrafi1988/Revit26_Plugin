// File: ShapeEditTransactionService.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Services.Transactions
//
// Responsibility:
// - Adds slab shape editing points to the roof
// - Runs in a SEPARATE transaction
//
// IMPORTANT:
// - No detail line creation
// - No geometry computation

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

using Revit26_Plugin.RoofRidgeLines_V06.Services.Context;

namespace Revit26_Plugin.RoofRidgeLines_V06.Services.Transactions
{
    public class ShapeEditTransactionService
    {
        private readonly RevitContextService _context;

        public ShapeEditTransactionService(RevitContextService context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Adds shape editing points to the given roof.
        /// </summary>
        public int AddShapePoints(
            RoofBase roof,
            IEnumerable<XYZ> points)
        {
            if (!roof.CanHaveShapeEditor)
                throw new InvalidOperationException(
                    "Roof does not support shape editing.");

            int count = 0;

            using Transaction tx = new Transaction(
                _context.Document,
                "Add Roof Shape Editing Points");

            tx.Start();

            SlabShapeEditor editor = roof.SlabShapeEditor;

            foreach (XYZ point in points)
            {
                editor.DrawPoint(point);
                count++;
            }

            tx.Commit();

            return count;
        }
    }
}
