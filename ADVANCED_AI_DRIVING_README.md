# Système d'IA de Conduite Avancé - REALIS

## 📋 Vue d'ensemble

Le système d'IA de conduite avancé améliore considérablement le comportement des NPCs au volant dans GTA V. Il se compose de plusieurs modules qui travaillent ensemble pour créer une expérience de conduite plus réaliste et intelligente.

## 🎯 Améliorations Principales

### ✅ Détection d'Obstacles Avancée
- **Raycast multi-directionnel** pour détecter les obstacles, murs et véhicules
- **Prédiction de collision** jusqu'à 3 secondes à l'avance
- **Mémorisation des obstacles** pour éviter les zones problématiques
- **Détection du terrain** et adaptation à la pente

### 🚗 Dépassement Intelligent
- **Analyse de sécurité** des voies adjacentes avant dépassement
- **Respect des feux rouges** - aucun dépassement aux intersections
- **Calcul des distances** de sécurité pour les manœuvres
- **Évaluation des bénéfices** du dépassement (gain de vitesse)

### 🚦 Gestion des Feux Rouges
- **Détection automatique** des intersections et feux de circulation
- **Arrêt obligatoire** aux feux rouges avec calcul de distance de freinage
- **Décisions intelligentes** aux feux orange (s'arrêter ou continuer selon la vitesse/distance)
- **Règles de priorité** aux intersections sans feux

### 🧠 Navigation Intelligente
- **Évitement prédictif** des collisions
- **Optimisation de route** en temps réel
- **Adaptation aux piétons** avec ralentissement et klaxon poli
- **Changements de voie** stratégiques pour améliorer le flux

## 🏗️ Architecture du Système

### 1. **AdvancedDrivingAI.cs**
Module principal qui coordonne toutes les améliorations de conduite :
- Analyse contextuelle de la conduite
- Application des améliorations en temps réel
- Gestion des cooldowns et performances

### 2. **SmartNavigationSystem.cs**
Système de navigation prédictive :
- Prédiction des collisions potentielles
- Détection avancée des piétons
- Optimisation des routes
- Gestion de la mémoire des obstacles

### 3. **TrafficLightManager.cs**
Gestionnaire spécialisé pour les feux et intersections :
- Simulation du cycle des feux de circulation
- Application des règles de priorité
- Gestion des arrêts et redémarrages

### 4. **AIConfig.cs**
Système de configuration flexible :
- Paramètres ajustables en temps réel
- Sauvegarde/chargement automatique
- Réglages de performance

## ⚙️ Configuration

### Fichier de Configuration : `REALIS_AIConfig.json`

Le système génère automatiquement un fichier de configuration dans le dossier `scripts/` avec les paramètres suivants :

```json
{
  "detectionSettings": {
    "obstacleDetectionRange": 25.0,
    "vehicleScanRadius": 60.0,
    "pedestrianDetectionRange": 12.0,
    "collisionPredictionTime": 3.0,
    "emergencyBrakeThreshold": 8.0
  },
  "drivingBehavior": {
    "maxCitySpeed": 50.0,
    "maxHighwaySpeed": 80.0,
    "normalFollowingDistance": 6.0,
    "enhancedFollowingDistance": 10.0,
    "intersectionSpeed": 15.0,
    "enableAdvancedSteering": true,
    "respectSpeedLimits": true
  },
  "trafficLightSettings": {
    "redLightBrakeDistance": 25.0,
    "strictRedLightCompliance": true,
    "enableTrafficLightDetection": true,
    "intersectionPriorityRules": true
  },
  "overtakingSettings": {
    "enableOvertaking": true,
    "minOvertakeSpeed": 12.0,
    "overtakeDetectionRange": 40.0,
    "safeOvertakeClearance": 8.0,
    "overtakeOnlyInSafeLanes": true
  },
  "performanceSettings": {
    "maxEnhancedVehicles": 12,
    "maxNavigationUpdates": 8,
    "enhancementCooldown": 8.0,
    "enablePerformanceLogging": false
  }
}
```

## 🔧 Personnalisation

### Ajuster la Performance
```json
"performanceSettings": {
  "maxEnhancedVehicles": 8,        // Réduire pour améliorer les FPS
  "maxNavigationUpdates": 6,       // Moins de véhicules traités par tick
  "enhancementCooldown": 10.0      // Augmenter pour réduire la charge CPU
}
```

### Modifier le Comportement de Conduite
```json
"drivingBehavior": {
  "maxCitySpeed": 40.0,           // Vitesse maximum en ville
  "driverAggressiveness": 0.2,     // Moins agressif (0.0 - 1.0)
  "enableAdvancedSteering": true   // Évitement avancé activé
}
```

### Paramétrer les Dépassements
```json
"overtakingSettings": {
  "enableOvertaking": true,        // Activer/désactiver les dépassements
  "minOvertakeSpeed": 15.0,       // Vitesse minimum pour dépasser
  "safeOvertakeClearance": 10.0   // Distance de sécurité augmentée
}
```

## 🚀 Installation

1. **Prérequis** :
   - ScriptHookV
   - ScriptHookVDotNet V3
   - .NET Framework 4.8

2. **Intégration** :
   - Les fichiers sont intégrés dans le namespace `REALIS.TrafficAI`
   - Compilation avec le projet REALIS existant
   - Configuration automatique au premier lancement

3. **Activation** :
   - Le système s'active automatiquement avec REALIS
   - Aucune intervention manuelle nécessaire
   - Fonctionne en arrière-plan sans interférer avec d'autres scripts

## 📊 Fonctionnalités Détaillées

### Détection d'Obstacles
- **Raycast 360°** : Scan complet autour du véhicule
- **Prédiction temporelle** : Anticipe les collisions futures
- **Classification des obstacles** : Différencie véhicules, piétons, structures
- **Adaptation dynamique** : Ajuste le comportement selon le type d'obstacle

### Dépassement Intelligent
- **Analyse des voies** : Vérifie la sécurité des voies adjacentes
- **Calcul des trajectoires** : Optimise le chemin de dépassement
- **Respect du code de la route** : Interdit les dépassements dangereux
- **Retour en voie** : Calcule le moment optimal pour se rabattre

### Gestion des Intersections
- **Détection automatique** : Identifie les intersections par analyse du trafic
- **Simulation des feux** : Cycle réaliste rouge/orange/vert
- **Règles de priorité** : "Priorité à droite" et autres règles
- **Évitement des embouteillages** : Évite de bloquer les intersections

## 🔄 Compatibilité

### ✅ Compatible avec :
- Tous les scripts REALIS existants
- Mods de circulation tiers
- Scripts de véhicules personnalisés
- Systèmes de police avancés

### ❌ Peut interférer avec :
- Mods qui modifient directement l'IA de conduite native
- Scripts qui prennent le contrôle forcé des véhicules NPCs
- Mods de "traffic override" agressifs

## 🐛 Résolution de Problèmes

### Performance Ralentie
```json
"performanceSettings": {
  "maxEnhancedVehicles": 6,
  "maxNavigationUpdates": 4,
  "enhancementCooldown": 12.0
}
```

### NPCs Trop Agressifs
```json
"drivingBehavior": {
  "driverAggressiveness": 0.1,
  "maxCitySpeed": 35.0,
  "enhancedFollowingDistance": 12.0
}
```

### Dépassements Trop Fréquents
```json
"overtakingSettings": {
  "minOvertakeSpeed": 20.0,
  "safeOvertakeClearance": 12.0,
  "overtakeOnlyInSafeLanes": true
}
```

## 📝 Logs et Debug

Le système génère des logs détaillés pour le debugging :

```
[REALIS] AdvancedDrivingAI: Enhanced vehicle 12345 with obstacle avoidance
[REALIS] SmartNavigationSystem: Predicted collision avoided for vehicle 67890
[REALIS] TrafficLightManager: Vehicle 11111 stopped at red light
[REALIS] AI Config - Overtaking Enabled: True
```

Activer le logging détaillé :
```json
"performanceSettings": {
  "enablePerformanceLogging": true
}
```

## 🤝 Contribution

Le système est conçu pour être extensible. Pour ajouter de nouvelles fonctionnalités :

1. Créer une nouvelle classe dans `TrafficAI/`
2. Implémenter `IEventHandler` si nécessaire
3. Ajouter les paramètres de configuration dans `AIConfig.cs`
4. Mettre à jour ce README

## 📈 Roadmap Futur

- **Détection météo** : Adaptation de la conduite selon les conditions
- **IA contextuelle** : Comportement différent selon l'heure/zone
- **Apprentissage adaptatif** : L'IA s'améliore avec le temps
- **Interface utilisateur** : Menu in-game pour ajuster les paramètres
- **Statistiques** : Tableau de bord des performances IA

---

> **Note** : Ce système a été conçu pour s'intégrer parfaitement avec vos scripts existants sans aucune interférence. Tous les paramètres sont ajustables en temps réel via le fichier de configuration. 