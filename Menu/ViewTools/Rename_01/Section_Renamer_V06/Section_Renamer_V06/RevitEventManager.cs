using Autodesk.Revit.UI;
using Revit26_Plugin.SARV6.ViewModels;
using System.Collections.Generic;

namespace Revit26_Plugin.SARV6.Events;

public static class RevitEventManager
{
    private static RenameSectionsHandler _handler;
    private static ExternalEvent _event;

    public static void Initialize()
    {
        if (_handler != null) return;

        _handler = new RenameSectionsHandler();
        _event = ExternalEvent.Create(_handler);
    }

    public static void RequestRename(
        List<SectionItemViewModel> items,
        SectionsListViewModel vm)
    {
        _handler.Payload = items;
        _handler.Vm = vm;
        _event.Raise();
    }
}
