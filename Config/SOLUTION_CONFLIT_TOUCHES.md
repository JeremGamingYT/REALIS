# 📻 Solution au conflit de touches Police Dispatch vs TeslaX

## Problème identifié

Le système **Police Dispatch** et le système **TeslaX** utilisaient tous les deux la touche **N**, créant un conflit :

- **TeslaX** : Touche N pour navigation vers waypoint
- **Police Dispatch** : Touche N pour ouvrir le menu dispatch

## Solution implémentée

### 1. Nouvelle touche configurable pour Police Dispatch

La touche du dispatch de police est maintenant **configurable** et définie par défaut sur **R** (Radio) au lieu de **N**.

### 2. Configuration

Dans `Config/UserConfig.json` :

```json
{
  "keybinds": {
    "policeDispatchKey": {
      "key": "R",
      "requiresModifier": false,
      "modifierKey": "None",
      "description": "Open Police Dispatch Radio",
      "enabled": true
    }
  }
}
```

### 3. Utilisation

- **Police Dispatch Radio** : Appuyez sur **R** (Radio - configurable)
- **TeslaX Navigation** : Appuyez sur **N** (comme avant)
- **TeslaX Autopilot** : Appuyez sur **J** (comme avant)

## Pourquoi la touche R ?

✅ **R = Radio** → Logique et mémorable pour la radio police  
✅ **Touche libre** → Non utilisée par GTA V ou d'autres systèmes du mod  
✅ **Proche de WASD** → Facile d'accès pendant le jeu  
✅ **Pas de conflit** → Évite les problèmes avec le menu pause (P) et TeslaX (N)

## Comment utiliser le Police Dispatch

### 1. Activation du service de police

1. Allez à un poste de police (blip bleu sur la carte)
2. Entrez dans le bâtiment
3. Utilisez le menu pour prendre votre service
4. Une fois en service, vous recevrez un message de bienvenue

### 2. Utilisation de la radio dispatch

- **Touche R** : Ouvrir/fermer la radio police
- Répondez aux appels d'urgence qui apparaissent automatiquement
- Choisissez votre réponse dans le menu radio

### 3. Mode Debug (pour diagnostics)

Si la radio ne fonctionne pas :

1. Activez le mode debug : `"debugMode": true` dans `UserConfig.json`
2. **Touche L** : Force l'activation du dispatch pour test
3. Observez les informations de debug à l'écran

## Messages d'aide

### Si vous n'êtes pas en service :
```
[RADIO POLICE] Vous devez être en service de police pour utiliser la radio dispatch
(Touche L pour test en mode debug)
```

### Si vous utilisez encore la touche N dans un TeslaX :
```
[DISPATCH] Touche N utilisée par TeslaX
Changez la touche dispatch vers R dans les paramètres
```

## Personnalisation

Vous pouvez changer la touche de la radio en modifiant la valeur `"key"` dans la configuration :

```json
"policeDispatchKey": {
  "key": "U",  // Exemple : utiliser U au lieu de R
  "requiresModifier": false,
  "modifierKey": "None",
  "description": "Open Police Dispatch Radio",
  "enabled": true
}
```

## Touches libres disponibles

Si vous voulez changer la touche R, voici les touches alphabétiques libres :
- **A, B, C, D, G, I, K, U, V, Y, Z**

**Évitez ces touches** (déjà utilisées) :
- **E** : Interaction  
- **F** : Sortir véhicule  
- **H** : Klaxon  
- **J** : TeslaX Autopilot  
- **L** : Debug dispatch  
- **M** : Menus services  
- **N** : TeslaX Navigation  
- **O** : Portes bus  
- **P** : **MENU PAUSE GTA**  
- **Q, W, S** : Contrôles train  
- **T** : Téléphone  
- **X** : Détacher wagons  

## Résolution des problèmes

### La radio dispatch n'apparaît pas :

1. Vérifiez que vous êtes en service de police
2. Activez le mode debug pour voir l'état du système
3. Utilisez la touche L (en mode debug) pour forcer l'activation
4. Vérifiez que la touche n'est pas en conflit avec d'autres mods

### Conflit avec d'autres mods :

Si un autre mod utilise la touche R, changez-la dans la configuration vers une touche libre (ex: U, V, etc.).

## Support

En cas de problème persistant :

1. Activez `"debugMode": true`
2. Observez les messages de debug
3. Vérifiez les logs pour identifier le problème
4. Testez avec la touche L en mode debug

---

✅ **Conflit résolu** : TeslaX utilise N, Police Radio utilise R par défaut  
📻 **R = Radio** : Logique, mémorable et libre de conflit ! 