using Autodesk.Revit.DB;
using Revit26_Plugin.ParaManager.V003.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.ParaManager.V003.Services
{
    /// <summary>
    /// Supplies the category list offered in the multi-select combo.
    ///
    /// ASSUMPTION (flagged, not baked in silently): the mockup only showed
    /// Roofs/Floors/Walls as example chips. This list below is a reasonable
    /// "model element categories relevant to WAMI-style tools" starter set,
    /// mirrored from the categories your other tools (AutoSlopeByPoint,
    /// RoofRidgeLines, FloorsAndRoofFromLinkedRooms) already operate on.
    /// Confirm this list, or tell me to pull the FULL Document.Settings.Categories
    /// list dynamically instead (every category in the project, not a fixed set).
    /// </summary>
    public static class CategoryProvider
    {
        public static List<CategoryInfo> GetAvailableCategories()
        {
            return new List<CategoryInfo>
            {
                new CategoryInfo("Roofs", BuiltInCategory.OST_Roofs),
                new CategoryInfo("Floors", BuiltInCategory.OST_Floors),
                new CategoryInfo("Walls", BuiltInCategory.OST_Walls),
                new CategoryInfo("Ceilings", BuiltInCategory.OST_Ceilings),
                new CategoryInfo("Structural Framing", BuiltInCategory.OST_StructuralFraming),
                new CategoryInfo("Structural Columns", BuiltInCategory.OST_StructuralColumns),
                new CategoryInfo("Curtain Panels", BuiltInCategory.OST_CurtainWallPanels),
                new CategoryInfo("Doors", BuiltInCategory.OST_Doors),
                new CategoryInfo("Windows", BuiltInCategory.OST_Windows),
                new CategoryInfo("Generic Models", BuiltInCategory.OST_GenericModel),
            };
        }
    }
}
