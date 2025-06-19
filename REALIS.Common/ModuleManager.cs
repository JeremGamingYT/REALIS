using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace REALIS.Common
{
    /// <summary>
    /// Discovers and manages the lifecycle of all IModule implementations.
    /// </summary>
    public static class ModuleManager
    {
        private static readonly List<IModule> _modules = new List<IModule>();
        private static bool _initialized;

        /// <summary>
        /// Discovers all modules and calls their Initialize().
        /// </summary>
        public static void InitializeAll()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // Find all loaded assemblies starting with REALIS.
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name.StartsWith("REALIS."))
                    .ToList();

                // Debug simple pour éviter les problèmes de namespace
                System.Diagnostics.Debug.WriteLine($"Trouvé {assemblies.Count} assemblies REALIS");

                var moduleTypes = assemblies
                    .SelectMany(a => {
                        try 
                        { 
                            var types = a.GetTypes();
                            System.Diagnostics.Debug.WriteLine($"Assembly {a.GetName().Name}: {types.Length} types");
                            return types;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Erreur lors du chargement des types de {a.GetName().Name}: {ex.Message}");
                            return Array.Empty<Type>();
                        }
                    })
                    .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Trouvé {moduleTypes.Count} modules IModule");

                foreach (var type in moduleTypes)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Tentative de chargement du module: {type.Name}");
                        
                        if (Activator.CreateInstance(type) is IModule module)
                        {
                            _modules.Add(module);
                            module.Initialize();
                            System.Diagnostics.Debug.WriteLine($"Module {type.Name} chargé avec succès");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Impossible de créer une instance du module {type.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Erreur lors du chargement du module {type.Name}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"ModuleManager initialisé avec {_modules.Count} modules");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur critique dans InitializeAll: {ex.Message}");
            }
        }

        /// <summary>
        /// Calls Update() on all registered modules.
        /// </summary>
        public static void UpdateAll()
        {
            foreach (var module in _modules)
            {
                try { module.Update(); }
                catch (Exception ex) 
                { 
                    System.Diagnostics.Debug.WriteLine($"Erreur dans Update du module {module.GetType().Name}: {ex.Message}");
                }
            }

            // Exécuter les actions différées planifiées sans bloquer le thread principal
            GameScheduler.Tick();
        }

        /// <summary>
        /// Calls Dispose() on all modules and clears the list.
        /// </summary>
        public static void DisposeAll()
        {
            foreach (var module in _modules)
            {
                try { module.Dispose(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erreur dans Dispose du module {module.GetType().Name}: {ex.Message}");
                }
            }
            _modules.Clear();
        }
    }
} 