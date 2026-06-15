// File: ViewModels/PlacementProgressViewModel.cs
namespace Revit26_Plugin.APUS_V330.ViewModels
{
    public class PlacementProgressViewModel : BaseViewModel
    {
        private int _total;
        public int Total
        {
            get => _total;
            set => SetField(ref _total, value);
        }

        private int _current;
        public int Current
        {
            get => _current;
            set => SetField(ref _current, value);
        }

        private bool _isCancelled;
        public bool IsCancelled
        {
            get => _isCancelled;
            private set => SetField(ref _isCancelled, value);
        }

        private string _currentOperation = "";
        public string CurrentOperation
        {
            get => _currentOperation;
            set => SetField(ref _currentOperation, value);
        }

        public double Percentage => Total > 0 ? (Current * 100.0) / Total : 0;

        public void Reset(int totalCount)
        {
            Total            = totalCount;
            Current          = 0;
            IsCancelled      = false;
            CurrentOperation = "Starting placement...";
        }

        public void Step(string operation = "")
        {
            if (Current < Total)
            {
                Current++;
                if (!string.IsNullOrEmpty(operation))
                    CurrentOperation = operation;
            }
        }

        public void Update(int value, string operation = "")
        {
            if (value >= 0 && value <= Total)
            {
                Current = value;
                if (!string.IsNullOrEmpty(operation))
                    CurrentOperation = operation;
            }
        }

        public void Cancel()
        {
            IsCancelled      = true;
            CurrentOperation = "Cancelling...";
        }

        public bool ShouldContinue => !IsCancelled && Current < Total;
    }
}
