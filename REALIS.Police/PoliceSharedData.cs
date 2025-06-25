using System.Collections.Generic;
using GTA;
using LemonUI.Menus;

namespace REALIS.Police
{
    /// <summary>
    /// Classe statique pour partager des données entre les modules de police
    /// </summary>
    public static class PoliceSharedData
    {
        /// <summary>
        /// Le partenaire actuel du joueur (peut être null)
        /// </summary>
        public static Ped CurrentPartner { get; set; } = null;
        
        /// <summary>
        /// Liste des PNJ qui sont actuellement gérés par le PartnerModule
        /// </summary>
        public static HashSet<int> ManagedPartnerPeds { get; private set; } = new HashSet<int>();
        
        /// <summary>
        /// Le menu actuellement ouvert (un seul menu à la fois)
        /// </summary>
        public static NativeMenu CurrentOpenMenu { get; private set; } = null;
        
        /// <summary>
        /// Le module qui a ouvert le menu actuel
        /// </summary>
        public static string CurrentMenuOwner { get; private set; } = null;
        
        /// <summary>
        /// Tentative d'ouvrir un menu. Retourne true si le menu peut être ouvert.
        /// </summary>
        public static bool TryOpenMenu(NativeMenu menu, string ownerModule)
        {
            // Si aucun menu n'est ouvert, on peut l'ouvrir
            if (CurrentOpenMenu == null || !CurrentOpenMenu.Visible)
            {
                CurrentOpenMenu = menu;
                CurrentMenuOwner = ownerModule;
                return true;
            }
            
            // Si c'est le même module qui veut ouvrir un autre menu, on ferme l'ancien
            if (CurrentMenuOwner == ownerModule)
            {
                CurrentOpenMenu.Visible = false;
                CurrentOpenMenu = menu;
                CurrentMenuOwner = ownerModule;
                return true;
            }
            
            // Sinon, un autre menu est déjà ouvert
            return false;
        }
        
        /// <summary>
        /// Fermer le menu actuel si il appartient au module spécifié
        /// </summary>
        public static void CloseMenuIfOwner(string ownerModule)
        {
            if (CurrentMenuOwner == ownerModule && CurrentOpenMenu != null)
            {
                CurrentOpenMenu.Visible = false;
                CurrentOpenMenu = null;
                CurrentMenuOwner = null;
            }
        }
        
        /// <summary>
        /// Fermer le menu actuel peu importe le propriétaire
        /// </summary>
        public static void ForceCloseMenu()
        {
            if (CurrentOpenMenu != null)
            {
                CurrentOpenMenu.Visible = false;
                CurrentOpenMenu = null;
                CurrentMenuOwner = null;
            }
        }
        
        /// <summary>
        /// Vérifier si un menu est actuellement ouvert
        /// </summary>
        public static bool IsAnyMenuOpen()
        {
            return CurrentOpenMenu != null && CurrentOpenMenu.Visible;
        }
        
        /// <summary>
        /// Mettre à jour l'état des menus (à appeler dans chaque tick)
        /// </summary>
        public static void UpdateMenuState()
        {
            // Si le menu actuel n'est plus visible, on nettoie
            if (CurrentOpenMenu != null && !CurrentOpenMenu.Visible)
            {
                CurrentOpenMenu = null;
                CurrentMenuOwner = null;
            }
        }
        
        /// <summary>
        /// Ajouter un PNJ à la liste des partenaires gérés
        /// </summary>
        public static void AddManagedPartner(Ped ped)
        {
            if (ped != null && ped.Exists())
            {
                ManagedPartnerPeds.Add(ped.Handle);
            }
        }
        
        /// <summary>
        /// Supprimer un PNJ de la liste des partenaires gérés
        /// </summary>
        public static void RemoveManagedPartner(Ped ped)
        {
            if (ped != null)
            {
                ManagedPartnerPeds.Remove(ped.Handle);
            }
        }
        
        /// <summary>
        /// Vérifier si un PNJ est géré par le PartnerModule
        /// </summary>
        public static bool IsManagedPartner(Ped ped)
        {
            if (ped == null || !ped.Exists()) return false;
            return ManagedPartnerPeds.Contains(ped.Handle);
        }
        
        /// <summary>
        /// Nettoyer les références mortes (à appeler périodiquement)
        /// </summary>
        public static void CleanupDeadReferences()
        {
            // Pour simplifier, on nettoie quand le partenaire actuel meurt
            if (CurrentPartner != null && (!CurrentPartner.Exists() || CurrentPartner.IsDead))
            {
                ManagedPartnerPeds.Clear();
                CurrentPartner = null;
            }
        }
    }
} 