using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.Math;

namespace REALIS.NPC.Police
{
    /// <summary>
    /// Gestionnaire du processus d'arrestation complet
    /// </summary>
    public class PoliceArrestHandler
    {
        private ArrestState _currentState;
        private DateTime _stateStartTime;
        private Ped? _arrestingOfficer;
        private Vehicle? _policeVehicle;
        private Vector3 _nearestPoliceStation;
        private bool _isTransporting;

        public bool IsArrestInProgress => _currentState != ArrestState.None;

        public PoliceArrestHandler()
        {
            _currentState = ArrestState.None;
            _stateStartTime = DateTime.Now;
            _arrestingOfficer = null;
            _policeVehicle = null;
            _isTransporting = false;
        }

        public void Update()
        {
            if (_currentState == ArrestState.None) return;

            CheckForArrestOpportunity();
            UpdateCurrentState();
        }

        private void CheckForArrestOpportunity()
        {
            if (_currentState != ArrestState.None) return;

            var player = Game.Player.Character;
            
            // Ne pas arrêter si le joueur est dans un véhicule ou armé
            if (player.IsInVehicle() || Function.Call<bool>(Hash.IS_PED_ARMED, player, 7)) return;

            // Chercher un officier proche pour l'arrestation
            var nearbyPeds = World.GetNearbyPeds(player, 15f);
            
            foreach (var ped in nearbyPeds)
            {
                if (IsPoliceOfficer(ped) && Vector3.Distance(ped.Position, player.Position) < 5f)
                {
                    StartArrestProcess(ped);
                    break;
                }
            }
        }

        private void StartArrestProcess(Ped officer)
        {
            _arrestingOfficer = officer;
            _currentState = ArrestState.Approaching;
            _stateStartTime = DateTime.Now;

            GTA.UI.Notification.PostTicker("~b~Officier:~w~ Ne bougez pas! Vous êtes en état d'arrestation!", true);
        }

        private void UpdateCurrentState()
        {
            var timeSinceStateStart = DateTime.Now - _stateStartTime;

            switch (_currentState)
            {
                case ArrestState.Approaching:
                    HandleApproaching(timeSinceStateStart);
                    break;
                case ArrestState.Handcuffing:
                    HandleHandcuffing(timeSinceStateStart);
                    break;
                case ArrestState.EscortingToVehicle:
                    HandleEscortingToVehicle(timeSinceStateStart);
                    break;
                case ArrestState.TransportingToStation:
                    HandleTransportingToStation(timeSinceStateStart);
                    break;
                case ArrestState.ArrivingAtStation:
                    HandleArrivingAtStation(timeSinceStateStart);
                    break;
            }
        }

        private void HandleApproaching(TimeSpan timeElapsed)
        {
            if (!IsOfficerValid() || _arrestingOfficer == null) 
            {
                Reset();
                return;
            }

            var player = Game.Player.Character;
            var distance = Vector3.Distance(_arrestingOfficer.Position, player.Position);

            // L'officier s'approche du joueur
            Function.Call(Hash.TASK_GO_TO_ENTITY, _arrestingOfficer, player, -1, 1.5f, 2f, 1073741824, 0);

            if (distance < 2f || timeElapsed.TotalSeconds > 10)
            {
                TransitionToState(ArrestState.Handcuffing);
            }
        }

        private void HandleHandcuffing(TimeSpan timeElapsed)
        {
            if (!IsOfficerValid() || _arrestingOfficer == null) 
            {
                Reset();
                return;
            }

            var player = Game.Player.Character;

            // Animation de menottage
            if (timeElapsed.TotalSeconds < 1)
            {
                Function.Call(Hash.TASK_TURN_PED_TO_FACE_ENTITY, _arrestingOfficer, player, -1);
                GTA.UI.Notification.PostTicker("~b~Officier:~w~ Mettez vos mains derrière le dos!", true);
            }
            else if (timeElapsed.TotalSeconds < 3)
            {
                // Forcer le joueur à s'arrêter
                Function.Call(Hash.SET_PLAYER_CONTROL, Game.Player, false, 0);
                
                // Animation de menottage
                Function.Call(Hash.TASK_ARREST_PED, _arrestingOfficer, player);
                
                if (timeElapsed.TotalSeconds > 2)
                {
                    GTA.UI.Notification.PostTicker("~y~*Clic* Vous êtes menotté!", true);
                }
            }
            else
            {
                TransitionToState(ArrestState.EscortingToVehicle);
            }
        }

        private void HandleEscortingToVehicle(TimeSpan timeElapsed)
        {
            if (!IsOfficerValid() || _arrestingOfficer == null) 
            {
                Reset();
                return;
            }

            // Chercher ou créer un véhicule de police
            if (_policeVehicle == null || !_policeVehicle.Exists())
            {
                FindOrCreatePoliceVehicle();
            }

            if (_policeVehicle != null && _policeVehicle.Exists())
            {
                var player = Game.Player.Character;
                
                // Escorter le joueur vers le véhicule
                var vehiclePosition = _policeVehicle.Position;
                var rearDoorPosition = _policeVehicle.GetOffsetPosition(new Vector3(-1.5f, -1f, 0f));

                // L'officier escorte le joueur
                Function.Call(Hash.TASK_GO_TO_COORD_ANY_MEANS, _arrestingOfficer, rearDoorPosition.X, rearDoorPosition.Y, rearDoorPosition.Z, 1f, 0, false, 786603, 0xbf800000);
                
                var distance = Vector3.Distance(player.Position, rearDoorPosition);
                if (distance < 2f || timeElapsed.TotalSeconds > 15)
                {
                    // Faire monter le joueur dans le véhicule
                    Function.Call(Hash.TASK_ENTER_VEHICLE, player, _policeVehicle, -1, 1, 1f, 1, 0); // Siège arrière gauche
                    
                    TransitionToState(ArrestState.TransportingToStation);
                }
            }
        }

        private void HandleTransportingToStation(TimeSpan timeElapsed)
        {
            if (_policeVehicle == null || !_policeVehicle.Exists() || _arrestingOfficer == null)
            {
                Reset();
                return;
            }

            var player = Game.Player.Character;

            if (!_isTransporting)
            {
                // Trouver le poste de police le plus proche
                _nearestPoliceStation = FindNearestPoliceStation();
                
                // L'officier monte dans le véhicule et démarre le transport
                if (!_arrestingOfficer.IsInVehicle(_policeVehicle))
                {
                    Function.Call(Hash.TASK_ENTER_VEHICLE, _arrestingOfficer, _policeVehicle, -1, -1, 1f, 1, 0); // Siège conducteur
                }
                else
                {
                    // Conduire vers le poste
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, _arrestingOfficer, _policeVehicle, 
                        _nearestPoliceStation.X, _nearestPoliceStation.Y, _nearestPoliceStation.Z, 25f, 0, 
                        _policeVehicle.Model.Hash, 786603, 5f, -1);
                    
                    _isTransporting = true;
                    GTA.UI.Notification.PostTicker("~b~Officier:~w~ Direction le poste de police...", true);
                }
            }
            else
            {
                // Vérifier si on est arrivé au poste
                var distance = Vector3.Distance(_policeVehicle.Position, _nearestPoliceStation);
                if (distance < 10f || timeElapsed.TotalSeconds > 60)
                {
                    TransitionToState(ArrestState.ArrivingAtStation);
                }
            }
        }

        private void HandleArrivingAtStation(TimeSpan timeElapsed)
        {
            var player = Game.Player.Character;

            if (timeElapsed.TotalSeconds < 2)
            {
                GTA.UI.Notification.PostTicker("~b~Officier:~w~ Nous sommes arrivés. Sortez du véhicule.", true);
            }
            else if (timeElapsed.TotalSeconds < 5)
            {
                // Faire sortir le joueur
                if (player.IsInVehicle())
                {
                    Function.Call(Hash.TASK_LEAVE_VEHICLE, player, player.CurrentVehicle, 0);
                }
            }
            else
            {
                // Fin du processus d'arrestation
                CompleteArrest();
            }
        }

        private void CompleteArrest()
        {
            var player = Game.Player.Character;

            // Rendre le contrôle au joueur
            Function.Call(Hash.SET_PLAYER_CONTROL, Game.Player, true, 0);
            
            // Supprimer le niveau de recherche
            Game.Player.Wanted.SetWantedLevel(0, false);
            
            // Message final
            GTA.UI.Notification.PostTicker("~g~Vous avez été relâché avec un avertissement.", true);
            GTA.UI.Notification.PostTicker("~y~Évitez les ennuis à l'avenir!", true);

            Reset();
        }

        private void FindOrCreatePoliceVehicle()
        {
            var player = Game.Player.Character;
            var nearbyVehicles = World.GetNearbyVehicles(player, 50f);

            // Chercher un véhicule de police existant
            foreach (var vehicle in nearbyVehicles)
            {
                if (IsPoliceVehicle(vehicle) && vehicle.Driver == null)
                {
                    _policeVehicle = vehicle;
                    return;
                }
            }

            // Créer un nouveau véhicule de police si nécessaire
            var spawnPos = player.Position + player.ForwardVector * 10f;
            _policeVehicle = World.CreateVehicle(VehicleHash.Police, spawnPos);
            
            if (_policeVehicle != null)
            {
                _policeVehicle.IsPersistent = true;
            }
        }

        private Vector3 FindNearestPoliceStation()
        {
            // Postes de police principaux de Los Santos
            var policeStations = new Vector3[]
            {
                new Vector3(436.1f, -982.1f, 30.7f),    // Mission Row Police Station
                new Vector3(-448.8f, 6014.0f, 31.7f),   // Paleto Bay Sheriff
                new Vector3(1853.2f, 3689.6f, 34.3f),   // Sandy Shores Sheriff
                new Vector3(-1108.4f, -845.8f, 19.3f)   // Vespucci Police Station
            };

            var playerPos = Game.Player.Character.Position;
            var nearestStation = policeStations[0];
            var shortestDistance = Vector3.Distance(playerPos, nearestStation);

            foreach (var station in policeStations)
            {
                var distance = Vector3.Distance(playerPos, station);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearestStation = station;
                }
            }

            return nearestStation;
        }

        private bool IsPoliceOfficer(Ped ped)
        {
            if (ped == null || !ped.Exists() || !ped.IsAlive) return false;
            
            var model = ped.Model;
            return model == PedHash.Cop01SFY || 
                   model == PedHash.Cop01SMY || 
                   model == PedHash.Sheriff01SFY || 
                   model == PedHash.Sheriff01SMY ||
                   model == PedHash.Swat01SMY;
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

        private bool IsOfficerValid()
        {
            return _arrestingOfficer != null && _arrestingOfficer.Exists() && _arrestingOfficer.IsAlive;
        }

        private void TransitionToState(ArrestState newState)
        {
            _currentState = newState;
            _stateStartTime = DateTime.Now;
        }

        public void Reset()
        {
            // Rendre le contrôle au joueur si nécessaire
            Function.Call(Hash.SET_PLAYER_CONTROL, Game.Player, true, 0);

            _currentState = ArrestState.None;
            _arrestingOfficer = null;
            _policeVehicle = null;
            _isTransporting = false;
        }
    }

    /// <summary>
    /// États du processus d'arrestation
    /// </summary>
    public enum ArrestState
    {
        None,                    // Pas d'arrestation en cours
        Approaching,             // L'officier s'approche
        Handcuffing,            // Processus de menottage
        EscortingToVehicle,     // Escorte vers le véhicule
        TransportingToStation,  // Transport vers le poste
        ArrivingAtStation       // Arrivée au poste
    }
} 