using System;
using GTA;
using GTA.UI;

namespace REALIS.Common
{
    /// <summary>
    /// Global crash guard: intercepts unhandled exceptions and logs/alerts the player.
    /// </summary>
    public static class CrashHandler
    {
        private static bool _isInitialized;

        /// <summary>Initializes the crash guard; subscribe to AppDomain and TaskScheduler events.</summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _isInitialized = true;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleException(e.ExceptionObject as Exception, "UnhandledException");
        }

        private static void OnUnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            HandleException(e.Exception, "TaskException");
            e.SetObserved();
        }

        private static void HandleException(Exception ex, string source)
        {
            string message = ex?.ToString() ?? "Unknown error (null Exception)";
            // TODO: integrate with REALIS logging
            try
            {
                Notification.PostTicker($"~r~REALIS : erreur interceptée ([{source}]) – voir log", false, false);
            }
            catch { }
        }
    }
} 