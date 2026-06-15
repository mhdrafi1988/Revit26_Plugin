using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.ViewModels.Items;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V315.Services.Abstractions;

public interface ISectionCollector
{
    IReadOnlyList<SectionItemViewModel> Collect(Document document);
}