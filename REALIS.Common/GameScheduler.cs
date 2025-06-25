using System;
using System.Collections.Generic;

namespace REALIS.Common
{
    /// <summary>
    /// Permet de planifier lexécution différée dune action sans bloquer le thread ScriptHookVDotNet.
    /// Chaque Tick, ModuleManager appellera <see cref="Tick"/> afin dexécuter celles dont léchéance est atteinte.
    /// </summary>
    public static class GameScheduler
    {
        private class ScheduledItem
        {
            public DateTime RunAt;
            public Action Action;
        }

        private static readonly List<ScheduledItem> _items = new List<ScheduledItem>();
        private static readonly object _lockObj = new object();

        /// <summary>
        /// Planifie laction <paramref name="action"/> pour quelle sexécute après <paramref name="delayMs"/> millisecondes.
        /// </summary>
        public static void Schedule(Action action, int delayMs)
        {
            if (action == null) return;
            lock (_lockObj)
            {
                _items.Add(new ScheduledItem { RunAt = DateTime.Now.AddMilliseconds(delayMs), Action = action });
            }
        }

        /// <summary>
        /// Doit être appelée à chaque Tick pour exécuter les actions planifiées arrivées à échéance.
        /// </summary>
        public static void Tick()
        {
            List<ScheduledItem> toRun = null;

            lock (_lockObj)
            {
                var now = DateTime.Now;
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    if (now >= _items[i].RunAt)
                    {
                        if (toRun == null) toRun = new List<ScheduledItem>();
                        toRun.Add(_items[i]);
                        _items.RemoveAt(i);
                    }
                }
            }

            if (toRun == null) return;
            foreach (var item in toRun)
            {
                try { item.Action.Invoke(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GameScheduler action exception: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Représente une plage horaire pour les activités du jeu
    /// </summary>
    public class TimeRange
    {
        public static readonly TimeRange AnyTime = new TimeRange(0, 24);
        
        public int StartHour { get; set; }
        public int EndHour { get; set; }

        public TimeRange(int startHour, int endHour)
        {
            StartHour = startHour;
            EndHour = endHour;
        }

        public bool IsActive(int currentHour)
        {
            if (StartHour <= EndHour)
                return currentHour >= StartHour && currentHour <= EndHour;
            else
                return currentHour >= StartHour || currentHour <= EndHour;
        }
    }
} 