using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using Newtonsoft.Json;

namespace REALIS.Core
{
    /// <summary>
    /// Configuration utilisateur principal
    /// </summary>
    public class UserConfiguration
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";
        
        [JsonProperty("isFirstRunCompleted")]
        public bool IsFirstRunCompleted { get; set; } = false;
        
        [JsonProperty("firstRunCompletedDate")]
        public DateTime? FirstRunCompletedDate { get; set; }
        
        [JsonProperty("language")]
        public string Language { get; set; } = "en";
        
        [JsonProperty("enableNotifications")]
        public bool EnableNotifications { get; set; } = true;
        
        [JsonProperty("enableAudioFeedback")]
        public bool EnableAudioFeedback { get; set; } = true;
        
        [JsonProperty("debugMode")]
        public bool DebugMode { get; set; } = false;
        
        [JsonProperty("autoSaveInterval")]
        public int AutoSaveIntervalMinutes { get; set; } = 30;
        
        [JsonProperty("modSettings")]
        public ModSettings ModSettings { get; set; } = new ModSettings();
    }
    
    /// <summary>
    /// Paramètres spécifiques du mod
    /// </summary>
    public class ModSettings
    {
        [JsonProperty("policeSystemEnabled")]
        public bool PoliceSystemEnabled { get; set; } = true;
        
        [JsonProperty("trafficAIEnabled")]
        public bool TrafficAIEnabled { get; set; } = true;
        
        [JsonProperty("gasStationSystemEnabled")]
        public bool GasStationSystemEnabled { get; set; } = true;
        
        [JsonProperty("foodStoreSystemEnabled")]
        public bool FoodStoreSystemEnabled { get; set; } = true;
        
        [JsonProperty("vehicleDealershipEnabled")]
        public bool VehicleDealershipEnabled { get; set; } = true;
        
        [JsonProperty("busDriverSystemEnabled")]
        public bool BusDriverSystemEnabled { get; set; } = true;
        
        [JsonProperty("taxiDriverSystemEnabled")]
        public bool TaxiDriverSystemEnabled { get; set; } = true;
        
        [JsonProperty("firefighterSystemEnabled")]
        public bool FirefighterSystemEnabled { get; set; } = true;
        
        [JsonProperty("ambulanceSystemEnabled")]
        public bool AmbulanceSystemEnabled { get; set; } = true;
        
        [JsonProperty("policeJobSystemEnabled")]
        public bool PoliceJobSystemEnabled { get; set; } = true;
        
        [JsonProperty("realisticTrafficDensity")]
        public float RealisticTrafficDensity { get; set; } = 1.0f;
        
        [JsonProperty("policeResponseLevel")]
        public int PoliceResponseLevel { get; set; } = 3; // 1-5 scale
        
        [JsonProperty("economicDifficulty")]
        public int EconomicDifficulty { get; set; } = 2; // 1-3 scale
    }
    
    /// <summary>
    /// Configuration des langues et traductions
    /// </summary>
    public class LanguageConfiguration
    {
        [JsonProperty("supportedLanguages")]
        public Dictionary<string, string> SupportedLanguages { get; set; } = new Dictionary<string, string>
        {
            { "en", "English" },
            { "fr", "Français" },
            { "es", "Español" },
            { "de", "Deutsch" },
            { "it", "Italiano" },
            { "pt", "Português" },
            { "ru", "Русский" }
        };
        
        [JsonProperty("translations")]
        public Dictionary<string, Dictionary<string, string>> Translations { get; set; } = 
            new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "en", new Dictionary<string, string>
                    {
                        { "welcome_title", "Welcome to REALIS" },
                        { "welcome_subtitle", "Enhanced Realism for GTA V" },
                        { "first_run_message", "Thank you for installing REALIS! Let's configure your experience." },
                        { "language_selection", "Select Language" },
                        { "language_prompt", "Choose your preferred language:" },
                        { "keybind_setup", "Key Bindings Setup" },
                        { "keybind_prompt", "Configure your control keys:" },
                        { "settings_title", "General Settings" },
                        { "enable_notifications", "Enable notifications" },
                        { "enable_audio", "Enable audio feedback" },
                        { "debug_mode", "Debug mode" },
                        { "police_system", "Police System" },
                        { "traffic_ai", "Traffic AI" },
                        { "gas_stations", "Gas Stations" },
                        { "food_stores", "Food Stores" },
                        { "vehicle_dealerships", "Vehicle Dealerships" },
                        { "bus_driver_system", "Bus Driver System" },
                        { "firefighter_system", "Firefighter System" },
                        { "ambulance_system", "Ambulance System" },
                        { "save_and_continue", "Save & Continue" },
                        { "skip_setup", "Skip Setup" },
                        { "setup_complete", "Setup Complete!" },
                        { "setup_complete_message", "REALIS has been configured successfully. Enjoy your enhanced experience!" },
                        { "menu_title", "REALIS Configuration" },
                        { "open_menu", "Open Configuration Menu" },
                        { "toggle_police", "Toggle Police System" },
                        { "toggle_traffic", "Toggle Traffic AI" }
                    }
                },
                {
                    "fr", new Dictionary<string, string>
                    {
                        { "welcome_title", "Bienvenue dans REALIS" },
                        { "welcome_subtitle", "Réalisme Amélioré pour GTA V" },
                        { "first_run_message", "Merci d'avoir installé REALIS ! Configurons votre expérience." },
                        { "language_selection", "Sélection de la langue" },
                        { "language_prompt", "Choisissez votre langue préférée :" },
                        { "keybind_setup", "Configuration des touches" },
                        { "keybind_prompt", "Configurez vos touches de contrôle :" },
                        { "settings_title", "Paramètres généraux" },
                        { "enable_notifications", "Activer les notifications" },
                        { "enable_audio", "Activer le retour audio" },
                        { "debug_mode", "Mode debug" },
                        { "police_system", "Système de police" },
                        { "traffic_ai", "IA de trafic" },
                        { "gas_stations", "Stations-service" },
                        { "food_stores", "Magasins d'alimentation" },
                        { "vehicle_dealerships", "Concessionnaires automobiles" },
                        { "bus_driver_system", "Système de Chauffeur de Bus" },
                        { "firefighter_system", "Système de Pompier" },
                        { "ambulance_system", "Système d'Ambulancier" },
                        { "save_and_continue", "Sauvegarder et continuer" },
                        { "skip_setup", "Ignorer la configuration" },
                        { "setup_complete", "Configuration terminée !" },
                        { "setup_complete_message", "REALIS a été configuré avec succès. Profitez de votre expérience améliorée !" },
                        { "menu_title", "Configuration REALIS" },
                        { "open_menu", "Ouvrir le menu de configuration" },
                        { "toggle_police", "Basculer le système de police" },
                        { "toggle_traffic", "Basculer l'IA de trafic" }
                    }
                }
            };
        
        /// <summary>
        /// Obtient une traduction pour une clé donnée
        /// </summary>
        public string GetTranslation(string key, string language = "en")
        {
            if (Translations.ContainsKey(language) && Translations[language].ContainsKey(key))
            {
                return Translations[language][key];
            }
            
            // Fallback vers l'anglais si la traduction n'existe pas
            if (language != "en" && Translations.ContainsKey("en") && Translations["en"].ContainsKey(key))
            {
                return Translations["en"][key];
            }
            
            return key; // Retourne la clé si aucune traduction n'est trouvée
        }
    }
    
    /// <summary>
    /// Configuration des touches du clavier
    /// </summary>
    public class KeybindConfiguration
    {
        [JsonProperty("openMenu")]
        public KeyBindInfo OpenMenu { get; set; } = new KeyBindInfo(Keys.F9);
        
        [JsonProperty("togglePoliceSystem")]
        public KeyBindInfo TogglePoliceSystem { get; set; } = new KeyBindInfo(Keys.F10);
        
        [JsonProperty("toggleTrafficAI")]
        public KeyBindInfo ToggleTrafficAI { get; set; } = new KeyBindInfo(Keys.F11);
        
        [JsonProperty("emergencyServices")]
        public KeyBindInfo EmergencyServices { get; set; } = new KeyBindInfo(Keys.F12);
        
        [JsonProperty("quickSave")]
        public KeyBindInfo QuickSave { get; set; } = new KeyBindInfo(Keys.F5);
        
        [JsonProperty("debugToggle")]
        public KeyBindInfo DebugToggle { get; set; } = new KeyBindInfo(Keys.F8);
        
        [JsonProperty("interactionKey")]
        public KeyBindInfo InteractionKey { get; set; } = new KeyBindInfo(Keys.E);
        
        [JsonProperty("modifierKey")]
        public KeyBindInfo ModifierKey { get; set; } = new KeyBindInfo(Keys.LControlKey);
    }
    
    /// <summary>
    /// Informations sur une liaison de touche
    /// </summary>
    public class KeyBindInfo
    {
        [JsonProperty("key")]
        public Keys Key { get; set; }
        
        [JsonProperty("requiresModifier")]
        public bool RequiresModifier { get; set; } = false;
        
        [JsonProperty("modifierKey")]
        public Keys ModifierKey { get; set; } = Keys.None;
        
        [JsonProperty("description")]
        public string Description { get; set; } = "";
        
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;
        
        public KeyBindInfo() { }
        
        public KeyBindInfo(Keys key, bool requiresModifier = false, Keys modifierKey = Keys.None, string description = "")
        {
            Key = key;
            RequiresModifier = requiresModifier;
            ModifierKey = modifierKey;
            Description = description;
        }
        
        /// <summary>
        /// Vérifie si la combinaison de touches est actuellement pressée
        /// </summary>
        public bool IsPressed()
        {
            if (!Enabled) return false;
            
            if (RequiresModifier && ModifierKey != Keys.None)
            {
                return Game.IsKeyPressed(Key) && Game.IsKeyPressed(ModifierKey);
            }
            
            return Game.IsKeyPressed(Key);
        }
        
        /// <summary>
        /// Vérifie si la combinaison de touches vient d'être pressée (une seule fois)
        /// </summary>
        public bool IsJustPressed()
        {
            if (!Enabled) return false;
            
            if (RequiresModifier && ModifierKey != Keys.None)
            {
                return Game.IsKeyPressed(Key) && Game.IsKeyPressed(ModifierKey);
            }
            
            return Game.IsKeyPressed(Key);
        }
        
        /// <summary>
        /// Retourne une représentation textuelle de la liaison
        /// </summary>
        public override string ToString()
        {
            if (RequiresModifier && ModifierKey != Keys.None)
            {
                return $"{ModifierKey} + {Key}";
            }
            return Key.ToString();
        }
    }
} 