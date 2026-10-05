using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.PlanFromScopeBox.V004.UI.Views;
using Revit26_Plugin.Utilities;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.PlanFromScopeBox.V004.Commands
{
    /// <summary>
    /// Ribbon entry point for the Plan From Scope Box tool. Opens the modeless window.
    /// The window owns its ViewModel, which creates the ExternalEvent inside a valid API
    /// context (the command execution) — never lazily inside Execute().
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class PlanFromScopeBoxCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteUnguarded);

        private Result ExecuteUnguarded(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;

                if (uidoc == null || uidoc.Document == null)
                {
                    message = "No active Revit document.";
                    return Result.Failed;
                }

                var window = new PlanFromScopeBoxWindow(uiApp);
                window.Show();   // modeless — user closes manually
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("PlanFromScopeBoxCommand", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
