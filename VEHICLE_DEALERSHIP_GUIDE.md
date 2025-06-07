# 🚗 Guide du Système de Concessionnaire REALIS

## Vue d'ensemble

Le système de concessionnaire REALIS permet d'acheter des véhicules directement dans le monde ouvert de GTA V sans avoir besoin d'ouvrir des menus complexes. C'est une approche immersive et intuitive pour l'acquisition de véhicules.

## Comment ça fonctionne

### 1. **Localisation des Concessionnaires**
Les véhicules à vendre sont situés dans plusieurs concessionnaires autour de Los Santos :
- **Premium Deluxe Motorsport** (centre-ville)
- **Legendary Motorsport** (Rockford Hills)
- **Grand Senora Desert** (désert)
- **Los Santos Customs** (quartier sud)
- **Downtown Vinewood** (Vinewood)

### 2. **Affichage des Prix**
- Quand vous vous approchez d'un véhicule en vente (dans un rayon de 15 mètres), le **nom du véhicule** et son **prix** s'affichent automatiquement au-dessus de celui-ci
- **Couleur verte** = Vous avez assez d'argent pour l'acheter
- **Couleur rouge** = Vous n'avez pas assez d'argent

### 3. **Processus d'Achat**
1. **Entrez dans le véhicule** que vous souhaitez acheter
2. Une fois à l'intérieur, un message d'achat apparaît à l'écran
3. **Appuyez sur la touche "E"** pour confirmer l'achat
4. Si vous avez suffisamment d'argent, le véhicule devient vôtre !

### 4. **Après l'Achat**
- L'argent est automatiquement déduit de votre compte
- Le véhicule vous appartient maintenant (déverrouillé, plus invincible)
- Un nouveau véhicule apparaîtra à la même position après quelques secondes
- Vous recevez une notification de confirmation

## Types de Véhicules Disponibles

### 🏎️ **Voitures de Sport** (725 000$ - 2 400 000$)
- Adder, Zentorno, Osiris, T20, Turismo2

### 🚙 **Voitures de Luxe** (60 000$ - 200 000$)
- Cognoscenti, Exemplar, Felon, Jackal, Oracle

### 🏁 **Voitures Classiques** (126 000$ - 795 000$)
- Banshee, Bullet, Cheetah, EntityXF, Infernus

### 🚐 **SUVs** (35 000$ - 90 000$)
- Baller, Cavalcade, Dubsta, FQ2, Granger

### 🚗 **Voitures Compactes** (15 000$ - 85 000$)
- Blista, Brioso, Dilettante, Issi2, Panto

### 🏍️ **Motos** (9 000$ - 82 000$)
- Akuma, Bati, CarbonRS, Double, Hakuchou

## Configuration

### Activation/Désactivation
Le système de concessionnaire peut être activé ou désactivé dans la configuration REALIS :
```json
{
  "modSettings": {
    "vehicleDealershipEnabled": true
  }
}
```

### Touches de Contrôle
- **Touche d'interaction** : E (par défaut, configurable dans les paramètres)

## Caractéristiques du Système

### ✅ **Avantages**
- **Pas de menu** - Interface immersive
- **Affichage visuel des prix** - Information claire
- **Vérification automatique des fonds** - Empêche les achats impossibles
- **Renouvellement automatique** - Nouveaux véhicules après chaque vente
- **Couleurs aléatoires** - Variété visuelle

### 🔧 **Fonctionnalités Techniques**
- Véhicules persistants et invincibles (avant achat)
- Gestion intelligente de la mémoire
- Nettoyage automatique lors de la fermeture du mod
- Journalisation complète pour le débogage

## Conseils d'Utilisation

1. **Vérifiez votre argent** avant de vous rendre dans un concessionnaire
2. **Explorez différents concessionnaires** pour trouver le véhicule qui vous plaît
3. **Les véhicules se renouvellent** - revenez plus tard si la sélection ne vous convient pas
4. **Sauvegardez régulièrement** votre progression

## Dépannage

### Problèmes Courants

**Q : Les prix ne s'affichent pas**
R : Assurez-vous d'être à moins de 15 mètres du véhicule et que le système est activé dans la configuration.

**Q : Impossible d'acheter un véhicule**
R : Vérifiez que vous avez suffisamment d'argent et que vous êtes bien à l'intérieur du véhicule.

**Q : Les véhicules n'apparaissent pas**
R : Redémarrez le mod ou vérifiez que le système de concessionnaire est activé dans les paramètres.

**Q : Le véhicule ne se déverrouille pas après l'achat**
R : Sortez et rentrez dans le véhicule, ou redémarrez le moteur.

## Support

Pour tout problème ou suggestion concernant le système de concessionnaire :
1. Vérifiez les logs de REALIS pour des messages d'erreur
2. Consultez la configuration du mod
3. Redémarrez le script si nécessaire

---

*Le système de concessionnaire REALIS offre une expérience d'achat de véhicules naturelle et immersive dans GTA V. Profitez de votre nouvelle voiture !* 🚗✨ 