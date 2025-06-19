using System;
using System.Collections.Generic;

namespace REALIS.Common
{
    /// <summary>
    /// Permet de planifier lexécution différée dune action sans bloquer le thread ScriptHookVDotNet.
    /// Chaque Tick, ModuleManager appellera <see cref="Tick"/> afin dexécuter celles dont léchéance est atteinte.
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
        /// Planifie laction <paramref name="action"/> pour quelle sexécute après <paramref name="delayMs"/> millisecondes.
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
} 