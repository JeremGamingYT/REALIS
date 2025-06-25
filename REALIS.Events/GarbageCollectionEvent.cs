using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.Math;
using REALIS.Common;

namespace REALIS.Events
{
    /// <summary>
    /// Événement simple faisant circuler des camions poubelle qui ramassent les poubelles placées dans les rues.
    /// Les camions reçoivent un blip afin d'être visibles sur la mini-carte et la carte.
    /// </summary>
    public class GarbageCollectionEvent : IModule
    {
        // Paramètres
        private const int MaxTrucks = 2;                // Nombre de camions simultanés
        private const float BinPickupDistance = 7f;     // Distance de ramassage
        private readonly Model _truckModel = new Model(VehicleHash.Trash2);
        private readonly Model _driverModel = new Model(PedHash.GarbageSMY);
        private readonly string[] _binModelNames =
        {
            "prop_rub_binbag_01",
            "prop_rub_binbag_01b",
            "prop_rub_binbag_03",
            "prop_rub_binbag_03b",
            "prop_rub_binbag_04",
            "prop_rub_binbag_05",
            "prop_rub_binbag_06",
            "prop_rub_binbag_08",
            "prop_rub_binbag_sd_01",
            "prop_rub_binbag_sd_02"
        };

        private readonly List<Model> _binModels = new List<Model>();

        // Éléments dynamiques
        private readonly List<Vehicle> _trucks = new List<Vehicle>();
        private readonly List<Ped> _drivers = new List<Ped>();
        private readonly List<Prop> _bins = new List<Prop>(); // référentiel temporaire (mis à jour chaque tick)

        private readonly Random _rng = new Random();

        // Pour éviter d'initier plusieurs fois une collecte avec le même conducteur
        private readonly HashSet<Ped> _busyDrivers = new HashSet<Ped>();

        public void Initialize()
        {
            // Charger les modèles de façon synchrone (peu coûteux car une fois au démarrage uniquement).
            _truckModel.Request(5000);
            _driverModel.Request(5000);
            foreach (var name in _binModelNames)
            {
                var m = new Model(name);
                m.Request(5000);
                if (m.IsInCdImage && m.IsValid)
                    _binModels.Add(m);
            }

            SpawnInitialEntities();
        }

        public void Update()
        {
            // Si le joueur se trouve dans une nouvelle zone (grande distance), on peut respawn les poubelles pour garder l'activité près de lui.
            RefreshNearbyBins();
            MaintainTrucks();
            CheckBinPickup();
        }

        public void Dispose()
        {
            foreach (var bin in _bins)
            {
                if (bin?.Exists() == true)
                {
                    bin.Delete();
                }
            }
            _bins.Clear();

            foreach (var ped in _drivers)
            {
                if (ped?.Exists() == true)
                {
                    ped.MarkAsNoLongerNeeded();
                    ped.Delete();
                }
            }
            _drivers.Clear();

            foreach (var veh in _trucks)
            {
                if (veh?.Exists() == true)
                {
                    veh.MarkAsNoLongerNeeded();
                    veh.Delete();
                }
            }
            _trucks.Clear();

            foreach (var m in _binModels) m.MarkAsNoLongerNeeded();
        }

        private void SpawnInitialEntities()
        {
            RefreshNearbyBins();
            SpawnTrucks();
        }

        #region Bins management

        /// <summary>
        /// Récupère les sacs poubelle déjà présents dans le monde autour du joueur (rayon 250 m).
        /// </summary>
        private void RefreshNearbyBins()
        {
            _bins.Clear();

            var playerPos = Game.Player.Character.Position;
            foreach (var prop in World.GetAllProps())
            {
                if (prop == null || !prop.Exists()) continue;

                // Filtre modèle
                if (!_binModels.Exists(m => m.Hash == prop.Model.Hash)) continue;

                if (playerPos.DistanceToSquared(prop.Position) <= 250f * 250f)
                    _bins.Add(prop);
            }
        }

        private Vector3 GetRandomSidewalkPositionNearPlayer(float minDist, float maxDist)
        {
            var playerPos = Game.Player.Character.Position;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var offset = new Vector3(
                    _rng.NextFloat(-maxDist, maxDist),
                    _rng.NextFloat(-maxDist, maxDist),
                    0);

                if (offset.Length() < minDist) continue;
                var candidate = playerPos + offset;
                candidate = World.GetNextPositionOnSidewalk(candidate);
                if (candidate != Vector3.Zero)
                    return candidate;
            }
            return Vector3.Zero;
        }

        #endregion

        #region Trucks management

        private void SpawnTrucks()
        {
            while (_trucks.Count < MaxTrucks)
            {
                var spawnPos = GetRandomSidewalkPositionNearPlayer(150f, 300f);
                if (spawnPos == Vector3.Zero) break;

                var truck = World.CreateVehicle(_truckModel, spawnPos, _rng.NextFloat(0, 360));
                if (truck == null) break;
                truck.IsPersistent = true;
                _trucks.Add(truck);

                // Créer un conducteur
                var driver = World.CreatePed(_driverModel, truck.Position);
                if (driver != null)
                {
                    driver.SetIntoVehicle(truck, VehicleSeat.Driver);
                    driver.Task.CruiseWithVehicle(truck, 15f, VehicleDrivingFlags.DrivingModeAvoidVehicles);
                    _drivers.Add(driver);
                }

                // Ajouter blip pour visibilité
                var blip = truck.AddBlip();
                blip.Sprite = BlipSprite.Truck;
                blip.Color = BlipColor.Green;
                blip.Name = "Camion poubelle";
            }
        }

        private void MaintainTrucks()
        {
            for (int i = _trucks.Count - 1; i >= 0; i--)
            {
                var truck = _trucks[i];
                if (truck == null || !truck.Exists())
                {
                    _trucks.RemoveAt(i);
                    continue;
                }

                // Si le camion est trop loin (> 800 m) et que personne ne le voit, on le supprime pour optimiser
                if (Game.Player.Character.Position.DistanceToSquared(truck.Position) > 800f * 800f)
                {
                    truck.Delete();
                    _trucks.RemoveAt(i);
                }
            }

            // Respawn si besoin
            if (_trucks.Count < MaxTrucks)
                SpawnTrucks();
        }

        #endregion

        #region Interaction truck/bin

        private void CheckBinPickup()
        {
            if (_bins.Count == 0 || _trucks.Count == 0) return;

            foreach (var truck in _trucks)
            {
                if (truck == null || !truck.Exists()) continue;

                var driver = truck.Driver;
                if (driver == null || !driver.Exists()) continue;

                for (int i = _bins.Count - 1; i >= 0; i--)
                {
                    var bin = _bins[i];
                    if (bin == null || !bin.Exists())
                    {
                        _bins.RemoveAt(i);
                        continue;
                    }

                    // Déclenche la séquence de ramassage si le camion est suffisamment proche
                    if (truck.Position.DistanceToSquared(bin.Position) <= 25f * 25f && !_busyDrivers.Contains(driver))
                    {
                        StartPickupSequence(driver, truck, bin);
                    }
                }
            }
        }

        /// <summary>
        /// Lance une petite coroutine via GameScheduler : le conducteur se gare, sort, va jusqu'à la poubelle puis revient au volant.
        /// </summary>
        private void StartPickupSequence(Ped driver, Vehicle truck, Prop bin)
        {
            if (driver == null || !driver.Exists() || bin == null || !bin.Exists()) return;

            _busyDrivers.Add(driver);

            // 1) Arrêt en douceur du véhicule
            driver.Task.CruiseWithVehicle(truck, 0f, VehicleDrivingFlags.None);

            // 2) Sortie après 1 s
            GameScheduler.Schedule(() =>
            {
                if (!driver.Exists()) { _busyDrivers.Remove(driver); return; }

                driver.Task.LeaveVehicle(truck, LeaveVehicleFlags.LeaveDoorOpen);

                // 3) Marche jusqu'au sac
                GameScheduler.Schedule(() =>
                {
                    if (!driver.Exists()) { _busyDrivers.Remove(driver); return; }

                    driver.Task.FollowNavMeshTo(bin.Position);

                    // Vérifier périodiquement l'arrivée
                    void CheckArrival()
                    {
                        if (!driver.Exists()) { _busyDrivers.Remove(driver); return; }

                        if (driver.Position.DistanceToSquared(bin.Position) <= 4f) // ~2 m
                        {
                            // 4) Animation de ramassage
                            driver.Task.StartScenarioInPlace("WORLD_HUMAN_JANITOR", 4000, true);

                            // Supprimer après 3 s d'animation
                            GameScheduler.Schedule(() =>
                            {
                                if (bin.Exists())
                                {
                                    bin.Delete();
                                    _bins.Remove(bin);
                                }

                                // 5) Retour dans le camion
                                if (driver.Exists())
                                    driver.Task.EnterVehicle(truck, VehicleSeat.Driver);

                                // Reprise de la route
                                GameScheduler.Schedule(() =>
                                {
                                    if (driver.Exists())
                                        driver.Task.CruiseWithVehicle(truck, 15f, VehicleDrivingFlags.DrivingModeAvoidVehicles);
                                    _busyDrivers.Remove(driver);
                                }, 5000);

                            }, 3000);
                        }
                        else
                        {
                            // Re-check dans 500 ms
                            GameScheduler.Schedule(CheckArrival, 500);
                        }
                    }

                    // Première vérification après 500 ms
                    GameScheduler.Schedule(CheckArrival, 500);

                }, 1000);

            }, 1000);
        }

        #endregion
    }

    internal static class RandomExtensions
    {
        public static float NextFloat(this Random rng, float min, float max)
        {
            return (float)(min + rng.NextDouble() * (max - min));
        }
    }
} 