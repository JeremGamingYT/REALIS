# REALIS - Système de Configuration

## Vue d'ensemble

Le mod REALIS dispose d'un système de configuration avancé qui permet aux utilisateurs de personnaliser entièrement leur expérience. Le système comprend :

1. **Menu de première utilisation élégant** - Configuration initiale interactive
2. **Gestion multi-langues** - Support de 7 langues
3. **Configuration des touches personnalisable** - Touches configurables pour toutes les fonctions
4. **Paramètres persistants** - Sauvegarde automatique des configurations

## Emplacement des Fichiers

Tous les fichiers de configuration sont automatiquement créés dans :
```
<GTA V Directory>/scripts/REALIS/
```

### Fichiers Générés :

- **UserConfig.json** - Configuration utilisateur principale
- **Languages.json** - Traductions et langues supportées
- **Keybinds.json** - Configuration des touches
- **Logs/** - Dossier des logs (si activé)

## Première Utilisation

### Menu de Configuration Initial

Au premier lancement du mod, un menu élégant s'affiche automatiquement avec les étapes suivantes :

1. **Écran de Bienvenue**
   - Présentation du mod
   - Option pour continuer ou ignorer la configuration

2. **Sélection de la Langue**
   - Choix parmi 7 langues supportées
   - Interface traduite en temps réel

3. **Configuration des Touches**
   - Attribution personnalisée des touches
   - Vérification des conflits
   - Aperçu en temps réel

4. **Paramètres Généraux**
   - Activation/désactivation des systèmes
   - Options de notification
   - Mode debug

5. **Finalisation**
   - Résumé des paramètres
   - Sauvegarde automatique

### Navigation du Menu

- **↑/↓ ou W/S** : Naviguer dans les options
- **Enter/Espace** : Sélectionner/Valider
- **Escape** : Retour/Annuler
- **←/→ ou A/D** : Ajuster les valeurs

## Configuration Détaillée

### Paramètres Utilisateur (UserConfig.json)

```json
{
  "version": "1.0.0",
  "isFirstRunCompleted": true,
  "language": "fr",
  "enableNotifications": true,
  "enableAudioFeedback": true,
  "debugMode": false,
  "autoSaveInterval": 30,
  "modSettings": {
    "policeSystemEnabled": true,
    "trafficAIEnabled": true,
    "gasStationSystemEnabled": true,
    "foodStoreSystemEnabled": true,
    "realisticTrafficDensity": 1.0,
    "policeResponseLevel": 3,
    "economicDifficulty": 2
  }
}
```

### Configuration des Touches (Keybinds.json)

```json
{
  "openMenu": {
    "key": "F9",
    "requiresModifier": false,
    "description": "Open Configuration Menu",
    "enabled": true
  }
}
```

### Touches Par Défaut

| Action | Touche | Description |
|--------|--------|-------------|
| F9 | Menu Configuration | Ouvre le menu de configuration |
| F10 | Toggle Police | Active/Désactive le système de police |
| F11 | Toggle Traffic | Active/Désactive l'IA de trafic |
| F12 | Services d'Urgence | Appel des services d'urgence |
| F5 | Sauvegarde Rapide | Sauvegarde la configuration |
| F8 | Mode Debug | Active/Désactive le debug |
| E | Interaction | Touche d'interaction principale |

## Langues Supportées

Le mod supporte actuellement 7 langues avec traductions complètes :

- **English** (en) - Langue par défaut
- **Français** (fr) - Traduction complète
- **Español** (es) - Traduction complète
- **Deutsch** (de) - Traduction complète
- **Italiano** (it) - En développement
- **Português** (pt) - En développement
- **Русский** (ru) - En développement

## Fonctionnalités Avancées

### Détection de Première Utilisation

Le système détecte automatiquement si c'est la première utilisation en vérifiant l'existence du fichier `UserConfig.json`. Si ce fichier n'existe pas, le menu de configuration s'affiche automatiquement.

### Sauvegarde Automatique

- Configuration sauvegardée automatiquement lors des changements
- Sauvegarde de sécurité au déchargement du mod
- Possibilité de sauvegarde manuelle avec F5

### Gestion des Erreurs

- Récupération automatique en cas de fichier de configuration corrompu
- Création de configurations par défaut si nécessaire
- Logs détaillés pour le débogage

### Isolation des Scripts

Le système de configuration est conçu pour éviter les conflits entre différents modules du mod :

- Gestionnaire d'événements centralisé
- Isolation des configurations par module
- Prévention des interférences entre scripts

## Personnalisation Avancée

### Modification Manuelle des Fichiers

Les utilisateurs avancés peuvent modifier directement les fichiers JSON :

1. Arrêter GTA V
2. Modifier les fichiers de configuration
3. Vérifier la syntaxe JSON
4. Relancer le jeu

### Ajout de Nouvelles Langues

Pour ajouter une nouvelle langue :

1. Modifier `Languages.json`
2. Ajouter le code de langue dans `supportedLanguages`
3. Ajouter toutes les traductions dans `translations`
4. Redémarrer le mod

### Configuration des Touches Personnalisées

Les touches peuvent utiliser des modificateurs :

```json
{
  "customAction": {
    "key": "G",
    "requiresModifier": true,
    "modifierKey": "LControlKey",
    "description": "Ctrl+G pour action personnalisée"
  }
}
```

## Résolution de Problèmes

### Menu de Configuration Ne S'Affiche Pas

1. Vérifier que `UserConfig.json` n'existe pas dans le dossier de configuration
2. Supprimer le fichier pour forcer la première utilisation
3. Vérifier les logs pour les erreurs

### Configuration Perdue

1. Vérifier l'intégrité des fichiers JSON
2. Restaurer depuis une sauvegarde si disponible
3. Supprimer les fichiers corrompus pour recréer les défauts

### Touches Ne Fonctionnent Pas

1. Vérifier la configuration dans `Keybinds.json`
2. S'assurer qu'il n'y a pas de conflits avec d'autres mods
3. Réinitialiser les touches par défaut

## Support Technique

Pour obtenir de l'aide avec le système de configuration :

1. Consulter les logs dans le dossier REALIS
2. Vérifier la syntaxe des fichiers JSON
3. Réinitialiser la configuration si nécessaire

## Changelog

### Version 1.0.0
- Menu de première utilisation
- Système multi-langues
- Configuration des touches
- Sauvegarde automatique
- Gestion d'erreurs robuste 