using System;
using System.Collections.Generic;

namespace REALIS.Job
{
    /// <summary>
    /// Contient les informations partagées des suspects pour les différents modules Police.
    /// </summary>
    public static class PoliceArrestShared
    {
        /// <summary>Handle des peds qui ont levé les mains.</summary>
        public static readonly HashSet<int> SurrenderedPeds = new HashSet<int>();

        /// <summary>Handle des peds menottés.</summary>
        public static readonly HashSet<int> CuffedPeds = new HashSet<int>();

        /// <summary>Infos d'identité retournées pour chaque ped après contrôle.</summary>
        public static readonly Dictionary<int, SuspectInfo> IdInfos = new Dictionary<int, SuspectInfo>();

        /// <summary>Handle du ped actuellement escorté par le joueur ( -1 si aucun ).</summary>
        public static int EscortedPedHandle = -1;

        public class SuspectInfo
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public bool IsWanted { get; set; }
        }
    }
} 