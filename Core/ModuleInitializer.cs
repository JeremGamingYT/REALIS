// This file contains a module initializer executed as soon as the assembly is loaded by SHVDN.
// If the current GTA V build is not supported, the initializer will show a notification and
// throw an exception so that ScriptHookVDotNet skips loading all REALIS scripts, which prevents
// crashes with future/Enhanced versions of the game.

using System;
using System.Runtime.CompilerServices;
using GTA.UI;
using REALIS.Core;

// Provide the attribute for older TargetFrameworks (net48) if it doesn't exist.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}
#endif

namespace REALIS
{
    internal static class AssemblyGuard
    {
        [ModuleInitializer]
        internal static void CheckGameVersionAtLoad()
        {
            // Si la version du jeu est non supportée, on empêche tout script de se charger.
            if (GameCompatibility.IsUnsupported())
            {
                try
                {
                    Notification.PostTicker("~o~REALIS désactivé : version du jeu non supportée.", false, false);
                }
                catch { /* UI non disponible */ }

                // Lancer une exception empêche SHVDN d'utiliser l'assembly.
                throw new InvalidOperationException(
                    "REALIS: unsupported GTA V version – assembly initialization aborted.");
            }
        }
    }
} 