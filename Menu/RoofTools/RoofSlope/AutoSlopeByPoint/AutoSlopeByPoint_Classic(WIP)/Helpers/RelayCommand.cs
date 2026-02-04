using System;
using System.Windows.Input;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Helpers
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;
        private readonly Action _executeNoParam;
        private readonly Func<bool> _canExecuteNoParam;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _executeNoParam = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecuteNoParam = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_canExecuteNoParam != null)
                return _canExecuteNoParam();

            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            if (_executeNoParam != null)
                _executeNoParam();
            else
                _execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}