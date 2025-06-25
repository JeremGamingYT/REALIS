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
    /// Module de gestion des chaînes d'événements d'urgence
    /// Simule des incidents réalistes avec réponse des services d'urgence
    /// </summary>
    public class EmergencyChainModule : IModule
    {
        private readonly List<EmergencyIncident> _activeIncidents = new List<EmergencyIncident>();
        private readonly Random _rng = new Random();
        private DateTime _lastIncidentTime = DateTime.MinValue;
        private readonly TimeSpan _incidentCooldown = TimeSpan.FromMinutes(2);

        // Services d'urgence disponibles
        private readonly Dictionary<EmergencyServiceType, EmergencyService> _services = new Dictionary<EmergencyServiceType, EmergencyService>();

        public void Initialize()
        {
            // Initialiser les services d'urgence
            _services[EmergencyServiceType.Police] = new EmergencyService(EmergencyServiceType.Police);
            _services[EmergencyServiceType.Fire] = new EmergencyService(EmergencyServiceType.Fire);
            _services[EmergencyServiceType.Medical] = new EmergencyService(EmergencyServiceType.Medical);
            _services[EmergencyServiceType.Towing] = new EmergencyService(EmergencyServiceType.Towing);
        }

        public void Update()
        {
            try
            {
                // Générer nouveaux incidents
                if (DateTime.Now - _lastIncidentTime > _incidentCooldown && _activeIncidents.Count < 3)
                {
                    if (_rng.Next(100) < 15) // 15% de chance par cycle
                    {
                        GenerateRandomIncident();
                        _lastIncidentTime = DateTime.Now;
                    }
                }

                // Mettre à jour incidents actifs
                for (int i = _activeIncidents.Count - 1; i >= 0; i--)
                {
                    var incident = _activeIncidents[i];
                    UpdateIncident(incident);

                    if (incident.IsCompleted || incident.IsExpired)
                    {
                        CleanupIncident(incident);
                        _activeIncidents.RemoveAt(i);
                    }
                }

                // Mettre à jour les services
                foreach (var service in _services.Values)
                {
                    service.Update();
                }
            }
            catch (Exception ex)
            {
                // Log error silently
            }
        }

        public void Dispose()
        {
            foreach (var incident in _activeIncidents)
            {
                CleanupIncident(incident);
            }
            _activeIncidents.Clear();

            foreach (var service in _services.Values)
            {
                service.Dispose();
            }
            _services.Clear();
        }

        #region Incident Generation

        private void GenerateRandomIncident()
        {
            var player = Game.Player.Character;
            var incidentTypes = Enum.GetValues(typeof(IncidentType)).Cast<IncidentType>().ToArray();
            var incidentType = incidentTypes[_rng.Next(incidentTypes.Length)];

            var incident = new EmergencyIncident
            {
                Id = Guid.NewGuid(),
                Type = incidentType,
                Position = GetRandomNearbyPosition(player.Position, 200f, 800f),
                StartTime = DateTime.Now,
                Priority = GetIncidentPriority(incidentType),
                RequiredServices = GetRequiredServices(incidentType)
            };

            _activeIncidents.Add(incident);
            CreateIncidentScene(incident);
            DispatchServices(incident);

            // Notification au joueur
            GTA.UI.Notification.PostTicker($"~r~Incident signalé - {GetIncidentDescription(incidentType)}", false, false);
        }

        private void CreateIncidentScene(EmergencyIncident incident)
        {
            switch (incident.Type)
            {
                case IncidentType.CarAccident:
                    CreateCarAccidentScene(incident);
                    break;
                case IncidentType.Fire:
                    CreateFireScene(incident);
                    break;
                case IncidentType.MedicalEmergency:
                    CreateMedicalEmergencyScene(incident);
                    break;
                case IncidentType.Crime:
                    CreateCrimeScene(incident);
                    break;
                case IncidentType.StructuralCollapse:
                    CreateCollapseScene(incident);
                    break;
            }

            // Créer blip pour l'incident
            incident.Blip = World.CreateBlip(incident.Position);
            incident.Blip.Sprite = GetIncidentBlipSprite(incident.Type);
            incident.Blip.Color = BlipColor.Red;
            incident.Blip.Scale = 0.8f;
            incident.Blip.Name = GetIncidentDescription(incident.Type);
        }

        private void CreateCarAccidentScene(EmergencyIncident incident)
        {
            // Créer véhicules accidentés
            var vehicleModels = new[] { VehicleHash.Adder, VehicleHash.Blista, VehicleHash.Kuruma };
            
            for (int i = 0; i < _rng.Next(2, 4); i++)
            {
                var model = vehicleModels[_rng.Next(vehicleModels.Length)];
                var vehicle = World.CreateVehicle(new Model(model), 
                    incident.Position + Vector3.RandomXY() * 5f);
                
                if (vehicle != null && vehicle.Exists())
                {
                    vehicle.Health = _rng.Next(50, 200);
                    vehicle.EngineHealth = _rng.Next(0, 100);
                    
                    // Endommager visuellement
                    // Endommager le véhicule avec un impact
                    GTA.Native.Function.Call(GTA.Native.Hash.SET_VEHICLE_DAMAGE, vehicle, 0f, 0f, 0f, 200f, 100f, true);
                    
                    incident.InvolvedVehicles.Add(vehicle);
                }
            }

            // Créer témoins
            for (int i = 0; i < _rng.Next(2, 5); i++)
            {
                var witness = World.CreateRandomPed(incident.Position + Vector3.RandomXY() * 10f);
                if (witness != null && witness.Exists())
                {
                    witness.Task.LookAt(incident.Position);
                    incident.InvolvedPeds.Add(witness);
                }
            }
        }

        private void CreateFireScene(EmergencyIncident incident)
        {
            // Créer véhicule en feu
            var burningVehicle = World.CreateVehicle(new Model(VehicleHash.Blista), incident.Position);
            if (burningVehicle != null && burningVehicle.Exists())
            {
                burningVehicle.Health = 100;
                burningVehicle.EngineHealth = 0;
                // Utiliser Native pour allumer le feu
                GTA.Native.Function.Call(GTA.Native.Hash.START_ENTITY_FIRE, burningVehicle);
                incident.InvolvedVehicles.Add(burningVehicle);
            }

            // Créer foule de spectateurs à distance sécurisée
            for (int i = 0; i < _rng.Next(3, 8); i++)
            {
                var bystander = World.CreateRandomPed(incident.Position + Vector3.RandomXY() * 15f);
                if (bystander != null && bystander.Exists())
                {
                    if (burningVehicle != null)
                    {
                        bystander.Task.LookAt(burningVehicle);
                    }
                    else
                    {
                        bystander.Task.LookAt(incident.Position);
                    }
                    incident.InvolvedPeds.Add(bystander);
                }
            }
        }

        private void CreateMedicalEmergencyScene(EmergencyIncident incident)
        {
            // Créer patient
            var patient = World.CreateRandomPed(incident.Position);
            if (patient != null && patient.Exists())
            {
                patient.Health = 40; // État critique
                patient.Task.PlayAnimation("amb@world_human_bum_slumped@male@laying_on_left_side@base", "base", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                incident.InvolvedPeds.Add(patient);
            }

            // Créer personnes qui appellent les secours
            for (int i = 0; i < _rng.Next(1, 3); i++)
            {
                var caller = World.CreateRandomPed(incident.Position + Vector3.RandomXY() * 5f);
                if (caller != null && caller.Exists())
                {
                    caller.Task.LookAt(patient);
                    // Animation téléphone
                    caller.Task.PlayAnimation("cellphone@", "cellphone_call_listen_base", 8f, -8f, 5000, AnimationFlags.Loop, 0f);
                    incident.InvolvedPeds.Add(caller);
                }
            }
        }

        private void CreateCrimeScene(EmergencyIncident incident)
        {
            // Créer suspect en fuite
            var suspect = World.CreateRandomPed(incident.Position);
            if (suspect != null && suspect.Exists())
            {
                suspect.Weapons.Give(WeaponHash.Knife, 1, false, true);
                suspect.Task.FleeFrom(incident.Position);
                incident.InvolvedPeds.Add(suspect);
                incident.Suspect = suspect;
            }

            // Créer victime
            var victim = World.CreateRandomPed(incident.Position + Vector3.RandomXY() * 3f);
            if (victim != null && victim.Exists())
            {
                victim.Health = 70; // Blessé
                victim.Task.HandsUp(30000);
                incident.InvolvedPeds.Add(victim);
            }
        }

        private void CreateCollapseScene(EmergencyIncident incident)
        {
            // Simuler effondrement avec props de débris
            for (int i = 0; i < 5; i++)
            {
                var debris = World.CreatePropNoOffset(new Model("prop_rub_buswreck_01"), 
                    incident.Position + Vector3.RandomXY() * 8f, true);
                if (debris != null && debris.Exists())
                {
                    incident.InvolvedProps.Add(debris);
                }
            }

            // Personnes piégées
            for (int i = 0; i < _rng.Next(1, 4); i++)
            {
                var trapped = World.CreateRandomPed(incident.Position + Vector3.RandomXY() * 5f);
                if (trapped != null && trapped.Exists())
                {
                    trapped.Health = 50;
                    trapped.Task.PlayAnimation("amb@world_human_bum_slumped@male@laying_on_left_side@base", "base", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                    incident.InvolvedPeds.Add(trapped);
                }
            }
        }

        private void DispatchServices(EmergencyIncident incident)
        {
            foreach (var serviceType in incident.RequiredServices)
            {
                if (_services.ContainsKey(serviceType))
                {
                    _services[serviceType].DispatchTo(incident);
                }
            }
        }

        private void UpdateIncident(EmergencyIncident incident)
        {
            // Vérifier si l'incident est résolu
            var servicesPresentAndReady = incident.RequiredServices.All(service => 
                _services[service].IsAtLocation(incident.Position));

            if (servicesPresentAndReady && !incident.ResolutionStarted)
            {
                incident.ResolutionStarted = true;
                incident.ResolutionStartTime = DateTime.Now;
                
                // Notification de prise en charge
                GTA.UI.Notification.PostTicker($"~g~Services d'urgence sur place - {GetIncidentDescription(incident.Type)}", false, false);
            }

            // Processus de résolution
            if (incident.ResolutionStarted)
            {
                var resolutionTime = GetResolutionTime(incident.Type);
                if (DateTime.Now - incident.ResolutionStartTime > resolutionTime)
                {
                    incident.IsCompleted = true;
                    GTA.UI.Notification.PostTicker($"~b~Incident résolu - {GetIncidentDescription(incident.Type)}", false, false);
                }
            }

            // Expiration de l'incident
            if (DateTime.Now - incident.StartTime > TimeSpan.FromMinutes(15))
            {
                incident.IsExpired = true;
            }
        }

        private void CleanupIncident(EmergencyIncident incident)
        {
            // Nettoyer blip
            if (incident.Blip != null && incident.Blip.Exists())
            {
                incident.Blip.Delete();
            }

            // Nettoyer véhicules (optionnel - peut les laisser pour le réalisme)
            foreach (var vehicle in incident.InvolvedVehicles.Where(v => v != null && v.Exists()))
            {
                if (_rng.Next(100) < 30) // 30% de chance de nettoyer
                {
                    vehicle.Delete();
                }
            }

            // Nettoyer props de débris
            foreach (var prop in incident.InvolvedProps.Where(p => p != null && p.Exists()))
            {
                prop.Delete();
            }

            // Rappeler les services
            foreach (var serviceType in incident.RequiredServices)
            {
                if (_services.ContainsKey(serviceType))
                {
                    _services[serviceType].RecallFromIncident(incident.Id);
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

        private IncidentPriority GetIncidentPriority(IncidentType type)
        {
            switch (type)
            {
                case IncidentType.MedicalEmergency:
                    return IncidentPriority.Critical;
                case IncidentType.Fire:
                    return IncidentPriority.High;
                case IncidentType.Crime:
                    return IncidentPriority.High;
                case IncidentType.StructuralCollapse:
                    return IncidentPriority.Critical;
                case IncidentType.CarAccident:
                    return IncidentPriority.Medium;
                default:
                    return IncidentPriority.Low;
            }
        }

        private List<EmergencyServiceType> GetRequiredServices(IncidentType type)
        {
            switch (type)
            {
                case IncidentType.CarAccident:
                    return new List<EmergencyServiceType> { EmergencyServiceType.Police, EmergencyServiceType.Medical, EmergencyServiceType.Towing };
                case IncidentType.Fire:
                    return new List<EmergencyServiceType> { EmergencyServiceType.Fire, EmergencyServiceType.Police, EmergencyServiceType.Medical };
                case IncidentType.MedicalEmergency:
                    return new List<EmergencyServiceType> { EmergencyServiceType.Medical, EmergencyServiceType.Police };
                case IncidentType.Crime:
                    return new List<EmergencyServiceType> { EmergencyServiceType.Police, EmergencyServiceType.Medical };
                case IncidentType.StructuralCollapse:
                    return new List<EmergencyServiceType> { EmergencyServiceType.Fire, EmergencyServiceType.Medical, EmergencyServiceType.Police };
                default:
                    return new List<EmergencyServiceType> { EmergencyServiceType.Police };
            }
        }

        private BlipSprite GetIncidentBlipSprite(IncidentType type)
        {
            switch (type)
            {
                case IncidentType.Fire:
                    return BlipSprite.Standard;
                case IncidentType.Crime:
                    return BlipSprite.Safehouse;
                default:
                    return BlipSprite.Standard;
            }
        }

        private string GetIncidentDescription(IncidentType type)
        {
            switch (type)
            {
                case IncidentType.CarAccident:
                    return "Accident de la route";
                case IncidentType.Fire:
                    return "Incendie";
                case IncidentType.MedicalEmergency:
                    return "Urgence médicale";
                case IncidentType.Crime:
                    return "Crime en cours";
                case IncidentType.StructuralCollapse:
                    return "Effondrement de structure";
                default:
                    return "Incident";
            }
        }

        private TimeSpan GetResolutionTime(IncidentType type)
        {
            switch (type)
            {
                case IncidentType.MedicalEmergency:
                    return TimeSpan.FromMinutes(3);
                case IncidentType.Fire:
                    return TimeSpan.FromMinutes(5);
                case IncidentType.Crime:
                    return TimeSpan.FromMinutes(2);
                case IncidentType.StructuralCollapse:
                    return TimeSpan.FromMinutes(8);
                case IncidentType.CarAccident:
                    return TimeSpan.FromMinutes(4);
                default:
                    return TimeSpan.FromMinutes(3);
            }
        }

        #endregion
    }

    #region Data Classes

    public class EmergencyIncident
    {
        public Guid Id { get; set; }
        public IncidentType Type { get; set; }
        public Vector3 Position { get; set; }
        public DateTime StartTime { get; set; }
        public IncidentPriority Priority { get; set; }
        public List<EmergencyServiceType> RequiredServices { get; set; } = new List<EmergencyServiceType>();
        public List<Vehicle> InvolvedVehicles { get; set; } = new List<Vehicle>();
        public List<Ped> InvolvedPeds { get; set; } = new List<Ped>();
        public List<Prop> InvolvedProps { get; set; } = new List<Prop>();
        public Blip Blip { get; set; }
        public bool ResolutionStarted { get; set; }
        public DateTime ResolutionStartTime { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsExpired { get; set; }
        public Ped Suspect { get; set; } // Pour les crimes
    }

    public class EmergencyService
    {
        public EmergencyServiceType Type { get; set; }
        public List<EmergencyUnit> Units { get; set; } = new List<EmergencyUnit>();
        public Dictionary<Guid, EmergencyUnit> DispatchedUnits { get; set; } = new Dictionary<Guid, EmergencyUnit>();

        public EmergencyService(EmergencyServiceType type)
        {
            Type = type;
        }

        public void DispatchTo(EmergencyIncident incident)
        {
            // Créer unité d'urgence
            var unit = CreateUnit(incident.Position);
            if (unit != null)
            {
                DispatchedUnits[incident.Id] = unit;
                Units.Add(unit);
            }
        }

        public bool IsAtLocation(Vector3 position)
        {
            return Units.Any(unit => unit.Vehicle != null && unit.Vehicle.Exists() && 
                                   unit.Vehicle.Position.DistanceTo(position) < 20f);
        }

        public void RecallFromIncident(Guid incidentId)
        {
            if (DispatchedUnits.ContainsKey(incidentId))
            {
                var unit = DispatchedUnits[incidentId];
                Units.Remove(unit);
                DispatchedUnits.Remove(incidentId);
                
                // Faire partir l'unité
                if (unit.Vehicle != null && unit.Vehicle.Exists())
                {
                    // Faire partir l'unité - les véhicules n'ont pas de Task, seuls les conducteurs en ont
                    if (unit.Driver != null && unit.Driver.Exists())
                    {
                        unit.Driver.Task.Wander();
                    }
                }
            }
        }

        public void Update()
        {
            // Nettoyer les unités qui n'existent plus
            for (int i = Units.Count - 1; i >= 0; i--)
            {
                var unit = Units[i];
                if (unit.Vehicle == null || !unit.Vehicle.Exists())
                {
                    Units.RemoveAt(i);
                }
            }
        }

        public void Dispose()
        {
            foreach (var unit in Units.Where(u => u.Vehicle != null && u.Vehicle.Exists()))
            {
                unit.Vehicle.Delete();
            }
            Units.Clear();
            DispatchedUnits.Clear();
        }

        private EmergencyUnit CreateUnit(Vector3 destination)
        {
            Vehicle vehicle = null;
            var spawnPos = GetSpawnPosition(destination);

            switch (Type)
            {
                case EmergencyServiceType.Police:
                    vehicle = World.CreateVehicle(new Model(VehicleHash.Police), spawnPos);
                    break;
                case EmergencyServiceType.Fire:
                    vehicle = World.CreateVehicle(new Model(VehicleHash.FireTruck), spawnPos);
                    break;
                case EmergencyServiceType.Medical:
                    vehicle = World.CreateVehicle(new Model(VehicleHash.Ambulance), spawnPos);
                    break;
                case EmergencyServiceType.Towing:
                    vehicle = World.CreateVehicle(new Model(VehicleHash.TowTruck), spawnPos);
                    break;
            }

            if (vehicle != null && vehicle.Exists())
            {
                // Configurer le véhicule
                vehicle.IsSirenActive = true;
                vehicle.IsEngineRunning = true;
                
                // Ajouter conducteur
                var driver = vehicle.CreatePedOnSeat(VehicleSeat.Driver, new Model(PedHash.Cop01SMY));
                if (driver != null && driver.Exists())
                {
                    driver.VehicleDrivingFlags = VehicleDrivingFlags.None;
                    driver.Task.CruiseWithVehicle(vehicle, 35f);
                }

                return new EmergencyUnit
                {
                    Vehicle = vehicle,
                    Driver = driver,
                    Type = Type,
                    Destination = destination
                };
            }

            return null;
        }

        private Vector3 GetSpawnPosition(Vector3 destination)
        {
            // Trouver position spawn à distance raisonnable
            var player = Game.Player.Character;
            var spawnDistance = 100f + (new Random().NextDouble() * 100f); // 100-200m
            
            var angle = new Random().NextDouble() * Math.PI * 2;
            var offset = new Vector3(
                (float)(Math.Cos(angle) * spawnDistance),
                (float)(Math.Sin(angle) * spawnDistance),
                0f
            );

            return World.GetNextPositionOnStreet(destination + offset);
        }
    }

    public class EmergencyUnit
    {
        public Vehicle Vehicle { get; set; }
        public Ped Driver { get; set; }
        public EmergencyServiceType Type { get; set; }
        public Vector3 Destination { get; set; }
    }

    public enum IncidentType
    {
        CarAccident,
        Fire,
        MedicalEmergency,
        Crime,
        StructuralCollapse
    }

    public enum IncidentPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum EmergencyServiceType
    {
        Police,
        Fire,
        Medical,
        Towing
    }

    #endregion
}