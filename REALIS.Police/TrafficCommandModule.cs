using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Common;
using LemonUI;
using LemonUI.Menus;

namespace REALIS.Police
{
    /// <summary>
    /// Permet d'ordonner à un véhicule civil ciblé de se ranger ou de suivre le joueur.
    /// Double-appui rapide sur « E » – Le conducteur se range.
    /// Appui sur « K » – Le véhicule suit le joueur.
    /// </summary>
    public class TrafficCommandModule : IModule
    {
        private const Keys PullOverKey = Keys.E; // double-tap
        private const Keys FollowKey    = Keys.K;

        private const int DoubleTapWindowMs = 450;
        private int _lastEPressTime = -10000;
        private bool _eHeld = false;
        private bool _followHeld = false;

        private readonly List<PullOverOrder> _pullOverOrders = new List<PullOverOrder>();
        private readonly List<FollowOrder>   _followOrders   = new List<FollowOrder>();

        // --- UI LemonUI pour contrôle routier ---
        private ObjectPool _uiPool;
        private NativeMenu _stopMenu;
        private Ped _currentStoppedDriver;

        public void Initialize() { /* rien */ }

        public void Update()
        {
            if (!DutyState.PoliceOnDuty) return;

            HandlePullOverInput();
            HandleFollowInput();
            ProcessActiveOrders();

            HandleTrafficStopInteraction();

            _uiPool?.Process();
        }

        public void Dispose()
        {
            _pullOverOrders.Clear();
            _followOrders.Clear();
        }

        #region Input Handling

        private void HandlePullOverInput()
        {
            bool keyDown = Game.IsKeyPressed(PullOverKey);

            if (keyDown && !_eHeld)
            {
                int now = Game.GameTime;
                int diff = now - _lastEPressTime;
                _lastEPressTime = now;

                if (diff > 0 && diff <= DoubleTapWindowMs)
                {
                    IssuePullOver();
                }
            }
            _eHeld = keyDown;
        }

        private void HandleFollowInput()
        {
            bool keyDown = Game.IsKeyPressed(FollowKey);
            if (keyDown && !_followHeld)
            {
                IssueFollow();
            }
            _followHeld = keyDown;
        }

        #endregion

        #region Order issuing

        private void IssuePullOver()
        {
            Vehicle veh = GetTargetVehicle();
            if (veh == null || !veh.Exists() || veh.IsSeatFree(VehicleSeat.Driver)) return;
            Ped driver = veh.GetPedOnSeat(VehicleSeat.Driver);
            if (driver == null || !driver.Exists() || driver.IsPlayer) return;

            // Recherche d'un point sûr sur le bas-côté : on se décale latéralement (droite) puis on cherche la prochaine position valide sur la route.
            Vector3 baseOffset = veh.ForwardVector * 35f + veh.RightVector * 15f;
            Vector3 tentative = veh.Position + baseOffset;

            // « GetNextPositionOnStreet » garantit que le point retourné est sur une portion de route praticable.
            Vector3 dest = World.GetNextPositionOnStreet(tentative);

            // Si pour une raison quelconque la recherche échoue (vector zéro), on retombe sur l'ancienne logique.
            if (dest == Vector3.Zero)
            {
                dest = World.GetNextPositionOnStreet(veh.Position + veh.ForwardVector * 25f + veh.RightVector * 8f);
            }

            driver.AlwaysKeepTask = true;
            driver.Task.DriveTo(veh, dest, 1.5f, 10f, DrivingStyle.Normal);

            _pullOverOrders.Add(new PullOverOrder(driver, veh, dest));
            GTA.UI.Notification.Show("~b~Rangement demandé");
        }

        private void IssueFollow()
        {
            Vehicle veh = GetTargetVehicle();
            if (veh == null || !veh.Exists() || veh.IsSeatFree(VehicleSeat.Driver)) return;
            Ped driver = veh.GetPedOnSeat(VehicleSeat.Driver);
            if (driver == null || !driver.Exists() || driver.IsPlayer) return;

            _followOrders.Add(new FollowOrder(driver, veh));
            GTA.UI.Notification.Show("~b~Suivi demandé");
        }

        #endregion

        #region Processing

        private void ProcessActiveOrders()
        {
            // Pullover – vérifier arrivée
            for (int i = _pullOverOrders.Count - 1; i >= 0; i--)
            {
                var o = _pullOverOrders[i];
                if (!o.IsValid)
                {
                    _pullOverOrders.RemoveAt(i);
                    continue;
                }

                if (!o.Completed && o.Vehicle.Position.DistanceTo(o.Destination) < 5f)
                {
                    // Arrêt définitif
                    o.Driver.Task.CruiseWithVehicle(o.Vehicle, 0f, DrivingStyle.Normal);
                    o.Completed = true;
                    o.CompleteTime = Game.GameTime;

                    // Définit le conducteur actuel comme cible d'interaction
                    _currentStoppedDriver = o.Driver;
                }

                // Supprimer les ordres terminés après quelques secondes
                if (o.Completed && Game.GameTime - o.CompleteTime > 10000)
                {
                    _pullOverOrders.RemoveAt(i);
                }
            }

            // Follow – mettre à jour la position cible régulièrement
            Ped player = Game.Player.Character;
            Vector3 followTargetPos = player.Position + player.ForwardVector * -8f;

            for (int i = _followOrders.Count - 1; i >= 0; i--)
            {
                var f = _followOrders[i];
                if (!f.IsValid)
                {
                    _followOrders.RemoveAt(i);
                    continue;
                }

                if (Game.GameTime - f.LastCommandTime > 2000)
                {
                    f.Driver.Task.DriveTo(f.Vehicle, followTargetPos, 4f, 22f, DrivingStyle.Normal);
                    f.LastCommandTime = Game.GameTime;
                }
            }
        }

        #endregion

        #region Utils

        private Vehicle GetTargetVehicle()
        {
            Vector3 source = GameplayCamera.Position;
            Vector3 dir = GameplayCamera.Direction;
            RaycastResult hit = World.Raycast(source, dir, 40f, IntersectFlags.Vehicles, Game.Player.Character);
            if (hit.DidHit && hit.HitEntity is Vehicle v) return v;
            return World.GetClosestVehicle(source + dir * 10f, 8f);
        }

        #endregion

        #region Nested Order structs

        private class PullOverOrder
        {
            public readonly Ped Driver;
            public readonly Vehicle Vehicle;
            public readonly Vector3 Destination;
            public bool Completed;
            public int CompleteTime;

            public PullOverOrder(Ped d, Vehicle v, Vector3 dest)
            {
                Driver = d; Vehicle = v; Destination = dest; Completed = false; CompleteTime = 0;
            }

            public bool IsValid => Driver != null && Driver.Exists() && Vehicle != null && Vehicle.Exists();
        }

        private class FollowOrder
        {
            public readonly Ped Driver;
            public readonly Vehicle Vehicle;
            public int LastCommandTime;

            public FollowOrder(Ped d, Vehicle v)
            {
                Driver = d; Vehicle = v; LastCommandTime = 0;
            }

            public bool IsValid => Driver != null && Driver.Exists() && Vehicle != null && Vehicle.Exists();
        }

        #endregion

        private void EnsureMenu()
        {
            if (_uiPool != null) return;

            _uiPool = new ObjectPool();
            _stopMenu = new NativeMenu("Contrôle routier", "Options de contrôle");
            _stopMenu.Add(new NativeItem("Demander papiers"));          // 0
            _stopMenu.Add(new NativeItem("Donner contravention"));      // 1
            _stopMenu.Add(new NativeItem("Demander de sortir"));        // 2
            _stopMenu.Add(new NativeItem("Terminer contrôle"));         // 3
            _stopMenu.ItemActivated += OnStopMenuItem;

            _uiPool.Add(_stopMenu);
        }

        private void OnStopMenuItem(object sender, LemonUI.Menus.ItemActivatedArgs e)
        {
            int idx = _stopMenu.Items.IndexOf(e.Item);
            switch (idx)
            {
                case 0:
                    GTA.UI.Notification.Show("~b~Le conducteur remet ses papiers...");
                    break;
                case 1:
                    GTA.UI.Notification.Show("~y~Contravention émise");
                    break;
                case 2:
                    if (_currentStoppedDriver != null && _currentStoppedDriver.Exists())
                    {
                        _currentStoppedDriver.Task.LeaveVehicle(_currentStoppedDriver.CurrentVehicle, true);
                    }
                    break;
                case 3:
                    _stopMenu.Visible = false;
                    break;
            }
        }

        private void HandleTrafficStopInteraction()
        {
            if (_currentStoppedDriver == null || !_currentStoppedDriver.Exists()) return;

            Ped player = Game.Player.Character;

            if (player.Position.DistanceTo(_currentStoppedDriver.Position) > 4f)
            {
                // Trop loin : on cache le menu si ouvert
                if (_stopMenu != null) _stopMenu.Visible = false;
                return;
            }

            // Affiche aide contexte à chaque frame quand nécessaire
            GTA.UI.Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Ouvrir menu contrôle routier");

            if (Game.IsControlJustPressed(GTA.Control.Context))
            {
                EnsureMenu();
                _stopMenu.Visible = true;
            }
        }
    }
} 