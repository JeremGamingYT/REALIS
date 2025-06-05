using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.UI;

namespace REALIS.Core
{
    /// <summary>
    /// Gère l'affichage des stations-service sur la carte avec horaires d'ouverture.
    /// Les blips sont verts si la station est ouverte, rouges sinon.
    /// </summary>
    public class GasStationManager : Script
    {
        private class GasStation
        {
            public Vector3 Position { get; }
            public TimeSpan OpenTime { get; }
            public TimeSpan CloseTime { get; }
            public bool Accessible { get; }
            public Blip? Blip { get; set; }
            public Ped? Customer { get; set; }
            public Vehicle? CustomerVehicle { get; set; }
            public bool LastOpenState { get; set; }
            public DateTime LastNotificationTime { get; set; }
            public DateTime LastAccessDeniedTime { get; set; }
            public bool PlayerWasInside { get; set; }

            public GasStation(Vector3 pos, TimeSpan open, TimeSpan close, bool accessible)
            {
                Position = pos;
                OpenTime = open;
                CloseTime = close;
                Accessible = accessible;
                LastOpenState = false;
                LastNotificationTime = DateTime.MinValue;
                LastAccessDeniedTime = DateTime.MinValue;
            }

            public bool IsOpen()
            {
                var now = DateTime.Now.TimeOfDay;
                if (OpenTime <= CloseTime)
                    return now >= OpenTime && now <= CloseTime;

                // handle stations that close after midnight
                return now >= OpenTime || now <= CloseTime;
            }
        }

        private readonly List<GasStation> _stations = new();
        private readonly List<Ped> _spawnedPeds = new();
        private readonly List<Vehicle> _spawnedVehicles = new();
        private int _tickCounter = 0;
        private const int UPDATE_INTERVAL = 100;

        public GasStationManager()
        {
            Tick += OnTick;
            Aborted += OnAborted;


            InitializeStations();
            CreateBlips();
        }

        private void InitializeStations()
        {
            // Stations-service accessibles où le joueur peut entrer
            _stations.Add(new GasStation(new Vector3(-70.2148f, -1761.792f, 29.534f), TimeSpan.Zero, new TimeSpan(23, 59, 59), true)); // Grove Street 24/7
            _stations.Add(new GasStation(new Vector3(265.648f, -1261.309f, 29.292f), TimeSpan.Zero, new TimeSpan(23, 59, 59), true)); // Strawberry 24/7
            _stations.Add(new GasStation(new Vector3(819.653f, -1028.846f, 26.403f), new TimeSpan(6,0,0), new TimeSpan(23,0,0), true));
            _stations.Add(new GasStation(new Vector3(1208.951f, -1402.567f,35.224f), new TimeSpan(6,0,0), new TimeSpan(23,0,0), true));
            _stations.Add(new GasStation(new Vector3(-1437.622f, -276.747f, 46.207f), new TimeSpan(7,0,0), new TimeSpan(21,0,0), true));
            _stations.Add(new GasStation(new Vector3(1181.381f, -330.847f, 69.316f), new TimeSpan(7,0,0), new TimeSpan(21,0,0), true));
            _stations.Add(new GasStation(new Vector3(620.843f, 269.100f, 103.089f), new TimeSpan(6,0,0), new TimeSpan(22,0,0), false)); // pas d'accès magasin
            _stations.Add(new GasStation(new Vector3(2581.321f, 362.039f, 108.468f), TimeSpan.Zero, new TimeSpan(23, 59, 59), false)); // station uniquement
            _stations.Add(new GasStation(new Vector3(176.631f, -1562.025f, 29.263f), new TimeSpan(6,0,0), new TimeSpan(22,0,0), true));
            _stations.Add(new GasStation(new Vector3(-319.292f, -1471.715f, 30.549f), TimeSpan.Zero, new TimeSpan(23, 59, 59), false)); // pas d'accès magasin
            _stations.Add(new GasStation(new Vector3(620.843f, 269.100f, 103.089f), new TimeSpan(6,0,0), new TimeSpan(22,0,0), true));
            _stations.Add(new GasStation(new Vector3(2581.321f, 362.039f, 108.468f), TimeSpan.Zero, new TimeSpan(23, 59, 59), true));
            _stations.Add(new GasStation(new Vector3(176.631f, -1562.025f, 29.263f), new TimeSpan(6,0,0), new TimeSpan(22,0,0), true));
            _stations.Add(new GasStation(new Vector3(-319.292f, -1471.715f, 30.549f), TimeSpan.Zero, new TimeSpan(23, 59, 59), true));
        }

        private void CreateBlips()
        {
            foreach (var station in _stations)
            {
                if (!station.Accessible) continue;

                var blip = World.CreateBlip(station.Position);
                blip.Sprite = BlipSprite.JerryCan;
                blip.Scale = 0.9f; // plus visible sur la carte
                station.Blip = blip;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _tickCounter++;
                if (_tickCounter % UPDATE_INTERVAL != 0) return;

                

                foreach (var station in _stations)
                {
                    if (!station.Accessible || station.Blip == null) continue;

                    bool open = station.IsOpen();
                    station.Blip.Color = open ? BlipColor.Green : BlipColor.Red;
                    station.Blip.Name = open ? "Station-service (ouverte)" : "Station-service (fermée)";

                    float dist = Game.Player.Character.Position.DistanceTo(station.Position);

                    bool inside = dist < 25f;

                    if (inside && (!station.PlayerWasInside || open != station.LastOpenState))
                    {
                        if ((DateTime.Now - station.LastNotificationTime).TotalSeconds > 1)
                        {
                            Notification.PostTicker(open ? "~g~Station-service ouverte" : "~r~Station-service fermée", true);
                            station.LastNotificationTime = DateTime.Now;
                        }
                    }

                    if (inside)
                    {
                        if (!open)
                        {
                            HandleClosedStation(station, dist);
                        }
                    }
                    else
                    {
                        RemoveCustomer(station);
                    }

                    station.PlayerWasInside = inside;
                    station.LastOpenState = open;

                }

            }
            catch (Exception ex)
            {
                Logger.Error($"GasStation tick error: {ex.Message}");
            }
        }

        private void SpawnCustomer(GasStation station)
        {
            try
            {
                if (station.Customer != null && station.Customer.Exists()) return;
                if (station.CustomerVehicle != null && station.CustomerVehicle.Exists()) return;

                Model pedModel = new Model(PedHash.ShopMaskSMY);
                Model vehModel = new Model(VehicleHash.Panto);

                if (!pedModel.IsLoaded) pedModel.Request(500);
                if (!vehModel.IsLoaded) vehModel.Request(500);
                if (!pedModel.IsLoaded || !vehModel.IsLoaded) return;

                Vector3 spawnPos = station.Position + new Vector3(15f, 15f, 0f);
                var vehicle = World.CreateVehicle(vehModel, spawnPos, 0f);
                if (vehicle == null || !vehicle.Exists()) return;

                var ped = vehicle.CreatePedOnSeat(VehicleSeat.Driver, pedModel);
                if (ped == null || !ped.Exists())
                {
                    vehicle.Delete();
                    return;
                }

                TaskSequence seq = new TaskSequence();
                seq.AddTask.DriveTo(vehicle, station.Position, 5f, VehicleDrivingFlags.StopForVehicles | VehicleDrivingFlags.StopAtTrafficLights, 10f);
                seq.AddTask.LeaveVehicle(vehicle, LeaveVehicleFlags.None);
                seq.AddTask.GoStraightTo(station.Position);
                seq.AddTask.StartScenarioInPlace("WORLD_HUMAN_STAND_IMPATIENT", 0, true);
                seq.Close();
                ped.Task.PerformSequence(seq);
                seq.Dispose();

                station.Customer = ped;
                station.CustomerVehicle = vehicle;
                _spawnedPeds.Add(ped);
                _spawnedVehicles.Add(vehicle);

                pedModel.MarkAsNoLongerNeeded();
                vehModel.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                Logger.Error($"Customer spawn error: {ex.Message}");
            }
        }

        private void RemoveCustomer(GasStation station)
        {
            if (station.Customer == null && station.CustomerVehicle == null) return;

            try
            {
                if (station.Customer != null && station.Customer.Exists())
                    station.Customer.Delete();
                if (station.CustomerVehicle != null && station.CustomerVehicle.Exists())
                    station.CustomerVehicle.Delete();
            }
            catch (Exception ex)
            {
                Logger.Error($"Customer remove error: {ex.Message}");
            }
            finally
            {
                if (station.Customer != null)
                {
                    _spawnedPeds.Remove(station.Customer);
                    station.Customer = null;
                }
                if (station.CustomerVehicle != null)
                {
                    _spawnedVehicles.Remove(station.CustomerVehicle);
                    station.CustomerVehicle = null;
                }
            }
        }

        private void HandleClosedStation(GasStation station, float distance)
        {
            try
            {
                RemoveCustomer(station);

                if ((DateTime.Now - station.LastAccessDeniedTime).TotalSeconds < 5)
                    return;

                if (distance < 3f)
                {
                    if ((DateTime.Now - station.LastAccessDeniedTime).TotalSeconds > 1)
                    {
                        Notification.PostTicker("~r~Magasin fermé", true);
                        station.LastAccessDeniedTime = DateTime.Now;
                    }
                    ForcePlayerUTurn();
                }

                ClearStore(station);
            }
            catch (Exception ex)
            {
                Logger.Error($"Closed station handling error: {ex.Message}");
            }
        }

        private void ClearStore(GasStation station)
        {
            try
            {
                var peds = World.GetNearbyPeds(station.Position, 5f);
                foreach (var ped in peds)
                {
                    if (ped == null || !ped.Exists()) continue;
                    if (ped == Game.Player.Character) continue;
                    if (_spawnedPeds.Contains(ped)) continue;
                    ped.Delete();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Clear store error: {ex.Message}");
            }
        }

        private void ForcePlayerUTurn()
        {
            try
            {
                var player = Game.Player.Character;
                Vector3 targetPos = player.Position - player.ForwardVector * 7f;
                float heading = (player.Heading + 180f) % 360f;

                if (player.IsInVehicle())
                {
                    Vehicle veh = player.CurrentVehicle;
                    TaskSequence seq = new TaskSequence();
                    seq.AddTask.AchieveHeading(heading);
                    seq.AddTask.DriveTo(veh, targetPos, 5f, VehicleDrivingFlags.StopForVehicles, 10f);
                    seq.Close();
                    player.Task.PerformSequence(seq);
                    seq.Dispose();
                }
                else
                {
                    TaskSequence seq = new TaskSequence();
                    seq.AddTask.AchieveHeading(heading);
                    seq.AddTask.GoStraightTo(targetPos);
                    seq.Close();
                    player.Task.PerformSequence(seq);
                    seq.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"U-Turn error: {ex.Message}");
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                foreach (var station in _stations)
                {
                    if (!station.Accessible) continue;
                    station.Blip?.Delete();
                    RemoveCustomer(station);
                }

                _spawnedPeds.Clear();
                _spawnedVehicles.Clear();
            }
            catch (Exception ex)
            {
                Logger.Error($"GasStation cleanup error: {ex.Message}");
            }
        }
    }
}
