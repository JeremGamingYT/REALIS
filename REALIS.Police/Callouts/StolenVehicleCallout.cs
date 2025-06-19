using GTA;
using GTA.Math;
using GTA.Native;
using System;

namespace REALIS.Police.Callouts
{
    /// <summary>
    /// Exemple de callout: un véhicule volé à intercepter.
    /// Sert de démonstration pour la nouvelle architecture.
    /// </summary>
    public class StolenVehicleCallout : CalloutBase
    {
        private Vehicle _suspectVehicle;
        private Ped _suspect;
        private Blip _blip;

        public static Ped CurrentSuspect { get; private set; }

        private bool _pullOverRequested;
        private bool _suspectComplies;
        private bool _waitingExit;

        public StolenVehicleCallout() : base("Véhicule volé", "Un véhicule vient d'être signalé comme volé.",
            World.GetNextPositionOnStreet(Game.Player.Character.Position + Game.Player.Character.ForwardVector * 100f))
        {
        }

        protected override void OnStart()
        {
            // Crée le véhicule et le suspect
            Model vehModel = new Model(VehicleHash.Premier);
            vehModel.Request(1000);
            if (!vehModel.IsInCdImage || !vehModel.IsValid) return;

            _suspectVehicle = World.CreateVehicle(vehModel, StartPosition);
            if (_suspectVehicle == null) return;

            _suspect = World.CreateRandomPed(StartPosition);
            if (_suspect == null) return;

            _suspect.SetIntoVehicle(_suspectVehicle, VehicleSeat.Driver);
            _suspect.Task.CruiseWithVehicle(_suspectVehicle, 25f, DrivingStyle.Rushed);

            // Marque sur la carte
            _blip = _suspectVehicle.AddBlip();
            _blip.Sprite = BlipSprite.Enemy;
            _blip.Color = BlipColor.Red;
            _blip.Name = "Véhicule suspect";
            _blip.IsFlashing = true;
            
            // Empêche le PNJ d'avoir des comportements indésirables
            _suspect.BlockPermanentEvents = true;

            CurrentSuspect = _suspect;

            GTA.UI.Notification.Show("~b~Callout accepté :~w~ Interceptez le véhicule volé.");
        }

        protected override void OnUpdate()
        {
            var player = Game.Player.Character;

            // Met à jour la zone de recherche pour suivre le véhicule
            if (_blip != null && _blip.Exists())
            {
                _blip.Position = _suspectVehicle.Position;
            }

            // Si les entités principales n'existent plus ou que le suspect est mort : fin.
            if (!_suspect.Exists() || _suspect.IsDead || !_suspectVehicle.Exists())
            {
                GTA.UI.Notification.Show("~g~Suspect neutralisé. Callout terminé.");
                End();
                return;
            }

            // Demande de stop (touche Y) si proche et sirène active
            if (!_pullOverRequested && Game.IsKeyPressed(System.Windows.Forms.Keys.Y) && player.Position.DistanceTo(_suspectVehicle.Position) < 40f)
            {
                _pullOverRequested = true;
                _suspectComplies = new Random().NextDouble() < 0.6; // 60% de coopération
                GTA.UI.Notification.Show("~b~Dispatch:~w~ Ordre de s'arrêter envoyé");

                if (_suspectComplies)
                {
                    // Stoppe le véhicule et force la reddition complète
                    _suspectVehicle.Speed = 0f;
                    Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, _suspectVehicle, true);
                    Function.Call(Hash.SET_VEHICLE_ENGINE_ON, _suspectVehicle, false, true, false);

                    TaskSequence seq = new TaskSequence();
                    seq.AddTask.LeaveVehicle(_suspectVehicle, true);
                    seq.AddTask.HandsUp(-1);
                    seq.Close();
                    _suspect.Task.PerformSequence(seq);
                    _suspect.AlwaysKeepTask = true;
                    _waitingExit = true;
                }
                else
                {
                    // Le suspect accélère
                    _suspect.Task.CruiseWithVehicle(_suspectVehicle, 35f, DrivingStyle.Rushed);
                    GTA.UI.Notification.Show("~r~Le suspect refuse d'obtempérer !");
                }
            }

            // Si le véhicule est bloqué et le joueur juste devant, forcer sortie
            if (!_suspect.IsInVehicle() && _suspect.IsCuffed) { /* already out */ }
            else if (_suspect.IsInVehicle(_suspectVehicle) && _suspectVehicle.Speed < 0.3f && player.Position.DistanceTo(_suspectVehicle.Position) < 5f)
            {
                GTA.UI.Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Sortir le suspect");
                if (Game.IsControlJustPressed(GTA.Control.Context))
                {
                    _suspect.Task.LeaveVehicle(_suspectVehicle, true);
                    _suspect.Task.HandsUp(-1);
                }
            }

            // Afficher une aide contextuelle quand le joueur est proche du suspect immobilisé.
            if (player.Position.DistanceTo(_suspect.Position) < 3f && _suspectVehicle.Speed < 0.1f)
            {
                GTA.UI.Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Mettre fin au callout");

                // Permettre au joueur de terminer manuellement (touche E par défaut)
                if (Game.IsControlJustPressed(GTA.Control.Context))
                {
                    GTA.UI.Notification.Show("~g~Callout terminé.");
                    End();
                }
            }

            else if (_waitingExit && !_suspect.IsInVehicle())
            {
                _waitingExit = false;
                _suspect.Task.HandsUp(-1);
                _suspect.AlwaysKeepTask = true;
            }
        }

        protected override void OnEnd()
        {
            CurrentSuspect = null;
            if (_blip != null && _blip.Exists()) _blip.Delete();

            if (_suspectVehicle != null && _suspectVehicle.Exists())
            {
                _suspectVehicle.MarkAsNoLongerNeeded();
            }

            if (_suspect != null && _suspect.Exists())
            {
                _suspect.MarkAsNoLongerNeeded();
            }
        }
    }
} 