# Contribuer à GazeBar

*English version: [CONTRIBUTING.md](CONTRIBUTING.md).*

Merci de votre intérêt pour GazeBar, un outil Windows qui commande la souris au regard.
Commencez par l'[architecture](docs/architecture.fr.md) pour vous repérer dans le code.

## Règles de langue

- **Le code, les noms et les commentaires sont écrits en anglais** : classes, méthodes, champs,
  fichiers, dossiers, messages du journal et commentaires du code.
- **Les commentaires décrivent le code tel qu'il est**, au présent. Pas de « step 12: », pas de
  dates, pas de « avant, … » : l'historique est dans Git et dans les pull requests.
- Les textes montrés à l'utilisateur ne sont PAS écrits dans le code : ils sont dans les fichiers
  de langue du dossier `Languages` (`Languages/en.json`, `Languages/fr.json`…) et sont lus avec
  `Texts.Get("clé")` ou `Texts.Format("clé", valeurs)` (voir `Texts.cs`). Les messages du journal
  restent dans le code, en anglais.
- Pour ajouter une langue : copier `Languages/en.json` (la langue de référence) en
  `Languages/xx.json` (code ISO 639-1), traduire les valeurs (jamais les clés) et garder les
  marques `{0}`, `{1}`.
- L'auteur du projet en discute en français ; les issues et les pull requests peuvent être
  écrites en anglais ou en français.

## Issues et pull requests

- Pour signaler un problème ou proposer une idée, ouvrez une issue : GitHub propose deux
  formulaires, « Problème » et « Idée » (`.github/ISSUE_TEMPLATE/`).
- Une nouvelle pull request commence par une petite liste à cocher
  (`.github/pull_request_template.md`). Écrivez-y `Closes #12` pour fermer l'issue 12
  automatiquement quand la pull request est fusionnée.

## Règles techniques

- WPF sur .NET Framework 4.8, en x86 uniquement (les bibliothèques de l'eyetracker sont en
  32 bits).
- Pas de nouvelle dépendance sans en discuter d'abord dans une issue.
- Une étape de la feuille de route par branche et par pull request.
- Le dictionnaire personnel (`%APPDATA%\GazeBar\personal-dictionary.json`) et toute donnée
  personnelle restent hors du dépôt.
- Le texte tapé n'est jamais écrit dans le journal.
- Les données du regard ne sont jamais enregistrées (licence de l'eyetracker) : pas de fichier de
  positions, pas de coordonnées du regard dans le journal. Les afficher en direct à l'écran est
  permis.

## Documentation

- Chaque fichier explicatif existe en anglais (`nom.md`) et en français (`nom.fr.md`), avec en
  haut un lien vers l'autre version. Les garder à jour ensemble.
- `README.md` reste court : ce qu'est GazeBar, comment l'installer, des liens.
- Les détails vont dans `docs/` : `user-guide.md` (pour les utilisateurs), `architecture.md`
  (pour les développeurs), `manual-tests.md`. `docs/GUIDE-GIT.md` existe seulement en français.
- Une pull request qui change ce que voit l'utilisateur met à jour le guide d'utilisation ; celle
  qui ajoute une classe ou un réglage met à jour l'architecture ; une nouvelle fonction ajoute son
  test manuel.
- Un nouveau fichier qui vient de quelqu'un d'autre (liste de mots, image, code) est ajouté à
  `THIRD-PARTY-NOTICES.md` (et `THIRD-PARTY-NOTICES.fr.md`) avec sa licence.

## Tests

GazeBar est testé par son utilisateur sur un Tobii PCEye 5. Dites clairement dans votre pull
request ce qui a été compilé et ce qui a été testé sur le vrai matériel, à l'aide des
[tests manuels](docs/manual-tests.fr.md). Chaque pull request est compilée automatiquement sur
GitHub (voir `.github/workflows/build.yml`).
