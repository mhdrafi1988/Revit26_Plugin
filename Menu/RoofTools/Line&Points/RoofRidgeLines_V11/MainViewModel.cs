using System;
using System.ComponentModel;
using System.Windows.Input;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event Action RequestStart;
        public event Action RequestClose;

        public ICommand StartCommand => new Relay(() => RequestStart?.Invoke());
        public ICommand CloseCommand => new Relay(() => RequestClose?.Invoke());

        private string _status = "Ready";
        public string StatusMessage
        {
            get => _status;
            set { _status = value; PropertyChanged?.Invoke(this, new(nameof(StatusMessage))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private class Relay : ICommand
        {
            private readonly Action _a;
            public Relay(Action a) => _a = a;
            public bool CanExecute(object p) => true;
            public void Execute(object p) => _a();
            public event EventHandler CanExecuteChanged { add { } remove { } }
        }
    }
}
