using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace REALIS.Core
{
    public class PoliceSystem
    {
        private static List<Ped> _activeCops = new List<Ped>();
        private static List<Vehicle> _activePoliceVehicles = new List<Vehicle>();
        private static List<Vehicle> _barrierVehicles = new List<Vehicle>();
        private static bool _playerPointingWeaponAtCop = false;
        private static DateTime _lastPitAttempt = DateTime.MinValue;
        private static DateTime _lastBarrierCheck = DateTime.MinValue;
        private static DateTime _lastBlockingAttempt = DateTime.MinValue;
        private static bool _barrierInProgress = false;
        private static DateTime _barrierStartTime = DateTime.MinValue;
        private static int _maxCops = 5; // DRASTIQUEMENT RÉDUIT pour éviter les crashes
        private static int _maxVehicles = 3; // DRASTIQUEMENT RÉDUIT
        
        // Nouveau système de poursuite
        private static DateTime _pursuitStartTime = DateTime.MinValue;
        private static bool _pursuitActive = false;
        private static int _lastWantedLevel = 0;
        private static DateTime _lastPlayerSpottedTime = DateTime.MinValue;
        private static Vector3 _lastKnownPlayerPosition = Vector3.Zero;
        private static int _totalBarriersCreated = 0;
        
        // Cooldowns augmentés pour réduire la fréquence des opérations
        private const int PIT_COOLDOWN_MS = 8000; // Augmenté de 3 à 8 secondes
        private const int BARRIER_CHECK_INTERVAL_MS = 20000; // Augmenté à 20 secondes
        private const int BLOCKING_COOLDOWN_MS = 5000; // Augmenté de 2 à 5 secondes
        private const float WEAPON_POINTING_DISTANCE = 15f;
        private const float BLOCKING_DISTANCE = 50f;
        
        // Nouveaux paramètres pour la détection réaliste - RÉDUITS
        private const float COP_VISION_RANGE = 60f; // Réduit de 80 à 60
        private const float COP_VISION_ANGLE = 90f; // Réduit de 120 à 90
        private const float SEARCH_RADIUS_MULTIPLIER = 1.2f; // Réduit de 1.5 à 1.2
        private const int PURSUIT_DURATION_FOR_BARRIERS = 90; // Augmenté de 45 à 90 secondes
        private const int MAX_BARRIERS_PER_PURSUIT = 1; // Réduit de 3 à 1
        private const int BARRIER_COOLDOWN_SECONDS = 60; // Augmenté de 30 à 60 secondes
        
        // Nouveaux cooldowns pour les opérations critiques
        private static DateTime _lastEntityCleanup = DateTime.MinValue;
        private static DateTime _lastUpdate = DateTime.MinValue;
        private const int UPDATE_INTERVAL_MS = 500; // Mise à jour toutes les 500ms au lieu de chaque frame
        private const int CLEANUP_INTERVAL_MS = 2000; // Nettoyage toutes les 2 secondes

        public static void Initialize()
        {
            try
            {
                // Désactiver le comportement de tir par défaut de la police
                DisableDefaultPoliceShooting();
                GTA.UI.Notification.Show("~g~Système de police REALIS initialisé (Mode Sécurisé)");
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"Police Init error: {ex.Message}");
            }
        }

        public static void Update()
        {
            try
            {
                // Limiter la fréquence des mises à jour pour éviter les crashes
                if (DateTime.Now.Subtract(_lastUpdate).TotalMilliseconds < UPDATE_INTERVAL_MS)
                    return;
                
                _lastUpdate = DateTime.Now;
                
                // Toujours s'assurer que le tir automatique est désactivé
                DisableDefaultPoliceShooting();
                
                // Gérer le système de poursuite
                UpdatePursuitSystem();
                
                // Vérifier si le barrage doit se terminer
                if (_barrierInProgress && DateTime.Now.Subtract(_barrierStartTime).TotalSeconds > 20)
                {
                    _barrierInProgress = false;
                }
                
                if (Game.Player.WantedLevel > 0)
                {
                    // Réduire la fréquence des opérations
                    UpdateActiveCopsConservative();
                    HandlePoliceAIConservative();
                    CheckPlayerWeaponPointing();
                    
                    if (Game.Player.Character.IsInVehicle())
                    {
                        HandleVehicleChaseConservative();
                        HandlePoliceBlockingConservative();
                    }
                    else
                    {
                        HandleOnFootChaseConservative();
                    }
                }
                
                // Nettoyage moins fréquent mais plus approfondi
                if (DateTime.Now.Subtract(_lastEntityCleanup).TotalMilliseconds > CLEANUP_INTERVAL_MS)
                {
                    CleanupInactiveEntitiesConservative();
                    _lastEntityCleanup = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"Police Update error: {ex.Message}");
                // En cas d'erreur, faire un reset complet
                EmergencyReset();
            }
        }

        private static void UpdatePursuitSystem()
        {
            int currentWantedLevel = Game.Player.WantedLevel;
            
            // Démarrer ou arrêter la poursuite
            if (currentWantedLevel > 0 && !_pursuitActive)
            {
                _pursuitActive = true;
                _pursuitStartTime = DateTime.Now;
                _totalBarriersCreated = 0;
                GTA.UI.Notification.Show("~r~Poursuite active - Les forces de l'ordre sont en alerte");
            }
            else if (currentWantedLevel == 0 && _pursuitActive)
            {
                _pursuitActive = false;
                _pursuitStartTime = DateTime.MinValue;
                _totalBarriersCreated = 0;
                GTA.UI.Notification.Show("~g~Poursuite terminée - Vous avez échappé aux forces de l'ordre");
            }
            
            _lastWantedLevel = currentWantedLevel;
        }

        private static double GetPursuitDuration()
        {
            if (!_pursuitActive) return 0;
            return DateTime.Now.Subtract(_pursuitStartTime).TotalSeconds;
        }

        private static void DisableDefaultPoliceShooting()
        {
            try
            {
                // Désactiver complètement la capacité de la police à tirer automatiquement
                var wanted = Game.Player.WantedLevel;
                if (wanted > 0)
                {
                    Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, true);
                    Function.Call(Hash.SET_EVERYONE_IGNORE_PLAYER, Game.Player, true);
                    
                    // Forcer le système à ne pas dispatcher de policiers armés
                    Function.Call(Hash.SET_DISPATCH_COPS_FOR_PLAYER, Game.Player, false);
                    Function.Call(Hash.SET_CREATE_RANDOM_COPS, false);
                    Function.Call(Hash.SET_CREATE_RANDOM_COPS_NOT_ON_SCENARIOS, false);
                    Function.Call(Hash.SET_CREATE_RANDOM_COPS_ON_SCENARIOS, false);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                GTA.UI.Notification.Show($"Police system error: {ex.Message}");
            }
        }

        private static void UpdateActiveCopsConservative()
        {
            try
            {
                // Encore plus conservateur - limiter drastiquement
                if (_activeCops.Count >= _maxCops) return;
                
                // Réduire la fréquence de recherche de nouveaux cops
                var nearbyPeds = World.GetNearbyPeds(Game.Player.Character.Position, COP_VISION_RANGE).Take(10).ToArray(); // Limiter à 10 peds max
                
                int newCopsAdded = 0;
                foreach (var ped in nearbyPeds)
                {
                    if (_activeCops.Count >= _maxCops || newCopsAdded >= 2) break; // Max 2 nouveaux cops par update
                    
                    if (IsCop(ped) && !_activeCops.Contains(ped))
                    {
                        // Vérifications de sécurité supplémentaires
                        if (ped != null && ped.Exists() && !ped.IsDead)
                        {
                            if (CanCopSeePlayer(ped))
                            {
                                _activeCops.Add(ped);
                                ConfigureCopBehaviorSafe(ped);
                                _lastPlayerSpottedTime = DateTime.Now;
                                _lastKnownPlayerPosition = Game.Player.Character.Position;
                                newCopsAdded++;
                            }
                        }
                    }
                }
                
                // Véhicules encore plus conservateur
                if (_activePoliceVehicles.Count < _maxVehicles)
                {
                    var nearbyVehicles = World.GetNearbyVehicles(Game.Player.Character.Position, COP_VISION_RANGE).Take(5).ToArray(); // Limiter à 5 véhicules max
                    
                    foreach (var vehicle in nearbyVehicles)
                    {
                        if (_activePoliceVehicles.Count >= _maxVehicles) break;
                        
                        if (IsPoliceVehicle(vehicle) && !_activePoliceVehicles.Contains(vehicle))
                        {
                            var driver = vehicle.Driver;
                            if (driver != null && driver.Exists() && !driver.IsDead)
                            {
                                _activePoliceVehicles.Add(vehicle);
                                _lastPlayerSpottedTime = DateTime.Now;
                                _lastKnownPlayerPosition = Game.Player.Character.Position;
                                break; // Un seul véhicule par update
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"UpdateActiveCops error: {ex.Message}");
            }
        }

        private static bool CanCopSeePlayer(Ped cop)
        {
            try
            {
                if (cop == null || !cop.Exists()) return false;
                
                float distance = cop.Position.DistanceTo(Game.Player.Character.Position);
                return distance <= COP_VISION_RANGE; // Vérification simplifiée sans raycast
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPlayerInSearchArea(Ped cop)
        {
            if (_lastKnownPlayerPosition == Vector3.Zero) return false;
            
            float distance = cop.Position.DistanceTo(_lastKnownPlayerPosition);
            float searchRadius = COP_VISION_RANGE * SEARCH_RADIUS_MULTIPLIER;
            
            // Le policier recherche dans une zone élargie autour de la dernière position connue
            return distance <= searchRadius;
        }

        private static void AssignSearchTask(Ped cop)
        {
            if (_lastKnownPlayerPosition == Vector3.Zero) return;
            
            // Assigner une tâche de recherche vers la dernière position connue
            Vector3 searchPoint = _lastKnownPlayerPosition + new Vector3(
                new Random().Next(-20, 20),
                new Random().Next(-20, 20),
                0
            );
            
            cop.Task.GoToPointAnyMeansExtraParamsWithCruiseSpeed(
                searchPoint,
                PedMoveBlendRatio.Walk,
                null,
                false,
                VehicleDrivingFlags.DrivingModeStopForVehicles,
                -1, 0, 20,
                TaskGoToPointAnyMeansFlags.Default,
                -1, 2f
            );
        }

        private static void ConfigureCopBehaviorSafe(Ped cop)
        {
            try
            {
                if (cop == null || !cop.Exists() || cop.IsDead) return;

                // Configuration minimale pour éviter les crashes
                cop.BlockPermanentEvents = true;
                
                // Seulement les attributs essentiels
                cop.SetCombatAttribute(CombatAttributes.AlwaysFlee, false);
                cop.SetCombatAttribute(CombatAttributes.AlwaysFight, false);
                
                // Empêcher le tir par défaut - méthode sécurisée
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, cop, 5, false); // Pas de combat automatique
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"ConfigureCop error: {ex.Message}");
                // Si la configuration échoue, retirer le cop de la liste
                if (_activeCops.Contains(cop))
                {
                    _activeCops.Remove(cop);
                }
            }
        }

        private static void HandlePoliceAIConservative()
        {
            try
            {
                // Traiter seulement quelques cops à la fois
                var copsToProcess = _activeCops.Take(3).ToList(); // Max 3 cops par update
                
                foreach (var cop in copsToProcess)
                {
                    if (cop == null || !cop.Exists() || cop.IsDead)
                    {
                        _activeCops.Remove(cop);
                        continue;
                    }

                    // Vérification de ligne de vue simplifiée
                    float distance = cop.Position.DistanceTo(Game.Player.Character.Position);
                    if (distance > COP_VISION_RANGE * 2) // Trop loin
                    {
                        _activeCops.Remove(cop);
                        continue;
                    }

                    // Gestion des armes très conservatrice
                    if (_playerPointingWeaponAtCop && IsPlayerPointingWeaponAtCop(cop))
                    {
                        AuthorizeRetaliationFireSafe(cop);
                    }
                    else
                    {
                        PreventShootingSafe(cop);
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"HandlePoliceAI error: {ex.Message}");
            }
        }

        private static void HandleVehicleChaseConservative()
        {
            try
            {
                var playerVehicle = Game.Player.Character.CurrentVehicle;
                if (playerVehicle == null) return;

                // PIT maneuvers très réduits
                if (DateTime.Now.Subtract(_lastPitAttempt).TotalMilliseconds > PIT_COOLDOWN_MS)
                {
                    AttemptPitManeuverSafe(playerVehicle);
                    _lastPitAttempt = DateTime.Now;
                }

                // Barrages très rares
                if (ShouldCreateBarrierConservative())
                {
                    SetupPoliceBarrier(playerVehicle);
                    _lastBarrierCheck = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"HandleVehicleChase error: {ex.Message}");
            }
        }

        private static bool ShouldCreateBarrierConservative()
        {
            // Conditions encore plus strictes
            if (!_pursuitActive) return false;
            if (_barrierInProgress) return false;
            if (_totalBarriersCreated >= MAX_BARRIERS_PER_PURSUIT) return false;
            if (!Game.Player.Character.IsInVehicle()) return false;
            if (_activePoliceVehicles.Count == 0) return false; // Pas de véhicules disponibles
            
            double pursuitDuration = GetPursuitDuration();
            if (pursuitDuration < PURSUIT_DURATION_FOR_BARRIERS) return false;
            
            if (DateTime.Now.Subtract(_lastBarrierCheck).TotalSeconds < BARRIER_COOLDOWN_SECONDS) return false;
            
            // Probabilité très réduite
            double baseChance = 0.1; // 10% seulement
            return new Random().NextDouble() < baseChance;
        }

        private static void AttemptPitManeuverSafe(Vehicle playerVehicle)
        {
            try
            {
                // Traiter seulement UN véhicule à la fois
                var policeVehicle = _activePoliceVehicles.FirstOrDefault(v => 
                    v != null && v.Exists() && 
                    v.Driver != null && v.Driver.Exists() && !v.Driver.IsDead);
                
                if (policeVehicle == null) return;
                
                var driver = policeVehicle.Driver;
                float distance = policeVehicle.Position.DistanceTo(playerVehicle.Position);
                
                if (distance < 30f && distance > 10f)
                {
                    // Tâche simple seulement
                    driver.Task.VehicleChase(Game.Player.Character);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"PitManeuver error: {ex.Message}");
            }
        }

        private static void HandlePoliceBlockingConservative()
        {
            try
            {
                if (DateTime.Now.Subtract(_lastBlockingAttempt).TotalMilliseconds < BLOCKING_COOLDOWN_MS)
                    return;
                    
                var playerVehicle = Game.Player.Character.CurrentVehicle;
                if (playerVehicle == null) return;
                
                // Un seul véhicule de blocage maximum
                var policeVehicle = _activePoliceVehicles.FirstOrDefault(v => 
                    v != null && v.Exists() && 
                    v.Driver != null && v.Driver.Exists() && !v.Driver.IsDead);
                
                if (policeVehicle != null)
                {
                    var driver = policeVehicle.Driver;
                    float distance = policeVehicle.Position.DistanceTo(playerVehicle.Position);
                    
                    if (distance < BLOCKING_DISTANCE && distance > 15f)
                    {
                        driver.Task.VehicleChase(Game.Player.Character);
                    }
                }
                
                _lastBlockingAttempt = DateTime.Now;
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"HandleBlocking error: {ex.Message}");
            }
        }

        private static void HandleOnFootChaseConservative()
        {
            try
            {
                // Traiter seulement 2 cops max
                var copsToProcess = _activeCops.Take(2).ToList();
                
                foreach (var cop in copsToProcess)
                {
                    if (cop == null || !cop.Exists() || cop.IsDead) continue;
                    
                    float distance = cop.Position.DistanceTo(Game.Player.Character.Position);
                    if (distance < 40f && distance > 8f)
                    {
                        // Tâche très simple
                        cop.Task.GoTo(Game.Player.Character.Position);
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"HandleOnFoot error: {ex.Message}");
            }
        }

        private static bool IsPlayerPointingWeaponAtCop(Ped cop)
        {
            try
            {
                var player = Game.Player.Character;
                
                if (player.Weapons.Current.Hash == WeaponHash.Unarmed || !Game.IsControlPressed(Control.Aim))
                    return false;

                float distance = cop.Position.DistanceTo(player.Position);
                return distance <= WEAPON_POINTING_DISTANCE;
            }
            catch
            {
                return false;
            }
        }

        private static void AuthorizeRetaliationFireSafe(Ped cop)
        {
            try
            {
                if (cop == null || !cop.Exists() || cop.IsDead) return;

                // Méthode très sécurisée
                if (cop.Weapons.Current.Hash == WeaponHash.Unarmed)
                {
                    cop.Weapons.Give(WeaponHash.Pistol, 30, false, true); // Moins de munitions
                }
                
                cop.Task.FightAgainst(Game.Player.Character);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"AuthorizeRetaliation error: {ex.Message}");
            }
        }

        private static void PreventShootingSafe(Ped cop)
        {
            try
            {
                if (cop == null || !cop.Exists() || cop.IsDead) return;
                
                // Méthode très sécurisée
                cop.Weapons.RemoveAll();
            }
            catch (Exception ex)
            {
                // Ignorer les erreurs de suppression d'armes
            }
        }

        private static bool IsCop(Ped ped)
        {
            try
            {
                if (ped == null || !ped.Exists()) return false;
                
                return ped.Model.Hash == PedHash.Cop01SFY.GetHashCode() ||
                       ped.Model.Hash == PedHash.Cop01SMY.GetHashCode();
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPoliceVehicle(Vehicle vehicle)
        {
            try
            {
                if (vehicle == null || !vehicle.Exists()) return false;
                
                return vehicle.Model.Hash == VehicleHash.Police.GetHashCode() ||
                       vehicle.Model.Hash == VehicleHash.Police2.GetHashCode() ||
                       vehicle.Model.Hash == VehicleHash.Police3.GetHashCode() ||
                       vehicle.Model.Hash == VehicleHash.Police4.GetHashCode();
            }
            catch
            {
                return false;
            }
        }

        private static void CleanupInactiveEntitiesConservative()
        {
            try
            {
                // Nettoyage très prudent
                for (int i = _activeCops.Count - 1; i >= 0; i--)
                {
                    var cop = _activeCops[i];
                    if (cop == null || !cop.Exists() || cop.IsDead || 
                        cop.Position.DistanceTo(Game.Player.Character.Position) > COP_VISION_RANGE * 3)
                    {
                        _activeCops.RemoveAt(i);
                    }
                }
                
                for (int i = _activePoliceVehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = _activePoliceVehicles[i];
                    if (vehicle == null || !vehicle.Exists() || 
                        vehicle.Position.DistanceTo(Game.Player.Character.Position) > COP_VISION_RANGE * 3)
                    {
                        _activePoliceVehicles.RemoveAt(i);
                    }
                }
                
                for (int i = _barrierVehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = _barrierVehicles[i];
                    if (vehicle == null || !vehicle.Exists())
                    {
                        _barrierVehicles.RemoveAt(i);
                    }
                }
                
                // Forcer la limitation si dépassement
                if (_activeCops.Count > _maxCops)
                {
                    _activeCops = _activeCops.Take(_maxCops).ToList();
                }
                
                if (_activePoliceVehicles.Count > _maxVehicles)
                {
                    _activePoliceVehicles = _activePoliceVehicles.Take(_maxVehicles).ToList();
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"Cleanup error: {ex.Message}");
                // Reset complet en cas d'erreur critique
                _activeCops.Clear();
                _activePoliceVehicles.Clear();
                _barrierVehicles.Clear();
            }
        }

        public static void EmergencyReset()
        {
            try
            {
                // Réinitialisation d'urgence en cas de crash
                _barrierInProgress = false;
                _playerPointingWeaponAtCop = false;
                _pursuitActive = false;
                _totalBarriersCreated = 0;
                _lastKnownPlayerPosition = Vector3.Zero;
                
                // Nettoyer toutes les entités
                foreach (var cop in _activeCops.ToList())
                {
                    try { cop?.Delete(); } catch { }
                }
                
                foreach (var vehicle in _activePoliceVehicles.ToList())
                {
                    try { vehicle?.Delete(); } catch { }
                }
                
                foreach (var vehicle in _barrierVehicles.ToList())
                {
                    try { vehicle?.Delete(); } catch { }
                }
                
                _activeCops.Clear();
                _activePoliceVehicles.Clear();
                _barrierVehicles.Clear();
                
                GTA.UI.Notification.Show("Police system reset successfully");
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"Emergency reset error: {ex.Message}");
            }
        }

        public static void Dispose()
        {
            try
            {
                foreach (var cop in _activeCops)
                {
                    cop?.Delete();
                }
                
                foreach (var vehicle in _activePoliceVehicles)
                {
                    vehicle?.Delete();
                }
                
                foreach (var vehicle in _barrierVehicles)
                {
                    vehicle?.Delete();
                }
                
                _activeCops.Clear();
                _activePoliceVehicles.Clear();
                _barrierVehicles.Clear();
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"Dispose error: {ex.Message}");
            }
        }

        private static void CheckPlayerWeaponPointing()
        {
            try
            {
                var player = Game.Player.Character;
                _playerPointingWeaponAtCop = false;

                if (player.Weapons.Current.Hash == WeaponHash.Unarmed)
                    return;

                // Vérifier si le joueur vise - version simplifiée
                if (Game.IsControlPressed(Control.Aim))
                {
                    // Vérifier seulement les 2 premiers cops pour réduire les calculs
                    var copsToCheck = _activeCops.Take(2).ToList();
                    
                    foreach (var cop in copsToCheck)
                    {
                        if (cop == null || !cop.Exists()) continue;
                        
                        float distance = cop.Position.DistanceTo(player.Position);
                        if (distance <= WEAPON_POINTING_DISTANCE)
                        {
                            _playerPointingWeaponAtCop = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"CheckWeaponPointing error: {ex.Message}");
            }
        }

        private static void SetupPoliceBarrier(Vehicle playerVehicle)
        {
            try
            {
                if (_barrierInProgress) return;
                
                _barrierInProgress = true;
                _barrierStartTime = DateTime.Now;
                _totalBarriersCreated++;
                
                // Notification simple
                GTA.UI.Notification.Show($"~r~BARRAGE ROUTIER - {_totalBarriersCreated}/{MAX_BARRIERS_PER_PURSUIT}");
                
                // Utiliser un véhicule existant seulement
                var policeVehicle = _activePoliceVehicles.FirstOrDefault(v => 
                    v != null && v.Exists() && 
                    v.Driver != null && v.Driver.Exists() && !v.Driver.IsDead);
                
                if (policeVehicle != null)
                {
                    var driver = policeVehicle.Driver;
                    
                    // Tâche simple - aller vers le joueur
                    driver.Task.VehicleChase(Game.Player.Character);
                    
                    if (!_barrierVehicles.Contains(policeVehicle))
                    {
                        _barrierVehicles.Add(policeVehicle);
                    }
                    
                    GTA.UI.Notification.Show("~y~Barrage en cours de déploiement");
                }
                else
                {
                    _barrierInProgress = false;
                    GTA.UI.Notification.Show("~o~Aucun véhicule disponible pour le barrage");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"SetupBarrier error: {ex.Message}");
                _barrierInProgress = false;
            }
        }
    }
}