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
    /// RÉACTIVÉ POUR TEST ISOLÉ - Phase 2 du diagnostic
    /// Système d'intelligence de conduite avancé pour les NPCs.
    /// Améliore la détection d'obstacles, le dépassement intelligent et la navigation
    /// tout en respectant le code de la route (feux rouges, priorités).
    /// </summary>
    public class AdvancedDrivingAI : Script, IEventHandler
    {
        private readonly Dictionary<int, AdvancedVehicleInfo> _enhancedVehicles = new();
        private readonly Dictionary<int, DateTime> _lastEnhancement = new();
        private readonly HashSet<int> _processedThisTick = new();
        
        // Configuration de l'IA avancée - EXTRÊMEMENT RÉDUITE pour test isolé
        private const float ENHANCED_SCAN_RADIUS = 20f;      // Réduit encore plus : 35 → 20
        private const float OBSTACLE_DETECTION_RANGE = 10f;   // Réduit encore plus : 15 → 10  
        private const float OVERTAKE_DETECTION_RANGE = 15f;   // Réduit encore plus : 25 → 15
        private const float SAFE_OVERTAKE_CLEARANCE = 8f;
        private const float TRAFFIC_LIGHT_DETECTION_RANGE = 15f; // Réduit encore plus : 20 → 15
        private const float INTERSECTION_APPROACH_SPEED = 15f;
        private const float NORMAL_FOLLOWING_DISTANCE = 6f;
        private const float ENHANCED_FOLLOWING_DISTANCE = 10f;
        private const int MAX_ENHANCED_VEHICLES = 3;          // Réduit encore plus : 6 → 3
        private const float ENHANCEMENT_COOLDOWN = 20f;       // Augmenté encore plus : 12 → 20
        
        // Seuils de vitesse pour l'IA
        private const float SLOW_VEHICLE_THRESHOLD = 8f;
        private const float STOPPED_VEHICLE_THRESHOLD = 2f;
        private const float MIN_OVERTAKE_SPEED = 12f;
        private const float MAX_CITY_SPEED = 50f;
        private const float MAX_HIGHWAY_SPEED = 80f;

        private DateTime _initializationTime = DateTime.Now;
        private bool _isInitialized = false;
        
        public AdvancedDrivingAI()
        {
            Tick += OnTick;
            Interval = 15000; // ULTRA conservateur pour test isolé
            Logger.Info("AdvancedDrivingAI RÉACTIVÉ pour test isolé - Interval 15s");
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                // Délai d'initialisation pour éviter les plantages au démarrage
                if (!_isInitialized)
                {
                    if ((DateTime.Now - _initializationTime).TotalSeconds < 30) return;
                    
                    // Enregistrement retardé au système d'événements
                    try
                    {
                        CentralEventManager.Instance?.RegisterHandler(REALISEventType.TrafficBlock, this);
                        CentralEventManager.Instance?.RegisterHandler(REALISEventType.TrafficJam, this);
                    }
                    catch
                    {
                        // Ignore les erreurs d'enregistrement
                    }
                    
                    _isInitialized = true;
                    Logger.Info("AdvancedDrivingAI initialized after 30s delay");
                    return;
                }
                
                if (!ShouldProcessAI() || EmergencyDisable.IsAIDisabled) return;
                
                _processedThisTick.Clear();
                
                var player = Game.Player.Character;
                if (player?.Exists() != true) return;
                
                var nearbyVehicles = GetRelevantVehicles(player.Position);
                
                // Traitement EXTRÊMEMENT conservateur pour test isolé
                if (nearbyVehicles.Length > 0)
                {
                    ProcessAdvancedDrivingBehavior(nearbyVehicles.Take(1).ToArray()); // TEST ISOLÉ : 1 seul véhicule max
                }
                
                CleanupStaleEntries();
            }
            catch (Exception ex)
            {
                Logger.Error($"AdvancedDrivingAI error: {ex.Message}");
            }
        }

        private bool ShouldProcessAI()
        {
            var player = Game.Player.Character;
            if (player?.Exists() != true) return false;
            
            // SÉCURITÉ SPÉCIALE : Si le joueur est à pied devant des véhicules, réduire drastiquement l'IA
            if (!player.IsInVehicle())
            {
                var nearbyVehicles = World.GetNearbyVehicles(player.Position, 8f);
                if (nearbyVehicles.Length > 2)
                {
                    Logger.Error("Player on foot near multiple vehicles - reducing AI activity");
                    return false; // Désactiver complètement l'IA dans cette situation
                }
            }
            
            return player.IsInVehicle() || player.Position.DistanceTo(Vector3.Zero) > 5f;
        }

        private Vehicle[] GetRelevantVehicles(Vector3 playerPosition)
        {
            try
            {
                return VehicleQueryService.GetNearbyVehicles(playerPosition, ENHANCED_SCAN_RADIUS)
                    .Where(v => v.Driver != null && v.Driver.IsAlive && !v.Driver.IsPlayer)
                    .Where(v => v.Speed > 1f || IsVehicleInTraffic(v))
                    .OrderBy(v => v.Position.DistanceTo(playerPosition))
                    .Take(MAX_ENHANCED_VEHICLES)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<Vehicle>();
            }
        }

        private void ProcessAdvancedDrivingBehavior(Vehicle[] vehicles)
        {
            foreach (var vehicle in vehicles)
            {
                if (_processedThisTick.Count >= MAX_ENHANCED_VEHICLES) break;
                if (_processedThisTick.Contains(vehicle.Handle)) continue;
                
                try
                {
                    if (ShouldEnhanceVehicle(vehicle))
                    {
                        EnhanceVehicleDriving(vehicle);
                        _processedThisTick.Add(vehicle.Handle);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Vehicle enhancement error for {vehicle.Handle}: {ex.Message}");
                }
            }
        }

        private bool ShouldEnhanceVehicle(Vehicle vehicle)
        {
            if (!VehicleQueryService.TryAcquireControl(vehicle)) return false;
            
            try
            {
                // Vérifier le cooldown
                if (_lastEnhancement.TryGetValue(vehicle.Handle, out var lastTime))
                {
                    if ((DateTime.Now - lastTime).TotalSeconds < ENHANCEMENT_COOLDOWN)
                        return false;
                }

                // Analyser si le véhicule a besoin d'amélioration
                var analysis = AnalyzeDrivingContext(vehicle);
                return analysis.NeedsEnhancement;
            }
            finally
            {
                VehicleQueryService.ReleaseControl(vehicle);
            }
        }

        private void EnhanceVehicleDriving(Vehicle vehicle)
        {
            if (!VehicleQueryService.TryAcquireControl(vehicle)) return;
            
            try
            {
                var info = GetOrCreateVehicleInfo(vehicle);
                var analysis = AnalyzeDrivingContext(vehicle);
                
                ApplyDrivingEnhancements(vehicle, info, analysis);
                _lastEnhancement[vehicle.Handle] = DateTime.Now;
            }
            finally
            {
                VehicleQueryService.ReleaseControl(vehicle);
            }
        }

        private AdvancedVehicleInfo GetOrCreateVehicleInfo(Vehicle vehicle)
        {
            if (!_enhancedVehicles.TryGetValue(vehicle.Handle, out var info))
            {
                info = new AdvancedVehicleInfo(vehicle);
                _enhancedVehicles[vehicle.Handle] = info;
            }
            
            info.LastSeen = DateTime.Now;
            return info;
        }

        private DrivingContextAnalysis AnalyzeDrivingContext(Vehicle vehicle)
        {
            var analysis = new DrivingContextAnalysis();
            var driver = vehicle.Driver;
            var position = vehicle.Position;
            var speed = vehicle.Speed;
            
            // Analyse des obstacles et du contexte de conduite
            analysis.ObstaclesAhead = DetectObstaclesAhead(vehicle, OBSTACLE_DETECTION_RANGE);
            analysis.SlowVehicleAhead = DetectSlowVehicleAhead(vehicle);
            analysis.CanSafelyOvertake = CanSafelyOvertake(vehicle);
            analysis.NearTrafficLight = IsNearTrafficLight(position);
            analysis.InIntersection = IsInIntersection(position);
            analysis.IsStuck = IsVehicleStuck(vehicle);
            analysis.CurrentSpeed = speed;
            analysis.RecommendedSpeed = CalculateRecommendedSpeed(vehicle, analysis);
            
            // Déterminer si une amélioration est nécessaire
            analysis.NeedsEnhancement = analysis.ObstaclesAhead.Count > 0 ||
                                      analysis.SlowVehicleAhead ||
                                      analysis.IsStuck ||
                                      (analysis.CanSafelyOvertake && analysis.SlowVehicleAhead);
            
            return analysis;
        }

        private void ApplyDrivingEnhancements(Vehicle vehicle, AdvancedVehicleInfo info, DrivingContextAnalysis analysis)
        {
            var driver = vehicle.Driver;
            
            // 1. Amélioration de la détection d'obstacles
            if (analysis.ObstaclesAhead.Count > 0)
            {
                ApplyObstacleAvoidance(vehicle, driver, analysis.ObstaclesAhead);
            }
            
            // 2. Gestion du dépassement intelligent
            if (analysis.CanSafelyOvertake && analysis.SlowVehicleAhead && !analysis.NearTrafficLight)
            {
                ExecuteIntelligentOvertake(vehicle, driver, analysis);
            }
            
            // 3. Amélioration de la conduite aux intersections
            if (analysis.NearTrafficLight || analysis.InIntersection)
            {
                ApplyIntersectionDriving(vehicle, driver, analysis);
            }
            
            // 4. Ajustement de la vitesse et distance de sécurité
            ApplySpeedAndDistanceAdjustments(vehicle, driver, analysis);
            
            // 5. Amélioration de la navigation générale
            ApplyNavigationEnhancements(vehicle, driver, analysis);
        }

        private List<Vector3> DetectObstaclesAhead(Vehicle vehicle, float range)
        {
            var obstacles = new List<Vector3>();
            var startPos = vehicle.Position;
            var direction = vehicle.ForwardVector;
            
            // Vérification par raycast pour détecter les obstacles
            for (float distance = 5f; distance <= range; distance += 3f)
            {
                var checkPos = startPos + direction * distance;
                
                // Raycast vers le bas pour vérifier le terrain
                var groundHit = World.Raycast(checkPos + Vector3.WorldUp * 2f, 
                                            checkPos - Vector3.WorldUp * 5f, 
                                            IntersectFlags.Map | IntersectFlags.Objects);
                
                if (groundHit.DidHit)
                {
                    var groundHeight = groundHit.HitPosition.Z;
                    if (Math.Abs(groundHeight - vehicle.Position.Z) > 3f)
                    {
                        obstacles.Add(checkPos);
                    }
                }
                
                // Vérification des objets devant
                var objectHit = World.Raycast(startPos, checkPos, 
                                            IntersectFlags.Everything, 
                                            vehicle);
                
                if (objectHit.DidHit && objectHit.HitEntity != vehicle)
                {
                    obstacles.Add(objectHit.HitPosition);
                }
            }
            
            return obstacles;
        }

        private bool DetectSlowVehicleAhead(Vehicle vehicle)
        {
            var vehiclesAhead = World.GetNearbyVehicles(vehicle.Position + vehicle.ForwardVector * 15f, 20f)
                .Where(v => v != vehicle && v.Driver != null)
                .Where(v => IsVehicleInFront(vehicle, v))
                .ToArray();
                
            return vehiclesAhead.Any(v => v.Speed < SLOW_VEHICLE_THRESHOLD);
        }

        private bool CanSafelyOvertake(Vehicle vehicle)
        {
            if (vehicle.Speed < MIN_OVERTAKE_SPEED) return false;
            
            var leftLane = GetAdjacentLanePosition(vehicle, true);
            var rightLane = GetAdjacentLanePosition(vehicle, false);
            
            // Vérifier si les voies adjacentes sont libres
            bool leftClear = IsLaneClear(leftLane, OVERTAKE_DETECTION_RANGE);
            bool rightClear = IsLaneClear(rightLane, OVERTAKE_DETECTION_RANGE);
            
            return leftClear || rightClear;
        }

        private bool IsNearTrafficLight(Vector3 position)
        {
            // Utiliser les fonctions natives pour détecter les feu de circulation
            return Function.Call<bool>(Hash.IS_POINT_OBSCURED_BY_A_MISSION_ENTITY, 
                                     position.X, position.Y, position.Z, 
                                     5f, 5f, 5f, 0);
        }

        private bool IsInIntersection(Vector3 position)
        {
            // Détecter si le véhicule est dans une intersection
            return Function.Call<bool>(Hash.IS_POINT_ON_ROAD, 
                                     position.X, position.Y, position.Z, 0);
        }

        private bool IsVehicleStuck(Vehicle vehicle)
        {
            if (!_enhancedVehicles.TryGetValue(vehicle.Handle, out var info))
                return false;
                
            var timeSinceMoving = (DateTime.Now - info.LastMovementTime).TotalSeconds;
            return vehicle.Speed < STOPPED_VEHICLE_THRESHOLD && timeSinceMoving > 5f;
        }

        private float CalculateRecommendedSpeed(Vehicle vehicle, DrivingContextAnalysis analysis)
        {
            float baseSpeed = IsOnHighway(vehicle.Position) ? MAX_HIGHWAY_SPEED : MAX_CITY_SPEED;
            
            // Réduire la vitesse selon le contexte
            if (analysis.NearTrafficLight) baseSpeed *= 0.6f;
            if (analysis.InIntersection) baseSpeed *= 0.4f;
            if (analysis.ObstaclesAhead.Count > 0) baseSpeed *= 0.5f;
            if (analysis.SlowVehicleAhead) baseSpeed *= 0.7f;
            
            return Math.Max(baseSpeed, 10f); // Vitesse minimum
        }

        private void ApplyObstacleAvoidance(Vehicle vehicle, Ped driver, List<Vector3> obstacles)
        {
            if (obstacles.Count == 0) return;
            
            var nearestObstacle = obstacles.OrderBy(o => o.DistanceTo(vehicle.Position)).First();
            var avoidanceDirection = CalculateAvoidanceDirection(vehicle, nearestObstacle);
            
            // Appliquer l'évitement d'obstacles
            var avoidanceTarget = vehicle.Position + avoidanceDirection * 10f;
            
            Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
                         driver.Handle,
                         vehicle.Handle,
                         avoidanceTarget.X, avoidanceTarget.Y, avoidanceTarget.Z,
                         vehicle.Speed * 0.7f,
                         786603, // Mode de conduite normal avec évitement
                         5f);
        }

        private void ExecuteIntelligentOvertake(Vehicle vehicle, Ped driver, DrivingContextAnalysis analysis)
        {
            var overtakeDirection = DetermineOvertakeDirection(vehicle);
            if (overtakeDirection == Vector3.Zero) return;
            
            var overtakeTarget = vehicle.Position + overtakeDirection * 25f + vehicle.ForwardVector * 30f;
            
            // Configurer le dépassement
            Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
                         driver.Handle,
                         vehicle.Handle,
                         overtakeTarget.X, overtakeTarget.Y, overtakeTarget.Z,
                         Math.Min(analysis.RecommendedSpeed * 1.2f, MAX_CITY_SPEED),
                         786468, // Mode de conduite pour dépassement
                         3f);
            
            // Améliorer le comportement de conduite
            SetDrivingStyle(driver, DrivingStyle.AvoidTrafficExtremely);
        }

        private void ApplyIntersectionDriving(Vehicle vehicle, Ped driver, DrivingContextAnalysis analysis)
        {
            // Ralentir à l'approche des intersections
            var targetSpeed = INTERSECTION_APPROACH_SPEED;
            
            if (analysis.NearTrafficLight)
            {
                // Vérifier l'état du feu (approximation)
                var lightState = GetTrafficLightState(vehicle.Position);
                if (lightState == TrafficAI.TrafficLightState.Red || lightState == TrafficAI.TrafficLightState.Yellow)
                {
                    // S'arrêter au feu rouge/orange
                    Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION,
                                 driver.Handle,
                                 vehicle.Handle,
                                 6, // Brake
                                 3000);
                    return;
                }
            }
            
            // Conduite prudente en intersection
            SetDrivingStyle(driver, DrivingStyle.Normal);
            
            Function.Call(Hash.SET_DRIVE_TASK_CRUISE_SPEED,
                         driver.Handle,
                         targetSpeed);
        }

        private void ApplySpeedAndDistanceAdjustments(Vehicle vehicle, Ped driver, DrivingContextAnalysis analysis)
        {
            // Ajuster la distance de sécurité
            var followingDistance = analysis.SlowVehicleAhead ? 
                                  ENHANCED_FOLLOWING_DISTANCE : 
                                  NORMAL_FOLLOWING_DISTANCE;
            
            // Régler la vitesse de croisière
            Function.Call(Hash.SET_DRIVE_TASK_CRUISE_SPEED,
                         driver.Handle,
                         analysis.RecommendedSpeed);
            
            // Améliorer le comportement général
            Function.Call(Hash.SET_DRIVER_ABILITY,
                         driver.Handle,
                         1.0f); // Capacité de conduite maximale
            
            Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS,
                         driver.Handle,
                         0.3f); // Agressivité modérée
        }

        private void ApplyNavigationEnhancements(Vehicle vehicle, Ped driver, DrivingContextAnalysis analysis)
        {
            // Améliorer la navigation générale
            SetDrivingStyle(driver, DrivingStyle.Normal);
            
            // Activer les comportements avancés
            Function.Call(Hash.SET_PED_CONFIG_FLAG,
                         driver.Handle,
                         (int)PedConfigFlagToggles.SteersAroundVehicles,
                         true);
            
            Function.Call(Hash.SET_PED_CONFIG_FLAG,
                         driver.Handle,
                         (int)PedConfigFlagToggles.SteersAroundObjects,
                         true);
            
            Function.Call(Hash.SET_PED_CONFIG_FLAG,
                         driver.Handle,
                         (int)PedConfigFlagToggles.SteerAroundDeadBodies,
                         true);
        }

        // Méthodes utilitaires

        private Vector3 CalculateAvoidanceDirection(Vehicle vehicle, Vector3 obstacle)
        {
            var toObstacle = obstacle - vehicle.Position;
            var rightVector = vehicle.RightVector;
            
            // Choisir la direction d'évitement (droite ou gauche)
            var dot = Vector3.Dot(toObstacle, rightVector);
            return dot > 0 ? -rightVector : rightVector;
        }

        private Vector3 DetermineOvertakeDirection(Vehicle vehicle)
        {
            var leftLane = GetAdjacentLanePosition(vehicle, true);
            var rightLane = GetAdjacentLanePosition(vehicle, false);
            
            bool leftClear = IsLaneClear(leftLane, OVERTAKE_DETECTION_RANGE);
            bool rightClear = IsLaneClear(rightLane, OVERTAKE_DETECTION_RANGE);
            
            if (leftClear && !rightClear) return -vehicle.RightVector;
            if (rightClear && !leftClear) return vehicle.RightVector;
            if (leftClear && rightClear) return -vehicle.RightVector; // Préférer la gauche
            
            return Vector3.Zero;
        }

        private Vector3 GetAdjacentLanePosition(Vehicle vehicle, bool left)
        {
            var offset = left ? -vehicle.RightVector * 4f : vehicle.RightVector * 4f;
            return vehicle.Position + offset;
        }

        private bool IsLaneClear(Vector3 lanePosition, float distance)
        {
            var vehicles = World.GetNearbyVehicles(lanePosition, distance);
            return vehicles.Length < 2; // Tolérer un véhicule distant
        }

        private bool IsVehicleInFront(Vehicle reference, Vehicle target)
        {
            var toTarget = target.Position - reference.Position;
            var dot = Vector3.Dot(toTarget.Normalized, reference.ForwardVector);
            return dot > 0.7f; // Angle de 45 degrés environ
        }

        private bool IsOnHighway(Vector3 position)
        {
            // Approximation basée sur la zone
            return position.DistanceTo(Vector3.Zero) > 500f; // Zones périphériques = autoroutes
        }

        private TrafficAI.TrafficLightState GetTrafficLightState(Vector3 position)
        {
            // Approximation de l'état des feux (à améliorer avec plus de logique)
            var hash = (int)(position.X + position.Y) % 3;
            return (TrafficAI.TrafficLightState)hash;
        }

        private bool IsVehicleInTraffic(Vehicle vehicle)
        {
            var nearbyVehicles = World.GetNearbyVehicles(vehicle.Position, 15f);
            return nearbyVehicles.Length > 2;
        }

        private void SetDrivingStyle(Ped driver, DrivingStyle style)
        {
            Function.Call(Hash.SET_DRIVE_TASK_DRIVING_STYLE,
                         driver.Handle,
                         (int)style);
        }

        private void CleanupStaleEntries()
        {
            var cutoff = DateTime.Now.AddMinutes(-2);
            
            var staleKeys = _enhancedVehicles
                .Where(kvp => kvp.Value.LastSeen < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in staleKeys)
            {
                _enhancedVehicles.Remove(key);
                _lastEnhancement.Remove(key);
            }
        }

        // Implémentation IEventHandler
        public bool CanHandle(GameEvent gameEvent)
        {
            return gameEvent.EventType == REALISEventType.TrafficBlock ||
                   gameEvent.EventType == REALISEventType.TrafficJam;
        }

        public void Handle(GameEvent gameEvent)
        {
            try
            {
                if (gameEvent is TrafficBlockEvent trafficEvent)
                {
                    HandleTrafficBlockEvent(trafficEvent);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Event handling error: {ex.Message}");
            }
        }

        private void HandleTrafficBlockEvent(TrafficBlockEvent trafficEvent)
        {
            var vehicle = trafficEvent.BlockedVehicle;
            if (vehicle?.Driver != null && !vehicle.Driver.IsPlayer)
            {
                // Forcer l'amélioration du véhicule bloqué
                _lastEnhancement.Remove(vehicle.Handle);
                EnhanceVehicleDriving(vehicle);
            }
        }

        ~AdvancedDrivingAI()
        {
            try
            {
                CentralEventManager.Instance?.UnregisterHandler(REALISEventType.TrafficBlock, this);
                CentralEventManager.Instance?.UnregisterHandler(REALISEventType.TrafficJam, this);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    // Classes de support

    public class AdvancedVehicleInfo
    {
        public Vehicle Vehicle { get; }
        public DateTime LastSeen { get; set; }
        public DateTime LastMovementTime { get; set; }
        public float LastRecordedSpeed { get; set; }
        public int EnhancementCount { get; set; }

        public AdvancedVehicleInfo(Vehicle vehicle)
        {
            Vehicle = vehicle;
            LastSeen = DateTime.Now;
            LastMovementTime = DateTime.Now;
            LastRecordedSpeed = vehicle.Speed;
        }
    }

    public class DrivingContextAnalysis
    {
        public List<Vector3> ObstaclesAhead { get; set; } = new();
        public bool SlowVehicleAhead { get; set; }
        public bool CanSafelyOvertake { get; set; }
        public bool NearTrafficLight { get; set; }
        public bool InIntersection { get; set; }
        public bool IsStuck { get; set; }
        public float CurrentSpeed { get; set; }
        public float RecommendedSpeed { get; set; }
        public bool NeedsEnhancement { get; set; }
    }



    public enum DrivingStyle
    {
        Normal = 786603,
        IgnoreLights = 2883621,
        SometimesOvertakeTraffic = 5,
        Rushed = 1074528293,
        AvoidTraffic = 786468,
        AvoidTrafficExtremely = 6
    }
}