using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace REALIS.Police.Callouts
{
    public class StreetRacingCallout : CalloutBase
    {
        private readonly List<Vehicle> _raceCars = new List<Vehicle>();
        private readonly List<Ped> _racers = new List<Ped>();
        private readonly List<Ped> _spectators = new List<Ped>();
        private readonly List<Blip> _blips = new List<Blip>();
        
        private Vector3 _raceLocation;
        private bool _dispersalActive;
        private DateTime _lastSiren;

        public StreetRacingCallout() : base("COURSE ILLÉGALE", 
            "Course de rue illégale en cours. Multiples spectateurs sur place.", 
            new Vector3(717.61f, -1064.76f, 21.95f)) // LSIA area
        {
        }

        public override bool CanSpawn()
        {
            // Plus fréquent la nuit
            var currentHour = World.CurrentTimeOfDay.Hours;
            return currentHour >= 22 || currentHour <= 4;
        }

        protected override void OnStart()
        {
            _raceLocation = StartPosition;
            _lastSiren = DateTime.Now.AddSeconds(-10); // Permet usage immédiat
            
            SpawnRaceAndSpectators();
            
            GTA.UI.Notification.Show("~r~COURSE ILLÉGALE DÉTECTÉE!~w~~n~Dispersez la foule avec la sirène!");
        }

        private void SpawnRaceAndSpectators()
        {
            // Voitures de course modifiées
            var raceVehicles = new VehicleHash[] 
            { 
                VehicleHash.Elegy, VehicleHash.Sultan, VehicleHash.Kuruma, VehicleHash.Jester 
            };

            for (int i = 0; i < 4; i++)
            {
                var raceCar = World.CreateVehicle(raceVehicles[i], _raceLocation + new Vector3(i * 5f, 0f, 0f));
                if (raceCar == null) continue;

                // Customisation visuelle
                Function.Call(Hash.SET_VEHICLE_COLOURS, raceCar, i * 10, i * 10);
                Function.Call(Hash.SET_VEHICLE_MOD, raceCar, 0, -1, false); // Spoiler
                
                var racer = World.CreateRandomPed(_raceLocation + new Vector3(i * 5f, -2f, 0f));
                if (racer != null)
                {
                    racer.SetIntoVehicle(raceCar, VehicleSeat.Driver);
                    racer.Task.CruiseWithVehicle(raceCar, 45f, DrivingStyle.Rushed);
                    racer.BlockPermanentEvents = true;
                    _racers.Add(racer);
                }
                
                _raceCars.Add(raceCar);
                
                var carBlip = raceCar.AddBlip();
                carBlip.Sprite = BlipSprite.Enemy;
                carBlip.Color = BlipColor.Orange;
                carBlip.Name = "Coureur";
                _blips.Add(carBlip);
            }

            // Spectateurs nombreux
            for (int i = 0; i < 20; i++)
            {
                var spectator = World.CreateRandomPed(_raceLocation + Vector3.RandomXY() * 25f);
                if (spectator == null) continue;

                spectator.Task.StartScenario("WORLD_HUMAN_CHEERING", 0);
                spectator.BlockPermanentEvents = true;
                _spectators.Add(spectator);
            }

            // Explosions spectaculaires de départ
            for (int i = 0; i < 3; i++)
            {
                World.AddExplosion(_raceLocation + Vector3.RandomXY() * 10f, ExplosionType.Flare, 2f, 0.3f);
            }
        }

        protected override void OnUpdate()
        {
            var player = Game.Player.Character;
            
            HandleSirenDispersal();
            HandleRacerBehavior();
            CheckEndConditions();
            DisplayInstructions();
        }

        private void HandleSirenDispersal()
        {
            var player = Game.Player.Character;
            
            // Système de sirène pour disperser
            if (player.IsInVehicle() && 
                Game.IsKeyPressed(System.Windows.Forms.Keys.E) && 
                (DateTime.Now - _lastSiren).TotalSeconds > 3)
            {
                _lastSiren = DateTime.Now;
                _dispersalActive = true;
                
                GTA.UI.Notification.Show("~b~Sirène activée!~w~ Les spectateurs se dispersent!");
                
                // Disperser les spectateurs proches
                foreach (var spectator in _spectators.Where(s => s.Exists()))
                {
                    if (player.Position.DistanceTo(spectator.Position) < 50f)
                    {
                        spectator.Task.FleeFrom(player.Position);
                    }
                }
            }
        }

        private void HandleRacerBehavior()
        {
            var player = Game.Player.Character;
            
            // Les coureurs réagissent à l'approche de la police
            foreach (var racer in _racers.Where(r => r.Exists() && r.IsInVehicle()))
            {
                if (player.Position.DistanceTo(racer.Position) < 40f)
                {
                    var vehicle = racer.CurrentVehicle;
                    if (vehicle != null && vehicle.Exists())
                    {
                        racer.Task.FleeFrom(player.Position);
                    }
                }
            }
        }

        private void CheckEndConditions()
        {
            // Compter les spectateurs restants
            var remainingSpectators = _spectators.Where(s => s.Exists() && 
                s.Position.DistanceTo(_raceLocation) < 100f).Count();
                
            var remainingRacers = _racers.Where(r => r.Exists() && 
                r.Position.DistanceTo(_raceLocation) < 100f).Count();
            
            if (remainingSpectators <= 5 && remainingRacers <= 1)
            {
                GTA.UI.Notification.Show("~g~Zone dispersée!~w~ Course illégale interrompue avec succès.");
                End();
            }
        }

        private void DisplayInstructions()
        {
            var player = Game.Player.Character;
            
            if (player.IsInVehicle())
            {
                GTA.UI.Screen.ShowHelpTextThisFrame("~g~E~w~ - Activer la sirène pour disperser");
            }
        }

        protected override void OnEnd()
        {
            foreach (var blip in _blips.Where(b => b.Exists()))
            {
                blip.Delete();
            }
            
            foreach (var racer in _racers.Where(r => r.Exists()))
            {
                racer.MarkAsNoLongerNeeded();
            }
            
            foreach (var car in _raceCars.Where(c => c.Exists()))
            {
                car.MarkAsNoLongerNeeded();
            }
            
            foreach (var spectator in _spectators.Where(s => s.Exists()))
            {
                spectator.MarkAsNoLongerNeeded();
            }
        }
    }
} 