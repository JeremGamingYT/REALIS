using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using LemonUI;
using LemonUI.Menus;
using REALIS.Core;

namespace REALIS.Core
{
    /// <summary>
    /// Système de dispatch de police avec réponses interactives et audio
    /// </summary>
    public class PoliceDispatchSystem : Script
    {
        // Référence au système de job de police
        private PoliceJobSystem? _policeJobSystem;
        
        // Interface utilisateur
        private ObjectPool _menuPool = null!;
        private NativeMenu _dispatchMenu = null!;
        
        // État du système
        private bool _isDispatchMenuOpen = false;
        private bool _isOnDuty = false;
        
        // Messages de dispatch simulés
        private readonly List<DispatchMessage> _dispatchMessages = new List<DispatchMessage>
        {
            new DispatchMessage("10-54", "Véhicule en panne sur l'autoroute", new Vector3(100.0f, 200.0f, 30.0f)),
            new DispatchMessage("10-16", "Arrestation domestique en cours", new Vector3(150.0f, 250.0f, 25.0f)),
            new DispatchMessage("10-80", "Poursuite de véhicule en cours", new Vector3(200.0f, 300.0f, 35.0f)),
            new DispatchMessage("10-71", "Tir d'arme à feu signalé", new Vector3(300.0f, 400.0f, 40.0f)),
            new DispatchMessage("10-90", "Alarme déclenchée - banque", new Vector3(250.0f, 350.0f, 30.0f))
        };
        
        // Réponses disponibles
        private readonly Dictionary<string, DispatchResponse> _dispatchResponses = new Dictionary<string, DispatchResponse>
        {
            { "J'arrive", new DispatchResponse("10-4, en route", "DISPATCH_RESPONDING") },
            { "Pas disponible", new DispatchResponse("10-7, indisponible", "DISPATCH_UNAVAILABLE") },
            { "Demande renforts", new DispatchResponse("10-78, besoin d'assistance", "DISPATCH_BACKUP") },
            { "Code 4", new DispatchResponse("10-4, situation maîtrisée", "DISPATCH_SITUATION_HANDLED") },
            { "Envoyer une autre unité", new DispatchResponse("10-22, envoyez autre unité", "DISPATCH_SEND_OTHER") },
            { "En patrouille", new DispatchResponse("10-8, en service", "DISPATCH_IN_SERVICE") }
        };
        
        // Message de dispatch actuel
        private DispatchMessage? _currentDispatchMessage;
        private Random _random = new Random();
        
        // Timer pour les messages automatiques
        private DateTime _lastDispatchMessageTime = DateTime.Now;
        private readonly TimeSpan _dispatchMessageInterval = TimeSpan.FromMinutes(2); // Message toutes les 2 minutes

        public PoliceDispatchSystem()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            
            InitializeSystem();
            CreateDispatchMenu();
            
            // Message de confirmation
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"~g~[RADIO POLICE]~w~\nSystème dispatch initialisé!\n~b~Touche B~w~ pour ouvrir le menu\n~y~{_dispatchResponses.Count} réponses disponibles~w~");
            Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
            
            Logger.Info("Police Dispatch System initialized with B key.");
        }
        
        public void SetPoliceJobSystem(PoliceJobSystem policeJobSystem)
        {
            _policeJobSystem = policeJobSystem;
        }
        
        public void SetOnDutyStatus(bool onDuty)
        {
            _isOnDuty = onDuty;
            
            // Log pour debug
            Logger.Info($"Police Dispatch - SetOnDutyStatus called with: {onDuty}");
            
            if (onDuty)
            {
                ShowWelcomeMessage();
                _lastDispatchMessageTime = DateTime.Now;
                
                // Message de confirmation immédiat en mode debug
                if (ConfigurationManager.UserConfig.DebugMode)
                {
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "~g~[DEBUG DISPATCH]~w~\nStatut EN SERVICE confirmé!\nLa radio dispatch est maintenant active");
                    Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                }
            }
            else
            {
                CloseDispatchMenu();
                
                if (ConfigurationManager.UserConfig.DebugMode)
                {
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "~r~[DEBUG DISPATCH]~w~\nStatut HORS SERVICE confirmé!\nLa radio dispatch est désactivée");
                    Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                }
            }
        }
        
        /// <summary>
        /// Méthode publique pour diagnostiquer l'état du dispatch
        /// </summary>
        public void DiagnoseDispatchStatus()
        {
            string status = $"~y~[DIAGNOSTIC DISPATCH]~w~\n" +
                           $"En service: {_isOnDuty}\n" +
                           $"Menu ouvert: {_isDispatchMenuOpen}\n" +
                           $"Dernière activation: {_lastDispatchMessageTime:HH:mm:ss}\n" +
                           $"Messages disponibles: {_dispatchMessages.Count}";
            
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, status);
            Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
        }

        private void InitializeSystem()
        {
            _menuPool = new ObjectPool();
            
            // Initialiser les réponses dispatch par défaut
            InitializeDispatchData();
        }
        
        private void InitializeDispatchData()
        {
            // Ajouter des réponses de dispatch par défaut
            _dispatchResponses.Add("10-4", new DispatchResponse("10-4, bien reçu", "RADIO_ACK"));
            _dispatchResponses.Add("En route", new DispatchResponse("En route vers la position", "RADIO_ACK"));
            _dispatchResponses.Add("Sur place", new DispatchResponse("Arrivé sur les lieux", "RADIO_ACK"));
            _dispatchResponses.Add("Situation sous contrôle", new DispatchResponse("Situation maîtrisée", "RADIO_ACK"));
            _dispatchResponses.Add("Besoin de renforts", new DispatchResponse("Demande de renforts", "RADIO_CHATTER"));
            _dispatchResponses.Add("Poursuite en cours", new DispatchResponse("Poursuite en cours", "RADIO_CHATTER"));
            _dispatchResponses.Add("Suspect appréhendé", new DispatchResponse("Suspect en garde à vue", "RADIO_ACK"));
            _dispatchResponses.Add("Mission terminée", new DispatchResponse("Mission accomplie", "RADIO_ACK"));
            
            // Ajouter des messages de dispatch par défaut
            _dispatchMessages.Add(new DispatchMessage("10-50", "Accident de la circulation signalé", new Vector3(-554.6f, -183.0f, 38.2f)));
            _dispatchMessages.Add(new DispatchMessage("10-54", "Véhicule en panne sur l'autoroute", new Vector3(-812.1f, -1236.8f, 7.3f)));
            _dispatchMessages.Add(new DispatchMessage("10-31", "Crime en cours", new Vector3(147.9f, -1035.8f, 29.3f)));
            _dispatchMessages.Add(new DispatchMessage("10-90", "Alarme déclenchée", new Vector3(-47.2f, -1757.5f, 29.4f)));
            _dispatchMessages.Add(new DispatchMessage("10-15", "Personne suspecte", new Vector3(425.1f, -979.5f, 30.7f)));
            _dispatchMessages.Add(new DispatchMessage("10-32", "Homme armé", new Vector3(-618.4f, -230.8f, 38.1f)));
        }

        private void CreateDispatchMenu()
        {
            try
            {
                Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"~b~[DEBUG]~w~ Création menu dispatch...\nRéponses: {_dispatchResponses.Count}");
                Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                
                _dispatchMenu = new NativeMenu("📻 Radio Police", "Système de communication radio");
                _menuPool.Add(_dispatchMenu);
                
                Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "~b~[DEBUG]~w~ Menu de base créé, ajout des items...");
                Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                
                // Créer les éléments de menu pour chaque réponse
                foreach (var response in _dispatchResponses)
                {
                    var item = new NativeItem($"📡 {response.Key}", response.Value.Message);
                    _dispatchMenu.Add(item);
                }
                
                // Ajouter option pour fermer
                var closeItem = new NativeItem("❌ Fermer", "Fermer le dispatch");
                _dispatchMenu.Add(closeItem);
                
                // Événements
                _dispatchMenu.ItemActivated += OnDispatchMenuItemActivated;
                _dispatchMenu.Closed += OnDispatchMenuClosed;
                
                Logger.Info("Dispatch menu created successfully with " + _dispatchResponses.Count + " responses.");
                
                Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"~g~[DEBUG]~w~ Menu créé avec succès!\nItems total: {_dispatchMenu.Items.Count}");
                Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
            }
            catch (Exception ex)
            {
                Logger.Error("Erreur création menu dispatch: " + ex.Message);
                
                Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"~r~[ERREUR]~w~ Impossible de créer le menu dispatch!\n{ex.Message}");
                Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            // Traiter les menus TOUJOURS (même si pas en service)
            _menuPool.Process();
            
            // Le reste ne fonctionne que si on est en service
            if (!_isOnDuty) return;
            
            // Envoyer des messages de dispatch automatiquement
            CheckForAutomaticDispatchMessages();
            
            // Debug: Afficher l'état du système périodiquement (chaque 5 secondes)
            if (ConfigurationManager.UserConfig.DebugMode)
            {
                DisplayDebugInfo();
            }
        }

        private void CheckForAutomaticDispatchMessages()
        {
            if (DateTime.Now - _lastDispatchMessageTime >= _dispatchMessageInterval)
            {
                SendRandomDispatchMessage();
                _lastDispatchMessageTime = DateTime.Now;
            }
        }

        private void SendRandomDispatchMessage()
        {
            if (_dispatchMessages.Count > 0)
            {
                _currentDispatchMessage = _dispatchMessages[_random.Next(_dispatchMessages.Count)];
                ShowDispatchNotification();
                PlayDispatchAudio("RADIO_CHATTER");
            }
        }

        private void ShowDispatchNotification()
        {
            if (_currentDispatchMessage == null) return;
            
            string notificationText = $"~r~[DISPATCH]~w~\n~b~{_currentDispatchMessage.Code}~w~\n{_currentDispatchMessage.Description}\n~g~Appuyez sur N pour répondre";
            
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, notificationText);
            Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
            
            // Créer un blip temporaire pour la localisation
            if (_currentDispatchMessage.Location != Vector3.Zero)
            {
                CreateTemporaryDispatchBlip(_currentDispatchMessage.Location);
            }
        }

        private void CreateTemporaryDispatchBlip(Vector3 location)
        {
            var blip = World.CreateBlip(location);
            blip.Sprite = BlipSprite.PoliceStation;
            blip.Color = BlipColor.Red;
            blip.Name = "Appel d'urgence";
            blip.IsFlashing = true;
            
            // Supprimer le blip après 30 secondes
            Script.Wait(30000);
            if (blip.Exists()) blip.Delete();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // TOUCHE B = OUVRIR LE MENU DISPATCH (SIMPLE ET DIRECT)
            if (e.KeyCode == Keys.B)
            {
                // Debug immédiat
                Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "~y~[DEBUG]~w~ Touche B pressée!");
                Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                
                // FORCER le menu à s'ouvrir même si pas en service
                if (!_isDispatchMenuOpen)
                {
                    // Créer le menu s'il n'existe pas
                    if (_dispatchMenu == null)
                    {
                        CreateDispatchMenu();
                    }
                    
                    if (_dispatchMenu != null)
                    {
                        _dispatchMenu.Visible = true;
                        _isDispatchMenuOpen = true;
                        
                        Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"~g~[RADIO]~w~ Menu dispatch ouvert!\nItems: {_dispatchMenu.Items.Count}\nVisible: {_dispatchMenu.Visible}");
                        Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                        
                        PlayDispatchAudio("RADIO_ON");
                    }
                    else
                    {
                        Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "~r~[ERREUR]~w~ Menu dispatch est null après création!");
                        Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                    }
                }
                else
                {
                    CloseDispatchMenu();
                }
                return;
            }
            
            // Commande de test - Touche L pour forcer l'activation du dispatch (debug)
            if (e.KeyCode == Keys.L && ConfigurationManager.UserConfig.DebugMode)
            {
                _isOnDuty = true; // Forcer le mode en service
                Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "~g~[DEBUG]~w~ Dispatch forcé en service\nAppuyez sur B pour tester la radio");
                Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                
                // Envoyer un message de test immédiatement
                SendRandomDispatchMessage();
                return;
            }
            
            // Debug complet - Touche K pour afficher l'état détaillé
            if (e.KeyCode == Keys.K && ConfigurationManager.UserConfig.DebugMode)
            {
                string debugInfo = $"~y~[DEBUG DISPATCH COMPLET]~w~\n" +
                                  $"En service: {_isOnDuty}\n" +
                                  $"Menu ouvert: {_isDispatchMenuOpen}\n" +
                                  $"Touche config: {ConfigurationManager.KeybindConfig?.PoliceDispatchKey?.Key.ToString() ?? "Aucune"}\n" +
                                  $"Messages dispo: {_dispatchMessages.Count}\n" +
                                  $"Réponses dispo: {_dispatchResponses.Count}";
                
                Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, debugInfo);
                Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                return;
            }
            
            if (ConfigurationManager.KeybindConfig?.PoliceDispatchKey != null && e.KeyCode == ConfigurationManager.KeybindConfig.PoliceDispatchKey.Key)
            {
                // Message de debug pour voir qu'on passe ici
                if (ConfigurationManager.UserConfig.DebugMode)
                {
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"~y~[DEBUG]~w~ Touche {ConfigurationManager.KeybindConfig?.PoliceDispatchKey?.Key.ToString() ?? "Aucune"} pressée\nStatut en service: {_isOnDuty}");
                    Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                }
                
                // Vérifier d'abord si on est en service
                if (!_isOnDuty) 
                {
                    // Afficher un message informatif plutôt que de rester silencieux
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "~r~[RADIO POLICE]~w~\nVous devez être en service de police\npour utiliser la radio dispatch\n~y~(L: Force test | K: Debug info)");
                    Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                    return;
                }
                
                // Vérifier si on est dans un véhicule TeslaX (pour éviter le conflit si la touche est N)
                if (ConfigurationManager.KeybindConfig?.PoliceDispatchKey?.Key == Keys.N && IsInTeslaxVehicle())
                {
                    // Si la touche est N et qu'on est dans un TeslaX, afficher un message informatif
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "~y~[DISPATCH]~w~\nTouche N utilisée par TeslaX\nChangez la touche dispatch vers B dans les paramètres");
                    Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                    return; // Laisser le système TeslaX gérer la touche N
                }
                
                // Si on arrive ici, on peut utiliser le dispatch
                if (!_isDispatchMenuOpen)
                {
                    OpenDispatchMenu();
                }
                else
                {
                    CloseDispatchMenu();
                }
            }
        }
        
        private bool IsInTeslaxVehicle()
        {
            var player = Game.Player.Character;
            
            if (!player.IsInVehicle())
            {
                return false;
            }
            
            var vehicle = player.CurrentVehicle;
            if (vehicle == null) return false;
            
            try
            {
                // Même logique que dans TestlaxController
                string modelName = vehicle.Model.ToString().ToLower();
                string displayName = vehicle.DisplayName.ToLower();
                
                return modelName == "teslax" || 
                       modelName.Contains("teslax") || 
                       displayName.Contains("teslax");
            }
            catch
            {
                return false;
            }
        }

        private void OpenDispatchMenu()
        {
            if (_dispatchMenu == null)
            {
                CreateDispatchMenu();
            }
            
            if (_dispatchMenu != null)
            {
                if (_currentDispatchMessage != null)
                {
                    _dispatchMenu.Name = _currentDispatchMessage.Code + " - " + _currentDispatchMessage.Description;
                }
                else
                {
                    _dispatchMenu.Name = "Dispatch Radio";
                }
                
                _dispatchMenu.Visible = true;
                _isDispatchMenuOpen = true;
                PlayDispatchAudio("RADIO_ON");
            }
        }

        private void CloseDispatchMenu()
        {
            if (_dispatchMenu != null && _dispatchMenu.Visible)
            {
                _dispatchMenu.Visible = false;
                _isDispatchMenuOpen = false;
                PlayDispatchAudio("RADIO_OFF");
            }
        }

        private void OnDispatchMenuItemActivated(object sender, ItemActivatedArgs e)
        {
            var selectedItem = e.Item.Title.Replace("📡 ", "").Replace("❌ ", "");
            
            if (selectedItem == "Fermer")
            {
                CloseDispatchMenu();
                return;
            }
            
            if (_dispatchResponses.ContainsKey(selectedItem))
            {
                var response = _dispatchResponses[selectedItem];
                SendDispatchResponse(response);
                
                // Afficher la confirmation
                ShowResponseConfirmation(response.Message);
                
                // Fermer le menu après la réponse
                CloseDispatchMenu();
            }
        }

        private void OnDispatchMenuClosed(object sender, EventArgs e)
        {
            _isDispatchMenuOpen = false;
        }

        private void SendDispatchResponse(DispatchResponse response)
        {
            // Jouer l'audio de réponse
            PlayDispatchAudio(response.AudioCue);
            
            // Afficher la réponse dans le chat/notification
            string responseText = $"~g~[VOUS]~w~ {response.Message}";
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, responseText);
            Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
            
            // Simuler une réponse du dispatch
            Script.Wait(2000);
            SimulateDispatchAck();
        }

        private void SimulateDispatchAck()
        {
            string[] ackMessages = {
                "~b~[DISPATCH]~w~ 10-4, reçu",
                "~b~[DISPATCH]~w~ Message bien reçu",
                "~b~[DISPATCH]~w~ Compris, terminé",
                "~b~[DISPATCH]~w~ Roger that"
            };
            
            string ackText = ackMessages[_random.Next(ackMessages.Length)];
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, ackText);
            Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
            
            PlayDispatchAudio("RADIO_ACK");
        }

        private void ShowResponseConfirmation(string message)
        {
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"~g~Réponse envoyée:~w~ {message}");
            Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
        }

        private void PlayDispatchAudio(string audioCue)
        {
            try
            {
                // Utiliser les sons de radio du jeu
                switch (audioCue)
                {
                    case "RADIO_ON":
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "RADIO_ON", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
                        break;
                    case "RADIO_OFF":
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "RADIO_OFF", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
                        break;
                    case "RADIO_CHATTER":
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "CHALLENGE_UNLOCKED", "HUD_AWARDS", false);
                        break;
                    case "RADIO_ACK":
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "BEEP", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
                        break;
                    case "DISPATCH_RESPONDING":
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "CONFIRM_BEEP", "HUD_MINI_GAME_SOUNDSET", false);
                        break;
                    case "DISPATCH_UNAVAILABLE":
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "ERROR", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
                        break;
                    case "DISPATCH_BACKUP":
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "POLICE_SCANNER_SIREN", "EPSILONISM_04_SOUNDSET", false);
                        break;
                    case "DISPATCH_SITUATION_HANDLED":
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "SUCCESS", "HUD_AWARDS", false);
                        break;
                    case "DISPATCH_SEND_OTHER":
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
                        break;
                    case "DISPATCH_IN_SERVICE":
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
                        break;
                    default:
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "BEEP", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Erreur lors de la lecture audio: {ex.Message}");
            }
        }

        private void ShowWelcomeMessage()
        {
            var config = ConfigurationManager.KeybindConfig;
            string keyName = config.PoliceDispatchKey.Key.ToString();
            
            string welcomeText = $"~g~[RADIO POLICE]~w~\nBienvenue en service!\n~b~{keyName}~w~ pour ouvrir la radio dispatch\n~y~(Touche B = Bouton radio, libre de conflit)";
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, welcomeText);
            Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
            
            PlayDispatchAudio("DISPATCH_IN_SERVICE");
        }

        private void OnAborted(object sender, EventArgs e)
        {
            CloseDispatchMenu();
            Logger.Info("Police Dispatch System terminated.");
        }

        private void DisplayDebugInfo()
        {
            // Afficher uniquement toutes les 5 secondes pour éviter le spam
            if ((DateTime.Now - _lastDispatchMessageTime).TotalSeconds % 5 < 0.1)
            {
                var config = ConfigurationManager.KeybindConfig;
                string debugText = $"~y~[DEBUG DISPATCH]~w~\nEn service: {_isOnDuty}\nMenu ouvert: {_isDispatchMenuOpen}\nTouche: {config.PoliceDispatchKey.Key}\nMessages: {_dispatchMessages.Count}";
                
                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, debugText);
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.7f, 0.1f);
            }
        }
    }

    /// <summary>
    /// Structure pour les messages de dispatch
    /// </summary>
    public class DispatchMessage
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Vector3 Location { get; set; }

        public DispatchMessage(string code, string description, Vector3 location)
        {
            Code = code;
            Description = description;
            Location = location;
        }
    }

    /// <summary>
    /// Structure pour les réponses de dispatch
    /// </summary>
    public class DispatchResponse
    {
        public string Message { get; set; } = string.Empty;
        public string AudioCue { get; set; } = string.Empty;

        public DispatchResponse(string message, string audioCue)
        {
            Message = message;
            AudioCue = audioCue;
        }
    }
} 