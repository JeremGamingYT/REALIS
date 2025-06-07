# Guide d'utilisation du Menu REALIS

## Comment voir le menu de première utilisation

### 1. Premier lancement automatique
- Au **premier lancement** du mod, le menu apparaît automatiquement
- Si vous n'avez jamais utilisé REALIS, le menu se lance automatiquement

### 2. Forcer l'affichage du menu (mode debug)
- Appuyez sur **F8** à tout moment dans le jeu pour afficher le menu de setup
- Ceci fonctionne même si vous avez déjà terminé la configuration

## ⚠️ IMPORTANT : Comment repérer que le menu est actif

### Signes que le menu fonctionne :
1. **Le temps ralentit drastiquement** (0.1x) - c'est normal !
2. **Notifications répétées** toutes les 1.5 secondes avec le contenu du menu
3. **Message d'alerte** : "REALIS SETUP MENU IS ACTIVE - TIME SLOWED!"
4. **Le jeu devient très lent** - c'est voulu pour attirer votre attention

## Fonctionnement du menu

### Navigation
- **↑ / W** : Naviguer vers le haut
- **↓ / S** : Naviguer vers le bas  
- **ENTER / SPACE** : Sélectionner l'option
- **ESC** : Retour/Annuler

### Étapes du menu
1. **Accueil** : Choisir de continuer ou ignorer la configuration
2. **Langue** : Sélectionner votre langue préférée
3. **Touches** : Voir les raccourcis clavier par défaut
4. **Terminé** : Finaliser la configuration

### Effets visuels
- Le **temps ralentit drastiquement** (0.1x) pendant que le menu est ouvert pour forcer votre attention
- Les **notifications se répètent** toutes les 1.5 secondes avec le contenu du menu complet
- **Feedback immédiat** sur chaque action (UP, DOWN, SELECT, BACK)
- **Messages d'alerte** pour vous confirmer que le menu est actif

## Touches par défaut configurées

- **F9** : Ouvrir le menu de configuration (à venir)
- **F10** : Activer/Désactiver le système de police
- **F11** : Activer/Désactiver l'IA de trafic
- **F12** : Services d'urgence (à venir)
- **F5** : Sauvegarde rapide des paramètres

## Résolution de problèmes

### Le menu n'apparaît pas ?
1. Vérifiez que le mod REALIS est bien chargé (regardez les logs)
2. Appuyez sur **F8** pour forcer l'affichage
3. Vérifiez que les notifications sont activées dans GTA V

### Le menu disparaît trop vite ?
- Le menu utilise maintenant `PostTickerForced` pour une durée plus longue
- Il se rafraîchit automatiquement toutes les 2 secondes
- Le temps est ralenti pour vous laisser le temps de lire

### Navigation ne fonctionne pas ?
- Assurez-vous de ne pas appuyer sur les touches trop rapidement
- Il y a un délai de 300ms entre les inputs pour éviter les doublons
- Utilisez soit les flèches directionnelles, soit WASD

## Fichiers de configuration créés

Après la première configuration, ces fichiers sont créés dans `scripts/REALIS/` :
- `UserConfig.json` : Vos préférences personnelles
- `Languages.json` : Configuration des langues
- `Keybinds.json` : Vos raccourcis clavier

## Notes importantes

- La configuration est **sauvegardée automatiquement** à la fin du setup
- Vous pouvez **modifier manuellement** les fichiers JSON si nécessaire
- Le menu ne s'affiche plus automatiquement après la première configuration (sauf si vous supprimez `UserConfig.json`) 