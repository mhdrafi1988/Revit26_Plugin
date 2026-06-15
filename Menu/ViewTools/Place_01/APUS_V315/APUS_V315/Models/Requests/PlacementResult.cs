using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V315.Models.Requests;

public record PlacementResult(
    bool Success,
    int ViewsPlaced,
    IReadOnlyList<string> SheetNumbers,
    string? ErrorMessage = null
);