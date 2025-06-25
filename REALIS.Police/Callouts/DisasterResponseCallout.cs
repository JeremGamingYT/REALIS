using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace REALIS.Police.Callouts
{
    /// <summary>
    /// Callout de catastrophe naturelle - Gestion de crise et évacuation d'urgence
    /// </summary>
    public class DisasterResponseCallout : CalloutBase
    {
        private readonly List<Ped> _victims = new List<Ped>();
        private readonly List<Ped> _rescueTeam = new List<Ped>();
        private readonly List<Vehicle> _emergencyVehicles = new List<Vehicle>();
        private readonly List<Blip> _blips = new List<Blip>();
        
        private Vector3 _disasterSite;
        private bool _evacuationActive;
        private bool _rescueActive;
        private DateTime _disasterStart;

        public DisasterResponseCallout() : base("RÉPONSE D'URGENCE", 
            "Désastre naturel. Victimes nombreuses. Assistance requise.", 
            new Vector3(1961.64f, 3777.38f, 32.23f)) // Sandy Shores area
        {
        }

        public override bool CanSpawn()
        {
            // Toujours disponible pour les tests
            return true;
        }

        protected override void OnStart()
        {
            _disasterSite = StartPosition;
            _disasterStart = DateTime.Now;
            _evacuationActive = true;
            
            // Effets de tremblement de terre simulé
            Function.Call(Hash.SHAKE_GAMEPLAY_CAM, "SMALL_EXPLOSION_SHAKE", 0.3f);
            
            SpawnDisasterScene();
            
            GTA.UI.Notification.Show("~r~ALERTE MAJEURE!~w~~n~Tremblement de terre détecté. Victimes multiples!");
            GTA.UI.Notification.Show("~b~Mission:~w~ Coordonner les secours et l'évacuation.");
        }

        private void SpawnDisasterScene()
        {
            // Victimes à secourir
            for (int i = 0; i < 12; i++)
            {
                var victim = World.CreateRandomPed(_disasterSite + Vector3.RandomXY() * 40f);
                if (victim == null) continue;

                victim.Health = new Random().Next(20, 80); // Blessés
                victim.Task.PlayAnimation("amb@world_human_bum_wash@male@low@idle_a", "idle_a", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                victim.BlockPermanentEvents = true;
                
                _victims.Add(victim);
                
                var victimBlip = victim.AddBlip();
                victimBlip.Sprite = BlipSprite.Health;
                victimBlip.Color = BlipColor.Red;
                victimBlip.Name = "Victime";
                _blips.Add(victimBlip);
            }

            // Équipes de secours
            SpawnRescueTeams();
            
            // Zone de désastre
            var disasterBlip = World.CreateBlip(_disasterSite);
            disasterBlip.Sprite = BlipSprite.BigCircle;
            disasterBlip.Color = BlipColor.Orange;
            disasterBlip.Name = "ZONE SINISTRÉE";
            disasterBlip.Alpha = 128;
            disasterBlip.Scale = 3f;
            _blips.Add(disasterBlip);
        }

        private void SpawnRescueTeams()
        {
            // Ambulances
            for (int i = 0; i < 3; i++)
            {
                var ambulance = World.CreateVehicle(VehicleHash.Ambulance, _disasterSite + new Vector3(30f + i * 10f, 0f, 0f));
                if (ambulance != null)
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, ambulance, true);
                    _emergencyVehicles.Add(ambulance);
                    
                    // Équipe médicale
                    for (int j = 0; j < 2; j++)
                    {
                        var medic = World.CreateRandomPed(ambulance.Position + Vector3.RandomXY() * 3f);
                        if (medic != null)
                        {
                            medic.Task.GuardCurrentPosition();
                            medic.BlockPermanentEvents = true;
                            _rescueTeam.Add(medic);
                        }
                    }
                }
            }

            // Véhicules de pompiers
            for (int i = 0; i < 2; i++)
            {
                var firetruck = World.CreateVehicle(VehicleHash.FireTruck, _disasterSite + new Vector3(-30f, i * 15f, 0f));
                if (firetruck != null)
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, firetruck, true);
                    _emergencyVehicles.Add(firetruck);
                    
                    // Équipe de pompiers
                    for (int j = 0; j < 3; j++)
                    {
                        var firefighter = World.CreateRandomPed(firetruck.Position + Vector3.RandomXY() * 4f);
                        if (firefighter != null)
                        {
                            firefighter.Task.GuardCurrentPosition();
                            firefighter.BlockPermanentEvents = true;
                            _rescueTeam.Add(firefighter);
                        }
                    }
                }
            }
        }

        protected override void OnUpdate()
        {
            var player = Game.Player.Character;
            
            HandleDisasterEffects();
            HandleRescueOperations();
            CheckProgressConditions();
            DisplayInstructions();
        }

        private void HandleDisasterEffects()
        {
            // Répliques occasionnelles
            if (new Random().NextDouble() < 0.005f) // 0.5% chance par frame
            {
                Function.Call(Hash.SHAKE_GAMEPLAY_CAM, "SMALL_EXPLOSION_SHAKE", 0.2f);
                GTA.UI.Notification.Show("~r~Réplique détectée!");
            }
        }

        private void HandleRescueOperations()
        {
            var player = Game.Player.Character;
            
            // Le joueur peut aider les victimes
            foreach (var victim in _victims.Where(v => v.Exists() && v.Health < 100))
            {
                if (player.Position.DistanceTo(victim.Position) < 3f)
                {
                    GTA.UI.Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Porter secours à la victime");
                    
                    if (Game.IsControlJustPressed(Control.Context))
                    {
                        victim.Health = 100;
                        victim.Task.FleeFrom(_disasterSite);
                        GTA.UI.Notification.Show("~g~Victime secourue!~w~ Elle se dirige vers la zone sûre.");
                    }
                }
            }
        }

        private void CheckProgressConditions()
        {
            // Compter les victimes secourues
            var rescuedVictims = _victims.Where(v => v.Exists() && v.Health >= 100).Count();
            var totalVictims = _victims.Count;
            
            if (rescuedVictims >= totalVictims * 0.8f) // 80% secourues
            {
                GTA.UI.Notification.Show("~g~Opération de secours réussie!~w~ La plupart des victimes sont en sécurité.");
                End();
            }
            
            // Timeout après 15 minutes
            if ((DateTime.Now - _disasterStart).TotalMinutes > 15)
            {
                GTA.UI.Notification.Show("~y~Opération de secours terminée.~w~ Les équipes se retirent.");
                End();
            }
        }

        private void DisplayInstructions()
        {
            var rescuedCount = _victims.Where(v => v.Exists() && v.Health >= 100).Count();
            var totalCount = _victims.Count;
            
            GTA.UI.Screen.ShowHelpTextThisFrame($"~r~OPÉRATION DE SECOURS~w~~n~Victimes secourues: {rescuedCount}/{totalCount}");
        }

        protected override void OnEnd()
        {
            foreach (var blip in _blips.Where(b => b.Exists()))
            {
                blip.Delete();
            }
            
            foreach (var victim in _victims.Where(v => v.Exists()))
            {
                victim.MarkAsNoLongerNeeded();
            }
            
            foreach (var rescuer in _rescueTeam.Where(r => r.Exists()))
            {
                rescuer.MarkAsNoLongerNeeded();
            }
            
            foreach (var vehicle in _emergencyVehicles.Where(v => v.Exists()))
            {
                vehicle.MarkAsNoLongerNeeded();
            }
        }
    }
} 