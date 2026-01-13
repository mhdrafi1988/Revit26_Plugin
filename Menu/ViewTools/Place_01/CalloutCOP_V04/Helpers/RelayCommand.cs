using System;
using System.Windows.Input;

namespace Revit26_Plugin.CalloutCOP_V04.Helpers
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object _) => _canExecute?.Invoke() ?? true;
        public void Execute(object _) => _execute();

        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
