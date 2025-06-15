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
    /// Système de transport en commun intelligent
    /// </summary>
    public class PublicTransportSystem : IDisposable
    {
        private readonly List<BusRoute> _busRoutes = new List<BusRoute>();
        private readonly List<BusStop> _busStops = new List<BusStop>();
        private readonly List<ActiveBus> _activeBuses = new List<ActiveBus>();
        private readonly Random _random = new Random();
        
        private DateTime _lastBusSpawn = DateTime.MinValue;
        private DateTime _lastSystemUpdate = DateTime.MinValue;
        
        private const double BUS_SPAWN_INTERVAL = 120.0; // 2 minutes
        private const double SYSTEM_UPDATE_INTERVAL = 5.0; // 5 secondes
        private const int MAX_ACTIVE_BUSES = 6;
        
        public void Initialize()
        {
            CreateBusStops();
            CreateBusRoutes();
            Logger.Info("Public Transport System initialized");
        }
        
        public void Update(Vector3 playerPosition)
        {
            try
            {
                if ((DateTime.Now - _lastSystemUpdate).TotalSeconds >= SYSTEM_UPDATE_INTERVAL)
                {
                    UpdateActiveBuses(playerPosition);
                    ManageBusStops(playerPosition);
                    _lastSystemUpdate = DateTime.Now;
                }
                
                if ((DateTime.Now - _lastBusSpawn).TotalSeconds >= BUS_SPAWN_INTERVAL)
                {
                    SpawnNewBus(playerPosition);
                    _lastBusSpawn = DateTime.Now;
                }
                
                CleanupDistantBuses(playerPosition);
            }
            catch (Exception ex)
            {
                Logger.Error($"Public Transport System update error: {ex.Message}");
            }
        }
        
        private void CreateBusStops()
        {
            // Arrêts de bus dans Los Santos
            var stopLocations = new[]
            {
                // Centre-ville
                new Vector3(-800, -200, 20),
                new Vector3(-600, -400, 25),
                new Vector3(-400, -600, 30),
                new Vector3(-200, -800, 25),
                
                // Zone résidentielle
                new Vector3(-1500, -300, 30),
                new Vector3(-1600, -500, 32),
                new Vector3(-1400, -700, 28),
                
                // Zone commerciale
                new Vector3(-1000, -400, 40),
                new Vector3(-1200, -800, 35),
                new Vector3(-800, -1000, 30),
                
                // Aéroport et périphérie
                new Vector3(-1000, -2000, 15),
                new Vector3(-500, -1500, 25),
                new Vector3(-300, -1200, 22)
            };
            
            for (int i = 0; i < stopLocations.Length; i++)
            {
                var stop = new BusStop
                {
                    Id = i,
                    Position = stopLocations[i],
                    Name = GetBusStopName(i),
                    Radius = 15f,
                    WaitingPassengers = new List<Ped>()
                };
                
                _busStops.Add(stop);
            }
            
            Logger.Info($"Created {_busStops.Count} bus stops");
        }
        
        private string GetBusStopName(int id)
        {
            var names = new[]
            {
                "Centre-ville Sud", "Place Wilson", "Quartier des Affaires", "Gare Centrale",
                "Résidence Oak", "Villa Heights", "Suburban Plaza", 
                "Commerce Center", "City Hall", "Market District",
                "Airport Terminal", "Industrial Zone", "University Campus"
            };
            
            return id < names.Length ? names[id] : $"Arrêt {id + 1}";
        }
        
        private void CreateBusRoutes()
        {
            // Ligne 1: Centre-ville
            var route1 = new BusRoute
            {
                Id = 1,
                Name = "Ligne 1 - Centre",
                Color = "~b~",
                StopIds = new List<int> { 0, 1, 2, 3, 8, 9 },
                Frequency = TimeSpan.FromMinutes(8) // Bus toutes les 8 minutes
            };
            _busRoutes.Add(route1);
            
            // Ligne 2: Résidentielle
            var route2 = new BusRoute
            {
                Id = 2,
                Name = "Ligne 2 - Résidentiel",
                Color = "~g~",
                StopIds = new List<int> { 4, 5, 6, 7, 1 },
                Frequency = TimeSpan.FromMinutes(12)
            };
            _busRoutes.Add(route2);
            
            // Ligne 3: Express Aéroport
            var route3 = new BusRoute
            {
                Id = 3,
                Name = "Ligne 3 - Express",
                Color = "~r~",
                StopIds = new List<int> { 0, 3, 10, 11, 12 },
                Frequency = TimeSpan.FromMinutes(15)
            };
            _busRoutes.Add(route3);
            
            Logger.Info($"Created {_busRoutes.Count} bus routes");
        }
        
        private void SpawnNewBus(Vector3 playerPosition)
        {
            if (_activeBuses.Count >= MAX_ACTIVE_BUSES) return;
            
            // Choisir une ligne aléatoire
            var route = _busRoutes[_random.Next(_busRoutes.Count)];
            
            // Vérifier si un bus est déjà actif sur cette ligne
            if (_activeBuses.Any(b => b.Route.Id == route.Id)) return;
            
            // Trouver un arrêt de départ proche du joueur
            var startStop = FindNearestBusStop(playerPosition, route);
            if (startStop == null) return;
            
            // Spawn du bus
            var busPosition = startStop.Position + Vector3.RandomXY() * 20f;
            var bus = World.CreateVehicle(VehicleHash.Bus, busPosition);
            
            if (bus?.Exists() == true)
            {
                // Créer le conducteur
                var driver = World.CreatePed(PedHash.FreemodeMale01, busPosition);
                if (driver?.Exists() == true)
                {
                    driver.SetIntoVehicle(bus, VehicleSeat.Driver);
                    
                    var activeBus = new ActiveBus
                    {
                        Vehicle = bus,
                        Driver = driver,
                        Route = route,
                        CurrentStopIndex = route.StopIds.IndexOf(startStop.Id),
                        NextStopId = GetNextStopId(route, startStop.Id),
                        LastStopTime = DateTime.Now,
                        Passengers = new List<Ped>()
                    };
                    
                    // Spawn des passagers initiaux
                    SpawnBusPassengers(activeBus, 2 + _random.Next(4));
                    
                    _activeBuses.Add(activeBus);
                    
                    Logger.Info($"Bus spawned on {route.Name} at stop {startStop.Name}");
                    
                    // Notifier le joueur s'il est proche
                    if (playerPosition.DistanceTo(busPosition) < 100f)
                    {
                        Screen.ShowSubtitle($"{route.Color}{route.Name}~w~\nDirection: {GetDestinationName(route, activeBus.NextStopId)}", 4000);
                    }
                }
            }
        }
        
        private void UpdateActiveBuses(Vector3 playerPosition)
        {
            var busesToRemove = new List<ActiveBus>();
            
            foreach (var activeBus in _activeBuses)
            {
                if (activeBus.Vehicle?.Exists() != true || activeBus.Driver?.Exists() != true)
                {
                    busesToRemove.Add(activeBus);
                    continue;
                }
                
                // Vérifier si le bus est trop loin du joueur
                if (playerPosition.DistanceTo(activeBus.Vehicle.Position) > 1500f)
                {
                    busesToRemove.Add(activeBus);
                    continue;
                }
                
                UpdateBusRoute(activeBus);
                CheckBusStopArrival(activeBus);
                ManageBusPassengers(activeBus);
            }
            
            foreach (var bus in busesToRemove)
            {
                RemoveBus(bus);
            }
        }
        
        private void UpdateBusRoute(ActiveBus activeBus)
        {
            var nextStop = _busStops.FirstOrDefault(s => s.Id == activeBus.NextStopId);
            if (nextStop == null) return;
            
            var distance = activeBus.Vehicle.Position.DistanceTo(nextStop.Position);
            
            if (distance > 30f)
            {
                // En route vers l'arrêt
                activeBus.Driver.Task.DriveTo(activeBus.Vehicle, nextStop.Position, 30f, 25f / 3.6f);
            }
            else if (distance < 15f && !activeBus.IsAtStop)
            {
                // Arrivée à l'arrêt
                activeBus.IsAtStop = true;
                activeBus.StopArrivalTime = DateTime.Now;
                
                // Arrêter le bus
                activeBus.Driver.Task.CruiseWithVehicle(activeBus.Vehicle, 0f, (VehicleDrivingFlags)786603);
                
                Logger.Info($"Bus arrived at stop {nextStop.Name}");
            }
        }
        
        private void CheckBusStopArrival(ActiveBus activeBus)
        {
            if (!activeBus.IsAtStop) return;
            
            var stopDuration = DateTime.Now - activeBus.StopArrivalTime;
            var requiredStopTime = TimeSpan.FromSeconds(15 + _random.Next(15)); // 15-30 secondes
            
            if (stopDuration >= requiredStopTime)
            {
                // Temps d'arrêt écoulé, continuer vers l'arrêt suivant
                var currentStop = _busStops.FirstOrDefault(s => s.Id == activeBus.NextStopId);
                if (currentStop != null)
                {
                    // Passagers qui descendent
                    HandlePassengerDisembark(activeBus, currentStop);
                    
                    // Passagers qui montènt
                    HandlePassengerBoard(activeBus, currentStop);
                    
                    // Passer à l'arrêt suivant
                    activeBus.CurrentStopIndex = (activeBus.CurrentStopIndex + 1) % activeBus.Route.StopIds.Count;
                    activeBus.NextStopId = activeBus.Route.StopIds[activeBus.CurrentStopIndex];
                    activeBus.IsAtStop = false;
                    activeBus.LastStopTime = DateTime.Now;
                    
                    var nextStop = _busStops.FirstOrDefault(s => s.Id == activeBus.NextStopId);
                    if (nextStop != null)
                    {
                        Logger.Info($"Bus departing to {nextStop.Name}");
                    }
                }
            }
        }
        
        private void HandlePassengerDisembark(ActiveBus activeBus, BusStop currentStop)
        {
            // 20-40% des passagers descendent à chaque arrêt
            var disembarkCount = (int)(activeBus.Passengers.Count * (0.2f + (float)_random.NextDouble() * 0.2f));
            
            for (int i = 0; i < disembarkCount && activeBus.Passengers.Count > 0; i++)
            {
                var passenger = activeBus.Passengers[_random.Next(activeBus.Passengers.Count)];
                if (passenger?.Exists() == true)
                {
                    // Faire sortir le passager du bus
                    passenger.Task.LeaveVehicle(activeBus.Vehicle, false);
                    
                    // Le faire marcher un peu puis disparaître
                    var walkTarget = currentStop.Position + Vector3.RandomXY() * 50f;
                    passenger.Task.GoTo(walkTarget);
                    
                    activeBus.Passengers.Remove(passenger);
                    
                    // Supprimer le passager après un délai
                    System.Threading.Tasks.Task.Delay(10000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (passenger?.Exists() == true)
                            {
                                passenger.Delete();
                            }
                        }
                        catch { }
                    });
                }
            }
        }
        
        private void HandlePassengerBoard(ActiveBus activeBus, BusStop currentStop)
        {
            // Passagers en attente montent dans le bus
            var availableSeats = 20 - activeBus.Passengers.Count; // Bus de 20 places
            var boardingPassengers = Math.Min(currentStop.WaitingPassengers.Count, availableSeats);
            
            for (int i = 0; i < boardingPassengers; i++)
            {
                var passenger = currentStop.WaitingPassengers[0];
                if (passenger?.Exists() == true)
                {
                    // Faire monter le passager
                    var seat = GetAvailableBusSeat(activeBus.Vehicle);
                    if (seat != VehicleSeat.None)
                    {
                        passenger.Task.EnterVehicle(activeBus.Vehicle, seat);
                        activeBus.Passengers.Add(passenger);
                    }
                }
                
                currentStop.WaitingPassengers.RemoveAt(0);
            }
            
            // Spawn de nouveaux passagers en attente
            if (_random.Next(100) < 40) // 40% de chance
            {
                SpawnWaitingPassengers(currentStop, 1 + _random.Next(3));
            }
        }
        
        private VehicleSeat GetAvailableBusSeat(Vehicle bus)
        {
            // Vérifier les sièges passagers (1-15 pour un bus)
            for (int i = 1; i <= 15; i++)
            {
                var seat = (VehicleSeat)i;
                if (bus.IsSeatFree(seat))
                {
                    return seat;
                }
            }
            
            return VehicleSeat.None;
        }
        
        private void SpawnBusPassengers(ActiveBus activeBus, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var passenger = World.CreatePed(GetRandomPedHash(), activeBus.Vehicle.Position);
                if (passenger?.Exists() == true)
                {
                    var seat = GetAvailableBusSeat(activeBus.Vehicle);
                    if (seat != VehicleSeat.None)
                    {
                        passenger.SetIntoVehicle(activeBus.Vehicle, seat);
                        activeBus.Passengers.Add(passenger);
                    }
                    else
                    {
                        passenger.Delete();
                        break;
                    }
                }
            }
        }
        
        private void SpawnWaitingPassengers(BusStop stop, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var passengerPos = stop.Position + Vector3.RandomXY() * 8f;
                var passenger = World.CreatePed(GetRandomPedHash(), passengerPos);
                
                if (passenger?.Exists() == true)
                {
                    // Animation d'attente
                    var waitAnimations = new[] { "WORLD_HUMAN_STAND_MOBILE", "WORLD_HUMAN_SMOKING", "WORLD_HUMAN_STAND_IMPATIENT" };
                    passenger.Task.StartScenario(waitAnimations[_random.Next(waitAnimations.Length)], 0);
                    
                    stop.WaitingPassengers.Add(passenger);
                }
            }
        }
        
        private void ManageBusStops(Vector3 playerPosition)
        {
            foreach (var stop in _busStops)
            {
                if (playerPosition.DistanceTo(stop.Position) > 200f) continue;
                
                // Nettoyer les passagers qui n'existent plus
                stop.WaitingPassengers.RemoveAll(p => p?.Exists() != true);
                
                // Spawn aléatoire de passagers
                if (stop.WaitingPassengers.Count < 5 && _random.Next(100) < 5) // 5% de chance
                {
                    SpawnWaitingPassengers(stop, 1);
                }
                
                // Afficher l'information de l'arrêt au joueur s'il est proche
                if (playerPosition.DistanceTo(stop.Position) < 25f)
                {
                    var nextBusInfo = GetNextBusInfo(stop);
                    if (!string.IsNullOrEmpty(nextBusInfo))
                    {
                        Screen.ShowSubtitle($"~y~{stop.Name}~w~\n{nextBusInfo}", 3000);
                    }
                }
            }
        }
        
        private string GetNextBusInfo(BusStop stop)
        {
            var routesAtStop = _busRoutes.Where(r => r.StopIds.Contains(stop.Id)).ToList();
            if (!routesAtStop.Any()) return "";
            
            var info = new List<string>();
            foreach (var route in routesAtStop)
            {
                var activeBus = _activeBuses.FirstOrDefault(b => b.Route.Id == route.Id);
                if (activeBus != null)
                {
                    var distance = activeBus.Vehicle.Position.DistanceTo(stop.Position);
                    var eta = (int)(distance / 15f * 60f); // Estimation en secondes
                    info.Add($"{route.Color}{route.Name}~w~: {eta}s");
                }
                else
                {
                    info.Add($"{route.Color}{route.Name}~w~: En attente");
                }
            }
            
            return string.Join("\n", info);
        }
        
        private void ManageBusPassengers(ActiveBus activeBus)
        {
            // Nettoyer les passagers qui n'existent plus
            activeBus.Passengers.RemoveAll(p => p?.Exists() != true);
        }
        
        private void CleanupDistantBuses(Vector3 playerPosition)
        {
            var distantBuses = _activeBuses.Where(b => 
                b.Vehicle?.Exists() == true && 
                playerPosition.DistanceTo(b.Vehicle.Position) > 2000f).ToList();
                
            foreach (var bus in distantBuses)
            {
                RemoveBus(bus);
            }
        }
        
        private void RemoveBus(ActiveBus activeBus)
        {
            try
            {
                // Supprimer les passagers
                foreach (var passenger in activeBus.Passengers)
                {
                    if (passenger?.Exists() == true)
                    {
                        passenger.Delete();
                    }
                }
                
                // Supprimer le conducteur et le véhicule
                if (activeBus.Driver?.Exists() == true)
                {
                    activeBus.Driver.Delete();
                }
                
                if (activeBus.Vehicle?.Exists() == true)
                {
                    activeBus.Vehicle.Delete();
                }
                
                _activeBuses.Remove(activeBus);
                Logger.Info($"Bus removed from {activeBus.Route.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error removing bus: {ex.Message}");
            }
        }
        
        // Méthodes utilitaires
        private BusStop FindNearestBusStop(Vector3 position, BusRoute route)
        {
            return _busStops
                .Where(s => route.StopIds.Contains(s.Id))
                .OrderBy(s => s.Position.DistanceTo(position))
                .FirstOrDefault();
        }
        
        private int GetNextStopId(BusRoute route, int currentStopId)
        {
            var currentIndex = route.StopIds.IndexOf(currentStopId);
            var nextIndex = (currentIndex + 1) % route.StopIds.Count;
            return route.StopIds[nextIndex];
        }
        
        private string GetDestinationName(BusRoute route, int stopId)
        {
            var stop = _busStops.FirstOrDefault(s => s.Id == stopId);
            return stop?.Name ?? "Destination inconnue";
        }
        
        private PedHash GetRandomPedHash()
        {
            var peds = new[] { PedHash.Business01AMY, PedHash.Downtown01AMY, PedHash.Eastsa01AMY,
                             PedHash.Eastsa02AFY, PedHash.Business01AFY,
                             PedHash.Genfat01AMM, PedHash.Genfat02AMM, PedHash.Hipster01AMY };
            return peds[_random.Next(peds.Length)];
        }
        
        public void Dispose()
        {
            foreach (var bus in _activeBuses.ToList())
            {
                RemoveBus(bus);
            }
            
            // Nettoyer les passagers en attente
            foreach (var stop in _busStops)
            {
                foreach (var passenger in stop.WaitingPassengers)
                {
                    if (passenger?.Exists() == true)
                    {
                        passenger.Delete();
                    }
                }
                stop.WaitingPassengers.Clear();
            }
            
            _activeBuses.Clear();
            _busRoutes.Clear();
            _busStops.Clear();
        }
    }
    
    /// <summary>
    /// Ligne de bus
    /// </summary>
    public class BusRoute
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Color { get; set; } = "~w~";
        public List<int> StopIds { get; set; } = new List<int>();
        public TimeSpan Frequency { get; set; }
    }
    
    /// <summary>
    /// Arrêt de bus
    /// </summary>
    public class BusStop
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public Vector3 Position { get; set; }
        public float Radius { get; set; }
        public List<Ped> WaitingPassengers { get; set; } = new List<Ped>();
    }
    
    /// <summary>
    /// Bus actif en circulation
    /// </summary>
    public class ActiveBus
    {
        public Vehicle Vehicle { get; set; }
        public Ped Driver { get; set; }
        public BusRoute Route { get; set; }
        public int CurrentStopIndex { get; set; }
        public int NextStopId { get; set; }
        public bool IsAtStop { get; set; }
        public DateTime StopArrivalTime { get; set; }
        public DateTime LastStopTime { get; set; }
        public List<Ped> Passengers { get; set; } = new List<Ped>();
    }
} 