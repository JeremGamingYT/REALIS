using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using REALIS.Common;
using LemonUI;
using LemonUI.Menus;
using LemonUI.Elements;

namespace REALIS.Core
{
    /// <summary>
    /// Gestionnaire des concessionnaires de véhicules - Permet l'achat sans menu
    /// </summary>
    public class VehicleDealershipManager : Script
    {
        private readonly List<DealershipVehicle> _dealershipVehicles = new();
        private readonly Dictionary<VehicleHash, int> _vehiclePrices = new();
        private readonly Dictionary<VehicleHash, VehicleSpecs> _vehicleSpecs = new();
        private readonly List<Blip> _dealershipBlips = new();
        private readonly Dictionary<int, VehicleDisplayInfo> _vehicleDisplayCache = new(); // Cache des infos d'affichage
        private bool _isEnabled = true;
        private DateTime _lastUpdate = DateTime.MinValue;
        private DateTime _lastTextUpdate = DateTime.MinValue;
        private const int UPDATE_INTERVAL_MS = 0;
        private const int TEXT_UPDATE_INTERVAL_MS = 0; // Texte mis à jour seulement 2 fois par seconde
        
        // LemonUI Elements
        private readonly ObjectPool _menuPool = new();
        private NativeMenu? _vehiclePreviewMenu;
        private NativeMenu? _purchaseConfirmMenu;
        private DealershipVehicle? _currentVehicleBeingViewed;
        private bool _isMenuActive = false;
        
        // Système d'essai de véhicules
        private DealershipVehicle? _currentTestDriveVehicle = null;
        private DateTime _testDriveStartTime;
        private const int TEST_DRIVE_DURATION_SECONDS = 60; // 1 minute d'essai
        private bool _isTestDriving = false;
        private float _initialVehicleHealth = 1000.0f;
        private Vector3 _initialVehiclePosition;
        
        // Positions des cours de concessionnaires (extérieures)
        private readonly List<DealershipLocation> _dealershipLocations = new()
        {
            new DealershipLocation(new Vector3(-56.0f, -1109.0f, 26.4f), "Simeon's Premium Deluxe", "Cour de Simeon"),
            new DealershipLocation(new Vector3(-1255.0f, -368.0f, 36.9f), "Mosley's Auto Sales", "Cour de voitures de luxe"),
            new DealershipLocation(new Vector3(-29.0f, -1090.0f, 26.4f), "Sanders Motorcycle Works", "Cour de motos"),
            new DealershipLocation(new Vector3(1224.5f, 2728.0f, 38.0f), "Larry's RV Sales", "Concessionnaire du désert"),
            new DealershipLocation(new Vector3(-1067.0f, -1266.0f, 5.6f), "Luxury Autos", "Cour de véhicules premium"),
            new DealershipLocation(new Vector3(731.0f, -1088.0f, 22.2f), "Classic Car Collector", "Cour de voitures classiques"),
            new DealershipLocation(new Vector3(-1143.0f, -1986.0f, 13.2f), "Vespucci Cycles", "Cour de vélos et motos"),
            new DealershipLocation(new Vector3(1181.0f, 2711.0f, 38.1f), "Roadside Dealer North", "Vendeur de bord de route")
        };

        // Positions prédéfinies pour Luxury Autos avec orientation exacte
        private readonly List<VehicleSpawnData> _luxuryAutosPositions = new()
        {
            new VehicleSpawnData(new Vector3(-1078.16f, -1260.97f, 5.29f), 301.16f),
            new VehicleSpawnData(new Vector3(-1080.64f, -1258.04f, 5.12f), 299.72f),
            new VehicleSpawnData(new Vector3(-1063.53f, -1259.94f, 5.60f), 298.36f),
            new VehicleSpawnData(new Vector3(-1066.56f, -1257.53f, 5.51f), 300.53f),
            new VehicleSpawnData(new Vector3(-1068.56f, -1254.55f, 5.40f), 298.61f),
            new VehicleSpawnData(new Vector3(-1070.44f, -1251.33f, 5.27f), 300.18f),
            new VehicleSpawnData(new Vector3(-1072.33f, -1248.60f, 5.13f), 301.54f),
            new VehicleSpawnData(new Vector3(-1074.15f, -1245.36f, 4.97f), 301.50f)
        };

        public VehicleDealershipManager()
        {
            Tick += OnTick;
            Aborted += OnAborted;
            
            InitializeVehiclePrices();
            InitializeVehicleSpecs();
            CreateLemonUIMenus();
            CreateDealershipBlips();
            SpawnDealershipVehicles();
            
            Logger.Info("Vehicle Dealership Manager initialized.");
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_isEnabled) return;
            
            // Traitement des menus LemonUI
            _menuPool.Process();
            
            var now = DateTime.Now;
            if ((now - _lastUpdate).TotalMilliseconds < UPDATE_INTERVAL_MS)
                return;
            
            _lastUpdate = now;
            
            try
            {
                ProcessDealershipInteractions();
                UpdateVehicleDisplays(); // Maintenant géré individuellement par véhicule
                ProcessVehicleReplacements();
                ProcessTestDrive(); // Gérer les essais de véhicules
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in VehicleDealershipManager tick: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialise les prix des véhicules
        /// </summary>
        private void InitializeVehiclePrices()
        {
            // Voitures de sport
            _vehiclePrices[VehicleHash.Adder] = 1000000;
            _vehiclePrices[VehicleHash.Zentorno] = 725000;
            _vehiclePrices[VehicleHash.Osiris] = 1950000;
            _vehiclePrices[VehicleHash.T20] = 2200000;
            _vehiclePrices[VehicleHash.Turismo2] = 2400000;
            
            // Voitures de luxe
            _vehiclePrices[VehicleHash.Cognoscenti] = 180000;
            _vehiclePrices[VehicleHash.Exemplar] = 200000;
            _vehiclePrices[VehicleHash.Felon] = 90000;
            _vehiclePrices[VehicleHash.Jackal] = 60000;
            _vehiclePrices[VehicleHash.Oracle] = 80000;
            
            // Voitures classiques
            _vehiclePrices[VehicleHash.Banshee] = 126000;
            _vehiclePrices[VehicleHash.Bullet] = 155000;
            _vehiclePrices[VehicleHash.Cheetah] = 650000;
            _vehiclePrices[VehicleHash.EntityXF] = 795000;
            _vehiclePrices[VehicleHash.Infernus] = 440000;
            
            // SUVs
            _vehiclePrices[VehicleHash.Baller] = 90000;
            _vehiclePrices[VehicleHash.Cavalcade] = 60000;
            _vehiclePrices[VehicleHash.Dubsta] = 70000;
            _vehiclePrices[VehicleHash.FQ2] = 50000;
            _vehiclePrices[VehicleHash.Granger] = 35000;
            
            // Voitures compactes
            _vehiclePrices[VehicleHash.Blista] = 15000;
            _vehiclePrices[VehicleHash.Brioso] = 18000;
            _vehiclePrices[VehicleHash.Dilettante] = 25000;
            _vehiclePrices[VehicleHash.Issi2] = 18000;
            _vehiclePrices[VehicleHash.Panto] = 85000;
            
            // Motos
            _vehiclePrices[VehicleHash.Akuma] = 9000;
            _vehiclePrices[VehicleHash.Bati] = 15000;
            _vehiclePrices[VehicleHash.CarbonRS] = 40000;
            _vehiclePrices[VehicleHash.Double] = 12000;
            _vehiclePrices[VehicleHash.Hakuchou] = 82000;
        }

        /// <summary>
        /// Initialise les spécifications détaillées des véhicules
        /// </summary>
        private void InitializeVehicleSpecs()
        {
            // Voitures de sport
            _vehicleSpecs[VehicleHash.Adder] = new VehicleSpecs("Super Sport", 8.5f, 8.0f, 3.3f, 8.8f, 2, 
                "Hypercar légendaire avec des performances exceptionnelles", 
                "Moteur W16", "Traction intégrale", "Aérodynamique active");
            _vehicleSpecs[VehicleHash.Zentorno] = new VehicleSpecs("Super Sport", 8.7f, 8.2f, 3.5f, 8.5f, 2,
                "Design futuriste inspiré par la course", 
                "Moteur V12", "Carrosserie carbone", "Aileron actif");
            _vehicleSpecs[VehicleHash.Osiris] = new VehicleSpecs("Super Sport", 8.3f, 7.8f, 3.8f, 8.7f, 2,
                "Élégance et performance réunies",
                "Moteur V8 Biturbo", "Châssis allégé", "Freins céramique");
            _vehicleSpecs[VehicleHash.T20] = new VehicleSpecs("Super Sport", 8.6f, 8.1f, 3.6f, 8.9f, 2,
                "Technologie de pointe pour les pistes",
                "Système hybride", "DRS activable", "Suspension adaptive");
            _vehicleSpecs[VehicleHash.Turismo2] = new VehicleSpecs("Super Sport", 8.4f, 7.9f, 3.7f, 8.6f, 2,
                "Héritage racing dans un design moderne",
                "Moteur V10", "Transmission séquentielle", "Différentiel autobloquant");

            // Voitures de luxe
            _vehicleSpecs[VehicleHash.Cognoscenti] = new VehicleSpecs("Berline de Luxe", 6.2f, 5.8f, 6.5f, 6.8f, 4,
                "Confort et raffinement pour la route",
                "Intérieur cuir", "Système audio premium", "Conduite assistée");
            _vehicleSpecs[VehicleHash.Exemplar] = new VehicleSpecs("Berline de Luxe", 6.8f, 6.2f, 6.8f, 7.2f, 4,
                "Performance et élégance combinées",
                "Moteur V8", "Finitions premium", "Technologie avancée");
            _vehicleSpecs[VehicleHash.Felon] = new VehicleSpecs("Coupé de Luxe", 7.1f, 6.5f, 6.2f, 7.5f, 2,
                "Style et sophistication",
                "Design aérodynamique", "Habitacle spacieux", "Performances équilibrées");
            _vehicleSpecs[VehicleHash.Jackal] = new VehicleSpecs("Berline Executive", 6.5f, 6.0f, 6.8f, 7.0f, 4,
                "Véhicule d'affaires par excellence",
                "Confort de conduite", "Économie de carburant", "Fiabilité prouvée");
            _vehicleSpecs[VehicleHash.Oracle] = new VehicleSpecs("Berline Premium", 6.3f, 5.9f, 6.6f, 6.9f, 4,
                "Luxe accessible au quotidien",
                "Équipements complets", "Sécurité renforcée", "Maintenance réduite");

            // Motos
            _vehicleSpecs[VehicleHash.Akuma] = new VehicleSpecs("Moto Sport", 7.8f, 8.5f, 4.2f, 8.0f, 1,
                "Moto sportive accessible et performante",
                "Moteur 600cc", "Poids plume", "Agilité maximale");
            _vehicleSpecs[VehicleHash.Bati] = new VehicleSpecs("Moto Sport", 8.2f, 8.8f, 4.0f, 8.3f, 1,
                "Performance pure sur deux roues",
                "Moteur 1000cc", "Electronics avancées", "Position de course");
            _vehicleSpecs[VehicleHash.CarbonRS] = new VehicleSpecs("Moto Racing", 8.6f, 9.2f, 3.8f, 8.7f, 1,
                "Technologie de course adaptée à la route",
                "Carrosserie carbone", "Suspension réglable", "Freinage racing");
        }

        /// <summary>
        /// Crée les menus LemonUI pour l'interface du concessionnaire
        /// </summary>
        private void CreateLemonUIMenus()
        {
            // Menu principal de prévisualisation du véhicule
            _vehiclePreviewMenu = new NativeMenu("[AUTO] Concessionnaire REALIS", "Informations sur le véhicule")
            {
                Alignment = Alignment.Left
            };

            // Menu de confirmation d'achat
            _purchaseConfirmMenu = new NativeMenu("[$] Confirmer l'achat", "Finaliser votre achat")
            {
                Alignment = Alignment.Left
            };

            // Ajouter les menus au pool
            _menuPool.Add(_vehiclePreviewMenu);
            _menuPool.Add(_purchaseConfirmMenu);

            // Event handlers
            _vehiclePreviewMenu.Closed += (sender, e) => 
            {
                _isMenuActive = false;
                _currentVehicleBeingViewed = null;
            };

            _purchaseConfirmMenu.Closed += (sender, e) => 
            {
                _isMenuActive = false;
                _purchaseConfirmMenu?.Clear();
            };
        }

        /// <summary>
        /// Crée les blips sur la minimap pour les concessionnaires
        /// </summary>
        private void CreateDealershipBlips()
        {
            foreach (var dealershipLocation in _dealershipLocations)
            {
                var blip = World.CreateBlip(dealershipLocation.Position);
                
                // Utiliser l'icône SimeonCarShowroom qui est parfaite pour les concessionnaires
                blip.Sprite = BlipSprite.SimeonCarShowroom; // Icône de concessionnaire automobile
                blip.Color = BlipColor.Green;
                blip.Scale = 0.9f;
                blip.Name = dealershipLocation.Name;
                blip.IsShortRange = true;
                
                _dealershipBlips.Add(blip);
                
                Logger.Info($"Created dealership blip: {dealershipLocation.Name} at {dealershipLocation.Position}");
            }
        }

        /// <summary>
        /// Génère des véhicules dans les concessionnaires
        /// </summary>
        private void SpawnDealershipVehicles()
        {
            var random = new Random();
            var availableVehicles = _vehiclePrices.Keys.ToList();
            
            foreach (var dealershipLocation in _dealershipLocations)
            {
                // Traitement spécial pour Luxury Autos
                if (dealershipLocation.Name == "Luxury Autos")
                {
                    SpawnLuxuryAutosVehicles(availableVehicles, random);
                    continue;
                }
                
                // Spawner 3-5 véhicules par concessionnaire (autres concessionnaires)
                var vehicleCount = random.Next(3, 6);
                
                for (int i = 0; i < vehicleCount; i++)
                {
                    var vehicleHash = availableVehicles[random.Next(availableVehicles.Count)];
                    var spawnPosition = GetSpawnPositionNearLocation(dealershipLocation.Position, i);
                    
                    SpawnDealershipVehicle(vehicleHash, spawnPosition);
                }
            }
        }

        /// <summary>
        /// Spawn les véhicules de Luxury Autos aux positions exactes prédéfinies
        /// </summary>
        private void SpawnLuxuryAutosVehicles(List<VehicleHash> availableVehicles, Random random)
        {
            for (int i = 0; i < _luxuryAutosPositions.Count; i++)
            {
                var spawnData = _luxuryAutosPositions[i];
                var vehicleHash = availableVehicles[random.Next(availableVehicles.Count)];
                
                SpawnDealershipVehicleWithHeading(vehicleHash, spawnData.Position, spawnData.Heading);
            }
        }

        /// <summary>
        /// Calcule une position de spawn près d'un concessionnaire
        /// </summary>
        private Vector3 GetSpawnPositionNearLocation(Vector3 dealershipLocation, int index)
        {
            var random = new Random();
            var offsetX = (index % 3) * 8.0f - 8.0f; // -8, 0, 8
            var offsetY = (index / 3) * 6.0f;
            var offsetZ = 0.5f;
            
            return new Vector3(
                dealershipLocation.X + offsetX + random.Next(-2, 3),
                dealershipLocation.Y + offsetY + random.Next(-2, 3),
                dealershipLocation.Z + offsetZ
            );
        }

        /// <summary>
        /// Spawn un véhicule de concessionnaire
        /// </summary>
        private void SpawnDealershipVehicle(VehicleHash vehicleHash, Vector3 position)
        {
            try
            {
                var model = new Model(vehicleHash);
                if (!model.IsValid || !model.IsVehicle)
                    return;

                model.Request(5000);
                if (!model.IsLoaded)
                    return;

                var vehicle = World.CreateVehicle(model, position);
                if (vehicle == null || !vehicle.Exists())
                    return;

                // Configuration du véhicule
                vehicle.IsPersistent = true;
                vehicle.IsInvincible = true;
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle, false, false, true);
                Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, vehicle, false);
                
                // Permettre l'entrée mais immobiliser le véhicule
                vehicle.LockStatus = VehicleLockStatus.Unlocked;
                Function.Call(Hash.FREEZE_ENTITY_POSITION, vehicle, true); // Immobiliser
                
                // Couleur aléatoire
                var random = new Random();
                Function.Call(Hash.SET_VEHICLE_COLOURS, vehicle, random.Next(0, 160), random.Next(0, 160));
                
                // Ajouter à la liste des véhicules de concessionnaire
                var dealershipVehicle = new DealershipVehicle
                {
                    Vehicle = vehicle,
                    Hash = vehicleHash,
                    Price = _vehiclePrices.ContainsKey(vehicleHash) ? _vehiclePrices[vehicleHash] : 50000,
                    SpawnPosition = position,
                    IsAvailable = true
                };
                
                _dealershipVehicles.Add(dealershipVehicle);
                
                model.MarkAsNoLongerNeeded();
                
                Logger.Info($"Spawned dealership vehicle: {vehicleHash} at {position} for ${dealershipVehicle.Price}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning dealership vehicle: {ex.Message}");
            }
        }

        /// <summary>
        /// Spawn un véhicule de concessionnaire avec position et orientation spécifiques
        /// </summary>
        private void SpawnDealershipVehicleWithHeading(VehicleHash vehicleHash, Vector3 position, float heading)
        {
            try
            {
                var model = new Model(vehicleHash);
                if (!model.IsValid || !model.IsVehicle)
                    return;

                model.Request(5000);
                if (!model.IsLoaded)
                    return;

                var vehicle = World.CreateVehicle(model, position, heading); // Utiliser la surcharge avec heading
                if (vehicle == null || !vehicle.Exists())
                    return;

                // Configuration du véhicule
                vehicle.IsPersistent = true;
                vehicle.IsInvincible = true;
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle, false, false, true);
                Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, vehicle, false);
                
                // Permettre l'entrée mais immobiliser le véhicule
                vehicle.LockStatus = VehicleLockStatus.Unlocked;
                Function.Call(Hash.FREEZE_ENTITY_POSITION, vehicle, true); // Immobiliser
                
                // Couleur aléatoire
                var random = new Random();
                Function.Call(Hash.SET_VEHICLE_COLOURS, vehicle, random.Next(0, 160), random.Next(0, 160));
                
                // Ajouter à la liste des véhicules de concessionnaire
                var dealershipVehicle = new DealershipVehicle
                {
                    Vehicle = vehicle,
                    Hash = vehicleHash,
                    Price = _vehiclePrices.ContainsKey(vehicleHash) ? _vehiclePrices[vehicleHash] : 50000,
                    SpawnPosition = position,
                    IsAvailable = true
                };
                
                _dealershipVehicles.Add(dealershipVehicle);
                
                model.MarkAsNoLongerNeeded();
                
                Logger.Info($"Spawned dealership vehicle: {vehicleHash} at {position} with heading {heading} for ${dealershipVehicle.Price}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning dealership vehicle with heading: {ex.Message}");
            }
        }

        /// <summary>
        /// Traite les interactions avec les véhicules de concessionnaire
        /// </summary>
        private void ProcessDealershipInteractions()
        {
            var player = Game.Player.Character;
            var playerVehicle = player.CurrentVehicle;
            
            // Vérifier si le joueur est dans un véhicule de concessionnaire (sauf pendant un essai)
            if (playerVehicle != null && playerVehicle.Exists() && !_isTestDriving)
            {
                var dealershipVehicle = _dealershipVehicles.FirstOrDefault(dv => 
                    dv.Vehicle != null && dv.Vehicle.Handle == playerVehicle.Handle && dv.IsAvailable);
                
                if (dealershipVehicle?.Vehicle != null)
                {
                    // Si le menu n'est pas actif et qu'un véhicule différent est sélectionné
                    if (!_isMenuActive || _currentVehicleBeingViewed != dealershipVehicle)
                    {
                        ShowVehiclePreviewMenu(dealershipVehicle);
                    }
                    
                    // Afficher également l'ancien prompt en petit
                    ShowSimplePurchasePrompt(dealershipVehicle);
                }
            }
            else if (_isMenuActive && _currentVehicleBeingViewed != null)
            {
                // Le joueur est sorti du véhicule, fermer le menu
                if (_vehiclePreviewMenu != null)
                    _vehiclePreviewMenu.Visible = false;
                _isMenuActive = false;
                _currentVehicleBeingViewed = null;
            }
        }

        /// <summary>
        /// Affiche le menu de prévisualisation du véhicule avec LemonUI
        /// </summary>
        private void ShowVehiclePreviewMenu(DealershipVehicle dealershipVehicle)
        {
            if (dealershipVehicle.Vehicle == null || _vehiclePreviewMenu == null) return;
            
            _currentVehicleBeingViewed = dealershipVehicle;
            _vehiclePreviewMenu.Clear();
            
            var vehicle = dealershipVehicle.Vehicle;
            var price = dealershipVehicle.Price;
            var vehicleName = vehicle.DisplayName;
            var playerMoney = Game.Player.Money;
            var canAfford = playerMoney >= price;
            
            // Obtenir les spécifications du véhicule
            var specs = _vehicleSpecs.ContainsKey(dealershipVehicle.Hash) 
                ? _vehicleSpecs[dealershipVehicle.Hash] 
                : new VehicleSpecs("Véhicule", 5.0f, 5.0f, 5.0f, 5.0f, 2, "Informations non disponibles");
            
            // Titre avec le nom du véhicule
            var titleItem = new NativeSeparatorItem($"[AUTO] {vehicleName}")
            {
                Description = specs.Description
            };
            _vehiclePreviewMenu.Add(titleItem);
            
            // Prix
            var priceColor = canAfford ? "~g~" : "~r~";
            var priceItem = new NativeItem($"[$] Prix: {priceColor}${price:N0}")
            {
                Description = canAfford ? "Vous pouvez acheter ce véhicule" : "Vous n'avez pas assez d'argent"
            };
            _vehiclePreviewMenu.Add(priceItem);
            
            // Catégorie
            var categoryItem = new NativeItem($"[INFO] Categorie: {specs.Category}")
            {
                Description = "Type de véhicule"
            };
            _vehiclePreviewMenu.Add(categoryItem);
            
            // Séparateur pour les performances
            _vehiclePreviewMenu.Add(new NativeSeparatorItem("[STATS] Performances"));
            
            // Performances avec barres visuelles
            AddPerformanceItem("[SPEED] Vitesse Max", specs.TopSpeed);
            AddPerformanceItem("[ACCEL] Acceleration", specs.Acceleration);
            AddPerformanceItem("[BRAKE] Freinage", specs.Braking);
            AddPerformanceItem("[HANDLE] Maniabilite", specs.Handling);
            
            // Informations supplémentaires
            _vehiclePreviewMenu.Add(new NativeSeparatorItem("[INFO] Informations"));
            
            var seatsItem = new NativeItem($"[SEATS] Places: {specs.Seats}")
            {
                Description = "Nombre de places disponibles"
            };
            _vehiclePreviewMenu.Add(seatsItem);
            
            // Équipements
            if (specs.Features != null && specs.Features.Length > 0)
            {
                _vehiclePreviewMenu.Add(new NativeSeparatorItem("[EQUIP] Equipements"));
                foreach (var feature in specs.Features)
                {
                    var featureItem = new NativeItem($"- {feature}")
                    {
                        Description = "Equipement inclus"
                    };
                    _vehiclePreviewMenu.Add(featureItem);
                }
            }
            
            // Séparateur pour les actions
            _vehiclePreviewMenu.Add(new NativeSeparatorItem("[ACTIONS] Actions"));
            
            // Bouton d'essai
            var testDriveItem = new NativeItem($"[TEST] Essayer le vehicule ({TEST_DRIVE_DURATION_SECONDS}s)")
            {
                Description = $"Faire un essai de {TEST_DRIVE_DURATION_SECONDS} secondes avec ce véhicule",
                Enabled = !_isTestDriving
            };
            testDriveItem.Activated += (sender, e) => StartTestDrive(dealershipVehicle);
            _vehiclePreviewMenu.Add(testDriveItem);
            
            // Bouton d'achat
            var purchaseItem = new NativeItem(canAfford ? "[BUY] Acheter ce vehicule" : "[X] Fonds insuffisants")
            {
                Description = canAfford ? $"Acheter {vehicleName} pour ${price:N0}" : $"Il vous manque ${(price - playerMoney):N0}",
                Enabled = canAfford
            };
            
            if (canAfford)
            {
                purchaseItem.Activated += (sender, e) => ShowPurchaseConfirmation(dealershipVehicle);
            }
            
            _vehiclePreviewMenu.Add(purchaseItem);
            
            // Bouton de fermeture
            var closeItem = new NativeItem("[X] Fermer")
            {
                Description = "Fermer ce menu"
            };
            closeItem.Activated += (sender, e) => 
            {
                if (_vehiclePreviewMenu != null)
                    _vehiclePreviewMenu.Visible = false;
                _isMenuActive = false;
                _currentVehicleBeingViewed = null;
            };
            _vehiclePreviewMenu.Add(closeItem);
            
            // Afficher le menu
            _vehiclePreviewMenu.Visible = true;
            _isMenuActive = true;
        }
        
        /// <summary>
        /// Ajoute un élément de performance avec un score numérique
        /// </summary>
        private void AddPerformanceItem(string name, float value)
        {
            if (_vehiclePreviewMenu == null) return;
            
            var normalizedValue = Math.Min(10, Math.Max(0, value));
            
            var performanceItem = new NativeItem($"{name}: {normalizedValue:F1}/10")
            {
                Description = $"Niveau de performance: {normalizedValue:F1}/10"
            };
            _vehiclePreviewMenu.Add(performanceItem);
        }
        
        /// <summary>
        /// Affiche un prompt simple en complément du menu
        /// </summary>
        private void ShowSimplePurchasePrompt(DealershipVehicle dealershipVehicle)
        {
            if (dealershipVehicle.Vehicle == null) return;
            
            var vehicleName = dealershipVehicle.Vehicle.DisplayName;
            var price = dealershipVehicle.Price;
            var canAfford = Game.Player.Money >= price;
            var color = canAfford ? "~g~" : "~r~";
            
            var message = $"{color}{vehicleName} - ${price:N0}\n~w~Menu detaille: ~b~Retour Arriere~w~ | Achat rapide: ~g~E";
            
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, message);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, -1);
            
            // Achat rapide avec E
            if (Game.IsKeyPressed(Keys.E) && canAfford)
            {
                ShowPurchaseConfirmation(dealershipVehicle);
            }
        }
        
        /// <summary>
        /// Affiche le menu de confirmation d'achat
        /// </summary>
        private void ShowPurchaseConfirmation(DealershipVehicle dealershipVehicle)
        {
            if (dealershipVehicle.Vehicle == null || _purchaseConfirmMenu == null) return;
            
            _purchaseConfirmMenu.Clear();
            
            var vehicle = dealershipVehicle.Vehicle;
            var price = dealershipVehicle.Price;
            var vehicleName = vehicle.DisplayName;
            var playerMoney = Game.Player.Money;
            
            // Titre
            var titleItem = new NativeSeparatorItem($"[$] Confirmer l'achat")
            {
                Description = $"Achat de {vehicleName}"
            };
            _purchaseConfirmMenu.Add(titleItem);
            
            // Récapitulatif
            var summaryItem = new NativeItem($"[AUTO] Vehicule: {vehicleName}")
            {
                Description = "Vehicule selectionne"
            };
            _purchaseConfirmMenu.Add(summaryItem);
            
            var priceItem = new NativeItem($"[$] Prix: ${price:N0}")
            {
                Description = "Prix d'achat"
            };
            _purchaseConfirmMenu.Add(priceItem);
            
            var moneyAfterItem = new NativeItem($"[$] Argent restant: ${(playerMoney - price):N0}")
            {
                Description = "Votre argent apres l'achat"
            };
            _purchaseConfirmMenu.Add(moneyAfterItem);
            
            // Actions
            _purchaseConfirmMenu.Add(new NativeSeparatorItem("[ACTIONS] Actions"));
            
            // Confirmer l'achat
            var confirmItem = new NativeItem("[OK] Confirmer l'achat")
            {
                Description = $"Acheter definitivement {vehicleName}"
            };
            confirmItem.Activated += (sender, e) => 
            {
                PurchaseVehicle(dealershipVehicle);
                if (_purchaseConfirmMenu != null)
                    _purchaseConfirmMenu.Visible = false;
                if (_vehiclePreviewMenu != null)
                    _vehiclePreviewMenu.Visible = false;
                _isMenuActive = false;
                _currentVehicleBeingViewed = null;
            };
            _purchaseConfirmMenu.Add(confirmItem);
            
            // Annuler
            var cancelItem = new NativeItem("[X] Annuler")
            {
                Description = "Annuler cet achat"
            };
            cancelItem.Activated += (sender, e) => 
            {
                if (_purchaseConfirmMenu != null)
                    _purchaseConfirmMenu.Visible = false;
                if (_vehiclePreviewMenu != null)
                    _vehiclePreviewMenu.Visible = true; // Retour au menu principal
            };
            _purchaseConfirmMenu.Add(cancelItem);
            
            // Fermer le menu principal et afficher la confirmation
            if (_vehiclePreviewMenu != null)
                _vehiclePreviewMenu.Visible = false;
            _purchaseConfirmMenu.Visible = true;
        }

        /// <summary>
        /// Achète un véhicule
        /// </summary>
        private void PurchaseVehicle(DealershipVehicle dealershipVehicle)
        {
            var player = Game.Player;
            var price = dealershipVehicle.Price;
            var vehicle = dealershipVehicle.Vehicle;
            
            if (vehicle == null)
            {
                Logger.Error("Cannot purchase vehicle: vehicle is null");
                return;
            }
            
            if (player.Money < price)
            {
                Notification.PostTicker("~r~Vous n'avez pas assez d'argent pour acheter ce véhicule!", false, true);
                return;
            }
            
            // Déduire l'argent
            player.Money -= price;
            
            // Transférer la propriété du véhicule
            vehicle.IsPersistent = false;
            vehicle.IsInvincible = false;
            vehicle.LockStatus = VehicleLockStatus.Unlocked;
            Function.Call(Hash.FREEZE_ENTITY_POSITION, vehicle, false); // Permettre le mouvement
            
            // Marquer comme vendu
            dealershipVehicle.IsAvailable = false;
            
            // Notification de succès
            var vehicleName = vehicle.DisplayName;
            Notification.PostTicker($"~g~Félicitations ! Vous avez acheté {vehicleName} pour ${price:N0}!", false, true);
            
            // Marquer pour remplacement
            dealershipVehicle.SpawnTime = DateTime.Now.AddSeconds(5);
            
            Logger.Info($"Player purchased {vehicleName} for ${price}");
        }

        /// <summary>
        /// Traite le remplacement des véhicules vendus
        /// </summary>
        private void ProcessVehicleReplacements()
        {
            var vehiclesToReplace = _dealershipVehicles
                .Where(dv => !dv.IsAvailable && DateTime.Now >= dv.SpawnTime)
                .ToList();
            
            foreach (var vehicle in vehiclesToReplace)
            {
                ReplaceVehicle(vehicle);
            }
        }

        /// <summary>
        /// Remplace un véhicule vendu par un nouveau
        /// </summary>
        private void ReplaceVehicle(DealershipVehicle soldVehicle)
        {
            try
            {
                // Nettoyer le cache d'affichage pour ce véhicule
                if (soldVehicle.Vehicle != null)
                {
                    _vehicleDisplayCache.Remove(soldVehicle.Vehicle.Handle);
                }
                
                // Supprimer l'ancien véhicule de la liste
                _dealershipVehicles.Remove(soldVehicle);
                
                // Spawner un nouveau véhicule
                var random = new Random();
                var availableVehicles = _vehiclePrices.Keys.ToList();
                var newVehicleHash = availableVehicles[random.Next(availableVehicles.Count)];
                
                SpawnDealershipVehicle(newVehicleHash, soldVehicle.SpawnPosition);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error replacing vehicle: {ex.Message}");
            }
        }

        /// <summary>
        /// Met à jour l'affichage des véhicules (prix au-dessus) - Appelé à chaque frame
        /// </summary>
        private void UpdateVehicleDisplays()
        {
            var player = Game.Player.Character;
            var now = DateTime.Now;
            
            foreach (var dealershipVehicle in _dealershipVehicles.Where(dv => dv.Vehicle != null && dv.Vehicle.Exists() && dv.IsAvailable))
            {
                if (dealershipVehicle.Vehicle == null) continue;
                
                var distance = Vector3.Distance(player.Position, dealershipVehicle.Vehicle.Position);
                
                // Afficher le prix seulement si le joueur est proche
                if (distance <= 15f)
                {
                    var vehicleHandle = dealershipVehicle.Vehicle.Handle;
                    
                    // Mettre à jour le cache seulement si nécessaire (évite les recalculs)
                    if (!_vehicleDisplayCache.ContainsKey(vehicleHandle) || 
                        (now - _vehicleDisplayCache[vehicleHandle].LastUpdate).TotalMilliseconds >= TEXT_UPDATE_INTERVAL_MS)
                    {
                        UpdateVehicleDisplayCache(dealershipVehicle, vehicleHandle);
                    }
                    
                    // Afficher le texte depuis le cache à chaque frame (pas de clignotement)
                    DrawCachedVehiclePrice(vehicleHandle);
                }
            }
        }

        /// <summary>
        /// Met à jour le cache d'affichage pour un véhicule
        /// </summary>
        private void UpdateVehicleDisplayCache(DealershipVehicle dealershipVehicle, int vehicleHandle)
        {
            try
            {
                if (dealershipVehicle.Vehicle == null || !dealershipVehicle.Vehicle.Exists()) return;
                
                var vehicle = dealershipVehicle.Vehicle;
                var vehicleName = Game.GetLocalizedString(vehicle.DisplayName);
                var price = dealershipVehicle.Price;
                
                // Position au-dessus du véhicule
                var worldPos = vehicle.Position + new Vector3(0, 0, 2.5f);
                
                var playerMoney = Game.Player.Money;
                var canAfford = playerMoney >= price;
                var displayText = canAfford 
                    ? $"~w~{vehicleName}~n~${price:N0}" 
                    : $"~r~{vehicleName}~n~${price:N0}";
                
                // Mettre à jour ou créer l'entrée dans le cache
                if (!_vehicleDisplayCache.ContainsKey(vehicleHandle))
                {
                    _vehicleDisplayCache[vehicleHandle] = new VehicleDisplayInfo();
                }
                
                _vehicleDisplayCache[vehicleHandle].DisplayText = displayText;
                _vehicleDisplayCache[vehicleHandle].Position = worldPos;
                _vehicleDisplayCache[vehicleHandle].LastUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating vehicle display cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Dessine le prix d'un véhicule depuis le cache (stable, pas de clignotement)
        /// </summary>
        private void DrawCachedVehiclePrice(int vehicleHandle)
        {
            try
            {
                if (!_vehicleDisplayCache.ContainsKey(vehicleHandle)) return;
                
                var displayInfo = _vehicleDisplayCache[vehicleHandle];
                
                // Configuration du texte pour éviter le clignotement
                Function.Call(Hash.SET_TEXT_SCALE, 0.45f, 0.45f);
                Function.Call(Hash.SET_TEXT_FONT, 4);
                Function.Call(Hash.SET_TEXT_CENTRE, true);
                Function.Call(Hash.SET_TEXT_DROPSHADOW, 2, 0, 0, 0, 200);
                Function.Call(Hash.SET_TEXT_OUTLINE);
                
                // Affichage stable du texte 3D depuis le cache
                Function.Call(Hash.SET_DRAW_ORIGIN, displayInfo.Position.X, displayInfo.Position.Y, displayInfo.Position.Z, 0);
                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, displayInfo.DisplayText);
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.0f, 0.0f, 0);
                Function.Call(Hash.CLEAR_DRAW_ORIGIN);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error drawing cached vehicle price: {ex.Message}");
            }
        }

        /// <summary>
        /// Dessine le prix au-dessus du véhicule
        /// </summary>
        private void DrawVehiclePrice(DealershipVehicle dealershipVehicle)
        {
            var vehicle = dealershipVehicle.Vehicle;
            if (vehicle == null) return;
            
            var price = dealershipVehicle.Price;
            var vehicleName = vehicle.DisplayName;
            
            // Position au-dessus du véhicule
            var worldPos = vehicle.Position + new Vector3(0, 0, 2.5f);
            
            // Couleur selon si le joueur peut se le permettre
            var canAfford = Game.Player.Money >= price;
            
            // Texte en blanc par défaut, rouge seulement si pas assez d'argent
            var priceText = canAfford 
                ? $"~w~{vehicleName}~n~${price:N0}" 
                : $"~r~{vehicleName}~n~${price:N0}";
            
            // Configuration du texte pour éviter le clignotement
            Function.Call(Hash.SET_TEXT_SCALE, 0.45f, 0.45f);
            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_CENTRE, true);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 2, 0, 0, 0, 200);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            
            // Affichage stable du texte 3D
            Function.Call(Hash.SET_DRAW_ORIGIN, worldPos.X, worldPos.Y, worldPos.Z, 0);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, priceText);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.0f, 0.0f, 0);
            Function.Call(Hash.CLEAR_DRAW_ORIGIN);
        }

        /// <summary>
        /// Démarre un essai de véhicule
        /// </summary>
        private void StartTestDrive(DealershipVehicle dealershipVehicle)
        {
            if (_isTestDriving || dealershipVehicle?.Vehicle == null) return;
            
            try
            {
                _currentTestDriveVehicle = dealershipVehicle;
                _testDriveStartTime = DateTime.Now;
                _isTestDriving = true;
                
                // Enregistrer l'état initial du véhicule
                _initialVehicleHealth = dealershipVehicle.Vehicle.Health;
                _initialVehiclePosition = dealershipVehicle.Vehicle.Position;
                
                // Débloquer le véhicule pour l'essai
                Function.Call(Hash.FREEZE_ENTITY_POSITION, dealershipVehicle.Vehicle, false);
                
                // Fermer le menu
                if (_vehiclePreviewMenu != null)
                    _vehiclePreviewMenu.Visible = false;
                _isMenuActive = false;
                _currentVehicleBeingViewed = null;
                
                // Message d'information
                Function.Call(Hash.BEGIN_TEXT_COMMAND_PRINT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, 
                    $"~g~Essai commencé!~w~\nVous avez {TEST_DRIVE_DURATION_SECONDS} secondes pour essayer ce véhicule.");
                Function.Call(Hash.END_TEXT_COMMAND_PRINT, 3000, true);
                
                Logger.Info($"Test drive started for vehicle: {dealershipVehicle.Hash}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting test drive: {ex.Message}");
                _isTestDriving = false;
                _currentTestDriveVehicle = null;
            }
        }

        /// <summary>
        /// Traite les essais de véhicules en cours
        /// </summary>
        private void ProcessTestDrive()
        {
            if (!_isTestDriving || _currentTestDriveVehicle?.Vehicle == null) return;
            
            try
            {
                var elapsed = (DateTime.Now - _testDriveStartTime).TotalSeconds;
                var timeRemaining = TEST_DRIVE_DURATION_SECONDS - elapsed;
                
                // Afficher le timer à l'écran
                if (timeRemaining > 0)
                {
                    var minutes = (int)(timeRemaining / 60);
                    var seconds = (int)(timeRemaining % 60);
                    var timerText = $"~y~ESSAI: ~w~{minutes:00}:{seconds:00}";
                    
                    Function.Call(Hash.SET_TEXT_SCALE, 0.6f, 0.6f);
                    Function.Call(Hash.SET_TEXT_FONT, 4);
                    Function.Call(Hash.SET_TEXT_CENTRE, false);
                    Function.Call(Hash.SET_TEXT_DROPSHADOW, 2, 0, 0, 0, 255);
                    Function.Call(Hash.SET_TEXT_OUTLINE);
                    Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
                    
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, timerText);
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.02f, 0.02f, 0);
                    
                    // Avertissement quand il reste 10 secondes
                    if (timeRemaining <= 10 && timeRemaining > 9)
                    {
                        Function.Call(Hash.BEGIN_TEXT_COMMAND_PRINT, "STRING");
                        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, 
                            "~r~Attention!~w~\nL'essai se termine dans 10 secondes!");
                        Function.Call(Hash.END_TEXT_COMMAND_PRINT, 2000, true);
                    }
                }
                else
                {
                    // Temps écoulé, terminer l'essai
                    EndTestDrive();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing test drive: {ex.Message}");
                EndTestDrive();
            }
        }

        /// <summary>
        /// Termine l'essai et téléporte le véhicule et le joueur à la position d'origine
        /// </summary>
        private void EndTestDrive()
        {
            if (!_isTestDriving || _currentTestDriveVehicle?.Vehicle == null) return;
            
            try
            {
                var vehicle = _currentTestDriveVehicle.Vehicle;
                var originalPosition = _currentTestDriveVehicle.SpawnPosition;
                var player = Game.Player.Character;
                
                // Vérifier si le joueur est dans le véhicule d'essai
                bool playerInTestVehicle = player.CurrentVehicle != null && 
                                         player.CurrentVehicle.Handle == vehicle.Handle;
                
                // Calculer et facturer les dégâts
                var damageCharges = CalculateAndChargeDamages(vehicle);
                
                // Téléporter le véhicule à sa position d'origine
                vehicle.Position = originalPosition;
                
                // Si le véhicule est de Luxury Autos, restaurer l'orientation exacte
                if (IsLuxuryAutosVehicle(vehicle))
                {
                    var spawnData = GetLuxuryAutosSpawnData(originalPosition);
                    if (spawnData != null)
                    {
                        vehicle.Heading = spawnData.Heading;
                    }
                }
                
                // Téléporter le joueur avec le véhicule s'il était dedans
                if (playerInTestVehicle)
                {
                    player.Position = originalPosition + new Vector3(0, 0, 1);
                }
                
                // Réparer complètement le véhicule
                RepairVehicleCompletely(vehicle);
                
                // Remettre le véhicule en place et l'immobiliser
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicle);
                Function.Call(Hash.FREEZE_ENTITY_POSITION, vehicle, true);
                
                // Message de fin d'essai avec informations sur les dégâts
                var endMessage = damageCharges > 0 
                    ? $"~r~Essai terminé!~w~\nFacturation dégâts: ~r~${damageCharges:N0}~w~\nVéhicule réparé et remis à sa place."
                    : "~r~Essai terminé!~w~\nAucun dégât - Le véhicule a été remis à sa place.";
                
                Function.Call(Hash.BEGIN_TEXT_COMMAND_PRINT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, endMessage);
                Function.Call(Hash.END_TEXT_COMMAND_PRINT, 4000, true);
                
                Logger.Info($"Test drive ended for vehicle: {_currentTestDriveVehicle.Hash}, damage charges: ${damageCharges}");
                
                // Réinitialiser l'état d'essai
                _isTestDriving = false;
                _currentTestDriveVehicle = null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ending test drive: {ex.Message}");
                _isTestDriving = false;
                _currentTestDriveVehicle = null;
            }
        }

        /// <summary>
        /// Calcule les dégâts et facture le joueur en conséquence
        /// </summary>
        private int CalculateAndChargeDamages(Vehicle vehicle)
        {
            try
            {
                var currentHealth = vehicle.Health;
                var healthLoss = Math.Max(0, _initialVehicleHealth - currentHealth);
                var healthPercentage = currentHealth / _initialVehicleHealth;
                
                // Vérifier si le véhicule est dans l'eau
                bool inWater = Function.Call<bool>(Hash.IS_ENTITY_IN_WATER, vehicle);
                
                // Calculer le facteur de dégâts (0 = pas de dégâts, 1 = destruction totale)
                var damageFactor = 1.0f - healthPercentage;
                
                // Bonus de dégâts si le véhicule est dans l'eau
                if (inWater)
                {
                    damageFactor += 0.3f; // 30% de dégâts supplémentaires pour l'eau
                    Logger.Info("Vehicle was found in water during test drive");
                }
                
                // Calculer le coût des dégâts (max 50% du prix du véhicule)
                var vehiclePrice = _currentTestDriveVehicle?.Price ?? 50000;
                var maxDamageCharge = (int)(vehiclePrice * 0.5f); // Maximum 50% du prix
                var damageCharge = (int)(maxDamageCharge * Math.Min(1.0f, damageFactor));
                
                // Facturer le joueur si des dégâts ont été causés
                if (damageCharge > 0)
                {
                    var playerMoney = Game.Player.Money;
                    if (playerMoney >= damageCharge)
                    {
                        Game.Player.Money -= damageCharge;
                        Logger.Info($"Charged player ${damageCharge} for vehicle damage. Health: {currentHealth}/{_initialVehicleHealth}, In water: {inWater}");
                    }
                    else
                    {
                        // Si le joueur n'a pas assez d'argent, prendre tout ce qu'il a
                        Game.Player.Money = 0;
                        damageCharge = playerMoney;
                        Logger.Info($"Player didn't have enough money. Charged ${damageCharge} (all available money)");
                    }
                }
                
                return damageCharge;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating damage charges: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Répare complètement un véhicule
        /// </summary>
        private void RepairVehicleCompletely(Vehicle vehicle)
        {
            try
            {
                // Réparer la santé du véhicule
                vehicle.Health = vehicle.MaxHealth;
                vehicle.EngineHealth = 1000.0f;
                vehicle.PetrolTankHealth = 1000.0f;
                
                // Réparer la carrosserie
                vehicle.Repair();
                
                // Nettoyer le véhicule
                vehicle.Wash();
                
                // Réparer les pneus
                Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, vehicle, true);
                Function.Call(Hash.SET_VEHICLE_TYRE_FIXED, vehicle, 0);
                Function.Call(Hash.SET_VEHICLE_TYRE_FIXED, vehicle, 1);
                Function.Call(Hash.SET_VEHICLE_TYRE_FIXED, vehicle, 2);
                Function.Call(Hash.SET_VEHICLE_TYRE_FIXED, vehicle, 3);
                Function.Call(Hash.SET_VEHICLE_TYRE_FIXED, vehicle, 4);
                Function.Call(Hash.SET_VEHICLE_TYRE_FIXED, vehicle, 5);
                
                // Réparer les vitres
                Function.Call(Hash.FIX_VEHICLE_WINDOW, vehicle, 0);
                Function.Call(Hash.FIX_VEHICLE_WINDOW, vehicle, 1);
                Function.Call(Hash.FIX_VEHICLE_WINDOW, vehicle, 2);
                Function.Call(Hash.FIX_VEHICLE_WINDOW, vehicle, 3);
                Function.Call(Hash.FIX_VEHICLE_WINDOW, vehicle, 4);
                Function.Call(Hash.FIX_VEHICLE_WINDOW, vehicle, 5);
                Function.Call(Hash.FIX_VEHICLE_WINDOW, vehicle, 6);
                Function.Call(Hash.FIX_VEHICLE_WINDOW, vehicle, 7);
                
                // Éteindre le moteur et redémarrer pour réinitialiser l'état
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle, false, false, true);
                
                Logger.Info("Vehicle completely repaired and restored");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error repairing vehicle: {ex.Message}");
            }
        }

        /// <summary>
        /// Vérifie si un véhicule appartient au concessionnaire Luxury Autos
        /// </summary>
        private bool IsLuxuryAutosVehicle(Vehicle vehicle)
        {
            var luxuryAutosLocation = _dealershipLocations.FirstOrDefault(d => d.Name == "Luxury Autos");
            if (luxuryAutosLocation == null) return false;
            
            return vehicle.Position.DistanceTo(luxuryAutosLocation.Position) < 50.0f;
        }

        /// <summary>
        /// Récupère les données de spawn pour une position de Luxury Autos
        /// </summary>
        private VehicleSpawnData? GetLuxuryAutosSpawnData(Vector3 position)
        {
            return _luxuryAutosPositions.FirstOrDefault(spawn => 
                spawn.Position.DistanceTo(position) < 2.0f);
        }

        /// <summary>
        /// Active ou désactive le système
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            
            // Gérer la visibilité des blips
            foreach (var blip in _dealershipBlips)
            {
                if (blip != null && blip.Exists())
                {
                    blip.Alpha = enabled ? 255 : 0;
                }
            }
            
            Logger.Info($"Vehicle Dealership Manager {(enabled ? "enabled" : "disabled")}");
        }

        private void OnAborted(object sender, EventArgs e)
        {
            // Terminer tout essai en cours
            if (_isTestDriving)
            {
                _isTestDriving = false;
                _currentTestDriveVehicle = null;
            }
            
            // Nettoyer les véhicules persistants
            foreach (var dealershipVehicle in _dealershipVehicles)
            {
                if (dealershipVehicle.Vehicle != null && dealershipVehicle.Vehicle.Exists())
                {
                    dealershipVehicle.Vehicle.Delete();
                }
            }
            
            // Nettoyer les blips
            foreach (var blip in _dealershipBlips)
            {
                if (blip != null && blip.Exists())
                {
                    blip.Delete();
                }
            }
            
            // Nettoyer les menus LemonUI
            try
            {
                if (_vehiclePreviewMenu != null)
                {
                    _vehiclePreviewMenu.Visible = false;
                    _vehiclePreviewMenu.Clear();
                }
                if (_purchaseConfirmMenu != null)
                {
                    _purchaseConfirmMenu.Visible = false;
                    _purchaseConfirmMenu.Clear();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error cleaning up menus: {ex.Message}");
            }
            
            _dealershipVehicles.Clear();
            _dealershipBlips.Clear();
            _isMenuActive = false;
            _currentVehicleBeingViewed = null;
            Logger.Info("Vehicle Dealership Manager cleaned up.");
        }
    }

    /// <summary>
    /// Représente un véhicule de concessionnaire
    /// </summary>
    public class DealershipVehicle
    {
        public Vehicle? Vehicle { get; set; }
        public VehicleHash Hash { get; set; }
        public int Price { get; set; }
        public Vector3 SpawnPosition { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime SpawnTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Cache des informations d'affichage pour éviter le clignotement
    /// </summary>
    public class VehicleDisplayInfo
    {
        public string DisplayText { get; set; } = "";
        public Vector3 Position { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Représente un emplacement de concessionnaire
    /// </summary>
    public class DealershipLocation
    {
        public Vector3 Position { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public DealershipLocation(Vector3 position, string name, string description)
        {
            Position = position;
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Spécifications détaillées d'un véhicule
    /// </summary>
    public class VehicleSpecs
    {
        public string Category { get; set; }
        public float TopSpeed { get; set; }
        public float Acceleration { get; set; }
        public float Braking { get; set; }
        public float Handling { get; set; }
        public int Seats { get; set; }
        public string Description { get; set; }
        public string[] Features { get; set; }

        public VehicleSpecs(string category, float topSpeed, float acceleration, float braking, float handling, int seats, string description, params string[] features)
        {
            Category = category;
            TopSpeed = topSpeed;
            Acceleration = acceleration;
            Braking = braking;
            Handling = handling;
            Seats = seats;
            Description = description;
            Features = features ?? new string[0];
        }
    }

    /// <summary>
    /// Données de spawn d'un véhicule avec position et orientation
    /// </summary>
    public class VehicleSpawnData
    {
        public Vector3 Position { get; set; }
        public float Heading { get; set; }

        public VehicleSpawnData(Vector3 position, float heading)
        {
            Position = position;
            Heading = heading;
        }
    }
}