using System;
using GTA;
using GTA.Native;
using REALIS.Common;

namespace REALIS.Police
{
    /// <summary>
    /// Système centralisé gérant les effets visuels d'un pseudo "WantedLevel" sans impacter l'IA.
    /// Pour l'instant : clignotement du radar uniquement (FLASH_MINIMAP_DISPLAY)
    /// + API simple SetLevel / Clear pour les modules (callouts, backup, etc.).
    /// Les icônes d'étoiles seront ajoutées plus tard.
    /// </summary>
    public class WantedVisualSystem : IModule
    {
        public static WantedVisualSystem Instance { get; private set; }

        private int _currentLevel; // 0-5. 0 = inactif
        private DateTime _lastFlash;
        private readonly TimeSpan _flashInterval = TimeSpan.FromMilliseconds(800);
        private bool _flashRed = true; // conserve l'alternance rouge / bleu à terme (placeholder)

        // API publique ------------------------------------------------------
        public void SetLevel(int level)
        {
            _currentLevel = Math.Max(0, Math.Min(5, level));
            // Rien d'autre à faire, le clignotement est géré dans Update()
        }

        public void Clear() => _currentLevel = 0;

        public int CurrentLevel => _currentLevel;

        // Cycle de vie IModule ----------------------------------------------
        public void Initialize()
        {
            Instance = this;
            _lastFlash = DateTime.UtcNow; // aucun objet graphique à créer désormais
        }

        public void Update()
        {
            if (_currentLevel <= 0) return;

            var now = DateTime.UtcNow;
            if (now - _lastFlash >= _flashInterval)
            {
                _lastFlash = now;

                // Alterne la couleur (potentiellement exploitable plus tard avec *_WITH_COLOR)
                _flashRed = !_flashRed;

                // Fait clignoter la mini-carte elle-même (natif GTA V)
                Function.Call(Hash.FLASH_MINIMAP_DISPLAY);
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
} 