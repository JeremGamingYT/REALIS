namespace REALIS.Common
{
    /// <summary>
    /// Stocke l'état global des métiers du joueur pour permettre à d'autres modules de s'adapter.
    /// </summary>
    public static class DutyState
    {
        /// <summary>
        /// True si le joueur est actuellement en service de police.
        /// </summary>
        public static bool PoliceOnDuty { get; set; }
    }
} 