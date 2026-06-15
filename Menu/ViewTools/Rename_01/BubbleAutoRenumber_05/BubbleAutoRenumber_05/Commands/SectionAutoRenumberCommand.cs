using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionAutoRenumber.Handlers;
using Revit26_Plugin.SectionAutoRenumber.Views;
using System;

namespace Revit26_Plugin.SectionAutoRenumber.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SectionAutoRenumberCommand : IExternalCommand
    {
        // Single window instance — prevents opening duplicates
        private static SectionAutoRenumberWindow? _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (_window != null)
                {
                    _window.Activate();
                    return Result.Succeeded;
                }

                UIApplication uiapp = commandData.Application;
                UIDocument    uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null)
                {
                    TaskDialog.Show("Section Auto Renumber", "No active document.");
                    return Result.Failed;
                }

                var handler       = new SectionAutoRenumberHandler();
                var externalEvent = ExternalEvent.Create(handler);

                _window = new SectionAutoRenumberWindow(uidoc, uiapp, handler, externalEvent);
                _window.Closed += (_, _) => _window = null;
                _window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Section Auto Renumber — Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
