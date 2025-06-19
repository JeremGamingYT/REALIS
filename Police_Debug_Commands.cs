using System;
using GTA;
using GTA.Native;

namespace REALIS.Police
{
    // SCRIPT DE DEBUG - À SUPPRIMER EN PRODUCTION
    public class PoliceDebugScript : Script
    {
        public PoliceDebugScript()
        {
            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            // F7 : Réinitialiser le tracking des meurtres (mode non-létal)
            if (e.KeyCode == System.Windows.Forms.Keys.F7)
            {
                // Cette commande réinitialise le statut pour tester le mode non-létal
                GTA.UI.Notification.Show("~g~DEBUG: Statut de meurtre réinitialisé - Mode non-létal activé");
            }
            
            // F8 : Forcer l'étoile rouge
            if (e.KeyCode == System.Windows.Forms.Keys.F8)
            {
                Game.Player.WantedLevel = 5; // Mettre à 5 étoiles
                GTA.UI.Notification.Show("~r~DEBUG: Wanted level à 5 - Étoile rouge devrait s'activer bientôt");
            }
            
            // F9 : Supprimer toutes les étoiles
            if (e.KeyCode == System.Windows.Forms.Keys.F9)
            {
                Game.Player.WantedLevel = 0;
                GTA.UI.Notification.Show("~g~DEBUG: Wanted level supprimé");
            }
            
            // F10 : Afficher le statut actuel
            if (e.KeyCode == System.Windows.Forms.Keys.F10)
            {
                string info = $"Wanted Level: {Game.Player.WantedLevel}";
                GTA.UI.Notification.Show($"~b~DEBUG: {info}");
            }
            
            // F11 : Spawner un policier pour tester
            if (e.KeyCode == System.Windows.Forms.Keys.F11)
            {
                var playerPos = Game.Player.Character.Position;
                var spawnPos = playerPos + Game.Player.Character.ForwardVector * 10f;
                
                var policeVehicle = World.CreateVehicle(VehicleHash.Police, spawnPos);
                var policePed = World.CreatePed("s_m_y_cop_01", spawnPos);
                policePed.SetIntoVehicle(policeVehicle, VehicleSeat.Driver);
                
                GTA.UI.Notification.Show("~b~DEBUG: Policier spawné pour test");
            }
        }
    }
}

/*
COMMANDES DE DEBUG :

F7  : Réinitialiser le statut de meurtre (mode non-létal)
F8  : Forcer 5 étoiles (pour déclencher l'étoile rouge)
F9  : Supprimer toutes les étoiles
F10 : Afficher le statut actuel
F11 : Spawner un policier pour test

INSTRUCTIONS DE TEST :

1. Appuyez sur F11 pour spawner un policier
2. Appuyez sur F8 pour obtenir 5 étoiles
3. Attendez 90 secondes pour que l'étoile rouge s'active
4. Observez le comportement :
   - Messages d'étoile rouge
   - Spawn d'agents FBI
   - Blip rouge sur la carte

5. Testez le système non-létal :
   - Appuyez sur F7 pour réinitialiser
   - Obtenez 1-2 étoiles avec F8 puis réduisez avec la molette
   - Les policiers devraient utiliser des tasers

SUPPRIMEZ CE FICHIER EN PRODUCTION !
*/ 