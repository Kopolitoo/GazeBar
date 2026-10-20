# GazeBar

Barre d'outils pour la commande de l'ordinateur au regard, en complément de TD Control
(Tobii Dynavox PCEye 5). Application WPF (.NET Framework 4.8, 32 bits), code commenté en français.

> Travail à plusieurs, branches et GitHub : voir [docs/GUIDE-GIT.md](docs/GUIDE-GIT.md).

## But de cette première version

1. **Vérifier que le curseur piloté par TD Control est un bon substitut du regard** :
   cadence, régularité, couverture de tout l'écran.
2. Offrir trois actions que TD Control ne propose pas :
   - **Appui long** (bouton gauche maintenu 1,5 s) ;
   - **Glisser-déposer avec le bouton gauche** ;
   - **Glisser-déposer avec le bouton droit**.

## Installation

- Visual Studio 2026 avec la charge de travail « Développement d'applications de bureau .NET »
  et le kit de développement .NET Framework 4.8.
- Le moteur Tobii doit tourner (processus tobii.service / tobii interaction engine), comme pour Mill Mouse.
- Ouvrir `GazeBar.slnx`, puis F5.

Ce code n'a pas pu être compilé par son auteur : si Visual Studio signale une erreur,
envoyez-en le texte, elle sera corrigée.

## Principe

Le regard réel est lu par le moteur Tobii (paquet NuGet Tobii.Interaction, classe GazeSource) :
~30 mesures/s, y compris hors de l'écran (coordonnées négatives ou supérieures à la taille de l'écran).
La barre et la fenêtre Diagnostic l'affichent en vert, à côté du curseur de TD Control.

Pour les boutons : TD Control déplace le curseur de Windows avec le regard. GazeBar lit la position de ce
curseur (`GetCursorPos`) et injecte seulement des appuis/relâchements de boutons
(`SendInput`), jamais de déplacement. Pendant qu'un bouton est maintenu, TD Control continue
de déplacer le curseur : c'est ce qui produit le glisser.

La barre ne prend jamais le focus, pour ne pas perturber la fenêtre où l'on agit.

## Protocole de test

1. Lancer GazeBar. La barre apparaît en haut de l'écran avec « Curseur : X … Y … | N changements/s ».
   Regardez différentes zones de l'écran : les valeurs doivent suivre le regard, y compris dans les coins.
2. Bouton **Diagnostic** → **Enregistrer 20 s** : parcourez les coins, les bords et le centre
   du regard, puis fixez le centre 3 secondes. Le fichier CSV est écrit dans
   `Documents\GazeBar\positions-….csv` : envoyez-le pour analyse.
3. **Appui long** : cliquez le bouton (3 s de compte à rebours), regardez l'endroit voulu.
   Le bouton gauche est appuyé à la fin du compte à rebours, puis relâché 1,5 s plus tard.
4. **Glisser (clic droit)** : par exemple dans l'Explorateur sur un fichier, le menu de
   déplacement apparaît au relâchement. **Glisser (gauche)** : par exemple dans Paint.
5. **Souris regard** (bouton à bascule) : le curseur suit directement le regard, lissé. Regarder hors de
   l'écran met en pause. Réglages en haut de `GazeMouse.cs` (lissage, zone morte). Avec ce mode, les
   glisser-déposer suivent le vrai regard.
6. **Annuler** relâche tout immédiatement. **Quitter** ferme le programme.

## Réglages

Constantes en haut de `HoldSession.cs` : durée du compte à rebours, durée de l'appui long,
rayon et durée de fixation pour déposer, distance minimale de glissement, durée maximale.

## Limites connues

- Une fenêtre lancée « en tant qu'administrateur » ignore les clics injectés, sauf si GazeBar
  est lui aussi lancé en administrateur.
- Le mode jeu (regard vers déplacement relatif de la souris) n'est pas encore étudié.
- Journal : `%LOCALAPPDATA%\GazeBar\gazebar.log`.
