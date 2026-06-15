using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Models.Requests;

namespace Revit26_Plugin.APUS_V315.Services.Abstractions;

public interface IPlacementOrchestrator
{
    PlacementResult Execute(Document document, PlacementRequest request);
    void Cancel();
}