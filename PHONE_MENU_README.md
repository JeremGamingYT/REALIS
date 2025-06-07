# Menu Téléphone REALIS

## Vue d'ensemble

Le Menu Téléphone REALIS remplace le téléphone par défaut de GTA V avec un système LemonUI complet qui offre des fonctionnalités avancées et immersives. Ce système s'intègre parfaitement avec les autres systèmes REALIS existants.

## Fonctionnalités

### 🚨 Services d'Urgence
- **Police (911)**: Appel automatique d'une patrouille de police qui se rend à votre position
- **Ambulancier (112)**: Les secours médicaux arrivent avec une ambulance équipée
- **Pompier (114)**: Intervention des pompiers avec un camion de pompiers
- **Garde-côtes (115)**: Patrouille maritime pour les situations près de l'eau

### 🍔 Livraison de Nourriture (Style UberEats)
Trois restaurants disponibles avec des menus complets:

#### Burger Shot
- Big Burger ($12.99) - Burger avec fromage, salade, tomate
- Chicken Wrap ($8.99) - Wrap au poulet grillé
- Frites ($4.99) - Portion de frites croustillantes
- Soda ($2.99) - Boisson gazeuse rafraîchissante

#### Cluckin' Bell
- Poulet Frit ($15.99) - Morceaux de poulet épicé
- Chicken Burger ($10.99) - Burger au poulet croustillant
- Salade César ($9.99) - Salade fraîche avec poulet
- Milkshake ($5.99) - Milkshake vanille ou chocolat

#### Pizza This
- Pizza Margherita ($18.99) - Pizza classique tomate-mozzarella
- Pizza Pepperoni ($21.99) - Pizza au pepperoni
- Calzone ($16.99) - Calzone farci au fromage
- Tiramisu ($7.99) - Dessert italien traditionnel

### 🚛 Service de Remorquage
- **Remorquage Rapide ($150)**: Intervention immédiate (2 secondes)
- **Remorquage Programmé ($100)**: Arrivée dans 5 minutes
- **Remorquage Lourd ($300)**: Pour véhicules lourds et spéciaux

## Contrôles

### Ouverture du Menu
- **Touche T**: Ouvre/ferme le menu téléphone
- **Flèches directionnelles**: Navigation dans les menus
- **Entrée**: Sélection d'un élément
- **Retour**: Retour au menu précédent

### Navigation
- Utilisez les **flèches haut/bas** pour naviguer entre les options
- Appuyez sur **Entrée** pour sélectionner
- Utilisez l'option **"← Retour"** pour revenir au menu précédent
- Appuyez sur **T** à nouveau pour fermer complètement le téléphone

## Fonctionnement des Services

### Services d'Urgence
1. Sélectionnez le service désiré dans le menu
2. L'appel est automatiquement passé avec simulation sonore
3. Le véhicule de service apparaît dans un rayon aléatoire autour de votre position
4. Le véhicule se dirige vers vous avec les signaux d'urgence
5. Un blip sur la carte indique l'arrivée du service

### Livraison de Nourriture
1. Choisissez un restaurant dans le menu "Livraison Nourriture"
2. Sélectionnez votre plat dans le menu du restaurant
3. Le coût est automatiquement déduit de votre argent
4. La livraison arrive entre 5-10 minutes (temps accéléré)
5. Un véhicule de livraison avec blip apparaît sur votre carte
6. Le livreur se dirige vers votre position actuelle

### Service de Remorquage
1. Choisissez le type de remorquage selon vos besoins
2. Le coût est déduit immédiatement de votre argent
3. La remorqueuse arrive selon le délai choisi:
   - Rapide: 2 secondes
   - Programmé: 5 minutes réelles
   - Lourd: 8 secondes
4. Un blip jaune indique l'arrivée de la remorqueuse

## Intégration avec les Systèmes REALIS

### Système Économique
- Tous les services utilisent l'argent du joueur
- Vérification automatique des fonds avant service
- Messages d'erreur si fonds insuffisants

### Système de Blips
- Blips automatiques pour tous les véhicules de service
- Couleurs distinctives par type de service:
  - 🔵 Bleu: Police
  - 🟢 Vert: Ambulance/Livraison
  - 🔴 Rouge: Pompiers
  - 🟡 Jaune: Remorquage

### Gestion des Véhicules
- Spawn intelligent évitant les collisions
- Positions aléatoires dans un rayon défini
- Nettoyage automatique des blips après utilisation
- Véhicules avec conducteurs AI intelligents

## Configuration Technique

### Installation
Le système est automatiquement initialisé avec REALIS Main. Aucune configuration supplémentaire n'est requise.

### Dépendances
- LemonUI.SHVDN3 (v2.2.0)
- ScriptHookVDotNet3
- REALIS.Core.Logger pour la journalisation

### Personnalisation
Le code est structuré pour permettre facilement:
- Ajout de nouveaux restaurants
- Modification des prix
- Ajout de nouveaux types de services
- Personnalisation des délais de livraison

## Fonctionnalités Avancées

### Système de Spawn Intelligent
- Position aléatoire calculée autour du joueur
- Vérification de la hauteur du sol
- Évitement des zones d'eau pour les véhicules terrestres
- Spawn adaptatif selon le type de service

### Détection de l'Environnement
- Détection automatique de la proximité de l'eau pour les garde-côtes
- Adaptation des services selon la position du joueur
- Messages contextuels selon l'environnement

### Interface Utilisateur
- Interface LemonUI native et fluide
- Badges visuels pour chaque type de service
- Descriptions détaillées pour chaque option
- Prix affichés en temps réel
- Navigation intuitive avec retours visuels

## Dépannage

### Problèmes Courants

**Le menu ne s'ouvre pas**
- Vérifiez que LemonUI est correctement installé
- Assurez-vous qu'aucun autre mod ne capture la touche T

**Les véhicules n'apparaissent pas**
- Vérifiez les logs REALIS pour les erreurs
- Assurez-vous d'avoir suffisamment d'argent pour le service

**Navigation difficile**
- Utilisez uniquement les flèches directionnelles
- Évitez d'utiliser la souris (désactivée intentionnellement)

### Logs
Tous les événements sont enregistrés via le système Logger de REALIS. Consultez les logs pour diagnostiquer les problèmes.

## Développement Futur

### Fonctionnalités Prévues
- Ajout de plus de restaurants
- Services de taxi intégrés
- Système de commande de véhicules
- Intégration avec le système bancaire
- Historique des commandes
- Système de favoris
- Notifications push pour les livraisons

### Extensibilité
Le système est conçu pour être facilement extensible. Les développeurs peuvent ajouter de nouveaux services en implémentant les interfaces appropriées.

---

*Ce système fait partie du mod REALIS et s'intègre parfaitement avec tous les autres systèmes du mod pour une expérience de jeu immersive et cohérente.* 