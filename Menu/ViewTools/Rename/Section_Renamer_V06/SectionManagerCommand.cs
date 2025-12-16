using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SARV6.Events;
using Revit26_Plugin.SARV6.ViewModels;
using Revit26_Plugin.SARV6.Views;
using System.Linq;

namespace Revit26_Plugin.SARV6.Commands;

[Transaction(TransactionMode.Manual)]
public class OpenSectionManagerCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
    {
        RevitEventManager.Initialize();

        var uidoc = c.Application.ActiveUIDocument;
        var doc = uidoc.Document;

        var activeSheet = uidoc.ActiveView as ViewSheet;
        string activeSheetNumber = activeSheet?.SheetNumber;

        var sections = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSection))
            .Cast<ViewSection>()
            .Where(v => !v.IsTemplate)
            .Select((v, i) => new SectionItemViewModel(v, i + 1))
            .ToList();

        var vm = new SectionsListViewModel(sections, activeSheetNumber);
        new SectionsListWindow(vm).Show();

        return Result.Succeeded;
    }
}
