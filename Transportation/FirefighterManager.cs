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
    /// Types d'incendies disponibles
    /// </summary>
    public enum FireType
    {
        Building,     // Bâtiments/Intérieurs
        Vehicle,      // Véhicules en feu
        Vegetation,   // Forêts, arbres, buissons
        Industrial,   // Stations-service, usines
        Parking,      // Parkings, zones ouvertes
        Residential   // Maisons, appartements
    }

    /// <summary>
    /// Système de pompier avec missions d'extinction d'incendies
    /// </summary>
    public class FirefighterManager : Script
    {
        #region Fields
        
        private ObjectPool _menuPool;
        private NativeMenu _firefighterMenu = null!;
        private NativeItem _startMissionItem = null!;
        private NativeItem _endShiftItem = null!;
        
        private Vehicle? _currentFireTruck;
        private bool _isOnShift;
        private bool _isOnMission;
        private List<FireLocation> _fireLocations = new();
        private FireLocation? _currentFire;
        private Vector3 _jobLocation = new Vector3(1193.5f, -1473.0f, 34.7f); // Station de pompiers Davis
        private Blip? _jobBlip;
        private Blip? _missionBlip;
        private Blip? _truckBlip;
        
        // Mission management
        private DateTime _missionStartTime;
        private bool _fireExtinguished;
        private int _fireIntensity = 100; // 0-100, diminue quand on éteint
        private DateTime _lastExtinguishTime;
        
        // Économie
        private int _totalEarnings;
        private int _missionsCompleted;
        private const int BASE_REWARD = 500; // Récompense de base par mission
        private const int TIME_BONUS = 50; // Bonus pour rapidité
        
        // Inventaire
        private bool _hasFireExtinguisher = false;
        
        // Particules de feu
        private int _fireParticleHandle = -1;
        private bool _fireParticleActive = false;
        
        #endregion
        
        #region Initialization
        
        public FirefighterManager()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            
            _menuPool = new ObjectPool();
            InitializeMenu();
            InitializeFireLocations();
            CreateJobLocationBlip();
            
            Logger.Info("Firefighter Manager initialized.");
        }
        
        private void InitializeMenu()
        {
            _firefighterMenu = new NativeMenu("Service Pompier", "Gestion des missions");
            _menuPool.Add(_firefighterMenu);
            
            _startMissionItem = new NativeItem("Commencer une mission", "Démarrer une nouvelle mission d'extinction d'incendie");
            _endShiftItem = new NativeItem("Terminer le service", "Finir votre service de pompier");
            
            _firefighterMenu.Add(_startMissionItem);
            _firefighterMenu.Add(_endShiftItem);
            
            _startMissionItem.Activated += OnStartMission;
            _endShiftItem.Activated += OnEndShift;
        }
        
        private void InitializeFireLocations()
        {
            _fireLocations = new List<FireLocation>
            {
                // === INTÉRIEURS ACCESSIBLES ===
                new FireLocation("Incendie Commissariat LSPD", new Vector3(436.1f, -982.1f, 30.7f), "Incendie dans le commissariat de Mission Row", FireType.Building),
                new FireLocation("Incendie Hôpital Central", new Vector3(294.9f, -1448.1f, 29.97f), "Feu dans le hall de l'hôpital", FireType.Building),
                new FireLocation("Incendie Magasin Ammunation", new Vector3(252.8f, -50.4f, 69.9f), "Incendie dans le magasin d'armes", FireType.Building),
                new FireLocation("Incendie Garage LS Customs", new Vector3(-362.5f, -132.3f, 38.7f), "Feu dans le garage automobile", FireType.Building),
                new FireLocation("Incendie Strip Club Vanilla", new Vector3(127.8f, -1307.5f, 29.2f), "Incendie dans le club", FireType.Building),
                new FireLocation("Incendie Restaurant Cluckin Bell", new Vector3(-146.4f, -256.8f, 43.6f), "Feu dans la cuisine du restaurant", FireType.Building),
                new FireLocation("Incendie Magasin 24/7 Downtown", new Vector3(24.9f, -1347.3f, 29.5f), "Incendie dans le magasin", FireType.Building),
                new FireLocation("Incendie Banque Fleeca", new Vector3(147.0f, -1042.2f, 29.4f), "Feu dans la banque", FireType.Building),
                
                // === VÉHICULES EN FEU ===
                new FireLocation("Voiture en Feu - Grove Street", new Vector3(-127.8f, -1438.5f, 31.3f), "Véhicule en flammes dans Grove Street", FireType.Vehicle),
                new FireLocation("Camion en Feu - Autoroute", new Vector3(1163.5f, -1489.2f, 34.7f), "Camion accidenté en feu", FireType.Vehicle),
                new FireLocation("Bus en Feu - Downtown", new Vector3(230.8f, -873.4f, 30.5f), "Bus de ville en flammes", FireType.Vehicle),
                new FireLocation("Moto en Feu - Vinewood", new Vector3(374.2f, 323.8f, 103.6f), "Moto accidentée et en feu", FireType.Vehicle),
                new FireLocation("Voiture Sportive - Del Perro", new Vector3(-1425.6f, -276.0f, 46.2f), "Voiture de sport en flammes", FireType.Vehicle),
                
                // === VÉGÉTATION ET EXTÉRIEUR ===
                new FireLocation("Feu de Forêt - Mount Chiliad", new Vector3(486.3f, 5593.1f, 794.3f), "Feu de forêt dans les collines", FireType.Vegetation),
                new FireLocation("Arbres en Feu - Vinewood Hills", new Vector3(746.2f, 1275.8f, 360.3f), "Plusieurs arbres en flammes", FireType.Vegetation),
                new FireLocation("Buissons en Feu - Sandy Shores", new Vector3(1532.8f, 3724.5f, 34.0f), "Végétation en feu dans le désert", FireType.Vegetation),
                new FireLocation("Feu de Camp Dangereux", new Vector3(2476.8f, 4438.2f, 35.3f), "Feu de camp qui s'étend", FireType.Vegetation),
                
                // === STATIONS-SERVICE ET INDUSTRIEL ===
                new FireLocation("Station-Service Paleto Bay", new Vector3(161.4f, 6641.7f, 31.6f), "Pompes à essence en feu", FireType.Industrial),
                new FireLocation("Raffinerie en Feu", new Vector3(2682.9f, 1469.2f, 24.5f), "Incendie dans la raffinerie", FireType.Industrial),
                new FireLocation("Entrepôt Terminal", new Vector3(1009.0f, -2110.5f, 31.0f), "Entrepôt du port en flammes", FireType.Industrial),
                new FireLocation("Usine Textile", new Vector3(712.9f, -962.1f, 20.9f), "Feu dans l'usine textile", FireType.Industrial),
                
                // === PARKING ET ZONES OUVERTES ===
                new FireLocation("Parking Aéroport LSX", new Vector3(-1037.8f, -2738.9f, 13.8f), "Incendie dans le parking de l'aéroport", FireType.Parking),
                new FireLocation("Parking Maze Bank", new Vector3(-75.2f, -818.8f, 326.2f), "Feu dans le parking du gratte-ciel", FireType.Parking),
                new FireLocation("Parking Plage Vespucci", new Vector3(-1183.4f, -1511.2f, 4.4f), "Incendie près de la plage", FireType.Parking),
                
                // === MAISONS ET RÉSIDENTIEL ===
                new FireLocation("Maison Grove Street", new Vector3(-9.8f, -1438.5f, 31.1f), "Incendie dans une maison", FireType.Residential),
                new FireLocation("Maison Vinewood Hills", new Vector3(117.2f, 563.8f, 183.96f), "Villa en feu dans les collines", FireType.Residential),
                new FireLocation("Appartement Downtown", new Vector3(-269.3f, -957.2f, 31.2f), "Feu dans un immeuble résidentiel", FireType.Residential)
            };
        }
        
        private void CreateJobLocationBlip()
        {
            _jobBlip = World.CreateBlip(_jobLocation);
            _jobBlip.Sprite = BlipSprite.Garage;
            _jobBlip.Color = BlipColor.Red;
            _jobBlip.Name = "Emploi - Pompier";
            _jobBlip.IsShortRange = false;
            _jobBlip.Scale = 0.9f;
            
            Function.Call(Hash.SET_BLIP_AS_MISSION_CREATOR_BLIP, _jobBlip.Handle, true);
            Function.Call(Hash.SET_BLIP_PRIORITY, _jobBlip.Handle, 10);
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
                else if (_isOnShift && _currentFireTruck != null && _currentFireTruck.Exists())
                {
                    if (_isOnMission && _currentFire != null)
                    {
                        HandleFireMission(player);
                    }
                    
                    DisplayFirefighterHUD();
                }
                else if (_isOnShift && (_currentFireTruck == null || !_currentFireTruck.Exists()))
                {
                    // Le camion a disparu, terminer le service
                    EndShift();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Firefighter tick error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Job Management
        
        private void CheckJobLocation(Ped player)
        {
            float distance = player.Position.DistanceTo(_jobLocation);
            if (distance < 5.0f)
            {
                Screen.ShowSubtitle("~INPUT_CONTEXT~ Commencer votre service de pompier", 100);
                
                if (Game.IsControlJustPressed(GTA.Control.Context))
                {
                    Logger.Info("Firefighter job activation detected!");
                    StartFirefighterJob();
                }
            }
        }
        
        private void StartFirefighterJob()
        {
            try
            {
                Logger.Info("Starting firefighter job...");
                
                // Effet de transition
                Screen.FadeOut(1000);
                Script.Wait(1000);
                
                Ped player = Game.Player.Character;
                
                // Spawn du camion de pompier près de la caserne
                Vector3 truckSpawnPos = new Vector3(1207.0f, -1477.0f, 34.7f);
                _currentFireTruck = World.CreateVehicle(VehicleHash.FireTruck, truckSpawnPos, 135.0f);
                
                if (_currentFireTruck != null)
                {
                    _currentFireTruck.IsEngineRunning = true;
                    _currentFireTruck.FuelLevel = 100.0f;
                    
                    // Téléporter le joueur dans le camion
                    player.Task.WarpIntoVehicle(_currentFireTruck, VehicleSeat.Driver);
                    
                    // Créer blip pour le camion
                    _truckBlip = _currentFireTruck.AddBlip();
                    _truckBlip.Sprite = BlipSprite.PersonalVehicleCar;
                    _truckBlip.Color = BlipColor.Red;
                    _truckBlip.Name = "Camion de Pompier";
                    
                    // Donner l'extincteur au joueur
                    GiveFireExtinguisher(player);
                    
                    _isOnShift = true;
                    _totalEarnings = 0;
                    
                    // Message de bienvenue
                    Notification.PostTicker("~g~Service de pompier commencé ! Utilisez M pour accéder au menu des missions.", false, true);
                    
                    Logger.Info("Firefighter job started successfully.");
                }
                
                Screen.FadeIn(1000);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting firefighter job: {ex.Message}");
                Screen.FadeIn(1000);
            }
        }
        
        private void GiveFireExtinguisher(Ped player)
        {
            // Supprimer toutes les armes existantes
            player.Weapons.RemoveAll();
            
            // Donner seulement l'extincteur
            player.Weapons.Give(WeaponHash.FireExtinguisher, 9999, true, true);
            _hasFireExtinguisher = true;
            
            Notification.PostTicker("~b~Extincteur ajouté à votre inventaire !", false, true);
        }
        
        private void StartRandomMission()
        {
            if (_isOnMission) return;
            
            // Sélectionner un feu aléatoire
            Random rand = new Random();
            _currentFire = _fireLocations[rand.Next(_fireLocations.Count)];
            
            // Créer le blip de mission
            _missionBlip = World.CreateBlip(_currentFire.Position);
            _missionBlip.Sprite = BlipSprite.Waypoint;
            _missionBlip.Color = BlipColor.Red;
            _missionBlip.Name = _currentFire.Name;
            Function.Call(Hash.SET_BLIP_ROUTE, _missionBlip.Handle, true);
            Function.Call(Hash.SET_BLIP_ROUTE_COLOUR, _missionBlip.Handle, (int)BlipColor.Red);
            
            // Initialiser la mission
            _isOnMission = true;
            _fireExtinguished = false;
            _fireIntensity = 100;
            _missionStartTime = DateTime.Now;
            
            // Créer les particules de feu à la destination
            CreateFireParticles(_currentFire.Position);
            
            Notification.PostTicker($"~r~URGENCE ! {_currentFire.Name} - {_currentFire.Description}", false, true);
            Notification.PostTicker("~y~Rendez-vous sur les lieux et éteignez l'incendie !", false, true);
        }
        
        private void CreateFireParticles(Vector3 position)
        {
            try
            {
                if (_currentFire == null) return;
                
                // Ajuster la position selon le type de feu pour éviter qu'il flotte
                Vector3 adjustedPosition = GetAdjustedFirePosition(position, _currentFire.Type);
                
                // Créer un vrai feu dans le jeu avec intensité adaptée
                int fireSize = GetFireSizeForType(_currentFire.Type);
                Function.Call(Hash.START_SCRIPT_FIRE, adjustedPosition.X, adjustedPosition.Y, adjustedPosition.Z, fireSize, true);
                
                // Spawner des objets selon le type d'incendie
                SpawnFireObjects(adjustedPosition, _currentFire.Type);
                
                // Ajouter des particules spécialisées pour plus d'effet
                CreateSpecializedParticles(adjustedPosition, _currentFire.Type);
                
                Logger.Info($"Fire started at position: {adjustedPosition} (Type: {_currentFire.Type})");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating fire particles: {ex.Message}");
            }
        }
        
        private Vector3 GetAdjustedFirePosition(Vector3 originalPosition, FireType fireType)
        {
            // Ajuster la hauteur pour éviter que le feu flotte
            switch (fireType)
            {
                case FireType.Building:
                    // Légèrement au-dessus du sol pour les bâtiments
                    return new Vector3(originalPosition.X, originalPosition.Y, originalPosition.Z + 0.5f);
                
                case FireType.Vehicle:
                    // Au niveau du sol pour les véhicules
                    return new Vector3(originalPosition.X, originalPosition.Y, originalPosition.Z);
                
                case FireType.Vegetation:
                    // Légèrement au-dessus du sol pour la végétation
                    return new Vector3(originalPosition.X, originalPosition.Y, originalPosition.Z + 0.3f);
                
                case FireType.Industrial:
                    // Plus haut pour les installations industrielles
                    return new Vector3(originalPosition.X, originalPosition.Y, originalPosition.Z + 1.0f);
                
                case FireType.Parking:
                case FireType.Residential:
                default:
                    // Position standard
                    return originalPosition;
            }
        }
        
        private int GetFireSizeForType(FireType fireType)
        {
            return fireType switch
            {
                FireType.Building => 15,      // Feu modéré dans les bâtiments
                FireType.Vehicle => 8,        // Feu plus petit pour les véhicules
                FireType.Vegetation => 25,    // Feu plus large pour la végétation
                FireType.Industrial => 35,    // Gros feu pour l'industriel
                FireType.Parking => 12,       // Feu modéré pour les parkings
                FireType.Residential => 18,   // Feu résidentiel
                _ => 20
            };
        }
        
        private void SpawnFireObjects(Vector3 position, FireType fireType)
        {
            try
            {
                switch (fireType)
                {
                    case FireType.Vehicle:
                        // Spawner un véhicule endommagé aléatoire
                        SpawnBurntVehicle(position);
                        break;
                        
                    case FireType.Vegetation:
                        // Spawner des objets de végétation (optionnel)
                        break;
                        
                    case FireType.Industrial:
                        // Ajouter des barils d'essence ou autres objets industriels
                        SpawnIndustrialObjects(position);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning fire objects: {ex.Message}");
            }
        }
        
        private void SpawnBurntVehicle(Vector3 position)
        {
            try
            {
                // Véhicules aléatoires qui peuvent prendre feu
                VehicleHash[] burnableVehicles = {
                    VehicleHash.Blista, VehicleHash.Panto, VehicleHash.Futo,
                    VehicleHash.Sentinel, VehicleHash.Penumbra, VehicleHash.Premier,
                    VehicleHash.Hauler, VehicleHash.Mule, VehicleHash.Pounder,
                    VehicleHash.Bus, VehicleHash.Coach
                };
                
                Random rand = new Random();
                VehicleHash selectedVehicle = burnableVehicles[rand.Next(burnableVehicles.Length)];
                
                // Trouver une position libre près de la position donnée
                Vector3 spawnPos = World.GetNextPositionOnStreet(position);
                
                Vehicle burningVehicle = World.CreateVehicle(selectedVehicle, spawnPos, rand.Next(0, 360));
                if (burningVehicle != null)
                {
                    // Endommager le véhicule
                    burningVehicle.EngineHealth = 0;
                    burningVehicle.BodyHealth = 100;
                    burningVehicle.IsEngineRunning = false;
                    
                    // Lui donner un aspect endommagé en réduisant sa santé
                    burningVehicle.HealthFloat = 200.0f;
                    
                    Logger.Info($"Spawned burning vehicle: {selectedVehicle} at {spawnPos}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning burnt vehicle: {ex.Message}");
            }
        }
        
        private void SpawnIndustrialObjects(Vector3 position)
        {
            try
            {
                // Ajouter des barils d'essence ou autres objets industriels
                Model barrelModel = "prop_barrel_02a";
                if (barrelModel.IsValid)
                {
                    barrelModel.Request(5000);
                    if (barrelModel.IsLoaded)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            Vector3 barrelPos = position + new Vector3(
                                (float)(new Random().NextDouble() * 4 - 2),
                                (float)(new Random().NextDouble() * 4 - 2),
                                0
                            );
                            World.CreateProp(barrelModel, barrelPos, false, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning industrial objects: {ex.Message}");
            }
        }
        
        private void CreateSpecializedParticles(Vector3 position, FireType fireType)
        {
            try
            {
                string particleAsset = "core";
                string particleName = GetParticleNameForType(fireType);
                float particleScale = GetParticleScaleForType(fireType);
                
                Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, particleAsset);
                
                while (!Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, particleAsset))
                {
                    Script.Wait(1);
                }
                
                Function.Call(Hash.USE_PARTICLE_FX_ASSET, particleAsset);
                _fireParticleHandle = Function.Call<int>(Hash.START_PARTICLE_FX_LOOPED_AT_COORD, 
                    particleName, position.X, position.Y, position.Z, 
                    0.0f, 0.0f, 0.0f, particleScale, false, false, false, false);
                    
                _fireParticleActive = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating specialized particles: {ex.Message}");
            }
        }
        
        private string GetParticleNameForType(FireType fireType)
        {
            return fireType switch
            {
                FireType.Vehicle => "fire_wrecked_car",
                FireType.Building => "fire_wrecked_plane_cockpit",
                FireType.Vegetation => "fire_object_plane_wing",
                FireType.Industrial => "exp_grd_bzgas_smoke",
                FireType.Parking => "fire_wrecked_car",
                FireType.Residential => "fire_wrecked_plane_cockpit",
                _ => "fire_wrecked_plane_cockpit"
            };
        }
        
        private float GetParticleScaleForType(FireType fireType)
        {
            return fireType switch
            {
                FireType.Vehicle => 2.0f,
                FireType.Building => 3.0f,
                FireType.Vegetation => 4.0f,
                FireType.Industrial => 5.0f,
                FireType.Parking => 2.5f,
                FireType.Residential => 3.5f,
                _ => 3.0f
            };
        }
        
        private void StopFireParticles()
        {
            if (_fireParticleActive && _fireParticleHandle != -1)
            {
                Function.Call(Hash.STOP_PARTICLE_FX_LOOPED, _fireParticleHandle, false);
                _fireParticleActive = false;
                _fireParticleHandle = -1;
            }
            
            // Éteindre tous les feux dans un rayon autour de la position
            if (_currentFire != null)
            {
                Function.Call(Hash.STOP_FIRE_IN_RANGE, _currentFire.Position.X, _currentFire.Position.Y, _currentFire.Position.Z, 50.0f);
                Logger.Info($"Fire extinguished at position: {_currentFire.Position}");
            }
        }
        
        private void HandleFireMission(Ped player)
        {
            if (_currentFire == null) return;
            
            float distance = player.Position.DistanceTo(_currentFire.Position);
            
            if (distance < 15.0f && !_fireExtinguished)
            {
                Screen.ShowSubtitle("~r~Utilisez votre extincteur pour éteindre l'incendie !", 100);
                
                // Vérifier si le joueur utilise l'extincteur
                if (Function.Call<bool>(Hash.IS_PED_SHOOTING, player.Handle))
                {
                    ExtinguishFire();
                }
            }
            
            if (_fireExtinguished)
            {
                CompleteMission();
            }
        }
        
        private void ExtinguishFire()
        {
            if ((DateTime.Now - _lastExtinguishTime).TotalMilliseconds < 100) return;
            
            _fireIntensity -= 2; // Diminuer l'intensité du feu
            _lastExtinguishTime = DateTime.Now;
            
            if (_fireIntensity <= 0)
            {
                _fireExtinguished = true;
                StopFireParticles();
                Notification.PostTicker("~g~Incendie éteint avec succès !", false, true);
            }
            else
            {
                // Afficher le progrès
                string progressBar = "";
                int progress = (100 - _fireIntensity) / 10;
                for (int i = 0; i < 10; i++)
                {
                    progressBar += i < progress ? "█" : "░";
                }
                Screen.ShowSubtitle($"~b~Extinction en cours: {progressBar} {100 - _fireIntensity}%", 100);
            }
        }
        
        private void CompleteMission()
        {
            if (_currentFire == null) return;
            
            // Calculer la récompense
            TimeSpan missionTime = DateTime.Now - _missionStartTime;
            int reward = BASE_REWARD;
            
            // Bonus de rapidité (moins de 5 minutes)
            if (missionTime.TotalMinutes < 5)
            {
                reward += TIME_BONUS * (int)(5 - missionTime.TotalMinutes);
            }
            
            // Donner l'argent au joueur
            Game.Player.Money += reward;
            _totalEarnings += reward;
            _missionsCompleted++;
            
            // Nettoyer la mission
            _missionBlip?.Delete();
            _missionBlip = null;
            _currentFire = null;
            _isOnMission = false;
            
            // Messages de succès
            Notification.PostTicker($"~g~Mission terminée ! +${reward}", false, true);
            Notification.PostTicker("~b~Retournez à la caserne ou utilisez M pour une nouvelle mission", false, true);
        }
        
        private void EndShift()
        {
            try
            {
                Logger.Info("Ending firefighter shift...");
                
                // Nettoyer les ressources
                StopFireParticles();
                
                _missionBlip?.Delete();
                _truckBlip?.Delete();
                
                if (_currentFireTruck != null && _currentFireTruck.Exists())
                {
                    _currentFireTruck.Delete();
                }
                
                // Restaurer l'inventaire normal
                Ped player = Game.Player.Character;
                player.Weapons.RemoveAll();
                
                _isOnShift = false;
                _isOnMission = false;
                _hasFireExtinguisher = false;
                
                // Message de fin
                if (_missionsCompleted > 0)
                {
                    Notification.PostTicker($"~g~Service terminé ! {_missionsCompleted} missions complétées. Total gagné: ${_totalEarnings}", false, true);
                }
                else
                {
                    Notification.PostTicker("~y~Service terminé sans mission complétée.", false, true);
                }
                
                Logger.Info("Firefighter shift ended successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ending firefighter shift: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Menu Events
        
        private void OnStartMission(object sender, EventArgs e)
        {
            if (!_isOnShift) return;
            
            if (_isOnMission)
            {
                Notification.PostTicker("~r~Vous avez déjà une mission en cours !", false, true);
                return;
            }
            
            StartRandomMission();
            _firefighterMenu.Visible = false;
        }
        
        private void OnEndShift(object sender, EventArgs e)
        {
            EndShift();
            _firefighterMenu.Visible = false;
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
                        if (_firefighterMenu.Visible)
                        {
                            _firefighterMenu.Visible = false;
                        }
                        else
                        {
                            _firefighterMenu.Visible = true;
                        }
                        break;
                        
                    case Keys.End:
                        EndShift();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling firefighter key input: {ex.Message}");
            }
        }
        
        #endregion
        
        #region HUD
        
        private void DisplayFirefighterHUD()
        {
            try
            {
                string hudText = "~r~SERVICE POMPIER~w~";
                
                if (_isOnMission && _currentFire != null)
                {
                    // Afficher le type d'incendie avec une icône
                    string fireTypeIcon = GetFireTypeIcon(_currentFire.Type);
                    string fireTypeName = GetFireTypeName(_currentFire.Type);
                    
                    hudText += $" | {fireTypeIcon} {fireTypeName}: {_currentFire.Name}";
                    if (!_fireExtinguished)
                    {
                        hudText += $" | ~r~Intensité: {_fireIntensity}%~w~";
                    }
                    else
                    {
                        hudText += " | ~g~Éteint!~w~";
                    }
                }
                else
                {
                    hudText += " | ~y~En attente de mission~w~";
                }
                
                hudText += $" | Missions: {_missionsCompleted} | Gains: ~g~${_totalEarnings}~w~";
                hudText += $" | Extincteur: {(_hasFireExtinguisher ? "~g~✓~w~" : "~r~✗~w~")}";
                hudText += " | ~b~M~w~: Menu | ~b~End~w~: Terminer";
                
                Screen.ShowSubtitle(hudText, 100);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error displaying firefighter HUD: {ex.Message}");
            }
        }
        
        private string GetFireTypeIcon(FireType fireType)
        {
            return fireType switch
            {
                FireType.Building => "🏢",
                FireType.Vehicle => "🚗",
                FireType.Vegetation => "🌲",
                FireType.Industrial => "🏭",
                FireType.Parking => "🅿️",
                FireType.Residential => "🏠",
                _ => "🔥"
            };
        }
        
        private string GetFireTypeName(FireType fireType)
        {
            return fireType switch
            {
                FireType.Building => "Bâtiment",
                FireType.Vehicle => "Véhicule",
                FireType.Vegetation => "Végétation",
                FireType.Industrial => "Industriel",
                FireType.Parking => "Parking",
                FireType.Residential => "Résidentiel",
                _ => "Incendie"
            };
        }
        
        #endregion
        
        #region Cleanup
        
        private void OnAborted(object sender, EventArgs e)
        {
            Logger.Info("Firefighter Manager is being unloaded.");
            
            try
            {
                StopFireParticles();
                _jobBlip?.Delete();
                _missionBlip?.Delete();
                _truckBlip?.Delete();
                
                if (_currentFireTruck != null && _currentFireTruck.Exists())
                {
                    _currentFireTruck.Delete();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during firefighter cleanup: {ex.Message}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Représente un emplacement d'incendie
    /// </summary>
    public class FireLocation
    {
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public string Description { get; set; }
        public FireType Type { get; set; }
        
        public FireLocation(string name, Vector3 position, string description, FireType type)
        {
            Name = name;
            Position = position;
            Description = description;
            Type = type;
        }
    }
}