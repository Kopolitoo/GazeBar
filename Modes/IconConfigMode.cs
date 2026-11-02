using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// MODE CONFIGURATION DES ICÔNES (étape 10 du cahier des charges).
    ///
    /// Entrée : bouton « Configurer les icônes » de l'onglet « Icônes » du mode paramètres.
    /// Sortie : icône « retour » (au milieu du bord droit) ou touche Échap de l'aidant : retour
    /// au mode paramètres, sur l'onglet « Icônes ». Le menu du bas ne s'ouvre pas ici.
    ///
    /// Mise en page (demande de l'utilisateur du 28/10/2026) : LA MÊME que le choix d'action
    /// (DispositionChoixAction), sur le même écran noir du calque. À la place de la photo, un
    /// cadre gris qui explique l'écran. Les quatorze emplacements sont dessinés : les huit de la
    /// couronne, les trois modificateurs (bord gauche) et les trois boutons utilitaires (bord
    /// haut). Chacun porte une pastille ronde en haut à droite :
    ///   - « − » rouge : une action est assignée. Regarder l'icône RETIRE l'action ;
    ///     l'emplacement devient vide (pastille « + ») ;
    ///   - « + » verte : l'emplacement est vide. Regarder l'icône ouvre LA LISTE.
    ///
    /// La liste : un rectangle aux bords arrondis qui occupe les trois quarts de l'écran, avec
    /// TOUTES les actions du même groupe, chacune avec son nom en dessous, et un bouton
    /// « Annuler ». Une action peut être placée plusieurs fois dans son groupe (demande de
    /// l'utilisateur du 28/10/2026), par exemple deux clics gauches dans la couronne. Une fois
    /// l'action choisie, elle est placée et la liste se ferme.
    ///
    /// Chaque changement est appliqué tout de suite (Parametres.Courants) et enregistré dans
    /// parametres.json : le prochain choix d'action utilise la nouvelle disposition.
    ///
    /// Après chaque changement, l'écran est redessiné et l'icône qui se trouve sous le regard
    /// est bloquée tant que le regard ne l'a pas quittée (ViseurBoutons.ReinitialiserEtBloquer) :
    /// sans cela, en continuant à regarder une icône qu'on vient de vider, on rouvrirait
    /// aussitôt la liste.
    ///
    /// Ce mode est dessiné sur le calque, qui laisse passer la souris : il se commande au
    /// regard uniquement (l'aidant peut en sortir avec Échap).
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal sealed class IconConfigMode : Mode
    {
        /// <summary>Proportion de l'écran occupée par la liste (les trois quarts, en largeur et en hauteur).</summary>
        private const double ListRatio = 0.75;

        /// <summary>Nombre maximal de colonnes de la liste (huit actions → deux lignes de quatre).</summary>
        private const int ListColumns = 4;

        // ── Couleurs.
        private static readonly Brush PreviewBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x20, 0x21, 0x24)));
        private static readonly Brush ListBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x26)));
        private static readonly Brush Soft = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAD)));
        private static readonly Brush Veil = Freeze(new SolidColorBrush(Color.FromArgb(0xC0, 0x00, 0x00, 0x00)));

        // Les boutons affichés, et ce que fait chacun (même ordre).
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly List<Action> _actions = new List<Action>();

        // Décide quel bouton est regardé et quand il s'active (classe partagée avec le menu).
        private readonly ButtonAimer _aimer;

        // Vrai tant que l'écran n'a pas pu être construit (calque pas encore prêt).
        private bool _toBuild;

        // La liste est-elle ouverte ? Et pour quel emplacement ?
        private bool _listOpen;
        private IconGroup _chosenGroup;
        private int _chosenSlot;

        // Le petit cercle qui montre le regard (comme dans le choix d'action).
        private Ellipse _gazeCircleBackground;
        private Ellipse _gazeCircle;
        private readonly Smoother _smoothed = new Smoother();
        private bool _gazeCirclePlaced;

        public IconConfigMode()
        {
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get { return "Configuration des icônes"; }
        }

        public override void Enter(GazeState gaze)
        {
            Manager.Layer.HideAll();
            Manager.Layer.BringToFront();
            _listOpen = false;
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

            // Dans la liste, chaque action a déjà son nom écrit en dessous : le libellé au
            // survol se superposerait à ce nom, il n'est donc pas affiché.
            bool labels = p.ShowLabels && !_listOpen;
            int active = _aimer.Update(gaze, p.ActivationDelay, labels);
            if (active >= 0)
            {
                App.Log("Configuration des icônes : " + _buttons[active].Label);
                _actions[active]();
                return;
            }

            MoveGazeCircle(gaze, p);
        }

        /// <summary>
        /// L'écran a changé (étape 11) : on reste ici, et tout l'écran est redessiné au
        /// prochain tic avec les nouvelles tailles (la liste reste ouverte si elle l'était).
        /// </summary>
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

        /// <summary>
        /// Icône « retour » ou touche Échap (appelée aussi par App) : retour au mode
        /// paramètres, sur l'onglet « Icônes ».
        /// </summary>
        public void BackToSettings()
        {
            App.Log("Configuration des icônes : retour aux paramètres");
            Manager.ChangeMode(new SettingsMode(SettingsCatalog.IconsTabIndex));
        }

        // ───────────────────────── Les actions ─────────────────────────

        /// <summary>Icône assignée (pastille « − ») : l'action est retirée, l'emplacement devient vide.</summary>
        private void Remove(IconGroup group, int num)
        {
            AppSettings p = AppSettings.Current;
            p.SetSlot(group, num, null);
            p.Save();
            App.Log("Configuration des icônes : " + group + " " + num + " vidé");
            Build();
        }

        /// <summary>Icône vide (pastille « + ») : ouvre la liste des actions à placer.</summary>
        private void OpenList(IconGroup group, int num)
        {
            _listOpen = true;
            _chosenGroup = group;
            _chosenSlot = num;
            Build();
        }

        /// <summary>Une action de la liste : elle est placée, la liste se ferme.</summary>
        private void PlaceAction(ChoiceAction action)
        {
            AppSettings p = AppSettings.Current;
            p.SetSlot(_chosenGroup, _chosenSlot, action);
            p.Save();
            App.Log("Configuration des icônes : " + action + " placé en " + _chosenGroup + " " + _chosenSlot);
            _listOpen = false;
            Build();
        }

        /// <summary>« Annuler » dans la liste : rien ne change, la liste se ferme.</summary>
        private void CloseList()
        {
            _listOpen = false;
            Build();
        }

        // ───────────────────────── Construction de l'écran ─────────────────────────

        /// <summary>
        /// (Re)dessine tout l'écran : la disposition, et la liste par-dessus si elle est ouverte.
        /// Toutes les positions sont calculées en pixels d'écran, puis converties par le calque.
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

            // La photo du choix d'action a le même côté : 2 × [RayonCercle], au plus l'écran.
            double photoSide = Math.Min(Math.Round(2 * p.CircleRadiusPx), Math.Min(width, height));
            ActionChoiceLayout placed = new ActionChoiceLayout(width, height, size, photoSide);

            layer.PrepareChoiceScreen();
            Canvas screen = layer.ChoiceScreen;
            _buttons.Clear();
            _actions.Clear();

            // Quand la liste est ouverte, la disposition est dessinée derrière, mais ses icônes
            // ne sont pas des cibles du regard (seules celles de la liste le sont).
            bool targets = !_listOpen;

            BuildPreview(layer, screen, placed);

            foreach (IconGroup group in new[] { IconGroup.Crown, IconGroup.Utilities, IconGroup.Modifiers })
            {
                ChoiceAction?[] layout = p.Layout(group);
                Point[] slots = placed.Slots(group);
                for (int i = 0; i < layout.Length; i++)
                {
                    IconGroup g = group;   // copies pour les fonctions ci-dessous
                    int num = i;            // (sinon elles verraient les dernières valeurs)
                    GazeButton button;
                    Action action;
                    if (layout[i].HasValue)
                    {
                        button = ActionCatalog.CreateButton(layout[i].Value);
                        button.SetConfigBadge(false);
                        action = () => Remove(g, num);
                    }
                    else
                    {
                        button = GazeButton.ConfigurableEmpty("Emplacement libre : choisir une action");
                        button.SetConfigBadge(true);
                        action = () => OpenList(g, num);
                    }
                    AddButton(layer, screen, button, action, slots[i].X, slots[i].Y, size, targets);
                }
            }

            AddButton(layer, screen, ActionCatalog.CreateButton(ChoiceAction.Back), BackToSettings,
                          placed.Back.X, placed.Back.Y, size, targets);

            if (_listOpen)
            {
                BuildList(layer, screen, placed, p);
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
            // pas tant que le regard ne l'a pas quitté.
            _aimer.ResetAndBlock();
        }

        /// <summary>
        /// À la place de la photo : un carré gris de la même taille, avec le titre de l'écran
        /// et un rappel du rôle des pastilles.
        /// </summary>
        private static void BuildPreview(OverlayWindow layer, Canvas screen, ActionChoiceLayout placed)
        {
            double side = placed.EnlargedPhotoSide;
            Point corner = layer.ToWpf(placed.CenterX - side / 2, placed.CenterY - side / 2);
            double sideWpf = layer.LengthToWpf(side);
            double titleFont = layer.LengthToWpf(placed.Size * 0.26);
            double textFont = layer.LengthToWpf(placed.Size * 0.18);

            StackPanel texts = new StackPanel();
            texts.VerticalAlignment = VerticalAlignment.Center;
            texts.Margin = new Thickness(sideWpf * 0.06);
            texts.Children.Add(Text("Configuration des icônes", titleFont, Brushes.White, true));
            TextBlock plus = Text("+ : placer une action", textFont, Soft, false);
            plus.Margin = new Thickness(0, textFont, 0, 0);
            texts.Children.Add(plus);
            texts.Children.Add(Text("− : retirer l'action", textFont, Soft, false));

            Border frame = new Border();
            frame.Width = sideWpf;
            frame.Height = sideWpf;
            frame.Background = PreviewBackground;
            frame.BorderBrush = Brushes.Gray;
            frame.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            frame.Child = texts;
            Canvas.SetLeft(frame, corner.X);
            Canvas.SetTop(frame, corner.Y);
            screen.Children.Add(frame);
        }

        /// <summary>
        /// La liste : un voile sombre sur tout l'écran, puis un rectangle aux bords arrondis
        /// (les trois quarts de l'écran) avec un titre, toutes les actions du groupe en grille
        /// (quatre colonnes au plus), chacune avec son nom, et « Annuler » en bas.
        /// </summary>
        private void BuildList(OverlayWindow layer, Canvas screen, ActionChoiceLayout placed, AppSettings p)
        {
            double size = placed.Size;
            double space = ActionChoiceLayout.Space;

            // ── Le voile, qui assombrit la disposition derrière la liste.
            Rectangle veil = new Rectangle();
            veil.Fill = Veil;
            veil.Width = screen.Width;
            veil.Height = screen.Height;
            Canvas.SetLeft(veil, 0);
            Canvas.SetTop(veil, 0);
            screen.Children.Add(veil);

            // ── Le rectangle aux bords arrondis, centré.
            double l = placed.Width * ListRatio;
            double h = placed.Height * ListRatio;
            double left = (placed.Width - l) / 2;
            double top = (placed.Height - h) / 2;
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

            // ── Le titre, en haut du rectangle.
            // Les textes de la liste suivent aussi la taille du texte de Windows (étape 11).
            double titleFont = size * 0.3 * ScreenMetrics.WindowsText;
            AddCenteredText(layer, screen, "Choisir l'action à placer", placed.CenterX, top + size * 0.35,
                               l, titleFont, Brushes.White, true);

            // ── « Annuler », en bas du rectangle, avec son nom en dessous.
            //    E711 = croix (annuler).
            double nameFont = size * 0.2 * ScreenMetrics.WindowsText;
            double yCancel = top + h - size / 2 - nameFont * 2.5;
            AddButton(layer, screen, new GazeButton("", null, "Annuler"), CloseList,
                          placed.CenterX, yCancel, size, true);
            AddCenteredText(layer, screen, "Annuler", placed.CenterX, yCancel + size / 2 + nameFont * 0.4,
                               size * 3, nameFont, Soft, false);

            // ── Toutes les actions du groupe, même celles déjà placées ailleurs : une action
            //    peut apparaître plusieurs fois (demande de l'utilisateur du 28/10/2026).
            List<ChoiceAction> free = new List<ChoiceAction>(ActionCatalog.Actions(_chosenGroup));

            // La zone de la grille : entre le titre et « Annuler ».
            double gridTop = top + size * 0.35 + titleFont * 2;
            double gridBottom = yCancel - size / 2 - space / 2;

            if (free.Count == 0)
            {
                // Ne devrait pas arriver : chaque groupe a au moins une action.
                AddCenteredText(layer, screen, "Aucune action pour ce groupe.",
                                   placed.CenterX, (gridTop + gridBottom) / 2, l, nameFont * 1.3, Soft, false);
                return;
            }

            int columns = Math.Min(free.Count, ListColumns);
            int rows = (free.Count + columns - 1) / columns;
            // La grille occupe toute la place du rectangle (demande de l'utilisateur du
            // 28/10/2026 : les icônes paraissaient petites au milieu de beaucoup d'espace).
            // Chaque action reçoit une « case » : la largeur du rectangle partagée entre les
            // colonnes, et la hauteur entre le titre et « Annuler » partagée entre les lignes.
            // L'icône et son nom sont centrés dans leur case.
            double stepX = l / columns;
            double stepY = Math.Max((gridBottom - gridTop) / rows, size + nameFont * 2 + space * 0.5);
            // Centre de l'icône de la première ligne : le milieu de la case, remonté de la
            // moitié de la hauteur du nom (le nom est écrit sous l'icône).
            double yFirst = (gridTop + gridBottom) / 2 - (rows - 1) * stepY / 2 - nameFont;

            for (int i = 0; i < free.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                // La dernière ligne, si elle est incomplète, est centrée elle aussi.
                int inRow = Math.Min(columns, free.Count - row * columns);
                double x = placed.CenterX + (column - (inRow - 1) / 2.0) * stepX;
                double y = yFirst + row * stepY;

                ChoiceAction action = free[i];   // copie pour la fonction ci-dessous
                AddButton(layer, screen, ActionCatalog.CreateButton(action), () => PlaceAction(action),
                              x, y, size, true);
                AddCenteredText(layer, screen, ActionCatalog.ShortName(action), x, y + size / 2 + nameFont * 0.4,
                                   stepX - space, nameFont, Brushes.White, false);
            }
        }

        // ───────────────────────── Petits outils ─────────────────────────

        /// <summary>
        /// Place un bouton (centré sur x, y en pixels d'écran). cible = vrai : le regard peut
        /// l'activer, et « action » est alors appelée ; faux : il est seulement dessiné.
        /// </summary>
        private void AddButton(OverlayWindow layer, Canvas screen, GazeButton button, Action action,
                                   double x, double y, double size, bool target)
        {
            button.ZoneFactor = ActionChoiceMode.ZoneFactor;
            button.Place(layer, screen, x, y, size);
            if (target)
            {
                _buttons.Add(button);
                _actions.Add(action);
            }
        }

        /// <summary>
        /// Écrit un texte centré horizontalement sur x, son haut en y (pixels d'écran), sur une
        /// largeur de « largeur » pixels (le texte passe à la ligne au-delà).
        /// </summary>
        private static void AddCenteredText(OverlayWindow layer, Canvas screen, string text, double x, double y,
                                               double width, double fontPixels, Brush color, bool bold)
        {
            TextBlock block = Text(text, layer.LengthToWpf(fontPixels), color, bold);
            block.Width = layer.LengthToWpf(width);
            block.TextAlignment = TextAlignment.Center;
            block.TextWrapping = TextWrapping.Wrap;
            Point point = layer.ToWpf(x, y);
            Canvas.SetLeft(block, point.X - block.Width / 2);
            Canvas.SetTop(block, point.Y);
            screen.Children.Add(block);
        }

        private static TextBlock Text(string text, double size, Brush color, bool bold)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontSize = size;
            block.Foreground = color;
            block.FontFamily = new FontFamily("Segoe UI");
            block.TextWrapping = TextWrapping.Wrap;
            if (bold)
            {
                block.FontWeight = FontWeights.Bold;
            }
            return block;
        }

        /// <summary>
        /// Déplace le petit cercle du regard : il suit le regard en douceur ([Lissage]), comme
        /// dans le choix d'action. Son rayon est la moitié de [RayonCercle]. Regard hors de
        /// l'écran : il glisse le long du bord. Regard perdu : il reste où il est.
        /// </summary>
        private void MoveGazeCircle(GazeState gaze, AppSettings p)
        {
            if (_gazeCircle == null || !gaze.Available)
            {
                return;
            }

            // Position ramenée dans l'écran : regard hors de l'écran → le cercle glisse le
            // long du bord (voir EtatRegard.XEcran).
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
