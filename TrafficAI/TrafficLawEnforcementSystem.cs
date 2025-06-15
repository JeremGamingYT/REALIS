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
    /// Système de contrôles routiers aléatoires et d'application de la loi du trafic
    /// </summary>
    public class TrafficLawEnforcementSystem : IDisposable
    {
        private readonly List<PoliceCheckpoint> _activeCheckpoints = new List<PoliceCheckpoint>();
        private readonly List<TrafficViolation> _recentViolations = new List<TrafficViolation>();
        private readonly Random _random = new Random();
        
        private DateTime _lastCheckpointCheck = DateTime.MinValue;
        private DateTime _lastViolationCheck = DateTime.MinValue;
        
        private const double CHECKPOINT_CHECK_INTERVAL = 30.0; // 30 secondes
        private const double VIOLATION_CHECK_INTERVAL = 2.0; // 2 secondes
        private const int MAX_ACTIVE_CHECKPOINTS = 2;
        private const float CHECKPOINT_TRIGGER_DISTANCE = 100f;
        
        // Probabilités des contrôles
        private const int RANDOM_CHECKPOINT_CHANCE = 5; // 5% toutes les 30s
        private const int VIOLATION_CHECKPOINT_CHANCE = 25; // 25% après violation
        
        public void Initialize()
        {
            // Logger.Info("Traffic Law Enforcement System initialized");
        }
        
        public void Update(Vector3 playerPosition)
        {
            try
            {
                CheckForRandomCheckpoints(playerPosition);
                MonitorTrafficViolations(playerPosition);
                UpdateActiveCheckpoints(playerPosition);
                CleanupOldViolations();
            }
            catch (Exception ex)
            {
                // Logger.Error($"Traffic Law Enforcement update error: {ex.Message}");
            }
        }
        
        private void CheckForRandomCheckpoints(Vector3 playerPosition)
        {
            if ((DateTime.Now - _lastCheckpointCheck).TotalSeconds < CHECKPOINT_CHECK_INTERVAL) return;
            
            _lastCheckpointCheck = DateTime.Now;
            
            // Ne pas créer de checkpoint si le joueur n'est pas en véhicule
            if (!Game.Player.Character.IsInVehicle()) return;
            
            // Vérifier s'il y a trop de checkpoints actifs
            if (_activeCheckpoints.Count >= MAX_ACTIVE_CHECKPOINTS) return;
            
            // Chance aléatoire de créer un checkpoint
            if (_random.Next(100) < RANDOM_CHECKPOINT_CHANCE)
            {
                CreateRandomCheckpoint(playerPosition);
            }
        }
        
        private void MonitorTrafficViolations(Vector3 playerPosition)
        {
            if ((DateTime.Now - _lastViolationCheck).TotalSeconds < VIOLATION_CHECK_INTERVAL) return;
            
            _lastViolationCheck = DateTime.Now;
            
            if (!Game.Player.Character.IsInVehicle()) return;
            
            var vehicle = Game.Player.Character.CurrentVehicle;
            CheckForViolations(vehicle, playerPosition);
        }
        
        private void CheckForViolations(Vehicle vehicle, Vector3 position)
        {
            var violations = new List<TrafficViolation>();
            
            // Vérifier excès de vitesse
            var speed = vehicle.Speed * 3.6f;
            var speedLimit = GetSpeedLimitAtPosition(position);
            if (speed > speedLimit + 15)
            {
                violations.Add(new TrafficViolation
                {
                    Type = ViolationType.Speeding,
                    Severity = speed > speedLimit + 30 ? ViolationSeverity.Severe : ViolationSeverity.Minor,
                    Location = position,
                    Time = DateTime.Now,
                    Details = $"Excès de vitesse: {(int)speed} km/h (limite: {speedLimit} km/h)"
                });
            }
            
            // Vérifier grillage de feu rouge
            if (IsNearTrafficLight(position) && IsRunningRedLight(vehicle))
            {
                violations.Add(new TrafficViolation
                {
                    Type = ViolationType.RedLightViolation,
                    Severity = ViolationSeverity.Major,
                    Location = position,
                    Time = DateTime.Now,
                    Details = "Grillage de feu rouge"
                });
            }
            
            // Vérifier conduite dangereuse
            if (IsDrivingDangerously(vehicle))
            {
                violations.Add(new TrafficViolation
                {
                    Type = ViolationType.RecklessDriving,
                    Severity = ViolationSeverity.Major,
                    Location = position,
                    Time = DateTime.Now,
                    Details = "Conduite dangereuse"
                });
            }
            
            // Traiter les violations
            foreach (var violation in violations)
            {
                ProcessViolation(violation);
            }
        }
        
        private void ProcessViolation(TrafficViolation violation)
        {
            _recentViolations.Add(violation);
            
            // Notifier le joueur
            string color = violation.Severity switch
            {
                ViolationSeverity.Minor => "~y~",
                ViolationSeverity.Major => "~o~",
                ViolationSeverity.Severe => "~r~",
                _ => "~w~"
            };
            
            Screen.ShowSubtitle($"{color}INFRACTION DÉTECTÉE~w~\n{violation.Details}", 4000);
            
            // Chance de déclencher un contrôle selon la gravité
            int checkpointChance = violation.Severity switch
            {
                ViolationSeverity.Minor => 10,
                ViolationSeverity.Major => 25,
                ViolationSeverity.Severe => 50,
                _ => 0
            };
            
            if (_random.Next(100) < checkpointChance)
            {
                CreateViolationCheckpoint(violation.Location);
            }
            
            // Logger.Info($"Traffic violation processed: {violation.Type} at {violation.Location}");
        }
        
        private void CreateRandomCheckpoint(Vector3 playerPosition)
        {
            // Trouver une position appropriée pour le checkpoint
            var checkpointPosition = FindSuitableCheckpointPosition(playerPosition);
            if (checkpointPosition == Vector3.Zero) return;
            
            var checkpoint = new PoliceCheckpoint
            {
                Position = checkpointPosition,
                Type = CheckpointType.Random,
                CreatedTime = DateTime.Now,
                IsActive = true,
                Reason = "Contrôle routier de routine"
            };
            
            SpawnCheckpointUnits(checkpoint);
            _activeCheckpoints.Add(checkpoint);
            
            // Add map blip for checkpoint
            checkpoint.MapBlip = World.CreateBlip(checkpoint.Position);
            checkpoint.MapBlip.Sprite = BlipSprite.Standard;
            checkpoint.MapBlip.Color = BlipColor.Blue;
            checkpoint.MapBlip.IsShortRange = true;
            checkpoint.MapBlip.Name = checkpoint.Reason;
            
            // Logger.Info($"Random checkpoint created at {checkpointPosition}");
            Notification.PostTicker("~b~Contrôle routier~w~ détecté sur votre route", false);
        }
        
        private void CreateViolationCheckpoint(Vector3 violationPosition)
        {
            var checkpointPosition = FindSuitableCheckpointPosition(violationPosition);
            if (checkpointPosition == Vector3.Zero) return;
            
            var checkpoint = new PoliceCheckpoint
            {
                Position = checkpointPosition,
                Type = CheckpointType.Violation,
                CreatedTime = DateTime.Now,
                IsActive = true,
                Reason = "Contrôle suite à infraction"
            };
            
            SpawnCheckpointUnits(checkpoint);
            _activeCheckpoints.Add(checkpoint);
            
            // Add map blip for violation checkpoint
            checkpoint.MapBlip = World.CreateBlip(checkpoint.Position);
            checkpoint.MapBlip.Sprite = BlipSprite.Standard;
            checkpoint.MapBlip.Color = BlipColor.Red;
            checkpoint.MapBlip.IsShortRange = true;
            checkpoint.MapBlip.Name = checkpoint.Reason;
            
            // Logger.Info($"Violation checkpoint created at {checkpointPosition}");
            Screen.ShowSubtitle("~r~POLICE~w~\nVous êtes poursuivi pour infraction!", 5000);
        }
        
        private Vector3 FindSuitableCheckpointPosition(Vector3 referencePosition)
        {
            // Chercher une position sur la route à une distance appropriée
            for (int i = 0; i < 10; i++)
            {
                var angle = _random.NextDouble() * Math.PI * 2;
                var distance = 200f + _random.Next(300); // 200-500m
                
                var testPosition = new Vector3(
                    referencePosition.X + (float)(Math.Cos(angle) * distance),
                    referencePosition.Y + (float)(Math.Sin(angle) * distance),
                    referencePosition.Z
                );
                
                // Vérifier si c'est sur une route
                if (IsPositionOnRoad(testPosition))
                {
                    return testPosition;
                }
            }
            
            return Vector3.Zero; // Aucune position trouvée
        }
        
        private void SpawnCheckpointUnits(PoliceCheckpoint checkpoint)
        {
            try
            {
                // Spawn des véhicules de police
                var policeModel = VehicleHash.Police;
                var copModel = PedHash.Cop01SMY;
                
                // Première voiture de police
                var policeVehicle1 = World.CreateVehicle(policeModel, checkpoint.Position);
                if (policeVehicle1?.Exists() == true)
                {
                    policeVehicle1.Heading = _random.Next(360);
                                            Function.Call(Hash.SET_VEHICLE_SIREN, policeVehicle1, true);
                    checkpoint.Vehicles.Add(policeVehicle1);
                    
                    // Spawn des policiers
                    for (int i = 0; i < 2; i++)
                    {
                        var cop = World.CreatePed(copModel, checkpoint.Position + Vector3.RandomXY() * 3f);
                        if (cop?.Exists() == true)
                        {
                            cop.Weapons.Give(WeaponHash.Pistol, 100, true, false);
                            cop.Task.StartScenario("WORLD_HUMAN_COP_IDLES", 0);
                            checkpoint.Officers.Add(cop);
                        }
                    }
                }
                
                // Deuxième voiture si contrôle majeur
                if (checkpoint.Type == CheckpointType.Violation)
                {
                    var policeVehicle2 = World.CreateVehicle(policeModel, 
                        checkpoint.Position + Vector3.RandomXY() * 10f);
                    if (policeVehicle2?.Exists() == true)
                    {
                        policeVehicle2.Heading = _random.Next(360);
                        checkpoint.Vehicles.Add(policeVehicle2);
                    }
                }
                
                // Logger.Info($"Checkpoint units spawned: {checkpoint.Vehicles.Count} vehicles, {checkpoint.Officers.Count} officers");
            }
            catch (Exception ex)
            {
                // Logger.Error($"Error spawning checkpoint units: {ex.Message}");
            }
        }
        
        private void UpdateActiveCheckpoints(Vector3 playerPosition)
        {
            var checkpointsToRemove = new List<PoliceCheckpoint>();
            
            foreach (var checkpoint in _activeCheckpoints)
            {
                // Vérifier si le checkpoint doit être supprimé
                var age = (DateTime.Now - checkpoint.CreatedTime).TotalMinutes;
                var distanceToPlayer = playerPosition.DistanceTo(checkpoint.Position);
                
                if (age > 10 || distanceToPlayer > 1000f) // 10 minutes ou 1km
                {
                    checkpointsToRemove.Add(checkpoint);
                }
                else if (distanceToPlayer < CHECKPOINT_TRIGGER_DISTANCE && checkpoint.IsActive)
                {
                    TriggerCheckpointInteraction(checkpoint, playerPosition);
                }
            }
            
            // Supprimer les checkpoints expirés
            foreach (var checkpoint in checkpointsToRemove)
            {
                RemoveCheckpoint(checkpoint);
            }
        }
        
        private void TriggerCheckpointInteraction(PoliceCheckpoint checkpoint, Vector3 playerPosition)
        {
            if (!Game.Player.Character.IsInVehicle()) return;
            
            checkpoint.IsActive = false; // Éviter les déclenchements multiples
            
            // Interaction selon le type de checkpoint
            if (checkpoint.Type == CheckpointType.Random)
            {
                Screen.ShowSubtitle("~b~CONTRÔLE ROUTIER~w~\nPrésentez vos papiers", 5000);
                
                // Contrôle de routine - chance de problème
                if (_random.Next(100) < 20) // 20% de chance
                {
                    Game.Player.WantedLevel = 1;
                    Screen.ShowSubtitle("~r~PROBLÈME DÉTECTÉ~w~\nVous êtes en état d'arrestation!", 5000);
                }
                else
                {
                    Screen.ShowSubtitle("~g~CONTRÔLE TERMINÉ~w~\nBonne route!", 3000);
                }
            }
            else if (checkpoint.Type == CheckpointType.Violation)
            {
                // Contrôle suite à violation - plus strict
                Screen.ShowSubtitle("~r~ARRÊT OBLIGATOIRE~w~\nInfraction constatée!", 5000);
                Game.Player.WantedLevel = Math.Min(Game.Player.WantedLevel + 1, 3);
            }
            
            // Logger.Info($"Checkpoint interaction triggered: {checkpoint.Type}");
        }
        
        private void RemoveCheckpoint(PoliceCheckpoint checkpoint)
        {
            try
            {
                // Remove blip
                if (checkpoint.MapBlip != null)
                    Function.Call(Hash.REMOVE_BLIP, checkpoint.MapBlip.Handle);
                
                // Supprimer les véhicules
                foreach (var vehicle in checkpoint.Vehicles)
                {
                    if (vehicle?.Exists() == true)
                    {
                        vehicle.Delete();
                    }
                }
                
                // Supprimer les officiers
                foreach (var officer in checkpoint.Officers)
                {
                    if (officer?.Exists() == true)
                    {
                        officer.Delete();
                    }
                }
                
                _activeCheckpoints.Remove(checkpoint);
                // Logger.Info($"Checkpoint removed at {checkpoint.Position}");
            }
            catch (Exception ex)
            {
                // Logger.Error($"Error removing checkpoint: {ex.Message}");
            }
        }
        
        private void CleanupOldViolations()
        {
            // Supprimer les violations de plus de 5 minutes
            _recentViolations.RemoveAll(v => (DateTime.Now - v.Time).TotalMinutes > 5);
        }
        
        // Méthodes utilitaires
        private int GetSpeedLimitAtPosition(Vector3 position)
        {
            // Logique simplifiée - peut être étendue
            return IsHighway(position) ? 120 : IsResidential(position) ? 50 : 80;
        }
        
        private bool IsHighway(Vector3 position) => position.Y > 1000 || position.Y < -2000;
        private bool IsResidential(Vector3 position) => position.DistanceTo(Vector3.Zero) < 1000;
        
        private bool IsNearTrafficLight(Vector3 position)
        {
            // Vérifier la présence de feux de circulation
            return Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, 
                position.X, position.Y, position.Z, 1, 1073741824, 0, 0);
        }
        
        private bool IsRunningRedLight(Vehicle vehicle)
        {
            // Logique simplifiée pour détecter le grillage de feu rouge
            return vehicle.Speed > 10f && _random.Next(100) < 5; // 5% de chance
        }
        
        private bool IsDrivingDangerously(Vehicle vehicle)
        {
            return vehicle.Speed > 30f && 
                   (vehicle.HasCollided || vehicle.IsOnFire || vehicle.IsUpsideDown);
        }
        
        private bool IsPositionOnRoad(Vector3 position)
        {
            return Function.Call<bool>(Hash.IS_POINT_ON_ROAD, position.X, position.Y, position.Z, 0);
        }
        
        public void Dispose()
        {
            // Nettoyer tous les checkpoints actifs
            foreach (var checkpoint in _activeCheckpoints.ToList())
            {
                RemoveCheckpoint(checkpoint);
            }
            
            _activeCheckpoints.Clear();
            _recentViolations.Clear();
        }
    }
    
    /// <summary>
    /// Checkpoint de police
    /// </summary>
    public class PoliceCheckpoint
    {
        public Vector3 Position { get; set; }
        public CheckpointType Type { get; set; }
        public DateTime CreatedTime { get; set; }
        public bool IsActive { get; set; }
        public string Reason { get; set; } = "";
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public List<Ped> Officers { get; set; } = new List<Ped>();
        // Map blip for this checkpoint
        public Blip MapBlip { get; set; }
    }
    
    /// <summary>
    /// Violation du code de la route
    /// </summary>
    public class TrafficViolation
    {
        public ViolationType Type { get; set; }
        public ViolationSeverity Severity { get; set; }
        public Vector3 Location { get; set; }
        public DateTime Time { get; set; }
        public string Details { get; set; } = "";
    }
    
    // Enums supprimés car définis dans TrafficCommonTypes.cs
} 