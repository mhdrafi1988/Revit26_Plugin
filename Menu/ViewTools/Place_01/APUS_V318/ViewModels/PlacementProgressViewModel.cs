// File: PlacementProgressViewModel.cs
// FIX: Make setters public for UI updates
using System.ComponentModel;

namespace Revit26_Plugin.APUS_V318.ViewModels
{
    public class PlacementProgressViewModel : BaseViewModel
    {
        private int _total;
        public int Total
        {
            get => _total;
            set => SetField(ref _total, value);  // Made setter public
        }

        private int _current;
        public int Current
        {
            get => _current;
            set => SetField(ref _current, value);  // Made setter public
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

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double Percentage => Total > 0 ? (Current * 100.0) / Total : 0;

        /// <summary>
        /// Initialize progress state
        /// </summary>
        public void Reset(int totalCount)
        {
            Total = totalCount;
            Current = 0;
            IsCancelled = false;
            CurrentOperation = "Starting placement...";
        }

        /// <summary>
        /// Advance progress by one step
        /// </summary>
        public void Step(string operation = "")
        {
            if (Current < Total)
            {
                Current++;
                if (!string.IsNullOrEmpty(operation))
                {
                    CurrentOperation = operation;
                }
            }
        }

        /// <summary>
        /// Update progress with specific value
        /// </summary>
        public void Update(int value, string operation = "")
        {
            if (value >= 0 && value <= Total)
            {
                Current = value;
                if (!string.IsNullOrEmpty(operation))
                {
                    CurrentOperation = operation;
                }
            }
        }

        /// <summary>
        /// Request cancellation
        /// </summary>
        public void Cancel()
        {
            IsCancelled = true;
            CurrentOperation = "Cancelling...";
        }

        /// <summary>
        /// Check if operation should continue
        /// </summary>
        public bool ShouldContinue => !IsCancelled && Current < Total;
    }
}