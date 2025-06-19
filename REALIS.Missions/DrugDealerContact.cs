using System;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.UI;
using GTA.Native;
using REALIS.Common;
using LemonUI;
using LemonUI.Menus;
using UIScreen = GTA.UI.Screen;

namespace REALIS.Missions
{
    /// <summary>
    /// Contact pour les missions de livraison de drogue
    /// </summary>
    public class DrugDealerContact : IModule
    {
        private readonly Vector3 _contactPosition = new Vector3(-1037.73f, -2738.12f, 20.17f); // Terminal
        private Ped _contactPed;
        private Blip _contactBlip;
        private bool _playerNearContact = false;
        private bool _showingContactUI = false;
        private DrugDeliveryMission _currentMission;
        
        // Disponibilité nocturne (20h00 – 05h00)
        private readonly TimeSpan _availabilityStart = new TimeSpan(20, 0, 0);
        private readonly TimeSpan _availabilityEnd = new TimeSpan(5, 0, 0);
        private bool _isMissionAvailable = false;
        private bool _availabilityNotified = false;
        
        // LemonUI
        private ObjectPool _uiPool;
        private NativeMenu _contactMenu;
        
        public void Initialize()
        {
            CreateContact();

            // Préparer LemonUI
            _uiPool = new ObjectPool();
            BuildContactMenu();
        }
        
        public void Update()
        {
            UpdateAvailabilityWindow();
            CheckPlayerProximity();
            HandleContactInteraction();
            
            // Mettre à jour LemonUI
            _uiPool?.Process();
            
            if (_showingContactUI)
            {
                DrawContactUI();
            }

            // Propager la mise à jour à la mission en cours (si elle existe)
            _currentMission?.Update();
        }
        
        public void Dispose()
        {
            CleanupContact();
        }
        
        private void CreateContact()
        {
            try
            {
                uint modelHash = (uint)PedHash.Business01AMY;

                // Demander le modèle sans bloquer
                Function.Call(Hash.REQUEST_MODEL, (int)modelHash);

                void trySpawn()
                {
                    if (Function.Call<bool>(Hash.HAS_MODEL_LOADED, (int)modelHash))
                    {
                        var model = new Model((int)modelHash);

                        _contactPed = World.CreatePed(model, _contactPosition);
                        _contactPed.IsPersistent = true;
                        _contactPed.BlockPermanentEvents = true;
                        _contactPed.IsInvincible = true;
                        _contactPed.CanBeTargetted = false;

                        // Orientation
                        _contactPed.Heading = 180f;

                        // Blip
                        _contactBlip = _contactPed.AddBlip();
                        _contactBlip.Sprite = BlipSprite.Package;
                        _contactBlip.Color = BlipColor.Green;
                        _contactBlip.Name = "Contact - Livraisons";
                        _contactBlip.IsShortRange = true;

                        // Libérer le modèle
                        Function.Call(Hash.SET_MODEL_AS_NO_LONGER_NEEDED, (int)modelHash);
                    }
                    else
                    {
                        // Réessayer dans 50 ms
                        GameScheduler.Schedule(trySpawn, 50);
                    }
                }

                // Tentative initiale
                trySpawn();
            }
            catch (Exception ex)
            {
                UIScreen.ShowSubtitle("~r~Erreur: Impossible de créer le contact: " + ex.Message, 2000);
            }
        }
        
        private void CheckPlayerProximity()
        {
            if (_contactPed == null || !_contactPed.Exists()) return;
            
            var player = Game.Player.Character;
            var distance = Vector3.Distance(player.Position, _contactPed.Position);
            
            var wasNear = _playerNearContact;
            _playerNearContact = distance < 3.0f;
            
            if (_playerNearContact && !wasNear)
            {
                // Joueur s'approche
                if (_isMissionAvailable)
                {
                    _showingContactUI = true;

                    // Faire regarder le contact vers le joueur
                    _contactPed.Task.LookAt(player, 5000);

                    // Aide à l'écran
                    UIScreen.ShowSubtitle("Appuyez sur ~INPUT_CONTEXT~ pour parler au contact", 1000);
                }
                else
                {
                    // Mission indisponible actuellement
                    _showingContactUI = false;
                    UIScreen.ShowSubtitle("Le contact n'a rien pour vous pour l'instant. Revenez ce soir (20h – 5h).", 1500);
                }
            }
            else if (!_playerNearContact && wasNear)
            {
                // Joueur s'éloigne
                _showingContactUI = false;
                _contactPed.Task.ClearLookAt();
            }
        }
        
        private void HandleContactInteraction()
        {
            if (!_playerNearContact) return;
            
            if (Game.IsControlJustPressed(GTA.Control.Context)) // Touche E
            {
                if (!_isMissionAvailable)
                {
                    UIScreen.ShowSubtitle("Revenez ce soir pour du travail.", 1000);
                    return;
                }

                if (_currentMission != null && _currentMission.IsMissionActive())
                {
                    UIScreen.ShowSubtitle("~y~Attention: Vous avez déjà une mission en cours!", 1000);
                    return;
                }

                // Ouvre le menu de dialogue
                _contactMenu.Visible = true;
                _showingContactUI = false; // Cache le prompt pendant le menu
            }
        }
        
        private void StartNewMission()
        {
            try
            {
                // Dialogue du contact (non bloquant)
                var delayBeforeMission = ShowContactDialogue();
                
                // Planifier le démarrage réel de la mission après le dialogue
                GameScheduler.Schedule(() =>
                {
                    _currentMission = new DrugDeliveryMission();
                    _currentMission.Initialize();
                    _currentMission.StartMissionFromContact();
                    // Faire dire quelque chose au contact
                    Function.Call(Hash.PLAY_PED_AMBIENT_SPEECH_NATIVE, _contactPed, "GENERIC_BUY", "SPEECH_PARAMS_FORCE_NORMAL");
                }, delayBeforeMission);
            }
            catch (Exception ex)
            {
                UIScreen.ShowSubtitle("~r~Erreur: Impossible de démarrer la mission: " + ex.Message, 2000);
            }
        }
        
        private int ShowContactDialogue()
        {
            // Afficher un dialogue simple de manière non bloquante
            var dialogues = new[]
            {
                "J'ai du boulot pour toi si tu es intéressé...",
                "Il faut livrer quelques paquets en ville.",
                "Attention à ne pas abîmer le véhicule!",
                "Les flics surveillent, alors conduis prudemment."
            };

            int cumulativeDelay = 0;
            const int lineDuration = 3000;

            foreach (var dialogue in dialogues)
            {
                var msg = dialogue; // Copie locale pour le lambda
                GameScheduler.Schedule(() => UIScreen.ShowSubtitle("~b~Contact: " + msg, lineDuration), cumulativeDelay);
                cumulativeDelay += lineDuration;
            }

            // Message final d'acceptation
            GameScheduler.Schedule(() => UIScreen.ShowSubtitle("~g~Mission acceptée: Récupérez le véhicule et commencez les livraisons!", 3000), cumulativeDelay);
            cumulativeDelay += 3000;

            return cumulativeDelay; // Durée totale avant lancement de mission
        }
        
        private void DrawContactUI()
        {
            if (!_playerNearContact || (_contactMenu != null && _contactMenu.Visible)) return;
            
            // Afficher une indication au-dessus du contact
            var screenPos = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_SCREEN_COORD_FROM_WORLD_COORD, 
                _contactPed.Position.X, _contactPed.Position.Y, _contactPed.Position.Z + 1.0f, 
                screenPos, screenPos))
            {
                // Texte au-dessus du contact
                Function.Call(Hash.SET_TEXT_FONT, 4);
                Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
                Function.Call(Hash.SET_TEXT_SCALE, 0.5f, 0.5f);
                Function.Call(Hash.SET_TEXT_OUTLINE);
                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "Contact - Livraisons");
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, screenPos.GetResult<float>(), screenPos.GetResult<float>() - 0.05f);
                
                // Indicateur d'interaction
                Function.Call(Hash.SET_TEXT_FONT, 4);
                Function.Call(Hash.SET_TEXT_COLOUR, 0, 255, 0, 255);
                Function.Call(Hash.SET_TEXT_SCALE, 0.4f, 0.4f);
                Function.Call(Hash.SET_TEXT_OUTLINE);
                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "[E] Parler");
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, screenPos.GetResult<float>(), screenPos.GetResult<float>() + 0.02f);
            }
        }
        
        private void CleanupContact()
        {
            try
            {
                if (_contactBlip != null && _contactBlip.Exists())
                    _contactBlip.Delete();
                
                if (_contactPed != null && _contactPed.Exists())
                {
                    _contactPed.IsPersistent = false;
                    _contactPed.MarkAsNoLongerNeeded();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors du nettoyage du contact: {ex.Message}");
            }
        }

        /// <summary>
        /// Met à jour l'état de disponibilité de la mission et affiche une notification lorsque la plage horaire commence.
        /// </summary>
        private void UpdateAvailabilityWindow()
        {
            int hour = Function.Call<int>(Hash.GET_CLOCK_HOURS);
            int minute = Function.Call<int>(Hash.GET_CLOCK_MINUTES);
            TimeSpan current = new TimeSpan(hour, minute, 0);

            bool nowAvailable;
            // Gère les plages horaires qui traversent minuit
            if (_availabilityStart <= _availabilityEnd)
            {
                nowAvailable = current >= _availabilityStart && current < _availabilityEnd;
            }
            else
            {
                // Exemple : 20h00 – 05h00
                nowAvailable = current >= _availabilityStart || current < _availabilityEnd;
            }

            if (nowAvailable && !_isMissionAvailable)
            {
                _isMissionAvailable = true;

                if (!_availabilityNotified)
                {
                    Notification.Show("~g~Le contact est disponible pour des livraisons ce soir");
                    _availabilityNotified = true;
                }
            }
            else if (!nowAvailable)
            {
                _isMissionAvailable = false;
                _availabilityNotified = false; // Réinitialise pour la prochaine nuit
            }
        }

        /// <summary>
        /// Construit le menu LemonUI pour le contact (appelé une seule fois au démarrage).
        /// </summary>
        private void BuildContactMenu()
        {
            _contactMenu = new NativeMenu("Contact", "Livraison de drogue");

            // Petite description désactivée (non sélectionnable)
            var desc = new NativeItem("\"J'ai du boulot pour toi : livrer quelques paquets discrètement. Intéressé ?\"");
            desc.Enabled = false;
            _contactMenu.Add(desc);

            var accept = new NativeItem("Oui, je m'en occupe");
            var decline = new NativeItem("Pas maintenant");

            _contactMenu.Add(accept);
            _contactMenu.Add(decline);

            accept.Activated += (_, __) =>
            {
                _contactMenu.Visible = false;
                StartNewMission();
            };

            decline.Activated += (_, __) =>
            {
                _contactMenu.Visible = false;
                UIScreen.ShowSubtitle("Peut-être une autre fois…", 1500);
            };

            _contactMenu.Closed += (_, __) =>
            {
                // Remet l'indicateur si le joueur est toujours à proximité après fermeture
                if (_playerNearContact)
                {
                    _showingContactUI = true;
                }
            };

            _uiPool.Add(_contactMenu);
        }
    }
} 