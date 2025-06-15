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
    /// Gestionnaire des heures de pointe et embouteillages
    /// </summary>
    public class RushHourManager : IDisposable
    {
        private readonly List<TrafficJamZone> _activeJams = new List<TrafficJamZone>();
        private readonly List<Vector3> _commonJamLocations = new List<Vector3>();
        private readonly Random _random = new Random();
        
        private DateTime _lastJamCheck = DateTime.MinValue;
        private DateTime _lastTrafficUpdate = DateTime.MinValue;
        
        private const double JAM_CHECK_INTERVAL = 60.0; // 1 minute
        private const double TRAFFIC_UPDATE_INTERVAL = 10.0; // 10 secondes
        private const int MAX_ACTIVE_JAMS = 5;
        
        // Heures de pointe
        private readonly int[] MORNING_RUSH = { 7, 8, 9 };
        private readonly int[] EVENING_RUSH = { 17, 18, 19 };
        private readonly int[] WEEKEND_BUSY = { 14, 15, 16 };
        
        public void Initialize()
        {
            InitializeJamLocations();
            Logger.Info("Rush Hour Manager initialized");
        }
        
        public void Update()
        {
            try
            {
                CheckForNewTrafficJams();
                UpdateActiveTrafficJams();
                ManageTrafficDensity();
                CleanupOldJams();
            }
            catch (Exception ex)
            {
                Logger.Error($"Rush Hour Manager update error: {ex.Message}");
            }
        }
        
        private void InitializeJamLocations()
        {
            // Emplacements typiques d'embouteillages à Los Santos
            _commonJamLocations.AddRange(new[]
            {
                // Centre-ville
                new Vector3(-800, -200, 20),
                new Vector3(-600, -300, 35),
                new Vector3(-400, -500, 30),
                
                // Autoroutes
                new Vector3(-1500, 300, 50),
                new Vector3(-2000, 0, 30),
                new Vector3(-1000, -1500, 40),
                
                // Intersections importantes
                new Vector3(-200, -800, 25),
                new Vector3(-1200, -800, 45),
                new Vector3(-500, -1200, 30),
                
                // Ponts
                new Vector3(-300, -100, 28),
                new Vector3(-700, -400, 32)
            });
        }
        
        private void CheckForNewTrafficJams()
        {
            if ((DateTime.Now - _lastJamCheck).TotalSeconds < JAM_CHECK_INTERVAL) return;
            
            _lastJamCheck = DateTime.Now;
            
            if (_activeJams.Count >= MAX_ACTIVE_JAMS) return;
            
            var jamChance = CalculateJamProbability();
            
            if (_random.Next(100) < jamChance)
            {
                CreateTrafficJam();
            }
        }
        
        private int CalculateJamProbability()
        {
            var currentHour = GTA.Chrono.GameClock.Hour;
            var currentDay = DateTime.Now.DayOfWeek;
            
            int baseChance = 5; // 5% base
            
            // Heures de pointe en semaine
            if (currentDay != DayOfWeek.Saturday && currentDay != DayOfWeek.Sunday)
            {
                if (MORNING_RUSH.Contains(currentHour) || EVENING_RUSH.Contains(currentHour))
                {
                    baseChance = 40; // 40% pendant les heures de pointe
                }
                else if (currentHour >= 10 && currentHour <= 16)
                {
                    baseChance = 15; // 15% en journée
                }
            }
            // Weekend
            else if (WEEKEND_BUSY.Contains(currentHour))
            {
                baseChance = 25; // 25% weekend après-midi
            }
            
            // Modifier selon la météo
            var weather = World.Weather;
            if (weather == Weather.Raining || weather == Weather.ThunderStorm)
            {
                baseChance = (int)(baseChance * 1.5f);
            }
            
            return baseChance;
        }
        
        private void CreateTrafficJam()
        {
            var jamLocation = SelectJamLocation();
            if (jamLocation == Vector3.Zero) return;
            
            var jam = new TrafficJamZone
            {
                Center = jamLocation,
                Radius = 50f + _random.Next(50), // 50-100m radius
                Intensity = DetermineJamIntensity(),
                CreatedTime = DateTime.Now,
                Duration = TimeSpan.FromMinutes(5 + _random.Next(20)), // 5-25 minutes
                Cause = DetermineJamCause()
            };
            
            CreateJamVehicles(jam);
            _activeJams.Add(jam);
            
            Logger.Info($"Traffic jam created at {jamLocation} - Cause: {jam.Cause}, Intensity: {jam.Intensity}");
            
            // Notifier le joueur s'il est proche
            var playerPos = Game.Player.Character.Position;
            if (playerPos.DistanceTo(jamLocation) < 200f)
            {
                Screen.ShowSubtitle($"~o~EMBOUTEILLAGE~w~\n{GetJamDescription(jam)}", 5000);
            }
        }
        
        private Vector3 SelectJamLocation()
        {
            var playerPos = Game.Player.Character.Position;
            
            // Préférer les emplacements proches du joueur mais pas trop
            var suitableLocations = _commonJamLocations
                .Where(loc => 
                {
                    var distance = playerPos.DistanceTo(loc);
                    return distance > 100f && distance < 1000f;
                })
                .ToList();
            
            if (suitableLocations.Any())
            {
                return suitableLocations[_random.Next(suitableLocations.Count)];
            }
            
            // Générer une position aléatoire sur route
            for (int i = 0; i < 10; i++)
            {
                var angle = _random.NextDouble() * Math.PI * 2;
                var distance = 200f + _random.Next(500);
                
                var testPos = new Vector3(
                    playerPos.X + (float)(Math.Cos(angle) * distance),
                    playerPos.Y + (float)(Math.Sin(angle) * distance),
                    playerPos.Z
                );
                
                if (IsPositionOnRoad(testPos))
                {
                    return testPos;
                }
            }
            
            return Vector3.Zero;
        }
        
        private JamIntensity DetermineJamIntensity()
        {
            var roll = _random.Next(100);
            return roll switch
            {
                < 40 => JamIntensity.Light,
                < 75 => JamIntensity.Moderate,
                < 90 => JamIntensity.Heavy,
                _ => JamIntensity.Severe
            };
        }
        
        private JamCause DetermineJamCause()
        {
            var causes = (JamCause[])Enum.GetValues(typeof(JamCause));
            return causes[_random.Next(causes.Length)];
        }
        
        private void CreateJamVehicles(TrafficJamZone jam)
        {
            try
            {
                var vehicleCount = jam.Intensity switch
                {
                    JamIntensity.Light => 3 + _random.Next(3), // 3-5 véhicules
                    JamIntensity.Moderate => 6 + _random.Next(4), // 6-9 véhicules
                    JamIntensity.Heavy => 10 + _random.Next(5), // 10-14 véhicules
                    JamIntensity.Severe => 15 + _random.Next(5), // 15-19 véhicules
                    _ => 5
                };
                
                for (int i = 0; i < vehicleCount; i++)
                {
                    var spawnPos = jam.Center + Vector3.RandomXY() * jam.Radius;
                    var vehicleHash = GetRandomVehicleHash();
                    
                    var vehicle = World.CreateVehicle(vehicleHash, spawnPos);
                    if (vehicle?.Exists() == true)
                    {
                        // Créer un conducteur
                        var driver = World.CreatePed(GetRandomPedHash(), spawnPos);
                        if (driver?.Exists() == true)
                        {
                            driver.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                            
                            // Comportement d'embouteillage
                            ApplyJamBehavior(vehicle, driver, jam);
                        }
                        
                        jam.Vehicles.Add(vehicle);
                    }
                }
                
                Logger.Info($"Created {jam.Vehicles.Count} vehicles for traffic jam");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating jam vehicles: {ex.Message}");
            }
        }
        
        private void ApplyJamBehavior(Vehicle vehicle, Ped driver, TrafficJamZone jam)
        {
            // Vitesse très réduite
            var maxSpeed = jam.Intensity switch
            {
                JamIntensity.Light => 10f, // 10 km/h
                JamIntensity.Moderate => 5f, // 5 km/h
                JamIntensity.Heavy => 2f, // 2 km/h
                JamIntensity.Severe => 0f, // Arrêt complet
                _ => 5f
            };
            
            // Appliquer le comportement de conduite
            driver.Task.CruiseWithVehicle(vehicle, maxSpeed / 3.6f, (VehicleDrivingFlags)786603);
            
            // Certains véhicules klaxonnent
            if (_random.Next(5) == 0)
            {
                vehicle.SoundHorn(1000 + _random.Next(2000));
            }
            
            // Animation du conducteur (impatience)
            if (jam.Intensity >= JamIntensity.Heavy && _random.Next(3) == 0)
            {
                driver.Task.PlayAnimation("random@car_thief", "shoplift_high", 2f, 3000, AnimationFlags.None);
            }
        }
        
        private void UpdateActiveTrafficJams()
        {
            if ((DateTime.Now - _lastTrafficUpdate).TotalSeconds < TRAFFIC_UPDATE_INTERVAL) return;
            
            _lastTrafficUpdate = DateTime.Now;
            
            var jamsToRemove = new List<TrafficJamZone>();
            
            foreach (var jam in _activeJams)
            {
                var elapsed = DateTime.Now - jam.CreatedTime;
                
                // Vérifier si l'embouteillage doit se terminer
                if (elapsed >= jam.Duration)
                {
                    jamsToRemove.Add(jam);
                    continue;
                }
                
                // Mise à jour dynamique de l'intensité
                UpdateJamIntensity(jam, elapsed);
                
                // Mise à jour des véhicules dans l'embouteillage
                UpdateJamVehicles(jam);
            }
            
            foreach (var jam in jamsToRemove)
            {
                ResolveTrafficJam(jam);
            }
        }
        
        private void UpdateJamIntensity(TrafficJamZone jam, TimeSpan elapsed)
        {
            var progressPercent = elapsed.TotalMinutes / jam.Duration.TotalMinutes;
            
            // L'embouteillage s'atténue vers la fin
            if (progressPercent > 0.7) // Après 70% du temps
            {
                var originalIntensity = jam.Intensity;
                jam.Intensity = originalIntensity switch
                {
                    JamIntensity.Severe => JamIntensity.Heavy,
                    JamIntensity.Heavy => JamIntensity.Moderate,
                    JamIntensity.Moderate => JamIntensity.Light,
                    _ => jam.Intensity
                };
                
                if (jam.Intensity != originalIntensity)
                {
                    Logger.Info($"Traffic jam intensity reduced from {originalIntensity} to {jam.Intensity}");
                }
            }
        }
        
        private void UpdateJamVehicles(TrafficJamZone jam)
        {
            // Supprimer les véhicules qui n'existent plus
            jam.Vehicles.RemoveAll(v => v?.Exists() != true);
            
            // Mettre à jour le comportement des véhicules restants
            foreach (var vehicle in jam.Vehicles)
            {
                if (vehicle?.Driver?.Exists() == true)
                {
                    ApplyJamBehavior(vehicle, vehicle.Driver, jam);
                }
            }
        }
        
        private void ResolveTrafficJam(TrafficJamZone jam)
        {
            try
            {
                Logger.Info($"Resolving traffic jam at {jam.Center}");
                
                // Libérer progressivement les véhicules
                foreach (var vehicle in jam.Vehicles)
                {
                    if (vehicle?.Driver?.Exists() == true)
                    {
                        // Reprendre la conduite normale
                        vehicle.Driver.Task.CruiseWithVehicle(vehicle, 30f / 3.6f, (VehicleDrivingFlags)786603);
                        
                        // Supprimer le véhicule après un délai aléatoire
                        var delay = _random.Next(5000, 15000); // 5-15 secondes
                        System.Threading.Tasks.Task.Delay(delay).ContinueWith(_ =>
                        {
                            try
                            {
                                if (vehicle?.Exists() == true)
                                {
                                    vehicle.Delete();
                                }
                            }
                            catch { }
                        });
                    }
                }
                
                _activeJams.Remove(jam);
                
                // Notifier le joueur s'il est proche
                var playerPos = Game.Player.Character.Position;
                if (playerPos.DistanceTo(jam.Center) < 300f)
                {
                    Screen.ShowSubtitle("~g~CIRCULATION RÉTABLIE~w~\nTrafic fluide", 3000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error resolving traffic jam: {ex.Message}");
            }
        }
        
        private void ManageTrafficDensity()
        {
            try
            {
                var currentHour = GTA.Chrono.GameClock.Hour;
                var densityMultiplier = CalculateTrafficDensityMultiplier(currentHour);
                
                // Appliquer le multiplicateur de densité de trafic
                Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, densityMultiplier);
                Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, densityMultiplier);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error managing traffic density: {ex.Message}");
            }
        }
        
        private float CalculateTrafficDensityMultiplier(int hour)
        {
            var currentDay = DateTime.Now.DayOfWeek;
            
            // Densité de base
            float multiplier = 1.0f;
            
            // Heures de pointe en semaine
            if (currentDay != DayOfWeek.Saturday && currentDay != DayOfWeek.Sunday)
            {
                if (MORNING_RUSH.Contains(hour) || EVENING_RUSH.Contains(hour))
                {
                    multiplier = 2.0f; // Double densité
                }
                else if (hour >= 10 && hour <= 16)
                {
                    multiplier = 1.3f; // 30% de plus en journée
                }
                else if (hour >= 20 || hour <= 6)
                {
                    multiplier = 0.6f; // 40% de moins la nuit
                }
            }
            // Weekend
            else
            {
                if (WEEKEND_BUSY.Contains(hour))
                {
                    multiplier = 1.5f; // 50% de plus l'après-midi
                }
                else if (hour >= 22 || hour <= 8)
                {
                    multiplier = 0.4f; // 60% de moins la nuit
                }
            }
            
            return multiplier;
        }
        
        private void CleanupOldJams()
        {
            var cutoffTime = DateTime.Now.AddHours(-1);
            var oldJams = _activeJams.Where(j => j.CreatedTime < cutoffTime).ToList();
            
            foreach (var jam in oldJams)
            {
                ResolveTrafficJam(jam);
            }
        }
        
        // Méthodes utilitaires
        private string GetJamDescription(TrafficJamZone jam)
        {
            var intensity = jam.Intensity switch
            {
                JamIntensity.Light => "Ralentissements",
                JamIntensity.Moderate => "Trafic dense",
                JamIntensity.Heavy => "Embouteillage",
                JamIntensity.Severe => "Blocage total",
                _ => "Trafic"
            };
            
            var cause = jam.Cause switch
            {
                JamCause.Accident => "suite à accident",
                JamCause.Construction => "travaux en cours",
                JamCause.RushHour => "heure de pointe",
                JamCause.Event => "événement spécial",
                JamCause.Weather => "conditions météo",
                _ => ""
            };
            
            return $"{intensity} {cause}";
        }
        
        private bool IsPositionOnRoad(Vector3 position)
        {
            return Function.Call<bool>(Hash.IS_POINT_ON_ROAD, position.X, position.Y, position.Z, 0);
        }
        
        private VehicleHash GetRandomVehicleHash()
        {
            var vehicles = new[] { VehicleHash.Blista, VehicleHash.Premier, VehicleHash.Sultan, 
                                 VehicleHash.Taxi, VehicleHash.Ingot, VehicleHash.Stanier, 
                                 VehicleHash.Washington, VehicleHash.Fugitive };
            return vehicles[_random.Next(vehicles.Length)];
        }
        
        private PedHash GetRandomPedHash()
        {
            var peds = new[] { PedHash.Business01AMY, PedHash.Downtown01AMY, PedHash.Eastsa01AMY,
                             PedHash.Eastsa02AFY, PedHash.Business01AFY };
            return peds[_random.Next(peds.Length)];
        }
        
        public void Dispose()
        {
            foreach (var jam in _activeJams.ToList())
            {
                ResolveTrafficJam(jam);
            }
            
            _activeJams.Clear();
            _commonJamLocations.Clear();
        }
    }
    
    /// <summary>
    /// Zone d'embouteillage
    /// </summary>
    public class TrafficJamZone
    {
        public Vector3 Center { get; set; }
        public float Radius { get; set; }
        public JamIntensity Intensity { get; set; }
        public JamCause Cause { get; set; }
        public DateTime CreatedTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    }
    
    public enum JamIntensity
    {
        Light,      // Ralentissements
        Moderate,   // Trafic dense
        Heavy,      // Embouteillage
        Severe      // Blocage total
    }
    
    public enum JamCause
    {
        RushHour,
        Accident,
        Construction,
        Event,
        Weather
    }
} 