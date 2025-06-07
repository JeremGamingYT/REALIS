using GTA;
using GTA.Native;
using System;
using System.Windows.Forms;
using REALIS.Core;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Système de désactivation d'urgence pour éviter les plantages des scripts d'IA.
    /// Appuyez sur F8 pour désactiver temporairement tous les scripts d'IA.
    /// </summary>
    public class EmergencyDisable : Script
    {
        public static bool IsAIDisabled { get; private set; } = false;
        private DateTime _lastToggle = DateTime.MinValue;
        private const float TOGGLE_COOLDOWN = 2f; // secondes

        public EmergencyDisable()
        {
            KeyDown += OnKeyDown;
            Tick += OnTick;
            Interval = 1000;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // F8 pour basculer l'état des scripts d'IA
                if (e.KeyCode == Keys.F8)
                {
                    if ((DateTime.Now - _lastToggle).TotalSeconds < TOGGLE_COOLDOWN) return;
                    
                    IsAIDisabled = !IsAIDisabled;
                    _lastToggle = DateTime.Now;
                    
                    var status = IsAIDisabled ? "DÉSACTIVÉS" : "ACTIVÉS";
                    var color = IsAIDisabled ? "~r~" : "~g~";
                    
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, 
                        $"{color}Scripts IA de Conduite {status}~n~~w~F8 pour basculer");
                    Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
                    
                    Logger.Info($"AI Scripts {status} via F8 key");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"EmergencyDisable key error: {ex.Message}");
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                // Affichage discret du statut si désactivé
                if (IsAIDisabled)
                {
                    Function.Call(Hash.SET_TEXT_FONT, 4);
                    Function.Call(Hash.SET_TEXT_PROPORTIONAL, true);
                    Function.Call(Hash.SET_TEXT_SCALE, 0.25f, 0.25f);
                    Function.Call(Hash.SET_TEXT_COLOUR, 255, 100, 100, 200);
                    Function.Call(Hash.SET_TEXT_DROPSHADOW, 1, 0, 0, 0, 255);
                    Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                    Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "IA DÉSACTIVÉE");
                    Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.01f, 0.01f);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"EmergencyDisable display error: {ex.Message}");
            }
        }
    }
} 