using System;
using GTA;
using GTA.UI;

namespace REALIS.Core
{
    /// <summary>
    /// Regroupe les utilitaires liés à la compatibilité de version du jeu.
    /// Permet de désactiver proprement le mod si une version non supportée est détectée
    /// (ex. GTA V Expanded & Enhanced sur PC ou toute version future non testée).
    /// </summary>
    internal static class GameCompatibility
    {
        /// <summary>
        /// Numéro de build (troisième composant) à partir duquel on considère la version comme non supportée.
        /// Les builds 3258 et supérieurs correspondent au patch « Expanded & Enhanced ».
        /// </summary>
        private const int UnsupportedBuildThreshold = 3258;

        /// <summary>
        /// Indique si la version courante du jeu est reconnue comme non supportée.
        /// </summary>
        public static bool IsUnsupported()
        {
            try
            {
                var versionEnum = Game.Version;
                // Lorsque SHVDN ne connaît pas encore la version, celle-ci peut être Unknown.
                if (versionEnum == GTA.GameVersion.Unknown)
                    return false;

                // Exemple de nom : v1_0_3258_0_Steam
                string name = versionEnum.ToString();
                int majorSeparator = name.IndexOf("v1_0_");
                if (majorSeparator < 0)
                    return false;

                // Récupère la partie après « v1_0_ » puis split sur les underscores.
                string[] parts = name.Substring(majorSeparator + 4).Split('_');
                if (parts.Length < 3)
                    return false;

                if (int.TryParse(parts[2], out int buildNumber))
                {
                    return buildNumber >= UnsupportedBuildThreshold;
                }
            }
            catch
            {
                // Ignorer et considérer comme supporté en cas d'erreur de parsing
            }

            return false;
        }

        /// <summary>
        /// Affiche une notification in-game (silencieuse) avertissant le joueur que le mod est désactivé.
        /// </summary>
        public static void NotifyModDisabled()
        {
            try
            {
                Notification.PostTicker("~o~REALIS désactivé : version du jeu non supportée.", false, false);
            }
            catch
            {
                // Impossible d'afficher la notification : on ne fait rien.
            }
        }
    }
} 