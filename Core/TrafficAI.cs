using System;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Config;
using REALIS.Events;
using GTA.UI;
using System.Collections.Generic;

namespace REALIS.Core
{
    /// <summary>
    /// Main script improving vehicle traffic behaviour around the player.
    /// </summary>
    public class TrafficAI : Script
    {
        private int _tickCount;
        private readonly Dictionary<Ped, BypassState> _states = new();

        private class BypassState
        {
            public bool GoLeft;
            public int FramesLeft;
        }

        public TrafficAI()
        {
            Tick += OnTick;
            Aborted += OnAborted;
            Notification.Show("TrafficAI loaded");
            Logger.Info("TrafficAI initialized");
        }

        private void OnTick(object sender, EventArgs e)
        {
            _tickCount++;
            if (_tickCount % TrafficAIConfig.UpdateInterval != 0) return;

            try
            {
                UpdateNearbyVehicles();
            }
            catch (Exception ex)
            {
                Screen.ShowSubtitle($"TrafficAI error: {ex.Message}");
                Logger.Error(ex.ToString());
            }
        }

        private void UpdateNearbyVehicles()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists()) return;
            Vehicle playerVeh = player.CurrentVehicle;
            if (playerVeh == null || !playerVeh.Exists()) return;

            CleanStates();

            Ped[] nearbyPeds = World.GetNearbyPeds(player, TrafficAIConfig.DetectionRadius);
            foreach (var ped in nearbyPeds)
            {
                if (ped == null || !ped.Exists() || !ped.IsInVehicle() || ped.IsPlayer) continue;

                Vehicle npcVeh = ped.CurrentVehicle;
                if (npcVeh == null || !npcVeh.Exists()) continue;

                Vector3 toPlayer = playerVeh.Position - npcVeh.Position;
                float distance = toPlayer.Length();
                if (distance > TrafficAIConfig.DetectionRadius) continue;

                Vector3 dir = toPlayer.Normalized;
                float dot = Vector3.Dot(npcVeh.ForwardVector, dir);

                if (dot > TrafficAIConfig.ForwardThreshold)
                {
                    if (!_states.TryGetValue(ped, out var state))
                    {
                        bool goLeft = Vector3.Cross(npcVeh.ForwardVector, toPlayer).Z > 0f;
                        state = new BypassState { GoLeft = goLeft, FramesLeft = TrafficAIConfig.BypassDuration };
                        _states[ped] = state;
                        Logger.Info($"Bypass start for ped {ped.Handle} side {(goLeft ? "left" : "right")}");
                        if (TrafficAIConfig.ShowDebug)
                            Screen.ShowSubtitle($"Bypass {(goLeft ? "left" : "right")}");
                    }
                    else
                    {
                        state.FramesLeft = TrafficAIConfig.BypassDuration;
                    }

                    Vector3 side = state.GoLeft ? -npcVeh.RightVector : npcVeh.RightVector;
                    Vector3 target = npcVeh.Position + side * TrafficAIConfig.BypassOffset;

                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
                        ped.Handle, npcVeh.Handle, target.X, target.Y, target.Z,
                        20f, (int)DrivingStyle.AvoidTrafficExtremely, 5f);

                    TrafficEvents.OnVehicleBypass();
                }
            }
        }

        private void CleanStates()
        {
            var toRemove = new List<Ped>();
            foreach (var kvp in _states)
            {
                var ped = kvp.Key;
                var st = kvp.Value;
                if (ped == null || !ped.Exists() || --st.FramesLeft <= 0)
                    toRemove.Add(ped);
            }
            foreach (var ped in toRemove)
                _states.Remove(ped);
        }

        private void OnAborted(object sender, EventArgs e)
        {
            Logger.Info("TrafficAI aborted");
        }
    }
}
