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
    /// Système de chauffeur de bus avec trajets dynamiques et PNJ passagers
    /// </summary>
    public class BusDriverManager : Script
    {
        #region Fields
        
        private ObjectPool _menuPool;
        private NativeMenu _passengerMenu = null!;
        private NativeItem _acceptPassengerItem = null!;
        private NativeItem _declinePassengerItem = null!;
        private NativeItem _endShiftItem = null!;
        
        private Vehicle? _currentBus;
        private bool _isDriving;
        private bool _isOnShift;
        private List<Ped> _passengers = new();
        private List<BusStop> _busStops = new();
        private List<BusRoute> _availableRoutes = new();
        private BusRoute? _currentRoute;
        private int _currentStopIndex;
        private DateTime _lastStopTime;
        private List<Ped> _waitingPassengers = new(); // Liste pour 8 PNJ
        private Vector3 _jobLocation = new Vector3(-1037.41f, -2737.24f, 20.17f); // Près du terminal de bus à l'aéroport
        private Blip? _jobBlip;
        private Blip? _busBlip;
        private List<Blip> _stopBlips = new();
        
        // HUD Elements
        private bool _showingPassengerRequest;
        private DateTime _passengerRequestTime;
        private string _passengerDestination = "";
        private int _passengerFare = 0;
        
        // Économie
        private int _totalEarnings;
        private int _shiftsCompleted;
        
        // Gestion des portes
        private bool _doorsOpen = false;
        
        #endregion
        
        #region Initialization
        
        public BusDriverManager()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            
            _menuPool = new ObjectPool();
            InitializeMenu();
            InitializeBusStops();
            InitializeBusRoutes();
            CreateJobLocationBlip();
            
            Logger.Info("Bus Driver Manager initialized.");
        }
        
        private void InitializeMenu()
        {
            _passengerMenu = new NativeMenu("Gestion Bus", "Gérer les passagers");
            _menuPool.Add(_passengerMenu);
            
            _acceptPassengerItem = new NativeItem("Accepter le passager", "Accepter ce passager dans le bus");
            _declinePassengerItem = new NativeItem("Refuser le passager", "Refuser ce passager");
            _endShiftItem = new NativeItem("Terminer le service", "Finir votre service de bus");
            
            _passengerMenu.Add(_acceptPassengerItem);
            _passengerMenu.Add(_declinePassengerItem);
            _passengerMenu.Add(_endShiftItem);
            
            _acceptPassengerItem.Activated += OnAcceptPassenger;
            _declinePassengerItem.Activated += OnDeclinePassenger;
            _endShiftItem.Activated += OnEndShift;
        }
        
        private void InitializeBusStops()
        {
            _busStops = new List<BusStop>
            {
                new BusStop("Aéroport International", new Vector3(-1037.0f, -2730.0f, 13.8f), "Terminal Principal"),
                new BusStop("Centre-ville", new Vector3(-256.0f, -715.0f, 33.5f), "Place du Centre"),
                new BusStop("Vinewood", new Vector3(294.0f, 180.0f, 104.4f), "Boulevard de Vinewood"),
                new BusStop("Plage de Vespucci", new Vector3(-1238.0f, -1491.0f, 4.0f), "Front de mer"),
                new BusStop("Port de Los Santos", new Vector3(390.0f, -2627.0f, 6.0f), "Terminal Maritime"),
                new BusStop("Little Seoul", new Vector3(-526.0f, -1211.0f, 18.2f), "Centre Commercial"),
                new BusStop("Paleto Bay", new Vector3(-279.0f, 6226.0f, 31.5f), "Centre-ville"),
                new BusStop("Sandy Shores", new Vector3(1960.0f, 3740.0f, 32.3f), "Station Service"),
                new BusStop("Grapeseed", new Vector3(1699.0f, 4924.0f, 42.1f), "Centre du village"),
                new BusStop("Quartier Résidentiel", new Vector3(-314.0f, -718.0f, 28.0f), "Arrêt Résidentiel")
            };
        }
        
        private void InitializeBusRoutes()
        {
            _availableRoutes = new List<BusRoute>
            {
                new BusRoute("Ligne 1 - Centre", new List<string> 
                { 
                    "Aéroport International", "Centre-ville", "Vinewood", "Centre-ville", "Aéroport International" 
                }),
                new BusRoute("Ligne 2 - Côtière", new List<string> 
                { 
                    "Port de Los Santos", "Plage de Vespucci", "Little Seoul", "Centre-ville", "Port de Los Santos" 
                }),
                new BusRoute("Ligne 3 - Campagne", new List<string> 
                { 
                    "Sandy Shores", "Grapeseed", "Paleto Bay", "Grapeseed", "Sandy Shores" 
                })
            };
        }
        
        private void CreateJobLocationBlip()
        {
            _jobBlip = World.CreateBlip(_jobLocation);
            _jobBlip.Sprite = BlipSprite.Bus;
            _jobBlip.Color = BlipColor.Yellow;
            _jobBlip.Name = "Emploi - Chauffeur de Bus";
            _jobBlip.IsShortRange = false; // Visible sur la carte complète
            _jobBlip.Scale = 0.9f; // Taille réduite pour la minimap
            
            // Ajouter des propriétés pour le rendre plus visible
            Function.Call(Hash.SET_BLIP_AS_MISSION_CREATOR_BLIP, _jobBlip.Handle, true);
            Function.Call(Hash.SET_BLIP_PRIORITY, _jobBlip.Handle, 10); // Haute priorité
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
                else if (_isDriving && _currentBus != null && _currentBus.Exists())
                {
                    HandleBusDriving();
                    CheckBusStops();
                    ManagePassengers();
                    DisplayBusHUD();
                }
                else if (_isOnShift && (_currentBus == null || !_currentBus.Exists()))
                {
                    // Le bus a disparu, terminer le service
                    EndShift();
                }
                
                // Gérer les demandes de passagers
                if (_showingPassengerRequest)
                {
                    HandlePassengerRequest();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"BusDriver tick error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Job Management
        
        private void CheckJobLocation(Ped player)
        {
            float distance = player.Position.DistanceTo(_jobLocation);
            if (distance < 3.0f)
            {
                Screen.ShowSubtitle("~INPUT_CONTEXT~ Commencer votre service de chauffeur de bus", 100);
                
                if (Game.IsControlJustPressed(GTA.Control.Context))
                {
                    StartBusJob();
                }
            }
        }
        
        private void StartBusJob()
        {
            try
            {
                // Effet de fade
                Screen.FadeOut(1000);
                Script.Wait(1000);
                
                // Choisir une route aléatoire
                _currentRoute = _availableRoutes[new Random().Next(_availableRoutes.Count)];
                _currentStopIndex = 0;
                
                // Spawner le bus aux coordonnées exactes
                Vector3 spawnPosition = new Vector3(-1028.18f, -2726.49f, 13.66f);
                float spawnHeading = 238.91f;
                
                // Créer le bus
                var busModel = new Model(VehicleHash.Bus);
                busModel.Request(5000);
                
                if (busModel.IsLoaded)
                {
                    _currentBus = World.CreateVehicle(busModel, spawnPosition, spawnHeading);
                    if (_currentBus != null)
                    {
                        _currentBus.IsPersistent = true;
                        _currentBus.IsEngineRunning = true;
                        
                        // Téléporter le joueur dans le bus
                        Game.Player.Character.Task.WarpIntoVehicle(_currentBus, VehicleSeat.Driver);
                        
                        _isDriving = true;
                        _isOnShift = true;
                        _currentStopIndex = 0;
                        _lastStopTime = DateTime.Now;
                        
                        // Créer le blip du bus
                        _busBlip = _currentBus.AddBlip();
                        _busBlip.Sprite = BlipSprite.Bus;
                        _busBlip.Color = BlipColor.Green;
                        _busBlip.Name = $"Bus - {_currentRoute.Name}";
                        
                        // Créer les blips des arrêts
                        CreateStopBlips();
                        
                        // Spawner des passagers près du lieu de spawn initial du bus
                        SpawnInitialPassengers(spawnPosition);
                        
                        Notification.PostTicker($"~g~Service commencé ! Route : {_currentRoute.Name}", false, true);
                        Logger.Info($"Bus job started on route: {_currentRoute.Name}");
                    }
                }
                
                busModel.MarkAsNoLongerNeeded();
                
                Screen.FadeIn(1000);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting bus job: {ex.Message}");
                Screen.FadeIn(1000);
            }
        }
        
        private void SpawnInitialPassengers(Vector3 busSpawnPosition)
        {
            try
            {
                // Nettoyer les anciens passagers en attente s'il y en a
                foreach (var oldPassenger in _waitingPassengers)
                {
                    if (oldPassenger != null && oldPassenger.Exists())
                    {
                        oldPassenger.MarkAsNoLongerNeeded();
                    }
                }
                _waitingPassengers.Clear();
                
                // Position de base pour spawner les PNJ (coordonnées spécifiques)
                var basePosition = new Vector3(-1032.73f, -2731f, 13.76f);
                var pedHeading = 58.91f; // Heading vers le bus
                
                // Différents modèles de PNJ pour la variété
                var pedModels = new PedHash[]
                {
                    PedHash.Business01AMY,
                    PedHash.Business02AMY,
                    PedHash.Business03AMY,
                    PedHash.Hipster01AMY,
                    PedHash.Hipster02AMY,
                    PedHash.Hipster03AMY,
                    PedHash.Golfer01AMY,
                    PedHash.GenCasPat01AMY
                };
                
                // Spawner 6 PNJ près du lieu de spawn du bus
                for (int i = 0; i < 6; i++)
                {
                    // Variation de position pour éviter qu'ils soient tous au même endroit
                    var offsetX = (i % 3) * 1.2f - 1.2f; // Répartition sur X (-1.2, 0, 1.2)
                    var offsetY = (i / 3) * 1.5f - 0.75f; // Répartition sur Y (-0.75, 0.75)
                    var pedPosition = new Vector3(basePosition.X + offsetX, basePosition.Y + offsetY, basePosition.Z);
                    
                    var pedModel = new Model(pedModels[i]);
                    
                    if (pedModel.Request(2000))
                    {
                        var waitingPassenger = World.CreatePed(pedModel, pedPosition, pedHeading);
                        if (waitingPassenger != null)
                        {
                            waitingPassenger.IsPersistent = true;
                            waitingPassenger.BlockPermanentEvents = true;
                            
                            // Faire attendre le PNJ avec le bon heading
                            waitingPassenger.Heading = pedHeading;
                            waitingPassenger.Task.StartScenarioInPlace("WORLD_HUMAN_WAITING", 0, true);
                            
                            // Faire regarder le PNJ vers le bus
                            if (_currentBus != null && _currentBus.Exists())
                            {
                                waitingPassenger.Task.LookAt(_currentBus, 8000);
                            }
                            
                            _waitingPassengers.Add(waitingPassenger);
                        }
                    }
                    
                    pedModel.MarkAsNoLongerNeeded();
                }
                
                if (_waitingPassengers.Count > 0)
                {
                    // Générer une destination et un prix pour les passagers initiaux
                    GeneratePassengerRequest();
                    
                    _showingPassengerRequest = true;
                    _passengerRequestTime = DateTime.Now;
                    
                    // Ouvrir automatiquement le menu
                    _passengerMenu.Visible = true;
                    
                    Screen.ShowSubtitle($"~y~{_waitingPassengers.Count} passagers attendent près du bus ! Menu ouvert automatiquement.", 5000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning initial passengers: {ex.Message}");
            }
        }
        
        private void CreateStopBlips()
        {
            foreach (var stopName in _currentRoute!.StopNames)
            {
                var stop = _busStops.FirstOrDefault(s => s.Name == stopName);
                if (stop != null)
                {
                    var blip = World.CreateBlip(stop.Position);
                    blip.Sprite = BlipSprite.Bus;
                    blip.Color = BlipColor.Blue;
                    blip.Name = $"Arrêt: {stop.Name}";
                    blip.IsShortRange = true;
                    _stopBlips.Add(blip);
                }
            }
        }
        
        private void EndShift()
        {
            try
            {
                _isDriving = false;
                _isOnShift = false;
                
                // Nettoyer les passagers
                foreach (var passenger in _passengers)
                {
                    if (passenger != null && passenger.Exists())
                    {
                        passenger.Task.LeaveVehicle();
                        passenger.MarkAsNoLongerNeeded();
                    }
                }
                _passengers.Clear();
                
                // Nettoyer les blips
                _busBlip?.Delete();
                foreach (var blip in _stopBlips)
                {
                    blip?.Delete();
                }
                _stopBlips.Clear();
                
                // Supprimer le bus
                if (_currentBus != null && _currentBus.Exists())
                {
                    _currentBus.Delete();
                }
                
                _currentBus = null;
                _currentRoute = null;
                _waitingPassengers.Clear();
                _showingPassengerRequest = false;
                _doorsOpen = false;
                
                // Afficher les gains
                var message = $"Service terminé ! Gains: ${_totalEarnings} | Services: {++_shiftsCompleted}";
                Notification.PostTicker($"~g~{message}", false, true);
                
                Logger.Info($"Bus shift ended. Total earnings: {_totalEarnings}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ending shift: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Bus Operations
        
        private void HandleBusDriving()
        {
            if (_currentBus == null || !_currentBus.Exists()) return;
            
            // Logique de conduite automatique basique si le joueur ne conduit pas activement
            if (_currentBus.Speed < 5.0f && Game.Player.Character.IsInVehicle(_currentBus))
            {
                // Le bus s'arrête trop longtemps, donner un petit coup de pouce
                var nextStop = GetNextStop();
                if (nextStop != null)
                {
                    var direction = (nextStop.Position - _currentBus.Position).Normalized;
                    // Pas de conduite automatique forcée, laisser le joueur conduire
                }
            }
        }
        
        private void CheckBusStops()
        {
            if (_currentRoute == null || _currentBus == null) return;
            
            var nextStop = GetNextStop();
            if (nextStop == null) return;
            
            float distance = _currentBus.Position.DistanceTo(nextStop.Position);
            if (distance < 15.0f && _currentBus.Speed < 10.0f)
            {
                // Arrivé à un arrêt
                HandleStopArrival(nextStop);
            }
        }
        
        private BusStop? GetNextStop()
        {
            if (_currentRoute == null || _currentStopIndex >= _currentRoute.StopNames.Count)
                return null;
                
            var stopName = _currentRoute.StopNames[_currentStopIndex];
            return _busStops.FirstOrDefault(s => s.Name == stopName);
        }
        
        private void HandleStopArrival(BusStop stop)
        {
            if ((DateTime.Now - _lastStopTime).TotalSeconds < 10) return; // Éviter les répétitions
            
            _lastStopTime = DateTime.Now;
            
            Screen.ShowSubtitle($"~g~Arrêt: {stop.Name} - {stop.Description}", 3000);
            
            // Faire descendre certains passagers
            DropOffPassengers();
            
            // Spawner de nouveaux passagers potentiels
            SpawnWaitingPassengers(stop);
            
            // Passer au prochain arrêt
            _currentStopIndex = (_currentStopIndex + 1) % _currentRoute!.StopNames.Count;
        }
        
        #endregion
        
        #region Passenger Management
        
        private void SpawnWaitingPassengers(BusStop stop)
        {
            // Nettoyer les anciens passagers en attente
            foreach (var oldPassenger in _waitingPassengers)
            {
                if (oldPassenger != null && oldPassenger.Exists())
                {
                    oldPassenger.MarkAsNoLongerNeeded();
                }
            }
            _waitingPassengers.Clear();
            
            // Spawner 8 PNJ à l'arrêt de bus actuel
            var basePosition = stop.Position; // Utiliser la position de l'arrêt actuel
            
            // Si c'est l'arrêt de l'aéroport, utiliser les coordonnées spécifiques
            if (stop.Name == "Aéroport International")
            {
                basePosition = new Vector3(-1032.73f, -2731f, 13.76f); // Coordonnées spécifiques pour l'aéroport
            }
            
            var pedHeading = 0f; // Heading vers la route
            
            // Différents modèles de PNJ pour la variété
            var pedModels = new PedHash[]
            {
                PedHash.Business01AMY,
                PedHash.Business02AMY,
                PedHash.Business03AMY,
                PedHash.Hipster01AMY,
                PedHash.Hipster02AMY,
                PedHash.Hipster03AMY,
                PedHash.Golfer01AMY,
                PedHash.GenCasPat01AMY
            };
            
            for (int i = 0; i < 8; i++)
            {
                // Petite variation de position pour éviter qu'ils soient tous au même endroit
                var offsetX = (i % 4) * 0.5f - 1.0f; // Répartition sur X
                var offsetY = (i / 4) * 0.5f - 0.25f; // Répartition sur Y
                var pedPosition = new Vector3(basePosition.X + offsetX, basePosition.Y + offsetY, basePosition.Z);
                
                var pedModel = new Model(pedModels[i]);
                
                if (pedModel.Request(2000))
                {
                    var waitingPassenger = World.CreatePed(pedModel, pedPosition, pedHeading);
                    if (waitingPassenger != null)
                    {
                        waitingPassenger.IsPersistent = true;
                        waitingPassenger.BlockPermanentEvents = true;
                        
                        // Faire attendre le PNJ avec le bon heading
                        waitingPassenger.Heading = pedHeading;
                        waitingPassenger.Task.StartScenarioInPlace("WORLD_HUMAN_WAITING", 0, true);
                        
                        // Faire regarder le PNJ vers le bus si il existe
                        if (_currentBus != null && _currentBus.Exists())
                        {
                            waitingPassenger.Task.LookAt(_currentBus, 5000);
                        }
                        
                        _waitingPassengers.Add(waitingPassenger);
                    }
                }
                
                pedModel.MarkAsNoLongerNeeded();
            }
            
            if (_waitingPassengers.Count > 0)
            {
                // Générer une destination et un prix
                GeneratePassengerRequest();
                
                _showingPassengerRequest = true;
                _passengerRequestTime = DateTime.Now;
                
                // Ouvrir automatiquement le menu
                _passengerMenu.Visible = true;
                Screen.ShowSubtitle($"~y~{_waitingPassengers.Count} passagers à l'arrêt ! Menu ouvert automatiquement.", 3000);
            }
        }
        
        private void GeneratePassengerRequest()
        {
            var possibleDestinations = _currentRoute!.StopNames.Where(s => s != _currentRoute.StopNames[_currentStopIndex]).ToList();
            if (possibleDestinations.Any())
            {
                _passengerDestination = possibleDestinations[new Random().Next(possibleDestinations.Count)];
                _passengerFare = new Random().Next(10, 50); // $10-50 selon la distance
            }
        }
        
        private void HandlePassengerRequest()
        {
            if ((DateTime.Now - _passengerRequestTime).TotalSeconds > 15)
            {
                // Timeout - tous les passagers partent
                foreach (var waitingPassenger in _waitingPassengers)
                {
                    if (waitingPassenger != null && waitingPassenger.Exists())
                    {
                        waitingPassenger.Task.Wander();
                        waitingPassenger.MarkAsNoLongerNeeded();
                    }
                }
                _showingPassengerRequest = false;
                _waitingPassengers.Clear();
            }
        }
        
        private void ManagePassengers()
        {
            // Nettoyer les passagers qui ne sont plus dans le bus
            _passengers.RemoveAll(p => p == null || !p.Exists() || !p.IsInVehicle(_currentBus));
        }
        
        private void DropOffPassengers()
        {
            if (_passengers.Count == 0) return;
            
            var passengersToRemove = new List<Ped>();
            int totalFareEarned = 0;
            
            // Faire descendre 50% des passagers à chaque arrêt (minimum 1 si il y en a)
            int passengersToDropOff = Math.Max(1, _passengers.Count / 2);
            
            for (int i = 0; i < passengersToDropOff && i < _passengers.Count; i++)
            {
                var passenger = _passengers[i];
                if (passenger != null && passenger.Exists())
                {
                    // Méthode directe : retirer le PNJ du véhicule
                    passenger.Task.ClearAllImmediately();
                    
                    // Téléporter le PNJ hors du véhicule
                    if (_currentBus != null && _currentBus.Exists())
                    {
                        var exitPosition = _currentBus.Position + _currentBus.RightVector * 3.0f;
                        passenger.Position = exitPosition;
                    }
                    
                    // Faire marcher le PNJ
                    passenger.Task.Wander();
                    passenger.MarkAsNoLongerNeeded();
                    passengersToRemove.Add(passenger);
                    
                    // Gagner de l'argent pour chaque passager qui descend
                    int fare = new Random().Next(20, 45);
                    totalFareEarned += fare;
                }
            }
            
            // Retirer les passagers de la liste
            foreach (var passenger in passengersToRemove)
            {
                _passengers.Remove(passenger);
            }
            
            // Mettre à jour les gains
            if (totalFareEarned > 0)
            {
                _totalEarnings += totalFareEarned;
                Game.Player.Money += totalFareEarned;
                
                Screen.ShowSubtitle($"~g~{passengersToRemove.Count} passager(s) descendu(s) - Gain total: ${totalFareEarned} | Total session: ${_totalEarnings}", 4000);
            }
        }
        
        #endregion
        
        #region Menu Events
        
        private void OnAcceptPassenger(object sender, EventArgs e)
        {
            if (_waitingPassengers.Count > 0 && _currentBus != null)
            {
                // Ouvrir automatiquement les portes si elles ne le sont pas
                if (!_doorsOpen)
                {
                    ToggleBusDoors();
                    Script.Wait(500); // Attendre que les portes s'ouvrent
                }
                
                // Faire monter autant de passagers que possible (jusqu'à 8 maximum)
                int passengersToBoard = Math.Min(_waitingPassengers.Count, 8 - _passengers.Count);
                var passengersBoarded = new List<Ped>();
                
                // Sièges disponibles dans l'ordre de priorité
                var availableSeats = new[] { 
                    VehicleSeat.Passenger, 
                    VehicleSeat.LeftRear, 
                    VehicleSeat.RightRear,
                    VehicleSeat.ExtraSeat1,
                    VehicleSeat.ExtraSeat2,
                    VehicleSeat.ExtraSeat3,
                    VehicleSeat.ExtraSeat4,
                    VehicleSeat.ExtraSeat5
                };
                
                int seatIndex = 0;
                for (int i = 0; i < passengersToBoard && seatIndex < availableSeats.Length; i++)
                {
                    var waitingPassenger = _waitingPassengers[i];
                    if (waitingPassenger != null && waitingPassenger.Exists())
                    {
                        // Trouver le prochain siège libre
                        VehicleSeat targetSeat = VehicleSeat.None;
                        for (int j = seatIndex; j < availableSeats.Length; j++)
                        {
                            if (_currentBus.IsSeatFree(availableSeats[j]))
                            {
                                targetSeat = availableSeats[j];
                                seatIndex = j + 1;
                                break;
                            }
                        }
                        
                        if (targetSeat != VehicleSeat.None)
                        {
                            // Méthode directe : téléporter le PNJ dans le véhicule
                            waitingPassenger.Task.ClearAllImmediately();
                            waitingPassenger.SetIntoVehicle(_currentBus, targetSeat);
                            _passengers.Add(waitingPassenger);
                            passengersBoarded.Add(waitingPassenger);
                        }
                        else
                        {
                            break; // Plus de sièges libres
                        }
                    }
                }
                
                // Retirer les passagers qui montent du bus de la liste d'attente
                foreach (var passenger in passengersBoarded)
                {
                    _waitingPassengers.Remove(passenger);
                }
                
                if (passengersBoarded.Count > 0)
                {
                    var totalFare = _passengerFare * passengersBoarded.Count;
                    _totalEarnings += totalFare;
                    Game.Player.Money += totalFare;
                    Screen.ShowSubtitle($"~g~{passengersBoarded.Count} passager(s) accepté(s) - Destination: {_passengerDestination} - Gain: ${totalFare}", 4000);
                }
                
                if (_waitingPassengers.Count == 0)
                {
                    _showingPassengerRequest = false;
                }
                
                if (passengersBoarded.Count < passengersToBoard)
                {
                    Screen.ShowSubtitle("~r~Bus complet !", 2000);
                }
            }
            
            _passengerMenu.Visible = false;
        }
        
        private void OnDeclinePassenger(object sender, EventArgs e)
        {
            foreach (var waitingPassenger in _waitingPassengers)
            {
                if (waitingPassenger != null && waitingPassenger.Exists())
                {
                    waitingPassenger.Task.Wander();
                    waitingPassenger.MarkAsNoLongerNeeded();
                }
            }
            
            _showingPassengerRequest = false;
            _waitingPassengers.Clear();
            _passengerMenu.Visible = false;
            
            Screen.ShowSubtitle("~y~Passagers refusés", 2000);
        }
        
        private void OnEndShift(object sender, EventArgs e)
        {
            _passengerMenu.Visible = false;
            EndShift();
        }
        
        private VehicleSeat GetFreeSeat()
        {
            if (_currentBus == null) return VehicleSeat.None;
            
            // Vérifier les sièges passagers (ignorer le conducteur)
            // Les bus ont généralement plusieurs sièges, on teste les principaux
            var seatsToCheck = new[] { VehicleSeat.Passenger, VehicleSeat.LeftRear, VehicleSeat.RightRear };
            
            foreach (var seat in seatsToCheck)
            {
                if (_currentBus.IsSeatFree(seat))
                {
                    return seat;
                }
            }
            
            return VehicleSeat.None;
        }
        
        #endregion
        
        #region Door Management
        
        private void ToggleBusDoors()
        {
            if (_currentBus == null || !_currentBus.Exists()) return;
            
            if (_doorsOpen)
            {
                // Fermer les portes
                Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, _currentBus.Handle, 0, false); // Porte avant gauche
                Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, _currentBus.Handle, 1, false); // Porte avant droite
                Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, _currentBus.Handle, 2, false); // Porte arrière gauche
                Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, _currentBus.Handle, 3, false); // Porte arrière droite
                _doorsOpen = false;
                Screen.ShowSubtitle("~r~Portes fermées", 1500);
            }
            else
            {
                // Ouvrir les portes
                Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, _currentBus.Handle, 0, false, false); // Porte avant gauche
                Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, _currentBus.Handle, 1, false, false); // Porte avant droite
                Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, _currentBus.Handle, 2, false, false); // Porte arrière gauche
                Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, _currentBus.Handle, 3, false, false); // Porte arrière droite
                _doorsOpen = true;
                Screen.ShowSubtitle("~g~Portes ouvertes", 1500);
                
                // Faire monter automatiquement les passagers en attente
                if (_waitingPassengers.Count > 0)
                {
                    AutoBoardPassengers();
                }
            }
        }
        
        private void AutoBoardPassengers()
        {
            // Ne plus faire de montée automatique - juste ouvrir le menu
            if (_waitingPassengers.Count > 0 && !_passengerMenu.Visible)
            {
                _passengerMenu.Visible = true;
                Screen.ShowSubtitle("~y~Des passagers veulent monter ! Utilisez le menu pour accepter ou refuser.", 3000);
            }
        }
        
        #endregion
        
        #region Input Handling
        
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (_isOnShift && _isDriving)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.N:
                            if (_showingPassengerRequest)
                            {
                                _passengerMenu.Visible = !_passengerMenu.Visible;
                            }
                            break;
                            
                        case Keys.H:
                            // Klaxon du bus
                            if (_currentBus != null && _currentBus.Exists())
                            {
                                _currentBus.SoundHorn(1000);
                            }
                            break;
                            
                        case Keys.End:
                            // Terminer le service rapidement
                            EndShift();
                            break;
                            
                        case Keys.O:
                            // Ouvrir/Fermer les portes du bus
                            ToggleBusDoors();
                            break;
                            
                        case Keys.P:
                            // Forcer la descente des passagers (pour test)
                            if (_passengers.Count > 0)
                            {
                                DropOffPassengers();
                            }
                            else
                            {
                                Screen.ShowSubtitle("~r~Aucun passager dans le bus", 2000);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"BusDriver key error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region HUD Display
        
        private void DisplayBusHUD()
        {
            if (_currentBus == null || _currentRoute == null) return;
            
            // Informations de base
            var nextStop = GetNextStop();
            var routeInfo = $"Route: {_currentRoute.Name}";
            var stopInfo = nextStop != null ? $"Prochain arrêt: {nextStop.Name}" : "Fin de route";
            var passengerInfo = $"Passagers: {_passengers.Count}/8";
            var earningsInfo = $"Gains: ${_totalEarnings}";
            var doorsInfo = _doorsOpen ? "Portes: OUVERTES" : "Portes: FERMÉES";
            
            // Afficher les informations - utiliser Screen.ShowSubtitle qui est plus simple
            var hudText = $"{routeInfo} | {stopInfo} | {passengerInfo} | {earningsInfo} | {doorsInfo}";
            
            // Demande de passager
            if (_showingPassengerRequest)
            {
                var doorsStatus = _doorsOpen ? "" : " | ~r~OUVREZ LES PORTES~w~";
                var requestText = $"~y~DEMANDE PASSAGER~w~ - Destination: {_passengerDestination} - Prix: ${_passengerFare} - N: Menu | O: Portes{doorsStatus}";
                Screen.ShowSubtitle(requestText, 100);
            }
            else
            {
                var controlsText = " | N: Menu | H: Klaxon | O: Portes | P: Descendre passagers | End: Fin";
                Screen.ShowSubtitle(hudText + controlsText, 100);
            }
        }
        
        #endregion
        
        #region Cleanup
        
        private void OnAborted(object sender, EventArgs e)
        {
            Logger.Info("Bus Driver Manager is being unloaded.");
            
            try
            {
                EndShift();
                _jobBlip?.Delete();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during BusDriver cleanup: {ex.Message}");
            }
        }
        
        #endregion
    }
    
    #region Data Classes
    
    public class BusStop
    {
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public string Description { get; set; }
        
        public BusStop(string name, Vector3 position, string description)
        {
            Name = name;
            Position = position;
            Description = description;
        }
    }
    
    public class BusRoute
    {
        public string Name { get; set; }
        public List<string> StopNames { get; set; }
        
        public BusRoute(string name, List<string> stopNames)
        {
            Name = name;
            StopNames = stopNames;
        }
    }
    
    #endregion
}