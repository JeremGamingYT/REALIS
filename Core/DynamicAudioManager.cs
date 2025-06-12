using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace REALIS.Core
{
    /// <summary>
    ///     Gère les améliorations audio dynamiques :
    ///     1. Réverbération adaptative dans les environnements fermés (tunnels, parkings…)
    ///     2. Pas contextuels (variation de volume, matériau du sol – à affiner)
    ///
    ///     Le script est autonome : il se charge dès que l’assembly est chargée par SHVDN.
    /// </summary>
    public class DynamicAudioManager : Script
    {
        // --- Réverbération dynamique ---
        private bool _reverbActive;
        private bool _sceneActive;
        private const float CeilingCheckHeight = 10f;   // hauteur à laquelle on cherche un plafond
        private const float CeilingThreshold = 6f;      // si le plafond est plus bas que ça → environnement clos
        private const string ReverbScene = "CAR_TUNNEL_SCENE"; // audio scène appliquant un fort écho sur tous les sons moteur/klaxon

        // --- Pas contextuels ---
        private DateTime _lastFootStep;

        public DynamicAudioManager()
        {
            // Tick rapide pour la gestion audio
            Tick += OnTick;
            Interval = 0; // appel à chaque frame
        }

        private void OnTick(object sender, EventArgs e)
        {
            var player = Game.Player.Character;
            if (!player.Exists() || player.IsDead)
                return;

            try
            {
                UpdateReverb(player);
                UpdateFootsteps(player);
            }
            catch (Exception ex)
            {
                // On ne veut pas spammer le log en cas d’erreur dans le tick : il se produit 30-60 fois par seconde.
                GTA.UI.Screen.ShowSubtitle($"Audio error: {ex.Message}", 1000);
            }
        }

        #region Réverbération

        private void UpdateReverb(Ped player)
        {
            bool shouldEnable = IsInClosedEnvironment(player);

            if (shouldEnable && !_reverbActive)
            {
                // Bascule : réverbération ON
                GTA.Audio.SetAudioFlag(GTA.AudioFlags.ListenerReverbDisabled, false);
                // Lancer une AudioScene prévue pour les tunnels (applique la convolution d'écho aux véhicules)
                if (!_sceneActive)
                {
                    Function.Call(Hash.START_AUDIO_SCENE, ReverbScene);
                    _sceneActive = true;
                }
                // Optionnel : remplacer les paramètres de portail pour un tunnel générique.
                Function.Call(Hash.SET_PORTAL_SETTINGS_OVERRIDE, "", "V");
                _reverbActive = true;
            }
            else if (!shouldEnable && _reverbActive)
            {
                // Bascule : réverbération OFF
                GTA.Audio.SetAudioFlag(GTA.AudioFlags.ListenerReverbDisabled, true);
                if (_sceneActive)
                {
                    Function.Call(Hash.STOP_AUDIO_SCENE, ReverbScene);
                    _sceneActive = false;
                }
                // Réinitialiser les paramètres audio du portail
                Function.Call(Hash.SET_PORTAL_SETTINGS_OVERRIDE, "", "");
                _reverbActive = false;
            }
        }

        /// <summary>
        /// Estimation naïve : si un plafond (collision de la map) se trouve à quelques mètres au-dessus de la tête,
        /// on considère que l'environnement est fermé → on active la réverbération.
        /// </summary>
        private bool IsInClosedEnvironment(Ped player)
        {
            Vector3 from = player.Position + Vector3.WorldUp * 0.25f; // point de départ légèrement au-dessus du sol
            Vector3 to = from + Vector3.WorldUp * CeilingCheckHeight;

            var result = World.Raycast(from, to, IntersectFlags.Map, player);
            if (!result.DidHit) return false;

            float dist = result.HitPosition.DistanceTo(from);
            return dist < CeilingThreshold;
        }

        #endregion

        #region Pas contextuels

        private void UpdateFootsteps(Ped player)
        {
            // Détection simple des pas : si le joueur est à pied et se déplace au-delà d'une certaine vitesse,
            // on déclenche un son toutes les X millisecondes (dépendant de la vitesse).
            if (!player.IsOnFoot) return;
            if (player.IsSwimming) return;

            float speed = player.Velocity.Length();
            if (speed < 0.7f) return; // immobile ou presque

            double interval = speed > 3f ? 200 : 350; // plus on va vite, plus les pas sont rapprochés
            if ((DateTime.UtcNow - _lastFootStep).TotalMilliseconds < interval) return;

            _lastFootStep = DateTime.UtcNow;

            // TODO : déterminer le matériau du sol pour choisir le sample
            // Pour l'instant on joue un son de pas générique avec un volume fonction de la vitesse.
            string soundName = "CONTINUE_BUTTON_PRESS"; // placeholder (son discret)
            string setName = "HUD_FRONTEND_DEFAULT_SOUNDSET";

            // On utilise l'intensité pour encoder le volume dans [0.2 ‑ 1.0]
            float volume = Math.Min(1.0f, 0.2f + speed / 6f);

            // SHVDN3 ne propose pas un paramètre de volume directement — on contourne en jouant via NAudio
            // ou en utilisant différentes banques. Pour rester simple on lance le son tel quel.
            try
            {
                Audio.PlaySoundFromEntityAndForget(player, soundName, setName);
            }
            catch
            {
                // Ignorer si le son n'existe pas (selon la version du jeu)
            }
        }

        #endregion
    }
} 