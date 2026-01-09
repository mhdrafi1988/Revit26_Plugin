using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.AutoRoofSections.Models;
using Revit22_Plugin.AutoRoofSections.MVVM;
using Revit22_Plugin.AutoRoofSections.Utils;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Documents;
using System.Windows.Input;

namespace Revit22_Plugin.AutoRoofSections.ViewModels
{
    public class RoofSectionViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;
        private readonly UIApplication _uiapp;
        private readonly Document _doc;

        // =============================================================
        // UI-Bound Properties
        // =============================================================

        private string _prefix = "ROOF_";
        public string Prefix
        {
            get => _prefix;
            set { _prefix = value; OnPropertyChanged(); }
        }

        private int _scale = 20;
        public int Scale
        {
            get => _scale;
            set { _scale = value; OnPropertyChanged(); }
        }

        private double _minEdgeLengthMm = 100;
        public double MinEdgeLengthMm
        {
            get => _minEdgeLengthMm;
            set { _minEdgeLengthMm = value; OnPropertyChanged(); }
        }

        private bool _includeTimestamp = true;
        public bool IncludeTimestamp
        {
            get => _includeTimestamp;
            set { _includeTimestamp = value; OnPropertyChanged(); }
        }

        private string _selectedDirectionMode = "Auto";
        public string SelectedDirectionMode
        {
            get => _selectedDirectionMode;
            set { _selectedDirectionMode = value; OnPropertyChanged(); }
        }

        // View Templates
        public ObservableCollection<View> ViewTemplates { get; }
        private View _selectedViewTemplate;
        public View SelectedViewTemplate
        {
            get => _selectedViewTemplate;
            set { _selectedViewTemplate = value; OnPropertyChanged(); }
        }

        // =============================================================
        // Logging
        // =============================================================
        public FlowDocument LogDocument { get; } = new FlowDocument();

        public void Log(string msg)
        {
            LogDocument.AppendLogLine(msg);
        }

        // =============================================================
        // Commands
        // =============================================================
        public ICommand RunCommand { get; }

        // =============================================================
        // Constructor
        // =============================================================
        public RoofSectionViewModel(UIDocument uidoc, UIApplication uiapp)
        {
            _uidoc = uidoc;
            _uiapp = uiapp;
            _doc = uidoc.Document;

            // Load view templates (non-templates, real section templates)
            ViewTemplates = new ObservableCollection<View>(
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.IsTemplate)
                    .OrderBy(v => v.Name)
            );

            SelectedViewTemplate = ViewTemplates.Count > 0 ? ViewTemplates[0] : null;

            // Commands
            RunCommand = new RelayCommand(_ => ExecuteRun());
        }

        // =============================================================
        // Run logic (UI thread → ExternalEvent)
        // =============================================================
        private void ExecuteRun()
        {
            try
            {
                Log("Starting Auto Roof Edge Sections...");
                var settings = BuildSettings();

                RoofSectionsEventManager.Handler.Payload = settings;
                RoofSectionsEventManager.Event.Raise();

                Log("Request dispatched to Revit engine...");
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
            }
        }

        // =============================================================
        // Build settings model
        // =============================================================
        private SectionSettings BuildSettings()
        {
            return new SectionSettings
            {
                Prefix = this.Prefix,
                Scale = this.Scale,
                MinEdgeLengthMm = this.MinEdgeLengthMm,
                IncludeTimestamp = this.IncludeTimestamp,
                DirectionMode = this.SelectedDirectionMode,
                SelectedViewTemplate = this.SelectedViewTemplate,
                Uidoc = _uidoc,
                Uiapp = _uiapp,
                LogAction = this.Log
            };
        }

        // =============================================================
        // INotifyPropertyChanged
        // =============================================================
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    // ======================================================================
    // RelayCommand Implementation (Simple)
    // ======================================================================
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;

        public RelayCommand(Action<object> execute)
        {
            _execute = execute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }
}
