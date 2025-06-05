using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using REALIS.Common;
using REALIS.Core;

namespace REALIS.NPC
{
    /// <summary>
    /// Gestionnaire de réactions dynamiques des PNJ face aux situations criminelles.
    /// </summary>
    public class ReactiveNPCManager : Script
    {
        private const int UPDATE_INTERVAL = 100; // Limit processing load
        private const float SCAN_RADIUS = 25f;
        private const float REACTION_COOLDOWN = 20f;
        private readonly Dictionary<int, DateTime> _lastReaction = new();
        private readonly Random _rand = new();
        private int _tick;

        public ReactiveNPCManager()
        {
            Tick += OnTick;
            Aborted += OnAborted;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _tick++;
                if (_tick % UPDATE_INTERVAL != 0) return;

                ProcessPeds();
            }
            catch (Exception ex)
            {
                Logger.Error($"ReactiveNPC tick error: {ex.Message}");
            }
        }

        private void ProcessPeds()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            Ped[] peds = World.GetNearbyPeds(player.Position, SCAN_RADIUS);
            foreach (var ped in peds)
            {
                if (!IsValidPed(ped, player)) continue;

                if (_lastReaction.TryGetValue(ped.Handle, out var last) &&
                    (DateTime.Now - last).TotalSeconds < REACTION_COOLDOWN)
                    continue;

                bool aiming = Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING, Game.Player.Handle);
                bool weaponDrawn = player.Weapons.Current.Hash != WeaponHash.Unarmed;
                bool playerThreat = (player.IsShooting || (weaponDrawn && aiming)) &&
                                     Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY, ped.Handle, player.Handle, 17);

                bool seesFire = World.GetNearbyPeds(ped.Position, 8f).Any(p => p != ped && p.Exists() && p.IsOnFire);
                bool seesDead = World.GetNearbyPeds(ped.Position, 8f).Any(p => p != ped && p.Exists() && p.IsDead);

                if (playerThreat)
                {
                    ReactToThreat(ped, player);
                }
                else if (seesFire)
                {
                    CallEmergencyServices(ped, AmbientInteractionType.CallFireDept);
                }
                else if (seesDead)
                {
                    CallEmergencyServices(ped, AmbientInteractionType.CallAmbulance);
                    if (_rand.NextDouble() < 0.5)
                        CallEmergencyServices(ped, AmbientInteractionType.CallPolice);
                }
                else
                {
                    continue;
                }

                _lastReaction[ped.Handle] = DateTime.Now;
            }
        }

        private static bool IsValidPed(Ped ped, Ped player)
        {
            return ped != null && ped.Exists() && !ped.IsInVehicle() && ped != player && ped.IsAlive;
        }

        private void ReactToThreat(Ped ped, Ped player)
        {
            double r = _rand.NextDouble();
            if (r < 0.33)
            {
                Function.Call(Hash.TASK_SMART_FLEE_PED, ped.Handle, player.Handle, 80f, -1, false, false);
                FireAmbientEvent(ped, AmbientInteractionType.Flee);
            }
            else if (r < 0.66)
            {
                Function.Call(Hash.TASK_SEEK_COVER_FROM_PED, ped.Handle, player.Handle, -1, false);
                Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ped.Handle, "WORLD_HUMAN_STAND_MOBILE", 0, true);
                FireAmbientEvent(ped, AmbientInteractionType.TakeCover);
            }
            else
            {
                Function.Call(Hash.TASK_COWER, ped.Handle, -1);
                FireAmbientEvent(ped, AmbientInteractionType.Cower);
            }
        }

        private void CallEmergencyServices(Ped ped, AmbientInteractionType service)
        {
            Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ped.Handle, "WORLD_HUMAN_STAND_MOBILE", 0, true);
            FireAmbientEvent(ped, service);
        }

        private void FireAmbientEvent(Ped ped, AmbientInteractionType type)
        {
            try
            {
                var evt = new AmbientInteractionEvent(ped, type, ped.Position);
                CentralEventManager.Instance?.FireEvent(evt);
            }
            catch (Exception ex)
            {
                Logger.Error($"ReactiveNPC event error: {ex.Message}");
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            _lastReaction.Clear();
        }
    }
}
