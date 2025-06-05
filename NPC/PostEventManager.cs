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
        private enum SceneStage
        {
            WaitingForAmbulance,
            AmbulanceDriving,
            MedicsWorking
        }

        private class SceneInfo
        {
            public Ped Body { get; }
            public DateTime Created { get; } = DateTime.Now;
            public Vehicle? Ambulance { get; set; }
            public Ped? Medic1 { get; set; }
            public Ped? Medic2 { get; set; }
            public Prop? Cover { get; set; }
            public List<Ped> Onlookers { get; } = new();
            public DateTime StageTime { get; set; } = DateTime.Now;
            public SceneStage Stage { get; set; } = SceneStage.WaitingForAmbulance;

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

                switch (scene.Stage)
                {
                    case SceneStage.WaitingForAmbulance:
                        DispatchAmbulance(scene);
                        break;

                    case SceneStage.AmbulanceDriving:
                        CheckAmbulanceArrival(scene);
                        break;

                    case SceneStage.MedicsWorking:
                        HandleActiveScene(scene);
                        break;
                }

                if ((DateTime.Now - scene.Created).TotalSeconds > 90)
                {
                    CleanupScene(scene);
                }
            }
        }

        private void DispatchAmbulance(SceneInfo scene)
        {
            try
            {
                Vector3 spawnPos = World.GetNextPositionOnStreet(scene.Body.Position.Around(60f));

                var ambModel = new Model(VehicleHash.Ambulance);
                ambModel.Request(500);
                var pedModel = new Model(PedHash.Paramedic01SMM);
                pedModel.Request(500);
                if (!ambModel.IsLoaded || !pedModel.IsLoaded) return;

                var ambulance = World.CreateVehicle(ambModel, spawnPos);
                if (ambulance == null || !ambulance.Exists()) return;

                var driver = ambulance.CreatePedOnSeat(VehicleSeat.Driver, pedModel);
                var pass = ambulance.CreatePedOnSeat(VehicleSeat.Passenger, pedModel);
                if (driver == null || pass == null) { ambulance.Delete(); return; }

                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD,
                    driver.Handle,
                    ambulance.Handle,
                    scene.Body.Position.X,
                    scene.Body.Position.Y,
                    scene.Body.Position.Z,
                    25f,
                    0,
                    5f);

                scene.Ambulance = ambulance;
                scene.Medic1 = driver;
                scene.Medic2 = pass;
                scene.Stage = SceneStage.AmbulanceDriving;
                scene.StageTime = DateTime.Now;

                ambModel.MarkAsNoLongerNeeded();
                pedModel.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                Logger.Error($"Dispatch ambulance error: {ex.Message}");
            }
        }

        private void CheckAmbulanceArrival(SceneInfo scene)
        {
            try
            {
                if (scene.Ambulance == null || !scene.Ambulance.Exists())
                {
                    scene.Stage = SceneStage.WaitingForAmbulance;
                    return;
                }

                if (scene.Ambulance.Position.DistanceTo(scene.Body.Position) < 7f)
                {
                    Function.Call(Hash.TASK_LEAVE_VEHICLE, scene.Medic1.Handle, scene.Ambulance.Handle, 0);
                    Function.Call(Hash.TASK_LEAVE_VEHICLE, scene.Medic2.Handle, scene.Ambulance.Handle, 0);

                    scene.Medic1.Task.GoTo(scene.Body.Position + new Vector3(0.5f, 0f, 0f));
                    scene.Medic1.Task.StartScenario("CODE_HUMAN_MEDIC_KNEEL", -1);
                    scene.Medic2.Task.GoTo(scene.Body.Position + new Vector3(-0.5f, 0f, 0f));
                    scene.Medic2.Task.StartScenario("CODE_HUMAN_MEDIC_KNEEL", -1);

                    SpawnOnlookers(scene);

                    scene.Stage = SceneStage.MedicsWorking;
                    scene.StageTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ambulance arrival error: {ex.Message}");
            }
        }

        private void HandleActiveScene(SceneInfo scene)
        {
            try
            {
                if (scene.Cover == null && (DateTime.Now - scene.StageTime).TotalSeconds > 15)
                {
                    TryCoverBody(scene);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Active scene error: {ex.Message}");
            }
        }

        private void SpawnOnlookers(SceneInfo scene)
        {
            try
            {
                var peds = World.GetNearbyPeds(scene.Body.Position, 15f);
                int added = 0;
                foreach (var ped in peds)
                {
                    if (added >= 3) break;
                    if (ped == null || !ped.Exists() || ped == scene.Medic1 || ped == scene.Medic2 || ped == scene.Body) continue;
                    if (scene.Onlookers.Contains(ped)) continue;

                    ped.Task.GoTo(scene.Body.Position.Around(2f));
                    string scenario = _rand.NextDouble() < 0.5 ? "WORLD_HUMAN_STAND_MOBILE" : "WORLD_HUMAN_MOBILE_FILM_SHOCKING";
                    Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ped.Handle, scenario, 0, true);
                    scene.Onlookers.Add(ped);
                    added++;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Spawn onlookers error: {ex.Message}");
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
                scene.Medic1?.Delete();
                scene.Medic2?.Delete();
                scene.Ambulance?.Delete();
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
