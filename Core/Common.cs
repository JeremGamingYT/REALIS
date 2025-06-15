using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using REALIS.Core;

namespace REALIS.Common
{
    /// <summary>
    /// Gestionnaire centralisé d'événements pour éviter les conflits entre modules.
    /// </summary>
    public class CentralEventManager
    {
        private static CentralEventManager? _instance;
        public static CentralEventManager Instance => _instance ??= new CentralEventManager();

        private readonly Dictionary<REALISEventType, List<IEventHandler>> _handlers = new();
        private readonly object _lock = new object();

        private CentralEventManager() { }

        public void RegisterHandler(REALISEventType eventType, IEventHandler handler)
        {
            lock (_lock)
            {
                if (!_handlers.ContainsKey(eventType))
                    _handlers[eventType] = new List<IEventHandler>();

                if (!_handlers[eventType].Contains(handler))
                    _handlers[eventType].Add(handler);
            }
        }

        public void UnregisterHandler(REALISEventType eventType, IEventHandler handler)
        {
            lock (_lock)
            {
                if (_handlers.ContainsKey(eventType))
                    _handlers[eventType].Remove(handler);
            }
        }

        public void FireEvent(GameEvent gameEvent)
        {
            lock (_lock)
            {
                if (_handlers.ContainsKey(gameEvent.EventType))
                {
                    foreach (var handler in _handlers[gameEvent.EventType].ToList())
                    {
                        try
                        {
                            if (handler.CanHandle(gameEvent))
                                handler.Handle(gameEvent);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Event handler error: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Interface pour les gestionnaires d'événements.
    /// </summary>
    public interface IEventHandler
    {
        bool CanHandle(GameEvent gameEvent);
        void Handle(GameEvent gameEvent);
    }

    /// <summary>
    /// Types d'événements du système REALIS.
    /// </summary>
    public enum REALISEventType
    {
        TrafficBlock,
        VehicleStuck,
        PlayerBlockingTraffic,
        TrafficJam,
        AmbientInteraction,
        WeatherEvent
    }

    /// <summary>
    /// Classe de base pour tous les événements du jeu.
    /// </summary>
    public abstract class GameEvent
    {
        public REALISEventType EventType { get; protected set; }
        public DateTime Timestamp { get; protected set; } = DateTime.Now;
        public Vector3 Position { get; protected set; }
    }

    /// <summary>
    /// Événement de blocage de trafic.
    /// </summary>
    public class TrafficBlockEvent : GameEvent
    {
        public Vehicle BlockedVehicle { get; }
        public Vehicle BlockingVehicle { get; }
        public float BlockedDuration { get; }

        public TrafficBlockEvent(Vehicle blockedVehicle, Vehicle blockingVehicle, float duration, Vector3 position)
        {
            EventType = REALISEventType.TrafficBlock;
            BlockedVehicle = blockedVehicle;
            BlockingVehicle = blockingVehicle;
            BlockedDuration = duration;
            Position = position;
        }
    }

    /// <summary>
    /// Événement pour les interactions d'ambiance des PNJ.
    /// </summary>
    public class AmbientInteractionEvent : GameEvent
    {
        public Ped Actor { get; }
        public AmbientInteractionType Interaction { get; }

        public AmbientInteractionEvent(Ped actor, AmbientInteractionType interaction, Vector3 position)
        {
            EventType = REALISEventType.AmbientInteraction;
            Actor = actor;
            Interaction = interaction;
            Position = position;
        }
    }

    /// <summary>
    /// Événement pour les phénomènes météorologiques.
    /// </summary>
    public class WeatherEvent : GameEvent
    {
        public WeatherEventType WeatherType { get; }
        public float Intensity { get; }
        public float Duration { get; }

        public WeatherEvent(WeatherEventType weatherType, Vector3 position, float intensity, float duration)
        {
            EventType = REALISEventType.WeatherEvent;
            WeatherType = weatherType;
            Position = position;
            Intensity = intensity;
            Duration = duration;
        }
    }

    /// <summary>
    /// Types d'événements météorologiques.
    /// </summary>
    public enum WeatherEventType
    {
        Tornado,
        Thunderstorm,
        BlizzardStorm,
        SandStorm
    }

    /// <summary>
    /// Types d'interactions d'ambiance pour les PNJ.
    /// </summary>
    public enum AmbientInteractionType
    {
        IdleScenario,
        Greeting,
        Flee,
        TakeCover,
        Cower,
        CallPolice,
        CallAmbulance,
        CallFireDept
    }

    /// <summary>
    /// Système de limitation de traitement pour éviter les surcharges.
    /// </summary>
    public static class MovementThrottler
    {
        private static DateTime _lastTrafficProcessing = DateTime.MinValue;
        private static DateTime _lastVehicleProcessing = DateTime.MinValue;
        
        private const double TRAFFIC_THROTTLE_MS = 1000; // 1 seconde
        private const double VEHICLE_THROTTLE_MS = 500;  // 0.5 seconde

        public static bool CanProcessTraffic()
        {
            var now = DateTime.Now;
            if ((now - _lastTrafficProcessing).TotalMilliseconds < TRAFFIC_THROTTLE_MS)
                return false;

            _lastTrafficProcessing = now;
            return true;
        }

        public static bool CanProcessVehicle()
        {
            var now = DateTime.Now;
            if ((now - _lastVehicleProcessing).TotalMilliseconds < VEHICLE_THROTTLE_MS)
                return false;

            _lastVehicleProcessing = now;
            return true;
        }
    }

    /// <summary>
    /// Service de requête de véhicules avec gestion de contrôle.
    /// </summary>
    public static class VehicleQueryService
    {
        private static readonly HashSet<int> _controlledVehicles = new();
        private static readonly object _controlLock = new object();

        public static Vehicle[] GetNearbyVehicles(Vector3 position, float radius)
        {
            try
            {
                return World.GetNearbyVehicles(position, radius)
                    .Where(v => v != null && v.Exists() && !v.IsDead)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<Vehicle>();
            }
        }

        public static bool TryAcquireControl(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
                return false;

            lock (_controlLock)
            {
                if (_controlledVehicles.Contains(vehicle.Handle))
                    return false;

                _controlledVehicles.Add(vehicle.Handle);
                return true;
            }
        }

        public static void ReleaseControl(Vehicle vehicle)
        {
            if (vehicle == null)
                return;

            lock (_controlLock)
            {
                _controlledVehicles.Remove(vehicle.Handle);
            }
        }

        public static void Cleanup()
        {
            lock (_controlLock)
            {
                var toRemove = _controlledVehicles.Where(handle =>
                {
                    var vehicle = World.GetAllVehicles().FirstOrDefault(v => v.Handle == handle);
                    return vehicle == null || !vehicle.Exists();
                }).ToList();

                foreach (var handle in toRemove)
                {
                    _controlledVehicles.Remove(handle);
                }
            }
        }
    }
} 