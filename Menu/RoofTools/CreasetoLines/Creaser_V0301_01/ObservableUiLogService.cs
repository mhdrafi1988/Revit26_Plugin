// File: ObservableUiLogService.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.Creaser_V32.Helpers
{
    public class ObservableUiLogService : INotifyPropertyChanged
    {
        private ObservableCollection<string> _logEntries;
        private string _fullText;

        public ObservableCollection<string> LogEntries
        {
            get => _logEntries;
            set
            {
                _logEntries = value;
                OnPropertyChanged();
            }
        }

        public string FullText
        {
            get => _fullText;
            private set
            {
                _fullText = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableUiLogService()
        {
            LogEntries = new ObservableCollection<string>();
            FullText = string.Empty;
        }

        public void Write(string message)
        {
            // Add timestamp
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var formattedMessage = $"[{timestamp}] {message}";

            // Add to collection
            LogEntries.Insert(0, formattedMessage); // Insert at beginning for newest-first

            // Update full text
            FullText = string.Join(Environment.NewLine, LogEntries);
        }

        public void Clear()
        {
            LogEntries.Clear();
            FullText = string.Empty;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}