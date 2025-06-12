using System;
using GTA;
using GTA.Native;
using GTA.Math;
using GTA.UI;
using LemonUI;
using LemonUI.Menus;
using LemonUI.Elements;

namespace REALIS.Core
{
    /// <summary>
    /// Gère la réparation des véhicules dans un garage
    /// </summary>
    public class RepairShopManager : Script
    {
        private readonly Vector3 _repairPoint = new Vector3(535.79f, -180.39f, 53.95f);
        private const float InteractionRange = 3.0f;

        private ObjectPool _menuPool;
        private NativeMenu? _repairMenu;
        private NativeItem? _repairItem;
        private NativeItem? _closeItem;
        // État de réparation asynchrone
        private bool _isRepairing = false;
        private DateTime _repairEndTime;
        // Hash du modèle du véhicule à réparer
        private VehicleHash? _repairVehicleHash;

        public RepairShopManager()
        {
            Tick += OnTick;
            Aborted += OnAborted;

            _menuPool = new ObjectPool();
            CreateRepairMenu();
            CreateBlip();
        }

        private void CreateBlip()
        {
            var blip = World.CreateBlip(_repairPoint);
            blip.Sprite = BlipSprite.Repair;
            blip.Color = BlipColor.Green;
            blip.Name = "Garage de réparation";
            blip.Scale = 0.9f;
            blip.IsShortRange = false;
        }

        private void CreateRepairMenu()
        {
            _repairMenu = new NativeMenu("Garage Réparation", "Réparer votre véhicule")
            {
                Alignment = Alignment.Left
            };
            _menuPool.Add(_repairMenu);

            _repairItem = new NativeItem("Réparer le véhicule", "Calcul en cours...")
            {
                Enabled = true
            };
            _repairItem.Activated += OnRepairActivated;
            _repairMenu.Add(_repairItem);

            _closeItem = new NativeItem("Fermer", "Fermer le menu");
            _closeItem.Activated += (s, e) => { _repairMenu!.Visible = false; };
            _repairMenu.Add(_closeItem);
        }

        private void OnTick(object sender, EventArgs e)
        {
            // Vérifier si la réparation asynchrone est terminée
            if (_isRepairing && DateTime.Now >= _repairEndTime)
            {
                CompleteRepair();
            }
            _menuPool.Process();

            var player = Game.Player.Character;
            float dist = player.Position.DistanceTo(_repairPoint);

            if (dist < InteractionRange)
            {
                if (player.CurrentVehicle != null && player.CurrentVehicle.Exists())
                {
                    GTA.UI.Screen.ShowSubtitle("~INPUT_CONTEXT~ Ouvrir le menu réparation", 100);

                    if (Game.IsControlJustPressed(GTA.Control.Context))
                    {
                        // Met à jour la description en fonction des dégâts
                        var veh = player.CurrentVehicle;
                        float maxHealth = 1000f;
                        int missingHealth = (int)Math.Max(0, maxHealth - veh.Health);
                        int timeSec = (int)Math.Ceiling(missingHealth / 10f);
                        _repairItem!.Description = missingHealth > 0
                            ? $"Temps estimé: {timeSec}s"
                            : "Aucun dommage détecté.";
                        _repairItem!.Enabled = missingHealth > 0;

                        _repairMenu!.Visible = true;
                    }
                }
                else
                {
                    GTA.UI.Screen.ShowSubtitle("~y~Montez dans un véhicule pour accéder à la réparation", 100);
                }
            }
        }

        private void OnRepairActivated(object sender, EventArgs e)
        {
            var player = Game.Player.Character;
            var veh = player.CurrentVehicle;

            if (veh == null || !veh.Exists())
            {
                Notification.PostTicker("~r~Pas de véhicule à réparer", false, true);
                return;
            }

            float maxHealth = 1000f;
            int missingHealth = (int)Math.Max(0, maxHealth - veh.Health);
            int timeSec = (int)Math.Ceiling(missingHealth / 10f);
            if (timeSec <= 0)
            {
                Notification.PostTicker("~g~Votre véhicule n'a pas de dégâts", false, true);
                return;
            }

            // Cinématique fade
            GTA.UI.Screen.FadeOut(1000);
            Script.Wait(1000);

            GTA.UI.Screen.FadeIn(1000);
            Script.Wait(1000);

            // Notification de démarrage asynchrone
            Notification.PostTicker($"~b~Réparation en cours ({timeSec}s) en arrière-plan", false, true);

            // Planifier la fin de réparation et stocker le modèle
            _repairVehicleHash = (VehicleHash)veh.Model.Hash;
            _isRepairing = true;
            _repairEndTime = DateTime.Now.AddSeconds(timeSec);
            // Supprimer le véhicule pour qu'il ne soit plus conduisible
            veh.Delete();
            _repairMenu!.Visible = false;
        }

        /// <summary>
        /// Termine la réparation planifiée et notifie le joueur
        /// </summary>
        private void CompleteRepair()
        {
            if (_repairVehicleHash.HasValue)
            {
                var hash = _repairVehicleHash.Value;
                var model = new Model(hash);
                model.Request(5000);
                if (model.IsLoaded)
                {
                    var newVeh = World.CreateVehicle(model, _repairPoint);
                    if (newVeh != null && newVeh.Exists())
                    {
                        newVeh.IsPersistent = true;
                        newVeh.Repair();
                        newVeh.PlaceOnGround();
                        var blip = newVeh.AddBlip();
                        blip.Sprite = BlipSprite.PersonalVehicleCar;
                        blip.Color = BlipColor.Green;
                        blip.Name = "Véhicule réparé";
                        blip.IsShortRange = false;
                        Notification.PostTicker("~g~Réparation terminée ! Votre véhicule est prêt", false, true);
                    }
                    model.MarkAsNoLongerNeeded();
                }
            }
            _isRepairing = false;
            _repairVehicleHash = null;
        }

        private void OnAborted(object sender, EventArgs e)
        {
            if (_repairMenu != null)
            {
                _repairMenu.Visible = false;
                _repairMenu.Clear();
            }
        }
    }
} 