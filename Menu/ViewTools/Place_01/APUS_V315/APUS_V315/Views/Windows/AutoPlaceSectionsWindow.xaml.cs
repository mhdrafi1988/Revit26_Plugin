using Revit26_Plugin.APUS_V315.ViewModels.Main;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Revit26_Plugin.APUS_V315.Views.Windows;

public partial class AutoPlaceSectionsWindow : Window
{
    private readonly AutoPlaceSectionsViewModel _viewModel;
    private DispatcherTimer? _autoScrollTimer;

    public AutoPlaceSectionsWindow(AutoPlaceSectionsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        // Set Revit as owner
        try
        {
            var revitHandle = Process.GetCurrentProcess().MainWindowHandle;
            if (revitHandle != IntPtr.Zero)
            {
                new WindowInteropHelper(this) { Owner = revitHandle };
            }
        }
        catch
        {
            // Ignore owner setting errors
        }

        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        StartAutoScroll();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        StopAutoScroll();

        if (_viewModel.IsProcessing)
        {
            var result = MessageBox.Show(
                Revit26_Plugin.Menu.ViewTools.Place_01.APUS_V315.Resources.Strings.Resources.ConfirmCloseMessage,
                Revit26_Plugin.Menu.ViewTools.Place_01.APUS_V315.Resources.Strings.Resources.ConfirmCloseTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.CancelCommand.Execute(null);
                _viewModel.Dispose();
            }
            else
            {
                e.Cancel = true;
                return;
            }
        }
        else
        {
            _viewModel.Dispose();
        }
    }

    private void StartAutoScroll()
    {
        _autoScrollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _autoScrollTimer.Tick += (s, e) => ScrollToBottom();
        _autoScrollTimer.Start();
    }

    private void StopAutoScroll()
    {
        _autoScrollTimer?.Stop();
        _autoScrollTimer = null;
    }

    private void ScrollToBottom()
    {
        if (LogListBox?.Items.Count > 0)
        {
            LogListBox.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                try
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                }
                catch
                {
                    // Ignore scrolling errors
                }
            });
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}