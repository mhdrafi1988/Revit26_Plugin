using Revit26_Plugin.APUS_V315.DependencyInjection;
using Revit26_Plugin.APUS_V315.ViewModels.Main;
using System;

namespace Revit26_Plugin.APUS_V315.ViewModels.Base;

public static class ViewModelLocator
{
    private static AutoPlaceSectionsViewModel? _autoPlaceSections;

    public static AutoPlaceSectionsViewModel AutoPlaceSections
    {
        get
        {
            _autoPlaceSections ??= ServiceLocator.Get<AutoPlaceSectionsViewModel>();
            return _autoPlaceSections;
        }
    }

    public static void Cleanup()
    {
        if (_autoPlaceSections is IDisposable disposable)
            disposable.Dispose();

        _autoPlaceSections = null;
    }
}