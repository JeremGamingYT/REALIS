using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using REALIS.Core;
using Screen = GTA.UI.Screen;

namespace REALIS.Transportation
{
    /// <summary>
    /// Permet au joueur d'entrer et de conduire les trains de Los Santos
    /// </summary>
    public class TrainDriverManager : Script
    {
        private Vehicle? _currentTrain;
        private bool _isDriving;
        private float _targetSpeed;
        private bool _emergencyBrake;
        private DateTime _lastStationTime;
        private readonly string[] _stations = {
            "Gare Centrale", "Port de Los Santos", "Aéroport International",
            "Quartier des Affaires", "Little Seoul", "Vinewood Hills",
            "Sandy Shores", "Paleto Bay", "Terminal Marchandises"
        };
        private int _currentStationIndex;
        
        // Système de blips pour la minimap
        private readonly Dictionary<int, Blip> _trainBlips = new();
        private int _blipUpdateTick;

        public TrainDriverManager()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                Ped player = Game.Player.Character;

                if (!_isDriving)
                {
                    // Chercher un train proche pour y entrer
                    CheckForNearbyTrains(player);
                }
                else if (_currentTrain != null && _currentTrain.Exists())
                {
                    // Gérer la conduite du train
                    HandleTrainDriving();
                    
                    // Afficher l'interface de conduite
                    DisplayTrainHUD();
                }
                else
                {
                    // Le train a disparu, sortir du mode conduite
                    ExitTrainMode();
                }
                
                // Mettre à jour les blips de trains toutes les 2 secondes
                _blipUpdateTick++;
                if (_blipUpdateTick % 120 == 0) // 60 FPS * 2 = 120 ticks
                {
                    UpdateTrainBlips();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"TrainDriver tick error: {ex.Message}");
            }
        }

        private void CheckForNearbyTrains(Ped player)
        {
            var vehicles = World.GetNearbyVehicles(player.Position, 15f);
            
            foreach (var vehicle in vehicles)
            {
                if (vehicle == null || !vehicle.Exists()) continue;
                
                // Vérifier si c'est un train
                if (vehicle.Model.Hash == 0x3D6AAA9B || // freight
                    vehicle.Model.Hash == 0x33C9E158 || // freightcar
                    vehicle.Model.Hash == 0x5B76BB8E || // freightgrain
                    vehicle.Model.Hash == 0x0AFD22A6 || // freightcont1
                    vehicle.Model.Hash == 0x36630ED4 || // freightcont2
                    vehicle.Model.Hash == 0x19FF6A4E || // tankercar
                    vehicle.Model.Hash == 0x5677C9DF)   // metrotrain
                {
                    float distance = player.Position.DistanceTo(vehicle.Position);
                    if (distance < 5f)
                    {
                        // Afficher l'instruction pour entrer
                        Screen.ShowSubtitle("~INPUT_CONTEXT~ Entrer dans le train", 100);
                        return;
                    }
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                Ped player = Game.Player.Character;

                if (e.KeyCode == Keys.E && !_isDriving)
                {
                    // Essayer d'entrer dans un train proche
                    TryEnterTrain(player);
                }
                else if (e.KeyCode == Keys.F && _isDriving)
                {
                    // Sortir du train
                    ExitTrain();
                }
                else if (_isDriving && _currentTrain != null)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.W:
                            // Accélérer
                            _targetSpeed = Math.Min(_targetSpeed + 15f, 120f);
                            break;
                        case Keys.S:
                            // Freiner seulement (pas de marche arrière)
                            _targetSpeed = Math.Max(_targetSpeed - 20f, 0f);
                            break;
                        case Keys.Space:
                            // Frein d'urgence
                            _emergencyBrake = true;
                            _targetSpeed = 0f;
                            Notification.PostTicker("~r~FREIN D'URGENCE ACTIVÉ!", true);
                            break;
                        case Keys.Q:
                            // Arrêt graduel
                            _targetSpeed = 0f;
                            break;
                        case Keys.Tab:
                            // Annoncer la prochaine station
                            AnnounceNextStation();
                            break;
                        case Keys.H:
                            // Klaxon de train
                            SoundTrainHorn();
                            break;
                        case Keys.X:
                            // Supprimer les wagons (simulation de détachement)
                            DetachCarriagesByDeletion();
                            break;

                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"TrainDriver key error: {ex.Message}");
            }
        }

        private void TryEnterTrain(Ped player)
        {
            var vehicles = World.GetNearbyVehicles(player.Position, 10f);
            
            foreach (var vehicle in vehicles)
            {
                if (vehicle == null || !vehicle.Exists()) continue;
                
                // Vérifier si c'est un train
                if (IsTrainVehicle(vehicle))
                {
                    float distance = player.Position.DistanceTo(vehicle.Position);
                    if (distance < 8f)
                    {
                        // Téléporter le joueur dans la locomotive
                        player.Task.WarpIntoVehicle(vehicle, VehicleSeat.Driver);
                        
                        _currentTrain = vehicle;
                        _isDriving = true;
                        _targetSpeed = 0f;
                        _emergencyBrake = false;
                        _lastStationTime = DateTime.Now;
                        
                        // Prendre le contrôle du train et s'assurer qu'il peut bouger
                        Function.Call(Hash.SET_TRAIN_SPEED, vehicle.Handle, 0f);
                        Function.Call(Hash.SET_TRAIN_CRUISE_SPEED, vehicle.Handle, 0f);
                        
                        // S'assurer que le train peut être contrôlé
                        Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle.Handle, true, true, false);
                        Function.Call(Hash.SET_VEHICLE_HANDBRAKE, vehicle.Handle, false);
                        
                        // Mettre à jour immédiatement la couleur du blip
                        UpdateTrainBlipColor(vehicle);
                        
                        Notification.PostTicker("~g~Vous conduisez maintenant le train!", false);
                        Notification.PostTicker("~y~W: Accélérer | S: Freiner | Q: Arrêt | SPACE: Frein urgence | F: Sortir | TAB: Station | H: Klaxon | X: Détacher", false);
                        
                        break;
                    }
                }
            }
        }

        private bool IsTrainVehicle(Vehicle vehicle)
        {
            var hash = vehicle.Model.Hash;
            return hash == 0x3D6AAA9B || // freight
                   hash == 0x33C9E158 || // freightcar
                   hash == 0x5B76BB8E || // freightgrain
                   hash == 0x0AFD22A6 || // freightcont1
                   hash == 0x36630ED4 || // freightcont2
                   hash == 0x19FF6A4E || // tankercar
                   hash == 0x5677C9DF;   // metrotrain
        }

        /// <summary>
        /// Vérifie si le véhicule est une locomotive (pas un wagon)
        /// </summary>
        private bool IsTrainLocomotive(Vehicle vehicle)
        {
            var hash = vehicle.Model.Hash;
            return hash == 0x3D6AAA9B || // freight (locomotive principale)
                   hash == 0x5677C9DF;   // metrotrain (locomotive métro)
        }

        /// <summary>
        /// Vérifie si le véhicule est un wagon de train
        /// </summary>
        private bool IsTrainCarriage(Vehicle vehicle)
        {
            var hash = vehicle.Model.Hash;
            return hash == 0x33C9E158 || // freightcar
                   hash == 0x5B76BB8E || // freightgrain
                   hash == 0x0AFD22A6 || // freightcont1
                   hash == 0x36630ED4 || // freightcont2
                   hash == 0x19FF6A4E;   // tankercar
        }

        private void HandleTrainDriving()
        {
            if (_currentTrain == null || !_currentTrain.Exists()) return;

            // Obtenir la vitesse actuelle
            float currentSpeed = _currentTrain.Speed * 3.6f; // Convertir en km/h
            float speedDiff = _targetSpeed - currentSpeed;
            
            // Appliquer la vitesse de manière plus directe
            if (Math.Abs(speedDiff) > 2f)
            {
                float newSpeedMs = _targetSpeed / 3.6f; // Convertir en m/s
                
                if (_emergencyBrake)
                {
                    // Frein d'urgence - arrêt immédiat
                    Function.Call(Hash.SET_TRAIN_SPEED, _currentTrain.Handle, 0f);
                    Function.Call(Hash.SET_TRAIN_CRUISE_SPEED, _currentTrain.Handle, 0f);
                }
                else
                {
                    // Application progressive de la vitesse
                    float acceleration = speedDiff > 0 ? 0.5f : -0.8f;
                    float smoothSpeed = currentSpeed + (acceleration * 60f * Game.LastFrameTime);
                    smoothSpeed = Math.Max(0f, Math.Min(120f, smoothSpeed));
                    
                    Function.Call(Hash.SET_TRAIN_SPEED, _currentTrain.Handle, smoothSpeed / 3.6f);
                    Function.Call(Hash.SET_TRAIN_CRUISE_SPEED, _currentTrain.Handle, smoothSpeed / 3.6f);
                }
            }

            // Désactiver le frein d'urgence après arrêt complet
            if (_emergencyBrake && currentSpeed < 1f)
            {
                _emergencyBrake = false;
            }

            // Vérifier si on passe près d'une "station" (simulation)
            CheckStationProximity();
        }

        private void CheckStationProximity()
        {
            if ((DateTime.Now - _lastStationTime).TotalSeconds > 30) // Nouvelle station toutes les 30 secondes
            {
                _currentStationIndex = (_currentStationIndex + 1) % _stations.Length;
                _lastStationTime = DateTime.Now;
                
                string stationName = _stations[_currentStationIndex];
                Notification.PostTicker($"~b~Approche de: {stationName}", false);
                
                // Son d'annonce (simulation)
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET", 0);
            }
        }

        private void AnnounceNextStation()
        {
            string nextStation = _stations[(_currentStationIndex + 1) % _stations.Length];
            Notification.PostTicker($"~b~Prochaine station: {nextStation}", false);
            Screen.ShowSubtitle($"Prochaine station: {nextStation}", 3000);
        }

        /// <summary>
        /// Joue le son du klaxon de train
        /// </summary>
        private void SoundTrainHorn()
        {
            if (_currentTrain == null || !_currentTrain.Exists()) return;

            try
            {
                // Jouer le son de klaxon de train
                Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "HORN", _currentTrain.Handle, "DLC_IMPORT_EXPORT_GENERAL_SOUNDS", true, 0);
                
                // Son alternatif si le premier ne fonctionne pas
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "HORN_LONG", "HUD_FRONTEND_DEFAULT_SOUNDSET", 0);
                
                // Notification visuelle
                Notification.PostTicker("~y~🚂 KLAXON TRAIN!", false);
                
                // Faire clignoter brièvement les phares
                Function.Call(Hash.SET_VEHICLE_LIGHTS, _currentTrain.Handle, 2);
                
                // Remettre les phares normaux après 1 seconde (sera géré par le moteur automatiquement)
            }
            catch (Exception ex)
            {
                Logger.Error($"SoundTrainHorn error: {ex.Message}");
            }
        }

        /// <summary>
        /// Simule le détachement des wagons en les supprimant (plus propre et réaliste)
        /// </summary>
        private void DetachCarriagesByDeletion()
        {
            if (_currentTrain == null || !_currentTrain.Exists())
            {
                Notification.PostTicker("~r~Aucun train à manipuler!", false);
                return;
            }

            try
            {
                // Arrêter le train d'abord pour plus de réalisme
                Function.Call(Hash.SET_TRAIN_SPEED, _currentTrain.Handle, 0f);
                Function.Call(Hash.SET_TRAIN_CRUISE_SPEED, _currentTrain.Handle, 0f);
                
                // Chercher tous les wagons attachés dans un rayon proche
                var nearbyVehicles = World.GetNearbyVehicles(_currentTrain.Position, 150f);
                int deletedCount = 0;
                
                foreach (var vehicle in nearbyVehicles)
                {
                    if (vehicle == null || !vehicle.Exists()) continue;
                    if (vehicle.Handle == _currentTrain.Handle) continue; // Ne pas supprimer la locomotive
                    
                    // Si c'est un wagon de train
                    if (IsTrainCarriage(vehicle))
                    {
                        // Vérifier s'il est proche de notre train (probablement attaché)
                        float distance = vehicle.Position.DistanceTo(_currentTrain.Position);
                        if (distance < 120f) // Distance généreuse pour capturer tous les wagons du convoi
                        {
                            // Effet visuel avant suppression
                            CreateDetachmentEffect(vehicle);
                            
                            // Supprimer le wagon proprement
                            vehicle.Delete();
                            deletedCount++;
                        }
                    }
                }
                
                if (deletedCount > 0)
                {
                    Notification.PostTicker($"~g~{deletedCount} wagon(s) détaché(s) avec succès!", false);
                    Screen.ShowSubtitle($"~g~✂️ {deletedCount} wagon(s) détaché(s)!", 2500);
                    
                    // Son de détachement mécanique
                    Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET", 0);
                    
                    // Son supplémentaire de métal qui se sépare
                    Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "MEDAL_UP", "HUD_MINI_GAME_SOUNDSET", 0);
                }
                else
                {
                    Notification.PostTicker("~y~Aucun wagon trouvé à détacher", false);
                    Screen.ShowSubtitle("~y~Train déjà sans wagons", 2000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"DetachCarriagesByDeletion error: {ex.Message}");
                Notification.PostTicker("~r~Erreur lors du détachement des wagons", false);
            }
        }

        /// <summary>
        /// Crée un petit effet visuel lors du détachement d'un wagon
        /// </summary>
        private void CreateDetachmentEffect(Vehicle wagon)
        {
            try
            {
                // Petit effet de particules métalliques
                Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "core");
                
                if (Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, "core"))
                {
                    Function.Call(Hash.USE_PARTICLE_FX_ASSET, "core");
                    Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD,
                        "ent_dst_sparking_wires", 
                        wagon.Position.X, wagon.Position.Y, wagon.Position.Z + 1f,
                        0f, 0f, 0f, 0.5f, false, false, false);
                }
                
                // Son de métal qui craque
                Function.Call(Hash.PLAY_SOUND_FROM_COORD, -1, "Object_Break", 
                    wagon.Position.X, wagon.Position.Y, wagon.Position.Z,
                    "DLC_HEIST_HACKING_SNAKE_SOUNDS", false, 20f, false);
            }
            catch (Exception ex)
            {
                Logger.Error($"CreateDetachmentEffect error: {ex.Message}");
            }
        }



        private void DisplayTrainHUD()
        {
            if (_currentTrain == null) return;

            float speed = _currentTrain.Speed * 3.6f; // km/h
            string speedColor = speed > 80f ? "~r~" : (speed > 40f ? "~y~" : "~g~");
            
            // Affichage principal
            var text = $"~b~=== CONDUITE TRAIN ===~n~" +
                      $"Vitesse: {speedColor}{speed:F0} km/h~n~" +
                      $"Vitesse cible: ~w~{_targetSpeed:F0} km/h~n~" +
                      $"Station actuelle: ~y~{_stations[_currentStationIndex]}~n~" +
                      $"~w~W: +15 km/h | S: -20 km/h | H: Klaxon | X: Détacher~n~" +
                      $"{(_emergencyBrake ? "~r~FREIN D'URGENCE!" : "")}";

            // Afficher en haut à droite
            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_SCALE, 0.35f, 0.35f);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
            Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, true);
            Function.Call(Hash.SET_TEXT_WRAP, 0.0f, 0.98f);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.98f, 0.02f);

            // Barre de vitesse visuelle
            DrawSpeedBar(speed);
        }

        private void DrawSpeedBar(float speed)
        {
            float normalizedSpeed = speed / 120f; // Normaliser sur 120 km/h max
            normalizedSpeed = Math.Min(1f, Math.Max(0f, normalizedSpeed));

            // Fond de la barre
            Function.Call(Hash.DRAW_RECT, 0.92f, 0.5f, 0.02f, 0.3f, 0, 0, 0, 150);
            
            // Barre de vitesse
            Color barColor = normalizedSpeed > 0.67f ? Color.Red : (normalizedSpeed > 0.33f ? Color.Yellow : Color.Green);
            float barHeight = normalizedSpeed * 0.28f;
            Function.Call(Hash.DRAW_RECT, 0.92f, 0.65f - barHeight/2, 0.018f, barHeight, 
                         barColor.R, barColor.G, barColor.B, 200);
        }

        private void ExitTrain()
        {
            if (_currentTrain != null && _currentTrain.Exists())
            {
                Ped player = Game.Player.Character;
                
                // Arrêter le train graduellement
                Function.Call(Hash.SET_TRAIN_SPEED, _currentTrain.Handle, 0f);
                Function.Call(Hash.SET_TRAIN_CRUISE_SPEED, _currentTrain.Handle, 0f);
                
                // Faire sortir le joueur
                player.Task.LeaveVehicle();
                
                Notification.PostTicker("~y~Vous avez quitté la conduite du train", false);
            }
            
            ExitTrainMode();
        }

        private void ExitTrainMode()
        {
            // Remettre le blip en blanc avant de perdre la référence
            if (_currentTrain != null && _currentTrain.Exists())
            {
                UpdateTrainBlipColor(_currentTrain);
            }
            
            _isDriving = false;
            _currentTrain = null;
            _targetSpeed = 0f;
            _emergencyBrake = false;
        }

        private void UpdateTrainBlips()
        {
            try
            {
                // Obtenir tous les véhicules dans une large zone
                var vehicles = World.GetNearbyVehicles(Game.Player.Character.Position, 2000f);
                var currentTrains = new HashSet<int>();

                foreach (var vehicle in vehicles)
                {
                    if (vehicle == null || !vehicle.Exists()) continue;
                    
                    // Créer des blips uniquement pour les locomotives, pas les wagons
                    if (IsTrainLocomotive(vehicle))
                    {
                        currentTrains.Add(vehicle.Handle);
                        
                        // Créer ou mettre à jour le blip pour cette locomotive
                        if (!_trainBlips.ContainsKey(vehicle.Handle))
                        {
                            CreateTrainBlip(vehicle);
                        }
                        else
                        {
                            UpdateTrainBlipColor(vehicle);
                        }
                    }
                }

                // Supprimer les blips des trains qui n'existent plus
                var blipsToRemove = new List<int>();
                foreach (var kvp in _trainBlips)
                {
                    if (!currentTrains.Contains(kvp.Key))
                    {
                        kvp.Value.Delete();
                        blipsToRemove.Add(kvp.Key);
                    }
                }

                foreach (var handle in blipsToRemove)
                {
                    _trainBlips.Remove(handle);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"UpdateTrainBlips error: {ex.Message}");
            }
        }

        private void CreateTrainBlip(Vehicle train)
        {
            try
            {
                var blip = train.AddBlip();
                if (blip != null && blip.Exists())
                {
                    blip.Sprite = BlipSprite.Train;
                    blip.Name = "Train";
                    blip.Scale = 0.8f;
                    blip.IsShortRange = false; // Visible même de loin
                    
                    // Couleur initiale (blanc si pas conduit)
                    blip.Color = (_currentTrain != null && _currentTrain.Handle == train.Handle) 
                                ? BlipColor.Blue2 
                                : BlipColor.White;
                    
                    _trainBlips[train.Handle] = blip;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"CreateTrainBlip error: {ex.Message}");
            }
        }

        private void UpdateTrainBlipColor(Vehicle train)
        {
            try
            {
                if (_trainBlips.TryGetValue(train.Handle, out var blip) && blip.Exists())
                {
                    // Bleu si c'est le train que le joueur conduit, blanc sinon
                    BlipColor newColor = (_currentTrain != null && _currentTrain.Handle == train.Handle) 
                                        ? BlipColor.Blue2 
                                        : BlipColor.White;
                    
                    if (blip.Color != newColor)
                    {
                        blip.Color = newColor;
                        
                        // Changer aussi le nom si c'est le train du joueur
                        if (newColor == BlipColor.Blue2)
                        {
                            blip.Name = "🚂 Train (Vous conduisez)";
                        }
                        else
                        {
                            blip.Name = "Train";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"UpdateTrainBlipColor error: {ex.Message}");
            }
        }

        private void CleanupAllBlips()
        {
            try
            {
                foreach (var blip in _trainBlips.Values)
                {
                    if (blip != null && blip.Exists())
                    {
                        blip.Delete();
                    }
                }
                _trainBlips.Clear();
            }
            catch (Exception ex)
            {
                Logger.Error($"CleanupAllBlips error: {ex.Message}");
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            CleanupAllBlips();
            ExitTrainMode();
        }
    }
} 