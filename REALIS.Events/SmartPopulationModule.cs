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
    /// Module gérant une population dynamique intelligente avec emplois du temps et interactions sociales
    /// </summary>
    public class SmartPopulationModule : IModule
    {
        private readonly List<SmartPed> _activePeds = new List<SmartPed>();
        private readonly List<SocialEvent> _activeSocialEvents = new List<SocialEvent>();
        private readonly List<PopulationZone> _populationZones = new List<PopulationZone>();
        private readonly Random _rng = new Random();
        
        private DateTime _lastPopulationUpdate = DateTime.MinValue;
        private DateTime _lastEventUpdate = DateTime.MinValue;
        private readonly TimeSpan _populationUpdateInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _eventUpdateInterval = TimeSpan.FromMinutes(2);

        // Contrôle de la densité de population
        private float _currentDensityMultiplier = 1.0f;

        public void Initialize()
        {
            InitializePopulationZones();
        }

        public void Update()
        {
            var player = Game.Player.Character;
            if (!player.Exists()) return;

            // Ajuster la densité selon l'heure
            AdjustPopulationDensity();
            
            // Gérer les zones de population
            ManagePopulationZones(player);
            
            // Mettre à jour les PNJ intelligents
            UpdateSmartPeds();
            
            // Gérer les événements sociaux
            ManageSocialEvents(player);
            
            // Nettoyage périodique
            CleanupExpiredElements();
        }

        public void Dispose()
        {
            foreach (var smartPed in _activePeds)
            {
                CleanupSmartPed(smartPed);
            }
            _activePeds.Clear();

            foreach (var socialEvent in _activeSocialEvents)
            {
                CleanupSocialEvent(socialEvent);
            }
            _activeSocialEvents.Clear();
        }

        private void InitializePopulationZones()
        {
            // Zone bureau - Downtown
            _populationZones.Add(new PopulationZone
            {
                Name = "District Financier",
                Center = new Vector3(-225f, -876f, 30f),
                Radius = 200f,
                Type = ZoneType.Business,
                ActiveHours = new TimeRange(8, 17),
                PedTypes = new[] 
                { 
                    PedHash.Business01AMY, PedHash.Business02AMY, PedHash.Business03AMY,
                    PedHash.Business01AFY, PedHash.Business02AFY 
                },
                MaxPeds = 15,
                ActivityLevel = 0.8f,
                WeatherSensitive = true
            });

            // Zone plage - Vespucci
            _populationZones.Add(new PopulationZone
            {
                Name = "Plage de Vespucci",
                Center = new Vector3(-1396f, -1020f, 2f),
                Radius = 300f,
                Type = ZoneType.Beach,
                ActiveHours = new TimeRange(10, 18),
                PedTypes = new[] 
                { 
                    PedHash.Beach01AMY, PedHash.Beach02AMY, PedHash.Tourist01AFY,
                    PedHash.Beach01AFY, PedHash.Cyclist01AMY 
                },
                MaxPeds = 20,
                ActivityLevel = 1.0f,
                WeatherSensitive = true
            });

            // Zone résidentielle - Vinewood Hills
            _populationZones.Add(new PopulationZone
            {
                Name = "Vinewood Hills",
                Center = new Vector3(-1308f, 448f, 100f),
                Radius = 250f,
                Type = ZoneType.Residential,
                ActiveHours = new TimeRange(6, 22),
                PedTypes = new[] 
                { 
                    PedHash.Hiker01AFY, PedHash.Hiker01AMY, PedHash.Runner01AFY,
                    PedHash.Runner02AMY, PedHash.Cyclist01AMY 
                },
                MaxPeds = 8,
                ActivityLevel = 0.4f,
                WeatherSensitive = false
            });

            // Zone nocturne - Bars et clubs
            _populationZones.Add(new PopulationZone
            {
                Name = "Vie Nocturne",
                Center = new Vector3(126f, -1278f, 29f),
                Radius = 150f,
                Type = ZoneType.Nightlife,
                ActiveHours = new TimeRange(21, 3),
                PedTypes = new[] 
                { 
                    PedHash.Business01AFY, PedHash.Business02AFM, PedHash.Business03AFY,
                    PedHash.Business04AFY, PedHash.Hipster01AFY 
                },
                MaxPeds = 12,
                ActivityLevel = 0.9f,
                WeatherSensitive = false
            });

            // Zone parc - Parcs publics
            _populationZones.Add(new PopulationZone
            {
                Name = "Parc Central",
                Center = new Vector3(-319f, -134f, 39f),
                Radius = 180f,
                Type = ZoneType.Park,
                ActiveHours = new TimeRange(6, 20),
                PedTypes = new[] 
                { 
                    PedHash.Runner01AFY, PedHash.Runner02AMY, PedHash.Cyclist01AMY,
                    PedHash.Hiker01AFY, PedHash.Yoga01AFY 
                },
                MaxPeds = 10,
                ActivityLevel = 0.6f,
                WeatherSensitive = true
            });
        }

        private void AdjustPopulationDensity()
        {
            var currentHour = GTA.Chrono.GameClock.Hour;
            var weather = Function.Call<int>(Hash.GET_PREV_WEATHER_TYPE_HASH_NAME);
            
            // Densité selon l'heure
            float hourMultiplier = GetHourDensityMultiplier(currentHour);
            
            // Densité selon la météo
            float weatherMultiplier = GetWeatherDensityMultiplier(weather);
            
            _currentDensityMultiplier = hourMultiplier * weatherMultiplier;
            
            // Appliquer à l'engine GTA
            World.SetAmbientPedDensityMultiplierThisFrame(_currentDensityMultiplier);
        }

        private void ManagePopulationZones(Ped player)
        {
            if (DateTime.Now - _lastPopulationUpdate < _populationUpdateInterval) return;

            var currentHour = GTA.Chrono.GameClock.Hour;
            
            foreach (var zone in _populationZones)
            {
                bool isActive = zone.ActiveHours.IsActive(currentHour);
                bool playerNearby = player.Position.DistanceTo(zone.Center) < zone.Radius * 1.5f;
                bool goodWeather = !zone.WeatherSensitive || IsGoodWeather();
                
                if (isActive && playerNearby && goodWeather)
                {
                    SpawnZonePeds(zone);
                }
                else
                {
                    ReduceZoneActivity(zone);
                }
            }
            
            _lastPopulationUpdate = DateTime.Now;
        }

        private void SpawnZonePeds(PopulationZone zone)
        {
            if (zone.CurrentPedCount >= zone.MaxPeds) return;

            var spawnChance = zone.ActivityLevel * _currentDensityMultiplier * 0.1f;
            if (_rng.NextDouble() < spawnChance)
            {
                var pedData = CreateSmartPed(zone);
                if (pedData != null)
                {
                    _activePeds.Add(pedData);
                }
            }
        }

        private SmartPed CreateSmartPed(PopulationZone zone)
        {
            var spawnPos = GetRandomPositionInZone(zone);
            var pedModel = zone.PedTypes[_rng.Next(zone.PedTypes.Length)];
            
            var ped = World.CreatePed(new Model(pedModel), spawnPos);
            if (ped == null || !ped.Exists()) return null;

            var smartPed = new SmartPed
            {
                Ped = ped,
                PedType = GetPedPersonality(zone.Type),
                Zone = zone,
                SpawnTime = DateTime.Now,
                Schedule = GenerateSchedule(zone.Type),
                CurrentActivity = ActivityType.Idle,
                Mood = (PedMood)_rng.Next(Enum.GetValues(typeof(PedMood)).Length),
                SocialLevel = _rng.Next(1, 10)
            };

            // Configuration initiale
            ConfigurePedForZone(smartPed, zone);
            zone.CurrentPedCount++;

            return smartPed;
        }

        private void ConfigurePedForZone(SmartPed smartPed, PopulationZone zone)
        {
            var ped = smartPed.Ped;
            
            switch (zone.Type)
            {
                case ZoneType.Business:
                    // Comportement professionnel
                    ped.Task.ChatTo(ped); // Appels téléphoniques
                    break;
                    
                case ZoneType.Beach:
                    // Activités de plage
                    var beachActivity = _rng.Next(4);
                    switch (beachActivity)
                    {
                        case 0:
                            ped.Task.PlayAnimation("amb@world_human_sunbathe@male@back@idle_a", 
                                "idle_a", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                            break;
                        case 1:
                            StartJogging(ped);
                            break;
                        case 2:
                            ped.Task.PlayAnimation("amb@world_human_yoga@male@base", 
                                "base_a", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                            break;
                        default:
                            ped.Task.Wander();
                            break;
                    }
                    break;
                    
                case ZoneType.Residential:
                    // Activités résidentielles
                    if (_rng.Next(100) < 40)
                        StartJogging(ped);
                    else
                        ped.Task.Wander();
                    break;
                    
                case ZoneType.Nightlife:
                    // Comportement festif
                    ped.Task.PlayAnimation("mini@strip_club@lap_dance@ld_girl_a_song_a_p1", 
                        "ld_girl_a_song_a_p1_f", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                    break;
                    
                case ZoneType.Park:
                    // Activités de parc
                    var parkActivity = _rng.Next(3);
                    switch (parkActivity)
                    {
                        case 0:
                            StartJogging(ped);
                            break;
                        case 1:
                            ped.Task.PlayAnimation("amb@world_human_picnic@male@idle_a", 
                                "idle_a", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                            break;
                        default:
                            ped.Task.Wander();
                            break;
                    }
                    break;
            }
        }

        private void UpdateSmartPeds()
        {
            var currentTime = DateTime.Now;
            
            for (int i = _activePeds.Count - 1; i >= 0; i--)
            {
                var smartPed = _activePeds[i];
                
                if (smartPed.Ped == null || !smartPed.Ped.Exists() || smartPed.Ped.IsDead)
                {
                    CleanupSmartPed(smartPed);
                    _activePeds.RemoveAt(i);
                    continue;
                }

                // Mettre à jour l'activité selon l'emploi du temps
                UpdatePedSchedule(smartPed, currentTime);
                
                // Interactions sociales aléatoires
                HandleSocialInteractions(smartPed);
                
                // Réactions à l'environnement
                HandleEnvironmentalReactions(smartPed);
            }
        }

        private void UpdatePedSchedule(SmartPed smartPed, DateTime currentTime)
        {
            var timeSpentInActivity = currentTime - smartPed.LastActivityChange;
            
            // Changer d'activité après un certain temps
            if (timeSpentInActivity > TimeSpan.FromMinutes(5))
            {
                var newActivity = GetNextActivity(smartPed);
                if (newActivity != smartPed.CurrentActivity)
                {
                    ChangeActivity(smartPed, newActivity);
                }
            }
        }

        private void ChangeActivity(SmartPed smartPed, ActivityType newActivity)
        {
            var ped = smartPed.Ped;
            ped.Task.ClearAll();
            
            switch (newActivity)
            {
                case ActivityType.Walking:
                    var walkTarget = smartPed.Zone.Center + Vector3.RandomXY() * smartPed.Zone.Radius * 0.8f;
                    ped.Task.FollowNavMeshTo(walkTarget);
                    break;
                    
                case ActivityType.Talking:
                    var nearbyPed = FindNearbyPed(ped, 10f);
                    if (nearbyPed != null)
                    {
                        ped.Task.ChatTo(nearbyPed);
                        // Créer un événement social
                        CreateConversation(smartPed, nearbyPed);
                    }
                    break;
                    
                case ActivityType.Exercising:
                    StartJogging(ped);
                    break;
                    
                case ActivityType.Resting:
                    ped.Task.PlayAnimation("amb@world_human_seat_wall@male@feet_air@idle_a", 
                        "idle_a", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                    break;
                    
                case ActivityType.PhoneCall:
                    ped.Task.PlayAnimation("cellphone@", "cellphone_call_listen_base", 
                        8f, -8f, 15000, AnimationFlags.Loop, 0f);
                    break;
                    
                default:
                    ped.Task.Wander();
                    break;
            }
            
            smartPed.CurrentActivity = newActivity;
            smartPed.LastActivityChange = DateTime.Now;
        }

        private void HandleSocialInteractions(SmartPed smartPed)
        {
            if (smartPed.SocialLevel > 7 && _rng.Next(1000) < 2)
            {
                // PNJ sociable - plus de chances d'interaction
                InitiateSocialEvent(smartPed);
            }
        }

        private void InitiateSocialEvent(SmartPed smartPed)
        {
            var eventType = (SocialEventType)_rng.Next(Enum.GetValues(typeof(SocialEventType)).Length);
            var socialEvent = new SocialEvent
            {
                Id = Guid.NewGuid(),
                Type = eventType,
                Position = smartPed.Ped.Position,
                StartTime = DateTime.Now,
                Duration = TimeSpan.FromMinutes(_rng.Next(2, 8)),
                Initiator = smartPed,
                Participants = new List<SmartPed> { smartPed }
            };

            CreateSocialEventScene(socialEvent);
            _activeSocialEvents.Add(socialEvent);
        }

        private void CreateSocialEventScene(SocialEvent socialEvent)
        {
            switch (socialEvent.Type)
            {
                case SocialEventType.Conversation:
                    CreateConversationEvent(socialEvent);
                    break;
                case SocialEventType.GroupGathering:
                    CreateGroupGatheringEvent(socialEvent);
                    break;
                case SocialEventType.StreetPerformance:
                    CreateStreetPerformanceEvent(socialEvent);
                    break;
                case SocialEventType.Argument:
                    CreateArgumentEvent(socialEvent);
                    break;
            }
        }

        private void CreateConversationEvent(SocialEvent socialEvent)
        {
            var initiator = socialEvent.Initiator.Ped;
            var nearbyPeds = World.GetNearbyPeds(initiator, 15f)
                .Where(p => p != initiator && !p.IsPlayer)
                .Take(2);

            foreach (var ped in nearbyPeds)
            {
                ped.Task.ChatTo(initiator);
                ped.Task.LookAt(initiator);
                
                // Ajouter aux participants si c'est un SmartPed
                var smartPed = _activePeds.FirstOrDefault(sp => sp.Ped == ped);
                if (smartPed != null)
                {
                    socialEvent.Participants.Add(smartPed);
                }
            }
        }

        private void CreateGroupGatheringEvent(SocialEvent socialEvent)
        {
            var center = socialEvent.Position;
            var participants = World.GetNearbyPeds(socialEvent.Initiator.Ped, 20f)
                .Where(p => !p.IsPlayer)
                .Take(5);

            foreach (var ped in participants)
            {
                ped.Task.FollowNavMeshTo(center + Vector3.RandomXY() * 3f);
                ped.Task.LookAt(center);
            }
        }

        private void CreateStreetPerformanceEvent(SocialEvent socialEvent)
        {
            var performer = socialEvent.Initiator.Ped;
            performer.Task.PlayAnimation("amb@world_human_musician@guitar@male@base", 
                "base", 8f, -8f, -1, AnimationFlags.Loop, 0f);

            // Créer audience
            var audience = World.GetNearbyPeds(performer, 25f)
                .Where(p => p != performer && !p.IsPlayer)
                .Take(6);

            foreach (var spectator in audience)
            {
                var watchPos = performer.Position + Vector3.RandomXY() * _rng.Next(5, 12);
                spectator.Task.FollowNavMeshTo(watchPos);
                spectator.Task.LookAt(performer);
            }
        }

        private void CreateArgumentEvent(SocialEvent socialEvent)
        {
            var initiator = socialEvent.Initiator.Ped;
            var target = FindNearbyPed(initiator, 10f);
            
            if (target != null)
            {
                initiator.Task.Combat(target);
                target.Task.Combat(initiator);
                
                // Notification
                GTA.UI.Notification.PostTicker("~r~Dispute en cours dans la zone!", false, false);
            }
        }

        private void HandleEnvironmentalReactions(SmartPed smartPed)
        {
            var ped = smartPed.Ped;
            var player = Game.Player.Character;
            
            // Réaction aux coups de feu
            if (Function.Call<bool>(Hash.IS_PROJECTILE_TYPE_IN_AREA, 
                ped.Position.X - 50f, ped.Position.Y - 50f, ped.Position.Z - 10f,
                ped.Position.X + 50f, ped.Position.Y + 50f, ped.Position.Z + 10f,
                (int)WeaponHash.Pistol, false))
            {
                ped.Task.ReactAndFlee(player);
                smartPed.Mood = PedMood.Scared;
            }

            // Réaction aux véhicules d'urgence
            var nearbyVehicles = World.GetNearbyVehicles(ped, 30f);
            foreach (var vehicle in nearbyVehicles)
            {
                if (vehicle.IsSirenActive && vehicle.ClassType == VehicleClass.Emergency)
                {
                    ped.Task.LookAt(vehicle);
                    break;
                }
            }

            // Réaction à la météo
            var weather = Function.Call<int>(Hash.GET_PREV_WEATHER_TYPE_HASH_NAME);
            if (weather == (int)Weather.Raining && smartPed.Zone.WeatherSensitive)
            {
                // Chercher un abri
                ped.Task.FollowNavMeshTo(GetNearestShelter(ped.Position));
            }
        }

        private void ManageSocialEvents(Ped player)
        {
            if (DateTime.Now - _lastEventUpdate < _eventUpdateInterval) return;

            // Mise à jour des événements sociaux
            for (int i = _activeSocialEvents.Count - 1; i >= 0; i--)
            {
                var socialEvent = _activeSocialEvents[i];
                
                if (DateTime.Now - socialEvent.StartTime > socialEvent.Duration)
                {
                    CleanupSocialEvent(socialEvent);
                    _activeSocialEvents.RemoveAt(i);
                }
            }

            _lastEventUpdate = DateTime.Now;
        }

        #region Helper Methods

        private Vector3 GetRandomPositionInZone(PopulationZone zone)
        {
            var angle = _rng.NextDouble() * Math.PI * 2;
            var distance = _rng.NextDouble() * zone.Radius;
            
            var offset = new Vector3(
                (float)(Math.Cos(angle) * distance),
                (float)(Math.Sin(angle) * distance),
                0f
            );

            return World.GetNextPositionOnSidewalk(zone.Center + offset);
        }

        private void StartJogging(Ped ped)
        {
            var jogTarget = ped.Position + Vector3.RandomXY() * 50f;
            ped.Task.RunTo(jogTarget);
        }

        private Ped FindNearbyPed(Ped sourcePed, float radius)
        {
            var nearbyPeds = World.GetNearbyPeds(sourcePed, radius);
            return nearbyPeds.FirstOrDefault(p => p != sourcePed && !p.IsPlayer);
        }

        private void CreateConversation(SmartPed ped1, Ped ped2)
        {
            var conversation = new SocialEvent
            {
                Id = Guid.NewGuid(),
                Type = SocialEventType.Conversation,
                Position = ped1.Ped.Position,
                StartTime = DateTime.Now,
                Duration = TimeSpan.FromMinutes(3),
                Initiator = ped1,
                Participants = new List<SmartPed> { ped1 }
            };

            _activeSocialEvents.Add(conversation);
        }

        private PedPersonality GetPedPersonality(ZoneType zoneType)
        {
            switch (zoneType)
            {
                case ZoneType.Business:
                    return PedPersonality.Professional;
                case ZoneType.Beach:
                    return PedPersonality.Relaxed;
                case ZoneType.Nightlife:
                    return PedPersonality.Party;
                case ZoneType.Residential:
                    return PedPersonality.Friendly;
                case ZoneType.Park:
                    return PedPersonality.Active;
                default:
                    return PedPersonality.Normal;
            }
        }

        private List<ScheduleEntry> GenerateSchedule(ZoneType zoneType)
        {
            var schedule = new List<ScheduleEntry>();
            
            switch (zoneType)
            {
                case ZoneType.Business:
                    schedule.Add(new ScheduleEntry { StartHour = 8, EndHour = 12, Activity = ActivityType.Walking });
                    schedule.Add(new ScheduleEntry { StartHour = 12, EndHour = 13, Activity = ActivityType.Resting });
                    schedule.Add(new ScheduleEntry { StartHour = 13, EndHour = 17, Activity = ActivityType.PhoneCall });
                    break;
                    
                case ZoneType.Beach:
                    schedule.Add(new ScheduleEntry { StartHour = 10, EndHour = 14, Activity = ActivityType.Resting });
                    schedule.Add(new ScheduleEntry { StartHour = 14, EndHour = 16, Activity = ActivityType.Exercising });
                    schedule.Add(new ScheduleEntry { StartHour = 16, EndHour = 18, Activity = ActivityType.Talking });
                    break;
            }
            
            return schedule;
        }

        private ActivityType GetNextActivity(SmartPed smartPed)
        {
            var currentHour = GTA.Chrono.GameClock.Hour;
            var schedule = smartPed.Schedule.FirstOrDefault(s => 
                currentHour >= s.StartHour && currentHour <= s.EndHour);
                
            if (schedule != null)
                return schedule.Activity;
                
            // Activité par défaut selon la personnalité
            switch (smartPed.PedType)
            {
                case PedPersonality.Active:
                    return ActivityType.Exercising;
                case PedPersonality.Social:
                    return ActivityType.Talking;
                case PedPersonality.Professional:
                    return ActivityType.PhoneCall;
                default:
                    return ActivityType.Walking;
            }
        }

        private float GetHourDensityMultiplier(int hour)
        {
            if (hour >= 6 && hour <= 8) return 0.8f;    // Matin calme
            if (hour >= 9 && hour <= 11) return 1.2f;   // Matinée active
            if (hour >= 12 && hour <= 14) return 1.5f;  // Heure du déjeuner
            if (hour >= 15 && hour <= 17) return 1.3f;  // Après-midi
            if (hour >= 18 && hour <= 20) return 1.1f;  // Soirée
            if (hour >= 21 && hour <= 23) return 0.9f;  // Nuit
            return 0.4f;                                 // Nuit profonde
        }

        private float GetWeatherDensityMultiplier(int weather)
        {
            switch (weather)
            {
                case (int)Weather.Clear:
                    return 1.0f;
                case (int)Weather.ExtraSunny:
                    return 1.2f;
                case (int)Weather.Clouds:
                    return 0.9f;
                case (int)Weather.Overcast:
                    return 0.8f;
                case (int)Weather.Raining:
                    return 0.5f;
                case (int)Weather.ThunderStorm:
                    return 0.3f;
                default:
                    return 1.0f;
            }
        }

        private bool IsGoodWeather()
        {
            var weather = Function.Call<int>(Hash.GET_PREV_WEATHER_TYPE_HASH_NAME);
            return weather == (int)Weather.Clear || 
                   weather == (int)Weather.ExtraSunny || 
                   weather == (int)Weather.Clouds;
        }

        private Vector3 GetNearestShelter(Vector3 position)
        {
            // Trouver le bâtiment le plus proche comme abri
            var buildings = World.GetAllBuildings();
            var nearestBuilding = buildings
                .Where(b => b.Position.DistanceTo(position) < 100f)
                .OrderBy(b => b.Position.DistanceTo(position))
                .FirstOrDefault();
                
            return nearestBuilding?.Position ?? position;
        }

        private void ReduceZoneActivity(PopulationZone zone)
        {
            zone.CurrentPedCount = Math.Max(0, zone.CurrentPedCount - 1);
        }

        private void CleanupSmartPed(SmartPed smartPed)
        {
            if (smartPed.Zone != null)
            {
                smartPed.Zone.CurrentPedCount--;
            }
        }

        private void CleanupSocialEvent(SocialEvent socialEvent)
        {
            // Remettre les participants en mode normal
            foreach (var participant in socialEvent.Participants)
            {
                if (participant.Ped != null && participant.Ped.Exists())
                {
                    participant.Ped.Task.ClearAllImmediately();
                    participant.Ped.Task.Wander();
                }
            }
        }

        private void CleanupExpiredElements()
        {
            // Nettoyer les PNJ qui sont restés trop longtemps
            var expiredPeds = _activePeds.Where(sp => 
                DateTime.Now - sp.SpawnTime > TimeSpan.FromMinutes(15)).ToList();
                
            foreach (var expiredPed in expiredPeds)
            {
                if (expiredPed.Ped != null && expiredPed.Ped.Exists())
                {
                    expiredPed.Ped.Delete();
                }
                CleanupSmartPed(expiredPed);
                _activePeds.Remove(expiredPed);
            }
        }

        #endregion
    }

    #region Data Classes

    public class SmartPed
    {
        public Ped Ped { get; set; }
        public PedPersonality PedType { get; set; }
        public PopulationZone Zone { get; set; }
        public DateTime SpawnTime { get; set; }
        public List<ScheduleEntry> Schedule { get; set; } = new List<ScheduleEntry>();
        public ActivityType CurrentActivity { get; set; }
        public DateTime LastActivityChange { get; set; }
        public PedMood Mood { get; set; }
        public int SocialLevel { get; set; } // 1-10
    }

    public class PopulationZone
    {
        public string Name { get; set; }
        public Vector3 Center { get; set; }
        public float Radius { get; set; }
        public ZoneType Type { get; set; }
        public TimeRange ActiveHours { get; set; }
        public PedHash[] PedTypes { get; set; }
        public int MaxPeds { get; set; }
        public int CurrentPedCount { get; set; }
        public float ActivityLevel { get; set; }
        public bool WeatherSensitive { get; set; }
    }

    public class SocialEvent
    {
        public Guid Id { get; set; }
        public SocialEventType Type { get; set; }
        public Vector3 Position { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public SmartPed Initiator { get; set; }
        public List<SmartPed> Participants { get; set; } = new List<SmartPed>();
    }

    public class ScheduleEntry
    {
        public int StartHour { get; set; }
        public int EndHour { get; set; }
        public ActivityType Activity { get; set; }
    }



    public enum PedPersonality
    {
        Normal,
        Professional,
        Relaxed,
        Party,
        Friendly,
        Active,
        Social
    }

    public enum ActivityType  
    {
        Idle,
        Walking,
        Talking,
        Exercising,
        Resting,
        PhoneCall
    }

    public enum PedMood
    {
        Happy,
        Neutral,
        Sad,
        Angry,
        Excited,
        Scared
    }

    public enum SocialEventType
    {
        Conversation,
        GroupGathering,
        StreetPerformance,
        Argument
    }

    public enum ZoneType
    {
        Business,
        Beach,
        Residential,
        Nightlife,
        Park
    }

    #endregion
}