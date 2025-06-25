using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Common;
using System.Drawing;

namespace REALIS.Traffic
{
    /// <summary>
    /// Module de gestion du trafic intelligent avec événements dynamiques
    /// </summary>
    public class SmartTrafficModule : IModule
    {
        private readonly List<TrafficEvent> _activeEvents = new List<TrafficEvent>();
        private readonly List<SpecializedZone> _zones = new List<SpecializedZone>();
        private readonly Random _rng = new Random();
        private DateTime _lastEventTime = DateTime.MinValue;
        private DateTime _lastZoneUpdate = DateTime.MinValue;
        private readonly TimeSpan _eventCooldown = TimeSpan.FromMinutes(3);
        private readonly TimeSpan _zoneUpdateInterval = TimeSpan.FromMinutes(1);

        // Données de suivi des véhicules pour comportements avancés
        private readonly Dictionary<Vehicle, VehicleTrafficData> _vehicleData = new Dictionary<Vehicle, VehicleTrafficData>();

        public void Initialize()
        {
            InitializeSpecializedZones();
            // Module initialisé - Smart Traffic
        }

        public void Update()
        {
            try
            {
                var player = Game.Player.Character;
                if (player == null || !player.Exists()) return;

                ManageTrafficEvents();
                UpdateSpecializedZones();
                EnhanceNearbyVehicleBehavior(player);
                CleanupExpiredData();
            }
            catch (Exception ex)
            {
                // Erreur dans le module Traffic : " + ex.Message
            }
        }

        public void Dispose()
        {
            // Nettoyer les événements actifs
            foreach (var trafficEvent in _activeEvents.ToList())
            {
                CleanupTrafficEvent(trafficEvent);
            }
            _activeEvents.Clear();

            // Nettoyer les zones
            foreach (var zone in _zones)
            {
                zone.Dispose();
            }
            _zones.Clear();

            GTA.UI.Screen.ShowSubtitle("[REALIS Traffic] Module fermé", 3000);
        }

        #region Zone Management

        private void InitializeSpecializedZones()
        {
            // Zone industrielle - Port de Los Santos
            _zones.Add(new SpecializedZone
            {
                Name = "Zone Industrielle",
                Center = new Vector3(1011f, -2905f, 6f),
                Radius = 200f,
                Type = ZoneType.Industrial,
                ActiveHours = new TimeRange(6, 18),
                VehicleTypes = new[] 
                { 
                    VehicleHash.Hauler, VehicleHash.Phantom, VehicleHash.Packer,
                    VehicleHash.Benson, VehicleHash.Mule 
                },
                MaxVehicles = 8,
                ActivityLevel = 0.8f
            });

            // Zone résidentielle - Vinewood Hills
            _zones.Add(new SpecializedZone
            {
                Name = "Zone Résidentielle",
                Center = new Vector3(126f, 550f, 184f),
                Radius = 300f,
                Type = ZoneType.Residential,
                ActiveHours = new TimeRange(7, 22),
                VehicleTypes = new[] 
                { 
                    VehicleHash.Asea, VehicleHash.Asterope, VehicleHash.Emperor,
                    VehicleHash.Ingot, VehicleHash.Premier 
                },
                MaxVehicles = 12,
                ActivityLevel = 0.6f
            });

            // Zone commerciale - Downtown
            _zones.Add(new SpecializedZone
            {
                Name = "Zone Commerciale",
                Center = new Vector3(240f, -880f, 30f),
                Radius = 250f,
                Type = ZoneType.Commercial,
                ActiveHours = new TimeRange(8, 20),
                VehicleTypes = new[] 
                { 
                    VehicleHash.Taxi, VehicleHash.Bus, VehicleHash.Coach,
                    VehicleHash.Stretch, VehicleHash.Surge 
                },
                MaxVehicles = 15,
                ActivityLevel = 0.9f
            });

            // Zone aéroport
            _zones.Add(new SpecializedZone
            {
                Name = "Aéroport",
                Center = new Vector3(-1037f, -2737f, 14f),
                Radius = 400f,
                Type = ZoneType.Airport,
                ActiveHours = new TimeRange(0, 23),
                VehicleTypes = new[] 
                { 
                    VehicleHash.Bus, VehicleHash.Taxi, VehicleHash.Stretch,
                    VehicleHash.Airbus, VehicleHash.RentalBus 
                },
                MaxVehicles = 10,
                ActivityLevel = 0.7f
            });
        }

        #endregion

        #region Traffic Events

        private void ManageTrafficEvents()
        {
            // Nettoyer les événements expirés
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                var trafficEvent = _activeEvents[i];
                if (DateTime.Now - trafficEvent.StartTime > trafficEvent.Duration)
                {
                    CleanupTrafficEvent(trafficEvent);
                    _activeEvents.RemoveAt(i);
                }
                else
                {
                    UpdateTrafficEvent(trafficEvent);
                }
            }

            // Générer de nouveaux événements
            var player = Game.Player.Character;
            if (DateTime.Now - _lastEventTime > _eventCooldown && 
                _activeEvents.Count < 3 && 
                _rng.Next(100) < 20)
            {
                GenerateTrafficEvent();
                _lastEventTime = DateTime.Now;
            }
        }

        private void GenerateTrafficEvent()
        {
            var player = Game.Player.Character;
            var eventTypes = Enum.GetValues(typeof(TrafficEventType)).Cast<TrafficEventType>().ToArray();
            var eventType = eventTypes[_rng.Next(eventTypes.Length)];
            
            var eventPosition = GetRandomNearbyPosition(player.Position, 100f, 500f);
            
            var trafficEvent = new TrafficEvent
            {
                Id = Guid.NewGuid(),
                Type = eventType,
                Position = eventPosition,
                StartTime = DateTime.Now,
                Duration = GetEventDuration(eventType),
                AffectedLanes = _rng.Next(1, 3)
            };

            CreateTrafficEventScene(trafficEvent);
            _activeEvents.Add(trafficEvent);

            // Notification et waypoint
            GTA.UI.Screen.ShowSubtitle($"[Traffic] {GetEventDescription(eventType)} signalé", 3000);
        }

        private void CreateTrafficEventScene(TrafficEvent trafficEvent)
        {
            switch (trafficEvent.Type)
            {
                case TrafficEventType.Breakdown:
                    CreateVehicleBreakdown(trafficEvent, trafficEvent.Position);
                    break;
                case TrafficEventType.Delivery:
                    CreateDeliveryScene(trafficEvent, trafficEvent.Position);
                    break;
                case TrafficEventType.Construction:
                    CreateConstructionSite(trafficEvent, trafficEvent.Position);
                    break;
                case TrafficEventType.PoliceStop:
                    CreatePoliceStop(trafficEvent, trafficEvent.Position);
                    break;
                case TrafficEventType.ConvoyEscort:
                    CreateConvoyEscort(trafficEvent, trafficEvent.Position);
                    break;
            }

            // Créer blip
            trafficEvent.Blip = World.CreateBlip(trafficEvent.Position);
            trafficEvent.Blip.Sprite = GetEventBlipSprite(trafficEvent.Type);
            trafficEvent.Blip.Color = BlipColor.Orange;
            trafficEvent.Blip.Scale = 0.8f;
            trafficEvent.Blip.Name = GetEventDescription(trafficEvent.Type);
        }

        private void CreateVehicleBreakdown(TrafficEvent trafficEvent, Vector3 position)
        {
            // Créer véhicule en panne
            var model = new Model(VehicleHash.Asea);
            if (model.IsValid && model.Request(5000))
            {
                var vehicle = World.CreateVehicle(model, position, 0f);
                if (vehicle != null)
                {
                    vehicle.EngineHealth = 0f;
                    vehicle.Health = (int)(vehicle.MaxHealth * 0.3f);
                    vehicle.IsEngineRunning = false;
                    
                    // Créer conducteur frustré
                    var pedModel = new Model(PedHash.Business01AMY);
                    if (pedModel.IsValid && pedModel.Request(5000))
                    {
                        var ped = World.CreatePed(pedModel, position + Vector3.RandomXY() * 3f, 0f);
                        if (ped != null)
                        {
                            ped.Task.StandStill(10000);
                            trafficEvent.InvolvedPeds.Add(ped);
                        }
                    }
                    
                    trafficEvent.InvolvedVehicles.Add(vehicle);
                }
            }
        }

        private void CreateDeliveryScene(TrafficEvent trafficEvent, Vector3 position)
        {
            // Créer véhicule de livraison
            var model = new Model(VehicleHash.Mule);
            if (model.IsValid && model.Request(5000))
            {
                var vehicle = World.CreateVehicle(model, position, 0f);
                if (vehicle != null)
                {
                    vehicle.IsEngineRunning = true;
                    vehicle.FuelLevel = 100f;
                    
                    // Créer livreur
                    var pedModel = new Model(PedHash.Trucker01SMM);
                    if (pedModel.IsValid && pedModel.Request(5000))
                    {
                        var driver = vehicle.CreatePedOnSeat(VehicleSeat.Driver, pedModel);
                        if (driver != null)
                        {
                            trafficEvent.InvolvedPeds.Add(driver);
                        }
                    }
                    
                    trafficEvent.InvolvedVehicles.Add(vehicle);
                }
            }
        }

        private void CreateConstructionSite(TrafficEvent trafficEvent, Vector3 position)
        {
            // Créer véhicule de travaux
            var model = new Model(VehicleHash.Mule);
            if (model.IsValid && model.Request(5000))
            {
                var vehicle = World.CreateVehicle(model, position, 0f);
                if (vehicle != null)
                {
                    vehicle.IsEngineRunning = true;
                    
                    // Créer ouvriers
                    for (int i = 0; i < 2; i++)
                    {
                        var pedModel = new Model(PedHash.Business01AMY);
                        if (pedModel.IsValid && pedModel.Request(5000))
                        {
                            var worker = World.CreatePed(pedModel, position + Vector3.RandomXY() * 8f, 0f);
                            if (worker != null)
                            {
                                worker.Task.StartScenario("WORLD_HUMAN_HAMMERING", 0);
                                trafficEvent.InvolvedPeds.Add(worker);
                            }
                        }
                    }
                    
                    trafficEvent.InvolvedVehicles.Add(vehicle);
                    CreateConstructionProps(position);
                }
            }
        }

        private void CreatePoliceStop(TrafficEvent trafficEvent, Vector3 position)
        {
            // Créer véhicule de police
            var policeModel = new Model(VehicleHash.Police);
            if (policeModel.IsValid && policeModel.Request(5000))
            {
                var policeVehicle = World.CreateVehicle(policeModel, position, 0f);
                if (policeVehicle != null)
                {
                    policeVehicle.IsSirenActive = true;
                    
                    // Créer véhicule arrêté
                    var civilModel = new Model(VehicleHash.Buffalo);
                    if (civilModel.IsValid && civilModel.Request(5000))
                    {
                        var civilVehicle = World.CreateVehicle(civilModel, position + Vector3.RelativeFront * 10f, 0f);
                        if (civilVehicle != null)
                        {
                            trafficEvent.InvolvedVehicles.Add(civilVehicle);
                        }
                    }
                    
                    trafficEvent.InvolvedVehicles.Add(policeVehicle);
                }
            }
        }

        private void CreateConvoyEscort(TrafficEvent trafficEvent, Vector3 position)
        {
            // Créer convoi avec escorte
            var vehicles = new VehicleHash[] { VehicleHash.Police, VehicleHash.Stockade, VehicleHash.Police };
            
            for (int i = 0; i < vehicles.Length; i++)
            {
                var model = new Model(vehicles[i]);
                if (model.IsValid && model.Request(5000))
                {
                    var vehicle = World.CreateVehicle(model, position + Vector3.RelativeFront * (i * 15f), 0f);
                    if (vehicle != null)
                    {
                        if (vehicles[i] == VehicleHash.Police)
                        {
                            vehicle.IsSirenActive = true;
                        }
                        trafficEvent.InvolvedVehicles.Add(vehicle);
                    }
                }
            }
        }

        #endregion

        #region Specialized Zones

        private void UpdateSpecializedZones()
        {
            if (DateTime.Now - _lastZoneUpdate < _zoneUpdateInterval) return;

            var timeMultiplier = GetTimeMultiplier();
            var currentTime = Function.Call<int>(Hash.GET_CLOCK_HOURS);

            foreach (var zone in _zones)
            {
                if (IsTimeInRange(currentTime, zone.ActiveHours))
                {
                    UpdateZoneTraffic(zone);
                }
            }

            _lastZoneUpdate = DateTime.Now;
        }

        private bool IsTimeInRange(double currentTime, TimeRange timeRange)
        {
            if (timeRange.Start <= timeRange.End)
            {
                return currentTime >= timeRange.Start && currentTime <= timeRange.End;
            }
            else
            {
                return currentTime >= timeRange.Start || currentTime <= timeRange.End;
            }
        }

        private void UpdateZoneTraffic(SpecializedZone zone)
        {
            // Nettoyer véhicules inexistants
            zone.SpawnedVehicles.RemoveAll(v => v == null || !v.Exists());
            zone.CurrentVehicleCount = zone.SpawnedVehicles.Count;

            // Ajouter véhicules si nécessaire
            if (zone.CurrentVehicleCount < zone.MaxVehicles && _rng.NextDouble() < zone.ActivityLevel)
            {
                SpawnZoneVehicle(zone);
            }
        }

        private void SpawnZoneVehicle(SpecializedZone zone)
        {
            var vehicleHash = zone.VehicleTypes[_rng.Next(zone.VehicleTypes.Length)];
            var model = new Model(vehicleHash);
            
            if (model.IsValid && model.Request(5000))
            {
                var spawnPosition = GetRandomPositionInZone(zone);
                var vehicle = World.CreateVehicle(model, spawnPosition, _rng.Next(0, 360));
                
                if (vehicle != null)
                {
                    ConfigureVehicleForZone(vehicle, zone);
                    zone.SpawnedVehicles.Add(vehicle);
                    zone.CurrentVehicleCount++;
                }
            }
        }

        private void ConfigureVehicleForZone(Vehicle vehicle, SpecializedZone zone)
        {
            switch (zone.Type)
            {
                case ZoneType.Industrial:
                    vehicle.DirtLevel = 0.7f; // Véhicules plus sales
                    if (vehicle.Driver != null)
                        vehicle.Driver.VehicleDrivingFlags = VehicleDrivingFlags.StopForVehicles;
                    break;
                    
                case ZoneType.Residential:
                    vehicle.DirtLevel = 0.1f; // Véhicules propres
                    if (vehicle.Driver != null)
                        vehicle.Driver.VehicleDrivingFlags = VehicleDrivingFlags.FollowTraffic | VehicleDrivingFlags.StopAtDestination;
                    break;
                    
                case ZoneType.Commercial:
                    if (vehicle.Driver != null)
                        vehicle.Driver.VehicleDrivingFlags = VehicleDrivingFlags.StopForVehicles;
                    break;
                    
                case ZoneType.Airport:
                    if (unchecked((int)vehicle.Model.Hash) == unchecked((int)VehicleHash.Bus) || unchecked((int)vehicle.Model.Hash) == unchecked((int)VehicleHash.Taxi))
                    {
                        // Créer passagers
                        for (int i = 1; i < vehicle.PassengerCapacity && _rng.Next(100) < 60; i++)
                        {
                            var passenger = vehicle.CreatePedOnSeat((VehicleSeat)i, PedHash.Business01AMY);
                        }
                    }
                    break;
            }
        }

        private void EnhanceNearbyVehicleBehavior(Ped player)
        {
            var nearbyVehicles = World.GetNearbyVehicles(player, 150f);
            
            foreach (var vehicle in nearbyVehicles)
            {
                if (vehicle == null || !vehicle.Exists() || vehicle == player.CurrentVehicle) continue;
                
                EnhanceVehicleBehavior(vehicle);
            }
        }

        private void EnhanceVehicleBehavior(Vehicle vehicle)
        {
            if (!_vehicleData.ContainsKey(vehicle))
            {
                _vehicleData[vehicle] = new VehicleTrafficData
                {
                    LastUpdate = DateTime.Now,
                    OriginalDrivingStyle = VehicleDrivingFlags.FollowTraffic | VehicleDrivingFlags.StopAtDestination
                };
            }

            var data = _vehicleData[vehicle];
            var driver = vehicle.Driver;
            
            if (driver == null || !driver.Exists()) return;

            // Comportement réactif aux événements de circulation
            foreach (var trafficEvent in _activeEvents)
            {
                var distanceToEvent = vehicle.Position.DistanceTo(trafficEvent.Position);
                if (distanceToEvent < 50f)
                {
                    // Ralentir à proximité des événements
                    driver.VehicleDrivingFlags = VehicleDrivingFlags.StopForVehicles;
                    
                    // Changer de voie si possible
                    if (_rng.Next(100) < 30)
                    {
                        driver.Task.FollowNavMeshTo(vehicle.Position + vehicle.RightVector * 3f);
                    }
                }
            }

            // Réaction aux véhicules d'urgence
            var emergencyVehicles = World.GetNearbyVehicles(vehicle.Position, 30f)
                .Where(v => v.IsSirenActive && v.ClassType == VehicleClass.Emergency);
                
            foreach (var emergency in emergencyVehicles)
            {
                // Se ranger pour laisser passer
                var emergencyDriver = emergency.Driver;
                if (emergencyDriver != null && emergencyDriver.Exists())
                {
                    driver.Task.FollowNavMeshTo(vehicle.Position + vehicle.RightVector * 4f);
                }
            }
        }

        #endregion

        #region Helper Methods

        private Vector3 GetRandomNearbyPosition(Vector3 center, float minDistance, float maxDistance)
        {
            var angle = _rng.NextDouble() * Math.PI * 2;
            var distance = minDistance + (_rng.NextDouble() * (maxDistance - minDistance));
            
            var offset = new Vector3(
                (float)(Math.Cos(angle) * distance),
                (float)(Math.Sin(angle) * distance),
                0f
            );

            return World.GetNextPositionOnStreet(center + offset);
        }

        private Vector3 GetRandomPositionInZone(SpecializedZone zone)
        {
            var angle = _rng.NextDouble() * Math.PI * 2;
            var distance = _rng.NextDouble() * zone.Radius;
            
            var offset = new Vector3(
                (float)(Math.Cos(angle) * distance),
                (float)(Math.Sin(angle) * distance),
                0f
            );

            return World.GetNextPositionOnStreet(zone.Center + offset);
        }

        private void CreateTrafficCones(Vector3 position)
        {
            for (int i = 0; i < 4; i++)
            {
                var cone = World.CreatePropNoOffset(new Model("prop_roadcone02a"), 
                    position + Vector3.RandomXY() * 5f, false);
            }
        }

        private void CreateConstructionProps(Vector3 position)
        {
            // Ajouter des props de construction
            var barrier = World.CreatePropNoOffset(new Model("prop_barrier_work05"), position, false);
        }

        private TimeSpan GetEventDuration(TrafficEventType type)
        {
            switch (type)
            {
                case TrafficEventType.Breakdown:
                    return TimeSpan.FromMinutes(5);
                case TrafficEventType.Delivery:
                    return TimeSpan.FromMinutes(3);
                case TrafficEventType.Construction:
                    return TimeSpan.FromMinutes(10);
                case TrafficEventType.PoliceStop:
                    return TimeSpan.FromMinutes(4);
                case TrafficEventType.ConvoyEscort:
                    return TimeSpan.FromMinutes(2);
                default:
                    return TimeSpan.FromMinutes(5);
            }
        }

        private BlipSprite GetEventBlipSprite(TrafficEventType type)
        {
            switch (type)
            {
                case TrafficEventType.Breakdown:
                    return BlipSprite.Garage;
                case TrafficEventType.Delivery:
                    return BlipSprite.Package;
                case TrafficEventType.Construction:
                    return BlipSprite.Repair;
                case TrafficEventType.PoliceStop:
                    return BlipSprite.PoliceStation;
                case TrafficEventType.ConvoyEscort:
                    return BlipSprite.Truck;
                default:
                    return BlipSprite.Information;
            }
        }

        private string GetEventDescription(TrafficEventType type)
        {
            switch (type)
            {
                case TrafficEventType.Breakdown:
                    return "Véhicule en panne";
                case TrafficEventType.Delivery:
                    return "Livraison en cours";
                case TrafficEventType.Construction:
                    return "Travaux sur la chaussée";
                case TrafficEventType.PoliceStop:
                    return "Contrôle de police";
                case TrafficEventType.ConvoyEscort:
                    return "Convoi exceptionnel";
                default:
                    return "Incident de circulation";
            }
        }

        private float GetTimeMultiplier()
        {
            var currentTime = Function.Call<int>(Hash.GET_CLOCK_HOURS);
            
            // Heures de pointe (7-9h et 17-19h)
            if ((currentTime >= 7 && currentTime <= 9) || (currentTime >= 17 && currentTime <= 19))
                return 1.5f;
            
            return 1.0f;
        }

        private void UpdateTrafficEvent(TrafficEvent trafficEvent)
        {
            // Logique de mise à jour spécifique par type d'événement
        }

        private void CleanupTrafficEvent(TrafficEvent trafficEvent)
        {
            // Nettoyer les véhicules
            foreach (var vehicle in trafficEvent.InvolvedVehicles.ToList())
            {
                if (vehicle != null && vehicle.Exists())
                {
                    vehicle.MarkAsNoLongerNeeded();
                }
            }
            
            // Nettoyer les peds
            foreach (var ped in trafficEvent.InvolvedPeds.ToList())
            {
                if (ped != null && ped.Exists())
                {
                    ped.MarkAsNoLongerNeeded();
                }
            }
            
            // Nettoyer le blip
            if (trafficEvent.Blip != null && trafficEvent.Blip.Exists())
            {
                trafficEvent.Blip.Delete();
            }
            
            trafficEvent.IsExpired = true;
        }

        private void CleanupExpiredData()
        {
            // Nettoyer les données des véhicules qui n'existent plus
            var expiredVehicles = _vehicleData.Keys.Where(v => v == null || !v.Exists()).ToList();
            foreach (var vehicle in expiredVehicles)
            {
                _vehicleData.Remove(vehicle);
            }
        }

        #endregion
    }

    public class TrafficEvent
    {
        public Guid Id { get; set; }
        public TrafficEventType Type { get; set; }
        public Vector3 Position { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int AffectedLanes { get; set; }
        public List<Vehicle> InvolvedVehicles { get; set; } = new List<Vehicle>();
        public List<Ped> InvolvedPeds { get; set; } = new List<Ped>();
        public Blip Blip { get; set; }
        public bool IsExpired { get; set; }
    }

    public class SpecializedZone
    {
        public string Name { get; set; }
        public Vector3 Center { get; set; }
        public float Radius { get; set; }
        public ZoneType Type { get; set; }
        public TimeRange ActiveHours { get; set; }
        public VehicleHash[] VehicleTypes { get; set; }
        public int MaxVehicles { get; set; }
        public int CurrentVehicleCount { get; set; }
        public float ActivityLevel { get; set; }
        public List<Vehicle> SpawnedVehicles { get; set; } = new List<Vehicle>();

        public void Dispose()
        {
            foreach (var vehicle in SpawnedVehicles.ToList())
            {
                if (vehicle != null && vehicle.Exists())
                {
                    vehicle.MarkAsNoLongerNeeded();
                }
            }
            SpawnedVehicles.Clear();
        }
    }

    public class VehicleTrafficData
    {
        public DateTime LastUpdate { get; set; }
        public VehicleDrivingFlags OriginalDrivingStyle { get; set; }
        public bool IsInTrafficEvent { get; set; }
    }

    public enum TrafficEventType
    {
        Breakdown,
        Delivery,
        Construction,
        PoliceStop,
        ConvoyEscort
    }

    public enum ZoneType
    {
        Industrial,
        Residential,
        Commercial,
        Airport
    }

    public class TimeRange
    {
        public int Start { get; set; }
        public int End { get; set; }

        public TimeRange(int start, int end)
        {
            Start = start;
            End = end;
        }
    }
}