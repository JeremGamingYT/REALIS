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
            public Blip? Blip { get; set; }

            public GasStation(Vector3 pos, TimeSpan open, TimeSpan close)
            {
                Position = pos;
                OpenTime = open;
                CloseTime = close;
            }

            public bool IsOpen()
            {
                var now = DateTime.Now.TimeOfDay;
                return now >= OpenTime && now <= CloseTime;
            }
        }

        private readonly List<GasStation> _stations = new();
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
            // Coordonnées de quelques stations-service courantes
            _stations.Add(new GasStation(new Vector3(-72.5f, -1761.0f, 29.5f), new TimeSpan(6,0,0), new TimeSpan(22,0,0)));
            _stations.Add(new GasStation(new Vector3(263.9f, -1260.3f, 29.0f), new TimeSpan(6,0,0), new TimeSpan(22,0,0)));
        }

        private void CreateBlips()
        {
            foreach (var station in _stations)
            {
                var blip = World.CreateBlip(station.Position);
                blip.Sprite = BlipSprite.JerryCan;
                blip.Scale = 0.6f;
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
                    if (station.Blip == null) continue;

                    bool open = station.IsOpen();
                    station.Blip.Color = open ? BlipColor.Green : BlipColor.Red;
                    station.Blip.Name = open ? "Station-service (ouverte)" : "Station-service (fermée)";

                    float dist = Game.Player.Character.Position.DistanceTo(station.Position);
                    if (dist < 20f)
                    {
                        string status = open ? "~g~Ouverte" : "~r~Fermée";
                        Screen.ShowSubtitle($"Station-service : {status}", 1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"GasStation tick error: {ex.Message}");
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                foreach (var station in _stations)
                {
                    station.Blip?.Delete();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"GasStation cleanup error: {ex.Message}");
            }
        }
    }
}
