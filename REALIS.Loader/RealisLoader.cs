using System;
using GTA;
using REALIS.Common;

namespace REALIS.Loader
{
    /// <summary>
    /// SHVDN entry point: initializes crash guard and delegates to modules.
    /// </summary>
    public class RealisLoader : Script
    {
        public RealisLoader()
        {
            // Initialize global crash handler
            CrashHandler.Initialize();

            // Discover and initialize all modules
            ModuleManager.InitializeAll();

            // Hook game tick and abort events
            Tick += OnTick;
            Aborted += OnAborted;
        }

        private void OnTick(object sender, EventArgs e)
        {
            ModuleManager.UpdateAll();
        }

        private void OnAborted(object sender, EventArgs e)
        {
            ModuleManager.DisposeAll();
        }
    }
} 