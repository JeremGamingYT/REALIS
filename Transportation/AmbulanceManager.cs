using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using LemonUI;
using LemonUI.Menus;
using REALIS.Core;
using Screen = GTA.UI.Screen;

namespace REALIS.Transportation
{
    /// <summary>
    /// Types d'urgences médicales disponibles
    /// </summary>
    public enum EmergencyType
    {
        Accident,        // Accidents de voiture
        HeartAttack,     // Crises cardiaques
        Overdose,        // Surdoses
        Violence,        // Victimes de violence
        Sports,          // Blessures sportives
        Workplace,       // Accidents du travail
        Fire,            // Blessés d'incendie
        Drowning,        // Noyades
        Fall,            // Chutes
        Poisoning        // Empoisonnements
    }

    /// <summary>
    /// Système d'ambulancier avec missions de secours médical
    /// </summary>
    public class AmbulanceManager : Script
    {
        #region Fields
        
        private ObjectPool _menuPool;
        private NativeMenu _ambulanceMenu = null!;
        private NativeItem _startMissionItem = null!;
        private NativeItem _endShiftItem = null!;
        
        private Vehicle? _currentAmbulance;
        private bool _isOnShift;
        private bool _isOnMission;
        private List<EmergencyLocation> _emergencyLocations = new();
        private EmergencyLocation? _currentEmergency;
        private Vector3 _jobLocation = new Vector3(294.5f, -1448.5f, 29.97f); // Hôpital Central LS
        private Blip? _jobBlip;
        private Blip? _missionBlip;
        private Blip? _ambulanceBlip;
        
        // Mission management
        private DateTime _missionStartTime;
        private bool _patientSaved;
        private int _patientHealth = 0; // 0-100, augmente quand on soigne
        private DateTime _lastTreatmentTime;
        private Ped? _currentPatient;
        
        // Économie
        private int _totalEarnings;
        private int _patientsRescued;
        private const int BASE_REWARD = 750; // Récompense de base par sauvetage
        private const int TIME_BONUS = 75; // Bonus pour rapidité
        
        // Inventaire médical
        private bool _hasMedicalKit = false;
        
        // Animation et traitement
        private bool _isPerformingTreatment = false;
        private DateTime _treatmentAnimationStart;
        private const int TREATMENT_ANIMATION_DURATION = 3000; // 3 secondes
        
        // Système d'uniforme ambulancier
        private bool _originalOutfitSaved = false;
        private readonly Dictionary<int, int> _originalOutfit = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _originalOutfitTexture = new Dictionary<int, int>();
        
        // Animations de soins
        private readonly string[] MEDICAL_ANIMATIONS = new string[]
        {
            "mini@cpr@char_a@cpr_str", // RCP
            "mini@cpr@char_a@cpr_def", // Défibrillateur
            "amb@medic@standing@kneel@base", // Médecin à genoux
            "mini@repair", // Animation de réparation adaptée pour les soins
            "mp_ped_interaction" // Interaction générale
        };
        
        // Constantes d'animation
        private const string MEDICAL_DICT = "mini@cpr@char_a@cpr_str";
        private const string MEDICAL_ANIM = "cpr_pumpchest";
        private const string KNEEL_DICT = "amb@medic@standing@kneel@base";
        private const string KNEEL_ANIM = "base";
        
        #endregion
        
        #region Initialization
        
        public AmbulanceManager()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            
            _menuPool = new ObjectPool();
            InitializeMenu();
            InitializeEmergencyLocations();
            CreateJobLocationBlip();
            LoadMedicalAnimations();
            
            Logger.Info("Ambulance Manager initialized.");
        }
        
        private void InitializeMenu()
        {
            _ambulanceMenu = new NativeMenu("Service Ambulancier", "Gestion des urgences médicales");
            _menuPool.Add(_ambulanceMenu);
            
            _startMissionItem = new NativeItem("Commencer une mission", "Démarrer une nouvelle mission de secours");
            _endShiftItem = new NativeItem("Terminer le service", "Finir votre service d'ambulancier");
            
            _ambulanceMenu.Add(_startMissionItem);
            _ambulanceMenu.Add(_endShiftItem);
            
            _startMissionItem.Activated += OnStartMission;
            _endShiftItem.Activated += OnEndShift;
        }
        
        private void InitializeEmergencyLocations()
        {
            _emergencyLocations = new List<EmergencyLocation>
            {
                // === ACCIDENTS DE CIRCULATION ===
                new EmergencyLocation("Accident Autoroute LS", new Vector3(448.2f, -1707.3f, 29.7f), "Collision multi-véhicules sur l'autoroute", EmergencyType.Accident),
                new EmergencyLocation("Accident Carrefour Vinewood", new Vector3(213.4f, 162.8f, 104.6f), "Accident grave à un carrefour", EmergencyType.Accident),
                new EmergencyLocation("Accident Pont Davis", new Vector3(157.9f, -1729.4f, 29.3f), "Véhicule tombé du pont", EmergencyType.Accident),
                new EmergencyLocation("Accident Beach Boulevard", new Vector3(-1588.2f, -1038.7f, 13.0f), "Collision frontale sur la côte", EmergencyType.Accident),
                new EmergencyLocation("Accident Tunnel Downtown", new Vector3(240.8f, -1379.2f, 33.7f), "Carambolage dans le tunnel", EmergencyType.Accident),
                
                // === URGENCES CARDIAQUES ===
                new EmergencyLocation("Crise Cardiaque Banque Downtown", new Vector3(-112.2f, -818.4f, 30.8f), "Client en arrêt cardiaque dans la banque", EmergencyType.HeartAttack),
                new EmergencyLocation("Malaise Golf Country Club", new Vector3(-1320.5f, 158.7f, 57.9f), "Golfeur victime d'un malaise", EmergencyType.HeartAttack),
                new EmergencyLocation("Urgence Casino Diamond", new Vector3(925.3f, 46.2f, 80.9f), "Client en détresse cardiaque", EmergencyType.HeartAttack),
                new EmergencyLocation("Crise Restaurant Cluckin Bell", new Vector3(-146.4f, -256.8f, 43.6f), "Chef cuisinier en arrêt cardiaque", EmergencyType.HeartAttack),
                new EmergencyLocation("Malaise Centre Commercial", new Vector3(-668.4f, -854.5f, 24.5f), "Personne âgée en détresse", EmergencyType.HeartAttack),
                new EmergencyLocation("Crise Salle de Sport", new Vector3(-1201.3f, -1568.1f, 4.6f), "Sportif en arrêt cardiaque", EmergencyType.HeartAttack),
                
                // === SURDOSES ===
                new EmergencyLocation("Surdose Strawberry", new Vector3(174.8f, -1738.9f, 29.3f), "Overdose dans un appartement", EmergencyType.Overdose),
                new EmergencyLocation("Surdose Grove Street", new Vector3(-136.2f, -1609.4f, 35.0f), "Jeune en surdose dans la rue", EmergencyType.Overdose),
                new EmergencyLocation("Urgence Club Vanilla", new Vector3(127.8f, -1307.5f, 29.2f), "Surdose dans les toilettes du club", EmergencyType.Overdose),
                new EmergencyLocation("Surdose Skate Park", new Vector3(-1287.8f, -1387.5f, 4.3f), "Skateboard en overdose", EmergencyType.Overdose),
                
                // === VIOLENCE ===
                new EmergencyLocation("Agression Commissariat", new Vector3(436.1f, -982.1f, 30.7f), "Policier blessé par balle", EmergencyType.Violence),
                new EmergencyLocation("Bagarre Bar Tequi-la-la", new Vector3(-561.2f, 286.4f, 82.2f), "Bagarre générale, plusieurs blessés", EmergencyType.Violence),
                new EmergencyLocation("Violence Domestic", new Vector3(-1158.4f, -1528.1f, 10.6f), "Violence conjugale, femme blessée", EmergencyType.Violence),
                new EmergencyLocation("Attaque Parking", new Vector3(215.9f, -809.3f, 30.7f), "Victime d'agression au couteau", EmergencyType.Violence),
                
                // === ACCIDENTS SPORTIFS ===
                new EmergencyLocation("Blessure Tennis Country Club", new Vector3(-1001.2f, -768.3f, 19.3f), "Joueur de tennis blessé", EmergencyType.Sports),
                new EmergencyLocation("Accident Vélo BMX", new Vector3(-1287.8f, -1387.5f, 4.3f), "Cycliste accidenté au skate park", EmergencyType.Sports),
                new EmergencyLocation("Accident Jogging Vinewood", new Vector3(300.5f, 198.2f, 104.4f), "Joggeur blessé en courant", EmergencyType.Sports),
                new EmergencyLocation("Blessure Football Plage", new Vector3(-1604.7f, -1015.8f, 13.0f), "Footballeur blessé sur le terrain", EmergencyType.Sports),
                new EmergencyLocation("Accident Gym Muscle Sands", new Vector3(-1375.8f, -1552.5f, 4.4f), "Bodybuilder blessé", EmergencyType.Sports),
                
                // === ACCIDENTS DU TRAVAIL ===
                new EmergencyLocation("Accident Chantier", new Vector3(-141.3f, -1736.5f, 30.1f), "Ouvrier tombé d'un échafaudage", EmergencyType.Workplace),
                new EmergencyLocation("Brûlure Usine", new Vector3(715.7f, -962.1f, 30.4f), "Employé brûlé par produits chimiques", EmergencyType.Workplace),
                new EmergencyLocation("Électrocution Port", new Vector3(1205.5f, -2872.2f, 13.9f), "Docker électrocuté", EmergencyType.Workplace),
                new EmergencyLocation("Accident Aéroport", new Vector3(-1336.2f, -3044.7f, 13.9f), "Mécanicien blessé par hélice", EmergencyType.Workplace),
                
                // === NOYADES ===
                new EmergencyLocation("Noyade Vespucci Beach", new Vector3(-1223.4f, -1618.2f, 4.0f), "Baigneur en détresse", EmergencyType.Drowning),
                new EmergencyLocation("Urgence Piscine", new Vector3(-820.4f, 179.2f, 72.1f), "Enfant tombé dans la piscine", EmergencyType.Drowning),
                new EmergencyLocation("Noyade Port", new Vector3(-1850.1f, -1248.7f, 8.6f), "Pêcheur tombé à l'eau", EmergencyType.Drowning),
                new EmergencyLocation("Sauvetage Lac", new Vector3(-1368.5f, 4439.3f, 25.1f), "Kayakiste en difficulté", EmergencyType.Drowning),
                
                // === CHUTES ===
                new EmergencyLocation("Chute Escalier Metro", new Vector3(-806.2f, -132.4f, 19.9f), "Personne tombée dans les escaliers", EmergencyType.Fall),
                new EmergencyLocation("Chute Échelle Pompiers", new Vector3(1193.5f, -1473.0f, 34.7f), "Pompier tombé de l'échelle", EmergencyType.Fall),
                new EmergencyLocation("Chute Parking Multiniveau", new Vector3(-796.8f, -2023.5f, 9.2f), "Chute depuis un parking", EmergencyType.Fall),
                new EmergencyLocation("Chute Grue Chantier", new Vector3(-141.3f, -1736.5f, 30.1f), "Ouvrier tombé d'une grue", EmergencyType.Fall),
                new EmergencyLocation("Accident Skate Park", new Vector3(-1287.8f, -1387.5f, 4.3f), "Skateur blessé après une chute", EmergencyType.Fall),
                
                // === EMPOISONNEMENTS ===
                new EmergencyLocation("Intoxication Restaurant", new Vector3(-146.4f, -256.8f, 43.6f), "Intoxication alimentaire massive", EmergencyType.Poisoning),
                new EmergencyLocation("Fuite Gaz Usine", new Vector3(715.7f, -962.1f, 30.4f), "Empoisonnement aux gaz toxiques", EmergencyType.Poisoning),
                new EmergencyLocation("Produits Ménagers Magasin", new Vector3(24.9f, -1347.3f, 29.5f), "Enfant ayant bu des produits ménagers", EmergencyType.Poisoning),
                new EmergencyLocation("Contamination Station Service", new Vector3(265.0f, -1261.3f, 29.3f), "Contamination chimique", EmergencyType.Poisoning),
                new EmergencyLocation("Intoxication Fast Food", new Vector3(-1193.8f, -893.0f, 13.9f), "Intoxication alimentaire", EmergencyType.Poisoning)
            };
        }
        
        private void CreateJobLocationBlip()
        {
            _jobBlip = World.CreateBlip(_jobLocation);
            _jobBlip.Sprite = BlipSprite.Hospital;
            _jobBlip.Color = BlipColor.White;
            _jobBlip.Name = "Emploi - Ambulancier";
            _jobBlip.IsShortRange = false;
            _jobBlip.Scale = 0.9f;
            
            Function.Call(Hash.SET_BLIP_AS_MISSION_CREATOR_BLIP, _jobBlip.Handle, true);
            Function.Call(Hash.SET_BLIP_PRIORITY, _jobBlip.Handle, 10);
        }
        
        /// <summary>
        /// Charge les animations médicales nécessaires pour les soins
        /// </summary>
        private void LoadMedicalAnimations()
        {
            try
            {
                // Charger les dictionnaires d'animation
                Function.Call(Hash.REQUEST_ANIM_DICT, MEDICAL_DICT);
                Function.Call(Hash.REQUEST_ANIM_DICT, KNEEL_DICT);
                Function.Call(Hash.REQUEST_ANIM_DICT, "mini@repair");
                Function.Call(Hash.REQUEST_ANIM_DICT, "mp_ped_interaction");
                
                Logger.Info("Medical animations loaded successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading medical animations: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sauvegarde la tenue actuelle du joueur
        /// </summary>
        private void SaveOriginalOutfit(Ped player)
        {
            try
            {
                if (_originalOutfitSaved) return; // Déjà sauvegardé
                
                _originalOutfit.Clear();
                _originalOutfitTexture.Clear();
                
                // Sauvegarder les composants de vêtements principaux
                for (int i = 0; i < 12; i++) // 0-11 sont les slots de vêtements principaux
                {
                    _originalOutfit[i] = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, player.Handle, i);
                    _originalOutfitTexture[i] = Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, player.Handle, i);
                }
                
                _originalOutfitSaved = true;
                Logger.Info("Original outfit saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving original outfit: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Applique l'uniforme d'ambulancier au joueur
        /// </summary>
        private void ApplyAmbulanceUniform(Ped player)
        {
            try
            {
                // Sauvegarder la tenue originale d'abord
                SaveOriginalOutfit(player);
                
                // Vérifier le genre du personnage pour appliquer les bons vêtements
                bool isMale = Function.Call<bool>(Hash.IS_PED_MALE, player.Handle);
                
                if (isMale)
                {
                    // Uniforme masculin d'ambulancier
                    ApplyMaleAmbulanceUniform(player);
                }
                else
                {
                    // Uniforme féminin d'ambulancier
                    ApplyFemaleAmbulanceUniform(player);
                }
                
                Notification.PostTicker("~b~👨‍⚕️ Uniforme d'ambulancier équipé !", false, true);
                Logger.Info($"Ambulance uniform applied successfully for {(isMale ? "male" : "female")} character.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error applying ambulance uniform: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Applique l'uniforme masculin d'ambulancier
        /// </summary>
        private void ApplyMaleAmbulanceUniform(Ped player)
        {
            // Uniforme médical masculin basique (tenue blanche/bleue)
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 11, 31, 0, 0);  // Torse médical blanc
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 8, 57, 0, 0);   // Sous-vêtement blanc
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 4, 28, 0, 0);   // Pantalon médical
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 6, 54, 0, 0);   // Chaussures médicales
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 3, 85, 0, 0);   // Bras/manches courtes
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 7, 0, 0, 0);    // Pas d'accessoires
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 9, 0, 0, 0);    // Pas de gilet
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 1, 0, 0, 0);    // Visage normal
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 5, 0, 0, 0);    // Pas de sac
        }
        
        /// <summary>
        /// Applique l'uniforme féminin d'ambulancier
        /// </summary>
        private void ApplyFemaleAmbulanceUniform(Ped player)
        {
            // Uniforme médical féminin basique (tenue blanche/bleue)
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 11, 48, 0, 0);  // Torse médical blanc femme
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 8, 36, 0, 0);   // Sous-vêtement blanc
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 4, 34, 0, 0);   // Pantalon médical femme
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 6, 52, 0, 0);   // Chaussures médicales femme
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 3, 109, 0, 0);  // Bras/manches courtes femme
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 7, 0, 0, 0);    // Pas d'accessoires
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 9, 0, 0, 0);    // Pas de gilet
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 1, 0, 0, 0);    // Visage normal
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, 5, 0, 0, 0);    // Pas de sac
        }
        
        /// <summary>
        /// Restaure la tenue originale du joueur
        /// </summary>
        private void RestoreOriginalOutfit(Ped player)
        {
            try
            {
                if (!_originalOutfitSaved)
                {
                    Logger.Info("No original outfit saved to restore.");
                    return;
                }
                
                // Restaurer tous les composants sauvegardés
                foreach (var kvp in _originalOutfit)
                {
                    int componentId = kvp.Key;
                    int drawableId = kvp.Value;
                    int textureId = _originalOutfitTexture.ContainsKey(componentId) ? _originalOutfitTexture[componentId] : 0;
                    
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player.Handle, componentId, drawableId, textureId, 0);
                }
                
                // Réinitialiser les variables
                _originalOutfit.Clear();
                _originalOutfitTexture.Clear();
                _originalOutfitSaved = false;
                
                Notification.PostTicker("~g~Tenue originale restaurée !", false, true);
                Logger.Info("Original outfit restored successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error restoring original outfit: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Main Loop
        
        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _menuPool.Process();
                
                Ped player = Game.Player.Character;
                
                if (!_isOnShift)
                {
                    CheckJobLocation(player);
                }
                else if (_isOnShift && _currentAmbulance != null && _currentAmbulance.Exists())
                {
                    if (_isOnMission && _currentEmergency != null)
                    {
                        HandleEmergencyMission(player);
                    }
                    
                    DisplayAmbulanceHUD();
                }
                else if (_isOnShift && (_currentAmbulance == null || !_currentAmbulance.Exists()))
                {
                    // L'ambulance a disparu, terminer le service
                    EndShift();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ambulance tick error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Job Management
        
        private void CheckJobLocation(Ped player)
        {
            float distance = player.Position.DistanceTo(_jobLocation);
            if (distance < 5.0f)
            {
                Screen.ShowSubtitle("~INPUT_CONTEXT~ Commencer votre service d'ambulancier", 100);
                
                if (Game.IsControlJustPressed(GTA.Control.Context))
                {
                    Logger.Info("Ambulance job activation detected!");
                    StartAmbulanceJob();
                }
            }
        }
        
        private void StartAmbulanceJob()
        {
            try
            {
                Logger.Info("Starting ambulance job...");
                
                // Effet de transition
                Screen.FadeOut(1000);
                Script.Wait(1000);
                
                Ped player = Game.Player.Character;
                
                // Spawn de l'ambulance aux coordonnées exactes spécifiées
                Vector3 ambulanceSpawnPos = new Vector3(292.96f, -1438.64f, 29.36f);
                _currentAmbulance = World.CreateVehicle(VehicleHash.Ambulance, ambulanceSpawnPos, 229.93f);
                
                if (_currentAmbulance != null)
                {
                    _currentAmbulance.IsEngineRunning = true;
                    _currentAmbulance.FuelLevel = 100.0f;
                    
                    // Téléporter le joueur dans l'ambulance
                    player.Task.WarpIntoVehicle(_currentAmbulance, VehicleSeat.Driver);
                    
                    // Créer blip pour l'ambulance
                    _ambulanceBlip = _currentAmbulance.AddBlip();
                    _ambulanceBlip.Sprite = BlipSprite.PersonalVehicleCar;
                    _ambulanceBlip.Color = BlipColor.White;
                    _ambulanceBlip.Name = "Ambulance";
                    
                    // Donner la trousse médicale au joueur
                    GiveMedicalKit(player);
                    
                    // Équiper l'uniforme d'ambulancier
                    ApplyAmbulanceUniform(player);
                    
                    _isOnShift = true;
                    _totalEarnings = 0;
                    
                    // Message de bienvenue
                    Notification.PostTicker("~g~Service d'ambulancier commencé ! Utilisez M pour accéder aux urgences.", false, true);
                    
                    Logger.Info("Ambulance job started successfully.");
                }
                
                Screen.FadeIn(1000);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting ambulance job: {ex.Message}");
                Screen.FadeIn(1000);
            }
        }
        
        private void GiveMedicalKit(Ped player)
        {
            // Supprimer toutes les armes existantes
            player.Weapons.RemoveAll();
            
            // Donner une trousse médicale plus réaliste (utiliser un objet moins violent)
            player.Weapons.Give(WeaponHash.Flashlight, 1, true, true);
            _hasMedicalKit = true;
            
            Notification.PostTicker("~b~Trousse médicale ajoutée à votre inventaire !", false, true);
        }
        
        private void StartRandomMission()
        {
            if (_isOnMission) return;
            
            // Sélectionner une urgence aléatoire
            Random rand = new Random();
            _currentEmergency = _emergencyLocations[rand.Next(_emergencyLocations.Count)];
            
            // Créer le blip de mission
            _missionBlip = World.CreateBlip(_currentEmergency.Position);
            _missionBlip.Sprite = BlipSprite.Waypoint;
            _missionBlip.Color = BlipColor.Red;
            _missionBlip.Name = _currentEmergency.Name;
            Function.Call(Hash.SET_BLIP_ROUTE, _missionBlip.Handle, true);
            Function.Call(Hash.SET_BLIP_ROUTE_COLOUR, _missionBlip.Handle, (int)BlipColor.Red);
            
            // Initialiser la mission
            _isOnMission = true;
            _patientSaved = false;
            _patientHealth = GetInitialPatientHealth(_currentEmergency.Type);
            _missionStartTime = DateTime.Now;
            
            // Créer le patient à la destination
            CreatePatient(_currentEmergency.Position, _currentEmergency.Type);
            
            Notification.PostTicker($"~r~URGENCE ! {_currentEmergency.Name} - {_currentEmergency.Description}", false, true);
            Notification.PostTicker("~y~Rendez-vous sur les lieux et soignez la victime !", false, true);
        }
        
        private int GetInitialPatientHealth(EmergencyType emergencyType)
        {
            return emergencyType switch
            {
                EmergencyType.HeartAttack => 10,     // État critique
                EmergencyType.Overdose => 15,        // Très grave
                EmergencyType.Violence => 25,        // Grave
                EmergencyType.Accident => 30,        // Grave
                EmergencyType.Drowning => 20,        // Très grave
                EmergencyType.Fall => 35,            // Modéré à grave
                EmergencyType.Poisoning => 20,       // Très grave
                EmergencyType.Fire => 25,            // Grave (brûlures)
                EmergencyType.Sports => 50,          // Modéré
                EmergencyType.Workplace => 40,       // Modéré à grave
                _ => 30
            };
        }
        
        private void CreatePatient(Vector3 position, EmergencyType emergencyType)
        {
            try
            {
                // Choisir un modèle de PED approprié
                PedHash patientModel = GetPatientModelForType(emergencyType);
                
                // Ajuster la position pour éviter que le patient apparaisse dans un mur
                Vector3 adjustedPosition = GetAdjustedPatientPosition(position);
                
                // Créer le patient
                _currentPatient = World.CreatePed(patientModel, adjustedPosition);
                
                if (_currentPatient != null && _currentPatient.Exists())
                {
                    // Configuration du patient selon le type d'urgence
                    ConfigurePatientForEmergency(_currentPatient, emergencyType);
                    
                    // Créer un blip pour le patient
                    Blip patientBlip = _currentPatient.AddBlip();
                    patientBlip.Sprite = BlipSprite.Health;
                    patientBlip.Color = BlipColor.Red;
                    patientBlip.Name = "Patient";
                    patientBlip.Scale = 0.8f;
                    
                    Logger.Info($"Patient created at position: {adjustedPosition} (Type: {emergencyType})");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating patient: {ex.Message}");
            }
        }
        
        private PedHash GetPatientModelForType(EmergencyType emergencyType)
        {
            Random rand = new Random();
            
            // Sélectionner des modèles appropriés selon le type d'urgence
            return emergencyType switch
            {
                EmergencyType.Sports => rand.Next(2) == 0 ? PedHash.Runner01AMY : PedHash.Fitness01AFY,
                EmergencyType.Workplace => rand.Next(2) == 0 ? PedHash.Armoured01SMM : PedHash.Construct01SMY,
                EmergencyType.Violence => rand.Next(2) == 0 ? PedHash.Business01AMY : PedHash.Business01AFY,
                EmergencyType.Overdose => rand.Next(2) == 0 ? PedHash.Hipster01AMY : PedHash.Hipster01AFY,
                _ => rand.Next(2) == 0 ? PedHash.Business01AMY : PedHash.Business01AFY
            };
        }
        
        private Vector3 GetAdjustedPatientPosition(Vector3 originalPosition)
        {
            // Utiliser la position exacte pour éviter le décalage waypoint/patient
            World.GetGroundHeight(originalPosition, out float groundZ);
            
            // Si la hauteur du sol est trop différente, utiliser la position originale
            if (Math.Abs(groundZ - originalPosition.Z) > 5.0f)
            {
                return originalPosition;
            }
            
            return new Vector3(originalPosition.X, originalPosition.Y, groundZ + 0.1f);
        }
        
        private void ConfigurePatientForEmergency(Ped patient, EmergencyType emergencyType)
        {
            patient.Health = Math.Max(1, _patientHealth * 10); // Convertir en santé GTA (1-1000)
            patient.CanRagdoll = true;
            patient.CanBeDraggedOutOfVehicle = false;
            patient.CanBeTargetted = false;
            patient.BlockPermanentEvents = true; // Empêche le patient de faire des actions automatiques
            
            // Forcer le patient au sol et blessé pour TOUS les types d'urgence
            patient.Task.ClearAll();
            Script.Wait(100);
            
            // Réduire la santé pour montrer qu'il est blessé
            patient.Health = Math.Max(1, _patientHealth * 3);
            
            // Forcer le patient au sol avec une animation de blessé
            switch (emergencyType)
            {
                case EmergencyType.HeartAttack:
                case EmergencyType.Overdose:
                case EmergencyType.Poisoning:
                    // Patient inconscient au sol
                    Function.Call(Hash.TASK_PLAY_ANIM, patient.Handle, "missminuteman_1ig_2", "handsup_base", 8.0f, -8.0f, -1, 1, 0, false, false, false);
                    break;
                    
                default:
                    // Patient au sol, blessé mais conscient
                    Function.Call(Hash.TASK_PLAY_ANIM, patient.Handle, "random@dealgonewrong", "idle_a", 8.0f, -8.0f, -1, 1, 0, false, false, false);
                    break;
            }
            
            // S'assurer que le patient reste au sol
            Function.Call(Hash.FREEZE_ENTITY_POSITION, patient.Handle, false);
            patient.CanRagdoll = false; // Empêche de se relever
            
            // Ajouter du sang si nécessaire
            if (emergencyType == EmergencyType.Violence || emergencyType == EmergencyType.Accident)
            {
                Function.Call(Hash.APPLY_PED_BLOOD_DAMAGE_BY_ZONE, patient.Handle, 3, 0.0f, 0.0f, 0.0f, "wound_sheet");
            }
            
            Logger.Info($"Patient configured for {emergencyType} - Health: {patient.Health}");
        }
        
        private void HandleEmergencyMission(Ped player)
        {
            if (_currentEmergency == null || _currentPatient == null || !_currentPatient.Exists()) return;
            
            float distance = player.Position.DistanceTo(_currentPatient.Position);
            
            // Gestion de l'animation de traitement
            if (_isPerformingTreatment)
            {
                // Vérifier si l'animation de traitement est terminée
                if ((DateTime.Now - _treatmentAnimationStart).TotalMilliseconds >= TREATMENT_ANIMATION_DURATION)
                {
                    _isPerformingTreatment = false;
                    player.Task.ClearAll();
                    TreatPatient();
                }
                else
                {
                    // Afficher le progrès du traitement
                    int remainingTime = (int)((TREATMENT_ANIMATION_DURATION - (DateTime.Now - _treatmentAnimationStart).TotalMilliseconds) / 1000);
                    Screen.ShowSubtitle($"~b~Soins en cours... {remainingTime + 1}s", 100);
                }
                return;
            }
            
            if (distance < 3.0f && !_patientSaved)
            {
                Screen.ShowSubtitle("~INPUT_CONTEXT~ Soigner le patient avec votre trousse médicale", 100);
                
                // Vérifier si le joueur appuie sur E pour soigner
                if (Game.IsControlJustPressed(GTA.Control.Context))
                {
                    StartTreatmentAnimation(player);
                }
            }
            
            if (_patientSaved)
            {
                Screen.ShowSubtitle("~g~Transportez le patient à l'hôpital !", 100);
                
                // Vérifier si le patient est dans l'ambulance
                if (_currentAmbulance != null && _currentPatient != null && _currentPatient.Exists() && _currentPatient.IsInVehicle(_currentAmbulance))
                {
                    // S'assurer que le patient reste dans l'ambulance
                    EnsurePatientStaysInAmbulance();
                    
                    // Créer un waypoint vers l'hôpital si pas déjà fait
                    if (_missionBlip == null || !_missionBlip.Exists() || _missionBlip.Color != BlipColor.Green)
                    {
                        CreateHospitalWaypoint();
                    }
                    CheckHospitalArrival(player);
                }
                else if (distance < 2.0f)
                {
                    Screen.ShowSubtitle("~INPUT_CONTEXT~ Charger le patient dans l'ambulance", 100);
                    if (Game.IsControlJustPressed(GTA.Control.Context))
                    {
                        LoadPatientInAmbulanceWithAnimation(player);
                    }
                }
            }
        }
        
        private void TreatPatient()
        {
            if ((DateTime.Now - _lastTreatmentTime).TotalMilliseconds < 100) return;
            
            _patientHealth += 3; // Améliorer l'état du patient
            _lastTreatmentTime = DateTime.Now;
            
            if (_patientHealth >= 80)
            {
                _patientSaved = true;
                
                // Améliorer l'état du patient
                if (_currentPatient != null && _currentPatient.Exists())
                {
                    _currentPatient.Task.ClearAll();
                    _currentPatient.Health = 800; // Améliorer la santé
                }
                
                Notification.PostTicker("~g~Patient stabilisé ! Transportez-le à l'hôpital.", false, true);
            }
            else
            {
                // Afficher le progrès
                string progressBar = "";
                int progress = _patientHealth / 10;
                for (int i = 0; i < 10; i++)
                {
                    progressBar += i < progress ? "█" : "░";
                }
                Screen.ShowSubtitle($"~b~Soins en cours: {progressBar} {_patientHealth}%", 100);
            }
        }
        
        /// <summary>
        /// Démarre l'animation de traitement médical
        /// </summary>
        private void StartTreatmentAnimation(Ped player)
        {
            try
            {
                _isPerformingTreatment = true;
                _treatmentAnimationStart = DateTime.Now;
                
                // Position du joueur face au patient
                Vector3 patientPos = _currentPatient?.Position ?? Vector3.Zero;
                Vector3 directionToPatient = (patientPos - player.Position).Normalized;
                
                // Placer le joueur en position pour les soins
                Vector3 treatmentPosition = patientPos + directionToPatient * -1.5f;
                player.Position = treatmentPosition;
                player.Heading = directionToPatient.ToHeading();
                
                // Démarrer l'animation médicale appropriée selon le type d'urgence
                string animDict = MEDICAL_DICT;
                string animName = MEDICAL_ANIM;
                
                // Adapter l'animation selon le type d'urgence
                if (_currentEmergency?.Type == EmergencyType.HeartAttack || _currentEmergency?.Type == EmergencyType.Drowning)
                {
                    animDict = MEDICAL_DICT;
                    animName = MEDICAL_ANIM; // Animation RCP
                }
                else
                {
                    animDict = "mini@repair";
                    animName = "fixing_a_player"; // Animation de soins généraux
                }
                
                // Lancer l'animation
                Function.Call(Hash.TASK_PLAY_ANIM, player.Handle, animDict, animName, 8.0f, -8.0f, -1, 0, 0, false, false, false);
                
                // Effet sonore médical
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "BEDS_HEART_MONITOR", "DLC_BTL_COMPUTER_BEEPS_AND_BOOPS_SOUNDS", true);
                
                Logger.Info($"Started treatment animation for {_currentEmergency?.Type}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting treatment animation: {ex.Message}");
                _isPerformingTreatment = false;
            }
        }
        
        /// <summary>
        /// Charge le patient dans l'ambulance avec animation améliorée
        /// </summary>
        private void LoadPatientInAmbulanceWithAnimation(Ped player)
        {
            if (_currentPatient == null || _currentAmbulance == null) return;
            
            try
            {
                Logger.Info("Starting patient loading with animation...");
                
                // Préparer le patient et le joueur
                _currentPatient.CanRagdoll = false;
                Function.Call(Hash.FREEZE_ENTITY_POSITION, _currentPatient.Handle, true);
                _currentPatient.Task.ClearAll();
                
                // Position du joueur pour l'animation de transport
                Vector3 patientPos = _currentPatient.Position;
                Vector3 ambulancePos = _currentAmbulance.Position;
                Vector3 directionToAmbulance = (ambulancePos - patientPos).Normalized;
                
                // Placer le joueur près du patient
                Vector3 pickupPosition = patientPos + directionToAmbulance * -1.0f;
                player.Position = pickupPosition;
                player.Heading = directionToAmbulance.ToHeading();
                
                // Animation de prise en charge du patient (porter)
                Function.Call(Hash.REQUEST_ANIM_DICT, "anim@heists@box_carry@");
                Script.Wait(100);
                Function.Call(Hash.TASK_PLAY_ANIM, player.Handle, "anim@heists@box_carry@", "idle", 8.0f, 8.0f, -1, 50, 0, false, false, false);
                
                // Message de progression
                Screen.ShowSubtitle("~b~Transport du patient en cours...", 2000);
                
                // Attendre 2 secondes pour l'animation
                Script.Wait(2000);
                
                // Arrêter l'animation du joueur
                player.Task.ClearAll();
                
                // Effet de transition
                Screen.FadeOut(800);
                Script.Wait(800);
                
                // Préparer le patient pour le transport
                Function.Call(Hash.FREEZE_ENTITY_POSITION, _currentPatient.Handle, false);
                _currentPatient.CanRagdoll = true;
                _currentPatient.Task.ClearAll();
                _currentPatient.BlockPermanentEvents = true;
                Script.Wait(200);
                
                // Téléporter le patient dans l'ambulance (siège passager arrière)
                _currentPatient.SetIntoVehicle(_currentAmbulance, VehicleSeat.RightRear);
                Script.Wait(500);
                
                // Vérifier si le patient est bien dans l'ambulance et forcer si nécessaire
                if (!_currentPatient.IsInVehicle(_currentAmbulance))
                {
                    Logger.Info("Patient not in vehicle, forcing entry...");
                    _currentPatient.Task.WarpIntoVehicle(_currentAmbulance, VehicleSeat.RightRear);
                    Script.Wait(300);
                }
                
                // S'assurer que le patient reste dans le véhicule
                _currentPatient.CanBeDraggedOutOfVehicle = false;
                _currentPatient.KnockOffVehicleType = KnockOffVehicleType.Never;
                _currentPatient.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                
                // Le patient porte une ceinture de sécurité (animation assise)
                Function.Call(Hash.TASK_PLAY_ANIM, _currentPatient.Handle, "veh@std@ds@base", "sit", 8.0f, -8.0f, -1, 1, 0, false, false, false);
                
                Screen.FadeIn(800);
                
                // Message de succès et création automatique du waypoint
                Notification.PostTicker("~g~Patient chargé ! Waypoint vers l'hôpital créé.", false, true);
                
                // Créer automatiquement le waypoint vers l'hôpital
                CreateHospitalWaypoint();
                
                Logger.Info("Patient successfully loaded into ambulance with animation");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading patient in ambulance with animation: {ex.Message}");
                Screen.FadeIn(500); // S'assurer que l'écran revient même en cas d'erreur
            }
        }
        
        /// <summary>
        /// Version simple de chargement du patient (utilisée par d'autres méthodes)
        /// </summary>
        private void LoadPatientInAmbulance()
        {
            if (_currentPatient == null || _currentAmbulance == null) return;
            
            try
            {
                // Le patient ne peut plus bouger et reste conscient
                _currentPatient.CanRagdoll = false;
                Function.Call(Hash.FREEZE_ENTITY_POSITION, _currentPatient.Handle, true);
                _currentPatient.Task.ClearAll();
                
                // Téléporter le patient dans l'ambulance (siège passager arrière)
                _currentPatient.Task.WarpIntoVehicle(_currentAmbulance, VehicleSeat.RightRear);
                
                // S'assurer que le patient reste dans le véhicule
                _currentPatient.CanBeDraggedOutOfVehicle = false;
                _currentPatient.KnockOffVehicleType = KnockOffVehicleType.Never;
                _currentPatient.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                
                Logger.Info("Patient successfully loaded into ambulance");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading patient in ambulance: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Crée un waypoint vers l'hôpital avec route GPS activée
        /// </summary>
        private void CreateHospitalWaypoint()
        {
            try
            {
                // Supprimer l'ancien blip de mission
                _missionBlip?.Delete();
                
                // Créer un nouveau blip vers l'hôpital
                _missionBlip = World.CreateBlip(_jobLocation);
                _missionBlip.Sprite = BlipSprite.Hospital;
                _missionBlip.Color = BlipColor.Green;
                _missionBlip.Name = "🏥 Hôpital Central LS - Urgence";
                _missionBlip.Scale = 1.2f;
                
                // Activer la route GPS
                Function.Call(Hash.SET_BLIP_ROUTE, _missionBlip.Handle, true);
                Function.Call(Hash.SET_BLIP_ROUTE_COLOUR, _missionBlip.Handle, (int)BlipColor.Green);
                
                // Définir comme blip de mission prioritaire
                Function.Call(Hash.SET_BLIP_AS_MISSION_CREATOR_BLIP, _missionBlip.Handle, true);
                Function.Call(Hash.SET_BLIP_PRIORITY, _missionBlip.Handle, 10);
                
                // Son de notification GPS
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
                
                Logger.Info("Hospital waypoint created successfully with GPS route");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating hospital waypoint: {ex.Message}");
            }
        }
        
        private void EnsurePatientStaysInAmbulance()
        {
            if (_currentPatient == null || !_currentPatient.Exists() || _currentAmbulance == null) return;
            
            try
            {
                // Vérifier si le patient est dans l'ambulance
                bool isInVehicle = _currentPatient.IsInVehicle(_currentAmbulance);
                
                // Si le patient n'est plus dans l'ambulance, le remettre dedans
                if (!isInVehicle)
                {
                    Logger.Info("Patient escaped ambulance, forcing back in...");
                    _currentPatient.Task.ClearAll();
                    _currentPatient.Task.WarpIntoVehicle(_currentAmbulance, VehicleSeat.RightRear);
                    _currentPatient.CanBeDraggedOutOfVehicle = false;
                    _currentPatient.BlockPermanentEvents = true;
                    Script.Wait(100);
                }
                
                // S'assurer qu'il ne peut pas sortir
                _currentPatient.CanBeDraggedOutOfVehicle = false;
                _currentPatient.KnockOffVehicleType = KnockOffVehicleType.Never;
                _currentPatient.BlockPermanentEvents = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ensuring patient stays in ambulance: {ex.Message}");
            }
        }
        
        private void CheckHospitalArrival(Ped player)
        {
            float distance = player.Position.DistanceTo(_jobLocation);
            
            // Vérifier que le joueur est près de l'hôpital ET que le patient est dans l'ambulance
            if (distance < 15.0f && _currentPatient != null && _currentPatient.Exists() && 
                _currentAmbulance != null && _currentPatient.IsInVehicle(_currentAmbulance))
            {
                Screen.ShowSubtitle("~g~Patient livré à l'hôpital ! Mission accomplie !", 3000);
                Script.Wait(1000); // Petit délai pour voir le message
                CompleteMission();
            }
            else if (distance < 15.0f && (_currentPatient == null || !_currentPatient.IsInVehicle(_currentAmbulance)))
            {
                Screen.ShowSubtitle("~r~Le patient n'est pas dans l'ambulance ! Retournez le chercher.", 100);
            }
            else if (distance < 15.0f)
            {
                Screen.ShowSubtitle("~y~Arrivé à l'hôpital - Livrez le patient...", 100);
            }
        }
        
        private void CompleteMission()
        {
            try
            {
                // Calculer la récompense
                TimeSpan missionTime = DateTime.Now - _missionStartTime;
                int timeBonus = Math.Max(0, TIME_BONUS - (int)missionTime.TotalMinutes * 10);
                int reward = BASE_REWARD + timeBonus;
                
                _totalEarnings += reward;
                _patientsRescued++;
                
                // Message de succès avec le type d'urgence (sauvegardé avant la suppression)
                string emergencyTypeText = GetEmergencyTypeName(_currentEmergency?.Type ?? EmergencyType.Accident);
                
                // Supprimer le patient et les blips
                _currentPatient?.Delete();
                _missionBlip?.Delete();
                
                // Désactiver la route GPS
                Function.Call(Hash.SET_BLIP_ROUTE, false);
                
                // Reset de la mission
                _isOnMission = false;
                _patientSaved = false;
                _isPerformingTreatment = false;
                _currentEmergency = null;
                _currentPatient = null;
                _patientHealth = 0;
                
                // Ajouter l'argent au joueur
                Game.Player.Money += reward;
                
                // Son de mission accomplie
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "MISSION_PASS_NOTIFY", "DLC_HEISTS_GENERAL_FRONTEND_SOUNDS", false);
                
                // Message de succès détaillé
                Notification.PostTicker($"~g~🏥 Mission réussie ! {emergencyTypeText} - ${reward} gagnés", false, true);
                
                // Message d'encouragement
                if (_patientsRescued % 5 == 0)
                {
                    Notification.PostTicker($"~b~🎖️ Excellente performance ! {_patientsRescued} patients sauvés au total !", false, true);
                }
                
                Logger.Info($"Emergency mission completed. Reward: ${reward}, Total patients rescued: {_patientsRescued}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error completing emergency mission: {ex.Message}");
            }
        }
        
        private void EndShift()
        {
            try
            {
                Logger.Info("Ending ambulance shift...");
                
                // Nettoyer les ressources
                _currentPatient?.Delete();
                _missionBlip?.Delete();
                _ambulanceBlip?.Delete();
                
                if (_currentAmbulance != null && _currentAmbulance.Exists())
                {
                    _currentAmbulance.Delete();
                }
                
                // Restaurer l'inventaire normal
                Ped player = Game.Player.Character;
                player.Weapons.RemoveAll();
                
                // Restaurer la tenue originale
                RestoreOriginalOutfit(player);
                
                _isOnShift = false;
                _isOnMission = false;
                _hasMedicalKit = false;
                
                // Message de fin
                if (_patientsRescued > 0)
                {
                    Notification.PostTicker($"~g~Service terminé ! {_patientsRescued} patients sauvés. Total gagné: ${_totalEarnings}", false, true);
                }
                else
                {
                    Notification.PostTicker("~y~Service terminé sans patient secouru.", false, true);
                }
                
                Logger.Info("Ambulance shift ended successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ending ambulance shift: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Menu Events
        
        private void OnStartMission(object sender, EventArgs e)
        {
            if (!_isOnShift) return;
            
            if (_isOnMission)
            {
                Notification.PostTicker("~r~Vous avez déjà une urgence en cours !", false, true);
                return;
            }
            
            StartRandomMission();
            _ambulanceMenu.Visible = false;
        }
        
        private void OnEndShift(object sender, EventArgs e)
        {
            EndShift();
            _ambulanceMenu.Visible = false;
        }
        
        #endregion
        
        #region Controls
        
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (!_isOnShift) return;
                
                switch (e.KeyCode)
                {
                    case Keys.M:
                        if (_ambulanceMenu.Visible)
                        {
                            _ambulanceMenu.Visible = false;
                        }
                        else
                        {
                            _ambulanceMenu.Visible = true;
                        }
                        break;
                        
                    case Keys.End:
                        EndShift();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling ambulance key input: {ex.Message}");
            }
        }
        
        #endregion
        
        #region HUD
        
        private void DisplayAmbulanceHUD()
        {
            try
            {
                string hudText = "~w~SERVICE AMBULANCIER~w~";
                
                if (_isOnMission && _currentEmergency != null)
                {
                    // Afficher le type d'urgence avec une icône
                    string emergencyIcon = GetEmergencyTypeIcon(_currentEmergency.Type);
                    string emergencyName = GetEmergencyTypeName(_currentEmergency.Type);
                    
                    hudText += $" | {emergencyIcon} {emergencyName}: {_currentEmergency.Name}";
                    if (!_patientSaved)
                    {
                        hudText += $" | ~r~État: {_patientHealth}%~w~";
                    }
                    else
                    {
                        // Vérifier si le patient est dans l'ambulance
                        bool patientInAmbulance = _currentPatient != null && _currentPatient.Exists() && 
                                                _currentAmbulance != null && _currentPatient.IsInVehicle(_currentAmbulance);
                        
                        if (patientInAmbulance)
                        {
                            hudText += " | ~g~Patient à bord - Direction hôpital!~w~";
                        }
                        else
                        {
                            hudText += " | ~y~Patient stabilisé - Chargez-le!~w~";
                        }
                    }
                }
                else
                {
                    hudText += " | ~y~En attente d'urgence~w~";
                }
                
                hudText += $" | Patients: {_patientsRescued} | Gains: ~g~${_totalEarnings}~w~";
                hudText += $" | Trousse: {(_hasMedicalKit ? "~g~✓~w~" : "~r~✗~w~")}";
                hudText += " | ~b~M~w~: Menu | ~b~E~w~: Soigner/Charger | ~b~End~w~: Terminer";
                
                Screen.ShowSubtitle(hudText, 100);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error displaying ambulance HUD: {ex.Message}");
            }
        }
        
        private string GetEmergencyTypeIcon(EmergencyType emergencyType)
        {
            return emergencyType switch
            {
                EmergencyType.Accident => "🚗",
                EmergencyType.HeartAttack => "💓",
                EmergencyType.Overdose => "💊",
                EmergencyType.Violence => "🔪",
                EmergencyType.Sports => "⚽",
                EmergencyType.Workplace => "⚠️",
                EmergencyType.Fire => "🔥",
                EmergencyType.Drowning => "🌊",
                EmergencyType.Fall => "⬇️",
                EmergencyType.Poisoning => "☠️",
                _ => "🚑"
            };
        }
        
        private string GetEmergencyTypeName(EmergencyType emergencyType)
        {
            return emergencyType switch
            {
                EmergencyType.Accident => "Accident",
                EmergencyType.HeartAttack => "Cardiaque",
                EmergencyType.Overdose => "Surdose",
                EmergencyType.Violence => "Agression",
                EmergencyType.Sports => "Sportif",
                EmergencyType.Workplace => "Travail",
                EmergencyType.Fire => "Brûlures",
                EmergencyType.Drowning => "Noyade",
                EmergencyType.Fall => "Chute",
                EmergencyType.Poisoning => "Empoisonnement",
                _ => "Urgence"
            };
        }
        
        #endregion
        
        #region Cleanup
        
        private void OnAborted(object sender, EventArgs e)
        {
            Logger.Info("Ambulance Manager is being unloaded.");
            
            try
            {
                _currentPatient?.Delete();
                _jobBlip?.Delete();
                _missionBlip?.Delete();
                _ambulanceBlip?.Delete();
                
                if (_currentAmbulance != null && _currentAmbulance.Exists())
                {
                    _currentAmbulance.Delete();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during ambulance cleanup: {ex.Message}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Représente un emplacement d'urgence médicale
    /// </summary>
    public class EmergencyLocation
    {
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public string Description { get; set; }
        public EmergencyType Type { get; set; }
        
        public EmergencyLocation(string name, Vector3 position, string description, EmergencyType type)
        {
            Name = name;
            Position = position;
            Description = description;
            Type = type;
        }
    }
}