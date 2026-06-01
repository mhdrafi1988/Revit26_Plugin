// =======================================================
// File: Commands/RelayCommand.cs
// Description: RelayCommand implementation for MVVM
// =======================================================

using System;
using System.Windows.Input;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        private readonly Action<object> _executeWithParam;

        public event EventHandler CanExecuteChanged;

        // Parameterless constructor
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // With parameter constructor
        public RelayCommand(Action<object> executeWithParam, Func<bool> canExecute = null)
        {
            _executeWithParam = executeWithParam ?? throw new ArgumentNullException(nameof(executeWithParam));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            if (_execute != null)
                _execute();
            else if (_executeWithParam != null)
                _executeWithParam(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}