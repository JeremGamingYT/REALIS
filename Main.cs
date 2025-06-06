using System;
using System.Windows.Forms;
using GTA;
using GTA.UI;
using REALIS.NPC.Core;

/// <summary>
/// Point d'entrée principal pour le mod REALIS
/// Système de police réaliste pour GTA V
/// </summary>
public class REALISMain : Script
{
    private RealisticPoliceManager? _policeManager;
    
    public REALISMain()
    {
        // Initialiser les systèmes
        InitializeSystems();
        
        // Événements
        KeyDown += OnKeyDown;
        Tick += OnTick;
        Aborted += OnAborted;
    }
    
    private void InitializeSystems()
    {
        try
        {
            // Initialiser le système de police réaliste
            _policeManager = new RealisticPoliceManager();
            
            // Message de confirmation
            Notification.PostTicker("~g~REALIS~w~ - Système de police réaliste activé", true);
            Notification.PostTicker("~y~Les policiers utilisent maintenant un comportement réaliste", true);
        }
        catch (Exception ex)
        {
            Notification.PostTicker($"~r~Erreur REALIS:~w~ {ex.Message}", true);
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        try
        {
            // Mettre à jour le système de police
            _policeManager?.Update();
        }
        catch (Exception ex)
        {
            Notification.PostTicker($"~r~Police System Error:~w~ {ex.Message}", true);
        }
    }
    
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Raccourci pour activer/désactiver le système (F5)
        if (e.KeyCode == Keys.F5)
        {
            if (_policeManager != null)
            {
                _policeManager.ToggleSystem();
                Notification.PostTicker("~y~REALIS~w~ - Basculement du système police", true);
            }
        }
    }
    
    private void OnAborted(object sender, EventArgs e)
    {
        _policeManager?.Cleanup();
        _policeManager = null;
        Notification.PostTicker("~r~REALIS~w~ - Système désactivé", true);
    }
} 