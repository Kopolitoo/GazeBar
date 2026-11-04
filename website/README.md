# GazeBar website

*Version française : [README.fr.md](README.fr.md).*

The small website that presents GazeBar to non-technical visitors, at
<https://gazebar.kopol.xyz>. Plain HTML and CSS, no tool and no dependency.

- `index.html`: French page (home page of the site).
- `en.html`: English page. Both pages have the same content: change them together.
- `style.css`: shared style. Large text and large buttons (at least 72 px tall), because
  visitors may use an eyetracker or a head mouse.
- `images/`: logo and icon (copies of `docs/images` and the application icon, resized).
- `favicon.ico`: copy of `Resources/GazeBar.ico`.

To see a change, open `index.html` in a web browser.

Going online: the OVH hosting is linked to this repository (branch `main`). When a pull request
is merged, a GitHub webhook tells OVH, which copies the repository again. The `.htaccess` file
at the root of the repository makes the site show only the `website/` folder.
