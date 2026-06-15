using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionAutoRenamer.09.Events;
using Revit26_Plugin.SectionAutoRenamer.09.ViewModels;
using Revit26_Plugin.SectionAutoRenamer.09.Views;
using System.Linq;

namespace Revit26_Plugin.SectionAutoRenamer.09.Commands;

[Transaction(TransactionMode.Manual)]
public class OpenSectionManagerCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
    {
        RevitEventManager.Initialize();

        var uidoc = c.Application.ActiveUIDocument;
        var doc   = uidoc.Document;

        var activeSheet       = uidoc.ActiveView as ViewSheet;
        string activeSheetNum = activeSheet?.SheetNumber ?? "";

        // Only collect true section views (not callouts — both are ViewSection subclasses)
        var sections = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSection))
            .Cast<ViewSection>()
            .Where(v => !v.IsTemplate && v.ViewType == ViewType.Section)
            .Select(v => new SectionItemViewModel(v))
            .ToList();

        var vm = new SectionsListViewModel(sections, activeSheetNum);
        new SectionsListWindow(vm).Show();

        return Result.Succeeded;
    }
}
