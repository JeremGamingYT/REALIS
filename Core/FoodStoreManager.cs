using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.UI;

namespace REALIS.Core
{
    /// <summary>
    /// Affiche les magasins d'alimentation accessibles sur la carte.
    /// </summary>
    public class FoodStoreManager : Script
    {
        private class FoodStore
        {
            public Vector3 Position { get; }
            public Vector3 Entrance { get; }
            public TimeSpan OpenTime { get; }
            public TimeSpan CloseTime { get; }
            public bool Accessible { get; }
            public Blip? Blip { get; set; }
            public bool LastOpenState { get; set; }
            public DateTime LastNotificationTime { get; set; }
            public DateTime LastAccessDeniedTime { get; set; }
            public bool PlayerWasInside { get; set; }

            public FoodStore(Vector3 pos, TimeSpan open, TimeSpan close, bool accessible)
            {
                Position = pos;
                Entrance = pos;
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

                return now >= OpenTime || now <= CloseTime;
            }
        }

        private readonly List<FoodStore> _stores = new();

        public FoodStoreManager()
        {
            Tick += OnTick;
            Aborted += OnAborted;

            InitializeStores();
            CreateBlips();
        }

        private void InitializeStores()
        {
            TimeSpan open = new TimeSpan(6, 0, 0);
            TimeSpan close = new TimeSpan(23, 0, 0);

            // Magasins accessibles hors stations-service
            _stores.Add(new FoodStore(new Vector3(25.7f, -1347.3f, 29.5f), open, close, true));
            _stores.Add(new FoodStore(new Vector3(-1222.9f, -907.0f, 12.3f), open, close, true));
            _stores.Add(new FoodStore(new Vector3(-1487.5f, -379.1f, 40.1f), open, close, true));
            _stores.Add(new FoodStore(new Vector3(-2968.2f, 390.9f, 15.0f), open, close, true));
            _stores.Add(new FoodStore(new Vector3(-3242.2f, 999.9f, 12.8f), open, close, true));
            _stores.Add(new FoodStore(new Vector3(548.5f, 2671.4f, 42.2f), open, close, true));
            _stores.Add(new FoodStore(new Vector3(1959.0f, 3741.4f, 32.3f), open, close, true));
            _stores.Add(new FoodStore(new Vector3(2676.0f, 3280.0f, 55.2f), open, close, true));
            _stores.Add(new FoodStore(new Vector3(1729.7f, 6414.9f, 35.0f), open, close, true));
            _stores.Add(new FoodStore(new Vector3(1134.0f, -983.1f, 46.4f), open, close, true));
        }

        private void CreateBlips()
        {
            foreach (var store in _stores)
            {
                if (!store.Accessible) continue;

                var blip = World.CreateBlip(store.Position);
                blip.Sprite = BlipSprite.Store;
                blip.Scale = 0.9f;
                blip.Color = store.IsOpen() ? BlipColor.Green : BlipColor.Red;
                blip.Name = store.IsOpen() ? "Magasin (ouvert)" : "Magasin (fermé)";
                store.Blip = blip;
            }
        }

        private int _tickCounter = 0;
        private const int UPDATE_INTERVAL = 100;

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _tickCounter++;
                if (_tickCounter % UPDATE_INTERVAL != 0) return;

                foreach (var store in _stores)
                {
                    if (!store.Accessible || store.Blip == null) continue;

                    bool open = store.IsOpen();
                    store.Blip.Color = open ? BlipColor.Green : BlipColor.Red;
                    store.Blip.Name = open ? "Magasin (ouvert)" : "Magasin (fermé)";

                    float dist = Game.Player.Character.Position.DistanceTo(store.Entrance);
                    bool inside = dist < 25f;

                    if (inside && (!store.PlayerWasInside || open != store.LastOpenState))
                    {
                        if ((DateTime.Now - store.LastNotificationTime).TotalSeconds > 1)
                        {
                            Notification.PostTicker(open ? "~g~Magasin ouvert" : "~r~Magasin fermé", true);
                            store.LastNotificationTime = DateTime.Now;
                        }
                    }

                    if (!open && inside && dist < 5f)
                    {
                        if ((DateTime.Now - store.LastAccessDeniedTime).TotalSeconds > 1)
                        {
                            Notification.PostTicker("~r~Magasin fermé", true);
                            store.LastAccessDeniedTime = DateTime.Now;
                        }
                    }

                    store.PlayerWasInside = inside;
                    store.LastOpenState = open;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"FoodStore tick error: {ex.Message}");
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                foreach (var store in _stores)
                {
                    store.Blip?.Delete();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"FoodStore cleanup error: {ex.Message}");
            }
        }
    }
}
