using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using REALIS.Common;

namespace REALIS.Job
{
    /// <summary>
    /// Permet de déposer les suspects menottés à un poste de police.
    /// Lorsque le joueur, dans un véhicule de police contenant un suspect, entre dans une zone dédiée,
    /// un fade-out / fade-in est déclenché et le suspect est retiré du véhicule.
    /// </summary>
    public class PoliceJailModule : IModule
    {
        private class JailPoint
        {
            public Vector3 Position;
            public float Radius;
        }

        // Très simple : un point pour Mission Row. D'autres pourront être ajoutés.
        private readonly List<JailPoint> _jailPoints = new List<JailPoint>
        {
            new JailPoint { Position = new Vector3(468.86f, -1015.93f, 26.39f), Radius = 5f }, // Mission Row (garage extérieur)
            new JailPoint { Position = new Vector3(1856.1f, 3688.2f, 34.26f), Radius = 5f },  // Sandy Shores
            new JailPoint { Position = new Vector3(-448.3f, 6013.3f, 31.72f), Radius = 5f }   // Paleto Bay
        };

        private const int FadeDurationMs = 600;

        private Blip _targetBlip;

        public void Initialize() { /* nothing */ }

        public void Update()
        {
            if (!DutyState.PoliceOnDuty) return;

            var player = Game.Player.Character;
            if (!player.Exists() || player.IsDead) return;

            Vehicle veh = player.CurrentVehicle;

            // Si le joueur est à pied, on ignore les vérifs véhicule
            bool processingOnFoot = !player.IsInVehicle();

            if (!processingOnFoot)
            {
                if (veh == null || !veh.Exists()) return;

                // Vérifie que le véhicule est un véhicule d'urgence pour éviter les abus
                if (veh.ClassType != VehicleClass.Emergency) return;
            }

            // Vérifie s'il y a un suspect menotté à l'intérieur
            Ped suspect = null;

            if (veh != null && veh.Exists())
            {
                foreach (VehicleSeat seat in new[] { VehicleSeat.RightRear, VehicleSeat.LeftRear })
                {
                    Ped occ = veh.GetPedOnSeat(seat);
                    if (occ != null && occ.Exists() && PoliceArrestShared.CuffedPeds.Contains(occ.Handle))
                    {
                        suspect = occ;
                        break;
                    }
                }
            }

            // Si aucun suspect dans le véhicule, on regarde si un suspect menotté est escorté ou proche du joueur
            if (suspect == null)
            {
                // 1) Le joueur escorte-t-il un ped ?
                if (PoliceArrestShared.EscortedPedHandle != -1)
                {
                    Ped esc = Entity.FromHandle(PoliceArrestShared.EscortedPedHandle) as Ped;
                    if (esc != null && esc.Exists())
                    {
                        suspect = esc;
                    }
                }

                // 2) Sinon, cherche un ped menotté proche (<4 m)
                if (suspect == null)
                {
                    foreach (int handle in PoliceArrestShared.CuffedPeds)
                    {
                        Ped ped = Entity.FromHandle(handle) as Ped;
                        if (ped == null || !ped.Exists()) continue;

                        if (ped.Position.DistanceTo(player.Position) < 4f)
                        {
                            suspect = ped;
                            break;
                        }
                    }
                }

                // Toujours aucun suspect -> nettoyage des blips éventuels puis sortie
                if (suspect == null)
                {
                    if (_targetBlip != null && _targetBlip.Exists())
                    {
                        _targetBlip.Delete();
                        _targetBlip = null;
                    }
                    return;
                }
            }

            // Créer un blip de destination si absent
            if (_targetBlip == null || !_targetBlip.Exists())
            {
                var jailPos = _jailPoints[0].Position; // Mission Row par défaut
                _targetBlip = World.CreateBlip(jailPos);
                if (_targetBlip != null)
                {
                    _targetBlip.Sprite = BlipSprite.Standard;
                    _targetBlip.Color = BlipColor.Blue;
                    _targetBlip.Name = "Remise en cellule";
                    _targetBlip.IsShortRange = false;
                    _targetBlip.ShowRoute = true;
                }
            }

            // Détection de la proximité d'un point de jail
            foreach (var jp in _jailPoints)
            {
                if (player.Position.DistanceTo(jp.Position) <= jp.Radius)
                {
                    GTA.UI.Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Livrer le suspect");

                    if (Game.IsControlJustPressed(GTA.Control.Context))
                    {
                        StartJailSequence(veh, suspect);
                    }
                    break;
                }
            }
        }

        public void Dispose() { }

        private void StartJailSequence(Vehicle veh, Ped suspect)
        {
            try
            {
                // Empêche déclenchement multiple
                if (!suspect.Exists()) return;

                // Fade out immédiatement
                Function.Call(Hash.DO_SCREEN_FADE_OUT, FadeDurationMs);

                // Planifie la suite après le fade
                GameScheduler.Schedule(() =>
                {
                    // Retirer le suspect
                    try { if (suspect.Exists()) suspect.Delete(); } catch { }

                    // Fermer les portes du véhicule si pertinent
                    if (veh != null && veh.Exists())
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            try { Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, veh.Handle, i, true); } catch { }
                        }
                    }

                    // Fade in
                    Function.Call(Hash.DO_SCREEN_FADE_IN, FadeDurationMs);

                    GTA.UI.Notification.Show("~b~Suspect placé en cellule.");

                    // Supprimer le blip objectif si plus de suspects à bord
                    GameScheduler.Schedule(() =>
                    {
                        if (_targetBlip != null && _targetBlip.Exists())
                        {
                            _targetBlip.Delete();
                            _targetBlip = null;
                        }
                    }, 200);
                }, FadeDurationMs + 200);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PoliceJailModule StartJailSequence error: {ex.Message}");
            }
        }
    }
} 