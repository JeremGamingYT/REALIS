using System;
using GTA;
using GTA.UI;
using LemonUI;
using LemonUI.Menus;
using Newtonsoft.Json;

namespace REALIS.Core
{
    /// <summary>
    /// Menu principal REALIS utilisant LemonUI - Version corrigée
    /// </summary>
    public class REALISMenuFixed : Script
    {
        private readonly ObjectPool menuPool;
        private readonly NativeMenu mainMenu;
        private readonly NativeMenu vehicleMenu;
        private readonly NativeMenu policeMenu;

        // État de REALIS
        private bool isRealisActive = true;
        private bool realisticPoliceEnabled = true;
        private float economyMultiplier = 1.0f;

        public REALISMenuFixed()
        {
            // Initialisation du pool de menus LemonUI
            menuPool = new ObjectPool();

            // Création du menu principal
            mainMenu = new NativeMenu("REALIS", "Système de Réalisme v2.0");
            vehicleMenu = new NativeMenu("REALIS", "Gestion des Véhicules");
            policeMenu = new NativeMenu("REALIS", "Système de Police");

            // Configuration des menus
            SetupMainMenu();
            SetupVehicleMenu();
            SetupPoliceMenu();

            // Ajout de tous les menus au pool
            menuPool.Add(mainMenu);
            menuPool.Add(vehicleMenu);
            menuPool.Add(policeMenu);

            // Events
            Tick += OnTick;
            KeyDown += OnKeyDown;

            // Notification de bienvenue
            GTA.UI.Screen.ShowSubtitle("~g~REALIS Menu LemonUI~w~ chargé ! Appuyez sur ~b~F9~w~ pour ouvrir");
        }

        private void SetupMainMenu()
        {
            // Toggle principal REALIS
            var toggleRealis = new NativeCheckboxItem("Activer REALIS", isRealisActive)
            {
                Description = "Active ou désactive l'ensemble du système REALIS"
            };
            toggleRealis.CheckboxChanged += (sender, args) =>
            {
                isRealisActive = toggleRealis.Checked;
                GTA.UI.Screen.ShowSubtitle($"REALIS {(toggleRealis.Checked ? "~g~activé" : "~r~désactivé")}~w~");
            };
            mainMenu.Add(toggleRealis);

            // Séparateur
            mainMenu.Add(new NativeSeparatorItem("Modules REALIS"));

            // Item véhicules
            var vehicleItem = new NativeItem("🚗 Véhicules & Concessionnaires", "Gestion des véhicules, concessionnaires et économie automobile");
            vehicleItem.Activated += (sender, args) =>
            {
                mainMenu.Visible = false;
                vehicleMenu.Visible = true;
            };
            mainMenu.Add(vehicleItem);

            // Item police
            var policeItem = new NativeItem("👮 Système de Police", "Configuration du système de police réaliste");
            policeItem.Activated += (sender, args) =>
            {
                mainMenu.Visible = false;
                policeMenu.Visible = true;
            };
            mainMenu.Add(policeItem);

            // Item application de livraison
            var foodAppItem = new NativeItem("📱 QuickEats App", "Ouvrir l'application de livraison de nourriture");
            foodAppItem.Activated += (sender, args) =>
            {
                mainMenu.Visible = false;
                // Ici, nous pourrions déclencher l'ouverture de l'app QuickEats
                GTA.UI.Screen.ShowSubtitle("~b~Appuyez sur F8 pour ouvrir QuickEats~w~ (ou intégration directe si disponible)");
            };
            mainMenu.Add(foodAppItem);

            // Séparateur
            mainMenu.Add(new NativeSeparatorItem("Actions Rapides"));

            // Sauvegarder la configuration
            var saveConfig = new NativeItem("💾 Sauvegarder Config", "Sauvegarde la configuration actuelle");
            saveConfig.Activated += SaveConfiguration;
            mainMenu.Add(saveConfig);

            // Fermer le menu
            var closeItem = new NativeItem("❌ Fermer le Menu");
            closeItem.Activated += (sender, args) => mainMenu.Visible = false;
            mainMenu.Add(closeItem);
        }

        private void SetupVehicleMenu()
        {
            // Liste des concessionnaires
            var dealershipList = new NativeListItem<string>("Concessionnaire Actif",
                "Sélectionnez le concessionnaire à gérer",
                "Premium Deluxe Motorsport", "Simeon Yetarian", "Super Autos", 
                "Southern SA Auto", "Luxury Autos");

            dealershipList.ItemChanged += (sender, args) =>
            {
                GTA.UI.Screen.ShowSubtitle($"~b~Concessionnaire sélectionné:~w~ {args.Object}");
            };
            vehicleMenu.Add(dealershipList);

            // Toggle pour concessionnaires réalistes
            var realisticDealers = new NativeCheckboxItem("Concessionnaires Réalistes", true)
            {
                Description = "Active les heures d'ouverture et stock limité"
            };
            vehicleMenu.Add(realisticDealers);

            // Actions véhicules
            vehicleMenu.Add(new NativeSeparatorItem("Actions"));

            var spawnTestVehicle = new NativeItem("🚗 Test Drive", "Fait apparaître un véhicule de test");
            spawnTestVehicle.Activated += SpawnTestVehicle;
            vehicleMenu.Add(spawnTestVehicle);

            var repairCurrentVehicle = new NativeItem("🔧 Réparer Véhicule", "Répare le véhicule actuel");
            repairCurrentVehicle.Activated += RepairCurrentVehicle;
            vehicleMenu.Add(repairCurrentVehicle);

            // Retour
            AddBackButton(vehicleMenu, mainMenu);
        }

        private void SetupPoliceMenu()
        {
            // Toggle police réaliste
            var realisticPolice = new NativeCheckboxItem("Police Réaliste", realisticPoliceEnabled)
            {
                Description = "Active le système de police réaliste avec IA améliorée"
            };
            realisticPolice.CheckboxChanged += (sender, args) =>
            {
                realisticPoliceEnabled = realisticPolice.Checked;
                GTA.UI.Screen.ShowSubtitle($"Police réaliste {(realisticPolice.Checked ? "~g~activée" : "~r~désactivée")}~w~");
            };
            policeMenu.Add(realisticPolice);

            // Agressivité de la police
            var policeAggression = new NativeListItem<string>("Agressivité Police",
                "Définit le comportement de la police",
                "Très Passive", "Passive", "Normale", "Agressive", "Très Agressive");
            policeMenu.Add(policeAggression);

            // Séparateur
            policeMenu.Add(new NativeSeparatorItem("Actions Rapides"));

            // Effacer niveau de recherche
            var clearWanted = new NativeItem("🚫 Effacer Recherche", "Remet le niveau à 0");
            clearWanted.Activated += (sender, args) =>
            {
                Game.Player.Wanted.SetWantedLevel(0, false);
                Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
                GTA.UI.Screen.ShowSubtitle("~g~Niveau de recherche effacé");
            };
            policeMenu.Add(clearWanted);

            // Spawner police
            var spawnPolice = new NativeItem("🚔 Spawner Patrouille", "Fait apparaître une patrouille");
            spawnPolice.Activated += SpawnPolicePatrol;
            policeMenu.Add(spawnPolice);

            AddBackButton(policeMenu, mainMenu);
        }

        private void AddBackButton(NativeMenu currentMenu, NativeMenu parentMenu)
        {
            currentMenu.Add(new NativeSeparatorItem(""));
            var backItem = new NativeItem("← Retour au Menu Principal");
            backItem.Activated += (sender, args) =>
            {
                currentMenu.Visible = false;
                parentMenu.Visible = true;
            };
            currentMenu.Add(backItem);
        }

        private void SpawnTestVehicle(object sender, EventArgs e)
        {
            var testVehicles = new[] { "adder", "zentorno", "t20", "osiris", "entityxf", "vacca", "infernus" };
            var randomVehicle = testVehicles[new Random().Next(testVehicles.Length)];

            var playerPos = Game.Player.Character.Position;
            var spawnPos = playerPos + Game.Player.Character.ForwardVector * 3;

            var vehicle = World.CreateVehicle(randomVehicle, spawnPos);
            if (vehicle != null)
            {
                vehicle.PlaceOnGround();
                vehicle.Repair();
                GTA.UI.Screen.ShowSubtitle($"~g~Véhicule de test spawné:~w~ {randomVehicle}");
            }
        }

        private void RepairCurrentVehicle(object sender, EventArgs e)
        {
            if (Game.Player.Character.IsInVehicle())
            {
                var vehicle = Game.Player.Character.CurrentVehicle;
                vehicle.Repair();
                vehicle.EngineHealth = 1000f;
                vehicle.PetrolTankHealth = 1000f;
                GTA.UI.Screen.ShowSubtitle("~g~Véhicule réparé !");
            }
            else
            {
                GTA.UI.Screen.ShowSubtitle("~r~Vous devez être dans un véhicule !");
            }
        }

        private void SpawnPolicePatrol(object sender, EventArgs e)
        {
            var playerPos = Game.Player.Character.Position;
            var spawnPos = playerPos + Game.Player.Character.RightVector * 10;

            var policeCar = World.CreateVehicle("police", spawnPos);
            if (policeCar != null)
            {
                policeCar.PlaceOnGround();
                
                // Créer un policier
                var cop = World.CreatePed(PedHash.Cop01SFY, spawnPos);
                if (cop != null)
                {
                    cop.SetIntoVehicle(policeCar, VehicleSeat.Driver);
                    cop.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                    cop.BlockPermanentEvents = true;
                }
                
                GTA.UI.Screen.ShowSubtitle("~b~Patrouille de police déployée");
            }
        }

        private void SaveConfiguration(object sender, EventArgs e)
        {
            try
            {
                var config = new
                {
                    IsActive = isRealisActive,
                    RealisticPolice = realisticPoliceEnabled,
                    EconomyMultiplier = economyMultiplier,
                    SavedAt = DateTime.Now
                };

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                System.IO.File.WriteAllText("scripts/REALIS_Config.json", json);
                
                GTA.UI.Screen.ShowSubtitle("~g~Configuration sauvegardée !");
            }
            catch (Exception ex)
            {
                GTA.UI.Screen.ShowSubtitle($"~r~Erreur sauvegarde: {ex.Message}");
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            // Traitement essentiel des menus LemonUI
            menuPool.Process();
        }

        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            // F9 pour ouvrir/fermer le menu principal
            if (e.KeyCode == System.Windows.Forms.Keys.F6)
            {
                mainMenu.Visible = !mainMenu.Visible;
            }
        }
    }
} 