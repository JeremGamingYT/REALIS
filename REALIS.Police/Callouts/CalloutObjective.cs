using System;
using GTA; // For UI.Notification if used directly, or for general GTA types

namespace REALIS.Police.Callouts
{
    public enum CalloutObjectiveStatus
    {
        InProgress,
        Completed,
        Failed,
        Optional // Optional objectives might not affect main success/failure but could give bonuses
    }

    public class CalloutObjective
    {
        public string Description { get; private set; }
        public CalloutObjectiveStatus Status { get; private set; }
        public bool IsOptional { get; private set; }
        public string Id { get; private set; } // Unique ID for managing this objective

        private bool _notificationShownForCompletion = false;

        public CalloutObjective(string id, string description, bool isOptional = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            IsOptional = isOptional;
            Status = CalloutObjectiveStatus.InProgress;
        }

        public void UpdateStatus(CalloutObjectiveStatus newStatus, bool showNotification = true)
        {
            if (Status == newStatus) return;

            Status = newStatus;
            if (showNotification && !_notificationShownForCompletion)
            {
                string statusText = "";
                switch (Status)
                {
                    case CalloutObjectiveStatus.Completed:
                        statusText = "~g~Terminé";
                        _notificationShownForCompletion = true; // Only show completion once
                        break;
                    case CalloutObjectiveStatus.Failed:
                        statusText = "~r~Échoué";
                        _notificationShownForCompletion = true; // Also show failure once
                        break;
                    case CalloutObjectiveStatus.InProgress:
                         // Typically don't notify for "InProgress" unless it's a new objective being set.
                        statusText = "~b~En cours";
                        break;
                }
                if (!string.IsNullOrEmpty(statusText)) {
                    GTA.UI.Notification.Show($"Objectif: {Description} - {statusText}");
                }
            }
        }
         public override string ToString()
        {
            string statusMarker = "";
            switch (Status)
            {
                case CalloutObjectiveStatus.InProgress: statusMarker = "[ ]"; break;
                case CalloutObjectiveStatus.Completed: statusMarker = "[X]"; break;
                case CalloutObjectiveStatus.Failed: statusMarker = "[!]"; break;
            }
            return $"{statusMarker} {Description}{(IsOptional ? " (Optionnel)" : "")}";
        }
    }
}
