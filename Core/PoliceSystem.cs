using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Common;
using REALIS.Config;

namespace REALIS.Core
{
    /// <summary>
    /// Système de police personnalisé avec gestion des arrestations et comportements spéciaux
    /// </summary>
    public class PoliceSystem : Script
    {
        private DateTime _lastStoppedTime = DateTime.MinValue;
        private bool _isPlayerStopped = false;
        private Vector3 _lastPlayerPosition = Vector3.Zero;
        private bool _isBeingArrested = false;
        private Vehicle? _arrestVehicle = null;
        private Ped? _arrestOfficer = null;
        private PoliceConfig _config;

        public PoliceSystem()
        {
            _config = PoliceConfig.Instance;
            Tick += OnTick;
            
            Logger.Info("Police System initialized with interiors enabled");
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                if (Game.IsCutsceneActive || Game.IsPaused || !_config.EnableCustomPolice)
                    return;

                var player = Game.Player.Character;
                if (player == null || !player.Exists() || player.IsDead)
                    return;

                // 1. Gérer le comportement des policiers (ne pas tirer sauf si menacés)
                if (_config.EnablePoliceAggressionControl)
                    ModifyPoliceAggressiveness();

                // 2. Gérer l'arrestation automatique si le joueur s'arrête
                if (_config.EnableAutoArrest)
                    HandlePlayerStopArrest(player);

                // 3. Gérer le transport vers le poste de police
                if (_config.EnablePoliceTransport && _isBeingArrested && !_isEscorting)
                    HandlePoliceTransport();

                // 4. Gérer l'escorte à pied
                if (_isEscorting)
                    HandleEscortProcess();
            }
            catch (Exception ex)
            {
                Logger.Error($"Police System error: {ex.Message}");
            }
        }

        /// <summary>
        /// Modifie l'agressivité de la police pour qu'elle ne tire que si menacée
        /// </summary>
        private void ModifyPoliceAggressiveness()
        {
            var player = Game.Player.Character;
            var wantedLevel = Game.Player.Wanted.WantedLevel;

            if (wantedLevel > 0)
            {
                var nearbyCops = World.GetNearbyPeds(player.Position, 50f)
                    .Where(p => p.IsAlive && IsCop(p))
                    .ToArray();

                foreach (var cop in nearbyCops)
                {
                    if (cop.IsInCombat && !IsPlayerThreateningCop(player, cop))
                    {
                        // Arrêter le combat si le joueur ne menace pas
                        cop.Task.ClearAll();
                        cop.Task.GuardCurrentPosition();
                    }
                    
                    // Configurer le policier pour être moins agressif
                    Function.Call(Hash.SET_PED_COMBAT_RANGE, cop, 1); // Combat à courte portée seulement
                }
            }
        }

        /// <summary>
        /// Vérifie si un PED est un policier
        /// </summary>
        private bool IsCop(Ped ped)
        {
            var pedModel = ped.Model.Hash;
            return pedModel == unchecked((int)PedHash.Cop01SFY) || 
                   pedModel == unchecked((int)PedHash.Cop01SMY) || 
                   pedModel == unchecked((int)PedHash.Sheriff01SFY) || 
                   pedModel == unchecked((int)PedHash.Sheriff01SMY) ||
                   Function.Call<bool>(Hash.IS_PED_IN_GROUP, ped);
        }

        /// <summary>
        /// Vérifie si le joueur menace un policier
        /// </summary>
        private bool IsPlayerThreateningCop(Ped player, Ped cop)
        {
            // Vérifier si le joueur vise le policier
            if (player.IsAiming && Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player, cop))
            {
                return true;
            }

            // Vérifier si le joueur a une arme et est proche
            var currentWeapon = player.Weapons.Current;
            if (currentWeapon != null && 
                currentWeapon.Hash != WeaponHash.Unarmed &&
                Vector3.Distance(player.Position, cop.Position) < _config.WeaponThreatDistance)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gère l'arrestation du joueur s'il s'arrête pendant X secondes (seulement à pied)
        /// </summary>
        private void HandlePlayerStopArrest(Ped player)
        {
            if (_isBeingArrested || Game.Player.Wanted.WantedLevel == 0)
                return;

            // L'arrestation automatique ne fonctionne que si le joueur est à pied
            if (player.IsInVehicle())
                return;

            var currentPosition = player.Position;
            var playerSpeed = player.Velocity.Length();

            // Vérifier si le joueur s'est arrêté
            if (playerSpeed < _config.StopThreshold)
            {
                if (!_isPlayerStopped)
                {
                    _isPlayerStopped = true;
                    _lastStoppedTime = DateTime.Now;
                    _lastPlayerPosition = currentPosition;
                    
                    var message = string.Format(_config.Messages.InitialWarning, _config.ArrestDelaySeconds);
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, message);
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 2000);
                }
                else
                {
                    // Vérifier le temps d'arrêt
                    var stoppedDuration = DateTime.Now - _lastStoppedTime;
                    var remainingTime = _config.ArrestDelaySeconds - (int)stoppedDuration.TotalSeconds;
                    
                    if (remainingTime > 0)
                    {
                        var message = string.Format(_config.Messages.CountdownWarning, remainingTime);
                        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
                        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, message);
                        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 100);
                    }
                    else if (stoppedDuration.TotalSeconds >= _config.ArrestDelaySeconds)
                    {
                        InitiateArrest(player);
                    }
                }
            }
            else
            {
                _isPlayerStopped = false;
            }
        }

        /// <summary>
        /// Lance la procédure d'arrestation
        /// </summary>
        private void InitiateArrest(Ped player)
        {
            _isBeingArrested = true;
            
            // Suppression complète du niveau de recherche et des poursuites
            Game.Player.Wanted.SetWantedLevel(0, false);
            Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
            Game.Player.Wanted.SetPoliceIgnorePlayer(true);
            Game.Player.Wanted.SetEveryoneIgnorePlayer(true);
            
            // Nettoyer la zone des policiers agressifs
            World.ClearAreaOfCops(player.Position, 100f);

            // Trouver la voiture de police la plus proche
            var nearbyVehicles = World.GetNearbyVehicles(player.Position, _config.PoliceVehicleSearchRadius);
            var policeVehicle = nearbyVehicles
                .Where(v => IsPoliceVehicle(v) && v.IsAlive)
                .OrderBy(v => Vector3.Distance(v.Position, player.Position))
                .FirstOrDefault();

            if (policeVehicle == null)
            {
                // Créer une voiture de police si aucune n'est trouvée
                policeVehicle = CreatePoliceVehicle(player.Position);
            }

            if (policeVehicle != null)
            {
                _arrestVehicle = policeVehicle;
                TeleportPlayerToPoliceVehicle(player, policeVehicle);
                CreateArrestOfficer(policeVehicle);
            }

            // Afficher le message d'arrestation
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, _config.Messages.ArrestMessage);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 3000);
        }

        /// <summary>
        /// Vérifie si un véhicule est une voiture de police
        /// </summary>
        private bool IsPoliceVehicle(Vehicle vehicle)
        {
            var vehicleClass = Function.Call<int>(Hash.GET_VEHICLE_CLASS, vehicle);
            return vehicleClass == 18; // Emergency vehicles
        }

        /// <summary>
        /// Crée une voiture de police près du joueur
        /// </summary>
        private Vehicle? CreatePoliceVehicle(Vector3 playerPosition)
        {
            var spawnPosition = playerPosition + Vector3.RandomXY() * 20f;
            var vehicle = World.CreateVehicle(VehicleHash.Police, spawnPosition);
            
            if (vehicle != null)
            {
                vehicle.PlaceOnGround();
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle, true, true, false);
            }
            
            return vehicle;
        }

        /// <summary>
        /// Téléporte le joueur à l'arrière de la voiture de police
        /// </summary>
        private void TeleportPlayerToPoliceVehicle(Ped player, Vehicle policeVehicle)
        {
            // Téléporter le joueur à l'arrière du véhicule
            Function.Call(Hash.SET_PED_INTO_VEHICLE, player, policeVehicle, 1); // Siège arrière gauche
            
            // Bloquer les commandes du joueur temporairement
            Function.Call(Hash.SET_PLAYER_CONTROL, Game.Player, false, 0);
            
            // Rendre le contrôle après 2 secondes (via un timer)
            Script.Wait(2000);
            Function.Call(Hash.SET_PLAYER_CONTROL, Game.Player, true, 0);
        }

        /// <summary>
        /// Crée un officier de police pour conduire
        /// </summary>
        private void CreateArrestOfficer(Vehicle policeVehicle)
        {
            var officer = World.CreatePed(PedHash.Cop01SMY, policeVehicle.Position);
            if (officer != null)
            {
                _arrestOfficer = officer;
                Function.Call(Hash.SET_PED_INTO_VEHICLE, officer, policeVehicle, -1); // Siège conducteur
                
                // Configurer l'officier pour qu'il ne soit pas agressif
                Function.Call(Hash.SET_PED_AS_COP, officer, false); // Pas de comportement de flic agressif
                officer.IsInvincible = true; // Rendre l'officier invincible pendant le transport
                officer.BlockPermanentEvents = true; // Empêcher les réactions automatiques
                
                // S'assurer qu'il n'attaque pas le joueur
                Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, officer, Function.Call<uint>(Hash.GET_HASH_KEY, "CIVMALE"));
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, Function.Call<uint>(Hash.GET_HASH_KEY, "CIVMALE"), Function.Call<uint>(Hash.GET_HASH_KEY, "PLAYER"));
                
                // Commencer le transport vers le poste de police
                StartTransportToStation();
            }
        }

        /// <summary>
        /// Démarre le transport vers Mission Row Police Station (seul intérieur accessible)
        /// </summary>
        private void StartTransportToStation()
        {
            if (_arrestOfficer == null || _arrestVehicle == null)
                return;

            // Coordonnées de l'ENTRÉE de Mission Row Police Station (devant le bâtiment)
            var missionRowEntrance = new Vector3(441.7f, -975.3f, 30.69f);

            // Utiliser une mission de véhicule plus appropriée pour l'escorte
            _arrestOfficer.Task.GoToPointAnyMeansExtraParamsWithCruiseSpeed(
                missionRowEntrance, 
                PedMoveBlendRatio.Walk, 
                _arrestVehicle, 
                false, 
                VehicleDrivingFlags.DrivingModeStopForVehicles, 
                -1, 
                0, 
                20, 
                TaskGoToPointAnyMeansFlags.Default, 
                15f, 
                4f
            );
            
            // Message spécifique pour Mission Row
            var transportMessage = "~b~Transport vers Mission Row Police Station...";
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, transportMessage);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 5000);
        }

        /// <summary>
        /// Gère le transport du joueur vers Mission Row
        /// </summary>
        private void HandlePoliceTransport()
        {
            if (!_isBeingArrested || _arrestVehicle == null || _arrestOfficer == null)
                return;

            var player = Game.Player.Character;
            
            // Vérifier si on est arrivé devant Mission Row Police Station
            var missionRowEntrance = new Vector3(441.7f, -975.3f, 30.69f);

            if (Vector3.Distance(player.Position, missionRowEntrance) < 20f)
            {
                CompleteArrestWithEscort();
            }

            // Vérifier si le véhicule ou l'officier sont détruits
            if (_arrestVehicle.IsDead || _arrestOfficer.IsDead)
            {
                CancelArrest();
            }
        }

        // Variables pour gérer l'escorte
        private bool _isEscorting = false;
        private DateTime _escortStartTime = DateTime.MinValue;
        private Ped? _chairOfficer = null;

        /// <summary>
        /// Complète la procédure d'arrestation avec escorte à pied
        /// </summary>
        private void CompleteArrestWithEscort()
        {
            var player = Game.Player.Character;
            
            // Initier l'escorte
            _isEscorting = true;
            _escortStartTime = DateTime.Now;
            _escortPhase = 0;
            
            // Sortir le joueur du véhicule sans bloquer
            player.Task.LeaveVehicle();
            
            // Message d'escorte
            var escortMessage = "~y~Sortez du véhicule pour être escorté vers l'entrée...";
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, escortMessage);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 4000);
        }

        // Variables pour gérer les phases d'escorte
        private int _escortPhase = 0; // 0: sortie véhicule, 1: vers entrée, 2: vers intérieur, 3: fini
        private DateTime _lastTaskTime = DateTime.MinValue;

        /// <summary>
        /// Gère l'escorte progressive du joueur
        /// </summary>
        private void HandleEscortProcess()
        {
            if (!_isEscorting || _arrestOfficer == null || !_arrestOfficer.Exists())
                return;

            var player = Game.Player.Character;
            
            // Coordonnées basées sur vos spécifications
            var entranceDoor = new Vector3(431.44f, -981.70f, 30.71f); // Porte d'entrée
            var interiorDestination = new Vector3(439.71f, -981.10f, 30.69f); // Intérieur du bâtiment
            
            // Phase 0: Sortie du véhicule
            if (_escortPhase == 0)
            {
                // Vérifier si le joueur est sorti du véhicule
                if (!player.IsInVehicle())
                {
                    // Faire sortir l'officier du véhicule aussi
                    if (_arrestOfficer.IsInVehicle())
                    {
                        _arrestOfficer.Task.LeaveVehicle();
                    }
                    
                    // Attendre que l'officier sorte
                    if (!_arrestOfficer.IsInVehicle())
                    {
                        _escortPhase = 1;
                        _lastTaskTime = DateTime.Now;
                        
                        var startMessage = "~y~Début de l'escorte vers l'entrée...";
                        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
                        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, startMessage);
                        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 3000);
                    }
                }
                return;
            }
            
            // Phase 1: Escorte vers la porte d'entrée
            if (_escortPhase == 1)
            {
                var distanceToEntrance = Vector3.Distance(player.Position, entranceDoor);
                var officerDistanceToEntrance = Vector3.Distance(_arrestOfficer.Position, entranceDoor);
                
                // Donner les tâches de mouvement toutes les 2 secondes pour s'assurer qu'elles sont actives
                if ((DateTime.Now - _lastTaskTime).TotalSeconds > 2)
                {
                    // L'officier marche vers l'entrée en contournant les obstacles
                    _arrestOfficer.Task.ClearAllImmediately();
                    _arrestOfficer.Task.FollowNavMeshTo(entranceDoor, PedMoveBlendRatio.Walk, 10000, 1.0f);
                    
                    // Le joueur marche vers l'entrée en contournant les obstacles
                    player.Task.ClearAllImmediately();
                    player.Task.FollowNavMeshTo(entranceDoor, PedMoveBlendRatio.Walk, 10000, 1.0f);
                    
                    _lastTaskTime = DateTime.Now;
                    
                    var walkMessage = "~b~Marchez vers l'entrée du bâtiment...";
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, walkMessage);
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 1000);
                }
                
                // Vérifier si les deux sont arrivés à l'entrée
                if (distanceToEntrance < 3f && officerDistanceToEntrance < 3f)
                {
                    _escortPhase = 2;
                    _lastTaskTime = DateTime.Now;
                    
                    var entranceMessage = "~g~Entrée atteinte! Direction l'intérieur...";
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, entranceMessage);
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 3000);
                }
                return;
            }
            
            // Phase 2: Escorte vers l'intérieur
            if (_escortPhase == 2)
            {
                var distanceToInterior = Vector3.Distance(player.Position, interiorDestination);
                var officerDistanceToInterior = Vector3.Distance(_arrestOfficer.Position, interiorDestination);
                
                // Donner les tâches de mouvement toutes les 2 secondes
                if ((DateTime.Now - _lastTaskTime).TotalSeconds > 2)
                {
                    // L'officier marche vers l'intérieur en contournant les obstacles
                    _arrestOfficer.Task.ClearAllImmediately();
                    _arrestOfficer.Task.FollowNavMeshTo(interiorDestination, PedMoveBlendRatio.Walk, 10000, 1.0f);
                    
                    // Le joueur marche vers l'intérieur en contournant les obstacles
                    player.Task.ClearAllImmediately();
                    player.Task.FollowNavMeshTo(interiorDestination, PedMoveBlendRatio.Walk, 10000, 1.0f);
                    
                    _lastTaskTime = DateTime.Now;
                    
                    var interiorMessage = "~b~Entrez dans le poste de police...";
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, interiorMessage);
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 1000);
                }
                
                // Vérifier si les deux sont arrivés à l'intérieur
                if (distanceToInterior < 2f && officerDistanceToInterior < 3f)
                {
                    _escortPhase = 3;
                    
                    // Orienter le joueur avec le bon heading
                    player.Heading = 275.85f;
                    player.Task.ClearAllImmediately();
                    
                    // Arrêter l'officier
                    _arrestOfficer.Task.ClearAllImmediately();
                    _arrestOfficer.Task.StandStill(5000);
                    
                    // Créer le policier assis dans le fauteuil
                    CreateChairOfficer(player);
                    
                    var finalMessage = "~g~Arrestation terminée! Vous êtes arrivé à Mission Row Police Station.";
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, finalMessage);
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 5000);
                    
                    // Programmer la fin de l'escorte dans 3 secondes
                    _escortStartTime = DateTime.Now.AddSeconds(-177); // Force le timeout dans 3 secondes
                }
                return;
            }
            
            // Timeout de sécurité (3 minutes maximum) ou si on a terminé la phase 3
            if ((DateTime.Now - _escortStartTime).TotalMinutes > 3 || 
                (DateTime.Now - _escortStartTime).TotalSeconds > -170)  // Check pour terminer après phase 3
            {
                if (_escortPhase >= 3 || (DateTime.Now - _escortStartTime).TotalSeconds > -170)
                {
                    // Terminer normalement après phase 3
                    FinishEscort();
                }
                else
                {
                    // Timeout réel - téléporter à la destination
                    var timeoutMessage = "~r~Escorte interrompue par timeout. Téléportation à la destination.";
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, timeoutMessage);
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 3000);
                    
                    // Téléporter le joueur à la destination finale en cas de timeout
                    player.Position = interiorDestination;
                    player.Heading = 275.85f;
                    
                    FinishEscort();
                }
            }
        }

        /// <summary>
        /// Crée un policier assis dans le fauteuil qui regarde le joueur
        /// </summary>
        private void CreateChairOfficer(Ped player)
        {
            // Coordonnées du fauteuil
            var chairPosition = new Vector3(442.19f, -978.86f, 30.69f);
            var chairHeading = 172.36f;
            
            // Vérifier si le policier n'existe pas déjà
            if (_chairOfficer != null && _chairOfficer.Exists())
            {
                return; // Déjà créé
            }
            
            // Créer le policier
            var model = new Model(PedHash.Cop01SMY);
            model.Request(5000);
            
            if (model.IsLoaded)
            {
                _chairOfficer = World.CreatePed(model, chairPosition, chairHeading);
                
                if (_chairOfficer != null)
                {
                    // Configurer le policier
                    _chairOfficer.IsInvincible = true;
                    _chairOfficer.BlockPermanentEvents = true;
                    _chairOfficer.CanRagdoll = false;
                    
                    // Positionner et orienter le policier
                    _chairOfficer.Position = chairPosition;
                    _chairOfficer.Heading = chairHeading;
                    
                    // Attendre un frame pour que le policier soit bien positionné
                    Script.Wait(100);
                    
                    // Faire asseoir le policier (simulation de s'asseoir dans le fauteuil)
                    _chairOfficer.Task.StandStill(-1); // Rester immobile
                    
                    // Le faire regarder vers le joueur
                    _chairOfficer.Task.LookAt(player, -1, LookAtFlags.Default, LookAtPriority.High);
                    
                    // Optionnel : Ajouter une animation d'assis ou de repos
                    Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, _chairOfficer, "WORLD_HUMAN_CLIPBOARD", 0, true);
                    
                    Logger.Info("Policier créé dans le fauteuil aux coordonnées spécifiées");
                }
            }
            
            model.MarkAsNoLongerNeeded();
        }

        /// <summary>
        /// Termine l'escorte et remet à zéro les états
        /// </summary>
        private void FinishEscort()
        {
            _isEscorting = false;
            
            // Rétablir le comportement normal de la police
            Game.Player.Wanted.SetPoliceIgnorePlayer(false);
            Game.Player.Wanted.SetEveryoneIgnorePlayer(false);
            
            // Libérer le contrôle du joueur
            var player = Game.Player.Character;
            player.Task.ClearAllImmediately();
            
            ResetArrestState();
        }

        /// <summary>
        /// Complète la procédure d'arrestation (méthode legacy pour compatibilité)
        /// </summary>
        private void CompleteArrest()
        {
            CompleteArrestWithEscort();
        }

        /// <summary>
        /// Annule l'arrestation
        /// </summary>
        private void CancelArrest()
        {
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, _config.Messages.ArrestCancelled);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 3000);
            
            ResetArrestState();
        }

        /// <summary>
        /// Remet à zéro l'état d'arrestation
        /// </summary>
        private void ResetArrestState()
        {
            _isBeingArrested = false;
            _isPlayerStopped = false;
            _isEscorting = false;
            _escortPhase = 0;
            _arrestVehicle = null;
            
            // Nettoyer l'officier d'arrestation
            if (_arrestOfficer != null)
            {
                _arrestOfficer.IsInvincible = false;
                _arrestOfficer.BlockPermanentEvents = false;
                _arrestOfficer = null;
            }
            
            // Nettoyer le policier du fauteuil
            if (_chairOfficer != null)
            {
                _chairOfficer.IsInvincible = false;
                _chairOfficer.BlockPermanentEvents = false;
                _chairOfficer.Task.ClearAllImmediately();
                _chairOfficer.Delete();
                _chairOfficer = null;
            }
            
            // S'assurer que le comportement de la police est rétabli
            Game.Player.Wanted.SetPoliceIgnorePlayer(false);
            Game.Player.Wanted.SetEveryoneIgnorePlayer(false);
        }

        /// <summary>
        /// Active les intérieurs des postes de police
        /// </summary>
        private void EnablePoliceStationInteriors()
        {
            // Mission Row Police Station - intérieurs complets avec sous-sol et cellules
            Function.Call(Hash.REQUEST_IPL, "sp1_10_fake_interior");
            Function.Call(Hash.REQUEST_IPL, "sp1_10_real_interior");
            Function.Call(Hash.REQUEST_IPL, "cs1_02_cf_onmission");
            Function.Call(Hash.REQUEST_IPL, "cs1_02_cf_offmission");  // Backup interior
            
            // Intérieurs spéciaux pour Mission Row (sous-sol et cellules)
            Function.Call(Hash.REQUEST_IPL, "sp1_10_real_interior_phys");
            Function.Call(Hash.REQUEST_IPL, "sp1_10_fake_interior_phys");
            
            // Rockford Hills Police Station  
            Function.Call(Hash.REQUEST_IPL, "rockford_hills_police");
            
            // La Mesa Police Station
            Function.Call(Hash.REQUEST_IPL, "la_mesa_police");
            
            // Sandy Shores Sheriff
            Function.Call(Hash.REQUEST_IPL, "cs3_05_water_grp1");
            Function.Call(Hash.REQUEST_IPL, "cs3_05_water_grp1_lod");
            Function.Call(Hash.REQUEST_IPL, "cs3_05_water_grp2");
            Function.Call(Hash.REQUEST_IPL, "cs3_05_water_grp2_lod");
            
            // Paleto Bay Sheriff
            Function.Call(Hash.REQUEST_IPL, "cs3_03_ug_office");
            Function.Call(Hash.REQUEST_IPL, "cs3_03_ug_office_lod");
            
            // Vespucci Police Station
            Function.Call(Hash.REQUEST_IPL, "vespucci_police_station");
            
            // IPLs génériques pour les intérieurs de police
            Function.Call(Hash.REQUEST_IPL, "police_station");
            Function.Call(Hash.REQUEST_IPL, "police_station_interior");
        }

        /// <summary>
        /// Active les intérieurs importants pour éviter les problèmes de téléportation
        /// </summary>
        private void EnableImportantInteriors()
        {
            // Hôpitaux
            Function.Call(Hash.REQUEST_IPL, "RC12B_Default");
            Function.Call(Hash.REQUEST_IPL, "RC12B_Fixed");
            
            // Intérieurs gouvernementaux  
            Function.Call(Hash.REQUEST_IPL, "FIBlobby");
            Function.Call(Hash.REQUEST_IPL, "FIBlobbyfake");
            
            // Commissariats supplémentaires
            Function.Call(Hash.REQUEST_IPL, "cs1_02_cf_offmission");
            Function.Call(Hash.REQUEST_IPL, "prologue01");
            Function.Call(Hash.REQUEST_IPL, "prologue01_lod");
            Function.Call(Hash.REQUEST_IPL, "prologue01c");
            Function.Call(Hash.REQUEST_IPL, "prologue01c_lod");
            Function.Call(Hash.REQUEST_IPL, "prologue01d");
            Function.Call(Hash.REQUEST_IPL, "prologue01d_lod");
            Function.Call(Hash.REQUEST_IPL, "prologue01e");
            Function.Call(Hash.REQUEST_IPL, "prologue01e_lod");
            Function.Call(Hash.REQUEST_IPL, "prologue01f");
            Function.Call(Hash.REQUEST_IPL, "prologue01f_lod");
            Function.Call(Hash.REQUEST_IPL, "prologue01g");
            Function.Call(Hash.REQUEST_IPL, "prologue01h");
            Function.Call(Hash.REQUEST_IPL, "prologue01i");
            Function.Call(Hash.REQUEST_IPL, "prologue01j");
            Function.Call(Hash.REQUEST_IPL, "prologue01k");
            Function.Call(Hash.REQUEST_IPL, "prologue01z");
            Function.Call(Hash.REQUEST_IPL, "prologue01z_lod");
            Function.Call(Hash.REQUEST_IPL, "plg_01");
            Function.Call(Hash.REQUEST_IPL, "prologue_grv_cov");
            Function.Call(Hash.REQUEST_IPL, "prologue_grv_cov_lod");
            Function.Call(Hash.REQUEST_IPL, "des_protree_end");
            Function.Call(Hash.REQUEST_IPL, "des_protree_start");
            Function.Call(Hash.REQUEST_IPL, "des_protree_start_lod");
        }

        /// <summary>
        /// S'assure qu'un intérieur spécifique est chargé avant téléportation
        /// </summary>
        private void EnsureInteriorLoaded(Vector3 position)
        {
            // Forcer le chargement de l'intérieur à cette position
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, position.X, position.Y, position.Z);
            
            // Attendre que le collision soit chargé
            int timeout = 0;
            while (!Function.Call<bool>(Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, Game.Player.Character) && timeout < 50)
            {
                Script.Wait(100);
                timeout++;
            }
            
            // Charger les modèles de base
            Function.Call(Hash.REQUEST_MODEL, Function.Call<uint>(Hash.GET_HASH_KEY, "prop_chair_01a"));
            Function.Call(Hash.REQUEST_MODEL, Function.Call<uint>(Hash.GET_HASH_KEY, "prop_table_01"));
            Function.Call(Hash.REQUEST_MODEL, Function.Call<uint>(Hash.GET_HASH_KEY, "prop_tv_flat_01"));
        }

        public void OnAborted()
        {
            ResetArrestState();
        }
    }
} 