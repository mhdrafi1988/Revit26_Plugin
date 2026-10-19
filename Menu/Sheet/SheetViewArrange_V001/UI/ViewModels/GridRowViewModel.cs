using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SheetViewArrange.V001.Core.Models;
using Revit26_Plugin.SheetViewArrange.V001.Core.Services;
using System;
using System.Collections;
using System.Windows.Input;

namespace Revit26_Plugin.SheetViewArrange.V001.UI.ViewModels
{
    /// <summary>What changed when a grid row took a new plan row.</summary>
    [Flags]
    public enum RowChange
    {
        /// <summary>Nothing visible changed.</summary>
        None = 0,

        /// <summary>Plan-derived cells changed (#, Row, Status, note, tick) — the grid only repaints them.</summary>
        Values = 1,

        /// <summary>Something that decides the row's position in the list changed — the list must be re-sorted.</summary>
        Position = 2
    }

    /// <summary>
    /// One line of the Views grid. The same instance lives as long as its viewport is on the sheet:
    /// each re-plan updates it in place, so scroll position and selection survive ticking.
    /// </summary>
    public sealed class GridRowViewModel : ObservableObject
    {
        private readonly Action<GridRowViewModel> _tickToggled;
        private readonly Func<ArrangeGroupBy> _groupBy;
        private bool _isTicked;

        /// <summary>Creates a row from <paramref name="data"/>.</summary>
        /// <param name="data">The plan row.</param>
        /// <param name="tickToggled">Called when the user ticks or unticks this row.</param>
        /// <param name="groupBy">Current grouping (read when the list is grouped).</param>
        public GridRowViewModel(ArrangeRow data, Action<GridRowViewModel> tickToggled, Func<ArrangeGroupBy> groupBy)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            _tickToggled = tickToggled ?? throw new ArgumentNullException(nameof(tickToggled));
            _groupBy = groupBy ?? throw new ArgumentNullException(nameof(groupBy));
            _isTicked = data.IsTicked;
        }

        /// <summary>The plan row this line currently shows.</summary>
        public ArrangeRow Data { get; private set; }

        /// <summary>Viewport key — identifies the row across refreshes.</summary>
        public long Key => Data.ViewportKey;

        /// <summary>"#" column text.</summary>
        public string OrderText => Data.Order?.ToString() ?? ArrangeRow.NoValue;

        /// <summary>Detail number.</summary>
        public string DetailNumber => Data.DetailNumber;

        /// <summary>View name.</summary>
        public string ViewName => Data.ViewName;

        /// <summary>View type.</summary>
        public string ViewTypeName => Data.ViewTypeName;

        /// <summary>"W × H" mm.</summary>
        public string SizeText => Data.SizeText;

        /// <summary>Row column text.</summary>
        public string RowText => Data.RowLabel;

        /// <summary>Planned status (drives the pill colour).</summary>
        public ArrangeStatus Status => Data.Status;

        /// <summary>Status text.</summary>
        public string StatusLabel => Data.StatusLabel;

        /// <summary>Note next to the status.</summary>
        public string Note => Data.Note;

        /// <summary>False for views that are left alone anyway (owned by another user, pinned and not moved).</summary>
        public bool CanTick => Data.CanTick;

        /// <summary>Group header text for the current grouping.</summary>
        public string GroupName => ArrangeGridSort.GroupName(Data, _groupBy());

        /// <summary>The tick box. Setting it (by the user) re-plans; the plan itself sets it silently.</summary>
        public bool IsTicked
        {
            get => _isTicked;
            set
            {
                if (_isTicked == value)
                    return;
                _isTicked = value;
                OnPropertyChanged();
                _tickToggled(this);
            }
        }

        /// <summary>Takes the new plan row; returns what changed so the caller can decide whether the list needs re-sorting.</summary>
        public RowChange Update(ArrangeRow data)
        {
            var old = Data;
            Data = data ?? throw new ArgumentNullException(nameof(data));

            var change = RowChange.None;
            if (old.ReadingKey != data.ReadingKey || old.DetailNumber != data.DetailNumber || old.ViewName != data.ViewName
                || old.ViewTypeName != data.ViewTypeName || old.SizeArea != data.SizeArea)
                change |= RowChange.Position;
            if (old.Order != data.Order || old.Row != data.Row || old.Status != data.Status || old.Note != data.Note
                || old.CanTick != data.CanTick || old.IsTicked != data.IsTicked || old.SizeText != data.SizeText)
                change |= RowChange.Values;

            if (change != RowChange.None)
            {
                _isTicked = data.IsTicked;      // silent: this is the plan talking, not the user
                OnPropertyChanged(string.Empty); // every cell re-reads
            }
            return change;
        }
    }

    /// <summary>Sorts <see cref="GridRowViewModel"/> items through a comparison on their plan rows.</summary>
    public sealed class GridRowComparer : IComparer
    {
        private readonly Comparison<ArrangeRow> _comparison;

        /// <summary>Creates the comparer over <paramref name="comparison"/>.</summary>
        public GridRowComparer(Comparison<ArrangeRow> comparison)
            => _comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));

        /// <inheritdoc/>
        public int Compare(object x, object y)
            => _comparison(((GridRowViewModel)x).Data, ((GridRowViewModel)y).Data);
    }

    /// <summary>One tick box in a filter pop-up (a view type, status or row).</summary>
    public sealed partial class FilterOptionViewModel : ObservableObject
    {
        private readonly Action _changed;
        private bool _isChecked;

        /// <summary>Creates an option.</summary>
        /// <param name="key">Value the filter matches on.</param>
        /// <param name="label">Text shown.</param>
        /// <param name="changed">Called when the user (un)ticks it.</param>
        public FilterOptionViewModel(string key, string label, Action changed)
        {
            Key = key;
            Label = label;
            _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        }

        /// <summary>Value the filter matches on.</summary>
        public string Key { get; }

        /// <summary>Text shown.</summary>
        public string Label { get; private set; }

        /// <summary>How many views have this value.</summary>
        [ObservableProperty] private int count;

        /// <summary>True when the filter is limited to this value.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (SetProperty(ref _isChecked, value))
                    _changed();
            }
        }

        /// <summary>Updates the shown text (e.g. a row label) without raising a filter change.</summary>
        public void SetLabel(string label)
        {
            if (Label == label) return;
            Label = label;
            OnPropertyChanged(nameof(Label));
        }
    }

    /// <summary>A removable "Type: Section" tag under the filter bar.</summary>
    public sealed class FilterChipViewModel
    {
        /// <summary>Creates a chip that runs <paramref name="remove"/> when its × is pressed.</summary>
        public FilterChipViewModel(string text, Action remove)
        {
            Text = text;
            RemoveCommand = new RelayCommand(remove ?? throw new ArgumentNullException(nameof(remove)));
        }

        /// <summary>Chip text.</summary>
        public string Text { get; }

        /// <summary>Removes the filter this chip stands for.</summary>
        public ICommand RemoveCommand { get; }
    }
}
