using System;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.UI;
using Font = GTA.UI.Font;

namespace REALIS.Core
{
    /// <summary>
    /// Test d'urgence ultra-simple pour vérifier si les éléments UI s'affichent
    /// </summary>
    public class EmergencyMenuTest : Script
    {
        private bool _showTest = false;
        private DateTime _testStart;

        public EmergencyMenuTest()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            
            Logger.Info("Emergency Menu Test loaded - Press F12 to show test");
            
            // Notification immédiate
            Notification.PostTicker("~r~EMERGENCY MENU TEST: Press F12!", false, true);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_showTest)
            {
                DrawEmergencyTest();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F12)
            {
                _showTest = !_showTest;
                if (_showTest)
                {
                    _testStart = DateTime.Now;
                    Logger.Info("Emergency test activated");
                    Notification.PostTicker("~g~Test d'urgence activé!", false, true);
                }
                else
                {
                    Logger.Info("Emergency test deactivated");
                    Notification.PostTicker("~r~Test d'urgence désactivé!", false, true);
                }
            }
        }

        private void DrawEmergencyTest()
        {
            try
            {
                var elapsed = (DateTime.Now - _testStart).TotalSeconds;
                
                // Test simple d'affichage de texte
                var title = new TextElement("=== TEST D'URGENCE MENU ===", 
                    new PointF(100, 100), 1.5f, Color.Red, Font.ChaletLondon, Alignment.Left);
                title.Draw();

                var info = new TextElement($"Temps écoulé: {elapsed:F1}s", 
                    new PointF(100, 150), 1.0f, Color.Yellow, Font.ChaletLondon, Alignment.Left);
                info.Draw();

                var instruction = new TextElement("Si vous voyez ce texte, les éléments UI fonctionnent!", 
                    new PointF(100, 200), 0.8f, Color.Lime, Font.ChaletLondon, Alignment.Left);
                instruction.Draw();

                var control = new TextElement("F12 pour fermer ce test", 
                    new PointF(100, 250), 0.7f, Color.White, Font.ChaletLondon, Alignment.Left);
                control.Draw();

                // Test de rectangle coloré (simulé avec du texte)
                var rect = new TextElement("████████████████████████", 
                    new PointF(100, 300), 1.0f, Color.Blue, Font.ChaletLondon, Alignment.Left);
                rect.Draw();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in emergency test draw: {ex.Message}");
            }
        }
    }
} 