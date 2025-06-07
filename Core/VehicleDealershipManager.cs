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

namespace REALIS.Core
{
    /// <summary>
    /// Gestionnaire des concessionnaires de véhicules - Permet l'achat sans menu
    /// </summary>
    public class VehicleDealershipManager : Script
    {
        private readonly List<DealershipVehicle> _dealershipVehicles = new();
        private readonly Dictionary<VehicleHash, int> _vehiclePrices = new();
        private readonly List<Blip> _dealershipBlips = new();
        private readonly Dictionary<int, VehicleDisplayInfo> _vehicleDisplayCache = new(); // Cache des infos d'affichage
        private bool _isEnabled = true;
        private DateTime _lastUpdate = DateTime.MinValue;
        private DateTime _lastTextUpdate = DateTime.MinValue;
        private const int UPDATE_INTERVAL_MS = 0;
        private const int TEXT_UPDATE_INTERVAL_MS = 0; // Texte mis à jour seulement 2 fois par seconde
        
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

        public VehicleDealershipManager()
        {
            Tick += OnTick;
            Aborted += OnAborted;
            
            InitializeVehiclePrices();
            CreateDealershipBlips();
            SpawnDealershipVehicles();
            
            Logger.Info("Vehicle Dealership Manager initialized.");
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_isEnabled) return;
            
            var now = DateTime.Now;
            if ((now - _lastUpdate).TotalMilliseconds < UPDATE_INTERVAL_MS)
                return;
            
            _lastUpdate = now;
            
            try
            {
                ProcessDealershipInteractions();
                UpdateVehicleDisplays(); // Maintenant géré individuellement par véhicule
                ProcessVehicleReplacements();
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
                // Spawner 3-5 véhicules par concessionnaire
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
        /// Traite les interactions avec les véhicules de concessionnaire
        /// </summary>
        private void ProcessDealershipInteractions()
        {
            var player = Game.Player.Character;
            var playerVehicle = player.CurrentVehicle;
            
            // Vérifier si le joueur est dans un véhicule de concessionnaire
            if (playerVehicle != null && playerVehicle.Exists())
            {
                var dealershipVehicle = _dealershipVehicles.FirstOrDefault(dv => 
                    dv.Vehicle != null && dv.Vehicle.Handle == playerVehicle.Handle && dv.IsAvailable);
                
                if (dealershipVehicle?.Vehicle != null)
                {
                    ShowPurchasePromptInside(dealershipVehicle);
                    HandlePurchaseInput(dealershipVehicle);
                }
            }
        }

        /// <summary>
        /// Affiche l'invite d'achat quand le joueur est dans le véhicule
        /// </summary>
        private void ShowPurchasePromptInside(DealershipVehicle dealershipVehicle)
        {
            if (dealershipVehicle.Vehicle == null) return;
            
            var price = dealershipVehicle.Price;
            var vehicleName = dealershipVehicle.Vehicle.DisplayName;
            var playerMoney = Game.Player.Money;
            
            var canAfford = playerMoney >= price;
            var color = canAfford ? "~g~" : "~r~";
            
            var message = $"{color}{vehicleName} - ${price:N0}\n~w~Appuyez sur ~g~E~w~ pour acheter ce véhicule";
            
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, message);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, -1);
        }

        /// <summary>
        /// Gère l'input pour l'achat
        /// </summary>
        private void HandlePurchaseInput(DealershipVehicle dealershipVehicle)
        {
            if (Game.IsKeyPressed(Keys.E))
            {
                PurchaseVehicle(dealershipVehicle);
            }
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
            
            _dealershipVehicles.Clear();
            _dealershipBlips.Clear();
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
}