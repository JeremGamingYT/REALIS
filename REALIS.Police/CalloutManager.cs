using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using REALIS.Common;
using REALIS.Police.Callouts;
using LemonUI.Menus;
using LemonUI;

namespace REALIS.Police
{
    /// <summary>
    /// Gère la génération et le suivi des callouts de police.
    /// </summary>
    public class CalloutManager : IModule
    {
        public static CalloutManager Instance { get; private set; }

        private readonly List<CalloutBase> _calloutPool = new List<CalloutBase>();
        private CalloutBase _pendingCallout;
        private DateTime _offerDeadline;
        private CalloutBase _activeCallout;
        private readonly Random _rng = new Random();
        private DateTime _nextCalloutTime;
        private ObjectPool _uiPool;
        private NativeMenu _responseMenu;

        public void Initialize()
        {
            Instance = this;
            // Enregistre les callouts disponibles
            _calloutPool.Add(new StolenVehicleCallout());
            
            // Callout de test
            _calloutPool.Add(new TestCallout());
            
            // Nouveaux callouts épiques !
            _calloutPool.Add(new StreetRacingCallout());
            _calloutPool.Add(new HostageSituationCallout());
            _calloutPool.Add(new CartelWarCallout());
            _calloutPool.Add(new DisasterResponseCallout());
            
            // Callouts activés
            _calloutPool.Add(new BankRobberyCallout());
            _calloutPool.Add(new TerroristAttackCallout());
            
            ScheduleNext();

            _uiPool = new ObjectPool();
        }

        public void Update()
        {
            if (!DutyState.PoliceOnDuty) return;

            _uiPool.Process();

            // Si l'officier est marqué comme occupé, ne rien proposer
            if (!REALIS.Police.Radio.BackupRadioModule.Available) return;

            // Si un callout est actif, le mettre à jour
            if (_activeCallout != null)
            {
                _activeCallout.Update();
                if (!_activeCallout.IsActive)
                {
                    _activeCallout = null;
                    // Désactive le visuel de recherche
                    WantedVisualSystem.Instance?.Clear();
                    ScheduleNext();
                }
                return;
            }

            // Gestion d'une offre en attente
            if (_pendingCallout != null)
            {
                // Affiche le texte d'aide chaque frame
                GTA.UI.Screen.ShowHelpTextThisFrame("~y~Y~w~ : répondre au callout");

                if (Game.IsKeyPressed(System.Windows.Forms.Keys.Y) && _responseMenu == null)
                {
                    ShowResponseMenu();
                }

                // Annuler automatiquement à expiration
                if (DateTime.Now > _offerDeadline)
                {
                    GTA.UI.Notification.Show("~r~Callout refusé");
                    _pendingCallout = null;
                    if (_responseMenu != null)
                    {
                        _responseMenu.Visible = false;
                        _uiPool.Remove(_responseMenu);
                        _responseMenu = null;
                    }
                    ScheduleNext();
                }

                return;
            }

            // Si c'est trop tôt pour le prochain callout, quitter.
            if (DateTime.Now < _nextCalloutTime) return;

            // Choisir un callout aléatoire qui peut spawn.
            var candidates = _calloutPool.Where(c => c.CanSpawn()).ToList();
            if (candidates.Count == 0)
            {
                ScheduleNext();
                return;
            }

            _pendingCallout = candidates[_rng.Next(candidates.Count)];
            _offerDeadline = DateTime.Now.AddSeconds(30);

            ShowCalloutNotification(_pendingCallout);
        }

        public void Dispose()
        {
            _activeCallout?.End();
        }

        private void ScheduleNext()
        {
            // Entre 1 et 4 minutes (plus fréquent pour plus d'action!)
            int minutes = _rng.Next(1, 5);
            _nextCalloutTime = DateTime.Now.AddMinutes(minutes);
        }

        private static void ShowCalloutNotification(CalloutBase callout)
        {
            GTA.UI.Notification.Show($"~b~Appel reçu:~w~ {callout.Name}\n{callout.Description}");
            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "CONFIRM_BEEP", "HUD_MINIGAME_SOUNDSET");
        }

        public bool HasActiveCallout => _activeCallout != null;

        /// <summary>
        /// Termine immédiatement le callout actif, s'il y en a un.
        /// </summary>
        public void ForceEndActive()
        {
            if (_activeCallout != null)
            {
                _activeCallout.End();
                _activeCallout = null;
                WantedVisualSystem.Instance?.Clear();
                ScheduleNext();
            }
        }

        /// <summary>
        /// Propose immédiatement un callout s'il n'y en a pas déjà un actif ou en attente.
        /// </summary>
        public void OfferImmediateCallout()
        {
            if (_activeCallout != null || _pendingCallout != null) return;

            var candidates = _calloutPool.Where(c => c.CanSpawn()).ToList();
            if (candidates.Count == 0) return;

            _pendingCallout = candidates[_rng.Next(candidates.Count)];
            _offerDeadline = DateTime.Now.AddSeconds(30);
            ShowCalloutNotification(_pendingCallout);
        }

        /// <summary>
        /// Démarre immédiatement un callout spécifique par son nom de classe.
        /// </summary>
        public void StartSpecificCallout(string calloutTypeName)
        {
            if (_activeCallout != null)
            {
                GTA.UI.Notification.Show("~r~Un callout est déjà en cours!");
                return;
            }

            if (_pendingCallout != null)
            {
                GTA.UI.Notification.Show("~r~Un callout est déjà en attente!");
                return;
            }

            var specificCallout = _calloutPool.FirstOrDefault(c => c.GetType().Name == calloutTypeName);
            if (specificCallout == null)
            {
                GTA.UI.Notification.Show($"~r~Callout '{calloutTypeName}' non trouvé!");
                return;
            }

            if (!specificCallout.CanSpawn())
            {
                GTA.UI.Notification.Show($"~r~{specificCallout.Name} n'est pas disponible actuellement!");
                return;
            }

            // Démarrer immédiatement le callout
            _activeCallout = specificCallout;
            _activeCallout.Start();
            WantedVisualSystem.Instance?.SetLevel(1);
            GTA.UI.Notification.Show($"~g~{specificCallout.Name} démarré!");
        }

        private void ShowResponseMenu()
        {
            _responseMenu = new NativeMenu("Appel reçu", _pendingCallout?.Name ?? "Callout");
            _responseMenu.Add(new NativeItem("Accepter"));
            _responseMenu.Add(new NativeItem("Refuser"));
            _responseMenu.ItemActivated += OnResponseActivated;
            _uiPool.Add(_responseMenu);
            _responseMenu.Visible = true;
        }

        private void OnResponseActivated(object sender, ItemActivatedArgs e)
        {
            int idx = _responseMenu.Items.IndexOf(e.Item);
            _responseMenu.Visible = false;
            _uiPool.Remove(_responseMenu);
            _responseMenu = null;

            if (idx == 0)
            {
                // Accepter
                _activeCallout = _pendingCallout;
                _activeCallout.Start();
                WantedVisualSystem.Instance?.SetLevel(1);
            }
            else
            {
                // Refuser
                GTA.UI.Notification.Show("~r~Callout refusé");
                ScheduleNext();
            }

            _pendingCallout = null;
        }
    }
} 