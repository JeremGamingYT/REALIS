using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Common;
using System.Windows.Forms;

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

        public void Initialize() { /* Rien à initialiser */ }

        public void Update()
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
                if (contextPressed && dist <= ArrestRange && !PoliceArrestShared.SurrenderedPeds.Contains(aimedPed.Handle))
                {
                    OrderPedToSurrender(aimedPed);
                    return; // on attend pression suivante pour menottes
                }
            }

            // 2. Vérifie si le joueur est proche d'un ped déjà surrender pour le menotter
            if (contextPressed)
            {
                foreach (int handle in PoliceArrestShared.SurrenderedPeds)
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
                foreach (int handle in PoliceArrestShared.SurrenderedPeds)
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
                // --- Cas n°1 : Aucun suspect escorté actuellement, on tente d'en prendre un ---
                if (PoliceArrestShared.EscortedPedHandle == -1)
                {
                    bool picked = false;
                    foreach (int handle in PoliceArrestShared.CuffedPeds)
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
                        Vehicle vehNearby = FindNearestPoliceVehicle(player, 6f);
                        if (vehNearby != null)
                        {
                            foreach (VehicleSeat seat in new[] { VehicleSeat.RightRear, VehicleSeat.LeftRear })
                            {
                                Ped occ = vehNearby.GetPedOnSeat(seat);
                                if (occ != null && occ.Exists() && PoliceArrestShared.CuffedPeds.Contains(occ.Handle))
                                {
                                    RemoveFromVehicleAndEscort(occ, vehNearby);
                                    picked = true;
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
                        // Si un véhicule de police à proximité possède un siège libre -> on place le suspect dedans
                        Vehicle veh = FindNearestPoliceVehicle(player, 6f);
                        if (veh != null)
                        {
                            SeatPedInVehicle(ped, veh);
                            StopEscort(ped);
                        }
                        else
                        {
                            // Pas de véhicule à proximité -> on libère l'escorte (toggle OFF)
                            StopEscort(ped);
                        }
                    }
                    else
                    {
                        // Handle invalide -> reset
                        PoliceArrestShared.EscortedPedHandle = -1;
                    }
                }
            }
        }

        public void Dispose() { /* Rien à nettoyer pour l'instant */ }

        /// <summary>
        /// Renvoie l'entité que le joueur vise via un raycast.
        /// </summary>
        private Entity GetEntityPlayerIsAimingAt()
        {
            // Raycast depuis la caméra
            Vector3 camPos = GameplayCamera.Position;
            Vector3 dir = GameplayCamera.Direction;
            RaycastResult res = World.Raycast(camPos, camPos + dir * 100f, IntersectFlags.Peds, Game.Player.Character);
            return res.DidHit ? res.HitEntity : null;
        }

        private void OrderPedToSurrender(Ped ped)
        {
            try
            {
                // Bloque les events aléatoires pour éviter la fuite
                ped.BlockPermanentEvents = true;
                ped.CanRagdoll = false;

                // Lève les mains pendant 15 sec (Boucle si non menotté entre temps)
                ped.Task.HandsUp(15000);

                PoliceArrestShared.SurrenderedPeds.Add(ped.Handle);

                GTA.UI.Notification.Show("~y~Suspect : mains en l'air !");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PoliceArrestModule OrderPedToSurrender error: {ex.Message}");
            }
        }

        private void CuffPed(Ped ped)
        {
            try
            {
                // On stoppe l'animation de reddition et on fige le ped
                ped.Task.ClearAll();
                ped.Task.PlayAnimation("mp_arresting", "idle", 8f, -8f, -1, AnimationFlags.Loop, 0f);
                ped.Heading = (Game.Player.Character.Position - ped.Position).ToHeading();
                ped.IsPositionFrozen = true;

                PoliceArrestShared.CuffedPeds.Add(ped.Handle);

                GTA.UI.Notification.Show("~g~Suspect menotté.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PoliceArrestModule CuffPed error: {ex.Message}");
            }
        }

        private void ShowSuspectId(Ped ped)
        {
            if (PoliceArrestShared.IdInfos.ContainsKey(ped.Handle))
            {
                DisplayInfoNotification(PoliceArrestShared.IdInfos[ped.Handle]);
                return;
            }

            // Génère des infos aléatoires (placeholder)
            var info = new PoliceArrestShared.SuspectInfo
            {
                Name = GenerateRandomName(ped.Gender == Gender.Male),
                Age = _rng.Next(18, 58),
                IsWanted = _rng.NextDouble() < 0.15 // 15 % de chances d'être recherché
            };

            PoliceArrestShared.IdInfos[ped.Handle] = info;
            DisplayInfoNotification(info);
        }

        private static void DisplayInfoNotification(PoliceArrestShared.SuspectInfo info)
        {
            string status = info.IsWanted ? "~r~Recherché" : "~g~Aucun mandat";
            GTA.UI.Notification.Show($"~y~Dispatch :~s~ {info.Name}, {info.Age} ans | {status}");
        }

        private static string GenerateRandomName(bool male)
        {
            string[] maleNames = { "John", "Michael", "David", "James", "Robert", "Alex", "Chris" };
            string[] femaleNames = { "Sarah", "Emily", "Jessica", "Laura", "Anna", "Kate", "Maria" };
            string[] lastNames = { "Smith", "Johnson", "Brown", "Davis", "Miller", "Wilson", "Taylor" };

            string first = male ? maleNames[_rng.Next(maleNames.Length)] : femaleNames[_rng.Next(femaleNames.Length)];
            string last = lastNames[_rng.Next(lastNames.Length)];
            return $"{first} {last}";
        }

        private static Vehicle FindNearestPoliceVehicle(Ped player, float radius)
        {
            Vehicle nearest = null;
            float nearestDist = radius;

            foreach (Vehicle veh in World.GetAllVehicles())
            {
                if (!veh.Exists() || veh.IsDead) continue;

                float dist = player.Position.DistanceTo(veh.Position);
                if (dist > radius) continue;

                // Vérifie si le véhicule est un véhicule d'urgence
                if (veh.ClassType != VehicleClass.Emergency) continue;

                if (dist < nearestDist)
                {
                    nearest = veh;
                    nearestDist = dist;
                }
            }
            return nearest;
        }

        private static void SeatPedInVehicle(Ped ped, Vehicle veh)
        {
            VehicleSeat seatToUse = VehicleSeat.RightRear;
            if (!veh.IsSeatFree(seatToUse)) seatToUse = VehicleSeat.LeftRear;
            if (!veh.IsSeatFree(seatToUse))
            {
                GTA.UI.Notification.Show("~r~Aucun siège arrière libre dans ce véhicule.");
                return;
            }

            // Ouvrir la porte correspondante
            int doorIdx = seatToUse == VehicleSeat.RightRear ? 3 : 2; // indices GTAV
            Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, veh.Handle, doorIdx, false, false);

            // On s'assure que le ped ne soit plus attaché / escorté
            ped.Detach();
            ped.IsPositionFrozen = false;
            ped.Task.ClearAll();

            // Place immédiatement le suspect à bord pour éviter les errances IA
            ped.SetIntoVehicle(veh, seatToUse);

            // Empêche de sortir/n'être éjecté
            Function.Call(Hash.SET_PED_CAN_BE_DRAGGED_OUT, ped.Handle, false);
            // Interdit à l'IA de décider de quitter le véhicule
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 3, false); // CA_LEAVE_VEHICLES disabled
            // Empêche le joueur de tirer physiquement le ped hors du véhicule
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 398, true); // PlayersDontDragMeOut
            // 184 = PreventAutoShuffleToDriversSeat
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 184, true);
            // 27 = StayInCarOnJack (le ped reste assis même si le conducteur entre ou est éjecté)
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 27, true);
            try { ped.StaysInVehicleWhenJacked = true; } catch { }
            try { ped.AlwaysKeepTask = true; } catch { }

            // Laisser l'animation de siège s'installer puis geler définitivement la position
            GameScheduler.Schedule(() =>
            {
                if (ped.Exists() && ped.IsInVehicle(veh))
                {
                    ped.Task.StandStill(-1);
                    ped.BlockPermanentEvents = true;
                    ped.CanRagdoll = false;
                    ped.IsPositionFrozen = true;
                }
            }, 2000);

            // Fermer la porte après une courte temporisation (optionnel)
            Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, veh.Handle, doorIdx, false);

            GTA.UI.Notification.Show("~g~Suspect placé dans le véhicule.");
        }

        private void StartEscort(Ped ped)
        {
            try
            {
                // On dégel le ped pour qu'il puisse se déplacer à nouveau
                ped.IsPositionFrozen = false;
                ped.Task.ClearAll();

                // Le ped suit immédiatement le joueur à courte distance
                ped.Task.FollowToOffsetFromEntity(Game.Player.Character, new Vector3(0, -0.7f, 0), 1f, -1);
                ped.BlockPermanentEvents = true;
                ped.CanRagdoll = false;

                PoliceArrestShared.EscortedPedHandle = ped.Handle;

                GTA.UI.Notification.Show("~y~Vous escortez le suspect. Appuyez sur ~INPUT_PICKUP~ près d'un véhicule pour le placer.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PoliceArrestModule StartEscort error: {ex.Message}");
            }
        }

        private void StopEscort(Ped ped)
        {
            try
            {
                // Arrête de suivre
                ped.Task.ClearAll();
                // Le remet en position figée (toujours menotté)
                ped.IsPositionFrozen = true;
                PoliceArrestShared.EscortedPedHandle = -1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PoliceArrestModule StopEscort error: {ex.Message}");
            }
        }

        private void RemoveFromVehicleAndEscort(Ped ped, Vehicle veh)
        {
            try
            {
                // Autoriser le ped à quitter le véhicule
                ped.IsPositionFrozen = false;
                ped.Task.ClearAll();

                // Ouvre automatiquement la porte et fait sortir le suspect
                ped.Task.LeaveVehicle(veh, true);

                // Lance l'escorte après une courte temporisation (le temps que l'animation de sortie se joue)
                GameScheduler.Schedule(() =>
                {
                    if (ped.Exists())
                    {
                        StartEscort(ped);
                    }
                }, 1200);

                GTA.UI.Notification.Show("~y~Le suspect sort du véhicule.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PoliceArrestModule RemoveFromVehicleAndEscort error: {ex.Message}");
            }
        }
    }
} 