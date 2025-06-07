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
    /// Système de navigation intelligent pour améliorer la planification de route
    /// et la détection de collisions des NPCs.
    /// </summary>
    public class SmartNavigationSystem_DISABLED : Script
    {
        private readonly Dictionary<int, NavigationInfo> _navigatingVehicles = new();
        private readonly HashSet<Vector3> _knownObstacles = new();
        private readonly Dictionary<Vector3, DateTime> _obstacleTimestamps = new();
        
        // Configuration du système de navigation
        private const float NAVIGATION_SCAN_RADIUS = 75f;
        private const float COLLISION_PREDICT_DISTANCE = 20f;
        private const float ROUTE_OPTIMIZATION_INTERVAL = 15f;
        private const float OBSTACLE_MEMORY_TIME = 30f; // Mémoriser les obstacles 30 secondes
        private const int MAX_NAVIGATION_UPDATES = 8;
        private const float INTERSECTION_SLOWDOWN_DISTANCE = 25f;
        private const float LANE_CHANGE_SAFETY_DISTANCE = 15f;
        
        // Paramètres de détection avancée
        private const float PEDESTRIAN_DETECTION_RANGE = 12f;
        private const float VEHICLE_PREDICTION_TIME = 3f; // Prédire 3 secondes à l'avance
        private const float EMERGENCY_BRAKE_THRESHOLD = 8f;
        
        private DateTime _lastNavigationUpdate = DateTime.MinValue;

        private DateTime _initializationTime = DateTime.Now;
        private bool _isInitialized = false;
        
        public SmartNavigationSystem_DISABLED()
        {
            // TEMPORAIREMENT DÉSACTIVÉ POUR TEST
            // Tick += OnTick;
            // Interval = 15000;
            Logger.Info("SmartNavigationSystem DISABLED for crash testing");
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                // Délai d'initialisation encore plus long
                if (!_isInitialized)
                {
                    if ((DateTime.Now - _initializationTime).TotalSeconds < 60) return;
                    _isInitialized = true;
                    Logger.Info("SmartNavigationSystem initialized after 60s delay");
                    return;
                }
                
                if (!ShouldProcessNavigation() || EmergencyDisable.IsAIDisabled) return;
                
                var player = Game.Player.Character;
                if (player?.Exists() != true) return;
                
                var relevantVehicles = GetNavigationCandidates(player.Position);
                
                // Traitement ultra-limité
                if (relevantVehicles.Length > 0)
                {
                    ProcessSmartNavigation(relevantVehicles.Take(2).ToArray()); // Max 2 véhicules
                }
                
                UpdateObstacleMemory();
                
                _lastNavigationUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
                Logger.Error($"SmartNavigationSystem error: {ex.Message}");
            }
        }

        private bool ShouldProcessNavigation()
        {
            if ((DateTime.Now - _lastNavigationUpdate).TotalSeconds < ROUTE_OPTIMIZATION_INTERVAL)
                return false;
                
            var player = Game.Player.Character;
            return player?.Exists() == true;
        }

        private Vehicle[] GetNavigationCandidates(Vector3 playerPosition)
        {
            try
            {
                return VehicleQueryService.GetNearbyVehicles(playerPosition, NAVIGATION_SCAN_RADIUS)
                    .Where(v => v.Driver != null && v.Driver.IsAlive && !v.Driver.IsPlayer)
                    .Where(v => v.Speed > 3f) // Véhicules en mouvement
                    .OrderBy(v => v.Position.DistanceTo(playerPosition))
                    .Take(MAX_NAVIGATION_UPDATES)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<Vehicle>();
            }
        }

        private void ProcessSmartNavigation(Vehicle[] vehicles)
        {
            foreach (var vehicle in vehicles)
            {
                try
                {
                    if (!VehicleQueryService.TryAcquireControl(vehicle)) continue;
                    
                    var navInfo = GetOrCreateNavigationInfo(vehicle);
                    var analysis = AnalyzeNavigationContext(vehicle);
                    
                    ApplySmartNavigation(vehicle, navInfo, analysis);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Navigation processing error for {vehicle.Handle}: {ex.Message}");
                }
                finally
                {
                    VehicleQueryService.ReleaseControl(vehicle);
                }
            }
        }

        private NavigationInfo GetOrCreateNavigationInfo(Vehicle vehicle)
        {
            if (!_navigatingVehicles.TryGetValue(vehicle.Handle, out var info))
            {
                info = new NavigationInfo(vehicle);
                _navigatingVehicles[vehicle.Handle] = info;
            }
            
            info.LastUpdate = DateTime.Now;
            return info;
        }

        private NavigationAnalysis AnalyzeNavigationContext(Vehicle vehicle)
        {
            var analysis = new NavigationAnalysis();
            var position = vehicle.Position;
            var forward = vehicle.ForwardVector;
            
            // Analyse prédictive des collisions
            analysis.PotentialCollisions = PredictPotentialCollisions(vehicle);
            analysis.PedestriansNear = DetectNearbyPedestrians(vehicle);
            analysis.IntersectionAhead = DetectIntersectionAhead(vehicle);
            analysis.LaneChangeOpportunity = AnalyzeLaneChangeOpportunity(vehicle);
            analysis.OptimalRoute = CalculateOptimalRoute(vehicle);
            analysis.EmergencyBrakeNeeded = RequiresEmergencyBraking(vehicle, analysis);
            
            // Analyse du terrain et des obstacles
            analysis.TerrainDifficulty = AnalyzeTerrainDifficulty(vehicle);
            analysis.KnownObstaclesAhead = GetKnownObstaclesInPath(vehicle);
            
            return analysis;
        }

        private void ApplySmartNavigation(Vehicle vehicle, NavigationInfo navInfo, NavigationAnalysis analysis)
        {
            var driver = vehicle.Driver;
            
            // 1. Gestion des freinages d'urgence
            if (analysis.EmergencyBrakeNeeded)
            {
                ApplyEmergencyBraking(vehicle, driver);
                return;
            }
            
            // 2. Navigation prédictive pour éviter les collisions
            if (analysis.PotentialCollisions.Count > 0)
            {
                ApplyCollisionAvoidance(vehicle, driver, analysis.PotentialCollisions);
            }
            
            // 3. Gestion intelligente des piétons
            if (analysis.PedestriansNear.Count > 0)
            {
                ApplyPedestrianAwareness(vehicle, driver, analysis.PedestriansNear);
            }
            
            // 4. Navigation aux intersections
            if (analysis.IntersectionAhead)
            {
                ApplyIntersectionNavigation(vehicle, driver);
            }
            
            // 5. Changements de voie intelligents
            if (analysis.LaneChangeOpportunity.IsBeneficial)
            {
                ExecuteSmartLaneChange(vehicle, driver, analysis.LaneChangeOpportunity);
            }
            
            // 6. Optimisation de la route
            if (analysis.OptimalRoute.Count > 0)
            {
                ApplyRouteOptimization(vehicle, driver, analysis.OptimalRoute);
            }
            
            // 7. Amélioration de la détection d'obstacles
            ApplyAdvancedObstacleDetection(vehicle, driver, analysis);
        }

        private List<CollisionPrediction> PredictPotentialCollisions(Vehicle vehicle)
        {
            var predictions = new List<CollisionPrediction>();
            var currentPos = vehicle.Position;
            var velocity = vehicle.Velocity;
            
            // Prédire les positions futures
            for (float time = 0.5f; time <= VEHICLE_PREDICTION_TIME; time += 0.5f)
            {
                var futurePos = currentPos + velocity * time;
                
                // Vérifier les collisions potentielles à cette position
                var nearbyVehicles = World.GetNearbyVehicles(futurePos, 8f)
                    .Where(v => v != vehicle)
                    .ToArray();
                
                foreach (var otherVehicle in nearbyVehicles)
                {
                    var otherFuturePos = otherVehicle.Position + otherVehicle.Velocity * time;
                    var distance = futurePos.DistanceTo(otherFuturePos);
                    
                    if (distance < 6f) // Collision potentielle
                    {
                        predictions.Add(new CollisionPrediction
                        {
                            TimeToCollision = time,
                            CollisionPoint = futurePos,
                            OtherVehicle = otherVehicle,
                            Severity = CalculateCollisionSeverity(vehicle, otherVehicle, distance)
                        });
                    }
                }
            }
            
            return predictions.OrderBy(p => p.TimeToCollision).ToList();
        }

        private List<Ped> DetectNearbyPedestrians(Vehicle vehicle)
        {
            var pedestrians = World.GetNearbyPeds(vehicle.Position + vehicle.ForwardVector * 8f, PEDESTRIAN_DETECTION_RANGE)
                .Where(p => p.IsOnFoot && p.IsAlive)
                .Where(p => IsInVehiclePath(vehicle, p.Position))
                .ToList();
                
            return pedestrians;
        }

        private bool DetectIntersectionAhead(Vehicle vehicle)
        {
            var checkPos = vehicle.Position + vehicle.ForwardVector * INTERSECTION_SLOWDOWN_DISTANCE;
            
            // Approximation de détection d'intersection
            var nearbyVehicles = World.GetNearbyVehicles(checkPos, 15f);
            var vehiclesFromDifferentDirections = nearbyVehicles.Count(v => 
                Math.Abs(Vector3.Dot(v.ForwardVector, vehicle.ForwardVector)) < 0.5f);
                
            return vehiclesFromDifferentDirections >= 2;
        }

        private LaneChangeAnalysis AnalyzeLaneChangeOpportunity(Vehicle vehicle)
        {
            var analysis = new LaneChangeAnalysis();
            
            var leftLane = vehicle.Position - vehicle.RightVector * 4f;
            var rightLane = vehicle.Position + vehicle.RightVector * 4f;
            
            // Analyser la voie de gauche
            var leftSafety = AnalyzeLaneSafety(leftLane, vehicle);
            var rightSafety = AnalyzeLaneSafety(rightLane, vehicle);
            
            analysis.LeftLaneSafe = leftSafety.IsSafe;
            analysis.RightLaneSafe = rightSafety.IsSafe;
            analysis.LeftLaneBenefit = leftSafety.SpeedBenefit;
            analysis.RightLaneBenefit = rightSafety.SpeedBenefit;
            analysis.IsBeneficial = (leftSafety.IsSafe && leftSafety.SpeedBenefit > 5f) ||
                                   (rightSafety.IsSafe && rightSafety.SpeedBenefit > 5f);
            
            analysis.PreferredDirection = leftSafety.SpeedBenefit > rightSafety.SpeedBenefit ? 
                                        LaneChangeDirection.Left : LaneChangeDirection.Right;
            
            return analysis;
        }

        private List<Vector3> CalculateOptimalRoute(Vehicle vehicle)
        {
            var route = new List<Vector3>();
            var currentPos = vehicle.Position;
            var direction = vehicle.ForwardVector;
            
            // Calculer une route optimale en évitant les obstacles connus
            for (float distance = 20f; distance <= 60f; distance += 20f)
            {
                var targetPos = currentPos + direction * distance;
                
                // Ajuster pour éviter les obstacles connus
                targetPos = AdjustForKnownObstacles(targetPos);
                route.Add(targetPos);
            }
            
            return route;
        }

        private bool RequiresEmergencyBraking(Vehicle vehicle, NavigationAnalysis analysis)
        {
            // Vérifier si un freinage d'urgence est nécessaire
            if (analysis.PotentialCollisions.Count > 0)
            {
                var immediateThreat = analysis.PotentialCollisions.First();
                return immediateThreat.TimeToCollision < 1.5f && 
                       immediateThreat.Severity > CollisionSeverity.Minor;
            }
            
            // Vérifier les piétons en danger
            foreach (var ped in analysis.PedestriansNear)
            {
                var distance = ped.Position.DistanceTo(vehicle.Position);
                if (distance < EMERGENCY_BRAKE_THRESHOLD && vehicle.Speed > 10f)
                    return true;
            }
            
            return false;
        }

        private TerrainDifficulty AnalyzeTerrainDifficulty(Vehicle vehicle)
        {
            var ahead = vehicle.Position + vehicle.ForwardVector * 15f;
            
            // Vérifier la pente
            var heightDiff = ahead.Z - vehicle.Position.Z;
            if (Math.Abs(heightDiff) > 3f)
                return TerrainDifficulty.Steep;
            
            // Vérifier les surfaces
            var surfaceHash = Function.Call<uint>(Hash.GET_STREET_NAME_AT_COORD, 
                                                ahead.X, ahead.Y, ahead.Z);
            
            // Logique simplifiée pour déterminer la difficulté du terrain
            return TerrainDifficulty.Normal;
        }

        private List<Vector3> GetKnownObstaclesInPath(Vehicle vehicle)
        {
            var vehiclePath = GetVehiclePathPoints(vehicle, 30f);
            var obstaclesInPath = new List<Vector3>();
            
            foreach (var pathPoint in vehiclePath)
            {
                var nearbyObstacles = _knownObstacles
                    .Where(obs => obs.DistanceTo(pathPoint) < 5f)
                    .ToList();
                    
                obstaclesInPath.AddRange(nearbyObstacles);
            }
            
            return obstaclesInPath;
        }

        private void ApplyEmergencyBraking(Vehicle vehicle, Ped driver)
        {
            // Freinage d'urgence
            Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION,
                         driver.Handle,
                         vehicle.Handle,
                         6, // Full brake
                         2000);
            
            // Klaxonner pour avertir
            vehicle.SoundHorn(1000);
        }

        private void ApplyCollisionAvoidance(Vehicle vehicle, Ped driver, List<CollisionPrediction> collisions)
        {
            var mostUrgent = collisions.First();
            var avoidanceDirection = CalculateCollisionAvoidanceDirection(vehicle, mostUrgent);
            
            if (avoidanceDirection != Vector3.Zero)
            {
                var avoidanceTarget = vehicle.Position + avoidanceDirection * 12f;
                
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                             driver.Handle,
                             vehicle.Handle,
                             avoidanceTarget.X, avoidanceTarget.Y, avoidanceTarget.Z,
                             Math.Max(vehicle.Speed * 0.8f, 15f),
                             1.0f,
                             vehicle.Model.Hash,
                             786468, // Mode d'évitement
                             7.5f,
                             -1);
            }
        }

        private void ApplyPedestrianAwareness(Vehicle vehicle, Ped driver, List<Ped> pedestrians)
        {
            // Ralentir et klaxonner légèrement
            var targetSpeed = Math.Max(vehicle.Speed * 0.6f, 8f);
            
            Function.Call(Hash.SET_DRIVE_TASK_CRUISE_SPEED,
                         driver.Handle,
                         targetSpeed);
            
            // Klaxon bref et poli
            vehicle.SoundHorn(500);
            
            // Activer la détection améliorée des piétons
            Function.Call(Hash.SET_PED_CONFIG_FLAG,
                         driver.Handle,
                         (int)PedConfigFlagToggles.SteersAroundPeds,
                         true);
        }

        private void ApplyIntersectionNavigation(Vehicle vehicle, Ped driver)
        {
            // Ralentir à l'approche de l'intersection
            var intersectionSpeed = Math.Min(vehicle.Speed, 20f);
            
            Function.Call(Hash.SET_DRIVE_TASK_CRUISE_SPEED,
                         driver.Handle,
                         intersectionSpeed);
            
            // Mode de conduite prudent
            Function.Call(Hash.SET_DRIVE_TASK_DRIVING_STYLE,
                         driver.Handle,
                         786603); // Normal, prudent
        }

        private void ExecuteSmartLaneChange(Vehicle vehicle, Ped driver, LaneChangeAnalysis analysis)
        {
            var changeDirection = analysis.PreferredDirection == LaneChangeDirection.Left ? 
                                -vehicle.RightVector : vehicle.RightVector;
            
            var targetLane = vehicle.Position + changeDirection * 4f + vehicle.ForwardVector * 20f;
            
            Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                         driver.Handle,
                         vehicle.Handle,
                         targetLane.X, targetLane.Y, targetLane.Z,
                         vehicle.Speed,
                         1.0f,
                         vehicle.Model.Hash,
                         786468, // Mode de changement de voie
                         5.0f,
                         -1);
        }

        private void ApplyRouteOptimization(Vehicle vehicle, Ped driver, List<Vector3> optimalRoute)
        {
            if (optimalRoute.Count > 0)
            {
                var nextWaypoint = optimalRoute[0];
                
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
                             driver.Handle,
                             vehicle.Handle,
                             nextWaypoint.X, nextWaypoint.Y, nextWaypoint.Z,
                             vehicle.Speed,
                             786603, // Mode normal optimisé
                             6.0f);
            }
        }

        private void ApplyAdvancedObstacleDetection(Vehicle vehicle, Ped driver, NavigationAnalysis analysis)
        {
            // Mémoriser les nouveaux obstacles détectés
            foreach (var obstacle in analysis.KnownObstaclesAhead)
            {
                if (!_knownObstacles.Contains(obstacle))
                {
                    _knownObstacles.Add(obstacle);
                    _obstacleTimestamps[obstacle] = DateTime.Now;
                }
            }
            
            // Améliorer la détection en temps réel
            Function.Call(Hash.SET_PED_CONFIG_FLAG,
                         driver.Handle,
                         (int)PedConfigFlagToggles.SteersAroundObjects,
                         true);
        }

        // Méthodes utilitaires

        private CollisionSeverity CalculateCollisionSeverity(Vehicle vehicle1, Vehicle vehicle2, float distance)
        {
            var relativeSpeed = (vehicle1.Velocity - vehicle2.Velocity).Length();
            
            if (relativeSpeed > 20f && distance < 3f) return CollisionSeverity.Severe;
            if (relativeSpeed > 10f && distance < 4f) return CollisionSeverity.Moderate;
            return CollisionSeverity.Minor;
        }

        private bool IsInVehiclePath(Vehicle vehicle, Vector3 position)
        {
            var toPosition = position - vehicle.Position;
            var dot = Vector3.Dot(toPosition.Normalized, vehicle.ForwardVector);
            return dot > 0.3f && toPosition.Length() < 25f;
        }

        private LaneSafetyAnalysis AnalyzeLaneSafety(Vector3 lanePosition, Vehicle vehicle)
        {
            var analysis = new LaneSafetyAnalysis();
            
            var nearbyVehicles = World.GetNearbyVehicles(lanePosition, LANE_CHANGE_SAFETY_DISTANCE);
            analysis.IsSafe = nearbyVehicles.Length < 2;
            
            if (analysis.IsSafe && nearbyVehicles.Length > 0)
            {
                var avgSpeedInLane = nearbyVehicles.Average(v => v.Speed);
                analysis.SpeedBenefit = avgSpeedInLane - vehicle.Speed;
            }
            
            return analysis;
        }

        private Vector3 AdjustForKnownObstacles(Vector3 originalTarget)
        {
            var adjustedTarget = originalTarget;
            
            foreach (var obstacle in _knownObstacles)
            {
                if (obstacle.DistanceTo(originalTarget) < 8f)
                {
                    // Déplacer le point cible pour éviter l'obstacle
                    var avoidanceVector = (originalTarget - obstacle).Normalized;
                    adjustedTarget = obstacle + avoidanceVector * 12f;
                }
            }
            
            return adjustedTarget;
        }

        private List<Vector3> GetVehiclePathPoints(Vehicle vehicle, float distance)
        {
            var points = new List<Vector3>();
            var direction = vehicle.ForwardVector;
            var currentPos = vehicle.Position;
            
            for (float d = 5f; d <= distance; d += 5f)
            {
                points.Add(currentPos + direction * d);
            }
            
            return points;
        }

        private Vector3 CalculateCollisionAvoidanceDirection(Vehicle vehicle, CollisionPrediction collision)
        {
            var toCollision = collision.CollisionPoint - vehicle.Position;
            var rightVector = vehicle.RightVector;
            
            // Choisir la direction d'évitement la plus sûre
            var leftOption = vehicle.Position - rightVector * 6f;
            var rightOption = vehicle.Position + rightVector * 6f;
            
            var leftClear = World.GetNearbyVehicles(leftOption, 8f).Length < 2;
            var rightClear = World.GetNearbyVehicles(rightOption, 8f).Length < 2;
            
            if (leftClear && !rightClear) return -rightVector;
            if (rightClear && !leftClear) return rightVector;
            if (leftClear && rightClear) return -rightVector; // Préférer la gauche
            
            return Vector3.Zero; // Aucune direction sûre
        }

        private void UpdateObstacleMemory()
        {
            var expiredObstacles = _obstacleTimestamps
                .Where(kvp => (DateTime.Now - kvp.Value).TotalSeconds > OBSTACLE_MEMORY_TIME)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var expired in expiredObstacles)
            {
                _knownObstacles.Remove(expired);
                _obstacleTimestamps.Remove(expired);
            }
        }

        private void CleanupNavigationInfo()
        {
            var cutoff = DateTime.Now.AddMinutes(-3);
            var staleEntries = _navigatingVehicles
                .Where(kvp => kvp.Value.LastUpdate < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in staleEntries)
            {
                _navigatingVehicles.Remove(key);
            }
        }
    }

    // Classes de support pour la navigation

    public class NavigationInfo
    {
        public Vehicle Vehicle { get; }
        public DateTime LastUpdate { get; set; }
        public List<Vector3> PlannedRoute { get; set; } = new();
        public int RouteOptimizations { get; set; }

        public NavigationInfo(Vehicle vehicle)
        {
            Vehicle = vehicle;
            LastUpdate = DateTime.Now;
        }
    }

    public class NavigationAnalysis
    {
        public List<CollisionPrediction> PotentialCollisions { get; set; } = new();
        public List<Ped> PedestriansNear { get; set; } = new();
        public bool IntersectionAhead { get; set; }
        public LaneChangeAnalysis LaneChangeOpportunity { get; set; } = new();
        public List<Vector3> OptimalRoute { get; set; } = new();
        public bool EmergencyBrakeNeeded { get; set; }
        public TerrainDifficulty TerrainDifficulty { get; set; }
        public List<Vector3> KnownObstaclesAhead { get; set; } = new();
    }

    public class CollisionPrediction
    {
        public float TimeToCollision { get; set; }
        public Vector3 CollisionPoint { get; set; }
        public Vehicle? OtherVehicle { get; set; }
        public CollisionSeverity Severity { get; set; }
    }

    public class LaneChangeAnalysis
    {
        public bool LeftLaneSafe { get; set; }
        public bool RightLaneSafe { get; set; }
        public float LeftLaneBenefit { get; set; }
        public float RightLaneBenefit { get; set; }
        public bool IsBeneficial { get; set; }
        public LaneChangeDirection PreferredDirection { get; set; }
    }

    public class LaneSafetyAnalysis
    {
        public bool IsSafe { get; set; }
        public float SpeedBenefit { get; set; }
    }

    public enum CollisionSeverity
    {
        Minor,
        Moderate,
        Severe
    }

    public enum TerrainDifficulty
    {
        Easy,
        Normal,
        Steep,
        Difficult
    }

    public enum LaneChangeDirection
    {
        Left,
        Right
    }
}