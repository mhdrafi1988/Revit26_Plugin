using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using Revit26_Plugin.APUS_V315.ViewModels.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V315.Services.Implementation;

public sealed class TitleBlockCollector : ITitleBlockCollector
{
    public IReadOnlyList<TitleBlockItemViewModel> Collect(Document document)
    {
        if (document == null)
            return Array.Empty<TitleBlockItemViewModel>();

        return new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .OrderBy(x => x.FamilyName)
            .ThenBy(x => x.Name)
            .Select(x => new TitleBlockItemViewModel(x))
            .ToList();
    }
}