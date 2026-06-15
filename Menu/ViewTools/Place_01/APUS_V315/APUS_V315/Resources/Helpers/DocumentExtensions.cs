using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Revit26_Plugin.APUS_V315.Helpers;

public static class DocumentExtensions
{
    public static bool IsProjectDocument(this Document document)
    {
        return document != null && !document.IsFamilyDocument;
    }

    public static bool TryGetActiveView(this UIDocument uiDocument, out View view)
    {
        view = uiDocument?.ActiveView;
        return view != null;
    }

    public static bool IsValidSection(this ViewSection view)
    {
        return view != null &&
               view.IsValidObject &&
               !view.IsTemplate &&
               view.ViewType == ViewType.Section;
    }

    public static string GetParameterValue(this Element element, BuiltInParameter param, string defaultValue = "")
    {
        try
        {
            var parameter = element.get_Parameter(param);
            if (parameter != null && parameter.HasValue)
                return parameter.AsValueString() ?? parameter.AsString() ?? defaultValue;
        }
        catch
        {
            // Ignore parameter errors
        }

        return defaultValue;
    }
}