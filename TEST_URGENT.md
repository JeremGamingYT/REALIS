# TEST URGENT - DIAGNOSTIC DE CRASH

## 🔥 ÉTAPES DE DIAGNOSTIC IMMÉDIAT

### 1. Testez la version simplifiée MAINTENANT

1. **Lancez GTA V** avec le mod
2. **Allez à l'aéroport de Los Santos** (près de la tour de contrôle)
3. **Cherchez un blip ORANGE** avec "Mission Simple"
4. **Approchez-vous** du blip
5. **Regardez si le message "Appuyez sur E" apparaît**
6. **Appuyez DOUCEMENT sur E**

### 2. Si ça crash AVANT d'appuyer sur E :
- ❌ **Le problème est dans le chargement du module**
- ✅ **Vérifiez que tous les DLL sont dans le bon dossier**
- ✅ **Vérifiez ScriptHookV + ScriptHookVDotNet**

### 3. Si ça crash QUAND vous appuyez sur E :
- ❌ **Le problème est dans StartMission()**  
- ✅ **On va débugger plus précisément**

### 4. Si PAS DE CRASH sur la mission simple :
- ✅ **Le module fonctionne !**
- ❌ **L'ancienne mission était trop complexe**
- ✅ **On peut réactiver petit à petit**

## 🚨 SI ÇA CRASH ENCORE :

### Option 1 - ScriptHookV
Vérifiez que vous avez les bonnes versions :
- **ScriptHookV.dll** dans le dossier GTA V
- **dinput8.dll** dans le dossier GTA V  
- **ScriptHookVDotNet3.dll** dans le dossier GTA V

### Option 2 - Logs Windows
1. Ouvrez **Observateur d'événements Windows**
2. Allez dans **Journaux Windows > Application**
3. Reproduisez le crash
4. Regardez les erreurs GTA5.exe

### Option 3 - Debug Tools
1. Téléchargez **DebugView** (Microsoft)
2. Lancez-le AVANT GTA V
3. Reproduisez le crash  
4. Regardez les messages "=== MISSION SIMPLE ===" 

## 📍 COORDONNÉES DE TEST
Aéroport LS : **X: -1172, Y: -1571, Z: 4**
(Près des hangars, côté tour de contrôle)

## ⚡ TEST EN 30 SECONDES
1. Lance GTA V
2. Va à l'aéroport  
3. Cherche blip orange
4. Appuie sur E
5. Regarde si "Mission test OK" apparaît

**SI CETTE VERSION SIMPLE CRASH ENCORE**, le problème n'est PAS dans votre mission mais dans votre installation ScriptHookV/SHVDN ! 