using GTA.Math;

namespace REALIS.Job
{
    /// <summary>
    /// Données centralisées des commissariats accessibles pour le mode Police.
    /// </summary>
    public static class PoliceStations
    {
        /// <summary>
        /// Positions des postes de police dans lesquels le joueur peut entrer en solo.
        /// </summary>
        public static readonly Vector3[] Locations =
        {
            // Mission Row Police Station
            new Vector3(441.6f,  -982.4f, 30.69f),
            // Sandy Shores Sheriff Station
            new Vector3(1855.21f, 3683.51f, 34.26f),
            // Paleto Bay Sheriff Station
            new Vector3(-449.86f, 6012.21f, 31.72f)
        };
    }
} 