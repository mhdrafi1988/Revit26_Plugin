using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Models.Requests;
using Revit26_Plugin.APUS_V315.ViewModels.Items;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V315.Services.Abstractions;

public interface IPlacementStrategy
{
    string Name { get; }
    string Description { get; }

    PlacementResult Place(
        Document document,
        IReadOnlyList<SectionItemViewModel> sections,
        ElementId titleBlockId,
        Margins margins,
        Gaps gaps,
        Func<bool> isCancelled
    );
}