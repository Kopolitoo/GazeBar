# GazeBar

*English version: [README.md](README.md).*

[![Build](https://github.com/Kopolitoo/GazeBar/actions/workflows/build.yml/badge.svg)](https://github.com/Kopolitoo/GazeBar/actions/workflows/build.yml)

**GazeBar permet d'utiliser un ordinateur Windows uniquement avec les yeux**, grâce à un
eyetracker. Un cercle suit le regard ; on fixe un endroit, puis on choisit quoi y faire : clic,
double clic, clic droit, glisser-déposer, défilement, loupe pour les petites cibles. Il propose
aussi un clavier au regard avec prédiction de mots, un mode jeu, des paramètres réglables au
regard, et une interface en français et en anglais.

GazeBar est conçu par et pour une personne tétraplégique atteinte de SLA (maladie de Charcot),
qui le développe uniquement avec la voix et les yeux, avec l'aide de l'assistant d'IA Claude.
Depuis novembre 2026, il utilise GazeBar seul, tous les jours, sans TD Control : il est donc
testé en permanence, sur un Tobii Dynavox PCEye 5. Si vous êtes dans la même situation, nous
espérons qu'il pourra vous aider aussi.

## État du projet

GazeBar est un projet jeune : il n'y a pas encore de téléchargement ni d'installateur, il faut
le compiler avec Visual Studio. Prévu ensuite : téléchargement prêt à lancer, retour sonore,
pause automatique, thèmes de couleur, assistant de premier lancement, synthèse vocale.

## Prérequis

- Windows 10 ou 11, un seul écran.
- Un eyetracker Tobii avec son pilote officiel (testé : PCEye 5 ; autres modèles non testés).
- Le logiciel de commande au regard fourni avec l'eyetracker (TD Control) est **mis en pause**
  pendant l'utilisation de GazeBar.
- Pour compiler : Visual Studio 2026 avec la charge de travail « Développement d'applications de
  bureau .NET » et le kit de développement .NET Framework 4.8.

## Installation

1. Installer le pilote de l'eyetracker depuis le site du fabricant.
2. Cloner ce dépôt, ouvrir `GazeBar.slnx` dans Visual Studio, appuyer sur F5.
3. Les DLL de l'eyetracker ne sont pas livrées avec GazeBar : sur le PC où on le compile,
   GazeBar les trouve tout seul. Sur un autre PC, voir
   [Fichiers de l'eyetracker](docs/user-guide.fr.md#fichiers-de-leyetracker).

## Documentation

- [Guide d'utilisation](docs/user-guide.fr.md) : comment se servir de GazeBar.
- [Architecture](docs/architecture.md) (en anglais) : organisation du code, pour les
  développeurs.
- [Tests manuels](docs/manual-tests.fr.md) : ce qu'il faut vérifier à la main avec un eyetracker.
- [Contribuer](CONTRIBUTING.md) (en anglais) : règles pour le code, les textes et les pull requests.
- [Guide Git](docs/GUIDE-GIT.md) : branches et pull requests dans Visual Studio.

## Avertissement

- **GazeBar n'est pas un dispositif médical.** Il n'est pas certifié, est fourni sans aucune
  garantie, et ne doit pas être le seul moyen d'appeler à l'aide.
- Un seul écran. Les fenêtres lancées « en tant qu'administrateur » ignorent les clics de GazeBar.
- GazeBar n'enregistre jamais les données du regard et n'écrit jamais le texte tapé dans son
  journal.

## Licence

MIT : voir [LICENSE.txt](LICENSE.txt). Les dictionnaires de mots sont sous licence CC BY-SA 4.0,
et les DLL de l'eyetracker appartiennent à Tobii AB : voir
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Mentions légales

Ce projet est un logiciel indépendant et open source. Il n'est ni affilié, ni approuvé, ni soutenu
par Tobii Dynavox. TD Control, Tobii et Dynavox sont des marques déposées de Tobii AB.
