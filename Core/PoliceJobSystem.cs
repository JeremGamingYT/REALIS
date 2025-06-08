using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using LemonUI;
using LemonUI.Menus;
using REALIS.Common;

namespace REALIS.Core
{
    /// <summary>
    /// Système de job de policier avec blip, zone d'interaction, véhicules personnalisables et système duty
    /// </summary>
    public class PoliceJobSystem : Script
    {
        // Constantes
        private const float INTERACTION_DISTANCE = 3.0f;
        private const float FADE_DURATION = 2000f;
        
        // État du système
        private bool _isOnDuty = false;
        private bool _isInInteraction = false;
        private bool _isFading = false;
        private bool _isInPoliceStation = false;
        private bool _isInCharacterCustomization = false;
        
        // Blips et zones
        private Blip? _policeStationBlip;
        private Vector3 _policeStationEntrance = new Vector3(441.2f, -975.3f, 30.7f); // Mission Row Police Station
        private Vector3 _interactionZone = new Vector3(441.2f, -975.3f, 30.7f);
        private Vector3 _vehicleGarageLocation = new Vector3(3000.0f, 5000.0f, 1000.0f); // Zone cachée hors map
        private Vector3 _vehicleSpawnLocation = new Vector3(3010.0f, 5000.0f, 1000.0f); // Position du véhicule
        private Vector3 _playerHiddenLocation = new Vector3(2990.0f, 4990.0f, 1000.0f); // Position cachée du joueur
        
        // Position et orientation du personnage pour la personnalisation
        private Vector3 _characterPosition = new Vector3(452.52f, -974.01f, 30.69f);
        private float _characterHeading = 88.58f;
        
        // Système de personnage policier
        private Ped? _previewCharacter;
        private Camera? _characterCamera;
        
        // Véhicules de police disponibles
        private readonly Dictionary<string, VehicleHash> _policeVehicles = new Dictionary<string, VehicleHash>
        {
            { "Police Cruiser", VehicleHash.Police },
            { "Police Interceptor", VehicleHash.Police2 },
            { "Police Buffalo", VehicleHash.Police3 },
            { "Police Ranger", VehicleHash.PoliceT },
            { "Police Bike", VehicleHash.Policeb },
            { "SWAT Van", VehicleHash.FBI },
            { "Unmarked Cruiser", VehicleHash.FBI2 }
        };
        
        // Interface utilisateur
        private ObjectPool _menuPool = null!;
        private NativeMenu _mainMenu = null!;
        private NativeMenu _characterMenu = null!;
        private NativeMenu _clothingMenu = null!;
        private NativeMenu _vehicleMenu = null!;
        private NativeMenu _customizationMenu = null!;
        
        // Véhicule sélectionné
        private Vehicle? _selectedVehicle;
        private Vehicle? _previewVehicle; // Véhicule de prévisualisation
        private VehicleHash _selectedVehicleHash;
        private string _selectedVehicleName = "";
        private int _currentVehicleIndex = 0; // Index du véhicule actuellement affiché
        
        // Caméra de prévisualisation
        private Camera? _previewCamera;
        
        // Plateformes invisibles
        private Prop? _invisiblePlatform;
        private Prop? _playerPlatform;
        
        // Variables de personnalisation du personnage
        private PedHash _selectedPedModel = PedHash.Cop01SMY;
        private readonly Dictionary<string, PedHash> _policeCharacters = new Dictionary<string, PedHash>
        {
            { "Male Police Officer 1", PedHash.Cop01SMY },
            { "Male Police Officer 2", PedHash.Sheriff01SMY },
            { "Female Police Officer 1", PedHash.Cop01SFY },
            { "Female Police Officer 2", PedHash.Sheriff01SFY },
            { "SWAT Officer", PedHash.Swat01SMY },
            { "Highway Patrol", PedHash.Hwaycop01SMY }
        };
        
        // Vêtements de police (composants)
        private readonly Dictionary<string, Dictionary<int, int>> _policeOutfits = new Dictionary<string, Dictionary<int, int>>
        {
            { "Standard Uniform", new Dictionary<int, int> { {3, 0}, {4, 25}, {6, 25}, {8, 15}, {11, 55} }},
            { "Traffic Uniform", new Dictionary<int, int> { {3, 1}, {4, 25}, {6, 25}, {8, 15}, {11, 55} }},
            { "SWAT Uniform", new Dictionary<int, int> { {3, 0}, {4, 31}, {6, 25}, {8, 15}, {11, 49} }},
            { "Detective Suit", new Dictionary<int, int> { {3, 0}, {4, 10}, {6, 10}, {8, 15}, {11, 4} }},
            { "Undercover", new Dictionary<int, int> { {3, 0}, {4, 1}, {6, 1}, {8, 2}, {11, 0} }}
        };
        
        private string _selectedOutfit = "Standard Uniform";
        
        // Gestion des wanted levels - supprimé car non utilisé

        public PoliceJobSystem()
        {
            Tick += OnTick;
            Aborted += OnAborted;
            KeyDown += OnKeyDown;
            
            InitializeSystem();
            CreateMenus();
            
            Logger.Info("Police Job System initialized.");
        }

        private void InitializeSystem()
        {
            // Créer le blip du poste de police
            CreatePoliceStationBlip();
            
            // Initialiser le pool de menus
            _menuPool = new ObjectPool();
        }

        private void CreatePoliceStationBlip()
        {
            _policeStationBlip = World.CreateBlip(_policeStationEntrance);
            _policeStationBlip.Sprite = BlipSprite.PoliceStation;
            _policeStationBlip.Color = BlipColor.Blue;
            _policeStationBlip.Name = "Police Job";
            _policeStationBlip.IsShortRange = true;
            
            Logger.Info("Police station blip created.");
        }

        private void CreateMenus()
        {
            // Menu principal
            _mainMenu = new NativeMenu("Police Job", "Welcome to the Police Department");
            _menuPool.Add(_mainMenu);
            
            var startDutyItem = new NativeItem("Start Duty", "Begin your shift as a police officer");
            _mainMenu.Add(startDutyItem);
            
            var exitItem = new NativeItem("Exit", "Leave the police station");
            _mainMenu.Add(exitItem);
            
            // Menu de sélection de personnage
            _characterMenu = new NativeMenu("Police Character", "Select your police character");
            _menuPool.Add(_characterMenu);
            
            foreach (var character in _policeCharacters)
            {
                var characterItem = new NativeItem(character.Key, $"Select {character.Key}");
                _characterMenu.Add(characterItem);
            }
            
            var backFromCharacterItem = new NativeItem("Back", "Return to main menu");
            _characterMenu.Add(backFromCharacterItem);
            
            // Menu de personnalisation des vêtements
            _clothingMenu = new NativeMenu("Police Uniform", "Customize your police uniform");
            _menuPool.Add(_clothingMenu);
            
            foreach (var outfit in _policeOutfits)
            {
                var outfitItem = new NativeItem(outfit.Key, $"Wear {outfit.Key}");
                _clothingMenu.Add(outfitItem);
            }
            
            var continueToVehiclesItem = new NativeItem("Continue to Vehicles", "Proceed to vehicle selection");
            _clothingMenu.Add(continueToVehiclesItem);
            
            var backFromClothingItem = new NativeItem("Back", "Return to character selection");
            _clothingMenu.Add(backFromClothingItem);
            
            // Menu de sélection de véhicules
            _vehicleMenu = new NativeMenu("Police Vehicles", "Select your patrol vehicle");
            _menuPool.Add(_vehicleMenu);
            
            foreach (var vehicle in _policeVehicles)
            {
                var vehicleItem = new NativeItem(vehicle.Key, $"Select {vehicle.Key}");
                _vehicleMenu.Add(vehicleItem);
            }
            
            var backItem = new NativeItem("Back", "Return to main menu");
            _vehicleMenu.Add(backItem);
            
            // Menu de personnalisation
            _customizationMenu = new NativeMenu("Vehicle Performance", "Upgrade your patrol vehicle performance");
            _menuPool.Add(_customizationMenu);
            
            var engineItem = new NativeItem("Engine", "Upgrade engine performance");
            var brakeItem = new NativeItem("Brakes", "Upgrade brake performance");
            var transmissionItem = new NativeItem("Transmission", "Upgrade transmission performance");
            var suspensionItem = new NativeItem("Suspension", "Upgrade suspension performance");
            var turboItem = new NativeItem("Turbo", "Install/Remove turbo");
            var maxTuneItem = new NativeItem("Max Tune", "Apply maximum upgrades to all components");
            var confirmItem = new NativeItem("Confirm Selection", "Start duty with this vehicle");
            var cancelItem = new NativeItem("Cancel", "Go back to vehicle selection");
            
            _customizationMenu.Add(engineItem);
            _customizationMenu.Add(brakeItem);
            _customizationMenu.Add(transmissionItem);
            _customizationMenu.Add(suspensionItem);
            _customizationMenu.Add(turboItem);
            _customizationMenu.Add(maxTuneItem);
            _customizationMenu.Add(confirmItem);
            _customizationMenu.Add(cancelItem);
            
            // Événements des menus
            _mainMenu.ItemActivated += OnMainMenuItemActivated;
            _characterMenu.ItemActivated += OnCharacterMenuItemActivated;
            _clothingMenu.ItemActivated += OnClothingMenuItemActivated;
            _vehicleMenu.ItemActivated += OnVehicleMenuItemActivated;
            _customizationMenu.ItemActivated += OnCustomizationMenuItemActivated;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                // Traiter les menus
                _menuPool.Process();
                
                // Vérifier si le joueur est près de la zone d'interaction
                CheckInteractionZone();
                
                // Gérer le système wanted si on duty
                if (_isOnDuty)
                {
                    ManageWantedSystem();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Police Job System error: {ex.Message}");
            }
        }

        private void CheckInteractionZone()
        {
            if (_isFading || _isInInteraction || _isInCharacterCustomization) return;
            
            var player = Game.Player.Character;
            if (player == null || !player.Exists()) return;
            
            var distance = Vector3.Distance(player.Position, _interactionZone);
            
            // Zone plus large pour la protection "never wanted" (rayon de 15 mètres)
            const float SAFE_ZONE_RADIUS = 15.0f;
            
            if (distance <= SAFE_ZONE_RADIUS)
            {
                // Entrer dans la zone de sécurité du poste de police
                if (!_isInPoliceStation)
                {
                    _isInPoliceStation = true;
                    EnablePoliceStationSafety();
                }
                
                if (distance <= INTERACTION_DISTANCE)
                {
                    // Afficher l'instruction d'interaction
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "Press ~INPUT_CONTEXT~ to enter the Police Station");
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, -1);
                    
                    // Vérifier si E est pressé
                    if (Game.IsKeyPressed(Keys.E))
                    {
                        StartPoliceInteraction();
                    }
                }
            }
            else
            {
                // Sortir de la zone de sécurité du poste de police
                if (_isInPoliceStation)
                {
                    _isInPoliceStation = false;
                    DisablePoliceStationSafety();
                }
            }
        }

        private void StartPoliceInteraction()
        {
            if (_isInInteraction || _isFading) return;
            
            _isInInteraction = true;
            _isFading = true;
            
            // Animation fade out
            Function.Call(Hash.DO_SCREEN_FADE_OUT, (int)FADE_DURATION);
            
            // Attendre le fade out puis téléporter
            Wait(2000);
            
            // Téléporter le joueur dans une position cachée
            var player = Game.Player.Character;
            player.Position = _playerHiddenLocation;
            player.Heading = 0.0f;
            player.IsVisible = false; // Masquer le joueur
            
            // Créer l'environnement de prévisualisation
            CreatePreviewEnvironment();
            
            // Fade in
            Function.Call(Hash.DO_SCREEN_FADE_IN, (int)FADE_DURATION);
            
            Wait(2000);
            
            _isFading = false;
            
            // Ouvrir le menu principal
            _mainMenu.Visible = true;
        }

        private void CreatePreviewEnvironment()
        {
            // Nettoyer les véhicules existants dans la zone
            ClearAreaOfVehicles(_vehicleSpawnLocation, 50.0f);
            
            // Créer la plateforme invisible
            CreateInvisiblePlatform();
            
            // Créer la caméra de prévisualisation
            CreatePreviewCamera();
            
            // Créer le premier véhicule de prévisualisation
            CreatePreviewVehicle(_currentVehicleIndex);
        }

        private void CreatePreviewCamera()
        {
            // Position de la caméra (en face du véhicule)
            var cameraPos = _vehicleSpawnLocation + new Vector3(-8.0f, -3.0f, 2.0f);
            var lookAtPos = _vehicleSpawnLocation;
            
            // Créer la caméra avec la nouvelle API
            _previewCamera = new Camera(Function.Call<int>(Hash.CREATE_CAM, "DEFAULT_SCRIPTED_CAMERA", true));
            _previewCamera.Position = cameraPos;
            _previewCamera.FieldOfView = 60.0f;
            _previewCamera.PointAt(lookAtPos);
            
            // Activer la caméra
            Function.Call(Hash.SET_CAM_ACTIVE, _previewCamera, true);
            Function.Call(Hash.RENDER_SCRIPT_CAMS, true, false, 0, true, false);
        }

        private void CreateInvisiblePlatform()
        {
            // Créer une plateforme invisible sous la zone de spawn des véhicules
            var platformPos = _vehicleSpawnLocation + new Vector3(0.0f, 0.0f, -2.0f); // 2 mètres en dessous
            
            // Créer une plateforme pour le joueur aussi
            var playerPlatformPos = _playerHiddenLocation + new Vector3(0.0f, 0.0f, -2.0f);
            
            // Utiliser un modèle de plateforme plus large (container)
            var platformModel = new Model("prop_container_01a");
            platformModel.Request(5000);
            
            if (platformModel.IsLoaded)
            {
                // Plateforme principale pour les véhicules (plus large)
                _invisiblePlatform = World.CreatePropNoOffset(platformModel, platformPos, Vector3.Zero, false);
                
                if (_invisiblePlatform != null && _invisiblePlatform.Exists())
                {
                    // Rendre la plateforme invisible
                    _invisiblePlatform.IsVisible = false;
                    
                    // S'assurer qu'elle a une collision
                    Function.Call(Hash.SET_ENTITY_COLLISION, _invisiblePlatform, true, true);
                    
                    // La rendre statique
                    Function.Call(Hash.FREEZE_ENTITY_POSITION, _invisiblePlatform, true);
                    
                    Logger.Info("Main invisible platform created for vehicle preview.");
                }
                
                // Plateforme pour le joueur
                _playerPlatform = World.CreatePropNoOffset(platformModel, playerPlatformPos, Vector3.Zero, false);
                
                if (_playerPlatform != null && _playerPlatform.Exists())
                {
                    // Rendre la plateforme invisible
                    _playerPlatform.IsVisible = false;
                    
                    // S'assurer qu'elle a une collision
                    Function.Call(Hash.SET_ENTITY_COLLISION, _playerPlatform, true, true);
                    
                    // La rendre statique
                    Function.Call(Hash.FREEZE_ENTITY_POSITION, _playerPlatform, true);
                    
                    Logger.Info("Player invisible platform created.");
                }
                
            }
            
            platformModel.MarkAsNoLongerNeeded();
        }

        private void CreateAdditionalPlatforms(Vector3 centerPos, Model platformModel)
        {
            // Créer plusieurs plateformes autour de la position centrale pour une zone plus large
            var offsets = new Vector3[]
            {
                new Vector3(10.0f, 0.0f, 0.0f),    // Est
                new Vector3(-10.0f, 0.0f, 0.0f),   // Ouest
                new Vector3(0.0f, 10.0f, 0.0f),    // Nord
                new Vector3(0.0f, -10.0f, 0.0f),   // Sud
                new Vector3(10.0f, 10.0f, 0.0f),   // Nord-Est
                new Vector3(-10.0f, 10.0f, 0.0f),  // Nord-Ouest
                new Vector3(10.0f, -10.0f, 0.0f),  // Sud-Est
                new Vector3(-10.0f, -10.0f, 0.0f)  // Sud-Ouest
            };

            foreach (var offset in offsets)
            {
                var pos = centerPos + offset;
                var extraPlatform = World.CreatePropNoOffset(platformModel, pos, Vector3.Zero, false);
                
                if (extraPlatform != null && extraPlatform.Exists())
                {
                    extraPlatform.IsVisible = false;
                    Function.Call(Hash.SET_ENTITY_COLLISION, extraPlatform, true, true);
                    Function.Call(Hash.FREEZE_ENTITY_POSITION, extraPlatform, true);
                }
            }
            
            Logger.Info("Additional platforms created for larger coverage.");
        }

        private void CreatePreviewVehicle(int vehicleIndex)
        {
            // Supprimer l'ancien véhicule de prévisualisation
            if (_previewVehicle != null && _previewVehicle.Exists())
            {
                _previewVehicle.Delete();
            }
            
            // Obtenir le véhicule à l'index spécifié
            var vehicleList = _policeVehicles.ToList();
            if (vehicleIndex >= 0 && vehicleIndex < vehicleList.Count)
            {
                var vehicleData = vehicleList[vehicleIndex];
                
                // Créer le véhicule
                _previewVehicle = World.CreateVehicle(vehicleData.Value, _vehicleSpawnLocation);
                
                if (_previewVehicle != null && _previewVehicle.Exists())
                {
                    _previewVehicle.Heading = 45.0f; // Angle pour une belle vue
                    _previewVehicle.IsEngineRunning = false;
                    _previewVehicle.IsInvincible = true;
                    
                    // Configuration de police par défaut
                    SetupPoliceVehicle(_previewVehicle);
                    
                    // Lumières allumées pour la présentation
                    Function.Call(Hash.SET_VEHICLE_LIGHTS, _previewVehicle, 2);
                    Function.Call(Hash.SET_VEHICLE_FULLBEAM, _previewVehicle, true);
                    
                    Logger.Info($"Preview vehicle created: {vehicleData.Key}");
                }
            }
        }

        private void ClearAreaOfVehicles(Vector3 position, float radius)
        {
            Function.Call(Hash.CLEAR_AREA_OF_VEHICLES, position.X, position.Y, position.Z, radius, false, false, false, false, false);
        }

        private void OnMainMenuItemActivated(object sender, ItemActivatedArgs e)
        {
            switch (e.Item.Title)
            {
                case "Start Duty":
                    _mainMenu.Visible = false;
                    StartCharacterCustomization();
                    break;
                    
                case "Exit":
                    ExitPoliceStation();
                    break;
            }
        }

        private void OnVehicleMenuItemActivated(object sender, ItemActivatedArgs e)
        {
            if (e.Item.Title == "Back")
            {
                _vehicleMenu.Visible = false;
                _mainMenu.Visible = true;
                return;
            }
            
            // Sélectionner le véhicule
            if (_policeVehicles.ContainsKey(e.Item.Title))
            {
                // Trouver l'index du véhicule sélectionné
                var vehicleList = _policeVehicles.ToList();
                for (int i = 0; i < vehicleList.Count; i++)
                {
                    if (vehicleList[i].Key == e.Item.Title)
                    {
                        _currentVehicleIndex = i;
                        break;
                    }
                }
                
                _selectedVehicleHash = _policeVehicles[e.Item.Title];
                _selectedVehicleName = e.Item.Title;
                
                // Changer le véhicule de prévisualisation
                CreatePreviewVehicle(_currentVehicleIndex);
                
                // Aller au menu de customisation
                _vehicleMenu.Visible = false;
                _customizationMenu.Visible = true;
            }
        }

        private void UpdatePreviewVehicleCustomization()
        {
            // Le véhicule de prévisualisation est déjà créé, on le met juste à jour
            if (_previewVehicle != null && _previewVehicle.Exists())
            {
                // Réappliquer la configuration de police
                SetupPoliceVehicle(_previewVehicle);
                
                // Maintenir les lumières allumées
                Function.Call(Hash.SET_VEHICLE_LIGHTS, _previewVehicle, 2);
                Function.Call(Hash.SET_VEHICLE_FULLBEAM, _previewVehicle, true);
            }
        }

        private void SetupPoliceVehicle(Vehicle vehicle)
        {
            // Installer le mod kit pour permettre les modifications
            vehicle.Mods.InstallModKit();
            
            // Couleurs de police par défaut
            vehicle.Mods.PrimaryColor = VehicleColor.MatteBlack;
            vehicle.Mods.SecondaryColor = VehicleColor.PureWhite;
            
            // Ajouter les équipements de police
            Function.Call(Hash.SET_VEHICLE_IS_STOLEN, vehicle, false);
            Function.Call(Hash.SET_VEHICLE_IS_CONSIDERED_BY_PLAYER, vehicle, true);
            Function.Call(Hash.SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER, vehicle, true);
            
            // Sirène
            Function.Call(Hash.SET_VEHICLE_HAS_MUTED_SIRENS, vehicle, false);
        }

        private void OnCustomizationMenuItemActivated(object sender, ItemActivatedArgs e)
        {
            switch (e.Item.Title)
            {
                case "Engine":
                    CycleEngineUpgrade();
                    break;
                    
                case "Brakes":
                    CycleBrakeUpgrade();
                    break;
                    
                case "Transmission":
                    CycleTransmissionUpgrade();
                    break;
                    
                case "Suspension":
                    CycleSuspensionUpgrade();
                    break;
                    
                case "Turbo":
                    ToggleTurbo();
                    break;
                    
                case "Max Tune":
                    ApplyMaxTune();
                    break;
                    
                case "Confirm Selection":
                    ConfirmVehicleSelection();
                    break;
                    
                case "Cancel":
                    CancelVehicleSelection();
                    break;
            }
        }

        private void CycleEngineUpgrade()
        {
            if (_previewVehicle == null || !_previewVehicle.Exists()) return;
            
            // Installer le mod kit d'abord
            _previewVehicle.Mods.InstallModKit();
            
            var mod = _previewVehicle.Mods[VehicleModType.Engine];
            var currentIndex = mod.Index;
            
            // Cycle through engine levels: Stock (-1), Level 1 (0), Level 2 (1), Level 3 (2), Level 4 (3)
            var nextIndex = currentIndex < 3 ? currentIndex + 1 : -1;
            mod.Index = nextIndex;
            
            var levelText = nextIndex == -1 ? "Stock" : $"Level {nextIndex + 1}";
            Notification.PostTicker($"Engine: {levelText}", false, true);
        }

        private void CycleBrakeUpgrade()
        {
            if (_previewVehicle == null || !_previewVehicle.Exists()) return;
            
            // Installer le mod kit d'abord
            _previewVehicle.Mods.InstallModKit();
            
            var mod = _previewVehicle.Mods[VehicleModType.Brakes];
            var currentIndex = mod.Index;
            
            // Cycle through brake levels: Stock (-1), Level 1 (0), Level 2 (1), Level 3 (2)
            var nextIndex = currentIndex < 2 ? currentIndex + 1 : -1;
            mod.Index = nextIndex;
            
            var levelText = nextIndex == -1 ? "Stock" : $"Level {nextIndex + 1}";
            Notification.PostTicker($"Brakes: {levelText}", false, true);
        }

        private void CycleTransmissionUpgrade()
        {
            if (_previewVehicle == null || !_previewVehicle.Exists()) return;
            
            // Installer le mod kit d'abord
            _previewVehicle.Mods.InstallModKit();
            
            var mod = _previewVehicle.Mods[VehicleModType.Transmission];
            var currentIndex = mod.Index;
            
            // Cycle through transmission levels: Stock (-1), Level 1 (0), Level 2 (1), Level 3 (2)
            var nextIndex = currentIndex < 2 ? currentIndex + 1 : -1;
            mod.Index = nextIndex;
            
            var levelText = nextIndex == -1 ? "Stock" : $"Level {nextIndex + 1}";
            Notification.PostTicker($"Transmission: {levelText}", false, true);
        }

        private void CycleSuspensionUpgrade()
        {
            if (_previewVehicle == null || !_previewVehicle.Exists()) return;
            
            // Installer le mod kit d'abord
            _previewVehicle.Mods.InstallModKit();
            
            var mod = _previewVehicle.Mods[VehicleModType.Suspension];
            var currentIndex = mod.Index;
            
            // Cycle through suspension levels: Stock (-1), Level 1 (0), Level 2 (1), Level 3 (2), Level 4 (3)
            var nextIndex = currentIndex < 3 ? currentIndex + 1 : -1;
            mod.Index = nextIndex;
            
            var levelText = nextIndex == -1 ? "Stock" : $"Level {nextIndex + 1}";
            Notification.PostTicker($"Suspension: {levelText}", false, true);
        }

        private void ToggleTurbo()
        {
            if (_previewVehicle == null || !_previewVehicle.Exists()) return;
            
            // Installer le mod kit d'abord
            _previewVehicle.Mods.InstallModKit();
            
            var turboMod = _previewVehicle.Mods[VehicleToggleModType.Turbo];
            turboMod.IsInstalled = !turboMod.IsInstalled;
            
            var statusText = turboMod.IsInstalled ? "Installed" : "Removed";
            Notification.PostTicker($"Turbo: {statusText}", false, true);
        }

        private void ApplyMaxTune()
        {
            if (_previewVehicle == null || !_previewVehicle.Exists()) return;
            
            // Installer le mod kit d'abord
            _previewVehicle.Mods.InstallModKit();
            
            // Appliquer les modifications maximales
            _previewVehicle.Mods[VehicleModType.Engine].Index = 3;        // Niveau 4 (index 3)
            _previewVehicle.Mods[VehicleModType.Brakes].Index = 2;        // Niveau 3 (index 2)
            _previewVehicle.Mods[VehicleModType.Transmission].Index = 2;   // Niveau 3 (index 2)
            _previewVehicle.Mods[VehicleModType.Suspension].Index = 3;     // Niveau 4 (index 3)
            _previewVehicle.Mods[VehicleToggleModType.Turbo].IsInstalled = true;
            
            Notification.PostTicker("~g~MAX TUNE APPLIED! All performance upgrades maxed out!", false, true);
        }

        private void ConfirmVehicleSelection()
        {
            if (_previewVehicle == null || !_previewVehicle.Exists()) return;
            
            _customizationMenu.Visible = false;
            
            // Créer le véhicule sélectionné avec les customisations
            CreateSelectedVehicle();
            
            // Téléporter à l'extérieur avec le véhicule
            TeleportOutsideWithVehicle();
        }

        private void CreateSelectedVehicle()
        {
            // Sauvegarder les modifications de performance du véhicule de prévisualisation
            var engineLevel = _previewVehicle?.Mods[VehicleModType.Engine].Index ?? -1;
            var brakeLevel = _previewVehicle?.Mods[VehicleModType.Brakes].Index ?? -1;
            var transmissionLevel = _previewVehicle?.Mods[VehicleModType.Transmission].Index ?? -1;
            var suspensionLevel = _previewVehicle?.Mods[VehicleModType.Suspension].Index ?? -1;
            var turboInstalled = _previewVehicle?.Mods[VehicleToggleModType.Turbo].IsInstalled ?? false;
            
            // Créer le véhicule sélectionné aux coordonnées spécifiées
            var spawnPos = new Vector3(439.03f, -1025.86f, 28.52f);
            _selectedVehicle = World.CreateVehicle(_selectedVehicleHash, spawnPos);
            
            if (_selectedVehicle != null && _selectedVehicle.Exists())
            {
                _selectedVehicle.Heading = 1.42f;
                
                // Appliquer les customisations de base (inclut InstallModKit)
                SetupPoliceVehicle(_selectedVehicle);
                
                // Appliquer les modifications de performance
                if (engineLevel >= 0)
                    _selectedVehicle.Mods[VehicleModType.Engine].Index = engineLevel;
                if (brakeLevel >= 0)
                    _selectedVehicle.Mods[VehicleModType.Brakes].Index = brakeLevel;
                if (transmissionLevel >= 0)
                    _selectedVehicle.Mods[VehicleModType.Transmission].Index = transmissionLevel;
                if (suspensionLevel >= 0)
                    _selectedVehicle.Mods[VehicleModType.Suspension].Index = suspensionLevel;
                
                _selectedVehicle.Mods[VehicleToggleModType.Turbo].IsInstalled = turboInstalled;
                
                Logger.Info($"Vehicle created with performance upgrades: Engine={engineLevel}, Brakes={brakeLevel}, Transmission={transmissionLevel}, Suspension={suspensionLevel}, Turbo={turboInstalled}");
            }
        }

        private void TeleportOutsideWithVehicle()
        {
            if (_selectedVehicle == null || !_selectedVehicle.Exists()) return;
            
            _isFading = true;
            
            // Fade out
            Function.Call(Hash.DO_SCREEN_FADE_OUT, (int)FADE_DURATION);
            
            Wait(2000);
            
            // Nettoyer l'environnement de prévisualisation
            CleanupPreviewEnvironment();
            
            // Restaurer la visibilité du joueur et téléporter
            var player = Game.Player.Character;
            player.IsVisible = true;
            player.SetIntoVehicle(_selectedVehicle, VehicleSeat.Driver);
            
            // Activer le mode duty
            StartDuty();
            
            // Fade in
            Function.Call(Hash.DO_SCREEN_FADE_IN, (int)FADE_DURATION);
            
            Wait(2000);
            
            _isFading = false;
            _isInInteraction = false;
            
            // Notification
            Notification.PostTicker("~g~You are now ON DUTY!", false, true);
            
            Logger.Info("Player is now on duty as police officer.");
        }

        private void CleanupPreviewEnvironment()
        {
            // Supprimer le véhicule de prévisualisation
            if (_previewVehicle != null && _previewVehicle.Exists())
            {
                _previewVehicle.Delete();
                _previewVehicle = null;
            }
            
            // Désactiver la caméra de prévisualisation
            if (_previewCamera != null && _previewCamera.Exists())
            {
                Function.Call(Hash.RENDER_SCRIPT_CAMS, false, false, 0, true, false);
                Function.Call(Hash.SET_CAM_ACTIVE, _previewCamera, false);
                _previewCamera.Delete();
                _previewCamera = null;
            }
            
            // Supprimer les plateformes invisibles
            if (_invisiblePlatform != null && _invisiblePlatform.Exists())
            {
                _invisiblePlatform.Delete();
                _invisiblePlatform = null;
            }
            
            if (_playerPlatform != null && _playerPlatform.Exists())
            {
                _playerPlatform.Delete();
                _playerPlatform = null;
            }
            
            // Nettoyer toute la zone de prévisualisation
            Function.Call(Hash.CLEAR_AREA, _vehicleSpawnLocation.X, _vehicleSpawnLocation.Y, _vehicleSpawnLocation.Z, 100.0f, true, false, false, false);
            Function.Call(Hash.CLEAR_AREA, _playerHiddenLocation.X, _playerHiddenLocation.Y, _playerHiddenLocation.Z, 50.0f, true, false, false, false);
        }

        // Gestionnaires des nouveaux menus
        private void OnCharacterMenuItemActivated(object sender, ItemActivatedArgs e)
        {
            if (e.Item.Title == "Back")
            {
                _characterMenu.Visible = false;
                CleanupCharacterPreview();
                ExitPoliceStation();
                return;
            }
            
            // Sélectionner le personnage
            if (_policeCharacters.ContainsKey(e.Item.Title))
            {
                _selectedPedModel = _policeCharacters[e.Item.Title];
                CreateCharacterPreview();
                
                // Aller au menu de personnalisation des vêtements
                _characterMenu.Visible = false;
                _clothingMenu.Visible = true;
            }
        }
        
        private void OnClothingMenuItemActivated(object sender, ItemActivatedArgs e)
        {
            switch (e.Item.Title)
            {
                case "Back":
                    _clothingMenu.Visible = false;
                    _characterMenu.Visible = true;
                    break;
                    
                case "Continue to Vehicles":
                    _clothingMenu.Visible = false;
                    ApplyCharacterToPlayer();
                    StartVehicleSelection();
                    break;
                    
                default:
                    if (_policeOutfits.ContainsKey(e.Item.Title))
                    {
                        _selectedOutfit = e.Item.Title;
                        ApplyOutfitToPreview();
                    }
                    break;
            }
        }
        
        private void StartCharacterCustomization()
        {
            _isInCharacterCustomization = true;
            
            // Créer la caméra pour voir le personnage aux coordonnées spécifiées
            CreateCharacterCamera();
            
            // Créer le personnage de prévisualisation
            CreateCharacterPreview();
            
            // Ouvrir le menu de sélection de personnage
            _characterMenu.Visible = true;
        }
        
        private void CreateCharacterCamera()
        {
            // Position de la caméra devant le personnage
            var cameraPos = _characterPosition + new Vector3(-2.0f, 1.5f, 0.5f);
            
            // Créer la caméra
            _characterCamera = new Camera(Function.Call<int>(Hash.CREATE_CAM, "DEFAULT_SCRIPTED_CAMERA", true));
            _characterCamera.Position = cameraPos;
            _characterCamera.FieldOfView = 50.0f;
            _characterCamera.PointAt(_characterPosition + new Vector3(0.0f, 0.0f, 0.7f));
            
            // Activer la caméra
            Function.Call(Hash.SET_CAM_ACTIVE, _characterCamera, true);
            Function.Call(Hash.RENDER_SCRIPT_CAMS, true, false, 0, true, false);
        }
        
        private void CreateCharacterPreview()
        {
            // Supprimer l'ancien personnage s'il existe
            if (_previewCharacter != null && _previewCharacter.Exists())
            {
                _previewCharacter.Delete();
            }
            
            // Créer le nouveau personnage
            _previewCharacter = World.CreatePed(_selectedPedModel, _characterPosition, _characterHeading);
            
            if (_previewCharacter != null && _previewCharacter.Exists())
            {
                _previewCharacter.IsInvincible = true;
                _previewCharacter.CanRagdoll = false;
                Function.Call(Hash.FREEZE_ENTITY_POSITION, _previewCharacter, true);
                
                // Appliquer la tenue par défaut
                ApplyOutfitToPreview();
                
                Logger.Info($"Character preview created: {_selectedPedModel}");
            }
        }
        
        private void ApplyOutfitToPreview()
        {
            if (_previewCharacter == null || !_previewCharacter.Exists()) return;
            
            if (_policeOutfits.ContainsKey(_selectedOutfit))
            {
                var outfit = _policeOutfits[_selectedOutfit];
                
                foreach (var component in outfit)
                {
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, _previewCharacter, component.Key, component.Value, 0, 2);
                }
                
                Logger.Info($"Applied outfit: {_selectedOutfit}");
            }
        }
        
        private void ApplyCharacterToPlayer()
        {
            var player = Game.Player.Character;
            
            // Changer le modèle du joueur
            var model = new Model(_selectedPedModel);
            model.Request(5000);
            
            if (model.IsLoaded)
            {
                Function.Call(Hash.SET_PLAYER_MODEL, Game.Player, model.Hash);
                player = Game.Player.Character; // Récupérer la nouvelle référence
                
                // Appliquer la tenue sélectionnée
                if (_policeOutfits.ContainsKey(_selectedOutfit))
                {
                    var outfit = _policeOutfits[_selectedOutfit];
                    
                    foreach (var component in outfit)
                    {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, player, component.Key, component.Value, 0, 2);
                    }
                }
                
                // Positionner le joueur aux coordonnées exactes spécifiées
                player.Position = _characterPosition;
                player.Heading = _characterHeading;
                
                Logger.Info($"Player character changed to: {_selectedPedModel} with outfit: {_selectedOutfit}");
            }
            
            model.MarkAsNoLongerNeeded();
        }
        
        private void StartVehicleSelection()
        {
            // Nettoyer la prévisualisation du personnage
            CleanupCharacterPreview();
            
            // Masquer le joueur et téléporter dans la zone cachée pour la sélection de véhicule
            var player = Game.Player.Character;
            player.IsVisible = false;
            player.Position = _playerHiddenLocation;
            
            // Créer l'environnement de prévisualisation des véhicules
            CreatePreviewEnvironment();
            
            // Ouvrir le menu de sélection de véhicules
            _vehicleMenu.Visible = true;
            
            _isInCharacterCustomization = false;
        }
        
        private void CleanupCharacterPreview()
        {
            // Supprimer le personnage de prévisualisation
            if (_previewCharacter != null && _previewCharacter.Exists())
            {
                _previewCharacter.Delete();
                _previewCharacter = null;
            }
            
            // Supprimer la caméra de personnage
            if (_characterCamera != null)
            {
                Function.Call(Hash.RENDER_SCRIPT_CAMS, false, false, 0, true, false);
                Function.Call(Hash.SET_CAM_ACTIVE, _characterCamera, false);
                _characterCamera.Delete();
                _characterCamera = null;
            }
        }

        private void StartDuty()
        {
            _isOnDuty = true;
            
            // Désactiver le wanted system
            Game.Player.Wanted.SetEveryoneIgnorePlayer(true);
            Game.Player.Wanted.SetPoliceIgnorePlayer(true);
        }

        private void CancelVehicleSelection()
        {
            // Pas besoin de supprimer le véhicule de prévisualisation ici,
            // on revient juste au menu de sélection
            _customizationMenu.Visible = false;
            _vehicleMenu.Visible = true;
        }

        private void ExitPoliceStation()
        {
            _mainMenu.Visible = false;
            _isFading = true;
            
            // Fade out
            Function.Call(Hash.DO_SCREEN_FADE_OUT, (int)FADE_DURATION);
            
            Wait(2000);
            
            // Nettoyer l'environnement de prévisualisation
            CleanupPreviewEnvironment();
            
            // Téléporter à l'extérieur et restaurer la visibilité
            var player = Game.Player.Character;
            player.Position = _policeStationEntrance;
            player.Heading = 180.0f;
            player.IsVisible = true;
            
            // Fade in
            Function.Call(Hash.DO_SCREEN_FADE_IN, (int)FADE_DURATION);
            
            Wait(2000);
            
            _isFading = false;
            _isInInteraction = false;
        }

        private void ManageWantedSystem()
        {
            // S'assurer que le joueur n'a pas de wanted level quand on duty
            if (Game.Player.Wanted.WantedLevel > 0)
            {
                Game.Player.Wanted.SetWantedLevel(0, false);
                Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
            }
            
            // Maintenir l'immunité
            Game.Player.Wanted.SetEveryoneIgnorePlayer(true);
            Game.Player.Wanted.SetPoliceIgnorePlayer(true);
        }

        public void EndDuty()
        {
            if (!_isOnDuty) return;
            
            _isOnDuty = false;
            
            // Restaurer le système wanted seulement si on n'est pas dans le poste de police
            if (!_isInPoliceStation)
            {
                Game.Player.Wanted.SetEveryoneIgnorePlayer(false);
                Game.Player.Wanted.SetPoliceIgnorePlayer(false);
            }
            // Si on est encore dans le poste, la protection reste active
            
            Notification.PostTicker("~r~You are now OFF DUTY!", false, true);
            
            Logger.Info("Player is now off duty.");
        }

        private void EnablePoliceStationSafety()
        {
            // Désactiver complètement le système wanted dans le poste de police
            Game.Player.Wanted.SetWantedLevel(0, false);
            Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
            Game.Player.Wanted.SetEveryoneIgnorePlayer(true);
            Game.Player.Wanted.SetPoliceIgnorePlayer(true);
            
            // Nettoyer les policiers autour du joueur pour éviter les conflits
            var player = Game.Player.Character;
            if (player != null && player.Exists())
            {
                World.ClearAreaOfCops(player.Position, 20.0f);
            }
            
            Logger.Info("Police station safety zone activated - Never Wanted mode enabled.");
        }

        private void DisablePoliceStationSafety()
        {
            // Restaurer le système wanted normal seulement si le joueur n'est pas en service
            if (!_isOnDuty)
            {
                Game.Player.Wanted.SetEveryoneIgnorePlayer(false);
                Game.Player.Wanted.SetPoliceIgnorePlayer(false);
                
                Logger.Info("Police station safety zone deactivated - Normal wanted system restored.");
            }
            // Si le joueur est en service, on garde la protection active
        }

        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            // Gestion des touches pour les menus
            if (e.KeyCode == Keys.Escape)
            {
                if (_customizationMenu.Visible)
                {
                    CancelVehicleSelection();
                }
                else if (_vehicleMenu.Visible)
                {
                    _vehicleMenu.Visible = false;
                    _mainMenu.Visible = true;
                }
                else if (_clothingMenu.Visible)
                {
                    _clothingMenu.Visible = false;
                    _characterMenu.Visible = true;
                }
                else if (_characterMenu.Visible)
                {
                    _characterMenu.Visible = false;
                    CleanupCharacterPreview();
                    ExitPoliceStation();
                }
                else if (_mainMenu.Visible)
                {
                    ExitPoliceStation();
                }
            }
            
            // Touche pour terminer le duty (F6)
            if (e.KeyCode == Keys.F6 && _isOnDuty)
            {
                EndDuty();
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            // Nettoyer les ressources
            if (_policeStationBlip != null && _policeStationBlip.Exists())
            {
                _policeStationBlip.Delete();
            }
            
            if (_selectedVehicle != null && _selectedVehicle.Exists())
            {
                _selectedVehicle.Delete();
            }
            
            // Nettoyer l'environnement de prévisualisation
            CleanupPreviewEnvironment();
            
            // Nettoyer la prévisualisation du personnage
            CleanupCharacterPreview();
            
            // Supprimer la plateforme invisible
            if (_invisiblePlatform != null && _invisiblePlatform.Exists())
            {
                _invisiblePlatform.Delete();
                _invisiblePlatform = null;
            }
            
            // Restaurer la visibilité du joueur
            var player = Game.Player.Character;
            if (player != null && player.Exists())
            {
                player.IsVisible = true;
            }
            
            // Restaurer le système wanted si nécessaire
            if (_isOnDuty || _isInPoliceStation)
            {
                Game.Player.Wanted.SetEveryoneIgnorePlayer(false);
                Game.Player.Wanted.SetPoliceIgnorePlayer(false);
            }
            
            Logger.Info("Police Job System cleaned up.");
        }
    }
}