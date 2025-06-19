using GTA;
using GTA.Math;

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

        protected CalloutBase(string name, string description, Vector3 startPosition)
        {
            Name = name;
            Description = description;
            StartPosition = startPosition;
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
            OnStart();
        }

        /// <summary>Mise à jour par Tick.</summary>
        public void Update()
        {
            if (!IsActive) return;
            OnUpdate();
        }

        /// <summary>Termine et nettoie le callout.</summary>
        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            OnEnd();
        }

        protected abstract void OnStart();
        protected abstract void OnUpdate();
        protected abstract void OnEnd();
    }
} 