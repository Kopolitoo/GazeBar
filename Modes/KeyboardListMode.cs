using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// MODE LISTE DU CLAVIER (étape 14, « clavier-options »).
    ///
    /// Deux listes : « Langue » s'ouvre depuis la barre de menu du clavier au regard, « Mes
    /// mots » depuis l'onglet Général des paramètres (demande de l'utilisateur du 02/11/2026) :
    ///   - LANGUE : les dispositions trouvées dans le dossier Claviers (une par fichier JSON).
    ///     Celle qu'on utilise a un bord vert. En regarder une autre la choisit (retenu dans
    ///     parametres.json), puis on revient au clavier ;
    ///   - MES MOTS : les mots ajoutés au dictionnaire personnel avec la case « + », dans
    ///     l'ordre alphabétique. Chaque mot a une pastille rouge « − » : le regarder le RETIRE
    ///     tout de suite (comme pour retirer une icône à l'étape 10), et on reste dans la liste
    ///     pour pouvoir en retirer d'autres. Les mots de Lexique ne sont jamais concernés.
    ///
    /// Étape 15 (« profils-parametres ») : une troisième liste, qui n'a rien à voir avec le
    /// clavier mais réutilise la même mise en page :
    ///   - PROFILS : les profils de réglages du dossier Documents\GazeBar\Profils, le plus
    ///     récent en premier (voir ProfilsParametres). En regarder un revient aux paramètres,
    ///     onglet Profils, avec la question « Charger le profil … ? » : rien ne change sans
    ///     ce « Oui ». « Retour aux paramètres » revient au même onglet sans rien changer.
    ///
    /// Mise en page : la même que la liste de la configuration des icônes (étape 10) : écran
    /// noir, rectangle aux bords arrondis sur les trois quarts de l'écran, titre en haut,
    /// grille d'au plus 4 colonnes × 3 rangées (12 par page). « Retour » (au clavier pour
    /// Langue, aux paramètres pour Mes mots) est à droite de l'écran, centré verticalement, hors
    /// du rectangle (02/11/2026). En bas du rectangle : « Page précédente » et « Page
    /// suivante » quand la liste ne tient pas sur une page.
    ///
    /// Le clavier est caché pendant ce mode (ce n'est ni le mode mouvement ni le regard fixe)
    /// et réapparaît au retour en mode mouvement. Le menu du bas ne s'ouvre pas ici ; la
    /// touche Échap de l'aidant fait comme « Retour » (App.OnEchap).
    ///
    /// Les mots sont affichés à l'écran mais jamais écrits dans le journal (vie privée).
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal sealed class KeyboardListMode : Mode
    {
        /// <summary>Les deux listes possibles.</summary>
        public enum ListKind
        {
            Language,
            MyWords,
            Profiles
        }

        /// <summary>Proportion de l'écran occupée par la liste (comme à l'étape 10).</summary>
        private const double ListRatio = 0.75;

        /// <summary>Grille d'une page : 4 colonnes × 3 rangées.</summary>
        private const int Columns = 4;
        private const int Rows = 3;
        private const int PerPage = Columns * Rows;

        // Couleurs (les mêmes que la liste de la configuration des icônes).
        private static readonly Brush ListBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x26)));
        private static readonly Brush Soft = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAD)));

        private readonly ListKind _kind;

        // Les boutons affichés et ce que fait chacun (même ordre).
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly List<Action> _actions = new List<Action>();
        private readonly ButtonAimer _aimer;

        // Le contenu de la liste : les dispositions (Langue) ou les mots (Mes mots).
        private List<FoundLayout> _layouts = new List<FoundLayout>();
        private List<string> _words = new List<string>();
        private List<FoundProfile> _profiles = new List<FoundProfile>();

        // La page affichée (0 = la première).
        private int _page;

        // Un message affiché sous le titre (erreur), ou null.
        private string _message;

        // Vrai tant que l'écran n'a pas pu être construit (calque pas encore prêt).
        private bool _toBuild;

        // Le petit cercle qui montre le regard (comme dans le choix d'action).
        private Ellipse _gazeCircleBackground;
        private Ellipse _gazeCircle;
        private readonly Smoother _smoothed = new Smoother();
        private bool _gazeCirclePlaced;

        public KeyboardListMode(ListKind kind)
        {
            _kind = kind;
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get
            {
                switch (_kind)
                {
                    case ListKind.Language: return "Langue du clavier";
                    case ListKind.Profiles: return "Profils";
                    default: return "Mes mots";
                }
            }
        }

        public override void Enter(GazeState gaze)
        {
            OverlayWindow layer = Manager.Layer;
            layer.HideAll();
            layer.BringToFront();
            _page = 0;
            _message = null;

            if (_kind == ListKind.Language)
            {
                _layouts = KeyboardLayout.List();
                // On commence à la page de la disposition utilisée.
                int currentOne = CurrentLayoutIndex();
                _page = currentOne < 0 ? 0 : currentOne / PerPage;
            }
            else if (_kind == ListKind.Profiles)
            {
                _profiles = SettingsProfiles.List();
            }
            else
            {
                _words = Manager.Keyboard.Predictor.AddedWords();
            }

            _toBuild = true;
            Build();
        }

        public override void Update(GazeState gaze)
        {
            if (_toBuild)
            {
                Build();
                return;
            }

            AppSettings p = AppSettings.Current;
            // Chaque élément a déjà son nom écrit en dessous : pas de libellé au survol, il se
            // superposerait à ce nom (comme dans la liste de l'étape 10).
            int active = _aimer.Update(gaze, p.ActivationDelay, false);
            if (active >= 0)
            {
                _actions[active]();
                return;
            }

            MoveGazeCircle(gaze, p);
        }

        /// <summary>L'écran a changé (étape 11) : on reste ici, tout est redessiné au prochain tic.</summary>
        public override bool AdaptToScreen()
        {
            _toBuild = true;
            return true;
        }

        public override void Quit()
        {
            Manager.Layer.HideChoiceScreen();
            Manager.Layer.ChoiceScreen.Children.Clear();
            _buttons.Clear();
            _actions.Clear();
            _gazeCircle = null;
            _gazeCircleBackground = null;
        }

        // ───────────────────────── Les actions ─────────────────────────

        /// <summary>
        /// Bouton « Retour » ou touche Échap (appelée aussi par App) : Langue → le mode
        /// mouvement, où le clavier réapparaît ; Mes mots → les paramètres, onglet Général ;
        /// Profils → les paramètres, onglet Profils.
        /// </summary>
        public void Back()
        {
            App.Log(Name + " : retour");
            if (_kind == ListKind.MyWords)
            {
                Manager.ChangeMode(new SettingsMode(SettingsCatalog.GeneralTabIndex));
            }
            else if (_kind == ListKind.Profiles)
            {
                Manager.ChangeMode(new SettingsMode(SettingsCatalog.ProfilesTabIndex));
            }
            else
            {
                Manager.ChangeMode(new MovementMode());
            }
        }

        private void ChangePage(int offset)
        {
            _page += offset;
            Build();
        }

        /// <summary>Langue : une disposition est choisie ; on revient au clavier si elle se lit bien.</summary>
        private void ChooseLayout(FoundLayout layout)
        {
            string message;
            if (Manager.Keyboard.ChangeLayout(layout.File, out message))
            {
                Manager.ChangeMode(new MovementMode());
            }
            else
            {
                // Le fichier a pu être modifié depuis l'ouverture de la liste : on le dit.
                _message = message;
                Build();
            }
        }

        /// <summary>
        /// Profils : un profil est choisi. On revient aux paramètres, onglet Profils, qui
        /// demande confirmation avant de le charger.
        /// </summary>
        private void ChooseProfile(FoundProfile profile)
        {
            App.Log("Profils : profil choisi « " + profile.Name + " », confirmation demandée");
            Manager.ChangeMode(new SettingsMode(SettingsCatalog.ProfilesTabIndex, profile));
        }

        /// <summary>Mes mots : le mot est retiré du dictionnaire personnel, la liste est redessinée.</summary>
        private void RemoveWord(string word)
        {
            Manager.Keyboard.Predictor.Delete(word);
            _words.Remove(word);
            Build();
        }

        private int CurrentLayoutIndex()
        {
            string currentOne = AppSettings.Current.KeyboardLayout;
            for (int i = 0; i < _layouts.Count; i++)
            {
                if (string.Equals(_layouts[i].File, currentOne, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        // ───────────────────────── Construction de l'écran ─────────────────────────

        /// <summary>
        /// (Re)dessine tout l'écran. Toutes les positions sont calculées en pixels d'écran,
        /// puis converties par le calque.
        /// </summary>
        private void Build()
        {
            OverlayWindow layer = Manager.Layer;
            if (!layer.Ready)
            {
                _toBuild = true;   // on réessaiera au prochain tic
                return;
            }
            _toBuild = false;

            AppSettings p = AppSettings.Current;
            double width = Manager.ScreenWidth;
            double height = Manager.ScreenHeight;
            double size = p.IconSizePx;
            double space = ActionChoiceLayout.Space;

            layer.PrepareChoiceScreen();
            Canvas screen = layer.ChoiceScreen;
            _buttons.Clear();
            _actions.Clear();

            // ── Le rectangle aux bords arrondis, centré.
            double l = width * ListRatio;
            double h = height * ListRatio;
            double left = (width - l) / 2;
            double top = (height - h) / 2;
            double centerX = width / 2;
            Point corner = layer.ToWpf(left, top);
            Border frame = new Border();
            frame.Width = layer.LengthToWpf(l);
            frame.Height = layer.LengthToWpf(h);
            frame.CornerRadius = new CornerRadius(layer.LengthToWpf(size * 0.3));
            frame.Background = ListBackground;
            frame.BorderBrush = Brushes.White;
            frame.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            Canvas.SetLeft(frame, corner.X);
            Canvas.SetTop(frame, corner.Y);
            screen.Children.Add(frame);

            // ── Le nombre d'éléments et de pages.
            int number = _kind == ListKind.Language ? _layouts.Count
                       : _kind == ListKind.Profiles ? _profiles.Count
                       : _words.Count;
            int pages = Math.Max(1, (number + PerPage - 1) / PerPage);
            // Après une suppression, la dernière page peut être vide : on recule.
            _page = Math.Max(0, Math.Min(_page, pages - 1));

            // ── Le titre, et sous lui une explication (ou le message d'erreur).
            //    Les textes suivent la taille du texte de Windows (étape 11).
            double titleFont = size * 0.3 * ScreenMetrics.WindowsText;
            double nameFont = size * 0.2 * ScreenMetrics.WindowsText;
            string title = _kind == ListKind.Profiles ? "Charger un profil" : Name;
            if (pages > 1)
            {
                title += "  (page " + (_page + 1) + " sur " + pages + ")";
            }
            double yTitle = top + size * 0.35;
            AddCenteredText(layer, screen, title, centerX, yTitle, l, titleFont, Brushes.White, true);
            string explanation = _message ?? (_kind == ListKind.Language
                ? "Regarde une disposition pour la choisir."
                : _kind == ListKind.Profiles
                ? "Regarde un profil pour le choisir (une confirmation suit)."
                : "Regarde un mot pour le retirer de ton dictionnaire.");
            double yExplanation = yTitle + titleFont * 1.5;
            AddCenteredText(layer, screen, explanation, centerX, yExplanation, l, nameFont,
                               _message != null ? Brushes.Orange : Soft, false);

            // ── « Retour » (croix E711) : à droite de l'écran, centré verticalement, EN DEHORS
            //    du rectangle de la liste (au milieu de la marge de droite), pour ne pas le viser
            //    en regardant un mot (demande de l'utilisateur du 02/11/2026).
            string backName = _kind == ListKind.Language ? "Retour au clavier" : "Retour aux paramètres";
            double rightMargin = width - (left + l);
            double xBack = left + l + rightMargin / 2;
            double yBack = height / 2;
            AddButton(layer, screen, new GazeButton("", null, backName), Back,
                          xBack, yBack, size);
            AddCenteredText(layer, screen, backName, xBack, yBack + size / 2 + nameFont * 0.4,
                               rightMargin - space / 2, nameFont, Soft, false);

            // ── La rangée du bas : Page précédente · Page suivante (E72B / E72A = flèches).
            double yBottom = top + h - size / 2 - nameFont * 2.5;
            double bottomStep = size + 2 * space;
            if (_page > 0)
            {
                AddButton(layer, screen, new GazeButton("", null, "Page précédente"), () => ChangePage(-1),
                              centerX - bottomStep * 1.5, yBottom, size);
                AddCenteredText(layer, screen, "Page précédente", centerX - bottomStep * 1.5,
                                   yBottom + size / 2 + nameFont * 0.4, bottomStep, nameFont, Soft, false);
            }
            if (_page < pages - 1)
            {
                AddButton(layer, screen, new GazeButton("", null, "Page suivante"), () => ChangePage(1),
                              centerX + bottomStep * 1.5, yBottom, size);
                AddCenteredText(layer, screen, "Page suivante", centerX + bottomStep * 1.5,
                                   yBottom + size / 2 + nameFont * 0.4, bottomStep, nameFont, Soft, false);
            }

            // ── La grille de la page : entre l'explication et la rangée du bas.
            double gridTop = yExplanation + nameFont * 2.5;
            double gridBottom = yBottom - size / 2 - space / 2;
            int isFirst = _page * PerPage;
            int inPage = Math.Max(0, Math.Min(PerPage, number - isFirst));

            if (inPage == 0)
            {
                string empty = _kind == ListKind.Language
                    ? "Aucune disposition trouvée dans le dossier Claviers."
                    : _kind == ListKind.Profiles
                    ? "Aucun profil pour l'instant. Le bouton « Enregistrer les réglages actuels » en crée un."
                    : "Aucun mot ajouté pour l'instant. Un mot inconnu s'ajoute avec la case « + » du clavier.";
                AddCenteredText(layer, screen, empty, centerX, (gridTop + gridBottom) / 2 - nameFont,
                                   l * 0.8, nameFont * 1.3, Soft, false);
            }
            else
            {
                int columns = Math.Min(inPage, Columns);
                int rows = (inPage + columns - 1) / columns;
                // Comme à l'étape 10 : chaque élément reçoit une case, et l'icône avec son nom
                // dessous sont centrés dans la case.
                double stepX = l / columns;
                double stepY = Math.Max((gridBottom - gridTop) / rows, size + nameFont * 2 + space * 0.5);
                double yFirst = (gridTop + gridBottom) / 2 - (rows - 1) * stepY / 2 - nameFont;

                for (int i = 0; i < inPage; i++)
                {
                    int row = i / columns;
                    int column = i % columns;
                    int inRow = Math.Min(columns, inPage - row * columns);
                    double x = centerX + (column - (inRow - 1) / 2.0) * stepX;
                    double y = yFirst + row * stepY;

                    GazeButton button;
                    Action action;
                    string name;
                    if (_kind == ListKind.Language)
                    {
                        FoundLayout d = _layouts[isFirst + i];   // copie pour la fonction ci-dessous
                        // Le bouton montre le code de la langue (« FR ») ; la disposition utilisée a un bord vert.
                        button = GazeButton.Text(d.Language.ToUpperInvariant(), d.Name);
                        button.SetLit(isFirst + i == CurrentLayoutIndex());
                        action = () => ChooseLayout(d);
                        name = d.Name;
                    }
                    else if (_kind == ListKind.Profiles)
                    {
                        FoundProfile profile = _profiles[isFirst + i];   // copie pour la fonction ci-dessous
                        // E77B = personne ; E7A7 = flèche « annuler » pour la sauvegarde
                        // automatique (revenir aux réglages d'avant le dernier chargement).
                        button = new GazeButton(profile.IsBackup ? "\uE7A7" : "\uE77B", null, "Charger ce profil");
                        action = () => ChooseProfile(profile);
                        name = profile.Name;
                    }
                    else
                    {
                        string word = _words[isFirst + i];   // copie pour la fonction ci-dessous
                        // E74D = corbeille, avec la pastille rouge « − » de l'étape 10.
                        button = new GazeButton("", null, "Retirer ce mot");
                        button.SetConfigBadge(false);
                        action = () => RemoveWord(word);
                        name = word;
                    }
                    AddButton(layer, screen, button, action, x, y, size);
                    AddCenteredText(layer, screen, name, x, y + size / 2 + nameFont * 0.4,
                                       stepX - space, nameFont, Brushes.White, false);
                }
            }

            // Le petit cercle du regard, ajouté EN DERNIER pour être par-dessus tout.
            _gazeCircleBackground = new Ellipse();
            _gazeCircleBackground.Stroke = Brushes.Black;
            _gazeCircleBackground.StrokeThickness = ScreenMetrics.Units(4);
            _gazeCircleBackground.Opacity = 0.6;
            _gazeCircleBackground.Visibility = Visibility.Collapsed;
            _gazeCircle = new Ellipse();
            _gazeCircle.Stroke = Brushes.LimeGreen;
            _gazeCircle.StrokeThickness = ScreenMetrics.Units(2);
            _gazeCircle.Visibility = Visibility.Collapsed;
            screen.Children.Add(_gazeCircleBackground);
            screen.Children.Add(_gazeCircle);

            layer.ShowChoiceScreen();

            // Les boutons viennent d'être remplacés : celui qui est sous le regard ne s'active
            // pas tant que le regard ne l'a pas quitté (sinon, après avoir retiré un mot, on
            // retirerait aussitôt celui qui prend sa place).
            _aimer.ResetAndBlock();
        }

        // ───────────────────────── Petits outils ─────────────────────────

        /// <summary>Place un bouton (centré sur x, y en pixels d'écran) que le regard peut activer.</summary>
        private void AddButton(OverlayWindow layer, Canvas screen, GazeButton button, Action action,
                                   double x, double y, double size)
        {
            button.ZoneFactor = ActionChoiceMode.ZoneFactor;
            button.Place(layer, screen, x, y, size);
            _buttons.Add(button);
            _actions.Add(action);
        }

        /// <summary>
        /// Écrit un texte centré horizontalement sur x, son haut en y (pixels d'écran), sur une
        /// largeur de « largeur » pixels (le texte passe à la ligne au-delà).
        /// </summary>
        private static void AddCenteredText(OverlayWindow layer, Canvas screen, string text, double x, double y,
                                               double width, double fontPixels, Brush color, bool bold)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontSize = layer.LengthToWpf(fontPixels);
            block.Foreground = color;
            block.FontFamily = new FontFamily("Segoe UI");
            block.TextWrapping = TextWrapping.Wrap;
            if (bold)
            {
                block.FontWeight = FontWeights.Bold;
            }
            block.Width = layer.LengthToWpf(width);
            block.TextAlignment = TextAlignment.Center;
            Point point = layer.ToWpf(x, y);
            Canvas.SetLeft(block, point.X - block.Width / 2);
            Canvas.SetTop(block, point.Y);
            screen.Children.Add(block);
        }

        /// <summary>
        /// Déplace le petit cercle du regard, comme dans la configuration des icônes : il suit
        /// le regard en douceur, son rayon est la moitié de [RayonCercle]. Regard hors de
        /// l'écran : il glisse le long du bord. Regard perdu : il reste où il est.
        /// </summary>
        private void MoveGazeCircle(GazeState gaze, AppSettings p)
        {
            if (_gazeCircle == null || !gaze.Available)
            {
                return;
            }
            if (!_gazeCirclePlaced)
            {
                _smoothed.Place(gaze.XScreen, gaze.YScreen);
                _gazeCirclePlaced = true;
            }
            _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);

            OverlayWindow layer = Manager.Layer;
            Point center = layer.ToWpf(_smoothed.X, _smoothed.Y);
            double radius = layer.LengthToWpf(p.CircleRadiusPx / 2);
            PlaceEllipse(_gazeCircleBackground, center, radius);
            PlaceEllipse(_gazeCircle, center, radius);
        }

        private static void PlaceEllipse(Ellipse ellipse, Point center, double radius)
        {
            ellipse.Width = radius * 2;
            ellipse.Height = radius * 2;
            Canvas.SetLeft(ellipse, center.X - radius);
            Canvas.SetTop(ellipse, center.Y - radius);
            ellipse.Visibility = Visibility.Visible;
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
