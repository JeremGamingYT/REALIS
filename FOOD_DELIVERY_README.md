# 📱 Application QuickEats - Livraison de Nourriture

## 🎯 Vue d'ensemble

L'application **QuickEats** ajoute un système complet de livraison de nourriture à votre mod REALIS pour GTA V. Elle simule une véritable application mobile de type UberEats avec des restaurants authentiques de Los Santos.

## ✨ Fonctionnalités

### 🏪 Restaurants Disponibles
- **Burger Shot** - Fast-food classique (burgers, frites, boissons)
- **Cluckin' Bell** - Spécialité poulet (ailes, burgers poulet)
- **Pizza This** - Restaurant italien (pizzas, pâtes, desserts)
- **Taco Bomb** - Cuisine mexicaine (tacos, burritos, nachos)

### 📱 Système de Téléphone Simulé
- Interface téléphone réaliste avec applications
- Navigation avec les flèches ↑↓ et Entrée
- Applications factices (Messages, Contacts, Photos, Paramètres)
- Son et effets authentiques du téléphone GTA V

### 🛒 Système de Commande
- Panier de commande intelligent
- Calcul automatique des taxes et frais de livraison (15%)
- Estimation du temps de livraison
- Historique des commandes
- Suivi en temps réel

### 💳 Système de Paiement
- Déduction automatique de l'argent du joueur
- Vérification des fonds disponibles
- Facturation transparente avec détails

### 🚚 Livraison Réaliste
- Statuts de commande : Confirmée → En préparation → En livraison → Livrée
- Notifications push style téléphone mobile
- Temps de livraison réaliste (15-20 minutes)
- Restauration de santé à la livraison

## 🎮 Contrôles

### Simulateur de Téléphone
- **F7** : Ouvrir/fermer le téléphone
- **↑/↓** : Naviguer dans les applications
- **Entrée** : Sélectionner une application

### Application QuickEats
- **F8** : Ouvrir directement QuickEats
- **F9** : Menu REALIS (contient un lien vers QuickEats)

## 🛠 Installation et Configuration

### 1. Fichiers à Ajouter
```
REALIS/
├── Core/
│   ├── FoodDeliveryApp.cs              # Application de base
│   ├── FoodDeliveryAppEnhanced.cs      # Version améliorée
│   └── PhoneSimulatorSimple.cs         # Simulateur de téléphone
```

### 2. Intégration dans REALIS
L'application s'intègre automatiquement dans votre système REALIS existant. Un nouvel élément de menu "📱 QuickEats App" est ajouté au menu principal.

### 3. Dépendances
- **SHVDN V3** (ScriptHookVDotNet)
- **LemonUI** pour l'interface utilisateur
- **GTA V** avec mod support activé

## 🎨 Personnalisation

### Ajouter de Nouveaux Restaurants
```csharp
restaurants["Nouveau Restaurant"] = new List<FoodItem>
{
    new FoodItem("Nom du Plat", "Description", 12.99f, "🍕", 20),
    // Prix, Emoji, Temps de préparation (minutes)
};
```

### Modifier les Prix et Frais
```csharp
// Dans ProcessMobileOrder()
var fees = subtotal * 0.15f; // 15% de frais (modifiable)
```

### Personnaliser les Notifications
```csharp
var notification = new PhoneNotification
{
    Title = "Votre Titre",
    Message = "Votre message personnalisé",
    IsImportant = false
};
```

## 🎭 Expérience Utilisateur

### Scénario Typique d'Utilisation

1. **📱 Ouverture du téléphone** (F7)
   - Interface téléphone avec applications disponibles
   - Navigation intuitive entre les apps

2. **🍔 Lancement de QuickEats**
   - Sélection depuis le téléphone ou directement (F8)
   - Accueil avec restaurants disponibles

3. **🏪 Parcours des restaurants**
   - 4 restaurants avec leurs spécialités
   - Prix et temps de préparation affichés
   - Descriptions détaillées des plats

4. **🛒 Ajout au panier**
   - Notifications instantanées d'ajout
   - Calcul automatique du total
   - Suivi en temps réel du panier

5. **💳 Finalisation de commande**
   - Vérification des fonds
   - Calcul des frais et taxes
   - Confirmation avec numéro de commande

6. **🚚 Suivi de livraison**
   - Notifications de statut progressives
   - Estimation temps réel
   - Notification de livraison finale

7. **❤️ Bénéfices de gameplay**
   - Restauration de santé à la livraison
   - Immersion renforcée dans Los Santos
   - Économie du jeu intégrée

## 🏆 Fonctionnalités Avancées

### Notifications Intelligentes
- Queue de notifications pour éviter le spam
- Délai entre notifications (2 secondes)
- Sons authentiques du téléphone GTA V
- Messages formatés avec couleurs GTA

### Gestion d'État Persistante
- Historique des commandes conservé
- Panier maintenu entre sessions
- Statut de livraison en temps réel

### Intégration REALIS
- Accès depuis le menu principal REALIS
- Cohérence avec le style du mod
- Configuration sauvegardable

## 🚀 Extensions Possibles

### Futures Améliorations
- **🎯 Géolocalisation** : Livraison selon la position du joueur
- **⭐ Système de notation** : Évaluation des restaurants
- **🎁 Programme de fidélité** : Points et récompenses
- **👥 Commandes de groupe** : Commandes pour plusieurs joueurs
- **📊 Statistiques** : Analyse des habitudes de commande
- **🕐 Heures d'ouverture** : Restaurants fermés la nuit
- **🌮 Plats du jour** : Offres spéciales quotidiennes
- **💰 Codes promo** : Réductions et offres

### Intégrations Techniques
- **API météo** : Influence sur les temps de livraison
- **Traffic AI** : Délais variables selon la circulation
- **Base de données** : Sauvegarde persistante des données
- **Multijoueur** : Synchronisation entre joueurs

## 🐛 Dépannage

### Problèmes Courants

**Le téléphone ne s'ouvre pas (F7)**
- Vérifiez que SHVDN est installé correctement
- Assurez-vous qu'aucun autre mod ne confliggit avec F7

**L'application QuickEats ne fonctionne pas (F8)**
- Vérifiez l'installation de LemonUI
- Consultez les logs SHVDN pour les erreurs

**Pas de notifications**
- Vérifiez que les notifications GTA V sont activées
- Redémarrez le jeu si nécessaire

**Argent non déduit lors de la commande**
- Problème possible avec l'API GTA V Money
- Utilisez un trainer pour ajuster manuellement

### Compatibilité
- ✅ **GTA V** : Toutes versions supportées par SHVDN
- ✅ **SHVDN V3** : Version recommandée
- ✅ **LemonUI** : Version SHVDN3 requise
- ⚠️ **Autres mods téléphone** : Conflits possibles

## 📞 Support

Pour toute question ou problème :
1. Vérifiez ce README
2. Consultez les logs SHVDN
3. Testez sans autres mods actifs
4. Contactez le support REALIS

---

**🍔 Bon appétit dans Los Santos ! 🌮** 