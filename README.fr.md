# Prunel

*English version: [README.md](README.md).*

<p align="center"><img src="docs/images/prunel-logo.fr.png" alt="Logo de Prunel : un œil avec un pointeur de souris" width="600"></p>

[![Build](https://github.com/Kopolitoo/Prunel/actions/workflows/build.yml/badge.svg)](https://github.com/Kopolitoo/Prunel/actions/workflows/build.yml)

**Prunel permet d'utiliser un ordinateur Windows uniquement avec les yeux**, grâce à un
eyetracker. Un cercle suit le regard ; on fixe un endroit, puis on choisit quoi y faire : clic,
double clic, clic droit, glisser-déposer, défilement, loupe pour les petites cibles. Il propose
aussi un clavier au regard avec prédiction de mots, un mode jeu, des paramètres réglables au
regard, et une interface en français et en anglais.

Prunel est conçu par et pour une personne tétraplégique atteinte de SLA (maladie de Charcot),
qui le développe uniquement avec la voix et les yeux, avec l'aide de l'assistant d'IA Claude.
Depuis novembre 2026, il utilise Prunel seul, tous les jours, sans TD Control : il est donc
testé en permanence, sur un Tobii Dynavox PCEye 5. Si vous êtes dans la même situation, nous
espérons qu'il pourra vous aider aussi.

Prunel s'appelait GazeBar jusqu'en novembre 2026 (version 0.2.0). À partir de la version 0.3.0,
Prunel reprend tout seul les réglages, les mots personnels et les profils de GazeBar, et son
installateur remplace l'ancien GazeBar.

## État du projet

Prunel est un projet jeune (version 0.x) : des versions prêtes à lancer sont sur la page
[Releases](https://github.com/Kopolitoo/Prunel/releases), sans installateur pour l'instant.
Prévu ensuite : retour sonore, pause automatique, thèmes de couleur, assistant de premier
lancement, synthèse vocale.

## Prérequis

- Windows 10 ou 11, un seul écran.
- Un eyetracker Tobii avec son pilote officiel (testé : PCEye 5 ; autres modèles non testés).
- Le logiciel de commande au regard fourni avec l'eyetracker (TD Control) est **mis en pause**
  pendant l'utilisation de Prunel.
- Pour compiler : Visual Studio 2026 avec la charge de travail « Développement d'applications de
  bureau .NET » et le kit de développement .NET Framework 4.8.

## Installation

1. Installer le pilote de l'eyetracker depuis le site du fabricant.
2. Sur la page [Releases](https://github.com/Kopolitoo/Prunel/releases), télécharger
   `Prunel-x.y.z.zip` (sous « Assets ») et le décompresser, par exemple dans
   `Documents\Prunel`.
3. Lancer `Prunel.exe`. Le programme n'est pas signé : Windows peut afficher « Windows a protégé
   votre ordinateur » ; cliquer sur « Informations complémentaires », puis « Exécuter quand
   même ».
4. Les DLL de l'eyetracker ne sont pas livrées avec Prunel : Prunel les cherche dans le
   logiciel de l'eyetracker. Si la fenêtre Diagnostic dit qu'elles manquent, voir
   [Fichiers de l'eyetracker](docs/user-guide.fr.md#fichiers-de-leyetracker).

Pour l'**installation complète** (cercle au-dessus du menu Démarrer, clics dans les fenêtres
administrateur), télécharger plutôt l'installateur `Prunel-x.y.z-setup.exe` et double-cliquer
dessus : voir [Installation complète](docs/user-guide.fr.md#installation-complète-accès-à-linterface).

Pour le compiler soi-même à la place : cloner ce dépôt, ouvrir `Prunel.slnx` dans Visual
Studio, appuyer sur F5.

## Documentation

- [Guide d'utilisation](docs/user-guide.fr.md) : comment se servir de Prunel.
- [Architecture](docs/architecture.fr.md) : organisation du code, pour les développeurs.
- [Tests manuels](docs/manual-tests.fr.md) : ce qu'il faut vérifier à la main avec un eyetracker.
- [Contribuer](CONTRIBUTING.fr.md) : règles pour le code, les textes et les pull requests.
- [Guide Git](docs/GUIDE-GIT.md) : branches et pull requests dans Visual Studio.

## Avertissement

- **Prunel n'est pas un dispositif médical.** Il n'est pas certifié, est fourni sans aucune
  garantie, et ne doit pas être le seul moyen d'appeler à l'aide.
- Un seul écran. Les fenêtres lancées « en tant qu'administrateur » ignorent les clics de Prunel.
- Prunel n'enregistre jamais les données du regard et n'écrit jamais le texte tapé dans son
  journal.

## Contact

- Questions et échanges : [GitHub Discussions](https://github.com/Kopolitoo/Prunel/discussions)
  (il faut un compte GitHub, gratuit).
- Un problème ou une idée : [issues](https://github.com/Kopolitoo/Prunel/issues/new/choose).
- Sans compte GitHub : l'adresse e-mail est dans la
  [section Contact du site](https://prunel.kopol.xyz/#contact).

Prunel est fait par une seule personne, atteinte de SLA, dont l'énergie est limitée : les
réponses peuvent prendre du temps.

## Licence

MIT : voir [LICENSE.txt](LICENSE.txt). Les dictionnaires de mots sont sous licence CC BY-SA 4.0,
et les DLL de l'eyetracker appartiennent à Tobii AB : voir
[THIRD-PARTY-NOTICES.fr.md](THIRD-PARTY-NOTICES.fr.md).

## Mentions légales

Ce projet est un logiciel indépendant et open source. Il n'est ni affilié, ni approuvé, ni soutenu
par Tobii Dynavox. TD Control, Tobii et Dynavox sont des marques déposées de Tobii AB.
