using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.Math;
using GTA.UI;

namespace REALIS.Core
{
    public class TestlaxController : Script
    {
        private bool isAutoPilotActive = false;
        private bool isNavigatingToWaypoint = false;
        private Vehicle? currentVehicle;
        private Ped? player;
        private HashSet<int> debuggedVehicles = new HashSet<int>();
        
        // Système d'IA Tesla fluide
        private DateTime lastObstacleCheck = DateTime.MinValue;
        private DateTime alertShownTime = DateTime.MinValue;
        private bool isWaitingForObstacle = false;
        private Vector3 lastWaypointPos = Vector3.Zero;
        private float targetSpeed = 50f;
        private float currentThrottle = 0f;
        private float currentSteering = 0f;
        private bool isOvertaking = false;
        private DateTime overtakeStartTime = DateTime.MinValue;
        
        public TestlaxController()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            
            // Afficher les instructions au joueur
            Notification.PostTicker("~g~Teslax Controller chargé!~n~~w~J: Autopilot ON/OFF~n~N: Aller au waypoint", true);
        }

        private void OnTick(object sender, EventArgs e)
        {
            player = Game.Player.Character;
            
            // Vérifier si le joueur est dans un véhicule
            if (player.IsInVehicle())
            {
                currentVehicle = player.CurrentVehicle;
                
                // Vérifier si c'est le véhicule "testlax"
                if (IsTestlaxVehicle(currentVehicle))
                {
                    // Gérer l'autopilot
                    if (isAutoPilotActive)
                    {
                        HandleAutoPilot();
                        HandleTeslaAI(); // IA Tesla avancée
                    }
                    
                    // Gérer la navigation vers waypoint
                    if (isNavigatingToWaypoint)
                    {
                        HandleWaypointNavigation();
                        HandleTeslaAI(); // IA Tesla avancée
                        HandleWaypointAssistance(); // Assistance pour relancer automatiquement
                    }
                }
            }
            else
            {
                // Désactiver les modes si le joueur n'est plus dans le véhicule
                if (isAutoPilotActive || isNavigatingToWaypoint)
                {
                    DeactivateAllModes();
                }
            }
        }

        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            // Vérifier les touches même si pas dans le bon véhicule (pour debug)
            if (e.KeyCode == System.Windows.Forms.Keys.J || e.KeyCode == System.Windows.Forms.Keys.N)
            {
                if (player?.IsInVehicle() != true)
                {
                    Notification.PostTicker("~r~Vous devez être dans un véhicule!", true);
                    return;
                }

                if (!IsTestlaxVehicle(currentVehicle))
                {
                    Notification.PostTicker("~r~Ce n'est pas un véhicule Teslax!", true);
                    return;
                }

                // Si on arrive ici, c'est le bon véhicule
                switch (e.KeyCode)
                {
                    case System.Windows.Forms.Keys.J:
                        ToggleAutoPilot();
                        break;
                    
                    case System.Windows.Forms.Keys.N:
                        ToggleWaypointNavigation();
                        break;
                }
            }
        }

        private bool IsTestlaxVehicle(Vehicle? vehicle)
        {
            if (vehicle == null) return false;
            
            try
            {
                // Méthode 1: Vérifier le nom exact du modèle
                string modelName = vehicle.Model.ToString().ToLower();
                if (modelName == "teslax")
                {
                    return true;
                }
                
                // Méthode 2: Chercher dans le nom si il contient "testlax"
                if (modelName.Contains("teslax"))
                {
                    return true;
                }
                
                // Méthode 3: Vérifier le DisplayName si disponible
                string displayName = vehicle.DisplayName.ToLower();
                if (displayName.Contains("teslax"))
                {
                    return true;
                }
                
                // Debug: Afficher le nom du véhicule pour diagnostic (seulement une fois par véhicule)
                if (currentVehicle != null && !debuggedVehicles.Contains(currentVehicle.Handle))
                {
                    debuggedVehicles.Add(currentVehicle.Handle);
                    Notification.PostTicker($"~y~Véhicule: {modelName} | Display: {displayName}", true);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                // En cas d'erreur, log et retourner false
                Notification.PostTicker($"~r~Erreur détection véhicule: {ex.Message}", true);
                return false;
            }
        }

        private void ToggleAutoPilot()
        {
            isAutoPilotActive = !isAutoPilotActive;
            
            if (isAutoPilotActive)
            {
                // Désactiver la navigation waypoint si elle est active
                isNavigatingToWaypoint = false;
                
                // Activer l'autopilot avec les natives
                if (currentVehicle != null && player != null)
                {
                    // Utiliser les natives pour activer l'autopilot du véhicule
                    Function.Call(Hash.SET_VEHICLE_ENGINE_ON, currentVehicle, true, true, false);
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, player, currentVehicle, 50f, (int)VehicleDrivingFlags.StopForVehicles);
                    Notification.PostTicker("~g~Autopilot activé!", true);
                }
            }
            else
            {
                // Désactiver l'autopilot
                if (player != null)
                {
                    Function.Call(Hash.CLEAR_PED_TASKS, player);
                }
                Notification.PostTicker("~r~Autopilot désactivé!", true);
            }
        }

        private void ToggleWaypointNavigation()
        {
            // Vérifier s'il y a un waypoint actif
            if (!Game.IsWaypointActive)
            {
                Notification.PostTicker("~r~Aucun waypoint défini! Placez un waypoint sur la carte d'abord.", true);
                return;
            }

            isNavigatingToWaypoint = !isNavigatingToWaypoint;
            
            if (isNavigatingToWaypoint)
            {
                // Désactiver l'autopilot si il est actif
                isAutoPilotActive = false;
                
                // Commencer la navigation vers le waypoint
                Vector3 waypointPos = GetWaypointPosition();
                
                if (currentVehicle != null && player != null && waypointPos != Vector3.Zero)
                {
                    // Utiliser les natives pour aller au waypoint
                    Function.Call(Hash.SET_VEHICLE_ENGINE_ON, currentVehicle, true, true, false);
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, player, currentVehicle, 
                        waypointPos.X, waypointPos.Y, waypointPos.Z, 50f, 0, 
                        currentVehicle.Model.Hash, (int)VehicleDrivingFlags.StopForVehicles, 15f, -1f);
                    Notification.PostTicker("~g~Navigation vers waypoint activée!", true);
                }
            }
            else
            {
                // Arrêter la navigation
                if (player != null)
                {
                    Function.Call(Hash.CLEAR_PED_TASKS, player);
                }
                Notification.PostTicker("~r~Navigation vers waypoint désactivée!", true);
            }
        }

        private Vector3 GetWaypointPosition()
        {
            // Utiliser les natives pour obtenir la position du waypoint
            if (Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
            {
                Vector3 coords = Function.Call<Vector3>(Hash.GET_BLIP_COORDS, Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, 8));
                coords.Z = World.GetGroundHeight(coords, out float groundZ, GetGroundHeightMode.Normal) ? groundZ : coords.Z;
                return coords;
            }
            return Vector3.Zero;
        }

        private void HandleAutoPilot()
        {
            // Vérifier si l'autopilot est encore actif et si le véhicule s'est arrêté
            if (currentVehicle != null && player != null)
            {
                // Vérifier si la tâche est encore active avec les natives
                bool hasTask = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, player, 151); // 151 = TASK_VEHICLE_DRIVE_WANDER
                
                // Si le véhicule est arrêté et qu'il n'y a pas de tâche active, relancer l'autopilot
                if (currentVehicle.Speed < 2f && !hasTask)
                {
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, player, currentVehicle, 50f, (int)VehicleDrivingFlags.StopForVehicles);
                }
            }
        }

        private void HandleWaypointNavigation()
        {
            if (currentVehicle == null || player == null) return;
            
            // Vérifier si on est arrivé au waypoint
            Vector3 waypointPos = GetWaypointPosition();
            if (waypointPos == Vector3.Zero)
            {
                // Le waypoint a été supprimé
                isNavigatingToWaypoint = false;
                Function.Call(Hash.CLEAR_PED_TASKS, player);
                Notification.PostTicker("~r~Waypoint supprimé, navigation arrêtée!", true);
                return;
            }
            
            float distance = Vector3.Distance(currentVehicle.Position, waypointPos);
            
            if (distance < 15f) // Arrivé au waypoint
            {
                isNavigatingToWaypoint = false;
                Function.Call(Hash.CLEAR_PED_TASKS, player);
                Notification.PostTicker("~g~Waypoint atteint!", true);
            }
            else
            {
                // Vérifier si la tâche de navigation est encore active
                bool hasTask = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, player, 16); // 16 = TASK_VEHICLE_DRIVE_TO_COORD
                
                // Relancer la navigation si la tâche s'est arrêtée
                if (!hasTask && currentVehicle.Speed < 2f)
                {
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, player, currentVehicle, 
                        waypointPos.X, waypointPos.Y, waypointPos.Z, 50f, 0, 
                        currentVehicle.Model.Hash, (int)VehicleDrivingFlags.StopForVehicles, 15f, -1f);
                }
            }
        }

        private void DeactivateAllModes()
        {
            isAutoPilotActive = false;
            isNavigatingToWaypoint = false;
            isWaitingForObstacle = false;
            
            if (player != null)
            {
                Function.Call(Hash.CLEAR_PED_TASKS, player);
            }
        }

        // Système d'IA Tesla fluide et réaliste
        private void HandleTeslaAI()
        {
            if (currentVehicle == null || player == null) return;
            
            // Vérifier les obstacles toutes les 200ms pour plus de fluidité
            if ((DateTime.Now - lastObstacleCheck).TotalMilliseconds > 200)
            {
                lastObstacleCheck = DateTime.Now;
                CheckForObstaclesSmoothly();
            }
            
            // Contrôle fluide de la vitesse et direction
            ApplySmoothControls();
            
            // Gérer les dépassements fluides
            HandleSmoothOvertaking();
        }

        private void CheckForObstaclesSmoothly()
        {
            if (currentVehicle == null || player == null) return;
            
            Vector3 vehiclePos = currentVehicle.Position;
            Vector3 forwardVector = currentVehicle.ForwardVector;
            
            float closestObstacleDistance = 100f;
            bool obstacleDetected = false;
            
            // Vérifier les véhicules proches devant
            Vehicle[] nearbyVehicles = World.GetNearbyVehicles(vehiclePos, 30f);
            foreach (var vehicle in nearbyVehicles)
            {
                if (vehicle == currentVehicle || !vehicle.Exists()) continue;
                
                Vector3 relativePos = vehicle.Position - vehiclePos;
                float distanceAhead = Vector3.Dot(relativePos, forwardVector);
                
                // Si véhicule devant dans notre trajectoire
                if (distanceAhead > 0 && distanceAhead < 25f && Math.Abs(relativePos.X) < 4f)
                {
                    closestObstacleDistance = Math.Min(closestObstacleDistance, distanceAhead);
                    obstacleDetected = true;
                }
            }
            
            // Vérifier les piétons proches
            Ped[] nearbyPeds = World.GetNearbyPeds(vehiclePos, 20f);
            foreach (var ped in nearbyPeds)
            {
                if (ped == player || !ped.Exists()) continue;
                
                Vector3 relativePos = ped.Position - vehiclePos;
                float distanceAhead = Vector3.Dot(relativePos, forwardVector);
                
                // Si piéton devant dans notre trajectoire
                if (distanceAhead > 0 && distanceAhead < 15f && Math.Abs(relativePos.X) < 3f)
                {
                    closestObstacleDistance = Math.Min(closestObstacleDistance, distanceAhead);
                    obstacleDetected = true;
                    
                    if (distanceAhead < 8f && (DateTime.Now - alertShownTime).TotalSeconds > 2)
                    {
                        Notification.PostTicker("🚨 ALERTE: Piéton proche - Freinage adaptatif", true);
                        alertShownTime = DateTime.Now;
                    }
                }
            }
            
            // Adapter la vitesse selon la distance de l'obstacle
            if (obstacleDetected)
            {
                if (closestObstacleDistance < 10f)
                {
                    targetSpeed = 0f; // Arrêt
                    isWaitingForObstacle = true;
                }
                else if (closestObstacleDistance < 20f)
                {
                    targetSpeed = 20f; // Ralentissement
                }
                else
                {
                    targetSpeed = 35f; // Vitesse réduite
                }
            }
            else
            {
                targetSpeed = 50f; // Vitesse normale
                if (isWaitingForObstacle)
                {
                    isWaitingForObstacle = false;
                    Notification.PostTicker("✅ Tesla: Reprise de la vitesse normale", true);
                }
            }
        }

        private void ApplySmoothControls()
        {
            if (currentVehicle == null || player == null) return;
            
            // Contrôle fluide de l'accélération/freinage
            float speedDifference = targetSpeed - currentVehicle.Speed;
            
            if (Math.Abs(speedDifference) > 2f)
            {
                if (speedDifference > 0)
                {
                    // Accélération progressive
                    currentThrottle = Math.Min(1f, currentThrottle + 0.02f);
                    Function.Call(Hash.SET_CONTROL_VALUE_NEXT_FRAME, 0, (int)Control.VehicleAccelerate, currentThrottle);
                }
                else
                {
                    // Freinage progressif
                    currentThrottle = Math.Max(0f, currentThrottle - 0.05f);
                    Function.Call(Hash.SET_CONTROL_VALUE_NEXT_FRAME, 0, (int)Control.VehicleBrake, Math.Min(1f, -speedDifference / 20f));
                }
            }
            else
            {
                // Maintenir la vitesse
                currentThrottle = 0.3f;
                Function.Call(Hash.SET_CONTROL_VALUE_NEXT_FRAME, 0, (int)Control.VehicleAccelerate, currentThrottle);
            }
        }

        private void HandleSmoothOvertaking()
        {
            if (currentVehicle == null || isWaitingForObstacle) return;
            
            Vector3 vehiclePos = currentVehicle.Position;
            Vector3 forwardVector = currentVehicle.ForwardVector;
            
            // Si on est en train de dépasser, continuer la manœuvre
            if (isOvertaking)
            {
                ContinueOvertaking();
                return;
            }
            
            // Détecter si on est bloqué derrière un véhicule lent
            Vehicle[] nearbyVehicles = World.GetNearbyVehicles(vehiclePos, 25f);
            foreach (var vehicle in nearbyVehicles)
            {
                if (vehicle == currentVehicle || !vehicle.Exists()) continue;
                
                Vector3 relativePos = vehicle.Position - vehiclePos;
                float distanceAhead = Vector3.Dot(relativePos, forwardVector);
                
                // Véhicule devant qui roule lentement
                if (distanceAhead > 10f && distanceAhead < 25f && 
                    vehicle.Speed < 20f && currentVehicle.Speed < 15f)
                {
                    // Essayer de dépasser de manière fluide
                    StartSmoothOvertake();
                    break;
                }
            }
        }

        private void StartSmoothOvertake()
        {
            if (currentVehicle == null || player == null || isOvertaking) return;
            
            Vector3 vehiclePos = currentVehicle.Position;
            Vector3 rightVector = currentVehicle.RightVector;
            
            // Vérifier la voie de droite pour dépasser
            Vector3 overtakePos = vehiclePos + rightVector * 4f;
            
            // Vérifier qu'il n'y a pas d'obstacle sur la voie de dépassement
            bool canOvertake = true;
            Vehicle[] vehicles = World.GetNearbyVehicles(overtakePos, 20f);
            
            foreach (var vehicle in vehicles)
            {
                if (vehicle == currentVehicle) continue;
                if (Vector3.Distance(vehicle.Position, overtakePos) < 12f)
                {
                    canOvertake = false;
                    break;
                }
            }
            
            if (canOvertake)
            {
                isOvertaking = true;
                overtakeStartTime = DateTime.Now;
                targetSpeed = 60f; // Accélérer pour dépasser
                Notification.PostTicker("🚗 Tesla: Dépassement initié", true);
            }
        }

        private void ContinueOvertaking()
        {
            if (currentVehicle == null) return;
            
            double overtakeDuration = (DateTime.Now - overtakeStartTime).TotalSeconds;
            
            if (overtakeDuration < 3f)
            {
                // Phase 1: Tourner à droite progressivement
                currentSteering = Math.Min(0.3f, currentSteering + 0.02f);
                Function.Call(Hash.SET_CONTROL_VALUE_NEXT_FRAME, 0, (int)Control.VehicleMoveLeftRight, currentSteering);
            }
            else if (overtakeDuration < 6f)
            {
                // Phase 2: Maintenir la trajectoire de dépassement
                currentSteering = 0.1f;
                Function.Call(Hash.SET_CONTROL_VALUE_NEXT_FRAME, 0, (int)Control.VehicleMoveLeftRight, currentSteering);
            }
            else if (overtakeDuration < 9f)
            {
                // Phase 3: Revenir dans la voie
                currentSteering = Math.Max(-0.3f, currentSteering - 0.02f);
                Function.Call(Hash.SET_CONTROL_VALUE_NEXT_FRAME, 0, (int)Control.VehicleMoveLeftRight, currentSteering);
            }
            else
            {
                // Fin du dépassement
                isOvertaking = false;
                currentSteering = 0f;
                targetSpeed = 50f;
                Notification.PostTicker("✅ Tesla: Dépassement terminé", true);
            }
        }

        private void HandleWaypointAssistance()
        {
            if (currentVehicle == null || player == null) return;
            
            Vector3 waypointPos = GetWaypointPosition();
            if (waypointPos == Vector3.Zero) return;
            
            // Si le waypoint a changé, relancer automatiquement
            if (Vector3.Distance(waypointPos, lastWaypointPos) > 5f)
            {
                lastWaypointPos = waypointPos;
                RestartWaypointNavigation();
            }
            
            // Si le véhicule est arrêté depuis trop longtemps, relancer
            if (currentVehicle.Speed < 2f && !isWaitingForObstacle)
            {
                RestartWaypointNavigation();
            }
        }

        private void RestartWaypointNavigation()
        {
            if (currentVehicle == null || player == null) return;
            
            Vector3 waypointPos = GetWaypointPosition();
            if (waypointPos != Vector3.Zero)
            {
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, currentVehicle, true, true, false);
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, player, currentVehicle, 
                    waypointPos.X, waypointPos.Y, waypointPos.Z, 50f, 0, 
                    currentVehicle.Model.Hash, (int)VehicleDrivingFlags.StopForVehicles, 15f, -1f);
            }
        }
    }
} 