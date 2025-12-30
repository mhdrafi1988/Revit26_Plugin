using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// Strategy interface for corner-to-drain pathfinding.
    /// Implementations must NOT call Revit API.
    /// </summary>
    public interface IPathFindingStrategy
    {
        PathResult FindPath(
            RoofGraph graph,
            GraphNode startNode);
    }
}
