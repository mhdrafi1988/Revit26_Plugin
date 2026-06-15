using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Models.Entities;
using Revit26_Plugin.APUS_V315.Models.Requests;

namespace Revit26_Plugin.APUS_V315.Services.Abstractions;

public interface ISheetService
{
    ViewSheet CreateSheet(Document document, ElementId titleBlockId, int index);
    ViewSheet CreateSheet(Document document, ElementId titleBlockId, string number, string name);
    SheetPlacementArea CalculatePlacementArea(ViewSheet sheet, Margins margins);
    void DeleteSheet(Document document, ElementId sheetId);
}