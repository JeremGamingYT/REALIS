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
    /// TEMPORAIREMENT DÉSACTIVÉ - Test de crash
    /// Gestionnaire avancé des feux de circulation et intersections.
    /// Assure que les NPCs respectent les feux rouges et gèrent correctement les priorités.
    /// </summary>
    public class TrafficLightManager_DISABLED : Script
    {
        private readonly Dictionary<Vector3, TrafficLightData> _knownTrafficLights = new();
        private readonly Dictionary<int, IntersectionBehavior> _vehicleBehaviors = new();
        private readonly HashSet<Vector3> _activeIntersections = new();
        
        // Configuration des feux de circulation
        private const float TRAFFIC_LIGHT_SCAN_RADIUS = 100f;
        private const float INTERSECTION_DETECTION_RADIUS = 20f;
        private const float STOP_LINE_DISTANCE = 5f;
        private const float SAFE_FOLLOWING_DISTANCE = 8f;
        private const float RED_LIGHT_BRAKE_DISTANCE = 25f;
        private const float YELLOW_LIGHT_DECISION_DISTANCE = 15f;
        private const int MAX_VEHICLES_PER_TICK = 10;
        
        // Timing des feux (approximation)
        private const float RED_LIGHT_DURATION = 30f;
        private const float YELLOW_LIGHT_DURATION = 5f;
        private const float GREEN_LIGHT_DURATION = 25f;
        
        private DateTime _lastIntersectionScan = DateTime.MinValue;

        private DateTime _initializationTime = DateTime.Now;
        private bool _isInitialized = false;
        
        public TrafficLightManager_DISABLED()
        {
            // TEMPORAIREMENT DÉSACTIVÉ POUR TEST
            // Tick += OnTick;
            // Interval = 10000;
            Logger.Info("TrafficLightManager DISABLED for crash testing");
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                // Délai d'initialisation très long
                if (!_isInitialized)
                {
                    if ((DateTime.Now - _initializationTime).TotalSeconds < 90) return;
                    _isInitialized = true;
                    Logger.Info("TrafficLightManager initialized after 90s delay");
                    return;
                }
                
                if (!ShouldProcessTrafficLights() || EmergencyDisable.IsAIDisabled) return;
                
                var player = Game.Player.Character;
                if (player?.Exists() != true) return;
                
                // Traitement très limité
                try
                {
                    UpdateTrafficLightData(player.Position);
                    ProcessVehiclesAtIntersections();
                }
                catch
                {
                    // Ignore les erreurs de traitement
                }
                
                CleanupStaleData();
                
                _lastIntersectionScan = DateTime.Now;
            }
            catch (Exception ex)
            {
                Logger.Error($"TrafficLightManager error: {ex.Message}");
            }
        }

        private bool ShouldProcessTrafficLights()
        {
            return (DateTime.Now - _lastIntersectionScan).TotalSeconds > 2f;
        }

        private void UpdateTrafficLightData(Vector3 playerPosition)
        {
            try
            {
                // Scanner les intersections dans la zone
                var intersections = DetectNearbyIntersections(playerPosition);
                
                foreach (var intersection in intersections)
                {
                    UpdateIntersectionState(intersection);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Traffic light data update error: {ex.Message}");
            }
        }

        private void ProcessVehiclesAtIntersections()
        {
            var processedCount = 0;
            var player = Game.Player.Character;
            
            var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(player.Position, TRAFFIC_LIGHT_SCAN_RADIUS)
                .Where(v => v.Driver != null && !v.Driver.IsPlayer && v.Driver.IsAlive)
                .OrderBy(v => v.Position.DistanceTo(player.Position))
                .Take(MAX_VEHICLES_PER_TICK);
            
            foreach (var vehicle in nearbyVehicles)
            {
                if (processedCount >= MAX_VEHICLES_PER_TICK) break;
                
                try
                {
                    if (VehicleQueryService.TryAcquireControl(vehicle))
                    {
                        ProcessVehicleTrafficLightBehavior(vehicle);
                        processedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Vehicle traffic light processing error: {ex.Message}");
                }
                finally
                {
                    VehicleQueryService.ReleaseControl(vehicle);
                }
            }
        }

        private List<Vector3> DetectNearbyIntersections(Vector3 position)
        {
            var intersections = new List<Vector3>();
            
            // Détecter les intersections en analysant la densité de véhicules avec différentes directions
            for (float x = -50f; x <= 50f; x += 25f)
            {
                for (float y = -50f; y <= 50f; y += 25f)
                {
                    var checkPos = position + new Vector3(x, y, 0);
                    
                    if (IsLikelyIntersection(checkPos))
                    {
                        intersections.Add(checkPos);
                        _activeIntersections.Add(checkPos);
                    }
                }
            }
            
            return intersections;
        }

        private bool IsLikelyIntersection(Vector3 position)
        {
            var nearbyVehicles = World.GetNearbyVehicles(position, INTERSECTION_DETECTION_RADIUS);
            
            if (nearbyVehicles.Length < 3) return false;
            
            // Analyser les directions des véhicules
            var directions = new List<Vector3>();
            foreach (var vehicle in nearbyVehicles)
            {
                if (vehicle.Speed > 1f)
                {
                    directions.Add(vehicle.ForwardVector);
                }
            }
            
            // Vérifier s'il y a des véhicules venant de directions différentes
            int differentDirections = 0;
            foreach (var dir1 in directions)
            {
                bool hasDifferent = directions.Any(dir2 => 
                    Math.Abs(Vector3.Dot(dir1, dir2)) < 0.3f);
                
                if (hasDifferent) differentDirections++;
            }
            
            return differentDirections >= 2;
        }

        private void UpdateIntersectionState(Vector3 intersection)
        {
            if (!_knownTrafficLights.ContainsKey(intersection))
            {
                _knownTrafficLights[intersection] = new TrafficLightData(intersection);
            }
            
            var lightData = _knownTrafficLights[intersection];
            lightData.UpdateState();
        }

        private void ProcessVehicleTrafficLightBehavior(Vehicle vehicle)
        {
            var behavior = GetOrCreateVehicleBehavior(vehicle);
            var nearestIntersection = FindNearestIntersection(vehicle.Position);
            
            if (nearestIntersection.HasValue)
            {
                var intersection = nearestIntersection.Value;
                _knownTrafficLights.TryGetValue(intersection, out var lightData);
                
                if (lightData != null)
                {
                    ApplyTrafficLightBehavior(vehicle, lightData, behavior);
                }
            }
            
            // Gestion générale des intersections sans feux
            HandleIntersectionPriority(vehicle, behavior);
        }

        private IntersectionBehavior GetOrCreateVehicleBehavior(Vehicle vehicle)
        {
            if (!_vehicleBehaviors.TryGetValue(vehicle.Handle, out var behavior))
            {
                behavior = new IntersectionBehavior(vehicle);
                _vehicleBehaviors[vehicle.Handle] = behavior;
            }
            
            behavior.LastUpdate = DateTime.Now;
            return behavior;
        }

        private Vector3? FindNearestIntersection(Vector3 position)
        {
            var nearest = _activeIntersections
                .Where(i => i.DistanceTo(position) < 50f)
                .OrderBy(i => i.DistanceTo(position))
                .FirstOrDefault();
            
            return nearest == Vector3.Zero ? null : nearest;
        }

        private void ApplyTrafficLightBehavior(Vehicle vehicle, TrafficLightData lightData, IntersectionBehavior behavior)
        {
            var driver = vehicle.Driver;
            var distanceToLight = vehicle.Position.DistanceTo(lightData.Position);
            var approachingLight = IsApproachingTrafficLight(vehicle, lightData.Position);
            
            if (!approachingLight || distanceToLight > RED_LIGHT_BRAKE_DISTANCE) return;
            
            switch (lightData.CurrentState)
            {
                case TrafficLightState.Red:
                    HandleRedLight(vehicle, driver, lightData, distanceToLight);
                    break;
                    
                case TrafficLightState.Yellow:
                    HandleYellowLight(vehicle, driver, lightData, distanceToLight);
                    break;
                    
                case TrafficLightState.Green:
                    HandleGreenLight(vehicle, driver, lightData, distanceToLight);
                    break;
            }
        }

        private void HandleRedLight(Vehicle vehicle, Ped driver, TrafficLightData lightData, float distance)
        {
            // S'arrêter au feu rouge
            if (distance > STOP_LINE_DISTANCE && vehicle.Speed > 2f)
            {
                // Ralentir graduellement
                var stopPosition = lightData.Position - vehicle.ForwardVector * STOP_LINE_DISTANCE;
                
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                             driver.Handle,
                             vehicle.Handle,
                             stopPosition.X, stopPosition.Y, stopPosition.Z,
                             0f, // S'arrêter complètement
                             1.0f,
                             vehicle.Model.Hash,
                             786603, // Mode de conduite normal
                             STOP_LINE_DISTANCE,
                             -1);
                
                // Assurer un arrêt complet
                if (distance < STOP_LINE_DISTANCE + 3f)
                {
                    Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION,
                                 driver.Handle,
                                 vehicle.Handle,
                                 6, // Brake
                                 5000);
                }
            }
        }

        private void HandleYellowLight(Vehicle vehicle, Ped driver, TrafficLightData lightData, float distance)
        {
            // Décision intelligente pour le feu orange
            var canStopSafely = distance > YELLOW_LIGHT_DECISION_DISTANCE && vehicle.Speed < 40f;
            var willClearIntersection = distance < 8f && vehicle.Speed > 15f;
            
            if (canStopSafely && !willClearIntersection)
            {
                // S'arrêter en sécurité
                HandleRedLight(vehicle, driver, lightData, distance);
            }
            else
            {
                // Continuer mais avec prudence
                var safeSpeed = Math.Max(vehicle.Speed * 0.8f, 20f);
                Function.Call(Hash.SET_DRIVE_TASK_CRUISE_SPEED,
                             driver.Handle,
                             safeSpeed);
            }
        }

        private void HandleGreenLight(Vehicle vehicle, Ped driver, TrafficLightData lightData, float distance)
        {
            // Reprendre la conduite normale au feu vert
            if (vehicle.Speed < 5f && distance < 15f)
            {
                // Redémarrer après un arrêt
                var targetPosition = vehicle.Position + vehicle.ForwardVector * 30f;
                
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                             driver.Handle,
                             vehicle.Handle,
                             targetPosition.X, targetPosition.Y, targetPosition.Z,
                             35f, // Vitesse normale
                             1.0f,
                             vehicle.Model.Hash,
                             786603,
                             10f,
                             -1);
            }
            
            // Vérifier qu'il n'y a pas d'embouteillage avant de continuer
            if (!IsIntersectionClear(lightData.Position, vehicle))
            {
                Function.Call(Hash.SET_DRIVE_TASK_CRUISE_SPEED,
                             driver.Handle,
                             10f); // Vitesse réduite
            }
        }

        private void HandleIntersectionPriority(Vehicle vehicle, IntersectionBehavior behavior)
        {
            var driver = vehicle.Driver;
            var nearIntersection = _activeIntersections
                .FirstOrDefault(i => i.DistanceTo(vehicle.Position) < INTERSECTION_DETECTION_RADIUS);
            
            if (nearIntersection != Vector3.Zero)
            {
                // Appliquer les règles de priorité
                var conflictVehicles = DetectConflictingVehicles(vehicle, nearIntersection);
                
                if (conflictVehicles.Count > 0)
                {
                    var shouldYield = ShouldYieldToOtherVehicles(vehicle, conflictVehicles);
                    
                    if (shouldYield)
                    {
                        // Céder le passage
                        Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION,
                                     driver.Handle,
                                     vehicle.Handle,
                                     6, // Brake
                                     2000);
                        
                        behavior.LastYieldTime = DateTime.Now;
                    }
                    else
                    {
                        // Continuer mais avec prudence
                        Function.Call(Hash.SET_DRIVE_TASK_CRUISE_SPEED,
                                     driver.Handle,
                                     15f); // Vitesse réduite en intersection
                    }
                }
            }
        }

        private bool IsApproachingTrafficLight(Vehicle vehicle, Vector3 lightPosition)
        {
            var toLight = lightPosition - vehicle.Position;
            var dot = Vector3.Dot(toLight.Normalized, vehicle.ForwardVector);
            return dot > 0.7f && toLight.Length() < RED_LIGHT_BRAKE_DISTANCE;
        }

        private bool IsIntersectionClear(Vector3 intersection, Vehicle excludeVehicle)
        {
            var vehiclesInIntersection = World.GetNearbyVehicles(intersection, 12f)
                .Where(v => v != excludeVehicle && v.Speed < 5f)
                .Count();
            
            return vehiclesInIntersection < 2;
        }

        private List<Vehicle> DetectConflictingVehicles(Vehicle vehicle, Vector3 intersection)
        {
            var conflicting = new List<Vehicle>();
            var nearbyVehicles = World.GetNearbyVehicles(intersection, INTERSECTION_DETECTION_RADIUS);
            
            foreach (var other in nearbyVehicles)
            {
                if (other == vehicle || other.Driver?.IsPlayer == true) continue;
                
                // Vérifier si les trajectoires se croisent
                if (WillTrajectorisCross(vehicle, other, intersection))
                {
                    conflicting.Add(other);
                }
            }
            
            return conflicting;
        }

        private bool ShouldYieldToOtherVehicles(Vehicle vehicle, List<Vehicle> conflictVehicles)
        {
            foreach (var other in conflictVehicles)
            {
                // Règles de priorité simples
                var myAngle = CalculateApproachAngle(vehicle, other.Position);
                var otherAngle = CalculateApproachAngle(other, vehicle.Position);
                
                // Priorité à droite (règle européenne)
                if (IsComingFromRight(vehicle, other))
                {
                    return true;
                }
                
                // Si l'autre véhicule est déjà engagé dans l'intersection
                if (other.Speed > 10f && vehicle.Speed < 5f)
                {
                    return true;
                }
            }
            
            return false;
        }

        private bool WillTrajectorisCross(Vehicle vehicle1, Vehicle vehicle2, Vector3 intersection)
        {
            var direction1 = vehicle1.ForwardVector;
            var direction2 = vehicle2.ForwardVector;
            
            // Calculer si les trajectoires se croisent dans l'intersection
            var dot = Math.Abs(Vector3.Dot(direction1, direction2));
            return dot < 0.5f; // Angles différents = trajectoires qui se croisent
        }

        private float CalculateApproachAngle(Vehicle vehicle, Vector3 referencePoint)
        {
            var toReference = referencePoint - vehicle.Position;
            var angle = Math.Atan2(toReference.Y, toReference.X);
            return (float)(angle * 180.0 / Math.PI);
        }

        private bool IsComingFromRight(Vehicle vehicle, Vehicle other)
        {
            var toOther = other.Position - vehicle.Position;
            var dot = Vector3.Dot(toOther.Normalized, vehicle.RightVector);
            return dot > 0.3f; // L'autre véhicule vient de la droite
        }

        private void CleanupStaleData()
        {
            var cutoff = DateTime.Now.AddMinutes(-5);
            
            // Nettoyer les comportements de véhicules
            var staleBehaviors = _vehicleBehaviors
                .Where(kvp => kvp.Value.LastUpdate < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in staleBehaviors)
            {
                _vehicleBehaviors.Remove(key);
            }
            
            // Nettoyer les intersections inactives
            var player = Game.Player.Character;
            if (player?.Exists() == true)
            {
                var farIntersections = _activeIntersections
                    .Where(i => i.DistanceTo(player.Position) > TRAFFIC_LIGHT_SCAN_RADIUS * 2)
                    .ToList();
                
                foreach (var intersection in farIntersections)
                {
                    _activeIntersections.Remove(intersection);
                    _knownTrafficLights.Remove(intersection);
                }
            }
        }
    }

    // Classes de support pour les feux de circulation

    public class TrafficLightData
    {
        public Vector3 Position { get; }
        public TrafficLightState CurrentState { get; private set; }
        public DateTime LastStateChange { get; private set; }
        public float StateDuration { get; private set; }

        public TrafficLightData(Vector3 position)
        {
            Position = position;
            CurrentState = TrafficLightState.Green; // État initial
            LastStateChange = DateTime.Now;
            StateDuration = 0f;
        }

        public void UpdateState()
        {
            var elapsed = (DateTime.Now - LastStateChange).TotalSeconds;
            
            // Simuler le cycle des feux de circulation
            switch (CurrentState)
            {
                case TrafficLightState.Green when elapsed > 25f:
                    ChangeState(TrafficLightState.Yellow);
                    break;
                    
                case TrafficLightState.Yellow when elapsed > 5f:
                    ChangeState(TrafficLightState.Red);
                    break;
                    
                case TrafficLightState.Red when elapsed > 30f:
                    ChangeState(TrafficLightState.Green);
                    break;
            }
            
            StateDuration = (float)elapsed;
        }

        private void ChangeState(TrafficLightState newState)
        {
            CurrentState = newState;
            LastStateChange = DateTime.Now;
            StateDuration = 0f;
        }
    }

    public class IntersectionBehavior
    {
        public Vehicle Vehicle { get; }
        public DateTime LastUpdate { get; set; }
        public DateTime LastYieldTime { get; set; }
        public bool HasStoppedAtLight { get; set; }
        public Vector3? LastKnownLightPosition { get; set; }

        public IntersectionBehavior(Vehicle vehicle)
        {
            Vehicle = vehicle;
            LastUpdate = DateTime.Now;
            LastYieldTime = DateTime.MinValue;
        }
    }

    public enum TrafficLightState
    {
        Green,
        Yellow,
        Red
    }
}