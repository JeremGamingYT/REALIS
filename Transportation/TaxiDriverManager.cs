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
        private NativeMenu _clientMenu = null!;
        private NativeItem _acceptClientItem = null!;
        private NativeItem _declineClientItem = null!;
        private NativeItem _endShiftItem = null!;
        
        private Vehicle? _currentTaxi;
        private bool _isDriving;
        private bool _isOnShift;
        private Ped? _currentClient;
        private bool _hasActiveRide;
        private Vector3 _clientDestination;
        private string _destinationName = "";
        private int _rideFare = 0;
        private List<Ped> _waitingClients = new();
        private Vector3 _jobLocation = new Vector3(907.47f, -177.23f, 74.22f); // Downtown Cab Co.
        private Blip? _jobBlip;
        private Blip? _taxiBlip;
        private Blip? _destinationBlip;
        private List<Blip> _clientBlips = new();
        
        // HUD Elements
        private bool _showingClientRequest;
        private DateTime _clientRequestTime;
        private string _clientPickupLocation = "";
        
        // Économie
        private int _totalEarnings;
        private int _ridesCompleted;
        private float _totalDistance;
        
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
            _clientMenu = new NativeMenu("Gestion Taxi", "Gérer les clients");
            _menuPool.Add(_clientMenu);
            
            _acceptClientItem = new NativeItem("Accepter le client", "Accepter ce client dans le taxi");
            _declineClientItem = new NativeItem("Refuser le client", "Refuser ce client");
            _endShiftItem = new NativeItem("Terminer le service", "Finir votre service de taxi");
            
            _clientMenu.Add(_acceptClientItem);
            _clientMenu.Add(_declineClientItem);
            _clientMenu.Add(_endShiftItem);
            
            _acceptClientItem.Activated += OnAcceptClient;
            _declineClientItem.Activated += OnDeclineClient;
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
                else if (_isDriving && _currentTaxi != null && _currentTaxi.Exists())
                {
                    HandleTaxiDriving();
                    CheckForRandomClients();
                    ManageCurrentRide();
                    DisplayTaxiHUD();
                }
                else if (_isOnShift && (_currentTaxi == null || !_currentTaxi.Exists()))
                {
                    // Le taxi a disparu, terminer le service
                    EndShift();
                }
                
                // Gérer les demandes de clients
                if (_showingClientRequest)
                {
                    HandleClientRequest();
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
                Screen.FadeOut(1000);
                Script.Wait(1000);
                
                // Choisir une position de spawn pour le taxi
                Vector3 spawnPosition = new Vector3(903.47f, -191.23f, 73.22f);
                float spawnHeading = 58.0f;
                
                // Créer le taxi
                var taxiModel = new Model(VehicleHash.Taxi);
                taxiModel.Request(5000);
                
                if (taxiModel.IsLoaded)
                {
                    _currentTaxi = World.CreateVehicle(taxiModel, spawnPosition, spawnHeading);
                    if (_currentTaxi != null)
                    {
                        _currentTaxi.IsPersistent = true;
                        _currentTaxi.IsEngineRunning = true;
                        _currentTaxi.IsTaxiLightOn = true; // Allumer le signe taxi
                        
                        // Téléporter le joueur dans le taxi
                        Game.Player.Character.Task.WarpIntoVehicle(_currentTaxi, VehicleSeat.Driver);
                        
                        _isDriving = true;
                        _isOnShift = true;
                        _hasActiveRide = false;
                        
                        // Créer le blip du taxi
                        _taxiBlip = _currentTaxi.AddBlip();
                        _taxiBlip.Sprite = BlipSprite.Store;
                        _taxiBlip.Color = BlipColor.Green;
                        _taxiBlip.Name = "Taxi - En service";
                        
                        Notification.PostTicker("~g~Service de taxi commencé ! Cherchez des clients.", false, true);
                        Logger.Info("Taxi job started");
                        
                        // Spawner un premier client près du lieu de travail
                        SpawnNearbyClient();
                    }
                }
                
                taxiModel.MarkAsNoLongerNeeded();
                Screen.FadeIn(1000);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting taxi job: {ex.Message}");
                Screen.FadeIn(1000);
            }
        }
        
        private void EndShift()
        {
            try
            {
                _isDriving = false;
                _isOnShift = false;
                _hasActiveRide = false;
                
                // Nettoyer le client actuel
                if (_currentClient != null && _currentClient.Exists())
                {
                    _currentClient.Task.LeaveVehicle();
                    _currentClient.MarkAsNoLongerNeeded();
                }
                _currentClient = null;
                
                // Nettoyer les clients en attente
                foreach (var client in _waitingClients)
                {
                    if (client != null && client.Exists())
                    {
                        client.MarkAsNoLongerNeeded();
                    }
                }
                _waitingClients.Clear();
                
                // Nettoyer les blips
                _taxiBlip?.Delete();
                _destinationBlip?.Delete();
                foreach (var blip in _clientBlips)
                {
                    blip?.Delete();
                }
                _clientBlips.Clear();
                
                // Supprimer le taxi
                if (_currentTaxi != null && _currentTaxi.Exists())
                {
                    _currentTaxi.Delete();
                }
                
                _currentTaxi = null;
                _showingClientRequest = false;
                
                // Afficher les statistiques
                var avgDistance = _ridesCompleted > 0 ? _totalDistance / _ridesCompleted : 0;
                var message = $"Service terminé ! Gains: ${_totalEarnings} | Courses: {_ridesCompleted} | Distance moy.: {avgDistance:F1}m";
                Notification.PostTicker($"~g~{message}", false, true);
                
                Logger.Info($"Taxi shift ended. Earnings: {_totalEarnings}, Rides: {_ridesCompleted}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ending taxi shift: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Client Management
        
        private void CheckForRandomClients()
        {
            if (_hasActiveRide || _showingClientRequest) return;
            
            // Chance de spawner un client toutes les 30 secondes environ
            if (new Random().Next(0, 1800) < 1) // ~0.055% chance par tick
            {
                SpawnNearbyClient();
            }
        }
        
        private void SpawnNearbyClient()
        {
            try
            {
                if (_currentTaxi == null || !_currentTaxi.Exists()) return;
                
                // Nettoyer les anciens clients en attente
                foreach (var oldClient in _waitingClients)
                {
                    if (oldClient != null && oldClient.Exists())
                    {
                        oldClient.MarkAsNoLongerNeeded();
                    }
                }
                _waitingClients.Clear();
                
                // Nettoyer les anciens blips
                foreach (var blip in _clientBlips)
                {
                    blip?.Delete();
                }
                _clientBlips.Clear();
                
                // Trouver une position près du taxi
                Vector3 playerPosition = _currentTaxi.Position;
                Vector3 spawnPosition = GetRandomPositionNearby(playerPosition, 50f, 150f);
                
                // Modèles de clients variés
                var clientModels = new PedHash[]
                {
                    PedHash.Business01AMY, PedHash.Business02AMY, PedHash.Business03AMY,
                    PedHash.Hipster01AMY, PedHash.Hipster02AMY, PedHash.Hipster03AMY,
                    PedHash.Golfer01AMY, PedHash.GenCasPat01AMY, PedHash.Beach01AMY,
                    PedHash.StrPunk01GMY, PedHash.StrPunk02GMY, PedHash.Beachvesp01AMY
                };
                
                var randomModel = clientModels[new Random().Next(clientModels.Length)];
                var clientModel = new Model(randomModel);
                
                if (clientModel.Request(2000))
                {
                    var client = World.CreatePed(clientModel, spawnPosition);
                    if (client != null)
                    {
                        client.IsPersistent = true;
                        client.BlockPermanentEvents = true;
                        
                        // Faire attendre le client
                        client.Task.StartScenarioInPlace("WORLD_HUMAN_HAIL_TAXI", 0, true);
                        
                        // Créer un blip temporaire pour le client
                        var clientBlip = client.AddBlip();
                        clientBlip.Sprite = BlipSprite.Standard;
                        clientBlip.Color = BlipColor.Green;
                        clientBlip.Name = "Client Taxi";
                        clientBlip.Scale = 0.8f;
                        _clientBlips.Add(clientBlip);
                        
                        _waitingClients.Add(client);
                        
                        // Générer la demande de course
                        GenerateRideRequest();
                        
                        _showingClientRequest = true;
                        _clientRequestTime = DateTime.Now;
                        _clientPickupLocation = GetNearestLocationName(spawnPosition);
                        
                        // Ouvrir le menu automatiquement
                        _clientMenu.Visible = true;
                        
                        Screen.ShowSubtitle($"~g~Nouveau client ! Lieu: {_clientPickupLocation}", 4000);
                        
                        // Notification sonore
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Menu_Accept", "Phone_SoundSet_Default", 0);
                    }
                }
                
                clientModel.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning client: {ex.Message}");
            }
        }
        
        private Vector3 GetRandomPositionNearby(Vector3 center, float minDistance, float maxDistance)
        {
            var random = new Random();
            var angle = random.NextDouble() * 2 * Math.PI;
            var distance = minDistance + random.NextDouble() * (maxDistance - minDistance);
            
            var x = center.X + (float)(Math.Cos(angle) * distance);
            var y = center.Y + (float)(Math.Sin(angle) * distance);
            
            // Trouver une position valide sur le sol
            var groundZ = 0f;
            Function.Call(Hash.GET_GROUND_Z_FOR_3D_COORD, x, y, center.Z + 10f, groundZ, 0);
            return new Vector3(x, y, groundZ + 1.0f);
        }
        
        private void GenerateRideRequest()
        {
            var destination = _popularDestinations[new Random().Next(_popularDestinations.Count)];
            _clientDestination = destination.Position;
            _destinationName = destination.Name;
            
            // Calculer le tarif basé sur la distance
            float distance = _currentTaxi != null ? _currentTaxi.Position.DistanceTo(_clientDestination) : 1000f;
            _rideFare = CalculateFare(distance);
        }
        
        private int CalculateFare(float distance)
        {
            // Tarif de base + prix par distance
            int baseFare = 15; // $15 de base
            int distanceFare = (int)(distance / 100f * 5); // $5 par 100m
            return Math.Max(baseFare + distanceFare, 20); // Minimum $20
        }
        
        private string GetNearestLocationName(Vector3 position)
        {
            var nearestDestination = _popularDestinations
                .OrderBy(d => d.Position.DistanceTo(position))
                .FirstOrDefault();
            
            return nearestDestination?.Name ?? "Lieu inconnu";
        }
        
        private void HandleClientRequest()
        {
            if ((DateTime.Now - _clientRequestTime).TotalSeconds > 20)
            {
                // Timeout - le client part
                foreach (var client in _waitingClients)
                {
                    if (client != null && client.Exists())
                    {
                        client.Task.Wander();
                        client.MarkAsNoLongerNeeded();
                    }
                }
                foreach (var blip in _clientBlips)
                {
                    blip?.Delete();
                }
                _showingClientRequest = false;
                _waitingClients.Clear();
                _clientBlips.Clear();
                Screen.ShowSubtitle("~r~Le client est parti...", 3000);
            }
        }
        
        private void ManageCurrentRide()
        {
            if (!_hasActiveRide || _currentClient == null || !_currentClient.Exists()) return;
            
            // Vérifier si on est arrivé à destination
            if (_currentTaxi != null && _currentTaxi.Position.DistanceTo(_clientDestination) < 15.0f)
            {
                CompleteRide();
            }
        }
        
        private void CompleteRide()
        {
            try
            {
                if (_currentClient != null && _currentClient.Exists())
                {
                    // Calculer la distance réelle parcourue
                    float actualDistance = _currentTaxi?.Position.DistanceTo(_clientDestination) ?? 0f;
                    
                    // Faire descendre le client
                    _currentClient.Task.LeaveVehicle();
                    _currentClient.Task.Wander();
                    _currentClient.MarkAsNoLongerNeeded();
                    
                    // Mettre à jour les statistiques
                    _totalEarnings += _rideFare;
                    _ridesCompleted++;
                    _totalDistance += actualDistance;
                    
                    // Payer le joueur
                    Game.Player.Money += _rideFare;
                    
                    // Nettoyer
                    _destinationBlip?.Delete();
                    _destinationBlip = null;
                    _currentClient = null;
                    _hasActiveRide = false;
                    
                    // Activer le signe taxi
                    if (_currentTaxi != null && _currentTaxi.Exists())
                    {
                        _currentTaxi.IsTaxiLightOn = true;
                        _taxiBlip.Color = BlipColor.Green;
                        _taxiBlip.Name = "Taxi - En service";
                    }
                    
                    Screen.ShowSubtitle($"~g~Course terminée ! Gain: ${_rideFare} | Total: ${_totalEarnings}", 4000);
                    
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
        
        #region Menu Events
        
        private void OnAcceptClient(object sender, EventArgs e)
        {
            if (_waitingClients.Count > 0 && _currentTaxi != null)
            {
                var client = _waitingClients.FirstOrDefault();
                if (client != null && client.Exists())
                {
                    // Téléporter le client dans le taxi
                    client.Task.ClearAllImmediately();
                    client.SetIntoVehicle(_currentTaxi, VehicleSeat.Passenger);
                    _currentClient = client;
                    _hasActiveRide = true;
                    
                    // Créer le blip de destination
                    _destinationBlip = World.CreateBlip(_clientDestination);
                    _destinationBlip.Sprite = BlipSprite.Standard;
                    _destinationBlip.Color = BlipColor.Yellow;
                    _destinationBlip.Name = $"Destination: {_destinationName}";
                    Function.Call(Hash.SET_BLIP_ROUTE, _destinationBlip.Handle, true);
                    
                    // Désactiver le signe taxi
                    _currentTaxi.IsTaxiLightOn = false;
                    _taxiBlip.Color = BlipColor.Red;
                    _taxiBlip.Name = "Taxi - Occupé";
                    
                    Screen.ShowSubtitle($"~g~Client embarqué ! Destination: {_destinationName} - Tarif: ${_rideFare}", 4000);
                }
                
                // Nettoyer les autres clients en attente
                foreach (var otherClient in _waitingClients.Where(c => c != client))
                {
                    if (otherClient != null && otherClient.Exists())
                    {
                        otherClient.MarkAsNoLongerNeeded();
                    }
                }
                
                // Nettoyer tous les blips
                foreach (var blip in _clientBlips)
                {
                    blip?.Delete();
                }
                _clientBlips.Clear();
                
                _waitingClients.Clear();
                _showingClientRequest = false;
            }
            
            _clientMenu.Visible = false;
        }
        
        private void OnDeclineClient(object sender, EventArgs e)
        {
            foreach (var client in _waitingClients)
            {
                if (client != null && client.Exists())
                {
                    client.Task.Wander();
                    client.MarkAsNoLongerNeeded();
                }
            }
            
            foreach (var blip in _clientBlips)
            {
                blip?.Delete();
            }
            
            _showingClientRequest = false;
            _waitingClients.Clear();
            _clientBlips.Clear();
            _clientMenu.Visible = false;
            
            Screen.ShowSubtitle("~y~Client refusé", 2000);
        }
        
        private void OnEndShift(object sender, EventArgs e)
        {
            _clientMenu.Visible = false;
            EndShift();
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
                if (_isOnShift && _isDriving)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.N:
                            if (_showingClientRequest)
                            {
                                _clientMenu.Visible = !_clientMenu.Visible;
                            }
                            break;
                            
                        case Keys.H:
                            // Klaxon du taxi
                            if (_currentTaxi != null && _currentTaxi.Exists())
                            {
                                _currentTaxi.SoundHorn(1000);
                            }
                            break;
                            
                        case Keys.End:
                            // Terminer le service rapidement
                            EndShift();
                            break;
                            
                        case Keys.T:
                            // Toggle taxi light
                            if (_currentTaxi != null && _currentTaxi.Exists() && !_hasActiveRide)
                            {
                                _currentTaxi.IsTaxiLightOn = !_currentTaxi.IsTaxiLightOn;
                                var status = _currentTaxi.IsTaxiLightOn ? "ACTIVÉ" : "DÉSACTIVÉ";
                                Screen.ShowSubtitle($"~y~Signe taxi {status}", 2000);
                            }
                            break;
                            
                        case Keys.C:
                            // Forcer l'arrivée ou spawner un client
                            if (_hasActiveRide)
                            {
                                CompleteRide();
                            }
                            else
                            {
                                SpawnNearbyClient();
                            }
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
        
        #region HUD Display
        
        private void DisplayTaxiHUD()
        {
            if (_currentTaxi == null) return;
            
            // Informations de base
            var statusInfo = _hasActiveRide ? $"EN COURSE vers {_destinationName}" : "LIBRE";
            var clientInfo = _currentClient != null ? "Client à bord" : "Aucun client";
            var earningsInfo = $"Gains: ${_totalEarnings}";
            var ridesInfo = $"Courses: {_ridesCompleted}";
            var taxiLightInfo = _currentTaxi.IsTaxiLightOn ? "Signe: ON" : "Signe: OFF";
            
            // Demande de client
            if (_showingClientRequest)
            {
                var requestText = $"~g~DEMANDE CLIENT~w~ - Lieu: {_clientPickupLocation} - Destination: {_destinationName} - Prix: ${_rideFare} | N: Menu";
                Screen.ShowSubtitle(requestText, 100);
            }
            else if (_hasActiveRide)
            {
                var rideText = $"{statusInfo} | {clientInfo} | Tarif: ${_rideFare} | {earningsInfo} | C: Terminer course";
                Screen.ShowSubtitle(rideText, 100);
            }
            else
            {
                var freeText = $"{statusInfo} | {earningsInfo} | {ridesInfo} | {taxiLightInfo} | H: Klaxon | T: Signe | C: Client | End: Fin";
                Screen.ShowSubtitle(freeText, 100);
            }
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