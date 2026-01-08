using System;
using System.ComponentModel;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _statusMessage = "Ready to process roof ridge lines.";

        public MainViewModel()
        {
            StartCommand = new RelayCommand(OnStart);
            CloseCommand = new RelayCommand(OnClose);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        public RelayCommand StartCommand { get; }
        public RelayCommand CloseCommand { get; }

        public event Action RequestStart;
        public event Action RequestClose;

        private void OnStart() => RequestStart?.Invoke();
        private void OnClose() => RequestClose?.Invoke();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public class RelayCommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute();
    }
}