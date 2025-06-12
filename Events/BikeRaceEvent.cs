using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using REALIS.Core;

namespace REALIS.Events
{
    /// <summary>
    /// Course de vélo simple dans les rues de Los Santos.
    /// Appuyez sur F10 pour lancer ou annuler la course.
    /// </summary>
    public class BikeRaceEvent : Script
    {
        private readonly Keys _activationKey = Keys.F10;
        private readonly Vector3 _startPoint = new Vector3(-273.50f, -897.0f, 31.0f);  // Proche d'Integrity Way
        private readonly Vector3 _finishPoint = new Vector3(-1182.0f, -1504.0f, 4.0f); // Del Perro Beach
        private readonly int _racerCount = 6;

        private readonly List<Ped> _racers = new();
        private readonly List<Vehicle> _bikes = new();
        private readonly List<Prop> _barriers = new();
        private Blip? _raceBlip;
        private bool _raceActive;
        private bool _raceFinished;

        private readonly Random _rand = new();

        public BikeRaceEvent()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += (_, _) => Cleanup();

            Logger.Info("BikeRaceEvent initialisé : F10 pour commencer la course de vélo.");
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != _activationKey) return;

            if (!_raceActive)
            {
                StartRace();
            }
            else
            {
                CancelRace();
            }
        }

        private void StartRace()
        {
            try
            {
                Cleanup();
                _raceActive = true;
                _raceFinished = false;
                CreateBlip();
                SpawnRacers();
                SpawnBarriers();

                Notification.PostTicker("~g~🚴‍♂️ Course de vélo lancée !", false, true);
                Logger.Info("Course de vélo lancée");
            }
            catch (Exception ex)
            {
                Logger.Error($"BikeRaceEvent StartRace error: {ex.Message}");
                Notification.PostTicker("~r~Erreur lors du démarrage de la course", false, true);
            }
        }

        private void SpawnRacers()
        {
            Model bikeModel = new Model("bmx");
            Model pedModel = new Model(PedHash.Beach01AMY);

            bikeModel.Request(10000);
            pedModel.Request(10000);

            for (int i = 0; i < _racerCount; i++)
            {
                float lateral = (i - _racerCount / 2f) * 2.0f; // étale les coureurs sur la ligne de départ
                Vector3 spawnPos = _startPoint + new Vector3(lateral, 0f, 0f);

                Vehicle bike = World.CreateVehicle(bikeModel, spawnPos);
                Ped rider = World.CreatePed(pedModel, spawnPos);

                if (bike == null || rider == null) continue;

                bike.IsPersistent = true;
                rider.IsPersistent = true;
                rider.BlockPermanentEvents = true;

                rider.SetIntoVehicle(bike, VehicleSeat.Driver);

                // Couleur aléatoire pour différencier les vélos
                bike.Mods.PrimaryColor = (VehicleColor)_rand.Next(0, 161);

                // Démarre la conduite jusqu'à la ligne d'arrivée
                rider.Task.DriveTo(bike, _finishPoint, 15f,
                    VehicleDrivingFlags.SwerveAroundAllVehicles,
                    8f);

                _racers.Add(rider);
                _bikes.Add(bike);
            }

            bikeModel.MarkAsNoLongerNeeded();
            pedModel.MarkAsNoLongerNeeded();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (!_raceActive) return;

            // Surveille si un coureur atteint la ligne d'arrivée
            foreach (var racer in _racers)
            {
                if (!racer.Exists() || racer.IsDead) continue;

                if (racer.Position.DistanceToSquared(_finishPoint) < 25f) // ~5 m
                {
                    AnnounceWinner(racer);
                    break;
                }
            }
        }

        private void AnnounceWinner(Ped winner)
        {
            if (_raceFinished) return;
            _raceFinished = true;

            string winnerName = $"Coureur #{_racers.IndexOf(winner) + 1}";
            Notification.PostTicker($"~y~🏁 {winnerName} a franchi la ligne d'arrivée !", false, true);
            Logger.Info($"{winnerName} a gagné la course de vélo.");

            EndRace();
        }

        private void EndRace()
        {
            _raceActive = false;

            // Laisser les PNJ vaquer à leurs occupations après la course
            foreach (var racer in _racers)
            {
                if (racer.Exists() && !racer.IsDead)
                {
                    racer.Task.WanderAround(racer.Position, 20f);
                }
            }

            // Nettoyer le blip après quelques secondes
            Script.Wait(3000);
            Cleanup();
        }

        private void CancelRace()
        {
            Notification.PostTicker("~r~Course de vélo annulée", false, true);
            Logger.Info("Course de vélo annulée par l'utilisateur");
            Cleanup();
            _raceActive = false;
        }

        private void CreateBlip()
        {
            _raceBlip = World.CreateBlip(_startPoint);
            if (_raceBlip == null) return;

            _raceBlip.Sprite = BlipSprite.RaceBike; // 376 selon la doc SHVDN
            _raceBlip.Color = BlipColor.Blue;
            _raceBlip.Scale = 1.0f;
            _raceBlip.IsShortRange = false;
            _raceBlip.Name = "Course de vélo";
        }

        private void Cleanup()
        {
            foreach (var ped in _racers)
            {
                if (ped?.Exists() == true)
                {
                    ped.MarkAsNoLongerNeeded();
                    ped.Delete();
                }
            }
            _racers.Clear();

            foreach (var bike in _bikes)
            {
                if (bike?.Exists() == true)
                {
                    bike.MarkAsNoLongerNeeded();
                    bike.Delete();
                }
            }
            _bikes.Clear();

            // Supprimer les barrières
            foreach (var prop in _barriers)
            {
                if (prop?.Exists() == true)
                {
                    prop.MarkAsNoLongerNeeded();
                    prop.Delete();
                }
            }
            _barriers.Clear();

            if (_raceBlip?.Exists() == true)
            {
                _raceBlip.Delete();
                _raceBlip = null;
            }
        }

        private void SpawnBarriers()
        {
            try
            {
                const float interval = 40f;      // Distance entre deux barrières (m)
                const float sideOffset = 2.5f;   // Distance latérale (gauche/droite)

                // Modèle de petite barrière
                const string barrierModelName = "prop_mp_barrier_02a"; // Petit plot plastique rouge/blanc

                Model barrierModel = new Model(barrierModelName);
                if (!barrierModel.IsLoaded)
                {
                    barrierModel.Request(5000);
                    int waitCycles = 0;
                    while (!barrierModel.IsLoaded && waitCycles < 50)
                    {
                        Script.Wait(100);
                        waitCycles++;
                    }
                }
                if (!barrierModel.IsLoaded)
                {
                    Logger.Error($"Barrier model '{barrierModelName}' failed to load");
                    return;
                }

                float totalDist = _startPoint.DistanceTo(_finishPoint);
                int count = (int)(totalDist / interval);

                Vector3 direction = (_finishPoint - _startPoint).Normalized;
                Vector3 perpendicular = new Vector3(-direction.Y, direction.X, 0f);

                for (int i = 0; i <= count; i++)
                {
                    float t = i * interval;
                    Vector3 centerPos = _startPoint + direction * t;

                    // Obtenir la hauteur du sol
                    float groundZ;
                    World.GetGroundHeight(centerPos, out groundZ, GetGroundHeightMode.Normal);
                    centerPos.Z = groundZ;

                    // Côté gauche et droit
                    Vector3 leftPos = centerPos + perpendicular * sideOffset;
                    Vector3 rightPos = centerPos - perpendicular * sideOffset;

                    // Ajuster la hauteur du sol pour chaque côté
                    if (World.GetGroundHeight(leftPos, out float leftZ, GetGroundHeightMode.Normal))
                        leftPos.Z = leftZ;
                    if (World.GetGroundHeight(rightPos, out float rightZ, GetGroundHeightMode.Normal))
                        rightPos.Z = rightZ;

                    SpawnBarrier(barrierModel, leftPos, direction.ToHeading() + 90f);
                    SpawnBarrier(barrierModel, rightPos, direction.ToHeading() - 90f);
                }

                barrierModel.MarkAsNoLongerNeeded();
                Logger.Info($"Spawned {_barriers.Count} race barriers");
            }
            catch (Exception ex)
            {
                Logger.Error($"SpawnBarriers error: {ex.Message}");
            }
        }

        private void SpawnBarrier(Model model, Vector3 position, float heading)
        {
            Prop barrier = World.CreatePropNoOffset(model, position, Vector3.Zero, true);
            if (barrier == null || !barrier.Exists()) return;

            barrier.Heading = heading;
            barrier.IsPositionFrozen = true;
            barrier.IsPersistent = true;
            _barriers.Add(barrier);
        }
    }
} 