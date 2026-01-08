// File: RevitContextService.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Services.Context
//
// Responsibility:
// - Encapsulates Revit UIApplication, UIDocument, and Document
// - Validates active view compatibility (Plan View only)
// - Provides safe access to Revit context for services
//
// IMPORTANT:
// - No UI logic
// - No transactions
// - No geometry operations

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;

namespace Revit26_Plugin.RoofRidgeLines_V06.Services.Context
{
    public class RevitContextService
    {
        public UIApplication UIApplication { get; }
        public UIDocument UIDocument { get; }
        public Document Document { get; }
        public View ActiveView { get; }

        public RevitContextService(
            UIApplication uiApplication,
            UIDocument uiDocument,
            Document document)
        {
            UIApplication = uiApplication ?? throw new ArgumentNullException(nameof(uiApplication));
            UIDocument = uiDocument ?? throw new ArgumentNullException(nameof(uiDocument));
            Document = document ?? throw new ArgumentNullException(nameof(document));

            ActiveView = document.ActiveView
                ?? throw new InvalidOperationException("Active view is null.");
        }

        /// <summary>
        /// Ensures the active view is a plan view that supports detail lines.
        /// </summary>
        public void ValidatePlanView()
        {
            if (ActiveView.ViewType != ViewType.FloorPlan &&
                ActiveView.ViewType != ViewType.EngineeringPlan &&
                ActiveView.ViewType != ViewType.CeilingPlan)
            {
                throw new InvalidOperationException(
                    "Active view must be a plan view to create detail lines.");
            }

            if (ActiveView.IsTemplate)
            {
                throw new InvalidOperationException(
                    "Active view is a template and cannot be used.");
            }
        }

        /// <summary>
        /// Provides access to the Revit selection object.
        /// </summary>
        public Selection Selection => UIDocument.Selection;
    }
}
