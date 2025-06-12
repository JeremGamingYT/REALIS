using System;
using System.IO;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.Math;
using GTA.UI;
using Newtonsoft.Json;
using REALIS.Common;

namespace REALIS.Core
{
    /// <summary>
    /// System for enabling and loading interiors and creating map blips for them.
    /// </summary>
    public class EnableInteriors : Script
    {
        private List<InteriorDefinition> _interiors = new List<InteriorDefinition>();
        private readonly string _configPath;
        private List<int> _pinnedInteriorIds = new List<int>();
        private List<TeleportLink> _teleports = new List<TeleportLink>();

        public EnableInteriors()
        {
            Tick += OnTick;
            Aborted += OnAborted;

            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "InteriorsConfig.json");
            LoadInteriorsConfig();
            EnableAllInteriors();
            CreateBlips();
            Logger.Info("EnableInteriors system initialized.");
        }

        private void OnTick(object sender, EventArgs e)
        {
            HandleTeleports();
        }

        private void LoadInteriorsConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _interiors = JsonConvert.DeserializeObject<List<InteriorDefinition>>(json)
                                 ?? new List<InteriorDefinition>();
                }
                else
                {
                    Logger.Error($"EnableInteriors: Config file not found at {_configPath}");
                }

                // build teleports list
                foreach (var def in _interiors)
                {
                    if (def.Teleport != null)
                    {
                        _teleports.Add(def.Teleport);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"EnableInteriors: Failed to load config - {ex.Message}");
            }
        }

        private void EnableAllInteriors()
        {
            foreach (var def in _interiors)
            {
                if (def.Ipls != null)
                {
                    foreach (var ipl in def.Ipls)
                    {
                        RemoveFakeVariants(ipl);
                        Function.Call(Hash.REQUEST_IPL, ipl);
                    }
                }
                // First remove conflicting IPLs
                if (def.RemoveIpls != null)
                {
                    foreach (var rip in def.RemoveIpls)
                    {
                        Function.Call(Hash.REMOVE_IPL, rip);
                    }
                }
                // Pin interior in memory to ensure collision loaded
                int interiorId = Function.Call<int>(Hash.GET_INTERIOR_AT_COORDS, def.X, def.Y, def.Z);
                if (interiorId != 0)
                {
                    // ensure interior is not disabled or capped
                    Function.Call(Hash.DISABLE_INTERIOR, interiorId, false);
                    Function.Call(Hash.CAP_INTERIOR, interiorId, false);
                    Function.Call(Hash.PIN_INTERIOR_IN_MEMORY, interiorId);
                    _pinnedInteriorIds.Add(interiorId);
                }

                // Special cases requiring extra door logic
                if (def.Name?.Equals("Tequi-la-la", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // explicitly unlock front and rear doors so the player can enter
                    const int doorModel = 993120320; // door model hash for v_rockclub doors

                    // front entrance door
                    Function.Call((Hash)0x9B12F9A24FABEDB0, doorModel, -565.1712f, 276.6259f, 83.28626f, false, 0.0f, 0.0f, 0.0f);

                    // rear/side exit door
                    Function.Call((Hash)0x9B12F9A24FABEDB0, doorModel, -561.2866f, 293.5044f, 87.77851f, false, 0.0f, 0.0f, 0.0f);
                }
            }
        }

        private void CreateBlips()
        {
            foreach (var def in _interiors)
            {
                var pos = new Vector3(def.X, def.Y, def.Z);
                var blip = World.CreateBlip(pos);
                blip.Sprite = BlipSprite.Standard;
                blip.Color = BlipColor.White;
                blip.Name = def.Name;
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            // Unpin all pinned interiors
            foreach (var id in _pinnedInteriorIds)
            {
                Function.Call(Hash.UNPIN_INTERIOR, id);
            }
        }

        private void RemoveFakeVariants(string ipl)
        {
            try
            {
                if (ipl.Contains("_real_"))
                {
                    var fake = ipl.Replace("_real_", "_fake_");
                    Function.Call(Hash.REMOVE_IPL, fake);
                    Function.Call(Hash.REMOVE_IPL, fake + "_lod");
                }
                if (ipl.EndsWith("_real_interior"))
                {
                    var baseName = ipl.Replace("_real_interior", "");
                    Function.Call(Hash.REMOVE_IPL, baseName + "_fake_interior");
                    Function.Call(Hash.REMOVE_IPL, baseName + "_fake_interior_lod");
                }
                if (ipl.Contains("onmission"))
                {
                    var off = ipl.Replace("onmission", "offmission");
                    Function.Call(Hash.REMOVE_IPL, off);
                    Function.Call(Hash.REMOVE_IPL, off + "_lod");
                }
                if (ipl.Equals("farm", StringComparison.OrdinalIgnoreCase))
                {
                    Function.Call(Hash.REMOVE_IPL, "farm_burnt");
                    Function.Call(Hash.REMOVE_IPL, "farm_burnt_props");
                }
                if (ipl.StartsWith("hei_yacht_heist", StringComparison.OrdinalIgnoreCase))
                {
                    Function.Call(Hash.REMOVE_IPL, "smboat");
                    Function.Call(Hash.REMOVE_IPL, "smboat_lod");
                }
            }
            catch { }
        }

        private void HandleTeleports()
        {
            if (_teleports.Count == 0) return;
            var player = Game.Player.Character;
            if (!player.Exists()) return;
            var pos = player.Position;
            foreach (var tp in _teleports)
            {
                if (pos.DistanceTo(tp.Entrance) < 1.5f)
                {
                    TeleportPlayer(tp.Interior, tp.InteriorHeading);
                    return;
                }
                if (pos.DistanceTo(tp.Interior) < 1.5f)
                {
                    TeleportPlayer(tp.Entrance, tp.EntranceHeading);
                    return;
                }
            }
        }

        private void TeleportPlayer(Vector3 target, float heading)
        {
            Function.Call(Hash.DO_SCREEN_FADE_OUT, 200);
            Wait(250);
            // Ensure the destination area is streamed in with collision before moving the player
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, target.X, target.Y, target.Z);
            // Force interior/exterior streaming if needed
            Function.Call(Hash.NEW_LOAD_SCENE_START_SPHERE, target.X, target.Y, target.Z, 100f, (int)NewLoadSceneFlags.InteriorAndExterior | (int)NewLoadSceneFlags.RequireCollision);

            int timeout = Game.GameTime + 5000; // 5-second safety timeout
            while (!Function.Call<bool>(Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, Game.Player.Character) && Game.GameTime < timeout)
            {
                Wait(50);
            }

            Game.Player.Character.Position = target;
            Game.Player.Character.Heading = heading;
            Wait(100);
            Function.Call(Hash.DO_SCREEN_FADE_IN, 200);
        }

        private class TeleportLink
        {
            public Vector3 Entrance { get; set; }
            public float EntranceHeading { get; set; }
            public Vector3 Interior { get; set; }
            public float InteriorHeading { get; set; }
        }

        private class InteriorDefinition
        {
            public string Name { get; set; }
            public List<string>? Ipls { get; set; }
            public List<string>? RemoveIpls { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public TeleportLink? Teleport { get; set; }
        }
    }
} 