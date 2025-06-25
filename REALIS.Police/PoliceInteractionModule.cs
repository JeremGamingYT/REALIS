using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Common;
using LemonUI;
using LemonUI.Menus;
using System.Windows.Forms;

namespace REALIS.Police
{
    /// <summary>
    /// Module pour les interactions policières avancées avec les PNJ : 
    /// contraventions, alcootests, interrogatoires, fouilles, etc.
    /// </summary>
    public class PoliceInteractionModule : IModule
    {
        private const float InteractionRange = 3f;
        private const GTA.Control InteractionKey = GTA.Control.Context; // E

        // UI LemonUI
        private ObjectPool uiPool;
        private NativeMenu mainInteractionMenu;
        private NativeMenu trafficTicketMenu;
        private NativeMenu interrogationMenu;
        
        private Ped targetPed;
        private Vehicle targetVehicle;
        private DateTime lastInteractionTime = DateTime.MinValue;
        private readonly TimeSpan interactionCooldown = TimeSpan.FromMilliseconds(500);
        
        // Pour contrôler le comportement du PNJ pendant l'interaction
        private bool isInteracting = false;
        private Ped interactingPed = null;

        // Timer for ped search
        private DateTime _nextPedSearchTime = DateTime.MinValue;
        private readonly TimeSpan _pedSearchInterval = TimeSpan.FromMilliseconds(350); // Reduced from 500ms for responsiveness

        // Données des contraventions
        private readonly Dictionary<string, int> trafficViolations = new Dictionary<string, int>
        {
            { "Excès de vitesse léger (+10-20 km/h)", 135 },
            { "Excès de vitesse modéré (+20-40 km/h)", 200 },
            { "Excès de vitesse élevé (+40+ km/h)", 400 },
            { "Conduite dangereuse", 250 },
            { "Feu rouge grillé", 135 },
            { "Stop non respecté", 135 },
            { "Stationnement interdit", 35 },
            { "Téléphone au volant", 135 },
            { "Ceinture de sécurité", 135 },
            { "Véhicule non conforme", 68 },
            { "Assurance non valide", 750 },
            { "Permis non valide/expiré", 800 }
        };

        public void Initialize()
        {
            uiPool = new ObjectPool();
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

                // Process UI seulement si ce module en possède un ouvert
                if (PoliceSharedData.CurrentMenuOwner == "PoliceInteractionModule")
                {
                    uiPool?.Process();
                }

                // Nettoyer les références mortes périodiquement (Consider moving to a less frequent timer if it grows)
                PoliceSharedData.CleanupDeadReferences();

                // Maintenir le PNJ figé pendant l'interaction
                MaintainInteractionState();

                // Vérifier si l'interaction doit se terminer
                CheckInteractionEnd(player);

                // Chercher des PNJ à proximité pour interaction (with timer)
                if (DateTime.Now >= _nextPedSearchTime)
                {
                    FindInteractionTarget(player);
                    _nextPedSearchTime = DateTime.Now + _pedSearchInterval;
                }

                // Gestion des interactions
                HandleInteractions(player);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in Update: {ex.Message} {ex.StackTrace}");
                // Basic recovery: end any ongoing interaction to prevent stuck state
                if (isInteracting) EndInteraction();
                if (mainInteractionMenu != null && mainInteractionMenu.Visible) mainInteractionMenu.Visible = false;
                PoliceSharedData.ForceCloseMenu();

            }
        }

        public void Dispose()
        {
            try
            {
                EndInteraction();

                if (mainInteractionMenu != null)
                    mainInteractionMenu.Visible = false;
                if (trafficTicketMenu != null)
                    trafficTicketMenu.Visible = false;
                if (interrogationMenu != null)
                    interrogationMenu.Visible = false;
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in Dispose: {ex.Message} {ex.StackTrace}");
            }
        }

        private void FindInteractionTarget(Ped player)
        {
            try
            {
                targetPed = null;
                targetVehicle = null;

                // Chercher le PNJ le plus proche (en excluant les policiers pour éviter conflit avec PartnerModule)
                Ped[] nearbyPeds = World.GetNearbyPeds(player, InteractionRange);
                float closestDist = InteractionRange;

                foreach (Ped ped in nearbyPeds)
                {
                    if (ped == null || !ped.Exists() || ped == player) continue;
                    if (ped.IsDead || !ped.IsHuman) continue;
                    
                    // IMPORTANT : Ignorer les policiers pour éviter la superposition avec PartnerModule
                    if (IsCop(ped)) continue;
                    
                    // IMPORTANT : Ignorer les partenaires gérés par PartnerModule
                    if (PoliceSharedData.IsManagedPartner(ped)) continue;

                    float dist = player.Position.DistanceTo(ped.Position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        targetPed = ped;

                        // IMPORTANT : Réinitialiser targetVehicle d'abord
                        targetVehicle = null;

                        // Si le PNJ est dans un véhicule, on récupère aussi le véhicule
                        if (ped.IsInVehicle())
                        {
                            targetVehicle = ped.CurrentVehicle;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in FindInteractionTarget: {ex.Message} {ex.StackTrace}");
                targetPed = null;
                targetVehicle = null;
            }
        }

        private void HandleInteractions(Ped player)
        {
            try
            {
                if (targetPed == null || !targetPed.Exists()) return;

                // Déterminer le type d'interaction disponible
                string helpText = GetHelpText();
                if (!string.IsNullOrEmpty(helpText))
                {
                    // Afficher le texte d'aide à chaque frame quand nécessaire
                    GTA.UI.Screen.ShowHelpTextThisFrame(helpText);

                    // Vérifier le cooldown uniquement pour l'action
                    if (Game.IsControlJustPressed(InteractionKey) && DateTime.Now - lastInteractionTime >= interactionCooldown)
                    {
                        lastInteractionTime = DateTime.Now;
                        OpenInteractionMenu();
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in HandleInteractions: {ex.Message} {ex.StackTrace}");
            }
        }

        private string GetHelpText()
        {
            // No try-catch here as it's simple property access and string returns.
            // If complex logic is added, it would need one.
            if (targetPed == null) return "";

            // Si le PNJ est menotté
            if (REALIS.Job.PoliceArrestShared.CuffedPeds.Contains(targetPed.Handle))
            {
                return "~INPUT_CONTEXT~ Interroger le suspect";
            }

            // Si le PNJ a les mains en l'air
            if (REALIS.Job.PoliceArrestShared.SurrenderedPeds.Contains(targetPed.Handle))
            {
                return "~INPUT_CONTEXT~ Contrôler la personne";
            }

            // Si le PNJ est au volant
            if (targetPed.IsInVehicle() && targetVehicle != null)
            {
                return "~INPUT_CONTEXT~ Contrôle routier";
            }

            // Interaction générale
            return "~INPUT_CONTEXT~ Contrôler la personne";
        }

        private void OpenInteractionMenu()
        {
            try
            {
                // Vérifier si on peut ouvrir le menu
                if (!PoliceSharedData.TryOpenMenu(null, "PoliceInteractionModule"))
                {
                    GTA.UI.Notification.Show("~r~Un autre menu est déjà ouvert.");
                    return;
                }

                // Toujours créer un nouveau menu pour éviter les problèmes de cache
                if (mainInteractionMenu != null)
                {
                    mainInteractionMenu.Visible = false;
                    uiPool?.Remove(mainInteractionMenu);
                    mainInteractionMenu = null;
                }

                string title = "Contrôle Police";
                string subtitle = "Options d'interaction";

                // Debug pour vérifier l'état du PNJ
                bool pedInVehicle = targetPed.IsInVehicle();
                bool hasTargetVehicle = targetVehicle != null;

                // Si le PNJ est menotté
                if (REALIS.Job.PoliceArrestShared.CuffedPeds.Contains(targetPed.Handle))
                {
                    title = "Interrogatoire";
                    subtitle = "Suspect menotté";
                }
                else if (pedInVehicle && hasTargetVehicle)
                {
                    title = "Contrôle Routier";
                    subtitle = "Conducteur";
                }
                else
                {
                    title = "Contrôle Police";
                    subtitle = "Personne à pied";
                }

                mainInteractionMenu = new NativeMenu(title, subtitle);

                // Options selon le contexte
                if (REALIS.Job.PoliceArrestShared.CuffedPeds.Contains(targetPed.Handle))
                {
                    // Suspect menotté - options d'interrogatoire
                    mainInteractionMenu.Add(new NativeItem("Interroger sur l'incident"));
                    mainInteractionMenu.Add(new NativeItem("Demander ses complices"));
                    mainInteractionMenu.Add(new NativeItem("Questions générales"));
                    mainInteractionMenu.Add(new NativeItem("Lire ses droits"));
                }
                else if (pedInVehicle && hasTargetVehicle)
                {
                    // Conducteur - contrôle routier
                    mainInteractionMenu.Add(new NativeItem("Demander papiers"));
                    mainInteractionMenu.Add(new NativeItem("Alcootest"));
                    mainInteractionMenu.Add(new NativeItem("Test de drogues"));
                    mainInteractionMenu.Add(new NativeItem("Contraventions"));
                    mainInteractionMenu.Add(new NativeItem("Fouiller le véhicule"));
                    mainInteractionMenu.Add(new NativeItem("Vérifier la plaque"));
                }
                else
                {
                    // Personne à pied - contrôle général
                    mainInteractionMenu.Add(new NativeItem("Demander pièce d'identité"));
                    mainInteractionMenu.Add(new NativeItem("Fouiller la personne"));
                    mainInteractionMenu.Add(new NativeItem("Questions de routine"));
                    mainInteractionMenu.Add(new NativeItem("Test d'alcoolémie"));
                }

                mainInteractionMenu.Add(new NativeItem("Terminer le contrôle"));

                mainInteractionMenu.ItemActivated += OnMainInteractionActivated;
                mainInteractionMenu.Closed += OnMainInteractionClosed;
                uiPool.Add(mainInteractionMenu);

                // Enregistrer et ouvrir le menu via le système centralisé
                if (PoliceSharedData.TryOpenMenu(mainInteractionMenu, "PoliceInteractionModule"))
                {
                    mainInteractionMenu.Visible = true;
                    // Figer le PNJ pendant l'interaction
                    StartInteraction(targetPed);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in OpenInteractionMenu: {ex.Message} {ex.StackTrace}");
                if (mainInteractionMenu != null) mainInteractionMenu.Visible = false;
                PoliceSharedData.ForceCloseMenu();
                EndInteraction(); // Try to unfreeze ped
            }
        }

        private void OnMainInteractionActivated(object sender, LemonUI.Menus.ItemActivatedArgs e)
        {
            try
            {
                string itemText = e.Item.Title;

                switch (itemText)
                {
                    case "Demander papiers":
                    case "Demander pièce d'identité":
                        CheckIdentification();
                        break;

                    case "Alcootest":
                    case "Test d'alcoolémie":
                        PerformBreathalyzer();
                        break;

                    case "Test de drogues":
                        PerformDrugTest();
                        break;

                    case "Contraventions":
                        OpenTrafficTicketMenu();
                        break;

                    case "Fouiller le véhicule":
                        SearchVehicle();
                        break;

                    case "Fouiller la personne":
                        SearchPerson();
                        break;

                    case "Vérifier la plaque":
                        CheckLicensePlate();
                        break;

                    case "Interroger sur l'incident":
                    case "Questions de routine":
                    case "Questions générales":
                        OpenInterrogationMenu();
                        break;

                    case "Demander ses complices":
                        AskAboutComplices();
                        break;

                    case "Lire ses droits":
                        ReadRights();
                        break;

                    case "Terminer le contrôle":
                        mainInteractionMenu.Visible = false;
                        PoliceSharedData.CloseMenuIfOwner("PoliceInteractionModule");
                        EndInteraction();
                        break;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in OnMainInteractionActivated for item '{e.Item?.Title}': {ex.Message} {ex.StackTrace}");
                if (mainInteractionMenu != null) mainInteractionMenu.Visible = false; // Attempt to close menu
                PoliceSharedData.ForceCloseMenu();
                EndInteraction(); // Try to unfreeze ped
            }
        }

        private void OnMainInteractionClosed(object sender, System.EventArgs e)
        {
            try
            {
                PoliceSharedData.CloseMenuIfOwner("PoliceInteractionModule");
                EndInteraction();
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in OnMainInteractionClosed: {ex.Message} {ex.StackTrace}");
                EndInteraction(); // Ensure ped is unfrozen if possible
            }
        }

        private void StartInteraction(Ped ped)
        {
            try
            {
                if (ped == null || !ped.Exists()) return;
                
                isInteracting = true;
                interactingPed = ped;
                
                // Arrêter toutes les tâches actuelles du PNJ
                ped.Task.ClearAll();
                
                // Faire regarder le joueur
                ped.Task.LookAt(Game.Player.Character, -1);
                
                // Empêcher le PNJ de bouger
                ped.Task.StandStill(-1);
                
                // Optionnel : Animation de salut ou d'attention
                if (!ped.IsInVehicle())
                {
                    Script.Wait(500);
                    ped.Task.PlayAnimation("gestures@m@standing@casual", "gesture_hello", 8f, -8f, 1000, AnimationFlags.None, 0f);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in StartInteraction: {ex.Message} {ex.StackTrace}");
                isInteracting = false; // Reset state
                interactingPed = null;
            }
        }

        private void EndInteraction()
        {
            try
            {
                if (interactingPed != null && interactingPed.Exists())
                {
                    // Libérer le PNJ
                    interactingPed.Task.ClearAll();

                    // Optionnel : Animation de fin d'interaction
                    if (!interactingPed.IsInVehicle())
                    {
                        interactingPed.Task.PlayAnimation("gestures@m@standing@casual", "gesture_goodbye", 8f, -8f, 2000, AnimationFlags.None, 0f);
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in EndInteraction (ped cleanup): {ex.Message} {ex.StackTrace}");
            }
            finally // Ensure state is reset even if ped operations fail
            {
                isInteracting = false;
                interactingPed = null;
            }
        }

        private void MaintainInteractionState()
        {
            try
            {
                if (!isInteracting || interactingPed == null || !interactingPed.Exists())
                    return;

                // Vérifier si le PNJ est encore en train de rester immobile
                // try-catch inside for the native call, as the outer try-catch is for the whole MaintainInteractionState
                try
                {
                    bool isStandingStill = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, interactingPed.Handle, 3); // Task type 3 = StandStill

                    // Si le PNJ n'est plus immobile, le remettre en place
                    if (!isStandingStill)
                    {
                        interactingPed.Task.ClearAll();
                        interactingPed.Task.LookAt(Game.Player.Character, -1);
                        interactingPed.Task.StandStill(-1);
                    }
                }
                catch (Exception exNative) // Catching exception from native call specifically
                {
                    GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in MaintainInteractionState (native task check): {exNative.Message} {exNative.StackTrace}");
                    // En cas d'erreur, remettre le PNJ en place
                    if(interactingPed != null && interactingPed.Exists()){
                        interactingPed.Task.ClearAll();
                        interactingPed.Task.LookAt(Game.Player.Character, -1);
                        interactingPed.Task.StandStill(-1);
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in MaintainInteractionState (outer): {ex.Message} {ex.StackTrace}");
                 // Attempt to recover by ending interaction if ped state is unstable
                EndInteraction();
            }
        }

        private void CheckInteractionEnd(Ped player)
        {
            try
            {
                if (!isInteracting) return;

                // Terminer l'interaction si le menu principal n'est plus visible
                if (mainInteractionMenu == null || !mainInteractionMenu.Visible)
                {
                    EndInteraction();
                    return;
                }

                // Terminer l'interaction si le joueur s'éloigne trop
                if (interactingPed != null && interactingPed.Exists())
                {
                    float distance = player.Position.DistanceTo(interactingPed.Position);
                    if (distance > InteractionRange * 2) // Double de la portée d'interaction
                    {
                        if (mainInteractionMenu != null)
                            mainInteractionMenu.Visible = false; // This will trigger OnMainInteractionClosed which calls EndInteraction
                    }
                }
                else
                {
                    // Le PNJ n'existe plus
                    EndInteraction();
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in CheckInteractionEnd: {ex.Message} {ex.StackTrace}");
                EndInteraction(); // Attempt to recover
            }
        }

        private void CheckIdentification()
        {
            try
            {
                GTA.UI.Notification.Show("~b~Contrôle des papiers en cours...");

                Script.Wait(2000); // Simulation du temps de vérification
                
                Random rng = new Random();
                bool hasValidId = rng.Next(100) > 20; // 80% de chance d'avoir des papiers valides
                
                if (hasValidId)
                {
                    string[] names = { "Jean Dupont", "Marie Martin", "Pierre Durand", "Sophie Leclerc", "Antoine Robert" };
                    string name = names[rng.Next(names.Length)];
                    int age = rng.Next(18, 70);

                    GTA.UI.Notification.Show($"~g~Papiers valides\n~w~Nom: {name}\nÂge: {age} ans");

                    // Vérifier si recherché
                    bool isWanted = rng.Next(100) < 10; // 10% de chance d'être recherché
                    if (isWanted)
                    {
                        GTA.UI.Notification.Show("~r~ATTENTION: Personne recherchée !");
                        GTA.UI.Notification.Show("~r~Mandat d'arrêt en cours");
                    }
                }
                else
                {
                    GTA.UI.Notification.Show("~r~Papiers non valides ou manquants");
                    GTA.UI.Notification.Show("~y~Possibilité de verbalisation");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in CheckIdentification: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors du contrôle d'identité.");
            }
        }

        private void PerformBreathalyzer()
        {
            try
            {
                GTA.UI.Notification.Show("~b~Test d'alcoolémie en cours...");

                // Animation du PNJ soufflant
                if(targetPed != null && targetPed.Exists())
                    targetPed.Task.PlayAnimation("anim@mp_player_intupperblow_kiss", "idle_a", 8f, -8f, 3000, AnimationFlags.None, 0f);

                Script.Wait(3000);

                Random rng = new Random();
                float alcoholLevel = (float)(rng.NextDouble() * 2.0); // 0 à 2.0 g/L

                if (alcoholLevel < 0.5f)
                {
                    GTA.UI.Notification.Show($"~g~Test négatif\n~w~Taux: {alcoholLevel:F2} g/L");
                }
                else if (alcoholLevel < 0.8f)
                {
                    GTA.UI.Notification.Show($"~y~Taux limite dépassé\n~w~Taux: {alcoholLevel:F2} g/L\n~y~Contravention requise");
                }
                else
                {
                    GTA.UI.Notification.Show($"~r~Alcoolémie excessive !\n~w~Taux: {alcoholLevel:F2} g/L\n~r~Arrestation nécessaire");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in PerformBreathalyzer: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors du test d'alcoolémie.");
            }
        }

        private void PerformDrugTest()
        {
            try
            {
                GTA.UI.Notification.Show("~b~Test de dépistage de drogues...");

                Script.Wait(2000);

                Random rng = new Random();
                bool isPositive = rng.Next(100) < 15; // 15% de chance d'être positif

                if (isPositive)
                {
                    string[] drugs = { "Cannabis", "Cocaïne", "Amphétamines", "Opiacés" };
                    string drug = drugs[rng.Next(drugs.Length)];
                    GTA.UI.Notification.Show($"~r~Test positif : {drug}\n~r~Arrestation requise");
                }
                else
                {
                    GTA.UI.Notification.Show("~g~Test négatif\n~w~Aucune substance détectée");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in PerformDrugTest: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors du test de drogues.");
            }
        }

        private void OpenTrafficTicketMenu()
        {
            try
            {
                // Fermer le menu principal temporairement
                if (mainInteractionMenu != null) mainInteractionMenu.Visible = false;

                if (trafficTicketMenu != null)
                {
                    if (PoliceSharedData.TryOpenMenu(trafficTicketMenu, "PoliceInteractionModule"))
                    {
                        trafficTicketMenu.Visible = true;
                    }
                    return;
                }

                trafficTicketMenu = new NativeMenu("Contraventions", "Infractions routières");

                foreach (var violation in trafficViolations)
                {
                    trafficTicketMenu.Add(new NativeItem($"{violation.Key} - {violation.Value}€"));
                }

                trafficTicketMenu.Add(new NativeItem("Retour"));

                trafficTicketMenu.ItemActivated += OnTrafficTicketActivated;
                trafficTicketMenu.Closed += OnTrafficTicketClosed;
                uiPool.Add(trafficTicketMenu);

                if (PoliceSharedData.TryOpenMenu(trafficTicketMenu, "PoliceInteractionModule"))
                {
                    trafficTicketMenu.Visible = true;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in OpenTrafficTicketMenu: {ex.Message} {ex.StackTrace}");
                if (trafficTicketMenu != null) trafficTicketMenu.Visible = false;
                PoliceSharedData.ForceCloseMenu();
                // Re-open main menu if it was visible
                if (mainInteractionMenu != null && PoliceSharedData.TryOpenMenu(mainInteractionMenu, "PoliceInteractionModule")) mainInteractionMenu.Visible = true;
            }
        }

        private void OnTrafficTicketActivated(object sender, LemonUI.Menus.ItemActivatedArgs e)
        {
            try
            {
                string itemText = e.Item.Title;

                if (itemText == "Retour")
                {
                    if(trafficTicketMenu != null) trafficTicketMenu.Visible = false;
                    if (mainInteractionMenu != null && PoliceSharedData.TryOpenMenu(mainInteractionMenu, "PoliceInteractionModule"))
                    {
                        mainInteractionMenu.Visible = true;
                    }
                    return;
                }

                // Extraire le montant de l'amende
                string[] parts = itemText.Split('-');
                if (parts.Length > 1)
                {
                    string amountStr = parts[1].Trim().Replace("€", "");
                    GTA.UI.Notification.Show($"~y~Contravention émise\n~w~Montant: {amountStr}€");
                    GTA.UI.Notification.Show("~b~Infraction enregistrée dans le système");
                }

                if(trafficTicketMenu != null) trafficTicketMenu.Visible = false;
                PoliceSharedData.CloseMenuIfOwner("PoliceInteractionModule");
                // No EndInteraction() here as the main interaction might still be ongoing.
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in OnTrafficTicketActivated for item '{e.Item?.Title}': {ex.Message} {ex.StackTrace}");
                if (trafficTicketMenu != null) trafficTicketMenu.Visible = false;
                PoliceSharedData.ForceCloseMenu();
                 if (mainInteractionMenu != null && PoliceSharedData.TryOpenMenu(mainInteractionMenu, "PoliceInteractionModule")) mainInteractionMenu.Visible = true;
            }
        }

        private void OnTrafficTicketClosed(object sender, System.EventArgs e)
        {
            try
            {
                // Retourner au menu principal
                if (mainInteractionMenu != null && PoliceSharedData.TryOpenMenu(mainInteractionMenu, "PoliceInteractionModule"))
                {
                    mainInteractionMenu.Visible = true;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in OnTrafficTicketClosed: {ex.Message} {ex.StackTrace}");
                // Ensure main menu is attempted to be shown or interaction ends
                if (mainInteractionMenu == null || !mainInteractionMenu.Visible) EndInteraction();
            }
        }

        private void SearchVehicle()
        {
            try
            {
                if (targetVehicle == null || !targetVehicle.Exists())
                {
                    GTA.UI.Notification.Show("~r~Aucun véhicule ciblé pour la fouille.");
                    return;
                }

                GTA.UI.Notification.Show("~b~Fouille du véhicule en cours...");

                Script.Wait(3000);

                Random rng = new Random();
                int searchResult = rng.Next(100);

                if (searchResult < 70)
                {
                    GTA.UI.Notification.Show("~g~Rien de suspect trouvé\n~w~Véhicule en règle");
                }
                else if (searchResult < 85)
                {
                    GTA.UI.Notification.Show("~y~Objets interdits trouvés\n~w~Contravention applicable");
                }
                else if (searchResult < 95)
                {
                    GTA.UI.Notification.Show("~r~Substances illicites découvertes !\n~r~Arrestation requise");
                }
                else
                {
                    GTA.UI.Notification.Show("~r~Armes illégales trouvées !\n~r~Arrestation immédiate");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in SearchVehicle: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors de la fouille du véhicule.");
            }
        }

        private void SearchPerson()
        {
            try
            {
                 if (targetPed == null || !targetPed.Exists())
                {
                    GTA.UI.Notification.Show("~r~Aucune personne ciblée pour la fouille.");
                    return;
                }
                GTA.UI.Notification.Show("~b~Fouille corporelle...");

                Script.Wait(2000);

                Random rng = new Random();
                int searchResult = rng.Next(100);

                if (searchResult < 75)
                {
                    GTA.UI.Notification.Show("~g~Rien de suspect\n~w~Personne en règle");
                }
                else if (searchResult < 90)
                {
                    GTA.UI.Notification.Show("~y~Objet suspect trouvé\n~w~Interrogatoire nécessaire");
                }
                else
                {
                    GTA.UI.Notification.Show("~r~Objet illégal découvert !\n~r~Arrestation requise");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in SearchPerson: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors de la fouille corporelle.");
            }
        }

        private void CheckLicensePlate()
        {
            try
            {
                if (targetVehicle == null || !targetVehicle.Exists())
                {
                    GTA.UI.Notification.Show("~r~Aucun véhicule ciblé pour la vérification.");
                    return;
                }

                string plate = Function.Call<string>(Hash.GET_VEHICLE_NUMBER_PLATE_TEXT, targetVehicle.Handle);
                GTA.UI.Notification.Show($"~b~Vérification plaque: {plate}");

                Script.Wait(2000);

                Random rng = new Random();
                bool isStolen = rng.Next(100) < 5; // 5% de chance d'être volé
                bool hasViolations = rng.Next(100) < 20; // 20% de chance d'avoir des infractions

                if (isStolen)
                {
                    GTA.UI.Notification.Show("~r~VÉHICULE VOLÉ !\n~r~Arrestation immédiate du conducteur");
                }
                else if (hasViolations)
                {
                    GTA.UI.Notification.Show("~y~Infractions en cours\n~w~Amendes impayées détectées");
                }
                else
                {
                    GTA.UI.Notification.Show("~g~Véhicule en règle\n~w~Aucune infraction");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in CheckLicensePlate: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors de la vérification de plaque.");
            }
        }

        private void OpenInterrogationMenu()
        {
            try
            {
                // Fermer le menu principal temporairement
                if(mainInteractionMenu != null) mainInteractionMenu.Visible = false;

                if (interrogationMenu != null)
                {
                    if (PoliceSharedData.TryOpenMenu(interrogationMenu, "PoliceInteractionModule"))
                    {
                        interrogationMenu.Visible = true;
                    }
                    return;
                }

                interrogationMenu = new NativeMenu("Interrogatoire", "Questions");

                interrogationMenu.Add(new NativeItem("Que faisiez-vous ?"));
                interrogationMenu.Add(new NativeItem("D'où venez-vous ?"));
                interrogationMenu.Add(new NativeItem("Où allez-vous ?"));
                interrogationMenu.Add(new NativeItem("Connaissez-vous la victime ?"));
                interrogationMenu.Add(new NativeItem("Avez-vous des témoins ?"));
                interrogationMenu.Add(new NativeItem("Retour"));

                interrogationMenu.ItemActivated += OnInterrogationActivated;
                interrogationMenu.Closed += OnInterrogationClosed;
                uiPool.Add(interrogationMenu);

                if (PoliceSharedData.TryOpenMenu(interrogationMenu, "PoliceInteractionModule"))
                {
                    interrogationMenu.Visible = true;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in OpenInterrogationMenu: {ex.Message} {ex.StackTrace}");
                if(interrogationMenu != null) interrogationMenu.Visible = false;
                PoliceSharedData.ForceCloseMenu();
                if (mainInteractionMenu != null && PoliceSharedData.TryOpenMenu(mainInteractionMenu, "PoliceInteractionModule")) mainInteractionMenu.Visible = true;
            }
        }

        private void OnInterrogationActivated(object sender, LemonUI.Menus.ItemActivatedArgs e)
        {
            try
            {
                string question = e.Item.Title;

                if (question == "Retour")
                {
                    if(interrogationMenu != null) interrogationMenu.Visible = false;
                    if (mainInteractionMenu != null && PoliceSharedData.TryOpenMenu(mainInteractionMenu, "PoliceInteractionModule"))
                    {
                        mainInteractionMenu.Visible = true;
                    }
                    return;
                }

                // Générer une réponse aléatoire
                string[] responses = {
                    "~w~Suspect: \"Je ne sais rien !\"",
                    "~w~Suspect: \"J'étais ailleurs...\"",
                    "~w~Suspect: \"Je ne me souviens pas.\"",
                    "~w~Suspect: \"C'est un malentendu !\"",
                    "~w~Suspect: \"Je coopère totalement.\"",
                    "~w~Suspect: \"J'appelle mon avocat !\"",
                    "~w~Suspect: \"Ce n'est pas ce que vous croyez.\""
                };

                Random rng = new Random();
                string response = responses[rng.Next(responses.Length)];

                GTA.UI.Notification.Show($"~b~Question: {question}");
                Script.Wait(1000);
                GTA.UI.Notification.Show(response);

                if(interrogationMenu != null) interrogationMenu.Visible = false;
                PoliceSharedData.CloseMenuIfOwner("PoliceInteractionModule");
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in OnInterrogationActivated for item '{e.Item?.Title}': {ex.Message} {ex.StackTrace}");
                if(interrogationMenu != null) interrogationMenu.Visible = false;
                PoliceSharedData.ForceCloseMenu();
                if (mainInteractionMenu != null && PoliceSharedData.TryOpenMenu(mainInteractionMenu, "PoliceInteractionModule")) mainInteractionMenu.Visible = true;
            }
        }

        private void OnInterrogationClosed(object sender, System.EventArgs e)
        {
            try
            {
                // Retourner au menu principal
                if (mainInteractionMenu != null && PoliceSharedData.TryOpenMenu(mainInteractionMenu, "PoliceInteractionModule"))
                {
                    mainInteractionMenu.Visible = true;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in OnInterrogationClosed: {ex.Message} {ex.StackTrace}");
                if (mainInteractionMenu == null || !mainInteractionMenu.Visible) EndInteraction();
            }
        }

        private void AskAboutComplices()
        {
            try
            {
                Random rng = new Random();
                string[] responses = {
                    "~w~Suspect: \"J'ai agi seul !\"",
                    "~w~Suspect: \"Je ne balance personne.\"",
                    "~w~Suspect: \"Il y avait... non, rien.\"",
                    "~w~Suspect: \"Je ne connais personne !\"",
                    "~w~Suspect: \"Mon avocat d'abord !\""
                };

                string response = responses[rng.Next(responses.Length)];
                GTA.UI.Notification.Show(response);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in AskAboutComplices: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors de l'interrogatoire.");
            }
        }

        private void ReadRights()
        {
            try
            {
                GTA.UI.Notification.Show("~b~Lecture des droits Miranda...");
                Script.Wait(1000);
                GTA.UI.Notification.Show("~w~\"Vous avez le droit de garder le silence.\"");
                Script.Wait(2000);
                GTA.UI.Notification.Show("~w~\"Tout ce que vous direz pourra être retenu contre vous.\"");
                Script.Wait(2000);
                GTA.UI.Notification.Show("~w~\"Vous avez droit à un avocat.\"");
                Script.Wait(2000);
                GTA.UI.Notification.Show("~g~Droits lus et compris");
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in ReadRights: {ex.Message} {ex.StackTrace}");
                GTA.UI.Notification.Show("~r~Erreur lors de la lecture des droits.");
            }
        }

        private bool IsCop(Ped ped)
        {
            // Vérifier plusieurs critères pour identifier un policier
            if (ped.Model.Hash.GetHashCode() == PedHash.Cop01SMY.GetHashCode() || ped.Model.Hash.GetHashCode() == PedHash.Cop01SFY.GetHashCode() || ped.Model.Hash.GetHashCode() == PedHash.Sheriff01SMY.GetHashCode() || ped.Model.Hash.GetHashCode() == PedHash.Sheriff01SFY.GetHashCode()) {
                return true;
            }

            // Vérifier le groupe relationnel
            try
            {
                int copGroup = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
                return ped.RelationshipGroup.Hash == copGroup;
            }
            catch (Exception ex) // Catch specific exception from native call if needed, or general
            {
                GTA.UI.Notification.Show($"~r~PoliceInteractionModule error in IsCop (RelationshipGroup check): {ex.Message} {ex.StackTrace}");
                return false; // Safer to assume not a cop if check fails
            }
        }
    }
} 