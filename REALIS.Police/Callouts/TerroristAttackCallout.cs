using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.Police.Callouts
{
    /// <summary>
    /// Callout d'attaque terroriste coordonnée avec explosifs.
    /// Événement extrême avec multiples sites d'attaque.
    /// </summary>
    public class TerroristAttackCallout : CalloutBase
    {
        private readonly List<TerroristCell> _terroristCells = new List<TerroristCell>();
        private readonly List<Blip> _blips = new List<Blip>();
        private readonly List<Vector3> _bombSites = new List<Vector3>();
        private readonly List<Ped> _evacuees = new List<Ped>();
        
        private bool _bombThreatActive;
        private DateTime _bombCountdown;
        private int _cellsNeutralized;
        private bool _evacuationInProgress;
        private DateTime _lastExplosion;

        private static readonly Vector3[] TerrorTargets = {
            new Vector3(-74.7f, -818.9f, 326.2f),    // Maze Bank Tower
            new Vector3(-1581.7f, -568.1f, 108.5f),  // Hospital
            new Vector3(441.0f, -979.7f, 30.7f),     // Police Station
            new Vector3(315.7f, -265.4f, 54.0f),     // FIB Building
            new Vector3(-543.0f, -204.6f, 38.2f),    // City Hall
            new Vector3(1692.6f, 3584.5f, 35.6f),    // Military Base
            new Vector3(-1047.9f, -2040.0f, 13.2f)   // Airport
        };

        public TerroristAttackCallout() : base("ALERTE TERRORISTE", 
            "Attaque terroriste coordonnée détectée. Multiples sites menacés!", 
            TerrorTargets[new Random().Next(TerrorTargets.Length)])
        {
        }

        public override bool CanSpawn()
        {
            // Toujours disponible pour les tests
            return true;
        }

        protected override void OnStart()
        {
            GTA.UI.Notification.Show("~r~ALERTE TERRORISTE NIVEAU ROUGE~w~~n~Multiples menaces détectées!");
            
            // Son d'alarme d'urgence
            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "EMERGENCY_SIREN", "DLC_HEIST_BIOLAB_PREP_HACKING_SOUNDS");
            
            SpawnTerroristCells();
            SetupBombThreats();
            InitiateEvacuation();
            
            _bombThreatActive = true;
            _bombCountdown = DateTime.Now.AddMinutes(10); // 10 minutes pour désamorcer
            
            // Alerte générale
            var emergencyBlip = World.CreateBlip(StartPosition);
            emergencyBlip.Sprite = BlipSprite.BigCircle;
            emergencyBlip.Color = BlipColor.Red;
            emergencyBlip.Name = "MENACE TERRORISTE";
            emergencyBlip.Alpha = 128;
            emergencyBlip.Scale = 2f;
            _blips.Add(emergencyBlip);
        }

        private void SpawnTerroristCells()
        {
            var selectedTargets = TerrorTargets.OrderBy(x => new Random().Next()).Take(3).ToArray();
            
            foreach (var target in selectedTargets)
            {
                var cell = new TerroristCell
                {
                    Position = target,
                    Terrorists = new List<Ped>(),
                    Vehicle = null,
                    IsNeutralized = false,
                    HasBomb = new Random().NextDouble() < 0.7f // 70% chance
                };

                // Spawn terroristes
                for (int i = 0; i < new Random().Next(3, 6); i++)
                {
                    var terrorist = World.CreateRandomPed(target + Vector3.RandomXY() * 10f);
                    if (terrorist == null) continue;

                    // Equipment lourd
                    terrorist.Weapons.Give(WeaponHash.AssaultRifle, 300, true, true);
                    terrorist.Weapons.Give(WeaponHash.RPG, 5, false, true);
                    terrorist.Weapons.Give(WeaponHash.Grenade, 10, false, false);
                    
                    terrorist.Armor = 150;
                    terrorist.MaxHealth = 400;
                    terrorist.Health = 400;
                    terrorist.BlockPermanentEvents = true;
                    terrorist.RelationshipGroup = Game.GenerateHash("TERRORIST");
                    
                    // Apparence menaçante
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, terrorist, 1, 52, 0, 0); // Mask
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, terrorist, 11, 220, 0, 0); // Vest
                    
                    cell.Terrorists.Add(terrorist);
                    
                    var terroristBlip = terrorist.AddBlip();
                    terroristBlip.Sprite = BlipSprite.Enemy;
                    terroristBlip.Color = BlipColor.Red;
                    terroristBlip.Name = "Terroriste";
                    _blips.Add(terroristBlip);
                }

                // Véhicule piégé
                if (cell.HasBomb)
                {
                    var bombVehicle = World.CreateVehicle(VehicleHash.Insurgent2, target + new Vector3(5f, 0f, 0f));
                    if (bombVehicle != null)
                    {
                        cell.Vehicle = bombVehicle;
                        _bombSites.Add(bombVehicle.Position);
                        
                        var bombBlip = bombVehicle.AddBlip();
                        bombBlip.Sprite = BlipSprite.Detonator;
                        bombBlip.Color = BlipColor.Red;
                        bombBlip.Name = "VÉHICULE PIÉGÉ";
                        bombBlip.IsFlashing = true;
                        _blips.Add(bombBlip);
                    }
                }

                _terroristCells.Add(cell);
            }
        }

        private void SetupBombThreats()
        {
            // Placer des explosifs dans des lieux stratégiques
            var additionalBombSites = new Vector3[]
            {
                new Vector3(-1266.8f, -3014.1f, 7.8f),   // Docks
                new Vector3(2540.6f, 2594.9f, 37.9f),    // Prison
                new Vector3(-775.1f, 5598.1f, 33.6f)     // Paleto Bay
            };

            foreach (var site in additionalBombSites.Take(2))
            {
                _bombSites.Add(site);
                
                var bombBlip = World.CreateBlip(site);
                bombBlip.Sprite = BlipSprite.Detonator;
                bombBlip.Color = BlipColor.Orange;
                bombBlip.Name = "EXPLOSIF SUSPECT";
                bombBlip.IsFlashing = true;
                _blips.Add(bombBlip);
            }
        }

        private void InitiateEvacuation()
        {
            _evacuationInProgress = true;
            
            // Spawn civils paniqués
            for (int i = 0; i < 15; i++)
            {
                var civilian = World.CreateRandomPed(StartPosition + Vector3.RandomXY() * 50f);
                if (civilian == null) continue;

                civilian.Task.FleeFrom(StartPosition);
                civilian.BlockPermanentEvents = false;
                _evacuees.Add(civilian);
            }

            // Véhicules d'urgence
            SpawnEmergencyVehicles();
        }

        private void SpawnEmergencyVehicles()
        {
            var emergencyPositions = new Vector3[]
            {
                StartPosition + new Vector3(20f, 0f, 0f),
                StartPosition + new Vector3(-20f, 15f, 0f),
                StartPosition + new Vector3(0f, -25f, 0f)
            };

            foreach (var pos in emergencyPositions)
            {
                // Ambulances
                var ambulance = World.CreateVehicle(VehicleHash.Ambulance, pos);
                if (ambulance != null)
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, ambulance, true);
                    var paramedic = World.CreateRandomPed(pos);
                    if (paramedic != null)
                    {
                        paramedic.SetIntoVehicle(ambulance, VehicleSeat.Driver);
                    }
                }

                // Camions de pompiers
                var firetruck = World.CreateVehicle(VehicleHash.FireTruck, pos + new Vector3(10f, 0f, 0f));
                if (firetruck != null)
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, firetruck, true);
                }
            }

            // Police response vehicles  
            for (int i = 0; i < 3; i++)
            {
                var policecar = World.CreateVehicle(VehicleHash.Police2, StartPosition + new Vector3(30f + i * 10f, 0f, 0f));
                if (policecar != null)
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, policecar, true);
                    
                    var officer = World.CreateRandomPed(policecar.Position);
                    if (officer != null)
                    {
                        officer.SetIntoVehicle(policecar, VehicleSeat.Driver);
                        officer.Weapons.Give(WeaponHash.CarbineRifle, 100, true, true);
                        officer.Task.GuardCurrentPosition();
                    }
                }
            }

            // SWAT response vehicles
            for (int i = 0; i < 2; i++)
            {
                var swatVan = World.CreateVehicle(VehicleHash.Riot, StartPosition + new Vector3(-40f, i * 15f, 0f));
                if (swatVan != null)
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, swatVan, true);
                    
                    for (int j = 0; j < 4; j++)
                    {
                        var swat = World.CreateRandomPed(swatVan.Position + Vector3.RandomXY() * 3f);
                        if (swat != null)
                        {
                            swat.Weapons.Give(WeaponHash.SpecialCarbine, 300, true, true);
                            swat.Armor = 200;
                            swat.Task.FightAgainst(_terroristCells.FirstOrDefault(c => !c.IsNeutralized)?.Terrorists.FirstOrDefault());
                        }
                    }
                }
            }
        }

        protected override void OnUpdate()
        {
            var player = Game.Player.Character;
            
            UpdateBombCountdown();
            UpdateTerroristCells();
            UpdateEvacuation();
            CheckPlayerActions();
            
            // Interface utilisateur
            DisplayThreatStatus();
            
            CheckEndConditions();
        }

        private void UpdateBombCountdown()
        {
            if (!_bombThreatActive) return;
            
            var timeLeft = _bombCountdown - DateTime.Now;
            
            if (timeLeft.TotalSeconds <= 0)
            {
                // EXPLOSION!
                DetonateBombs();
                _bombThreatActive = false;
            }
            else if (timeLeft.TotalMinutes < 2)
            {
                // Alerte critique
                if ((DateTime.Now - _lastExplosion).TotalSeconds > 5)
                {
                    _lastExplosion = DateTime.Now;
                    GTA.UI.Notification.Show($"~r~URGENCE: {timeLeft.Minutes:00}:{timeLeft.Seconds:00} AVANT DÉTONATION!");
                    Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "TIMER_STOP", "HUD_MINI_GAME_SOUNDSET");
                }
            }
        }

        private void UpdateTerroristCells()
        {
            foreach (var cell in _terroristCells.Where(c => !c.IsNeutralized))
            {
                var aliveTerrorists = cell.Terrorists.Where(t => t.Exists() && !t.IsDead).ToList();
                
                if (aliveTerrorists.Count == 0)
                {
                    cell.IsNeutralized = true;
                    _cellsNeutralized++;
                    GTA.UI.Notification.Show($"~g~CELLULE TERRORISTE NEUTRALISÉE! ({_cellsNeutralized}/{_terroristCells.Count})");
                    
                    // Récompense: désamorcer une bombe
                    if (cell.HasBomb && cell.Vehicle != null && cell.Vehicle.Exists())
                    {
                        DefuseBomb(cell.Vehicle.Position);
                    }
                }
                else
                {
                    // Terroristes attaquent agressivement
                    foreach (var terrorist in aliveTerrorists)
                    {
                        if (terrorist.Position.DistanceTo(Game.Player.Character.Position) < 100f)
                        {
                            terrorist.Task.FightAgainst(Game.Player.Character);
                        }
                    }
                }
            }
        }

        private void UpdateEvacuation()
        {
            if (!_evacuationInProgress) return;
            
            // S'assurer que les civils fuient
            foreach (var evacuee in _evacuees.Where(e => e.Exists() && !e.IsDead))
            {
                if (evacuee.Position.DistanceTo(StartPosition) < 100f)
                {
                    evacuee.Task.FleeFrom(StartPosition);
                }
            }
        }

        private void CheckPlayerActions()
        {
            var player = Game.Player.Character;
            
            // Désamorçage manuel des bombes
            foreach (var bombSite in _bombSites.ToList())
            {
                if (player.Position.DistanceTo(bombSite) < 3f)
                {
                    GTA.UI.Screen.ShowHelpTextThisFrame("~r~EXPLOSIF DÉTECTÉ~w~~n~~INPUT_CONTEXT~ Tenter le désamorçage");
                    
                    if (Game.IsControlJustPressed(Control.Context))
                    {
                        AttemptBombDefusal(bombSite);
                    }
                }
            }
        }

        private void AttemptBombDefusal(Vector3 bombPosition)
        {
            var success = new Random().NextDouble() < 0.8f; // 80% de succès
            
            if (success)
            {
                DefuseBomb(bombPosition);
                GTA.UI.Notification.Show("~g~EXPLOSIF DÉSAMORCÉ AVEC SUCCÈS!");
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "BOMB_DISARMED", "GTAO_SPEED_DRONE_RACE_SOUNDSET");
            }
            else
            {
                GTA.UI.Notification.Show("~r~ÉCHEC DU DÉSAMORÇAGE! Tentez à nouveau!");
                // Petite explosion
                World.AddExplosion(bombPosition, ExplosionType.Grenade, 5f, 0.3f);
            }
        }

        private void DefuseBomb(Vector3 bombPosition)
        {
            _bombSites.Remove(bombPosition);
            
            // Effets visuels de désamorçage
            Function.Call(Hash.ADD_EXPLOSION, bombPosition.X, bombPosition.Y, bombPosition.Z, 
                (int)ExplosionType.Flare, 0.0f, true, false, 0.1f);
            
            // Retirer les blips associés
            var bombBlips = _blips.Where(b => b.Exists() && b.Position.DistanceTo(bombPosition) < 5f).ToList();
            foreach (var blip in bombBlips)
            {
                blip.Delete();
                _blips.Remove(blip);
            }

            if (_bombSites.Count == 0)
            {
                _bombThreatActive = false;
                GTA.UI.Notification.Show("~g~TOUTES LES BOMBES DÉSAMORCÉES! MENACE NEUTRALISÉE!");
            }
        }

        private void DetonateBombs()
        {
            foreach (var bombSite in _bombSites)
            {
                World.AddExplosion(bombSite, ExplosionType.Tanker, 20f, 1.0f);
                
                // Effets secondaires
                Script.Wait(new Random().Next(500, 2000));
                World.AddExplosion(bombSite + Vector3.RandomXY() * 10f, ExplosionType.Car, 15f, 0.8f);
            }
            
            GTA.UI.Notification.Show("~r~LES BOMBES ONT EXPLOSÉ! CATASTROPHE!");
        }

        private void DisplayThreatStatus()
        {
            if (_bombThreatActive)
            {
                var timeLeft = _bombCountdown - DateTime.Now;
                var color = timeLeft.TotalMinutes < 2 ? "~r~" : "~y~";
                
                GTA.UI.Screen.ShowSubtitle($"{color}DÉTONATION: {timeLeft.Minutes:00}:{timeLeft.Seconds:00}~w~ | " +
                                         $"Cellules: {_cellsNeutralized}/{_terroristCells.Count} | " +
                                         $"Bombes: {_bombSites.Count}");
            }
        }

        private void CheckEndConditions()
        {
            var allCellsNeutralized = _cellsNeutralized >= _terroristCells.Count;
            var allBombsDefused = _bombSites.Count == 0;
            
            if (allCellsNeutralized && allBombsDefused)
            {
                GTA.UI.Notification.Show("~g~MENACE TERRORISTE COMPLÈTEMENT NEUTRALISÉE!~w~~n~Excellent travail, officier!");
                End();
            }
            else if (!_bombThreatActive && _bombSites.Count > 0)
            {
                // Bombes ont explosé
                GTA.UI.Notification.Show("~r~MISSION ÉCHOUÉE~w~~n~Les explosions ont causé des dégâts massifs...");
                Script.Wait(5000);
                End();
            }
        }

        protected override void OnEnd()
        {
            // Nettoyage
            foreach (var blip in _blips.Where(b => b.Exists()))
            {
                blip.Delete();
            }

            foreach (var cell in _terroristCells)
            {
                foreach (var terrorist in cell.Terrorists.Where(t => t.Exists()))
                {
                    terrorist.MarkAsNoLongerNeeded();
                }
                
                if (cell.Vehicle != null && cell.Vehicle.Exists())
                {
                    cell.Vehicle.MarkAsNoLongerNeeded();
                }
            }

            foreach (var evacuee in _evacuees.Where(e => e.Exists()))
            {
                evacuee.MarkAsNoLongerNeeded();
            }
        }

        private class TerroristCell
        {
            public Vector3 Position { get; set; }
            public List<Ped> Terrorists { get; set; }
            public Vehicle Vehicle { get; set; }
            public bool IsNeutralized { get; set; }
            public bool HasBomb { get; set; }
        }
    }
} 