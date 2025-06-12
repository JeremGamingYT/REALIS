using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using REALIS.Core;

namespace REALIS.Events.BankRob.Manager
{
    /// <summary>
    /// Gère le hall de la Pacific Standard Bank : fait apparaître des clients aléatoires et des guichetiers assis.
    /// Cette première étape prépare l'ambiance pour un futur braquage.
    /// </summary>
    public class BankRobManager : Script
    {
        private readonly List<Ped> _customers = new();
        private readonly List<Ped> _bankers = new();
        private readonly Random _rnd = new();

        private const int CUSTOMER_WANDER_RADIUS = 4;
        private const int MAX_CUSTOMERS = 8;
        private const int UPDATE_INTERVAL = 200; // tous les 200 ticks ~ 4 s

        private readonly Vector3 _lobbyCenter = new Vector3(247.0f, 228.0f, 106.29f);

        private readonly (Vector3 pos, float heading)[] _bankerSpots =
        {
            (new Vector3(243.85f, 226.25f, 106.29f), 70f),
            (new Vector3(247.21f, 224.93f, 106.29f), 70f),
            (new Vector3(253.98f, 223.53f, 106.29f), 250f),
            (new Vector3(252.28f, 223.15f, 106.29f), 250f)
        };

        private int _tickCounter = 0;
        private int _interiorId;

        public BankRobManager()
        {
            Tick += OnTick;
            Aborted += OnAborted;

            LoadBankInterior();
            SpawnBankers();
            SpawnInitialCustomers();
            Logger.Info("Bank robbery ambient system initialised (BankRobManager).");
        }

        #region Tick / Maintenance
        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _tickCounter++;
                if (_tickCounter % UPDATE_INTERVAL != 0) return;

                MaintainBankers();
                MaintainCustomers();
            }
            catch (Exception ex)
            {
                Logger.Error($"BankRobManager tick error: {ex.Message}");
            }
        }
        #endregion

        #region Bankers
        private void SpawnBankers()
        {
            foreach (var spot in _bankerSpots)
            {
                // Ignore player character when checking if the spot is free
                var nearbyPed = World.GetClosestPed(spot.pos, 1.0f);
                if (nearbyPed != null && nearbyPed != Game.Player.Character) continue;

                // Assurer le chargement des collisions autour de la zone
                Function.Call(Hash.REQUEST_COLLISION_AT_COORD, spot.pos.X, spot.pos.Y, spot.pos.Z);

                Model model = new Model("s_m_m_bankteller_01");
                if (!model.IsLoaded) model.Request(500);
                if (!model.IsLoaded)
                {
                    Logger.Error("Failed to load banker model: s_m_m_bankteller_01");
                    continue;
                }

                Ped ped = World.CreatePed(model, spot.pos, spot.heading);
                if (ped == null || !ped.Exists())
                {
                    Logger.Error("Could not create banker ped.");
                    continue;
                }

                SetupBankerPed(ped);
                _bankers.Add(ped);
                model.MarkAsNoLongerNeeded();
            }
        }

        private static void SetupBankerPed(Ped ped)
        {
            ped.IsPersistent = true;
            ped.BlockPermanentEvents = true;
            ped.CanBeTargetted = false;
            ped.IsPositionFrozen = false;
            ped.Health = 200;

            // Animation: assis devant son poste
            ped.Task.StartScenarioInPlace("PROP_HUMAN_SEAT_COMPUTER", 0, true);
        }

        private void MaintainBankers()
        {
            for (int i = 0; i < _bankers.Count; i++)
            {
                var banker = _bankers[i];
                if (banker == null || !banker.Exists())
                {
                    _bankers.RemoveAt(i);
                    i--;
                    continue;
                }

                banker.Task.StartScenarioInPlace("PROP_HUMAN_SEAT_COMPUTER", 0, true);
            }

            if (_bankers.Count < _bankerSpots.Length)
            {
                SpawnBankers();
            }
        }
        #endregion

        #region Customers
        private void SpawnInitialCustomers()
        {
            int customerCount = _rnd.Next(4, MAX_CUSTOMERS + 1);
            for (int i = 0; i < customerCount; i++)
            {
                SpawnCustomer();
            }
        }

        private void SpawnCustomer()
        {
            Model model = new Model(GetRandomCustomerModel());
            if (!model.IsLoaded) model.Request(500);
            if (!model.IsLoaded)
            {
                Logger.Error("Failed to load customer model.");
                return;
            }

            Vector3 pos = _lobbyCenter + new Vector3(
                _rnd.NextFloat(-CUSTOMER_WANDER_RADIUS, CUSTOMER_WANDER_RADIUS),
                _rnd.NextFloat(-CUSTOMER_WANDER_RADIUS, CUSTOMER_WANDER_RADIUS),
                0f);

            Ped ped = World.CreatePed(model, pos);
            if (ped == null || !ped.Exists())
            {
                Logger.Error("CreatePed failed for customer – will retry later.");
                return;
            }

            SetupCustomerPed(ped);
            _customers.Add(ped);
            model.MarkAsNoLongerNeeded();
        }

        private static void SetupCustomerPed(Ped ped)
        {
            ped.IsPersistent = true;
            ped.BlockPermanentEvents = true;
            ped.Health = 200;

            // Errance douce dans le hall
            ped.Task.WanderAround(ped.Position, 4f);
        }

        private void MaintainCustomers()
        {
            // Nettoyer les clients inexistants
            for (int i = 0; i < _customers.Count; i++)
            {
                var ped = _customers[i];
                if (ped == null || !ped.Exists())
                {
                    _customers.RemoveAt(i);
                    i--;
                }
            }

            // Respawn si besoin
            while (_customers.Count < MAX_CUSTOMERS)
            {
                SpawnCustomer();
            }
        }
        #endregion

        #region Utils / Cleanup
        private string GetRandomCustomerModel()
        {
            string[] models =
            {
                "a_m_y_business_01",
                "a_m_y_business_02",
                "a_m_y_business_03",
                "a_f_y_business_01",
                "a_f_y_business_02",
                "a_f_y_smart_01",
                "a_m_m_business_01",
                "a_m_y_genstreet_01",
                "a_f_y_genhot_01"
            };
            return models[_rnd.Next(models.Length)];
        }

        private void LoadBankInterior()
        {
            try
            {
                _interiorId = Function.Call<int>(Hash.GET_INTERIOR_AT_COORDS, _lobbyCenter.X, _lobbyCenter.Y, _lobbyCenter.Z);
                if (_interiorId != 0)
                {
                    Function.Call(Hash.PIN_INTERIOR_IN_MEMORY, _interiorId);
                    Function.Call(Hash.DISABLE_INTERIOR, _interiorId, false);
                    Logger.Info($"Pacific Standard interior pinned (ID: {_interiorId}).");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to pin bank interior: {ex.Message}");
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                foreach (var ped in _customers)
                {
                    if (ped != null && ped.Exists()) ped.Delete();
                }
                foreach (var ped in _bankers)
                {
                    if (ped != null && ped.Exists()) ped.Delete();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"BankRobManager cleanup error: {ex.Message}");
            }
            finally
            {
                _customers.Clear();
                _bankers.Clear();
            }

            // Unpin interior
            if (_interiorId != 0)
            {
                try { Function.Call(Hash.UNPIN_INTERIOR, _interiorId); }
                catch { }
            }
        }
        #endregion
    }

    /// <summary>
    /// Extension simple pour générer un float aléatoire.
    /// </summary>
    internal static class RandomExtensions
    {
        public static float NextFloat(this Random rnd, float minValue, float maxValue)
        {
            return (float)(minValue + rnd.NextDouble() * (maxValue - minValue));
        }
    }
} 