using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.UI;
using GTA.Native;
using REALIS.Common;
using REALIS.Core;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Système de trafic réaliste unifié avec toutes les améliorations
    /// </summary>
    public class RealisticTrafficEnhancements : Script
    {
        private readonly Random _random = new Random();
        private readonly List<TrafficCheckpoint> _checkpoints = new List<TrafficCheckpoint>();
        private readonly List<TrafficAccidentScene> _accidents = new List<TrafficAccidentScene>();
        private readonly List<PublicBus> _buses = new List<PublicBus>();
        private readonly List<SpeedZone> _speedZones = new List<SpeedZone>();
        
        private DateTime _lastUpdate = DateTime.MinValue;
        private DateTime _lastCheckpointCheck = DateTime.MinValue;
        private DateTime _lastAccidentCheck = DateTime.MinValue;
        private DateTime _lastBusSpawn = DateTime.MinValue;
        
        private const double UPDATE_INTERVAL = 5.0; // 5 secondes
        private const double CHECKPOINT_INTERVAL = 45.0; // 45 secondes
        private const double ACCIDENT_INTERVAL = 120.0; // 2 minutes
        private const double BUS_SPAWN_INTERVAL = 180.0; // 3 minutes
        
        public RealisticTrafficEnhancements()
        {
            try
            {
                Tick += OnTick;
                Aborted += OnAborted;
                
                InitializeSpeedZones();
                InitializeBusRoutes();
                
                Logger.Info("Realistic Traffic Enhancements initialized");
                Notification.PostTicker("~g~REALIS Traffic~w~ - Code de la route réaliste activé", true);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize Realistic Traffic Enhancements: {ex.Message}");
            }
        }
        
        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                var playerPos = Game.Player?.Character?.Position ?? Vector3.Zero;
                
                if ((now - _lastUpdate).TotalSeconds >= UPDATE_INTERVAL)
                {
                    UpdateSpeedLimits(playerPos);
                    UpdateActiveElements(playerPos);
                    _lastUpdate = now;
                }
                
                if ((now - _lastCheckpointCheck).TotalSeconds >= CHECKPOINT_INTERVAL)
                {
                    CheckForTrafficControls(playerPos);
                    _lastCheckpointCheck = now;
                }
                
                if ((now - _lastAccidentCheck).TotalSeconds >= ACCIDENT_INTERVAL)
                {
                    CheckForAccidents(playerPos);
                    _lastAccidentCheck = now;
                }
                
                if ((now - _lastBusSpawn).TotalSeconds >= BUS_SPAWN_INTERVAL)
                {
                    ManagePublicTransport(playerPos);
                    _lastBusSpawn = now;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Realistic Traffic tick error: {ex.Message}");
            }
        }
        
        private void InitializeSpeedZones()
        {
            // Centre-ville - 50 km/h
            _speedZones.Add(new SpeedZone { Center = new Vector3(-800, -200, 0), Radius = 500f, Limit = 50 });
            
            // Zone résidentielle - 30 km/h  
            _speedZones.Add(new SpeedZone { Center = new Vector3(-1500, -500, 0), Radius = 300f, Limit = 30 });
            
            // Autoroute - 120 km/h
            _speedZones.Add(new SpeedZone { Center = new Vector3(-2000, 0, 0), Radius = 1000f, Limit = 120 });
            
            Logger.Info($"Initialized {_speedZones.Count} speed zones");
        }
        
        private void InitializeBusRoutes()
        {
            // Points d'arrêt de bus dans Los Santos
            var busStops = new[]
            {
                new Vector3(-800, -200, 20),   // Centre-ville
                new Vector3(-600, -400, 25),   // Commercial
                new Vector3(-1500, -300, 30),  // Résidentiel
                new Vector3(-1200, -800, 35),  // Périphérie
                new Vector3(-400, -600, 30)    // Industrial
            };
            
            Logger.Info($"Initialized {busStops.Length} bus stops");
        }
        
        private void UpdateSpeedLimits(Vector3 playerPos)
        {
            if (!Game.Player.Character.IsInVehicle()) return;
            
            var vehicle = Game.Player.Character.CurrentVehicle;
            var currentSpeed = vehicle.Speed * 3.6f; // km/h
            var speedLimit = GetSpeedLimit(playerPos);
            
            // Vérifier l'excès de vitesse du joueur
            if (currentSpeed > speedLimit + 20)
            {
                Screen.ShowSubtitle($"~r~EXCÈS DE VITESSE~w~\nLimite: {speedLimit} km/h\nVitesse: {(int)currentSpeed} km/h", 3000);
                
                // Chance de déclencher un contrôle
                if (_random.Next(100) < 10) // 10% de chance
                {
                    CreateTrafficCheckpoint(playerPos);
                }
            }
            
            // Appliquer les limites aux NPCs
            var nearbyVehicles = World.GetNearbyVehicles(playerPos, 100f);
            foreach (var npcVehicle in nearbyVehicles)
            {
                if (npcVehicle?.Driver?.Exists() == true && !npcVehicle.Driver.IsPlayer)
                {
                    var npcSpeed = npcVehicle.Speed * 3.6f;
                    if (npcSpeed > speedLimit + 15)
                    {
                        npcVehicle.Driver.Task.CruiseWithVehicle(npcVehicle, (speedLimit + 5) / 3.6f, (VehicleDrivingFlags)786603);
                    }
                }
            }
        }
        
        private void CheckForTrafficControls(Vector3 playerPos)
        {
            if (_checkpoints.Count >= 2) return; // Maximum 2 contrôles actifs
            
            // 8% de chance de contrôle routier
            if (_random.Next(100) < 8)
            {
                CreateTrafficCheckpoint(playerPos);
            }
        }
        
        private void CreateTrafficCheckpoint(Vector3 playerPos)
        {
            var checkpointPos = FindSuitableRoadPosition(playerPos, 200f, 500f);
            if (checkpointPos == Vector3.Zero) return;
            
            var checkpoint = new TrafficCheckpoint
            {
                Position = checkpointPos,
                CreatedTime = DateTime.Now,
                Type = _random.Next(2) == 0 ? CheckpointType.Violation : CheckpointType.Random
            };
            
            // Spawn véhicule de police
            var policeVehicle = World.CreateVehicle(VehicleHash.Police, checkpointPos);
            if (policeVehicle?.Exists() == true)
            {
                Function.Call(Hash.SET_VEHICLE_SIREN, policeVehicle, true);
                checkpoint.Vehicle = policeVehicle;
                
                // Spawn policiers
                for (int i = 0; i < 2; i++)
                {
                    var cop = World.CreatePed(PedHash.Cop01SMY, checkpointPos + Vector3.RandomXY() * 5f);
                    if (cop?.Exists() == true)
                    {
                        cop.Weapons.Give(WeaponHash.Pistol, 100, true, false);
                        cop.Task.StartScenario("WORLD_HUMAN_COP_IDLES", 0);
                        checkpoint.Officers.Add(cop);
                    }
                }
                
                _checkpoints.Add(checkpoint);
                Logger.Info($"Traffic checkpoint created at {checkpointPos}");
                
                if (playerPos.DistanceTo(checkpointPos) < 300f)
                {
                    Notification.PostTicker("~b~Contrôle routier~w~ en cours dans la zone", false);
                }
            }
        }
        
        private void CheckForAccidents(Vector3 playerPos)
        {
            if (_accidents.Count >= 3) return; // Maximum 3 accidents actifs
            
            // Calculer la probabilité d'accident
            var hour = DateTime.Now.Hour;
            var weather = World.Weather;
            
            int accidentChance = 3; // 3% de base
            
            // Plus d'accidents la nuit
            if (hour >= 22 || hour <= 6) accidentChance *= 2;
            
            // Plus d'accidents par mauvais temps
            if (weather == Weather.Raining || weather == Weather.Foggy) accidentChance *= 3;
            
            if (_random.Next(100) < accidentChance)
            {
                CreateAccidentScene(playerPos);
            }
        }
        
        private void CreateAccidentScene(Vector3 playerPos)
        {
            var accidentPos = FindSuitableRoadPosition(playerPos, 150f, 400f);
            if (accidentPos == Vector3.Zero) return;
            
            var accident = new TrafficAccidentScene
            {
                Position = accidentPos,
                CreatedTime = DateTime.Now,
                Severity = DetermineAccidentSeverity()
            };
            
            // Spawn véhicules accidentés
            var vehicleCount = accident.Severity == AccidentSeverity.Minor ? 1 : 2;
            
            for (int i = 0; i < vehicleCount; i++)
            {
                var vehiclePos = accidentPos + Vector3.RandomXY() * (i * 10f);
                var vehicle = World.CreateVehicle(GetRandomVehicleHash(), vehiclePos);
                
                if (vehicle?.Exists() == true)
                {
                    // Appliquer les dégâts
                    ApplyAccidentDamage(vehicle, accident.Severity);
                    
                    // Conducteur blessé
                    var driver = World.CreatePed(GetRandomPedHash(), vehiclePos);
                    if (driver?.Exists() == true)
                    {
                        driver.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                        if (accident.Severity != AccidentSeverity.Minor)
                        {
                            driver.Health = Math.Max(50, driver.Health - 100);
                        }
                        accident.People.Add(driver);
                    }
                    
                    accident.Vehicles.Add(vehicle);
                }
            }
            
            // Spawn témoins
            for (int i = 0; i < 2 + _random.Next(3); i++)
            {
                var witnessPos = accidentPos + Vector3.RandomXY() * (15f + _random.Next(10));
                var witness = World.CreatePed(GetRandomPedHash(), witnessPos);
                
                if (witness?.Exists() == true)
                {
                    witness.Task.LookAt(accidentPos, 30000);
                    if (_random.Next(3) == 0)
                    {
                        witness.Task.UseMobilePhone(8000); // Appelle les secours
                    }
                    accident.Witnesses.Add(witness);
                }
            }
            
            _accidents.Add(accident);
            Logger.Info($"Accident created at {accidentPos} - Severity: {accident.Severity}");
            
            if (playerPos.DistanceTo(accidentPos) < 300f)
            {
                Notification.PostTicker("~r~ACCIDENT~w~ signalé dans la zone", false);
            }
            
            // Programmer l'arrivée des secours
            ScheduleEmergencyServices(accident);
        }
        
        private void ScheduleEmergencyServices(TrafficAccidentScene accident)
        {
            System.Threading.Tasks.Task.Delay(60000 + _random.Next(120000)).ContinueWith(_ =>
            {
                try
                {
                    SpawnEmergencyServices(accident);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error spawning emergency services: {ex.Message}");
                }
            });
        }
        
        private void SpawnEmergencyServices(TrafficAccidentScene accident)
        {
            if (accident.Vehicles.Any(v => v?.Exists() != true)) return;
            
            // Ambulance
            var ambulance = World.CreateVehicle(VehicleHash.Ambulance, accident.Position + Vector3.RandomXY() * 20f);
            if (ambulance?.Exists() == true)
            {
                Function.Call(Hash.SET_VEHICLE_SIREN, ambulance, true);
                accident.EmergencyVehicles.Add(ambulance);
                
                // Paramedics
                for (int i = 0; i < 2; i++)
                {
                    var medic = World.CreatePed(PedHash.Paramedic01SMM, ambulance.Position + Vector3.RandomXY() * 3f);
                    if (medic?.Exists() == true)
                    {
                        medic.Task.GoTo(accident.Position);
                        accident.EmergencyPersonnel.Add(medic);
                    }
                }
            }
            
            // Police
            var policecar = World.CreateVehicle(VehicleHash.Police, accident.Position + Vector3.RandomXY() * 15f);
            if (policecar?.Exists() == true)
            {
                Function.Call(Hash.SET_VEHICLE_SIREN, policecar, true);
                accident.EmergencyVehicles.Add(policecar);
            }
            
            Logger.Info($"Emergency services dispatched to accident at {accident.Position}");
        }
        
        private void ManagePublicTransport(Vector3 playerPos)
        {
            if (_buses.Count >= 4) return; // Maximum 4 bus actifs
            
            // 60% de chance de spawn d'un bus
            if (_random.Next(100) < 60)
            {
                CreatePublicBus(playerPos);
            }
        }
        
        private void CreatePublicBus(Vector3 playerPos)
        {
            var busPos = FindSuitableRoadPosition(playerPos, 100f, 300f);
            if (busPos == Vector3.Zero) return;
            
            var bus = World.CreateVehicle(VehicleHash.Bus, busPos);
            if (bus?.Exists() != true) return;
            
            var driver = World.CreatePed(PedHash.FreemodeMale01, busPos);
            if (driver?.Exists() == true)
            {
                driver.SetIntoVehicle(bus, VehicleSeat.Driver);
                
                var publicBus = new PublicBus
                {
                    Vehicle = bus,
                    Driver = driver,
                    Route = _random.Next(1, 4), // Routes 1-3
                    CreatedTime = DateTime.Now
                };
                
                // Passagers initiaux
                SpawnBusPassengers(publicBus, 3 + _random.Next(6));
                
                // Comportement de bus
                ApplyBusBehavior(publicBus);
                
                _buses.Add(publicBus);
                Logger.Info($"Public bus spawned on route {publicBus.Route}");
            }
        }
        
        private void SpawnBusPassengers(PublicBus bus, int count)
        {
            for (int i = 1; i <= Math.Min(count, 15); i++) // Max 15 passagers
            {
                var seat = (VehicleSeat)i;
                if (bus.Vehicle.IsSeatFree(seat))
                {
                    var passenger = World.CreatePed(GetRandomPedHash(), bus.Vehicle.Position);
                    if (passenger?.Exists() == true)
                    {
                        passenger.SetIntoVehicle(bus.Vehicle, seat);
                        bus.Passengers.Add(passenger);
                    }
                }
            }
        }
        
        private void ApplyBusBehavior(PublicBus bus)
        {
            // Conduite lente et prudente
            bus.Driver.Task.CruiseWithVehicle(bus.Vehicle, 35f / 3.6f, (VehicleDrivingFlags)786603);
            
            // Programmer les arrêts
            ScheduleBusStops(bus);
        }
        
        private void ScheduleBusStops(PublicBus bus)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(30000 + _random.Next(60000)); // 30s-1m30s
                    
                    if (bus.Vehicle?.Exists() == true)
                    {
                        // Arrêt du bus
                        bus.Driver.Task.CruiseWithVehicle(bus.Vehicle, 0f, (VehicleDrivingFlags)786603);
                        
                        await System.Threading.Tasks.Task.Delay(10000 + _random.Next(15000)); // 10-25s d'arrêt
                        
                        // Reprise
                        if (bus.Vehicle?.Exists() == true)
                        {
                            bus.Driver.Task.CruiseWithVehicle(bus.Vehicle, 35f / 3.6f, (VehicleDrivingFlags)786603);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Bus stop schedule error: {ex.Message}");
                }
            });
        }
        
        private void UpdateActiveElements(Vector3 playerPos)
        {
            // Cleanup checkpoints distants ou expirés
            var checkpointsToRemove = _checkpoints.Where(c => 
                c.Vehicle?.Exists() != true || 
                playerPos.DistanceTo(c.Position) > 1500f ||
                (DateTime.Now - c.CreatedTime).TotalMinutes > 10).ToList();
                
            foreach (var checkpoint in checkpointsToRemove)
            {
                RemoveCheckpoint(checkpoint);
            }
            
            // Cleanup accidents expirés
            var accidentsToRemove = _accidents.Where(a => 
                (DateTime.Now - a.CreatedTime).TotalMinutes > 30 ||
                playerPos.DistanceTo(a.Position) > 2000f).ToList();
                
            foreach (var accident in accidentsToRemove)
            {
                RemoveAccident(accident);
            }
            
            // Cleanup bus distants
            var busesToRemove = _buses.Where(b => 
                b.Vehicle?.Exists() != true ||
                playerPos.DistanceTo(b.Vehicle.Position) > 1500f ||
                (DateTime.Now - b.CreatedTime).TotalMinutes > 15).ToList();
                
            foreach (var bus in busesToRemove)
            {
                RemoveBus(bus);
            }
        }
        
        private void RemoveCheckpoint(TrafficCheckpoint checkpoint)
        {
            try
            {
                if (checkpoint.Vehicle?.Exists() == true)
                    checkpoint.Vehicle.Delete();
                    
                foreach (var officer in checkpoint.Officers)
                {
                    if (officer?.Exists() == true)
                        officer.Delete();
                }
                
                _checkpoints.Remove(checkpoint);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error removing checkpoint: {ex.Message}");
            }
        }
        
        private void RemoveAccident(TrafficAccidentScene accident)
        {
            try
            {
                foreach (var vehicle in accident.Vehicles)
                {
                    if (vehicle?.Exists() == true) vehicle.Delete();
                }
                
                foreach (var person in accident.People.Concat(accident.Witnesses).Concat(accident.EmergencyPersonnel))
                {
                    if (person?.Exists() == true) person.Delete();
                }
                
                foreach (var emergency in accident.EmergencyVehicles)
                {
                    if (emergency?.Exists() == true) emergency.Delete();
                }
                
                _accidents.Remove(accident);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error removing accident: {ex.Message}");
            }
        }
        
        private void RemoveBus(PublicBus bus)
        {
            try
            {
                if (bus.Driver?.Exists() == true) bus.Driver.Delete();
                
                foreach (var passenger in bus.Passengers)
                {
                    if (passenger?.Exists() == true) passenger.Delete();
                }
                
                if (bus.Vehicle?.Exists() == true) bus.Vehicle.Delete();
                
                _buses.Remove(bus);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error removing bus: {ex.Message}");
            }
        }
        
        // Méthodes utilitaires
        private int GetSpeedLimit(Vector3 position)
        {
            foreach (var zone in _speedZones)
            {
                if (position.DistanceTo(zone.Center) <= zone.Radius)
                {
                    return zone.Limit;
                }
            }
            
            // Vitesse par défaut selon le type de zone
            if (IsHighway(position)) return 120;
            if (IsResidential(position)) return 50;
            return 80;
        }
        
        private bool IsHighway(Vector3 pos) => pos.Y > 1000 || pos.Y < -2000;
        private bool IsResidential(Vector3 pos) => pos.DistanceTo(Vector3.Zero) < 1000;
        
        private Vector3 FindSuitableRoadPosition(Vector3 center, float minDist, float maxDist)
        {
            for (int i = 0; i < 15; i++)
            {
                var angle = _random.NextDouble() * Math.PI * 2;
                var distance = minDist + _random.Next((int)(maxDist - minDist));
                
                var testPos = new Vector3(
                    center.X + (float)(Math.Cos(angle) * distance),
                    center.Y + (float)(Math.Sin(angle) * distance),
                    center.Z
                );
                
                if (Function.Call<bool>(Hash.IS_POINT_ON_ROAD, testPos.X, testPos.Y, testPos.Z, 0))
                {
                    return testPos;
                }
            }
            
            return Vector3.Zero;
        }
        
        private AccidentSeverity DetermineAccidentSeverity()
        {
            var roll = _random.Next(100);
            if (roll < 50) return AccidentSeverity.Minor;
            if (roll < 80) return AccidentSeverity.Major;
            return AccidentSeverity.Severe;
        }
        
        private void ApplyAccidentDamage(Vehicle vehicle, AccidentSeverity severity)
        {
            var damage = severity switch
            {
                AccidentSeverity.Minor => 300f,
                AccidentSeverity.Major => 600f,
                AccidentSeverity.Severe => 900f,
                _ => 200f
            };
            
            vehicle.Health = Math.Max(100, (int)(vehicle.MaxHealth - damage));
            // vehicle.Deform(vehicle.Position, 30f, damage); // deformation not supported
            
            if (severity == AccidentSeverity.Severe && _random.Next(3) == 0)
            {
                Function.Call(Hash.START_ENTITY_FIRE, vehicle);
            }
        }
        
        private VehicleHash GetRandomVehicleHash()
        {
            var vehicles = new[] { VehicleHash.Blista, VehicleHash.Premier, VehicleHash.Sultan, 
                                 VehicleHash.Taxi, VehicleHash.Ingot, VehicleHash.Stanier };
            return vehicles[_random.Next(vehicles.Length)];
        }
        
        private PedHash GetRandomPedHash()
        {
            var peds = new[] { PedHash.Business01AMY, PedHash.Downtown01AMY, PedHash.Eastsa01AMY,
                             PedHash.Eastsa02AFY, PedHash.Business01AFY };
            return peds[_random.Next(peds.Length)];
        }
        
        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                // Nettoyer toutes les entités créées
                foreach (var checkpoint in _checkpoints.ToList())
                {
                    RemoveCheckpoint(checkpoint);
                }
                
                foreach (var accident in _accidents.ToList())
                {
                    RemoveAccident(accident);
                }
                
                foreach (var bus in _buses.ToList())
                {
                    RemoveBus(bus);
                }
                
                Logger.Info("Realistic Traffic Enhancements disposed");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during disposal: {ex.Message}");
            }
        }
    }
    
    // Classes de données
    public class TrafficCheckpoint
    {
        public Vector3 Position { get; set; }
        public DateTime CreatedTime { get; set; }
        public CheckpointType Type { get; set; }
        public Vehicle Vehicle { get; set; }
        public List<Ped> Officers { get; set; } = new List<Ped>();
    }
    
    public class TrafficAccidentScene
    {
        public Vector3 Position { get; set; }
        public DateTime CreatedTime { get; set; }
        public AccidentSeverity Severity { get; set; }
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public List<Ped> People { get; set; } = new List<Ped>();
        public List<Ped> Witnesses { get; set; } = new List<Ped>();
        public List<Vehicle> EmergencyVehicles { get; set; } = new List<Vehicle>();
        public List<Ped> EmergencyPersonnel { get; set; } = new List<Ped>();
    }
    
    public class PublicBus
    {
        public Vehicle Vehicle { get; set; }
        public Ped Driver { get; set; }
        public int Route { get; set; }
        public DateTime CreatedTime { get; set; }
        public List<Ped> Passengers { get; set; } = new List<Ped>();
    }
    
    public class SpeedZone
    {
        public Vector3 Center { get; set; }
        public float Radius { get; set; }
        public int Limit { get; set; }
    }
    
    // Enums supprimés car définis dans d'autres fichiers
} 