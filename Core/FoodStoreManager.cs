using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;

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
            public bool Accessible { get; }
            public Blip? Blip { get; set; }

            public FoodStore(Vector3 pos, bool accessible)
            {
                Position = pos;
                Accessible = accessible;
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
            // Magasins accessibles hors stations-service
            _stores.Add(new FoodStore(new Vector3(25.7f, -1347.3f, 29.5f), true));
            _stores.Add(new FoodStore(new Vector3(-1222.9f, -907.0f, 12.3f), true));
            _stores.Add(new FoodStore(new Vector3(-1487.5f, -379.1f, 40.1f), true));
            _stores.Add(new FoodStore(new Vector3(-2968.2f, 390.9f, 15.0f), true));
            _stores.Add(new FoodStore(new Vector3(-3242.2f, 999.9f, 12.8f), true));
            _stores.Add(new FoodStore(new Vector3(548.5f, 2671.4f, 42.2f), true));
            _stores.Add(new FoodStore(new Vector3(1959.0f, 3741.4f, 32.3f), true));
            _stores.Add(new FoodStore(new Vector3(2676.0f, 3280.0f, 55.2f), true));
            _stores.Add(new FoodStore(new Vector3(1729.7f, 6414.9f, 35.0f), true));
            _stores.Add(new FoodStore(new Vector3(1134.0f, -983.1f, 46.4f), true));
        }

        private void CreateBlips()
        {
            foreach (var store in _stores)
            {
                if (!store.Accessible) continue;

                var blip = World.CreateBlip(store.Position);
                blip.Sprite = BlipSprite.Store;
                blip.Scale = 0.9f;
                store.Blip = blip;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            // Pas de traitement intensif nécessaire
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
