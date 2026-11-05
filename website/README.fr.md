# Site de Prunel

*English version: [README.md](README.md).*

Le petit site qui présente Prunel aux visiteurs non techniciens, à l'adresse
<https://prunel.kopol.xyz>. Du HTML et du CSS simples, sans outil ni dépendance.

- `index.html` : page en français (page d'accueil du site).
- `en.html` : page en anglais. Les deux pages ont le même contenu : les modifier ensemble.
- `style.css` : style commun. Gros textes et gros boutons (au moins 72 px de haut), car les
  visiteurs peuvent utiliser un eyetracker ou une souris de tête.
- `images/` : logo et icône (copies de `docs/images` et de l'icône de l'application, réduites).
- `favicon.ico` : copie de `Resources/GazeBar.ico`.

Pour voir une modification, ouvrir `index.html` dans un navigateur.

Mise en ligne : l'hébergement OVH est relié à ce dépôt (branche `main`). Quand une pull request
est fusionnée, un webhook GitHub prévient OVH, qui recopie le dépôt. Le fichier `.htaccess` à la
racine du dépôt fait que le site ne montre que le dossier `website/`.
