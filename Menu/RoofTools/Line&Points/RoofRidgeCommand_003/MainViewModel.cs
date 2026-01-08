using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Revit22_Plugin.RRLPV3.Models;

namespace Revit22_Plugin.RRLPV3.ViewModels
{
    /// <summary>
    /// ViewModel for the Start Window.
    /// Minimal + pure MVVM (no Revit API here).
    /// </summary>
    public class MainViewModel : BaseViewModel
    {
        private string _statusMessage = "Ready to start...";

        public MainViewModel()
        {
            StartCommand = new RelayCommand(OnStart);
            CloseCommand = new RelayCommand(OnClose);
        }

        // -----------------------------
        // Properties
        // -----------------------------

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value, nameof(StatusMessage));
        }

        // -----------------------------
        // Commands
        // -----------------------------

        public ICommand StartCommand { get; }
        public ICommand CloseCommand { get; }

        // -----------------------------
        // Events used by the Window
        // -----------------------------

        /// <summary>
        /// Raised when Start is clicked.
        /// Window closes → external command continues with processing.
        /// </summary>
        public event Action RequestStart;

        /// <summary>
        /// Raised when Cancel is clicked.
        /// </summary>
        public event Action RequestClose;

        private void OnStart()
        {
            RequestStart?.Invoke();
        }

        private void OnClose()
        {
            RequestClose?.Invoke();
        }
    }

    /// <summary>
    /// Base class for INotifyPropertyChanged
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        protected bool SetProperty<T>(ref T field, T value, string propName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propName);
            return true;
        }
    }

    /// <summary>
    /// RelayCommand - minimal + clean
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _action;

        public RelayCommand(Action execute)
        {
            _action = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _action();

        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
