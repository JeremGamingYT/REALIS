using GTA;
using GTA.Math;
using System.Collections.Generic;
using System.Linq;
using System; // For ArgumentNullException

namespace REALIS.Police.Callouts
{
    /// <summary>
    /// Représente un événement (callout) similaire à LSPDFR.
    /// </summary>
    public abstract class CalloutBase
    {
        /// <summary>Nom affiché pour le callout.</summary>
        public string Name { get; }

        /// <summary>Description succincte.</summary>
        public string Description { get; }

        /// <summary>Position de départ (où le joueur est appelé).</summary>
        public Vector3 StartPosition { get; }

        /// <summary>True lorsque l'événement est actif.</summary>
        public bool IsActive { get; private set; }

        protected List<CalloutObjective> Objectives { get; private set; }
        private DateTime _nextObjectiveDisplayTime;
        private readonly TimeSpan _objectiveDisplayInterval = TimeSpan.FromSeconds(5); // How often to show objectives if not all complete

        protected CalloutBase(string name, string description, Vector3 startPosition)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            StartPosition = startPosition;
            Objectives = new List<CalloutObjective>();
            _nextObjectiveDisplayTime = DateTime.MinValue;
        }

        protected void AddObjective(string id, string description, bool isOptional = false)
        {
            if (Objectives.Any(o => o.Id == id)) return; // Prevent duplicate IDs
            var objective = new CalloutObjective(id, description, isOptional);
            Objectives.Add(objective);
            GTA.UI.Notification.Show($"~b~Nouvel objectif:~w~ {description}"); // Notify on add
        }

        protected void UpdateObjectiveStatus(string id, CalloutObjectiveStatus status, bool showNotification = true)
        {
            var objective = Objectives.FirstOrDefault(o => o.Id == id);
            objective?.UpdateStatus(status, showNotification);
        }

        protected CalloutObjective GetObjective(string id)
        {
            return Objectives.FirstOrDefault(o => o.Id == id);
        }

        protected bool AreAllMandatoryObjectivesCompleted()
        {
            return Objectives.Where(o => !o.IsOptional).All(o => o.Status == CalloutObjectiveStatus.Completed);
        }

        protected bool HasAnyMandatoryObjectiveFailed()
        {
            return Objectives.Where(o => !o.IsOptional).Any(o => o.Status == CalloutObjectiveStatus.Failed);
        }

        protected virtual void DisplayObjectivesProgress()
        {
            if (DateTime.Now < _nextObjectiveDisplayTime && Objectives.All(o => o.Status == CalloutObjectiveStatus.Completed || o.Status == CalloutObjectiveStatus.Failed))
                 return; // All objectives are finalized, no need to spam. Or if interval not met.

            if (Objectives.Any(o => o.Status == CalloutObjectiveStatus.InProgress)) // Only display if there's something in progress
            {
                 string objectivesText = "Objectifs Actuels:\n";
                 foreach (var obj in Objectives.Where(o => o.Status == CalloutObjectiveStatus.InProgress || o.Status == CalloutObjectiveStatus.Failed)) // Show in progress or failed
                 {
                     objectivesText += obj.ToString() + "\n";
                 }
                 if(objectivesText != "Objectifs Actuels:\n") // Check if any objectives were actually added to the string
                 {
                    // Using ShowHelpTextThisFrame might be too spammy. Consider a less intrusive way or timed notification.
                    // For now, let's use a standard notification if it's time.
                    if (DateTime.Now >= _nextObjectiveDisplayTime)
                    {
                        GTA.UI.Notification.Show(objectivesText.TrimEnd('\n'), true); // `true` to blink for important info
                        _nextObjectiveDisplayTime = DateTime.Now + _objectiveDisplayInterval;
                    }
                 }
            }
        }


        /// <summary>
        /// Détermine si le callout peut être généré dans le monde actuel.
        /// </summary>
        public virtual bool CanSpawn() => true;

        /// <summary>Lance l'événement.</summary>
        public void Start()
        {
            if (IsActive) return;
            IsActive = true;
            Objectives.Clear(); // Clear objectives from any previous run (if instance is reused, though CalloutManager creates new ones)
            OnStart();
        }

        /// <summary>Mise à jour par Tick.</summary>
        public void Update()
        {
            if (!IsActive) return;
            OnUpdate();
            DisplayObjectivesProgress(); // Display objectives during the update
        }

        /// <summary>Termine et nettoie le callout.</summary>
        public void End()
        {
            if (!IsActive) return;
            IsActive = false; // Set inactive before OnEnd to prevent recursion if OnEnd calls End()
            OnEnd();
        }

        protected abstract void OnStart();
        protected abstract void OnUpdate();
        protected abstract void OnEnd();
    }
} 