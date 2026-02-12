using Revit26_Plugin.APUS_V315.Models.Enums;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V315.Services.Strategies;

public sealed class PlacementStrategyFactory : IPlacementStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<PlacementAlgorithm, Type> _strategies;

    public PlacementStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        _strategies = new Dictionary<PlacementAlgorithm, Type>
        {
            [PlacementAlgorithm.Grid] = typeof(GridPlacementStrategy),
            [PlacementAlgorithm.BinPacking] = typeof(BinPackingPlacementStrategy),
            [PlacementAlgorithm.Ordered] = typeof(OrderedPlacementStrategy),
            [PlacementAlgorithm.AdaptiveGrid] = typeof(AdaptiveGridPlacementStrategy)
        };
    }

    public IPlacementStrategy Create(PlacementAlgorithm algorithm)
    {
        if (!_strategies.TryGetValue(algorithm, out var type))
            throw new NotSupportedException($"Algorithm {algorithm} is not supported");

        return (IPlacementStrategy)_serviceProvider.GetService(type)
            ?? throw new InvalidOperationException($"Failed to create strategy for {algorithm}");
    }

    public IReadOnlyList<IPlacementStrategy> GetAllStrategies()
    {
        var strategies = new List<IPlacementStrategy>();
        foreach (var type in _strategies.Values)
        {
            if (_serviceProvider.GetService(type) is IPlacementStrategy strategy)
                strategies.Add(strategy);
        }
        return strategies;
    }
}