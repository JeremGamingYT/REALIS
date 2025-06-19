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

        // Gestion des unités de renfort actives
        private class BackupUnit
        {
            public Vehicle Veh;
            public Ped Driver;
            public Ped Officer;
            public bool Chasing;
            // Indique si l'unité est en route vers le joueur (pas de poursuite suspect).
            public bool DrivingToPlayer;

            public bool IsValid => Veh != null && Veh.Exists() && Driver != null && Driver.Exists();
        }

        private readonly System.Collections.Generic.List<BackupUnit> _activeBackups = new System.Collections.Generic.List<BackupUnit>();
        private readonly System.Collections.Generic.List<(Ped, Ped, Ped)> _pendingChaseTargets = new System.Collections.Generic.List<(Ped, Ped, Ped)>();

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

            // Met à jour le comportement des unités de renfort
            UpdateBackups();
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
                // Laisse le groupe relationnel par défaut (COP) pour conserver l'IA policière
                cop.AlwaysKeepTask = true; // évite que le moteur annule la tâche de conduite
            }

            // Conduite du véhicule vers la position du joueur / suspect
            Ped target = REALIS.Police.Callouts.StolenVehicleCallout.CurrentSuspect;
            Vector3 dest = target?.Position ?? player.Position;

            // Démarre par une croisière vers la destination pour éviter l'immobilisme initial
            driver.Task.CruiseWithVehicle(veh, 30f, DrivingStyle.Normal);

            if (target != null && target.Exists())
            {
                // Quelques millisecondes plus tard (UpdateBackups), on lancera une vraie poursuite
                // pour l'instant on note qu'on souhaite poursuivre
                _pendingChaseTargets.Add((driver, officer, target));
            }

            // Maintient les tâches actives.
            officer.AlwaysKeepTask = true;

            GTA.UI.Notification.Show("~b~Renforts en route");

            // Enregistre l'unité pour suivi
            _activeBackups.Add(new BackupUnit
            {
                Veh = veh,
                Driver = driver,
                Officer = officer,
                Chasing = target != null && target.Exists(),
                DrivingToPlayer = false
            });
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

        private void UpdateBackups()
        {
            var player = Game.Player.Character;

            Ped suspect = REALIS.Police.Callouts.StolenVehicleCallout.CurrentSuspect;
            bool suspectValid = suspect != null && suspect.Exists() && !suspect.IsDead && !suspect.IsCuffed;

            for (int i = _activeBackups.Count - 1; i >= 0; i--)
            {
                var unit = _activeBackups[i];

                if (!unit.IsValid)
                {
                    _activeBackups.RemoveAt(i);
                    continue;
                }

                // Rafraîchit la sirène si elle s'est éteinte
                if (!Function.Call<bool>(Hash.IS_VEHICLE_SIREN_ON, unit.Veh))
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, unit.Veh, true);
                }

                if (suspectValid)
                {
                    // S'assurer qu'ils poursuivent toujours
                    if (!unit.Chasing)
                    {
                        Function.Call(Hash.TASK_VEHICLE_CHASE, unit.Driver.Handle, suspect.Handle);
                        Function.Call(Hash.TASK_VEHICLE_CHASE, unit.Officer.Handle, suspect.Handle);
                        unit.Chasing = true;
                        unit.DrivingToPlayer = false;
                    }
                }
                else
                {
                    // Suspect neutralisé : rejoindre le joueur et se mettre en attente
                    if (unit.Chasing)
                    {
                        unit.Driver.Task.ClearAllImmediately();
                        unit.Officer.Task.ClearAllImmediately();
                        unit.Chasing = false;
                    }

                    float dist = unit.Veh.Position.DistanceTo(player.Position);

                    // Si l'unité est encore loin, on lance (une seule fois) la conduite vers le joueur.
                    if (dist > 8f)
                    {
                        if (!unit.DrivingToPlayer)
                        {
                            unit.Driver.Task.DriveTo(unit.Veh, player.Position, 5f, 30f, DrivingStyle.Normal);
                            unit.DrivingToPlayer = true;
                        }
                    }
                    else if (unit.Driver.IsInVehicle(unit.Veh))
                    {
                        // Arrivé près du joueur : les agents descendent.
                        unit.Driver.Task.LeaveVehicle(unit.Veh, true);
                        unit.Officer.Task.LeaveVehicle(unit.Veh, true);
                        unit.DrivingToPlayer = false;
                    }
                }
            }

            // Applique les poursuites en attente (une fois que tout est chargé)
            if (_pendingChaseTargets.Count > 0)
            {
                for (int p = _pendingChaseTargets.Count - 1; p >= 0; p--)
                {
                    var (drv, off, tgt) = _pendingChaseTargets[p];
                    if (drv.Exists() && off.Exists() && tgt.Exists())
                    {
                        Function.Call(Hash.TASK_VEHICLE_CHASE, drv.Handle, tgt.Handle);
                        Function.Call(Hash.TASK_VEHICLE_CHASE, off.Handle, tgt.Handle);
                    }
                    _pendingChaseTargets.RemoveAt(p);
                }
            }
        }
    }
} 