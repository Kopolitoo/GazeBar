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
6. **Mode mouvement** (bouton à bascule, activé au démarrage) : le cercle suit le regard, la
   fixation fige le cercle et place le curseur sur le point fixé (voir plus bas).
   **Suivi souris** (bouton à bascule, désactivé par défaut) : le curseur suit aussi le cercle ;
   sinon, il ne rejoint le regard qu'une fois celui-ci fixé. Ce réglage est enregistré.
7. **Annuler** relâche tout immédiatement. **Quitter** ferme le programme.

## Réglages

Tous les réglages sont dans la classe `Parametres` (fichier `Parametres.cs`) et enregistrés dans
`%APPDATA%\GazeBar\parametres.json`, créé au premier lancement avec les valeurs par défaut du
cahier des charges. On peut l'ouvrir avec le Bloc-notes (nombres décimaux avec un point : `0.3`),
puis relancer GazeBar. Un fichier illisible est mis de côté (`parametres.json.illisible`) et
remplacé par les valeurs par défaut. Seul le compte à rebours de 3 s reste une constante en haut
de `HoldSession.cs` (il disparaîtra à l'étape 5).

## Architecture des modes

- `OverlayWindow` : le « calque », une fenêtre transparente qui couvre tout l'écran, toujours au
  premier plan, que les clics traversent et qui ne prend jamais le focus. Il dessine le cercle,
  l'anneau de progression et l'icône d'options.
- `Modes/ModeManager` : connaît le mode courant et fait passer d'un mode à l'autre ; environ
  60 fois par seconde, il donne la position du regard (`EtatRegard`) au mode courant.
- `Modes/Mode` : modèle commun à tous les modes (`Entrer`, `Actualiser`, `Quitter`).
- `Modes/ModeMouvement` (étape 2, mode par défaut) : un cercle lissé suit le regard ; avec
  **Suivi souris**, le curseur le suit aussi. Quand le regard reste immobile pendant
  `DelaiFixation`, on passe en regard fixe (sans anneau de progression : l'anneau est réservé
  aux boutons).
- Chaque mode reçoit la position du regard dans `Entrer(regard)` pour s'initialiser, et reprend
  le cercle là où il est dessiné : pas de saut d'un mode à l'autre, pas de place par défaut.
- `Modes/DetecteurFixation` : reconnaît une fixation. Une mesure est immobile si sa vitesse reste
  sous `VitesseMaxFixation` (pixels par seconde) ET si elle reste à moins de `RayonFixation` du
  point d'ancrage (la moyenne des mesures de la fixation). Il faut `DelaiFixation` secondes
  d'immobilité, mesurées en vrai temps. Une mesure aberrante isolée est ignorée.
- `Modes/ModeRegardFixe` : le cercle se fige (bord blanc) avec une icône d'options collée du côté
  du centre de l'écran, et le curseur se place sur le point fixé. Si `ClicDirect` est activé
  (non par défaut, à mettre à `true` dans `parametres.json`), regarder dans le cercle pendant
  `DelaiClicDirect` fait un clic gauche ; regarder l'icône pendant `DelaiIconeOptions` (0,3 s,
  plus court que les autres boutons) ouvre le choix d'action ; sortir du
  cercle et de l'icône ramène au mode mouvement. Après un clic, une nouvelle fixation au même
  endroit n'est pas reconnue tant que le regard n'en est pas sorti (pas de clics en rafale).
- `Modes/ModeChoixAction` (étape 3) : la zone autour du curseur (carré de 2 × `RayonCercle`) est
  photographiée (`CaptureEcran`), puis affichée agrandie ×2 au centre d'un écran noir, avec une
  croix à la place du curseur. Autour, une couronne de sept emplacements : clic gauche (en haut),
  clic droit (en haut à droite), double clic (en haut à gauche) ; les quatre autres sont vides
  jusqu'aux étapes suivantes. À gauche, trois emplacements de modificateurs (vides jusqu'à
  l'étape 5). Les emplacements vides sont dessinés grisés avec un « + », pour montrer qu'on
  pourra les configurer. À droite, une icône « retour ». Un petit cercle (moitié de
  `RayonCercle`), par-dessus tout, montre où est le regard. Regarder une icône pendant
  `DelaiActivation` l'active (anneau de progression). Avant le clic, l'écran noir est caché et
  l'on attend 60 ms qu'il ait disparu ; puis retour au mode mouvement.
- `BoutonRegard` : un bouton activable au regard (symbole de la police Windows, pastille,
  anneau, libellé au survol), dessiné sur le calque. Il servira aussi pour la barre de menu.
- `Modes/ModeInactif` : plus rien ne suit le regard. Le bouton **Mode mouvement** de la barre y
  passe ; le mode mouvement est aussi suspendu pendant un appui long, un glisser (il reprend
  ensuite tout seul) ou la **Souris regard**.
- Le calque se remet au-dessus de toutes les fenêtres (y compris la barre) deux fois par seconde.
- `app.manifest` : déclare GazeBar « sensible au DPI », pour que le regard, le curseur et le
  dessin utilisent les mêmes pixels, même avec une mise à l'échelle de Windows à 125 % ou 150 %.

Test de l'étape 2 : on fixe un bouton, le cercle se fige (bord blanc, icône d'options) et le
curseur se place sur le bouton ; on regarde ailleurs, il repart. Avec `ClicDirect` à `true`, en
continuant à fixer le bouton, un anneau se remplit puis un clic gauche est fait. Le journal note chaque passage en regard fixe et
chaque clic direct.

Test de l'étape 3 : on fixe un fichier dans l'Explorateur, on regarde l'icône d'options, l'écran
noir montre la zone agrandie ; on regarde « double clic » : l'écran noir disparaît et le fichier
s'ouvre, sans toucher la souris.

## Limites connues

- Une fenêtre lancée « en tant qu'administrateur » ignore les clics injectés, sauf si GazeBar
  est lui aussi lancé en administrateur.
- Le mode jeu (regard vers déplacement relatif de la souris) n'est pas encore étudié.
- Journal : `%LOCALAPPDATA%\GazeBar\gazebar.log`.

## Licence

GazeBar est distribué sous licence MIT : chacun peut l'utiliser, le modifier et le redistribuer
librement, à condition de conserver la mention de copyright. Le logiciel est fourni sans garantie.
Texte complet : [LICENSE.txt](LICENSE.txt).
