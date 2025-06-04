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

        /// <summary>Side offset distance when NPC tries to bypass.</summary>
        public const float BypassOffset = 5.0f;

        /// <summary>Update interval in frames.</summary>
        public const int UpdateInterval = 50;

        /// <summary>Duration in frames to keep bypass state for a vehicle.</summary>
        public const int BypassDuration = 150;

        /// <summary>Show debug subtitles when actions occur.</summary>
        public const bool ShowDebug = true;
    }
}
