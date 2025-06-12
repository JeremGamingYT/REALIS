using System;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.UI;
using GTA.Native;
using REALIS.Core;

namespace REALIS.Core
{
    /// <summary>
    /// Système permettant de gérer une invasion extraterrestre
    /// </summary>
    public class UFOSystem : Script
    {
        // Configuration générale
        private bool _isInvasionActive = false;
        private readonly Keys _activationKey = Keys.F5;
        private readonly string _ufoModel = "p_spinning_anus_s";
        private readonly string[] _alienModels = { "s_m_m_movalien_01", "u_m_y_rsranger_01", "s_m_y_clown_01" };
        private readonly float _rotationSpeed = 1.0f;
        private readonly float _lightIntensity = 10f;
        private readonly Color _lightColor = Color.FromArgb(180, 90, 255);
        private readonly int _maxUFOs = 5;
        private readonly int _maxAliensPerUFO = 3;
        private readonly int _spawnRadius = 150;
        private readonly int _alienSpawnDelay = 8000; // milliseconds
        private readonly WeaponHash _alienWeapon = WeaponHash.Railgun;
        
        // Points de vie des vaisseaux
        private readonly int _mothershipMaxHealth = 1000;
        private readonly int _ufoMaxHealth = 300;
        private Dictionary<Prop, int> _ufoHealths = new Dictionary<Prop, int>();
        private bool _invasionDefeated = false;
        
        // Vaisseaux et aliens
        private Prop? _mothership = null;
        private readonly List<Prop> _ufos = new List<Prop>();
        private readonly List<Ped> _aliens = new List<Ped>();
        private readonly List<Blip> _alienBlips = new List<Blip>();
        private readonly List<Blip> _ufoBlips = new List<Blip>();
        private Blip? _invasionZoneBlip = null;
        
        // Chronomètres pour les événements
        private DateTime _lastAlienSpawn;
        private readonly Random _random = new Random();
        
        public UFOSystem()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;

            Logger.Info("UFO Invasion System initialized.");
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                if (_isInvasionActive)
                {
                    // Mise à jour des OVNI
                    UpdateUFOs();
                    
                    // Vérifier si l'invasion a été vaincue
                    if (CheckIfInvasionDefeated())
                    {
                        EndInvasion();
                        Notification.PostTicker("~g~Invasion extraterrestre repoussée! Victoire!", false, true);
                        return;
                    }
                    
                    // Apparition des aliens
                    if ((DateTime.Now - _lastAlienSpawn).TotalMilliseconds > _alienSpawnDelay)
                    {
                        SpawnAliens();
                        _lastAlienSpawn = DateTime.Now;
                    }
                    
                    // Mise à jour du comportement des aliens
                    UpdateAliens();
                    
                    // Nettoyage des aliens morts
                    CleanupDeadAliens();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in UFO invasion system tick: {ex.Message}");
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == _activationKey)
            {
                ToggleInvasion();
            }
        }

        private void ToggleInvasion()
        {
            if (_isInvasionActive)
            {
                EndInvasion();
            }
            else
            {
                StartInvasion();
            }
        }

        private void StartInvasion()
        {
            try
            {
                // Nettoyer toute invasion précédente
                EndInvasion();
                
                // Choisir un point central pour l'invasion
                Vector3 invasionCenter = ChooseInvasionCenter();
                
                // Créer la zone d'invasion sur la carte
                CreateInvasionZone(invasionCenter);
                
                // Créer le vaisseau mère
                SpawnMothership(invasionCenter);
                
                // Créer les vaisseaux plus petits
                SpawnSmallUFOs(invasionCenter);
                
                // Initialiser le chronomètre pour l'apparition des aliens
                _lastAlienSpawn = DateTime.Now;
                
                // Activer l'invasion
                _isInvasionActive = true;
                
                // Notification
                Notification.PostTicker("~r~ALERTE: Invasion extraterrestre en cours!", false, true);
                
                // Effets spéciaux
                Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, "THUNDER");
                
                // Journal
                Logger.Info($"UFO invasion started at {invasionCenter}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting UFO invasion: {ex.Message}");
                Notification.PostTicker("~r~REALIS: Erreur lors du démarrage de l'invasion!", false, true);
            }
        }

        private Vector3 ChooseInvasionCenter()
        {
            // Si le joueur est dans la ville, on choisit un point à proximité
            // Sinon, on choisit un point aléatoire sur la carte
            var playerPos = Game.Player.Character.Position;
            
            if (IsInCityArea(playerPos))
            {
                // Point aléatoire autour du joueur mais pas trop près
                float distance = _random.Next(300, 600);
                float angle = (float)_random.NextDouble() * 360f;
                float x = playerPos.X + distance * (float)Math.Cos(angle * Math.PI / 180);
                float y = playerPos.Y + distance * (float)Math.Sin(angle * Math.PI / 180);
                float z = playerPos.Z + 100f; // En hauteur
                
                return new Vector3(x, y, z);
            }
            else
            {
                // Points notables sur la carte où une invasion pourrait être intéressante
                Vector3[] notableLocations = new Vector3[]
                {
                    new Vector3(-74.94f, -818.63f, 326.16f),   // Maze Bank Building
                    new Vector3(1688.73f, 3280.03f, 50.0f),    // Sandy Shores
                    new Vector3(-2052.69f, 3116.85f, 60.0f),   // Fort Zancudo
                    new Vector3(342.17f, 4827.01f, 50.0f),     // Grapeseed
                    new Vector3(-1044.19f, -2746.08f, 50.0f)   // Aéroport de Los Santos
                };
                
                // Choisir un point aléatoire parmi les points notables
                Vector3 baseLocation = notableLocations[_random.Next(notableLocations.Length)];
                
                // Ajouter un peu d'aléatoire
                float offsetX = (_random.Next(-200, 200));
                float offsetY = (_random.Next(-200, 200));
                float height = baseLocation.Z + 100f;
                
                return new Vector3(baseLocation.X + offsetX, baseLocation.Y + offsetY, height);
            }
        }

        private bool IsInCityArea(Vector3 position)
        {
            // Coordonnées approximatives du centre-ville de Los Santos
            Vector3 cityCenter = new Vector3(0f, -500f, 0f);
            float cityRadius = 2000f;
            
            // Distance 2D (ignore la hauteur)
            float distance = new Vector2(position.X - cityCenter.X, position.Y - cityCenter.Y).Length();
            
            return distance < cityRadius;
        }

        private void CreateInvasionZone(Vector3 center)
        {
            // Créer un marqueur de zone sur la carte
            _invasionZoneBlip = World.CreateBlip(new Vector2(center.X, center.Y), 200f);
            
            if (_invasionZoneBlip != null)
            {
                _invasionZoneBlip.Color = BlipColor.Red;
                _invasionZoneBlip.Alpha = 128;
                _invasionZoneBlip.Name = "Zone d'invasion extraterrestre";
                _invasionZoneBlip.IsFlashing = true;
            }
        }

        private void SpawnMothership(Vector3 center)
        {
            try
            {
                // Créer le modèle
                var model = new Model(_ufoModel);
                
                // Charger le modèle
                if (!model.IsValid || !model.Request(10000))
                {
                    Logger.Error($"Mothership model '{_ufoModel}' could not be loaded.");
                    return;
                }
                
                // Position légèrement au-dessus du centre de l'invasion
                var spawnPos = new Vector3(center.X, center.Y, center.Z + 50f);
                
                // Créer le vaisseau mère
                _mothership = World.CreateProp(model, spawnPos, Vector3.Zero, false, false);
                
                if (_mothership == null)
                {
                    Logger.Error("Failed to create mothership.");
                    return;
                }
                
                // Configurer le vaisseau mère
                _mothership.IsPositionFrozen = true;
                _mothership.IsCollisionEnabled = true; // Activer la collision pour que les balles le touchent
                _mothership.IsBulletProof = false; // S'assurer qu'il n'est pas résistant aux balles
                _mothership.IsExplosionProof = false; // Permettre de l'endommager avec des explosifs
                _mothership.IsFireProof = false; // Permettre de l'endommager avec le feu
                
                // Initialiser la santé du vaisseau mère
                _ufoHealths[_mothership] = _mothershipMaxHealth;
                
                // Rendre le vaisseau mère un peu plus grand
                Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, _mothership.Handle, true, true);
                Function.Call(Hash.FORCE_ENTITY_AI_AND_ANIMATION_UPDATE, _mothership.Handle);
                Function.Call(Hash.SET_ENTITY_RENDER_SCORCHED, _mothership.Handle, true);
                
                // Ajouter un blip sur la carte
                var blip = _mothership.AddBlip();
                if (blip != null)
                {
                    blip.Sprite = (BlipSprite)58; // Utiliser un sprite proche d'un UFO
                    blip.Color = BlipColor.Red;
                    blip.Name = "Vaisseau mère extraterrestre";
                    blip.Scale = 1.5f;
                    blip.IsFlashing = true;
                    _ufoBlips.Add(blip);
                }
                
                // Libérer le modèle
                model.MarkAsNoLongerNeeded();
                
                Logger.Info("Mothership spawned.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning mothership: {ex.Message}");
            }
        }

        private void SpawnSmallUFOs(Vector3 center)
        {
            try
            {
                // Créer le modèle
                var model = new Model(_ufoModel);
                
                // Charger le modèle
                if (!model.IsValid || !model.Request(10000))
                {
                    Logger.Error($"UFO model '{_ufoModel}' could not be loaded.");
                    return;
                }
                
                // Créer plusieurs petits vaisseaux autour du vaisseau mère
                for (int i = 0; i < _maxUFOs; i++)
                {
                    // Position aléatoire autour du centre
                    float angle = i * (360f / _maxUFOs);
                    float distance = _random.Next(100, 400);
                    float height = _random.Next(30, 100);
                    
                    float x = center.X + distance * (float)Math.Cos(angle * Math.PI / 180);
                    float y = center.Y + distance * (float)Math.Sin(angle * Math.PI / 180);
                    float z = center.Z - 20f + height;
                    
                    var spawnPos = new Vector3(x, y, z);
                    
                    // Créer l'OVNI
                    var ufo = World.CreateProp(model, spawnPos, Vector3.Zero, false, false);
                    
                    if (ufo != null)
                    {
                        ufo.IsPositionFrozen = true;
                        ufo.IsCollisionEnabled = true; // Activer la collision pour que les balles le touchent
                        ufo.IsBulletProof = false; // S'assurer qu'il n'est pas résistant aux balles
                        ufo.IsExplosionProof = false; // Permettre de l'endommager avec des explosifs
                        ufo.IsFireProof = false; // Permettre de l'endommager avec le feu
                        _ufos.Add(ufo);
                        _ufoHealths[ufo] = _ufoMaxHealth;
                        
                        // Ajouter un blip sur la carte
                        var blip = ufo.AddBlip();
                        if (blip != null)
                        {
                            blip.Sprite = (BlipSprite)58; // Utiliser un sprite proche d'un UFO
                            blip.Color = BlipColor.Yellow;
                            blip.Name = "OVNI";
                            _ufoBlips.Add(blip);
                        }
                    }
                }
                
                // Libérer le modèle
                model.MarkAsNoLongerNeeded();
                
                Logger.Info($"{_ufos.Count} small UFOs spawned.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning small UFOs: {ex.Message}");
            }
        }

        private void SpawnAliens()
        {
            try
            {
                // Si tous les vaisseaux sont détruits, ne pas spawner d'aliens
                if ((_mothership == null || !_mothership.Exists() || !_ufoHealths.ContainsKey(_mothership) || _ufoHealths[_mothership] <= 0) &&
                    _ufos.Count == 0)
                {
                    return;
                }
                
                // Sélectionner un UFO aléatoire ou le vaisseau mère
                Prop? selectedUFO = null;
                
                if (_random.Next(10) < 3 && _mothership != null && _mothership.Exists() && _ufoHealths.ContainsKey(_mothership) && _ufoHealths[_mothership] > 0) // 30% de chance de spawn depuis le vaisseau mère
                {
                    selectedUFO = _mothership;
                }
                else if (_ufos.Count > 0)
                {
                    // Sélectionner uniquement un UFO qui n'est pas détruit
                    var availableUFOs = new List<Prop>();
                    foreach (var ufo in _ufos)
                    {
                        if (ufo != null && ufo.Exists() && _ufoHealths.ContainsKey(ufo) && _ufoHealths[ufo] > 0)
                        {
                            availableUFOs.Add(ufo);
                        }
                    }
                    
                    if (availableUFOs.Count > 0)
                    {
                        selectedUFO = availableUFOs[_random.Next(availableUFOs.Count)];
                    }
                }
                else if (_mothership != null && _mothership.Exists() && _ufoHealths.ContainsKey(_mothership) && _ufoHealths[_mothership] > 0)
                {
                    selectedUFO = _mothership;
                }
                else
                {
                    return; // Pas d'OVNI disponible
                }
                
                if (selectedUFO == null || !selectedUFO.Exists())
                    return;
                
                // Nombre d'aliens à spawner
                int alienCount = _random.Next(1, _maxAliensPerUFO + 1);
                
                // Position de base de l'OVNI
                Vector3 ufoPos = selectedUFO.Position;
                
                // Créer les aliens
                for (int i = 0; i < alienCount; i++)
                {
                    // Choisir un modèle d'alien aléatoire
                    string alienModel = _alienModels[_random.Next(_alienModels.Length)];
                    var model = new Model(alienModel);
                    
                    if (!model.IsValid || !model.Request(10000))
                    {
                        Logger.Error($"Alien model '{alienModel}' could not be loaded.");
                        continue;
                    }
                    
                    // Position aléatoire au sol sous l'OVNI
                    float angle = (float)_random.NextDouble() * 360f;
                    float distance = _random.Next(5, _spawnRadius);
                    float x = ufoPos.X + distance * (float)Math.Cos(angle * Math.PI / 180);
                    float y = ufoPos.Y + distance * (float)Math.Sin(angle * Math.PI / 180);
                    
                    // Trouver la hauteur du sol (méthode non obsolète)
                    float groundZ = 0f;
                    Vector3 position = new Vector3(x, y, 1000f);
                    bool foundGround = World.GetGroundHeight(position, out groundZ);
                    
                    if (!foundGround || groundZ == 0f)
                    {
                        // Utiliser une position par défaut si on ne trouve pas le sol
                        groundZ = ufoPos.Z - 50f;
                    }
                    
                    // Position finale
                    Vector3 spawnPos = new Vector3(x, y, groundZ + 1.0f);
                    
                    // Créer l'alien
                    var alien = World.CreatePed(model, spawnPos);
                    
                    if (alien != null)
                    {
                        // Configurer l'alien
                        alien.MaxHealth = 300;
                        alien.Health = 300;
                        alien.Armor = 100;
                        alien.Money = _random.Next(50, 200);
                        alien.IsFireProof = true;
                        alien.BlockPermanentEvents = true;
                        
                        // Donner une arme
                        alien.Weapons.Give(_alienWeapon, 999, true, true);
                        
                        // Ajouter un blip
                        var blip = alien.AddBlip();
                        if (blip != null)
                        {
                            blip.Sprite = BlipSprite.Enemy;
                            blip.Color = BlipColor.Red;
                            blip.Name = "Alien";
                            _alienBlips.Add(blip);
                        }
                        
                        // Rendre hostile envers tout le monde
                        Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, alien.Handle, Function.Call<int>(Hash.GET_HASH_KEY, "HATES_PLAYER"));
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, alien.Handle, 46, true);
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, alien.Handle, 5, true);
                        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, alien.Handle, 2, true);
                        
                        // Déclencher l'attaque (méthode non obsolète)
                        alien.Task.CombatHatedTargetsAroundPed(_spawnRadius);
                        
                        // Ajouter à la liste
                        _aliens.Add(alien);
                    }
                    
                    // Libérer le modèle
                    model.MarkAsNoLongerNeeded();
                }
                
                Logger.Info($"{alienCount} aliens spawned from UFO at {ufoPos}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning aliens: {ex.Message}");
            }
        }

        private void UpdateUFOs()
        {
            // Vérifier si le joueur est en train de tirer
            bool playerIsShooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, Game.Player.Character.Handle);
            Vector3 playerPosition = Game.Player.Character.Position;
            Vector3 aimingDirection = Game.Player.Character.ForwardVector;
            
            // Mettre à jour le vaisseau mère
            if (_mothership != null && _mothership.Exists())
            {
                // Vérifier si le vaisseau mère a été touché par un tir
                if (_ufoHealths.ContainsKey(_mothership))
                {
                    bool wasHit = false;
                    
                    // Méthode 1: Vérifier si l'entité a été endommagée par le joueur
                    if (Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, _mothership.Handle, Game.Player.Character.Handle, 1))
                    {
                        wasHit = true;
                        Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, _mothership.Handle);
                    }
                    
                    // Méthode 2: Si le joueur tire et vise dans la direction du vaisseau
                    if (playerIsShooting && !wasHit)
                    {
                        Vector3 toUFO = _mothership.Position - playerPosition;
                        float distance = toUFO.Length();
                        
                        // Normaliser le vecteur
                        toUFO = toUFO / distance;
                        
                        // Vérifier si le joueur vise à peu près vers l'OVNI (produit scalaire)
                        float dotProduct = Vector3.Dot(aimingDirection, toUFO);
                        
                        // Si le joueur vise dans la direction du vaisseau (cos < 0.7 est environ 45 degrés)
                        if (dotProduct > 0.7f && distance < 100f)
                        {
                            // Créer un rayon depuis la position du joueur dans la direction où il vise
                            RaycastResult raycast = World.Raycast(
                                playerPosition, 
                                playerPosition + (aimingDirection * 1000f),
                                IntersectFlags.Everything
                            );
                            
                            // Si le rayon touche l'OVNI ou se termine près de lui
                            if (raycast.HitEntity == _mothership || 
                                Vector3.Distance(raycast.HitPosition, _mothership.Position) < 5.0f)
                            {
                                wasHit = true;
                                // Utiliser la méthode directe pour appliquer les dégâts
                                ShootDirectlyAtUFO(_mothership, 50);
                                return; // Sortir de la méthode après avoir touché le vaisseau mère
                            }
                        }
                    }
                    
                    // Si le vaisseau a été touché, réduire sa santé
                    if (wasHit)
                    {
                        // Réduire les points de vie (valeur fixe basée sur l'arme)
                        int damage = 50; // Dommage par défaut
                        _ufoHealths[_mothership] -= damage;
                        
                        // Jouer un son de hit
                        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Explosion_01", "GTAO_FM_Events_Soundset", 0);
                        
                        // Message de notification pour les dégâts
                        Notification.PostTicker($"~y~Dégâts au vaisseau mère: -{damage} PV", false, false);
                        
                        // Si le vaisseau mère est détruit
                        if (_ufoHealths[_mothership] <= 0)
                        {
                            // Effet d'explosion
                            CreateExplosionEffect(_mothership.Position, 10.0f);
                            
                            // Supprimer le vaisseau mère
                            _mothership.Delete();
                            _mothership = null;
                            
                            Notification.PostTicker("~g~Le vaisseau mère a été détruit!", false, true);
                            return;
                        }
                    }
                    
                    // Rotation lente
                    var rotation = _mothership.Rotation;
                    rotation = new Vector3(rotation.X, rotation.Y, rotation.Z + _rotationSpeed * 0.5f);
                    _mothership.Rotation = rotation;
                    
                    // Effet de lumière
                    CreateUFOLightEffect(_mothership, 3.0f, Color.Red);
                    
                    // Toujours afficher la barre de vie si le joueur est à proximité ou s'il vise l'OVNI
                    if (World.GetDistance(Game.Player.Character.Position, _mothership.Position) < 200f || Game.Player.TargetedEntity == _mothership)
                    {
                        ShowUFOHealthBar(_mothership, _ufoHealths[_mothership], _mothershipMaxHealth);
                    }
                }
            }
            
            // Mettre à jour les petits vaisseaux
            foreach (var ufo in _ufos.ToArray())
            {
                if (ufo != null && ufo.Exists())
                {
                    // Vérifier si l'OVNI a été touché par un tir
                    if (_ufoHealths.ContainsKey(ufo))
                    {
                        bool wasHit = false;
                        
                        // Méthode 1: Vérifier si l'entité a été endommagée par le joueur
                        if (Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, ufo.Handle, Game.Player.Character.Handle, 1))
                        {
                            wasHit = true;
                            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, ufo.Handle);
                        }
                        
                        // Méthode 2: Si le joueur tire et vise dans la direction du vaisseau
                        if (playerIsShooting && !wasHit)
                        {
                            Vector3 toUFO = ufo.Position - playerPosition;
                            float distance = toUFO.Length();
                            
                            // Normaliser le vecteur
                            toUFO = toUFO / distance;
                            
                            // Vérifier si le joueur vise à peu près vers l'OVNI (produit scalaire)
                            float dotProduct = Vector3.Dot(aimingDirection, toUFO);
                            
                            // Si le joueur vise dans la direction du vaisseau (cos < 0.7 est environ 45 degrés)
                            if (dotProduct > 0.7f && distance < 100f)
                            {
                                // Créer un rayon depuis la position du joueur dans la direction où il vise
                                RaycastResult raycast = World.Raycast(
                                    playerPosition, 
                                    playerPosition + (aimingDirection * 1000f),
                                    IntersectFlags.Everything
                                );
                                
                                // Si le rayon touche l'OVNI ou se termine près de lui
                                if (raycast.HitEntity == ufo || 
                                    Vector3.Distance(raycast.HitPosition, ufo.Position) < 5.0f)
                                {
                                    wasHit = true;
                                    // Utiliser la méthode directe pour appliquer les dégâts
                                    ShootDirectlyAtUFO(ufo, 100);
                                    return; // Sortir de la méthode après avoir touché l'OVNI
                                }
                            }
                        }
                        
                        // Si le vaisseau a été touché, réduire sa santé
                        if (wasHit)
                        {
                            // Réduire les points de vie (valeur fixe)
                            int damage = 100; // Dommage par défaut pour les petits OVNI
                            _ufoHealths[ufo] -= damage;
                            
                            // Jouer un son de hit
                            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Explosion_01", "GTAO_FM_Events_Soundset", 0);
                            
                            // Message de notification pour les dégâts
                            Notification.PostTicker($"~y~Dégâts à l'OVNI: -{damage} PV", false, false);
                            
                            // Si l'OVNI est détruit
                            if (_ufoHealths[ufo] <= 0)
                            {
                                // Effet d'explosion
                                CreateExplosionEffect(ufo.Position, 5.0f);
                                
                                // Supprimer le blip
                                foreach (var blip in _ufoBlips.ToArray())
                                {
                                    if (blip != null && blip.Exists() && blip.Entity == ufo)
                                    {
                                        blip.Delete();
                                        _ufoBlips.Remove(blip);
                                        break;
                                    }
                                }
                                
                                // Supprimer l'OVNI
                                ufo.Delete();
                                _ufos.Remove(ufo);
                                _ufoHealths.Remove(ufo);
                                
                                Notification.PostTicker("~g~Un OVNI a été détruit!", false, true);
                                continue;
                            }
                        }
                        
                        // Rotation plus rapide pour les petits vaisseaux
                        var rotation = ufo.Rotation;
                        rotation = new Vector3(rotation.X, rotation.Y, rotation.Z + _rotationSpeed);
                        ufo.Rotation = rotation;
                        
                        // Léger mouvement aléatoire
                        if (_random.Next(100) < 5) // 5% de chance de se déplacer
                        {
                            var position = ufo.Position;
                            position = new Vector3(
                                position.X + (_random.Next(-5, 6) * 0.5f),
                                position.Y + (_random.Next(-5, 6) * 0.5f),
                                position.Z + (_random.Next(-2, 3) * 0.5f)
                            );
                            ufo.Position = position;
                        }
                        
                        // Effet de lumière
                        CreateUFOLightEffect(ufo, 1.0f, _lightColor);
                        
                        // Toujours afficher la barre de vie si le joueur est à proximité ou s'il vise l'OVNI
                        if (World.GetDistance(Game.Player.Character.Position, ufo.Position) < 150f || Game.Player.TargetedEntity == ufo)
                        {
                            ShowUFOHealthBar(ufo, _ufoHealths[ufo], _ufoMaxHealth);
                        }
                    }
                }
            }
        }

        private bool CheckIfInvasionDefeated()
        {
            if (_invasionDefeated)
                return true;
                
            // Vérifier si le vaisseau mère est détruit
            if (_mothership == null || !_mothership.Exists() || !_ufoHealths.ContainsKey(_mothership) || _ufoHealths[_mothership] <= 0)
            {
                // Vérifier si tous les petits OVNI sont détruits
                bool allSmallUFOsDestroyed = true;
                foreach (var ufo in _ufos.ToArray())
                {
                    if (ufo != null && ufo.Exists() && _ufoHealths.ContainsKey(ufo) && _ufoHealths[ufo] > 0)
                    {
                        allSmallUFOsDestroyed = false;
                        break;
                    }
                }
                
                // Si le vaisseau mère et tous les petits OVNI sont détruits, l'invasion est vaincue
                if (allSmallUFOsDestroyed)
                {
                    _invasionDefeated = true;
                    return true;
                }
            }
            
            return false;
        }

        private void ShowUFOHealthBar(Prop ufo, int currentHealth, int maxHealth)
        {
            if (ufo == null || !ufo.Exists()) return;
            
            // Position au-dessus de l'OVNI
            Vector3 position = ufo.Position;
            position.Z += 5.0f;
            
            // Calculer le pourcentage de santé
            float healthPercent = (float)currentHealth / maxHealth;
            
            // Limiter à 0-1
            healthPercent = Math.Max(0, Math.Min(1, healthPercent));
            
            // Couleur en fonction de la santé (vert->jaune->rouge)
            Color barColor;
            if (healthPercent > 0.6f)
                barColor = Color.Green;
            else if (healthPercent > 0.3f)
                barColor = Color.Yellow;
            else
                barColor = Color.Red;
            
            // Largeur de la barre
            float barWidth = 5.0f;
            float barHeight = 0.5f;
            
            // Position de départ de la barre
            Vector3 startPos = position - new Vector3(barWidth / 2, 0, 0);
            
            // Dessiner le fond de la barre (noir semi-transparent)
            Function.Call(Hash.DRAW_RECT, startPos.X + barWidth / 2, startPos.Y, barWidth, barHeight, 0, 0, 0, 150);
            
            // Dessiner la barre de vie
            Function.Call(Hash.DRAW_RECT, startPos.X + (barWidth * healthPercent) / 2, startPos.Y, barWidth * healthPercent, barHeight, barColor.R, barColor.G, barColor.B, 200);
            
            // Dessiner le texte des PV
            Vector3 textPos = startPos + new Vector3(0, 0, barHeight + 0.1f);
            String healthText = $"{currentHealth}/{maxHealth} PV";
            Function.Call(Hash.SET_TEXT_SCALE, 0.3f, 0.3f);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
            Function.Call(Hash.SET_TEXT_CENTRE, true);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 2, 0, 0, 0, 255);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, healthText);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, textPos.X, textPos.Y);
            
            // Si l'OVNI est visé par le joueur, afficher une barre de vie plus grande dans l'UI
            if (Game.Player.TargetedEntity == ufo)
            {
                // Dessiner une barre de vie en haut de l'écran
                float screenWidth = 0.3f; // 30% de la largeur de l'écran
                float screenHeight = 0.03f;
                float screenX = 0.5f; // Centre de l'écran
                float screenY = 0.1f; // En haut
                
                // Fond de la barre
                Function.Call(Hash.DRAW_RECT, screenX, screenY, screenWidth, screenHeight, 0, 0, 0, 200);
                
                // Barre de vie
                Function.Call(Hash.DRAW_RECT, screenX - screenWidth / 2 + screenWidth * healthPercent / 2, screenY, 
                    screenWidth * healthPercent, screenHeight, barColor.R, barColor.G, barColor.B, 200);
                
                // Texte
                String targetText = ufo == _mothership ? "VAISSEAU MÈRE" : "OVNI";
                Function.Call(Hash.SET_TEXT_SCALE, 0.5f, 0.5f);
                Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
                Function.Call(Hash.SET_TEXT_CENTRE, true);
                Function.Call(Hash.SET_TEXT_DROPSHADOW, 2, 0, 0, 0, 255);
                Function.Call(Hash.SET_TEXT_OUTLINE);
                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"{targetText}: {currentHealth}/{maxHealth}");
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, screenX, screenY - 0.03f);
            }
        }
        
        private void CreateExplosionEffect(Vector3 position, float radius)
        {
            // Créer une explosion sans dommages aux joueurs
            World.AddExplosion(position, ExplosionType.Rocket, radius, 1.0f, null, true, false);
            
            // Ajouter plusieurs petites explosions autour
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = new Vector3(
                    _random.Next(-10, 11) * 0.5f,
                    _random.Next(-10, 11) * 0.5f,
                    _random.Next(-10, 11) * 0.5f
                );
                
                World.AddExplosion(position + offset, ExplosionType.Rocket, radius * 0.6f, 0.5f, null, true, false);
                
                // Attendre un peu pour rendre l'effet plus réaliste
                Script.Wait(100);
            }
        }

        private void CreateUFOLightEffect(Prop ufo, float intensity, Color color)
        {
            if (ufo == null || !ufo.Exists()) return;

            // Créer un effet de lumière sous l'OVNI
            var lightPos = ufo.Position;
            lightPos.Z -= 2.0f;
            
            // Direction vers le bas
            var lightDir = new Vector3(0, 0, -1);
            
            // Dessiner un spot de lumière projetant une ombre
            World.DrawSpotLightWithShadow(
                lightPos, 
                lightDir, 
                color, 
                50.0f,                // distance
                _lightIntensity * intensity,  // luminosité
                0.5f,                 // arrondi
                10.0f * intensity,    // rayon
                40.0f                 // atténuation
            );
            
            // Occasionnellement, créer un éclair de lumière forte
            if (_random.Next(100) < 2) // 2% de chance
            {
                World.ForceLightningFlash();
            }
        }

        private void UpdateAliens()
        {
            foreach (var alien in _aliens.ToArray())
            {
                if (alien != null && alien.Exists() && alien.IsAlive)
                {
                    // Si l'alien n'est pas en train de combattre, on lui donne une nouvelle cible
                    if (!alien.IsInCombat)
                    {
                        // Vérifier si l'alien est inactif
                        if (_random.Next(100) < 10) // 10% de chance de renouveler le comportement
                        {
                            // Rechercher des PNJ à proximité
                            Ped[] nearbyPeds = World.GetNearbyPeds(alien.Position, _spawnRadius);
                            
                            if (nearbyPeds.Length > 0)
                            {
                                // Filtrer pour ne pas cibler d'autres aliens
                                var targets = new List<Ped>();
                                foreach (var ped in nearbyPeds)
                                {
                                    if (ped != null && ped.Exists() && !_aliens.Contains(ped) && ped != Game.Player.Character)
                                    {
                                        targets.Add(ped);
                                    }
                                }
                                
                                if (targets.Count > 0)
                                {
                                    // Choisir une cible aléatoire
                                    Ped target = targets[_random.Next(targets.Count)];
                                    alien.Task.Combat(target); // Méthode non obsolète
                                }
                                else
                                {
                                    // Aucune cible, se déplacer aléatoirement
                                    Vector3 randomPos = alien.Position + new Vector3(
                                        _random.Next(-20, 21),
                                        _random.Next(-20, 21),
                                        0
                                    );
                                    alien.Task.RunTo(randomPos);
                                }
                            }
                            else
                            {
                                // Aucun PNJ à proximité, se déplacer aléatoirement
                                alien.Task.CombatHatedTargetsAroundPed(_spawnRadius); // Méthode non obsolète
                            }
                        }
                    }
                }
            }
        }

        private void CleanupDeadAliens()
        {
            for (int i = _aliens.Count - 1; i >= 0; i--)
            {
                var alien = _aliens[i];
                
                if (alien == null || !alien.Exists() || !alien.IsAlive)
                {
                    // Supprimer le blip
                    if (i < _alienBlips.Count && _alienBlips[i] != null)
                    {
                        var blip = _alienBlips[i];
                        if (blip != null && blip.Exists())
                        {
                            blip.Delete();
                        }
                        _alienBlips.RemoveAt(i);
                    }
                    
                    // Supprimer l'alien
                    if (alien != null && alien.Exists())
                    {
                        alien.MarkAsNoLongerNeeded();
                    }
                    
                    _aliens.RemoveAt(i);
                }
            }
        }

        private void EndInvasion()
        {
            try
            {
                // Supprimer le vaisseau mère
                if (_mothership != null && _mothership.Exists())
                {
                    _mothership.Delete();
                    _mothership = null;
                }
                
                // Supprimer les petits vaisseaux
                foreach (var ufo in _ufos)
                {
                    if (ufo != null && ufo.Exists())
                    {
                        ufo.Delete();
                    }
                }
                _ufos.Clear();
                
                // Supprimer les aliens
                foreach (var alien in _aliens)
                {
                    if (alien != null && alien.Exists())
                    {
                        alien.Delete();
                    }
                }
                _aliens.Clear();
                
                // Supprimer les blips
                foreach (var blip in _alienBlips)
                {
                    if (blip != null && blip.Exists())
                    {
                        blip.Delete();
                    }
                }
                _alienBlips.Clear();
                
                foreach (var blip in _ufoBlips)
                {
                    if (blip != null && blip.Exists())
                    {
                        blip.Delete();
                    }
                }
                _ufoBlips.Clear();
                
                // Supprimer le blip de zone
                if (_invasionZoneBlip != null && _invasionZoneBlip.Exists())
                {
                    _invasionZoneBlip.Delete();
                    _invasionZoneBlip = null;
                }
                
                // Réinitialiser l'état
                _isInvasionActive = false;
                
                // Restaurer la météo
                Function.Call(Hash.CLEAR_WEATHER_TYPE_PERSIST);
                
                // Notification
                Notification.PostTicker("~g~REALIS: Invasion extraterrestre terminée!", false, true);
                
                Logger.Info("UFO invasion ended.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ending UFO invasion: {ex.Message}");
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            EndInvasion();
            Logger.Info("UFO Invasion System unloaded.");
        }

        // Méthode pour tirer directement sur un vaisseau
        private void ShootDirectlyAtUFO(Prop ufo, int damage)
        {
            // S'assurer que le vaisseau existe
            if (ufo == null || !ufo.Exists() || !_ufoHealths.ContainsKey(ufo))
                return;
                
            // Réduire les points de vie
            _ufoHealths[ufo] -= damage;
            
            // Jouer un son d'impact
            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Explosion_01", "GTAO_FM_Events_Soundset", 0);
            
            // Créer un effet visuel à l'impact
            Vector3 playerPos = Game.Player.Character.Position;
            Vector3 direction = (ufo.Position - playerPos).Normalized;
            Vector3 impactPoint = ufo.Position - (direction * 1.0f); // Légèrement décalé du centre
            
            // Créer une petite explosion sans dégâts
            World.AddExplosion(impactPoint, ExplosionType.Bullet, 0.5f, 0.1f, null, true, false);
            
            // Notification des dégâts
            string ufoType = (ufo == _mothership) ? "vaisseau mère" : "OVNI";
            Notification.PostTicker($"~y~Dégâts au {ufoType}: -{damage} PV", false, false);
            
            // Afficher la barre de vie
            int maxHealth = (ufo == _mothership) ? _mothershipMaxHealth : _ufoMaxHealth;
            ShowUFOHealthBar(ufo, _ufoHealths[ufo], maxHealth);
            
            // Vérifier si le vaisseau est détruit
            if (_ufoHealths[ufo] <= 0)
            {
                // Effet d'explosion
                float explosionSize = (ufo == _mothership) ? 10.0f : 5.0f;
                CreateExplosionEffect(ufo.Position, explosionSize);
                
                // Supprimer le blip
                foreach (var blip in _ufoBlips.ToArray())
                {
                    if (blip != null && blip.Exists() && blip.Entity == ufo)
                    {
                        blip.Delete();
                        _ufoBlips.Remove(blip);
                        break;
                    }
                }
                
                // Notification
                Notification.PostTicker($"~g~{(ufo == _mothership ? "Le vaisseau mère" : "Un OVNI")} a été détruit!", false, true);
                
                // Supprimer le vaisseau
                if (ufo == _mothership)
                {
                    _mothership.Delete();
                    _mothership = null;
                }
                else
                {
                    ufo.Delete();
                    _ufos.Remove(ufo);
                    _ufoHealths.Remove(ufo);
                }
            }
        }
    }
}