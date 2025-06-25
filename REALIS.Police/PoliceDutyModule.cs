using System;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using REALIS.Common;
using GTA.Math;
using REALIS.Core; // Added for Logger

namespace REALIS.Job
{
    /// <summary>
    /// Permet au joueur de prendre ou quitter son service de policier en approchant un commissariat.
    /// Étape 2 du système LSPDFR-like.
    /// </summary>
    public class PoliceDutyModule : IModule
    {
        private const float InteractionRange = 3.0f; // Distance pour interagir
        // Utilise le Control.Context (touche 'E' par défaut sur PC)

        private bool _onDuty;
        private bool _showingPrompt;
        private Vehicle _serviceVehicle;
        private int _originalArmor;
        private int _originalMaxWanted;
        private Blip _serviceBlip;

        // Un court délai après avoir quitté le service durant lequel la police continue d'ignorer le joueur.
        private bool _postDutyCooldownActive;
        private DateTime _postDutyCooldownStart;
        private readonly TimeSpan _postDutyDuration = TimeSpan.FromSeconds(10);

        public void Initialize() { /* Rien à initialiser */ }

        public void Update()
        {
            try
            {
                var player = Game.Player.Character;
                if (!player.Exists() || player.IsDead) return;

                // Recherche du commissariat le plus proche
                Vector3 nearest = PoliceStations.Locations
                    .OrderBy(pos => pos.DistanceTo(player.Position))
                    .FirstOrDefault();

                float dist = player.Position.DistanceTo(nearest);

                if (dist <= InteractionRange)
                {
                    // Afficher la prompt à chaque frame quand le joueur est dans la zone
                    _showingPrompt = true;
                    string msg = _onDuty
                        ? "~INPUT_CONTEXT~ Quitter le service" // INPUT_CONTEXT = touche E / Enter véhicule
                        : "~INPUT_CONTEXT~ Prendre son service";
                    GTA.UI.Screen.ShowHelpTextThisFrame(msg);

                    // Détection de la touche d'action
                    if (Game.IsControlJustPressed(GTA.Control.Context))
                    {
                        if (_onDuty) EndDuty(player);
                        else StartDuty(player);
                    }
                }
                else
                {
                    _showingPrompt = false;
                }

                // Si en service, s'assurer qu'aucune étoile n'est attribuée
                if (_onDuty)
                {
                    // Aucune étoile ne doit être attribuée lorsque le joueur est en service.
                    if (Game.Player.WantedLevel > 0)
                    {
                        Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, Game.Player);
                        Game.Player.WantedLevel = 0;
                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
                    }

                    // Gestion du blip du véhicule de service
                    if (_serviceVehicle != null && _serviceVehicle.Exists())
                    {
                        bool playerInVeh = Game.Player.Character.IsInVehicle(_serviceVehicle);

                        if (!playerInVeh && (_serviceBlip == null || !_serviceBlip.Exists()))
                        {
                            _serviceBlip = _serviceVehicle.AddBlip();
                            _serviceBlip.Sprite = BlipSprite.PoliceCar;
                            _serviceBlip.Color = BlipColor.Blue;
                            _serviceBlip.Name = "Véhicule de patrouille";
                        }
                        else if (playerInVeh && _serviceBlip != null && _serviceBlip.Exists())
                        {
                            _serviceBlip.Delete();
                            _serviceBlip = null;
                        }
                    }
                }
                else if (_postDutyCooldownActive)
                {
                    // Durant quelques secondes après la fin du service, on force la suppression d'éventuelles étoiles
                    if (Game.Player.WantedLevel > 0)
                    {
                        Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, Game.Player);
                        Game.Player.WantedLevel = 0;

                        // S'assure que l'état interne du jeu est instantanément mis à jour
                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
                    }

                    // Fin du cooldown après la durée définie
                    if (DateTime.Now - _postDutyCooldownStart > _postDutyDuration)
                    {
                        _postDutyCooldownActive = false;
                        Game.Player.IgnoredByPolice = false;
                        Game.Player.DispatchsCops = true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Ne pas faire crasher le jeu
                Logger.Error($"PoliceDutyModule error in Update: {ex.Message} {ex.StackTrace}");
            }
        }

        public void Dispose()
        {
            if (_onDuty && Game.Player.Character.Exists())
            {
                EndDuty(Game.Player.Character);
            }
        }

        private void StartDuty(Ped player)
        {
            _onDuty = true;

            // Annule tout cooldown post-duty encore actif si l'on reprend le service rapidement
            _postDutyCooldownActive = false;

            // Sauvegarder l'armure initiale pour la restaurer plus tard
            _originalArmor = player.Armor;

            // Sauvegarder le Max Wanted Level du jeu et le réduire à 0
            _originalMaxWanted = Game.MaxWantedLevel;
            Game.MaxWantedLevel = 0;
            Game.Player.WantedLevel = 0;

            // Désactiver la recherche par la police
            Game.Player.IgnoredByPolice = true;
            Game.Player.DispatchsCops = true;

            // S'assurer que les autres policiers ne considèrent pas le joueur comme hostile
            int playerGroup = Game.Player.Character.RelationshipGroup.Hash;
            // "COP" est le groupe relationnel par défaut des forces de l'ordre dans GTA V
            int copGroup = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
            // 1 = Respect / Companion  (voir natives)
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, playerGroup, copGroup);
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 1, copGroup, playerGroup);

            // Appliquer l'uniforme
            PoliceLoadout.ApplyUniform(player);

            // Equiper l'arme de service
            player.Weapons.Give(WeaponHash.Pistol, 120, true, true);
            player.Armor = 100;

            // Spawner un véhicule à proximité du commissariat
            _serviceVehicle = PoliceLoadout.SpawnServiceVehicle(player.Position);
            if (_serviceVehicle != null && _serviceVehicle.Exists())
            {
                // Empêcher que le véhicule soit considéré comme volé
                Function.Call(Hash.SET_VEHICLE_IS_STOLEN, _serviceVehicle.Handle, false);
                GTA.UI.Notification.Show("~b~Véhicule de patrouille assigné");

                _serviceBlip = _serviceVehicle.AddBlip();
                _serviceBlip.Sprite = BlipSprite.PoliceCar;
                _serviceBlip.Color = BlipColor.Blue;
                _serviceBlip.Name = "Véhicule de patrouille";
            }

            GTA.UI.Notification.Show("~b~Vous êtes maintenant en service policier");

            DutyState.PoliceOnDuty = true;
        }

        private void EndDuty(Ped player)
        {
            _onDuty = false;

            // Active la période de cooldown post-duty pour éviter que des crimes enregistrés juste avant ne déclenchent des étoiles
            _postDutyCooldownActive = true;
            _postDutyCooldownStart = DateTime.Now;

            // On s'assure que la police ignore toujours le joueur durant ce cooldown
            Game.Player.IgnoredByPolice = true;
            Game.Player.DispatchsCops = true;

            // Rétablir les relations par défaut (3 = Neutral)
            int playerGroup = Game.Player.Character.RelationshipGroup.Hash;
            int copGroup = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 3, playerGroup, copGroup);
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 3, copGroup, playerGroup);

            // Supprimer l'équipement policier
            player.Weapons.Remove(WeaponHash.Pistol);
            player.Armor = _originalArmor;

            // Restaurer la tenue par défaut
            Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, player);

            // Supprimer le véhicule de service s'il n'est plus utilisé
            if (_serviceVehicle != null && _serviceVehicle.Exists())
            {
                if (player.IsInVehicle(_serviceVehicle))
                {
                    player.Task.LeaveVehicle(_serviceVehicle, true);
                }
                _serviceVehicle.MarkAsNoLongerNeeded();
                _serviceVehicle.Delete();
                _serviceVehicle = null;
            }

            if (_serviceBlip != null && _serviceBlip.Exists())
            {
                _serviceBlip.Delete();
                _serviceBlip = null;
            }

            // Nettoyer complètement toute trace de criminalité enregistrée durant le service AVANT de ré-autoriser la police
            Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, Game.Player);
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);
            Game.Player.WantedLevel = 0;

            // S'assure que l'état interne du jeu est instantanément mis à jour
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player, false);

            // On ne restaure le MaxWantedLevel qu'après avoir nettoyé les crimes
            Game.MaxWantedLevel = _originalMaxWanted;

            // La remise à la normale de la police sera faite à la fin du cooldown dans Update()

            GTA.UI.Notification.Show("~w~Vous avez quitté le service (cooldown anti-étoiles actif)");

            DutyState.PoliceOnDuty = false;
        }
    }
} 