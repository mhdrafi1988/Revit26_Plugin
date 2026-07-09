using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using Revit26_Plugin.DetailLIneDimensions.V005.Models;

namespace Revit26_Plugin.DetailLIneDimensions.V005.Services
{
    /// <summary>
    /// Runs DimensionCreationService inside a valid Revit API execution context.
    /// The window is shown non-modally (Show, not ShowDialog), so the button click
    /// happens outside the original Execute() context — every Revit API call from
    /// here on must be routed through this handler via ExternalEvent.Raise().
    /// </summary>
    public class GenerateDimensionsEventHandler : IExternalEventHandler
    {
        public ComboItem DetailType { get; set; }
        public ComboItem DimensionType { get; set; }

        public Action<DimensionResult> OnCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var view = app.ActiveUIDocument.ActiveView;

            var result = DimensionCreationService.CreateDimensions(
                doc, view, DetailType, DimensionType);

            OnCompleted?.Invoke(result);
        }

        public string GetName() => "Generate Detail Line Dimensions";
    }
}
