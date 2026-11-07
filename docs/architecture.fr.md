# Architecture de Prunel

*English version: [architecture.md](architecture.md).*

*Pour les développeurs. Utilisateurs : voir le [guide d'utilisation](user-guide.fr.md). Règles
pour contribuer : [CONTRIBUTING.fr.md](../CONTRIBUTING.fr.md).*

Les « étape N » et noms d'étapes ci-dessous renvoient à la feuille de route du cahier des
charges (l'ordre dans lequel les fonctions ont été ajoutées). Ils aident à retrouver la pull
request correspondante dans l'historique. Les commentaires du code ne les utilisent pas : ils
décrivent le code tel qu'il est.

## Fonctionnement

- **Regard** : les modes lisent le regard par l'interface `IGazeSource`, que toute source du
  regard fournit (`GazeSource` contient la partie commune : horloge, dernière position, cadence).
  La source normale, `EyetrackerGazeSource`, lit le regard dans le moteur de l'eyetracker (paquet NuGet
  `Tobii.Interaction` 0.7.3), environ 30 mesures par seconde, en pixels de l'écran, y compris un
  peu hors de l'écran (coordonnées négatives ou plus grandes que l'écran). Le logiciel de commande
  au regard fourni avec l'eyetracker (TD Control) est mis en pause pendant que Prunel tourne.
- **Dessin** : `OverlayWindow` est une fenêtre transparente qui couvre tout l'écran, toujours au
  premier plan. Les clics la traversent et elle ne prend jamais le focus : la fenêtre utilisée
  n'est jamais dérangée.
- **Action** : Prunel déplace lui-même le curseur de Windows et injecte les clics, la molette
  et les touches (`SendInput` : `MouseInjector`, `WheelScroller`, `KeyboardInjector`).
- **Modes** : `Modes/ModeManager` connaît le mode courant et lui donne le regard environ 60 fois
  par seconde. Chaque mode est une classe du dossier `Modes` (liste plus bas).
- **Textes** : chaque texte montré à l'utilisateur est dans `Languages/*.json`, lu avec
  `Texts.Get` / `Texts.Format`.
- **Vie privée** : les données du regard ne sont jamais enregistrées (licence de l'eyetracker) et
  le texte tapé n'est jamais écrit dans le journal.

## Dossiers

Le code source est rangé par thème. Toutes les classes restent dans l'espace de noms `Prunel`,
quel que soit leur dossier. Le fichier de projet trouve tout seul les fichiers `.cs` et `.xaml` de
chaque dossier.

Les trois plus grosses classes sont des classes « partielles » (`partial`) : une seule classe
écrite dans plusieurs fichiers, nommés `Classe.Partie.cs`. `AppSettings` (`.Icons`, `.Defaults`,
`.File`), `SettingsWindow` (`.Build`, `.Profiles`, `.Actions`) et `GazeKeyboard` (`.Actions`,
`.InputBox`, `.Drawing`). Le fichier principal (`AppSettings.cs`…) contient les champs et
l'explication de la classe.

| Dossier | Contenu |
|---|---|
| (racine) | `App.xaml` (point d'entrée : lit les réglages, ouvre la surcouche et le gestionnaire de modes), `Prunel.csproj`, `app.manifest`, `app.uiaccess.manifest` |
| `Gaze` | lecture du regard et traitement : `IGazeSource`, `GazeSource`, `EyetrackerGazeSource`, `EyetrackerLoader`, `Calibration`, `GazeState`, `Smoother`, `FixationDetector`, `GazeStability` |
| `Modes` | une classe par mode, et `ModeManager` |
| `Actions` | ce que propose le choix d'action : `ActionCatalog`, `ActionChoiceLayout`, `Modifiers`, `DragGesture` |
| `Typing` | le clavier au regard et la prédiction de mots : `GazeKeyboard`, `KeyboardKey`, `KeyboardLayout`, `WordPredictor` |
| `Settings` | le fichier des réglages, les profils et la fenêtre des paramètres : `AppSettings`, `SettingsCatalog`, `SettingsProfiles`, `SettingsWindow`, `SettingTarget` |
| `Ui` | fenêtres et boutons au regard : `OverlayWindow`, `DiagnosticWindow`, `CursorSampler`, `GazeButton`, `ButtonAimer`, `IGazeTarget`, `Texts` |
| `Platform` | appels à Windows : `NativeMethods`, `MouseInjector`, `KeyboardInjector`, `KeyboardHook`, `MouseClickHook`, `WheelScroller`, `CursorWaker`, `ScreenCapture`, `ScreenMetrics`, `WindowsStartup`, `UiAccess`, `SoundFeedback` |
| `Languages`, `Keyboards`, `Dictionaries` | fichiers de données copiés à côté de `Prunel.exe` (textes, dispositions de clavier, dictionnaires de prédiction) |
| `Resources` | l'icône du programme |
| `Install` | l'installateur de l'installation complète (accès à l'interface) : `Prunel.iss` (Inno Setup) et `setup-certificate.ps1` |

## DLL de l'eyetracker

Les trois DLL de Tobii (`Tobii.Interaction.Net.dll`, `Tobii.Interaction.Model.dll`,
`Tobii.EyeX.Client.dll`) sont propriétaires et ne sont pas livrées avec Prunel. Le paquet NuGet
sert seulement à compiler (`ExcludeAssets="runtime;build"` dans `Prunel.csproj`), et une cible
de compilation efface tout `Tobii.*.dll` du dossier de sortie ; la compilation automatique (CI)
échoue s'il en reste un.

Au démarrage, `EyetrackerLoader` les cherche sur le PC, dans cet ordre :

1. le sous-dossier `Eyetracker` à côté de `Prunel.exe` ;
2. les dossiers d'installation des logiciels du fabricant (`Program Files\Tobii`,
   `Program Files\Tobii Dynavox`) ;
3. le cache NuGet (`%USERPROFILE%\.nuget\packages\tobii.interaction\0.7.3`) : sur le PC où l'on
   compile Prunel, il n'y a rien à faire.

Les DLL .NET sont données à .NET par l'événement `AssemblyResolve` ; la DLL native est chargée
avant, avec `LoadLibrary`, en 32 bits. `EyetrackerGazeSource` n'utilise les types de Tobii.Interaction que
dans `ConnectEyetracker`, pour que Prunel démarre même sans ces DLL. Le journal note où chaque
DLL a été trouvée. Comment un utilisateur les récupère : voir le
[guide d'utilisation](user-guide.fr.md#fichiers-de-leyetracker).

## Classes et modes

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
- `Gaze/FixationDetector` : reconnaît une fixation. Une mesure est immobile si sa vitesse reste
  sous `MaxFixationSpeed` (pixels par seconde) ET si elle reste à moins de `FixationRadius` du
  point d'ancrage (la moyenne des mesures de la fixation). Il faut `FixationDelay` secondes
  d'immobilité, mesurées en vrai temps. Une mesure aberrante isolée est ignorée.
- `Modes/FixedGazeMode` : le cercle se fige (bord blanc) avec une icône d'options collée du côté
  du centre de l'écran, et le curseur se place sur le point fixé. Si `DirectClick` est activé
  (non par défaut, onglet « Fixation » des paramètres), regarder dans le cercle pendant
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
  annule sans rien enfoncer. `Actions/DragGesture` décrit le glisser en cours (départ, bouton, pause).
- `GazeButton` : un bouton activable au regard (symbole de la police Windows, pastille,
  anneau, libellé au survol), dessiné sur le calque. Il peut être vide (« + » grisé),
  indisponible (son symbole, grisé) ou allumé (bord vert, pour un interrupteur).
- `ButtonAimer` (étape 4) : décide quel bouton est regardé et quand il s'active ; partagé par
  le choix d'action et le menu.
- `Modes/MenuMode` (étape 4) : regarder en bas, hors de l'écran, pendant `MenuDelay` (0,5 s)
  ouvre une barre centrée en bas de l'écran (boutons seuls, sans fond, espacés de 100 px sur
  un écran 4K : issue #54), uniquement depuis les modes mouvement et pause
  (et plus tard jeu, depuis l'étape 6 ; c'est le `ModeManager` qui surveille). La loupe, le
  choix d'action et le glisser-déposer ont leur bouton « retour ». Boutons : Paramètres, Jeu, Clavier
  (le clavier au regard, étape 12), Pause (ou Reprendre si l'on vient de la pause), Suivi souris (interrupteur, bord vert
  quand il est activé), Quitter Prunel (avec confirmation « Oui » / « Non »).
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
- Instance unique (étape 7, `App`) : si Prunel tourne déjà, un deuxième lancement se ferme
  aussitôt, sans rien afficher, et le note dans le journal.
- `WindowsStartup` (étape 7) : l'option `StartWithWindows` (onglet Général) inscrit Prunel
  dans la liste de démarrage de Windows (registre, `HKEY_CURRENT_USER\...\Run`), sans droits
  d'administrateur : les fenêtres administrateur continuent d'ignorer ses clics.
- `Modes/ScrollMode` (étape 8) : action « Défilement » de la couronne. Le cercle blanc reste
  fixé sur le curseur. Regard dans le cercle : rien ne défile (zone morte). Regard au-dessus, en
  dessous, à droite ou à gauche : la fenêtre sous le curseur défile dans ce sens (molette Windows,
  un seul sens à la fois), d'autant plus vite que le regard est loin du cercle, jusqu'à
  `MaxScrollSpeed` (5 crans/s par défaut depuis le 02/11/2026) trois `CircleRadius` au-delà du bord. Démarrage doux (issue #52) : la
  vitesse suit une courbe avec la distance (au carré : lente près du cercle) et chaque départ
  commence à 20 % de cette vitesse, puis atteint 100 % en 1,5 s. Un petit arc vert sur le
  cercle montre la direction. Rien ne défile pendant les 0,4 premières secondes, sauf si le regard
  est passé dans le cercle (à l'entrée, le regard est encore sur l'icône de la couronne), ni quand le regard est perdu, à plus de 200 px hors de l'écran (un regard un peu hors de
  l'écran compte, pour agrandir la plage de vitesse) ou sur le bouton « retour » (bord droit, centré),
  qui ramène au mode mouvement. Regarder hors de l'écran à droite pendant 1 s ramène aussi au
  mode mouvement (issue #51) ; rien n'y défile. Ctrl actif dans le choix d'action : Ctrl + molette (zoom).
  Depuis le 02/11/2026 : défilement fin (petites quantités de molette, jusqu'à une fois par
  image, et vitesse lissée qui monte et descend en douceur) ; en entrant, Prunel rend active la
  fenêtre sous le curseur (sans cliquer), pour ne plus avoir à cliquer avant de défiler.
  Onglet « Défilement » : option « Défiler les fenêtres inactives » (oui par défaut), qui
  règle l'option Windows du même nom (`WheelScroller`, appliquée au démarrage et à chaque changement).
- `Platform/SoundFeedback` (étape sound-feedback) : petit son optionnel quand un bouton s'active.
  `ButtonAimer` le joue à chaque activation (sa propriété `Sound` : `Button` par défaut, `Key` pour
  le clavier du regard), `FixedGazeMode` pour l'icône d'options. Deux réglages dans l'onglet
  « Sons », `KeySound` et `ButtonSound` (non par défaut). Les deux sons sont de petits WAV calculés
  en mémoire et joués avec `System.Media.SoundPlayer` (dans .NET Framework, sans bibliothèque en
  plus).
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
- `Modes/GameMode` (étape 9) : bouton « Jeu » du menu. Rien n'est dessiné ; l'utilisateur clique
  avec un contacteur externe ou la reconnaissance vocale. `GameMouse` « direct » (par défaut,
  pour Mindball Play) : le curseur suit le regard comme en mode mouvement avec suivi souris, avec
  son propre lissage `GameSmoothing`. `GameMouse` « relative » (non testé) : déplacements de la
  souris pour les jeux qui lisent l'entrée brute, en mode « tracking » ou « gamepad »
  (`GameRelative`, `GameSensitivity`). Le menu s'ouvre depuis ce mode (« Quitter le jeu »).
- `Modes/PauseMode` (étape 4) : plus rien ne suit le regard ; seul le menu peut encore s'ouvrir
  (bouton « Reprendre »).
- `EyetrackerLoader` (02/11/2026) : trouve et charge au démarrage les DLL de l'eyetracker
  (Tobii.Interaction), qui ne sont pas livrées avec Prunel (voir « Installation »). Les DLL .NET sont fournies à .NET par l'événement
  `AssemblyResolve` ; la DLL native est chargée à l'avance par `LoadLibrary`, en 32 bits.
  `EyetrackerGazeSource` n'utilise les types de Tobii.Interaction que dans `ConnectEyetracker`, pour que Prunel démarre
  même sans ces DLL.
- `Modes/CalibrationMode` et `Calibration` (étape 4) : bouton « Calibration » de l'onglet
  « Outils » des paramètres (avec « Diagnostic » ; tous deux étaient dans la barre de menu avant
  le 29/10/2026). Lancent l'outil de calibration de l'eyetracker
  et attendent la fermeture de sa fenêtre pour revenir au mode mouvement. Le nom exact de la
  commande n'a pas pu être vérifié : le journal note ce qui a été trouvé et essayé.
- `KeyboardHook` (étape 4) : la touche **Échap** ramène au mode mouvement depuis n'importe quel
  mode, pour l'aidant. La touche arrive aussi normalement au programme actif. Depuis l'étape 12,
  en mode mouvement, elle ferme le clavier au regard ; la touche « Esc » du clavier au regard,
  simulée par Prunel, est ignorée par le crochet (elle va seulement à l'application) : Prunel
  marque ses propres touches simulées (`dwExtraInfo`). Depuis l'issue #51, un Échap simulé par un
  autre logiciel (Dragon, à la voix) compte comme la vraie touche. En mode défilement et quand le
  clavier au regard est ouvert, Prunel GARDE Échap (appui et relâchement) : les autres logiciels
  ne le reçoivent pas (`KeepEscape`).
- `MouseClickHook` (issue #96) : tant que le clavier au regard est ouvert, un crochet souris de
  bas niveau (`WH_MOUSE_LL`) lui signale chaque appui sur un bouton de la souris, d'où qu'il
  vienne (clic au regard par Prunel, clic dit à la reconnaissance vocale, vraie souris). Il ne
  fait qu'observer : le clic continue normalement. Le clavier oublie alors le mot en cours et le
  mot précédent, car le curseur de texte est peut-être dans un autre champ : la lettre suivante
  commence un nouveau mot. De la même façon, `KeyboardHook` signale chaque touche qui ne vient
  pas de Prunel (vrai clavier, Dragon : flèches, Suppr, texte dicté…), sauf les touches de
  modification seules (`OtherKeyPressed`).
- Clavier au regard (étape 12, `GazeKeyboard`, `KeyboardKey`, `KeyboardLayout`) : bouton
  « Clavier » du menu, ou action « Clavier » du choix d'action (ouvre / ferme le clavier ; avant de l'ouvrir, un clic
  gauche sur P donne le focus au champ visé et y place le curseur de texte ; pas
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
  dans les paramètres), Transparence (0, 25, 50 ou 75 % des fonds : clavier, touches et zone de
  saisie ; les lettres, le survol et l'éclair vert restent opaques : `KeyboardTransparency`,
  `settings.json` version 15, issue #55), Pause (le bouton Fermer de cette barre a été retiré à l'étape 14 : il
  doublait celui au-dessus du clavier). Quand elle s'ouvre, le clavier en bas remonte au-dessus
  d'elle en glissant (et redescend de même). Fermeture : bouton à droite au-dessus du clavier (en dessous s'il est en haut),
  à 120 px de lui sur un écran 4K (`CloseGap4K`, issue #56) pour ne pas le déclencher par erreur, barre
  du clavier, ou Échap. Tab est une vraie tabulation : sa largeur (2 ou 4 espaces) se règle
  dans le logiciel où l'on écrit. Le texte tapé n'est jamais écrit dans le journal.
  Réglages : onglet « Clavier » des paramètres (`settings.json` version 9).
- Prédiction de mots (étape 13, `WordPredictor`) : les trois cases du haut du clavier
  proposent les trois mots les plus probables qui commencent par le mot en cours (lettres tapées
  depuis la dernière espace, ponctuation, Entrée, Tab, flèche, clic de souris ou touche venue d'ailleurs que Prunel), sans tenir compte des accents
  ni des majuscules (« ete » propose « été »). Si ce qui est tapé est déjà un mot connu, il est
  en 1re case : le regarder écrit juste l'espace après lui. En regarder une écrit le mot entier suivi d'une
  espace (pas d'espace après « l' », « qu' »…) ; le mot garde la majuscule de départ. Classement :
  fréquence dans Lexique + 20 par utilisation, divisé par 1,6 pour chaque lettre qui reste à taper
  (les mots courts d'abord, issue #71). Un mot inconnu de 2 caractères ou plus s'affiche dans
  la 1re case avec un « + » (en ambre) : la regarder l'ajoute au dictionnaire personnel. Un nombre
  (seulement des chiffres et des tirets, comme « 2026 ») n'a jamais de « + » ; un mot mêlant
  lettres et chiffres (« mp3 ») en a un (issue #61). Fichiers :
  `Dictionaries/fr.txt` (environ 125 000 mots tirés de Lexique 3.83, copié à côté de
  Prunel.exe, lu en arrière-plan au démarrage) et `%APPDATA%\Prunel\personal-dictionary.json`
  (mots ajoutés et nombres d'utilisations ; jamais dans le dépôt), enregistré après chaque ajout
  et, pour les utilisations, une fois par minute et à la fermeture. Mot suivant : Prunel retient les
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
- `app.manifest` : déclare Prunel « sensible au DPI », pour que le regard, le curseur et le
  dessin utilisent les mêmes pixels, même avec une mise à l'échelle de Windows à 125 % ou 150 %.
- `app.uiaccess.manifest` (étape `ui-access`) : le même manifeste avec `uiAccess="true"`, utilisé
  seulement par la version « installation complète » (`dotnet build -p:UiAccess=true`).
  L'« accès à l'interface » (UI access) met le cercle du regard au-dessus du menu Démarrer et
  permet aux clics et aux touches d'atteindre les fenêtres lancées en administrateur. Windows ne
  lance un tel programme que s'il est signé par un certificat approuvé et installé dans
  `C:\Program Files` : Visual Studio et le .zip simple gardent donc `app.manifest`. La
  compilation automatique vérifie que les deux manifestes ne diffèrent que par `uiAccess`.
  `UiAccess.IsActive()` écrit « UI access: yes » ou « no » dans le journal au démarrage. L'installateur (étape
  `installer`) livre cette version : `Install/Prunel.iss` fabrique `Prunel-<version>-setup.exe`
  avec [Inno Setup](https://jrsoftware.org/isinfo.php) 6, un outil de compilation seulement
  (`make-setup.ps1` dans `.github/scripts` l'installe sur la machine de GitHub si besoin).
  L'installateur copie la version dans `C:\Program Files\Prunel`, lance `setup-certificate.ps1`,
  qui la signe avec un certificat créé sur l'ordinateur de l'utilisateur (sa clé privée supprimée
  aussitôt), et ajoute un raccourci au menu Démarrer et une entrée dans « Paramètres >
  Applications » de Windows. Son programme de désinstallation retire le certificat et l'entrée
  « Démarrer avec Windows ». Il lance Prunel par ShellExecute (option `shellexec`) :
  CreateProcess refuse un programme avec accès à l'interface (erreur 740). Ses textes sont dans le
  fichier `.iss` (français ou anglais, selon la langue de Windows), pas dans `Languages/`, car
  Prunel ne tourne pas à ce moment-là.

## Fichier des réglages

Tous les réglages sont dans la classe `AppSettings` (fichier `AppSettings.cs`) et enregistrés dans
`%APPDATA%\Prunel\settings.json`, créé au premier lancement avec les valeurs par défaut du
cahier des charges. On peut l'ouvrir avec le Bloc-notes (nombres décimaux avec un point : `0.3`),
puis relancer Prunel. Un fichier illisible est mis de côté (`settings.json.unreadable`) et
remplacé par les valeurs par défaut.
Ancien nom : Prunel s'appelait GazeBar jusqu'à la version 0.2.0. `FormerName.Migrate`
(`Settings/FormerName.cs`), appelé au démarrage avant la première ligne du journal, renomme
`%APPDATA%\GazeBar`, `%LOCALAPPDATA%\GazeBar` et `Documents\GazeBar` en `Prunel` quand le
nouveau dossier n'existe pas encore (une copie si le déplacement est refusé). `WindowsStartup`
retire l'ancienne valeur `GazeBar` de la clé Run, et l'installateur (même `AppId`) passe à
`Program Files\Prunel` et supprime `Program Files\GazeBar`.
`FileVersion` (13 actuellement) indique quelle version a écrit le fichier : un fichier plus
ancien est converti au démarrage (voir la liste des versions en haut de `AppSettings.cs`). La
fenêtre des paramètres liste ses onglets et ses réglages dans `SettingsCatalog.cs`.

Depuis l'étape 11 (`ScreenMetrics.cs`), les tailles sont en **% de la hauteur de l'écran** et non plus en
pixels : `CircleRadius` 8.8 (190 px en 4K, 95 px en 1920 × 1080), `IconSize` 5.6,
`FixationRadius` 1.4, `CursorDeadZone` 0.4, `MaxFixationSpeed` 42 (%/s). Un fichier plus
ancien (version 6) est converti une fois, en divisant les pixels par 21.6 (valeurs réglées sur
un écran de 2160 px de haut). `InterfaceSize` (75, 100, 125 ou 150 %, onglet « Affichage »)
agrandit ou réduit tout ce qui est dessiné, sauf les réglages du regard. Les textes du calque
suivent aussi la taille du texte de Windows (Accessibilité). Les changements de résolution, de
mise à l'échelle et de taille du texte sont détectés pendant que Prunel tourne.

### Profils de réglages

Étape 15, `SettingsProfiles.cs`. Onglet « Profils » des paramètres
(catégorie Système). « Enregistrer les réglages actuels » ouvre une page Nom : une zone de texte
contenant déjà un nom daté (`Réglages AAAA-MM-JJ HHhMM`) que l'on garde ou remplace avec le clavier
du regard, affiché pour cette saisie ; « Enregistrer » (ou Entrée) copie les réglages dans
`Documents\Prunel\Profiles\<nom>.json` (si le nom existe, une question demande avant de le
remplacer), « Annuler » n'enregistre rien. L'aidant peut renommer ou supprimer ces fichiers dans
l'Explorateur. « Charger un profil » montre les fichiers `.json` de ce
dossier en grands boutons (le plus récent en premier, 12 par page) ; après « Oui », les réglages
actuels sont gardés dans « Sauvegarde automatique » (fichier `auto-backup.json`, remplacé à
chaque chargement, pour revenir en arrière au regard), puis le profil est appliqué tout de suite et
enregistré dans `settings.json`. Un profil d'une ancienne version est converti comme `settings.json`
au démarrage. « Démarrer avec Windows » et « Journal détaillé » ne changent pas (réglages de
l'ordinateur), ni la langue de l'interface, ni le dictionnaire personnel.

### Langue de l'interface

Étape langues-socle, `Texts.cs`. Tous les textes affichés sont dans
des fichiers de langue, dossier `Languages` copié à côté de Prunel.exe (`Languages/en.json`,
`Languages/fr.json`). Chaque texte a une clé (`"menu.settings": "Paramètres"`) ; `{0}`, `{1}`
sont remplacés par des valeurs. L'anglais est la langue de référence (étape langue-anglais) : c'est
la langue d'une nouvelle installation, et un texte absent de la langue choisie est pris en anglais
(la clé manquante est notée dans le journal) ; un ancien fichier de réglages (avant la version 12)
garde le français. Le bouton « Langue » (onglet Général) passe à la langue suivante du dossier
et redessine la page tout de suite ; le choix est le réglage `Language` (`settings.json` version 12).
Pour ajouter une langue : copier `en.json` en `xx.json`, traduire les valeurs (jamais les clés).

## Journal et diagnostic

- Journal : `%LOCALAPPDATA%\Prunel\prunel.log`. `App.Log` écrit les événements importants et
  les erreurs (toujours) ; `App.LogDetail` écrit les événements fréquents (modes, actions,
  touches, menu…) seulement quand le réglage « Journal détaillé » est activé. À 5 Mo, le fichier
  devient `prunel.old.log` et un nouveau commence. Ne jamais écrire de coordonnées du regard ni
  de texte tapé dans le journal.
- `DiagnosticWindow` (onglet « Outils » des paramètres) montre le curseur et le regard en direct ;
  `CursorSampler` (1000 lectures par seconde) ne tourne que pendant que cette fenêtre est ouverte.
- `GazeStability` (dossier `Gaze`), affiché dans la fenêtre Diagnostic, compte la stabilité du
  regard dans trois bandes de l'écran (agitation, pas trop rapides pour la fixation, pertes, hors
  écran). Il ne garde que des compteurs, jamais une position, et oublie tout à la fermeture.

## Vérification de la compilation (CI)

`.github/workflows/build.yml` compile Prunel sous Windows pour chaque pull request et chaque
envoi sur `main`. `.github/scripts/check-data-files.ps1` vérifie que les fichiers JSON des
claviers et des langues sont valides, et que chaque langue a les mêmes clés et les mêmes `{n}`
que `en.json`.

`.github/workflows/release.yml`, lancé à la main, compile une version avec le numéro tapé
(`-p:Version=…`, qui remplace `<Version>` de `Prunel.csproj`), vérifie qu'aucune DLL de
l'eyetracker ni aucun fichier personnel n'est dans le dossier à livrer, le zippe avec les
licences et prépare un brouillon de release GitHub. `App.Version` relit ce numéro (version
informative de l'assembly) pour le journal.
