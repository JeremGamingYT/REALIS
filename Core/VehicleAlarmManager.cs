using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;

namespace REALIS.Core
{
    /// <summary>
    ///     Déclenche intelligemment les alarmes des véhicules lorsquil y a des explosions, des tirs ou des projectiles
    ///     à proximité, afin de simuler un système dalarme réaliste comme dans la vie réelle.
    ///
    ///     Implémentation :
    ///     1. À chaque frame, on examine les véhicules proches du joueur (<= 120 m).
    ///     2. Pour chaque véhicule, on teste la présence dexplosions ou de projectiles/balles dans un rayon rapproché.
    ///     3. Si une menace est détectée, on déclenche lalarme via les natives START_VEHICLE_ALARM et SET_VEHICLE_ALARM.
    ///     4. On retient un timestamp afin déviter de lancer lalarme trop souvent pour le même véhicule.
    ///
    ///     Le système est léger : la logique ne sapplique quaux entités proches du joueur, et chaque véhicule mémorise
    ///     la dernière activation dalarme pour éviter le spam de natives.
    /// </summary>
    public class VehicleAlarmManager : Script
    {
        private const float ScanRadius = 120f;  // rayon max autour du joueur pour analyser les véhicules
        private const float ThreatRadius = 12f; // rayon où une explosion/projetile déclenche l'alarme
        private const int AlarmCooldownMs = 30_000; // ne pas relancer l'alarme plus d'une fois toutes les 30 s sur un même véhicule
        private const int AlarmDurationMs = 15_000; // durée estimée de l'alarme GTA (~10-15 s)

        private readonly Dictionary<int, DateTime> _lastAlarm = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, DateTime> _alarmEnds = new Dictionary<int, DateTime>();

        public VehicleAlarmManager()
        {
            Tick += OnTick;
            Interval = 0; // exécute le script chaque frame pour une réactivité maximale
        }

        private void OnTick(object sender, EventArgs e)
        {
            Ped? playerPed = Game.Player?.Character;
            if (playerPed == null || !playerPed.Exists())
                return;

            Vehicle[] vehicles = World.GetNearbyVehicles(playerPed, ScanRadius);
            DateTime now = DateTime.UtcNow;

            foreach (Vehicle veh in vehicles)
            {
                if (veh == null || !veh.Exists())
                    continue;

                int handle = veh.Handle;

                if (_lastAlarm.TryGetValue(handle, out DateTime last) && (now - last).TotalMilliseconds < AlarmCooldownMs)
                {
                    // Alarme déjà déclenchée récemment sur ce véhicule : on ignore.
                    continue;
                }

                Vector3 pos = veh.Position;

                bool explosionNear = Function.Call<bool>(Hash.IS_EXPLOSION_IN_SPHERE, -1 /* any explosion */,
                    pos.X, pos.Y, pos.Z, ThreatRadius);

                bool bulletNear = Function.Call<bool>(Hash.IS_BULLET_IN_AREA, pos.X, pos.Y, pos.Z, ThreatRadius,
                    false /* détecte toutes les balles, peu importe le tireur */);

                if (explosionNear || bulletNear)
                {
                    TriggerAlarm(veh);
                    _lastAlarm[handle] = now;
                    _alarmEnds[handle] = now.AddMilliseconds(AlarmDurationMs);
                }
            }

            // Nettoyer les entrées obsolètes pour les véhicules qui n'existent plus.
            List<int> toRemove = new List<int>();
            foreach (var kvp in _lastAlarm)
            {
                Entity ent = Entity.FromHandle(kvp.Key);
                if (ent == null || !ent.Exists())
                    toRemove.Add(kvp.Key);
            }
            foreach (int h in toRemove)
            {
                _lastAlarm.Remove(h);
                _alarmEnds.Remove(h);
            }

            // S'assure que l'alarm state est désactivé quand la durée est dépassée (si le moteur ne le fait pas seul)
            List<int> finished = new List<int>();
            foreach (var kvp in _alarmEnds)
            {
                if (now > kvp.Value)
                {
                    Entity entity = Entity.FromHandle(kvp.Key);
                    Vehicle? v = entity as Vehicle;
                    if (v != null && v.Exists())
                    {
                        Function.Call(Hash.SET_VEHICLE_ALARM, v, false);
                    }
                    finished.Add(kvp.Key);
                }
            }
            foreach (int h in finished)
            {
                _alarmEnds.Remove(h);
            }
        }

        private static void TriggerAlarm(Vehicle veh)
        {
            try
            {
                veh.StartAlarm();
            }
            catch (Exception ex)
            {
                GTA.UI.Screen.ShowSubtitle($"Alarm error: {ex.Message}", 1000);
            }
        }
    }
} 