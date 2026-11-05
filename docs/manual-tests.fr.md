# Tests manuels

*English version: [manual-tests.md](manual-tests.md).*

GazeBar ne peut pas être testé automatiquement : ces tests se font à la main, avec un
eyetracker. On fait ceux qui correspondent à ce que change une pull request, et on note dans la
pull request ceux qui ont réussi.

Avant de tester, activer « Journal détaillé » (Paramètres, Système, onglet « Général ») : les
lignes du journal citées plus bas ne sont écrites qu'avec cette option. Journal :
`%LOCALAPPDATA%\GazeBar\gazebar.log`.

## 1. Mouvement et regard fixe

On fixe un bouton, le cercle se fige (bord blanc, icône d'options) et le
curseur se place sur le bouton ; on regarde ailleurs, il repart. Avec « Clic direct » activé (onglet « Fixation »), en
continuant à fixer le bouton, un anneau se remplit puis un clic gauche est fait. Le journal note chaque passage en regard fixe et
chaque clic direct.

## 2. Choix d'action

On fixe un fichier dans l'Explorateur, on regarde l'icône d'options, l'écran
noir montre la zone agrandie ; on regarde « double clic » : l'écran noir disparaît et le fichier
s'ouvre, sans toucher la souris.

## 3. Menu et pause

On regarde en bas, hors de l'écran : la barre de menu apparaît : seulement des
boutons, sans fond sombre derrière eux, bien espacés (aussi dans la barre de menu du clavier). On regarde
« Pause » : tout s'arrête. On regarde de nouveau en bas, puis « Reprendre » : le cercle revient.
Le tout uniquement au regard. Échap ramène aussi au mode mouvement.

## 4. Loupe

On fixe une petite case à cocher, on regarde l'icône d'options, puis
« Loupe » dans la couronne : la zone apparaît agrandie ×4. On fixe la case sur la photo : le
choix d'action s'ouvre, la croix est sur la case ; « clic gauche » la coche. Avec « Loupe avant l'action » activée (onglet « Loupe, glisser »), la loupe s'ouvre directement après l'icône
d'options.

## 5. Modificateurs et glisser-déposer

Dans l'Explorateur, on fixe un fichier, on ouvre le choix d'action, on
regarde « Ctrl » (bord vert) puis « clic gauche » ; on recommence sur un autre fichier : les deux
sont sélectionnés. Puis on fixe un fichier, on choisit « glisser-déposer gauche » (quatre flèches,
pastille G), on regarde le dossier d'arrivée (la ligne en pointillés suit), on le fixe, on affine
dans la loupe : le fichier est déplacé. Le bouton « retour » à droite annule.

## 6. Paramètres

Menu, « Paramètres » : l'écran noir s'ouvre sur l'onglet Regard. On garde
le regard sur « + » du lissage : la valeur monte d'un pas toutes les 0,4 s. On regarde l'onglet
Fixation, puis l'interrupteur du clic direct : il passe au bleu, rond à droite, « Oui ». « Défaut », puis « Oui » :
les valeurs de l'onglet reviennent. L'aidant clique sur une valeur, tape 0,5 puis Entrée.
« Retour » ramène au mode mouvement. Lancer GazeBar une deuxième fois ne fait rien (voir le
journal).

## 7. Défilement

On ouvre une longue page web, on fixe le texte, on ouvre le choix d'action
puis « Défilement » (à gauche, en haut). Le cercle blanc reste en place. On regarde dans le
cercle : rien ne bouge. On regarde un peu sous le cercle : la page descend doucement ; plus bas :
plus vite. Au-dessus : elle remonte. « Retour » au bord droit ramène au mode mouvement.
Depuis le 02/11/2026 : à vitesse lente, la page doit glisser sans petits sauts, et démarrer
ou s'arrêter en douceur ; sans avoir cliqué dans la page avant, elle doit défiler quand même
(le journal note « fenêtre … rendue active » et l'état du réglage Windows de la molette).

## 8. Configuration des icônes

Menu, « Paramètres », onglet « Icônes », « Configurer les icônes ». On
regarde le « − » du clic gauche (en haut à gauche de la couronne) puis celui du clic droit : les
deux emplacements deviennent vides (« + »). On regarde le premier : la liste propose toutes les
actions de la couronne ; on choisit « Clic droit ». On fait de même avec l'autre emplacement et « Clic
gauche » : les deux actions ont échangé leur place. « Retour », puis dans le choix d'action, les
deux icônes sont à leur nouvelle place. Retirer « Couper » laisse un emplacement grisé en haut
du choix d'action. « Défaut » dans l'onglet « Icônes » remet tout comme avant.

## 9. Tailles d'écran

Écran en 1920 × 1080 à 100 %. Au premier lancement, le journal note
« mise à jour en version 7 » et l'écran détecté ; le cercle, les icônes et la barre de menu ont
à l'œil la même taille qu'avant en 4K. Onglet « Affichage » : « Écran détecté » donne la
résolution, l'échelle et la taille du texte ; « Taille de l'interface » à 150 % agrandit le
cercle, les icônes et le menu. Pendant que GazeBar tourne, on change la résolution, puis la
mise à l'échelle (125 %), puis la taille du texte de Windows : à chaque fois, le journal note
« changement détecté » et les tailles s'adaptent (un choix d'action ou une loupe ouverts se
ferment, retour au mode mouvement ; la pause, le jeu et les paramètres restent ouverts).

## 10. Clavier du regard

Dans le Bloc-notes, on ouvre le menu, « Clavier » : le clavier apparaît en
bas. On regarde des touches : elles s'éclaircissent, flashent et le texte s'écrit (« é » depuis
la page « À,È,Ê »). On regarde au-dessus du clavier : le cercle revient et le regard fixe marche.
On tape un moment sur les touches en haut à droite : le bouton Fermer, bien séparé du clavier,
ne se déclenche jamais par erreur (de même sous le clavier quand il est en haut).
On regarde sous l'écran : la barre du clavier s'ouvre et le clavier remonte au-dessus d'elle ;
« Haut / bas » le met en haut (il s'y rouvre la fois suivante), « Taille » passe à 75 % puis 50 %,
« Pause » le cache jusqu'à la reprise. Dans la configuration des icônes, on place « Clavier » sur la couronne : dans le choix
d'action, ce bouton ouvre le clavier. Dans le Bloc-notes avec plusieurs lignes de texte, on fixe
le milieu d'une ligne (Bloc-notes pas actif, ou une autre fenêtre devant), on choisit « Clavier » :
le curseur de texte va à cet endroit et les touches tapées s'y écrivent. Clavier ouvert, le même
bouton le ferme, sans clic.

## 11. Prédiction de mots

Dans le Bloc-notes, on ouvre le clavier et on tape « bo » : les cases
proposent « bon », « bonne »… ; on regarde « bonjour » (après « bonj ») : « bonjour » et une espace
s'écrivent. « ete » propose « été ». « pr » propose des mots courts (« pris », « près »…) avant « prendre ». On tape un mot inventé (« zorglub ») : la 1re case montre
« + zorglub » en ambre ; on la regarde : une espace s'écrit, et en tapant « zo » le mot est
proposé. On tape « 2026 » : aucune case « + » n'apparaît ; on tape « mp3x » : « + mp3x » apparaît. On écrit deux fois « je suis » (en tapant « suis » en entier puis une espace) : après
« je » et une espace, la 1re case propose « suis ». Après avoir fermé GazeBar, `%APPDATA%\GazeBar\personal-dictionary.json` contient le mot.

## 12. Options du clavier

Clavier ouvert, on regarde sous l'écran : la barre du clavier montre aussi
« Zone de saisie » et « Disposition du clavier ». « Zone de saisie » : le bord devient vert, une
bande apparaît au-dessus des touches ; on tape « bonjour à tous » : la bande le montre ; une
flèche la vide. « Disposition du clavier » : la liste montre « Français AZERTY » avec un bord vert ; « Retour au
clavier » ramène au clavier. Paramètres, onglet Général, « Mes mots » : « zorglub » (ajouté à
l'étape 13) est dans la liste ; on le regarde : il disparaît ; « Retour aux paramètres » ramène à
l'onglet Général. En tapant « zo », il n'est plus proposé.
« Transparence » (montre « 0 % ») : on la regarde : « 25 % », les fonds du clavier laissent
voir la fenêtre derrière ; encore : « 50 % », « 75 % », puis « 0 % » (opaque comme avant). À 75 %,
les lettres, l'éclair vert et le survol restent bien visibles. On ferme et rouvre GazeBar : la
valeur est gardée.

## 13. Clavier anglais

Clavier ouvert, « Disposition du clavier » : la liste montre
« English QWERTY » et « Français AZERTY » ; on choisit « English QWERTY » : lettres en QWERTY, et
avec l'interface en français, « bonj » propose toujours « bonjour ». Paramètres, Général,
« Langue » → English ; retour au clavier : « th » propose « the », « that », « this » (ou proches),
« don » et « don' » proposent « don't ».

## 14. Profils de réglages

Paramètres, Système, Profils, « Enregistrer les réglages actuels » : le
clavier du regard apparaît avec la page Nom ; écrire « Soir », regarder « Enregistrer » : le
message donne le nom du profil, et le fichier apparaît dans `Documents\GazeBar\Profiles`. On
change le rayon du cercle (Affichage). Profils, « Charger un profil » : la liste montre le profil ;
on le regarde : « Charger le profil … ? » ; « Non » ne change rien ; « Oui » remet l'ancien rayon et
crée « Sauvegarde automatique ». Charger « Sauvegarde automatique » remet le rayon modifié.

## 15. Retour sonore

Paramètres, « Interface », onglet « Sons » : les deux interrupteurs sont sur non ; rien ne
fait de son. Activer « Son des touches » : ouvrir le clavier et taper : un petit « tic » aigu à
chaque touche (aussi avec la répétition), mais aucun son pour le menu ni le choix d'action. Le
désactiver et activer « Son des boutons » : les touches sont muettes ; l'icône d'options, le choix
d'action, les boutons du menu et ceux des paramètres font un petit son de deux notes. Tout
désactiver : de nouveau le silence. Relancer GazeBar : les choix sont gardés.

## Installation complète (Setup.exe, accès à l'interface)

Prendre le zip `GazeBar-setup-…` de la compilation automatique (page Actions de la pull request,
« Artifacts ») ou le `GazeBar-x.y.z-setup.exe` d'un brouillon de release. Garder un autre moyen
de cliquer (voix, souris) pendant l'installation.

1. Double-cliquer sur `GazeBar-…-setup.exe`. Windows peut dire « éditeur inconnu »
   (« Informations complémentaires », « Exécuter quand même »), puis demande « Oui » pour
   l'administrateur. L'installateur parle la langue de Windows ; cliquer sur « Installer », puis
   « Terminer » avec « Exécuter GazeBar » coché : GazeBar démarre et le journal dit « UI access:
   yes ».
2. `C:\Program Files\GazeBar` existe, GazeBar est dans le menu Démarrer et dans « Paramètres >
   Applications » de Windows.
3. Ouvrir le menu Démarrer de Windows : le cercle du regard reste visible au-dessus, et un regard
   fixe puis un clic gauche ouvre un élément du menu.
4. Lancer le Bloc-notes en administrateur (clic droit, « Exécuter en tant qu'administrateur ») :
   un clic au regard y met le curseur de texte, et le clavier du regard y écrit.
5. Relancer l'installateur (mise à jour) : pas de question pour fermer GazeBar (il est fermé),
   même résultat, réglages inchangés.
6. Le désinstaller depuis « Paramètres > Applications » : le dossier, le raccourci et le
   certificat « GazeBar local signature » (`certlm.msc`, « Autorités de certification racines de
   confiance ») ont disparu ; les réglages sont toujours dans `%APPDATA%\GazeBar`.
7. Le zip simple et Visual Studio marchent comme avant, avec « UI access: no » dans le journal.

## Avant de publier une version

Télécharger le zip du brouillon de release et le décompresser dans un nouveau dossier (pas le
dossier de Visual Studio). Il contient `GazeBar.exe`, les dossiers `Languages`, `Keyboards` et
`Dictionaries`, les licences, et aucun `Tobii.*.dll`. Lancer `GazeBar.exe` (Windows peut demander
« Exécuter quand même ») : le regard fonctionne, le clavier du regard propose des mots, et les
premières lignes de `%LOCALAPPDATA%\GazeBar\gazebar.log` disent « GazeBar 0.1.0 starting » (le
numéro de la version). Le brouillon contient aussi `GazeBar-0.1.0-setup.exe` : faire avec lui le
test « Installation complète » ci-dessus.
