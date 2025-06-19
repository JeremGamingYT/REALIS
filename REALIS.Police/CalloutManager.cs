using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using REALIS.Common;
using REALIS.Police.Callouts;

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

        public void Initialize()
        {
            Instance = this;
            // Enregistre les callouts disponibles
            _calloutPool.Add(new StolenVehicleCallout());
            ScheduleNext();
        }

        public void Update()
        {
            if (!DutyState.PoliceOnDuty) return;

            // Si un callout est actif, le mettre à jour
            if (_activeCallout != null)
            {
                _activeCallout.Update();
                if (!_activeCallout.IsActive)
                {
                    _activeCallout = null;
                    // Désactive le visuel de recherche
                    WantedVisualModule.Instance?.Disable();
                    ScheduleNext();
                }
                return;
            }

            // Gestion d'une offre en attente
            if (_pendingCallout != null)
            {
                // Affiche le texte d'aide chaque frame
                GTA.UI.Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Accepter le callout");

                if (Game.IsControlJustPressed(GTA.Control.Context))
                {
                    // Accepté
                    _activeCallout = _pendingCallout;
                    _activeCallout.Start();
                    // Active le visuel de recherche
                    WantedVisualModule.Instance?.Enable();
                    _pendingCallout = null;
                    return;
                }

                // Annuler automatiquement à expiration
                if (DateTime.Now > _offerDeadline)
                {
                    GTA.UI.Notification.Show("~r~Callout refusé");
                    _pendingCallout = null;
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
            // Entre 2 et 5 minutes
            int minutes = _rng.Next(2, 6);
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
                WantedVisualModule.Instance?.Disable();
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
    }
} 