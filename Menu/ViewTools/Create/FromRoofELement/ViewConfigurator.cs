using Autodesk.Revit.DB;
using Revit22_Plugin.AutoRoofSections.Models;
using System;

namespace Revit22_Plugin.AutoRoofSections.Services
{
    public class ViewConfigurator
    {
        private readonly SectionSettings _settings;

        public ViewConfigurator(SectionSettings settings)
        {
            _settings = settings;
        }

        public void Apply(ViewSection sec, int scale)
        {
            sec.Scale = scale;

            if (_settings.SelectedViewTemplate != null)
            {
                sec.ViewTemplateId = _settings.SelectedViewTemplate.Id;
            }
        }
    }
}
