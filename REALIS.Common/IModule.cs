using System;

namespace REALIS.Common
{
    /// <summary>
    /// Interface for REALIS modules: lifecycle methods called by ModuleManager.
    /// </summary>
    public interface IModule
    {
        /// <summary>Called once when the module is initialized.</summary>
        void Initialize();

        /// <summary>Called on each game tick to update module logic.</summary>
        void Update();

        /// <summary>Called when the script is aborted or unloaded.</summary>
        void Dispose();
    }
} 