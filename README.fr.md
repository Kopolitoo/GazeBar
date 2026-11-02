# GazeBar

*English version: [README.md](README.md).*

Logiciel de commande de l'ordinateur au regard avec un eyetracker (développé et testé avec le
Tobii Dynavox PCEye 5), organisé en modes (mouvement, regard fixe, choix d'action, menu…).
Application WPF (.NET Framework 4.8, 32 bits) ; code et commentaires en anglais, interface en
anglais et en français.
Le logiciel de commande au regard fourni avec l'eyetracker est mis en pause pendant l'utilisation de GazeBar.

> Depuis l'étape 4, l'ancienne barre d'outils en haut de l'écran a disparu : tout se fait au
> regard, et la barre de menu s'ouvre en regardant **en bas, hors de l'écran**. Les parties
> « But de cette première version » et « Protocole de test » ci-dessous décrivent les premières
> versions (avec la barre d'outils) et sont gardées pour mémoire.

> Travail à plusieurs, branches et GitHub : voir [docs/GUIDE-GIT.md](docs/GUIDE-GIT.md).

## But de cette première version

1. **Vérifier que le curseur piloté par le logiciel de l'eyetracker est un bon substitut du regard** :
   cadence, régularité, couverture de tout l'écran.
2. Offrir trois actions que ce logiciel ne propose pas :
   - **Appui long** (bouton gauche maintenu 1,5 s) ;
   - **Glisser-déposer avec le bouton gauche** ;
   - **Glisser-déposer avec le bouton droit**.

## Installation

**Prérequis : un eyetracker compatible (Tobii) ; téléchargez et installez son pilote officiel sur le
site du constructeur.**

- Visual Studio 2026 avec la charge de travail « Développement d'applications de bureau .NET »
  et le kit de développement .NET Framework 4.8.
- Le moteur de l'eyetracker doit tourner (processus tobii.service / tobii interaction engine), comme pour Mill Mouse.
- Ouvrir `GazeBar.slnx`, puis F5.

Ce code n'a pas pu être compilé par son auteur : si Visual Studio signale une erreur,
envoyez-en le texte, elle sera corrigée.

### Les DLL de l'eyetracker ne sont pas livrées avec GazeBar

Le regard est lu par trois DLL de Tobii (`Tobii.Interaction.Net.dll`, `Tobii.Interaction.Model.dll`
et `Tobii.EyeX.Client.dll`). C'est du code propriétaire de Tobii AB : GazeBar ne les contient pas
et ne les distribue pas. Le paquet NuGet `Tobii.Interaction` sert seulement à compiler ; ses DLL
ne sont pas copiées à côté de `GazeBar.exe` (et sont effacées du dossier de sortie si elles s'y
trouvaient).

Au démarrage, GazeBar les cherche lui-même sur le PC (`EyetrackerLoader`), dans cet ordre :

1. le sous-dossier `Eyetracker` à côté de `GazeBar.exe` ;
2. les dossiers d'installation des logiciels du fabricant (`Program Files\Tobii`, `Program Files\Tobii Dynavox`) ;
3. le cache NuGet de Visual Studio (`%USERPROFILE%\.nuget\packages\tobii.interaction\0.7.3`) :
   sur le PC où l'on compile GazeBar, il n'y a donc rien à faire.

Sur un autre PC, si elles sont introuvables, la fenêtre Diagnostic l'indique. Il faut alors les
récupérer soi-même, en acceptant la licence de Tobii : télécharger le paquet `Tobii.Interaction`
version 0.7.3 sur nuget.org (« Download package »), renommer le fichier `.nupkg` en `.zip`,
l'ouvrir, et copier dans le sous-dossier `Eyetracker` de GazeBar les deux DLL de `lib\net45` et
celle de `build\x86`. Le journal (`gazebar.log`) note où chaque DLL a été trouvée.

## Principe

Le regard réel est lu par le moteur de l'eyetracker (paquet NuGet Tobii.Interaction, classe GazeSource) :
~30 mesures/s, y compris hors de l'écran (coordonnées négatives ou supérieures à la taille de l'écran).
La barre et la fenêtre Diagnostic l'affichent en vert, à côté du curseur piloté par le logiciel de l'eyetracker.

Pour les boutons : le logiciel de l'eyetracker déplace le curseur de Windows avec le regard. GazeBar lit
la position de ce curseur (`GetCursorPos`) et injecte seulement des appuis/relâchements de boutons
(`SendInput`), jamais de déplacement. Pendant qu'un bouton est maintenu, ce logiciel continue
de déplacer le curseur : c'est ce qui produit le glisser.

La barre ne prend jamais le focus, pour ne pas perturber la fenêtre où l'on agit.

## Protocole de test

1. Lancer GazeBar. La barre apparaît en haut de l'écran avec « Curseur : X … Y … | N changements/s ».
   Regardez différentes zones de l'écran : les valeurs doivent suivre le regard, y compris dans les coins.
2. Bouton **Diagnostic** (onglet « Outils » des paramètres) : parcourez les coins, les bords et le centre
   du regard ; la vignette montre le curseur et le regard en direct. GazeBar n'enregistre jamais les
   données du regard sur le disque (la licence de l'eyetracker l'interdit) : ni fichier de positions,
   ni coordonnées dans le journal.
3. **Appui long** : cliquez le bouton (3 s de compte à rebours), regardez l'endroit voulu.
   Le bouton gauche est appuyé à la fin du compte à rebours, puis relâché 1,5 s plus tard.
4. **Glisser (clic droit)** : par exemple dans l'Explorateur sur un fichier, le menu de
   déplacement apparaît au relâchement. **Glisser (gauche)** : par exemple dans Paint.
5. **Souris regard** (bouton à bascule) : le curseur suit directement le regard, lissé. Regarder hors de
   l'écran met en pause. Réglages `Smoothing` et `CursorDeadZone` dans `settings.json`. Avec ce mode, les
   glisser-déposer suivent le vrai regard.
6. **Mode mouvement** (bouton à bascule, activé au démarrage) : le cercle suit le regard, la
   fixation fige le cercle et place le curseur sur le point fixé (voir plus bas).
   **Suivi souris** (bouton à bascule, désactivé par défaut) : le curseur suit aussi le cercle ;
   sinon, il ne rejoint le regard qu'une fois celui-ci fixé. Ce réglage est enregistré.
7. **Annuler** relâche tout immédiatement. **Quitter** ferme le programme.

## Réglages

Tous les réglages sont dans la classe `AppSettings` (fichier `AppSettings.cs`) et enregistrés dans
`%APPDATA%\GazeBar\settings.json`, créé au premier lancement avec les valeurs par défaut du
cahier des charges. On peut l'ouvrir avec le Bloc-notes (nombres décimaux avec un point : `0.3`),
puis relancer GazeBar. Un fichier illisible est mis de côté (`settings.json.unreadable`) et
remplacé par les valeurs par défaut. Les anciens fichiers `HoldSession.cs` (appui long, compte à
rebours de 3 s) et `GazeMouse.cs` ont été supprimés à l'étape 6, ainsi que les réglages qui ne
servaient plus (`DureeAppuiLong`, `DelaiDepot`, `DistanceMinGlisser`, `DureeMaxAppui`).

Depuis l'étape 11 (`ScreenMetrics.cs`), les tailles sont en **% de la hauteur de l'écran** et non plus en
pixels : `CircleRadius` 8.8 (190 px en 4K, 95 px en 1920 × 1080), `IconSize` 5.6,
`FixationRadius` 1.4, `CursorDeadZone` 0.4, `MaxFixationSpeed` 42 (%/s). Un fichier plus
ancien (version 6) est converti une fois, en divisant les pixels par 21.6 (valeurs réglées sur
un écran de 2160 px de haut). `InterfaceSize` (75, 100, 125 ou 150 %, onglet « Affichage »)
agrandit ou réduit tout ce qui est dessiné, sauf les réglages du regard. Les textes du calque
suivent aussi la taille du texte de Windows (Accessibilité). Les changements de résolution, de
mise à l'échelle et de taille du texte sont détectés pendant que GazeBar tourne.

**Profils de réglages** (étape 15, `SettingsProfiles.cs`) : onglet « Profils » des paramètres
(catégorie Système). « Enregistrer les réglages actuels » ouvre une page Nom : une zone de texte
contenant déjà un nom daté (`Réglages AAAA-MM-JJ HHhMM`) que l'on garde ou remplace avec le clavier
du regard, affiché pour cette saisie ; « Enregistrer » (ou Entrée) copie les réglages dans
`Documents\GazeBar\Profiles\<nom>.json` (si le nom existe, une question demande avant de le
remplacer), « Annuler » n'enregistre rien. L'aidant peut renommer ou supprimer ces fichiers dans
l'Explorateur. « Charger un profil » montre les fichiers `.json` de ce
dossier en grands boutons (le plus récent en premier, 12 par page) ; après « Oui », les réglages
actuels sont gardés dans « Sauvegarde automatique » (fichier `auto-backup.json`, remplacé à
chaque chargement, pour revenir en arrière au regard), puis le profil est appliqué tout de suite et
enregistré dans `settings.json`. Un profil d'une ancienne version est converti comme `settings.json`
au démarrage. « Démarrer avec Windows » ne change pas (réglage de l'ordinateur), ni la langue de
l'interface, ni le dictionnaire personnel.

**Langue de l'interface** (étape langues-socle, `Texts.cs`) : tous les textes affichés sont dans
des fichiers de langue, dossier `Languages` copié à côté de GazeBar.exe (`Languages/en.json`,
`Languages/fr.json`). Chaque texte a une clé (`"menu.settings": "Paramètres"`) ; `{0}`, `{1}`
sont remplacés par des valeurs. L'anglais est la langue de référence (étape langue-anglais) : c'est
la langue d'une nouvelle installation, et un texte absent de la langue choisie est pris en anglais
(la clé manquante est notée dans le journal) ; un ancien fichier de réglages (avant la version 12)
garde le français. Le bouton « Langue » (onglet Général) passe à la langue suivante du dossier
et redessine la page tout de suite ; le choix est le réglage `Language` (`settings.json` version 12).
Pour ajouter une langue : copier `en.json` en `xx.json`, traduire les valeurs (jamais les clés).

## Architecture des modes

- `OverlayWindow` : le « calque », une fenêtre transparente qui couvre tout l'écran, toujours au
  premier plan, que les clics traversent et qui ne prend jamais le focus. Il dessine le cercle,
  l'anneau de progression et l'icône d'options.
- `Modes/ModeManager` : connaît le mode courant et fait passer d'un mode à l'autre ; environ
  60 fois par seconde, il donne la position du regard (`GazeState`) au mode courant.
- `Modes/Mode` : modèle commun à tous les modes (`Enter`, `Update`, `Quit`).
- `Modes/MovementMode` (étape 2, mode par défaut) : un cercle lissé suit le regard ; avec
  **Suivi souris**, le curseur le suit aussi. Quand le regard reste immobile pendant
  `FixationDelay`, on passe en regard fixe (sans anneau de progression : l'anneau est réservé
  aux boutons).
- Chaque mode reçoit la position du regard dans `Enter(gaze)` pour s'initialiser, et reprend
  le cercle là où il est dessiné : pas de saut d'un mode à l'autre, pas de place par défaut.
- `Modes/FixationDetector` : reconnaît une fixation. Une mesure est immobile si sa vitesse reste
  sous `MaxFixationSpeed` (pixels par seconde) ET si elle reste à moins de `FixationRadius` du
  point d'ancrage (la moyenne des mesures de la fixation). Il faut `FixationDelay` secondes
  d'immobilité, mesurées en vrai temps. Une mesure aberrante isolée est ignorée.
- `Modes/FixedGazeMode` : le cercle se fige (bord blanc) avec une icône d'options collée du côté
  du centre de l'écran, et le curseur se place sur le point fixé. Si `DirectClick` est activé
  (non par défaut, à mettre à `true` dans `settings.json`), regarder dans le cercle pendant
  `DirectClickDelay` fait un clic gauche ; regarder l'icône pendant `OptionsIconDelay` (0,3 s,
  plus court que les autres boutons ; l'icône s'allume d'un léger flash vert) ouvre le choix d'action (ou d'abord la loupe si
  `MagnifierBeforeAction` est à `true` dans `settings.json`) ; sortir du
  cercle et de l'icône ramène au mode mouvement. Après un clic, une nouvelle fixation au même
  endroit n'est pas reconnue tant que le regard n'en est pas sorti (pas de clics en rafale).
- `Modes/ActionChoiceMode` (étape 3) : la zone autour du curseur (carré de 2 × `CircleRadius`) est
  photographiée (`ScreenCapture`), puis affichée agrandie ×2 au centre d'un écran noir, avec une
  croix à la place du curseur. Autour, une couronne de huit emplacements, deux par côté de la
  photo : double clic et clic gauche (en haut), clic droit et glisser-déposer gauche (à droite),
  glisser-déposer droit et loupe (en bas), glisser-déposer android et défilement (à gauche, étape 8). Sur le bord gauche de l'écran, les trois modificateurs
  Ctrl, Maj, Alt (étape 6), à 60 px du bord et 60 px les uns des autres.
  Un emplacement vide serait dessiné grisé avec un « + », pour montrer qu'on pourra le configurer. À droite, une icône « retour ». Un petit cercle (moitié de
  `CircleRadius`), par-dessus tout, montre où est le regard. Regarder une icône pendant
  `ActivationDelay` l'active (anneau de progression). Avant le clic, l'écran noir est caché et
  l'on attend 60 ms qu'il ait disparu ; puis retour au mode mouvement.
- `Modes/MagnifierMode` (étape 5) : affine la position du curseur. La même zone est photographiée,
  puis affichée agrandie ×`MagnifierZoom` (×4, soit 800 × 800 px) au centre d'un écran noir ; un
  petit cercle de 50 px suit le regard. Quand le regard se fixe sur la photo (même détection
  qu'en mode mouvement), le point est ramené en coordonnées réelles (écart divisé par 4), le
  curseur y est placé et le choix d'action s'ouvre sur ce point. Une erreur de 30 px du regard
  ne vaut plus que 7 à 8 px réels. L'icône « retour » (à droite) revient sans rien faire au mode
  d'où l'on vient : le choix d'action, ou le mode mouvement si l'on vient du regard fixe.
  À l'ouverture, une fixation à l'endroit où l'on regardait déjà est ignorée tant que le regard
  n'en est pas sorti.
- Modificateurs (étape 6, `Modifiers`, `KeyboardInjector`) : dans le choix d'action, un
  regard sur « Ctrl », « Maj » ou « Alt » l'allume (bord vert), un second l'éteint. Les touches
  allumées sont enfoncées juste avant l'action et relâchées juste après (après le dépôt pour un
  glisser). Elles s'éteignent après chaque action, et à chaque retour au mode mouvement.
- Glisser-déposer (étape 6) : depuis la couronne du choix d'action, en trois temps.
  1. `Modes/DragTargetMode` : le cercle suit le regard, une ligne blanche en pointillés part
     du point de départ ; une fixation choisit l'arrivée.
  2. `Modes/MagnifierMode` : la loupe affine l'arrivée.
  3. `Modes/DragExecutionMode` : bouton enfoncé au départ, curseur déplacé jusqu'à l'arrivée
     en `DragDuration` (0,5 s), bouton relâché. Pour le glisser-déposer **android**, une pause
     de `AndroidPauseDuration` (1 s) sépare l'appui du déplacement (il remplace l'appui long).
  Pendant les temps 1 et 2, un bouton « retour » au bord droit de l'écran (centré verticalement)
  annule sans rien enfoncer. `Modes/DragGesture` décrit le glisser en cours (départ, bouton, pause).
- `GazeButton` : un bouton activable au regard (symbole de la police Windows, pastille,
  anneau, libellé au survol), dessiné sur le calque. Il peut être vide (« + » grisé),
  indisponible (son symbole, grisé) ou allumé (bord vert, pour un interrupteur).
- `ButtonAimer` (étape 4) : décide quel bouton est regardé et quand il s'active ; partagé par
  le choix d'action et le menu.
- `Modes/MenuMode` (étape 4) : regarder en bas, hors de l'écran, pendant `MenuDelay` (0,5 s)
  ouvre une barre centrée en bas de l'écran, uniquement depuis les modes mouvement et pause
  (et plus tard jeu, depuis l'étape 6 ; c'est le `ModeManager` qui surveille). La loupe, le
  choix d'action et le glisser-déposer ont leur bouton « retour ». Boutons : Paramètres, Jeu, Clavier
  (le clavier au regard, étape 12), Pause (ou Reprendre si l'on vient de la pause), Suivi souris (interrupteur, bord vert
  quand il est activé), Quitter GazeBar (avec confirmation « Oui » / « Non »).
  Regarder hors de la barre pendant `MenuCloseDelay` (1 s) la ferme et ramène au mode
  d'avant. Un petit cercle montre où est le regard.
- `Modes/SettingsMode` et `SettingsWindow` (étape 7) : bouton « Paramètres » du menu. Une
  fenêtre plein écran, fond noir, avec à gauche quatre catégories dépliantes (02/11/2026) : Regard (Regard,
  Fixation, Délais), Actions (Loupe et glisser, Défilement, Icônes), Affichage (Affichage, Clavier,
  Jeu) et Système (Général, Profils, Outils). Seule la catégorie ouverte montre ses onglets, en dessous d'elle
  et en retrait ; regarder une autre catégorie l'ouvre sur son premier onglet (liste dans
  `SettingsCatalog`). Chaque réglage : son nom, une
  explication, puis « − », la valeur, « + » (ou un interrupteur arrondi pour un oui / non : le
  regarder l'inverse). Garder le regard sur « − » ou
  « + » répète le changement toutes les 0,4 s. « Défaut » (en haut à droite) remet les valeurs de
  l'onglet affiché, après confirmation ; « Retour » (au milieu du bord droit) ou Échap ramène au
  mode mouvement. Les nombres s'affichent avec un point (0.16). L'aidant peut cliquer sur une
  valeur pour la taper (Entrée valide, Échap annule ; point ou virgule acceptés). Chaque
  changement est appliqué tout de suite et enregistré. Contrairement au calque, cette fenêtre
  accepte la souris et le clavier. `IGazeTarget` permet au `ButtonAimer` de viser ses cibles.
- Instance unique (étape 7, `App`) : si GazeBar tourne déjà, un deuxième lancement se ferme
  aussitôt, sans rien afficher, et le note dans le journal.
- `WindowsStartup` (étape 7) : l'option `StartWithWindows` (onglet Général) inscrit GazeBar
  dans la liste de démarrage de Windows (registre, `HKEY_CURRENT_USER\...\Run`), sans droits
  d'administrateur : les fenêtres administrateur continuent d'ignorer ses clics.
- `Modes/ScrollMode` (étape 8) : action « Défilement » de la couronne. Le cercle blanc reste
  fixé sur le curseur. Regard dans le cercle : rien ne défile (zone morte). Regard au-dessus, en
  dessous, à droite ou à gauche : la fenêtre sous le curseur défile dans ce sens (molette Windows,
  un seul sens à la fois), d'autant plus vite que le regard est loin du cercle, jusqu'à
  `MaxScrollSpeed` (5 crans/s par défaut depuis le 02/11/2026) trois `CircleRadius` au-delà du bord. Un petit arc vert sur le
  cercle montre la direction. Rien ne défile avant un premier passage du regard dans le cercle,
  ni quand le regard est perdu, à plus de 200 px hors de l'écran (un regard un peu hors de
  l'écran compte, pour agrandir la plage de vitesse) ou sur le bouton « retour » (bord droit, centré),
  qui ramène au mode mouvement. Ctrl actif dans le choix d'action : Ctrl + molette (zoom).
  Depuis le 02/11/2026 : défilement fin (petites quantités de molette, jusqu'à une fois par
  image, et vitesse lissée qui monte et descend en douceur) ; en entrant, GazeBar rend active la
  fenêtre sous le curseur (sans cliquer), pour ne plus avoir à cliquer avant de défiler.
  Onglet « Défilement » : option « Défiler les fenêtres inactives » (oui par défaut), qui
  règle l'option Windows du même nom (`WheelScroller`, appliquée au démarrage et à chaque changement).
- `Modes/IconConfigMode` (étape 10) : onglet « Icônes » des paramètres, bouton « Configurer les
  icônes ». Même mise en page que le choix d'action (`ActionChoiceLayout`, partagée par les
  deux modes), avec un cadre gris à la place de la photo. Les quatorze emplacements (huit de la
  couronne, trois modificateurs à gauche, Copier / Couper / Coller en haut) portent une pastille :
  « − » rouge sur une icône assignée (la regarder retire l'action), « + » verte sur un emplacement
  vide (la regarder ouvre une liste, dans un rectangle aux bords arrondis des trois quarts de
  l'écran, avec toutes les actions du même groupe et « Annuler » ; une action peut être placée
  plusieurs fois). Chaque changement
  est enregistré dans `settings.json` (listes `CrownIcons`, `ModifierIcons`,
  `UtilityIcons`, version 6 ; "" = emplacement vide) ; « Défaut » dans l'onglet « Icônes »
  remet la disposition d'origine. Dans le choix d'action, un emplacement vide est grisé et ne
  fait rien. « Retour » ou Échap ramène à l'onglet « Icônes ». Liste des actions : `ActionCatalog`.
- `Modes/PauseMode` (étape 4) : plus rien ne suit le regard ; seul le menu peut encore s'ouvrir
  (bouton « Reprendre »).
- `EyetrackerLoader` (02/11/2026) : trouve et charge au démarrage les DLL de l'eyetracker
  (Tobii.Interaction), qui ne sont pas livrées avec GazeBar (voir « Installation »). Les DLL .NET sont fournies à .NET par l'événement
  `AssemblyResolve` ; la DLL native est chargée à l'avance par `LoadLibrary`, en 32 bits.
  `GazeSource` n'utilise les types de Tobii.Interaction que dans `ConnectEyetracker`, pour que GazeBar démarre
  même sans ces DLL.
- `Modes/CalibrationMode` et `Calibration` (étape 4) : bouton « Calibration » de l'onglet
  « Outils » des paramètres (avec « Diagnostic » ; tous deux étaient dans la barre de menu avant
  le 29/10/2026). Lancent l'outil de calibration de l'eyetracker
  et attendent la fermeture de sa fenêtre pour revenir au mode mouvement. Le nom exact de la
  commande n'a pas pu être vérifié : le journal note ce qui a été trouvé et essayé.
- `KeyboardHook` (étape 4) : la touche **Échap** ramène au mode mouvement depuis n'importe quel
  mode, pour l'aidant. La touche arrive aussi normalement au programme actif. Depuis l'étape 12,
  en mode mouvement, elle ferme le clavier au regard ; la touche « Esc » du clavier au regard,
  simulée par GazeBar, est ignorée par le crochet (elle va seulement à l'application).
- Clavier au regard (étape 12, `GazeKeyboard`, `KeyboardKey`, `KeyboardLayout`) : bouton
  « Clavier » du menu, ou action « Clavier » du choix d'action (ouvre / ferme le clavier ; pas
  placée par défaut : on la met à la place d'une autre dans l'écran de configuration des icônes). Un clavier sur la moitié basse de l'écran (`KeyboardHeight`), par-dessus
  tout, sans réserver de place ni prendre le focus. Ses cinq pages (lettres AZERTY, accents,
  123.!, symboles et navigation, F1 à F12) viennent du fichier `Keyboards/fr-azerty.json`, relu à
  chaque ouverture : on peut changer une touche sans recompiler (format expliqué dans
  `KeyboardLayout.cs`). Regard sur le clavier : le curseur ne bouge pas, le cercle est
  masqué, la touche regardée s'éclaircit (avec un fin contour bleu clair qui apparaît en fondu ;
  une seule mesure du regard suffit, contre deux pour les autres boutons) puis flashe en vert et agit après `KeyDelay`
  (0,6 s) ; en gardant le regard dessus, elle se retape (`KeyRepeat`). Regard ailleurs :
  mode mouvement normal ; le clavier est masqué pendant la loupe, le choix d'action, les
  paramètres, la pause… et revient ensuite. Texte envoyé en Unicode (les accents marchent quelle
  que soit la disposition de Windows), touches spéciales et raccourcis en vraies touches. Maj
  (une lettre), Caps Lock, Ctrl, Alt, Win (pour la touche suivante) ; majuscule automatique après
  « . », « ? », « ! » (`AutoCapitalize`). « ⌫ ABC » efface le mot en cours (ou Ctrl + Retour
  arrière). Barre de menu du clavier (regard sous l'écran, qui remplace alors la barre
  classique) : Haut / bas (clavier en haut de l'écran, position retenue : `KeyboardAtTop`),
  Taille (100, 75 ou 50 %, largeur et hauteur ensemble, clavier centré : `KeyboardSize`, aussi
  dans les paramètres), Pause (le bouton Fermer de cette barre a été retiré à l'étape 14 : il
  doublait celui au-dessus du clavier). Quand elle s'ouvre, le clavier en bas remonte au-dessus
  d'elle en glissant (et redescend de même). Fermeture : bouton à droite au-dessus du clavier (en dessous s'il est en haut), barre
  du clavier, ou Échap. Tab est une vraie tabulation : sa largeur (2 ou 4 espaces) se règle
  dans le logiciel où l'on écrit. Le texte tapé n'est jamais écrit dans le journal.
  Réglages : onglet « Clavier » des paramètres (`settings.json` version 9).
- Prédiction de mots (étape 13, `WordPredictor`) : les trois cases du haut du clavier
  proposent les trois mots les plus probables qui commencent par le mot en cours (lettres tapées
  depuis la dernière espace, ponctuation, Entrée, Tab ou flèche), sans tenir compte des accents
  ni des majuscules (« ete » propose « été »). Si ce qui est tapé est déjà un mot connu, il est
  en 1re case : le regarder écrit juste l'espace après lui. En regarder une écrit le mot entier suivi d'une
  espace (pas d'espace après « l' », « qu' »…) ; le mot garde la majuscule de départ. Classement :
  fréquence dans Lexique + 20 par utilisation. Un mot inconnu de 2 lettres ou plus s'affiche dans
  la 1re case avec un « + » (en ambre) : la regarder l'ajoute au dictionnaire personnel. Fichiers :
  `Dictionaries/fr.txt` (environ 125 000 mots tirés de Lexique 3.83, copié à côté de
  GazeBar.exe, lu en arrière-plan au démarrage) et `%APPDATA%\GazeBar\personal-dictionary.json`
  (mots ajoutés et nombres d'utilisations ; jamais dans le dépôt), enregistré après chaque ajout
  et, pour les utilisations, une fois par minute et à la fermeture. Mot suivant : GazeBar retient les
  paires de mots écrits l'un après l'autre (« je » → « suis ») ; juste après une espace, les
  cases proposent les mots qui ont le plus souvent suivi le mot d'avant, et pendant la frappe
  ces mots passent devant (+ 200 par fois). Lexique n'a pas de paires : ces propositions
  apparaissent au fil des phrases écrites. Une ponctuation ou une fin de ligne coupe le lien.
- Clavier anglais (étape clavier-anglais) : `Keyboards/en-qwerty.json` (« English QWERTY »,
  lettres QWERTY, page d'accents É,Ü,Ñ) et son dictionnaire `Dictionaries/en.txt` (environ
  62 000 mots tirés de FrequencyWords, sous-titres OpenSubtitles 2018, filtrés par le
  dictionnaire orthographique Hunspell en_US ; les contractions « don't », « I'm »… ont une
  fréquence estimée). Le dictionnaire suit la langue de l'INTERFACE (bouton « Langue » des
  paramètres, demande du 2026-11-02), pas la disposition : le bouton « Disposition du clavier »
  change seulement les touches. Un dictionnaire dont la première ligne est `#apostropheInWord`
  (anglais) garde l'apostrophe après une lettre dans le mot (« don' » propose « don't ») ; sinon
  (français) l'apostrophe termine le mot élidé (« l' »). Une nouvelle installation démarre avec le
  clavier anglais ; un ancien fichier de réglages (avant la version 10) garde le clavier français.
- Options du clavier (étape 14, `KeyboardListMode`) : deux boutons de plus dans la barre du
  clavier, et un bouton « Mes mots » dans l'onglet Général des paramètres. « Zone de saisie » (interrupteur, bord vert, `InputBox`) : une bande au-dessus des
  touches montre les derniers mots tapés (simple aperçu : le texte part toujours dans le
  logiciel ; elle se vide quand le curseur de texte a pu bouger : flèche, Tab, raccourci, clic
  ailleurs). « Disposition du clavier » (appelé « Langue » avant l'étape
  clavier-anglais) : liste des dispositions du dossier `Keyboards` (bord vert = celle utilisée) ;
  le choix est retenu (`KeyboardLayout`). Le dictionnaire, lui, suit la langue de l'interface. « Mes mots » (onglet Général des paramètres) : liste des mots
  ajoutés avec « + », par pages de 12 ; regarder un mot le retire du dictionnaire personnel (avec ses utilisations et ses paires).
  `settings.json` version 10.
- Le calque se remet au-dessus de toutes les fenêtres deux fois par seconde.
- `app.manifest` : déclare GazeBar « sensible au DPI », pour que le regard, le curseur et le
  dessin utilisent les mêmes pixels, même avec une mise à l'échelle de Windows à 125 % ou 150 %.

Test de l'étape 2 : on fixe un bouton, le cercle se fige (bord blanc, icône d'options) et le
curseur se place sur le bouton ; on regarde ailleurs, il repart. Avec `DirectClick` à `true`, en
continuant à fixer le bouton, un anneau se remplit puis un clic gauche est fait. Le journal note chaque passage en regard fixe et
chaque clic direct.

Test de l'étape 3 : on fixe un fichier dans l'Explorateur, on regarde l'icône d'options, l'écran
noir montre la zone agrandie ; on regarde « double clic » : l'écran noir disparaît et le fichier
s'ouvre, sans toucher la souris.

Test de l'étape 4 : on regarde en bas, hors de l'écran : la barre de menu apparaît. On regarde
« Pause » : tout s'arrête. On regarde de nouveau en bas, puis « Reprendre » : le cercle revient.
Le tout uniquement au regard. Échap ramène aussi au mode mouvement.

Test de l'étape 7 : menu, « Paramètres » : l'écran noir s'ouvre sur l'onglet Regard. On garde
le regard sur « + » du lissage : la valeur monte d'un pas toutes les 0,4 s. On regarde l'onglet
Fixation, puis l'interrupteur du clic direct : il passe au bleu, rond à droite, « Oui ». « Défaut », puis « Oui » :
les valeurs de l'onglet reviennent. L'aidant clique sur une valeur, tape 0,5 puis Entrée.
« Retour » ramène au mode mouvement. Lancer GazeBar une deuxième fois ne fait rien (voir le
journal).

Test de l'étape 10 : menu, « Paramètres », onglet « Icônes », « Configurer les icônes ». On
regarde le « − » du clic gauche (en haut à gauche de la couronne) puis celui du clic droit : les
deux emplacements deviennent vides (« + »). On regarde le premier : la liste propose toutes les
actions de la couronne ; on choisit « Clic droit ». On fait de même avec l'autre emplacement et « Clic
gauche » : les deux actions ont échangé leur place. « Retour », puis dans le choix d'action, les
deux icônes sont à leur nouvelle place. Retirer « Couper » laisse un emplacement grisé en haut
du choix d'action. « Défaut » dans l'onglet « Icônes » remet tout comme avant.

Test de l'étape 8 : on ouvre une longue page web, on fixe le texte, on ouvre le choix d'action
puis « Défilement » (à gauche, en haut). Le cercle blanc reste en place. On regarde dans le
cercle : rien ne bouge. On regarde un peu sous le cercle : la page descend doucement ; plus bas :
plus vite. Au-dessus : elle remonte. « Retour » au bord droit ramène au mode mouvement.
Depuis le 02/11/2026 : à vitesse lente, la page doit glisser sans petits sauts, et démarrer
ou s'arrêter en douceur ; sans avoir cliqué dans la page avant, elle doit défiler quand même
(le journal note « fenêtre … rendue active » et l'état du réglage Windows de la molette).

Test de l'étape 11 : écran en 1920 × 1080 à 100 %. Au premier lancement, le journal note
« mise à jour en version 7 » et l'écran détecté ; le cercle, les icônes et la barre de menu ont
à l'œil la même taille qu'avant en 4K. Onglet « Affichage » : « Écran détecté » donne la
résolution, l'échelle et la taille du texte ; « Taille de l'interface » à 150 % agrandit le
cercle, les icônes et le menu. Pendant que GazeBar tourne, on change la résolution, puis la
mise à l'échelle (125 %), puis la taille du texte de Windows : à chaque fois, le journal note
« changement détecté » et les tailles s'adaptent (un choix d'action ou une loupe ouverts se
ferment, retour au mode mouvement ; la pause, le jeu et les paramètres restent ouverts).

Test de l'étape 5 : on fixe une petite case à cocher, on regarde l'icône d'options, puis
« Loupe » dans la couronne : la zone apparaît agrandie ×4. On fixe la case sur la photo : le
choix d'action s'ouvre, la croix est sur la case ; « clic gauche » la coche. Avec
`"MagnifierBeforeAction": true` dans `settings.json`, la loupe s'ouvre directement après l'icône
d'options.

Test de l'étape 6 : dans l'Explorateur, on fixe un fichier, on ouvre le choix d'action, on
regarde « Ctrl » (bord vert) puis « clic gauche » ; on recommence sur un autre fichier : les deux
sont sélectionnés. Puis on fixe un fichier, on choisit « glisser-déposer gauche » (quatre flèches,
pastille G), on regarde le dossier d'arrivée (la ligne en pointillés suit), on le fixe, on affine
dans la loupe : le fichier est déplacé. Le bouton « retour » à droite annule.

Test de l'étape 12 : dans le Bloc-notes, on ouvre le menu, « Clavier » : le clavier apparaît en
bas. On regarde des touches : elles s'éclaircissent, flashent et le texte s'écrit (« é » depuis
la page « À,È,Ê »). On regarde au-dessus du clavier : le cercle revient et le regard fixe marche.
On regarde sous l'écran : la barre du clavier s'ouvre et le clavier remonte au-dessus d'elle ;
« Haut / bas » le met en haut (il s'y rouvre la fois suivante), « Taille » passe à 75 % puis 50 %,
« Pause » le cache jusqu'à la reprise. Dans la configuration des icônes, on place « Clavier » sur la couronne : dans le choix
d'action, ce bouton ouvre le clavier.

Test de l'étape 13 : dans le Bloc-notes, on ouvre le clavier et on tape « bo » : les cases
proposent « bon », « bonne »… ; on regarde « bonjour » (après « bonj ») : « bonjour » et une espace
s'écrivent. « ete » propose « été ». On tape un mot inventé (« zorglub ») : la 1re case montre
« + zorglub » en ambre ; on la regarde : une espace s'écrit, et en tapant « zo » le mot est
proposé. On écrit deux fois « je suis » (en tapant « suis » en entier puis une espace) : après
« je » et une espace, la 1re case propose « suis ». Après avoir fermé GazeBar, `%APPDATA%\GazeBar\personal-dictionary.json` contient le mot.

Test de l'étape 14 : clavier ouvert, on regarde sous l'écran : la barre du clavier montre aussi
« Zone de saisie » et « Disposition du clavier ». « Zone de saisie » : le bord devient vert, une
bande apparaît au-dessus des touches ; on tape « bonjour à tous » : la bande le montre ; une
flèche la vide. « Disposition du clavier » : la liste montre « Français AZERTY » avec un bord vert ; « Retour au
clavier » ramène au clavier. Paramètres, onglet Général, « Mes mots » : « zorglub » (ajouté à
l'étape 13) est dans la liste ; on le regarde : il disparaît ; « Retour aux paramètres » ramène à
l'onglet Général. En tapant « zo », il n'est plus proposé.

Test de l'étape clavier-anglais : clavier ouvert, « Disposition du clavier » : la liste montre
« English QWERTY » et « Français AZERTY » ; on choisit « English QWERTY » : lettres en QWERTY, et
avec l'interface en français, « bonj » propose toujours « bonjour ». Paramètres, Général,
« Langue » → English ; retour au clavier : « th » propose « the », « that », « this » (ou proches),
« don » et « don' » proposent « don't ».

Test de l'étape 15 : Paramètres, Système, Profils, « Enregistrer les réglages actuels » : le
clavier du regard apparaît avec la page Nom ; écrire « Soir », regarder « Enregistrer » : le
message donne le nom du profil, et le fichier apparaît dans `Documents\GazeBar\Profiles`. On
change le rayon du cercle (Affichage). Profils, « Charger un profil » : la liste montre le profil ;
on le regarde : « Charger le profil … ? » ; « Non » ne change rien ; « Oui » remet l'ancien rayon et
crée « Sauvegarde automatique ». Charger « Sauvegarde automatique » remet le rayon modifié.

## Limites connues

- Une fenêtre lancée « en tant qu'administrateur » ignore les clics injectés et les touches du
  clavier au regard, sauf si GazeBar est lui aussi lancé en administrateur.
- Le mode jeu (regard vers déplacement relatif de la souris) n'est pas encore étudié.
- Journal : `%LOCALAPPDATA%\GazeBar\gazebar.log`.

## Licence

GazeBar est distribué sous licence MIT : chacun peut l'utiliser, le modifier et le redistribuer
librement, à condition de conserver la mention de copyright. Le logiciel est fourni sans garantie.
Texte complet : [LICENSE.txt](LICENSE.txt).

Exceptions : le dictionnaire `Dictionaries/fr.txt` est tiré de [Lexique 3.83](http://www.lexique.org)
(B. New, C. Pallier et coll.) et reste sous licence CC BY-SA 4.0 : voir
[Dictionaries/LICENSE-Lexique.txt](Dictionaries/LICENSE-Lexique.txt). Le dictionnaire
`Dictionaries/en.txt` est tiré de [FrequencyWords](https://github.com/hermitdave/FrequencyWords)
(H. Dave, d'après OpenSubtitles 2018) et reste sous licence CC BY-SA 4.0 : voir
[Dictionaries/LICENSE-FrequencyWords.txt](Dictionaries/LICENSE-FrequencyWords.txt).

## Mentions légales

Ce projet est un logiciel indépendant et open source. Il n'est ni affilié, ni approuvé, ni soutenu
par Tobii Dynavox. TD Control, Tobii et Dynavox sont des marques déposées de Tobii AB.

Les DLL de Tobii (Tobii Core SDK) ne font pas partie de GazeBar et ne sont pas distribuées avec
lui ; elles restent la propriété de Tobii AB et sont soumises à leur propre licence (« Tobii Core
SDK and Gaming SDK License Agreement »), que chacun accepte en les téléchargeant.
