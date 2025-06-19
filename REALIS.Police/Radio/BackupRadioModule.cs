using System;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using LemonUI.Menus;
using LemonUI;
using REALIS.Common;
using GTA.Native;

namespace REALIS.Police.Radio
{
    /// <summary>
    /// Menu radio LemonUI pour demander des renforts et autres interactions dispatch.
    /// </summary>
    public class BackupRadioModule : IModule
    {
        private const Keys ToggleKey = Keys.B; // touche radio
        private ObjectPool _pool;
        private NativeMenu _menu;

        public void Initialize()
        {
            _pool = new ObjectPool();

            _menu = new NativeMenu("Radio", "Menu Dispatch");
            _menu.Add(new NativeItem("Demander unité de soutien"));          // 0
            _menu.Add(new NativeItem("Demander unité SWAT"));               // 1
            _menu.Add(new NativeItem("Vérifier plaque du véhicule ciblé")); // 2
            _menu.Add(new NativeItem("Changer statut (Disponible / Occupé)")); // 3
            _menu.Add(new NativeItem("Demander un callout"));               // 4
            _menu.Add(new NativeItem("Terminer le callout en cours"));      // 5

            _menu.ItemActivated += OnItemActivated;
            _pool.Add(_menu);
        }

        public void Update()
        {
            if (!DutyState.PoliceOnDuty) return;

            // Ouvrir/fermer le menu
            if (Game.IsKeyPressed(ToggleKey))
            {
                _menu.Visible = !_menu.Visible;
            }

            // Process UI
            _pool.Process();
        }

        public void Dispose()
        {
            _menu.Visible = false;
        }

        private void OnItemActivated(object sender, ItemActivatedArgs e)
        {
            int idx = _menu.Items.IndexOf(e.Item);
            switch (idx)
            {
                case 0:
                    SpawnBackup(false);
                    break;
                case 1:
                    SpawnBackup(true);
                    break;
                case 2:
                    CheckPlate();
                    break;
                case 3:
                    ToggleAvailability();
                    break;
                case 4:
                    CalloutManager.Instance?.OfferImmediateCallout();
                    break;
                case 5:
                    CalloutManager.Instance?.ForceEndActive();
                    break;
            }
        }

        private void SpawnBackup(bool heavy)
        {
            var player = Game.Player.Character;

            // Création d'un véhicule de police avec 2 officiers
            Vector3 spawnPos = World.GetNextPositionOnStreet(player.Position + player.ForwardVector * 60f);

            Model vehModel = heavy ? new Model(VehicleHash.Riot) : new Model(VehicleHash.Police2);
            Model copModel = new Model(PedHash.Cop01SMY);
            vehModel.Request(500);
            copModel.Request(500);

            if (!vehModel.IsLoaded || !copModel.IsLoaded)
            {
                GTA.UI.Notification.Show("~r~Impossible de générer le véhicule de renfort");
                return;
            }

            Vehicle veh = World.CreateVehicle(vehModel, spawnPos, player.Heading);
            Function.Call(Hash.SET_VEHICLE_SIREN, veh, true);
            veh.IsPersistent = true;

            Ped driver = veh.CreatePedOnSeat(VehicleSeat.Driver, copModel);
            Ped officer = veh.CreatePedOnSeat(VehicleSeat.Passenger, copModel);

            foreach (var cop in new[] { driver, officer })
            {
                if (!cop.Exists()) continue;
                cop.Weapons.Give(heavy ? WeaponHash.CarbineRifle : WeaponHash.Pistol, 200, true, true);
                cop.RelationshipGroup = player.RelationshipGroup;

                // Pas de blip pour garder l'interface épurée
            }

            // Conduite du véhicule vers la position du joueur / suspect
            Ped target = REALIS.Police.Callouts.StolenVehicleCallout.CurrentSuspect;
            Vector3 dest = target?.Position ?? player.Position;

            // Nouvelle logique : si une cible est disponible, on poursuit le suspect en véhicule ;
            // sinon on roule simplement jusqu'à la position du joueur.
            if (target != null && target.Exists())
            {
                // Utilise la native TASK_VEHICLE_CHASE pour démarrer une poursuite automatique
                Function.Call(Hash.TASK_VEHICLE_CHASE, driver.Handle, target.Handle);

                // Officier passager participe à la poursuite et aux tirs
                Function.Call(Hash.TASK_VEHICLE_CHASE, officer.Handle, target.Handle);
            }
            else
            {
                // Pas de suspect identifié : le conducteur rejoint la position du joueur.
                driver.Task.DriveTo(veh, dest, 10f, 35f, DrivingStyle.Rushed);
            }

            // Maintient les tâches actives.
            officer.AlwaysKeepTask = true;

            GTA.UI.Notification.Show("~b~Renforts en route");
        }

        private void CheckPlate()
        {
            var player = Game.Player.Character;

            // Rayon devant le joueur ou caméra pour détecter un véhicule
            Vector3 source = GameplayCamera.Position;
            Vector3 dir = GameplayCamera.Direction;
            float maxDist = 40f;

            RaycastResult hit = World.Raycast(source, dir, maxDist, IntersectFlags.Vehicles, player);

            Vehicle targetVeh = null;

            if (hit.DidHit && hit.HitEntity is Vehicle)
            {
                targetVeh = (Vehicle)hit.HitEntity;
            }
            else
            {
                // Si aucun hit, prendre le véhicule le plus proche du rayon dans un petit cône
                targetVeh = World.GetClosestVehicle(source + dir * 10f, 8f);
            }

            if (targetVeh != null && targetVeh.Exists())
            {
                // Récupère la plaque via native
                string plate = Function.Call<string>(Hash.GET_VEHICLE_NUMBER_PLATE_TEXT, targetVeh);
                GTA.UI.Notification.Show($"~b~Dispatch:~w~ Plaque {plate}");
            }
            else
            {
                GTA.UI.Notification.Show("~r~Aucun véhicule détecté");
            }
        }

        private bool _available = true;
        private void ToggleAvailability()
        {
            _available = !_available;
            GTA.UI.Notification.Show(_available
                ? "~g~Statut: Disponible"
                : "~o~Statut: Occupé");
        }
    }
} 