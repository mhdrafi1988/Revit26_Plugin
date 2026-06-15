using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using Revit26_Plugin.APUS_V315.Services.Calculators;
using Revit26_Plugin.APUS_V315.Services.Implementation;
using Revit26_Plugin.APUS_V315.Services.Strategies;
using Revit26_Plugin.APUS_V315.ViewModels.Main;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V315.DependencyInjection;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _singletons = new();
    private static readonly Dictionary<Type, Func<object>> _transients = new();
    private static bool _isInitialized;
    private static UIDocument? _currentUidoc;

    static ServiceLocator()
    {
        // Register all services
        RegisterServices();
    }

    private static void RegisterServices()
    {
        // Core Services - Singletons
        _singletons[typeof(ILogService)] = new LogService();
        _singletons[typeof(IDialogService)] = new WpfDialogService();
        _singletons[typeof(IViewSizeCalculator)] = new ViewSizeCalculator();

        // GridLayoutCalculator depends on IViewSizeCalculator
        _singletons[typeof(IGridLayoutCalculator)] = new GridLayoutCalculator(Get<IViewSizeCalculator>());

        // Strategies - Transients
        _transients[typeof(GridPlacementStrategy)] = () => new GridPlacementStrategy(
            Get<ISheetService>(),
            Get<IViewSizeCalculator>(),
            Get<IGridLayoutCalculator>(),
            Get<ILogService>());

        _transients[typeof(BinPackingPlacementStrategy)] = () => new BinPackingPlacementStrategy(
            Get<ISheetService>(),
            Get<IViewSizeCalculator>(),
            Get<ILogService>());

        _transients[typeof(OrderedPlacementStrategy)] = () => new OrderedPlacementStrategy(
            Get<ISheetService>(),
            Get<IViewSizeCalculator>(),
            Get<ILogService>());

        _transients[typeof(AdaptiveGridPlacementStrategy)] = () => new AdaptiveGridPlacementStrategy(
            Get<ISheetService>(),
            Get<IViewSizeCalculator>(),
            Get<ILogService>());

        // Strategy Factory - Singleton (pass a service provider)
        _singletons[typeof(IPlacementStrategyFactory)] = new PlacementStrategyFactory(new ServiceProviderAdapter());

        // Orchestrator - Singleton
        _singletons[typeof(IPlacementOrchestrator)] = new PlacementOrchestrator(
            Get<IPlacementStrategyFactory>(),
            Get<ILogService>());

        // Collectors - Transient
        _transients[typeof(ISectionCollector)] = () => new SectionCollector(Get<IViewSizeCalculator>());
        _transients[typeof(ITitleBlockCollector)] = () => new TitleBlockCollector();

        // Sheet Service - Transient
        _transients[typeof(ISheetService)] = () => new SheetService();
    }

    public static void Initialize(UIDocument uidoc)
    {
        if (_isInitialized)
            return;

        _currentUidoc = uidoc;
        _singletons[typeof(UIDocument)] = uidoc;

        // ViewModel - Transient (created each time)
        _transients[typeof(AutoPlaceSectionsViewModel)] = () => new AutoPlaceSectionsViewModel(
            Get<UIDocument>(),
            Get<ILogService>(),
            Get<IDialogService>(),
            Get<IPlacementOrchestrator>(),
            Get<ISectionCollector>(),
            Get<ITitleBlockCollector>());

        _isInitialized = true;
    }

    public static T Get<T>() where T : notnull
    {
        var type = typeof(T);

        // Check singletons first
        if (_singletons.TryGetValue(type, out var singleton))
            return (T)singleton;

        // Check transients
        if (_transients.TryGetValue(type, out var factory))
            return (T)factory();

        throw new InvalidOperationException($"Service of type {type} not registered.");
    }

    public static object Get(Type type)
    {
        // Check singletons first
        if (_singletons.TryGetValue(type, out var singleton))
            return singleton;

        // Check transients
        if (_transients.TryGetValue(type, out var factory))
            return factory();

        throw new InvalidOperationException($"Service of type {type} not registered.");
    }

    public static void Cleanup()
    {
        foreach (var disposable in _singletons.Values.OfType<IDisposable>())
            disposable.Dispose();

        _singletons.Clear();
        _transients.Clear();
        _currentUidoc = null;
        _isInitialized = false;

        // Re-register services for next use
        RegisterServices();
    }

    // Helper method to check if a service is registered
    public static bool IsRegistered<T>()
    {
        var type = typeof(T);
        return _singletons.ContainsKey(type) || _transients.ContainsKey(type);
    }

    // Helper method to get all registered service types
    public static IEnumerable<Type> GetRegisteredServices()
    {
        return _singletons.Keys.Concat(_transients.Keys).Distinct();
    }

    // Add this class to adapt ServiceLocator to IServiceProvider
    private sealed class ServiceProviderAdapter : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            try
            {
                return ServiceLocator.Get(serviceType);
            }
            catch
            {
                return null;
            }
        }
    }
}