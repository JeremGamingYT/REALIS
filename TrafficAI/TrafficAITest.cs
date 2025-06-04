using System;
using GTA;
using GTA.UI;
using REALIS.Core;
using REALIS.Common;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Script de test pour vérifier le bon fonctionnement du nouveau système TrafficAI.
    /// Peut être activé temporairement pour diagnostics.
    /// </summary>
    public class TrafficAITest : Script
    {
        private Core.TrafficAI? _trafficAI;
        private DateTime _lastStatsDisplay = DateTime.MinValue;
        private bool _testModeActive = false;

        public TrafficAITest()
        {
            KeyDown += OnKeyDown;
            Logger.Info("TrafficAI Test script loaded - Press F7 to toggle test mode");
        }

        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            // F7 pour activer/désactiver le mode test
            if (e.KeyCode == System.Windows.Forms.Keys.F7)
            {
                ToggleTestMode();
            }
            
            // F8 pour afficher les statistiques
            if (e.KeyCode == System.Windows.Forms.Keys.F8 && _testModeActive)
            {
                DisplayStats();
            }
            
            // F9 pour redémarrer le système
            if (e.KeyCode == System.Windows.Forms.Keys.F9 && _testModeActive)
            {
                RestartTrafficAI();
            }
        }

        private void ToggleTestMode()
        {
            try
            {
                _testModeActive = !_testModeActive;
                
                if (_testModeActive)
                {
                    Notification.PostTicker("TrafficAI Test Mode: ON~n~F8: Stats, F9: Restart, F7: Off", true);
                    Logger.Info("TrafficAI test mode activated");
                    
                    // Démarre le tick de test
                    Tick += OnTestTick;
                }
                else
                {
                    Notification.PostTicker("TrafficAI Test Mode: OFF", true);
                    Logger.Info("TrafficAI test mode deactivated");
                    
                    // Arrête le tick de test
                    Tick -= OnTestTick;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error toggling test mode: {ex.Message}");
                Notification.PostTicker($"Test Mode Error: {ex.Message}", true);
            }
        }

        private void OnTestTick(object sender, EventArgs e)
        {
            try
            {
                // Affichage périodique des stats
                if ((DateTime.Now - _lastStatsDisplay).TotalSeconds > 10)
                {
                    DisplayQuickStats();
                    _lastStatsDisplay = DateTime.Now;
                }
                
                // Test de l'état du système central
                TestCentralEventManager();
                
                // Test du service de véhicules
                TestVehicleQueryService();
            }
            catch (Exception ex)
            {
                Logger.Error($"Test tick error: {ex.Message}");
            }
        }

        private void DisplayStats()
        {
            try
            {
                if (_trafficAI == null)
                {
                    _trafficAI = new Core.TrafficAI();
                }
                
                string stats = _trafficAI.GetSystemStats();
                Screen.ShowSubtitle(stats, 5000);
                Logger.Info($"TrafficAI Stats displayed: {stats}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error displaying stats: {ex.Message}");
                Screen.ShowSubtitle($"Stats Error: {ex.Message}", 3000);
            }
        }

        private void DisplayQuickStats()
        {
            try
            {
                var player = Game.Player.Character;
                if (player?.CurrentVehicle == null) return;

                var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(player.Position, 25f);
                
                string quickStats = $"Vehicles: {nearbyVehicles.Length} | " +
                                  $"EventMgr: {(CentralEventManager.Instance != null ? "OK" : "NULL")} | " +
                                  $"Speed: {player.CurrentVehicle.Speed:F1}";
                
                Screen.ShowSubtitle(quickStats, 1000);
            }
            catch (Exception ex)
            {
                Logger.Error($"Quick stats error: {ex.Message}");
            }
        }

        private void RestartTrafficAI()
        {
            try
            {
                if (_trafficAI != null)
                {
                    _trafficAI.RestartSystem();
                    Notification.PostTicker("TrafficAI System Restarted", true);
                    Logger.Info("TrafficAI system restarted via test script");
                }
                else
                {
                    Notification.PostTicker("TrafficAI not initialized", true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error restarting TrafficAI: {ex.Message}");
                Notification.PostTicker($"Restart Error: {ex.Message}", true);
            }
        }

        private void TestCentralEventManager()
        {
            try
            {
                // Test basique de l'existence du gestionnaire central
                if (CentralEventManager.Instance == null)
                {
                    Logger.Error("CentralEventManager.Instance is null");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"CentralEventManager test error: {ex.Message}");
            }
        }

        private void TestVehicleQueryService()
        {
            try
            {
                var player = Game.Player.Character;
                if (player?.CurrentVehicle == null) return;

                // Test de requête de véhicules
                var vehicles = VehicleQueryService.GetNearbyVehicles(player.Position, 15f);
                
                // Log silencieux pour éviter le spam
                if (vehicles.Length > 0)
                {
                    // Test d'acquisition de contrôle sur le premier véhicule
                    var testVehicle = vehicles[0];
                    if (testVehicle != null && testVehicle.Exists())
                    {
                        bool acquired = VehicleQueryService.TryAcquireControl(testVehicle);
                        if (acquired)
                        {
                            VehicleQueryService.ReleaseControl(testVehicle);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"VehicleQueryService test error: {ex.Message}");
            }
        }
    }
} 