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
            Logger.Info("Police System initialized");
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
                if (_config.EnablePoliceTransport)
                    HandlePoliceTransport();
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
        /// Démarre le transport vers le poste de police
        /// </summary>
        private void StartTransportToStation()
        {
            if (_arrestOfficer == null || _arrestVehicle == null)
                return;

            // Trouver le poste de police le plus proche
            var playerPosition = Game.Player.Character.Position;
            var nearestStation = _config.CustomPoliceStations
                .OrderBy(station => Vector3.Distance(station, playerPosition))
                .First();

            // Utiliser une mission de véhicule plus appropriée pour l'escorte
            _arrestOfficer.Task.GoToPointAnyMeansExtraParamsWithCruiseSpeed(
                nearestStation, 
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
            
            // Afficher le message de transport
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, _config.Messages.TransportMessage);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 5000);
        }

        /// <summary>
        /// Gère le transport du joueur
        /// </summary>
        private void HandlePoliceTransport()
        {
            if (!_isBeingArrested || _arrestVehicle == null || _arrestOfficer == null)
                return;

            var player = Game.Player.Character;
            
            // Vérifier si on est arrivé au poste
            var nearestStation = _config.CustomPoliceStations
                .OrderBy(station => Vector3.Distance(station, player.Position))
                .First();

            if (Vector3.Distance(player.Position, nearestStation) < 20f)
            {
                CompleteArrest();
            }

            // Vérifier si le véhicule ou l'officier sont détruits
            if (_arrestVehicle.IsDead || _arrestOfficer.IsDead)
            {
                CancelArrest();
            }
        }

        /// <summary>
        /// Complète la procédure d'arrestation
        /// </summary>
        private void CompleteArrest()
        {
            var player = Game.Player.Character;
            
            // Sortir le joueur du véhicule
            player.Task.LeaveVehicle();
            
            // Téléporter à l'entrée du poste
            var nearestStation = _config.CustomPoliceStations
                .OrderBy(station => Vector3.Distance(station, player.Position))
                .First();
            
            player.Position = nearestStation;
            
            // Rétablir le comportement normal de la police
            Game.Player.Wanted.SetPoliceIgnorePlayer(false);
            Game.Player.Wanted.SetEveryoneIgnorePlayer(false);
            
            // Afficher le message de libération
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, _config.Messages.ReleaseMessage);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, 5000);
            
            ResetArrestState();
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
            _arrestVehicle = null;
            
            // Nettoyer l'officier d'arrestation
            if (_arrestOfficer != null)
            {
                _arrestOfficer.IsInvincible = false;
                _arrestOfficer.BlockPermanentEvents = false;
                _arrestOfficer = null;
            }
            
            // S'assurer que le comportement de la police est rétabli
            Game.Player.Wanted.SetPoliceIgnorePlayer(false);
            Game.Player.Wanted.SetEveryoneIgnorePlayer(false);
        }

        public void OnAborted()
        {
            ResetArrestState();
        }
    }
} 