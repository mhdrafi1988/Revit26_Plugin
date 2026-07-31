using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Revit26_Plugin.RoofCreationIsolationTest.V001.Infrastructure.Helpers
{
    /// <summary>
    /// Wraps an ObservableCollection&lt;LogEntry&gt; so every Add() is marshaled onto the
    /// UI (Dispatcher) thread before mutating the collection.
    ///
    /// Root cause this fixes: RunTestHandler.Execute() and RoofTestService.RunTest()
    /// run on the Revit API thread (invoked via ExternalEvent), not the WPF UI thread.
    /// ObservableCollection's CollectionChanged event must be raised on the thread that
    /// owns the bound ListBox's Dispatcher, or WPF either silently drops the update or
    /// throws NotSupportedException. Every direct "Log.Add(...)" call from handler/service
    /// code was hitting this — the log area never updated because the adds were happening
    /// on the wrong thread.
    ///
    /// Usage: construct once in the ViewModel (which owns the Dispatcher context at
    /// construction time), pass this wrapper into RunTestHandler instead of the raw
    /// ObservableCollection, and call sink.Add(...) everywhere Log.Add(...) was called before.
    /// </summary>
    public class ThreadSafeLogSink
    {
        private readonly ObservableCollection<LogEntry> _collection;
        private readonly Dispatcher _dispatcher;

        public ThreadSafeLogSink(ObservableCollection<LogEntry> collection, Dispatcher dispatcher)
        {
            _collection = collection;
            _dispatcher = dispatcher;
        }

        /// <summary>Adds a LogEntry, marshaling onto the UI thread if called from any other thread.</summary>
        public void Add(LogEntry entry)
        {
            if (_dispatcher.CheckAccess())
            {
                _collection.Add(entry);
            }
            else
            {
                _dispatcher.Invoke(() => _collection.Add(entry));
            }
        }

        public void Add(LogLevel level, string message) => Add(new LogEntry(level, message));

        public void Clear()
        {
            if (_dispatcher.CheckAccess())
            {
                _collection.Clear();
            }
            else
            {
                _dispatcher.Invoke(() => _collection.Clear());
            }
        }
    }
}
