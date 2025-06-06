using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;

namespace REALIS.NPC.Police
{
    /// <summary>
    /// Gestionnaire des postes de police et de leurs informations
    /// </summary>
    public class PoliceStationManager
    {
        private readonly List<PoliceStation> _policeStations;

        public PoliceStationManager()
        {
            _policeStations = new List<PoliceStation>();
            InitializePoliceStations();
        }

        private void InitializePoliceStations()
        {
            // Mission Row Police Station
            _policeStations.Add(new PoliceStation
            {
                Name = "Mission Row Police Station",
                Position = new Vector3(436.1f, -982.1f, 30.7f),
                SpawnPoint = new Vector3(434.0f, -981.0f, 30.7f),
                Heading = 180f,
                Type = PoliceStationType.CityPolice
            });

            // Vespucci Police Station
            _policeStations.Add(new PoliceStation
            {
                Name = "Vespucci Police Station",
                Position = new Vector3(-1108.4f, -845.8f, 19.3f),
                SpawnPoint = new Vector3(-1110.0f, -847.0f, 19.3f),
                Heading = 45f,
                Type = PoliceStationType.CityPolice
            });

            // Davis Sheriff Station
            _policeStations.Add(new PoliceStation
            {
                Name = "Davis Sheriff Station",
                Position = new Vector3(361.9f, -1584.1f, 29.3f),
                SpawnPoint = new Vector3(360.0f, -1582.0f, 29.3f),
                Heading = 270f,
                Type = PoliceStationType.Sheriff
            });

            // Paleto Bay Sheriff Station
            _policeStations.Add(new PoliceStation
            {
                Name = "Paleto Bay Sheriff Station",
                Position = new Vector3(-448.8f, 6014.0f, 31.7f),
                SpawnPoint = new Vector3(-450.0f, 6012.0f, 31.7f),
                Heading = 135f,
                Type = PoliceStationType.Sheriff
            });

            // Sandy Shores Sheriff Station
            _policeStations.Add(new PoliceStation
            {
                Name = "Sandy Shores Sheriff Station",
                Position = new Vector3(1853.2f, 3689.6f, 34.3f),
                SpawnPoint = new Vector3(1851.0f, 3687.0f, 34.3f),
                Heading = 270f,
                Type = PoliceStationType.Sheriff
            });

            // La Mesa Police Station
            _policeStations.Add(new PoliceStation
            {
                Name = "La Mesa Police Station",
                Position = new Vector3(826.3f, -1290.0f, 28.2f),
                SpawnPoint = new Vector3(824.0f, -1288.0f, 28.2f),
                Heading = 90f,
                Type = PoliceStationType.CityPolice
            });
        }

        public PoliceStation GetNearestStation(Vector3 position)
        {
            PoliceStation nearestStation = _policeStations[0];
            float shortestDistance = Vector3.Distance(position, nearestStation.Position);

            foreach (var station in _policeStations)
            {
                float distance = Vector3.Distance(position, station.Position);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearestStation = station;
                }
            }

            return nearestStation;
        }

        public PoliceStation GetStationByType(PoliceStationType type)
        {
            foreach (var station in _policeStations)
            {
                if (station.Type == type)
                {
                    return station;
                }
            }

            return _policeStations[0]; // Retourner la première station par défaut
        }

        public List<PoliceStation> GetAllStations()
        {
            return new List<PoliceStation>(_policeStations);
        }

        public bool IsNearPoliceStation(Vector3 position, float radius = 20f)
        {
            foreach (var station in _policeStations)
            {
                if (Vector3.Distance(position, station.Position) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        public PoliceStation? GetStationAt(Vector3 position, float radius = 20f)
        {
            foreach (var station in _policeStations)
            {
                if (Vector3.Distance(position, station.Position) <= radius)
                {
                    return station;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Représente un poste de police
    /// </summary>
    public class PoliceStation
    {
        public string Name { get; set; } = string.Empty;
        public Vector3 Position { get; set; }
        public Vector3 SpawnPoint { get; set; }
        public float Heading { get; set; }
        public PoliceStationType Type { get; set; }
    }

    /// <summary>
    /// Types de postes de police
    /// </summary>
    public enum PoliceStationType
    {
        CityPolice,  // Police de la ville
        Sheriff,     // Sheriff du comté
        Highway      // Police routière
    }
} 