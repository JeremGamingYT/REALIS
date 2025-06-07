using System;
using System.Windows.Forms;
using GTA;
using GTA.UI;
using REALIS.Common;

namespace REALIS.Core
{
    /// <summary>
    /// Script principal du mod REALIS - Gère l'initialisation et la coordination des systèmes
    /// </summary>
    public class REALISMain : Script
    {
        private bool _isInitialized = false;
        private DateTime _lastConfigCheck = DateTime.MinValue;
        private const double CONFIG_CHECK_INTERVAL_SECONDS = 5.0;
        
        // Systèmes du mod
        private PoliceSystem? _policeSystem;
        private TrafficAI? _trafficAI;
        private GasStationManager? _gasStationManager;
        private FoodStoreManager? _foodStoreManager;
        private VehicleDealershipManager? _vehicleDealershipManager;
        
        public REALISMain()
        {
            Tick += OnTick;
            Aborted += OnAborted;
            KeyDown += OnKeyDown;
            
            Logger.Info("REALIS Main script loaded.");
            
            // Restaurer immédiatement le temps normal au cas où
            try
            {
                Game.TimeScale = 1.0f;
                Logger.Info("Time scale set to normal on startup.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting time scale on startup: {ex.Message}");
            }
            
            // Initialiser le système de configuration
            try
            {
                ConfigurationManager.Initialize();
                Logger.Info("Configuration system initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize configuration system: {ex.Message}");
                Notification.PostTicker("~r~REALIS: Configuration initialization failed!", false, true);
                return;
            }
            
            // Vérifier si c'est la première utilisation
            if (ConfigurationManager.IsFirstRun)
            {
                Logger.Info("First run detected - showing setup menu.");
            }
            else
            {
                Logger.Info("Configuration found - initializing systems.");
                InitializeSystems();
            }
        }
        
        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                // Vérifier périodiquement les changements de configuration
                if ((DateTime.Now - _lastConfigCheck).TotalSeconds >= CONFIG_CHECK_INTERVAL_SECONDS)
                {
                    CheckConfigurationChanges();
                    _lastConfigCheck = DateTime.Now;
                }
                
                // Mettre à jour les systèmes actifs
                UpdateActiveSystems();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in main tick: {ex.Message}");
            }
        }
        
        private void ProcessKeyboardInputs()
        {
            // Menu visuel géré par son propre script - pas besoin d'interception
            
            try
            {
                
                // Vérifier les touches une par une
                var config = ConfigurationManager.KeybindConfig;
                
                if (Game.IsKeyPressed(config.OpenMenu.Key))
                    HandleGlobalKeybinds(config.OpenMenu.Key);
                else if (Game.IsKeyPressed(config.TogglePoliceSystem.Key))
                    HandleGlobalKeybinds(config.TogglePoliceSystem.Key);
                else if (Game.IsKeyPressed(config.ToggleTrafficAI.Key))
                    HandleGlobalKeybinds(config.ToggleTrafficAI.Key);
                else if (Game.IsKeyPressed(config.EmergencyServices.Key))
                    HandleGlobalKeybinds(config.EmergencyServices.Key);
                else if (Game.IsKeyPressed(config.QuickSave.Key))
                    HandleGlobalKeybinds(config.QuickSave.Key);
                else if (Game.IsKeyPressed(config.DebugToggle.Key))
                    HandleGlobalKeybinds(config.DebugToggle.Key);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling key input: {ex.Message}");
            }
        }
        
        private void OnAborted(object sender, EventArgs e)
        {
            Logger.Info("REALIS Main script is being unloaded.");
            
            // Sauvegarder la configuration avant de quitter
            try
            {
                ConfigurationManager.SaveUserConfiguration();
                ConfigurationManager.SaveKeybindConfiguration();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving configuration on abort: {ex.Message}");
            }
            
            // Nettoyer les ressources
            CleanupSystems();
        }
        
        /// <summary>
        /// Initialise tous les systèmes du mod selon la configuration
        /// </summary>
        private void InitializeSystems()
        {
            if (_isInitialized) return;
            
            try
            {
                Logger.Info("Initializing REALIS systems...");
                
                // Initialiser le gestionnaire d'événements central
                var eventManager = CentralEventManager.Instance;
                
                // Initialiser les systèmes selon la configuration
                if (ConfigurationManager.UserConfig.ModSettings.PoliceSystemEnabled)
                {
                    InitializePoliceSystem();
                }
                
                if (ConfigurationManager.UserConfig.ModSettings.TrafficAIEnabled)
                {
                    InitializeTrafficAI();
                }
                
                if (ConfigurationManager.UserConfig.ModSettings.GasStationSystemEnabled)
                {
                    InitializeGasStationSystem();
                }
                
                if (ConfigurationManager.UserConfig.ModSettings.FoodStoreSystemEnabled)
                {
                    InitializeFoodStoreSystem();
                }
                
                if (ConfigurationManager.UserConfig.ModSettings.VehicleDealershipEnabled)
                {
                    InitializeVehicleDealershipSystem();
                }
                
                _isInitialized = true;
                
                // Notification de succès
                var message = ConfigurationManager.GetTranslation("systems_initialized") ?? "REALIS systems initialized successfully!";
                if (ConfigurationManager.UserConfig.EnableNotifications)
                {
                    Notification.PostTicker($"~g~REALIS: {message}", false, true);
                }
                
                Logger.Info("REALIS systems initialization completed.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize systems: {ex.Message}");
                Notification.PostTicker("~r~REALIS: System initialization failed!", false, true);
            }
        }
        
        private void InitializePoliceSystem()
        {
            try
            {
                _policeSystem = new PoliceSystem();
                Logger.Info("Police system initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize police system: {ex.Message}");
            }
        }
        
        private void InitializeTrafficAI()
        {
            try
            {
                _trafficAI = new TrafficAI();
                Logger.Info("Traffic AI system initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize traffic AI: {ex.Message}");
            }
        }
        
        private void InitializeGasStationSystem()
        {
            try
            {
                _gasStationManager = new GasStationManager();
                Logger.Info("Gas station system initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize gas station system: {ex.Message}");
            }
        }
        
        private void InitializeFoodStoreSystem()
        {
            try
            {
                _foodStoreManager = new FoodStoreManager();
                Logger.Info("Food store system initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize food store system: {ex.Message}");
            }
        }
        
        private void InitializeVehicleDealershipSystem()
        {
            try
            {
                _vehicleDealershipManager = new VehicleDealershipManager();
                Logger.Info("Vehicle dealership system initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize vehicle dealership system: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Vérifie les changements de configuration et met à jour les systèmes
        /// </summary>
        private void CheckConfigurationChanges()
        {
            // Cette méthode pourrait être étendue pour recharger la configuration
            // et ajuster les systèmes en temps réel si nécessaire
        }
        
        /// <summary>
        /// Gère les raccourcis clavier globaux
        /// </summary>
        private void HandleGlobalKeybinds(Keys key)
        {
            var config = ConfigurationManager.KeybindConfig;
            
            // Menu de configuration
            if (key == config.OpenMenu.Key)
            {
                ShowConfigurationMenu();
            }
            
            // Toggle Police System
            else if (key == config.TogglePoliceSystem.Key)
            {
                TogglePoliceSystem();
            }
            
            // Toggle Traffic AI
            else if (key == config.ToggleTrafficAI.Key)
            {
                ToggleTrafficAI();
            }
            
            // Services d'urgence
            else if (key == config.EmergencyServices.Key)
            {
                TriggerEmergencyServices();
            }
            
            // Sauvegarde rapide
            else if (key == config.QuickSave.Key)
            {
                QuickSave();
            }
            
            // Toggle Debug
            else if (key == config.DebugToggle.Key)
            {
                ToggleDebugMode();
            }
        }
        
        private void ShowConfigurationMenu()
        {
            // TODO: Implémenter un menu de configuration en jeu
            var message = ConfigurationManager.GetTranslation("menu_title") ?? "Configuration Menu";
            Notification.PostTicker($"~b~REALIS: {message} - Coming Soon!", false, true);
            Logger.Info("Configuration menu requested.");
        }
        
        private void TogglePoliceSystem()
        {
            var config = ConfigurationManager.UserConfig.ModSettings;
            config.PoliceSystemEnabled = !config.PoliceSystemEnabled;
            
            var status = config.PoliceSystemEnabled ? "Enabled" : "Disabled";
            var message = $"Police System: {status}";
            
            if (ConfigurationManager.UserConfig.EnableNotifications)
            {
                Notification.PostTicker($"~y~REALIS: {message}", false, true);
            }
            
            Logger.Info($"Police system toggled: {status}");
            ConfigurationManager.SaveUserConfiguration();
        }
        
        private void ToggleTrafficAI()
        {
            var config = ConfigurationManager.UserConfig.ModSettings;
            config.TrafficAIEnabled = !config.TrafficAIEnabled;
            
            var status = config.TrafficAIEnabled ? "Enabled" : "Disabled";
            var message = $"Traffic AI: {status}";
            
            if (ConfigurationManager.UserConfig.EnableNotifications)
            {
                Notification.PostTicker($"~y~REALIS: {message}", false, true);
            }
            
            Logger.Info($"Traffic AI toggled: {status}");
            ConfigurationManager.SaveUserConfiguration();
        }
        
        private void TriggerEmergencyServices()
        {
            // TODO: Implémenter le système de services d'urgence
            var message = "Emergency Services - Coming Soon!";
            
            if (ConfigurationManager.UserConfig.EnableNotifications)
            {
                Notification.PostTicker($"~r~REALIS: {message}", false, true);
            }
            
            Logger.Info("Emergency services requested.");
        }
        
        private void QuickSave()
        {
            try
            {
                ConfigurationManager.SaveUserConfiguration();
                ConfigurationManager.SaveKeybindConfiguration();
                
                if (ConfigurationManager.UserConfig.EnableNotifications)
                {
                    Notification.PostTicker("~g~REALIS: Configuration saved!", false, true);
                }
                
                Logger.Info("Quick save completed.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Quick save failed: {ex.Message}");
                Notification.PostTicker("~r~REALIS: Save failed!", false, true);
            }
        }
        
        private void ToggleDebugMode()
        {
            ConfigurationManager.UserConfig.DebugMode = !ConfigurationManager.UserConfig.DebugMode;
            var status = ConfigurationManager.UserConfig.DebugMode ? "Enabled" : "Disabled";
            
            if (ConfigurationManager.UserConfig.EnableNotifications)
            {
                Notification.PostTicker($"~y~REALIS: Debug Mode {status}", false, true);
            }
            
            Logger.Info($"Debug mode toggled: {status}");
            ConfigurationManager.SaveUserConfiguration();
        }
        
        /// <summary>
        /// Met à jour les systèmes actifs
        /// </summary>
        private void UpdateActiveSystems()
        {
            // Les systèmes individuels gèrent leur propre logique de mise à jour
            // Cette méthode peut être utilisée pour la coordination globale
        }
        
        /// <summary>
        /// Nettoie les ressources des systèmes
        /// </summary>
        private void CleanupSystems()
        {
            try
            {
                // Nettoyer le système de contrôle des véhicules
                VehicleQueryService.Cleanup();
                
                // Nettoyer le système de concessionnaire si actif
                if (_vehicleDealershipManager != null)
                {
                    _vehicleDealershipManager.SetEnabled(false);
                    _vehicleDealershipManager = null;
                }
                
                Logger.Info("REALIS systems cleanup completed.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during cleanup: {ex.Message}");
            }
        }

        // Gestionnaire d'événements pour les touches d'urgence
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Touche d'urgence pour restaurer le temps normal
            if (e.KeyCode == Keys.F3 && e.Control)
            {
                ForceRestoreNormalTime();
            }
            
            // Alternative pour forcer la fin du setup
            if (e.KeyCode == Keys.F4 && e.Control)
            {
                ForceCompleteFirstRunSetup();
            }
        }

        private void ForceRestoreNormalTime()
        {
            try
            {
                Game.TimeScale = 1.0f;
                Notification.PostTicker("~g~REALIS: Time scale restored to normal (forced)!", true, true);
                Logger.Info("Time scale forcibly restored to normal via emergency key.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error forcibly restoring time scale: {ex.Message}");
                Notification.PostTicker("~r~REALIS: Error restoring time scale!", true, true);
            }
        }

        private void ForceCompleteFirstRunSetup()
        {
            try
            {
                // Restaurer le temps normal
                Game.TimeScale = 1.0f;
                
                // Marquer comme terminé si pas déjà fait
                if (ConfigurationManager.IsFirstRun)
                {
                    ConfigurationManager.MarkFirstRunCompleted();
                    ConfigurationManager.SaveKeybindConfiguration();
                }
                
                // Initialiser les systèmes si pas déjà fait
                if (!_isInitialized)
                {
                    InitializeSystems();
                }
                
                Notification.PostTicker("~g~REALIS: First run setup force completed!", true, true);
                Logger.Info("First run setup forcibly completed via emergency key.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error forcibly completing first run setup: {ex.Message}");
                Notification.PostTicker("~r~REALIS: Error completing setup!", true, true);
            }
        }
    }
} 