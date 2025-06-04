using System;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Config;
using REALIS.Events;
using GTA.UI;
using System.Collections.Generic;
using System.Linq;

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
            public Vector3 TargetPosition;
            public int FramesLeft;
            public bool TaskActive;
            public int Side; // -1 = gauche, 1 = droite
            public int StuckCounter;
        }

        public TrafficAI()
        {
            Tick += OnTick;
            Aborted += OnAborted;
            Notification.PostTicker("TrafficAI loaded", true);
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
                        // Choisir le côté le plus sûr pour dépasser
                        var bypass = FindBypassTarget(npcVeh, playerVeh, ped);

                        if (bypass.HasValue)
                        {
                            state = new BypassState
                            {
                                FramesLeft = TrafficAIConfig.BypassDuration,
                                TargetPosition = bypass.Value.Position,
                                TaskActive = false,
                                Side = bypass.Value.Side,
                                StuckCounter = 0
                            };
                            _states[ped] = state;

                            Logger.Info($"Bypass prepared for ped {ped.Handle} on side {state.Side}");
                            if (TrafficAIConfig.ShowDebug)
                                Screen.ShowSubtitle($"Bypass {state.Side}", 2000);
                        }
                        else
                        {
                            // Aucun chemin sûr, suivre le joueur de loin
                            InitiateSlowFollow(ped, npcVeh, playerVeh);
                        }
                    }
                    else
                    {
                        state.FramesLeft = TrafficAIConfig.BypassDuration;
                        state.StuckCounter = 0;
                    }

                    if (_states.ContainsKey(ped))
                    {
                        // Exécuter la manœuvre d'évitement
                        ExecuteBypassManeuver(ped, npcVeh, _states[ped]);
                        TrafficEvents.OnVehicleBypass();
                    }
                }
            }
        }

        private (Vector3 Position, int Side)? FindBypassTarget(Vehicle npcVeh, Vehicle playerVeh, Ped currentPed)
        {
            var vehiclePos = npcVeh.Position;

            Vector3 leftTarget = vehiclePos - npcVeh.RightVector * TrafficAIConfig.BypassOffset +
                                 npcVeh.ForwardVector * TrafficAIConfig.BypassForwardOffset;
            Vector3 rightTarget = vehiclePos + npcVeh.RightVector * TrafficAIConfig.BypassOffset +
                                  npcVeh.ForwardVector * TrafficAIConfig.BypassForwardOffset;

            bool leftClear = IsPathClear(npcVeh, leftTarget);
            bool rightClear = IsPathClear(npcVeh, rightTarget);

            int preferredSide = Vector3.Dot(playerVeh.Position - vehiclePos, npcVeh.RightVector) > 0 ? -1 : 1;

            var sides = new List<(int side, Vector3 pos, bool clear)>
            {
                (-1, leftTarget, leftClear),
                (1, rightTarget, rightClear)
            };

            foreach (var s in sides.OrderByDescending(x => x.side == preferredSide))
            {
                if (!s.clear) continue;
                if (IsSideTaken(currentPed, npcVeh, s.side)) continue;
                return (s.pos, s.side);
            }

            return null;
        }

        private bool IsPathClear(Vehicle veh, Vector3 target)
        {
            #pragma warning disable CS0618
            RaycastResult ray = World.RaycastCapsule(
                veh.Position + veh.UpVector,
                target + veh.UpVector,
                TrafficAIConfig.ScanCapsuleRadius,
                IntersectFlags.Vehicles | IntersectFlags.Map | IntersectFlags.Objects,
                veh);
            #pragma warning restore CS0618

            return !ray.DidHit;
        }

        private bool IsSideTaken(Ped currentPed, Vehicle npcVeh, int side)
        {
            return _states.Any(s => s.Key != currentPed && s.Value.TaskActive && s.Value.Side == side &&
                                    s.Key.Position.DistanceTo(npcVeh.Position) < TrafficAIConfig.VehicleSeparation);
        }

        private void InitiateSlowFollow(Ped ped, Vehicle npcVeh, Vehicle playerVeh)
        {
            // Comportement réaliste : ralentir et maintenir une distance sécuritaire
            Logger.Info($"No bypass available for ped {ped.Handle}, initiating slow follow");
            if (TrafficAIConfig.ShowDebug)
                Screen.ShowSubtitle("Following at safe distance", 2000);
            
            // Ralentir et suivre à distance sécuritaire
            VehicleDrivingFlags followFlags = VehicleDrivingFlags.StopForVehicles | 
                                            VehicleDrivingFlags.StopAtTrafficLights |
                                            VehicleDrivingFlags.SwerveAroundAllVehicles;
            
            // Position derrière le joueur à distance sécuritaire
            Vector3 followPosition = playerVeh.Position - playerVeh.ForwardVector * 8f;
            
            Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                ped.Handle, npcVeh.Handle,
                followPosition.X, followPosition.Y, followPosition.Z,
                Math.Max(5f, playerVeh.Speed * 0.7f), // Vitesse réduite
                (int)followFlags,
                6f); // Large rayon d'acceptation
        }


        private void ExecuteBypassManeuver(Ped ped, Vehicle vehicle, BypassState state)
        {
            if (!state.TaskActive)
            {
                // Flags réalistes pour manœuvre douce
                VehicleDrivingFlags flags = VehicleDrivingFlags.SwerveAroundAllVehicles | 
                                          VehicleDrivingFlags.StopAtDestination |
                                          VehicleDrivingFlags.SteerAroundPeds;

                // Vitesse modérée pour une manœuvre réaliste
                float maneuverSpeed = Math.Min(25f, Math.Max(10f, vehicle.Speed * 1.1f));

                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                    ped.Handle, vehicle.Handle, 
                    state.TargetPosition.X, state.TargetPosition.Y, state.TargetPosition.Z,
                    maneuverSpeed,
                    (int)flags, 
                    3f); // Rayon d'arrivée

                state.TaskActive = true;
                Logger.Info($"Natural bypass maneuver started for ped {ped.Handle}");
            }
            else
            {
                // Vérifier la progression de la manœuvre
                float distanceToTarget = vehicle.Position.DistanceTo(state.TargetPosition);

                if (distanceToTarget < 4f)
                {
                    // Reprendre la conduite normale
                    ResumeNormalDriving(ped, vehicle);
                    state.FramesLeft = 0; // Marquer pour suppression
                    Logger.Info($"Natural bypass completed for ped {ped.Handle}");
                    return;
                }

                if (vehicle.Speed < 0.5f)
                {
                    state.StuckCounter++;
                    if (state.StuckCounter > TrafficAIConfig.BypassStuckFrames)
                    {
                        ResumeNormalDriving(ped, vehicle);
                        state.FramesLeft = 0;
                        Logger.Info($"Bypass aborted for ped {ped.Handle} (stuck)");
                    }
                }
                else
                {
                    state.StuckCounter = 0;
                }
            }
        }

        private void ResumeNormalDriving(Ped ped, Vehicle vehicle)
        {
            // Nettoyer les tâches actuelles
            Function.Call(Hash.CLEAR_PED_TASKS, ped.Handle);
            
            // Reprendre la conduite normale avec des flags réalistes
            VehicleDrivingFlags normalFlags = VehicleDrivingFlags.StopForVehicles | 
                                            VehicleDrivingFlags.StopAtTrafficLights | 
                                            VehicleDrivingFlags.SwerveAroundAllVehicles |
                                            VehicleDrivingFlags.SteerAroundPeds;

            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER,
                ped.Handle, vehicle.Handle, 20f, (int)normalFlags);
        }

        private void CleanStates()
        {
            var toRemove = new List<Ped>();
            foreach (var kvp in _states)
            {
                var ped = kvp.Key;
                var st = kvp.Value;
                if (ped == null || !ped.Exists() || --st.FramesLeft <= 0)
                {
                    if (ped != null && ped.Exists())
                    {
                        // Assurer que le véhicule reprend sa conduite normale
                        if (ped.IsInVehicle())
                        {
                            ResumeNormalDriving(ped, ped.CurrentVehicle);
                        }
                        toRemove.Add(ped);
                    }
                }
            }
            foreach (var ped in toRemove)
            {
                _states.Remove(ped);
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            // Nettoyer tous les states actifs
            foreach (var kvp in _states)
            {
                var ped = kvp.Key;
                if (ped != null && ped.Exists() && ped.IsInVehicle())
                {
                    ResumeNormalDriving(ped, ped.CurrentVehicle);
                }
            }
            _states.Clear();
            Logger.Info("TrafficAI aborted");
        }
    }
}
