using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.Math;
using GTA.UI;
using REALIS.Config;
using REALIS.Events;

namespace REALIS.Core
{
    /// <summary>
    /// Simple police system handling non lethal chases and arrests when player has one star.
    /// </summary>
    public class PoliceSystem : Script
    {
        private Ped? _arrestingOfficer;
        private Vehicle? _policeVehicle;
        private int _tickCounter;
        private int _stationaryMs;
        private bool _isEscorting;
        private bool _isTransporting;
        private readonly List<Ped> _spawnedPeds = new();
        private readonly List<Vehicle> _spawnedVehicles = new();

        private readonly Vector3[] _stations =
        {
            new Vector3(425.1f, -979.1f, 30.7f),   // Mission Row
            new Vector3(1855.8f, 3683.3f, 34.2f), // Sandy Shores
            new Vector3(-449.8f, 6012.9f, 31.7f)  // Paleto Bay
        };

        public PoliceSystem()
        {
            Tick += OnTick;
            Aborted += OnAborted;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _tickCounter++;
                if (_tickCounter % PoliceConfig.UpdateInterval != 0) return;

                Ped player = Game.Player.Character;
                if (!player.Exists()) return;

                if (Game.Player.Wanted.WantedLevel == 1)
                {
                    if (!_isEscorting && !_isTransporting)
                        HandleChase(player);
                }
                else
                {
                    if (!_isEscorting && !_isTransporting)
                        ResetState();
                }

                if (_isEscorting)
                    UpdateEscort(player);
                else if (_isTransporting)
                    UpdateTransport(player);
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceSystem tick error: {ex.Message}");
            }
        }

        private void HandleChase(Ped player)
        {
            var nearby = World.GetNearbyPeds(player.Position, PoliceConfig.POLICE_DETECTION_RANGE);
            Ped? closest = null;
            float closestDist = float.MaxValue;
            foreach (var ped in nearby)
            {
                if (ped == null || !ped.Exists() || ped.IsDead) continue;
                if (!IsPolicePed(ped)) continue;

                float dist = ped.Position.DistanceTo(player.Position);
                if (dist < closestDist)
                {
                    closest = ped;
                    closestDist = dist;
                }

                if (IsPlayerAimingAt(player, ped))
                {
                    PoliceEvents.OnPlayerAimingAtOfficer(ped);
                    ped.Task.ShootAt(player);
                    Notification.PostTicker(PoliceConfig.COMBAT_WARNING, true);
                    return;
                }
            }

            if (closest == null) return;

            if (player.Velocity.Length() < PoliceConfig.StationarySpeed)
                _stationaryMs += PoliceConfig.UpdateInterval;
            else
                _stationaryMs = 0;

            if (closest.Position.DistanceTo(player.Position) > PoliceConfig.ARREST_RANGE ||
                _stationaryMs < PoliceConfig.ArrestDelayMs)
            {
                closest.Task.RunTo(player.Position);
                PoliceEvents.OnPlayerChaseStarted(closest);
            }
            else
            {
                _stationaryMs = 0;
                StartArrest(closest, player);
            }
        }

        private bool IsPlayerAimingAt(Ped player, Ped target)
        {
            if (!player.IsAiming) return false;
            return Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player, target);
        }

        private void StartArrest(Ped officer, Ped player)
        {
            _arrestingOfficer = officer;
            _arrestingOfficer.Task.ClearAll();
            Function.Call(Hash.TASK_ARREST_PED, officer.Handle, player.Handle);
            Game.Player.Wanted.SetWantedLevel(0, false);
            Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
            PoliceEvents.OnPlayerArrested(officer);
            _isEscorting = true;
            Game.Player.Wanted.SetPoliceIgnorePlayer(true);
            Notification.PostTicker(PoliceConfig.ARREST_WARNING, true);
        }

        private void UpdateEscort(Ped player)
        {
            if (_arrestingOfficer == null || !_arrestingOfficer.Exists())
            {
                ResetState();
                return;
            }

            if (Game.IsControlJustReleased(Control.Jump))
            {
                // Prevent jumping while arrested
                Game.DisableControlThisFrame(Control.Jump);
            }

            // attendre que le joueur soit menotté avant de poursuivre l'escorte
            if (!Function.Call<bool>(Hash.IS_PED_RUNNING_ARREST_TASK, _arrestingOfficer) &&
                Function.Call<bool>(Hash.IS_PED_CUFFED, player))
            {
                if (_policeVehicle == null || !_policeVehicle.Exists())
                {
                    _policeVehicle = GetOrCreatePoliceVehicle(player.Position);
                    if (_policeVehicle != null) _spawnedVehicles.Add(_policeVehicle);
                }

                if (_policeVehicle != null)
                {
                    Game.Player.SetControlState(false);
                    player.Task.EnterVehicle(_policeVehicle, VehicleSeat.RightRear);
                    _arrestingOfficer.Task.ClearAll();
                    _arrestingOfficer.Task.EnterVehicle(_policeVehicle, VehicleSeat.Driver);
                    Game.Player.Wanted.SetWantedLevel(0, false);
                    Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
                    _isEscorting = false;
                    _isTransporting = true;
                    PoliceEvents.OnPlayerEscorted(_arrestingOfficer, _policeVehicle);
                    Notification.PostTicker(PoliceConfig.HANDCUFF_MESSAGE, true);
                }
            }
        }

        private void UpdateTransport(Ped player)
        {
            if (_policeVehicle == null || !_policeVehicle.Exists() || _arrestingOfficer == null || !_arrestingOfficer.Exists())
            {
                ResetState();
                return;
            }

            if (!_arrestingOfficer.IsInVehicle(_policeVehicle)) return;

            Vector3 destination = GetNearestStation(_policeVehicle.Position);
            _arrestingOfficer.Task.DriveTo(_policeVehicle, destination, 5f,
                VehicleDrivingFlags.DrivingModeAvoidVehicles, PoliceConfig.TransportSpeed);

            if (_policeVehicle.Position.DistanceTo(destination) < 6f)
            {
                player.Task.LeaveVehicle(_policeVehicle, LeaveVehicleFlags.None);
                Game.Player.SetControlState(true);
                Game.Player.Wanted.SetWantedLevel(0, false);
                Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
                PoliceEvents.OnPlayerTransported(_policeVehicle);
                Notification.PostTicker(PoliceConfig.RELEASE_MESSAGE, true);
                ResetState();
            }
        }

        private Vector3 GetNearestStation(Vector3 from)
        {
            Vector3 best = _stations[0];
            float dist = from.DistanceTo(best);
            foreach (var station in _stations)
            {
                float d = from.DistanceTo(station);
                if (d < dist)
                {
                    dist = d;
                    best = station;
                }
            }
            return best;
        }

        private Vehicle GetOrCreatePoliceVehicle(Vector3 around)
        {
            var vehicles = World.GetNearbyVehicles(around, PoliceConfig.POLICE_DETECTION_RANGE);
            var vehicles = World.GetNearbyVehicles(around, 40f);
            Vehicle? closest = null;
            float dist = float.MaxValue;
            foreach (var veh in vehicles)
            {
                if (veh == null || !veh.Exists()) continue;
                if (!IsPoliceVehicle(veh)) continue;
                float d = veh.Position.DistanceTo(around);
                if (d < dist)
                {
                    dist = d;
                    closest = veh;
                }
            }

            if (!PoliceConfig.AutoCreatePoliceVehicles)
                return null!;
            if (closest != null) return closest;

            if (!PoliceConfig.AutoCreatePoliceVehicles) return null!;

            Model model = new Model(PoliceConfig.POLICE_VEHICLE_MODELS[0]);
            DateTime startTime = DateTime.Now;
            while (!model.IsLoaded && (DateTime.Now - startTime).TotalMilliseconds < 2000)
            {
                model.Request();
                Script.Yield();
            }

            if (!model.IsLoaded) return null!;
            var v = World.CreateVehicle(model, around.Around(5f));
            model.MarkAsNoLongerNeeded();
            return v;
        }

        private bool IsPoliceVehicle(Vehicle veh)
        {
            foreach (var name in PoliceConfig.POLICE_VEHICLE_MODELS)
            {
                if (veh.Model.Hash == Function.Call<int>(Hash.GET_HASH_KEY, name))
                    return true;
            }
            return false;
        }

        private bool IsPolicePed(Ped ped)
        {
            foreach (var name in PoliceConfig.POLICE_PED_MODELS)
            {
                if (ped.Model.Hash == Function.Call<int>(Hash.GET_HASH_KEY, name))
                    return true;
            }
            return false;
        }

        private void ResetState()
        {
            try
            {
                Game.Player.SetControlState(true);
                Game.Player.Wanted.SetPoliceIgnorePlayer(false);
                if (_arrestingOfficer != null && _spawnedPeds.Contains(_arrestingOfficer))
                {
                    if (_arrestingOfficer.Exists())
                        _arrestingOfficer.Delete();
                    _spawnedPeds.Remove(_arrestingOfficer);
                }

                if (_policeVehicle != null && _spawnedVehicles.Contains(_policeVehicle))
                {
                    if (_policeVehicle.Exists())
                        _policeVehicle.Delete();
                    _spawnedVehicles.Remove(_policeVehicle);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Reset state error: {ex.Message}");
            }
            finally
            {
                _arrestingOfficer = null;
                _policeVehicle = null;
                _isEscorting = false;
                _isTransporting = false;
                _stationaryMs = 0;
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                ResetState();
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceSystem cleanup error: {ex.Message}");
            }
        }
    }
}