using System;

namespace REALIS.Events
{
    /// <summary>
    /// Gestionnaire d'événements pour le système de trafic
    /// </summary>
    public static class TrafficEvents
    {
        /// <summary>
        /// Événement déclenché lors d'un contournement de véhicule
        /// </summary>
        public static event Action? VehicleBypass;

        /// <summary>
        /// Déclenche l'événement VehicleBypass
        /// </summary>
        public static void OnVehicleBypass()
        {
            VehicleBypass?.Invoke();
        }

        /// <summary>
        /// Événement déclenché lors d'un changement de voie
        /// </summary>
        public static event Action? LaneChange;

        /// <summary>
        /// Déclenche l'événement LaneChange
        /// </summary>
        public static void OnLaneChange()
        {
            LaneChange?.Invoke();
        }

        /// <summary>
        /// Événement déclenché lors de la détection d'un obstacle
        /// </summary>
        public static event Action<object>? ObstacleDetected;

        /// <summary>
        /// Déclenche l'événement ObstacleDetected
        /// </summary>
        /// <param name="obstacle">L'obstacle détecté</param>
        public static void OnObstacleDetected(object obstacle)
        {
            ObstacleDetected?.Invoke(obstacle);
        }

        /// <summary>
        /// Événement déclenché lors du démarrage du système de trafic
        /// </summary>
        public static event Action? TrafficSystemStarted;

        /// <summary>
        /// Déclenche l'événement TrafficSystemStarted
        /// </summary>
        public static void OnTrafficSystemStarted()
        {
            TrafficSystemStarted?.Invoke();
        }

        /// <summary>
        /// Événement déclenché lors de l'arrêt du système de trafic
        /// </summary>
        public static event Action? TrafficSystemStopped;

        /// <summary>
        /// Déclenche l'événement TrafficSystemStopped
        /// </summary>
        public static void OnTrafficSystemStopped()
        {
            TrafficSystemStopped?.Invoke();
        }
    }
} 