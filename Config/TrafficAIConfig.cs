namespace REALIS.Config
{
    /// <summary>
    /// Simple configuration for TrafficAI - only essential parameters.
    /// </summary>
    public static class TrafficAIConfig
    {
        /// <summary>Distance de détection autour du joueur (en mètres)</summary>
        public const float DetectionRadius = 15.0f;

        /// <summary>Distance minimale avant qu'un NPC considère être bloqué</summary>
        public const float BlockingDistance = 8.0f;

        /// <summary>Vitesse minimale en dessous de laquelle un NPC est considéré comme bloqué</summary>
        public const float MinimumSpeed = 2.0f;

        /// <summary>Distance de contournement (gauche/droite)</summary>
        public const float BypassDistance = 6.0f;

        /// <summary>Distance vers l'avant pour le point de contournement</summary>
        public const float ForwardOffset = 10.0f;

        /// <summary>Distance de recul si impossible de contourner</summary>
        public const float BackupDistance = 8.0f;

        /// <summary>Dot product minimum pour considérer qu'un NPC fait face au joueur</summary>
        public const float FacingThreshold = 0.7f;

        /// <summary>Afficher les messages de debug</summary>
        public const bool ShowDebug = true;
    }
}
