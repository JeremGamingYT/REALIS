using System;

namespace REALIS.Config
{
    /// <summary>
    /// Configuration du système de police réaliste
    /// </summary>
    public static class PoliceConfig
    {
        /// <summary>
        /// Temps en millisecondes avant qu'un policier tente une arrestation si le joueur est immobile
        /// </summary>
        public static int ArrestDelayMs { get; set; } = 2000; // 2 secondes
        
        /// <summary>
        /// Temps en millisecondes de fuite avant qu'un policier utilise un taser
        /// </summary>
        public static int TaserDelayMs { get; set; } = 8000; // 8 secondes
        
        /// <summary>
        /// Distance maximale pour détecter les policiers autour du joueur
        /// </summary>
        public static float PoliceDetectionRange { get; set; } = 50f;
        
        /// <summary>
        /// Distance minimale pour considérer le joueur comme "proche" pour une arrestation
        /// </summary>
        public static float ArrestRange { get; set; } = 8f;
        
        /// <summary>
        /// Distance à partir de laquelle le joueur est considéré en fuite
        /// </summary>
        public static float FleeRange { get; set; } = 15f;
        
        /// <summary>
        /// Vitesse minimale du joueur pour être considéré comme "immobile"
        /// </summary>
        public static float StationarySpeed { get; set; } = 0.5f;
        
        /// <summary>
        /// Durée de l'effet taser en millisecondes
        /// </summary>
        public static int TaserEffectDuration { get; set; } = 4000; // 4 secondes
        
        /// <summary>
        /// Vitesse de conduite lors du transport vers le poste (en mph)
        /// </summary>
        public static float TransportSpeed { get; set; } = 30f;
        
        /// <summary>
        /// Active ou désactive les commandes verbales des policiers
        /// </summary>
        public static bool EnableVerbalCommands { get; set; } = true;
        
        /// <summary>
        /// Active ou désactive les effets sonores du taser
        /// </summary>
        public static bool EnableTaserSounds { get; set; } = true;
        
        /// <summary>
        /// Active ou désactive la création automatique de véhicules de police
        /// </summary>
        public static bool AutoCreatePoliceVehicles { get; set; } = true;
        
        /// <summary>
        /// Intervalle de mise à jour du système en millisecondes
        /// </summary>
        public static int UpdateInterval { get; set; } = 100;
        
        /// <summary>
        /// Active ou désactive le système de police réaliste
        /// </summary>
        public static bool SystemEnabled { get; set; } = true;
        
        /// <summary>
        /// Active ou désactive le mode debug (affichage d'informations supplémentaires)
        /// </summary>
        public static bool DebugMode { get; set; } = false;
        
        /// <summary>
        /// Charge la configuration par défaut
        /// </summary>
        public static void LoadDefaults()
        {
            ArrestDelayMs = 2000;
            TaserDelayMs = 8000;
            PoliceDetectionRange = 50f;
            ArrestRange = 8f;
            FleeRange = 15f;
            StationarySpeed = 0.5f;
            TaserEffectDuration = 4000;
            TransportSpeed = 30f;
            EnableVerbalCommands = true;
            EnableTaserSounds = true;
            AutoCreatePoliceVehicles = true;
            UpdateInterval = 100;
            SystemEnabled = true;
            DebugMode = false;
        }
        
        /// <summary>
        /// Valide la configuration et corrige les valeurs incorrectes
        /// </summary>
        public static void ValidateConfig()
        {
            if (ArrestDelayMs < 500) ArrestDelayMs = 500;
            if (TaserDelayMs < 1000) TaserDelayMs = 1000;
            if (PoliceDetectionRange < 10f) PoliceDetectionRange = 10f;
            if (ArrestRange < 2f) ArrestRange = 2f;
            if (FleeRange < 5f) FleeRange = 5f;
            if (StationarySpeed < 0.1f) StationarySpeed = 0.1f;
            if (TaserEffectDuration < 1000) TaserEffectDuration = 1000;
            if (TransportSpeed < 5f) TransportSpeed = 5f;
            if (UpdateInterval < 50) UpdateInterval = 50;
        }

        // Distances de détection
        public const float POLICE_DETECTION_RANGE = 100f;
        public const float ARREST_RANGE = 5f;
        public const float AIM_DETECTION_RANGE = 30f;
        public const float AIM_ANGLE_THRESHOLD = 15f;

        // Limites d'officiers
        public const int MAX_CHASE_OFFICERS = 5;
        public const int MIN_CHASE_OFFICERS = 2;

        // Temps d'attente (en secondes)
        public const int HANDCUFF_DURATION = 3;
        public const int ESCORT_TIMEOUT = 15;
        public const int TRANSPORT_TIMEOUT = 60;

        // Vitesses
        public const float PATROL_SPEED = 20f;
        public const float CHASE_SPEED = 25f;

        // Précision des tirs
        public const int NON_LETHAL_ACCURACY = 75;
        public const int LETHAL_ACCURACY = 85;
        public const int DEFAULT_ACCURACY = 50;

        // Messages système
        public const string ARREST_WARNING = "~b~Officier:~w~ Ne bougez pas! Vous êtes en état d'arrestation!";
        public const string HANDCUFF_MESSAGE = "~b~Officier:~w~ Mettez vos mains derrière le dos!";
        public const string TRANSPORT_MESSAGE = "~b~Officier:~w~ Direction le poste de police...";
        public const string RELEASE_MESSAGE = "~g~Vous avez été relâché avec un avertissement.";
        public const string COMBAT_WARNING = "~r~Police:~w~ Suspect armé! Ouvrez le feu!";
        public const string CHASE_WARNING = "~y~Police:~w~ Arrêtez-vous! Nous voulons juste vous parler!";

        // Modèles de véhicules de police
        public static readonly string[] POLICE_VEHICLE_MODELS = 
        {
            "POLICE",
            "POLICE2", 
            "POLICE3",
            "POLICE4",
            "SHERIFF",
            "SHERIFF2"
        };

        // Modèles de policiers
        public static readonly string[] POLICE_PED_MODELS = 
        {
            "s_m_y_cop_01",
            "s_f_y_cop_01",
            "s_m_y_sheriff_01",
            "s_f_y_sheriff_01"
        };
    }
} 