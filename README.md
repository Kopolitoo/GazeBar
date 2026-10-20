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
   l'écran met en pause. Réglages `Lissage` et `ZoneMorteCurseur` dans `parametres.json`. Avec ce mode, les
   glisser-déposer suivent le vrai regard.
6. **Annuler** relâche tout immédiatement. **Quitter** ferme le programme.

## Réglages

Tous les réglages sont dans la classe `Parametres` (fichier `Parametres.cs`) et enregistrés dans
`%APPDATA%\GazeBar\parametres.json`, créé au premier lancement avec les valeurs par défaut du
cahier des charges. On peut l'ouvrir avec le Bloc-notes (nombres décimaux avec un point : `0.3`),
puis relancer GazeBar. Un fichier illisible est mis de côté (`parametres.json.illisible`) et
remplacé par les valeurs par défaut. Seul le compte à rebours de 3 s reste une constante en haut
de `HoldSession.cs` (il disparaîtra à l'étape 5).

## Architecture des modes (étape 1 : socle-modes)

- `OverlayWindow` : le « calque », une fenêtre transparente qui couvre tout l'écran, toujours au
  premier plan, que les clics traversent et qui ne prend jamais le focus.
- `Modes/ModeManager` : connaît le mode courant et fait passer d'un mode à l'autre ; environ
  60 fois par seconde, il donne la position du regard (`EtatRegard`) au mode courant.
- `Modes/Mode` : modèle commun à tous les modes (`Entrer`, `Actualiser`, `Quitter`).
- `Modes/ModeTest` : provisoire, un cercle centré sur le regard brut (ou au centre de l'écran si
  le regard est indisponible). Le bouton **Cercle test** de la barre l'affiche ou le cache.
- `app.manifest` : déclare GazeBar « sensible au DPI », pour que le regard, le curseur et le
  dessin utilisent les mêmes pixels, même avec une mise à l'échelle de Windows à 125 % ou 150 %.

Test de l'étape 1 : le cercle s'affiche au-dessus de toutes les fenêtres, suit le regard, et les
clics passent à travers (on peut cliquer sur une icône du bureau située sous le cercle).
Le journal indique la taille de l'écran et l'échelle détectée (ligne « Calque : … »).

## Limites connues

- Une fenêtre lancée « en tant qu'administrateur » ignore les clics injectés, sauf si GazeBar
  est lui aussi lancé en administrateur.
- Le mode jeu (regard vers déplacement relatif de la souris) n'est pas encore étudié.
- Journal : `%LOCALAPPDATA%\GazeBar\gazebar.log`.

## Licence

GazeBar est distribué sous licence MIT : chacun peut l'utiliser, le modifier et le redistribuer
librement, à condition de conserver la mention de copyright. Le logiciel est fourni sans garantie.
Texte complet : [LICENSE.txt](LICENSE.txt).
