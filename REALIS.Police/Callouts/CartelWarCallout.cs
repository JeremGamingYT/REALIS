using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace REALIS.Police.Callouts
{
    /// <summary>
    /// Guerre entre cartels - Combat urbain intensif
    /// </summary>
    public class CartelWarCallout : CalloutBase
    {
        private readonly List<Ped> _cartelA = new List<Ped>();
        private readonly List<Ped> _cartelB = new List<Ped>();
        private readonly List<Vehicle> _vehicles = new List<Vehicle>();
        private readonly List<Blip> _blips = new List<Blip>();
        
        private Vector3 _warZone;
        private bool _warActive;
        private bool _policeIntervention;
        private DateTime _warStart;
        private int _casualtiesA;
        private int _casualtiesB;

        public CartelWarCallout() : base("GUERRE DE CARTELS", 
            "Affrontement armé entre cartels rivaux. Zone de guerre active.", 
            new Vector3(1394.26f, 3608.81f, 34.98f)) // Sandy Shores area
        {
        }

        public override bool CanSpawn()
        {
            // Toujours disponible pour les tests
            return true;
        }

        protected override void OnStart()
        {
            _warZone = StartPosition;
            _warStart = DateTime.Now;
            _warActive = true;
            
            SpawnCartelWar();
            
            GTA.UI.Notification.Show("~r~ALERTE ROUGE!~w~~n~Guerre de cartels en cours. Approchez avec EXTRÊME prudence!");
        }

        private void SpawnCartelWar()
        {
            // Cartel A (positions ouest)
            for (int i = 0; i < 6; i++)
            {
                var memberA = World.CreatePed(PedHash.MexGoon01GMY, _warZone + new Vector3(-15f + i * 2f, -10f, 0f));
                if (memberA == null) continue;

                memberA.Weapons.Give(WeaponHash.AssaultRifle, 300, true, true);
                memberA.Armor = 100;
                memberA.MaxHealth = 250;
                memberA.Health = 250;
                memberA.BlockPermanentEvents = true;
                
                _cartelA.Add(memberA);
                
                var blipA = memberA.AddBlip();
                blipA.Sprite = BlipSprite.Enemy;
                blipA.Color = BlipColor.Red;
                blipA.Name = "Cartel Alpha";
                _blips.Add(blipA);
            }

            // Véhicules Cartel A
            for (int i = 0; i < 2; i++)
            {
                var vehicleA = World.CreateVehicle(VehicleHash.Insurgent, _warZone + new Vector3(-20f, -5f + i * 5f, 0f));
                if (vehicleA != null)
                {
                    Function.Call(Hash.SET_VEHICLE_COLOURS, vehicleA, 27, 27); // Red color
                    _vehicles.Add(vehicleA);
                }
            }

            // Cartel B (positions est)
            for (int i = 0; i < 6; i++)
            {
                var memberB = World.CreatePed(PedHash.Korean01GMY, _warZone + new Vector3(15f - i * 2f, 10f, 0f));
                if (memberB == null) continue;

                memberB.Weapons.Give(WeaponHash.CarbineRifle, 300, true, true);
                memberB.Armor = 100;
                memberB.MaxHealth = 250;
                memberB.Health = 250;
                memberB.BlockPermanentEvents = true;
                
                _cartelB.Add(memberB);
                
                var blipB = memberB.AddBlip();
                blipB.Sprite = BlipSprite.Enemy;
                blipB.Color = BlipColor.Blue;
                blipB.Name = "Cartel Beta";
                _blips.Add(blipB);
            }

            // Véhicules Cartel B
            for (int i = 0; i < 2; i++)
            {
                var vehicleB = World.CreateVehicle(VehicleHash.Technical, _warZone + new Vector3(20f, 5f - i * 5f, 0f));
                if (vehicleB != null)
                {
                    Function.Call(Hash.SET_VEHICLE_COLOURS, vehicleB, 70, 70); // Blue color
                    _vehicles.Add(vehicleB);
                }
            }

            // Démarrer le combat
            StartCartelFight();
            
            // Zone de guerre principale
            var warBlip = World.CreateBlip(_warZone);
            warBlip.Sprite = BlipSprite.BigCircle;
            warBlip.Color = BlipColor.Red;
            warBlip.Name = "ZONE DE GUERRE";
            warBlip.Alpha = 128;
            warBlip.Scale = 3f;
            _blips.Add(warBlip);
        }

        private void StartCartelFight()
        {
            // Cartel A attaque Cartel B
            foreach (var memberA in _cartelA.Where(m => m.Exists()))
            {
                var targetB = _cartelB.Where(m => m.Exists() && !m.IsDead).FirstOrDefault();
                if (targetB != null)
                {
                    memberA.Task.FightAgainst(targetB);
                }
            }

            // Cartel B attaque Cartel A
            foreach (var memberB in _cartelB.Where(m => m.Exists()))
            {
                var targetA = _cartelA.Where(m => m.Exists() && !m.IsDead).FirstOrDefault();
                if (targetA != null)
                {
                    memberB.Task.FightAgainst(targetA);
                }
            }
        }

        protected override void OnUpdate()
        {
            var player = Game.Player.Character;
            
            if (_warActive)
            {
                HandleWarProgression();
                HandlePlayerIntervention();
            }
            
            if (_policeIntervention)
            {
                HandlePoliceResponse();
            }
            
            CheckEndConditions();
            DisplayWarInfo();
        }

        private void HandleWarProgression()
        {
            // Compter les survivants
            var aliveA = _cartelA.Where(m => m.Exists() && !m.IsDead).Count();
            var aliveB = _cartelB.Where(m => m.Exists() && !m.IsDead).Count();
            
            _casualtiesA = _cartelA.Count - aliveA;
            _casualtiesB = _cartelB.Count - aliveB;
            
            // Explosions aléatoires dans la zone
            if (new Random().NextDouble() < 0.01f) // 1% chance par frame
            {
                var explosionPos = _warZone + Vector3.RandomXY() * 30f;
                World.AddExplosion(explosionPos, ExplosionType.Grenade, 15f, 0.6f);
            }
        }

        private void HandlePlayerIntervention()
        {
            var player = Game.Player.Character;
            
            if (player.Position.DistanceTo(_warZone) < 50f && !_policeIntervention)
            {
                _policeIntervention = true;
                GTA.UI.Notification.Show("~r~POLICE DÉTECTÉE!~w~~n~Les cartels s'allient temporairement contre vous!");
                
                // Les deux cartels s'allient contre le joueur
                foreach (var member in _cartelA.Concat(_cartelB).Where(m => m.Exists() && !m.IsDead))
                {
                    member.Task.FightAgainst(player);
                }
                
                // Backup automatique
                SpawnPoliceBackup();
            }
        }

        private void SpawnPoliceBackup()
        {
            GTA.UI.Notification.Show("~b~Backup en route! SWAT déployé!");
            
            // SWAT vehicles
            for (int i = 0; i < 2; i++)
            {
                var swatVan = World.CreateVehicle(VehicleHash.Riot, _warZone + new Vector3(0f, -50f + i * 20f, 0f));
                if (swatVan != null)
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, swatVan, true);
                    _vehicles.Add(swatVan);
                    
                    // SWAT team
                    for (int j = 0; j < 4; j++)
                    {
                        var swat = World.CreatePed(PedHash.Swat01SMY, swatVan.Position + Vector3.RandomXY() * 3f);
                        if (swat != null)
                        {
                            swat.Weapons.Give(WeaponHash.SpecialCarbine, 400, true, true);
                            swat.Armor = 200;
                            swat.Task.FightAgainst(_cartelA.Concat(_cartelB).FirstOrDefault());
                        }
                    }
                }
            }
        }

        private void HandlePoliceResponse()
        {
            // Police response logic
        }

        private void CheckEndConditions()
        {
            var totalAlive = _cartelA.Concat(_cartelB).Where(m => m.Exists() && !m.IsDead).Count();
            
            if (totalAlive <= 2)
            {
                GTA.UI.Notification.Show("~g~Zone sécurisée!~w~ Guerre de cartels terminée.");
                End();
            }
            
            // Timeout après 10 minutes
            if ((DateTime.Now - _warStart).TotalMinutes > 10)
            {
                GTA.UI.Notification.Show("~y~Les cartels se retirent...~w~ Zone évacuée.");
                End();
            }
        }

        private void DisplayWarInfo()
        {
            var aliveA = _cartelA.Where(m => m.Exists() && !m.IsDead).Count();
            var aliveB = _cartelB.Where(m => m.Exists() && !m.IsDead).Count();
            
            GTA.UI.Screen.ShowHelpTextThisFrame($"~r~ZONE DE GUERRE~w~~n~Cartel Alpha: {aliveA} | Cartel Beta: {aliveB}");
        }

        protected override void OnEnd()
        {
            foreach (var blip in _blips.Where(b => b.Exists()))
            {
                blip.Delete();
            }
            
            foreach (var member in _cartelA.Concat(_cartelB).Where(m => m.Exists()))
            {
                member.MarkAsNoLongerNeeded();
            }
            
            foreach (var vehicle in _vehicles.Where(v => v.Exists()))
            {
                vehicle.MarkAsNoLongerNeeded();
            }
        }
    }
} 