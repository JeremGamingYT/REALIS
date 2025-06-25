using System;
using System.Collections.Generic;
using System.Linq; // Added for LINQ
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Common;
using System.Windows.Forms;
using REALIS.Core; // Added for Logger

namespace REALIS.Job
{
    /// <summary>
    /// Permet au joueur policier d'arrêter un suspect :
    /// 1. Viser un ped (<see cref="Player.IsFreeAiming"/>)
    /// 2. Appuyer sur la touche d'interaction (E) pour lui ordonner de lever les mains.
    /// 3. Appuyer de nouveau sur E à proximité pour le menotter (animation simple).
    /// </summary>
    public class PoliceArrestModule : IModule
    {
        private const float ArrestRange = 6f;   // distance maximale pour initier l'arrestation
        private const float CuffRange = 2.2f;   // distance requise pour menotter
        private const float EscortAttachRange = 2.5f; // distance pour démarrer l'escorte

        // Désormais, l'état est partagé via PoliceArrestShared afin que d'autres modules (transport, jail, etc.) puissent y accéder.

        private static readonly Random _rng = new Random();

        private readonly TimeSpan _idCooldown = TimeSpan.FromMilliseconds(300);
        private DateTime _lastIdPress = DateTime.MinValue;

        private readonly TimeSpan _seatCooldown = TimeSpan.FromMilliseconds(300);
        private DateTime _lastSeatPress = DateTime.MinValue;

        private DateTime _lastContextPress = DateTime.MinValue;
        private readonly TimeSpan _pressCooldown = TimeSpan.FromMilliseconds(300);

        // Optimized police vehicle search
        private static readonly Model[] PoliceVehicleModels = {
            new Model(VehicleHash.Police), new Model(VehicleHash.Police2), new Model(VehicleHash.Police3), new Model(VehicleHash.Police4),
            new Model(VehicleHash.Policeb), new Model(VehicleHash.Policet), new Model(VehicleHash.Sheriff), new Model(VehicleHash.Sheriff2),
            new Model(VehicleHash.Pranger), // Park Ranger
            new Model(VehicleHash.FIB), new Model(VehicleHash.FBI2) // Unmarked variants often used by police mods
        };
        private DateTime _nextVehicleSearchAllowed = DateTime.MinValue;
        private readonly TimeSpan _vehicleSearchCooldown = TimeSpan.FromSeconds(1); // Cooldown for FindNearestPoliceVehicle if T is spammed


        public void Initialize() { /* Rien à initialiser */ }

        public void Update()
        {
            try
            {
                if (!DutyState.PoliceOnDuty) return;

                var player = Game.Player.Character;
                if (!player.Exists() || player.IsDead) return;

                // Touche d'interaction "E" pour arrestation / menotter
                bool contextPressed = Game.IsControlJustPressed(GTA.Control.Context);
                if (contextPressed && DateTime.Now - _lastContextPress < _pressCooldown)
                    contextPressed = false; // anti spam
                if (contextPressed) _lastContextPress = DateTime.Now;

                // Touche « Y » : demander la pièce d'identité du suspect déjà mains en l'air
                bool idPressed = Game.IsKeyPressed(System.Windows.Forms.Keys.Y);
                if (idPressed && DateTime.Now - _lastIdPress < _idCooldown)
                    idPressed = false;
                if (idPressed) _lastIdPress = DateTime.Now;

                // Touche « T » : placer le suspect menotté à l'arrière d'un véhicule de police proche
                bool seatPressed = Game.IsKeyPressed(System.Windows.Forms.Keys.T);
                if (seatPressed && DateTime.Now - _lastSeatPress < _seatCooldown)
                    seatPressed = false;
                if (seatPressed) _lastSeatPress = DateTime.Now;

                // Recherche du ped visé par le joueur
                Ped aimedPed = null;
                if (Game.Player.IsAiming)
                {
                    Entity entAimed = GetEntityPlayerIsAimingAt();
                    if (entAimed != null && entAimed.Exists() && entAimed is Ped ped && ped.IsHuman && ped != player)
                        aimedPed = ped;
                }

                if (aimedPed != null)
                {
                    float dist = player.Position.DistanceTo(aimedPed.Position);

                    // 1. Première pression : mise en reddition
                    if (contextPressed && dist <= ArrestRange && !PoliceArrestShared.SurrenderedPeds.Contains(aimedPed.Handle) && !PoliceArrestShared.CuffedPeds.Contains(aimedPed.Handle))
                    {
                        OrderPedToSurrender(aimedPed);
                        return; // on attend pression suivante pour menottes
                    }
                }

                // 2. Vérifie si le joueur est proche d'un ped déjà surrender pour le menotter
                if (contextPressed)
                {
                    // Iterate over a copy in case CuffPed modifies the collection
                    foreach (int handle in PoliceArrestShared.SurrenderedPeds.ToList())
                    {
                        Ped ped = Entity.FromHandle(handle) as Ped;
                        if (ped == null || !ped.Exists() || ped.IsDead || ped.IsInVehicle()) continue;

                        if (player.Position.DistanceTo(ped.Position) <= CuffRange && !PoliceArrestShared.CuffedPeds.Contains(handle))
                        {
                            CuffPed(ped);
                            break;
                        }
                    }
                }

                // 3. Demande de pièce d'identité
                if (idPressed)
                {
                    foreach (int handle in PoliceArrestShared.SurrenderedPeds.ToList())
                    {
                        if (PoliceArrestShared.CuffedPeds.Contains(handle)) continue; // déjà menotté, skip ID

                        Ped ped = Entity.FromHandle(handle) as Ped;
                        if (ped == null || !ped.Exists() || ped.IsDead || ped.IsInVehicle()) continue;

                        if (player.Position.DistanceTo(ped.Position) <= CuffRange)
                        {
                            ShowSuspectId(ped);
                            break;
                        }
                    }
                }

                // 4. Gestion touche "T" : escorter OU placer dans un véhicule
                if (seatPressed)
                {
                    if (DateTime.Now < _nextVehicleSearchAllowed && PoliceArrestShared.EscortedPedHandle != -1)
                    {
                        GTA.UI.Notification.Show("~y~Recherche de véhicule en cours...");
                        return;
                    }

                    // --- Cas n°1 : Aucun suspect escorté actuellement, on tente d'en prendre un ---
                    if (PoliceArrestShared.EscortedPedHandle == -1)
                    {
                        bool picked = false;
                        foreach (int handle in PoliceArrestShared.CuffedPeds.ToList())
                        {
                            Ped ped = Entity.FromHandle(handle) as Ped;
                            if (ped == null || !ped.Exists() || ped.IsDead || ped.IsInVehicle()) continue;

                            if (player.Position.DistanceTo(ped.Position) <= EscortAttachRange)
                            {
                                StartEscort(ped);
                                picked = true;
                                break;
                            }
                        }

                        // Si aucun ped à l'extérieur, on regarde dans un véhicule de police proche
                        if (!picked)
                        {
                            Vehicle vehNearby = FindNearestPoliceVehicle(player.Position, 8f);
                            _nextVehicleSearchAllowed = DateTime.Now + _vehicleSearchCooldown;
                            if (vehNearby != null)
                            {
                                foreach (VehicleSeat seat in new[] { VehicleSeat.RightRear, VehicleSeat.LeftRear })
                                {
                                    Ped occ = vehNearby.GetPedOnSeat(seat);
                                    if (occ != null && occ.Exists() && PoliceArrestShared.CuffedPeds.Contains(occ.Handle))
                                    {
                                        RemoveFromVehicleAndEscort(occ, vehNearby);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    // --- Cas n°2 : Un suspect est déjà escorté ---
                    else
                    {
                        Ped ped = Entity.FromHandle(PoliceArrestShared.EscortedPedHandle) as Ped;
                        if (ped != null && ped.Exists())
                        {
                            Vehicle veh = FindNearestPoliceVehicle(player.Position, 8f);
                            _nextVehicleSearchAllowed = DateTime.Now + _vehicleSearchCooldown;
                            if (veh != null)
                            {
                                SeatPedInVehicle(ped, veh);
                                // StopEscort(ped); // SeatPedInVehicle should ideally handle un-escorting if successful
                            }
                            else
                            {
                                GTA.UI.Notification.Show("~y~Aucun véhicule de police adapté à proximité.");
                                StopEscort(ped); // Toggle off escort if no vehicle found
                            }
                        }
                        else
                        {
                            PoliceArrestShared.EscortedPedHandle = -1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule error in Update: {ex.Message} {ex.StackTrace}");
                if (PoliceArrestShared.EscortedPedHandle != -1)
                {
                    Ped ped = Entity.FromHandle(PoliceArrestShared.EscortedPedHandle) as Ped;
                    if(ped != null && ped.Exists()) StopEscort(ped);
                    else PoliceArrestShared.EscortedPedHandle = -1;
                }
            }
        }

        public void Dispose()
        {
            try
            {
                if (PoliceArrestShared.EscortedPedHandle != -1)
                {
                    Ped ped = Entity.FromHandle(PoliceArrestShared.EscortedPedHandle) as Ped;
                    if (ped != null && ped.Exists())
                    {
                        StopEscort(ped);
                    }
                    PoliceArrestShared.EscortedPedHandle = -1;
                }
            }
            catch (Exception ex)
            {
                 Logger.Error($"PoliceArrestModule error in Dispose: {ex.Message} {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Renvoie l'entité que le joueur vise via un raycast.
        /// </summary>
        private Entity GetEntityPlayerIsAimingAt()
        {
            try
            {
                Vector3 camPos = GameplayCamera.Position;
                Vector3 dir = GameplayCamera.Direction;
                RaycastResult res = World.Raycast(camPos, camPos + dir * 100f, IntersectFlags.Peds, Game.Player.Character);
                return res.DidHit ? res.HitEntity : null;
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule error in GetEntityPlayerIsAimingAt: {ex.Message} {ex.StackTrace}");
                return null;
            }
        }

        private void OrderPedToSurrender(Ped ped)
        {
            try
            {
                if (ped == null || !ped.Exists()) return;
                ped.BlockPermanentEvents = true;
                ped.CanRagdoll = false;
                ped.Task.HandsUp(15000);
                PoliceArrestShared.SurrenderedPeds.Add(ped.Handle);
                GTA.UI.Notification.Show("~y~Suspect : mains en l'air !");
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule OrderPedToSurrender error for ped {ped?.Handle}: {ex.Message} {ex.StackTrace}");
            }
        }

        private void CuffPed(Ped ped)
        {
            try
            {
                if (ped == null || !ped.Exists()) return;
                ped.Task.ClearAll();
                ped.Task.PlayAnimation("mp_arresting", "idle", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                 if(Game.Player.Character != null && Game.Player.Character.Exists())
                    ped.Heading = (Game.Player.Character.Position - ped.Position).ToHeading();
                ped.IsPositionFrozen = true;
                PoliceArrestShared.CuffedPeds.Add(ped.Handle);
                PoliceArrestShared.SurrenderedPeds.Remove(ped.Handle); // Remove from surrendered list
                GTA.UI.Notification.Show("~g~Suspect menotté.");
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule CuffPed error for ped {ped?.Handle}: {ex.Message} {ex.StackTrace}");
            }
        }

        private void ShowSuspectId(Ped ped)
        {
            try
            {
                if (ped == null || !ped.Exists()) return;
                if (PoliceArrestShared.IdInfos.ContainsKey(ped.Handle))
                {
                    DisplayInfoNotification(PoliceArrestShared.IdInfos[ped.Handle]);
                    return;
                }
                var info = new PoliceArrestShared.SuspectInfo
                {
                    Name = GenerateRandomName(ped.Gender == Gender.Male),
                    Age = _rng.Next(18, 58),
                    IsWanted = _rng.NextDouble() < 0.15
                };
                PoliceArrestShared.IdInfos[ped.Handle] = info;
                DisplayInfoNotification(info);
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule error in ShowSuspectId for ped {ped?.Handle}: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors de l'affichage de l'identité.");
            }
        }

        private static void DisplayInfoNotification(PoliceArrestShared.SuspectInfo info)
        {
            try
            {
                string status = info.IsWanted ? "~r~Recherché" : "~g~Aucun mandat";
                GTA.UI.Notification.Show($"~y~Dispatch :~s~ {info.Name}, {info.Age} ans | {status}");
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule DisplayInfoNotification error: {ex.Message} {ex.StackTrace}");
            }
        }

        private static string GenerateRandomName(bool male)
        {
            try
            {
                string[] maleNames = { "John", "Michael", "David", "James", "Robert", "Alex", "Chris" };
                string[] femaleNames = { "Sarah", "Emily", "Jessica", "Laura", "Anna", "Kate", "Maria" };
                string[] lastNames = { "Smith", "Johnson", "Brown", "Davis", "Miller", "Wilson", "Taylor" };
                string first = male ? maleNames[_rng.Next(maleNames.Length)] : femaleNames[_rng.Next(femaleNames.Length)];
                string last = lastNames[_rng.Next(lastNames.Length)];
                return $"{first} {last}";
            }
            catch (Exception ex)
            {
                 Logger.Error($"PoliceArrestModule GenerateRandomName error: {ex.Message} {ex.StackTrace}");
                 return male ? "John Doe" : "Jane Doe";
            }
        }

        private static Vehicle FindNearestPoliceVehicle(Vector3 position, float radius)
        {
            try
            {
                Vehicle foundVehicle = null;
                float closestDistSq = radius * radius;

                foreach (Model policeModel in PoliceVehicleModels)
                {
                    if (!policeModel.IsLoaded) policeModel.Request(500); // Request model if not loaded
                    if (!policeModel.IsLoaded) continue;

                    Vehicle[] nearbyPoliceVehicles = World.GetNearbyVehicles(position, radius, policeModel);

                    foreach (Vehicle veh in nearbyPoliceVehicles)
                    {
                        if (veh.Exists() && !veh.IsDead && veh.Driver == null) // Prefer empty or non-player driven police cars
                        {
                            float distSq = Vector3.DistanceSquared(position, veh.Position);
                            if (distSq < closestDistSq)
                            {
                                // Check if vehicle has available rear seats
                                if (veh.IsSeatFree(VehicleSeat.RightRear) || veh.IsSeatFree(VehicleSeat.LeftRear))
                                {
                                    closestDistSq = distSq;
                                    foundVehicle = veh;
                                }
                            }
                        }
                    }
                   // Model should not be marked as no longer needed here if it's a static array of models for repeated use.
                   // If dynamically loading many unique models, then yes. For a small static list, keep them loaded or rely on game's management.
                }
                return foundVehicle;
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule error in FindNearestPoliceVehicle: {ex.Message} {ex.StackTrace}");
                return null;
            }
        }

        private static void SeatPedInVehicle(Ped ped, Vehicle veh)
        {
            try
            {
                if (ped == null || !ped.Exists() || veh == null || !veh.Exists()) return;

                VehicleSeat seatToUse = VehicleSeat.None;
                if (veh.IsSeatFree(VehicleSeat.RightRear)) seatToUse = VehicleSeat.RightRear;
                else if (veh.IsSeatFree(VehicleSeat.LeftRear)) seatToUse = VehicleSeat.LeftRear;

                if (seatToUse == VehicleSeat.None)
                {
                    GTA.UI.Notification.Show("~r~Aucun siège arrière libre dans ce véhicule.");
                    // If escorting, stop escort as we couldn't seat them.
                    if (PoliceArrestShared.EscortedPedHandle == ped.Handle) StopEscort(ped);
                    return;
                }

                int doorIdx = (seatToUse == VehicleSeat.RightRear) ? 3 : 2;
                Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, veh.Handle, doorIdx, false, false);

                ped.IsPositionFrozen = false; // Unfreeze before tasking
                if (PoliceArrestShared.EscortedPedHandle == ped.Handle) {
                     // Clear follow task if they were being escorted by player
                    ped.Task.ClearAll(); // Important to clear follow task from StartEscort
                    PoliceArrestShared.EscortedPedHandle = -1; // No longer escorted by player
                } else {
                    ped.Task.ClearAll(); // Clear any other tasks
                }


                ped.SetIntoVehicle(veh, seatToUse);

                Function.Call(Hash.SET_PED_CAN_BE_DRAGGED_OUT, ped.Handle, false);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 3, false);
                Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 398, true);
                Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 184, true);
                Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 27, true);
                try { ped.StaysInVehicleWhenJacked = true; } catch { /* Ignored */ }
                try { ped.AlwaysKeepTask = true; } catch { /* Ignored */ }

                GameScheduler.Schedule(() =>
                {
                    try {
                        if (ped.Exists() && ped.IsInVehicle(veh))
                        {
                            ped.Task.StandStill(-1);
                            ped.BlockPermanentEvents = true;
                            ped.CanRagdoll = false;
                        }
                    } catch (Exception exScheduled) {
                        Logger.Error($"PoliceArrestModule error in Scheduled SeatPedInVehicle task: {exScheduled.Message} {exScheduled.StackTrace}");
                    }
                }, 1500);

                GameScheduler.Schedule(() => {
                   try { if(veh.Exists()) Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, veh.Handle, doorIdx, false); } catch {}
                }, 800);

                GTA.UI.Notification.Show("~g~Suspect placé dans le véhicule.");
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule error in SeatPedInVehicle (ped {ped?.Handle}, veh {veh?.Handle}): {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors du placement du suspect.");
                if(ped != null && ped.Exists()) {
                    ped.IsPositionFrozen = false; // Attempt to unfreeze
                    ped.Task.ClearAll();
                     // If they were escorted, put them back to frozen escorted state (or just cuffed frozen state)
                    if(PoliceArrestShared.CuffedPeds.Contains(ped.Handle)) ped.IsPositionFrozen = true;

                }
            }
        }

        private void StartEscort(Ped ped)
        {
            try
            {
                if (ped == null || !ped.Exists()) return;
                ped.IsPositionFrozen = false;
                ped.Task.ClearAll();
                ped.Task.FollowToOffsetFromEntity(Game.Player.Character, new Vector3(0, -0.8f, 0), 0.5f, -1, 2.0f);
                ped.BlockPermanentEvents = true;
                ped.CanRagdoll = false;
                PoliceArrestShared.EscortedPedHandle = ped.Handle;
                GTA.UI.Notification.Show("~y~Vous escortez le suspect. Appuyez sur ~INPUT_PICKUP~ près d'un véhicule pour le placer.");
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule StartEscort error for ped {ped?.Handle}: {ex.Message} {ex.StackTrace}");
                PoliceArrestShared.EscortedPedHandle = -1;
            }
        }

        private void StopEscort(Ped ped)
        {
            try
            {
                if (ped == null || !ped.Exists())
                {
                    if (ped != null && PoliceArrestShared.EscortedPedHandle == ped.Handle) PoliceArrestShared.EscortedPedHandle = -1;
                    return;
                }
                ped.Task.ClearAll();
                ped.IsPositionFrozen = true;
                if (PoliceArrestShared.EscortedPedHandle == ped.Handle) PoliceArrestShared.EscortedPedHandle = -1;
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule StopEscort error for ped {ped?.Handle}: {ex.Message} {ex.StackTrace}");
                if (ped != null && PoliceArrestShared.EscortedPedHandle == ped.Handle) PoliceArrestShared.EscortedPedHandle = -1;
                if(ped != null && ped.Exists()) ped.IsPositionFrozen = true;
            }
        }

        private void RemoveFromVehicleAndEscort(Ped ped, Vehicle veh)
        {
            try
            {
                if (ped == null || !ped.Exists() || veh == null || !veh.Exists()) return;
                ped.IsPositionFrozen = false;
                ped.Task.ClearAll();
                ped.Task.LeaveVehicle(veh, false);

                GameScheduler.Schedule(() =>
                {
                    try {
                        if (ped.Exists() && !ped.IsInVehicle())
                        {
                            StartEscort(ped);
                        }
                        else if (ped.Exists() && ped.IsInVehicle(veh))
                        {
                            Logger.Warn($"Ped {ped.Handle} failed to leave vehicle {veh.Handle} for escort. Trying to re-task.");
                            ped.Task.LeaveVehicle(veh,false); // Try again
                        }
                    } catch (Exception exScheduled) {
                         Logger.Error($"PoliceArrestModule error in Scheduled RemoveFromVehicleAndEscort task: {exScheduled.Message} {exScheduled.StackTrace}");
                    }
                }, 2000);

                GTA.UI.Notification.Show("~y~Le suspect sort du véhicule.");
            }
            catch (Exception ex)
            {
                Logger.Error($"PoliceArrestModule RemoveFromVehicleAndEscort error (ped {ped?.Handle}, veh {veh?.Handle}): {ex.Message} {ex.StackTrace}");
                if(ped != null && ped.Exists()) {
                    ped.IsPositionFrozen = false;
                    ped.Task.ClearAll();
                }
            }
        }
    }
} 