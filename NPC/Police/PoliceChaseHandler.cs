using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.Math;

namespace REALIS.NPC.Police
{
    /// <summary>
    /// Gestionnaire des poursuites policières
    /// </summary>
    public class PoliceChaseHandler
    {
        private ChaseType _currentChaseType;
        private DateTime _chaseStartTime;
        private List<Ped> _chasingOfficers;
        private bool _chaseActive;

        public bool IsChaseActive => _chaseActive;
        public ChaseType CurrentChaseType => _currentChaseType;

        public PoliceChaseHandler()
        {
            _currentChaseType = ChaseType.None;
            _chaseStartTime = DateTime.Now;
            _chasingOfficers = new List<Ped>();
            _chaseActive = false;
        }

        public void StartChase(ChaseType chaseType)
        {
            _currentChaseType = chaseType;
            _chaseStartTime = DateTime.Now;
            _chaseActive = true;

            // Nettoyer la liste des officiers invalides
            _chasingOfficers.RemoveAll(o => o == null || !o.Exists() || !o.IsAlive);

            // Trouver et assigner des officiers pour la poursuite
            FindAndAssignChaseOfficers();

            // Configurer le comportement selon le type de poursuite
            ConfigureChaseType(chaseType);
        }

        public void StopChase()
        {
            _chaseActive = false;
            _currentChaseType = ChaseType.None;

            // Nettoyer le comportement des officiers
            foreach (var officer in _chasingOfficers)
            {
                if (officer != null && officer.Exists() && officer.IsAlive)
                {
                    Function.Call(Hash.CLEAR_PED_TASKS, officer);
                    ResetOfficerCombatAttributes(officer);
                }
            }

            _chasingOfficers.Clear();
        }

        public void Update()
        {
            if (!_chaseActive) return;

            // Nettoyer les officiers invalides
            _chasingOfficers.RemoveAll(o => o == null || !o.Exists() || !o.IsAlive);

            // Vérifier si on doit arrêter la poursuite
            if (Game.Player.Wanted.WantedLevel == 0)
            {
                StopChase();
                return;
            }

            // Gérer la poursuite selon le type
            switch (_currentChaseType)
            {
                case ChaseType.NonLethal:
                    HandleNonLethalChase();
                    break;
                case ChaseType.Lethal:
                    HandleLethalChase();
                    break;
            }

            // Ajouter de nouveaux officiers si nécessaire
            if (_chasingOfficers.Count < 3)
            {
                FindAndAssignChaseOfficers();
            }
        }

        private void FindAndAssignChaseOfficers()
        {
            var player = Game.Player.Character;
            var nearbyPeds = World.GetNearbyPeds(player, 100f);

            foreach (var ped in nearbyPeds)
            {
                if (_chasingOfficers.Count >= 5) break; // Limiter le nombre d'officiers

                if (IsPoliceOfficer(ped) && !_chasingOfficers.Contains(ped))
                {
                    _chasingOfficers.Add(ped);
                    InitializeChaseOfficer(ped);
                }
            }
        }

        private void InitializeChaseOfficer(Ped officer)
        {
            if (officer == null || !officer.Exists()) return;

            // Configuration de base
            officer.BlockPermanentEvents = true;
            officer.CanRagdoll = false;

            // Selon le type de poursuite
            if (_currentChaseType == ChaseType.NonLethal)
            {
                SetNonLethalBehavior(officer);
            }
            else if (_currentChaseType == ChaseType.Lethal)
            {
                SetLethalBehavior(officer);
            }
        }

        private void HandleNonLethalChase()
        {
            var player = Game.Player.Character;

            foreach (var officer in _chasingOfficers)
            {
                if (officer == null || !officer.Exists() || !officer.IsAlive) continue;

                var distance = Vector3.Distance(officer.Position, player.Position);

                if (distance > 150f)
                {
                    // Téléporter l'officier plus près si trop loin
                    var randomDirection = GetRandomDirection();
                    var spawnPos = player.Position + randomDirection * 50f;
                    officer.Position = spawnPos;
                }

                // Comportement selon la distance
                if (distance < 5f && !player.IsInVehicle())
                {
                    // Assez proche pour arrêter
                    Function.Call(Hash.TASK_ARREST_PED, officer, player);
                }
                else if (player.IsInVehicle())
                {
                    // Poursuite en véhicule
                    HandleVehicleChase(officer, player);
                }
                else
                {
                    // Poursuite à pied
                    Function.Call(Hash.TASK_GO_TO_ENTITY, officer, player, -1, 3f, 2f, 1073741824, 0);
                }
            }
        }

        private void HandleLethalChase()
        {
            var player = Game.Player.Character;

            foreach (var officer in _chasingOfficers)
            {
                if (officer == null || !officer.Exists() || !officer.IsAlive) continue;

                var distance = Vector3.Distance(officer.Position, player.Position);

                if (distance > 150f)
                {
                    // Téléporter l'officier plus près si trop loin
                    var randomDirection = GetRandomDirection();
                    var spawnPos = player.Position + randomDirection * 50f;
                    officer.Position = spawnPos;
                }

                // Combat direct
                Function.Call(Hash.TASK_COMBAT_PED, officer, player, 0, 16);
            }
        }

        private void HandleVehicleChase(Ped officer, Ped player)
        {
            if (officer.IsInVehicle())
            {
                var vehicle = officer.CurrentVehicle;
                if (vehicle.Driver == officer)
                {
                    // Poursuite en véhicule
                    Function.Call(Hash.TASK_VEHICLE_CHASE, officer, player);
                }
            }
            else
            {
                // Trouver un véhicule proche ou en créer un
                var nearbyVehicles = World.GetNearbyVehicles(officer, 30f);
                Vehicle? targetVehicle = null;

                // Chercher un véhicule de police
                foreach (var vehicle in nearbyVehicles)
                {
                    if (IsPoliceVehicle(vehicle) && (vehicle.Driver == null || vehicle.Driver == officer))
                    {
                        targetVehicle = vehicle;
                        break;
                    }
                }

                if (targetVehicle != null)
                {
                    Function.Call(Hash.TASK_ENTER_VEHICLE, officer, targetVehicle, -1, -1, 1f, 1, 0);
                }
            }
        }

        private void ConfigureChaseType(ChaseType chaseType)
        {
            switch (chaseType)
            {
                case ChaseType.NonLethal:
                    // Désactiver les tirs automatiques des véhicules de police
                    Function.Call(Hash.SET_DISABLE_WANTED_CONES_RESPONSE, true);
                    break;

                case ChaseType.Lethal:
                    // Activer les tirs automatiques
                    Function.Call(Hash.SET_DISABLE_WANTED_CONES_RESPONSE, false);
                    break;
            }
        }

        private void SetNonLethalBehavior(Ped officer)
        {
            // Comportement non-létal
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 5, false);  // Ne tire pas sans être menacé
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 46, true);  // Comportement défensif
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 1, false);  // Ne peut pas utiliser les poings
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 2, false);  // Ne peut pas utiliser les armes
        }

        private void SetLethalBehavior(Ped officer)
        {
            // Comportement létal
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 5, true);   // Peut tirer
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 46, false); // Comportement agressif
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 1, true);   // Peut utiliser les poings
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 2, true);   // Peut utiliser les armes

            // Augmenter la précision et l'agressivité
            Function.Call(Hash.SET_PED_ACCURACY, officer, 85);
            Function.Call(Hash.SET_PED_COMBAT_RANGE, officer, 2); // Moyenne portée
        }

        private void ResetOfficerCombatAttributes(Ped officer)
        {
            // Remettre les attributs par défaut
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 5, false);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 46, true);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 1, true);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, officer, 2, true);
            Function.Call(Hash.SET_PED_ACCURACY, officer, 50);
        }

        private bool IsPoliceOfficer(Ped ped)
        {
            if (ped == null || !ped.Exists() || !ped.IsAlive) return false;

            var model = ped.Model;
            return model == PedHash.Cop01SFY ||
                   model == PedHash.Cop01SMY ||
                   model == PedHash.Sheriff01SFY ||
                   model == PedHash.Sheriff01SMY ||
                   model == PedHash.Swat01SMY ||
                   Function.Call<bool>(Hash.IS_PED_IN_ANY_POLICE_VEHICLE, ped);
        }

        private bool IsPoliceVehicle(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return false;

            var model = vehicle.Model;
            return model == VehicleHash.Police ||
                   model == VehicleHash.Police2 ||
                   model == VehicleHash.Police3 ||
                   model == VehicleHash.Police4 ||
                   model == VehicleHash.Sheriff ||
                   model == VehicleHash.Sheriff2;
        }

        private Vector3 GetRandomDirection()
        {
            var random = new Random();
            var angle = random.NextDouble() * 2 * Math.PI;
            return new Vector3((float)Math.Cos(angle), (float)Math.Sin(angle), 0);
        }
    }

    /// <summary>
    /// Types de poursuite
    /// </summary>
    public enum ChaseType
    {
        None,      // Pas de poursuite
        NonLethal, // Poursuite pour arrestation
        Lethal     // Poursuite avec tirs autorisés
    }
} 