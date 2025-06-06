using GTA;
using GTA.UI;
using System;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Représente un véhicule PNJ potentiellement bloqué.
    /// </summary>
    internal class BlockedVehicleInfo
    {
        public Ped Driver { get; }
        public Vehicle Vehicle { get; }
        public float BlockedTime { get; set; }
        public bool Honked { get; set; }
        public int BypassAttempts { get; set; }
        public bool HasReversed { get; set; }
        public DateTime LastReverseTime { get; set; }
        public DateTime LastSeen { get; set; }

        public BlockedVehicleInfo(Ped driver, Vehicle vehicle)
        {
            Driver = driver;
            Vehicle = vehicle;
            BlockedTime = 0f;
            Honked = false;
            BypassAttempts = 0;
            HasReversed = false;
            LastReverseTime = DateTime.MinValue;
            LastSeen = DateTime.Now;
        }
    }
} 