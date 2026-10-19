using Autodesk.Revit.UI;
using Revit26_Plugin.ViewAutoRenamer.V005.Models;
using Revit26_Plugin.ViewAutoRenamer.V005.ViewModels;
using System.Collections.Generic;

namespace Revit26_Plugin.ViewAutoRenamer.V005.Services;

public static class RevitEventManager
{
    private static RenameViewsHandler? _renameHandler;
    private static ExternalEvent?      _renameEvent;

    private static LoadViewsHandler?   _loadHandler;
    private static ExternalEvent?      _loadEvent;

    /// <summary>
    /// Idempotent — safe to call from the command on every open, which is
    /// the repo-wide pattern (see Claude/ARCHITECTURE.md §2).
    /// </summary>
    public static void Initialize()
    {
        if (_renameHandler == null)
        {
            _renameHandler = new RenameViewsHandler();
            _renameEvent   = ExternalEvent.Create(_renameHandler);
        }

        if (_loadHandler == null)
        {
            _loadHandler = new LoadViewsHandler();
            _loadEvent   = ExternalEvent.Create(_loadHandler);
        }
    }

    public static void RequestRename(
        List<ViewItemViewModel> items,
        ViewsListViewModel vm)
    {
        Initialize();
        _renameHandler!.Payload = items;
        _renameHandler.Vm       = vm;
        _renameEvent!.Raise();
    }

    /// <summary>Pulls only the views matching <paramref name="criteria"/> — see ViewLoadCriteria.</summary>
    public static void RequestLoad(
        ViewLoadCriteria criteria,
        ViewsListViewModel vm)
    {
        Initialize();
        _loadHandler!.Criteria = criteria;
        _loadHandler.Vm        = vm;
        _loadEvent!.Raise();
    }
}
