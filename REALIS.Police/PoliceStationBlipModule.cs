using System.Collections.Generic;
using GTA;
using GTA.Math;
using REALIS.Common;

namespace REALIS.Job
{
    /// <summary>
    /// Module responsable de l'ajout de Blips sur la carte pour les postes de police accessibles en solo.
    /// Étape 1 du futur système LSPDFR-like : simplement indiquer les commissariats où le joueur peut commencer le métier.
    /// </summary>
    public class PoliceStationBlipModule : IModule
    {
        private readonly List<Blip> _createdBlips = new List<Blip>();

        /// Référence aux positions des commissariats définies dans <see cref="PoliceStations"/>.
        /// </summary>
        private static readonly IReadOnlyList<Vector3> StationLocations = PoliceStations.Locations;

        public void Initialize()
        {
            CreateBlips();
        }

        public void Update()
        {
            // Rien à mettre à jour pour l'instant – tout est statique.
        }

        public void Dispose()
        {
            CleanupBlips();
        }

        private void CreateBlips()
        {
            CleanupBlips(); // Sécurité en cas de reload

            foreach (var pos in StationLocations)
            {
                var blip = World.CreateBlip(pos);
                if (blip == null || !blip.Exists())
                    continue;

                blip.Sprite = BlipSprite.PoliceStation; // Icône commissariat
                blip.Color = BlipColor.Blue;
                blip.IsShortRange = true; // Visible uniquement à proximité sur la mini-map
                blip.Scale = 0.9f;

                // Nom affiché sur la carte (supporte la localisation du jeu)
                blip.Name = "~b~Poste de police";

                _createdBlips.Add(blip);
            }
        }

        private void CleanupBlips()
        {
            foreach (var blip in _createdBlips)
            {
                try { if (blip != null && blip.Exists()) blip.Delete(); }
                catch { /* Ignorer les erreurs, par exemple si le jeu supprime déjà le blip */ }
            }
            _createdBlips.Clear();
        }
    }
} 