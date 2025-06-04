using System;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Config;
using REALIS.Events;
using GTA.UI;

namespace REALIS.Core
{
    /// <summary>
    /// Main script improving vehicle traffic behaviour around the player.
    /// </summary>
    public class TrafficAI : Script
    {
        private int _tickCount;

        public TrafficAI()
        {
            Tick += OnTick;
            Aborted += OnAborted;
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
            }
        }

        private void UpdateNearbyVehicles()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists()) return;
            Vehicle playerVeh = player.CurrentVehicle;
            if (playerVeh == null || !playerVeh.Exists()) return;

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
                    Vector3 sideOffset = npcVeh.RightVector * TrafficAIConfig.BypassOffset;
                    Vector3 target = npcVeh.Position + sideOffset;

                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE, ped.Handle,
                        npcVeh.Handle, target.X, target.Y, target.Z, 20f,
                        (int)DrivingStyle.AvoidTrafficExtremely, 5f);

                    TrafficEvents.OnVehicleBypass();
                }
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            // No persistent entities, but method kept for compliance and future use
        }
    }
}
