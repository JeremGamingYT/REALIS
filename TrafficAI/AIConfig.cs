using System;
using Newtonsoft.Json;
using System.IO;
using REALIS.Core;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Configuration avancée pour le système d'IA de conduite.
    /// Permet d'ajuster finement le comportement des NPCs.
    /// </summary>
    public class AIConfig
    {
        private static AIConfig? _instance;
        public static AIConfig Instance => _instance ??= LoadConfig();

        // Paramètres de détection et navigation
        [JsonProperty("detectionSettings")]
        public DetectionSettings Detection { get; set; } = new();

        [JsonProperty("drivingBehavior")]
        public DrivingBehaviorSettings DrivingBehavior { get; set; } = new();

        [JsonProperty("trafficLightSettings")]
        public TrafficLightSettings TrafficLights { get; set; } = new();

        [JsonProperty("overtakingSettings")]
        public OvertakingSettings Overtaking { get; set; } = new();

        [JsonProperty("performanceSettings")]
        public PerformanceSettings Performance { get; set; } = new();

        private static AIConfig LoadConfig()
        {
            try
            {
                var configPath = Path.Combine("scripts", "REALIS_AIConfig.json");
                
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<AIConfig>(json);
                    return config ?? new AIConfig();
                }
                
                // Créer un fichier de configuration par défaut
                var defaultConfig = new AIConfig();
                defaultConfig.SaveConfig();
                return defaultConfig;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load AI config: {ex.Message}");
                return new AIConfig();
            }
        }

        public void SaveConfig()
        {
            try
            {
                var configPath = Path.Combine("scripts", "REALIS_AIConfig.json");
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save AI config: {ex.Message}");
            }
        }

        public void ReloadConfig()
        {
            _instance = LoadConfig();
        }
    }

    public class DetectionSettings
    {
        [JsonProperty("obstacleDetectionRange")]
        public float ObstacleDetectionRange { get; set; } = 25f;

        [JsonProperty("vehicleScanRadius")]
        public float VehicleScanRadius { get; set; } = 60f;

        [JsonProperty("pedestrianDetectionRange")]
        public float PedestrianDetectionRange { get; set; } = 12f;

        [JsonProperty("collisionPredictionTime")]
        public float CollisionPredictionTime { get; set; } = 3f;

        [JsonProperty("emergencyBrakeThreshold")]
        public float EmergencyBrakeThreshold { get; set; } = 8f;

        [JsonProperty("intersectionDetectionRadius")]
        public float IntersectionDetectionRadius { get; set; } = 20f;
    }

    public class DrivingBehaviorSettings
    {
        [JsonProperty("maxCitySpeed")]
        public float MaxCitySpeed { get; set; } = 50f;

        [JsonProperty("maxHighwaySpeed")]
        public float MaxHighwaySpeed { get; set; } = 80f;

        [JsonProperty("normalFollowingDistance")]
        public float NormalFollowingDistance { get; set; } = 6f;

        [JsonProperty("enhancedFollowingDistance")]
        public float EnhancedFollowingDistance { get; set; } = 10f;

        [JsonProperty("intersectionSpeed")]
        public float IntersectionSpeed { get; set; } = 15f;

        [JsonProperty("driverAbility")]
        public float DriverAbility { get; set; } = 1.0f;

        [JsonProperty("driverAggressiveness")]
        public float DriverAggressiveness { get; set; } = 0.3f;

        [JsonProperty("enableAdvancedSteering")]
        public bool EnableAdvancedSteering { get; set; } = true;

        [JsonProperty("respectSpeedLimits")]
        public bool RespectSpeedLimits { get; set; } = true;
    }

    public class TrafficLightSettings
    {
        [JsonProperty("redLightBrakeDistance")]
        public float RedLightBrakeDistance { get; set; } = 25f;

        [JsonProperty("yellowLightDecisionDistance")]
        public float YellowLightDecisionDistance { get; set; } = 15f;

        [JsonProperty("stopLineDistance")]
        public float StopLineDistance { get; set; } = 5f;

        [JsonProperty("strictRedLightCompliance")]
        public bool StrictRedLightCompliance { get; set; } = true;

        [JsonProperty("enableTrafficLightDetection")]
        public bool EnableTrafficLightDetection { get; set; } = true;

        [JsonProperty("intersectionPriorityRules")]
        public bool IntersectionPriorityRules { get; set; } = true;
    }

    public class OvertakingSettings
    {
        [JsonProperty("enableOvertaking")]
        public bool EnableOvertaking { get; set; } = true;

        [JsonProperty("minOvertakeSpeed")]
        public float MinOvertakeSpeed { get; set; } = 12f;

        [JsonProperty("overtakeDetectionRange")]
        public float OvertakeDetectionRange { get; set; } = 40f;

        [JsonProperty("safeOvertakeClearance")]
        public float SafeOvertakeClearance { get; set; } = 8f;

        [JsonProperty("slowVehicleThreshold")]
        public float SlowVehicleThreshold { get; set; } = 8f;

        [JsonProperty("overtakeOnlyInSafeLanes")]
        public bool OvertakeOnlyInSafeLanes { get; set; } = true;

        [JsonProperty("respectOvertakingRestrictions")]
        public bool RespectOvertakingRestrictions { get; set; } = true;
    }

    public class PerformanceSettings
    {
        [JsonProperty("maxEnhancedVehicles")]
        public int MaxEnhancedVehicles { get; set; } = 3; // Drastiquement réduit

        [JsonProperty("maxNavigationUpdates")]
        public int MaxNavigationUpdates { get; set; } = 2; // Très limité

        [JsonProperty("maxTrafficLightVehicles")]
        public int MaxTrafficLightVehicles { get; set; } = 3; // Très limité

        [JsonProperty("enhancementCooldown")]
        public float EnhancementCooldown { get; set; } = 15f; // Cooldown très long

        [JsonProperty("routeOptimizationInterval")]
        public float RouteOptimizationInterval { get; set; } = 30f; // Intervalle très long

        [JsonProperty("obstacleMemoryTime")]
        public float ObstacleMemoryTime { get; set; } = 60f; // Mémoire plus longue

        [JsonProperty("enablePerformanceLogging")]
        public bool EnablePerformanceLogging { get; set; } = false;
        
        [JsonProperty("enableEmergencyMode")]
        public bool EnableEmergencyMode { get; set; } = true; // Mode sécurisé par défaut
    }

    /// <summary>
    /// Gestionnaire centralisé pour appliquer la configuration AI aux différents systèmes.
    /// </summary>
    public static class AIConfigManager
    {
        private static DateTime _lastConfigCheck = DateTime.MinValue;
        private const float CONFIG_CHECK_INTERVAL = 30f; // Vérifier la config toutes les 30 secondes

        public static void ApplyConfiguration()
        {
            try
            {
                var config = AIConfig.Instance;
                Logger.Info("Applied AI configuration settings");
                
                // Log des paramètres importants
                if (config.Performance.EnablePerformanceLogging)
                {
                    Logger.Info($"AI Config - Max Enhanced Vehicles: {config.Performance.MaxEnhancedVehicles}");
                    Logger.Info($"AI Config - Overtaking Enabled: {config.Overtaking.EnableOvertaking}");
                    Logger.Info($"AI Config - Traffic Light Detection: {config.TrafficLights.EnableTrafficLightDetection}");
                    Logger.Info($"AI Config - Max City Speed: {config.DrivingBehavior.MaxCitySpeed}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply AI configuration: {ex.Message}");
            }
        }

        public static void CheckForConfigUpdates()
        {
            try
            {
                if ((DateTime.Now - _lastConfigCheck).TotalSeconds > CONFIG_CHECK_INTERVAL)
                {
                    var configPath = Path.Combine("scripts", "REALIS_AIConfig.json");
                    
                    if (File.Exists(configPath))
                    {
                        var lastWrite = File.GetLastWriteTime(configPath);
                        if (lastWrite > _lastConfigCheck)
                        {
                            AIConfig.Instance.ReloadConfig();
                            ApplyConfiguration();
                            Logger.Info("AI Configuration reloaded from file");
                        }
                    }
                    
                    _lastConfigCheck = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Config update check failed: {ex.Message}");
            }
        }

        public static T GetSetting<T>(Func<AIConfig, T> selector)
        {
            try
            {
                return selector(AIConfig.Instance);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get AI setting: {ex.Message}");
                return default(T)!;
            }
        }

        public static void UpdateSetting<T>(Action<AIConfig> updater)
        {
            try
            {
                updater(AIConfig.Instance);
                AIConfig.Instance.SaveConfig();
                Logger.Info("AI Configuration updated and saved");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to update AI setting: {ex.Message}");
            }
        }
    }
} 