using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using Newtonsoft.Json;
using GTA;
using GTA.UI;

namespace REALIS.Core
{
    /// <summary>
    /// Gestionnaire principal des configurations du mod REALIS
    /// </summary>
    public static class ConfigurationManager
    {
        private static readonly string CONFIG_DIRECTORY = Path.Combine(Application.StartupPath, "scripts", "REALIS");
        private static readonly string USER_CONFIG_FILE = Path.Combine(CONFIG_DIRECTORY, "UserConfig.json");
        private static readonly string LANGUAGE_CONFIG_FILE = Path.Combine(CONFIG_DIRECTORY, "Languages.json");
        private static readonly string KEYBIND_CONFIG_FILE = Path.Combine(CONFIG_DIRECTORY, "Keybinds.json");
        
        public static UserConfiguration UserConfig { get; private set; } = new UserConfiguration();
        public static LanguageConfiguration LanguageConfig { get; private set; } = new LanguageConfiguration();
        public static KeybindConfiguration KeybindConfig { get; private set; } = new KeybindConfiguration();
        
        public static bool IsFirstRun => !File.Exists(USER_CONFIG_FILE);
        
        /// <summary>
        /// Initialise le système de configuration
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Créer le dossier de configuration s'il n'existe pas
                Directory.CreateDirectory(CONFIG_DIRECTORY);
                
                // Charger ou créer les configurations
                LoadConfigurations();
                
                Logger.Info("Configuration system initialized successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize configuration system: {ex.Message}");
                CreateDefaultConfigurations();
            }
        }
        
        /// <summary>
        /// Charge toutes les configurations depuis les fichiers
        /// </summary>
        private static void LoadConfigurations()
        {
            LoadUserConfiguration();
            LoadLanguageConfiguration();
            LoadKeybindConfiguration();
        }
        
        /// <summary>
        /// Charge la configuration utilisateur
        /// </summary>
        private static void LoadUserConfiguration()
        {
            if (File.Exists(USER_CONFIG_FILE))
            {
                try
                {
                    var json = File.ReadAllText(USER_CONFIG_FILE);
                    UserConfig = JsonConvert.DeserializeObject<UserConfiguration>(json) ?? new UserConfiguration();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load user configuration: {ex.Message}");
                    UserConfig = new UserConfiguration();
                }
            }
            else
            {
                UserConfig = new UserConfiguration();
            }
        }
        
        /// <summary>
        /// Charge la configuration des langues
        /// </summary>
        private static void LoadLanguageConfiguration()
        {
            if (File.Exists(LANGUAGE_CONFIG_FILE))
            {
                try
                {
                    var json = File.ReadAllText(LANGUAGE_CONFIG_FILE);
                    LanguageConfig = JsonConvert.DeserializeObject<LanguageConfiguration>(json) ?? new LanguageConfiguration();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load language configuration: {ex.Message}");
                    LanguageConfig = new LanguageConfiguration();
                }
            }
            else
            {
                CreateDefaultLanguageConfiguration();
            }
        }
        
        /// <summary>
        /// Charge la configuration des touches
        /// </summary>
        private static void LoadKeybindConfiguration()
        {
            if (File.Exists(KEYBIND_CONFIG_FILE))
            {
                try
                {
                    var json = File.ReadAllText(KEYBIND_CONFIG_FILE);
                    KeybindConfig = JsonConvert.DeserializeObject<KeybindConfiguration>(json) ?? new KeybindConfiguration();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load keybind configuration: {ex.Message}");
                    KeybindConfig = new KeybindConfiguration();
                }
            }
            else
            {
                CreateDefaultKeybindConfiguration();
            }
        }
        
        /// <summary>
        /// Sauvegarde la configuration utilisateur
        /// </summary>
        public static void SaveUserConfiguration()
        {
            try
            {
                var json = JsonConvert.SerializeObject(UserConfig, Formatting.Indented);
                File.WriteAllText(USER_CONFIG_FILE, json);
                Logger.Info("User configuration saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save user configuration: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sauvegarde la configuration des touches
        /// </summary>
        public static void SaveKeybindConfiguration()
        {
            try
            {
                var json = JsonConvert.SerializeObject(KeybindConfig, Formatting.Indented);
                File.WriteAllText(KEYBIND_CONFIG_FILE, json);
                Logger.Info("Keybind configuration saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save keybind configuration: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Crée les configurations par défaut
        /// </summary>
        private static void CreateDefaultConfigurations()
        {
            UserConfig = new UserConfiguration();
            CreateDefaultLanguageConfiguration();
            CreateDefaultKeybindConfiguration();
        }
        
        /// <summary>
        /// Crée la configuration de langue par défaut
        /// </summary>
        private static void CreateDefaultLanguageConfiguration()
        {
            LanguageConfig = new LanguageConfiguration();
            
            try
            {
                var json = JsonConvert.SerializeObject(LanguageConfig, Formatting.Indented);
                File.WriteAllText(LANGUAGE_CONFIG_FILE, json);
                Logger.Info("Default language configuration created.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create default language configuration: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Crée la configuration des touches par défaut
        /// </summary>
        private static void CreateDefaultKeybindConfiguration()
        {
            KeybindConfig = new KeybindConfiguration();
            
            try
            {
                var json = JsonConvert.SerializeObject(KeybindConfig, Formatting.Indented);
                File.WriteAllText(KEYBIND_CONFIG_FILE, json);
                Logger.Info("Default keybind configuration created.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create default keybind configuration: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Marque la première utilisation comme terminée
        /// </summary>
        public static void MarkFirstRunCompleted()
        {
            UserConfig.IsFirstRunCompleted = true;
            UserConfig.FirstRunCompletedDate = DateTime.Now;
            SaveUserConfiguration();
        }
        
        /// <summary>
        /// Obtient la traduction d'une clé
        /// </summary>
        public static string GetTranslation(string key)
        {
            return LanguageConfig.GetTranslation(key, UserConfig.Language);
        }
        
        /// <summary>
        /// Change la langue de l'interface
        /// </summary>
        public static void ChangeLanguage(string language)
        {
            if (LanguageConfig.SupportedLanguages.ContainsKey(language))
            {
                UserConfig.Language = language;
                SaveUserConfiguration();
                Logger.Info($"Language changed to: {language}");
            }
        }
    }
} 