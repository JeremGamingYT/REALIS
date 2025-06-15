using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.UI;
using GTA.Native;
using REALIS.Common;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Système d'accidents avec conséquences réelles
    /// </summary>
    public class AccidentSystem : IDisposable
    {
        private readonly List<TrafficAccident> _activeAccidents = new List<TrafficAccident>();
        private readonly Random _random = new Random();
        
        private DateTime _lastAccidentCheck = DateTime.MinValue;
        private DateTime _lastEmergencyCheck = DateTime.MinValue;
        
        private const double ACCIDENT_CHECK_INTERVAL = 10.0; // 10 secondes
        private const double EMERGENCY_CHECK_INTERVAL = 5.0; // 5 secondes
        private const int MAX_ACTIVE_ACCIDENTS = 3;
        private const float ACCIDENT_DETECTION_RADIUS = 200f;
        
        // Probabilités d'accidents
        private const int RANDOM_ACCIDENT_CHANCE = 2; // 2% toutes les 10s
        private const int TRAFFIC_JAM_ACCIDENT_CHANCE = 8; // 8% dans les embouteillages
        private const int WEATHER_ACCIDENT_MULTIPLIER = 3; // x3 par mauvais temps
        
        public void Initialize()
        {
            // Logger.Info("Accident System initialized");
        }
        
        public void Update(Vector3 playerPosition)
        {
            try
            {
                CheckForRandomAccidents(playerPosition);
                UpdateActiveAccidents(playerPosition);
                CheckEmergencyServices();
                CleanupOldAccidents();
            }
            catch (Exception ex)
            {
                // Logger.Error($"Accident System update error: {ex.Message}");
            }
        }
        
        private void CheckForRandomAccidents(Vector3 playerPosition)
        {
            if ((DateTime.Now - _lastAccidentCheck).TotalSeconds < ACCIDENT_CHECK_INTERVAL) return;
            
            _lastAccidentCheck = DateTime.Now;
            
            if (_activeAccidents.Count >= MAX_ACTIVE_ACCIDENTS) return;
            
            // Calculer la probabilité d'accident
            int accidentChance = RANDOM_ACCIDENT_CHANCE;
            
            // Modifier selon la météo
            var weather = World.Weather;
            if (weather == Weather.Raining || weather == Weather.ThunderStorm || weather == Weather.Foggy)
            {
                accidentChance *= WEATHER_ACCIDENT_MULTIPLIER;
            }
            
            // Modifier selon l'heure (plus d'accidents la nuit)
            var hour = World.CurrentTimeOfDay.Hours;
            if (hour >= 22 || hour <= 6)
            {
                accidentChance *= 2;
            }
            
            // Vérifier s'il y a des embouteillages
            if (IsTrafficJamNearby(playerPosition))
            {
                accidentChance = TRAFFIC_JAM_ACCIDENT_CHANCE;
            }
            
            if (_random.Next(100) < accidentChance)
            {
                CreateRandomAccident(playerPosition);
            }
        }
        
        private void CreateRandomAccident(Vector3 playerPosition)
        {
            var accidentPosition = FindSuitableAccidentPosition(playerPosition);
            if (accidentPosition == Vector3.Zero) return;
            
            var accidentType = DetermineAccidentType();
            var accident = new TrafficAccident
            {
                Position = accidentPosition,
                Type = accidentType,
                CreatedTime = DateTime.Now,
                Severity = DetermineAccidentSeverity(accidentType),
                Status = AccidentStatus.JustOccurred
            };
            
            SpawnAccidentScene(accident);
            _activeAccidents.Add(accident);
            
            // Logger.Info($"Accident created: {accidentType} at {accidentPosition}");
            
            // Notifier le joueur s'il est proche
            if (playerPosition.DistanceTo(accidentPosition) < 300f)
            {
                Notification.PostTicker("~r~ACCIDENT~w~ signalé dans la zone", false);
            }
        }
        
        private AccidentType DetermineAccidentType()
        {
            var types = (AccidentType[])Enum.GetValues(typeof(AccidentType));
            return types[_random.Next(types.Length)];
        }
        
        private AccidentSeverity DetermineAccidentSeverity(AccidentType type)
        {
            return type switch
            {
                AccidentType.MinorCollision => AccidentSeverity.Minor,
                AccidentType.MajorCollision => _random.Next(2) == 0 ? AccidentSeverity.Major : AccidentSeverity.Severe,
                AccidentType.VehicleFire => AccidentSeverity.Severe,
                AccidentType.Rollover => AccidentSeverity.Major,
                AccidentType.HeadOnCollision => AccidentSeverity.Severe,
                AccidentType.MultiVehicle => AccidentSeverity.Severe,
                _ => AccidentSeverity.Minor
            };
        }
        
        private void SpawnAccidentScene(TrafficAccident accident)
        {
            try
            {
                // Spawn des véhicules accidentés
                SpawnAccidentVehicles(accident);
                
                // Spawn des témoins/victimes
                SpawnAccidentPeople(accident);
                
                // Ajouter des débris
                SpawnAccidentDebris(accident);
                
                // Créer des embouteillages
                CreateTrafficJam(accident);
                
                // Logger.Info($"Accident scene spawned with {accident.Vehicles.Count} vehicles and {accident.People.Count} people");
            }
            catch (Exception ex)
            {
                // Logger.Error($"Error spawning accident scene: {ex.Message}");
            }
        }
        
        private void SpawnAccidentVehicles(TrafficAccident accident)
        {
            var vehicleCount = accident.Type switch
            {
                AccidentType.MultiVehicle => 3 + _random.Next(2), // 3-4 véhicules
                AccidentType.HeadOnCollision => 2,
                AccidentType.MajorCollision => 2,
                _ => 1 + _random.Next(2) // 1-2 véhicules
            };
            
            for (int i = 0; i < vehicleCount; i++)
            {
                var vehicleHash = GetRandomVehicleHash();
                var spawnPos = accident.Position + Vector3.RandomXY() * (i * 8f);
                
                var vehicle = World.CreateVehicle(vehicleHash, spawnPos);
                if (vehicle?.Exists() == true)
                {
                    // Appliquer les dégâts
                    ApplyAccidentDamage(vehicle, accident.Severity);
                    
                    // Positionner de manière réaliste
                    vehicle.Heading = _random.Next(360);
                    
                    // Ajouter un conducteur blessé
                    var driver = World.CreatePed(GetRandomPedHash(), vehicle.Position);
                    if (driver?.Exists() == true)
                    {
                        driver.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                        ApplyInjuries(driver, accident.Severity);
                        accident.People.Add(driver);
                    }
                    
                    accident.Vehicles.Add(vehicle);
                }
            }
        }
        
        private void SpawnAccidentPeople(TrafficAccident accident)
        {
            // Spawn des témoins
            var witnessCount = 2 + _random.Next(4); // 2-5 témoins
            
            for (int i = 0; i < witnessCount; i++)
            {
                var witnessPos = accident.Position + Vector3.RandomXY() * (10f + _random.Next(20));
                var witness = World.CreatePed(GetRandomPedHash(), witnessPos);
                
                if (witness?.Exists() == true)
                {
                    // Témoins regardent l'accident
                    witness.Task.LookAt(accident.Position, 30000);
                    
                    // Certains témoins appellent les secours
                    if (_random.Next(3) == 0)
                    {
                        witness.Task.UseMobilePhone(10000);
                    }
                    else
                    {
                        witness.Task.StartScenario("WORLD_HUMAN_STAND_MOBILE", 0);
                    }
                    
                    accident.Witnesses.Add(witness);
                }
            }
        }
        
        private void SpawnAccidentDebris(TrafficAccident accident)
        {
            // Ajouter des débris selon la gravité
            var debrisCount = accident.Severity switch
            {
                AccidentSeverity.Minor => 2,
                AccidentSeverity.Major => 4,
                AccidentSeverity.Severe => 6,
                _ => 2
            };
            
            for (int i = 0; i < debrisCount; i++)
            {
                var debrisPos = accident.Position + Vector3.RandomXY() * 5f;
                
                // Créer des débris visuels (objets props)
                var debrisHash = GetRandomDebrisHash();
                var debris = World.CreateProp(debrisHash, debrisPos, Vector3.RandomXY(), false, false);
                
                if (debris?.Exists() == true)
                {
                    accident.Debris.Add(debris);
                }
            }
        }
        
        private void CreateTrafficJam(TrafficAccident accident)
        {
            // Bloquer le trafic autour de l'accident
            var nearbyVehicles = World.GetNearbyVehicles(accident.Position, 100f);
            
            foreach (var vehicle in nearbyVehicles)
            {
                if (vehicle?.Driver?.Exists() == true && !vehicle.Driver.IsPlayer)
                {
                    // Faire s'arrêter les véhicules
                    vehicle.Driver.Task.CruiseWithVehicle(vehicle, 0f, (VehicleDrivingFlags)786603);
                    
                    // Certains klaxonnent
                    if (_random.Next(4) == 0)
                    {
                        vehicle.SoundHorn(2000);
                    }
                }
            }
        }
        
        private void UpdateActiveAccidents(TrafficAccident accident)
        {
            var timeSinceAccident = DateTime.Now - accident.CreatedTime;
            
            // Évolution de l'accident selon le temps
            if (timeSinceAccident.TotalMinutes > 1 && accident.Status == AccidentStatus.JustOccurred)
            {
                accident.Status = AccidentStatus.EmergencyDispatched;
                RequestEmergencyServices(accident);
            }
            else if (timeSinceAccident.TotalMinutes > 5 && accident.Status == AccidentStatus.EmergencyDispatched)
            {
                accident.Status = AccidentStatus.EmergencyOnScene;
                SpawnEmergencyServices(accident);
            }
            else if (timeSinceAccident.TotalMinutes > 15 && accident.Status == AccidentStatus.EmergencyOnScene)
            {
                accident.Status = AccidentStatus.Cleanup;
                StartCleanup(accident);
            }
            else if (timeSinceAccident.TotalMinutes > 25)
            {
                accident.Status = AccidentStatus.Resolved;
            }
        }
        
        private void UpdateActiveAccidents(Vector3 playerPosition)
        {
            var accidentsToRemove = new List<TrafficAccident>();
            
            foreach (var accident in _activeAccidents)
            {
                UpdateActiveAccidents(accident);
                
                // Supprimer les accidents résolus ou trop lointains
                var distanceToPlayer = playerPosition.DistanceTo(accident.Position);
                if (accident.Status == AccidentStatus.Resolved || distanceToPlayer > 1500f)
                {
                    accidentsToRemove.Add(accident);
                }
            }
            
            foreach (var accident in accidentsToRemove)
            {
                RemoveAccident(accident);
            }
        }
        
        private void RequestEmergencyServices(TrafficAccident accident)
        {
            // Logger.Info($"Emergency services requested for accident at {accident.Position}");
            
            // Notifier le joueur s'il est proche
            var playerPos = Game.Player.Character.Position;
            if (playerPos.DistanceTo(accident.Position) < 200f)
            {
                Screen.ShowSubtitle("~r~SERVICES D'URGENCE~w~\nIntervention en cours...", 4000);
            }
        }
        
        private void SpawnEmergencyServices(TrafficAccident accident)
        {
            try
            {
                // Spawn ambulance pour les blessés
                if (accident.People.Any(p => p.Exists() && p.Health < p.MaxHealth))
                {
                    var ambulance = World.CreateVehicle(VehicleHash.Ambulance, 
                        accident.Position + Vector3.RandomXY() * 20f);
                    if (ambulance?.Exists() == true)
                    {
                        if (ambulance.HasSiren)
                        {
                            Function.Call(Hash.SET_VEHICLE_SIREN, ambulance.Handle, true);
                        }
                        accident.EmergencyVehicles.Add(ambulance);
                        
                        // Spawn des paramedics
                        for (int i = 0; i < 2; i++)
                        {
                            var paramedic = World.CreatePed(PedHash.Paramedic01SMM, 
                                ambulance.Position + Vector3.RandomXY() * 3f);
                            if (paramedic?.Exists() == true)
                            {
                                paramedic.Task.GoTo(accident.Position);
                                accident.EmergencyPersonnel.Add(paramedic);
                            }
                        }
                    }
                }
                
                // Spawn pompiers si incendie
                if (accident.Type == AccidentType.VehicleFire || 
                    accident.Vehicles.Any(v => v.Exists() && v.IsOnFire))
                {
                    var firetruck = World.CreateVehicle(VehicleHash.FireTruck, 
                        accident.Position + Vector3.RandomXY() * 25f);
                    if (firetruck?.Exists() == true)
                    {
                        if (firetruck.HasSiren)
                        {
                            Function.Call(Hash.SET_VEHICLE_SIREN, firetruck.Handle, true);
                        }
                        accident.EmergencyVehicles.Add(firetruck);
                        
                        // Spawn des pompiers
                        for (int i = 0; i < 3; i++)
                        {
                            var firefighter = World.CreatePed(PedHash.Fireman01SMY, 
                                firetruck.Position + Vector3.RandomXY() * 4f);
                            if (firefighter?.Exists() == true)
                            {
                                firefighter.Task.GoTo(accident.Position);
                                accident.EmergencyPersonnel.Add(firefighter);
                            }
                        }
                    }
                }
                
                // Spawn police pour sécuriser
                var policecar = World.CreateVehicle(VehicleHash.Police, 
                    accident.Position + Vector3.RandomXY() * 15f);
                if (policecar?.Exists() == true)
                {
                    if (policecar.HasSiren)
                    {
                        Function.Call(Hash.SET_VEHICLE_SIREN, policecar.Handle, true);
                    }
                    accident.EmergencyVehicles.Add(policecar);
                    
                    // Spawn des policiers
                    for (int i = 0; i < 2; i++)
                    {
                        var cop = World.CreatePed(PedHash.Cop01SMY, 
                            policecar.Position + Vector3.RandomXY() * 3f);
                        if (cop?.Exists() == true)
                        {
                            cop.Task.GoTo(accident.Position);
                            cop.Weapons.Give(WeaponHash.Pistol, 100, true, false);
                            accident.EmergencyPersonnel.Add(cop);
                        }
                    }
                }
                
                // Logger.Info($"Emergency services spawned: {accident.EmergencyVehicles.Count} vehicles, {accident.EmergencyPersonnel.Count} personnel");
            }
            catch (Exception ex)
            {
                // Logger.Error($"Error spawning emergency services: {ex.Message}");
            }
        }
        
        private void StartCleanup(TrafficAccident accident)
        {
            // Commencer le nettoyage de l'accident
            // Logger.Info($"Starting cleanup for accident at {accident.Position}");
            
            // Faire disparaître certains débris
            foreach (var debris in accident.Debris.ToList())
            {
                if (debris?.Exists() == true && _random.Next(2) == 0)
                {
                    debris.Delete();
                    accident.Debris.Remove(debris);
                }
            }
            
            // Commencer à évacuer les blessés
            foreach (var person in accident.People.ToList())
            {
                if (person?.Exists() == true && person.Health < person.MaxHealth)
                {
                    // Simuler l'évacuation
                    if (_random.Next(3) == 0)
                    {
                        person.Delete();
                        accident.People.Remove(person);
                    }
                }
            }
        }
        
        private void CheckEmergencyServices()
        {
            if ((DateTime.Now - _lastEmergencyCheck).TotalSeconds < EMERGENCY_CHECK_INTERVAL) return;
            
            _lastEmergencyCheck = DateTime.Now;
            
            // Vérifier l'état des services d'urgence
            foreach (var accident in _activeAccidents)
            {
                if (accident.Status == AccidentStatus.EmergencyOnScene)
                {
                    // Simuler le travail des services d'urgence
                    foreach (var vehicle in accident.Vehicles)
                    {
                        if (vehicle?.Exists() == true && vehicle.IsOnFire)
                        {
                            // Les pompiers éteignent le feu
                            if (_random.Next(10) == 0)
                            {
                                Function.Call(Hash.STOP_ENTITY_FIRE, vehicle.Handle);
                            }
                        }
                    }
                }
            }
        }
        
        private void RemoveAccident(TrafficAccident accident)
        {
            try
            {
                // Supprimer tous les éléments de l'accident
                foreach (var vehicle in accident.Vehicles)
                {
                    if (vehicle?.Exists() == true) vehicle.Delete();
                }
                
                foreach (var person in accident.People)
                {
                    if (person?.Exists() == true) person.Delete();
                }
                
                foreach (var witness in accident.Witnesses)
                {
                    if (witness?.Exists() == true) witness.Delete();
                }
                
                foreach (var debris in accident.Debris)
                {
                    if (debris?.Exists() == true) debris.Delete();
                }
                
                foreach (var emergency in accident.EmergencyVehicles)
                {
                    if (emergency?.Exists() == true) emergency.Delete();
                }
                
                foreach (var personnel in accident.EmergencyPersonnel)
                {
                    if (personnel?.Exists() == true) personnel.Delete();
                }
                
                _activeAccidents.Remove(accident);
                // Logger.Info($"Accident removed at {accident.Position}");
            }
            catch (Exception ex)
            {
                // Logger.Error($"Error removing accident: {ex.Message}");
            }
        }
        
        private void CleanupOldAccidents()
        {
            var cutoffTime = DateTime.Now.AddMinutes(-30);
            var oldAccidents = _activeAccidents.Where(a => a.CreatedTime < cutoffTime).ToList();
            
            foreach (var accident in oldAccidents)
            {
                RemoveAccident(accident);
            }
        }
        
        // Méthodes utilitaires
        private Vector3 FindSuitableAccidentPosition(Vector3 playerPosition)
        {
            for (int i = 0; i < 20; i++)
            {
                var angle = _random.NextDouble() * Math.PI * 2;
                var distance = 100f + _random.Next(200); // 100-300m
                
                var testPosition = new Vector3(
                    playerPosition.X + (float)(Math.Cos(angle) * distance),
                    playerPosition.Y + (float)(Math.Sin(angle) * distance),
                    playerPosition.Z
                );
                
                if (IsPositionOnRoad(testPosition))
                {
                    return testPosition;
                }
            }
            
            return Vector3.Zero;
        }
        
        private bool IsTrafficJamNearby(Vector3 position)
        {
            var nearbyVehicles = World.GetNearbyVehicles(position, 50f);
            var slowVehicles = nearbyVehicles.Count(v => v.Speed < 5f);
            
            return slowVehicles >= 5; // 5+ véhicules lents = embouteillage
        }
        
        private bool IsPositionOnRoad(Vector3 position)
        {
            return Function.Call<bool>(Hash.IS_POINT_ON_ROAD, position.X, position.Y, position.Z, 0);
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
        
        private Model GetRandomDebrisHash()
        {
            var debris = new[] { "prop_rub_carpart_02", "prop_rub_carpart_03",
                               "prop_rub_carpart_04", "prop_rub_tyre_01" };
            return new Model(debris[_random.Next(debris.Length)]);
        }
        
        private void ApplyAccidentDamage(Vehicle vehicle, AccidentSeverity severity)
        {
            var damage = severity switch
            {
                AccidentSeverity.Minor => 200f,
                AccidentSeverity.Major => 600f,
                AccidentSeverity.Severe => 900f,
                _ => 100f
            };
            
            vehicle.Health = Math.Max(100, (int)(vehicle.MaxHealth - damage));
            
            // Incendie pour les accidents graves
            if (severity == AccidentSeverity.Severe && _random.Next(3) == 0)
            {
                Function.Call(Hash.START_ENTITY_FIRE, vehicle.Handle);
            }
        }
        
        private void ApplyInjuries(Ped ped, AccidentSeverity severity)
        {
            var healthLoss = severity switch
            {
                AccidentSeverity.Minor => 50,
                AccidentSeverity.Major => 150,
                AccidentSeverity.Severe => 250,
                _ => 25
            };
            
            ped.Health = Math.Max(1, ped.Health - healthLoss);
            
            // Animation de blessure
            if (ped.Health < ped.MaxHealth / 2)
            {
                ped.Task.PlayAnimation("missminuteman_1ig_2", "handsup_base", 8f, -1, AnimationFlags.Loop);
            }
        }
        
        public void Dispose()
        {
            foreach (var accident in _activeAccidents.ToList())
            {
                RemoveAccident(accident);
            }
            
            _activeAccidents.Clear();
        }
    }
    
    /// <summary>
    /// Représente un accident de circulation
    /// </summary>
    public class TrafficAccident
    {
        public Vector3 Position { get; set; }
        public AccidentType Type { get; set; }
        public AccidentSeverity Severity { get; set; }
        public AccidentStatus Status { get; set; }
        public DateTime CreatedTime { get; set; }
        
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public List<Ped> People { get; set; } = new List<Ped>();
        public List<Ped> Witnesses { get; set; } = new List<Ped>();
        public List<Prop> Debris { get; set; } = new List<Prop>();
        public List<Vehicle> EmergencyVehicles { get; set; } = new List<Vehicle>();
        public List<Ped> EmergencyPersonnel { get; set; } = new List<Ped>();
    }
    
    public enum AccidentType
    {
        MinorCollision,
        MajorCollision,
        VehicleFire,
        Rollover,
        HeadOnCollision,
        MultiVehicle
    }
    
    public enum AccidentSeverity
    {
        Minor,
        Major,
        Severe
    }
    
    public enum AccidentStatus
    {
        JustOccurred,
        EmergencyDispatched,
        EmergencyOnScene,
        Cleanup,
        Resolved
    }
} 