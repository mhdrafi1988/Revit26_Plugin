using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V315.Models.Requests;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using Revit26_Plugin.APUS_V315.ViewModels.Main;

namespace Revit26_Plugin.APUS_V315.ExternalEvents;

public sealed class SectionPlacementHandler : IExternalEventHandler
{
    private readonly IPlacementOrchestrator _orchestrator;
    private readonly AutoPlaceSectionsViewModel _viewModel;
    private PlacementRequest? _request;

    public SectionPlacementHandler(
        IPlacementOrchestrator orchestrator,
        AutoPlaceSectionsViewModel viewModel)
    {
        _orchestrator = orchestrator ?? throw new System.ArgumentNullException(nameof(orchestrator));
        _viewModel = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
    }

    public void SetRequest(PlacementRequest request) => _request = request;

    public void Execute(UIApplication app)
    {
        if (_request == null || app.ActiveUIDocument?.Document == null)
            return;

        var result = _orchestrator.Execute(app.ActiveUIDocument.Document, _request);
        _viewModel.OnPlacementComplete(result);
    }

    public string GetName() => "APUS V315 – Section Placement Handler";
}