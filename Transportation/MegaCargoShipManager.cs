using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
    /// Permet au joueur de piloter les ÉNORMES cargo ships porte-conteneurs du port de Los Santos
    /// Ces navires sont normalement des objets statiques mais ce système les rend pilotables
    /// </summary>
    public class MegaCargoShipManager : Script
    {
        private Entity? _currentMegaShip;
        private Vehicle? _controlVehicle; // Véhicule invisible pour les contrôles
        private bool _isPiloting;
        private float _targetSpeed;
        private float _currentHeading;
        private bool _emergencyStop;
        private DateTime _lastHornTime;
        private Vector3 _shipDimensions;
        
        private readonly string[] _cargoShipModelNames = {
            "freighter", "freighter2", "tanker", "tanker2", "container_ship", "oil_tanker",
            "prop_container_01a", "prop_container_01b", "prop_container_01c", "prop_container_01d",
            "prop_container_ld_01a", "prop_container_ld_01b", "prop_container_ld_01c", 
            "prop_container_ld_01d", "prop_container_ld_01e", "prop_container_ld_01f",
            "prop_dock_crane_01", "prop_dock_crane_02", "prop_dock_crane_03", "prop_dock_crane_04",
            "prop_cargo_cont_01", "prop_cargo_cont_02", "prop_cargo_cont_03", "prop_cargo_cont_04",
            "prop_cargo_cont_05", "prop_cargo_cont_mail_01", "prop_cargo_cont_mail_02",
            "prop_dock_shipyard_01", "prop_dock_shipyard_02", "prop_dock_shipyard_03"
        };

        // Hash des modèles connus de gros navires statiques dans GTA V
        private readonly uint[] _staticShipModelHashes = {
            3050275055u, // SS Bulker
            3186881744u, // Ocean Motion  
            3301691100u, // Daisy Lee
            1448677353u, // Other container ship variant
            0x9F6A4D1Eu, // Freighter hash
            0x724A9B86u, // Tanker hash
        };

        private readonly Vector3[] _megaCargoPositions = {
            new Vector3(1019.84f, -2998.56f, 5.9f),     // Position cargo principal (SS Bulker area)
            new Vector3(1156.78f, -3108.45f, 5.9f),     // Position cargo secondaire (Ocean Motion area)
            new Vector3(934.56f, -2876.12f, 5.9f),      // Position tanker (Daisy Lee area)
            new Vector3(778.45f, -2951.23f, 5.9f),      // Position cargo terminal sud
            new Vector3(1243.74f, -3207.89f, 5.9f),     // Position cargo terminal nord
            new Vector3(850.12f, -3025.34f, 5.9f),      // Position additionnelle port central
            new Vector3(1089.45f, -2890.67f, 5.9f),     // Position additionnelle terminal est
        };

        private readonly Dictionary<int, Blip> _megaShipBlips = new();

        public MegaCargoShipManager()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            
            Logger.Info("MegaCargoShipManager initialisé");
            Notification.PostTicker("~g~🚢 MEGA CARGO SHIP MANAGER chargé! ~n~~w~Approchez-vous des ÉNORMES cargos du port et appuyez sur ~INPUT_CONTEXT~ (E)!", true);
            
            // Créer des blips pour indiquer les zones de méga cargos
            CreateMegaCargoBlips();
            Logger.Info("Blips de méga cargo créés");
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                Ped player = Game.Player.Character;

                if (!_isPiloting)
                {
                    // Chercher un méga cargo proche
                    CheckForNearbyMegaShips(player);
                }
                else if (_currentMegaShip != null && _currentMegaShip.Exists())
                {
                    // Gérer le pilotage du méga cargo
                    HandleMegaShipPiloting();
                    
                    // Afficher l'interface de pilotage
                    DisplayMegaShipHUD();
                }
                else
                {
                    // Le méga cargo a disparu, sortir du mode pilotage
                    ExitMegaShipMode();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"MegaCargoShip tick error: {ex.Message}");
            }
        }

        private void CheckForNearbyMegaShips(Ped player)
        {
            try
            {
                Vector3 playerPos = player.Position;
                Logger.Info($"Vérification des méga ships à la position: {playerPos}");
                
                // Chercher des véhicules de méga cargo existants
                var nearbyVehicles = World.GetNearbyVehicles(playerPos, 200f);
                Logger.Info($"Véhicules trouvés: {nearbyVehicles.Length}");
                
                foreach (var vehicle in nearbyVehicles)
                {
                    if (vehicle != null && vehicle.Exists() && IsMegaCargoShip(vehicle))
                    {
                        float distance = playerPos.DistanceTo(vehicle.Position);
                        if (distance < 100f)
                        {
                            Screen.ShowSubtitle("~b~🚢 MÉGA CARGO PILOTABLE DÉTECTÉ! ~n~~INPUT_CONTEXT~ Prendre le contrôle (E)", 100);
                            Logger.Info($"Véhicule cargo trouvé à {distance}m");
                            return;
                        }
                    }
                }
                
                // Chercher des props (objets) de méga cargo ships - avec debugging
                var nearbyProps = World.GetNearbyProps(playerPos, 300f);
                Logger.Info($"Props trouvés: {nearbyProps.Length}");
                
                Entity? closestCargo = null;
                float closestDistance = float.MaxValue;
                int propsChecked = 0;
                
                foreach (var prop in nearbyProps)
                {
                    if (prop == null || !prop.Exists()) continue;
                    propsChecked++;
                    
                    string modelName = prop.Model.ToString().ToLower();
                    Logger.Info($"Prop {propsChecked}: {modelName}");
                    
                    if (IsMegaCargoShip(prop))
                    {
                        float distance = playerPos.DistanceTo(prop.Position);
                        Logger.Info($"CARGO SHIP DÉTECTÉ: {modelName} à {distance}m");
                        if (distance < closestDistance && distance < 150f)
                        {
                            closestCargo = prop;
                            closestDistance = distance;
                        }
                    }
                }

                Logger.Info($"Props vérifiés: {propsChecked}, Cargo le plus proche: {closestCargo?.Model.ToString() ?? "aucun"}");

                if (closestCargo != null)
                {
                    string model = closestCargo.Model.ToString().ToUpper();
                    Screen.ShowSubtitle($"~g~🚢 ÉNORME CARGO DÉTECTÉ! ~n~{model} à {closestDistance:F0}m ~n~~INPUT_CONTEXT~ Convertir en navire pilotable (E)", 100);
                    return;
                }

                // Vérifier les zones de méga cargos (toujours afficher la zone la plus proche)
                Vector3 nearestZone = Vector3.Zero;
                float nearestZoneDistance = float.MaxValue;
                int zoneIndex = -1;
                
                for (int i = 0; i < _megaCargoPositions.Length; i++)
                {
                    var megaPos = _megaCargoPositions[i];
                    float distance = playerPos.DistanceTo(megaPos);
                    if (distance < nearestZoneDistance)
                    {
                        nearestZone = megaPos;
                        nearestZoneDistance = distance;
                        zoneIndex = i;
                    }
                }

                if (nearestZoneDistance < 500f) // Augmenter la distance pour debug
                {
                    string[] zoneNames = { "SS Bulker", "Ocean Motion", "Daisy Lee", "Terminal Sud", "Terminal Nord", "Port Central", "Terminal Est" };
                    string zoneName = zoneIndex < zoneNames.Length ? zoneNames[zoneIndex] : $"Zone {zoneIndex + 1}";
                    
                    Screen.ShowSubtitle($"~y~🚢 ZONE: {zoneName} ~n~Distance: {nearestZoneDistance:F0}m ~n~~INPUT_CONTEXT~ Créer méga cargo (E) | ~INPUT_VEH_HORN~ Spawn rapide (R)", 100);
                    return;
                }
                
                // Si rien n'est trouvé, afficher info de debug
                Screen.ShowSubtitle($"~w~DEBUG: Props: {nearbyProps.Length}, Véhicules: {nearbyVehicles.Length} ~n~Position: {playerPos.X:F0}, {playerPos.Y:F0}, {playerPos.Z:F0}", 100);
                
            }
            catch (Exception ex)
            {
                Logger.Error($"Erreur dans CheckForNearbyMegaShips: {ex.Message}");
                Screen.ShowSubtitle($"~r~Erreur détection: {ex.Message}", 100);
            }
        }

        private bool IsMegaCargoShip(Entity entity)
        {
            if (entity == null || !entity.Exists()) return false;
            
            try
            {
                // Vérifier d'abord les hash de modèles connus
                uint modelHash = (uint)entity.Model.Hash;
                if (_staticShipModelHashes.Contains(modelHash))
                {
                    Logger.Info($"MEGA CARGO DÉTECTÉ par hash: {modelHash:X} à {entity.Position}");
                    return true;
                }

                // Vérifier le nom du modèle
                string modelName = entity.Model.ToString().ToLower();
                
                // Recherche spécifique des noms de navires de cargo connus
                foreach (string shipName in _cargoShipModelNames)
                {
                    if (modelName.Contains(shipName.ToLower()))
                    {
                        Logger.Info($"MEGA CARGO DÉTECTÉ par nom: {modelName} à {entity.Position}");
                        return true;
                    }
                }

                // Recherche de modèles de gros navires avec mots-clés étendus
                if (modelName.Contains("ship") || modelName.Contains("boat") || modelName.Contains("freighter") ||
                    modelName.Contains("tanker") || modelName.Contains("cargo") || modelName.Contains("container") ||
                    modelName.Contains("bulker") || modelName.Contains("vessel") || modelName.Contains("freight"))
                {
                    // Vérifier la taille pour s'assurer que c'est un gros navire
                    Vector3 dimensions = entity.Model.Dimensions.frontTopRight - entity.Model.Dimensions.rearBottomLeft;
                    float size = dimensions.Length();
                    
                    // Si l'objet fait plus de 60 unités, c'est probablement un méga cargo
                    if (size > 60f && entity.Position.Z < 30f)
                    {
                        Logger.Info($"MEGA CARGO DÉTECTÉ par taille: {modelName} ({size:F1}m) à {entity.Position}");
                        return true;
                    }
                }

                // Vérification finale par taille seule - pour les objets très grands près de l'eau
                try
                {
                    Vector3 dimensions = entity.Model.Dimensions.frontTopRight - entity.Model.Dimensions.rearBottomLeft;
                    float size = dimensions.Length();
                    
                    // Si l'objet fait plus de 120 unités et est près de l'eau, c'est probablement un méga cargo
                    if (size > 120f && entity.Position.Z < 20f && entity.Position.Z > -10f)
                    {
                        Logger.Info($"MEGA CARGO DÉTECTÉ par taille massive: {modelName} ({size:F1}m) à {entity.Position}");
                        return true;
                    }
                }
                                 catch (Exception ex)
                 {
                     Logger.Error($"Erreur lors de la vérification des dimensions pour {modelName}: {ex.Message}");
                 }
            }
            catch (Exception ex)
            {
                Logger.Error($"Erreur dans IsMegaCargoShip: {ex.Message}");
            }

            return false;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                Ped player = Game.Player.Character;
                Logger.Info($"Touche pressée: {e.KeyCode}, Piloting: {_isPiloting}");

                if (e.KeyCode == Keys.E && !_isPiloting)
                {
                    Logger.Info("Tentative de prise de contrôle d'un méga cargo (E)");
                    // Essayer de prendre le contrôle d'un méga cargo
                    TryTakeControlOfMegaShip(player);
                }
                else if (e.KeyCode == Keys.F && _isPiloting)
                {
                    Logger.Info("Abandon du contrôle du méga cargo (F)");
                    // Abandonner le contrôle
                    ExitMegaShip();
                }
                else if (e.KeyCode == Keys.R && !_isPiloting)
                {
                    Logger.Info("Spawn rapide d'un méga cargo (R)");
                    // Spawn rapide d'un méga cargo
                    SpawnMegaCargoShip();
                }
                else if (_isPiloting)
                {
                    HandleMegaShipControls(e.KeyCode);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"MegaCargoShip key error: {ex.Message}");
                Notification.PostTicker($"~r~Erreur touche: {ex.Message}", true);
            }
        }

        private void HandleMegaShipControls(Keys key)
        {
            switch (key)
            {
                case Keys.W:
                    _targetSpeed = Math.Min(_targetSpeed + 3f, 25f); // Vitesse max réaliste pour un méga cargo
                    Notification.PostTicker($"🚢 MOTEURS EN AVANT - Vitesse: {_targetSpeed:F0} km/h", false);
                    break;
                case Keys.S:
                    _targetSpeed = Math.Max(_targetSpeed - 3f, -8f);
                    Notification.PostTicker($"🚢 MOTEURS EN ARRIÈRE - Vitesse: {_targetSpeed:F0} km/h", false);
                    break;
                case Keys.A:
                    _currentHeading -= 0.8f; // Rotation très lente pour un méga cargo
                    Notification.PostTicker($"⬅️ BARRE À GAUCHE - Cap: {_currentHeading:F0}°", false);
                    break;
                case Keys.D:
                    _currentHeading += 0.8f;
                    Notification.PostTicker($"➡️ BARRE À DROITE - Cap: {_currentHeading:F0}°", false);
                    break;
                case Keys.Space:
                    _emergencyStop = true;
                    _targetSpeed = 0f;
                    Notification.PostTicker("🚨 ARRÊT D'URGENCE DU MÉGA CARGO!", true);
                    break;
                case Keys.Q:
                    _targetSpeed = 0f;
                    Notification.PostTicker("🛑 Arrêt graduel des moteurs", true);
                    break;
                case Keys.H:
                    SoundMegaShipHorn();
                    break;
            }
        }

        private void TryTakeControlOfMegaShip(Ped player)
        {
            try
            {
                Vector3 playerPos = player.Position;
                Logger.Info($"Recherche de méga cargos à proximité de {playerPos}...");
                
                // D'abord chercher un véhicule existant (déjà converti)
                var nearbyVehicles = World.GetNearbyVehicles(playerPos, 200f);
                Logger.Info($"Véhicules à proximité: {nearbyVehicles.Length}");
                
                foreach (var vehicle in nearbyVehicles)
                {
                    if (vehicle != null && vehicle.Exists() && IsMegaCargoShip(vehicle))
                    {
                        float distance = playerPos.DistanceTo(vehicle.Position);
                        if (distance < 100f)
                        {
                            Logger.Info($"Véhicule cargo trouvé: {vehicle.Model} à {distance:F1}m");
                            StartControllingMegaShip(vehicle, player);
                            return;
                        }
                    }
                }
                
                // Chercher des props (objets statiques) de méga cargo
                var nearbyProps = World.GetNearbyProps(playerPos, 300f);
                Logger.Info($"Props à proximité: {nearbyProps.Length}");
                
                Prop? targetShip = null;
                float closestDistance = float.MaxValue;
                
                foreach (var prop in nearbyProps)
                {
                    if (prop != null && prop.Exists() && IsMegaCargoShip(prop))
                    {
                        float distance = playerPos.DistanceTo(prop.Position);
                        Logger.Info($"Prop cargo candidat: {prop.Model} à {distance:F1}m");
                        if (distance < closestDistance && distance < 150f)
                        {
                            targetShip = prop;
                            closestDistance = distance;
                        }
                    }
                }

                // Si un prop de cargo a été trouvé, essayer de le "convertir"
                if (targetShip != null)
                {
                    Logger.Info($"Prop cargo trouvé: {targetShip.Model} à {closestDistance:F1}m - Conversion en cours...");
                    ConvertPropToControllableShip(targetShip, player);
                    return;
                }

                // Toujours essayer de créer dans la zone la plus proche
                Vector3 nearestCargoPos = Vector3.Zero;
                float nearestDistance = float.MaxValue;
                
                foreach (var megaPos in _megaCargoPositions)
                {
                    float distance = playerPos.DistanceTo(megaPos);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestCargoPos = megaPos;
                    }
                }

                if (nearestDistance < 500f) // Augmenter la zone
                {
                    Logger.Info($"Zone de cargo trouvée à {nearestDistance:F1}m - Création d'un nouveau cargo...");
                    CreateControllableMegaCargo(nearestCargoPos, player);
                    return;
                }
                
                // En dernier recours, créer à la position du joueur
                Logger.Info("Aucune zone trouvée, création à la position du joueur");
                CreateControllableMegaCargo(playerPos, player);
                
            }
            catch (Exception ex)
            {
                Logger.Error($"Erreur dans TryTakeControlOfMegaShip: {ex.Message}");
                Logger.Error($"StackTrace: {ex.StackTrace}");
                Notification.PostTicker($"~r~Erreur recherche: {ex.Message}", true);
            }
        }

        private void ConvertPropToControllableShip(Prop originalProp, Ped player)
        {
            try
            {
                Vector3 propPosition = originalProp.Position;
                float propHeading = originalProp.Heading;
                string propModel = originalProp.Model.ToString();
                
                Logger.Info($"Conversion du prop {propModel} à la position {propPosition}");
                
                // Marquer le prop original pour suppression (optionnel)
                // originalProp.Delete(); // On peut le laisser pour l'esthétique
                
                // Créer un véhicule pilotable à la même position
                Vector3 spawnPos = AdjustPositionForWater(propPosition);
                
                Notification.PostTicker("~y~🚢 Conversion du cargo en navire pilotable...", true);
                
                // Utiliser la méthode existante pour créer un cargo contrôlable
                CreateControllableMegaCargo(spawnPos, player);
                
                // Si la création réussit, ajuster la position et rotation pour correspondre au prop original
                if (_currentMegaShip != null && _currentMegaShip.Exists())
                {
                    _currentMegaShip.Position = spawnPos;
                    _currentMegaShip.Heading = propHeading;
                    _currentHeading = propHeading;
                    
                    Notification.PostTicker("🚢 CARGO CONVERTI AVEC SUCCÈS! ~n~Vous pouvez maintenant le piloter!", true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Erreur lors de la conversion du prop: {ex.Message}");
                Notification.PostTicker("~r~Erreur lors de la conversion du cargo!", true);
                
                // En cas d'échec, essayer de créer un cargo normal
                CreateControllableMegaCargo(originalProp.Position, player);
            }
        }

        private void StartControllingMegaShip(Entity ship, Ped player)
        {
            _currentMegaShip = ship;
            _isPiloting = true;
            _targetSpeed = 0f;
            _currentHeading = ship.Heading;
            _emergencyStop = false;
            _lastHornTime = DateTime.Now;

            // Calculer les dimensions approximatives
            try
            {
                _shipDimensions = ship.Model.Dimensions.frontTopRight - ship.Model.Dimensions.rearBottomLeft;
            }
            catch
            {
                _shipDimensions = new Vector3(200f, 50f, 30f); // Dimensions par défaut pour un méga cargo
            }

            // Téléporter le joueur sur la passerelle du méga cargo
            Vector3 shipPos = ship.Position;
            Vector3 bridgePosition = new Vector3(shipPos.X, shipPos.Y, shipPos.Z + Math.Max(_shipDimensions.Z, 20f));
            player.Position = bridgePosition;

            Notification.PostTicker("🚢 CONTRÔLE DU MÉGA CARGO ACTIVÉ! ~n~W/S: Moteurs | A/D: Direction | H: Corne | F: Quitter", true);
        }

        private void CreateControllableMegaCargo(Vector3 position, Ped player)
        {
            try
            {
                Logger.Info($"Création d'un méga cargo à la position: {position}");
                
                // Utiliser un modèle simple et fiable d'abord
                Model cargoModel = new Model(VehicleHash.Dinghy);
                Logger.Info("Demande de chargement du modèle Dinghy...");
                
                if (cargoModel.Request(10000))
                {
                    Logger.Info("Modèle Dinghy chargé avec succès");
                    
                    // Ajuster la position pour l'eau
                    position = AdjustPositionForWater(position);
                    Logger.Info($"Position ajustée: {position}");
                    
                    Vehicle megaCargo = World.CreateVehicle(cargoModel, position, 0f);
                    if (megaCargo != null && megaCargo.Exists())
                    {
                        Logger.Info($"Véhicule créé avec succès: {megaCargo.Handle}");
                        
                        // Configuration de base
                        megaCargo.CanEngineDegrade = false;
                        megaCargo.CanWheelsBreak = false;
                        megaCargo.Health = megaCargo.MaxHealth;
                        
                        // Prendre le contrôle
                        _currentMegaShip = megaCargo;
                        _isPiloting = true;
                        _targetSpeed = 0f;
                        _currentHeading = megaCargo.Heading;
                        _emergencyStop = false;
                        _lastHornTime = DateTime.Now;
                        _shipDimensions = new Vector3(200f, 50f, 30f);

                        // Téléporter le joueur sur le navire
                        Vector3 bridgePos = new Vector3(position.X, position.Y, position.Z + 2f);
                        player.Position = bridgePos;
                        
                        Notification.PostTicker("🚢 MÉGA CARGO CRÉÉ! (Version test) ~n~W/S: Moteurs | A/D: Direction | F: Quitter", true);
                        
                        // Créer un blip
                        Blip blip = megaCargo.AddBlip();
                        blip.Sprite = BlipSprite.Boat;
                        blip.Color = BlipColor.Blue;
                        blip.Name = "🚢 MÉGA CARGO TEST";
                        blip.Scale = 1.0f;
                        
                        Logger.Info("Méga cargo créé et configuré avec succès");
                    }
                    else
                    {
                        Logger.Error("Échec de création du véhicule");
                        Notification.PostTicker("~r~Échec de création du véhicule!", true);
                    }
                    
                    cargoModel.MarkAsNoLongerNeeded();
                }
                else
                {
                    Logger.Error("Échec de chargement du modèle Dinghy");
                    Notification.PostTicker("~r~Échec de chargement du modèle!", true);
                    cargoModel.MarkAsNoLongerNeeded();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Erreur critique dans CreateControllableMegaCargo: {ex.Message}");
                Logger.Error($"StackTrace: {ex.StackTrace}");
                Notification.PostTicker($"~r~Erreur: {ex.Message}", true);
            }
        }

        private void ConfigureMegaCargoVehicle(Vehicle vehicle)
        {
            try
            {
                // Configuration du moteur et des systèmes
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle.Handle, true, true, false);
                Function.Call(Hash.SET_VEHICLE_HANDBRAKE, vehicle.Handle, false);
                
                // Résistance maximale
                vehicle.CanEngineDegrade = false;
                vehicle.CanWheelsBreak = false;
                vehicle.CanTiresBurst = false;
                
                // Santé maximale
                vehicle.Health = vehicle.MaxHealth;
                vehicle.BodyHealth = 10000f;
                vehicle.EngineHealth = 10000f;
                
                // Propriétés physiques pour simulation d'un méga cargo
                Function.Call(Hash.SET_VEHICLE_GRAVITY, vehicle.Handle, false);
                Function.Call(Hash.SET_ENTITY_MAX_SPEED, vehicle.Handle, 15f); // Vitesse max réaliste
                
                // Configuration additionnelle pour le cargo
                Function.Call(Hash.SET_VEHICLE_PROVIDES_COVER, vehicle.Handle, true);
                
                Logger.Info("Configuration du méga cargo terminée");
            }
            catch (Exception ex)
            {
                Logger.Error($"Erreur lors de la configuration du véhicule: {ex.Message}");
            }
        }

        private void HandleMegaShipPiloting()
        {
            if (_currentMegaShip == null || !_currentMegaShip.Exists()) return;

            try
            {
                Ped player = Game.Player.Character;
                
                // Maintenir le joueur sur la passerelle
                Vector3 shipPos = _currentMegaShip.Position;
                Vector3 bridgePos = new Vector3(shipPos.X, shipPos.Y, shipPos.Z + Math.Max(_shipDimensions.Z, 20f));
                
                if (player.Position.DistanceTo(bridgePos) > 40f)
                {
                    player.Position = bridgePos;
                }

                // Déplacer le méga cargo graduellement
                if (!_emergencyStop && Math.Abs(_targetSpeed) > 0.1f)
                {
                    // Calculer le vecteur de direction
                    Vector3 forwardVector = new Vector3(
                        (float)Math.Sin(_currentHeading * Math.PI / 180.0),
                        (float)Math.Cos(_currentHeading * Math.PI / 180.0),
                        0f
                    );

                    // Mouvement très lent et réaliste
                    Vector3 movement = forwardVector * _targetSpeed * 0.008f; 
                    Vector3 newPosition = shipPos + movement;
                    
                    // S'assurer que le navire reste dans l'eau
                    float waterLevel = 0f;
                    Function.Call(Hash.GET_WATER_HEIGHT, newPosition.X, newPosition.Y, newPosition.Z, waterLevel);
                    newPosition.Z = waterLevel + 4f;
                    
                    // Déplacer le méga cargo
                    if (_currentMegaShip is Vehicle vehicle)
                    {
                        // Si c'est un véhicule, utiliser les méthodes véhicule
                        Vector3 velocity = forwardVector * _targetSpeed * 0.3f;
                        Function.Call(Hash.SET_ENTITY_VELOCITY, vehicle.Handle, velocity.X, velocity.Y, velocity.Z);
                        Function.Call(Hash.SET_ENTITY_HEADING, vehicle.Handle, _currentHeading);
                    }
                    else
                    {
                        // Si c'est un prop, déplacer directement
                        Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, _currentMegaShip.Handle, 
                            newPosition.X, newPosition.Y, newPosition.Z, false, false, false);
                        Function.Call(Hash.SET_ENTITY_HEADING, _currentMegaShip.Handle, _currentHeading);
                    }
                }

                // Créer des effets visuels de sillage massifs
                CreateMegaWakeEffects();
            }
            catch (Exception ex)
            {
                Logger.Error($"MegaShip piloting error: {ex.Message}");
            }
        }

        private void SoundMegaShipHorn()
        {
            if ((DateTime.Now - _lastHornTime).TotalSeconds < 5) return;
            
            _lastHornTime = DateTime.Now;
            
            if (_currentMegaShip != null)
            {
                // Son de corne de méga cargo - plus puissant et multiple
                Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "AIRHORN", _currentMegaShip.Handle, "DLC_TG_RUNNING_BACK_SOUNDS", false, 0);
                Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "BOAT_HORN", _currentMegaShip.Handle, "DLC_HEIST_FLEECA_SOUNDSET", false, 0);
                
                Notification.PostTicker("📯 CORNE DE MÉGA CARGO! ATTENTION NAVIGATION!", true);
                
                // Effet visuel lumineux sur plusieurs points du navire
                Vector3 basePos = _currentMegaShip.Position;
                for (int i = 0; i < 5; i++)
                {
                    Vector3 lightPos = basePos + new Vector3((i - 2) * 15f, 0f, _shipDimensions.Z + 5f);
                    Function.Call(Hash.DRAW_LIGHT_WITH_RANGE, lightPos.X, lightPos.Y, lightPos.Z, 255, 255, 0, 30f, 1.5f);
                }
            }
        }

        private void CreateMegaWakeEffects()
        {
            if (_currentMegaShip == null || Math.Abs(_targetSpeed) < 1f) return;

            Vector3 shipPos = _currentMegaShip.Position;
            Vector3 forwardVector = new Vector3(
                (float)Math.Sin(_currentHeading * Math.PI / 180.0),
                (float)Math.Cos(_currentHeading * Math.PI / 180.0),
                0f
            );

            // Créer de nombreux effets de sillage pour simuler un énorme navire
            for (int i = 0; i < 10; i++)
            {
                Vector3 wakePos = shipPos - forwardVector * (40f + i * 8f);
                
                // Créer un sillage large sur les côtés
                for (int side = -1; side <= 1; side++)
                {
                    Vector3 sidePos = wakePos;
                    sidePos.X += side * 15f;
                    sidePos.Z = shipPos.Z - 1.5f;
                    
                    Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "core");
                    if (Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, "core"))
                    {
                        Function.Call(Hash.USE_PARTICLE_FX_ASSET, "core");
                        Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD, "water_splash_ped_out",
                            sidePos.X, sidePos.Y, sidePos.Z, 0f, 0f, 0f, 4.0f, false, false, false);
                    }
                }
            }
        }

        private void SpawnMegaCargoShip()
        {
            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists())
                {
                    Notification.PostTicker("~r~Erreur: Joueur non trouvé!", true);
                    return;
                }

                // Trouver une position appropriée dans l'eau
                Vector3 spawnPos = GetBestCargoSpawnPosition(player.Position);
                
                Notification.PostTicker("~y~🚢 Création d'un MÉGA CARGO en cours...", true);
                CreateControllableMegaCargo(spawnPos, player);
            }
            catch (Exception ex)
            {
                Logger.Error($"Erreur lors du spawn de méga cargo: {ex.Message}");
                Notification.PostTicker("~r~Erreur lors de la création du méga cargo!", true);
            }
        }

        private Vector3 GetBestCargoSpawnPosition(Vector3 playerPos)
        {
            // Essayer d'abord les positions prédéfinies
            Vector3 nearestCargoPos = Vector3.Zero;
            float nearestDistance = float.MaxValue;
            
            foreach (var megaPos in _megaCargoPositions)
            {
                float distance = playerPos.DistanceTo(megaPos);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestCargoPos = megaPos;
                }
            }

            // Si on a trouvé une position proche (moins de 500m), l'utiliser
            if (nearestDistance < 500f)
            {
                return AdjustPositionForWater(nearestCargoPos);
            }

            // Sinon, créer à distance du joueur vers l'eau
            Vector3 spawnPos = playerPos + new Vector3(0f, 150f, 0f); // 150m vers le nord
            
            // Essayer de trouver de l'eau dans les environs
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * (float)Math.PI / 180f;
                Vector3 testPos = playerPos + new Vector3(
                    (float)Math.Sin(angle) * 200f,
                    (float)Math.Cos(angle) * 200f,
                    0f
                );
                
                float waterLevel = 0f;
                if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, testPos.X, testPos.Y, testPos.Z, waterLevel))
                {
                    testPos.Z = waterLevel + 4f;
                    return testPos;
                }
            }

            // En dernier recours, utiliser la position par défaut dans le port
            return AdjustPositionForWater(new Vector3(1019.84f, -2998.56f, 5.9f));
        }

        private Vector3 AdjustPositionForWater(Vector3 position)
        {
            try
            {
                float waterLevel = 0f;
                if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, position.X, position.Y, position.Z, waterLevel))
                {
                    position.Z = waterLevel + 4f;
                }
                else
                {
                    // Si pas d'eau détectée, utiliser une hauteur d'eau par défaut
                    position.Z = 0f;
                }
            }
            catch
            {
                position.Z = 0f;
            }
            
            return position;
        }

        private void DisplayMegaShipHUD()
        {
            if (_currentMegaShip == null) return;

            string status = _emergencyStop ? "🚨 ARRÊT D'URGENCE" : "🟢 NAVIGATION";
            
            Screen.ShowSubtitle($"🚢 MÉGA CARGO PILOT | {status} ~n~Vitesse: {_targetSpeed:F0} km/h | Cap: {_currentHeading:F0}° ~n~Longueur estimée: {_shipDimensions.Length():F0}m", 100);
        }

        private void ExitMegaShip()
        {
            if (_currentMegaShip == null) return;

            Ped player = Game.Player.Character;
            
            // Téléporter le joueur sur le quai le plus proche ou dans l'eau
            Vector3 exitPos = _currentMegaShip.Position + new Vector3(60f, 0f, 5f);
            
            // Vérifier s'il y a de la terre ferme nearby
            bool foundGround = false;
            for (float angle = 0; angle < 360; angle += 45)
            {
                Vector3 testPos = _currentMegaShip.Position + new Vector3(
                    (float)Math.Cos(angle * Math.PI / 180) * 80f,
                    (float)Math.Sin(angle * Math.PI / 180) * 80f,
                    10f
                );
                
                if (World.GetGroundHeight(testPos, out float groundZ, GetGroundHeightMode.Normal) && groundZ > -50f) // Si on trouve du sol valide
                {
                    exitPos = new Vector3(testPos.X, testPos.Y, groundZ + 1f);
                    foundGround = true;
                    break;
                }
            }
            
            if (!foundGround)
            {
                // Sinon mettre dans l'eau
                float waterLevel = 0f;
                Function.Call(Hash.GET_WATER_HEIGHT, exitPos.X, exitPos.Y, exitPos.Z, waterLevel);
                exitPos.Z = waterLevel + 0.5f;
            }
            
            player.Position = exitPos;
            
            ExitMegaShipMode();
            
            string exitMessage = foundGround ? "🚁 Vous avez quitté le méga cargo!" : "🏊 Vous avez quitté le méga cargo. Bonne nage!";
            Notification.PostTicker(exitMessage, true);
        }

        private void ExitMegaShipMode()
        {
            _isPiloting = false;
            _currentMegaShip = null;
            _targetSpeed = 0f;
            _emergencyStop = false;
            
            // Nettoyer le véhicule de contrôle s'il existe
            if (_controlVehicle != null && _controlVehicle.Exists())
            {
                _controlVehicle.Delete();
                _controlVehicle = null;
            }
        }

        private void CreateMegaCargoBlips()
        {
            for (int i = 0; i < _megaCargoPositions.Length; i++)
            {
                Blip blip = World.CreateBlip(_megaCargoPositions[i]);
                blip.Sprite = BlipSprite.Boat;
                blip.Color = BlipColor.Yellow;
                blip.Name = $"🚢 Zone Méga Cargo {i + 1}";
                blip.Scale = 0.9f;
                blip.IsShortRange = true;
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            if (_controlVehicle != null && _controlVehicle.Exists())
            {
                _controlVehicle.Delete();
            }
        }
    }
} 