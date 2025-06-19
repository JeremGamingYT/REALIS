using GTA;
using GTA.Math;
using GTA.Native;

namespace REALIS.Job
{
    /// <summary>
    /// Fournit l'équipement, l'uniforme et le véhicule de service.
    /// Séparé pour pouvoir être réutilisé par d'autres modules.
    /// </summary>
    public static class PoliceLoadout
    {
        private static readonly Vector3 MissionRowSpawn = new Vector3(407.57f, -979.43f, 29.02f);
        private const float MissionRowHeading = 231.43f;

        public static Vehicle SpawnServiceVehicle(Vector3 nearPosition)
        {
            // Modèle standard Police3 (Buffalo)
            var model = new Model(VehicleHash.Police3);
            if (!model.IsLoaded) model.Request(500);

            // Pour l'instant, spawn fixe Mission Row
            Vector3 spawnPos = MissionRowSpawn;

            var veh = World.CreateVehicle(model, spawnPos);
            if (veh != null && veh.Exists())
            {
                veh.Heading = MissionRowHeading;

                // Démarrer le moteur
                veh.IsEngineRunning = true;
            }
            model.MarkAsNoLongerNeeded();
            return veh;
        }

        public static void ApplyUniform(Ped ped)
        {
            if (!ped.Exists()) return;
            bool isMale = ped.Gender == Gender.Male;

            // Composant 11 = Haut, 8 = T-Shirt, 4 = Pantalon, 6 = Chaussures, 3 = Torse
            if (isMale)
            {
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, 11, 55, 0, 0); // chemise bleue police
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, 8, 58, 0, 0);  // radio
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, 4, 35, 0, 0); // pantalon noir
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, 6, 25, 0, 0); // chaussures
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, 3, 14, 0, 0); // mains
            }
            else
            {
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, 11, 48, 0, 0);
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, 8, 35, 0, 0);
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, 4, 34, 0, 0);
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, 6, 25, 0, 0);
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, 3, 15, 0, 0);
            }
        }
    }
} 