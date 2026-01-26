using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BatchDwgFamilyLinker.Models;
using Microsoft.Win32;
using System.IO;

namespace BatchDwgFamilyLinker.ViewModels
{
    public partial class BatchLinkViewModel : ObservableObject
    {
        private readonly ExternalEvent _externalEvent;
        private readonly FamilyBatchProcessor _handler;

        public BatchLinkViewModel(UIApplication uiApp)
        {
            _handler = new FamilyBatchProcessor(this);
            _externalEvent = ExternalEvent.Create(_handler);
            LiveLog = string.Empty;
        }

        [ObservableProperty] private string familyFolderPath;
        [ObservableProperty] private string dwgFolderPath;

        // LOAD MODE (DEFAULT = LINK)
        [ObservableProperty] private bool isLinkMode = true;
        [ObservableProperty] private bool isImportMode;

        // PLACEMENT
        [ObservableProperty] private bool isOriginToOrigin = true;
        [ObservableProperty] private bool isCenterToCenter;

        [ObservableProperty] private int totalFamilies;
        [ObservableProperty] private int processedCount;
        [ObservableProperty] private int failedCount;

        [ObservableProperty] private string liveLog;

        public bool CanStart =>
            Directory.Exists(FamilyFolderPath) &&
            Directory.Exists(DwgFolderPath);

        partial void OnFamilyFolderPathChanged(string value) => OnPropertyChanged(nameof(CanStart));
        partial void OnDwgFolderPathChanged(string value) => OnPropertyChanged(nameof(CanStart));

        [RelayCommand]
        private void BrowseFamilyFolder()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Revit Family (*.rfa)|*.rfa",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
                FamilyFolderPath = Path.GetDirectoryName(dlg.FileName);
        }

        [RelayCommand]
        private void BrowseDwgFolder()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "AutoCAD (*.dwg)|*.dwg",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
                DwgFolderPath = Path.GetDirectoryName(dlg.FileName);
        }

        [RelayCommand]
        private void StartBatch()
        {
            _handler.Options = new BatchOptions
            {
                FamilyFolderPath = FamilyFolderPath,
                DwgFolderPath = DwgFolderPath,
                PlacementMode = IsOriginToOrigin
                    ? DwgPlacementMode.OriginToOrigin
                    : DwgPlacementMode.CenterToCenter,

                LoadMode = IsImportMode
                    ? DwgLoadMode.Import
                    : DwgLoadMode.Link   // DEFAULT
            };

            _externalEvent.Raise();
        }

        [RelayCommand]
        private void Close()
        {
            System.Windows.Application.Current.Windows[0]?.Close();
        }

        public void AppendLog(string msg)
        {
            LiveLog += $"{System.DateTime.Now:HH:mm:ss}  {msg}\n";
        }
    }
}
