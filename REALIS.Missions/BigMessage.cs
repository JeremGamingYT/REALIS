using System;
using GTA;
using GTA.UI;

namespace REALIS.Missions
{
    /// <summary>
    ///  Affiche les écrans Mission Passed / Mission Failed sans dépendre de NativeUI.
    ///  Utilise le scaleform "MP_BIG_MESSAGE_FREEMODE" comme le jeu original.
    /// </summary>
    public static class BigMessage
    {
        private static Scaleform _sf;
        private static DateTime _displayUntil;

        public static void ShowMissionPassed(string title, string subTitleWithCash, int durationMs = 6000)
        {
            ShowInternal("SHOW_SHARD_MISSION_PASSED_MP_MESSAGE", title, subTitleWithCash, durationMs);
        }

        public static void ShowMissionFailed(string title, string reason, int durationMs = 6000)
        {
            ShowInternal("SHOW_SHARD_MISSION_FAILED_MESSAGE", title, reason, durationMs);
        }

        private static void ShowInternal(string func, string line1, string line2, int durationMs)
        {
            try
            {
                _sf?.Dispose();
                _sf = new Scaleform("MP_BIG_MESSAGE_FREEMODE");
                _sf.CallFunction(func, line1, line2, 0); // le 3e param est unused pour cette fonction
                _displayUntil = DateTime.Now.AddMilliseconds(durationMs);
            }
            catch (Exception ex)
            {
                Screen.ShowSubtitle($"~r~Erreur BigMessage: {ex.Message}");
            }
        }

        /// <summary>
        ///  À appeler chaque frame pour rendre le scaleform si besoin.
        /// </summary>
        public static void Render()
        {
            if (_sf == null) return;

            if (DateTime.Now > _displayUntil)
            {
                _sf.Dispose();
                _sf = null;
                return;
            }

            _sf.Render2D();
        }
    }
} 