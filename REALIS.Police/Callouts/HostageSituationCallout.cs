using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.Police.Callouts
{
    public class HostageSituationCallout : CalloutBase
    {
        private readonly List<Ped> _hostages = new List<Ped>();
        private readonly List<Ped> _criminals = new List<Ped>();
        private readonly List<Ped> _swatTeam = new List<Ped>();
        private readonly List<Blip> _blips = new List<Blip>();
        
        private Vector3 _buildingEntrance;
        private bool _negotiationActive;
        private bool _swatDeployed;
        private DateTime _negotiationStart;
        private int _hostagsReleased;

        public HostageSituationCallout() : base("PRISE D'OTAGES", 
            "Prise d'otages en cours dans un bâtiment commercial.", 
            new Vector3(1199.5f, -3253.6f, 7.1f))
        {
            _buildingEntrance = StartPosition;
        }

        protected override void OnStart()
        {
            GTA.UI.Notification.Show("~r~PRISE D'OTAGES EN COURS~w~~n~Approchez avec précaution!");
            
            SpawnHostageSituation();
            _negotiationActive = true;
            _negotiationStart = DateTime.Now;
        }

        private void SpawnHostageSituation()
        {
            // Criminels
            for (int i = 0; i < 3; i++)
            {
                var criminal = World.CreateRandomPed(_buildingEntrance + Vector3.RandomXY() * 5f);
                if (criminal == null) continue;

                criminal.Weapons.Give(WeaponHash.AssaultRifle, 200, true, true);
                criminal.BlockPermanentEvents = true;
                criminal.Task.GuardCurrentPosition();
                _criminals.Add(criminal);

                var crimBlip = criminal.AddBlip();
                crimBlip.Sprite = BlipSprite.Enemy;
                crimBlip.Color = BlipColor.Red;
                crimBlip.Name = "Preneur d'otages";
                _blips.Add(crimBlip);
            }

            // Otages
            for (int i = 0; i < 5; i++)
            {
                var hostage = World.CreateRandomPed(_buildingEntrance + Vector3.RandomXY() * 3f);
                if (hostage == null) continue;

                hostage.Task.HandsUp(-1);
                hostage.BlockPermanentEvents = true;
                _hostages.Add(hostage);

                var hostageBlip = hostage.AddBlip();
                hostageBlip.Sprite = BlipSprite.PointOfInterest;
                hostageBlip.Color = BlipColor.Orange;
                hostageBlip.Name = "Otage";
                _blips.Add(hostageBlip);
            }
        }

        protected override void OnUpdate()
        {
            if (_negotiationActive)
            {
                HandleNegotiation();
            }
            
            if (_swatDeployed)
            {
                UpdateSwatOperation();
            }
            
            CheckEndConditions();
        }

        private void HandleNegotiation()
        {
            var player = Game.Player.Character;
            var distance = player.Position.DistanceTo(_buildingEntrance);
            
            if (distance < 20f)
            {
                GTA.UI.Screen.ShowHelpTextThisFrame("~INPUT_TALK~ Négocier | ~INPUT_AIM~ Déployer SWAT");
                
                if (Game.IsControlJustPressed(Control.Talk))
                {
                    AttemptNegotiation();
                }
                else if (Game.IsControlJustPressed(Control.Aim))
                {
                    DeploySwat();
                }
            }
        }

        private void AttemptNegotiation()
        {
            var success = new Random().NextDouble() < 0.5f;
            
            if (success && _hostagsReleased < _hostages.Count)
            {
                var hostage = _hostages.Where(h => h.Exists()).FirstOrDefault();
                if (hostage != null)
                {
                    hostage.Task.RunTo(Game.Player.Character.Position);
                    _hostagsReleased++;
                    GTA.UI.Notification.Show($"~g~Otage libéré! ({_hostagsReleased}/{_hostages.Count})");
                }
            }
            else
            {
                GTA.UI.Notification.Show("~r~Négociation échouée!");
            }
        }

        private void DeploySwat()
        {
            if (_swatDeployed) return;
            
            _swatDeployed = true;
            _negotiationActive = false;
            
            GTA.UI.Notification.Show("~b~SWAT DÉPLOYÉ!");
            
            // Équipe SWAT
            for (int i = 0; i < 4; i++)
            {
                var swat = World.CreateRandomPed(_buildingEntrance + Vector3.RandomXY() * 15f);
                if (swat == null) continue;

                swat.Weapons.Give(WeaponHash.SpecialCarbine, 300, true, true);
                swat.Armor = 200;
                swat.Task.FightAgainst(_criminals.FirstOrDefault());
                _swatTeam.Add(swat);
            }
        }

        private void UpdateSwatOperation()
        {
            // SWAT avance vers les criminels
            foreach (var swat in _swatTeam.Where(s => s.Exists() && !s.IsDead))
            {
                var nearestCriminal = _criminals.Where(c => c.Exists() && !c.IsDead).OrderBy(c => 
                    c.Position.DistanceTo(swat.Position)).FirstOrDefault();
                
                if (nearestCriminal != null)
                {
                    swat.Task.FightAgainst(nearestCriminal);
                }
            }
        }

        private void CheckEndConditions()
        {
            var aliveCriminals = _criminals.Where(c => c.Exists() && !c.IsDead).Count();
            var aliveHostages = _hostages.Where(h => h.Exists() && !h.IsDead).Count();
            
            if (aliveCriminals == 0)
            {
                var rating = aliveHostages == _hostages.Count ? "PARFAIT" : "RÉUSSI";
                GTA.UI.Notification.Show($"~g~SITUATION RÉSOLUE - {rating}!");
                End();
            }
        }

        protected override void OnEnd()
        {
            foreach (var blip in _blips.Where(b => b.Exists()))
            {
                blip.Delete();
            }

            foreach (var criminal in _criminals.Where(c => c.Exists()))
            {
                criminal.MarkAsNoLongerNeeded();
            }

            foreach (var hostage in _hostages.Where(h => h.Exists()))
            {
                hostage.MarkAsNoLongerNeeded();
            }

            foreach (var swat in _swatTeam.Where(s => s.Exists()))
            {
                swat.MarkAsNoLongerNeeded();
            }
        }
    }
} 