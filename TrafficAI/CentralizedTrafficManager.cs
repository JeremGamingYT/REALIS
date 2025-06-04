using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using REALIS.Common;
using REALIS.Core;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Version centralisée et robuste du gestionnaire de trafic.
    /// Utilise le système d'événements central pour éviter les conflits.
    /// </summary>
    public class CentralizedTrafficManager : Script, IEventHandler
    {
        private readonly string MANAGER_ID = "CentralizedTrafficManager";
        private const int LOCK_PRIORITY = 10;
        private readonly Dictionary<int, TrafficVehicleInfo> _trackedVehicles = new();
        private readonly Dictionary<int, DateTime> _lastProcessTime = new();
        
        // Configuration ultra-conservative pour éviter les crashes
        private const float SCAN_RADIUS = 25f;
        private const float PROCESSING_INTERVAL = 10f;
        private const float SPEED_THRESHOLD = 1.2f;
        private const float BLOCKED_TIME_THRESHOLD = 8f;
        private const float HONK_COOLDOWN = 15f;
        private const float BYPASS_COOLDOWN = 20f;
        private const int MAX_CONCURRENT_PROCESSING = 3;
        private const float PLAYER_SAFE_ZONE = 10f;
        
        private DateTime _lastFullScan = DateTime.MinValue;
        private int _processedThisTick = 0;
        private bool _isRegistered = false;

        public CentralizedTrafficManager()
        {
            Tick += OnTick;
            Interval = 8000;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                if (!MovementThrottler.CanProcessTraffic())
                    return;
                    
                if (CentralEventManager.Instance == null)
                    return;
                
                if (!_isRegistered)
                {
                    CentralEventManager.Instance.RegisterHandler(REALISEventType.TrafficBlock, this);
                    CentralEventManager.Instance.RegisterHandler(REALISEventType.VehicleStuck, this);
                    _isRegistered = true;
                }

                ProcessTrafficAI();
            }
            catch (Exception ex)
            {
                SafeLogError($"Traffic AI error: {ex.Message}");
            }
        }

        private void ProcessTrafficAI()
        {
            try
            {
                if (!ShouldProcess()) return;
                
                _processedThisTick = 0;
                ProcessTrafficIntelligence();
                CleanupStaleEntries();
            }
            catch (Exception ex)
            {
                SafeLogError($"Traffic processing error: {ex.Message}");
            }
        }

        private bool ShouldProcess()
        {
            var player = Game.Player.Character;
            if (player?.CurrentVehicle == null || !player.Exists()) return false;

            var playerVehicle = player.CurrentVehicle;
            bool emergencyActive = playerVehicle.Model.IsEmergencyVehicle && playerVehicle.IsSirenActive;

            if (playerVehicle.Speed < 0.3f && !emergencyActive) return false;

            if ((DateTime.Now - _lastFullScan).TotalSeconds < PROCESSING_INTERVAL) return false;

            if (playerVehicle.Speed < 2f && !emergencyActive) return false;

            return true;
        }

        private void SafeLogError(string message)
        {
            try
            {
                Logger.Error($"[{MANAGER_ID}] {message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public bool CanHandle(GameEvent gameEvent)
        {
            return gameEvent.EventType == REALISEventType.TrafficBlock || 
                   gameEvent.EventType == REALISEventType.VehicleStuck;
        }

        public void Handle(GameEvent gameEvent)
        {
            try
            {
                if (gameEvent is TrafficBlockEvent trafficEvent)
                {
                    Logger.Info($"Handling traffic block event at {trafficEvent.Position}");
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"Event handling error: {ex.Message}");
            }
        }

        // Additional methods will be added in a separate file due to length limitations
        private void ProcessTrafficIntelligence()
        {
            var player = Game.Player.Character;
            var playerVehicle = player.CurrentVehicle;
            
            var nearbyVehicles = GetSafeNearbyVehicles(player.Position, SCAN_RADIUS);
            
            var relevantVehicles = nearbyVehicles
                .Where(v => v != null && v.Exists() && v.Driver != null)
                .Where(v => v.Driver != player)
                .OrderBy(v => v.Position.DistanceTo(player.Position))
                .Take(MAX_CONCURRENT_PROCESSING)
                .ToList();

            foreach (var vehicle in relevantVehicles)
            {
                if (_processedThisTick >= MAX_CONCURRENT_PROCESSING) break;
                
                ProcessSingleVehicle(vehicle, playerVehicle);
                _processedThisTick++;
            }
            
            _lastFullScan = DateTime.Now;
        }

        private Vehicle[] GetSafeNearbyVehicles(Vector3 position, float radius)
        {
            try
            {
                return World.GetNearbyVehicles(position, radius)
                    .Where(v => v != null && v.Exists() && !v.IsDead)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<Vehicle>();
            }
        }

        private void ProcessSingleVehicle(Vehicle vehicle, Vehicle playerVehicle)
        {
            try
            {
                if (!VehicleQueryService.TryAcquireControl(vehicle))
                    return;

                if (!_trackedVehicles.ContainsKey(vehicle.Handle))
                {
                    _trackedVehicles[vehicle.Handle] = new TrafficVehicleInfo(vehicle);
                }

                var info = _trackedVehicles[vehicle.Handle];
                info.LastSeen = DateTime.Now;

                UpdateVehicleAnalysis(info, playerVehicle);
            }
            catch (Exception ex)
            {
                SafeLogError($"Vehicle processing error for {vehicle.Handle}: {ex.Message}");
            }
            finally
            {
                VehicleQueryService.ReleaseControl(vehicle);
            }
        }

        private void UpdateVehicleAnalysis(TrafficVehicleInfo info, Vehicle playerVehicle)
        {
            var vehicle = info.Vehicle;
            if (!vehicle.Exists()) return;

            if (vehicle.Speed > SPEED_THRESHOLD)
            {
                info.BlockedDuration = 0f;
                info.HasHonked = false;
                return;
            }

            info.BlockedDuration += PROCESSING_INTERVAL;
            
            if (info.BlockedDuration > BLOCKED_TIME_THRESHOLD)
            {
                TakeSimpleAction(info, playerVehicle);
            }
        }

        private void TakeSimpleAction(TrafficVehicleInfo info, Vehicle playerVehicle)
        {
            var timeSinceLastAction = DateTime.Now - info.LastActionTime;
            if (timeSinceLastAction.TotalSeconds < HONK_COOLDOWN) return;

            var vehicle = info.Vehicle;
            var driver = vehicle.Driver;
            
            if (driver == null || !driver.Exists()) return;

            float distanceToPlayer = vehicle.Position.DistanceTo(playerVehicle.Position);
            
            if (!info.HasHonked && distanceToPlayer < 15f)
            {
                // Simple honk
                Function.Call(Hash.START_VEHICLE_HORN, vehicle.Handle, 800, 0, false);
                info.HasHonked = true;
                info.LastActionTime = DateTime.Now;
            }
            else if (info.BypassAttempts < 2 && timeSinceLastAction.TotalSeconds > BYPASS_COOLDOWN)
            {
                // Simple bypass
                PerformSimpleBypass(driver, vehicle, playerVehicle);
                info.BypassAttempts++;
                info.LastActionTime = DateTime.Now;
            }
        }

        private void PerformSimpleBypass(Ped driver, Vehicle vehicle, Vehicle playerVehicle)
        {
            try
            {
                Vector3 vehPos = vehicle.Position;
                Vector3 playerPos = playerVehicle.Position;
                Vector3 right = vehicle.RightVector;
                Vector3 forward = vehicle.ForwardVector;
                
                // Choose side away from player
                Vector3 toPlayer = playerPos - vehPos;
                float rightDot = Vector3.Dot(toPlayer, right);
                
                Vector3 targetPos = rightDot > 0 
                    ? vehPos - right * 6f + forward * 12f  // Player on right, go left
                    : vehPos + right * 6f + forward * 12f; // Player on left, go right
                
                Function.Call(Hash.CLEAR_PED_TASKS, driver.Handle);
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                    driver.Handle,
                    vehicle.Handle,
                    targetPos.X,
                    targetPos.Y,
                    targetPos.Z,
                    15f,
                    (int)(VehicleDrivingFlags.StopForVehicles | VehicleDrivingFlags.SwerveAroundAllVehicles),
                    5f
                );
            }
            catch (Exception ex)
            {
                SafeLogError($"Bypass error: {ex.Message}");
            }
        }

        private void CleanupStaleEntries()
        {
            try
            {
                var now = DateTime.Now;
                var staleEntries = _trackedVehicles.Where(kvp =>
                    !kvp.Value.Vehicle.Exists() ||
                    (now - kvp.Value.LastSeen).TotalSeconds > 60
                ).Select(kvp => kvp.Key).ToList();
                
                foreach (var key in staleEntries)
                {
                    _trackedVehicles.Remove(key);
                    _lastProcessTime.Remove(key);
                }
                
                VehicleQueryService.Cleanup();
            }
            catch (Exception ex)
            {
                SafeLogError($"Cleanup error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                if (_isRegistered && CentralEventManager.Instance != null)
                {
                    CentralEventManager.Instance.UnregisterHandler(REALISEventType.TrafficBlock, this);
                    CentralEventManager.Instance.UnregisterHandler(REALISEventType.VehicleStuck, this);
                }
                
                foreach (var info in _trackedVehicles.Values)
                {
                    if (info.Vehicle?.Driver != null && info.Vehicle.Driver.Exists())
                    {
                        Function.Call(Hash.CLEAR_PED_TASKS, info.Vehicle.Driver.Handle);
                    }
                }
                
                _trackedVehicles.Clear();
                _lastProcessTime.Clear();
            }
            catch (Exception ex)
            {
                SafeLogError($"Dispose error: {ex.Message}");
            }
        }
    }

    internal class TrafficVehicleInfo
    {
        public Vehicle Vehicle { get; }
        public float BlockedDuration { get; set; }
        public bool HasHonked { get; set; }
        public int BypassAttempts { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime LastActionTime { get; set; }

        public TrafficVehicleInfo(Vehicle vehicle)
        {
            Vehicle = vehicle;
            BlockedDuration = 0f;
            HasHonked = false;
            BypassAttempts = 0;
            LastSeen = DateTime.Now;
            LastActionTime = DateTime.MinValue;
        }
    }
} 