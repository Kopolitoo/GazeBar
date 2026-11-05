# Guide d'utilisation de GazeBar

*English version: [user-guide.md](user-guide.md).*

GazeBar permet d'utiliser un ordinateur Windows uniquement avec les yeux : on déplace un cercle
au regard, on fixe un endroit, puis on choisit quoi y faire (clic, double clic, glisser-déposer,
défilement, écriture…). Ce guide explique comment s'en servir. Pour l'installer, voir le
[README](../README.fr.md).

## Avant de commencer

- Installer le pilote officiel de l'eyetracker (Tobii) depuis le site du fabricant, et faire
  sa calibration.
- **Mettre en pause** le logiciel de commande au regard fourni avec l'eyetracker (TD Control)
  pendant l'utilisation de GazeBar, pour que les deux logiciels ne déplacent pas la souris en
  même temps.
- GazeBar a besoin de trois fichiers de l'eyetracker (voir
  [Fichiers de l'eyetracker](#fichiers-de-leyetracker)). Sur la plupart des ordinateurs, il les
  trouve tout seul.

## Se déplacer et fixer

- **Mode mouvement** (au démarrage) : un cercle blanc suit le regard. Il se déplace en douceur,
  sans sauts.
- **Regard fixe** : quand le regard reste immobile un instant, le cercle se fige, le curseur de
  la souris va sur le point fixé, et une **icône d'options** apparaît à côté du cercle.
  - Regarder l'icône d'options : le **choix d'action** s'ouvre.
  - Regarder ailleurs : le cercle suit de nouveau le regard.
  - Avec « Clic direct » activé (onglet « Fixation » des paramètres), garder le regard dans le
    cercle fait un clic gauche.
- **Suivi souris** (menu) : le curseur suit le cercle en permanence, et pas seulement quand le
  regard est fixé.

## Choix d'action

Un écran noir montre la zone fixée, agrandie deux fois, avec une croix sur le point exact.
Autour, des icônes : clic gauche, double clic, clic droit, loupe, glisser-déposer (bouton
gauche, bouton droit, android), défilement. On regarde une icône jusqu'à ce que son anneau soit
plein pour l'utiliser. Au bord gauche : **Ctrl**, **Maj**, **Alt** (un regard les active, le bord
devient vert ; ils sont appuyés pendant l'action suivante). En haut : **Copier**, **Couper**,
**Coller**. À droite : **Retour**. Après une action, on revient au mode mouvement.

Les icônes peuvent être déplacées ou retirées : Paramètres, onglet « Icônes », « Configurer les
icônes ».

## Loupe

Pour les petites cibles (une case à cocher, un petit lien). La zone est agrandie quatre fois ;
on fixe le point exact sur l'image, et le choix d'action s'ouvre sur ce point. « Loupe avant
l'action » (onglet « Loupe, glisser ») ouvre la loupe à chaque fois, avant le choix d'action.

## Glisser-déposer

On choisit un glisser-déposer dans le choix d'action, puis on regarde la destination : une ligne
pointillée suit le regard. On fixe la destination, on l'affine dans la loupe, et l'élément est
déplacé. « Android » ajoute une pause avant le déplacement (pour les applications tactiles). Le
bouton retour au bord droit annule.

## Défilement

On choisit « Défilement » dans le choix d'action. Le cercle reste en place. On regarde au-dessus,
en dessous, à gauche ou à droite pour faire défiler dans ce sens : plus le regard est loin, plus
c'est rapide. Regarder dans le cercle arrête. « Retour » (bord droit) termine le défilement.

## Menu

Regarder **sous l'écran** une demi-seconde : la barre de menu s'ouvre en bas. Elle s'ouvre depuis
le mode mouvement, la pause et le mode jeu. Boutons :

- **Paramètres** : voir plus bas.
- **Jeu** : mode jeu (voir plus bas).
- **Clavier** : le clavier du regard.
- **Pause** / **Reprendre** : plus rien ne suit le regard, jusqu'à ce qu'on rouvre le menu et
  qu'on choisisse « Reprendre ».
- **Suivi souris** : activé / désactivé (bord vert quand il est activé).
- **Quitter GazeBar** : demande d'abord « Oui » ou « Non ».

Regarder hors de la barre pendant une seconde la ferme.

## Clavier du regard

Menu, « Clavier ». Le clavier occupe la moitié basse de l'écran. On regarde une touche : elle
s'allume, clignote en vert et écrit. Au-dessus du clavier, le cercle fonctionne comme d'habitude
(on peut cliquer ailleurs). Cinq pages : lettres, accents, chiffres et ponctuation, symboles et
navigation, F1 à F12.

- **Prédiction de mots** : les trois cases du haut proposent des mots. En regarder une l'écrit
  avec une espace. Un mot inconnu apparaît avec un « + » orangé : le regarder l'ajoute au
  dictionnaire personnel. Les nombres ne sont jamais ajoutés (un mot mêlant lettres et chiffres,
  comme « mp3 », peut l'être). Les mots courts passent devant, sauf si un mot plus long est beaucoup
  plus courant ou souvent utilisé. GazeBar apprend aussi quel mot suit souvent un autre.
- **Barre du clavier** (regarder sous l'écran quand le clavier est ouvert) : monter ou descendre
  le clavier, changer sa taille, le mettre en pause, afficher la « Zone de saisie » (un aperçu de
  ce qui est écrit), choisir la « Disposition du clavier » (AZERTY, QWERTY…).
- **Mes mots** (Paramètres, onglet « Général ») : les mots ajoutés ; en regarder un le retire.
- On ferme le clavier par le bouton au-dessus de lui, par la barre du clavier, ou avec Échap.

## Mode jeu

Menu, « Jeu ». Rien n'est dessiné ; le curseur suit directement le regard (prévu pour des jeux
comme Mindball Play). Les clics se font avec un contacteur externe ou la reconnaissance vocale.
On ouvre le menu et on choisit « Quitter le jeu » pour revenir. L'onglet « Jeu » des paramètres
contient les options du jeu.

## Paramètres

Menu, « Paramètres ». Une page noire en plein écran, avec quatre catégories à gauche :
**Regard**, **Actions**, **Interface**, **Système**. Chaque réglage a « − » et « + » (garder le
regard dessus répète) ou un interrupteur oui / non. Les changements s'appliquent tout de suite et
sont enregistrés.

- **Défaut** (en haut à droite) : remet les valeurs d'origine de l'onglet affiché.
- **Retour** (bord droit) ou Échap : retour au mode mouvement.
- L'aidant peut aussi utiliser la souris et le clavier ici : cliquer sur une valeur pour la
  taper.
- **Langue** (onglet « Général ») : anglais ou français. Le dictionnaire de prédiction suit
  cette langue.
- **Profils** (onglet « Profils ») : enregistrer les réglages actuels sous un nom, et les
  recharger plus tard au regard (par exemple un profil « Matin » et un profil « Soir »).
- Onglet **Sons** : deux options oui / non, toutes deux désactivées au départ. **Son des
  touches** : un petit « tic » à chaque touche du clavier du regard. **Son des boutons** : un petit
  son pour tous les autres boutons (menu, choix d'action, icône d'options, paramètres). Le son
  suit le volume de Windows.
- Onglet **Outils** : **Calibration** (lance la calibration de l'eyetracker) et **Diagnostic**
  (montre le regard et le curseur en direct).

## Pour l'aidant

- **Échap** sur le clavier physique ramène GazeBar au mode mouvement, d'où que l'on soit.
- Lancer GazeBar une deuxième fois ne fait rien : il tourne déjà.
- « Démarrer avec Windows » (onglet « Général ») lance GazeBar automatiquement.

## Fichiers sur l'ordinateur

| Quoi | Où |
| --- | --- |
| Réglages | `%APPDATA%\GazeBar\settings.json` (s'ouvre avec le Bloc-notes) |
| Dictionnaire personnel | `%APPDATA%\GazeBar\personal-dictionary.json` |
| Profils | `Documents\GazeBar\Profiles` |
| Journal | `%LOCALAPPDATA%\GazeBar\gazebar.log` |

GazeBar n'enregistre jamais l'endroit où l'on regarde (la licence de l'eyetracker l'interdit) et
n'écrit jamais dans le journal ce que l'on tape. Le journal est court par défaut ; « Journal
détaillé » (onglet « Général ») note davantage, pour chercher un problème.

## Fichiers de l'eyetracker

Le regard est lu par trois fichiers de Tobii (`Tobii.Interaction.Net.dll`,
`Tobii.Interaction.Model.dll`, `Tobii.EyeX.Client.dll`). Ils appartiennent à Tobii AB, donc
GazeBar ne les contient pas. GazeBar les cherche dans le dossier `Eyetracker` à côté de
`GazeBar.exe`, puis dans les dossiers des logiciels Tobii, puis dans le cache NuGet de Visual
Studio.

Si la fenêtre Diagnostic indique qu'ils manquent, il faut les récupérer soi-même, en acceptant la
licence de Tobii :

1. Sur nuget.org, ouvrir le paquet `Tobii.Interaction`, version 0.7.3, et cliquer sur « Download
   package ».
2. Renommer le fichier `.nupkg` téléchargé en `.zip` et l'ouvrir.
3. Copier les deux DLL de `lib\net45` et celle de `build\x86` dans le dossier `Eyetracker` à côté
   de `GazeBar.exe` (le créer si besoin).

## Installation complète (accès à l'interface)

Le zip simple se lance depuis n'importe quel dossier, mais Windows lui interdit deux choses : le
menu Démarrer, la recherche et les notifications passent au-dessus du cercle du regard, et les
fenêtres lancées « en administrateur » ignorent ses clics et ses touches. L'**installation
complète** enlève ces deux limites. Windows ne donne cet « accès à l'interface » qu'à un programme
signé et installé dans `C:\Program Files` : elle a donc son propre installateur.

Pour installer ou mettre à jour :

1. Sur la page [Releases](https://github.com/Kopolitoo/GazeBar/releases), télécharger
   `GazeBar-x.y.z-setup.exe` (sous « Assets ») et double-cliquer dessus. Garder un autre moyen
   de cliquer (voix, souris, TD Control) pour cette étape : GazeBar se ferme pendant
   l'installation.
2. L'installateur n'est pas signé par un éditeur connu : Windows peut afficher « Windows a
   protégé votre ordinateur » ; cliquer sur « Informations complémentaires », puis « Exécuter
   quand même ». Windows demande ensuite « Voulez-vous autoriser cette application à apporter des
   modifications à votre appareil ? » : répondre « Oui ».
3. L'installateur parle la langue de Windows. Cliquer sur « Installer », puis « Terminer » avec
   « Exécuter GazeBar » coché : GazeBar démarre depuis `C:\Program Files\GazeBar`.

Ce que fait l'installateur : il copie GazeBar dans `C:\Program Files\GazeBar`, crée un
certificat sur cet ordinateur, le fait approuver par l'ordinateur, signe `GazeBar.exe` avec, puis
supprime la clé privée du certificat (plus rien ne pourra jamais être signé avec). Un nouveau
certificat est créé à chaque mise à jour. Il ajoute aussi GazeBar au menu Démarrer et à
« Paramètres > Applications » de Windows. Vos réglages sont gardés. Si « Démarrer avec Windows »
est activé, GazeBar met lui-même à jour son lancement au démarrage. Il met aussi à jour une copie
installée par l'ancien script `Install.cmd`.

Pour vérifier : le journal (`%LOCALAPPDATA%\GazeBar\gazebar.log`) dit « UI access: yes » au
démarrage.

Pour la retirer : « Paramètres > Applications > Applications installées » de Windows, GazeBar,
« Désinstaller ». Il supprime le dossier du programme, le raccourci et le certificat. Vos réglages
sont gardés.

Si les fichiers de l'eyetracker ont dû être copiés à la main (voir
[Fichiers de l'eyetracker](#fichiers-de-leyetracker)), mettre le dossier `Eyetracker` dans
`C:\Program Files\GazeBar` (Windows demande les droits d'administrateur). Les mises à jour le
gardent.

## Limites connues

- Un seul écran.
- Avec le zip simple, une fenêtre lancée « en tant qu'administrateur » ignore les clics et les
  touches de GazeBar, et le menu Démarrer de Windows peut cacher le cercle.
  L'[installation complète](#installation-complète-accès-à-linterface) enlève ces deux limites.
- Testé uniquement avec le Tobii Dynavox PCEye 5.
- Le mode jeu avec déplacement relatif de la souris n'a pas encore été testé.
- Le dictionnaire personnel est commun à toutes les langues.
