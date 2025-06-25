using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace REALIS.Police.Callouts
{
    /// <summary>
    /// Callout de braquage de banque avec otages et négociation.
    /// Événement majeur avec multiples phases.
    /// </summary>
    public class BankRobberyCallout : CalloutBase
    {
        private readonly List<Ped> _robbers = new List<Ped>();
        private readonly List<Vehicle> _getawayVehicles = new List<Vehicle>();
        private readonly List<Ped> _hostages = new List<Ped>();
        private readonly List<Blip> _blips = new List<Blip>();
        
        private Vector3 _bankLocation;
        private bool _negotiationActive;
        private bool _assaultPhase;
        private bool _escapePhase;
        private DateTime _lastNegotiation;
        private int _negotiationAttempts;
        private bool _shotsHard;

        public BankRobberyCallout() : base("BRAQUAGE DE BANQUE", 
            "Braquage en cours dans une banque. Situation d'otages confirmée.", 
            new Vector3(146.78f, -1045.71f, 29.37f)) // Fleeca Bank Downtown
        {
        }

        public override bool CanSpawn()
        {
            // Toujours disponible pour les tests  
            return true;
        }

        protected override void OnStart()
        {
            _bankLocation = StartPosition;
            _negotiationActive = true;
            _lastNegotiation = DateTime.Now;
            _negotiationAttempts = 0;

            SpawnRobbersAndHostages();
            SetupPerimeter();
            
            GTA.UI.Notification.Show("~r~BRAQUAGE EN COURS!~w~~n~Négociation disponible avec la touche ~g~T~w~");
            GTA.UI.Notification.Show("~b~Dispatch:~w~ SWAT disponible. Utilisez ~g~B~w~ pour déployer l'assaut.");
        }

        private void SpawnRobbersAndHostages()
        {
            // Braqueurs principaux
            var robberModels = new PedHash[] 
            { 
                PedHash.Lost01GMY, 
                PedHash.Lost02GMY,
                PedHash.Lost03GMY
            };

            for (int i = 0; i < 3; i++)
            {
                var robber = World.CreatePed(robberModels[i], _bankLocation + Vector3.RandomXY() * 5f);
                if (robber == null) continue;

                robber.Weapons.Give(WeaponHash.AssaultRifle, 200, true, true);
                robber.Armor = 100;
                robber.MaxHealth = 300;
                robber.Health = 300;
                robber.BlockPermanentEvents = true;
                robber.Task.GuardCurrentPosition();
                
                _robbers.Add(robber);
                
                var robberBlip = robber.AddBlip();
                robberBlip.Sprite = BlipSprite.Enemy;
                robberBlip.Color = BlipColor.Red;
                robberBlip.Name = "Braqueur";
                _blips.Add(robberBlip);
            }

            // Véhicule d'évasion
            var getawayCar = World.CreateVehicle(VehicleHash.Kuruma, _bankLocation + new Vector3(10f, 0f, 0f));
            if (getawayCar != null)
            {
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, getawayCar, false, true, false);
                getawayCar.LockStatus = VehicleLockStatus.Locked;
                _getawayVehicles.Add(getawayCar);
                
                var carBlip = getawayCar.AddBlip();
                carBlip.Sprite = BlipSprite.GetawayCar;
                carBlip.Color = BlipColor.Red;
                carBlip.Name = "Véhicule d'évasion";
                _blips.Add(carBlip);
            }

            // Otages
            for (int i = 0; i < 5; i++)
            {
                var hostage = World.CreateRandomPed(_bankLocation + Vector3.RandomXY() * 3f);
                if (hostage == null) continue;

                hostage.Task.HandsUp(-1);
                hostage.AlwaysKeepTask = true;
                hostage.BlockPermanentEvents = true;
                hostage.CanBeDraggedOutOfVehicle = false;
                
                _hostages.Add(hostage);
                
                var hostageBlip = hostage.AddBlip();
                hostageBlip.Sprite = BlipSprite.Friend;
                hostageBlip.Color = BlipColor.Blue;
                hostageBlip.Name = "Otage";
                _blips.Add(hostageBlip);
            }
        }

        private void SetupPerimeter()
        {
            // Police perimeter vehicles
            var positions = new Vector3[]
            {
                _bankLocation + new Vector3(30f, 15f, 0f),
                _bankLocation + new Vector3(-25f, 20f, 0f),
                _bankLocation + new Vector3(20f, -30f, 0f)
            };

            foreach (var pos in positions)
            {
                var policecar = World.CreateVehicle(VehicleHash.Police, pos);
                if (policecar != null)
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, policecar, true);
                    
                    var cop = World.CreateRandomPed(pos);
                    if (cop != null)
                    {
                        cop.SetIntoVehicle(policecar, VehicleSeat.Driver);
                        cop.Task.GuardCurrentPosition();
                        cop.BlockPermanentEvents = true;
                    }
                }
            }
        }

        protected override void OnUpdate()
        {
            var player = Game.Player.Character;
            
            if (_negotiationActive)
            {
                HandleNegotiation();
            }
            else if (_assaultPhase)
            {
                HandleAssault();
            }
            else if (_escapePhase)
            {
                HandleEscape();
            }
            
            CheckProgressConditions();
            DisplayInstructions();
        }

        private void HandleNegotiation()
        {
            var player = Game.Player.Character;
            
            // Négociation (touche T)
            if (Game.IsKeyPressed(System.Windows.Forms.Keys.T) && 
                (DateTime.Now - _lastNegotiation).TotalSeconds > 10)
            {
                _lastNegotiation = DateTime.Now;
                _negotiationAttempts++;
                
                var success = new Random().NextDouble() < (0.1f + _negotiationAttempts * 0.15f);
                
                if (success)
                {
                    GTA.UI.Notification.Show("~g~SUCCÈS!~w~ Les braqueurs acceptent de se rendre!");
                    StartSurrender();
                }
                else
                {
                    var responses = new string[]
                    {
                        "~r~Braqueur:~w~ Pas question! Restez où vous êtes!",
                        "~r~Braqueur:~w~ On a des otages! Ne tentez rien!",
                        "~r~Braqueur:~w~ Amenez-nous un hélico ou ils meurent!"
                    };
                    
                    GTA.UI.Notification.Show(responses[new Random().Next(responses.Length)]);
                    
                    if (_negotiationAttempts >= 5)
                    {
                        GTA.UI.Notification.Show("~r~Négociation échouée.~w~ Préparez l'assaut!");
                    }
                }
            }
            
            // Déploiement SWAT (touche B)
            if (Game.IsKeyPressed(System.Windows.Forms.Keys.B))
            {
                StartAssault();
            }
        }

        private void StartSurrender()
        {
            _negotiationActive = false;
            
            foreach (var robber in _robbers.Where(r => r.Exists() && !r.IsDead))
            {
                robber.Task.HandsUp(-1);
                robber.AlwaysKeepTask = true;
                robber.Weapons.RemoveAll();
            }
            
            foreach (var hostage in _hostages.Where(h => h.Exists()))
            {
                hostage.Task.FleeFrom(_bankLocation);
            }
            
            GTA.UI.Notification.Show("~g~Mission réussie!~w~ Tous les suspects se rendent pacifiquement.");
        }

        private void StartAssault()
        {
            _negotiationActive = false;
            _assaultPhase = true;
            
            GTA.UI.Notification.Show("~r~ASSAUT LANCÉ!~w~ SWAT en approche!");
            
            // SWAT team
            for (int i = 0; i < 4; i++)
            {
                var swat = World.CreatePed(PedHash.Swat01SMY, _bankLocation + Vector3.RandomXY() * 20f);
                if (swat == null) continue;

                swat.Weapons.Give(WeaponHash.CarbineRifle, 300, true, true);
                swat.Armor = 200;
                swat.Task.FightAgainst(Game.Player.Character);
            }
            
            // Les braqueurs deviennent hostiles
            foreach (var robber in _robbers.Where(r => r.Exists()))
            {
                robber.Task.FightAgainst(Game.Player.Character);
            }
        }

        private void HandleAssault()
        {
            // Si tous les braqueurs sont neutralisés
            if (_robbers.Where(r => r.Exists() && !r.IsDead).Count() == 0)
            {
                _assaultPhase = false;
                
                foreach (var hostage in _hostages.Where(h => h.Exists()))
                {
                    hostage.Task.FleeFrom(_bankLocation);
                }
                
                GTA.UI.Notification.Show("~g~Zone sécurisée!~w~ Tous les braqueurs neutralisés.");
                End();
            }
        }

        private void HandleEscape()
        {
            // Logic for escape phase if needed
        }

        private void CheckProgressConditions()
        {
            // Si trop d'otages sont morts
            var deadHostages = _hostages.Where(h => h.Exists() && h.IsDead).Count();
            if (deadHostages >= 3)
            {
                GTA.UI.Notification.Show("~r~ÉCHEC CRITIQUE!~w~ Trop d'otages ont été tués.");
                End();
            }
        }

        private void DisplayInstructions()
        {
            if (_negotiationActive)
            {
                GTA.UI.Screen.ShowHelpTextThisFrame("~g~T~w~ - Négocier | ~g~B~w~ - Assaut SWAT");
            }
        }

        protected override void OnEnd()
        {
            foreach (var blip in _blips.Where(b => b.Exists()))
            {
                blip.Delete();
            }
            
            foreach (var robber in _robbers.Where(r => r.Exists()))
            {
                robber.MarkAsNoLongerNeeded();
            }
            
            foreach (var vehicle in _getawayVehicles.Where(v => v.Exists()))
            {
                vehicle.MarkAsNoLongerNeeded();
            }
            
            foreach (var hostage in _hostages.Where(h => h.Exists()))
            {
                hostage.MarkAsNoLongerNeeded();
            }
        }
    }
} 