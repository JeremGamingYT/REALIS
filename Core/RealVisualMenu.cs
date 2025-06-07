using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Font = GTA.UI.Font;
using Screen = GTA.UI.Screen;
using GTA;
using GTA.UI;

namespace REALIS.Core
{
    /// <summary>
    /// VRAI MENU VISUEL avec éléments graphiques à l'écran
    /// </summary>
    public class RealVisualMenu : Script
    {
        private bool _menuActive = false;
        private int _selectedOption = 0;
        private DateTime _lastInput = DateTime.MinValue;
        private const double INPUT_DELAY_MS = 200;
        
        // Éléments visuels du menu
        private readonly List<TextElement> _menuElements = new List<TextElement>();
        private ContainerElement? _menuContainer;
        
        private readonly string[] _menuOptions = {
            "Complete First Run Setup",
            "Restore Normal Time", 
            "Skip All Setup",
            "Show Diagnostic",
            "Exit Menu"
        };

        // Couleurs du menu
        private readonly Color _backgroundColor = Color.FromArgb(200, 0, 0, 0); // Noir semi-transparent
        private readonly Color _titleColor = Color.Yellow;
        private readonly Color _selectedColor = Color.Lime;
        private readonly Color _normalColor = Color.White;
        private readonly Color _borderColor = Color.Cyan;

        public RealVisualMenu()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            
            Logger.Info("Real Visual Menu loaded - Press INSERT to show REAL visual menu");
            
            // Créer le container du menu
            CreateMenuContainer();
            
            // Message initial
            ShowWelcomeMessage();
        }

        private void ShowWelcomeMessage()
        {
            // Ne plus bloquer avec une boucle while, juste afficher une notification
            Notification.PostTicker("~g~REALIS Real Visual Menu Ready! Press INSERT to show menu", false, true);
            Logger.Info("Welcome message shown");
        }

        private void CreateMenuContainer()
        {
            // Créer un container pour le menu
            _menuContainer = new ContainerElement(new PointF(0, 0), new SizeF(400, 300), Color.Transparent);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_menuActive)
            {
                DrawMenu();
                
                // Traiter les inputs
                if ((DateTime.Now - _lastInput).TotalMilliseconds >= INPUT_DELAY_MS)
                {
                    ProcessInput();
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // INSERT : Activer/désactiver le menu
            if (e.KeyCode == Keys.Insert)
            {
                ToggleMenu();
            }
            
            // DELETE : Menu d'urgence également
            if (e.KeyCode == Keys.Delete && e.Control)
            {
                ShowMenu();
            }
        }

        private void ToggleMenu()
        {
            if (_menuActive)
            {
                HideMenu();
            }
            else
            {
                ShowMenu();
            }
        }

        private void ShowMenu()
        {
            _menuActive = true;
            _selectedOption = 0;
            
            // Ralentir légèrement le temps
            try
            {
                Game.TimeScale = 0.5f;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting time scale: {ex.Message}");
            }
            
            BuildMenuElements();
            Logger.Info("Real visual menu activated");
        }

        private void HideMenu()
        {
            _menuActive = false;
            
            // Restaurer le temps normal
            try
            {
                Game.TimeScale = 1.0f;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error restoring time scale: {ex.Message}");
            }
            
            // Nettoyer les éléments
            _menuElements.Clear();
            Logger.Info("Real visual menu deactivated");
        }

        private void BuildMenuElements()
        {
            _menuElements.Clear();
            
            var resolution = Screen.Resolution;
            var centerX = resolution.Width * 0.5f;
            var centerY = resolution.Height * 0.5f;
            var menuWidth = 500f;
            var menuHeight = 400f;
            var startX = centerX - menuWidth / 2;
            var startY = centerY - menuHeight / 2;
            
            // Titre du menu
            var title = new TextElement("=== REALIS REAL VISUAL MENU ===", 
                new PointF(centerX, startY + 40), 1.2f, _titleColor, Font.ChaletLondon, Alignment.Center);
            _menuElements.Add(title);
            
            // Sous-titre
            var subtitle = new TextElement("Use Arrow Keys to Navigate, ENTER to Select", 
                new PointF(centerX, startY + 80), 0.7f, Color.LightGray, Font.ChaletLondon, Alignment.Center);
            _menuElements.Add(subtitle);
            
            // Options du menu
            for (int i = 0; i < _menuOptions.Length; i++)
            {
                var isSelected = (i == _selectedOption);
                var color = isSelected ? _selectedColor : _normalColor;
                var prefix = isSelected ? ">>> " : "    ";
                var text = prefix + _menuOptions[i];
                var scale = isSelected ? 1.0f : 0.9f;
                
                var optionElement = new TextElement(text, 
                    new PointF(centerX, startY + 140 + (i * 40)), 
                    scale, color, Font.ChaletLondon, Alignment.Center);
                _menuElements.Add(optionElement);
            }
            
            // Instructions
            var instructions = new TextElement("↑↓ Navigate | ENTER Select | INSERT/ESC Close", 
                new PointF(centerX, startY + menuHeight - 60), 
                0.6f, Color.Orange, Font.ChaletLondon, Alignment.Center);
            _menuElements.Add(instructions);
            
            // Bordure (simulée avec des lignes de texte)
            CreateMenuBorder(startX, startY, menuWidth, menuHeight);
        }

        private void CreateMenuBorder(float x, float y, float width, float height)
        {
            // Ligne du haut
            var topBorder = new TextElement("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", 
                new PointF(x + width/2, y + 10), 0.5f, _borderColor, Font.ChaletLondon, Alignment.Center);
            _menuElements.Add(topBorder);
            
            // Ligne du bas
            var bottomBorder = new TextElement("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", 
                new PointF(x + width/2, y + height - 10), 0.5f, _borderColor, Font.ChaletLondon, Alignment.Center);
            _menuElements.Add(bottomBorder);
        }

        private void DrawMenu()
        {
            // Dessiner tous les éléments du menu
            foreach (var element in _menuElements)
            {
                element.Draw();
            }
        }

        private void ProcessInput()
        {
            bool inputProcessed = false;

            if (Game.IsKeyPressed(Keys.Up) || Game.IsKeyPressed(Keys.W))
            {
                _selectedOption = (_selectedOption - 1 + _menuOptions.Length) % _menuOptions.Length;
                BuildMenuElements(); // Reconstruire pour mettre à jour la sélection
                inputProcessed = true;
            }
            else if (Game.IsKeyPressed(Keys.Down) || Game.IsKeyPressed(Keys.S))
            {
                _selectedOption = (_selectedOption + 1) % _menuOptions.Length;
                BuildMenuElements(); // Reconstruire pour mettre à jour la sélection
                inputProcessed = true;
            }
            else if (Game.IsKeyPressed(Keys.Enter) || Game.IsKeyPressed(Keys.Space))
            {
                ExecuteSelectedOption();
                inputProcessed = true;
            }
            else if (Game.IsKeyPressed(Keys.Escape) || Game.IsKeyPressed(Keys.Insert))
            {
                HideMenu();
                inputProcessed = true;
            }

            if (inputProcessed)
            {
                _lastInput = DateTime.Now;
            }
        }

        private void ExecuteSelectedOption()
        {
            switch (_selectedOption)
            {
                case 0: // Complete First Run Setup
                    CompleteFirstRunSetup();
                    break;
                case 1: // Restore Normal Time
                    RestoreNormalTime();
                    break;
                case 2: // Skip All Setup
                    SkipAllSetup();
                    break;
                case 3: // Show Diagnostic
                    ShowDiagnostic();
                    break;
                case 4: // Exit Menu
                    HideMenu();
                    break;
            }
        }

        private void CompleteFirstRunSetup()
        {
            try
            {
                // Restaurer le temps normal
                Game.TimeScale = 1.0f;
                
                // Marquer comme terminé
                ConfigurationManager.MarkFirstRunCompleted();
                ConfigurationManager.SaveKeybindConfiguration();
                
                // Appliquer les paramètres par défaut
                ConfigurationManager.UserConfig.EnableNotifications = true;
                ConfigurationManager.UserConfig.EnableAudioFeedback = true;
                ConfigurationManager.UserConfig.ModSettings.PoliceSystemEnabled = true;
                ConfigurationManager.UserConfig.ModSettings.TrafficAIEnabled = true;
                ConfigurationManager.UserConfig.ModSettings.GasStationSystemEnabled = true;
                ConfigurationManager.UserConfig.ModSettings.FoodStoreSystemEnabled = true;
                
                ShowSuccessMessage("First Run Setup Completed Successfully!");
                Logger.Info("First run setup completed via real visual menu");
                
                HideMenu();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error completing first run setup: {ex.Message}");
                ShowErrorMessage("Failed to complete setup");
            }
        }

        private void RestoreNormalTime()
        {
            try
            {
                Game.TimeScale = 1.0f;
                ShowSuccessMessage("Time Scale Restored to Normal");
                Logger.Info("Time scale restored via real visual menu");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error restoring time scale: {ex.Message}");
                ShowErrorMessage("Failed to restore time scale");
            }
        }

        private void SkipAllSetup()
        {
            try
            {
                // Restaurer le temps normal
                Game.TimeScale = 1.0f;
                
                // Marquer comme terminé et sauvegarder
                ConfigurationManager.MarkFirstRunCompleted();
                ConfigurationManager.SaveKeybindConfiguration();
                ConfigurationManager.SaveUserConfiguration();
                
                ShowSuccessMessage("All Setup Skipped - REALIS Ready!");
                Logger.Info("All setup skipped via real visual menu");
                
                HideMenu();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error skipping setup: {ex.Message}");
                ShowErrorMessage("Failed to skip setup");
            }
        }

        private void ShowDiagnostic()
        {
            // Créer une fenêtre de diagnostic temporaire
            _menuElements.Clear();
            
            var resolution = Screen.Resolution;
            var centerX = resolution.Width * 0.5f;
            var centerY = resolution.Height * 0.5f;
            
            var title = new TextElement("=== DIAGNOSTIC ===", 
                new PointF(centerX, centerY - 100), 1.2f, Color.Cyan, Font.ChaletLondon, Alignment.Center);
            _menuElements.Add(title);
            
            var timeScale = new TextElement($"Time Scale: {Game.TimeScale:F2}", 
                new PointF(centerX, centerY - 60), 0.9f, Color.White, Font.ChaletLondon, Alignment.Center);
            _menuElements.Add(timeScale);
            
            try
            {
                var firstRun = new TextElement($"First Run: {ConfigurationManager.IsFirstRun}", 
                    new PointF(centerX, centerY - 20), 0.9f, Color.White, Font.ChaletLondon, Alignment.Center);
                _menuElements.Add(firstRun);
            }
            catch
            {
                var firstRunError = new TextElement("First Run: ERROR", 
                    new PointF(centerX, centerY - 20), 0.9f, Color.Red, Font.ChaletLondon, Alignment.Center);
                _menuElements.Add(firstRunError);
            }
            
            var resolutionText = new TextElement($"Resolution: {resolution.Width}x{resolution.Height}", 
                new PointF(centerX, centerY + 20), 0.9f, Color.White, Font.ChaletLondon, Alignment.Center);
            _menuElements.Add(resolutionText);
            
            var backInstruction = new TextElement("Press ESC to go back to menu", 
                new PointF(centerX, centerY + 80), 0.7f, Color.Orange, Font.ChaletLondon, Alignment.Center);
            _menuElements.Add(backInstruction);
            
            Logger.Info("Diagnostic shown via real visual menu");
        }

        private void ShowSuccessMessage(string message)
        {
            // Utiliser les notifications au lieu de bloquer
            Notification.PostTicker($"~g~SUCCESS: {message}", false, true);
            Logger.Info($"Success message: {message}");
        }

        private void ShowErrorMessage(string message)
        {
            // Utiliser les notifications au lieu de bloquer
            Notification.PostTicker($"~r~ERROR: {message}", false, true);
            Logger.Error($"Error message: {message}");
        }
    }
} 