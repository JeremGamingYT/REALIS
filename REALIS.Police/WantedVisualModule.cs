using GTA;
using GTA.Native;
using REALIS.Common;

namespace REALIS.Police
{
    /// <summary>
    /// Active un faux Wanted Level (1) uniquement pour bénéficier du flash rouge/bleu et des blips policiers,
    /// tout en masquant l'affichage des étoiles et en empêchant le dispatch automatique.
    /// </summary>
    public class WantedVisualModule : IModule
    {
        public static WantedVisualModule Instance { get; private set; }

        private bool _enabled;
        public bool IsActive => _enabled;
        private int _prevFakeLevel;
        private bool _prevDispatch;
        private int _prevMaxWanted;

        /// <summary>
        /// Appelé par le CalloutManager dès qu'un callout démarre ou quand on demande du backup.
        /// </summary>
        public void Enable()
        {
            if (_enabled) return;
            _enabled = true;

            _prevFakeLevel = 0; // on part du principe qu'il n'y avait rien
            _prevDispatch = true; // valeur par défaut (getter indisponible)
            _prevMaxWanted = Game.MaxWantedLevel;

            // Désactive le dispatch IA et fait ignorer le joueur
            Function.Call(Hash.SET_DISPATCH_COPS_FOR_PLAYER, Game.Player, false);
            Game.Player.IgnoredByPolice = true;
            Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, true);

            // Assure également la désactivation via l'API managed
            Game.Player.DispatchsCops = false;

            // Empêche toute vraie attribution d'étoiles
            Game.MaxWantedLevel = 0;
            Game.Player.WantedLevel = 0;

            // Applique un faux wanted : pur effet visuel (flash + blips) sans IA
            Function.Call(Hash.SET_FAKE_WANTED_LEVEL, 1);
        }

        public void Disable()
        {
            if (!_enabled) return;
            _enabled = false;

            // Supprime le faux wanted
            Function.Call(Hash.SET_FAKE_WANTED_LEVEL, 0);

            // Restaure les paramètres précédemment sauvegardés
            Game.MaxWantedLevel = _prevMaxWanted;

            // Restaure le dispatch IA (s'il était actif avant)
            Function.Call(Hash.SET_DISPATCH_COPS_FOR_PLAYER, Game.Player, _prevDispatch);

            Game.Player.DispatchsCops = _prevDispatch;

            Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, true);

            // Efface toute étoile réelle au cas où un crime aurait été enregistré
            if (Game.Player.WantedLevel > 0)
            {
                Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, Game.Player);
                Game.Player.WantedLevel = 0;
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
            }
        }

        public void Initialize()
        {
            Instance = this;
        }

        public void Update()
        {
            if (!_enabled) return;
            // Le faux wanted continue de clignoter automatiquement.
            // On masque quand même les étoiles (au cas où)
            Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, 7);

            // Assure que la police ignore toujours le joueur
            Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, true);

            // Au cas où un crime aurait été enregistré malgré tout, on purge immédiatement.
            if (Game.Player.WantedLevel > 0)
            {
                Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, Game.Player);
                Game.Player.WantedLevel = 0;
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
            }
        }

        public void Dispose()
        {
            Disable();
        }
    }
} 