using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.UI;
using GTA.Native;
using REALIS.Common;

namespace REALIS.Missions
{
    /// <summary>
    /// Mission de livraison de drogue avec système de dommages et interface cinématique
    /// </summary>
    public class DrugDeliveryMission : IModule
    {
        private enum MissionState
        {
            Inactive,
            FadeIn,
            GetToVehicle,
            DrivingToDelivery,
            AtDelivery,
            Completed,
            Failed,
            FadeOut
        }

        private MissionState _currentState = MissionState.Inactive;
        private DateTime _stateStartTime;
        private bool _missionActive = false;
        
        // Véhicule et dommages
        private Vehicle _deliveryVehicle;
        private float _initialVehicleHealth;
        private float _maxDamageThreshold = 0.3f; // 30% de dommages max
        
        // Points de livraison
        private readonly List<Vector3> _deliveryPoints = new List<Vector3>
        {
            new Vector3(372.64f, -1834.89f, 28.73f),    // Grove Street
            new Vector3(-1519.31f, -377.25f, 34.21f),   // Vespucci Beach
            new Vector3(1961.21f, 3740.5f, 32.34f),     // Sandy Shores
            new Vector3(-1037.73f, -2738.12f, 20.17f),  // Terminal
            new Vector3(24.44f, -1346.19f, 29.50f)      // Pillbox Hill
        };
        
        private Vector3 _currentDeliveryPoint;
        private Blip _deliveryBlip;
        private Blip _vehicleBlip;
        private Ped _recipientPed;
        private Blip _recipientBlip;
        
        // UI et cinématique
        private bool _showingUI = false;
        private string _currentInstruction = "";
        private int _deliveryCount = 0;
        private int _totalDeliveries = 3;
        private int _cashEarned = 0;
        
        // Timers
        private DateTime _lastUIUpdate = DateTime.Now;
        private DateTime _missionStartTime;
        
        // Indicateur proximité destinataire & props cargaison
        private readonly List<Prop> _cargoProps = new List<Prop>();
        
        public void Initialize()
        {
            // Ne pas démarrer automatiquement - sera déclenché par le contact
            // La mission est initialisée mais reste en attente
        }
        
        /// <summary>
        /// Démarre la mission (appelé par le contact)
        /// </summary>
        public void StartMissionFromContact()
        {
            StartMission();
        }
        
        /// <summary>
        /// Vérifie si la mission est actuellement active
        /// </summary>
        public bool IsMissionActive()
        {
            return _missionActive;
        }
        
        public void Update()
        {
            if (!_missionActive) return;
            
            // Garde-fou : si l'écran est encore fade-out depuis plus de 3 s alors que la mission n'est pas en transition fade,
            // on force un fade-in afin d'éviter un écran noir infini.
            if (Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT) &&
                _currentState != MissionState.FadeIn && _currentState != MissionState.FadeOut)
            {
                if ((DateTime.Now - _stateStartTime).TotalMilliseconds > 3000)
                {
                    Function.Call(Hash.DO_SCREEN_FADE_IN, 500);
                }
            }
            
            UpdateMissionState();
            DrawUI();
            HandleInput();
        }
        
        public void Dispose()
        {
            CleanupMission();
        }
        
        private void StartMission()
        {
            try
            {
                _missionActive = true;
                _currentState = MissionState.FadeIn;
                _stateStartTime = DateTime.Now;
                _missionStartTime = DateTime.Now;
                _deliveryCount = 0;
                _cashEarned = 0;
                
                // Fade out initial
                Function.Call(Hash.DO_SCREEN_FADE_OUT, 1000);
                
                // Créer le véhicule de livraison
                CreateDeliveryVehicle();
                
                // Premier point de livraison
                SetNextDeliveryPoint();
                
                // Notification de début avec effet
                MissionEffects.ShowStylizedNotification("Mission de livraison", "Récupérez le véhicule et livrez la marchandise.", NotificationIcon.Trevor);
            }
            catch (Exception ex)
            {
                Screen.ShowSubtitle("~r~Erreur: Impossible de démarrer la mission: " + ex.Message, 2000);
                FailMission("Erreur technique");
            }
        }
        
        private void CreateDeliveryVehicle()
        {
            try
            {
                // Position de spawn du véhicule (près du joueur mais pas trop)
                var player = Game.Player.Character;
                var spawnPos = player.Position + player.ForwardVector * 10f;
                spawnPos = World.GetNextPositionOnStreet(spawnPos);
                
                uint modelHash = (uint)VehicleHash.Speedo;

                // Demander le modèle de façon non bloquante
                Function.Call(Hash.REQUEST_MODEL, (int)modelHash);

                void trySpawn()
                {
                    if (Function.Call<bool>(Hash.HAS_MODEL_LOADED, (int)modelHash))
                    {
                        var model = new Model((int)modelHash);

                        _deliveryVehicle = World.CreateVehicle(model, spawnPos);
                        _deliveryVehicle.IsPersistent = true;
                        _deliveryVehicle.IsStolen = false;

                        // Enregistrer la santé initiale
                        _initialVehicleHealth = _deliveryVehicle.Health;

                        // Ajouter quelques paquets de drogue dans la soute du fourgon pour l'immersion
                        SpawnCargoInVehicle();

                        // Créer le blip du véhicule
                        _vehicleBlip = _deliveryVehicle.AddBlip();
                        _vehicleBlip.Sprite = BlipSprite.PersonalVehicleCar;
                        _vehicleBlip.Color = BlipColor.Blue;
                        _vehicleBlip.Name = "Véhicule de livraison";

                        // Libérer le modèle
                        Function.Call(Hash.SET_MODEL_AS_NO_LONGER_NEEDED, (int)modelHash);
                    }
                    else
                    {
                        // Réessayer dans 50 ms tant que le modèle n'est pas chargé
                        GameScheduler.Schedule(trySpawn, 50);
                    }
                }

                // Première tentative immédiate (planifiera de nouvelles tentatives si nécessaire)
                trySpawn();
            }
            catch (Exception ex)
            {
                Screen.ShowSubtitle("~r~Erreur: Impossible de créer le véhicule: " + ex.Message, 2000);
            }
        }
        
        private void SetNextDeliveryPoint()
        {
            if (_deliveryCount >= _totalDeliveries)
            {
                CompleteMission();
                return;
            }
            
            // Choisir un point de livraison aléatoire
            var random = new Random();
            _currentDeliveryPoint = _deliveryPoints[random.Next(_deliveryPoints.Count)];
            
            // Créer le blip de livraison
            RemoveBlip(ref _deliveryBlip);
                
            _deliveryBlip = World.CreateBlip(_currentDeliveryPoint);
            _deliveryBlip.Sprite = BlipSprite.Package;
            _deliveryBlip.Color = BlipColor.Yellow;
            _deliveryBlip.Name = $"Point de livraison {_deliveryCount + 1}/{_totalDeliveries}";
            _deliveryBlip.ShowRoute = true;

            // Créer le PNJ destinataire
            SpawnRecipientPed();
        }
        
        private void SpawnRecipientPed()
        {
            // Nettoyer l'ancien destinataire si nécessaire
            if (_recipientPed != null && _recipientPed.Exists())
            {
                _recipientPed.IsPersistent = false;
                _recipientPed.MarkAsNoLongerNeeded();
                _recipientPed = null;
            }

            uint pedHash = (uint)PedHash.Business01AMY;
            Function.Call(Hash.REQUEST_MODEL, (int)pedHash);

            void trySpawn()
            {
                if (Function.Call<bool>(Hash.HAS_MODEL_LOADED, (int)pedHash))
                {
                    var model = new Model((int)pedHash);
                    // Chercher en priorité un trottoir à proximité pour éviter de placer le PNJ au milieu de la route
                    var spawnPos = World.GetNextPositionOnSidewalk(_currentDeliveryPoint);

                    // Si aucune position de trottoir n'a été trouvée (peut arriver dans certaines zones),
                    // on se rabat sur la chaussée puis, en dernier recours, sur la position d'origine.
                    if (spawnPos == Vector3.Zero)
                        spawnPos = World.GetNextPositionOnStreet(_currentDeliveryPoint);
                    if (spawnPos == Vector3.Zero)
                        spawnPos = _currentDeliveryPoint;

                    // Récupère la hauteur du sol pour positionner correctement le PNJ
                    spawnPos.Z = World.GetGroundHeight(spawnPos);

                    _recipientPed = World.CreatePed(model, spawnPos);
                    _recipientPed.IsPersistent = true;
                    _recipientPed.BlockPermanentEvents = true;

                    // Faire face au point de livraison
                    _recipientPed.Heading = Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, 0f, 360f);

                    // Petite animation d'attente
                    Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, _recipientPed, "WORLD_HUMAN_STAND_IMPATIENT", 0, true);

                    // Blip sur le destinataire
                    _recipientBlip = _recipientPed.AddBlip();
                    _recipientBlip.Sprite = BlipSprite.Standard;
                    _recipientBlip.Color = BlipColor.Green;
                    _recipientBlip.IsShortRange = true;

                    Function.Call(Hash.SET_MODEL_AS_NO_LONGER_NEEDED, (int)pedHash);
                }
                else
                {
                    GameScheduler.Schedule(trySpawn, 50);
                }
            }

            trySpawn();
        }
        
        private void UpdateMissionState()
        {
            var timeSinceStateStart = DateTime.Now - _stateStartTime;
            var player = Game.Player.Character;
            
            switch (_currentState)
            {
                case MissionState.FadeIn:
                    if (timeSinceStateStart.TotalMilliseconds > 2000)
                    {
                        Function.Call(Hash.DO_SCREEN_FADE_IN, 1000);
                        ChangeState(MissionState.GetToVehicle);
                        _currentInstruction = "Montez dans le véhicule de livraison";
                    }
                    break;
                    
                case MissionState.GetToVehicle:
                    if (_deliveryVehicle != null && _deliveryVehicle.Exists())
                    {
                        if (player.IsInVehicle(_deliveryVehicle))
                        {
                            ChangeState(MissionState.DrivingToDelivery);
                            _currentInstruction = $"Livrez la marchandise ({_deliveryCount + 1}/{_totalDeliveries})";
                            
                            // Supprimer le blip du véhicule
                            RemoveBlip(ref _vehicleBlip);
                        }
                    }
                    else
                    {
                        FailMission("Le véhicule de livraison a été détruit");
                    }
                    break;
                    
                case MissionState.DrivingToDelivery:
                    // Vérifier les dommages du véhicule
                    CheckVehicleDamage();
                    
                    if (_deliveryVehicle != null && _deliveryVehicle.Exists())
                    {
                        var distanceToPoint = Vector3.Distance(player.Position, _currentDeliveryPoint);

                        // Si toujours en véhicule mais proche du point : demander de se garer
                        if (distanceToPoint < 10f && player.IsInVehicle(_deliveryVehicle))
                        {
                            _currentInstruction = "Garez-vous et sortez pour remettre la marchandise";
                        }

                        // Si le joueur est à pied près du destinataire
                        if (!player.IsInVehicle() && _recipientPed != null && _recipientPed.Exists())
                        {
                            var distToPed = Vector3.Distance(player.Position, _recipientPed.Position);
                            if (distToPed < 3f)
                            {
                                ChangeState(MissionState.AtDelivery);
                                _currentInstruction = "Appuyez sur E pour remettre la marchandise";
                            }
                        }
                    }
                    else
                    {
                        FailMission("Le véhicule de livraison a été détruit");
                    }
                    break;
                    
                case MissionState.AtDelivery:
                    var distance = Vector3.Distance(player.Position, _currentDeliveryPoint);
                    if (distance > 10.0f)
                    {
                        ChangeState(MissionState.DrivingToDelivery);
                        _currentInstruction = $"Retournez au point de livraison ({_deliveryCount + 1}/{_totalDeliveries})";
                    }
                    break;
                    
                case MissionState.Failed:
                    if (timeSinceStateStart.TotalMilliseconds > 3000)
                    {
                        ChangeState(MissionState.FadeOut);
                    }
                    break;
                    
                case MissionState.Completed:
                    if (timeSinceStateStart.TotalMilliseconds > 5000)
                    {
                        ChangeState(MissionState.FadeOut);
                    }
                    break;
                    
                case MissionState.FadeOut:
                    Function.Call(Hash.DO_SCREEN_FADE_OUT, 2000);
                    if (timeSinceStateStart.TotalMilliseconds > 3000)
                    {
                        CleanupMission();
                    }
                    break;
            }
        }
        
        private void CheckVehicleDamage()
        {
            if (_deliveryVehicle == null || !_deliveryVehicle.Exists()) return;
            
            var currentHealth = _deliveryVehicle.Health;
            var healthPercentage = currentHealth / _initialVehicleHealth;
            var damagePercentage = 1.0f - healthPercentage;
            
            if (damagePercentage > _maxDamageThreshold)
            {
                FailMission("Le véhicule est trop endommagé pour continuer la livraison");
            }
        }
        
        private void HandleInput()
        {
            if (_currentState == MissionState.AtDelivery)
            {
                if (Game.IsControlJustPressed(GTA.Control.Context)) // Touche E
                {
                    DeliverPackage();
                }
            }
        }
        
        private void DeliverPackage()
        {
            // Charger l'anim si nécessaire puis jouer
            const string animDict = "mp_common";
            Function.Call(Hash.REQUEST_ANIM_DICT, animDict);
            if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animDict))
            {
                // Essayer pendant 500 ms max (non bloquant via GameScheduler)
                uint start = (uint)Game.GameTime;
                void tryAnim()
                {
                    if (Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animDict))
                    {
                        _recipientPed?.Task.PlayAnimation(animDict, "givetake1_a", 8f, -8f, -1, AnimationFlags.None, 0f);
                    }
                    else if (Game.GameTime - start < 500)
                    {
                        GameScheduler.Schedule(tryAnim, 50);
                    }
                }
                tryAnim();
            }
            else
            {
                _recipientPed.Task.PlayAnimation(animDict, "givetake1_a", 8f, -8f, -1, AnimationFlags.None, 0f);
            }

            // Retirer une boîte de drogue du véhicule
            RemoveOneCargoPackage();

            _deliveryCount++;
            _cashEarned += 500; // 500$ par livraison
            
            // Effets visuels et sonores améliorés
            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "PURCHASE", "HUD_LIQUOR_STORE_SOUNDSET", 0);
            MissionEffects.DoColorFlash(0, 255, 0, 100, 500); // Flash vert
            
            // Petit message en haut (notification) + on laisse le BigMessage pour la fin de mission
            MissionEffects.ShowStylizedNotification("Livraison effectuée!", $"Vous avez gagné 500$. Total: {_cashEarned}$", NotificationIcon.Trevor);
            
            // Supprimer le blip actuel
            RemoveBlip(ref _deliveryBlip);
            
            // Le destinataire peut vaquer à ses occupations
            if (_recipientPed != null && _recipientPed.Exists())
            {
                _recipientPed.Task.ReactAndFlee(Game.Player.Character);
                _recipientPed.IsPersistent = false;
                _recipientPed.MarkAsNoLongerNeeded();
                _recipientPed = null;
            }
            RemoveBlip(ref _recipientBlip);
            
            // Passer à la livraison suivante ou terminer
            if (_deliveryCount >= _totalDeliveries)
            {
                CompleteMission();
            }
            else
            {
                SetNextDeliveryPoint();
                ChangeState(MissionState.DrivingToDelivery);
                _currentInstruction = $"Livrez la marchandise ({_deliveryCount + 1}/{_totalDeliveries})";
            }
        }
        
        private void CompleteMission()
        {
            ChangeState(MissionState.Completed);
            
            // Bonus de fin de mission
            _cashEarned += 1000;
            
            // Écran « Mission Passed » GTA-like
            BigMessage.ShowMissionPassed("Mission Terminée!", $"+{_cashEarned}$");
            
            // Effet de particules au point de livraison final
            MissionEffects.CreateParticleEffect(_currentDeliveryPoint, "exp_grd_bzgas_smoke");
            
            _currentInstruction = "Mission terminée avec succès!";
        }
        
        private void FailMission(string reason)
        {
            ChangeState(MissionState.Failed);
            
            // Écran « Mission Failed » (shard rouge)
            BigMessage.ShowMissionFailed("Mission Échouée", reason);
            
            _currentInstruction = $"Mission échouée: {reason}";
        }
        
        private void ChangeState(MissionState newState)
        {
            _currentState = newState;
            _stateStartTime = DateTime.Now;
        }
        
        /// <summary>
        /// Désactive la route si nécessaire puis supprime le blip proprement
        /// </summary>
        private void RemoveBlip(ref Blip blip)
        {
            if (blip != null && blip.Exists())
            {
                // Désactiver la route GPS éventuellement encore affichée
                Function.Call(Hash.SET_BLIP_ROUTE, blip.Handle, false);
                blip.Delete();
                blip = null;
            }
        }
        
        private void DrawUI()
        {
            if (!_showingUI && _currentState != MissionState.FadeIn && _currentState != MissionState.FadeOut)
                _showingUI = true;
            
            if (!_showingUI) return;
            
            // ------------------------------------------------------------
            //  Mise en page GTA-Like (zone inférieure gauche de l'écran)
            // ------------------------------------------------------------

            // Coordonnées normalisées (0.0 – 1.0)
            const float leftMargin = 0.025f;        // marge gauche ~2.5 %
            const float bottomMargin = 0.10f;       // marge bas 10 %
            const float barWidth = 0.25f;           // 25 % de la largeur
            const float barHeight = 0.02f;          // 2 % de la hauteur

            float barCenterX = leftMargin + barWidth / 2f;
            float barCenterY = 1.0f - bottomMargin; // depuis le bas de l'écran

            // Fond de la barre
            Function.Call(Hash.DRAW_RECT, barCenterX, barCenterY, barWidth, barHeight, 0, 0, 0, 180);

            // Barre de progression (vert de gauche à droite)
            float progress = _deliveryCount / (float)_totalDeliveries;
            float filledWidth = barWidth * progress;
            Function.Call(Hash.DRAW_RECT, barCenterX - (barWidth - filledWidth) / 2f, barCenterY, filledWidth, barHeight, 0, 255, 0, 200);

            // Texte « Livraisons » juste au-dessus de la barre
            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_SCALE, 0.45f, 0.45f);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"Livraisons : {_deliveryCount}/{_totalDeliveries}");
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, barCenterX - barWidth / 2f, barCenterY - 0.025f);

            // Affichage de l'argent gagné (couleur verte GTA) au-dessus, à droite de la barre
            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_SCALE, 0.5f, 0.5f);
            Function.Call(Hash.SET_TEXT_COLOUR, 0, 255, 0, 255);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"+$ {_cashEarned}");
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, barCenterX + barWidth / 2f - 0.05f, barCenterY - 0.025f);
            
            // Rendu du big message (mission pass/fail) si actif
            BigMessage.Render();
            
            // Instruction actuelle
            if (!string.IsNullOrEmpty(_currentInstruction))
            {
                Function.Call(Hash.SET_TEXT_FONT, 4);
                Function.Call(Hash.SET_TEXT_SCALE, 0.7f, 0.7f);
                Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
                Function.Call(Hash.SET_TEXT_OUTLINE);
                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, _currentInstruction);
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.5f, 0.85f);
            }

            // Affichage 3D au-dessus du destinataire lors de la remise
            if (_currentState == MissionState.AtDelivery && _recipientPed != null && _recipientPed.Exists())
            {
                var screenPos = new OutputArgument();
                if (Function.Call<bool>(Hash.GET_SCREEN_COORD_FROM_WORLD_COORD,
                        _recipientPed.Position.X, _recipientPed.Position.Y, _recipientPed.Position.Z + 1.0f,
                        screenPos, screenPos))
                {
                    // Nom du destinataire / rôle
                    Function.Call(Hash.SET_TEXT_FONT, 4);
                    Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
                    Function.Call(Hash.SET_TEXT_SCALE, 0.5f, 0.5f);
                    Function.Call(Hash.SET_TEXT_OUTLINE);
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "Destinataire");
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, screenPos.GetResult<float>(), screenPos.GetResult<float>() - 0.05f);

                    // Indicateur d'interaction
                    Function.Call(Hash.SET_TEXT_FONT, 4);
                    Function.Call(Hash.SET_TEXT_COLOUR, 0, 255, 0, 255);
                    Function.Call(Hash.SET_TEXT_SCALE, 0.4f, 0.4f);
                    Function.Call(Hash.SET_TEXT_OUTLINE);
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "[E] Remettre");
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, screenPos.GetResult<float>(), screenPos.GetResult<float>() + 0.02f);
                }
            }
        }
        
        private void CleanupMission()
        {
            try
            {
                _missionActive = false;
                _showingUI = false;
                
                // Nettoyer les blips
                RemoveBlip(ref _deliveryBlip);
                RemoveBlip(ref _vehicleBlip);
                
                // Nettoyer le véhicule
                if (_deliveryVehicle != null && _deliveryVehicle.Exists())
                {
                    _deliveryVehicle.IsPersistent = false;
                    _deliveryVehicle.MarkAsNoLongerNeeded();
                }
                
                // Fade in final
                Function.Call(Hash.DO_SCREEN_FADE_IN, 1000);
                
                // Nettoyer le PNJ destinataire restant
                RemoveBlip(ref _recipientBlip);
                
                // Nettoyer les props de cargaison
                foreach (var p in _cargoProps)
                {
                    if (p != null && p.Exists())
                    {
                        p.IsPersistent = false;
                        p.MarkAsNoLongerNeeded();
                    }
                }
                _cargoProps.Clear();
                
                // Reset des variables
                _currentState = MissionState.Inactive;
                _deliveryCount = 0;
                _cashEarned = 0;
                _currentInstruction = "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors du nettoyage: {ex.Message}");
            }
        }

        /// <summary>
        /// Fait apparaître et attache des paquets de drogue à l'intérieur du véhicule de livraison.
        /// Les objets sont gelés pour éviter qu'ils ne bougent pendant le trajet.
        /// </summary>
        private void SpawnCargoInVehicle()
        {
            if (_deliveryVehicle == null || !_deliveryVehicle.Exists()) return;

            // Petite sélection de modèles de paquets / sacs
            string[] models =
            {
                "prop_mp_drug_package",
                "prop_mp_drug_pack_blue",
                "prop_mp_drug_pack_red",
                "prop_meth_bag_01"
            };

            // Offsets (en mètre) pour placer les paquets approximativement dans la partie cargo
            Vector3[] offsets =
            {
                new Vector3(0.0f, -1.0f, 0.3f),
                new Vector3(-0.3f, -1.2f, 0.3f),
                new Vector3(0.3f, -1.2f, 0.3f),
                new Vector3(0.0f, -0.8f, 0.35f)
            };

            // On ne fait apparaître que le nombre de paquets correspondant au nombre de livraisons
            for (int i = 0; i < _totalDeliveries; i++)
            {
                string modelName = models[i % models.Length];
                int hash = Function.Call<int>(Hash.GET_HASH_KEY, modelName);
                Function.Call(Hash.REQUEST_MODEL, hash);

                void tryCreate()
                {
                    if (Function.Call<bool>(Hash.HAS_MODEL_LOADED, hash))
                    {
                        var propModel = new Model(hash);
                        var prop = World.CreateProp(propModel, _deliveryVehicle.Position, false, false);
                        if (prop != null && prop.Exists())
                        {
                            prop.IsPersistent = true;
                            Function.Call(Hash.FREEZE_ENTITY_POSITION, prop.Handle, true);
                            prop.AttachTo(_deliveryVehicle, offsets[i % offsets.Length], Vector3.Zero);
                            _cargoProps.Add(prop);
                        }

                        Function.Call(Hash.SET_MODEL_AS_NO_LONGER_NEEDED, hash);
                    }
                    else
                    {
                        GameScheduler.Schedule(tryCreate, 50);
                    }
                }

                tryCreate();
            }
        }

        /// <summary>
        /// Supprime visuellement une boîte de drogue du chargement (appelé après chaque livraison).
        /// </summary>
        private void RemoveOneCargoPackage()
        {
            if (_cargoProps.Count == 0) return;

            var prop = _cargoProps[0];
            _cargoProps.RemoveAt(0);

            if (prop != null && prop.Exists())
            {
                prop.Detach();
                prop.IsPersistent = false;
                prop.Delete();
            }
        }
    }
}