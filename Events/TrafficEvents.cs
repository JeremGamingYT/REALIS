namespace REALIS.Events
{
    /// <summary>
    /// Event container for TrafficAI related delegates.
    /// </summary>
    public static class TrafficEvents
    {
        /// <summary>
        /// Delegate triggered when a vehicle tries to bypass the player.
        /// </summary>
        public static event System.Action? VehicleBypass;

        internal static void OnVehicleBypass()
        {
            VehicleBypass?.Invoke();
        }
    }
}
