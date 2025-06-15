using System;
using GTA;
using GTA.UI;

namespace REALIS.Core
{
    /// <summary>
    /// Gestionnaire global d'exceptions non prises en charge.
    /// Intercepte les erreurs critiques dans n'importe quel thread afin d'éviter un crash brutal du jeu
    /// et affiche un message in-game pour prévenir le joueur.
    /// </summary>
    internal static class CrashHandler
    {
        private static bool _isInitialized;

        /// <summary>
        /// Active le crash-guard. À appeler une seule fois (ex. dans le constructeur du script principal).
        /// </summary>
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

        private static void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            HandleException(e.Exception, "TaskException");
            e.SetObserved(); // Empêche la propagation qui pourrait tuer le script
        }

        private static void HandleException(Exception? ex, string source)
        {
            string message = ex?.ToString() ?? "Unknown error (null Exception)";
            Logger.Error($"[CrashGuard::{source}] {message}");

            // Notification visuelle en jeu (non bloquante)
            try
            {
                Notification.PostTicker("~r~REALIS : erreur interceptée – voir TrafficAI.log", false, false);
            }
            catch { /* Serrer les dents si l'UI n'est pas disponible */ }
        }
    }
} 