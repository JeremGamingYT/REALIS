using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.UI;
using REALIS.Core;

namespace REALIS.NPC
{
    /// <summary>
    /// Réactions crédibles quand le joueur bouscule un passant.
    /// </summary>
    public class BumpReactionManager : Script
    {
        private readonly Dictionary<int, DateTime> _bumpTimes = new();
        private readonly Random _rand = new();
        private int _tick;
        private const int UPDATE_INTERVAL = 10;

        public BumpReactionManager()
        {
            Tick += OnTick;
            Aborted += OnAborted;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _tick++;
                if (_tick % UPDATE_INTERVAL != 0) return;

                Ped player = Game.Player.Character;
                var peds = World.GetNearbyPeds(player.Position, 2f);

                foreach (var ped in peds)
                {
                    if (ped == null || !ped.Exists() || ped == player) continue;

                    if (ped.IsTouching(player))
                    {
                        if (!_bumpTimes.ContainsKey(ped.Handle))
                        {
                            _bumpTimes[ped.Handle] = DateTime.Now;
                            string msg = _rand.NextDouble() < 0.5 ? "Fais gaffe, bordel !" : "Hey, tu m\u2019as touché !";
                            Screen.ShowSubtitle(msg, 1000);
                        }
                        else if ((DateTime.Now - _bumpTimes[ped.Handle]).TotalSeconds > 1.5)
                        {
                            if (_rand.NextDouble() < 0.5)
                            {
                                Screen.ShowSubtitle("J'appelle la police !", 1000);
                                Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ped.Handle, "WORLD_HUMAN_STAND_MOBILE", 0, true);
                            }
                            else
                            {
                                ped.Task.FightAgainst(player);
                            }
                            _bumpTimes[ped.Handle] = DateTime.Now.AddSeconds(5);
                        }
                    }
                    else if (_bumpTimes.ContainsKey(ped.Handle) &&
                             (DateTime.Now - _bumpTimes[ped.Handle]).TotalSeconds > 3)
                    {
                        _bumpTimes.Remove(ped.Handle);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"BumpReaction tick error: {ex.Message}");
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            _bumpTimes.Clear();
        }
    }
}
