using System;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.UI;
using GTA.Native;
using REALIS.Common;

namespace REALIS.Missions
{
    /// <summary>
    /// Classe utilitaire pour les effets spéciaux et cinématiques des missions
    /// </summary>
    public static class MissionEffects
    {
        /// <summary>
        /// Effectue un fade out puis fade in avec un délai
        /// </summary>
        public static void DoFadeTransition(int fadeOutTime = 1000, int waitTime = 2000, int fadeInTime = 1000)
        {
            Function.Call(Hash.DO_SCREEN_FADE_OUT, fadeOutTime);
            GameScheduler.Schedule(() => Function.Call(Hash.DO_SCREEN_FADE_IN, fadeInTime), fadeOutTime + waitTime);
        }
        
        /// <summary>
        /// Crée un effet de slow motion temporaire
        /// </summary>
        public static void DoSlowMotionEffect(float slowFactor = 0.3f, int duration = 3000)
        {
            Function.Call(Hash.SET_TIME_SCALE, slowFactor);
            GameScheduler.Schedule(() => Function.Call(Hash.SET_TIME_SCALE, 1.0f), duration);
        }
        
        /// <summary>
        /// Ajoute un effet de secousse de caméra
        /// </summary>
        public static void ShakeCamera(string shakeType = "SMALL_EXPLOSION_SHAKE", float intensity = 1.0f, int duration = 1000)
        {
            Function.Call(Hash.SHAKE_GAMEPLAY_CAM, shakeType, intensity);
            GameScheduler.Schedule(() => Function.Call(Hash.STOP_GAMEPLAY_CAM_SHAKING, true), duration);
        }
        
        /// <summary>
        /// Crée un effet de flash coloré sur l'écran
        /// </summary>
        public static void DoColorFlash(int r, int g, int b, int alpha, int duration = 500)
        {
            // Effet de flash simplifié via fade out/in
            Function.Call(Hash.DO_SCREEN_FADE_OUT, duration / 2);
            GameScheduler.Schedule(() => Function.Call(Hash.DO_SCREEN_FADE_IN, duration / 2), duration);
        }
        
        /// <summary>
        /// Joue un effet sonore 3D à une position
        /// </summary>
        public static void PlaySoundAt(Vector3 position, string soundName, string soundSet)
        {
            Function.Call(Hash.PLAY_SOUND_FROM_COORD, -1, soundName, position.X, position.Y, position.Z, soundSet, 0, 0, 0);
        }
        
        /// <summary>
        /// Crée un effet de particules à une position
        /// </summary>
        public static void CreateParticleEffect(Vector3 position, string effectName, string assetName = "core")
        {
            // Charger l'asset puis déclencher l'effet dès qu'il est prêt, sans bloquer le thread
            Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, assetName);

            void trySpawn()
            {
                if (Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, assetName))
                {
                    Function.Call(Hash.USE_PARTICLE_FX_ASSET, assetName);
                    Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD, effectName, position.X, position.Y, position.Z, 0, 0, 0, 1.0f, false, false, false);
                }
                else
                {
                    // Réessayer dans 50 ms jusqu'à ce que l'asset soit chargé
                    GameScheduler.Schedule(trySpawn, 50);
                }
            }

            // Première tentative immédiatement (elle re-planifiera si nécessaire)
            trySpawn();
        }
        
        /// <summary>
        /// Affiche un texte avec effet de typewriter
        /// </summary>
        public static void ShowTypewriterText(string text, float x = 0.5f, float y = 0.5f, float scale = 0.8f, int charDelay = 50)
        {
            // Affichage immédiat (le typewriter bloquant est supprimé pour éviter Wait)
            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
            Function.Call(Hash.SET_TEXT_CENTRE, true);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }
        
        /// <summary>
        /// Crée un effet de radar pulsant pour un blip
        /// </summary>
        public static void PulseBlip(Blip blip, int pulseCount = 3, int pulseDelay = 500)
        {
            if (blip == null || !blip.Exists()) return;
            
            for (int i = 0; i < pulseCount; i++)
            {
                GameScheduler.Schedule(() =>
                {
                    if (blip != null && blip.Exists())
                        Function.Call(Hash.PULSE_BLIP, blip);
                }, i * pulseDelay);
            }
        }
        
        /// <summary>
        /// Affiche une notification stylisée avec son
        /// </summary>
        public static void ShowStylizedNotification(string title, string message, NotificationIcon icon, string soundName = "CHALLENGE_UNLOCKED", string soundSet = "HUD_AWARDS")
        {
            // Jouer le son d'abord
            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, soundName, soundSet, 0);
            
            // Afficher la notification
            Notification.Show($"~b~{title}\n{message}");
        }
        
        /// <summary>
        /// Crée un effet de zoom cinématique sur un point
        /// </summary>
        public static void DoCinematicZoom(Vector3 targetPosition, int duration = 3000)
        {
#pragma warning disable CS0618 // Utilisation d'API marquée obsolète mais toujours fonctionnelle
            var camera = World.CreateCamera(targetPosition + new Vector3(0, 0, 50), Vector3.Zero, 60f);

            // Activer la caméra immédiatement
            World.RenderingCamera = camera;
#pragma warning restore CS0618

            // Forcer un fade-in si nécessaire
            if (Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT))
            {
                Function.Call(Hash.DO_SCREEN_FADE_IN, 500);
            }

            // Nombre d'étapes de l'animation (50 ms par étape)
            const int stepMs = 50;
            int steps = Math.Max(1, duration / stepMs);

            for (int i = 0; i < steps; i++)
            {
                int localStep = i; // Copie locale pour la capture du lambda
                GameScheduler.Schedule(() =>
                {
                    float progress = (float)localStep / steps;
                    var newPos = Vector3.Lerp(targetPosition + new Vector3(0, 0, 50), targetPosition + new Vector3(0, 0, 5), progress);
                    camera.Position = newPos;
                    camera.PointAt(targetPosition);
                }, localStep * stepMs);
            }

            // Planifier la fin de l'animation + cleanup
            GameScheduler.Schedule(() =>
            {
                // Petit fondu pour repasser sans heurt puis restauration
                Function.Call(Hash.DO_SCREEN_FADE_OUT, 300);
                GameScheduler.Schedule(() => Function.Call(Hash.DO_SCREEN_FADE_IN, 300), 300);

#pragma warning disable CS0618
                World.RenderingCamera = null;
#pragma warning restore CS0618
                camera.Delete();
            }, duration + 50); // Légère marge
        }
        
        /// <summary>
        /// Affiche une barre de progression animée
        /// </summary>
        public static void ShowProgressBar(string title, float progress, float x = 0.5f, float y = 0.1f)
        {
            // Dimensions de la barre (coordonnées normalisées autour de x,y)
            var barWidth = 0.3f;
            var barHeight = 0.03f;
            
            // Fond de la barre
            Function.Call(Hash.DRAW_RECT, x, y, barWidth, barHeight, 0, 0, 0, 180);
            
            // Barre de progression
            var filledWidth = barWidth * progress;
            Function.Call(Hash.DRAW_RECT, x, y, filledWidth, barHeight, 0, 255, 0, 200);
            
            // Bordure
            Function.Call(Hash.DRAW_RECT, x, y, barWidth, 0.002f, 255, 255, 255, 255); // Haut
            Function.Call(Hash.DRAW_RECT, x, y + barHeight, barWidth, 0.002f, 255, 255, 255, 255); // Bas
            Function.Call(Hash.DRAW_RECT, x - barWidth/2, y, 0.002f, barHeight, 255, 255, 255, 255); // Gauche
            Function.Call(Hash.DRAW_RECT, x + barWidth/2, y, 0.002f, barHeight, 255, 255, 255, 255); // Droite
            
            // Titre
            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_SCALE, 0.5f, 0.5f);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
            Function.Call(Hash.SET_TEXT_CENTRE, true);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, title);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y - 0.05f);
            
            // Pourcentage
            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_SCALE, 0.4f, 0.4f);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
            Function.Call(Hash.SET_TEXT_CENTRE, true);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"{(int)(progress * 100)}%");
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y + 0.04f);
        }
        
        /// <summary>
        /// Crée un effet de vague de choc visuelle
        /// </summary>
        public static void CreateShockwave(Vector3 center, float maxRadius = 20f, int duration = 2000)
        {
            // Implémentation simplifiée non bloquante : déclenche quelques cercles espacés
            int steps = duration / 200;
            for (int i = 0; i < steps; i++)
            {
                int localStep = i;
                GameScheduler.Schedule(() =>
                {
                    float progress = (float)localStep / steps;
                    float currentRadius = maxRadius * progress;
                    for (int angle = 0; angle < 360; angle += 30)
                    {
                        double radian = angle * Math.PI / 180;
                        var pos = center + new Vector3(
                            (float)(Math.Cos(radian) * currentRadius),
                            (float)(Math.Sin(radian) * currentRadius),
                            0);
                        CreateParticleEffect(pos, "exp_grd_bzgas_smoke");
                    }
                }, localStep * 200);
            }
        }
    }
} 