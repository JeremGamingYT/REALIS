using System;
using GTA;
using GTA.Math;

namespace REALIS.Police.Callouts
{
    /// <summary>
    /// Callout de test simple pour vérifier le système
    /// </summary>
    public class TestCallout : CalloutBase
    {
        private bool _completed;
        private DateTime _startTime;

        public TestCallout() : base("TEST CALLOUT", 
            "Callout de test pour vérifier le système", 
            new Vector3(0f, 0f, 72f)) // Centre de Los Santos
        {
        }

        public override bool CanSpawn()
        {
            return true; // Toujours disponible
        }

        protected override void OnStart()
        {
            _startTime = DateTime.Now;
            _completed = false;
            
            GTA.UI.Notification.Show("~g~TEST CALLOUT DÉMARRÉ!~w~~n~Système de callouts fonctionnel!");
            
            // Créer un blip à la position
            var blip = World.CreateBlip(StartPosition);
            blip.Sprite = BlipSprite.Standard;
            blip.Color = BlipColor.Green;
            blip.Name = "Test Location";
        }

        protected override void OnUpdate()
        {
            var player = Game.Player.Character;
            
            // Si le joueur est proche de la position
            if (player.Position.DistanceTo(StartPosition) < 5f && !_completed)
            {
                GTA.UI.Notification.Show("~g~TEST RÉUSSI!~w~~n~Vous avez atteint la position!");
                _completed = true;
                
                // Terminer après 3 secondes
                Script.Wait(3000);
                End();
            }
            
            // Auto-terminer après 2 minutes
            if ((DateTime.Now - _startTime).TotalMinutes > 2)
            {
                GTA.UI.Notification.Show("~y~Test callout terminé automatiquement");
                End();
            }
            
            // Afficher les instructions
            if (!_completed)
            {
                GTA.UI.Screen.ShowHelpTextThisFrame("~b~TEST CALLOUT~w~~n~Rendez-vous au point marqué pour compléter le test");
            }
        }

        protected override void OnEnd()
        {
            GTA.UI.Notification.Show("~g~Test callout terminé!");
        }
    }
} 