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
                // Spawn the ambulance farther away so it has to actually drive to the scene
                Vector3 spawnPos = World.GetNextPositionOnStreet(scene.Body.Position.Around(200f));

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

                // Drive to the victim location using a task sequence
                TaskSequence driveSeq = new TaskSequence();
                driveSeq.AddTask.DriveTo(ambulance, scene.Body.Position, 5f,
                    VehicleDrivingFlags.StopForVehicles | VehicleDrivingFlags.StopAtTrafficLights,
                    25f);
                driveSeq.Close();
                driver.Task.PerformSequence(driveSeq);
                driveSeq.Dispose();

                driver.BlockPermanentEvents = true;
                pass.BlockPermanentEvents = true;

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
                    if (scene.Medic1 != null && scene.Medic1.Exists())
                    {
                        scene.Medic1.BlockPermanentEvents = true;
                        scene.Medic1.Task.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);
                        Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, scene.Medic1.Handle, 
                            scene.Body.Position.X + 0.5f, scene.Body.Position.Y, scene.Body.Position.Z, 1.0f, -1, 0.0f, 0.0f);
                    }

                    if (scene.Medic2 != null && scene.Medic2.Exists())
                    {
                        scene.Medic2.BlockPermanentEvents = true;
                        scene.Medic2.Task.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);
                        Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, scene.Medic2.Handle, 
                            scene.Body.Position.X - 0.5f, scene.Body.Position.Y, scene.Body.Position.Z, 1.0f, -1, 0.0f, 0.0f);
                    }

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
                // Check if medics should start their scenarios
                if ((DateTime.Now - scene.StageTime).TotalSeconds > 3)
                {
                    if (scene.Medic1 != null && scene.Medic1.Exists() && 
                        scene.Medic1.Position.DistanceTo(scene.Body.Position) < 2f &&
                        !scene.Medic1.IsInVehicle())
                    {
                        Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, scene.Medic1.Handle, "CODE_HUMAN_MEDIC_KNEEL", 0, true);
                    }
                    
                    if (scene.Medic2 != null && scene.Medic2.Exists() && 
                        scene.Medic2.Position.DistanceTo(scene.Body.Position) < 2f &&
                        !scene.Medic2.IsInVehicle())
                    {
                        Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, scene.Medic2.Handle, "CODE_HUMAN_MEDIC_KNEEL", 0, true);
                    }
                }

                // Handle onlookers behavior
                var onlookersToRemove = new List<Ped>();
                foreach (var onlooker in scene.Onlookers)
                {
                    if (onlooker == null || !onlooker.Exists())
                    {
                        if (onlooker != null) onlookersToRemove.Add(onlooker);
                        continue;
                    }

                    // If onlooker is close enough and not doing anything specific, make them watch/film
                    if (onlooker.Position.DistanceTo(scene.Body.Position) < 5f && 
                        !onlooker.IsInVehicle() && 
                        onlooker.TaskSequenceProgress == -1)
                    {
                        string scenario = _rand.NextDouble() < 0.6 ? "WORLD_HUMAN_MOBILE_FILM_SHOCKING" : "WORLD_HUMAN_STAND_MOBILE";
                        Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, onlooker.Handle, scenario, 0, true);
                        Function.Call(Hash.TASK_LOOK_AT_ENTITY, onlooker.Handle, scene.Body.Handle, -1, 0, 2);
                    }
                }

                // Remove invalid onlookers
                foreach (var onlooker in onlookersToRemove)
                {
                    scene.Onlookers.Remove(onlooker);
                }

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
                    if (added >= 5) break;
                    if (ped == null || !ped.Exists() || ped == scene.Medic1 || ped == scene.Medic2 || ped == scene.Body) continue;
                    if (scene.Onlookers.Contains(ped)) continue;
                    if (ped.IsPlayer) continue;

                    // Block their reactions to the player so they stay focused on the scene
                    ped.BlockPermanentEvents = true;
                    
                    // Make them walk towards the scene
                    Vector3 lookPos = scene.Body.Position.Around(3f + (float)_rand.NextDouble() * 2f);
                    Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, ped.Handle, 
                        lookPos.X, lookPos.Y, lookPos.Z, 1.0f, -1, 0.0f, 0.0f);
                    
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