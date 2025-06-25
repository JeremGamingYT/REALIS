using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Common;

namespace REALIS.Events
{
    /// <summary>
    /// Module gérant des événements aléatoires pour rendre le monde plus vivant
    /// </summary>
    public class RandomEventModule : IModule
    {
        private readonly List<RandomEvent> _activeEvents = new List<RandomEvent>();
        private readonly List<WeatherEvent> _activeWeatherEvents = new List<WeatherEvent>();
        private readonly Random _rng = new Random();
        
        private DateTime _lastEventCheck = DateTime.MinValue;
        private DateTime _lastWeatherCheck = DateTime.MinValue;
        private readonly TimeSpan _eventCheckInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _weatherCheckInterval = TimeSpan.FromMinutes(5);

        // Configuration des événements
        private readonly Dictionary<RandomEventType, EventConfig> _eventConfigs = new Dictionary<RandomEventType, EventConfig>();

        public void Initialize()
        {
            InitializeEventConfigs();
        }

        public void Update()
        {
            var player = Game.Player.Character;
            if (!player.Exists()) return;

            // Vérifier et générer des événements aléatoires
            CheckForRandomEvents(player);
            
            // Gérer les événements météorologiques
            ManageWeatherEvents();
            
            // Mettre à jour les événements actifs
            UpdateActiveEvents();
            
            // Nettoyage périodique
            CleanupExpiredEvents();
        }

        public void Dispose()
        {
            foreach (var randomEvent in _activeEvents)
            {
                CleanupEvent(randomEvent);
            }
            _activeEvents.Clear();

            foreach (var weatherEvent in _activeWeatherEvents)
            {
                CleanupWeatherEvent(weatherEvent);
            }
            _activeWeatherEvents.Clear();
        }

        private void InitializeEventConfigs()
        {
            // Configuration de chaque type d'événement
            _eventConfigs[RandomEventType.StreetFight] = new EventConfig
            {
                Probability = 0.15f,
                Duration = TimeSpan.FromMinutes(3),
                RequiredTime = TimeRange.AnyTime,
                RequiredWeather = new[] { Weather.Clear, Weather.Clouds, Weather.ExtraSunny }
            };

            _eventConfigs[RandomEventType.DrugDeal] = new EventConfig
            {
                Probability = 0.1f,
                Duration = TimeSpan.FromMinutes(5),
                RequiredTime = new TimeRange(22, 5), // Nuit
                RequiredWeather = new[] { Weather.Clear, Weather.Clouds }
            };

            _eventConfigs[RandomEventType.PoliceChase] = new EventConfig
            {
                Probability = 0.12f,
                Duration = TimeSpan.FromMinutes(4),
                RequiredTime = TimeRange.AnyTime,
                RequiredWeather = new[] { Weather.Clear, Weather.Clouds, Weather.ExtraSunny, Weather.Overcast }
            };

            _eventConfigs[RandomEventType.FlashMob] = new EventConfig
            {
                Probability = 0.05f,
                Duration = TimeSpan.FromMinutes(8),
                RequiredTime = new TimeRange(12, 18), // Jour
                RequiredWeather = new[] { Weather.Clear, Weather.ExtraSunny }
            };

            _eventConfigs[RandomEventType.StreetPerformer] = new EventConfig
            {
                Probability = 0.2f,
                Duration = TimeSpan.FromMinutes(10),
                RequiredTime = new TimeRange(10, 20),
                RequiredWeather = new[] { Weather.Clear, Weather.ExtraSunny, Weather.Clouds }
            };

            _eventConfigs[RandomEventType.PowerOutage] = new EventConfig
            {
                Probability = 0.03f,
                Duration = TimeSpan.FromMinutes(15),
                RequiredTime = TimeRange.AnyTime,
                RequiredWeather = new[] { Weather.ThunderStorm, Weather.Raining }
            };

            _eventConfigs[RandomEventType.Protest] = new EventConfig
            {
                Probability = 0.08f,
                Duration = TimeSpan.FromMinutes(20),
                RequiredTime = new TimeRange(9, 17), // Heures d'affaires
                RequiredWeather = new[] { Weather.Clear, Weather.Clouds, Weather.ExtraSunny }
            };

            _eventConfigs[RandomEventType.CarShow] = new EventConfig
            {
                Probability = 0.06f,
                Duration = TimeSpan.FromMinutes(25),
                RequiredTime = new TimeRange(14, 18), // Week-end après-midi (simulé)
                RequiredWeather = new[] { Weather.Clear, Weather.ExtraSunny }
            };
        }

        private void CheckForRandomEvents(Ped player)
        {
            if (DateTime.Now - _lastEventCheck < _eventCheckInterval) return;

            var currentWeather = World.Weather;
            var currentHour = World.CurrentTimeOfDay.Hours;

            foreach (var eventType in _eventConfigs.Keys)
            {
                var config = _eventConfigs[eventType];
                
                // Vérifier les conditions
                if (!config.RequiredTime.IsActive(currentHour)) continue;
                if (!config.RequiredWeather.Contains(currentWeather)) continue;
                
                // Vérifier si l'événement peut se déclencher
                if (_rng.NextDouble() < config.Probability * 0.01f) // Ajuster la probabilité
                {
                    GenerateRandomEvent(eventType, player.Position);
                    break; // Un seul événement à la fois
                }
            }

            _lastEventCheck = DateTime.Now;
        }

        private void GenerateRandomEvent(RandomEventType eventType, Vector3 playerPosition)
        {
            // Éviter de spawner trop près du joueur
            var eventPosition = GetRandomNearbyPosition(playerPosition, 50f, 200f);
            
            var randomEvent = new RandomEvent
            {
                Id = Guid.NewGuid(),
                Type = eventType,
                Position = eventPosition,
                StartTime = DateTime.Now,
                Duration = _eventConfigs[eventType].Duration,
                IsActive = true
            };

            CreateEventScene(randomEvent);
            _activeEvents.Add(randomEvent);

            // Notification au joueur
            var eventName = GetEventName(eventType);
            GTA.UI.Notification.Show($"~o~Événement: {eventName} en cours dans la zone!");
        }

        private void CreateEventScene(RandomEvent randomEvent)
        {
            switch (randomEvent.Type)
            {
                case RandomEventType.StreetFight:
                    CreateStreetFightScene(randomEvent);
                    break;
                case RandomEventType.DrugDeal:
                    CreateDrugDealScene(randomEvent);
                    break;
                case RandomEventType.PoliceChase:
                    CreatePoliceChaseScene(randomEvent);
                    break;
                case RandomEventType.FlashMob:
                    CreateFlashMobScene(randomEvent);
                    break;
                case RandomEventType.StreetPerformer:
                    CreateStreetPerformerScene(randomEvent);
                    break;
                case RandomEventType.PowerOutage:
                    CreatePowerOutageScene(randomEvent);
                    break;
                case RandomEventType.Protest:
                    CreateProtestScene(randomEvent);
                    break;
                case RandomEventType.CarShow:
                    CreateCarShowScene(randomEvent);
                    break;
            }

            // Ajouter blip pour l'événement
            var blip = World.CreateBlip(randomEvent.Position);
            blip.Sprite = GetEventBlipSprite(randomEvent.Type);
            blip.Color = BlipColor.Yellow;
            blip.Scale = 0.9f;
            blip.Name = GetEventName(randomEvent.Type);
            randomEvent.Blip = blip;
        }

        private void CreateStreetFightScene(RandomEvent randomEvent)
        {
            var position = randomEvent.Position;
            
            // Créer combattants
            var fighter1 = World.CreateRandomPed(position);
            var fighter2 = World.CreateRandomPed(position + Vector3.RandomXY() * 3f);
            
            if (fighter1 != null && fighter1.Exists() && fighter2 != null && fighter2.Exists())
            {
                fighter1.Task.FightAgainst(fighter2);
                fighter2.Task.FightAgainst(fighter1);
                randomEvent.InvolvedPeds.AddRange(new[] { fighter1, fighter2 });
                
                // Créer spectateurs
                for (int i = 0; i < _rng.Next(3, 8); i++)
                {
                    var spectator = World.CreateRandomPed(position + Vector3.RandomXY() * _rng.Next(8, 15));
                    if (spectator != null && spectator.Exists())
                    {
                        spectator.Task.LookAt(fighter1);
                        randomEvent.InvolvedPeds.Add(spectator);
                    }
                }
            }
        }

        private void CreateDrugDealScene(RandomEvent randomEvent)
        {
            var position = randomEvent.Position;
            
            // Dealer
            var dealer = World.CreateRandomPed(position);
            // Client
            var client = World.CreateRandomPed(position + Vector3.RandomXY() * 2f);
            
            if (dealer != null && dealer.Exists() && client != null && client.Exists())
            {
                dealer.Weapons.Give(WeaponHash.Pistol, 30, false, false);
                client.Task.ChatTo(dealer);
                dealer.Task.LookAt(client);
                
                randomEvent.InvolvedPeds.AddRange(new[] { dealer, client });
                
                // Guetteur potentiel
                if (_rng.Next(100) < 60)
                {
                    var lookout = World.CreateRandomPed(position + Vector3.RandomXY() * 10f);
                    if (lookout != null && lookout.Exists())
                    {
                        lookout.Task.LookAt(dealer);
                        randomEvent.InvolvedPeds.Add(lookout);
                    }
                }
            }
        }

        private void CreatePoliceChaseScene(RandomEvent randomEvent)
        {
            var position = randomEvent.Position;
            var roadPos = World.GetNextPositionOnStreet(position);
            
            // Véhicule en fuite
            var suspectVehicle = World.CreateRandomVehicle(roadPos);
            if (suspectVehicle != null && suspectVehicle.Exists())
            {
                suspectVehicle.IsEngineRunning = true;
                suspectVehicle.MaxSpeed = 35f; // Vitesse élevée
                randomEvent.InvolvedVehicles.Add(suspectVehicle);
                
                if (suspectVehicle.Driver != null)
                {
                    suspectVehicle.Driver.DrivingStyle = DrivingStyle.Rushed;
                    suspectVehicle.Driver.Task.FleeFrom(roadPos);
                    randomEvent.InvolvedPeds.Add(suspectVehicle.Driver);
                }
                
                // Voitures de police
                for (int i = 0; i < 2; i++)
                {
                    var policeCar = World.CreateVehicle(new Model(VehicleHash.Police), 
                        roadPos + Vector3.RandomXY() * 15f);
                    if (policeCar != null && policeCar.Exists())
                    {
                        policeCar.IsEngineRunning = true;
                        Function.Call(Hash.SET_VEHICLE_SIREN, policeCar, true);
                        
                        if (policeCar.Driver != null)
                        {
                                            policeCar.Driver.VehicleDrivingFlags = VehicleDrivingFlags.None;
                policeCar.Driver.Task.FollowNavMeshTo(suspectVehicle.Position);
                        }
                        
                        randomEvent.InvolvedVehicles.Add(policeCar);
                    }
                }
            }
        }

        private void CreateFlashMobScene(RandomEvent randomEvent)
        {
            var position = randomEvent.Position;
            var center = World.GetNextPositionOnSidewalk(position);
            
            // Créer plusieurs participants
            for (int i = 0; i < _rng.Next(8, 15); i++)
            {
                var participant = World.CreateRandomPed(center + Vector3.RandomXY() * 8f);
                if (participant != null && participant.Exists())
                {
                    // Animations de danse
                    participant.Task.PlayAnimation("mini@strip_club@lap_dance@ld_girl_a_song_a_p1", 
                        "ld_girl_a_song_a_p1_f", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                    randomEvent.InvolvedPeds.Add(participant);
                }
            }
            
            // Spectateurs
            for (int i = 0; i < _rng.Next(5, 12); i++)
            {
                var spectator = World.CreateRandomPed(center + Vector3.RandomXY() * 15f);
                if (spectator != null && spectator.Exists())
                {
                    spectator.Task.LookAt(center);
                    randomEvent.InvolvedPeds.Add(spectator);
                }
            }
        }

        private void CreateStreetPerformerScene(RandomEvent randomEvent)
        {
            var position = randomEvent.Position;
            var performanceSpot = World.GetNextPositionOnSidewalk(position);
            
            // Artiste de rue
            var performer = World.CreateRandomPed(performanceSpot);
            if (performer != null && performer.Exists())
            {
                // Animation de musicien
                performer.Task.PlayAnimation("amb@world_human_musician@guitar@male@base", 
                    "base", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                randomEvent.InvolvedPeds.Add(performer);
                
                // Audience
                for (int i = 0; i < _rng.Next(4, 10); i++)
                {
                    var audience = World.CreateRandomPed(performanceSpot + Vector3.RandomXY() * _rng.Next(5, 12));
                    if (audience != null && audience.Exists())
                    {
                        audience.Task.LookAt(performer);
                        randomEvent.InvolvedPeds.Add(audience);
                    }
                }
            }
        }

        private void CreatePowerOutageScene(RandomEvent randomEvent)
        {
            var position = randomEvent.Position;
            
            // Simuler panne d'électricité en éteignant les lumières dans la zone
            Function.Call(Hash.SET_ARTIFICIAL_LIGHTS_STATE, true);
            
            // Créer des gens confus
            for (int i = 0; i < _rng.Next(3, 8); i++)
            {
                var confusedPed = World.CreateRandomPed(position + Vector3.RandomXY() * 20f);
                if (confusedPed != null && confusedPed.Exists())
                {
                    confusedPed.Task.Wander();
                    // Donner une lampe de poche
                    confusedPed.Weapons.Give(WeaponHash.Flashlight, 1, false, true);
                    randomEvent.InvolvedPeds.Add(confusedPed);
                }
            }
            
            randomEvent.CustomData["PowerOutage"] = true;
        }

        private void CreateProtestScene(RandomEvent randomEvent)
        {
            var position = randomEvent.Position;
            var protestCenter = World.GetNextPositionOnSidewalk(position);
            
            // Manifestants
            for (int i = 0; i < _rng.Next(10, 20); i++)
            {
                var protester = World.CreateRandomPed(protestCenter + Vector3.RandomXY() * 12f);
                if (protester != null && protester.Exists())
                {
                    // Animation de protestation
                    protester.Task.PlayAnimation("weapons@first_person@aim_rng@generic@projectile@sticky_bomb@", 
                        "plant_floor", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                    randomEvent.InvolvedPeds.Add(protester);
                }
            }
            
            // Police pour maintenir l'ordre
            for (int i = 0; i < 3; i++)
            {
                var officer = World.CreatePed(new Model(PedHash.Cop01SMY), 
                    protestCenter + Vector3.RandomXY() * 15f);
                if (officer != null && officer.Exists())
                {
                    officer.Task.LookAt(protestCenter);
                    randomEvent.InvolvedPeds.Add(officer);
                }
            }
        }

        private void CreateCarShowScene(RandomEvent randomEvent)
        {
            var position = randomEvent.Position;
            var showArea = World.GetNextPositionOnStreet(position);
            
            // Voitures d'exposition
            var carModels = new[] 
            { 
                VehicleHash.Adder, VehicleHash.EntityXF, VehicleHash.Zentorno, 
                VehicleHash.T20, VehicleHash.Osiris 
            };
            
            for (int i = 0; i < 5; i++)
            {
                var model = carModels[_rng.Next(carModels.Length)];
                var showCar = World.CreateVehicle(new Model(model), 
                    showArea + new Vector3(i * 8f, 0f, 0f));
                if (showCar != null && showCar.Exists())
                {
                    showCar.IsEngineRunning = false;
                    showCar.LockStatus = VehicleLockStatus.CannotEnter;
                    showCar.DirtLevel = 0f; // Voitures parfaitement propres
                    randomEvent.InvolvedVehicles.Add(showCar);
                }
            }
            
            // Visiteurs
            for (int i = 0; i < _rng.Next(8, 15); i++)
            {
                var visitor = World.CreateRandomPed(showArea + Vector3.RandomXY() * 20f);
                if (visitor != null && visitor.Exists())
                {
                    visitor.Task.Wander();
                    randomEvent.InvolvedPeds.Add(visitor);
                }
            }
        }

        private void ManageWeatherEvents()
        {
            if (DateTime.Now - _lastWeatherCheck < _weatherCheckInterval) return;

            var currentWeather = World.Weather;
            
            // Événements liés à la météo
            if (currentWeather == Weather.ThunderStorm && _rng.Next(100) < 20)
            {
                CreateLightningEvent();
            }
            
            if (currentWeather == Weather.Raining && _rng.Next(100) < 15)
            {
                CreateFloodEvent();
            }

            _lastWeatherCheck = DateTime.Now;
        }

        private void CreateLightningEvent()
        {
            var player = Game.Player.Character;
            var lightningPos = player.Position + Vector3.RandomXY() * _rng.Next(100, 300);
            
            // Effet de foudre
            World.ForceLightningFlash();
            
            // Son de tonnerre (délayé)
            Function.Call(Hash.PLAY_SOUND_FROM_COORD, -1, "Thunder", 
                lightningPos.X, lightningPos.Y, lightningPos.Z, "WEATHER_SOUNDSET", false, 0, false);
                
            GTA.UI.Notification.Show("~p~Éclair violent dans la zone!");
        }

        private void CreateFloodEvent()
        {
            var player = Game.Player.Character;
            var floodArea = player.Position + Vector3.RandomXY() * _rng.Next(50, 150);
            
            // Créer des véhicules "coincés" dans l'inondation
            for (int i = 0; i < 3; i++)
            {
                var stuckVehicle = World.CreateRandomVehicle(floodArea + Vector3.RandomXY() * 20f);
                if (stuckVehicle != null && stuckVehicle.Exists())
                {
                    stuckVehicle.EngineHealth = 100; // Moteur noyé
                    stuckVehicle.IsEngineRunning = false;
                    
                    if (stuckVehicle.Driver != null)
                    {
                        stuckVehicle.Driver.Task.LeaveVehicle();
                    }
                }
            }
            
            GTA.UI.Notification.Show("~b~Inondation signalée dans la zone!");
        }

        private void UpdateActiveEvents()
        {
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                var randomEvent = _activeEvents[i];
                
                if (DateTime.Now - randomEvent.StartTime > randomEvent.Duration)
                {
                    CleanupEvent(randomEvent);
                    _activeEvents.RemoveAt(i);
                }
            }
        }

        private void CleanupExpiredEvents()
        {
            // Nettoyer les événements météo
            for (int i = _activeWeatherEvents.Count - 1; i >= 0; i--)
            {
                var weatherEvent = _activeWeatherEvents[i];
                if (DateTime.Now - weatherEvent.StartTime > weatherEvent.Duration)
                {
                    CleanupWeatherEvent(weatherEvent);
                    _activeWeatherEvents.RemoveAt(i);
                }
            }
        }

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

            return center + offset;
        }

        private string GetEventName(RandomEventType eventType)
        {
            return eventType switch
            {
                RandomEventType.StreetFight => "Bagarre de rue",
                RandomEventType.DrugDeal => "Transaction suspecte",
                RandomEventType.PoliceChase => "Poursuite policière",
                RandomEventType.FlashMob => "Flash mob",
                RandomEventType.StreetPerformer => "Artiste de rue",
                RandomEventType.PowerOutage => "Panne d'électricité",
                RandomEventType.Protest => "Manifestation",
                RandomEventType.CarShow => "Exposition automobile",
                _ => "Événement"
            };
        }

        private BlipSprite GetEventBlipSprite(RandomEventType eventType)
        {
            return eventType switch
            {
                RandomEventType.StreetFight => BlipSprite.Shield,
                RandomEventType.DrugDeal => BlipSprite.Package,
                RandomEventType.PoliceChase => BlipSprite.PoliceStation,
                RandomEventType.FlashMob => BlipSprite.Friend,
                RandomEventType.StreetPerformer => BlipSprite.Store,
                RandomEventType.PowerOutage => BlipSprite.Repair,
                RandomEventType.Protest => BlipSprite.Information,
                RandomEventType.CarShow => BlipSprite.Garage,
                _ => BlipSprite.Standard
            };
        }

        private void CleanupEvent(RandomEvent randomEvent)
        {
            // Nettoyer blip
            if (randomEvent.Blip != null && randomEvent.Blip.Exists())
            {
                randomEvent.Blip.Delete();
            }

            // Nettoyer les véhicules impliqués
            foreach (var vehicle in randomEvent.InvolvedVehicles.Where(v => v != null && v.Exists()))
            {
                if (_rng.Next(100) < 60) // 60% de chance de nettoyer
                {
                    vehicle.Delete();
                }
            }

            // Nettoyer les PNJ impliqués
            foreach (var ped in randomEvent.InvolvedPeds.Where(p => p != null && p.Exists()))
            {
                if (_rng.Next(100) < 70) // 70% de chance de nettoyer
                {
                    ped.Delete();
                }
            }

            // Restaurer l'éclairage si panne d'électricité
            if (randomEvent.CustomData.ContainsKey("PowerOutage"))
            {
                Function.Call(Hash.SET_ARTIFICIAL_LIGHTS_STATE, false);
            }
        }

        private void CleanupWeatherEvent(WeatherEvent weatherEvent)
        {
            // Nettoyer selon le type d'événement météo
            // (Implementation spécifique selon les besoins)
        }

        #endregion
    }

    #region Data Classes

    public class RandomEvent
    {
        public Guid Id { get; set; }
        public RandomEventType Type { get; set; }
        public Vector3 Position { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsActive { get; set; }
        public List<Vehicle> InvolvedVehicles { get; set; } = new List<Vehicle>();
        public List<Ped> InvolvedPeds { get; set; } = new List<Ped>();
        public Blip Blip { get; set; }
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }

    public class WeatherEvent
    {
        public Guid Id { get; set; }
        public WeatherEventType Type { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public Vector3 Position { get; set; }
    }

    public class EventConfig
    {
        public float Probability { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeRange RequiredTime { get; set; }
        public Weather[] RequiredWeather { get; set; }
    }

    public enum RandomEventType
    {
        StreetFight,
        DrugDeal,
        PoliceChase,
        FlashMob,
        StreetPerformer,
        PowerOutage,
        Protest,
        CarShow
    }

    public enum WeatherEventType
    {
        Lightning,
        Flood,
        WindStorm
    }

    #endregion
}