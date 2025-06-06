using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using GTA.Math;
using REALIS.NPC.Police;
using REALIS.NPC.Core;

namespace REALIS.NPC.Core
{
    /// <summary>
    /// Gestionnaire principal du système de police réaliste
    /// Gère le comportement des policiers selon le niveau de recherche
    /// </summary>
    public class RealisticPoliceManager
    {
        private readonly List<PoliceOfficer> _activeOfficers;
        private readonly PoliceArrestHandler _arrestHandler;
        private readonly PoliceChaseHandler _chaseHandler;
        private readonly PoliceStationManager _stationManager;
        private int _lastWantedLevel;
        private bool _playerIsAiming;
        private bool _systemEnabled;

        public RealisticPoliceManager()
        {
            _activeOfficers = new List<PoliceOfficer>();
            _arrestHandler = new PoliceArrestHandler();
            _chaseHandler = new PoliceChaseHandler();
            _stationManager = new PoliceStationManager();
            _lastWantedLevel = 0;
            _playerIsAiming = false;
            _systemEnabled = true;

            // Démarrer le système de police
            StartPoliceSystem();
        }

        private void StartPoliceSystem()
        {
            // Cette méthode sera appelée depuis le script principal
            // Les événements Tick et Aborted seront gérés par Main.cs
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_systemEnabled) return;

            try
            {
                UpdateWantedLevel();
                UpdatePlayerAimingStatus();
                ManagePoliceOfficers();
                HandleArrestProcess();
            }
            catch (Exception ex)
            {
                // Log l'erreur mais continue l'exécution
                GTA.UI.Notification.PostTicker($"~r~Police System Error:~w~ {ex.Message}", true);
            }
        }

        private void UpdateWantedLevel()
        {
            var currentWantedLevel = Game.Player.Wanted.WantedLevel;
            
            if (currentWantedLevel != _lastWantedLevel)
            {
                OnWantedLevelChanged(currentWantedLevel, _lastWantedLevel);
                _lastWantedLevel = currentWantedLevel;
            }
        }

        private void UpdatePlayerAimingStatus()
        {
            var player = Game.Player.Character;
            var wasAiming = _playerIsAiming;
            
            // Vérifier si le joueur vise
            _playerIsAiming = Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING, Game.Player);
            
            // Si le joueur commence à viser et qu'il y a des policiers à proximité
            if (_playerIsAiming && !wasAiming && _lastWantedLevel > 0)
            {
                CheckIfAimingAtOfficer();
            }
        }

        private void CheckIfAimingAtOfficer()
        {
            var player = Game.Player.Character;
            var nearbyPeds = World.GetNearbyPeds(player, 50f);
            
            foreach (var ped in nearbyPeds)
            {
                if (IsPoliceOfficer(ped))
                {
                    var distance = Vector3.Distance(player.Position, ped.Position);
                    if (distance <= 30f && IsPlayerAimingAtPed(ped))
                    {
                        // Le joueur vise un policier - déclencher l'agression
                        TriggerPoliceAggression();
                        break;
                    }
                }
            }
        }

        private bool IsPlayerAimingAtPed(Ped ped)
        {
            var player = Game.Player.Character;
            var playerPos = player.Position;
            var pedPos = ped.Position;
            
            // Obtenir la direction du regard du joueur
            var direction = Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_ROT, 0);
            var forward = direction.Normalized;
            
            // Calculer l'angle entre la direction du joueur et la position du PED
            var toPed = (pedPos - playerPos).Normalized;
            var angle = Math.Acos(Vector3.Dot(forward.Normalized, toPed)) * 180.0 / Math.PI;
            
            return angle < 15f; // Angle de visée de 15 degrés
        }

        private void OnWantedLevelChanged(int newLevel, int oldLevel)
        {
            if (newLevel == 1 && oldLevel == 0)
            {
                // Démarrer la poursuite non-létale
                StartNonLethalChase();
            }
            else if (newLevel == 0 && oldLevel > 0)
            {
                // Arrêter toutes les poursuites
                StopAllChases();
            }
        }

        private void StartNonLethalChase()
        {
            // Désactiver les tirs des policiers
            SetPoliceFireMode(false);
            
            // Démarrer la poursuite avec l'objectif d'arrestation
            _chaseHandler.StartChase(ChaseType.NonLethal);
            
            GTA.UI.Notification.PostTicker("~y~Police:~w~ Arrêtez-vous! Nous voulons juste vous parler!", true);
        }

        private void TriggerPoliceAggression()
        {
            // Réactiver les tirs des policiers
            SetPoliceFireMode(true);
            
            // Passer en mode poursuite létale
            _chaseHandler.StartChase(ChaseType.Lethal);
            
            GTA.UI.Notification.PostTicker("~r~Police:~w~ Suspect armé! Ouvrez le feu!", true);
        }

        private void SetPoliceFireMode(bool canFire)
        {
            var nearbyPeds = World.GetNearbyPeds(Game.Player.Character, 100f);
            
            foreach (var ped in nearbyPeds)
            {
                if (IsPoliceOfficer(ped))
                {
                    if (canFire)
                    {
                        // Permettre aux policiers de tirer
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 5, true); // BF_CanFightArmedPedsWhenNotArmed
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 46, false); // BF_AlwaysFight
                    }
                    else
                    {
                        // Empêcher les policiers de tirer
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 5, false);
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 46, true);
                        
                        // Tâche d'arrestation au lieu de combat
                        Function.Call(Hash.TASK_ARREST_PED, ped, Game.Player.Character);
                    }
                }
            }
        }

        private void ManagePoliceOfficers()
        {
            // Nettoyer les officiers invalides
            _activeOfficers.RemoveAll(o => !o.IsValid);
            
            // Gérer le comportement des officiers actifs
            foreach (var officer in _activeOfficers)
            {
                officer.Update();
            }
        }

        private void HandleArrestProcess()
        {
            if (_lastWantedLevel == 1 && !_playerIsAiming)
            {
                _arrestHandler.Update();
            }
        }

        private bool IsPoliceOfficer(Ped ped)
        {
            if (ped == null || !ped.IsValid()) return false;
            
            var model = ped.Model;
            return model == PedHash.Cop01SFY || 
                   model == PedHash.Cop01SMY || 
                   model == PedHash.Sheriff01SFY || 
                   model == PedHash.Sheriff01SMY ||
                   model == PedHash.Swat01SMY ||
                   Function.Call<bool>(Hash.IS_PED_IN_ANY_POLICE_VEHICLE, ped);
        }

        private void StopAllChases()
        {
            _chaseHandler.StopChase();
            _arrestHandler.Reset();
            
            foreach (var officer in _activeOfficers)
            {
                officer.ResetBehavior();
            }
        }

        public void ToggleSystem()
        {
            _systemEnabled = !_systemEnabled;
            
            if (!_systemEnabled)
            {
                StopAllChases();
            }
        }

        public void Update()
        {
            if (!_systemEnabled) return;

            try
            {
                UpdateWantedLevel();
                UpdatePlayerAimingStatus();
                ManagePoliceOfficers();
                HandleArrestProcess();
            }
            catch (Exception ex)
            {
                // Log l'erreur mais continue l'exécution
                GTA.UI.Notification.PostTicker($"~r~Police System Error:~w~ {ex.Message}", true);
            }
        }

        public void Cleanup()
        {
            StopAllChases();
            _activeOfficers.Clear();
        }

        private void OnAborted(object sender, EventArgs e)
        {
            Cleanup();
        }
    }
} 