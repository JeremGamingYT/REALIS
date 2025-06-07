using System;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.UI;
using Font = GTA.UI.Font;
using Screen = GTA.UI.Screen;

namespace REALIS.Core
{
    /// <summary>
    /// Script de diagnostic pour forcer l'affichage du menu
    /// </summary>
    public class MenuDiagnostic : Script
    {
        private bool _showingDiagnostic = false;
        private DateTime _startTime;
        private RealVisualMenu? _forceMenu;

        public MenuDiagnostic()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            
            Logger.Info("Menu Diagnostic loaded - Press F9 to force show menu");
            
            // Message d'information
            Notification.PostTicker("~g~Menu Diagnostic: Press F9 to force show menu!", false, true);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_showingDiagnostic)
            {
                DrawDiagnosticInfo();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F9)
            {
                ForceShowMenu();
            }
            else if (e.KeyCode == Keys.F10)
            {
                ToggleDiagnostic();
            }
            else if (e.KeyCode == Keys.F11)
            {
                CreateNewMenu();
            }
        }

        private void ForceShowMenu()
        {
            try
            {
                Logger.Info("Force showing menu...");
                
                // Restaurer le temps normal d'abord
                Game.TimeScale = 1.0f;
                
                // Créer un nouveau menu
                _forceMenu = new RealVisualMenu();
                
                // Afficher une notification
                Notification.PostTicker("~g~Menu forcé créé! Appuyez sur INSERT pour l'ouvrir.", false, true);
                
                Logger.Info("Force menu created successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error force showing menu: {ex.Message}");
                Notification.PostTicker($"~r~Erreur: {ex.Message}", false, true);
            }
        }

        private void CreateNewMenu()
        {
            try
            {
                Logger.Info("Creating brand new menu instance...");
                
                // Détruire l'ancien menu s'il existe
                _forceMenu = null;
                
                // Créer un nouveau menu
                _forceMenu = new RealVisualMenu();
                
                Notification.PostTicker("~y~Nouveau menu créé! INSERT pour ouvrir.", false, true);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating new menu: {ex.Message}");
                Notification.PostTicker($"~r~Erreur création menu: {ex.Message}", false, true);
            }
        }

        private void ToggleDiagnostic()
        {
            _showingDiagnostic = !_showingDiagnostic;
            if (_showingDiagnostic)
            {
                _startTime = DateTime.Now;
                Logger.Info("Diagnostic display enabled");
            }
            else
            {
                Logger.Info("Diagnostic display disabled");
            }
        }

        private void DrawDiagnosticInfo()
        {
            var resolution = Screen.Resolution;
            var startY = 50f;
            var lineHeight = 25f;
            var currentY = startY;

            // Titre
            var title = new TextElement("=== MENU DIAGNOSTIC ===", 
                new PointF(50, currentY), 1.0f, Color.Yellow, Font.ChaletLondon, Alignment.Left);
            title.Draw();
            currentY += lineHeight * 2;

            // Informations système
            var info = new string[]
            {
                $"Résolution écran: {resolution.Width}x{resolution.Height}",
                $"Temps de jeu: {Game.TimeScale:F2}",
                $"Menu forcé: {(_forceMenu != null ? "OUI" : "NON")}",
                $"Temps diagnostic: {(DateTime.Now - _startTime).TotalSeconds:F1}s",
                "",
                "TOUCHES:",
                "F9 - Forcer nouveau menu",
                "F10 - Toggle diagnostic",
                "F11 - Recréer menu",
                "INSERT - Ouvrir menu (si créé)"
            };

            foreach (var line in info)
            {
                var element = new TextElement(line, 
                    new PointF(50, currentY), 0.7f, Color.White, Font.ChaletLondon, Alignment.Left);
                element.Draw();
                currentY += lineHeight;
            }
        }
    }
} 