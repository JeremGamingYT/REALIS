using GTA;

namespace REALIS.NPC.Core
{
    /// <summary>
    /// Extensions pour les classes GTA
    /// </summary>
    public static class PoliceExtensions
    {
        /// <summary>
        /// Vérifie si un Ped est valide
        /// </summary>
        public static bool IsValid(this Ped ped)
        {
            return ped != null && ped.Exists() && ped.IsAlive;
        }

        /// <summary>
        /// Vérifie si un Vehicle est valide
        /// </summary>
        public static bool IsValid(this Vehicle vehicle)
        {
            return vehicle != null && vehicle.Exists();
        }
    }
} 