using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Models.Requests;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using System;

namespace Revit26_Plugin.APUS_V315.Services.Implementation;

public sealed class PlacementOrchestrator : IPlacementOrchestrator
{
    private readonly IPlacementStrategyFactory _strategyFactory;
    private readonly ILogService _logService;
    private volatile bool _isCancelled;

    public PlacementOrchestrator(
        IPlacementStrategyFactory strategyFactory,
        ILogService logService)
    {
        _strategyFactory = strategyFactory ?? throw new ArgumentNullException(nameof(strategyFactory));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public PlacementResult Execute(Document document, PlacementRequest request)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _isCancelled = false;
        _logService.LogInfo("????????????????????????????????????????????????");
        _logService.LogInfo($"?? STARTING PLACEMENT - {request.Algorithm}");
        _logService.LogInfo($"   • Sections: {request.Sections.Count}");
        _logService.LogInfo($"   • Skip placed: {request.SkipPlacedViews}");
        _logService.LogInfo("????????????????????????????????????????????????");

        using var transaction = new Transaction(document, "APUS V315 – Section Placement");
        transaction.Start();

        try
        {
            var strategy = _strategyFactory.Create(request.Algorithm);
            _logService.LogInfo($"?? Strategy: {strategy.Name}");
            _logService.LogInfo($"?? {strategy.Description}");

            var result = strategy.Place(
                document,
                request.Sections,
                request.TitleBlockId,
                request.Margins,
                request.Gaps,
                () => _isCancelled);

            if (!result.Success || _isCancelled)
            {
                if (_isCancelled)
                {
                    _logService.LogWarning("?? Operation cancelled by user");
                    transaction.RollBack();
                    return new PlacementResult(false, 0, Array.Empty<string>(), "Cancelled");
                }

                _logService.LogError($"? Placement failed: {result.ErrorMessage}");
                transaction.RollBack();
                return result;
            }

            transaction.Commit();

            _logService.LogSuccess($"? Transaction committed: {result.ViewsPlaced} views placed");
            _logService.LogInfo($"?? Sheets used: {string.Join(", ", result.SheetNumbers)}");

            return result;
        }
        catch (Exception ex)
        {
            _logService.LogError($"? Placement failed: {ex.Message}");
            try { transaction.RollBack(); } catch { }
            return new PlacementResult(false, 0, Array.Empty<string>(), ex.Message);
        }
    }

    public void Cancel() => _isCancelled = true;
}