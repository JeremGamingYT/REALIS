# Guide de Diagnostic - Crash REALIS avec GTA V Enhanced Edition

## ✅ STATUT : BattlEye désactivé + ScriptHookVDotNet v3.7.0-nightly.28

Vous avez la bonne version et BattlEye est désactivé. Analysons les autres causes possibles.

## 🔍 Erreur principale identifiée : `SHVDN.NativeMemory` TypeInitializationException

D'après [l'issue GitHub #1451](https://github.com/scripthookvdotnet/scripthookvdotnet/issues/1451), voici l'erreur exacte qui cause les crashes :

```
System.TypeInitializationException: The type initializer for 'SHVDN.NativeMemory' threw an exception.
---> System.NullReferenceException: Object reference not set to an instance of an object.
at SHVDN.NativeMemory..cctor()
```

## 🎯 Diagnostics avancés pour Enhanced Edition

### **1. Vérification des prérequis**
- ❌ **Script Hook V** : Requis AVANT ScriptHookVDotNet
- ❌ **Visual C++ Redistributables** : 2019/2022 x64
- ❌ **Versions incompatibles** des ASI

### **2. Problèmes connus avec Enhanced Edition**

#### **A. Conflit de versions ASI**
Enhanced Edition change les pattern de mémoire que ScriptHookVDotNet utilise pour scanner.

#### **B. Ordre de chargement incorrect**
```
1. ScriptHookV.dll (OBLIGATOIRE en premier)
2. ScriptHookVDotNet.asi 
3. ScriptHookVDotNet3.dll
```

#### **C. Cache corrompue**
Enhanced Edition peut laisser des caches corrompues qui interfèrent.

## 🚨 Solutions spécifiques Enhanced Edition

### **Solution 1 : Installation propre (RECOMMANDÉ)**

1. **Désinstaller complètement les mods :**
   ```batch
   # Supprimer tous les fichiers mod
   del "ScriptHookV.dll"
   del "ScriptHookVDotNet.asi"
   del "ScriptHookVDotNet*.dll"
   del "asiloader.dll"
   rd /s /q "scripts"
   ```

2. **Télécharger Script Hook V :**
   - Site officiel : http://dev-c.com/gtav/scripthookv/
   - Copier `ScriptHookV.dll` ET `asiloader.dll` dans GTA V

3. **Installer ScriptHookVDotNet nightly :**
   - GitHub : https://github.com/scripthookvdotnet/scripthookvdotnet/releases
   - Version : v3.7.0-nightly.28 ou plus récent

### **Solution 2 : Vérification Visual C++**

Enhanced Edition nécessite les redistributables récents :
- **Microsoft Visual C++ 2015-2022 Redistributable (x64)**
- Télécharger depuis le site Microsoft officiel

### **Solution 3 : Mode Compatibilité**

Si le crash persiste, essayez :
1. Clic droit sur `GTA5.exe` > Propriétés
2. Onglet "Compatibilité"
3. Cocher "Exécuter en mode de compatibilité pour : Windows 10"
4. Cocher "Exécuter en tant qu'administrateur"

### **Solution 4 : Paramètres de lancement**

Pour Steam/Epic, ajoutez ces paramètres :
```
-windowed -norestrictions -noverify
```

## 🔧 Script de test automatique

### **Utilisation :**
1. Exécutez `install_to_gta.bat` 
2. Lancez GTA V
3. Si crash : Vérifiez les logs dans le dossier GTA V

### **Fichiers de log à vérifier :**
- `ScriptHookVDotNet.log` (dossier GTA V)
- `REALIS.log` (si généré)

## 📋 Checklist de diagnostic

- [ ] BattlEye désactivé ✅
- [ ] ScriptHookVDotNet v3.7.0+ ✅
- [ ] Script Hook V installé
- [ ] Visual C++ 2022 Redistributable x64
- [ ] Pas d'autres ASI conflictuels
- [ ] Dossier `scripts` propre
- [ ] Mode Administrateur
- [ ] Logs vérifiés

## 🆘 Si le problème persiste

**Collectez ces informations :**
1. Contenu de `ScriptHookVDotNet.log`
2. Version exacte de GTA V (F7 dans le jeu)
3. Liste des fichiers .asi dans le dossier
4. Message d'erreur exact

**Contact :**
- Issues GitHub ScriptHookVDotNet
- Forum GTA5-Mods avec ces informations

---

## Notes importantes

- ⚠️ **N'utilisez JAMAIS des versions mixées** (ASI v3.6 + DLL v3.7)
- ⚠️ **Enhanced Edition** nécessite des précautions supplémentaires
- ⚠️ **La console F4** peut crasher si NativeMemory n'est pas initialisé

---

*Dernière mise à jour : Diagnostic basé sur l'issue GitHub #1451* 