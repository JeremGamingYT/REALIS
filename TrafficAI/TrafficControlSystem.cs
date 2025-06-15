using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.UI;
using GTA.Native;
using REALIS.Core;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Système de contrôle de trafic (feux, stops, priorités)
    /// </summary>
    public class TrafficControlSystem : IDisposable
    {
        private readonly List<TrafficIntersection> _intersections = new List<TrafficIntersection>();
        private readonly List<StopSign> _stopSigns = new List<StopSign>();
        private readonly Random _random = new Random();
        
        private DateTime _lastTrafficUpdate = DateTime.MinValue;
        private const double TRAFFIC_UPDATE_INTERVAL = 2.0; // 2 secondes
        
        public void Initialize()
        {
            CreateTrafficIntersections();
            CreateStopSigns();
            Logger.Info("Traffic Control System initialized");
        }
        
        public void Update(Vector3 playerPosition)
        {
            try
            {
                if ((DateTime.Now - _lastTrafficUpdate).TotalSeconds < TRAFFIC_UPDATE_INTERVAL) return;
                
                _lastTrafficUpdate = DateTime.Now;
                
                UpdateTrafficLights();
                EnforceStopSigns(playerPosition);
                ManageIntersectionTraffic(playerPosition);
            }
            catch (Exception ex)
            {
                Logger.Error($"Traffic Control System update error: {ex.Message}");
            }
        }
        
        private void CreateTrafficIntersections()
        {
            // Intersections principales de Los Santos
            var intersectionLocations = new[]
            {
                new Vector3(-800, -200, 20),    // Centre-ville
                new Vector3(-600, -400, 25),
                new Vector3(-400, -600, 30),
                new Vector3(-1200, -800, 35),
                new Vector3(-200, -1000, 25),
                new Vector3(-1000, -400, 40),
                new Vector3(-500, -1500, 30)
            };
            
            foreach (var location in intersectionLocations)
            {
                var intersection = new TrafficIntersection
                {
                    Position = location,
                    LightState = TrafficLightState.Green,
                    LastStateChange = DateTime.Now,
                    GreenDuration = TimeSpan.FromSeconds(30 + _random.Next(30)), // 30-60s
                    RedDuration = TimeSpan.FromSeconds(25 + _random.Next(20))     // 25-45s
                };
                
                _intersections.Add(intersection);
            }
            
            Logger.Info($"Created {_intersections.Count} traffic intersections");
        }
        
        private void CreateStopSigns()
        {
            // Panneaux stop dans les zones résidentielles
            var stopLocations = new[]
            {
                new Vector3(-1500, -300, 30),
                new Vector3(-1600, -500, 32),
                new Vector3(-1400, -700, 28),
                new Vector3(-1300, -200, 35),
                new Vector3(-1700, -400, 30)
            };
            
            foreach (var location in stopLocations)
            {
                var stopSign = new StopSign
                {
                    Position = location,
                    Radius = 15f // Zone d'influence
                };
                
                _stopSigns.Add(stopSign);
            }
            
            Logger.Info($"Created {_stopSigns.Count} stop signs");
        }
        
        private void UpdateTrafficLights()
        {
            foreach (var intersection in _intersections)
            {
                var timeSinceChange = DateTime.Now - intersection.LastStateChange;
                bool shouldChange = false;
                
                if (intersection.LightState == TrafficLightState.Green && 
                    timeSinceChange >= intersection.GreenDuration)
                {
                    intersection.LightState = TrafficLightState.Yellow;
                    intersection.LastStateChange = DateTime.Now;
                    shouldChange = true;
                }
                else if (intersection.LightState == TrafficLightState.Yellow && 
                         timeSinceChange >= TimeSpan.FromSeconds(5)) // 5s de jaune
                {
                    intersection.LightState = TrafficLightState.Red;
                    intersection.LastStateChange = DateTime.Now;
                    shouldChange = true;
                }
                else if (intersection.LightState == TrafficLightState.Red && 
                         timeSinceChange >= intersection.RedDuration)
                {
                    intersection.LightState = TrafficLightState.Green;
                    intersection.LastStateChange = DateTime.Now;
                    // Randomiser les prochaines durées
                    intersection.GreenDuration = TimeSpan.FromSeconds(30 + _random.Next(30));
                    intersection.RedDuration = TimeSpan.FromSeconds(25 + _random.Next(20));
                    shouldChange = true;
                }
                
                if (shouldChange)
                {
                    ApplyTrafficLightToNearbyVehicles(intersection);
                }
            }
        }
        
        private void ApplyTrafficLightToNearbyVehicles(TrafficIntersection intersection)
        {
            var nearbyVehicles = World.GetNearbyVehicles(intersection.Position, 50f);
            
            foreach (var vehicle in nearbyVehicles)
            {
                if (vehicle?.Driver?.Exists() != true || vehicle.Driver.IsPlayer) continue;
                
                var distance = vehicle.Position.DistanceTo(intersection.Position);
                if (distance > 30f) continue; // Trop loin pour être affecté
                
                switch (intersection.LightState)
                {
                    case TrafficLightState.Red:
                        // Arrêter le véhicule
                        if (distance < 20f && vehicle.Speed > 2f)
                        {
                            vehicle.Driver.Task.CruiseWithVehicle(vehicle, 0f, (VehicleDrivingFlags)786603);
                        }
                        break;
                        
                    case TrafficLightState.Yellow:
                        // Ralentir ou s'arrêter selon la distance
                        if (distance < 15f)
                        {
                            vehicle.Driver.Task.CruiseWithVehicle(vehicle, 5f / 3.6f, (VehicleDrivingFlags)786603);
                        }
                        break;
                        
                    case TrafficLightState.Green:
                        // Reprendre la vitesse normale
                        if (vehicle.Speed < 5f)
                        {
                            var normalSpeed = IsResidentialArea(intersection.Position) ? 30f : 50f;
                            vehicle.Driver.Task.CruiseWithVehicle(vehicle, normalSpeed / 3.6f, (VehicleDrivingFlags)786603);
                        }
                        break;
                }
            }
        }
        
        private void EnforceStopSigns(Vector3 playerPosition)
        {
            foreach (var stopSign in _stopSigns)
            {
                if (playerPosition.DistanceTo(stopSign.Position) > 200f) continue;
                
                var nearbyVehicles = World.GetNearbyVehicles(stopSign.Position, stopSign.Radius);
                
                foreach (var vehicle in nearbyVehicles)
                {
                    if (vehicle?.Driver?.Exists() != true) continue;
                    
                    var distance = vehicle.Position.DistanceTo(stopSign.Position);
                    
                    if (vehicle.Driver.IsPlayer)
                    {
                        // Vérifier si le joueur respecte le stop
                        CheckPlayerStopCompliance(vehicle, stopSign, distance);
                    }
                    else
                    {
                        // Faire respecter le stop par les NPCs
                        ApplyStopSignBehavior(vehicle, stopSign, distance);
                    }
                }
            }
        }
        
        private void CheckPlayerStopCompliance(Vehicle vehicle, StopSign stopSign, float distance)
        {
            if (distance < 8f && vehicle.Speed > 3f) // 3 m/s = ~10 km/h
            {
                // Le joueur ne s'arrête pas au stop
                if (!stopSign.PlayerViolationReported)
                {
                    Screen.ShowSubtitle("~r~STOP NON RESPECTÉ~w~\nVous devez vous arrêter!", 4000);
                    stopSign.PlayerViolationReported = true;
                    stopSign.LastViolation = DateTime.Now;
                    
                    // Chance d'avoir une amende
                    if (_random.Next(100) < 30) // 30% de chance
                    {
                        Game.Player.Money -= 150; // Amende de 150$
                        Notification.PostTicker("~r~AMENDE~w~ - Non-respect d'un stop: -150$", false);
                    }
                }
            }
            else if (distance < 5f && vehicle.Speed < 1f)
            {
                // Le joueur s'arrête correctement
                if (stopSign.PlayerViolationReported && 
                    (DateTime.Now - stopSign.LastViolation).TotalSeconds > 2)
                {
                    stopSign.PlayerViolationReported = false;
                }
            }
        }
        
        private void ApplyStopSignBehavior(Vehicle vehicle, StopSign stopSign, float distance)
        {
            if (distance < 10f && vehicle.Speed > 1f)
            {
                // Ralentir en approchant du stop
                vehicle.Driver.Task.CruiseWithVehicle(vehicle, 5f / 3.6f, (VehicleDrivingFlags)786603);
            }
            else if (distance < 5f)
            {
                // S'arrêter complètement
                vehicle.Driver.Task.CruiseWithVehicle(vehicle, 0f, (VehicleDrivingFlags)786603);
                
                // Marquer le temps d'arrêt
                if (!stopSign.NPCStoppedVehicles.ContainsKey(vehicle.Handle))
                {
                    stopSign.NPCStoppedVehicles[vehicle.Handle] = DateTime.Now;
                }
                else
                {
                    var stopTime = DateTime.Now - stopSign.NPCStoppedVehicles[vehicle.Handle];
                    if (stopTime.TotalSeconds >= 2) // Arrêt de 2 secondes minimum
                    {
                        // Reprendre la route
                        vehicle.Driver.Task.CruiseWithVehicle(vehicle, 30f / 3.6f, (VehicleDrivingFlags)786603);
                        stopSign.NPCStoppedVehicles.Remove(vehicle.Handle);
                    }
                }
            }
        }
        
        private void ManageIntersectionTraffic(Vector3 playerPosition)
        {
            foreach (var intersection in _intersections)
            {
                if (playerPosition.DistanceTo(intersection.Position) > 150f) continue;
                
                var nearbyVehicles = World.GetNearbyVehicles(intersection.Position, 40f);
                
                // Gérer les priorités et les conflits
                foreach (var vehicle in nearbyVehicles)
                {
                    if (vehicle?.Driver?.Exists() != true || vehicle.Driver.IsPlayer) continue;
                    
                    ManageIntersectionPriority(vehicle, intersection);
                }
            }
        }
        
        private void ManageIntersectionPriority(Vehicle vehicle, TrafficIntersection intersection)
        {
            var distance = vehicle.Position.DistanceTo(intersection.Position);
            
            if (distance < 25f)
            {
                // Vérifier s'il y a d'autres véhicules avec priorité
                var conflictingVehicles = World.GetNearbyVehicles(intersection.Position, 30f)
                    .Where(v => v != vehicle && v?.Driver?.Exists() == true && !v.Driver.IsPlayer)
                    .Where(v => HasIntersectionPriority(v, vehicle, intersection))
                    .ToArray();
                
                if (conflictingVehicles.Any())
                {
                    // Céder le passage
                    vehicle.Driver.Task.CruiseWithVehicle(vehicle, 2f / 3.6f, (VehicleDrivingFlags)786603);
                }
                else if (intersection.LightState == TrafficLightState.Green || 
                         intersection.LightState == TrafficLightState.Yellow)
                {
                    // Passage libre
                    var speed = IsResidentialArea(intersection.Position) ? 25f : 40f;
                    vehicle.Driver.Task.CruiseWithVehicle(vehicle, speed / 3.6f, (VehicleDrivingFlags)786603);
                }
            }
        }
        
        private bool HasIntersectionPriority(Vehicle otherVehicle, Vehicle currentVehicle, TrafficIntersection intersection)
        {
            var otherDistance = otherVehicle.Position.DistanceTo(intersection.Position);
            var currentDistance = currentVehicle.Position.DistanceTo(intersection.Position);
            
            // Le véhicule le plus proche a la priorité (simplification)
            return otherDistance < currentDistance;
        }
        
        private bool IsResidentialArea(Vector3 position)
        {
            // Zone résidentielle simplifiée
            return position.DistanceTo(new Vector3(-1500, -500, 30)) < 400f;
        }
        
        public TrafficLightState GetTrafficLightState(Vector3 position)
        {
            var nearestIntersection = _intersections
                .Where(i => i.Position.DistanceTo(position) < 50f)
                .OrderBy(i => i.Position.DistanceTo(position))
                .FirstOrDefault();
                
            return nearestIntersection?.LightState ?? TrafficLightState.Green;
        }
        
        public void Dispose()
        {
            _intersections.Clear();
            _stopSigns.Clear();
        }
    }
    
    /// <summary>
    /// Intersection avec feux de circulation
    /// </summary>
    public class TrafficIntersection
    {
        public Vector3 Position { get; set; }
        public TrafficLightState LightState { get; set; }
        public DateTime LastStateChange { get; set; }
        public TimeSpan GreenDuration { get; set; }
        public TimeSpan RedDuration { get; set; }
    }
    
    /// <summary>
    /// Panneau stop
    /// </summary>
    public class StopSign
    {
        public Vector3 Position { get; set; }
        public float Radius { get; set; }
        public bool PlayerViolationReported { get; set; }
        public DateTime LastViolation { get; set; }
        public Dictionary<int, DateTime> NPCStoppedVehicles { get; set; } = new Dictionary<int, DateTime>();
    }
    
    // TrafficLightState enum supprimé car défini dans TrafficLightManager.cs
} 