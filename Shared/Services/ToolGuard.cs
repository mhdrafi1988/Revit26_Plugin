using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Shared.Services
{
    /// <summary>
    /// Signature of an <see cref="IExternalCommand.Execute"/> body, so it can be passed to
    /// <see cref="ToolGuard.RunCommand"/> as a method group.
    /// </summary>
    public delegate Result CommandBody(ExternalCommandData commandData, ref string message, ElementSet elements);

    /// <summary>
    /// Suite-wide safety net: no exception from this add-in may reach Revit unhandled.
    /// Every <see cref="IExternalCommand"/> and <see cref="IExternalEventHandler"/> routes its
    /// Execute through here; UI-thread exceptions raised by this assembly's windows are caught by
    /// <see cref="Install"/>. Each failure is written to the error log and shown to the user.
    /// Open Revit transactions are disposed (and so rolled back) by their <c>using</c> blocks
    /// as the exception unwinds, before it is caught here.
    /// </summary>
    public static class ToolGuard
    {
        private static readonly Assembly ThisAssembly = typeof(ToolGuard).Assembly;
        private static readonly Regex BuildSuffix = new(@"\s*[—-]\s*(V|VA|FX|WSEB)\d+\s*$", RegexOptions.Compiled);
        private static bool _installed;

        /// <summary>Folder holding the daily error logs: %AppData%\Revit26_Plugin\Logs.</summary>
        public static string LogFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Revit26_Plugin", "Logs");

        /// <summary>
        /// Runs a command body. Cancellation returns <see cref="Result.Cancelled"/>; any other
        /// exception is logged, shown, and returned as <see cref="Result.Failed"/>.
        /// </summary>
        public static Result RunCommand(Type commandType, ExternalCommandData commandData, ref string message,
            ElementSet elements, CommandBody body)
        {
            try
            {
                return body(commandData, ref message, elements);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Report(commandType, ex);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Runs an external-event handler body; any exception is logged and shown instead of
        /// propagating into Revit's idling loop.
        /// </summary>
        public static void RunHandler(Type handlerType, UIApplication app, Action<UIApplication> body)
        {
            try
            {
                body(app);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Report(handlerType, ex);
            }
        }

        /// <summary>
        /// Call once from <c>OnStartup</c>. Catches unhandled UI-thread exceptions thrown by this
        /// assembly's code (window events, view-model commands) so a tool error never crashes Revit,
        /// and appends each tool's version to its window titles.
        /// </summary>
        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            Dispatcher.CurrentDispatcher.UnhandledException += OnDispatcherUnhandledException;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));
        }

        /// <summary>
        /// Writes <paramref name="ex"/> to the error log and shows it to the user, naming the tool
        /// that owns <paramref name="source"/>. Never throws.
        /// </summary>
        public static void Report(Type source, Exception ex)
        {
            ToolInfo tool = ToolCatalog.Find(source);
            string toolTitle = tool?.Title ?? "SloperPro";
            string logPath = WriteLog(toolTitle, source, ex);

            try
            {
                var dialog = new TaskDialog(tool?.Name ?? "SloperPro")
                {
                    MainIcon = TaskDialogIcon.TaskDialogIconError,
                    MainInstruction = "The tool hit an error and stopped. The change in progress was rolled back.",
                    MainContent = ex.Message,
                    ExpandedContent = ex.ToString(),
                    FooterText = logPath == null ? toolTitle : $"{toolTitle} · Log: {logPath}"
                };
                dialog.Show();
            }
            catch
            {
                // Showing the dialog is best effort; the log already holds the details.
            }
        }

        private static string WriteLog(string toolTitle, Type source, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                string path = Path.Combine(LogFolder, $"errors-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {toolTitle} ({source?.FullName}){Environment.NewLine}" +
                    $"{ex}{Environment.NewLine}{Environment.NewLine}");
                return path;
            }
            catch
            {
                return null;
            }
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Only handle exceptions raised by this add-in; Revit's own (and other add-ins') stay theirs.
            Type source = FindOwnFrameType(e.Exception);
            if (source == null) return;

            e.Handled = true;
            Report(source, e.Exception);
        }

        private static Type FindOwnFrameType(Exception ex)
        {
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                foreach (StackFrame frame in new StackTrace(current, false).GetFrames() ?? Array.Empty<StackFrame>())
                {
                    Type type = frame.GetMethod()?.DeclaringType;
                    if (type?.Assembly == ThisAssembly && type != typeof(ToolGuard))
                        return type;
                }
            }
            return null;
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Window window || window.GetType().Assembly != ThisAssembly) return;

                ToolInfo tool = ToolCatalog.Find(window.GetType());
                if (tool == null) return;

                string suffix = $" — v{tool.Version}";
                string title = window.Title ?? string.Empty;
                if (title.EndsWith(suffix, StringComparison.Ordinal)) return;

                // "Sheet Auto Rearrange — V026" becomes "Sheet Auto Rearrange — v26.0.0".
                title = BuildSuffix.Replace(title, string.Empty);
                window.Title = string.IsNullOrWhiteSpace(title) ? tool.Title : title + suffix;
            }
            catch
            {
                // Title decoration must never break a tool window.
            }
        }
    }
}
