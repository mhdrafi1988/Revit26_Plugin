// File: LoggingExtensions.cs
using Revit26_Plugin.APUS_V317.Services;
using Revit26_Plugin.APUS_V317.ViewModels;
using System;

namespace Revit26_Plugin.APUS_V317.Extensions
{
    public static class LoggingExtensions
    {
        public static void LogDebug(this AutoPlaceSectionsViewModel vm, string message)
        {
            vm?.LogInfo($"[DEBUG] {message}");
        }

        public static void LogSectionDetails(this AutoPlaceSectionsViewModel vm, SectionItemViewModel section)
        {
            if (vm != null && section != null)
            {
                var size = ViewSizeService.Calculate(section.View);
                vm.LogInfo($"   ?? {section.ViewName}");
                vm.LogInfo($"   ?  ?? Size: {size.WidthFt:F3} × {size.HeightFt:F3} ft");
                vm.LogInfo($"   ?  ?? Scale: 1:{section.Scale}");
                vm.LogInfo($"   ?  ?? Placed: {section.IsPlaced}");
                vm.LogInfo($"   ?  ?? Sheet: {section.SheetNumber}");
            }
        }
    }
}