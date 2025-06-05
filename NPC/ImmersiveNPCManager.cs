using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.Math;
using REALIS.Common;
using REALIS.Core;

namespace REALIS.NPC
{
    /// <summary>
    /// Gestionnaire léger pour des interactions PNJ immersives.
    /// Utilise le CentralEventManager pour éviter les conflits avec d'autres scripts.
    /// </summary>
    public class ImmersiveNPCManager : Script, IEventHandler
    {
        private readonly Dictionary<int, DateTime> _lastInteraction = new();
        private const int UPDATE_INTERVAL = 200; // limiter la charge
        private const float SCAN_RADIUS = 10f;
        private const float INTERACTION_COOLDOWN = 30f; // secondes
        private int _tick = 0;
        private bool _registered = false;

        public ImmersiveNPCManager()
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

                if (!_registered && CentralEventManager.Instance != null)
                {
                    CentralEventManager.Instance.RegisterHandler(REALISEventType.AmbientInteraction, this);
                    _registered = true;
                }

                ProcessNearbyPeds();
            }
            catch (Exception ex)
            {
                Logger.Error($"ImmersiveNPC tick error: {ex.Message}");
            }
        }

        private void ProcessNearbyPeds()
        {
            Ped player = Game.Player.Character;
            var peds = World.GetNearbyPeds(player.Position, SCAN_RADIUS);

            foreach (var ped in peds)
            {
                if (ped == null || !ped.Exists() || ped.IsInVehicle()) continue;
                if (ped == player) continue;

                if (_lastInteraction.TryGetValue(ped.Handle, out var last) &&
                    (DateTime.Now - last).TotalSeconds < INTERACTION_COOLDOWN)
                    continue;

                // Choix simple d'interaction
                if (player.Position.DistanceTo(ped.Position) < 3f && ped.IsOnFoot)
                {
                    Function.Call(Hash.PLAY_PED_AMBIENT_SPEECH_NATIVE, ped.Handle, "GENERIC_HI", "SPEECH_PARAMS_FORCE");
                    FireAmbientEvent(ped, AmbientInteractionType.Greeting);
                }
                else
                {
                    TaskAmbientScenario(ped);
                    FireAmbientEvent(ped, AmbientInteractionType.IdleScenario);
                }

                _lastInteraction[ped.Handle] = DateTime.Now;
            }
        }

        private void TaskAmbientScenario(Ped ped)
        {
            try
            {
                Function.Call(Hash.CLEAR_PED_TASKS, ped.Handle);
                string scenario = GetRandomScenario();
                Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ped.Handle, scenario, 0, true);
            }
            catch (Exception ex)
            {
                Logger.Error($"Scenario task error: {ex.Message}");
            }
        }

        private string GetRandomScenario()
        {
            string[] scenarios =
            {
                "WORLD_HUMAN_SMOKING",
                "WORLD_HUMAN_STAND_MOBILE",
                "WORLD_HUMAN_AA_SMOKE",
                "WORLD_HUMAN_STAND_IMPATIENT"
            };
            return scenarios[new Random().Next(scenarios.Length)];
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
                Logger.Error($"Fire event error: {ex.Message}");
            }
        }

        public bool CanHandle(GameEvent gameEvent) => gameEvent.EventType == REALISEventType.AmbientInteraction;

        public void Handle(GameEvent gameEvent)
        {
            // Par défaut ce gestionnaire ne fait rien, mais la méthode est requise pour l'interface
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                if (_registered && CentralEventManager.Instance != null)
                {
                    CentralEventManager.Instance.UnregisterHandler(REALISEventType.AmbientInteraction, this);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"ImmersiveNPC cleanup error: {ex.Message}");
            }
        }
    }
}
