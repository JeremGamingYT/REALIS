using System;
using GTA;

namespace REALIS.Events
{
    /// <summary>
    /// Gestionnaire d'événements pour le système de police
    /// </summary>
    public static class PoliceEvents
    {
        /// <summary>
        /// Événement déclenché lors d'une poursuite par la police
        /// </summary>
        public static event Action<Ped>? PlayerChaseStarted;

        /// <summary>
        /// Déclenche l'événement PlayerChaseStarted
        /// </summary>
        public static void OnPlayerChaseStarted(Ped officer)
        {
            PlayerChaseStarted?.Invoke(officer);
        }

        /// <summary>
        /// Événement déclenché lors d'une arrestation
        /// </summary>
        public static event Action<Ped>? PlayerArrested;

        /// <summary>
        /// Déclenche l'événement PlayerArrested
        /// </summary>
        public static void OnPlayerArrested(Ped officer)
        {
            PlayerArrested?.Invoke(officer);
        }

        /// <summary>
        /// Événement déclenché lorsque le joueur vise un policier
        /// </summary>
        public static event Action<Ped>? PlayerAimingAtOfficer;

        /// <summary>
        /// Déclenche l'événement PlayerAimingAtOfficer
        /// </summary>
        public static void OnPlayerAimingAtOfficer(Ped officer)
        {
            PlayerAimingAtOfficer?.Invoke(officer);
        }

        /// <summary>
        /// Événement déclenché lorsque le joueur est menotté
        /// </summary>
        public static event Action<Ped>? PlayerHandcuffed;

        /// <summary>
        /// Déclenche l'événement PlayerHandcuffed
        /// </summary>
        public static void OnPlayerHandcuffed(Ped officer)
        {
            PlayerHandcuffed?.Invoke(officer);
        }

        /// <summary>
        /// Événement déclenché lorsque le joueur est escorté vers le véhicule de police
        /// </summary>
        public static event Action<Ped, Vehicle>? PlayerEscorted;

        /// <summary>
        /// Déclenche l'événement PlayerEscorted
        /// </summary>
        public static void OnPlayerEscorted(Ped officer, Vehicle policeVehicle)
        {
            PlayerEscorted?.Invoke(officer, policeVehicle);
        }

        /// <summary>
        /// Événement déclenché lorsque le joueur est transporté au poste de police
        /// </summary>
        public static event Action<Vehicle>? PlayerTransported;

        /// <summary>
        /// Déclenche l'événement PlayerTransported
        /// </summary>
        public static void OnPlayerTransported(Vehicle policeVehicle)
        {
            PlayerTransported?.Invoke(policeVehicle);
        }
    }
} 