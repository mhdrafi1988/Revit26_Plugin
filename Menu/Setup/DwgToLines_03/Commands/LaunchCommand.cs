// ==============================================
// File: LaunchCommand.cs
// Layer: Commands
// Namespace: Revit26_Plugin.DwgSymbolicConverter_V03.Commands
// ==============================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DwgSymbolicConverter_V03.Helpers;
using Revit26_Plugin.DwgSymbolicConverter_V03.Views;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Commands
{
    /// <summary>
    /// Entry point for the DWG Symbolic Converter tool.
    /// Enforces Family Editor context and launches the UI.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LaunchCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            // --------------------------------------------------
            // HARD RULE: Family Editor ONLY
            // --------------------------------------------------
            if (!RevitContextValidator.IsFamilyEditor(uiApp, out message))
            {
                return Result.Failed;
            }

            // --------------------------------------------------
            // Launch WPF window (modal)
            // --------------------------------------------------
            var view = new DwgSymbolicConverterView(uiApp);
            view.ShowDialog();

            return Result.Succeeded;
        }
    }
}
