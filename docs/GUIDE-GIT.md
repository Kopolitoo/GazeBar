# Guide Git et GitHub pour GazeBar

Ce guide explique comment publier le projet et travailler à plusieurs (vous et Claude)
avec des branches. Tout se fait dans Visual Studio, sans ligne de commande.

## 1. Les branches : le principe

- **`main`** : la version qui fonctionne. On n'y met que du code testé.
- **Une branche par fonctionnalité**, créée à partir de `main`. Exemples :
  - `fonction/clavier-predictif`
  - `fonction/mode-jeu`
  - `fonction/reglages`
  - `correction/nom-du-probleme` pour une réparation.
- Quand la fonctionnalité est testée et marche, on la **fusionne** dans `main` par une
  *pull request* (demande de fusion) sur GitHub, puis on supprime la branche.

Avantage : si un essai échoue, `main` reste intact, et on peut toujours revenir en arrière.

## 2. Publier le projet (une seule fois)

1. Ouvrir `GazeBar.slnx` dans Visual Studio.
2. Menu **Git → Créer un dépôt Git…**
3. Se connecter à son compte GitHub si demandé.
4. Remplir :
   - Nom du dépôt : `GazeBar`
   - Description : courte phrase (ex. « Barre d'outils de commande oculaire pour Tobii PCEye 5 »)
   - Visibilité : **Public** (projet open source)
   - Modèle .gitignore : **Aucun** (le projet a déjà le sien)
   - Licence : à choisir (**MIT** = très permissive ; **GPL-3.0** = les versions dérivées
     doivent rester libres). Ce choix est important : une fois le projet publié et utilisé
     par d'autres, il est difficile de le changer.
   - Ne pas ajouter de README : le projet en a déjà un.
5. **Créer et envoyer**. La version actuelle (qui fonctionne) devient le premier commit de `main`.

Si la branche principale s'appelle `master`, la renommer : **Git → Gérer les branches**,
clic droit sur `master` → **Renommer** → `main`, puis envoyer.

## 3. Travailler sur une fonctionnalité

1. **Git → Nouvelle branche**, nom `fonction/…`, basée sur `main`.
2. Modifier le code, tester (F5).
3. Fenêtre **Modifications Git** : écrire un message qui dit CE QUE vous avez changé
   et POURQUOI (en français, une phrase), puis **Valider tout** (commit).
4. **Envoyer** (push) pour publier la branche sur GitHub.
5. Sur GitHub, ouvrir une **Pull request** de la branche vers `main`, relire, fusionner.
6. Dans Visual Studio, repasser sur `main` et faire **Extraire** (pull).

Petits commits fréquents valent mieux qu'un gros : chacun est un point de retour possible.

## 4. Travailler avec Claude

- Donnez à Claude le nom du dépôt (`votre-compte/GazeBar`). Claude crée une branche,
  propose son code et ouvre une pull request avec une explication.
- Pour essayer son travail : dans Visual Studio, **Git → Extraire** puis changer de branche
  (**Git → Gérer les branches** → double-clic sur la branche de Claude), F5.
- Si cela marche : fusionner la pull request. Sinon, décrire le problème (copier le message
  d'erreur) ; Claude corrige sur la même branche.
- Vos propres modifications : même chose dans l'autre sens, envoyez votre branche et
  dites-le à Claude, qui lira la différence avec `main`.

## 5. Ce qu'il ne faut jamais publier

Le fichier `.gitignore` exclut déjà : les dossiers de compilation, les enregistrements
`positions-*.csv`, les journaux, les clés de signature et le futur dictionnaire personnel.
Avant d'envoyer, jetez un œil à la liste des fichiers dans **Modifications Git** : si un fichier
contient des informations privées (mots de passe, adresses, noms), ne le validez pas.
