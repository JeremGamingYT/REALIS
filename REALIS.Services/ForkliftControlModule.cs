using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Common;
using System.Windows.Forms;

namespace REALIS.Services
{
    /// <summary>
    /// Module pour contrôler les chariots élévateurs (forklifts).
    /// Permet de lever/baisser les fourches, incliner le mât, et autres fonctions hydrauliques.
    /// </summary>
    public class ForkliftControlModule : IModule
    {
        // Hashes des modèles de véhicules élévateurs dans GTA V
        private readonly VehicleHash[] _forkliftModels = new VehicleHash[]
        {
            VehicleHash.Forklift,
            VehicleHash.Docktug,
            VehicleHash.Caddy,
            VehicleHash.Caddy2,
            VehicleHash.Caddy3
        };

        // Noms des modèles additionnels (pour les véhicules comme Scissor)
        private readonly string[] _additionalModels = new string[]
        {
            "scissor",
            "handler",
            "airtug",
            "ripley",
            "tractor2",
            "tractor3"
        };

        // Contrôles personnalisés pour les chariots élévateurs
        private const Keys RaiseForkKey = Keys.Up;           // Lever les fourches (Flèche Haut)
        private const Keys LowerForkKey = Keys.Down;         // Baisser les fourches (Flèche Bas)
        private const Keys TiltForwardKey = Keys.Right;      // Incliner vers l'avant (Flèche Droite)
        private const Keys TiltBackwardKey = Keys.Left;      // Incliner vers l'arrière (Flèche Gauche)
        private const Keys ExtendForkKey = Keys.Q;           // Étendre les fourches
        private const Keys RetractForkKey = Keys.E;          // Rétracter les fourches
        private const Keys ToggleHelpKey = Keys.H;           // Afficher/masquer l'aide

        // État des touches pour éviter les répétitions
        private bool _raiseForkHeld = false;
        private bool _lowerForkHeld = false;
        private bool _tiltForwardHeld = false;
        private bool _tiltBackwardHeld = false;
        private bool _extendForkHeld = false;
        private bool _retractForkHeld = false;
        private bool _helpHeld = false;

        // État du chariot élévateur
        private float _forkHeight = 0.0f;     // Hauteur des fourches (0.0 = minimum, 1.0 = maximum)
        private float _forkTilt = 0.0f;       // Inclinaison des fourches (-1.0 = arrière, 1.0 = avant)
        private float _forkExtension = 0.0f;  // Extension des fourches (0.0 = rétractées, 1.0 = étendues)

        // Limites et vitesses
        private const float MaxForkHeight = 1.0f;
        private const float MinForkHeight = 0.0f;
        private const float MaxForkTilt = 1.0f;
        private const float MinForkTilt = -1.0f;
        private const float MaxForkExtension = 1.0f;
        private const float MinForkExtension = 0.0f;
        private const float ForkSpeed = 0.015f;      // Vitesse de mouvement des fourches
        private const float TiltSpeed = 0.02f;       // Vitesse d'inclinaison
        private const float ExtensionSpeed = 0.01f;  // Vitesse d'extension

        // Affichage de l'aide
        private bool _showHelp = false;
        private DateTime _lastHelpToggle = DateTime.MinValue;
        private readonly TimeSpan _helpToggleCooldown = TimeSpan.FromMilliseconds(500);

        public void Initialize()
        {
            // Initialisation du module
            GTA.UI.Notification.Show("~g~Module Chariot Élévateur activé~w~\nSupporte: Forklift, Scissor, Handler\nAppuyez sur ~b~H~w~ pour l'aide");
        }

        public void Update()
        {
            var player = Game.Player.Character;
            if (!player.Exists() || player.IsDead || !player.IsInVehicle()) return;

            var vehicle = player.CurrentVehicle;
            if (!IsForklift(vehicle)) return;

            HandleInput();
            UpdateForkliftControls(vehicle);
            
            if (_showHelp)
            {
                DisplayHelp();
            }
        }

        public void Dispose()
        {
            // Nettoyage si nécessaire
        }

        private bool IsForklift(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return false;
            
            // Vérifier les modèles standards
            foreach (var model in _forkliftModels)
            {
                if (vehicle.Model.Hash == (int)model)
                    return true;
            }
            
            // Vérifier les modèles additionnels par nom
            string modelName = vehicle.DisplayName.ToLower();
            foreach (var model in _additionalModels)
            {
                if (modelName.Contains(model))
                    return true;
            }
            
            // Vérifier par hash de modèle pour des véhicules spécifiques
            uint modelHash = (uint)vehicle.Model.Hash;
            switch (modelHash)
            {
                case 1677715697: // scissor
                case 444583674:  // handler
                case 1560980623: // airtug
                case 3448987385: // ripley
                    return true;
            }
            
            return false;
        }

        private void HandleInput()
        {
            var player = Game.Player.Character;
            var vehicle = player.CurrentVehicle;
            
            // Vérifier si Shift est pressé pour les mouvements rapides
            bool shiftPressed = Game.IsKeyPressed(Keys.LShiftKey) || Game.IsKeyPressed(Keys.RShiftKey);
            float currentForkSpeed = shiftPressed ? ForkSpeed * 2.0f : ForkSpeed;
            float currentTiltSpeed = shiftPressed ? TiltSpeed * 2.0f : TiltSpeed;
            float currentExtensionSpeed = shiftPressed ? ExtensionSpeed * 2.0f : ExtensionSpeed;

            // Utiliser directement les contrôles GTA natifs pour les hydrauliques
            bool raisePressed = Game.IsKeyPressed(RaiseForkKey);
            bool lowerPressed = Game.IsKeyPressed(LowerForkKey);
            bool tiltForwardPressed = Game.IsKeyPressed(TiltForwardKey);
            bool tiltBackwardPressed = Game.IsKeyPressed(TiltBackwardKey);
            bool extendPressed = Game.IsKeyPressed(ExtendForkKey);
            bool retractPressed = Game.IsKeyPressed(RetractForkKey);
            bool helpPressed = Game.IsKeyPressed(ToggleHelpKey);

            // Contrôles directs pour plateforme élévatrice
            if (raisePressed)
            {
                // Pour les plateformes élévatrices, utiliser les tâches de véhicule
                try
                {
                    Function.Call((Hash)0xE5810AC70602F2F5, vehicle.Handle, 0, 1.0f); // _SET_VEHICLE_HYDRAULIC_WHEEL_VALUE
                }
                catch
                {
                    // Fallback : manipuler la position du véhicule
                    Vector3 pos = vehicle.Position;
                    pos.Z += currentForkSpeed * 0.1f;
                    vehicle.Position = pos;
                }
                _forkHeight = Math.Min(_forkHeight + currentForkSpeed, MaxForkHeight);
                if (!_raiseForkHeld) PlayHydraulicSound();
            }
            _raiseForkHeld = raisePressed;

            if (lowerPressed)
            {
                try
                {
                    Function.Call((Hash)0xE5810AC70602F2F5, vehicle.Handle, 0, 0.0f); // _SET_VEHICLE_HYDRAULIC_WHEEL_VALUE
                }
                catch
                {
                    // Fallback : manipuler la position du véhicule
                    Vector3 pos = vehicle.Position;
                    pos.Z -= currentForkSpeed * 0.1f;
                    vehicle.Position = pos;
                }
                _forkHeight = Math.Max(_forkHeight - currentForkSpeed, MinForkHeight);
                if (!_lowerForkHeld) PlayHydraulicSound();
            }
            _lowerForkHeld = lowerPressed;

            if (tiltForwardPressed)
            {
                try
                {
                    Function.Call((Hash)0xE5810AC70602F2F5, vehicle.Handle, 1, _forkTilt);
                }
                catch
                {
                    // Fallback : incliner le véhicule
                    Vector3 rot = vehicle.Rotation;
                    rot.X += currentTiltSpeed * 2.0f;
                    vehicle.Rotation = rot;
                }
                _forkTilt = Math.Min(_forkTilt + currentTiltSpeed, MaxForkTilt);
                if (!_tiltForwardHeld) PlayHydraulicSound();
            }
            _tiltForwardHeld = tiltForwardPressed;

            if (tiltBackwardPressed)
            {
                try
                {
                    Function.Call((Hash)0xE5810AC70602F2F5, vehicle.Handle, 1, _forkTilt);
                }
                catch
                {
                    // Fallback : incliner le véhicule
                    Vector3 rot = vehicle.Rotation;
                    rot.X -= currentTiltSpeed * 2.0f;
                    vehicle.Rotation = rot;
                }
                _forkTilt = Math.Max(_forkTilt - currentTiltSpeed, MinForkTilt);
                if (!_tiltBackwardHeld) PlayHydraulicSound();
            }
            _tiltBackwardHeld = tiltBackwardPressed;

            // Extension/rétraction avec contrôles supplémentaires
            if (extendPressed)
            {
                _forkExtension = Math.Min(_forkExtension + currentExtensionSpeed, MaxForkExtension);
                if (!_extendForkHeld) PlayHydraulicSound();
            }
            _extendForkHeld = extendPressed;

            if (retractPressed)
            {
                _forkExtension = Math.Max(_forkExtension - currentExtensionSpeed, MinForkExtension);
                if (!_retractForkHeld) PlayHydraulicSound();
            }
            _retractForkHeld = retractPressed;

            // Basculer l'aide
            if (helpPressed && !_helpHeld && DateTime.Now - _lastHelpToggle > _helpToggleCooldown)
            {
                _showHelp = !_showHelp;
                _lastHelpToggle = DateTime.Now;
                GTA.UI.Notification.Show(_showHelp ? "~g~Aide affichée" : "~o~Aide masquée");
                
                // Debug : afficher les infos du véhicule
                if (_showHelp)
                {
                    GTA.UI.Notification.Show($"~b~Véhicule:~w~ {vehicle.DisplayName} (Hash: {vehicle.Model.Hash})");
                }
            }
            _helpHeld = helpPressed;
        }

        private void UpdateForkliftControls(Vehicle vehicle)
        {
            // Utilisation des natives GTA pour contrôler les systèmes hydrauliques
            // Utilisation des contrôles hydrauliques disponibles dans GTA V
            
            try
            {
                // Contrôle de la hauteur des fourches via les natives hydrauliques
                if (_forkHeight > 0.01f)
                {
                    Function.Call((Hash)0x28D034A93FE31BF5, vehicle.Handle, 0, _forkHeight); // SET_VEHICLE_HYDRAULIC_WHEEL_VALUE
                }
                
                // Contrôle de l'inclinaison du mât
                if (Math.Abs(_forkTilt) > 0.01f)
                {
                    Function.Call((Hash)0x28D034A93FE31BF5, vehicle.Handle, 1, _forkTilt); // SET_VEHICLE_HYDRAULIC_WHEEL_VALUE
                }
                
                // Contrôle de l'extension des fourches (si supporté)
                if (_forkExtension > 0.01f)
                {
                    Function.Call((Hash)0x28D034A93FE31BF5, vehicle.Handle, 2, _forkExtension); // SET_VEHICLE_HYDRAULIC_WHEEL_VALUE
                }

                // Utiliser les contrôles hydrauliques natifs disponibles
                if (_forkHeight > 0.01f || Math.Abs(_forkTilt) > 0.01f || _forkExtension > 0.01f)
                {
                    // Activer le système hydraulique du véhicule
                    Function.Call((Hash)0x8EA86DF356801C7E, vehicle.Handle, true); // SET_VEHICLE_HYDRAULIC_STATE
                }
            }
            catch
            {
                // Fallback : utiliser les contrôles de véhicule standards
                // Si les natives hydrauliques ne fonctionnent pas, utiliser les contrôles de base
                
                // Utiliser les contrôles de rotation du véhicule pour simuler l'inclinaison
                if (Math.Abs(_forkTilt) > 0.01f)
                {
                    Vector3 rotation = vehicle.Rotation;
                    rotation.X = _forkTilt * 10.0f; // Ajuster l'inclinaison
                    vehicle.Rotation = rotation;
                }
                
                // Utiliser la position Z pour simuler la hauteur des fourches
                if (_forkHeight > 0.01f)
                {
                    Vector3 position = vehicle.Position;
                    position.Z += _forkHeight * 0.5f; // Élever légèrement le véhicule
                    vehicle.Position = position;
                }
            }

            // Mise à jour de l'affichage des valeurs pour le débogage
            if (_showHelp)
            {
                UpdateHUD();
            }
        }

        private void PlayHydraulicSound()
        {
            // Jouer un son hydraulique réaliste
            try
            {
                Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "HYDRAULIC_MOVE", Game.Player.Character.Handle, 
                    "DLC_CHRISTMAS2017_VEHICLE_SOUNDS", false, 0);
            }
            catch
            {
                // Son de fallback si le son hydraulique n'est pas disponible
                Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "TIMER_STOP", Game.Player.Character.Handle, 
                    "HUD_MINI_GAME_SOUNDSET", false, 0);
            }
        }

        private void DisplayHelp()
        {
            string helpText = "~b~=== CONTRÔLES CHARIOT ÉLÉVATEUR ===~w~\n" +
                             "~y~Flèche Haut/Bas~w~ : Lever/Baisser les fourches\n" +
                             "~y~Flèche Gauche/Droite~w~ : Incliner le mât\n" +
                             "~y~Q/E~w~ : Étendre/Rétracter les fourches\n" +
                             "~y~Shift + Touches~w~ : Mouvement rapide\n" +
                             "~y~H~w~ : Afficher/masquer cette aide\n\n" +
                             $"~g~Hauteur~w~ : {(_forkHeight * 100):F0}%\n" +
                             $"~g~Inclinaison~w~ : {(_forkTilt * 100):F0}%\n" +
                             $"~g~Extension~w~ : {(_forkExtension * 100):F0}%";

            GTA.UI.Screen.ShowHelpTextThisFrame(helpText);
        }

        private void UpdateHUD()
        {
            // Afficher les valeurs actuelles dans le coin de l'écran
            string statusText = $"Chariot Élévateur\n" +
                               $"Hauteur: {(_forkHeight * 100):F0}%\n" +
                               $"Inclinaison: {(_forkTilt * 100):F0}%\n" +
                               $"Extension: {(_forkExtension * 100):F0}%";

            // Utiliser les natives pour afficher le texte à l'écran
            try
            {
                Function.Call(Hash.SET_TEXT_FONT, 0);
                Function.Call(Hash.SET_TEXT_PROPORTIONAL, 1);
                Function.Call(Hash.SET_TEXT_SCALE, 0.35f, 0.35f);
                Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
                Function.Call(Hash.SET_TEXT_DROPSHADOW, 1, 0, 0, 0, 255);
                Function.Call(Hash.SET_TEXT_EDGE, 1, 0, 0, 0, 255);
                Function.Call(Hash.SET_TEXT_CENTRE, 0);
                Function.Call((Hash)0x25FBB336DF1804CB, "STRING"); // _SET_TEXT_ENTRY
                Function.Call((Hash)0x6C188BE134E074AA, statusText); // _ADD_TEXT_COMPONENT_STRING
                Function.Call((Hash)0xCD015E5BB0D96A57, 0.02f, 0.8f); // _DRAW_TEXT
            }
            catch
            {
                // Fallback : utiliser les notifications si l'affichage HUD ne fonctionne pas
                GTA.UI.Notification.Show($"~b~Chariot:~w~ H:{(_forkHeight * 100):F0}% I:{(_forkTilt * 100):F0}% E:{(_forkExtension * 100):F0}%");
            }
        }
    }
} 