using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.UI;
using GTA.Native;
using LemonUI;
using LemonUI.Menus;
using REALIS.Common;

namespace REALIS.Core
{
    /// <summary>
    /// Gestionnaire du menu téléphone LemonUI simplifié qui remplace le téléphone du jeu
    /// </summary>
    public class PhoneMenuManagerSimple : Script
    {
        private readonly ObjectPool _menuPool;
        private readonly NativeMenu _mainMenu;
        private readonly NativeMenu _emergencyServicesMenu;
        private readonly NativeMenu _foodDeliveryMenu;
        private readonly NativeMenu _towingServiceMenu;
        
        // Services d'urgence
        private readonly Dictionary<string, int> _emergencyServices = new()
        {
            { "Police", 911 },
            { "Ambulancier", 112 },
            { "Pompier", 114 },
            { "Garde-côtes", 115 }
        };
        
        // Restaurants de livraison
        private readonly Dictionary<string, List<FoodItem>> _restaurants = new()
        {
            { "Burger Shot", new List<FoodItem>
                {
                    new("Big Burger", 12.99m, "Burger avec fromage, salade, tomate"),
                    new("Chicken Wrap", 8.99m, "Wrap au poulet grillé"),
                    new("Frites", 4.99m, "Portion de frites croustillantes"),
                    new("Soda", 2.99m, "Boisson gazeuse rafraîchissante")
                }
            },
            { "Cluckin' Bell", new List<FoodItem>
                {
                    new("Poulet Frit", 15.99m, "Morceaux de poulet épicé"),
                    new("Chicken Burger", 10.99m, "Burger au poulet croustillant"),
                    new("Salade César", 9.99m, "Salade fraîche avec poulet"),
                    new("Milkshake", 5.99m, "Milkshake vanille ou chocolat")
                }
            },
            { "Pizza This", new List<FoodItem>
                {
                    new("Pizza Margherita", 18.99m, "Pizza classique tomate-mozzarella"),
                    new("Pizza Pepperoni", 21.99m, "Pizza au pepperoni"),
                    new("Calzone", 16.99m, "Calzone farci au fromage"),
                    new("Tiramisu", 7.99m, "Dessert italien traditionnel")
                }
            }
        };
        
        private bool _isMenuOpen = false;
        private readonly Random _random = new();
        private readonly List<DelayedTask> _pendingTasks = new();
        
        private class DelayedTask
        {
            public DateTime ExecuteAt { get; set; }
            public Action Task { get; set; }
            public string Description { get; set; }
            
            public DelayedTask(int delayMs, Action task, string description = "")
            {
                ExecuteAt = DateTime.Now.AddMilliseconds(delayMs);
                Task = task;
                Description = description;
            }
        }
        
        public PhoneMenuManagerSimple()
        {
            _menuPool = new ObjectPool();
            
            // Menu principal
            _mainMenu = new NativeMenu("PHONE", "Menu Principal")
            {
                MouseBehavior = MenuMouseBehavior.Disabled,
                Alignment = Alignment.Left
            };
            
            // Sous-menus
            _emergencyServicesMenu = new NativeMenu("EMERGENCY", "Services d'Urgence")
            {
                MouseBehavior = MenuMouseBehavior.Disabled,
                Alignment = Alignment.Left
            };
            
            _foodDeliveryMenu = new NativeMenu("FOOD", "Livraison Nourriture")
            {
                MouseBehavior = MenuMouseBehavior.Disabled,
                Alignment = Alignment.Left
            };
            
            _towingServiceMenu = new NativeMenu("TOWING", "Service Remorquage")
            {
                MouseBehavior = MenuMouseBehavior.Disabled,
                Alignment = Alignment.Left
            };
            
            SetupMainMenu();
            SetupEmergencyServicesMenu();
            SetupFoodDeliveryMenu();
            SetupTowingServiceMenu();
            
            _menuPool.Add(_mainMenu);
            _menuPool.Add(_emergencyServicesMenu);
            _menuPool.Add(_foodDeliveryMenu);
            _menuPool.Add(_towingServiceMenu);
            
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            
            Logger.Info("Phone Menu Manager (Simple) initialized successfully.");
        }
        
        private void SetupMainMenu()
        {
            var emergencyItem = new NativeItem("Services d'Urgence", "Appeler police, ambulance, pompiers...");
            emergencyItem.Activated += (sender, e) => 
            {
                _mainMenu.Visible = false;
                _emergencyServicesMenu.Visible = true;
            };
            
            var foodItem = new NativeItem("Livraison Nourriture", "Commander de la nourriture à votre position");
            foodItem.Activated += (sender, e) => 
            {
                _mainMenu.Visible = false;
                _foodDeliveryMenu.Visible = true;
            };
            
            var towingItem = new NativeItem("Service Remorquage", "Appeler une remorqueuse");
            towingItem.Activated += (sender, e) => 
            {
                _mainMenu.Visible = false;
                _towingServiceMenu.Visible = true;
            };
            
            var closeItem = new NativeItem("Fermer Téléphone", "Fermer le menu téléphone");
            closeItem.Activated += (sender, e) => ClosePhone();
            
            _mainMenu.Add(emergencyItem);
            _mainMenu.Add(foodItem);
            _mainMenu.Add(towingItem);
            _mainMenu.Add(closeItem);
        }
        
        private void SetupEmergencyServicesMenu()
        {
            foreach (var service in _emergencyServices)
            {
                var item = new NativeItem($"Appeler {service.Key}", $"Composer le {service.Value}");
                item.Activated += (sender, e) => CallEmergencyService(service.Key, service.Value);
                _emergencyServicesMenu.Add(item);
            }
            
            var backItem = new NativeItem("← Retour", "Retourner au menu principal");
            backItem.Activated += (sender, e) => 
            {
                _emergencyServicesMenu.Visible = false;
                _mainMenu.Visible = true;
            };
            _emergencyServicesMenu.Add(backItem);
        }
        
        private void SetupFoodDeliveryMenu()
        {
            foreach (var restaurant in _restaurants)
            {
                var restaurantMenu = new NativeMenu("RESTAURANT", restaurant.Key)
                {
                    MouseBehavior = MenuMouseBehavior.Disabled,
                    Alignment = Alignment.Left
                };
                
                foreach (var food in restaurant.Value)
                {
                    var foodItem = new NativeItem($"{food.Name}", $"{food.Description} - ${food.Price:F2}");
                    foodItem.Activated += (sender, e) => OrderFood(restaurant.Key, food);
                    restaurantMenu.Add(foodItem);
                }
                
                var backToFood = new NativeItem("← Retour", "Retourner aux restaurants");
                backToFood.Activated += (sender, e) => 
                {
                    restaurantMenu.Visible = false;
                    _foodDeliveryMenu.Visible = true;
                };
                restaurantMenu.Add(backToFood);
                
                var restaurantItem = new NativeItem($"{restaurant.Key}", $"{restaurant.Value.Count} plats disponibles");
                restaurantItem.Activated += (sender, e) => 
                {
                    _foodDeliveryMenu.Visible = false;
                    restaurantMenu.Visible = true;
                };
                
                _foodDeliveryMenu.Add(restaurantItem);
                _menuPool.Add(restaurantMenu);
            }
            
            var backItem = new NativeItem("← Retour", "Retourner au menu principal");
            backItem.Activated += (sender, e) => 
            {
                _foodDeliveryMenu.Visible = false;
                _mainMenu.Visible = true;
            };
            _foodDeliveryMenu.Add(backItem);
        }
        
        private void SetupTowingServiceMenu()
        {
            var quickTowItem = new NativeItem("Remorquage Rapide - $150", "Remorquage immédiat");
            quickTowItem.Activated += (sender, e) => CallTowingService("quick");
            
            var scheduledTowItem = new NativeItem("Remorquage Programmé - $100", "Remorquage dans 5 minutes");
            scheduledTowItem.Activated += (sender, e) => CallTowingService("scheduled");
            
            var heavyTowItem = new NativeItem("Remorquage Lourd - $300", "Pour véhicules lourds");
            heavyTowItem.Activated += (sender, e) => CallTowingService("heavy");
            
            var backItem = new NativeItem("← Retour", "Retourner au menu principal");
            backItem.Activated += (sender, e) => 
            {
                _towingServiceMenu.Visible = false;
                _mainMenu.Visible = true;
            };
            
            _towingServiceMenu.Add(quickTowItem);
            _towingServiceMenu.Add(scheduledTowItem);
            _towingServiceMenu.Add(heavyTowItem);
            _towingServiceMenu.Add(backItem);
        }
        
        private void CallEmergencyService(string serviceName, int number)
        {
            var player = Game.Player.Character;
            var playerPos = player.Position;
            
            Notification.PostTicker($"~b~Appel en cours...~n~~w~Vous appelez les {serviceName}", false, true);
            
            // Programmer l'arrivée du service
            switch (serviceName)
            {
                case "Police":
                    SpawnPoliceResponse(playerPos);
                    break;
                case "Ambulancier":
                    SpawnAmbulanceResponse(playerPos);
                    break;
                case "Pompier":
                    SpawnFireResponse(playerPos);
                    break;
                case "Garde-côtes":
                    SpawnCoastGuardResponse(playerPos);
                    break;
            }
            
            ClosePhone();
        }
        
        private void OrderFood(string restaurantName, FoodItem food)
        {
            var player = Game.Player.Character;
            var playerPos = player.Position;
            
            Notification.PostTicker($"~g~Commande passée!~n~~w~{food.Name} de {restaurantName}~n~Prix: ${food.Price:F2}~n~Livraison estimée: 5-10 minutes", false, true);
            
            // Déduire l'argent du joueur
            if (Game.Player.Money >= (int)food.Price)
            {
                Game.Player.Money -= (int)food.Price;
                
                // Programmer la livraison
                ScheduleFoodDelivery(restaurantName, food, playerPos);
            }
            else
            {
                Notification.PostTicker("~r~Fonds insuffisants!", false, true);
            }
            
            ClosePhone();
        }
        
        private void CallTowingService(string serviceType)
        {
            var player = Game.Player.Character;
            var playerPos = player.Position;
            
            int cost = serviceType switch
            {
                "quick" => 150,
                "scheduled" => 100,
                "heavy" => 300,
                _ => 100
            };
            
            if (Game.Player.Money >= cost)
            {
                Game.Player.Money -= cost;
                
                string serviceDescription = serviceType switch
                {
                    "quick" => "Remorquage rapide demandé",
                    "scheduled" => "Remorquage programmé dans 5 minutes",
                    "heavy" => "Remorquage lourd demandé",
                    _ => "Service de remorquage"
                };
                
                Notification.PostTicker($"~g~{serviceDescription}~n~~w~Coût: ${cost}~n~La remorqueuse arrive...", false, true);
                
                // Programmer l'arrivée de la remorqueuse
                ScheduleTowTruck(serviceType, playerPos);
            }
            else
            {
                Notification.PostTicker("~r~Fonds insuffisants!", false, true);
            }
            
            ClosePhone();
        }
        
        private void SpawnPoliceResponse(Vector3 position)
        {
            ScheduleTask(3000 + _random.Next(2000), () =>
            {
                var spawnPos = GetSpawnPosition(position, 100f);
                var policeVehicle = World.CreateVehicle(VehicleHash.Police, spawnPos);
                
                if (policeVehicle != null && policeVehicle.Exists())
                {
                    var cop1 = policeVehicle.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Cop01SMY);
                    
                    if (cop1 != null && cop1.Exists())
                    {
                        cop1.Task.DriveTo(policeVehicle, position, 15f, VehicleDrivingFlags.None, 10f);
                        cop1.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                    }
                    
                    Notification.PostTicker("~b~Police~n~~w~Une patrouille est en route vers votre position", false, true);
                }
            }, "Police Response");
        }
        
                private void SpawnAmbulanceResponse(Vector3 position)
        {
            ScheduleTask(4000 + _random.Next(3000), () =>
            {
                var spawnPos = GetSpawnPosition(position, 120f);
                var ambulance = World.CreateVehicle(VehicleHash.Ambulance, spawnPos);
                
                if (ambulance != null && ambulance.Exists())
                {
                    var paramedic = ambulance.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Paramedic01SMM);
                    
                    if (paramedic != null && paramedic.Exists())
                    {
                        paramedic.Task.DriveTo(ambulance, position, 15f, VehicleDrivingFlags.None, 10f);
                        paramedic.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                    }
                    
                    Notification.PostTicker("~g~Ambulance~n~~w~Les secours arrivent sur les lieux", false, true);
                }
            }, "Ambulance Response");
        }

        private void SpawnFireResponse(Vector3 position)
        {
            ScheduleTask(5000 + _random.Next(3000), () =>
            {
                var spawnPos = GetSpawnPosition(position, 150f);
                var firetruck = World.CreateVehicle(VehicleHash.FireTruck, spawnPos);
                
                if (firetruck != null && firetruck.Exists())
                {
                    var firefighter = firetruck.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Fireman01SMY);
                    
                    if (firefighter != null && firefighter.Exists())
                    {
                        firefighter.Task.DriveTo(firetruck, position, 15f, VehicleDrivingFlags.None, 10f);
                        firefighter.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                    }
                    
                    Notification.PostTicker("~r~Pompiers~n~~w~Les pompiers interviennent", false, true);
                }
            }, "Fire Response");
        }

        private void SpawnCoastGuardResponse(Vector3 position)
        {
            ScheduleTask(6000 + _random.Next(4000), () =>
            {
                // Vérifier si le joueur est près de l'eau
                if (IsNearWater(position))
                {
                    var spawnPos = GetWaterSpawnPosition(position);
                    var boat = World.CreateVehicle(VehicleHash.Predator, spawnPos);
                    
                    if (boat != null && boat.Exists())
                    {
                        var coastGuard = boat.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Marine01SMM);
                        
                        if (coastGuard != null && coastGuard.Exists())
                        {
                            coastGuard.Task.DriveTo(boat, position, 15f, VehicleDrivingFlags.None, 10f);
                            coastGuard.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                        }
                        
                        Notification.PostTicker("~b~Garde-côtes~n~~w~Une patrouille maritime arrive", false, true);
                    }
                }
                else
                {
                    Notification.PostTicker("~y~Garde-côtes~n~~w~Vous n'êtes pas près de l'eau", false, true);
                }
            }, "Coast Guard Response");
        }

        private void ScheduleFoodDelivery(string restaurant, FoodItem food, Vector3 deliveryPos)
        {
            ScheduleTask(_random.Next(5, 11) * 1000, () =>
            {
                var spawnPos = GetSpawnPosition(deliveryPos, 80f);
                var deliveryVehicle = World.CreateVehicle(VehicleHash.Mule, spawnPos);
                
                if (deliveryVehicle != null && deliveryVehicle.Exists())
                {
                    var driver = deliveryVehicle.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Business01AMM);
                    
                    if (driver != null && driver.Exists())
                    {
                        driver.Task.DriveTo(deliveryVehicle, deliveryPos, 10f, VehicleDrivingFlags.None, 8f);
                        driver.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                        
                        // Marquer le véhicule sur la carte
                        var blip = deliveryVehicle.AddBlip();
                        if (blip != null)
                        {
                            blip.Sprite = BlipSprite.Package;
                            blip.Color = BlipColor.Green;
                            blip.Name = $"Livraison {restaurant}";
                        }
                        
                        Notification.PostTicker($"~g~Livraison en route!~n~~w~{food.Name} de {restaurant}~n~Regardez votre carte", false, true);
                    }
                }
            }, $"Food Delivery - {restaurant}");
        }

        private void ScheduleTowTruck(string serviceType, Vector3 position)
        {
            int delay = serviceType switch
            {
                "quick" => 2000,
                "scheduled" => 30000, // 30 secondes au lieu de 5 minutes
                "heavy" => 8000,
                _ => 5000
            };
            
            ScheduleTask(delay, () =>
            {
                var spawnPos = GetSpawnPosition(position, 100f);
                var towTruck = World.CreateVehicle(VehicleHash.TowTruck, spawnPos);
                
                if (towTruck != null && towTruck.Exists())
                {
                    var driver = towTruck.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Trucker01SMM);
                    
                    if (driver != null && driver.Exists())
                    {
                        driver.Task.DriveTo(towTruck, position, 10f, VehicleDrivingFlags.None, 8f);
                        driver.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                        
                        var blip = towTruck.AddBlip();
                        if (blip != null)
                        {
                            blip.Sprite = BlipSprite.TowTruck;
                            blip.Color = BlipColor.Yellow;
                            blip.Name = "Remorqueuse";
                        }
                        
                        Notification.PostTicker("~y~Remorqueuse~n~~w~La remorqueuse arrive sur les lieux", false, true);
                    }
                }
            }, $"Tow Truck - {serviceType}");
        }
        
        private Vector3 GetSpawnPosition(Vector3 center, float radius)
        {
            var angle = _random.NextDouble() * 2 * Math.PI;
            var distance = _random.Next(50, (int)radius);
            
            var x = center.X + (float)(Math.Cos(angle) * distance);
            var y = center.Y + (float)(Math.Sin(angle) * distance);
            
            // Utiliser la nouvelle méthode pour obtenir la hauteur du sol
            World.GetGroundHeight(new Vector3(x, y, center.Z), out float groundZ, GetGroundHeightMode.Normal);
            
            return new Vector3(x, y, groundZ);
        }
        
        private Vector3 GetWaterSpawnPosition(Vector3 playerPos)
        {
            // Positions d'eau connues près de Los Santos
            var waterPositions = new[]
            {
                new Vector3(-1600f, -1000f, 0f), // Océan ouest
                new Vector3(1200f, -3000f, 0f),  // Océan sud
                new Vector3(-300f, 6500f, 0f),   // Océan nord
                new Vector3(3500f, 3500f, 0f)    // Océan est
            };
            
            // Trouver la position d'eau la plus proche
            var closestWater = waterPositions
                .OrderBy(pos => playerPos.DistanceTo(pos))
                .First();
            
            return closestWater;
        }
        
        private bool IsNearWater(Vector3 position)
        {
            // Vérifier si le joueur est près de l'eau (approximation)
            return position.Z < 10f || // Probablement près de l'océan
                   (position.X < -1000f && position.Y > -2000f) || // Côte ouest
                   (position.Y < -2500f) || // Côte sud
                   (position.Y > 6000f);    // Côte nord
        }
        
        public void OpenPhone()
        {
            if (!_isMenuOpen)
            {
                _mainMenu.Visible = true;
                _isMenuOpen = true;
                
                Notification.PostTicker("~b~Téléphone ouvert~n~~w~Utilisez les flèches pour naviguer", false, true);
            }
        }
        
        public void ClosePhone()
        {
            _mainMenu.Visible = false;
            _emergencyServicesMenu.Visible = false;
            _foodDeliveryMenu.Visible = false;
            _towingServiceMenu.Visible = false;
            _isMenuOpen = false;
        }
        
        private void ProcessPendingTasks()
        {
            try
            {
                var now = DateTime.Now;
                for (int i = _pendingTasks.Count - 1; i >= 0; i--)
                {
                    var task = _pendingTasks[i];
                    if (now >= task.ExecuteAt)
                    {
                        try
                        {
                            task.Task?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error executing delayed task '{task.Description}': {ex.Message}");
                        }
                        finally
                        {
                            _pendingTasks.RemoveAt(i);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing pending tasks: {ex.Message}");
            }
        }
        
        private void ScheduleTask(int delayMs, Action task, string description = "")
        {
            _pendingTasks.Add(new DelayedTask(delayMs, task, description));
        }
        
        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _menuPool.Process();
                
                // Traiter les tâches en attente
                ProcessPendingTasks();
                
                // Désactiver le téléphone du jeu si notre menu est ouvert
                if (_isMenuOpen)
                {
                    Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 27, true); // Phone
                    Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, 19, true); // Alt
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Phone menu tick error: {ex.Message}");
            }
        }
        
        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            try
            {
                // Ouvrir/fermer le téléphone avec la touche T
                if (e.KeyCode == System.Windows.Forms.Keys.T)
                {
                    if (_isMenuOpen)
                        ClosePhone();
                    else
                        OpenPhone();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Phone menu key error: {ex.Message}");
            }
        }
        
        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                ClosePhone();
                
                // Nettoyer toutes les tâches en attente
                _pendingTasks.Clear();
                
                Logger.Info("Phone Menu Manager (Simple) unloaded.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Phone menu cleanup error: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Représente un plat d'un restaurant
    /// </summary>
    public class FoodItem
    {
        public string Name { get; }
        public decimal Price { get; }
        public string Description { get; }
        
        public FoodItem(string name, decimal price, string description)
        {
            Name = name;
            Price = price;
            Description = description;
        }
    }
}