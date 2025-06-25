using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace REALIS.Police.Callouts
{
    /// <summary>
    /// Callout de braquage de banque avec otages et négociation.
    /// Événement majeur avec multiples phases.
    /// </summary>
    public class BankRobberyCallout : CalloutBase
    {
        private readonly List<Ped> _robbers = new List<Ped>();
        private readonly List<Vehicle> _getawayVehicles = new List<Vehicle>();
        private readonly List<Ped> _hostages = new List<Ped>();
        private readonly List<Blip> _blips = new List<Blip>();
        private readonly List<Ped> _spawnedSwat = new List<Ped>();
        private readonly List<Ped> _perimeterCops = new List<Ped>();
        private readonly List<Vehicle> _perimeterVehicles = new List<Vehicle>();
        
        private Vector3 _bankLocation;
        private bool _negotiationActive;
        private bool _assaultPhase;
        private bool _escapePhase; // Not yet implemented
        private DateTime _lastNegotiation;
        private int _negotiationAttempts;
        // private bool _shotsHard; // This variable was unused

        public BankRobberyCallout() : base("BRAQUAGE DE BANQUE", 
            "Braquage en cours dans une banque. Situation d'otages confirmée.", 
            new Vector3(146.78f, -1045.71f, 29.37f)) // Fleeca Bank Downtown
        {
        }

        public override bool CanSpawn()
        {
            // Toujours disponible pour les tests  
            return true;
        }

        protected override void OnStart()
        {
            try
            {
                GTA.UI.Notification.Show("~r~BankRobberyCallout: OnStart initiated.");
                _bankLocation = StartPosition;
                _negotiationActive = true;
                _lastNegotiation = DateTime.Now;
                _negotiationAttempts = 0;
                _assaultPhase = false;
                _escapePhase = false;

                // Clear lists at the start of a new callout instance
                _robbers.Clear();
                _getawayVehicles.Clear();
                _hostages.Clear();
                _blips.Clear();
                _spawnedSwat.Clear();
                _perimeterCops.Clear();
                _perimeterVehicles.Clear();

                AddObjective("GO_TO_BANK", "Se rendre à la banque.", false);
                // AddObjective("ASSESS_SITUATION", "Évaluer la situation.", false); // Will be added once at bank

                SpawnRobbersAndHostages();
                SetupPerimeter();
                
                GTA.UI.Notification.Show("~r~BRAQUAGE EN COURS!~w~~n~Négociation disponible avec la touche ~g~T~w~");
                GTA.UI.Notification.Show("~b~Dispatch:~w~ SWAT disponible. Utilisez ~g~B~w~ pour déployer l'assaut.");
                GTA.UI.Notification.Show("~r~BankRobberyCallout: OnStart completed successfully.");
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout OnStart error: {ex.Message} {ex.StackTrace}");
                End();
            }
        }

        private void SpawnRobbersAndHostages()
        {
            try
            {
                Random rng = new Random();
                int numberOfRobbers = rng.Next(2, 5); // Randomly 2, 3, or 4 robbers

                var robberModels = new PedHash[] { PedHash.Lost01GMY, PedHash.Lost02GMY, PedHash.Lost03GMY, PedHash.BallaEast01GMY }; // Added one more model variety

                for (int i = 0; i < numberOfRobbers; i++)
                {
                    Model currentModel = robberModels[rng.Next(robberModels.Length)];
                    var robber = World.CreatePed(currentModel, _bankLocation + Vector3.RandomXY() * 5f);
                    if (robber == null) continue;

                    // Randomize weapon and stats slightly
                    WeaponHash robberWeapon = WeaponHash.AssaultRifle;
                    int robberArmor = 100;
                    int robberHealth = 250 + rng.Next(-25, 76); // Health between 225 and 325

                    if (rng.NextDouble() < 0.3) // 30% chance for a slightly better weapon or more armor
                    {
                        robberWeapon = WeaponHash.CarbineRifle;
                        robberArmor = 125;
                    }
                     if (i == 0 && numberOfRobbers > 2 && rng.NextDouble() < 0.5) // Leader might be tougher
                    {
                        robberHealth = 350;
                        robberArmor = 150;
                        robberWeapon = WeaponHash.AdvancedRifle;
                    }


                    robber.Weapons.Give(robberWeapon, 250, true, true);
                    robber.Armor = robberArmor;
                    robber.MaxHealth = robberHealth;
                    robber.Health = robberHealth;
                    robber.Accuracy = rng.Next(20, 51); // Random accuracy between 20 and 50
                    robber.ShootRate = rng.Next(80, 121); // Random shoot rate
                    robber.BlockPermanentEvents = true;
                    robber.Task.GuardCurrentPosition();
                    _robbers.Add(robber);

                    var robberBlip = robber.AddBlip();
                    robberBlip.Sprite = BlipSprite.Enemy;
                    robberBlip.Color = BlipColor.Red;
                    robberBlip.Name = "Braqueur";
                    _blips.Add(robberBlip);
                }

                var getawayCar = World.CreateVehicle(VehicleHash.Kuruma, _bankLocation + new Vector3(10f, 0f, 0f));
                if (getawayCar != null)
                {
                    Function.Call(Hash.SET_VEHICLE_ENGINE_ON, getawayCar, false, true, false);
                    getawayCar.LockStatus = VehicleLockStatus.Locked;
                    _getawayVehicles.Add(getawayCar);

                    var carBlip = getawayCar.AddBlip();
                    carBlip.Sprite = BlipSprite.GetawayCar;
                    carBlip.Color = BlipColor.Red;
                    carBlip.Name = "Véhicule d'évasion";
                    _blips.Add(carBlip);
                }

                for (int i = 0; i < 5; i++)
                {
                    var hostage = World.CreateRandomPed(_bankLocation + Vector3.RandomXY() * 3f);
                    if (hostage == null) continue;

                    hostage.Task.HandsUp(-1);
                    hostage.AlwaysKeepTask = true;
                    hostage.BlockPermanentEvents = true;
                    hostage.CanBeDraggedOutOfVehicle = false;
                    _hostages.Add(hostage);

                    var hostageBlip = hostage.AddBlip();
                    hostageBlip.Sprite = BlipSprite.Friend;
                    hostageBlip.Color = BlipColor.Blue;
                    hostageBlip.Name = "Otage";
                    _blips.Add(hostageBlip);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout SpawnRobbersAndHostages error: {ex.Message} {ex.StackTrace}");
            }
        }

        private void SetupPerimeter()
        {
            try
            {
                var positions = new Vector3[]
                {
                    _bankLocation + new Vector3(30f, 15f, 0f),
                    _bankLocation + new Vector3(-25f, 20f, 0f),
                    _bankLocation + new Vector3(20f, -30f, 0f)
                };

                foreach (var pos in positions)
                {
                    var policecar = World.CreateVehicle(VehicleHash.Police, pos);
                    if (policecar != null)
                    {
                        policecar.PlaceOnGround();
                        policecar.Heading = (_bankLocation - policecar.Position).ToHeading();
                        Function.Call(Hash.SET_VEHICLE_SIREN, policecar, true);
                        _perimeterVehicles.Add(policecar);

                        var cop = World.CreatePed(PedHash.Cop01SMY, policecar.Position - policecar.ForwardVector * 2f);
                        if (cop != null)
                        {
                            cop.SetIntoVehicle(policecar, VehicleSeat.Driver);
                            cop.Task.GuardCurrentPosition();
                            cop.BlockPermanentEvents = true;
                            _perimeterCops.Add(cop);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout SetupPerimeter error: {ex.Message} {ex.StackTrace}");
            }
        }

        protected override void OnUpdate()
        {
            try
            {
                if (!IsActive) return;
                var player = Game.Player.Character;
                if (player == null || !player.Exists()) { End(); return; }
                if (player.IsDead && IsActive) { GTA.UI.Notification.Show("~r~ÉCHEC DE LA MISSION!~w~ Officier hors-combat."); End(); return; }

                var goToBankObjective = GetObjective("GO_TO_BANK");
                if (goToBankObjective != null && goToBankObjective.Status == CalloutObjectiveStatus.InProgress)
                {
                    if (player.Position.DistanceTo(_bankLocation) < 50f) // Player is near the bank
                    {
                        UpdateObjectiveStatus("GO_TO_BANK", CalloutObjectiveStatus.Completed);
                        AddObjective("NEGOTIATE_OR_ASSAULT", "Négocier avec les braqueurs ou lancer l'assaut.", false);
                        // Optionally, add a time limit for this phase.
                    }
                }

                if (_negotiationActive) HandleNegotiation();
                else if (_assaultPhase) HandleAssault();
                else if (_escapePhase) HandleEscape();

                CheckProgressConditions(); // This might call End()
                if (!IsActive) return; // If CheckProgressConditions ended the callout

                DisplayInstructions();
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout OnUpdate error: {ex.Message} {ex.StackTrace}");
                End();
            }
        }

        private void HandleNegotiation()
        {
            try
            {
                if (Game.IsKeyPressed(System.Windows.Forms.Keys.T) && (DateTime.Now - _lastNegotiation).TotalSeconds > 10)
                {
                    _lastNegotiation = DateTime.Now;
                    _negotiationAttempts++;
                    
                    var success = new Random().NextDouble() < (0.1f + _negotiationAttempts * 0.15f);
                    
                    if (success && _negotiationAttempts < 4)
                    {
                        GTA.UI.Notification.Show("~g~SUCCÈS!~w~ Les braqueurs acceptent de se rendre!");
                        UpdateObjectiveStatus("NEGOTIATE_OR_ASSAULT", CalloutObjectiveStatus.Completed, false); // No separate notif for this step
                        StartSurrender();
                    }
                    else
                    {
                        var responses = new string[]
                        {
                            "~r~Braqueur:~w~ Pas question! Restez où vous êtes!",
                            "~r~Braqueur:~w~ On a des otages! Ne tentez rien!",
                            "~r~Braqueur:~w~ Amenez-nous un hélico ou ils meurent!"
                        };
                        GTA.UI.Notification.Show(responses[new Random().Next(responses.Length)]);

                        if (_negotiationAttempts >= 3)
                        {
                            GTA.UI.Notification.Show("~r~Négociation échouée.~w~ Préparez l'assaut!");
                            // Objective "NEGOTIATE_OR_ASSAULT" remains in progress or could be failed if player must assault
                        }
                    }
                }

                if (Game.IsKeyPressed(System.Windows.Forms.Keys.B) && _negotiationActive) // Can only start assault if negotiation was active
                {
                    UpdateObjectiveStatus("NEGOTIATE_OR_ASSAULT", CalloutObjectiveStatus.Failed, false); // Negotiation abandoned for assault
                    StartAssault();
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout HandleNegotiation error: {ex.Message} {ex.StackTrace}");
            }
        }

        private void StartSurrender()
        {
            try
            {
                _negotiationActive = false;
                _assaultPhase = false;
                _escapePhase = false;

                AddObjective("SECURE_SURRENDER", "S'assurer de la reddition des suspects.", false);

                foreach (var robber in _robbers.Where(r => r.Exists() && !r.IsDead))
                {
                    robber.Task.HandsUp(-1);
                    robber.AlwaysKeepTask = true;
                    robber.Weapons.RemoveAll();
                }

                foreach (var hostage in _hostages.Where(h => h.Exists() && !h.IsDead))
                {
                    hostage.Task.FleeFrom(_bankLocation);
                }

                UpdateObjectiveStatus("SECURE_SURRENDER", CalloutObjectiveStatus.Completed);
                GTA.UI.Notification.Show("~g~Mission réussie!~w~ Tous les suspects se rendent pacifiquement.");
                End();
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout StartSurrender error: {ex.Message} {ex.StackTrace}");
                UpdateObjectiveStatus("SECURE_SURRENDER", CalloutObjectiveStatus.Failed, false);
                End(); // Ensure callout ends even if surrender logic fails
            }
        }

        private void StartAssault()
        {
            try
            {
                if (_assaultPhase) return; // Prevent starting multiple times if already in assault
                // _negotiationActive check removed, as HandleNegotiation now controls when StartAssault can be called

                _negotiationActive = false; // Negotiation is over
                _assaultPhase = true;

                AddObjective("NEUTRALIZE_ROBBERS", "Neutraliser tous les braqueurs.", false);
                AddObjective("PROTECT_HOSTAGES", "Protéger les otages (max 1 victime).", true); // Optional or make it mandatory with different threshold

                GTA.UI.Notification.Show("~r~ASSAUT LANCÉ!~w~ SWAT en approche!");

                Ped playerChar = Game.Player.Character;

                // Hostage escape attempt
                if (_hostages.Any(h => h.Exists() && !h.IsDead))
                {
                    if (new Random().NextDouble() < 0.25) // 25% chance for a hostage to try to escape
                    {
                        var escapingHostage = _hostages.Where(h => h.Exists() && !h.IsDead).OrderBy(h => new Random().Next()).FirstOrDefault();
                        if (escapingHostage != null)
                        {
                            GTA.UI.Notification.Show("~y~Un otage tente de s'échapper!");
                            escapingHostage.Task.FleeFrom(_bankLocation); // Task them to flee from the bank center
                            // Robbers might react to this if their AI is set up for it (e.g. if they see a fleeing ped they are hostile to)
                            // For now, this is a simple trigger.
                        }
                    }
                }


                for (int i = 0; i < 4; i++)
                {
                    var swatSpawnPos = _bankLocation + Vector3.RandomXY().Normalized * 25f + new Vector3(0,0,1);
                    var swat = World.CreatePed(PedHash.Swat01SMY, swatSpawnPos);
                    if (swat == null) continue;

                    swat.Weapons.Give(WeaponHash.CarbineRifle, 300, true, true);
                    swat.Armor = 150;
                    _spawnedSwat.Add(swat);

                    Ped targetRobber = _robbers.FirstOrDefault(r => r.Exists() && !r.IsDead);
                    if (targetRobber != null)
                    {
                        Function.Call(Hash.TASK_COMBAT_PED, swat.Handle, targetRobber.Handle, 0, 16);
                    } else {
                        swat.Task.GoTo(_bankLocation);
                    }
                }

                foreach (var robber in _robbers.Where(r => r.Exists() && !r.IsDead))
                {
                    if(playerChar != null && playerChar.Exists())
                        Function.Call(Hash.TASK_COMBAT_PED, robber.Handle, playerChar.Handle, 0, 16);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout StartAssault error: {ex.Message} {ex.StackTrace}");
                _assaultPhase = false; // Reset phase if it failed to start properly
            }
        }

        private void HandleAssault()
        {
            try
            {
                if (!_assaultPhase) return;

                if (_robbers.All(r => !r.Exists() || r.IsDead))
                {
                    _assaultPhase = false;
                    UpdateObjectiveStatus("NEUTRALIZE_ROBBERS", CalloutObjectiveStatus.Completed);
                    GTA.UI.Notification.Show("~g~Zone sécurisée!~w~ Tous les braqueurs neutralisés.");

                    // Check hostage objective status before ending
                    var deadHostages = _hostages.Count(h => h.Exists() && h.IsDead);
                    if (GetObjective("PROTECT_HOSTAGES")?.Status == CalloutObjectiveStatus.InProgress) // Check if not already failed
                    {
                        if (deadHostages <= 1) UpdateObjectiveStatus("PROTECT_HOSTAGES", CalloutObjectiveStatus.Completed);
                        else UpdateObjectiveStatus("PROTECT_HOSTAGES", CalloutObjectiveStatus.Failed);
                    }

                    foreach (var hostage in _hostages.Where(h => h.Exists() && !h.IsDead))
                    {
                        hostage.Task.FleeFrom(_bankLocation);
                    }
                    End(); // End callout after objectives are updated
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout HandleAssault error: {ex.Message} {ex.StackTrace}");
                UpdateObjectiveStatus("NEUTRALIZE_ROBBERS", CalloutObjectiveStatus.Failed, false);
                End(); // End callout if assault handling errors out
            }
        }

        private void HandleEscape()
        {
            // Logic for escape phase if needed
        }

        private void CheckProgressConditions()
        {
            try
            {
                if (!IsActive) return;

                var player = Game.Player.Character;
                if (player == null || !player.Exists()) { End(); return; }

                // Hostage protection objective
                var protectHostagesObj = GetObjective("PROTECT_HOSTAGES");
                if (protectHostagesObj != null && protectHostagesObj.Status == CalloutObjectiveStatus.InProgress)
                {
                    var deadHostages = _hostages.Count(h => h.Exists() && h.IsDead);
                    if (deadHostages > 1) // Max 1 victim allowed for success of this objective
                    {
                        UpdateObjectiveStatus("PROTECT_HOSTAGES", CalloutObjectiveStatus.Failed);
                        GTA.UI.Notification.Show("~r~ Trop d'otages ont été perdus!");
                        // Depending on rules, this could fail the entire callout, or just this optional objective
                        // If "PROTECT_HOSTAGES" was mandatory, we might End() here.
                        // For now, assume it's optional or its failure doesn't auto-end the callout immediately unless it's the only path.
                    }
                }


                // Player death is handled in OnUpdate's initial check
                // Player abandoning area
                if (player.Position.DistanceTo(_bankLocation) > 250f)
                {
                    GTA.UI.Notification.Show("~r~ÉCHEC DE LA MISSION!~w~ Zone abandonnée.");
                    // Fail active objectives before ending
                    if (GetObjective("NEGOTIATE_OR_ASSAULT")?.Status == CalloutObjectiveStatus.InProgress) UpdateObjectiveStatus("NEGOTIATE_OR_ASSAULT", CalloutObjectiveStatus.Failed, false);
                    if (GetObjective("NEUTRALIZE_ROBBERS")?.Status == CalloutObjectiveStatus.InProgress) UpdateObjectiveStatus("NEUTRALIZE_ROBBERS", CalloutObjectiveStatus.Failed, false);
                    End();
                    return;
                }
                 // Check if all mandatory objectives have failed
                if (HasAnyMandatoryObjectiveFailed())
                {
                    GTA.UI.Notification.Show("~r~ÉCHEC DE LA MISSION!~w~ Un objectif critique a échoué.");
                    End();
                    return;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout CheckProgressConditions error: {ex.Message} {ex.StackTrace}");
                End();
            }
        }

        private void DisplayInstructions()
        {
            try
            {
                if (_negotiationActive && IsActive)
                {
                    GTA.UI.Screen.ShowHelpTextThisFrame("~g~T~w~ - Négocier | ~g~B~w~ - Assaut SWAT");
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout DisplayInstructions error: {ex.Message} {ex.StackTrace}");
            }
        }

        protected override void OnEnd()
        {
            try
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout: OnEnd initiated. IsActive: {IsActive}");

                foreach (var blip in _blips.Where(b => b.Exists())) blip.Delete();
                _blips.Clear();

                foreach (var robber in _robbers.Where(r => r.Exists())) { robber.MarkAsNoLongerNeeded(); robber.Delete(); }
                _robbers.Clear();

                foreach (var vehicle in _getawayVehicles.Where(v => v.Exists())) { vehicle.MarkAsNoLongerNeeded(); vehicle.Delete(); }
                _getawayVehicles.Clear();

                foreach (var hostage in _hostages.Where(h => h.Exists())) { hostage.MarkAsNoLongerNeeded(); hostage.Delete(); }
                _hostages.Clear();

                foreach (var swatMember in _spawnedSwat.Where(s => s.Exists())) { swatMember.MarkAsNoLongerNeeded(); swatMember.Delete(); }
                _spawnedSwat.Clear();

                foreach (var cop in _perimeterCops.Where(c => c.Exists())) { cop.MarkAsNoLongerNeeded(); cop.Delete(); }
                _perimeterCops.Clear();
                foreach (var vehicle in _perimeterVehicles.Where(v => v.Exists())) { vehicle.MarkAsNoLongerNeeded(); vehicle.Delete(); }
                _perimeterVehicles.Clear();

                GTA.UI.Notification.Show("~r~BankRobberyCallout OnEnd: Entities cleaned up.");
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.Show($"~r~BankRobberyCallout OnEnd error: {ex.Message} {ex.StackTrace}");
            }
            finally
            {
                 // Ensure base.End() is not called here as it's handled by CalloutManager
                 // IsActive should be set to false by CalloutManager after this returns or if End() is called directly
                GTA.UI.Notification.Show("~r~BankRobberyCallout: OnEnd finished.");
            }
        }
    }
} 