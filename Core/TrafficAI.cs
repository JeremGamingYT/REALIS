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
            public Vector3 TargetDirection;
            public int FramesLeft;
            public Vector3 TargetPosition;
            public bool TaskActive;
            public float BypassAngle; // Angle de contournement choisi
        }

        // Angles réalistes pour la détection d'obstacles (en radians)
        // De -45° à +45° avec priorité aux petits angles
        private readonly float[] _raycastAngles = { 
            0f,           // Tout droit (0°)
            -0.175f, 0.175f,  // ±10°
            -0.35f, 0.35f,    // ±20°
            -0.52f, 0.52f,    // ±30°
            -0.785f, 0.785f   // ±45° (maximum réaliste)
        };

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
                        // Système réaliste de détection d'obstacles et de choix de direction
                        var avoidanceResult = CalculateRealisticAvoidanceDirection(npcVeh, playerVeh);
                        
                        if (avoidanceResult.HasValue)
                        {
                            state = new BypassState 
                            { 
                                TargetDirection = avoidanceResult.Value.Direction,
                                FramesLeft = TrafficAIConfig.BypassDuration,
                                TargetPosition = avoidanceResult.Value.Position,
                                TaskActive = false,
                                BypassAngle = avoidanceResult.Value.Angle
                            };
                            _states[ped] = state;
                            
                            float angleDegrees = avoidanceResult.Value.Angle * 57.2958f; // Rad to degrees
                            Logger.Info($"Realistic bypass for ped {ped.Handle} at angle {angleDegrees:F1}°");
                            if (TrafficAIConfig.ShowDebug)
                                Screen.ShowSubtitle($"Natural bypass {angleDegrees:F0}°", 2000);
                        }
                        else
                        {
                            // Si aucune manœuvre possible, ralentir et suivre à distance
                            InitiateSlowFollow(ped, npcVeh, playerVeh);
                        }
                    }
                    else
                    {
                        state.FramesLeft = TrafficAIConfig.BypassDuration;
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

        private (Vector3 Direction, Vector3 Position, float Angle)? CalculateRealisticAvoidanceDirection(Vehicle npcVeh, Vehicle playerVeh)
        {
            Vector3 vehiclePos = npcVeh.Position;
            Vector3 vehicleForward = npcVeh.ForwardVector;
            
            // Détection progressive des obstacles avec priorité aux petits angles
            var obstacleMap = new Dictionary<float, float>(); // Angle -> Distance minimale
            
            foreach (float angle in _raycastAngles)
            {
                // Calculer la direction de raycast
                Vector3 rayDirection = RotateVector(vehicleForward, angle);
                Vector3 rayEndPoint = vehiclePos + rayDirection * TrafficAIConfig.RealisticScanDistance;
                
                // Raycast simplifié et ciblé
                float minDistance = float.MaxValue;
                
                // Détecter véhicules et obstacles principaux
                RaycastResult raycast = World.Raycast(vehiclePos, rayEndPoint, 
                    IntersectFlags.Vehicles | IntersectFlags.Map | IntersectFlags.Objects);
                
                if (raycast.DidHit)
                {
                    minDistance = Vector3.Distance(vehiclePos, raycast.HitPosition);
                }
                else
                {
                    minDistance = TrafficAIConfig.RealisticScanDistance;
                }
                
                obstacleMap[angle] = minDistance;
            }
            
            // Priorité aux angles les plus petits et aux directions les plus libres
            var candidateDirections = obstacleMap
                .Where(kvp => kvp.Value > TrafficAIConfig.RealisticMinClearance)
                .OrderBy(kvp => Math.Abs(kvp.Key)) // PRIORITÉ aux petits angles
                .ThenByDescending(kvp => kvp.Value) // Puis par distance libre
                .ToList();
            
            if (!candidateDirections.Any())
            {
                // Aucune direction libre - retourner null pour déclencher le slow follow
                return null;
            }
            
            var bestDirection = candidateDirections.First();
            float bestAngle = bestDirection.Key;
            Vector3 avoidanceDirection = RotateVector(vehicleForward, bestAngle);
            
            // Position cible plus conservative et réaliste
            float targetDistance = Math.Min(bestDirection.Value * 0.6f, TrafficAIConfig.RealisticBypassDistance);
            Vector3 targetPosition = vehiclePos + avoidanceDirection * targetDistance;
            
            return (avoidanceDirection, targetPosition, bestAngle);
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

        private Vector3 RotateVector(Vector3 vector, float angleRadians)
        {
            float cos = (float)Math.Cos(angleRadians);
            float sin = (float)Math.Sin(angleRadians);
            
            return new Vector3(
                vector.X * cos - vector.Y * sin,
                vector.X * sin + vector.Y * cos,
                vector.Z
            );
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
                
                if (distanceToTarget < 4f || vehicle.Speed < 1f)
                {
                    // Reprendre la conduite normale
                    ResumeNormalDriving(ped, vehicle);
                    state.FramesLeft = 0; // Marquer pour suppression
                    Logger.Info($"Natural bypass completed for ped {ped.Handle}");
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
