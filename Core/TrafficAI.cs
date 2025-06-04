using System;
using GTA;
using GTA.UI;
using REALIS.Config;
using REALIS.Events;
using REALIS.TrafficAI;
using REALIS.Common;

namespace REALIS.Core
{
    /// <summary>
    /// Gestionnaire principal du système TrafficAI utilisant les nouvelles architectures.
    /// Coordonne les différents gestionnaires de trafic pour une expérience optimale.
    /// </summary>
    public class TrafficAI : Script
    {
        private CentralizedTrafficManager? _centralizedManager;
        private TrafficIntelligenceManager? _intelligenceManager;
        private bool _isInitialized = false;
        private DateTime _lastStatusUpdate = DateTime.MinValue;

        public TrafficAI()
        {
            Tick += OnTick;
            Aborted += OnAborted;
            Logger.Info("Modern TrafficAI system starting...");
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                if (!_isInitialized)
                {
                    InitializeManagers();
                    return;
                }

                // Status update périodique
                if ((DateTime.Now - _lastStatusUpdate).TotalSeconds > 30)
                {
                    UpdateStatus();
                    _lastStatusUpdate = DateTime.Now;
                }

                // Le CentralEventManager et les gestionnaires se chargent du reste
                // via leurs propres systèmes de Tick
            }
            catch (Exception ex)
            {
                Logger.Error($"TrafficAI main loop error: {ex.Message}");
            }
        }

        private void InitializeManagers()
        {
            try
            {
                // Initialise le gestionnaire centralisé
                _centralizedManager = new CentralizedTrafficManager();

                // Initialise le gestionnaire d'intelligence
                _intelligenceManager = new TrafficIntelligenceManager();

                // S'abonne aux événements de trafic
                TrafficEvents.VehicleBypass += OnVehicleBypass;

                _isInitialized = true;

                Notification.PostTicker("REALIS TrafficAI v2.0 loaded", true);
                Logger.Info("Modern TrafficAI system initialized successfully");

                if (TrafficAIConfig.ShowDebug)
                {
                    Screen.ShowSubtitle("TrafficAI v2.0 - Enhanced Intelligence Active", 5000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize TrafficAI managers: {ex.Message}");
                _isInitialized = false;
            }
        }

        private void UpdateStatus()
        {
            try
            {
                // Nettoyage périodique du système central
                VehicleQueryService.Cleanup();

                if (TrafficAIConfig.ShowDebug)
                {
                    var player = Game.Player.Character;
                    if (player?.CurrentVehicle != null)
                    {
                        var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(player.Position, 25f);
                        Logger.Info($"TrafficAI Status: {nearbyVehicles.Length} vehicles in scan range");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Status update error: {ex.Message}");
            }
        }

        private void OnVehicleBypass()
        {
            try
            {
                // Gestion des événements de contournement via le système d'événements
                Logger.Info("Vehicle bypass event received");
                
                if (TrafficAIConfig.ShowDebug)
                {
                    Screen.ShowSubtitle("Traffic Bypass Maneuver", 2000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Vehicle bypass event error: {ex.Message}");
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("TrafficAI system shutting down...");

                // Désabonnement des événements
                TrafficEvents.VehicleBypass -= OnVehicleBypass;

                // Nettoyage des gestionnaires
                _centralizedManager?.Dispose();
                _intelligenceManager?.Dispose();

                // Nettoyage final du système
                VehicleQueryService.Cleanup();

                Logger.Info("TrafficAI system stopped successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"TrafficAI shutdown error: {ex.Message}");
            }
        }

        /// <summary>
        /// Méthode publique pour forcer le redémarrage du système
        /// </summary>
        public void RestartSystem()
        {
            try
            {
                Logger.Info("Restarting TrafficAI system...");
                
                _isInitialized = false;
                
                _centralizedManager?.Dispose();
                _intelligenceManager?.Dispose();
                
                _centralizedManager = null;
                _intelligenceManager = null;
                
                Logger.Info("TrafficAI system restart completed");
            }
            catch (Exception ex)
            {
                Logger.Error($"TrafficAI restart error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retourne des statistiques sur le système TrafficAI
        /// </summary>
        public string GetSystemStats()
        {
            try
            {
                var player = Game.Player.Character;
                if (player?.CurrentVehicle == null)
                    return "Player not in vehicle";

                var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(player.Position, TrafficAIConfig.DetectionRadius);
                
                return $"TrafficAI Stats:\n" +
                       $"- System: {(_isInitialized ? "Active" : "Inactive")}\n" +
                       $"- Nearby Vehicles: {nearbyVehicles.Length}\n" +
                       $"- Scan Radius: {TrafficAIConfig.DetectionRadius}m\n" +
                       $"- Debug Mode: {(TrafficAIConfig.ShowDebug ? "On" : "Off")}";
            }
            catch (Exception ex)
            {
                return $"Stats Error: {ex.Message}";
            }
        }
    }
}
