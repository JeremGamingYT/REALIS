using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using REALIS.Common;
using REALIS.Core;
using REALIS.Config;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// DÉSACTIVÉ TEMPORAIREMENT - Conflit avec AdvancedDrivingAI
    /// Gestionnaire avancé de comportements pour les PNJ bloqués dans la circulation.
    /// Système intelligent avec détection multi-directionnelle, limitation des ressources,
    /// et algorithmes de contournement adaptatifs.
    /// </summary>
    public class TrafficIntelligenceManager_DISABLED : Script
    {
        private readonly Dictionary<int, BlockedVehicleInfo> _tracked = new();
        private readonly HashSet<int> _processingVehicles = new(); // Prévient les doubles traitements
        private readonly Dictionary<int, DateTime> _lastActionTime = new(); // Cooldown par véhicule

        // Gestion du nettoyage des entrées trop anciennes
        private const float TrackingTimeout = 30f; // secondes
        
        // Configuration avancée
        private const float CheckRadius = 40f;
        private const float SpeedThreshold = 0.8f; 
        private const float HonkDelay = 3f;       
        private const float BypassDelay = 6f;     
        private const int MaxBypassAttempts = 2;  
        private const int MaxSimultaneousProcessing = 8; // Limite pour éviter les surcharges
        private const float MinCooldownSeconds = 5f; // Cooldown minimum entre actions
        private const float PlayerSafeZone = 8f; // Zone de sécurité autour du joueur
        
        // Compteurs pour debug et optimisation
        private int _processedThisTick = 0;
        private DateTime _lastCleanup = DateTime.Now;

        public TrafficIntelligenceManager_DISABLED()
        {
            // DÉSACTIVÉ - Conflit avec AdvancedDrivingAI
            // Tick += OnTick;
            // Interval = 1500;
            Logger.Info("TrafficIntelligenceManager DISABLED to avoid conflicts with AdvancedDrivingAI");
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _processedThisTick = 0;
                
                Ped player = Game.Player.Character;
                if (player?.CurrentVehicle == null || !player.Exists()) return;

                Vehicle playerVehicle = player.CurrentVehicle;
                if (playerVehicle.Speed < 0.5f && !IsEmergencyActive(playerVehicle))
                    return; // Le joueur ne bouge pas et aucune sirène active

                var nearby = VehicleQueryService.GetNearbyVehicles(player.Position, CheckRadius);
                if (nearby == null || nearby.Length == 0) return;

                // Traite seulement les véhicules les plus pertinents
                var relevantVehicles = nearby
                    .Where(IsVehicleRelevant)
                    .OrderBy(v => v.Position.DistanceTo(player.Position))
                    .Take(MaxSimultaneousProcessing)
                    .ToList();

                var emergencyVehicles = new List<Vehicle>();
                try
                {
                    if (IsEmergencyActive(playerVehicle))
                        emergencyVehicles.Add(playerVehicle);

                    emergencyVehicles.AddRange(
                        nearby.Where(v => v != playerVehicle &&
                                          v.Model.IsEmergencyVehicle &&
                                          v.IsSirenActive));
                }
                catch (Exception ex)
                {
                    Logger.Error($"Emergency vehicle scan error: {ex.Message}");
                }

                foreach (var veh in relevantVehicles)
                {
                    if (_processedThisTick >= MaxSimultaneousProcessing) break;

                    ProcessVehicle(veh, playerVehicle, emergencyVehicles);
                    _processedThisTick++;
                }

                // Nettoyage périodique plus efficace
                if ((DateTime.Now - _lastCleanup).TotalSeconds > 10)
                {
                    CleanupInvalidEntries();
                    CleanupStaleEntries();
                    _lastCleanup = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"TrafficIntelligenceManager error: {ex.Message}");
            }
        }

        private bool IsVehicleRelevant(Vehicle veh)
        {
            if (veh == null || !veh.Exists() || veh.Driver == null || !veh.Driver.IsAlive) 
                return false;

            // Ignore le véhicule du joueur
            if (veh.Driver == Game.Player.Character) 
                return false;

            // Ignore les véhicules trop proches (zone de sécurité)
            float distance = veh.Position.DistanceTo(Game.Player.Character.Position);
            if (distance < PlayerSafeZone || distance > CheckRadius) 
                return false;

            // Ignore les véhicules en mouvement rapide
            if (veh.Speed > SpeedThreshold * 3) 
                return false;

            // Ignore les véhicules déjà en traitement
            if (_processingVehicles.Contains(veh.Handle)) 
                return false;

            return true;
        }

        private void ProcessVehicle(Vehicle veh, Vehicle playerVehicle, List<Vehicle> emergencyVehicles)
        {
            try
            {
                if (!VehicleQueryService.TryAcquireControl(veh))
                    return;

                _processingVehicles.Add(veh.Handle);

                if (!_tracked.TryGetValue(veh.Handle, out var info))
                {
                    info = new BlockedVehicleInfo(veh.Driver, veh);
                    _tracked[veh.Handle] = info;
                }

                info.LastSeen = DateTime.Now;

                UpdateVehicleIntelligently(info, playerVehicle, emergencyVehicles);
            }
            catch (Exception ex)
            {
                Logger.Error($"ProcessVehicle error for {veh.Handle}: {ex.Message}");
                // Supprime de la liste en cas d'erreur
                _processingVehicles.Remove(veh.Handle);
                _tracked.Remove(veh.Handle);
            }
            finally
            {
                _processingVehicles.Remove(veh.Handle);
                VehicleQueryService.ReleaseControl(veh);
            }
        }

        private void UpdateVehicleIntelligently(BlockedVehicleInfo info, Vehicle playerVehicle, List<Vehicle> emergencyVehicles)
        {
            Vehicle veh = info.Vehicle;
            Ped driver = info.Driver;

            if (veh == null || !veh.Exists() || driver == null || !driver.Exists()) return;

            if (emergencyVehicles.Count > 0)
            {
                try
                {
                    if (HandleEmergencyYield(veh, driver, emergencyVehicles))
                    {
                        _lastActionTime[veh.Handle] = DateTime.Now;
                        ResetVehicleState(info);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Emergency yield error: {ex.Message}");
                }
            }

            // Respect du cooldown
            if (_lastActionTime.TryGetValue(veh.Handle, out var lastAction))
            {
                if ((DateTime.Now - lastAction).TotalSeconds < MinCooldownSeconds)
                    return;
            }

            // Reset si le véhicule bouge
            if (veh.Speed > SpeedThreshold)
            {
                ResetVehicleState(info);
                return;
            }

            // Détection avancée de blocage
            var blockageInfo = AnalyzeBlockage(veh, playerVehicle);
            if (!blockageInfo.IsBlocked)
            {
                ResetVehicleState(info);
                return;
            }

            info.BlockedTime += 1.5f;

            // Klaxon intelligent basé sur la distance et la situation
            if (ShouldHonk(info, blockageInfo))
            {
                PerformIntelligentHonk(veh, blockageInfo);
                info.Honked = true;
                _lastActionTime[veh.Handle] = DateTime.Now;
            }

            // Contournement adaptatif
            if (ShouldAttemptBypass(info, blockageInfo))
            {
                if (PerformRealisticBypass(info, playerVehicle, blockageInfo))
                {
                    info.BypassAttempts++;
                    info.BlockedTime = 0f;
                    _lastActionTime[veh.Handle] = DateTime.Now;
                }
            }
        }

        private BlockageAnalysis AnalyzeBlockage(Vehicle veh, Vehicle playerVehicle)
        {
            var analysis = new BlockageAnalysis();
            
            Vector3 vehPos = veh.Position;
            Vector3 playerPos = playerVehicle.Position;
            Vector3 vehForward = veh.ForwardVector;
            
            float distanceToPlayer = vehPos.DistanceTo(playerPos);
            
            // Vérifier si le véhicule fait face au joueur
            Vector3 toPlayer = (playerPos - vehPos).Normalized;
            float facingPlayer = Vector3.Dot(vehForward, toPlayer);
            
            analysis.IsPlayerBlocking = facingPlayer > 0.7f && distanceToPlayer < 15f;
            analysis.DistanceToObstacle = distanceToPlayer;
            
            // Vérifier si le chemin est bloqué
            bool pathBlocked = IsPathBlocked(veh, vehForward, 10f);
            
            // Analyser les possibilités de mouvement
            Vector3 leftDirection = -veh.RightVector;
            Vector3 rightDirection = veh.RightVector;
            Vector3 backwardDirection = -vehForward;
            
            analysis.CanGoLeft = !IsPathBlocked(veh, leftDirection, 6f);
            analysis.CanGoRight = !IsPathBlocked(veh, rightDirection, 6f);
            analysis.CanReverse = !IsPathBlocked(veh, backwardDirection, 8f);
            
            // Déterminer si c'est un embouteillage
            int nearbySlowVehicles = CountFrontVehicles(veh);
            analysis.IsInTrafficJam = nearbySlowVehicles >= 2;
            
            // Déterminer la direction optimale
            analysis.PreferredDirection = DetermineOptimalDirection(veh, analysis);
            
            // Conclusion finale
            analysis.IsBlocked = pathBlocked || analysis.IsPlayerBlocking || analysis.IsInTrafficJam;
            
            return analysis;
        }

        private bool IsPathBlocked(Vehicle veh, Vector3 direction, float distance)
        {
            Vector3 start = veh.Position + Vector3.WorldUp;
            Vector3 end = start + direction * distance;
            
            var ray = World.Raycast(start, end, IntersectFlags.Vehicles | IntersectFlags.Peds | IntersectFlags.Map, veh);
            return ray.DidHit;
        }

        private int CountFrontVehicles(Vehicle veh)
        {
            Vector3 searchPos = veh.Position + veh.ForwardVector * 8f;
            var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(searchPos, 12f);
            
            return nearbyVehicles.Count(v => 
                v != veh && 
                v.Speed < SpeedThreshold && 
                Vector3.Dot(v.Position - veh.Position, veh.ForwardVector) > 0);
        }

        private BypassDirection DetermineOptimalDirection(Vehicle veh, BlockageAnalysis analysis)
        {
            // Priorité : éviter le joueur
            if (analysis.IsPlayerBlocking)
            {
                Vector3 playerPos = Game.Player.Character.Position;
                Vector3 vehRight = veh.RightVector;
                Vector3 toPlayer = playerPos - veh.Position;
                
                float rightDot = Vector3.Dot(toPlayer, vehRight);
                
                if (rightDot > 0) // Joueur à droite
                    return analysis.CanGoLeft ? BypassDirection.Left : BypassDirection.None;
                else // Joueur à gauche
                    return analysis.CanGoRight ? BypassDirection.Right : BypassDirection.None;
            }
            
            // Sinon, choisir la meilleure option disponible
            if (analysis.CanGoLeft && analysis.CanGoRight)
                return new Random().Next(2) == 0 ? BypassDirection.Left : BypassDirection.Right;
            if (analysis.CanGoLeft) return BypassDirection.Left;
            if (analysis.CanGoRight) return BypassDirection.Right;
            if (analysis.CanReverse) return BypassDirection.Reverse;
            
            return BypassDirection.None;
        }

        private bool ShouldHonk(BlockedVehicleInfo info, BlockageAnalysis analysis)
        {
            return !info.Honked && 
                   info.BlockedTime > HonkDelay && 
                   analysis.IsPlayerBlocking && 
                   analysis.DistanceToObstacle < 12f;
        }

        private void PerformIntelligentHonk(Vehicle veh, BlockageAnalysis analysis)
        {
            // Klaxon contextuel basé sur la situation
            if (analysis.IsInTrafficJam)
                Function.Call(Hash.START_VEHICLE_HORN, veh.Handle, 1500, 0, false);
            else
                Function.Call(Hash.START_VEHICLE_HORN, veh.Handle, 800, 0, false);
        }

        private bool ShouldAttemptBypass(BlockedVehicleInfo info, BlockageAnalysis analysis)
        {
            return info.BlockedTime > BypassDelay && 
                   info.BypassAttempts < MaxBypassAttempts && 
                   analysis.PreferredDirection != BypassDirection.None;
        }

        private bool PerformIntelligentBypass(Ped driver, Vehicle veh, BlockageAnalysis analysis)
        {
            Vector3 targetPosition = CalculateBypassTarget(veh, analysis);
            if (targetPosition == Vector3.Zero) return false;

            try
            {
                // Nettoyer les tâches actuelles
                Function.Call(Hash.CLEAR_PED_TASKS, driver.Handle);
                
                // Commande de conduite vers le point cible
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                    driver.Handle,
                    veh.Handle,
                    targetPosition.X,
                    targetPosition.Y,
                    targetPosition.Z,
                    20f, // Vitesse modérée
                    (int)(VehicleDrivingFlags.StopForVehicles | VehicleDrivingFlags.SwerveAroundAllVehicles),
                    4f   // Rayon d'acceptation
                );
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"PerformIntelligentBypass error: {ex.Message}");
                return false;
            }
        }

        private Vector3 CalculateBypassTarget(Vehicle veh, BlockageAnalysis analysis)
        {
            Vector3 basePos = veh.Position;
            Vector3 forward = veh.ForwardVector;
            Vector3 right = veh.RightVector;

            switch (analysis.PreferredDirection)
            {
                case BypassDirection.Left:
                    return basePos - right * TrafficAIConfig.BypassDistance + forward * TrafficAIConfig.ForwardOffset;
                case BypassDirection.Right:
                    return basePos + right * TrafficAIConfig.BypassDistance + forward * TrafficAIConfig.ForwardOffset;
                case BypassDirection.Reverse:
                    return basePos - forward * 10f;
                default:
                    return Vector3.Zero;
            }
        }

        /// <summary>
        /// Contournement plus réaliste en deux étapes :
        /// recul si possible, puis tentative de dépassement côté gauche ou droit.
        /// </summary>
        private bool PerformRealisticBypass(BlockedVehicleInfo info, Vehicle playerVehicle, BlockageAnalysis analysis)
        {
            var veh = info.Vehicle;
            var driver = info.Driver;

            if (veh == null || driver == null) return false;

            try
            {
                if (!info.HasReversed)
                {
                    if (analysis.CanReverse)
                    {
                        Vector3 target = veh.Position - veh.ForwardVector * TrafficAIConfig.BackupDistance;
                        Function.Call(Hash.CLEAR_PED_TASKS, driver.Handle);
                        Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                            driver.Handle,
                            veh.Handle,
                            target.X,
                            target.Y,
                            target.Z,
                            6f,
                            0,
                            2f);
                        info.HasReversed = true;
                        info.LastReverseTime = DateTime.Now;
                        return true;
                    }
                    return false;
                }

                // Attendre la fin du recul
                if ((DateTime.Now - info.LastReverseTime).TotalSeconds < 1.5f)
                    return false;

                // Après le recul, réévalue et tente un dépassement
                var newAnalysis = AnalyzeBlockage(veh, playerVehicle);
                if (newAnalysis.CanGoLeft)
                {
                    newAnalysis.PreferredDirection = BypassDirection.Left;
                    info.HasReversed = false;
                    return PerformIntelligentBypass(driver, veh, newAnalysis);
                }
                else if (newAnalysis.CanGoRight)
                {
                    newAnalysis.PreferredDirection = BypassDirection.Right;
                    info.HasReversed = false;
                    return PerformIntelligentBypass(driver, veh, newAnalysis);
                }

                info.HasReversed = false;
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"PerformRealisticBypass error: {ex.Message}");
                info.HasReversed = false;
                return false;
            }
        }

        private void ResetVehicleState(BlockedVehicleInfo info)
        {
            info.BlockedTime = 0f;
            info.Honked = false;
            info.HasReversed = false;
            info.LastReverseTime = DateTime.MinValue;
            // Les tentatives de contournement ne sont pas réinitialisées pour éviter les boucles
        }

        private bool IsEmergencyActive(Vehicle veh)
        {
            try
            {
                return veh != null && veh.Exists() && veh.Model.IsEmergencyVehicle && veh.IsSirenActive;
            }
            catch
            {
                return false;
            }
        }

        private bool HandleEmergencyYield(Vehicle veh, Ped driver, List<Vehicle> emergencies)
        {
            foreach (var emer in emergencies)
            {
                if (emer == null || !emer.Exists() || emer == veh) continue;

                // Ignore player's emergency vehicle if it's not moving
                if (emer == Game.Player.Character.CurrentVehicle && emer.Speed < 1f)
                    continue;

                try
                {
                    Vector3 toVeh = veh.Position - emer.Position;
                    float distance = toVeh.Length();
                    Vector3 emerForward = emer.ForwardVector;
                    float dot = Vector3.Dot(emerForward, toVeh);

                    if (dot > 0 && distance < 30f)
                    {
                        Vector3 target = veh.Position + veh.RightVector * 5f;
                        Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                            driver.Handle,
                            veh.Handle,
                            target.X, target.Y, target.Z,
                            10f,
                            0,
                            2f);
                        return true;
                    }
                    else if (dot < 0 && distance < 20f && emer == Game.Player.Character.CurrentVehicle)
                    {
                        Vector3 target = veh.Position + veh.RightVector * 5f + veh.ForwardVector * 10f;
                        Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                            driver.Handle,
                            veh.Handle,
                            target.X, target.Y, target.Z,
                            15f,
                            (int)VehicleDrivingFlags.SwerveAroundAllVehicles,
                            4f);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Emergency yield task error: {ex.Message}");
                }
            }

            return false;
        }

        private void CleanupInvalidEntries()
        {
            var toRemove = _tracked.Where(kvp => 
                kvp.Value.Vehicle == null || 
                !kvp.Value.Vehicle.Exists() || 
                kvp.Value.Driver == null || 
                !kvp.Value.Driver.Exists()
            ).Select(kvp => kvp.Key).ToList();

            foreach (var key in toRemove)
            {
                _tracked.Remove(key);
                _lastActionTime.Remove(key);
            }
        }

        private void CleanupStaleEntries()
        {
            var now = DateTime.Now;
            var toRemove = _tracked.Where(kvp => 
                (now - kvp.Value.LastSeen).TotalSeconds > TrackingTimeout
            ).Select(kvp => kvp.Key).ToList();

            foreach (var key in toRemove)
            {
                _tracked.Remove(key);
                _lastActionTime.Remove(key);
            }
        }

        public void Dispose()
        {
            try
            {
                // Restore normal driving for all tracked vehicles
                foreach (var info in _tracked.Values)
                {
                    if (info.Vehicle?.Driver != null && info.Vehicle.Driver.Exists())
                    {
                        Function.Call(Hash.CLEAR_PED_TASKS, info.Vehicle.Driver.Handle);
                    }
                }
                
                _tracked.Clear();
                _lastActionTime.Clear();
                _processingVehicles.Clear();
                
                Logger.Info("TrafficIntelligenceManager disposed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"TrafficIntelligenceManager dispose error: {ex.Message}");
            }
        }
    }

    public class BlockageAnalysis
    {
        public bool IsBlocked { get; set; }
        public bool IsPlayerBlocking { get; set; }
        public float DistanceToObstacle { get; set; }
        public bool IsInTrafficJam { get; set; }
        public bool CanGoLeft { get; set; }
        public bool CanGoRight { get; set; }
        public bool CanReverse { get; set; }
        public BypassDirection PreferredDirection { get; set; }
    }

    public enum BypassDirection
    {
        None,
        Left,
        Right,
        Reverse
    }
} 