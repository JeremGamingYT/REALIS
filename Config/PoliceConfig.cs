using System;
using Newtonsoft.Json;
using System.IO;
using GTA.Math;

namespace REALIS.Config
{
    /// <summary>
    /// Configuration du système de police personnalisé
    /// </summary>
    public class PoliceConfig
    {
        private static PoliceConfig? _instance;
        private static readonly object _lock = new object();
        private const string CONFIG_FILE = "scripts/REALIS_PoliceConfig.json";

        public static PoliceConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = LoadConfig();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Temps d'arrêt requis avant arrestation (en secondes)
        /// </summary>
        [JsonProperty("arrest_delay_seconds")]
        public int ArrestDelaySeconds { get; set; } = 5;

        /// <summary>
        /// Seuil de vitesse pour considérer que le joueur s'est arrêté
        /// </summary>
        [JsonProperty("stop_threshold")]
        public float StopThreshold { get; set; } = 2f;

        /// <summary>
        /// Activer le système de police personnalisé
        /// </summary>
        [JsonProperty("enable_custom_police")]
        public bool EnableCustomPolice { get; set; } = true;

        /// <summary>
        /// Activer la modification de l'agressivité de la police
        /// </summary>
        [JsonProperty("enable_police_aggression_control")]
        public bool EnablePoliceAggressionControl { get; set; } = true;

        /// <summary>
        /// Activer l'arrestation automatique
        /// </summary>
        [JsonProperty("enable_auto_arrest")]
        public bool EnableAutoArrest { get; set; } = true;

        /// <summary>
        /// Activer le transport au poste de police
        /// </summary>
        [JsonProperty("enable_police_transport")]
        public bool EnablePoliceTransport { get; set; } = true;

        /// <summary>
        /// Distance maximale pour chercher une voiture de police
        /// </summary>
        [JsonProperty("police_vehicle_search_radius")]
        public float PoliceVehicleSearchRadius { get; set; } = 100f;

        /// <summary>
        /// Distance minimale pour considérer une menace armée
        /// </summary>
        [JsonProperty("weapon_threat_distance")]
        public float WeaponThreatDistance { get; set; } = 10f;

        /// <summary>
        /// Positions des postes de police personnalisés
        /// </summary>
        [JsonProperty("custom_police_stations")]
        public Vector3[] CustomPoliceStations { get; set; } = new Vector3[]
        {
            new Vector3(425.1f, -979.5f, 30.7f),     // Mission Row Police Station
            new Vector3(1855.1f, 3678.9f, 34.2f),    // Sandy Shores Sheriff
            new Vector3(-449.1f, 6008.5f, 31.7f),    // Paleto Bay Sheriff
            new Vector3(-561.3f, -131.9f, 38.4f),    // Rockford Hills Police
            new Vector3(827.0f, -1290.0f, 28.2f),    // La Mesa Police
            new Vector3(-1096.6f, -845.8f, 19.0f)    // Vespucci Police
        };

        /// <summary>
        /// Messages d'avertissement personnalisés
        /// </summary>
        [JsonProperty("warning_messages")]
        public WarningMessages Messages { get; set; } = new WarningMessages();

        private static PoliceConfig LoadConfig()
        {
            try
            {
                if (File.Exists(CONFIG_FILE))
                {
                    var json = File.ReadAllText(CONFIG_FILE);
                    var config = JsonConvert.DeserializeObject<PoliceConfig>(json);
                    return config ?? new PoliceConfig();
                }
                else
                {
                    // Créer le fichier de configuration par défaut
                    var defaultConfig = new PoliceConfig();
                    defaultConfig.SaveConfig();
                    return defaultConfig;
                }
            }
            catch (Exception ex)
            {
                Core.Logger.Error($"Error loading police config: {ex.Message}");
                return new PoliceConfig();
            }
        }

        /// <summary>
        /// Sauvegarde la configuration
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                var directory = Path.GetDirectoryName(CONFIG_FILE);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(CONFIG_FILE, json);
            }
            catch (Exception ex)
            {
                Core.Logger.Error($"Error saving police config: {ex.Message}");
            }
        }

        /// <summary>
        /// Recharge la configuration depuis le fichier
        /// </summary>
        public static void ReloadConfig()
        {
            lock (_lock)
            {
                _instance = LoadConfig();
            }
        }
    }

    /// <summary>
    /// Messages d'avertissement personnalisables
    /// </summary>
    public class WarningMessages
    {
        [JsonProperty("initial_warning")]
        public string InitialWarning { get; set; } = "~r~Arrêtez-vous ou vous serez arrêté! {0} secondes...";

        [JsonProperty("countdown_warning")]
        public string CountdownWarning { get; set; } = "~r~Arrestation dans {0} secondes...";

        [JsonProperty("arrest_message")]
        public string ArrestMessage { get; set; } = "~g~Vous êtes en état d'arrestation!";

        [JsonProperty("transport_message")]
        public string TransportMessage { get; set; } = "~b~Transport vers le poste de police...";

        [JsonProperty("release_message")]
        public string ReleaseMessage { get; set; } = "~g~Vous avez été relâché du poste de police.";

        [JsonProperty("arrest_cancelled")]
        public string ArrestCancelled { get; set; } = "~r~L'arrestation a été annulée.";
    }
} 