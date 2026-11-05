# Mentions des éléments tiers

*English version: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).*

Prunel lui-même est sous licence MIT ([LICENSE.txt](LICENSE.txt)). Ce fichier liste les parties
qui viennent d'autres personnes, et ce que Prunel utilise sans le livrer.

## Livré avec Prunel

### Liste de mots français : `Dictionaries/fr.txt`

- Source : [Lexique 3.83](http://www.lexique.org), de Boris New, Christophe Pallier et
  collaborateurs.
- Licence : Creative Commons Attribution - Partage dans les mêmes conditions 4.0 International
  ([CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/deed.fr)).
- Le fichier est une œuvre dérivée (seuls le mot écrit et sa fréquence sont gardés) et reste sous
  CC BY-SA 4.0. Détail des modifications et références à citer :
  [Dictionaries/LICENSE-Lexique.txt](Dictionaries/LICENSE-Lexique.txt).

### Liste de mots anglais : `Dictionaries/en.txt`

- Source : [FrequencyWords](https://github.com/hermitdave/FrequencyWords) (version 2018), de
  Hermit Dave, construite à partir du corpus OpenSubtitles 2018 (P. Lison et J. Tiedemann, 2016).
- Licence : [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/deed.fr).
- Le fichier est une œuvre dérivée et reste sous CC BY-SA 4.0. Le dictionnaire orthographique
  Hunspell en_US ([SCOWL](http://wordlist.aspell.net), version 2020.12.07) a seulement servi de
  filtre pour le construire ; aucun de ses mots n'a été ajouté. Détails (en anglais) :
  [Dictionaries/LICENSE-FrequencyWords.txt](Dictionaries/LICENSE-FrequencyWords.txt).

### Icône et logo de l'application : `Resources/GazeBar.ico`, `docs/images/gazebar-logo*.png`

Images d'abord créées avec ChatGPT (OpenAI), puis retouchées par l'auteur du projet avec
Photocraft. Les conditions d'utilisation d'OpenAI donnent les droits sur les images créées à la
personne qui les a demandées ; l'icône et le logo sont distribués sous licence MIT comme le reste du projet.

### Inno Setup (installateur seulement)

- L'installateur `GazeBar-x.y.z-setup.exe` et son programme de désinstallation sont fabriqués avec
  [Inno Setup](https://jrsoftware.org/isinfo.php) 6, © Jordan Russell et Martijn Laan, et
  contiennent son code.
- Licence : [Inno Setup License](https://jrsoftware.org/files/is/license.txt) (gratuite, y compris
  pour distribuer les installateurs qu'il fabrique).

## Utilisé mais pas livré

### Tobii.Interaction (bibliothèque de l'eyetracker)

- Paquet NuGet `Tobii.Interaction` 0.7.3 (Tobii Core SDK), © Tobii AB.
- Licence propriétaire : « Tobii Core SDK and Gaming SDK License Agreement », acceptée par
  quiconque le télécharge.
- Prunel utilise ce paquet seulement pour **compiler**. Ses DLL (`Tobii.Interaction.Net.dll`,
  `Tobii.Interaction.Model.dll`, `Tobii.EyeX.Client.dll`) ne sont pas dans ce dépôt et ne sont
  pas distribuées avec Prunel : Prunel les trouve sur l'ordinateur au démarrage (voir
  [docs/architecture.fr.md](docs/architecture.fr.md#dll-de-leyetracker)).
- La licence interdit d'enregistrer les données du regard : Prunel n'écrit jamais les positions
  du regard sur le disque.

### Microsoft .NET Framework 4.8, WPF et polices de Windows

Inclus dans Windows. Prunel utilise les symboles des polices de Windows (Segoe MDL2 Assets /
Segoe Fluent Icons) sans copier les polices.

## Marques

Tobii, Dynavox et TD Control sont des marques déposées de Tobii AB. Prunel est indépendant et
n'est ni affilié, ni approuvé, ni soutenu par Tobii Dynavox. Windows est une marque de Microsoft
Corporation.
