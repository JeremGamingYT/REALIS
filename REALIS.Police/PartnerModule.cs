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
        private enum PartnerOrder { None, Follow, Stay, HoldAndEngage, CoveringFire, ArrestSuspect }
        private PartnerOrder currentOrder = PartnerOrder.Follow; // Default to follow

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
        private DateTime lastPositionCheck = DateTime.MinValue; // Already used by MonitorPartnerSafety

        // Timers for less frequent updates
        private DateTime _nextNearbyOfficerSearchTime = DateTime.MinValue;
        private readonly TimeSpan _nearbyOfficerSearchInterval = TimeSpan.FromMilliseconds(400);
        private DateTime _nextPoliceRelationsCheckTime = DateTime.MinValue;
        private readonly TimeSpan _policeRelationsCheckInterval = TimeSpan.FromSeconds(5);


        public void Initialize() 
        { 
            try
            {
                uiPool = new ObjectPool();
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in Initialize: {ex.Message} {ex.StackTrace}");
            }
        }

        public void Update()
        {
            try
            {
                if (!DutyState.PoliceOnDuty) return;

                Ped player = Game.Player.Character;
                if (!player.Exists() || player.IsDead) return;

                // Mettre à jour l'état des menus
                PoliceSharedData.UpdateMenuState();

                // Nettoyer les références mortes périodiquement (Consider moving to a less frequent timer if it grows complex)
                PoliceSharedData.CleanupDeadReferences();

                // Vérifier et corriger les relations avec la police (timed)
                if (DateTime.Now >= _nextPoliceRelationsCheckTime)
                {
                    FixPoliceRelations(player);
                    _nextPoliceRelationsCheckTime = DateTime.Now + _policeRelationsCheckInterval;
                }

                // Chercher des officiers à proximité (timed)
                if (DateTime.Now >= _nextNearbyOfficerSearchTime)
                {
                    FindNearbyOfficer(player);
                    _nextNearbyOfficerSearchTime = DateTime.Now + _nearbyOfficerSearchInterval;
                }

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
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in Update: {ex.Message} {ex.StackTrace}");
                // Basic recovery: dismiss partner if state is unstable
                if (partner != null && partner.Exists())
                {
                    DismissPartner(); // This already logs errors if any
                }
                 if (partnerMenu != null && partnerMenu.Visible) partnerMenu.Visible = false;
                PoliceSharedData.ForceCloseMenu();
            }
        }

        public void Dispose()
        {
            try
            {
                if (partner != null && partner.Exists())
                {
                    ClearPartnerTasks(partner); // This method should also be wrapped
                    partner = null;
                }
                if (partnerMenu != null)
                    partnerMenu.Visible = false;
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in Dispose: {ex.Message} {ex.StackTrace}");
            }
        }

        private void FindNearbyOfficer(Ped player)
        {
            try
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
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in FindNearbyOfficer: {ex.Message} {ex.StackTrace}");
                nearbyOfficer = null;
            }
        }

        private void HandlePartnerInteraction(Ped player)
        {
            try
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
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in HandlePartnerInteraction: {ex.Message} {ex.StackTrace}");
            }
        }

        private void OpenPartnerMenu()
        {
            try
            {
                // Vérifier si on peut ouvrir le menu
                if (!PoliceSharedData.TryOpenMenu(null, "PartnerModule"))
                {
                    GTA.UI.Notification.Show("~r~Un autre menu est déjà ouvert.");
                    return;
                }

                if (partnerMenu != null)
                {
                    // Re-opening the same menu instance if it exists
                    if(PoliceSharedData.TryOpenMenu(partnerMenu, "PartnerModule"))
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
                partnerMenu.Add(new NativeItem("Renvoyer le partenaire")); // Index 0
                partnerMenu.Add(new NativeItem("Ordonner de rester ici")); // Index 1
                partnerMenu.Add(new NativeItem("Ordonner de me suivre")); // Index 2
                partnerMenu.Add(new NativeItem("Tenir position et engager")); // Index 3 - New
                partnerMenu.Add(new NativeItem("Feu de couverture")); // Index 4 - New
                partnerMenu.Add(new NativeItem("Arrêter suspect visé")); // Index 5 - New
                partnerMenu.Add(new NativeItem("Demander un rapport")); // Index 6 (was 3)
                }

            partnerMenu.Add(new NativeItem("Annuler")); // This will be index 7 if all above are present

                partnerMenu.ItemActivated += OnPartnerMenuItemActivated;
                partnerMenu.Closed += OnPartnerMenuClosed;
                uiPool.Add(partnerMenu);

                // Enregistrer et ouvrir le menu via le système centralisé
                if (PoliceSharedData.TryOpenMenu(partnerMenu, "PartnerModule"))
                {
                    partnerMenu.Visible = true;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in OpenPartnerMenu: {ex.Message} {ex.StackTrace}");
                if (partnerMenu != null) partnerMenu.Visible = false;
                PoliceSharedData.ForceCloseMenu();
            }
        }

        private void OnPartnerMenuItemActivated(object sender, LemonUI.Menus.ItemActivatedArgs e)
        {
            try
            {
                int idx = partnerMenu.Items.IndexOf(e.Item); // This can throw if item not in menu, though unlikely with LemonUI
                if (partnerMenu != null) partnerMenu.Visible = false; // Close menu first
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
                else // Partner exists
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
                    case 3: // Tenir position et engager
                        OrderHoldAndEngage();
                        break;
                    case 4: // Feu de couverture
                        OrderCoveringFire();
                        break;
                    case 5: // Arrêter suspect visé
                        OrderArrestSuspect();
                        break;
                    case 6: // Rapport
                            GetReport();
                            break;
                    // case 7 would be "Annuler"
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in OnPartnerMenuItemActivated for item '{e.Item?.Title}': {ex.Message} {ex.StackTrace}");
                if (partnerMenu != null) partnerMenu.Visible = false;
                PoliceSharedData.ForceCloseMenu();
            }
        }

        private void OnPartnerMenuClosed(object sender, System.EventArgs e)
        {
            try
            {
                PoliceSharedData.CloseMenuIfOwner("PartnerModule");
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in OnPartnerMenuClosed: {ex.Message} {ex.StackTrace}");
            }
        }

        // New Command Methods Stubs
        private void OrderHoldAndEngage()
        {
            try
            {
                if (partner != null && partner.Exists())
                {
                    currentOrder = PartnerOrder.HoldAndEngage;
                    partner.Task.ClearAll();
                    // Logic for holding position and engaging will be in UpdatePartnerBehaviour
                    GTA.UI.Notification.Show("~b~Partenaire: \"Je tiens la position et j'engage les hostiles!\"");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in OrderHoldAndEngage: {ex.Message} {ex.StackTrace}");
            }
        }

        private void OrderCoveringFire()
        {
            try
            {
                if (partner != null && partner.Exists())
                {
                    currentOrder = PartnerOrder.CoveringFire;
                    partner.Task.ClearAll();
                    // Logic for covering fire will be in UpdatePartnerBehaviour
                    GTA.UI.Notification.Show("~b~Partenaire: \"Je fournis un feu de couverture!\"");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in OrderCoveringFire: {ex.Message} {ex.StackTrace}");
            }
        }

        private void OrderArrestSuspect()
        {
            try
            {
                if (partner != null && partner.Exists())
                {
                    // Need to get aimed ped first
                    Ped aimedPed = GetEntityPlayerIsAimingAt(10f) as Ped; // Allow a bit more range for aiming
                    if (aimedPed != null && aimedPed.Exists() && aimedPed.IsHuman && !IsCop(aimedPed) && aimedPed.IsAlive)
                    {
                        currentOrder = PartnerOrder.ArrestSuspect;
                        partner.Task.ClearAll();
                        // Logic for arresting will be in UpdatePartnerBehaviour, passing aimedPed
                        GTA.UI.Notification.Show($"~b~Partenaire: \"Je vais tenter d'arrêter {aimedPed.Handle}!\"");
                        // Store the target for the partner to act upon in UpdatePartnerBehaviour
                        _targetSuspectForArrest = aimedPed;
                    }
                    else
                    {
                        GTA.UI.Notification.Show("~r~Aucun suspect valide visé pour l'arrestation.");
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in OrderArrestSuspect: {ex.Message} {ex.StackTrace}");
            }
        }
        private Ped _targetSuspectForArrest = null; // Field to store arrest target

        private Entity GetEntityPlayerIsAimingAt(float range) // Helper for aiming
        {
            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists()) return null;

                Vector3 camPos = GameplayCamera.Position;
                Vector3 dir = GameplayCamera.Direction;
                RaycastResult res = World.Raycast(camPos, camPos + dir * range, IntersectFlags.Peds, player);
                return res.DidHit ? res.HitEntity : null;
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule GetEntityPlayerIsAimingAt error: {ex.Message} {ex.StackTrace}");
                return null;
            }
        }


        private void RecruitPartner(Ped officer)
        {
            try
            {
                partner = officer;
                currentOrder = PartnerOrder.Follow; // Set default order
                _targetSuspectForArrest = null; // Clear any previous arrest target
                GTA.UI.Notification.Show("~b~Officier recruté comme partenaire");
                SetFriendlyToPlayer(partner);
                partner.AlwaysKeepTask = true;

                // Configurer le partenaire pour éviter les comportements erratiques
                partner.CanRagdoll = false;
                partner.CanBeKnockedOffBike = false;
                partner.CanFlyThroughWindscreen = false;
                partner.CanBeDraggedOutOfVehicle = false;
                partner.CanBeTargetted = false; // Player's choice, can make them invincible to direct enemy fire

                // Enregistrer dans les données partagées
                PoliceSharedData.CurrentPartner = partner;
                PoliceSharedData.AddManagedPartner(partner);

                // Faire saluer le nouveau partenaire
                partner.Task.PlayAnimation("gestures@m@standing@casual", "gesture_hello", 8f, -8f, 2000, AnimationFlags.None, 0f);
                GTA.UI.Notification.Show("~b~Partenaire: \"Prêt à vous suivre, chef !\"");
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in RecruitPartner: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors du recrutement du partenaire.");
                if (officer != null && officer.Exists() && PoliceSharedData.IsManagedPartner(officer))
                {
                     PoliceSharedData.RemoveManagedPartner(officer); // Clean up if partially added
                }
                partner = null; // Ensure partner is null on failure
                PoliceSharedData.CurrentPartner = null;
                currentOrder = PartnerOrder.None;
            }
        }

        private void DismissPartner()
        {
            try
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
                    currentOrder = PartnerOrder.None; // Reset order
                    _targetSuspectForArrest = null;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in DismissPartner: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors du renvoi du partenaire.");
                // Attempt to clean up state even on error
                if (partner != null) PoliceSharedData.RemoveManagedPartner(partner);
                PoliceSharedData.CurrentPartner = null;
                partner = null;
                currentOrder = PartnerOrder.None;
                _targetSuspectForArrest = null;
            }
        }

        private void RequestAssistance(Ped officer)
        {
            try
            {
                if (officer == null || !officer.Exists()) return;
                GTA.UI.Notification.Show("~g~Assistance demandée");
                officer.Task.PlayAnimation("gestures@m@standing@casual", "gesture_point", 8f, -8f, 3000, AnimationFlags.None, 0f);

                // L'officier aide temporairement
                officer.Task.FollowToOffsetFromEntity(Game.Player.Character, new Vector3(1, -1, 0), 3f, 10000);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in RequestAssistance: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors de la demande d'assistance.");
            }
        }

        private void OrderStay()
        {
            try
            {
                if (partner != null && partner.Exists())
                {
                    currentOrder = PartnerOrder.Stay;
                    partner.Task.ClearAll();
                    partner.Task.StandStill(-1); // Immediate action
                    GTA.UI.Notification.Show("~b~Partenaire: \"Je reste en position !\"");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in OrderStay: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur: impossible d'ordonner de rester.");
            }
        }

        private void OrderFollow()
        {
            try
            {
                if (partner != null && partner.Exists())
                {
                    currentOrder = PartnerOrder.Follow;
                    _targetSuspectForArrest = null; // Clear arrest target when switching to follow
                    partner.Task.ClearAll();
                    GTA.UI.Notification.Show("~b~Partenaire: \"Je vous suis !\"");
                    // Follow logic is handled in UpdatePartnerBehaviour's Follow case
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in OrderFollow: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur: impossible d'ordonner de suivre.");
            }
        }

        private void GetReport()
        {
            try
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
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in GetReport: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur: impossible d'obtenir un rapport.");
            }
        }

        private void UpdatePartnerBehaviour(Ped player)
        {
            try
            {
                if (partner == null || !partner.Exists()) return;

                if (partner.IsDead)
                {
                    GTA.UI.Notification.Show("~r~Votre partenaire est tombé au combat !");
                    PoliceSharedData.RemoveManagedPartner(partner);
                    PoliceSharedData.CurrentPartner = null;
                    // Ped deadPartner = partner; // For potential MarkAsNoLongerNeeded
                    partner = null;
                    currentOrder = PartnerOrder.None;
                    _targetSuspectForArrest = null;
                    // if(deadPartner != null) deadPartner.MarkAsNoLongerNeeded(); // Optional for dead peds
                    return;
                }

                // Common combat logic: If player or partner is in combat, ensure partner engages.
                // More specific orders below might override this general combat engagement.
                bool engageInCombat = false;
                Entity targetEnemy = null;

                if (player.IsInCombat || player.IsShooting)
                {
                    engageInCombat = true;
                    // Try to get player's targeted entity
                    targetEnemy = GetEntityPlayerIsAimingAt();
                }
                if (partner.IsInCombat)
                {
                    var aimed = GetEntityPedIsAimingAt(partner);
                    if (aimed != null)
                    {
                        engageInCombat = true;
                        if (targetEnemy == null) targetEnemy = aimed;
                    }
                }


                // Main logic switch based on currentOrder
                switch (currentOrder)
                {
                    case PartnerOrder.Follow:
                        HandleFollowOrder(player, engageInCombat, targetEnemy);
                        break;
                    case PartnerOrder.Stay:
                        HandleStayOrder(engageInCombat, targetEnemy);
                        break;
                    case PartnerOrder.HoldAndEngage:
                        HandleHoldAndEngageOrder(player, engageInCombat, targetEnemy);
                        break;
                    case PartnerOrder.CoveringFire:
                        // TODO: Implement CoveringFire logic
                        HandleCoveringFireOrder(player, engageInCombat, targetEnemy);
                        break;
                    case PartnerOrder.ArrestSuspect:
                        // TODO: Implement ArrestSuspect logic
                        HandleArrestSuspectOrder(player);
                        break;
                    default:
                        // Default to follow if order is None or unknown
                        currentOrder = PartnerOrder.Follow;
                        HandleFollowOrder(player, engageInCombat, targetEnemy);
                        break;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in UpdatePartnerBehaviour: {ex.Message} {ex.StackTrace}");
                if (partner != null && partner.Exists())
                {
                    try { partner.Task.ClearAll(); } catch { }
                    currentOrder = PartnerOrder.Follow; // Revert to default on error
                }
            }
        }

        private void HandleFollowOrder(Ped player, bool engageInCombat, Entity targetEnemy)
        {
            // Vehicle entry/exit logic (simplified from original)
            Vehicle currentPlayerVehicle = player.IsInVehicle() ? player.CurrentVehicle : null;
            if (lastPlayerVehicle != null && currentPlayerVehicle == null) lastVehicleExitTime = DateTime.Now;
            lastPlayerVehicle = currentPlayerVehicle;

            if (player.IsInVehicle())
            {
                Vehicle veh = player.CurrentVehicle;
                if (veh.Exists() && !partner.IsInVehicle(veh))
                {
                    bool isRecentlyExitedVehicle = (DateTime.Now - lastVehicleExitTime).TotalSeconds < 3;
                    if (!isRecentlyExitedVehicle || veh != lastPlayerVehicle)
                    {
                        VehicleSeat targetSeat = FindOpenSeat(veh);
                        if (targetSeat != VehicleSeat.None)
                        {
                            partner.Task.EnterVehicle(veh, targetSeat, -1, 2.0f, EnterVehicleFlags.None);
                            // Consider removing Script.Wait(100) and SetIntoVehicle for robustness, rely on task.
                        }
                    }
                }
            }
            else // Player on foot
            {
                if (partner.IsInVehicle())
                {
                    Vehicle partnerVehicle = partner.CurrentVehicle;
                    if (partnerVehicle != null && partnerVehicle.Exists() && partnerVehicle.Speed < 5f && !Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, partner.Handle, 2))
                    {
                        partner.Task.LeaveVehicle(partnerVehicle, false);
                    }
                }
            }

            // Combat engagement for Follow order
            if (engageInCombat)
            {
                 if (!partner.IsInVehicle() || (partner.CurrentVehicle != null && partner.CurrentVehicle.Speed < 1f)) {
                    if (targetEnemy != null && targetEnemy.Exists() && targetEnemy is Ped) {
                        partner.Task.FightAgainst((Ped)targetEnemy);
                    } else {
                        Function.Call(Hash.TASK_COMBAT_HATED_TARGETS_AROUND_PED, partner.Handle, 50f, 0);
                    }
                 }
            }
            else // Not in combat, continue following
            {
                if (partner.Exists() && !partner.IsInVehicle() && partner.TaskSequenceProgress == -1)
                {
                    float dist = player.Position.DistanceTo(partner.Position);
                    if (dist > 4f)
                    {
                        bool isFollowing = false;
                        try { isFollowing = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, partner.Handle, 46); } catch { }
                        if (!isFollowing || dist > 10f)
                        {
                            partner.Task.FollowToOffsetFromEntity(player, new Vector3(-1, -1.5f, 0), 1.0f, -1, 3.0f); // Adjusted offset
                        }
                    } else if (dist < 2.0f && !Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, partner.Handle, 3) /* not standing still */) {
                        // If too close and not already standing still, make them stand still briefly to avoid pushing player
                        // partner.Task.StandStill(1000); //This might make them look robotic, use with care.
                    }
                }
            }
        }

        private VehicleSeat FindOpenSeat(Vehicle veh)
        {
            if (veh.IsSeatFree(VehicleSeat.Passenger)) return VehicleSeat.Passenger;
            if (veh.IsSeatFree(VehicleSeat.RightRear)) return VehicleSeat.RightRear;
            if (veh.IsSeatFree(VehicleSeat.LeftRear)) return VehicleSeat.LeftRear;
            return VehicleSeat.None;
        }


        private void HandleStayOrder(bool engageInCombat, Entity targetEnemy)
        {
            // Ensure partner stays put unless engaging
            bool isStandingStill = false;
            try { isStandingStill = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, partner.Handle, 3); } catch {}

            if (engageInCombat)
            {
                 if (!partner.IsInVehicle() || (partner.CurrentVehicle != null && partner.CurrentVehicle.Speed < 1f)) {
                    if (targetEnemy != null && targetEnemy.Exists() && targetEnemy is Ped) {
                        partner.Task.FightAgainst((Ped)targetEnemy);
                    } else {
                        Function.Call(Hash.TASK_COMBAT_HATED_TARGETS_AROUND_PED, partner.Handle, 50f, 0);
                    }
                 }
            }
            else if (!isStandingStill && partner.TaskSequenceProgress == -1) // Not in combat, and not already standing still via task
            {
                partner.Task.StandStill(-1);
            }
        }

        private void HandleHoldAndEngageOrder(Ped player, bool engageInCombat, Entity targetEnemy)
        {
             // Similar to Stay, but might allow slight repositioning for better engagement angles
             // For now, treat similarly to Stay for combat, but ensure they don't follow player
            bool isGuarding = false; // Check if partner is already guarding a specific position or area
            // Example: Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, partner.Handle, TASK_TYPE_FOR_GUARD_AREA);

            if (engageInCombat)
            {
                if (!partner.IsInVehicle() || (partner.CurrentVehicle != null && partner.CurrentVehicle.Speed < 1f)) {
                    if (targetEnemy != null && targetEnemy.Exists() && targetEnemy is Ped) {
                        partner.Task.FightAgainst((Ped)targetEnemy);
                    } else {
                        // TASK_GUARD_CURRENT_POSITION makes them static, TASK_COMBAT_HATED_TARGETS_IN_AREA might be better
                        Vector3 pos = partner.Position;
                        Function.Call(Hash.TASK_COMBAT_HATED_TARGETS_IN_AREA, partner.Handle, pos.X, pos.Y, pos.Z, 50f, 0);
                    }
                }
            }
            else if (!isGuarding && partner.TaskSequenceProgress == -1)
            {
                // If not in combat and not already guarding, ensure they guard their current position
                partner.Task.GuardCurrentPosition();
            }

            // If player moves too far, revert to follow
            if (player.Position.DistanceTo(partner.Position) > 30f) // 30m threshold
            {
                GTA.UI.Notification.Show("~b~Partenaire: \"Je vous rejoins, chef!\"");
                currentOrder = PartnerOrder.Follow;
                _targetSuspectForArrest = null;
            }
        }

        private void HandleCoveringFireOrder(Ped player, bool engageInCombat, Entity targetEnemy)
        {
            try
            {
                if (!partner.Exists()) return;

                Vector3 coverPosition = Vector3.Zero;
                bool foundCover = false;

                // Try to find cover near the partner or player
                if (partner.IsInCover)
                {
                    coverPosition = partner.Position;
                    foundCover = true;
                }
                else
                {
                    // Search for cover near the partner first
                    coverPosition = World.GetSafeCoordForPed(partner.Position + partner.ForwardVector * 5f, true, 16);
                    if (coverPosition != Vector3.Zero && Function.Call<bool>(Hash.IS_POINT_OBSCURED_BY_A_MISSION_ENTITY, coverPosition.X, coverPosition.Y, coverPosition.Z, 1.0f, 1.0f, 1.0f, 0)) // Check if point is valid cover
                    {
                         foundCover = true;
                    } else {
                        // If no cover near partner, try near player
                         coverPosition = World.GetSafeCoordForPed(player.Position + player.ForwardVector * 5f, true, 16);
                         if (coverPosition != Vector3.Zero && Function.Call<bool>(Hash.IS_POINT_OBSCURED_BY_A_MISSION_ENTITY, coverPosition.X, coverPosition.Y, coverPosition.Z, 1.0f, 1.0f, 1.0f, 0))
                         {
                            foundCover = true;
                         }
                    }
                }

                if (engageInCombat)
                {
                    if (foundCover && partner.Position.DistanceTo(coverPosition) > 2f && !partner.IsInCover)
                    {
                        // Go to cover then engage
                        partner.Task.GoTo(coverPosition);
                        // Once in cover, or if already in cover, engage
                        // This needs state: if task is GoTo, wait. If task is Combat, good.
                        // For simplicity now, we'll assume they'll engage from cover once there or if already there.
                    }

                    // Prioritize player's attacker if player is being shot at
                    if (player.IsInCombat)
                    {
                        // This is hard to get the actual shooter without more complex logic (e.g. raycasting from player)
                        // For now, use general combat logic or player's target if available.
                        if (targetEnemy != null && targetEnemy.Exists() && targetEnemy is Ped)
                        {
                            partner.Task.FightAgainst((Ped)targetEnemy); // Combat ped
                        }
                        else
                        {
                            Vector3 pPos = player.Position;
                            Function.Call(Hash.TASK_COMBAT_HATED_TARGETS_IN_AREA, partner.Handle, pPos.X, pPos.Y, pPos.Z, 70f, 0);
                        }
                    }
                    else if (targetEnemy != null && targetEnemy.Exists() && targetEnemy is Ped)
                    {
                        partner.Task.FightAgainst((Ped)targetEnemy);
                    }
                    else
                    {
                        Function.Call(Hash.TASK_COMBAT_HATED_TARGETS_AROUND_PED, partner.Handle, 70f, 0);
                    }
                }
                else // Not actively in combat by player/partner direct engagement
                {
                    if (foundCover && partner.Position.DistanceTo(coverPosition) > 1.5f && !partner.IsInCover)
                    {
                         partner.Task.GoTo(coverPosition);
                    } else if (foundCover) {
                        partner.Task.AimAt(player.Position + player.ForwardVector * 20f, 5000); // Aim towards player's forward direction
                    } else {
                        // No cover found, revert to holding position and engaging
                        HandleHoldAndEngageOrder(player, false, null);
                    }
                }
                 // This order should probably be maintained until explicitly changed or player moves very far.
                if (player.Position.DistanceTo(partner.Position) > 40f) // Increased threshold
                {
                    GTA.UI.Notification.Show("~b~Partenaire: \"Je vous rejoins, chef!\"");
                    currentOrder = PartnerOrder.Follow;
                    _targetSuspectForArrest = null;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in HandleCoveringFireOrder: {ex.Message} {ex.StackTrace}");
                currentOrder = PartnerOrder.Follow; // Revert to default on error
            }
        }

        private void HandleArrestSuspectOrder(Ped player)
        {
            try
            {
                if (!partner.Exists() || _targetSuspectForArrest == null || !_targetSuspectForArrest.Exists() || !_targetSuspectForArrest.IsAlive)
                {
                    GTA.UI.Notification.Show("~b~Partenaire: \"Cible d'arrestation invalide ou neutralisée.\"");
                    currentOrder = PartnerOrder.Follow;
                    _targetSuspectForArrest = null;
                    return;
                }

                float distanceToSuspect = partner.Position.DistanceTo(_targetSuspectForArrest.Position);

                // If suspect is hostile or flees, partner should engage
                if (_targetSuspectForArrest.IsInCombatAgainst(partner) || _targetSuspectForArrest.IsInCombatAgainst(player) || Function.Call<bool>(Hash.IS_PED_FLEEING, _targetSuspectForArrest.Handle))
                {
                    GTA.UI.Notification.Show($"~b~Partenaire: \"La cible {_targetSuspectForArrest.Handle} est hostile/fuit! Engagement!\"");
                    partner.Task.FightAgainst(_targetSuspectForArrest);
                    // Consider reverting order after combat starts, or let combat logic in Follow/HoldEngage take over if more appropriate
                    // For now, let combat task run. The order will change if player gives a new one or this one times out/succeeds.
                    // To make it more robust, we might need a sub-state like "EngagingArrestTarget"
                    return;
                }

                if (distanceToSuspect > 15f && partner.TaskSequenceProgress == -1) // If too far and not already tasked
                {
                     partner.Task.GoTo(_targetSuspectForArrest.Position, -1);
                     return;
                }
                else if (distanceToSuspect > 3f && partner.TaskSequenceProgress == -1) // Getting closer
                {
                     partner.Task.GoTo(_targetSuspectForArrest.Position, -1); // Approach carefully
                     return;
                }
                else if (distanceToSuspect <= 3f) // Close enough
                {
                    // Partner aims at suspect
                    partner.Task.AimAt(_targetSuspectForArrest, -1); // Aim indefinitely

                    // Check if suspect surrenders (hands up) - this is a simplified check
                    // A more robust check would involve checking CTaskSimpleHandsUp (taskType 19)
                    // bool isSuspectSurrendering = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, _targetSuspectForArrest.Handle, 19);

                    // For now, we'll assume if the partner is aiming and suspect isn't hostile, it's a "standoff"
                    // A full arrest sequence (cuffing etc.) by AI is very complex.
                    // This order could time out or player could intervene.
                    GTA.UI.Screen.ShowHelpTextThisFrame($"Partenaire maintient suspect {_targetSuspectForArrest.Handle} en joue.");


                    // If player gets too far from partner or suspect, cancel.
                    if (player.Position.DistanceTo(partner.Position) > 20f || player.Position.DistanceTo(_targetSuspectForArrest.Position) > 25f)
                    {
                        GTA.UI.Notification.Show("~b~Partenaire: \"Je retourne en suivi, cible d'arrestation trop éloignée.\"");
                        currentOrder = PartnerOrder.Follow;
                        _targetSuspectForArrest = null;
                        partner.Task.ClearAll(); // Clear aiming task
                    }
                    // TODO: Add a timeout for this state?
                    return;
                }
                 // If none of the above, and partner is idle, try to re-evaluate (e.g. GoTo)
                if(partner.IsIdle && distanceToSuspect > 1.5f) {
                    partner.Task.GoTo(_targetSuspectForArrest.Position, -1);
                }

            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in HandleArrestSuspectOrder: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors de la tentative d'arrestation par le partenaire.");
                currentOrder = PartnerOrder.Follow; // Revert to default on error
                _targetSuspectForArrest = null;
                if(partner != null && partner.Exists()) partner.Task.ClearAll();
            }
        }


        private bool IsCop(Ped ped)
        {
            try
            {
                // Vérifier plusieurs critères pour identifier un policier
                if ((PedHash)ped.Model.Hash == PedHash.Cop01SMY ||
                    (PedHash)ped.Model.Hash == PedHash.Cop01SFY ||
                    (PedHash)ped.Model.Hash == PedHash.Sheriff01SMY ||
                    (PedHash)ped.Model.Hash == PedHash.Sheriff01SFY)
                    return true;

                // Vérifier le groupe relationnel
                int copGroup = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
                return ped.RelationshipGroup.Hash == copGroup;
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in IsCop: {ex.Message} {ex.StackTrace}");
                 return false; // Safer to assume not a cop if check fails
            }
        }

        private void FixPoliceRelations(Ped player)
        {
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
                    Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false); // Ensure it's immediate
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in FixPoliceRelations: {ex.Message} {ex.StackTrace}");
            }
        }

        private void SetFriendlyToPlayer(Ped ped)
        {
            try
            {
                if (ped == null || !ped.Exists() || Game.Player.Character == null || !Game.Player.Character.Exists()) return;
                int playerGroup = Game.Player.Character.RelationshipGroup.Hash;
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, ped.RelationshipGroup.Hash, playerGroup); // Ped -> Player: Respect/Companion
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, playerGroup, ped.RelationshipGroup.Hash); // Player -> Ped: Respect/Companion
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in SetFriendlyToPlayer for ped {ped?.Handle}: {ex.Message} {ex.StackTrace}");
            }
        }

        private void MonitorPartnerSafety(Ped player)
        {
            // This method already has good structure and a timer. Adding try-catch.
            try
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
                    // Use 2D distance for horizontal check
                    float horizontalDistance = currentPos.DistanceTo2D(lastPartnerPosition);

                    // Si le partenaire est tombé de plus de 10m ou s'est téléporté loin
                    if (verticalDrop > 10f || horizontalDistance > 100f)
                    {
                        GTA.UI.Notification.Show("~y~Partenaire en difficulté - Repositionnement...");
                        RescuePartner(player);
                        return;
                    }
                }

                // Vérifier si le partenaire est dans l'eau ou coincé, or low health
                if (partner.IsInWater || (partner.Health > 0 && partner.Health < partner.MaxHealth * 0.3f)) // Check health more proactively
                {
                    GTA.UI.Notification.Show("~y~Partenaire en danger - Repositionnement...");
                    RescuePartner(player);
                    return;
                }

                // Vérifier la distance avec le joueur
                float distanceToPlayer = player.Position.DistanceTo(currentPos);
                if (distanceToPlayer > 150f) // Reduced from 200f
                {
                    GTA.UI.Notification.Show("~y~Partenaire trop éloigné - Rappel...");
                    RescuePartner(player);
                    return;
                }

                lastPartnerPosition = currentPos;
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in MonitorPartnerSafety: {ex.Message} {ex.StackTrace}");
            }
        }

        private void RescuePartner(Ped player)
        {
            try
            {
                if (partner == null || !partner.Exists() || player == null || !player.Exists()) return;

                // Arrêter toutes les tâches
                partner.Task.ClearAll();
                
                // Restaurer la santé
                partner.Health = partner.MaxHealth;
                partner.Armor = 100; // Give some armor too
                
                // Téléporter près du joueur si nécessaire
                Vector3 playerPos = player.Position;
                // Attempt to find a safe sidewalk position near the player
                Vector3 rescuePos = World.GetNextPositionOnSidewalk(playerPos + player.ForwardVector * -3f + player.RightVector * 2f);
                if (rescuePos == Vector3.Zero) // Fallback if no sidewalk found
                {
                    rescuePos = playerPos + player.ForwardVector * -2f; // Simpler fallback
                    rescuePos.Z = World.GetGroundHeight(rescuePos); // Ensure Z is on ground
                }

                partner.Position = rescuePos;
                Function.Call(Hash.SET_PED_COORDS_KEEP_VEHICLE, partner.Handle, rescuePos.X, rescuePos.Y, rescuePos.Z);

                // Réappliquer les protections
                partner.CanRagdoll = false;
                partner.CanBeKnockedOffBike = false;
                partner.CanFlyThroughWindscreen = false;
                partner.CanBeDraggedOutOfVehicle = false;
                
                Script.Wait(500); // Increased wait slightly for teleport and settle
                
                // Reprendre le comportement normal (follow will be picked up by UpdatePartnerBehaviour)
                GTA.UI.Notification.Show("~g~Partenaire repositionné avec succès");
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in RescuePartner: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors du repositionnement du partenaire");
            }
        }

        private static Entity GetEntityPlayerIsAimingAt()
        {
            try
            {
                Vector3 camPos = GameplayCamera.Position;
                Vector3 dir = GameplayCamera.Direction;
                RaycastResult res = World.Raycast(camPos, camPos + dir * 100f, IntersectFlags.Peds, Game.Player.Character);
                return res.DidHit ? res.HitEntity : null;
            }
            catch
            {
                return null;
            }
        }

        private static Entity GetEntityPedIsAimingAt(Ped ped)
        {
            if (ped == null || !ped.Exists()) return null;
            try
            {
                Vector3 camPos = ped.Bones[Bone.SkelHead].Position;
                Vector3 dir = ped.ForwardVector;
                RaycastResult res = World.Raycast(camPos, camPos + dir * 100f, IntersectFlags.Peds, ped);
                return res.DidHit ? res.HitEntity : null;
            }
            catch
            {
                return null;
            }
        }

        private void ClearPartnerTasks(Ped ped)
        {
            try
            {
                if (ped != null && ped.Exists())
                {
                    ped.Task.ClearAll();
                    ped.AlwaysKeepTask = false; // Reset this flag
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PartnerModule error in ClearPartnerTasks for ped {ped?.Handle}: {ex.Message} {ex.StackTrace}");
            }
        }
    }
} 