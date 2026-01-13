using System;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionManager_V07.Services;

namespace Revit26_Plugin.SectionManager_V07.ViewModels
{
    public class SectionManagerViewModel : BaseViewModel, IDisposable
    {
        private readonly UIDocument _uiDoc;
        private readonly SectionCollectorService _collector;

        public ObservableCollection<SectionItemViewModel> FilteredSections { get; }
            = new ObservableCollection<SectionItemViewModel>();

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; RaisePropertyChanged(); }
        }

        public SectionManagerViewModel(UIApplication uiApp)
        {
            _uiDoc = uiApp.ActiveUIDocument
                ?? throw new InvalidOperationException("No active document.");

            _collector = new SectionCollectorService();

            LoadSections();
        }

        private void LoadSections()
        {
            FilteredSections.Clear();

            var sections = _collector.Collect(_uiDoc);

            int index = 1;
            foreach (var info in sections)
            {
                FilteredSections.Add(
                    new SectionItemViewModel(info.ElementId, info.OriginalName)
                    {
                        Index = index++
                    });
            }

            StatusText = $"Loaded {FilteredSections.Count} sections";
        }

        public void Dispose()
        {
            FilteredSections.Clear();
        }
    }
}
