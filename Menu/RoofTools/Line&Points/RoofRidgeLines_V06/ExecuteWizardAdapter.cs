// File: ExecuteWizardAdapter.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Views.Adapters
//
// Responsibility:
// - Bridges UI layer to execution service
// - Converts API-agnostic models into Revit API types
// - Triggers execution WITHOUT business logic
//
// IMPORTANT:
// - Revit API is allowed here
// - NO geometry logic
// - NO transaction logic

using System;
using System.Threading;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

using Revit26_Plugin.RoofRidgeLines_V06.Models;
using Revit26_Plugin.RoofRidgeLines_V06.Services.Context;
using Revit26_Plugin.RoofRidgeLines_V06.Services.Execution;

namespace Revit26_Plugin.RoofRidgeLines_V06.Views.Adapters
{
    public class ExecuteWizardAdapter
    {
        private readonly RevitContextService _context;
        private readonly WizardExecutionService _executionService;

        public ExecuteWizardAdapter(
            RevitContextService context,
            WizardExecutionService executionService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        }

        /// <summary>
        /// Executes the wizard workflow using UI-safe inputs.
        /// </summary>
        public ExecutionResult Execute(
            int roofElementId,
            PickedPointData point1,
            PickedPointData point2,
            CancellationToken cancellationToken)
        {
            if (point1 == null)
                throw new ArgumentNullException(nameof(point1));

            if (point2 == null)
                throw new ArgumentNullException(nameof(point2));

            // Resolve roof element safely at execution time
            Element roofElement =
                _context.Document.GetElement(new ElementId(roofElementId));

            if (roofElement is not RoofBase roof)
                throw new InvalidOperationException("Resolved element is not a roof.");

            // Convert UI-safe points → Revit XYZ
            XYZ p1 = new XYZ(point1.X, point1.Y, point1.Z);
            XYZ p2 = new XYZ(point2.X, point2.Y, point2.Z);

            // Delegate ALL real work to execution service
            return _executionService.Execute(
                roof,
                p1,
                p2,
                cancellationToken);
        }
    }
}
