using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Autodesk.Revit.DB;
using Revit22_Plugin.PlanSections.Services;

namespace Revit22_Plugin.PlanSections.ViewModels
{
    public class SectionDialogDtlLineViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        // SECTION TYPES
        public ObservableCollection<ViewFamilyType> SectionTypes { get; }
        public ViewFamilyType SelectedSectionType { get; set; }

        // TEMPLATES
        public ObservableCollection<View> ViewTemplates { get; }
        public View SelectedTemplate { get; set; }

        // USER INPUTS (V6)
        public string SectionPrefix { get; set; } = "Zone_00_Section";

        public double FarClipMm { get; set; } = 10;
        public double SearchThresholdMm { get; set; } = 2000;

        public double TopPaddingMm { get; set; } = 1000;
        public double BottomPaddingMm { get; set; } = 1000;

        /// <summary>
        /// NEW v6.1 – user bottom offset BELOW detected element.
        /// Example: 500mm → show more bottom region.
        /// </summary>
        public double BottomOffsetMm { get; set; } = 500;

        public bool IncludePlanLevelInName { get; set; } = true;
        public bool OpenAllAfterCreate { get; set; } = false;
        public bool DeleteLinesAfterCreate { get; set; } = false;

        public SnapSourceMode SelectedSnapSource { get; set; } = SnapSourceMode.HostAndLinked;

        public SectionDialogDtlLineViewModel(Document doc)
        {
            SectionTypes = new ObservableCollection<ViewFamilyType>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .Where(x => x.ViewFamily == ViewFamily.Section)
                    .OrderBy(x => x.Name)
            );

            SelectedSectionType = SectionTypes.FirstOrDefault();

            ViewTemplates = new ObservableCollection<View>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.IsTemplate && v.ViewType == ViewType.Section)
                    .OrderBy(v => v.Name)
            );

            SelectedTemplate = ViewTemplates.FirstOrDefault();
        }
    }
}
