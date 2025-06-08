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
    /// Système de chauffeur de taxi avec courses dynamiques et clients individuels
    /// </summary>
    public class TaxiDriverManager : Script
    {
        #region Fields
        
        private ObjectPool _menuPool;
        private NativeMenu _taxiMenu = null!;
        private NativeItem _endShiftItem = null!;
        
        private Vehicle? _currentTaxi;
        private bool _isDriving;
        private bool _isOnShift;
        private Ped? _currentClient;
        private bool _hasActiveRide;

        private Vector3 _jobLocation = new Vector3(907.47f, -177.23f, 74.22f); // Downtown Cab Co.
        private Blip? _jobBlip;
        private Blip? _taxiBlip;
        
        // Économie - tracking des vrais gains du jeu
        private int _initialMoney;
        private int _sessionEarnings;
        private int _ridesCompleted;
        private float _totalDistance;
        private int _lastMoneyCheck;
        
        // Fin de service
        private bool _isEndingService = false;
        private int _endServiceTimer = 0;
        
        // Locations populaires pour les courses
        private List<TaxiDestination> _popularDestinations = new();
        
        #endregion
        
        #region Initialization
        
        public TaxiDriverManager()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            
            _menuPool = new ObjectPool();
            InitializeMenu();
            InitializeDestinations();
            CreateJobLocationBlip();
            
            Logger.Info("Taxi Driver Manager initialized.");
        }
        
        private void InitializeMenu()
        {
            _taxiMenu = new NativeMenu("Gestion Taxi", "Menu du service taxi");
            _menuPool.Add(_taxiMenu);
            
            _endShiftItem = new NativeItem("Terminer le service", "Finir votre service de taxi");
            
            _taxiMenu.Add(_endShiftItem);
            
            _endShiftItem.Activated += OnEndShift;
        }
        
        private void InitializeDestinations()
        {
            _popularDestinations = new List<TaxiDestination>
            {
                new TaxiDestination("Aéroport International", new Vector3(-1037.0f, -2730.0f, 13.8f)),
                new TaxiDestination("Centre-ville", new Vector3(-256.0f, -715.0f, 33.5f)),
                new TaxiDestination("Vinewood", new Vector3(294.0f, 180.0f, 104.4f)),
                new TaxiDestination("Plage de Vespucci", new Vector3(-1238.0f, -1491.0f, 4.0f)),
                new TaxiDestination("Port de Los Santos", new Vector3(390.0f, -2627.0f, 6.0f)),
                new TaxiDestination("Little Seoul", new Vector3(-526.0f, -1211.0f, 18.2f)),
                new TaxiDestination("Paleto Bay", new Vector3(-279.0f, 6226.0f, 31.5f)),
                new TaxiDestination("Sandy Shores", new Vector3(1960.0f, 3740.0f, 32.3f)),
                new TaxiDestination("Casino Diamond", new Vector3(925.0f, 46.0f, 81.1f)),
                new TaxiDestination("Hôpital Central", new Vector3(-449.0f, -340.0f, 34.5f)),
                new TaxiDestination("Commissariat", new Vector3(428.0f, -982.0f, 30.7f)),
                new TaxiDestination("Grove Street", new Vector3(-128.0f, -1464.0f, 33.8f)),
                new TaxiDestination("Del Perro Pier", new Vector3(-1850.0f, -1231.0f, 13.0f)),
                new TaxiDestination("Maze Bank Tower", new Vector3(-75.0f, -818.0f, 326.2f)),
                new TaxiDestination("Stripclub Vanilla Unicorn", new Vector3(129.0f, -1299.0f, 29.2f))
            };
        }
        
        private void CreateJobLocationBlip()
        {
            _jobBlip = World.CreateBlip(_jobLocation);
            _jobBlip.Sprite = BlipSprite.Store;
            _jobBlip.Color = BlipColor.Yellow;
            _jobBlip.Name = "Emploi - Chauffeur de Taxi";
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
                else if (_isOnShift && _currentTaxi != null && _currentTaxi.Exists())
                {
                    if (_isEndingService)
                    {
                        HandleEndService();
                    }
                    else
                    {
                        // Vérifier si le joueur est monté dans le taxi
                        if (!_isDriving && player.IsInVehicle(_currentTaxi) && player.SeatIndex == VehicleSeat.Driver)
                        {
                            _isDriving = true;
                            Notification.PostTicker("~g~Vous êtes maintenant en service ! Le jeu gère les interactions taxi.", false, true);
                        }
                        
                        if (_isDriving)
                        {
                            HandleTaxiDriving();
                            ManageCurrentRide();
                            TrackEarnings();
                            DisplayTaxiHUD();
                        }
                        else
                        {
                            // Montrer instruction pour monter dans le taxi
                            Screen.ShowSubtitle("~y~Montez dans le taxi pour commencer le service", 100);
                        }
                    }
                }
                else if (_isOnShift && (_currentTaxi == null || !_currentTaxi.Exists()))
                {
                    // Le taxi a disparu, terminer le service
                    EndShift();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"TaxiDriver tick error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Job Management
        
        private void CheckJobLocation(Ped player)
        {
            float distance = player.Position.DistanceTo(_jobLocation);
            if (distance < 3.0f)
            {
                Screen.ShowSubtitle("~INPUT_CONTEXT~ Commencer votre service de chauffeur de taxi", 100);
                
                if (Game.IsControlJustPressed(GTA.Control.Context))
                {
                    StartTaxiJob();
                }
            }
        }
        
        private void StartTaxiJob()
        {
            try
            {
                Logger.Info("Starting taxi job...");
                
                // Effet de fade - transition immersive
                Screen.FadeOut(1000);
                Script.Wait(1000);
                
                // Choisir une position de spawn pour le taxi
                Vector3 spawnPosition = new Vector3(903.47f, -191.23f, 73.22f);
                float spawnHeading = 58.0f;
                
                // Utiliser un modèle de taxi plus fiable
                var taxiModel = new Model("taxi");
                if (!taxiModel.IsValid)
                {
                    Logger.Error("Taxi model is not valid, trying alternative...");
                    taxiModel = new Model(VehicleHash.Blista); // Véhicule de fallback
                }
                
                taxiModel.Request(5000);
                
                if (taxiModel.IsLoaded)
                {
                    _currentTaxi = World.CreateVehicle(taxiModel, spawnPosition, spawnHeading);
                    
                    if (_currentTaxi != null && _currentTaxi.Exists())
                    {
                        _currentTaxi.IsPersistent = true;
                        _currentTaxi.PlaceOnGround();
                        
                        // Personnaliser le taxi
                        _currentTaxi.Mods.LicensePlate = "TAXI";
                        
                        // Créer un blip pour le taxi
                        _taxiBlip = _currentTaxi.AddBlip();
                        _taxiBlip.Sprite = BlipSprite.PersonalVehicleCar;
                        _taxiBlip.Color = BlipColor.Yellow;
                        _taxiBlip.Name = "Taxi - En service";
                        _taxiBlip.IsShortRange = true;
                        
                        _isOnShift = true;
                        
                        // Initialiser le tracking des gains
                        _initialMoney = Game.Player.Money;
                        _lastMoneyCheck = Game.Player.Money;
                        _sessionEarnings = 0;
                        _ridesCompleted = 0;
                        _totalDistance = 0;
                        
                        Logger.Info($"Taxi spawned successfully at {spawnPosition}");
                        
                        // Fade in
                        Screen.FadeIn(1000);
                        
                        Notification.PostTicker("~g~Taxi créé ! Montez dedans pour commencer le service.", false, true);
                    }
                    else
                    {
                        Logger.Error("Failed to create taxi");
                        Screen.FadeIn(1000);
                        Notification.PostTicker("~r~Erreur lors de la création du taxi", false, true);
                    }
                }
                else
                {
                    Logger.Error("Failed to load taxi model");
                    Screen.FadeIn(1000);
                    Notification.PostTicker("~r~Erreur lors du chargement du modèle de taxi", false, true);
                }
                
                taxiModel.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting taxi job: {ex.Message}");
                Screen.FadeIn(1000);
                Notification.PostTicker("~r~Erreur lors du démarrage du service", false, true);
            }
        }
        
        private void StartEndService()
        {
            if (_isEndingService) return;
            
            _isEndingService = true;
            _endServiceTimer = 0;
            
            // Démarrer la conduite automatique IMMÉDIATEMENT vers le parking
            if (_currentTaxi != null && _currentTaxi.Exists())
            {
                Ped player = Game.Player.Character;
                Vector3 taxiParkingSpot = new Vector3(903.47f, -191.23f, 73.22f);
                
                // S'assurer que le joueur est dans le véhicule
                if (!player.IsInVehicle(_currentTaxi))
                {
                    player.SetIntoVehicle(_currentTaxi, VehicleSeat.Driver);
                }
                
                // Arrêter toute tâche existante pour éviter les conflits
                player.Task.ClearAll();
                
                // Attendre une frame puis lancer la conduite automatique
                Script.Wait(100);
                
                // Utiliser la méthode native TASK_VEHICLE_DRIVE_TO_COORD directement pour plus de contrôle
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, player.Handle, _currentTaxi.Handle,
                    taxiParkingSpot.X, taxiParkingSpot.Y, taxiParkingSpot.Z,
                    20.0f, // speed
                    0, // style (0 = normal, respecte les routes)
                    _currentTaxi.Model.Hash, // vehicleModel
                    (int)(VehicleDrivingFlags.StopForVehicles | VehicleDrivingFlags.StopAtTrafficLights | VehicleDrivingFlags.SwerveAroundAllVehicles),
                    3.0f, // targetRadius
                    0.0f); // straightLineDist - 0 pour forcer l'utilisation des routes
                
                Notification.PostTicker("~g~Conduite automatique activée ! Retour au parking...", false, true);
                
                Logger.Info("Starting IMMEDIATE automatic driving to parking spot");
            }
        }
        
        private void HandleEndService()
        {
            _endServiceTimer += (int)(Game.LastFrameTime * 1000); // Convertir en millisecondes
            
            Ped player = Game.Player.Character;
            Vector3 taxiParkingSpot = new Vector3(903.47f, -191.23f, 73.22f);
            float distanceToParkingSpot = player.Position.DistanceTo(taxiParkingSpot);
            
            // Phase 1 (0-5 secondes) : Conduite automatique visible vers le parking
            if (_endServiceTimer < 5000)
            {
                // Vérifier CONSTAMMENT que la conduite automatique est active
                if (player.IsInVehicle(_currentTaxi))
                {
                    // Vérifier si la tâche est active toutes les 500ms
                    if (_endServiceTimer % 500 < Game.LastFrameTime * 1000)
                    {
                                                 if (_currentTaxi != null && _currentTaxi.Exists())
                         {
                             if (!Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, player.Handle, _currentTaxi.Handle, false))
                             {
                                 // Le joueur n'est plus dans le véhicule, le remettre
                                 player.SetIntoVehicle(_currentTaxi, VehicleSeat.Driver);
                             }
                             
                             if (!Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, player.Handle, 16)) // TASK_VEHICLE_DRIVE_TO_COORD
                             {
                                 Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, player.Handle, _currentTaxi.Handle,
                                     taxiParkingSpot.X, taxiParkingSpot.Y, taxiParkingSpot.Z,
                                     25.0f, // speed - légèrement plus élevée pour la phase 1
                                     0, // style (0 = normal, respecte les routes)
                                     _currentTaxi.Model.Hash, // vehicleModel
                                     (int)(VehicleDrivingFlags.StopForVehicles | VehicleDrivingFlags.StopAtTrafficLights | VehicleDrivingFlags.SwerveAroundAllVehicles),
                                     3.0f, // targetRadius
                                     0.0f); // straightLineDist - 0 pour forcer l'utilisation des routes
                                 Logger.Info("Relaunching automatic driving task to parking spot");
                             }
                         }
                    }
                }
                
                // Afficher un message de progression chaque seconde
                if ((_endServiceTimer / 1000) != ((_endServiceTimer - (int)(Game.LastFrameTime * 1000)) / 1000))
                {
                    int secondsLeft = 5 - (_endServiceTimer / 1000);
                    Screen.ShowSubtitle($"~g~🚕 Retour au dépôt en cours... {secondsLeft}s restantes", 1100);
                }
            }
            // Phase 2 (5 secondes) : Commencer le fade out
            else if (_endServiceTimer >= 5000 && _endServiceTimer < 5100)
            {
                Function.Call(Hash.DO_SCREEN_FADE_OUT, 2000); // 2 secondes de fade out
                Screen.ShowSubtitle("~y~Arrivée au dépôt...", 3000);
                Logger.Info("Starting fade out - approaching depot");
            }
            // Phase 3 (7 secondes) : TÉLÉPORTATION PRÈS DU CENTRE DE TAXI
            else if (_endServiceTimer >= 7000 && _endServiceTimer < 7100)
            {
                // S'assurer que l'écran est complètement noir
                if (Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT))
                {
                    // TÉLÉPORTATION INVISIBLE près du centre de taxi
                    if (_currentTaxi != null && _currentTaxi.Exists() && player.IsInVehicle(_currentTaxi))
                    {
                        // Arrêter toute tâche de conduite
                        player.Task.ClearAll();
                        
                        // Téléporter près du centre de taxi (pas directement au parking)
                        Vector3 nearTaxiCenter = new Vector3(934.57f, -165.86f, 74.05f); // Position spécifique
                        _currentTaxi.Position = nearTaxiCenter;
                        _currentTaxi.Heading = 148.05f; // Heading spécifique
                        _currentTaxi.PlaceOnGround();
                        _currentTaxi.Speed = 0.0f;
                        
                        // Remettre le moteur en marche pour la conduite automatique
                        Function.Call(Hash.SET_VEHICLE_ENGINE_ON, _currentTaxi.Handle, true, true, true);
                        
                        Logger.Info("Vehicle teleported invisibly near taxi center during fadeout");
                    }
                }
            }
            // Phase 4 (8 secondes) : Fade in + conduite automatique vers le parking
            else if (_endServiceTimer >= 8000 && _endServiceTimer < 8100)
            {
                Function.Call(Hash.DO_SCREEN_FADE_IN, 2000); // 2 secondes de fade in
                Logger.Info("Starting fade in - preparing automatic drive to parking");
            }
            // Phase 5 (10 secondes) : Démarrer la conduite automatique vers le parking
            else if (_endServiceTimer >= 10000 && _endServiceTimer < 10100)
            {
                // S'assurer que l'écran est visible et lancer la conduite automatique
                if (!Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT) && !Function.Call<bool>(Hash.IS_SCREEN_FADING_OUT))
                {
                    if (_currentTaxi != null && _currentTaxi.Exists() && player.IsInVehicle(_currentTaxi))
                    {
                        // Lancer la conduite automatique vers le parking avec navigation routière
                        Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, player.Handle, _currentTaxi.Handle,
                            taxiParkingSpot.X, taxiParkingSpot.Y, taxiParkingSpot.Z,
                            20.0f, // speed
                            0, // style (0 = normal, respecte les routes)
                            _currentTaxi.Model.Hash, // vehicleModel
                            (int)(VehicleDrivingFlags.StopForVehicles | VehicleDrivingFlags.StopAtTrafficLights | VehicleDrivingFlags.SwerveAroundAllVehicles),
                            3.0f, // targetRadius
                            0.0f); // straightLineDist - 0 pour forcer l'utilisation des routes
                        
                        Logger.Info("Starting automatic drive to parking spot after fade in");
                    }
                }
            }
            // Phase 6 (12+ secondes) : Surveiller l'approche du parking et sortir du véhicule
            else if (_endServiceTimer >= 12000 && player.IsInVehicle(_currentTaxi))
            {
                // Surveiller la distance au parking
                if (distanceToParkingSpot <= 5.0f)
                {
                    // Arrêter le véhicule au parking
                    if (_currentTaxi != null && _currentTaxi.Exists())
                    {
                        _currentTaxi.Speed = 0.0f;
                        Function.Call(Hash.SET_VEHICLE_HANDBRAKE, _currentTaxi.Handle, true);
                        
                        // Faire sortir le personnage
                        player.Task.LeaveVehicle(_currentTaxi, true);
                        Logger.Info("Player leaving vehicle at parking spot");
                        
                        // Passer à la phase suivante
                        _endServiceTimer = 18000;
                    }
                }
                else
                {
                    // Afficher le statut de conduite automatique
                    Screen.ShowSubtitle($"~g~Stationnement automatique... ({distanceToParkingSpot:F0}m)", 1100);
                    
                    // Relancer la tâche si nécessaire
                    if (_endServiceTimer % 2000 < Game.LastFrameTime * 1000)
                    {
                        if (_currentTaxi != null && _currentTaxi.Exists() && !Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, player.Handle, 16)) // TASK_VEHICLE_DRIVE_TO_COORD
                        {
                            Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, player.Handle, _currentTaxi.Handle,
                                taxiParkingSpot.X, taxiParkingSpot.Y, taxiParkingSpot.Z,
                                20.0f, // speed
                                0, // style (0 = normal, respecte les routes)
                                _currentTaxi.Model.Hash, // vehicleModel
                                (int)(VehicleDrivingFlags.StopForVehicles | VehicleDrivingFlags.StopAtTrafficLights | VehicleDrivingFlags.SwerveAroundAllVehicles),
                                3.0f, // targetRadius
                                0.0f); // straightLineDist - 0 pour forcer l'utilisation des routes
                            Logger.Info("Re-launching automatic parking task");
                        }
                    }
                }
            }
            // Phase 7 (18+ secondes) : Marcher vers le point de démarrage
            else if (_endServiceTimer >= 18000 && !player.IsInVehicle())
            {
                float distanceToJobLocation = player.Position.DistanceTo(_jobLocation);
                
                if (distanceToJobLocation > 2.0f)
                {
                    // Faire marcher le personnage vers le point de démarrage
                    if (!Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, player.Handle, 27)) // TASK_FOLLOW_NAV_MESH_TO_COORD
                    {
                        player.Task.FollowNavMeshTo(_jobLocation);
                        Logger.Info("Player walking to job start location");
                    }
                    
                    Screen.ShowSubtitle("~b~Retour au poste de travail...", 1100);
                }
                else
                {
                    // Arrivé au point de démarrage - terminer la séquence
                    player.Task.ClearAll();
                    CompleteEndService();
                    Logger.Info("Cinematic end service sequence completed");
                }
            }
        }
        
        private void CompleteEndService()
        {
            try
            {
                _isDriving = false;
                _isOnShift = false;
                _hasActiveRide = false;
                _isEndingService = false;
                
                // Nettoyer le client actuel
                if (_currentClient != null && _currentClient.Exists())
                {
                    _currentClient.Task.LeaveVehicle();
                    _currentClient.MarkAsNoLongerNeeded();
                }
                _currentClient = null;
                
                // Nettoyer les blips
                _taxiBlip?.Delete();
                
                // Supprimer le taxi
                if (_currentTaxi != null && _currentTaxi.Exists())
                {
                    _currentTaxi.Delete();
                }
                
                _currentTaxi = null;
                
                // Afficher les statistiques finales
                var avgDistance = _ridesCompleted > 0 ? _totalDistance / _ridesCompleted : 0;
                var message = $"Service terminé ! Gains de la session: ${_sessionEarnings} | Courses: {_ridesCompleted} | Distance moy.: {avgDistance:F1}m";
                Notification.PostTicker($"~g~{message}", false, true);
                
                Logger.Info($"Taxi shift ended. Session earnings: {_sessionEarnings}, Rides: {_ridesCompleted}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ending taxi shift: {ex.Message}");
            }
        }
        
        private void EndShift()
        {
            CompleteEndService();
        }
        
        #endregion
        
        #region Ride Management
        
        private void ManageCurrentRide()
        {
            if (!_hasActiveRide || _currentClient == null || !_currentClient.Exists()) return;
            
            // La gestion des destinations est maintenant faite par le jeu de base
            // Pas besoin de vérifier manuellement l'arrivée
        }
        
        private void TrackEarnings()
        {
            try
            {
                int currentMoney = Game.Player.Money;
                
                // Détecter les gains (augmentation de l'argent)
                if (currentMoney > _lastMoneyCheck)
                {
                    int gain = currentMoney - _lastMoneyCheck;
                    _sessionEarnings += gain;
                    
                    // Supposer qu'un gain signifie qu'une course est terminée
                    if (gain > 0)
                    {
                        _ridesCompleted++;
                        _totalDistance += 100f; // Estimation de distance par défaut
                        Logger.Info($"Taxi ride completed. Gain: ${gain}, Total session: ${_sessionEarnings}");
                    }
                }
                
                _lastMoneyCheck = currentMoney;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error tracking earnings: {ex.Message}");
            }
        }
        
        private void CompleteRide()
        {
            try
            {
                if (_currentClient != null && _currentClient.Exists())
                {
                    // Faire descendre le client
                    _currentClient.Task.LeaveVehicle();
                    _currentClient.Task.Wander();
                    _currentClient.MarkAsNoLongerNeeded();
                    
                    // Nettoyer
                    _currentClient = null;
                    _hasActiveRide = false;
                    
                    // Réactiver le service
                    if (_currentTaxi != null && _currentTaxi.Exists() && _taxiBlip != null)
                    {
                        _taxiBlip.Color = BlipColor.Green;
                        _taxiBlip.Name = "Taxi - En service";
                    }
                    
                    // Son de notification
                    Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "LOCAL_PLYR_CASH_COUNTER_COMPLETE", "DLC_HEISTS_GENERAL_FRONTEND_SOUNDS", 0);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error completing ride: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Taxi Operations
        
        private void HandleTaxiDriving()
        {
            if (_currentTaxi == null || !_currentTaxi.Exists()) return;
            
            // Logique spécifique au taxi si nécessaire
        }
        
        #endregion
        
        #region Input Handling
        
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (_isOnShift && _isDriving && !_isEndingService)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.M:
                            // Ouvrir/fermer le menu taxi
                            _taxiMenu.Visible = !_taxiMenu.Visible;
                            break;
                            
                        case Keys.End:
                            // Terminer le service rapidement
                            StartEndService();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"TaxiDriver key error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Menu Events
        
        private void OnEndShift(object sender, EventArgs e)
        {
            _taxiMenu.Visible = false;
            StartEndService();
        }
        
        #endregion
        
        #region HUD Display
        
        private void DisplayTaxiHUD()
        {
            if (_currentTaxi == null) return;
            
            // Affichage avec vrais gains de session
            var statusInfo = _hasActiveRide ? "EN COURSE" : "LIBRE";
            var earningsInfo = $"Gains: ${_sessionEarnings}";
            var ridesInfo = $"Courses: {_ridesCompleted}";
            
            var hudText = $"TAXI {statusInfo} | {earningsInfo} | {ridesInfo} | M: Menu | End: Fin";
            Screen.ShowSubtitle(hudText, 100);
        }
        
        #endregion
        
        #region Cleanup
        
        private void OnAborted(object sender, EventArgs e)
        {
            Logger.Info("Taxi Driver Manager is being unloaded.");
            
            try
            {
                EndShift();
                _jobBlip?.Delete();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during TaxiDriver cleanup: {ex.Message}");
            }
        }
        
        #endregion
    }
    
    #region Data Classes
    
    public class TaxiDestination
    {
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        
        public TaxiDestination(string name, Vector3 position)
        {
            Name = name;
            Position = position;
        }
    }
    
    #endregion
}