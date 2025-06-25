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
        private NativeMenu _backupMenu; // sous-menu pour les différentes unités
        private NativeMenu _serviceMenu; // pompiers, ambulance, etc.
        private NativeMenu _lookupMenu; // options de recherche
        private NativeMenu _calloutMenu; // sous-menu pour choisir un callout spécifique
        private bool _toggleKeyHeld = false;

        private enum BackupKind
        {
            LSPD,
            Sheriff,
            FBI,
            SWAT,
            AirSupport
        }

        private enum ServiceKind
        {
            TowTruck,
            Ambulance,
            Firefighters
        }

        private enum LookupKind
        {
            Plate,
            Person
        }

        // Gestion des unités de renfort actives
        private class BackupUnit
        {
            public Vehicle Veh;
            public Ped Driver;
            public Ped Officer;
            public bool Chasing;
            public Blip Blip; // blip associé à l'unité
            // Indique si l'unité est en route vers le joueur (pas de poursuite suspect).
            public bool DrivingToPlayer;
            public DateTime LastColorSwitch = DateTime.MinValue;

            public bool IsValid => Veh != null && Veh.Exists() && Driver != null && Driver.Exists();
        }

        private readonly System.Collections.Generic.List<BackupUnit> _activeBackups = new System.Collections.Generic.List<BackupUnit>();
        private readonly System.Collections.Generic.List<(Ped, Ped, Ped)> _pendingChaseTargets = new System.Collections.Generic.List<(Ped, Ped, Ped)>();

        public static bool Available { get; private set; } = true;

        public void Initialize()
        {
            _pool = new ObjectPool();

            // Menu principal
            _menu = new NativeMenu("Dispatch", "Console radio");

            // Sous-menu : Demande de renfort
            _backupMenu = new NativeMenu("Renforts", "Sélection d'unité");
            _backupMenu.Add(new NativeItem("Unité LSPD"));       // 0
            _backupMenu.Add(new NativeItem("Unité Sheriff"));    // 1
            _backupMenu.Add(new NativeItem("Unité FIB"));        // 2
            _backupMenu.Add(new NativeItem("Unité SWAT"));       // 3
            _backupMenu.Add(new NativeItem("Support aérien"));   // 4

            _backupMenu.ItemActivated += OnBackupItemActivated;

            _menu.AddSubMenu(_backupMenu, "Renforts"); // index 0

            // Sous-menu : Services publics
            _serviceMenu = new NativeMenu("Services", "Demandes de service");
            _serviceMenu.Add(new NativeItem("Dépanneuse"));  // 0
            _serviceMenu.Add(new NativeItem("Ambulance"));   // 1
            _serviceMenu.Add(new NativeItem("Pompiers"));    // 2
            _serviceMenu.ItemActivated += OnServiceItemActivated;
            _menu.AddSubMenu(_serviceMenu, "Services"); // index 1

            // Sous-menu recherche
            _lookupMenu = new NativeMenu("Recherche", "Requêtes");
            _lookupMenu.Add(new NativeItem("Plaque d'immatriculation")); // 0
            _lookupMenu.Add(new NativeItem("Personne (nom)"));           // 1
            _lookupMenu.ItemActivated += OnLookupItemActivated;
            _menu.AddSubMenu(_lookupMenu, "Recherches"); // index 2

            // Sous-menu callouts
            _calloutMenu = new NativeMenu("Callouts", "Choisir une mission");
            _calloutMenu.Add(new NativeItem("TEST CALLOUT"));            // 0 
            _calloutMenu.Add(new NativeItem("Véhicule volé"));           // 1
            _calloutMenu.Add(new NativeItem("Course de rue"));           // 2
            _calloutMenu.Add(new NativeItem("Prise d'otage"));           // 3
            _calloutMenu.Add(new NativeItem("Guerre de cartels"));       // 4
            _calloutMenu.Add(new NativeItem("Catastrophe naturelle"));   // 5
            _calloutMenu.Add(new NativeItem("Braquage de banque"));      // 6
            _calloutMenu.Add(new NativeItem("Attaque terroriste"));      // 7
            _calloutMenu.Add(new NativeItem("Callout aléatoire"));       // 8
            _calloutMenu.ItemActivated += OnCalloutItemActivated;
            _menu.AddSubMenu(_calloutMenu, "Choisir callout"); // index 3

            // Autres options du menu principal
            _menu.Add(new NativeItem("Vérifier plaque véhicule ciblé")); // index 4
            _menu.Add(new NativeItem("Statut (Disponible / Occupé)"));   // 5
            _menu.Add(new NativeItem("Terminer le callout en cours"));   // 6
            _menu.Add(new NativeItem("Renvoyer tous les renforts"));    // 7

            _menu.ItemActivated += OnItemActivated;

            _pool.Add(_menu);
            _pool.Add(_backupMenu);
            _pool.Add(_serviceMenu);
            _pool.Add(_lookupMenu);
            _pool.Add(_calloutMenu);
        }

        public void Update()
        {
            if (!DutyState.PoliceOnDuty) return;

            // Ouvrir/fermer le menu
            bool keyDown = Game.IsKeyPressed(ToggleKey);
            if (keyDown && !_toggleKeyHeld)
            {
                _menu.Visible = !_menu.Visible;
            }
            _toggleKeyHeld = keyDown;

            // Process UI
            _pool.Process();

            HandleLongPressLookup();

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

            // 0,1,2,3 sont des sous-menus (renforts, services, recherches, callouts)
            switch (idx)
            {
                case 4:
                    CheckPlate();
                    break;
                case 5:
                    ToggleAvailability();
                    break;
                case 6:
                    CalloutManager.Instance?.ForceEndActive();
                    break;
                case 7:
                    DismissAllBackups();
                    break;
            }
        }

        private void OnBackupItemActivated(object sender, ItemActivatedArgs e)
        {
            int idx = _backupMenu.Items.IndexOf(e.Item);
            switch (idx)
            {
                case 0:
                    SpawnBackup(BackupKind.LSPD);
                    break;
                case 1:
                    SpawnBackup(BackupKind.Sheriff);
                    break;
                case 2:
                    SpawnBackup(BackupKind.FBI);
                    break;
                case 3:
                    SpawnBackup(BackupKind.SWAT);
                    break;
                case 4:
                    SpawnBackup(BackupKind.AirSupport);
                    break;
            }
        }

        private void SpawnBackup(BackupKind kind)
        {
            switch (kind)
            {
                case BackupKind.LSPD:
                    SpawnGroundBackup(VehicleHash.Police2, PedHash.Cop01SMY, false);
                    break;
                case BackupKind.Sheriff:
                    SpawnGroundBackup(VehicleHash.Sheriff2, PedHash.Sheriff01SMY, false);
                    break;
                case BackupKind.FBI:
                    SpawnGroundBackup(VehicleHash.FBI2, PedHash.Cop01SMY, true);
                    break;
                case BackupKind.SWAT:
                    SpawnGroundBackup(VehicleHash.Riot, PedHash.Swat01SMY, true);
                    break;
                case BackupKind.AirSupport:
                    SpawnAirSupport();
                    break;
            }
        }

        private void SpawnGroundBackup(VehicleHash vehHash, PedHash copHash, bool heavyWeapons)
        {
            var player = Game.Player.Character;

            // S'il y a déjà un suspect, faisons apparaître l'unité juste à côté de lui afin qu'elle intervienne plus vite.
            Ped target = REALIS.Police.Callouts.StolenVehicleCallout.CurrentSuspect;

            Vector3 basePos = (target != null && target.Exists()) ? target.Position : player.Position;
            Vector3 forward = (target != null && target.Exists()) ? target.ForwardVector : player.ForwardVector;

            // Tente de trouver une route proche pour spawn.
            Vector3 spawnPos = World.GetNextPositionOnStreet(basePos + forward * 20f);

            Model vehModel = new Model(vehHash);
            Model copModel = new Model(copHash);
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
                cop.Weapons.Give(heavyWeapons ? WeaponHash.CarbineRifle : WeaponHash.Pistol, 200, true, true);
                cop.Weapons.Give(WeaponHash.StunGun, 4, true, true);
                cop.Weapons.Select(WeaponHash.StunGun);
                cop.AlwaysKeepTask = true;

                SetFriendlyToPlayer(cop);
            }

            // Destination immédiate : suspect ou joueur
            Vector3 dest = (target != null && target.Exists()) ? target.Position : player.Position;

            // Rend le conducteur plus compétent et agressif
            Function.Call(Hash.SET_DRIVER_ABILITY, driver, 1.0f);
            Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver, 1.0f);
            driver.Task.DriveTo(veh, dest, 10f, 45f, DrivingStyle.Rushed);

            if (target != null && target.Exists())
            {
                // Le conducteur poursuit le suspect mais les agents n'ouvrent pas le feu tout de suite.
                Function.Call(Hash.TASK_VEHICLE_CHASE, driver.Handle, target.Handle);
                // L'officier passager reste passif pour l'instant.
            }
            else
            {
                // Pas de suspect : conduite rapide vers le joueur
                driver.Task.DriveTo(veh, dest, 10f, 45f, DrivingStyle.Rushed);
            }

            officer.AlwaysKeepTask = true;

            GTA.UI.Notification.Show("~b~Renforts en route");

            var blip = veh.AddBlip();
            blip.Sprite = BlipSprite.Standard;
            blip.Color = BlipColor.Red;
            blip.Scale = 0.6f;
            blip.IsFlashing = true;

            _activeBackups.Add(new BackupUnit
            {
                Veh = veh,
                Driver = driver,
                Officer = officer,
                Chasing = (target != null && target.Exists()),
                DrivingToPlayer = (target == null || !target.Exists()),
                Blip = blip
            });
        }

        private void SpawnAirSupport()
        {
            var player = Game.Player.Character;

            Vector3 spawnPos = player.Position + new Vector3(0, 0, 80f);

            Model heliModel = new Model(VehicleHash.Polmav);
            Model pilotModel = new Model(PedHash.Pilot01SMY);
            heliModel.Request(500);
            pilotModel.Request(500);

            if (!heliModel.IsLoaded || !pilotModel.IsLoaded)
            {
                GTA.UI.Notification.Show("~r~Impossible de générer l'hélicoptère de soutien");
                return;
            }

            Vehicle heli = World.CreateVehicle(heliModel, spawnPos);
            heli.IsPersistent = true;
            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, heli, true, true, false);

            Ped pilot = heli.CreatePedOnSeat(VehicleSeat.Driver, pilotModel);
            if (pilot.Exists())
            {
                pilot.Weapons.Give(WeaponHash.CarbineRifle, 200, true, true);
                pilot.AlwaysKeepTask = true;

                SetFriendlyToPlayer(pilot);

                // Optionnel : le pilote maintiendra une position au-dessus du joueur automatiquement.
            }

            var blip = heli.AddBlip();
            blip.Sprite = BlipSprite.Standard;
            blip.Color = BlipColor.Red;
            blip.Scale = 0.6f;
            blip.IsFlashing = true;

            _activeBackups.Add(new BackupUnit { Veh = heli, Driver = pilot, Officer = null, Chasing = false, DrivingToPlayer = true, Blip = blip });

            GTA.UI.Notification.Show("~b~Support aérien en route");
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

        private void ToggleAvailability()
        {
            Available = !Available;
            GTA.UI.Notification.Show(Available
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
                    if (unit.Blip != null && unit.Blip.Exists()) unit.Blip.Delete();
                    _activeBackups.RemoveAt(i);
                    continue;
                }

                // Rafraîchit la sirène si elle s'est éteinte
                if (!Function.Call<bool>(Hash.IS_VEHICLE_SIREN_ON, unit.Veh))
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, unit.Veh, true);
                }

                // Alternance couleur blip rouge/bleu toutes 800 ms
                if (unit.Blip != null && unit.Blip.Exists())
                {
                    if (unit.LastColorSwitch == DateTime.MinValue || (DateTime.Now - unit.LastColorSwitch).TotalMilliseconds > 800)
                    {
                        unit.Blip.Color = unit.Blip.Color == BlipColor.Red ? BlipColor.Blue : BlipColor.Red;
                        unit.LastColorSwitch = DateTime.Now;
                    }
                }

                if (suspectValid)
                {
                    // S'assurer qu'ils poursuivent toujours (uniquement si le conducteur est encore dans le véhicule)
                    if (!unit.Chasing && unit.Driver.IsInVehicle(unit.Veh))
                    {
                        Function.Call(Hash.TASK_VEHICLE_CHASE, unit.Driver.Handle, suspect.Handle);
                        if (unit.Officer != null && unit.Officer.Exists()) Function.Call(Hash.TASK_COMBAT_PED, unit.Officer.Handle, suspect.Handle, 0, 16);
                        unit.Chasing = true;
                        unit.DrivingToPlayer = false;
                    }

                    // Lorsque l'unité est assez proche, les agents descendent pour se battre à pied.
                    float distToSuspect = unit.Veh.Position.DistanceTo(suspect.Position);
                    if (distToSuspect < 25f && unit.Driver.IsInVehicle(unit.Veh))
                    {
                        unit.Driver.Task.LeaveVehicle(unit.Veh, true);
                        unit.Officer?.Task.LeaveVehicle(unit.Veh, true);
                    }

                    // Après descente : déterminer si suspect hostile
                    if (!unit.Driver.IsInVehicle())
                    {
                        bool hostile = suspect.IsShooting || suspect.IsInCombat;

                        if (hostile)
                        {
                            // Passage létal
                            if (unit.Driver.Weapons.HasWeapon(WeaponHash.CarbineRifle))
                                unit.Driver.Weapons.Select(WeaponHash.CarbineRifle);
                            else
                                unit.Driver.Weapons.Select(WeaponHash.Pistol);

                            if (unit.Officer != null && unit.Officer.Exists())
                            {
                                if (unit.Officer.Weapons.HasWeapon(WeaponHash.CarbineRifle))
                                    unit.Officer.Weapons.Select(WeaponHash.CarbineRifle);
                                else
                                    unit.Officer.Weapons.Select(WeaponHash.Pistol);
                            }

                            unit.Driver.Task.FightAgainst(suspect);
                            unit.Officer?.Task.FightAgainst(suspect);
                        }
                        else
                        {
                            // Non-léthal : utiliser taser et poursuite
                            unit.Driver.Weapons.Select(WeaponHash.StunGun);
                            unit.Officer?.Weapons.Select(WeaponHash.StunGun);

                            unit.Driver.Task.FightAgainst(suspect);
                            unit.Officer?.Task.FightAgainst(suspect);
                        }

                        unit.Chasing = false; // fin poursuite véhicule
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

        #region LongPress Lookup

        private const Keys LookupKey = Keys.Q;
        private int _lookupPressFrames = 0;
        private const int LONG_PRESS_FRAMES = 60; // ~1s à 60 FPS

        private void HandleLongPressLookup()
        {
            var player = Game.Player.Character;
            bool inPoliceVeh = player.CurrentVehicle != null; // Simplified check; could refine to police vehicles

            if (inPoliceVeh && Game.IsKeyPressed(LookupKey))
            {
                _lookupPressFrames++;
                if (_lookupPressFrames == LONG_PRESS_FRAMES)
                {
                    // Ouverture UI
                    RequestUserLookup(LookupKind.Plate);
                }
            }
            else
            {
                _lookupPressFrames = 0;
            }
        }

        private void RequestUserLookup(LookupKind kind)
        {
            string prompt = kind == LookupKind.Plate ? "Entrez la plaque" : "Entrez le nom complet";
            string input = Game.GetUserInput((WindowTitle)0, "", 32);
            if (!string.IsNullOrWhiteSpace(input))
            {
                PerformLookup(kind, input.Trim().ToUpper());
            }
        }

        private void PerformLookup(LookupKind kind, string query)
        {
            string result;
            if (kind == LookupKind.Plate)
            {
                result = GeneratePlateInfo(query);
            }
            else
            {
                result = GeneratePersonInfo(query);
            }

            // Affiche sous forme de notification et big message
            GTA.UI.Notification.Show(result);

            var big = new LemonUI.Scaleform.BigMessage("RECHERCHE", result);
            big.Type = LemonUI.Scaleform.MessageType.Customizable;
            _pool.Add(big);
        }

        // Générateurs déterministes pour garantir des résultats cohérents et uniques.
        private string GeneratePlateInfo(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate)) plate = "UNKNOWN";

            int seed = plate.GetHashCode();
            var rng = new System.Random(seed);

            // Propriétaire déterministe
            string owner = GenerateRandomName(rng);

            // Marque / modèle simulé à partir de listes basiques
            string[] makes = { "Vapid", "Bravado", "Karin", "Benefactor", "Albany", "Dundreary" };
            string[] models = { "Stanier", "Buffalo", "Sultan", "Schafter", "Primo", "Landstalker" };
            string model = makes[rng.Next(makes.Length)] + " " + models[rng.Next(models.Length)];

            // Statuts administratifs cohérents mais pas tous parfaits
            string permit = rng.Next(100) < 12 ? "Suspendu" : "Valide"; // ~12 % de suspensions
            string insurance = rng.Next(100) < 18 ? "Expirée" : "À jour"; // ~18 % expirées
            string wanted = rng.Next(100) < 4 ? "⚠ AVIS DE RECHERCHE ⚠" : "Aucun"; // ~4 % recherchés

            return $"Plaque: ~b~{plate}~w~\nPropriétaire: ~y~{owner}~w~\nVéhicule: {model}\nPermis: {permit}\nAssurance: {insurance}\nRecherches: {wanted}";
        }

        private string GeneratePersonInfo(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = GenerateRandomName(new System.Random(name.GetHashCode()));

            int seed = name.ToUpperInvariant().GetHashCode();
            var rng = new System.Random(seed);

            string id = $"#{100000 + (seed & 0x3FFFFF) % 900000}"; // ID pseudo-unique dérivé du hash

            string license = rng.Next(100) < 10 ? "Suspendu" : "Valide"; // 10 % suspendus
            string gunPermit = rng.Next(100) < 45 ? "Oui" : "Non";        // 45 % armés
            string outstanding = rng.Next(100) < 5 ? "Mandat actif" : "Aucun"; // 5 % mandats

            return $"Nom: ~b~{name}~w~\nID: {id}\nPermis conduire: {license}\nPermis d'arme: {gunPermit}\nMandats: {outstanding}";
        }

        private string GenerateRandomName(System.Random rng)
        {
            string[] first = { "John", "Anna", "Michael", "Lucas", "Sarah", "David", "Emma", "James", "Olivia", "Robert", "Sophia", "Daniel", "Chloe", "William", "Mia" };
            string[] last = { "Smith", "Johnson", "Brown", "Martin", "Garcia", "Wilson", "Davis", "Clark", "Lopez", "Lee", "Walker", "Young", "Hall", "Allen", "King" };
            return first[rng.Next(first.Length)] + " " + last[rng.Next(last.Length)];
        }

        #endregion

        private void OnServiceItemActivated(object sender, ItemActivatedArgs e)
        {
            int idx = _serviceMenu.Items.IndexOf(e.Item);
            switch (idx)
            {
                case 0:
                    SpawnService(ServiceKind.TowTruck);
                    break;
                case 1:
                    SpawnService(ServiceKind.Ambulance);
                    break;
                case 2:
                    SpawnService(ServiceKind.Firefighters);
                    break;
            }
        }

        private void OnLookupItemActivated(object sender, ItemActivatedArgs e)
        {
            int idx = _lookupMenu.Items.IndexOf(e.Item);
            switch (idx)
            {
                case 0:
                    LookupFromTarget(LookupKind.Plate);
                    break;
                case 1:
                    LookupFromTarget(LookupKind.Person);
                    break;
            }
        }

        private void OnCalloutItemActivated(object sender, ItemActivatedArgs e)
        {
            int idx = _calloutMenu.Items.IndexOf(e.Item);
            switch (idx)
            {
                case 0:
                    CalloutManager.Instance?.StartSpecificCallout("TestCallout");
                    break;
                case 1:
                    CalloutManager.Instance?.StartSpecificCallout("StolenVehicleCallout");
                    break;
                case 2:
                    CalloutManager.Instance?.StartSpecificCallout("StreetRacingCallout");
                    break;
                case 3:
                    CalloutManager.Instance?.StartSpecificCallout("HostageSituationCallout");
                    break;
                case 4:
                    CalloutManager.Instance?.StartSpecificCallout("CartelWarCallout");
                    break;
                case 5:
                    CalloutManager.Instance?.StartSpecificCallout("DisasterResponseCallout");
                    break;
                case 6:
                    CalloutManager.Instance?.StartSpecificCallout("BankRobberyCallout");
                    break;
                case 7:
                    CalloutManager.Instance?.StartSpecificCallout("TerroristAttackCallout");
                    break;
                case 8:
                    CalloutManager.Instance?.OfferImmediateCallout(); // Callout aléatoire
                    break;
            }
        }

        private void LookupFromTarget(LookupKind kind)
        {
            if (kind == LookupKind.Plate)
            {
                Vehicle veh = GetTargetVehicle();
                if (veh == null)
                {
                    GTA.UI.Notification.Show("~r~Aucun véhicule détecté");
                    return;
                }
                string plate = Function.Call<string>(Hash.GET_VEHICLE_NUMBER_PLATE_TEXT, veh);
                PerformLookup(LookupKind.Plate, plate);
            }
            else
            {
                Ped ped = GetTargetPed();
                if (ped == null)
                {
                    GTA.UI.Notification.Show("~r~Aucun piéton ciblé");
                    return;
                }
                string name = GetPedName(ped);
                PerformLookup(LookupKind.Person, name);
            }
        }

        private Vehicle GetTargetVehicle()
        {
            Vector3 source = GameplayCamera.Position;
            Vector3 dir = GameplayCamera.Direction;
            RaycastResult hit = World.Raycast(source, dir, 40f, IntersectFlags.Vehicles, Game.Player.Character);
            if (hit.DidHit && hit.HitEntity is Vehicle v) return v;
            return World.GetClosestVehicle(source + dir * 10f, 8f);
        }

        private Ped GetTargetPed()
        {
            Vector3 source = GameplayCamera.Position;
            Vector3 dir = GameplayCamera.Direction;
            RaycastResult hit = World.Raycast(source, dir, 30f, IntersectFlags.Peds, Game.Player.Character);
            if (hit.DidHit && hit.HitEntity is Ped ped && !ped.IsPlayer) return ped;
            return null;
        }

        private string GetPedName(Ped ped)
        {
            // Fictional name based on ped hash for consistency
            int hash = ped.Model.Hash;
            var localRng = new System.Random(hash);
            string[] first = { "John", "Anna", "Michael", "Lucas", "Sarah", "David", "Emma" };
            string[] last = { "Smith", "Johnson", "Brown", "Martin", "Garcia", "Wilson", "Davis" };
            return first[localRng.Next(first.Length)] + " " + last[localRng.Next(last.Length)];
        }

        private void SpawnService(ServiceKind kind)
        {
            var player = Game.Player.Character;
            Vector3 spawnPos = World.GetNextPositionOnStreet(player.Position + player.ForwardVector * 60f);

            VehicleHash vehHash;
            PedHash pedHash;

            switch (kind)
            {
                case ServiceKind.TowTruck:
                    vehHash = VehicleHash.TowTruck;
                    pedHash = PedHash.Cop01SMY; // Fallback ped model
                    break;
                case ServiceKind.Ambulance:
                    vehHash = VehicleHash.Ambulance;
                    pedHash = PedHash.Paramedic01SMM;
                    break;
                case ServiceKind.Firefighters:
                    vehHash = VehicleHash.FireTruck;
                    pedHash = PedHash.Fireman01SMY;
                    break;
                default:
                    return;
            }

            Model vModel = new Model(vehHash);
            Model pModel = new Model(pedHash);
            vModel.Request(500);
            pModel.Request(500);
            if (!vModel.IsLoaded || !pModel.IsLoaded) return;

            Vehicle v = World.CreateVehicle(vModel, spawnPos, player.Heading);
            Ped driver = v.CreatePedOnSeat(VehicleSeat.Driver, pModel);

            GTA.UI.Notification.Show("~b~Service en route");
        }

        private void DismissAllBackups()
        {
            foreach (var unit in _activeBackups)
            {
                if (unit.IsValid)
                {
                    unit.Driver.Task.ClearAllImmediately();
                    unit.Officer?.Task.ClearAllImmediately();
                    // Leur dire de rentrer
                    unit.Driver.Task.DriveTo(unit.Veh, unit.Veh.Position + unit.Veh.ForwardVector * 200f, 10f, 25f, DrivingStyle.Normal);
                }
                if (unit.Blip != null && unit.Blip.Exists()) unit.Blip.Delete();
            }
            _activeBackups.Clear();
            GTA.UI.Notification.Show("~g~Toutes les unités renvoyées");
        }

        private void SetFriendlyToPlayer(Ped ped)
        {
            int playerGroup = Game.Player.Character.RelationshipGroup.Hash;
            int copGroup = ped.RelationshipGroup.Hash;
            // 1 = Respect / Compagnon
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, playerGroup, copGroup);
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, copGroup, playerGroup);
        }
    }
} 