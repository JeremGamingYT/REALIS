namespace REALIS.Config
{
    /// <summary>
    /// Configuration parameters for the TrafficAI module.
    /// </summary>
    public static class TrafficAIConfig
    {
        /// <summary>Distance in meters to check around NPC vehicles.</summary>
        public const float DetectionRadius = 12.0f;

        /// <summary>Minimum dot product to consider the player blocking the NPC's path.</summary>
        public const float ForwardThreshold = 0.8f;

        /// <summary>Side offset distance when NPC tries to bypass (legacy, kept for compatibility).</summary>
        public const float BypassOffset = 5.0f;

        /// <summary>Forward offset distance when calculating bypass target position (legacy).</summary>
        public const float BypassForwardOffset = 8.0f;

        /// <summary>Realistic scan distance for obstacle detection (shorter for natural behavior).</summary>
        public const float RealisticScanDistance = 10.0f;

        /// <summary>Minimum clearance distance for realistic maneuvers.</summary>
        public const float RealisticMinClearance = 4.0f;

        /// <summary>Maximum bypass distance for natural lane changes.</summary>
        public const float RealisticBypassDistance = 6.0f;

        /// <summary>Update interval in frames.</summary>
        public const int UpdateInterval = 50;

        /// <summary>Duration in frames to keep bypass state for a vehicle.</summary>
        public const int BypassDuration = 150;

        /// <summary>Show debug subtitles when actions occur.</summary>
        public const bool ShowDebug = true;
    }
}
