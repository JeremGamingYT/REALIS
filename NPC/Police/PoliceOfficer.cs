using System;
using GTA;
using GTA.Native;
using GTA.Math;

namespace REALIS.NPC.Police
{
    /// <summary>
    /// Représente un officier de police individuel avec ses comportements spécifiques
    /// </summary>
    public class PoliceOfficer
    {
        private readonly Ped _ped;
        private OfficerState _currentState;
        private DateTime _lastStateChange;
        private Vector3 _lastKnownPlayerPosition;
        private bool _isInArrestMode;

        public Ped Ped => _ped;
        public bool IsValid => _ped != null && _ped.Exists() && _ped.IsAlive;
        public OfficerState CurrentState => _currentState;

        public PoliceOfficer(Ped ped)
        {
            _ped = ped ?? throw new ArgumentNullException(nameof(ped));
            _currentState = OfficerState.Patrol;
            _lastStateChange = DateTime.Now;
            _lastKnownPlayerPosition = Vector3.Zero;
            _isInArrestMode = false;

            InitializeOfficer();
        }

        private void InitializeOfficer()
        {
            if (!IsValid) return;

            // Configuration de base de l'officier
            _ped.CanRagdoll = false;
            _ped.BlockPermanentEvents = true;
            
            // Attributs de combat par défaut (non-agressif initialement)
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _ped, 5, false); // Ne tire pas sans être menacé
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _ped, 46, true); // Comportement défensif
            Function.Call(Hash.SET_PED_ACCURACY, _ped, 75); // Précision modérée
            
            // Relation avec le joueur
            Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, _ped, Function.Call<int>(Hash.GET_HASH_KEY, "COP"));
        }

        public void Update()
        {
            if (!IsValid) return;

            UpdateState();
            ExecuteCurrentState();
        }

        private void UpdateState()
        {
            var player = Game.Player.Character;
            var distanceToPlayer = Vector3.Distance(_ped.Position, player.Position);
            var timeSinceLastStateChange = DateTime.Now - _lastStateChange;

            switch (_currentState)
            {
                case OfficerState.Patrol:
                    if (Game.Player.Wanted.WantedLevel > 0 && distanceToPlayer < 100f)
                    {
                        ChangeState(OfficerState.Pursuing);
                    }
                    break;

                case OfficerState.Pursuing:
                    if (Game.Player.Wanted.WantedLevel == 0)
                    {
                        ChangeState(OfficerState.Patrol);
                    }
                    else if (distanceToPlayer < 5f && !_isInArrestMode)
                    {
                        ChangeState(OfficerState.Arresting);
                    }
                    break;

                case OfficerState.Arresting:
                    if (Game.Player.Wanted.WantedLevel == 0 || distanceToPlayer > 10f)
                    {
                        ChangeState(OfficerState.Patrol);
                    }
                    break;

                case OfficerState.Combat:
                    if (Game.Player.Wanted.WantedLevel == 0)
                    {
                        ChangeState(OfficerState.Patrol);
                    }
                    break;
            }
        }

        private void ExecuteCurrentState()
        {
            switch (_currentState)
            {
                case OfficerState.Patrol:
                    ExecutePatrol();
                    break;
                case OfficerState.Pursuing:
                    ExecutePursuit();
                    break;
                case OfficerState.Arresting:
                    ExecuteArrest();
                    break;
                case OfficerState.Combat:
                    ExecuteCombat();
                    break;
            }
        }

        private void ExecutePatrol()
        {
            // Comportement de patrouille normale
            if (_ped.IsInVehicle())
            {
                // Continuer la patrouille en voiture
                if (_ped.CurrentVehicle.Driver == _ped)
                {
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, _ped, _ped.CurrentVehicle, 20f, 786603);
                }
            }
            else
            {
                // Patrouille à pied
                Function.Call(Hash.TASK_WANDER_STANDARD, _ped, 10f, 10);
            }
        }

        private void ExecutePursuit()
        {
            var player = Game.Player.Character;
            _lastKnownPlayerPosition = player.Position;

            if (_ped.IsInVehicle())
            {
                // Poursuite en voiture
                var vehicle = _ped.CurrentVehicle;
                if (vehicle.Driver == _ped)
                {
                    Function.Call(Hash.TASK_VEHICLE_CHASE, _ped, player);
                }
            }
            else
            {
                // Poursuite à pied
                Function.Call(Hash.TASK_GO_TO_ENTITY, _ped, player, -1, 2f, 2f, 1073741824, 0);
            }

            // Vérifier si on peut arrêter le joueur
            var distance = Vector3.Distance(_ped.Position, player.Position);
            if (distance < 3f && !player.IsInVehicle())
            {
                ChangeState(OfficerState.Arresting);
            }
        }

        private void ExecuteArrest()
        {
            var player = Game.Player.Character;
            
            if (!_isInArrestMode)
            {
                _isInArrestMode = true;
                
                // Animation d'arrestation
                Function.Call(Hash.TASK_ARREST_PED, _ped, player);
                
                // Message d'arrestation
                GTA.UI.Notification.PostTicker("~b~Officier:~w~ Vous êtes en état d'arrestation!", true);
            }

            // Vérifier si l'arrestation est terminée
            if (Function.Call<bool>(Hash.IS_PED_BEING_ARRESTED, player))
            {
                // Procéder au menottage et transport
                StartTransportToStation();
            }
        }

        private void ExecuteCombat()
        {
            var player = Game.Player.Character;
            
            // Mode combat agressif
            Function.Call(Hash.TASK_COMBAT_PED, _ped, player, 0, 16);
            
            // Augmenter l'agressivité
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _ped, 5, true);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _ped, 46, false);
        }

        private void StartTransportToStation()
        {
            // Cette méthode sera appelée par PoliceArrestHandler
            // pour gérer le transport vers le poste
        }

        public void SetCombatMode(bool enableCombat)
        {
            if (!IsValid) return;

            if (enableCombat)
            {
                ChangeState(OfficerState.Combat);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _ped, 5, true);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _ped, 46, false);
            }
            else
            {
                if (_currentState == OfficerState.Combat)
                {
                    ChangeState(OfficerState.Pursuing);
                }
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _ped, 5, false);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _ped, 46, true);
            }
        }

        public void ResetBehavior()
        {
            if (!IsValid) return;

            _isInArrestMode = false;
            ChangeState(OfficerState.Patrol);
            Function.Call(Hash.CLEAR_PED_TASKS, _ped);
        }

        private void ChangeState(OfficerState newState)
        {
            if (_currentState != newState)
            {
                _currentState = newState;
                _lastStateChange = DateTime.Now;
                
                // Nettoyer les tâches précédentes
                Function.Call(Hash.CLEAR_PED_TASKS, _ped);
            }
        }
    }

    /// <summary>
    /// États possibles d'un officier de police
    /// </summary>
    public enum OfficerState
    {
        Patrol,      // Patrouille normale
        Pursuing,    // Poursuite du suspect
        Arresting,   // Processus d'arrestation
        Combat       // Combat avec le suspect
    }
} 