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
    /// Système de livreur avec livraisons dynamiques et gestion du temps
    /// </summary>
    public class DeliveryDriverManager : Script
    {
        #region Fields
        
        private ObjectPool _menuPool;
        private NativeMenu _deliveryMenu = null!;
        private NativeItem _endShiftItem = null!;
        
        private Vehicle? _currentDeliveryVehicle;
        private bool _isDriving;
        private bool _isOnShift;
        private DeliveryOrder? _currentOrder;
        private bool _hasActiveDelivery;
        
        private List<DeliveryLocation> _deliveryLocations = new();
        private List<Blip> _jobBlips = new();
        private Blip? _deliveryBlip;
        private Blip? _vehicleBlip;
        
        // Économie et timing
        private int _sessionEarnings;
        private int _deliveriesCompleted;
        private DateTime _orderStartTime;
        private int _basePayment;
        private float _timeBonus;
        

        
        // Locations populaires pour les livraisons
        private List<DeliveryDestination> _popularDestinations = new();
        
        // Ajout de champs pour gérer la cinématique et les objets
        private Prop? _foodContainer;
        private Prop? _foodContainerInHand;
        private Ped? _customerPed;
        private bool _isInDeliveryAnimation;
        private bool _hasFoodInHand;
        private bool _isInteractingWithVehicle;
        private DateTime _lastInteractionTime = DateTime.MinValue;
        private const int INTERACTION_COOLDOWN_MS = 2000; // 2 secondes de cooldown entre les interactions
        
        #endregion
        
        #region Initialization
        
        public DeliveryDriverManager()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            
            _menuPool = new ObjectPool();
            InitializeMenu();
            InitializeDeliveryLocations();
            InitializeDestinations();
            CreateJobLocationBlips();
            
            Logger.Info("Delivery Driver Manager initialized.");
        }
        
        private void InitializeMenu()
        {
            _deliveryMenu = new NativeMenu("Gestion Livreur", "Menu du service de livraison");
            _menuPool.Add(_deliveryMenu);
            
            _endShiftItem = new NativeItem("Terminer le service", "Finir votre service de livreur");
            
            _deliveryMenu.Add(_endShiftItem);
            
            _endShiftItem.Activated += OnEndShift;
        }
        
        private void InitializeDeliveryLocations()
        {
            _deliveryLocations = new List<DeliveryLocation>
            {
                // Restaurant chinois - coordonnées exactes comme demandé
                new DeliveryLocation(
                    "Noodle Exchange", 
                    new Vector3(-1229.79f, -286.50f, 37.73f), 
                    DeliveryType.Chinese,
                    "Livraison cuisine asiatique"
                )
            };
        }
        
        private void InitializeDestinations()
        {
            _popularDestinations = new List<DeliveryDestination>
            {
                // Seule destination possible comme demandé
                new DeliveryDestination("Résidence privée", new Vector3(-1629.42f, 37.54f, 62.94f))
            };
        }
        
        private void CreateJobLocationBlips()
        {
            foreach (var location in _deliveryLocations)
            {
                var blip = World.CreateBlip(location.Position);
                blip.Sprite = GetBlipSpriteForDeliveryType(location.Type);
                blip.Color = BlipColor.Yellow;
                blip.Name = $"Emploi - {location.Name}";
                blip.IsShortRange = true;
                blip.Scale = 0.8f;
                
                Function.Call(Hash.SET_BLIP_AS_MISSION_CREATOR_BLIP, blip.Handle, true);
                Function.Call(Hash.SET_BLIP_PRIORITY, blip.Handle, 8);
                
                _jobBlips.Add(blip);
            }
        }
        
        private BlipSprite GetBlipSpriteForDeliveryType(DeliveryType type)
        {
            return BlipSprite.Store; // Tous les restaurants utilisent le même sprite
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
                    CheckJobLocations(player);
                }
                else if (_isOnShift && _currentDeliveryVehicle != null && _currentDeliveryVehicle.Exists())
                {
                    if (!_isDriving && player.IsInVehicle(_currentDeliveryVehicle) && player.SeatIndex == VehicleSeat.Driver)
                    {
                        _isDriving = true;
                        Notification.PostTicker("~g~Vous êtes maintenant en service de livraison !", false, true);
                        
                        if (!_hasActiveDelivery)
                        {
                            GenerateNewOrder();
                        }
                    }
                    
                    if (_isDriving)
                    {
                        HandleDeliveryDriving();
                        ManageCurrentDelivery();
                        HandleFoodInteraction(player);
                        DisplayDeliveryHUD();
                    }
                    else
                    {
                        Screen.ShowSubtitle("~y~Montez dans le véhicule de livraison pour commencer", 100);
                    }
                    
                    // Empêcher les interactions pendant l'animation de livraison
                    if (_isInDeliveryAnimation || _isInteractingWithVehicle)
                    {
                        Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
                    }
                }
                else if (_isOnShift && (_currentDeliveryVehicle == null || !_currentDeliveryVehicle.Exists()))
                {
                    EndShift();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"DeliveryDriver tick error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Job Management
        
        private void CheckJobLocations(Ped player)
        {
            foreach (var location in _deliveryLocations)
            {
                float distance = player.Position.DistanceTo(location.Position);
                if (distance < 3.0f)
                {
                    Screen.ShowSubtitle($"~INPUT_CONTEXT~ Commencer le service de livraison - {location.Name}", 100);
                    
                    if (Game.IsControlJustPressed(GTA.Control.Context))
                    {
                        StartDeliveryJob(location);
                        return;
                    }
                }
            }
        }
        
        private void StartDeliveryJob(DeliveryLocation location)
        {
            try
            {
                Logger.Info($"Starting delivery job at {location.Name}...");
                
                _isOnShift = true;
                _sessionEarnings = 0;
                _deliveriesCompleted = 0;
                
                // Spawn du véhicule de livraison
                SpawnDeliveryVehicle(location);
                
                // Masquer les blips de job
                foreach (var blip in _jobBlips)
                {
                    if (blip.Exists())
                        blip.Alpha = 100; // Semi-transparent
                }
                
                Notification.PostTicker($"~g~Service de livraison commencé chez {location.Name}!", false, true);
                Notification.PostTicker("~b~Montez dans votre scooter pour recevoir des commandes", false, true);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting delivery job: {ex.Message}");
            }
        }
        
        private void SpawnDeliveryVehicle(DeliveryLocation location)
        {
            Vector3 spawnPosition = location.Position + new Vector3(10.0f, 10.0f, 0.0f);
            VehicleHash vehicleModel = GetDeliveryVehicleForType(location.Type);
            
            var model = new Model(vehicleModel);
            model.Request(5000);
            
            if (model.IsLoaded)
            {
                _currentDeliveryVehicle = World.CreateVehicle(model, spawnPosition);
                
                if (_currentDeliveryVehicle != null)
                {
                    _currentDeliveryVehicle.IsPersistent = true;
                    _currentDeliveryVehicle.IsInvincible = false;
                    _currentDeliveryVehicle.PlaceOnGround();
                    
                    // Améliorer les performances de la voiture
                    _currentDeliveryVehicle.EnginePowerMultiplier = 1.8f;       // Puissance du moteur augmentée
                    _currentDeliveryVehicle.EngineTorqueMultiplier = 1.5f;      // Couple du moteur augmenté
                    _currentDeliveryVehicle.MaxSpeed = 40.0f;                   // Vitesse maximale
                    
                    // Créer un blip pour le véhicule
                    _vehicleBlip = _currentDeliveryVehicle.AddBlip();
                    _vehicleBlip.Sprite = BlipSprite.PersonalVehicleCar;      // Icône de voiture
                    _vehicleBlip.Color = BlipColor.Blue;
                    _vehicleBlip.Name = "Véhicule de livraison";
                    
                    // Ajouter un conteneur de nourriture sur le siège passager
                    PlaceFoodContainerInVehicle();
                    
                    Logger.Info($"Delivery vehicle spawned: {vehicleModel}");
                }
            }
            
            model.MarkAsNoLongerNeeded();
        }
        
        private void PlaceFoodContainerInVehicle()
        {
            if (_currentDeliveryVehicle == null || !_currentDeliveryVehicle.Exists()) return;
            
            // Utiliser un prop de sac en papier/boîte comme conteneur de nourriture
            _foodContainer = World.CreateProp(new Model("prop_food_bs_bag_01"), _currentDeliveryVehicle.Position, false, false);
            
            if (_foodContainer != null && _foodContainer.Exists())
            {
                _foodContainer.IsPersistent = true;
                
                // Attacher le conteneur au siège passager avec l'API native
                int boneIndex = Function.Call<int>(Hash.GET_ENTITY_BONE_INDEX_BY_NAME, _currentDeliveryVehicle.Handle, "seat_pside_f");
                Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY, _foodContainer.Handle, _currentDeliveryVehicle.Handle, 
                    boneIndex, 0.0f, 0.2f, 0.15f, 0.0f, 0.0f, 0.0f, false, false, false, false, 2, true);
                
                Logger.Info("Food container placed in vehicle");
            }
            
            _hasFoodInHand = false;
        }
        
        private VehicleHash GetDeliveryVehicleForType(DeliveryType type)
        {
            return VehicleHash.Panto; // Petite voiture de livraison
        }
        
        private void GenerateNewOrder()
        {
            if (_hasActiveDelivery) return;
            
            Random rand = new Random();
            var destination = _popularDestinations[0]; // Il n'y a qu'une seule destination possible
            var deliveryType = DeliveryType.Chinese; // Il n'y a que le restaurant chinois
            
            _basePayment = rand.Next(50, 150); // Paiement de base
            _timeBonus = 1.0f;
            
            _currentOrder = new DeliveryOrder
            {
                Destination = destination,
                DeliveryType = deliveryType,
                OrderItems = GenerateOrderItems(deliveryType),
                BasePayment = _basePayment,
                MaxDeliveryTime = TimeSpan.FromMinutes(rand.Next(8, 15)) // 8-15 minutes
            };
            
            _hasActiveDelivery = true;
            _orderStartTime = DateTime.Now;
            
            // Créer un blip pour la destination
            _deliveryBlip = World.CreateBlip(_currentOrder.Destination.Position);
            _deliveryBlip.Sprite = BlipSprite.Waypoint;
            _deliveryBlip.Color = BlipColor.Green;
            _deliveryBlip.Name = "Livraison";
            
            Notification.PostTicker($"~g~Nouvelle commande reçue !", false, true);
            Notification.PostTicker($"~b~Livraison: {_currentOrder.OrderItems} à {destination.Name}", false, true);
            Notification.PostTicker($"~y~Paiement: ${_basePayment} (bonus temps possible)", false, true);
        }
        
        private string GenerateOrderItems(DeliveryType type)
        {
            Random rand = new Random();
            return type switch
            {
                DeliveryType.Pizza => $"{rand.Next(1, 3)} Pizza{(rand.Next(1, 3) > 1 ? "s" : "")} {GetRandomPizzaType()}",
                DeliveryType.Chinese => $"{GetRandomChineseFood()}",
                DeliveryType.FastFood => $"{GetRandomFastFood()}",
                _ => "Commande spéciale"
            };
        }
        
        private string GetRandomPizzaType()
        {
            string[] types = { "Margherita", "Pepperoni", "4 Fromages", "Végétarienne", "Spéciale" };
            return types[new Random().Next(types.Length)];
        }
        
        private string GetRandomChineseFood()
        {
            string[] foods = { "Poulet Général Tao", "Porc aigre-doux", "Bœuf aux brocolis", "Riz cantonais", "Nouilles sautées" };
            return foods[new Random().Next(foods.Length)];
        }
        
        private string GetRandomFastFood()
        {
            string[] foods = { "Menu Big Burger", "Nuggets + Frites", "Sandwich Poulet", "Menu Fish" };
            return foods[new Random().Next(foods.Length)];
        }
        
        private void ManageCurrentDelivery()
        {
            if (!_hasActiveDelivery || _currentOrder == null) return;
            
            Ped player = Game.Player.Character;
            float distance = player.Position.DistanceTo(_currentOrder.Destination.Position);
            
            // On n'affiche plus le message de livraison ici, car c'est géré dans HandleFoodInteraction
            // uniquement si le joueur a le sac en main
            
            // Calculer le bonus/malus de temps
            var timeElapsed = DateTime.Now - _orderStartTime;
            if (timeElapsed > _currentOrder.MaxDeliveryTime)
            {
                _timeBonus = Math.Max(0.5f, 1.0f - (float)(timeElapsed.TotalMinutes - _currentOrder.MaxDeliveryTime.TotalMinutes) / 10.0f);
            }
            else
            {
                _timeBonus = 1.0f + (float)(_currentOrder.MaxDeliveryTime.TotalMinutes - timeElapsed.TotalMinutes) / 20.0f;
            }
        }
        
        private void CompleteDelivery()
        {
            if (_currentOrder == null || !_hasFoodInHand || _foodContainerInHand == null) return;
            
            // Supprimer le sac de nourriture en main
            if (_foodContainerInHand.Exists())
            {
                _foodContainerInHand.Delete();
                _foodContainerInHand = null;
            }
            
            _hasFoodInHand = false;
            
            // Démarrer la cinématique de livraison
            PlayDeliveryAnimation();
            
            int finalPayment = (int)(_currentOrder.BasePayment * _timeBonus);
            _sessionEarnings += finalPayment;
            _deliveriesCompleted++;
            
            // Donner l'argent au joueur
            Game.Player.Money += finalPayment;
            
            var timeElapsed = DateTime.Now - _orderStartTime;
            string timeText = timeElapsed > _currentOrder.MaxDeliveryTime ? "Retard" : "À temps";
            
            Notification.PostTicker($"~g~Livraison terminée !", false, true);
            Notification.PostTicker($"~b~Paiement: ${finalPayment} ({timeText})", false, true);
            
            // Nettoyer la commande actuelle
            if (_deliveryBlip != null && _deliveryBlip.Exists())
            {
                _deliveryBlip.Delete();
            }
            
            _currentOrder = null;
            _hasActiveDelivery = false;
            
            // Regénérer le sac dans le véhicule
            if (_foodContainer == null || !_foodContainer.Exists())
            {
                PlaceFoodContainerInVehicle();
            }
            else if (_foodContainer.Exists())
            {
                Function.Call(Hash.SET_ENTITY_VISIBLE, _foodContainer.Handle, true, false);
            }
            
            // Générer une nouvelle commande après un court délai
            Script.Wait(2000);
            if (_isOnShift && _isDriving)
            {
                GenerateNewOrder();
            }
        }
        
        private void PlayDeliveryAnimation()
        {
            try
            {
                _isInDeliveryAnimation = true;
                
                // Fade out
                Function.Call(Hash.DO_SCREEN_FADE_OUT, 1000);
                Script.Wait(1000);
                
                Ped playerPed = Game.Player.Character;
                
                // Vérification pour éviter la référence null
                if (_currentOrder == null)
                {
                    // Si _currentOrder est null, utiliser une position par défaut
                    Logger.Info("ATTENTION: Current order is null during delivery animation");
                    _isInDeliveryAnimation = false;
                    Function.Call(Hash.DO_SCREEN_FADE_IN, 1000);
                    return;
                }
                
                Vector3 deliveryPosition = _currentOrder.Destination.Position;
                Vector3 doorPosition = deliveryPosition;
                
                // Créer un PNJ client
                Model customerModel = new Model(PedHash.Business01AMM);
                customerModel.Request(1000);
                
                if (customerModel.IsLoaded)
                {
                    // Créer le PNJ à l'intérieur de la maison
                    Vector3 insidePosition = doorPosition + new Vector3(0, 0, 0);
                    _customerPed = World.CreatePed(customerModel, insidePosition);
                    
                    if (_customerPed != null)
                    {
                        _customerPed.IsPersistent = true;
                        
                        // Positionner le joueur devant la porte
                        playerPed.Position = doorPosition - new Vector3(0, 1.5f, 0);
                        playerPed.Heading = 0; // Face à la porte
                        
                        // Positionner le client à la porte
                        _customerPed.Position = doorPosition;
                        _customerPed.Heading = 180; // Face au joueur
                        
                        // Fade in
                        Function.Call(Hash.DO_SCREEN_FADE_IN, 1000);
                        Script.Wait(1000);
                        
                        // Le client fait un geste de remerciement
                        _customerPed.Task.PlayAnimation("mp_common", "givethanks_male", 8.0f, -8.0f, 2000, AnimationFlags.None, 0.0f);
                        Script.Wait(2000);
                        
                        // Le joueur fait un geste de salut
                        playerPed.Task.PlayAnimation("mp_common", "goodbye_male", 8.0f, -8.0f, 2000, AnimationFlags.None, 0.0f);
                        Script.Wait(2000);
                        
                        // Fade out pour terminer la scène
                        Function.Call(Hash.DO_SCREEN_FADE_OUT, 1000);
                        Script.Wait(1000);
                        
                        // Nettoyer le PNJ
                        if (_customerPed.Exists())
                        {
                            _customerPed.Delete();
                            _customerPed = null;
                        }
                        
                        // Replacer le joueur près de son véhicule
                        if (_currentDeliveryVehicle != null && _currentDeliveryVehicle.Exists())
                        {
                            playerPed.Position = _currentDeliveryVehicle.Position + new Vector3(2.0f, 0, 0);
                            playerPed.Heading = _currentDeliveryVehicle.Heading;
                        }
                        
                        // Fade in
                        Function.Call(Hash.DO_SCREEN_FADE_IN, 1000);
                    }
                }
                
                customerModel.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in delivery animation: {ex.Message}");
            }
            finally
            {
                _isInDeliveryAnimation = false;
            }
        }
        
        private void HandleDeliveryDriving()
        {
            // Ne rien faire ici - permettre au joueur de livrer même s'il est sorti du véhicule
            // Nous gardons _isDriving à true une fois que le joueur a commencé
        }
        
        private void EndShift()
        {
            // Nettoyer les ressources
            if (_deliveryBlip != null && _deliveryBlip.Exists())
                _deliveryBlip.Delete();
            
            if (_vehicleBlip != null && _vehicleBlip.Exists())
                _vehicleBlip.Delete();
            
            if (_foodContainer != null && _foodContainer.Exists())
                _foodContainer.Delete();
            
            if (_foodContainerInHand != null && _foodContainerInHand.Exists())
                _foodContainerInHand.Delete();
            
            if (_customerPed != null && _customerPed.Exists())
                _customerPed.Delete();
            
            if (_currentDeliveryVehicle != null && _currentDeliveryVehicle.Exists())
            {
                _currentDeliveryVehicle.IsPersistent = false;
                _currentDeliveryVehicle.Delete();
            }
            
            // Restaurer les blips de job
            foreach (var blip in _jobBlips)
            {
                if (blip.Exists())
                    blip.Alpha = 255;
            }
            
            // Résumé de la session
            Notification.PostTicker($"~g~Service terminé !", false, true);
            Notification.PostTicker($"~b~Livraisons: {_deliveriesCompleted} | Gains: ${_sessionEarnings}", false, true);
            
            // Reset des variables
            _isOnShift = false;
            _isDriving = false;
            _hasActiveDelivery = false;
            _currentOrder = null;
            _currentDeliveryVehicle = null;
            _foodContainer = null;
            _foodContainerInHand = null;
            _customerPed = null;
            _sessionEarnings = 0;
            _deliveriesCompleted = 0;
            _isInDeliveryAnimation = false;
            _hasFoodInHand = false;
            _isInteractingWithVehicle = false;
        }
        
        #endregion
        

        
        #region HUD and Display
        
        private void DisplayDeliveryHUD()
        {
            if (!_hasActiveDelivery || _currentOrder == null) return;
            
            var timeElapsed = DateTime.Now - _orderStartTime;
            var timeRemaining = _currentOrder.MaxDeliveryTime - timeElapsed;
            
            string timeDisplay = timeRemaining.TotalSeconds > 0 
                ? $"{timeRemaining:mm\\:ss}" 
                : $"-{(-timeRemaining):mm\\:ss}";
            
            string bonusText = _timeBonus > 1.0f ? "BONUS" : _timeBonus < 1.0f ? "MALUS" : "NORMAL";
            
            var hudText = $"LIVRAISON: {_currentOrder.Destination.Name} | Temps: {timeDisplay} | " +
                         $"Paiement: ${(int)(_currentOrder.BasePayment * _timeBonus)} ({bonusText}) | " +
                         $"Livraisons: {_deliveriesCompleted} | Gains: ${_sessionEarnings}";
            
            Screen.ShowSubtitle(hudText, 100);
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_isOnShift && e.KeyCode == Keys.M)
            {
                if (_deliveryMenu.Visible)
                    _deliveryMenu.Visible = false;
                else
                    _deliveryMenu.Visible = true;
            }
        }
        
        private void OnEndShift(object sender, EventArgs e)
        {
            if (_isOnShift)
            {
                EndShift();
            }
        }
        
        private void OnAborted(object sender, EventArgs e)
        {
            // Nettoyer les ressources
            foreach (var blip in _jobBlips)
            {
                if (blip.Exists())
                    blip.Delete();
            }
            
            if (_deliveryBlip != null && _deliveryBlip.Exists())
                _deliveryBlip.Delete();
            
            if (_vehicleBlip != null && _vehicleBlip.Exists())
                _vehicleBlip.Delete();
            
            if (_foodContainer != null && _foodContainer.Exists())
                _foodContainer.Delete();
            
            if (_foodContainerInHand != null && _foodContainerInHand.Exists())
                _foodContainerInHand.Delete();
            
            if (_customerPed != null && _customerPed.Exists())
                _customerPed.Delete();
            
            Logger.Info("Delivery Driver Manager unloaded.");
        }
        
        private void HandleFoodInteraction(Ped player)
        {
            if (_isInDeliveryAnimation || _isInteractingWithVehicle) return;
            
            // Ne pas permettre d'interagir trop souvent
            if ((DateTime.Now - _lastInteractionTime).TotalMilliseconds < INTERACTION_COOLDOWN_MS) return;
            
            // Si le joueur est à proximité de la porte passager et n'est pas dans le véhicule
            if (_currentDeliveryVehicle != null && _currentDeliveryVehicle.Exists() && !player.IsInVehicle())
            {
                // Calculer la position de la porte passager
                Vector3 passengerDoorPosition = _currentDeliveryVehicle.GetOffsetPosition(new Vector3(0.8f, 0.5f, 0.0f));
                float distanceToDoor = player.Position.DistanceTo(passengerDoorPosition);
                
                if (distanceToDoor < 1.5f)
                {
                    // Afficher l'instruction d'interaction
                    if (!_hasFoodInHand && _foodContainer != null && _foodContainer.Exists())
                    {
                        Screen.ShowSubtitle("~INPUT_CONTEXT~ Prendre le sac de nourriture", 100);
                        
                        if (Game.IsControlJustPressed(GTA.Control.Context))
                        {
                            // Exécuter l'animation de prise du sac
                            _lastInteractionTime = DateTime.Now;
                            _isInteractingWithVehicle = true;
                            TakeFoodFromVehicle(player);
                        }
                    }
                    else if (_hasFoodInHand && _foodContainerInHand != null && _foodContainerInHand.Exists())
                    {
                        Screen.ShowSubtitle("~INPUT_CONTEXT~ Replacer le sac de nourriture", 100);
                        
                        if (Game.IsControlJustPressed(GTA.Control.Context))
                        {
                            // Replacer le sac dans le véhicule
                            _lastInteractionTime = DateTime.Now;
                            _isInteractingWithVehicle = true;
                            PutFoodBackInVehicle(player);
                        }
                    }
                }
                
                // Si le joueur est proche de la porte de livraison et qu'il a le sac en main
                if (_hasActiveDelivery && _currentOrder != null && _hasFoodInHand && _foodContainerInHand != null && _foodContainerInHand.Exists())
                {
                    float distanceToDelivery = player.Position.DistanceTo(_currentOrder.Destination.Position);
                    
                    if (distanceToDelivery < 5.0f)
                    {
                        Screen.ShowSubtitle("~INPUT_CONTEXT~ Livrer la commande", 100);
                        
                        if (Game.IsControlJustPressed(GTA.Control.Context))
                        {
                            CompleteDelivery();
                        }
                    }
                }
            }
        }
        
        private void TakeFoodFromVehicle(Ped player)
        {
            try
            {
                if (_currentDeliveryVehicle == null || !_currentDeliveryVehicle.Exists() || _foodContainer == null || !_foodContainer.Exists()) 
                {
                    _isInteractingWithVehicle = false;
                    return;
                }
                
                // Ouvrir la porte passager AVANT l'animation (index 1 = porte avant droite)
                Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, _currentDeliveryVehicle.Handle, 1, false, false);
                
                // Faire face au véhicule
                player.Task.LookAt(_currentDeliveryVehicle, 2000);
                Script.Wait(1000);
                
                // Animation de fouille/récupération
                player.Task.PlayAnimation("mp_common", "givetake1_a", 8.0f, -8.0f, 1500, AnimationFlags.None, 0.0f);
                Script.Wait(1500);
                
                // Cacher le sac original
                Function.Call(Hash.SET_ENTITY_VISIBLE, _foodContainer.Handle, false, false);
                
                // Créer un nouveau sac que le joueur aura en main
                _foodContainerInHand = World.CreateProp(new Model("prop_food_bs_bag_01"), player.Position, false, false);
                
                if (_foodContainerInHand != null && _foodContainerInHand.Exists())
                {
                    _foodContainerInHand.IsPersistent = true;
                    
                    // Utiliser le bon os pour la main droite
                    Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY, _foodContainerInHand.Handle, player.Handle, 
                        player.Bones[Bone.PHRightHand].Index, 0.3f, 0.0f, 0.0f, 0.3f, -90.0f, 0.0f, true, true, false, false, 2, true);
                    
                    _hasFoodInHand = true;
                    
                    Notification.PostTicker("~g~Vous avez pris le sac de nourriture.", false, true);
                }
                
                // Fermer la porte
                Script.Wait(500);
                Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, _currentDeliveryVehicle.Handle, 1, false);
                
                Script.Wait(500);
                _isInteractingWithVehicle = false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error taking food from vehicle: {ex.Message}");
                _isInteractingWithVehicle = false;
            }
        }
        
        private void PutFoodBackInVehicle(Ped player)
        {
            try
            {
                if (_currentDeliveryVehicle == null || !_currentDeliveryVehicle.Exists() || _foodContainerInHand == null || !_foodContainerInHand.Exists() || _foodContainer == null) 
                {
                    _isInteractingWithVehicle = false;
                    return;
                }
                
                // Ouvrir la porte passager AVANT l'animation (index 1 = porte avant droite)
                Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, _currentDeliveryVehicle.Handle, 1, false, false);
                
                // Faire face au véhicule
                player.Task.LookAt(_currentDeliveryVehicle, 2000);
                Script.Wait(1000);
                
                // Animation de placement
                player.Task.PlayAnimation("mp_common", "givetake1_a", 8.0f, -8.0f, 1500, AnimationFlags.None, 0.0f);
                Script.Wait(1500);
                
                // Supprimer le sac en main
                if (_foodContainerInHand.Exists())
                {
                    _foodContainerInHand.Delete();
                    _foodContainerInHand = null;
                }
                
                // Rendre visible le sac original dans le véhicule
                if (_foodContainer.Exists())
                {
                    Function.Call(Hash.SET_ENTITY_VISIBLE, _foodContainer.Handle, true, false);
                }
                
                _hasFoodInHand = false;
                
                // Fermer la porte
                Script.Wait(500);
                Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, _currentDeliveryVehicle.Handle, 1, false);
                
                Notification.PostTicker("~b~Vous avez replacé le sac de nourriture dans le véhicule.", false, true);
                
                Script.Wait(500);
                _isInteractingWithVehicle = false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error putting food back in vehicle: {ex.Message}");
                _isInteractingWithVehicle = false;
            }
        }
        
        #endregion
    }
    
    #region Data Classes
    
    public class DeliveryLocation
    {
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public DeliveryType Type { get; set; }
        public string Description { get; set; }
        
        public DeliveryLocation(string name, Vector3 position, DeliveryType type, string description)
        {
            Name = name;
            Position = position;
            Type = type;
            Description = description;
        }
    }
    
    public class DeliveryDestination
    {
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        
        public DeliveryDestination(string name, Vector3 position)
        {
            Name = name;
            Position = position;
        }
    }
    
    public class DeliveryOrder
    {
        public DeliveryDestination Destination { get; set; } = null!;
        public DeliveryType DeliveryType { get; set; }
        public string OrderItems { get; set; } = "";
        public int BasePayment { get; set; }
        public TimeSpan MaxDeliveryTime { get; set; }
    }
    
    public enum DeliveryType
    {
        Pizza,
        Chinese,
        FastFood
    }
    
    #endregion
} 