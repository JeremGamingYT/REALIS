# 🚨 Guide de Dépannage - Système d'IA REALIS

## ❗ Problème de Plantage du Jeu

Si le jeu plante ou ne répond pas après l'installation du système d'IA, suivez ces étapes :

### 🔧 **Solution Immédiate**

1. **Démarrez le jeu normalement**
2. **Attendez 30 secondes** après le chargement complet
3. **Appuyez sur F8** pour désactiver l'IA si le jeu lag
4. **Le système affichera "IA DÉSACTIVÉE"** en haut à gauche

### ⚙️ **Optimisations Appliquées**

Le système a été drastiquement optimisé pour éviter les plantages :

- ✅ **Délais d'initialisation** : 30s, 60s, 90s selon les modules
- ✅ **Limitation drastique** : Max 3 véhicules traités simultanément
- ✅ **Intervalles longs** : 8s, 15s, 10s entre les traitements
- ✅ **Système d'urgence** : F8 pour désactiver instantanément
- ✅ **Gestion d'erreurs** renforcée

### 📝 **Configuration Ultra-Conservatrice**

Le fichier `REALIS_AIConfig.json` utilise maintenant :

```json
{
  "performanceSettings": {
    "maxEnhancedVehicles": 3,
    "maxNavigationUpdates": 2,
    "maxTrafficLightVehicles": 3,
    "enhancementCooldown": 15.0,
    "routeOptimizationInterval": 30.0,
    "enableEmergencyMode": true
  }
}
```

### 🚀 **Comment Utiliser**

1. **Démarrage du jeu** :
   - Le jeu démarre normalement
   - Les scripts d'IA restent silencieux pendant 30-90 secondes
   - Vous verrez des messages de log d'initialisation

2. **Activation progressive** :
   - **30s** : AdvancedDrivingAI s'active
   - **60s** : SmartNavigationSystem s'active
   - **90s** : TrafficLightManager s'active

3. **Utilisation normale** :
   - L'IA fonctionne très discrètement
   - Améliore seulement 2-3 véhicules à la fois
   - Respecte des cooldowns longs

### 🆘 **Si le Jeu Plante Encore**

#### Option 1: Désactiver Temporairement
- Appuyez sur **F8** dans le jeu
- Message : "Scripts IA de Conduite DÉSACTIVÉS"
- Réactivez avec **F8** quand vous voulez

#### Option 2: Ajuster la Configuration
Éditez `REALIS_AIConfig.json` :

```json
{
  "performanceSettings": {
    "maxEnhancedVehicles": 1,      // Encore plus limité
    "maxNavigationUpdates": 1,     // Une seule mise à jour
    "enhancementCooldown": 30.0,   // Cooldown très long
    "enableEmergencyMode": true
  }
}
```

#### Option 3: Désactivation Complète
Renommez temporairement ces fichiers :
- `AdvancedDrivingAI.cs` → `AdvancedDrivingAI.cs.disabled`
- `SmartNavigationSystem.cs` → `SmartNavigationSystem.cs.disabled`
- `TrafficLightManager.cs` → `TrafficLightManager.cs.disabled`

Puis recompilez avec `dotnet build`

### 📊 **Surveillance des Performances**

#### Logs à Surveiller
```
[REALIS] AdvancedDrivingAI initialized after 30s delay
[REALIS] SmartNavigationSystem initialized after 60s delay
[REALIS] TrafficLightManager initialized after 90s delay
[REALIS] AI Scripts DÉSACTIVÉS via F8 key
```

#### Signaux d'Alerte
- Lag persistant après 2 minutes
- FPS qui chutent drastiquement
- Le jeu qui "freeze" périodiquement

### 🔧 **Optimisations Supplémentaires**

Si vous voulez pousser encore plus l'optimisation :

#### Dans chaque fichier `.cs`, changez :
```csharp
Interval = 8000;   // Vers 15000 ou 20000
```

#### Réduisez les maxima :
```csharp
.Take(3)           // Vers .Take(1)
MAX_ENHANCED = 3   // Vers 1
```

### 🎮 **Test de Performance**

1. **Démarrez en mode histoire**
2. **Attendez 2 minutes complètes**
3. **Vérifiez les FPS** (normalement stables)
4. **Testez F8** (doit basculer instantanément)
5. **Conduisez 5 minutes** pour observer l'IA

### ✅ **Checklist de Fonctionnement**

- [ ] Le jeu démarre sans crash
- [ ] Après 30s : Message "AdvancedDrivingAI initialized"
- [ ] Après 60s : Message "SmartNavigationSystem initialized"  
- [ ] Après 90s : Message "TrafficLightManager initialized"
- [ ] F8 fonctionne (bascule l'IA on/off)
- [ ] FPS stables pendant la conduite
- [ ] NPCs évitent mieux les obstacles (subtil)

### 🐛 **Problèmes Connus**

1. **Premier démarrage lent** : Normal, initialisation progressive
2. **Pas d'effet immédiat** : L'IA est très conservative
3. **Amélioration subtile** : L'effet est discret mais présent

### 📞 **Signaler un Problème**

Si le problème persiste, notez :
- Moment exact du plantage
- Messages dans les logs
- Votre configuration PC
- Autres mods installés

---

## 🚗 **Ce Que Fait le Système (Quand Actif)**

- **Détection d'obstacles** améliorée (3 véhicules max)
- **Dépassement intelligent** très occasionnel
- **Respect des feux rouges** progressif
- **Navigation prédictive** limitée

L'objectif est la **stabilité avant tout** ! 🛡️ 