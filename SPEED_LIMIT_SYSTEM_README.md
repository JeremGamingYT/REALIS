# Système de Limites de Vitesse REALIS

## Vue d'ensemble

Le système de limites de vitesse REALIS ajoute un contrôle réaliste de la vitesse sur les routes de Los Santos. Le système surveille la vitesse du joueur en temps réel et déclenche des réponses de la police en cas d'excès de vitesse.

## Fonctionnalités

### 🚗 Affichage HUD
- **Vitesse actuelle** : Affichée en km/h avec un code couleur
- **Limite de vitesse** : Limite actuelle de la zone
- **Indicateur d'excès** : Avertissement visuel "EXCÈS!" en cas de dépassement

### 🗺️ Zones de Vitesse
Le système définit plusieurs zones avec des limites de vitesse spécifiques :

| Zone | Limite | Description |
|------|--------|-------------|
| Centre-ville | 30 km/h | Zone urbaine dense |
| Vinewood | 40 km/h | Zone résidentielle |
| Rockford Hills | 35 km/h | Zone résidentielle haut de gamme |
| Autoroutes | 80 km/h | Routes principales |
| Aéroport | 25 km/h | Zone sécurisée |
| Zone industrielle | 45 km/h | Secteur industriel |
| Paleto Bay | 60 km/h | Zone rurale |
| Sandy Shores | 55 km/h | Ville du désert |
| **Défaut** | 50 km/h | Routes secondaires |

### 🚔 Système de Réponse Policière

#### Seuils de Déclenchement
- **Avertissement** : +5 km/h au-dessus de la limite
- **Intervention police** : +15 km/h au-dessus de la limite

#### Niveaux de Gravité
1. **Violation Mineure** (15-25 km/h au-dessus)
   - 1 patrouille de police
   - Niveau de recherche +1
   - Spawn à 200m

2. **Violation Modérée** (25-40 km/h au-dessus)
   - 1-2 patrouilles
   - Sirènes activées
   - Spawn à 150m
   - Conduite plus agressive

3. **Violation Grave** (+40 km/h au-dessus)
   - 2-4 patrouilles
   - Niveau de recherche +2
   - Spawn à 100m
   - Réponse immédiate

### 🎯 Code Couleur de Vitesse
- **🟢 Vert** : Vitesse respectée
- **🟡 Jaune** : Léger dépassement (0-5 km/h)
- **🟠 Orange** : Dépassement modéré (5-15 km/h)
- **🔴 Rouge** : Excès grave (+15 km/h)

## Mécanique du Système

### Détection Intelligente
- Vérification toutes les secondes
- Système de violations consécutives
- Probabilité d'intervention basée sur les récidives
- Cooldown de 30 secondes entre les interventions

### Réalisme
- Position géographique détermine la limite
- Différents types de routes = différentes limites
- Réponse policière graduée selon la gravité
- Comportement de conduite adapté à la gravité

### Intégration
- Automatiquement chargé avec REALIS
- Compatible avec le système de police existant
- Utilise le logger REALIS pour le debugging
- Gestion des erreurs robuste

## Configuration

Le système est activé par défaut. Les limites de vitesse et les zones peuvent être modifiées dans le code source (`SpeedLimitSystem.cs`).

### Personnalisation des Zones
```csharp
// Exemple d'ajout d'une nouvelle zone
{ "NouvelleZone", new SpeedZone(
    new Vector3(x1, y1, z1),  // Coin min
    new Vector3(x2, y2, z2),  // Coin max
    35f,                      // Limite en km/h
    "Nom de la zone") }
```

### Ajustement des Seuils
```csharp
private const float WARNING_THRESHOLD = 5f;  // Seuil d'avertissement
private const float POLICE_THRESHOLD = 15f;  // Seuil d'intervention
private const int POLICE_COOLDOWN_MS = 30000; // Cooldown police
```

## Compatibilité

- ✅ Compatible avec tous les véhicules
- ✅ Fonctionne uniquement quand le joueur conduit
- ✅ Intégré au système de wanted level de GTA V
- ✅ Utilise les mécaniques de conduite IA existantes

## Débogage

Le système utilise le logger REALIS. Pour activer les logs détaillés :
1. Activer le mode debug dans REALIS
2. Consulter les logs pour les erreurs de vitesse
3. Vérifier les spawns de police et les calculs de zone

## Notes Techniques

- Conversion automatique m/s → km/h
- Calcul de position 3D pour les zones
- Gestion des erreurs pour éviter les crashes
- Nettoyage automatique des ressources
- Performance optimisée (vérifications par seconde)

---

*Développé pour REALIS - Système de réalisme GTA V* 