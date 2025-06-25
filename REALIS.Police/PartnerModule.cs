using System;
using GTA;
using GTA.Native;
using GTA.Math;
using REALIS.Common;
using LemonUI;
using LemonUI.Menus;

namespace REALIS.Police
{
    /// <summary>
    /// Permet au joueur policier de recruter un agent solo comme partenaire (touche E près d'un policier).
    /// Le partenaire suit le joueur, entre/sort du véhicule et aide en combat.
    /// </summary>
    public class PartnerModule : IModule
    {
        private Ped partner;
        private const GTA.Control RecruitKey = GTA.Control.Context; // E par défaut
        private const float RecruitRange = 3f;
        private const float InteractionRange = 8f;

        // UI LemonUI
        private ObjectPool uiPool;
        private NativeMenu partnerMenu;
        private Ped nearbyOfficer;
        
        // Tracking pour éviter que le partenaire vole le véhicule
        private Vehicle lastPlayerVehicle;
        private DateTime lastVehicleExitTime = DateTime.MinValue;
        
        // Surveillance du partenaire
        private Vector3 lastPartnerPosition = Vector3.Zero;
        private DateTime lastPositionCheck = DateTime.MinValue;

        public void Initialize() 
        { 
            uiPool = new ObjectPool();
        }

        public void Update()
        {
            if (!DutyState.PoliceOnDuty) return;

            Ped player = Game.Player.Character;
            if (!player.Exists() || player.IsDead) return;

            // Mettre à jour l'état des menus
            PoliceSharedData.UpdateMenuState();

            // Nettoyer les références mortes périodiquement
            PoliceSharedData.CleanupDeadReferences();

            // Vérifier et corriger les relations avec la police
            FixPoliceRelations(player);

            // Chercher des officiers à proximité
            FindNearbyOfficer(player);

            // Gestion interaction menu partenaire
            HandlePartnerInteraction(player);

            // Process UI seulement si ce module en possède un ouvert
            if (PoliceSharedData.CurrentMenuOwner == "PartnerModule")
            {
                uiPool?.Process();
            }

            // Màj comportement partenaire existant
            UpdatePartnerBehaviour(player);
            
            // Surveillance du partenaire pour détecter les comportements anormaux
            MonitorPartnerSafety(player);
        }

        public void Dispose()
        {
            if (partner != null && partner.Exists())
            {
                ClearPartnerTasks(partner);
                partner = null;
            }
            if (partnerMenu != null)
                partnerMenu.Visible = false;
        }

        private void FindNearbyOfficer(Ped player)
        {
            nearbyOfficer = null;

            // Chercher le policier le plus proche
            Ped[] nearbyPeds = World.GetNearbyPeds(player, InteractionRange);
            float closestDist = InteractionRange;

            foreach (Ped ped in nearbyPeds)
            {
                if (ped == null || !ped.Exists() || ped == player || ped == partner) continue;
                if (!IsCop(ped) || ped.IsDead || ped.IsInVehicle()) continue;

                float dist = player.Position.DistanceTo(ped.Position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    nearbyOfficer = ped;
                }
            }
        }

        private void HandlePartnerInteraction(Ped player)
        {
            if (nearbyOfficer != null && nearbyOfficer.Exists())
            {
                float dist = player.Position.DistanceTo(nearbyOfficer.Position);
                if (dist <= RecruitRange)
                {
                    // Afficher l'aide contextuelle
                    if (partner == null)
                    {
                        GTA.UI.Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Parler à l'officier");
                    }
                    else
                    {
                        GTA.UI.Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Gérer le partenaire");
                    }

                    // Ouvrir le menu
                    if (Game.IsControlJustPressed(RecruitKey))
                    {
                        OpenPartnerMenu();
                    }
                }
            }
        }

        private void OpenPartnerMenu()
        {
            // Vérifier si on peut ouvrir le menu
            if (!PoliceSharedData.TryOpenMenu(null, "PartnerModule"))
            {
                GTA.UI.Notification.Show("~r~Un autre menu est déjà ouvert.");
                return;
            }

            if (partnerMenu != null)
            {
                PoliceSharedData.TryOpenMenu(partnerMenu, "PartnerModule");
                partnerMenu.Visible = true;
                return;
            }

            partnerMenu = new NativeMenu("Gestion Partenaire", "Options partenaire");

            if (partner == null)
            {
                partnerMenu.Add(new NativeItem("Recruter comme partenaire"));
                partnerMenu.Add(new NativeItem("Demander assistance"));
                partnerMenu.Add(new NativeItem("Poser une question"));
            }
            else
            {
                partnerMenu.Add(new NativeItem("Renvoyer le partenaire"));
                partnerMenu.Add(new NativeItem("Ordonner de rester ici"));
                partnerMenu.Add(new NativeItem("Ordonner de me suivre"));
                partnerMenu.Add(new NativeItem("Demander un rapport"));
            }

            partnerMenu.Add(new NativeItem("Annuler"));

            partnerMenu.ItemActivated += OnPartnerMenuItemActivated;
            partnerMenu.Closed += OnPartnerMenuClosed;
            uiPool.Add(partnerMenu);
            
            // Enregistrer et ouvrir le menu via le système centralisé
            if (PoliceSharedData.TryOpenMenu(partnerMenu, "PartnerModule"))
            {
                partnerMenu.Visible = true;
            }
        }

        private void OnPartnerMenuItemActivated(object sender, LemonUI.Menus.ItemActivatedArgs e)
        {
            int idx = partnerMenu.Items.IndexOf(e.Item);
            partnerMenu.Visible = false;
            PoliceSharedData.CloseMenuIfOwner("PartnerModule");

            if (partner == null)
            {
                switch (idx)
                {
                    case 0: // Recruter
                        if (nearbyOfficer != null && nearbyOfficer.Exists())
                        {
                            RecruitPartner(nearbyOfficer);
                        }
                        break;
                    case 1: // Assistance
                        if (nearbyOfficer != null && nearbyOfficer.Exists())
                        {
                            RequestAssistance(nearbyOfficer);
                        }
                        break;
                    case 2: // Question
                        GTA.UI.Notification.Show("~b~Officier: \"Tout va bien, chef !\"");
                        break;
                }
            }
            else
            {
                switch (idx)
                {
                    case 0: // Renvoyer
                        DismissPartner();
                        break;
                    case 1: // Rester ici
                        OrderStay();
                        break;
                    case 2: // Suivre
                        OrderFollow();
                        break;
                    case 3: // Rapport
                        GetReport();
                        break;
                }
            }
        }

        private void OnPartnerMenuClosed(object sender, System.EventArgs e)
        {
            PoliceSharedData.CloseMenuIfOwner("PartnerModule");
        }

        private void RecruitPartner(Ped officer)
        {
            partner = officer;
            GTA.UI.Notification.Show("~b~Officier recruté comme partenaire");
            SetFriendlyToPlayer(partner);
            partner.AlwaysKeepTask = true;
            
            // Configurer le partenaire pour éviter les comportements erratiques
            partner.CanRagdoll = false;
            partner.CanBeKnockedOffBike = false;
            partner.CanFlyThroughWindscreen = false;
            partner.CanBeDraggedOutOfVehicle = false;
            partner.CanBeTargetted = false;
            
            // Enregistrer dans les données partagées
            PoliceSharedData.CurrentPartner = partner;
            PoliceSharedData.AddManagedPartner(partner);
            
            // Faire saluer le nouveau partenaire
            partner.Task.PlayAnimation("gestures@m@standing@casual", "gesture_hello", 8f, -8f, 2000, AnimationFlags.None, 0f);
            GTA.UI.Notification.Show("~b~Partenaire: \"Prêt à vous suivre, chef !\"");
        }

        private void DismissPartner()
        {
            if (partner != null && partner.Exists())
            {
                GTA.UI.Notification.Show("~y~Partenaire renvoyé");
                ClearPartnerTasks(partner);
                partner.Task.PlayAnimation("gestures@m@standing@casual", "gesture_goodbye", 8f, -8f, 2000, AnimationFlags.None, 0f);
                
                // Nettoyer les données partagées
                PoliceSharedData.RemoveManagedPartner(partner);
                PoliceSharedData.CurrentPartner = null;
                
                partner = null;
            }
        }

        private void RequestAssistance(Ped officer)
        {
            GTA.UI.Notification.Show("~g~Assistance demandée");
            officer.Task.PlayAnimation("gestures@m@standing@casual", "gesture_point", 8f, -8f, 3000, AnimationFlags.None, 0f);
            
            // L'officier aide temporairement
            officer.Task.FollowToOffsetFromEntity(Game.Player.Character, new Vector3(1, -1, 0), 3f, 10000);
        }

        private void OrderStay()
        {
            if (partner != null && partner.Exists())
            {
                partner.Task.ClearAll();
                partner.Task.StandStill(-1);
                GTA.UI.Notification.Show("~b~Partenaire: \"Je reste en position !\"");
            }
        }

        private void OrderFollow()
        {
            if (partner != null && partner.Exists())
            {
                partner.Task.ClearAll();
                GTA.UI.Notification.Show("~b~Partenaire: \"Je vous suis !\"");
            }
        }

        private void GetReport()
        {
            if (partner != null && partner.Exists())
            {
                string[] reports = {
                    "~b~Partenaire: \"Secteur sécurisé, chef.\"",
                    "~b~Partenaire: \"Aucun incident à signaler.\"",
                    "~b~Partenaire: \"Tout est calme dans le périmètre.\"",
                    "~b~Partenaire: \"Prêt pour la prochaine mission.\""
                };
                
                Random rng = new Random();
                GTA.UI.Notification.Show(reports[rng.Next(reports.Length)]);
            }
        }

        private void UpdatePartnerBehaviour(Ped player)
        {
            if (partner == null || !partner.Exists()) return;

            // Si partenaire meurt, reset
            if (partner.IsDead)
            {
                GTA.UI.Notification.Show("~r~Votre partenaire est tombé au combat !");
                
                // Nettoyer les données partagées
                PoliceSharedData.RemoveManagedPartner(partner);
                PoliceSharedData.CurrentPartner = null;
                
                partner = null;
                return;
            }

            // Vérifier si le partenaire a un ordre de rester sur place
            if (partner.TaskSequenceProgress == -1)
            {
                // Vérifier si le partenaire est en train de rester immobile
                try
                {
                    bool isStanding = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, partner.Handle, 3);
                    if (isStanding) return; // Il exécute un ordre de rester sur place
                }
                catch { }
            }

            // Tracker les changements de véhicule du joueur
            Vehicle currentPlayerVehicle = player.IsInVehicle() ? player.CurrentVehicle : null;
            
            // Détecter si le joueur vient de sortir d'un véhicule
            if (lastPlayerVehicle != null && currentPlayerVehicle == null)
            {
                lastVehicleExitTime = DateTime.Now;
            }
            lastPlayerVehicle = currentPlayerVehicle;

            // Entrée/sortie véhicule - Version améliorée avec protection anti-vol
            if (player.IsInVehicle())
            {
                Vehicle veh = player.CurrentVehicle;
                if (!partner.IsInVehicle(veh))
                {
                    // Vérifier si c'est le véhicule que le joueur vient de quitter (protection anti-vol)
                    bool isRecentlyExitedVehicle = (DateTime.Now - lastVehicleExitTime).TotalSeconds < 3;
                    if (isRecentlyExitedVehicle && veh == lastPlayerVehicle)
                    {
                        // Attendre un peu avant que le partenaire puisse entrer
                        return;
                    }

                    // Trouver siège passager ou arrière avec priorité
                    VehicleSeat targetSeat = VehicleSeat.None;
                    
                    if (veh.IsSeatFree(VehicleSeat.Passenger))
                        targetSeat = VehicleSeat.Passenger;
                    else if (veh.IsSeatFree(VehicleSeat.RightRear))
                        targetSeat = VehicleSeat.RightRear;
                    else if (veh.IsSeatFree(VehicleSeat.LeftRear))
                        targetSeat = VehicleSeat.LeftRear;

                    if (targetSeat != VehicleSeat.None)
                    {
                        partner.Task.EnterVehicle(veh, targetSeat);
                        
                        // Forcer l'entrée si trop lent
                        Script.Wait(100);
                        if (!partner.IsInVehicle(veh) && partner.Position.DistanceTo(veh.Position) > 10f)
                        {
                            partner.SetIntoVehicle(veh, targetSeat);
                        }
                    }
                }
            }
            else
            {
                if (partner.IsInVehicle())
                {
                    // Vérifier si le partenaire n'est pas déjà en train de sortir
                    try
                    {
                        bool isGettingOut = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, partner.Handle, 2);
                        if (!isGettingOut)
                        {
                            // Sortie sécurisée du véhicule
                            Vehicle partnerVehicle = partner.CurrentVehicle;
                            if (partnerVehicle != null && partnerVehicle.Exists())
                            {
                                // Vérifier que le véhicule n'est pas en mouvement rapide
                                if (partnerVehicle.Speed < 5f) // Moins de 5 m/s
                                {
                                    partner.Task.LeaveVehicle(partnerVehicle, false);
                                }
                                else
                                {
                                    // Attendre que le véhicule ralentisse
                                    return;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // En cas d'erreur, sortie d'urgence sécurisée
                        if (partner.IsInVehicle())
                        {
                            partner.Task.ClearAll();
                            Script.Wait(500);
                        }
                    }
                }

                // Suivre le joueur à pied si pas d'ordre spécial
                if (!partner.IsInVehicle() && partner.TaskSequenceProgress == -1)
                {
                    float dist = player.Position.DistanceTo(partner.Position);
                    if (dist > 4f) // Suivre seulement si trop loin
                    {
                        partner.Task.FollowToOffsetFromEntity(player, new Vector3(-1, -2, 0), 2f, -1);
                    }
                }
            }

            // Combat : si joueur ciblé par ennemi ou si joueur tire -> partenaire engage même cible
            if (player.IsInCombat || player.IsShooting)
            {
                if (!partner.IsInCombat)
                {
                    try
                    {
                        // Vérifier que le partenaire n'est pas dans un véhicule en mouvement
                        if (!partner.IsInVehicle() || (partner.IsInVehicle() && partner.CurrentVehicle.Speed < 1f))
                        {
                            // Engage les cibles hostiles autour du partenaire (native).
                            Function.Call(Hash.TASK_COMBAT_HATED_TARGETS_AROUND_PED, partner.Handle, 50f, 0);
                        }
                    }
                    catch
                    {
                        // En cas d'erreur, ne pas engager le combat
                    }
                }
            }
            else if (partner.IsInCombat && !player.IsInCombat && !player.IsShooting)
            {
                // Si le joueur n'est plus en combat, arrêter le combat du partenaire
                try
                {
                    partner.Task.ClearAll();
                    Script.Wait(100);
                    // Retourner au comportement de suivi
                    if (!partner.IsInVehicle())
                    {
                        partner.Task.FollowToOffsetFromEntity(player, new Vector3(-1, -2, 0), 2f, -1);
                    }
                }
                catch
                {
                    // Ignorer les erreurs
                }
            }
        }

        private bool IsCop(Ped ped)
        {
            // Vérifier plusieurs critères pour identifier un policier
            if (ped.Model.Hash == PedHash.Cop01SMY.GetHashCode() ||
                ped.Model.Hash == PedHash.Cop01SFY.GetHashCode() ||
                ped.Model.Hash == PedHash.Sheriff01SMY.GetHashCode() ||
                ped.Model.Hash == PedHash.Sheriff01SFY.GetHashCode())
                return true;

            // Vérifier le groupe relationnel
            try
            {
                int copGroup = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
                return ped.RelationshipGroup.Hash == copGroup;
            }
            catch
            {
                return false;
            }
        }

        private void FixPoliceRelations(Ped player)
        {
            // Vérifier périodiquement si les relations avec la police sont correctes
            try
            {
                int playerGroup = player.RelationshipGroup.Hash;
                int copGroup = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
                
                // S'assurer que la police est amicale avec le joueur
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, copGroup, playerGroup); // Police -> Joueur : Respect
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, playerGroup, copGroup); // Joueur -> Police : Respect
                
                // Réinitialiser le niveau de recherche si nécessaire
                if (Game.Player.WantedLevel > 0 && DutyState.PoliceOnDuty)
                {
                    Game.Player.WantedLevel = 0;
                }
            }
            catch
            {
                // Ignorer les erreurs de relation
            }
        }

        private void SetFriendlyToPlayer(Ped ped)
        {
            int playerGroup = Game.Player.Character.RelationshipGroup.Hash;
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, ped.RelationshipGroup.Hash, playerGroup);
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, playerGroup, ped.RelationshipGroup.Hash);
        }

        private void MonitorPartnerSafety(Ped player)
        {
            if (partner == null || !partner.Exists()) return;

            DateTime now = DateTime.Now;
            
            // Vérifier toutes les 2 secondes
            if ((now - lastPositionCheck).TotalSeconds < 2) return;
            lastPositionCheck = now;

            Vector3 currentPos = partner.Position;
            
            // Détecter si le partenaire est tombé ou dans une situation dangereuse
            if (lastPartnerPosition != Vector3.Zero)
            {
                float verticalDrop = lastPartnerPosition.Z - currentPos.Z;
                float horizontalDistance = Vector3.Distance(new Vector3(lastPartnerPosition.X, lastPartnerPosition.Y, 0), 
                                                           new Vector3(currentPos.X, currentPos.Y, 0));
                
                // Si le partenaire est tombé de plus de 10m ou s'est téléporté loin
                if (verticalDrop > 10f || horizontalDistance > 100f)
                {
                    GTA.UI.Notification.Show("~y~Partenaire en difficulté - Repositionnement...");
                    RescuePartner(player);
                    return;
                }
            }

            // Vérifier si le partenaire est dans l'eau ou coincé
            if (partner.IsInWater || partner.Health < partner.MaxHealth * 0.5f)
            {
                RescuePartner(player);
                return;
            }

            // Vérifier la distance avec le joueur
            float distanceToPlayer = player.Position.DistanceTo(currentPos);
            if (distanceToPlayer > 200f) // Trop loin
            {
                GTA.UI.Notification.Show("~y~Partenaire trop éloigné - Rappel...");
                RescuePartner(player);
                return;
            }

            lastPartnerPosition = currentPos;
        }

        private void RescuePartner(Ped player)
        {
            if (partner == null || !partner.Exists()) return;

            try
            {
                // Arrêter toutes les tâches
                partner.Task.ClearAll();
                
                // Restaurer la santé
                partner.Health = partner.MaxHealth;
                
                // Téléporter près du joueur si nécessaire
                Vector3 playerPos = player.Position;
                Vector3 rescuePos = playerPos + player.ForwardVector * -3f + player.RightVector * 2f;
                
                // Vérifier que la position de secours est sûre
                if (World.GetGroundHeight(rescuePos) > rescuePos.Z - 10f)
                {
                    rescuePos.Z = World.GetGroundHeight(rescuePos) + 1f;
                    partner.Position = rescuePos;
                }

                // Réappliquer les protections
                partner.CanRagdoll = false;
                partner.CanBeKnockedOffBike = false;
                partner.CanFlyThroughWindscreen = false;
                partner.CanBeDraggedOutOfVehicle = false;
                
                Script.Wait(1000);
                
                // Reprendre le comportement normal
                if (!player.IsInVehicle())
                {
                    partner.Task.FollowToOffsetFromEntity(player, new Vector3(-1, -2, 0), 2f, -1);
                }

                GTA.UI.Notification.Show("~g~Partenaire repositionné avec succès");
            }
            catch
            {
                GTA.UI.Notification.Show("~r~Erreur lors du repositionnement du partenaire");
            }
        }

        private void ClearPartnerTasks(Ped ped)
        {
            if (!ped.Exists()) return;
            ped.Task.ClearAll();
            ped.AlwaysKeepTask = false;
        }
    }
} 