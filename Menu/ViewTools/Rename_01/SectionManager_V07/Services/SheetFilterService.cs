using Revit26_Plugin.SectionManager_V07.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SectionManager_V07.Services
{
    public class SheetFilterService
    {
        public IEnumerable<SectionInfo> Apply(
            IEnumerable<SectionInfo> sections,
            HashSet<string> selectedSheets)
        {
            if (selectedSheets == null || selectedSheets.Count == 0)
                return sections;

            return sections.Where(s =>
                (s.SheetNumber == null && selectedSheets.Contains("None")) ||
                (s.SheetNumber != null && selectedSheets.Contains(s.SheetNumber)));
        }
    }
}
