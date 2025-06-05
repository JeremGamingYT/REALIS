using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Core;

namespace REALIS.NPC
{
    /// <summary>
    /// Gère les réactions post-événement comme l'arrivée des secours
    /// après une fusillade ou un accident.
    /// </summary>
    public class PostEventManager : Script
    {
        private class SceneInfo
        {
            public Ped Body { get; }
            public DateTime Created { get; } = DateTime.Now;
            public Ped? Medic { get; set; }
            public Ped? Cop { get; set; }
            public Prop? Cover { get; set; }
            public List<Ped> Onlookers { get; } = new();

            public SceneInfo(Ped body)
            {
                Body = body;
            }
        }

        private readonly List<SceneInfo> _scenes = new();
        private readonly Random _rand = new();
        private int _tick;
        private const int UPDATE_INTERVAL = 50;
        private const float SCAN_RADIUS = 25f;
        private const int MAX_SCENES = 3;

        public PostEventManager()
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

                DetectScenes();
                UpdateScenes();
            }
            catch (Exception ex)
            {
                Logger.Error($"PostEvent tick error: {ex.Message}");
            }
        }

        private void DetectScenes()
        {
            Ped player = Game.Player.Character;
            var peds = World.GetNearbyPeds(player.Position, SCAN_RADIUS);
            foreach (var ped in peds)
            {
                if (ped == null || !ped.Exists() || ped == player) continue;
                if (!ped.IsDead) continue;
                if (_scenes.Exists(s => s.Body == ped)) continue;
                if (_scenes.Count >= MAX_SCENES) break;

                _scenes.Add(new SceneInfo(ped));
            }
        }

        private void UpdateScenes()
        {
            foreach (var scene in new List<SceneInfo>(_scenes))
            {
                if (scene.Body == null || !scene.Body.Exists())
                {
                    CleanupScene(scene);
                    continue;
                }

                if (scene.Medic == null)
                {
                    SpawnResponders(scene);
                }

                if (scene.Cover == null && (DateTime.Now - scene.Created).TotalSeconds > 10)
                {
                    TryCoverBody(scene);
                }

                if ((DateTime.Now - scene.Created).TotalSeconds > 60)
                {
                    CleanupScene(scene);
                }
            }
        }

        private void SpawnResponders(SceneInfo scene)
        {
            try
            {
                Vector3 pos = scene.Body.Position + new Vector3(1f, 1f, 0f);

                var medicModel = new Model(PedHash.Paramedic01SMM);
                medicModel.Request(500);
                if (medicModel.IsLoaded)
                {
                    var medic = World.CreatePed(medicModel, pos);
                    medic.Task.GoTo(scene.Body);
                    medic.Task.StartScenario("CODE_HUMAN_MEDIC_KNEEL", -1);
                    scene.Medic = medic;
                }

                if (_rand.NextDouble() < 0.5)
                {
                    var copModel = new Model(PedHash.Cop01SMY);
                    copModel.Request(500);
                    if (copModel.IsLoaded)
                    {
                        var cop = World.CreatePed(copModel, pos + new Vector3(1f, -1f, 0f));
                        cop.Task.GoTo(scene.Body.Position + new Vector3(0.5f, 0f, 0f));
                        scene.Cop = cop;
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    Vector3 off = pos + new Vector3(_rand.Next(-3, 4), _rand.Next(-3, 4), 0f);
                    var mdl = new Model(PedHash.Tramp01AMM);
                    mdl.Request(500);
                    if (mdl.IsLoaded)
                    {
                        var by = World.CreatePed(mdl, off);
                        Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, by.Handle, "WORLD_HUMAN_STAND_MOBILE", 0, true);
                        scene.Onlookers.Add(by);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"SpawnResponder error: {ex.Message}");
            }
        }

        private void TryCoverBody(SceneInfo scene)
        {
            try
            {
                var prop = World.CreateProp("prop_body_bag01", scene.Body.Position, false, false);
                if (prop != null && prop.Exists())
                {
                    scene.Cover = prop;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Cover body error: {ex.Message}");
            }
        }

        private void CleanupScene(SceneInfo scene)
        {
            try
            {
                scene.Medic?.Delete();
                scene.Cop?.Delete();
                scene.Cover?.Delete();
                foreach (var by in scene.Onlookers)
                {
                    if (by != null && by.Exists()) by.Delete();
                }
            }
            catch { }
            finally
            {
                _scenes.Remove(scene);
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            foreach (var scene in new List<SceneInfo>(_scenes))
            {
                CleanupScene(scene);
            }
        }
    }
}
