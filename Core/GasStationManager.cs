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
            public bool LastOpenState { get; set; }
            public DateTime LastNotificationTime { get; set; }

            public GasStation(Vector3 pos, TimeSpan open, TimeSpan close, bool accessible)
            {
                Position = pos;
                OpenTime = open;
                CloseTime = close;
                Accessible = accessible;
                LastOpenState = false;
                LastNotificationTime = DateTime.MinValue;
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

                    if (dist < 25f)
                    {
                        if (open != station.LastOpenState &&
                            (DateTime.Now - station.LastNotificationTime).TotalSeconds > 5)
                        {
                            Notification.PostTicker(open ? "Station-service ouverte" : "Station-service fermée", true);
                            station.LastOpenState = open;
                            station.LastNotificationTime = DateTime.Now;
                        }

                        if (open)
                            SpawnCustomer(station);
                        else
                            RemoveCustomer(station);
                    }
                    else
                    {
                        RemoveCustomer(station);
                    }

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

                Model model = new Model(PedHash.ShopMaskSMY);
                if (!model.IsLoaded) model.Request(500);
                if (!model.IsLoaded) return;

                var ped = World.CreatePed(model, station.Position + new Vector3(1f, 1f, 0f));
                if (ped == null || !ped.Exists()) return;

                ped.Task.StartScenarioInPlace("WORLD_HUMAN_STAND_IMPATIENT", 0, true);
                station.Customer = ped;
                _spawnedPeds.Add(ped);
                model.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                Logger.Error($"Customer spawn error: {ex.Message}");
            }
        }

        private void RemoveCustomer(GasStation station)
        {
            if (station.Customer == null) return;

            try
            {
                if (station.Customer.Exists())
                    station.Customer.Delete();
            }
            catch (Exception ex)
            {
                Logger.Error($"Customer remove error: {ex.Message}");
            }
            finally
            {
                _spawnedPeds.Remove(station.Customer);
                station.Customer = null;
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
            }
            catch (Exception ex)
            {
                Logger.Error($"GasStation cleanup error: {ex.Message}");
            }
        }
    }
}
