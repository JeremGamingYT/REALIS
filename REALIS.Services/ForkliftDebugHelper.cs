using System;
using GTA;
using GTA.Native;
using REALIS.Common;

namespace REALIS.Services
{
    /// <summary>
    /// Helper de debug pour tester la détection des véhicules élévateurs
    /// </summary>
    public class ForkliftDebugHelper : IModule
    {
        private DateTime _lastCheck = DateTime.MinValue;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(2);

        public void Initialize()
        {
            GTA.UI.Notification.Show("~b~Debug Chariot Élévateur activé~w~\nMontez dans un véhicule pour tester");
        }

        public void Update()
        {
            if (DateTime.Now - _lastCheck < _checkInterval) return;
            _lastCheck = DateTime.Now;

            var player = Game.Player.Character;
            if (!player.Exists() || !player.IsInVehicle()) return;

            var vehicle = player.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists()) return;

            // Afficher les informations du véhicule
            string vehicleInfo = $"~b~=== INFOS VÉHICULE ===~w~\n" +
                               $"Nom: {vehicle.DisplayName}\n" +
                               $"Modèle: {vehicle.Model}\n" +
                               $"Hash: {vehicle.Model.Hash}\n" +
                               $"Classe: {vehicle.ClassType}\n" +
                               $"Position: {vehicle.Position}";

            // Tester si c'est détecté comme chariot élévateur
            bool isForklift = TestForkliftDetection(vehicle);
            vehicleInfo += $"\n~g~Détecté comme élévateur: {(isForklift ? "OUI" : "NON")}~w~";

            // Tester les contrôles hydrauliques
            bool hasHydraulics = TestHydraulicControls(vehicle);
            vehicleInfo += $"\n~y~Hydrauliques disponibles: {(hasHydraulics ? "OUI" : "NON")}~w~";

            GTA.UI.Screen.ShowHelpTextThisFrame(vehicleInfo);
        }

        public void Dispose()
        {
            // Nettoyage
        }

        private bool TestForkliftDetection(Vehicle vehicle)
        {
            // Reproduire la logique de détection du ForkliftControlModule
            VehicleHash[] forkliftModels = new VehicleHash[]
            {
                VehicleHash.Forklift,
                VehicleHash.Docktug,
                VehicleHash.Caddy,
                VehicleHash.Caddy2,
                VehicleHash.Caddy3
            };

            string[] additionalModels = new string[]
            {
                "scissor",
                "handler",
                "airtug",
                "ripley",
                "tractor2",
                "tractor3"
            };

            // Vérifier les modèles standards
            foreach (var model in forkliftModels)
            {
                if (vehicle.Model.Hash == (int)model)
                    return true;
            }

            // Vérifier les modèles additionnels par nom
            string modelName = vehicle.DisplayName.ToLower();
            foreach (var model in additionalModels)
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

        private bool TestHydraulicControls(Vehicle vehicle)
        {
            try
            {
                // Tester si le véhicule répond aux contrôles hydrauliques
                Function.Call((Hash)0xE5810AC70602F2F5, vehicle.Handle, 0, 0.5f);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
} 