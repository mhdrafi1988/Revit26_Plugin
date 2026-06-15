using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Models.Enums;
using Revit26_Plugin.APUS_V315.ViewModels.Items;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V315.Models.Requests;

public record Margins(double LeftMm, double RightMm, double TopMm, double BottomMm);
public record Gaps(double HorizontalMm, double VerticalMm, double YToleranceMm);

public record PlacementRequest(
    ElementId TitleBlockId,
    IReadOnlyList<SectionItemViewModel> Sections,
    PlacementAlgorithm Algorithm,
    Margins Margins,
    Gaps Gaps,
    bool SkipPlacedViews,
    bool OpenSheetsAfterPlacement
);