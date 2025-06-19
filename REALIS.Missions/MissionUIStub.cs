using GTA;
using GTA.UI;

namespace NativeUI
{
    /// <summary>
    ///  Stub de BigMessageThread pour la compilation hors du runtime NativeUI.
    ///  Se limite à afficher un simple sous-titre ou notification.
    ///  Si NativeUI.dll est présent à l'exécution, ce fichier sera ignoré grâce au namespace identique.
    /// </summary>
    public static class BigMessageThread
    {
        public static class MessageInstance
        {
            public static void ShowMissionPassedMessage(string msg, int time = 5000)
            {
                Screen.ShowSubtitle(msg, time);
            }

            public static void ShowColoredShard(string title, string subtitle, HudColor textColor, HudColor bgColor, int time = 5000)
            {
                Screen.ShowSubtitle($"{title}\n{subtitle}", time);
            }
        }
    }

    public enum HudColor
    {
        None = 0
    }
} 