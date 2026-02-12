using Revit26_Plugin.APUS_V315.Models.Enums;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V315.Services.Abstractions;

public interface IPlacementStrategyFactory
{
    IPlacementStrategy Create(PlacementAlgorithm algorithm);
    IReadOnlyList<IPlacementStrategy> GetAllStrategies();
}