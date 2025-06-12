using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.Math;

namespace REALIS.Core
{
    /// <summary>
    ///     Maintient les feux stop (brake lights) allumés sur tous les véhicules à l'arrêt.
    ///     L'algorithme est léger : seules les entités proches du joueur sont traitées,
    ///     et l'état n'est envoyé que lorsqu'il doit changer.
    /// </summary>
    public class BrakeLightManager : Script
    {
        // Intervalle d'update (ms). 100 ms (≈10 fps) suffit pour conserver l'illusion visuelle
        private const int UpdateInterval = 50;
        private const float CheckRadius = 120.0f; // mètres
        // Tolérance de vitesse : certains véhicules oscillent autour de 0,30 m/s même à l'arrêt.
        private const float SpeedThreshold = 0.6f; // m/s

        private readonly Dictionary<int, bool> _forcedState = new Dictionary<int, bool>();

        public BrakeLightManager()
        {
            Tick += OnTick;
            Interval = UpdateInterval;
        }

        private void OnTick(object sender, EventArgs e)
        {
            Ped playerPed = Game.Player.Character;
            if (!playerPed.Exists())
                return;

            Vector3 pos = playerPed.Position;
            List<Vehicle> vehs = new List<Vehicle>(World.GetNearbyVehicles(playerPed, CheckRadius));

            // Inclure explicitement le véhicule du joueur, car GetNearbyVehicles peut l'omettre.
            Vehicle? playerVeh = playerPed.CurrentVehicle;
            if (playerVeh != null && playerVeh.Exists() && !vehs.Contains(playerVeh))
            {
                vehs.Add(playerVeh);
            }

            Vehicle[] nearby = vehs.ToArray();

            // Marquer tous les véhicules qu'on vient de voir pour clean plus tard
            HashSet<int> processed = new HashSet<int>();

            foreach (Vehicle veh in nearby)
            {
                if (!veh.Exists())
                    continue;

                int handle = veh.Handle;
                processed.Add(handle);

                // On ne force les feux que si un conducteur est présent ; ainsi, les véhicules vides à l'arrêt restent éteints
                bool hasDriver = veh.GetPedOnSeat(VehicleSeat.Driver)?.Exists() == true;
                bool shouldBrake = hasDriver && (veh.IsStopped || veh.Speed < SpeedThreshold);

                // Si le véhicule doit afficher les feux stop, on (ré)-envoie la commande CHAQUE tick, car
                // le moteur du jeu remet l'état à false dès qu'il le peut (notamment pour les véhicules pilotés).
                // Pour limiter les appels, on ne spamme que lorsqu'il faut maintenir l'état.

                if (shouldBrake)
                {
                    Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, veh, true);
                    _forcedState[handle] = true;
                }
                else
                {
                    bool prevForced;
                    if (_forcedState.TryGetValue(handle, out prevForced) && prevForced)
                    {
                        Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, veh, false);
                        _forcedState[handle] = false;
                    }
                }
            }

            // Nettoyer les entrées qui ne sont plus proches (véhicules supprimés ou trop loin)
            List<int> toRemove = new List<int>();
            foreach (int handle in _forcedState.Keys)
            {
                if (!processed.Contains(handle))
                {
                    toRemove.Add(handle);
                }
            }
            foreach (int h in toRemove)
            {
                _forcedState.Remove(h);
            }
        }
    }
} 