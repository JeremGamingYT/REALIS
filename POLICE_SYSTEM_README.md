# Système de Police Personnalisé REALIS

## Description

Ce système de police personnalisé pour GTA V modifie le comportement par défaut de la police selon vos spécifications :

1. **Comportement moins agressif** - Les policiers ne tirent plus automatiquement, seulement si vous les menacez avec une arme
2. **Arrestation automatique** - Si vous vous arrêtez pendant 5 secondes avec des étoiles de police, vous êtes automatiquement arrêté
3. **Transport au poste** - Un policier vous emmène automatiquement au poste de police le plus proche

## Fonctionnalités

### 1. Contrôle de l'agressivité de la police

- Les policiers n'attaquent que si vous :
  - Visez directement un policier avec une arme
  - Tenez une arme à moins de 10 mètres d'un policier
- Sinon, ils gardent leur position sans tirer

### 2. Système d'arrestation automatique

- Quand vous avez des étoiles de police et que vous vous arrêtez (vitesse < 2 km/h) :
  - Compte à rebours de 5 secondes affiché à l'écran
  - À zéro, vos étoiles disparaissent automatiquement
  - Vous êtes téléporté dans la voiture de police la plus proche (siège arrière)

### 3. Transport au poste de police

- Un officier de police NPC apparaît et conduit la voiture
- Il vous emmène automatiquement au poste de police le plus proche
- Une fois arrivé, vous êtes libéré à l'entrée du poste

## Configuration

Le système utilise un fichier de configuration JSON : `scripts/REALIS_PoliceConfig.json`

### Paramètres disponibles :

```json
{
  "arrest_delay_seconds": 5,              // Temps d'arrêt avant arrestation
  "stop_threshold": 2.0,                  // Vitesse max pour être considéré arrêté
  "enable_custom_police": true,           // Activer/désactiver le système
  "enable_police_aggression_control": true, // Contrôle de l'agressivité
  "enable_auto_arrest": true,             // Arrestation automatique
  "enable_police_transport": true,        // Transport au poste
  "police_vehicle_search_radius": 100.0,  // Rayon de recherche véhicule police
  "weapon_threat_distance": 10.0         // Distance de menace avec arme
}
```

### Postes de police personnalisés :

Le fichier de configuration inclut les coordonnées de tous les postes de police de GTA V :

- Mission Row Police Station
- Sandy Shores Sheriff
- Paleto Bay Sheriff  
- Rockford Hills Police
- La Mesa Police
- Vespucci Police

### Messages personnalisables :

Vous pouvez modifier tous les messages affichés dans le jeu :

```json
"warning_messages": {
  "initial_warning": "~r~Arrêtez-vous ou vous serez arrêté! {0} secondes...",
  "countdown_warning": "~r~Arrestation dans {0} secondes...",
  "arrest_message": "~g~Vous êtes en état d'arrestation!",
  "transport_message": "~b~Transport vers le poste de police...",
  "release_message": "~g~Vous avez été relâché du poste de police.",
  "arrest_cancelled": "~r~L'arrestation a été annulée."
}
```

## Installation

1. Assurez-vous que ScriptHookV et ScriptHookVDotNet3 sont installés
2. Compilez le projet REALIS
3. Placez le fichier `REALIS.dll` dans le dossier `scripts` de GTA V
4. Le fichier de configuration sera créé automatiquement au premier lancement

## Utilisation

### Scénario typique :

1. Commettez un délit pour obtenir des étoiles de police
2. Les policiers vous poursuivent mais ne tirent pas (sauf si vous les menacez)
3. Arrêtez-vous complètement pendant 5 secondes
4. Le compte à rebours s'affiche
5. Vous êtes automatiquement arrêté et téléporté dans une voiture de police
6. Un policier NPC vous conduit au poste le plus proche
7. Vous êtes libéré à l'entrée du poste

### Annulation d'arrestation :

L'arrestation peut être annulée si :
- Le véhicule de police est détruit
- L'officier de police est tué
- Une erreur technique survient

## Personnalisation avancée

### Modifier les temps d'arrestation :
Changez `arrest_delay_seconds` dans le fichier de configuration

### Ajouter des postes de police :
Ajoutez des coordonnées dans `custom_police_stations`

### Changer la sensibilité d'arrêt :
Modifiez `stop_threshold` (vitesse en m/s)

### Personnaliser les messages :
Modifiez la section `warning_messages`
- Utilisez `{0}` pour insérer des valeurs dynamiques (comme le temps restant)
- Utilisez les codes couleur GTA V (~r~ rouge, ~g~ vert, ~b~ bleu, etc.)

## Support technique

Le système inclut :
- Gestion d'erreurs complète
- Logs détaillés via le système Logger REALIS
- Nettoyage automatique des ressources
- Compatibilité avec les autres modules REALIS

## Codes couleur GTA V

- `~r~` Rouge
- `~g~` Vert  
- `~b~` Bleu
- `~y~` Jaune
- `~p~` Violet
- `~o~` Orange
- `~c~` Gris
- `~m~` Rose/Magenta
- `~u~` Noir
- `~n~` Nouvelle ligne
- `~s~` Blanc (par défaut)

## Limitations connues

- Le système fonctionne uniquement quand vous êtes à pied ou en voiture
- Les poursuites en hélicoptère ou bateau peuvent ne pas être affectées
- Certains événements de mission peuvent interférer avec le système
- La téléportation peut parfois placer le joueur dans une position non optimale 