using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GazeBar
{
    /// <summary>
    /// INTERFACE : le « calque » plein écran, transparent aux clics, toujours au premier plan.
    ///
    /// Les modes (classes Mode…) n'y dessinent pas directement : ils appellent des méthodes
    /// simples comme AfficherCercle ou MasquerCercle, en PIXELS D'ÉCRAN (les mêmes unités que
    /// le regard de l'eyetracker et le curseur). C'est cette fenêtre qui fait la conversion vers les
    /// unités de WPF.
    ///
    /// Pourquoi une conversion ? Quand Windows agrandit l'affichage (125 %, 150 %…), WPF ne
    /// compte plus en pixels mais en « unités indépendantes » (1 unité = 1/96 de pouce).
    /// À 150 %, 100 pixels réels ne valent que 66,7 unités WPF. Sans conversion, le cercle
    /// serait décalé par rapport au regard, d'autant plus qu'on s'éloigne du coin en haut à gauche.
    /// </summary>
    internal partial class OverlayWindow : Window
    {
        private readonly DispatcherTimer _foreground;

        public OverlayWindow()
        {
            InitializeComponent();

            // Plusieurs fenêtres peuvent être « toujours au premier plan » (la barre d'outils,
            // la fenêtre Diagnostic, d'autres logiciels…). Entre elles, c'est la dernière
            // affichée ou activée qui gagne. Pour que le calque reste AU-DESSUS DE TOUT, on le
            // remet en haut de la pile deux fois par seconde.
            //
            // Limite connue (29/10/2026) : le menu Démarrer, la recherche et le centre de
            // notifications de Windows sont dans une « couche » réservée au système, plus haute
            // que toutes les fenêtres « toujours au premier plan ». Le calque passe donc dessous
            // malgré ce minuteur. Seul un programme déclaré « uiAccess » (manifeste), signé
            // numériquement et installé dans C:\Program Files peut s'afficher au-dessus.
            _foreground = new DispatcherTimer();
            _foreground.Interval = TimeSpan.FromMilliseconds(500);
            _foreground.Tick += (sender, e) => BringToFront();
            _foreground.Start();

            // Étape 11 : surveillance de l'écran (résolution, mise à l'échelle, taille du
            // texte). Windows prévient le calque par des messages (voir Crochet), mais on
            // vérifie aussi toutes les deux secondes : on ne sait pas encore si Windows envoie
            // bien un message quand on change la taille du texte (à vérifier en essai).
            _screenWatch = new DispatcherTimer();
            _screenWatch.Interval = TimeSpan.FromSeconds(2);
            _screenWatch.Tick += (sender, e) => CoverScreen();
            _screenWatch.Start();

            // Après un message de Windows, on attend une demi-seconde avant de mesurer :
            // un changement de résolution envoie souvent plusieurs messages de suite, et
            // l'écran met un instant à se stabiliser. Une seule mesure suffit.
            _deferredMeasure = new DispatcherTimer();
            _deferredMeasure.Interval = TimeSpan.FromMilliseconds(500);
            _deferredMeasure.Tick += (sender, e) =>
            {
                _deferredMeasure.Stop();
                CoverScreen();
            };
        }

        private readonly DispatcherTimer _screenWatch;
        private readonly DispatcherTimer _deferredMeasure;

        /// <summary>
        /// Demande une nouvelle mesure de l'écran dans une demi-seconde. Si une demande est déjà
        /// en attente, le compte à rebours repart de zéro (Stop puis Start).
        /// </summary>
        private void ScheduleMeasure()
        {
            _deferredMeasure.Stop();
            _deferredMeasure.Start();
        }

        /// <summary>
        /// Le « crochet » des messages de Windows adressés au calque (étape 11). On ne garde
        /// que deux messages, envoyés à toutes les fenêtres :
        ///   WM_DISPLAYCHANGE : la résolution de l'écran a changé ;
        ///   WM_SETTINGCHANGE : un réglage de Windows a changé (peut-être la taille du texte).
        /// handled reste faux : le message continue son chemin normal.
        /// </summary>
        private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == NativeMethods.WM_DISPLAYCHANGE || message == NativeMethods.WM_SETTINGCHANGE)
            {
                ScheduleMeasure();
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Place le calque tout en haut des fenêtres « toujours au premier plan », sans le
        /// déplacer, sans changer sa taille et sans lui donner le focus.
        /// </summary>
        public void BringToFront()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;   // fenêtre pas encore créée
            }
            NativeMethods.SetWindowPos(
                handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        // ───────────────────────── Fenêtre transparente aux clics ─────────────────────────

        /// <summary>
        /// Appelée quand Windows a créé la fenêtre « réelle » (son handle, HWND).
        /// On lui ajoute les styles qui la rendent transparente aux clics et non activable,
        /// puis on lui donne la taille exacte de l'écran.
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr handle = new WindowInteropHelper(this).Handle;

            // Styles ajoutés (le « | » combine les options) :
            //  - TRANSPARENT : les clics et la molette traversent la fenêtre et arrivent à
            //    l'application qui est dessous ;
            //  - LAYERED : nécessaire pour que TRANSPARENT fonctionne (WPF le met déjà) ;
            //  - NOACTIVATE : la fenêtre ne prend jamais le focus ;
            //  - TOOLWINDOW : elle n'apparaît pas dans Alt+Tab.
            int style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(
                handle,
                NativeMethods.GWL_EXSTYLE,
                style
                | NativeMethods.WS_EX_TRANSPARENT
                | NativeMethods.WS_EX_LAYERED
                | NativeMethods.WS_EX_NOACTIVATE
                | NativeMethods.WS_EX_TOOLWINDOW);

            // Étape 11 : on écoute les messages de Windows (voir Crochet).
            HwndSource source = HwndSource.FromHwnd(handle);
            if (source != null)
            {
                source.AddHook(Hook);
            }

            CoverScreen();
        }

        /// <summary>
        /// Appelée par WPF si la mise à l'échelle de Windows change pendant que GazeBar tourne
        /// (par exemple de 100 % à 125 % dans les paramètres d'affichage). On recalcule la
        /// taille tout de suite, puis une deuxième fois une demi-seconde plus tard (étape 11) :
        /// la résolution change souvent en même temps.
        /// </summary>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            CoverScreen();
            ScheduleMeasure();
        }

        /// <summary>
        /// Place la fenêtre sur tout l'écran principal.
        ///
        /// TailleEcranPrincipal donne la taille de l'écran en pixels réels (GazeBar est déclaré
        /// « sensible au DPI » dans app.manifest). TransformFromDevice est la conversion
        /// « pixels réels → unités WPF » fournie par WPF pour cette fenêtre.
        ///
        /// Étape 11 : la mesure est aussi donnée à Ecran, qui prévient le reste du programme si
        /// la résolution, la mise à l'échelle ou la taille du texte a changé. La taille de la
        /// fenêtre n'est réécrite (et notée dans le journal) que si elle a changé, car cette
        /// méthode est maintenant appelée toutes les deux secondes.
        /// </summary>
        private void CoverScreen()
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null || source.CompositionTarget == null)
            {
                return;
            }

            // La vraie résolution, même après un changement (voir NativeMethods.TailleEcranPrincipal).
            double widthPixels;
            double heightPixels;
            NativeMethods.PrimaryScreenSize(out widthPixels, out heightPixels);

            if (widthPixels <= 0 || heightPixels <= 0)
            {
                return;   // écran en train de changer : on réessaiera plus tard
            }

            Matrix toWpf = source.CompositionTarget.TransformFromDevice;
            Point size = toWpf.Transform(new Point(widthPixels, heightPixels));

            // (Une taille pas encore donnée vaut NaN, « pas un nombre » : on la donne alors.)
            bool sizeChanged = double.IsNaN(Width) || double.IsNaN(Height) || Left != 0 || Top != 0 ||
                                 Math.Abs(Width - size.X) > 0.5 || Math.Abs(Height - size.Y) > 0.5;
            if (sizeChanged)
            {
                Left = 0;
                Top = 0;
                Width = size.X;
                Height = size.Y;

                App.Log("Calque : écran " + widthPixels + " × " + heightPixels + " pixels = " +
                        size.X.ToString("F0") + " × " + size.Y.ToString("F0") + " unités WPF (échelle " +
                        (widthPixels / size.X * 100).ToString("F0") + " %)");
            }

            // TransformToDevice : unités WPF → pixels réels. M22 = facteur vertical (1.25 à 125 %).
            ScreenMetrics.Measure(source.CompositionTarget.TransformToDevice.M22);
        }

        // ───────────────────────── Dessin ─────────────────────────

        // Couleurs du trait de dessus du cercle : vert discret en mouvement, blanc en regard fixe.
        // « Freeze » fige le pinceau : WPF le dessine alors plus vite.
        private static readonly SolidColorBrush MovementBrush = Freeze(new SolidColorBrush(Colors.DarkGray));
        private static readonly SolidColorBrush FixedBrush = Freeze(new SolidColorBrush(Colors.LightGray));

        private static SolidColorBrush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Affiche le cercle du mode mouvement (vert, discret) centré sur (x, y).
        /// Tout est en PIXELS D'ÉCRAN, comme le regard et le curseur.
        /// </summary>
        public void ShowCircle(double xPixels, double yPixels, double radiusPixels)
        {
            ShowCircle(xPixels, yPixels, radiusPixels, false);
        }

        /// <summary>
        /// Affiche le cercle centré sur (x, y), avec le rayon donné.
        /// fixe = vrai : cercle du mode regard fixe (trait blanc et opaque).
        /// </summary>
        public void ShowCircle(double xPixels, double yPixels, double radiusPixels, bool fixedValue)
        {
            ShowCircle(xPixels, yPixels, radiusPixels, fixedValue, 1.0);
        }

        /// <summary>
        /// Comme ci-dessus, avec une opacité réduite : attenuation = 1 → opacité habituelle,
        /// 0.5 → deux fois plus transparent (mode défilement, demande du 28/10/2026).
        /// </summary>
        public void ShowCircle(double xPixels, double yPixels, double radiusPixels, bool fixedValue,
                                   double damping)
        {
            CircleX = xPixels;
            CircleY = yPixels;
            CircleShown = true;

            Circle.Stroke = fixedValue ? FixedBrush : MovementBrush;
            // Épaisseurs en unités de l'écran de référence, converties (étape 11, voir Ecran.cs).
            Circle.StrokeThickness = ScreenMetrics.Units(fixedValue ? 1.5 : 1);
            Circle.Opacity = (fixedValue ? 0.6 : 0.3) * damping;
            CircleBackground.StrokeThickness = ScreenMetrics.Units(fixedValue ? 2.5 : 2);
            CircleBackground.Opacity = (fixedValue ? 0.7 : 0.5) * damping;

            // Pas encore affichée à l'écran : rien à dessiner (PointFromScreen échouerait).
            if (PresentationSource.FromVisual(this) == null)
            {
                return;
            }

            // PointFromScreen convertit un point de l'écran (pixels réels) en position
            // dans cette fenêtre (unités WPF). Il tient compte de la mise à l'échelle.
            Point center = PointFromScreen(new Point(xPixels, yPixels));
            double radius = PixelsToWpf(radiusPixels);

            PlaceEllipse(CircleBackground, center, PixelsToWpf(radiusPixels + ScreenMetrics.Px(2)));
            PlaceEllipse(Circle, center, radius);
        }

        /// <summary>Centre du cercle tel qu'il est dessiné, en pixels d'écran (valable si CercleAffiche).</summary>
        public double CircleX { get; private set; }

        public double CircleY { get; private set; }

        /// <summary>
        /// Vrai si le cercle est visible. Un mode qui commence s'en sert pour reprendre le
        /// cercle là où il est, au lieu de le faire sauter ailleurs.
        /// </summary>
        public bool CircleShown { get; private set; }

        /// <summary>Cache le cercle.</summary>
        public void HideCircle()
        {
            CircleShown = false;
            CircleBackground.Visibility = Visibility.Collapsed;
            Circle.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Affiche l'anneau de progression sur le bord d'un cercle centré sur (x, y).
        /// progression va de 0 (rien) à 1 (tour complet).
        /// </summary>
        public void ShowRing(double xPixels, double yPixels, double radiusPixels, double progress)
        {
            Ring.StrokeThickness = ScreenMetrics.Units(6);
            DrawArc(Ring, xPixels, yPixels, radiusPixels, progress);
        }

        public void HideRing()
        {
            Ring.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Affiche l'icône d'options : un carré de côté [taille] centré sur (x, y), en pixels
        /// d'écran. progression (0 à 1) remplit l'anneau autour d'elle ; à 0, pas d'anneau.
        /// afficherLibelle : montre le texte « Options » sous l'icône.
        /// </summary>
        public void ShowIcon(double xPixels, double yPixels, double sizePixels,
                                  double progress, bool showLabel)
        {
            if (PresentationSource.FromVisual(this) == null)
            {
                return;
            }

            Point center = PointFromScreen(new Point(xPixels, yPixels));
            double side = PixelsToWpf(sizePixels);

            OptionsIcon.Width = side;
            OptionsIcon.Height = side;
            OptionsIcon.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            OptionsIcon.CornerRadius = new CornerRadius(ScreenMetrics.Units(12));
            OptionsIconSymbol.FontSize = side * 0.5;
            OptionsIconRing.StrokeThickness = ScreenMetrics.Units(6);
            Canvas.SetLeft(OptionsIcon, center.X - side / 2);
            Canvas.SetTop(OptionsIcon, center.Y - side / 2);
            OptionsIcon.Visibility = Visibility.Visible;

            // L'anneau entoure le carré : rayon = demi-diagonale du carré.
            DrawArc(OptionsIconRing, xPixels, yPixels, sizePixels / 2 * Math.Sqrt(2) + ScreenMetrics.Px(4), progress);

            if (showLabel)
            {
                // Le libellé suit la taille du texte de Windows (étape 11, voir Ecran.Police).
                OptionsLabelText.FontSize = ScreenMetrics.Font(20);
                OptionsLabel.CornerRadius = new CornerRadius(ScreenMetrics.Units(6));
                OptionsLabel.Padding = new Thickness(ScreenMetrics.Units(8), ScreenMetrics.Units(2), ScreenMetrics.Units(8), ScreenMetrics.Units(2));
                // Mesure le libellé pour le centrer sous l'icône.
                OptionsLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(OptionsLabel, center.X - OptionsLabel.DesiredSize.Width / 2);
                Canvas.SetTop(OptionsLabel, center.Y + side / 2 + ScreenMetrics.Units(12));
                OptionsLabel.Visibility = Visibility.Visible;
            }
            else
            {
                OptionsLabel.Visibility = Visibility.Collapsed;
            }
        }

        public void HideIcon()
        {
            OptionsIcon.Visibility = Visibility.Collapsed;
            OptionsIconRing.Visibility = Visibility.Collapsed;
            OptionsLabel.Visibility = Visibility.Collapsed;
        }

        /// <summary>Cache tout ce qui est dessiné sur le calque (écran noir compris).</summary>
        public void HideAll()
        {
            HideCircle();
            HideRing();
            HideIcon();
            HideChoiceScreen();
            HideMenuBar();
            HideDragSurface();
            HideKeyboardSurface();
        }

        // ───────────────────────── Écran noir du choix d'action (étape 3) ─────────────────────────

        /// <summary>
        /// Vrai quand le calque est réellement affiché à l'écran. Avant, aucune conversion de
        /// coordonnées n'est possible (PointFromScreen échouerait).
        /// </summary>
        public bool Ready
        {
            get { return PresentationSource.FromVisual(this) != null; }
        }

        /// <summary>
        /// Vide l'écran noir de son contenu précédent et lui donne la taille du calque.
        /// Il reste caché : le mode le remplit, puis appelle AfficherEcranChoix.
        /// </summary>
        public void PrepareChoiceScreen()
        {
            ChoiceScreen.Children.Clear();
            ChoiceScreen.Width = ActualWidth;
            ChoiceScreen.Height = ActualHeight;
            Canvas.SetLeft(ChoiceScreen, 0);
            Canvas.SetTop(ChoiceScreen, 0);
        }

        public void ShowChoiceScreen()
        {
            ChoiceScreen.Visibility = Visibility.Visible;
        }

        /// <summary>Cache l'écran noir (son contenu est gardé jusqu'au prochain PreparerEcranChoix).</summary>
        public void HideChoiceScreen()
        {
            ChoiceScreen.Visibility = Visibility.Collapsed;
        }

        // ───────────────────────── Barre de menu (étape 4) ─────────────────────────

        /// <summary>
        /// Vide la surface de la barre de menu et lui donne la taille du calque.
        /// Elle reste cachée : le mode menu la remplit, puis appelle AfficherBarreMenu.
        /// </summary>
        public void PrepareMenuBar()
        {
            MenuBar.Children.Clear();
            MenuBar.Width = ActualWidth;
            MenuBar.Height = ActualHeight;
            Canvas.SetLeft(MenuBar, 0);
            Canvas.SetTop(MenuBar, 0);
        }

        /// <summary>Durée de l'animation d'apparition de la barre de menu, en millisecondes.</summary>
        private const double MenuAppearDurationMs = 250;

        /// <summary>
        /// Distance parcourue par la barre pendant l'apparition, en unités WPF : elle monte
        /// depuis un peu plus bas que sa place finale.
        /// </summary>
        private const double MenuAppearRise = 80;

        /// <summary>
        /// Affiche la barre de menu.
        /// anime = vrai : petite animation d'apparition (demande de l'utilisateur du 28/10/2026) :
        /// la barre monte de [MonteeApparitionMenu] unités jusqu'à sa place, en passant de
        /// transparente à opaque, en [DureeApparitionMenuMs] ms. Elle ralentit à la fin
        /// (« EaseOut »), ce qui paraît plus naturel qu'une vitesse constante.
        /// Ce sont les animations intégrées à WPF : aucune dépendance en plus.
        /// Les zones d'activation des boutons sont tout de suite à leur place finale : pendant
        /// ce quart de seconde, seul le dessin bouge (le délai d'activation est bien plus long).
        /// </summary>
        public void ShowMenuBar(bool animate = false)
        {
            // Une fermeture animée encore en cours est annulée : sa fin ne doit pas cacher
            // la barre qu'on est en train d'afficher.
            _menuClosing = null;
            MenuBar.Visibility = Visibility.Visible;

            // Un TranslateTransform décale le dessin de la surface sans changer sa position.
            TranslateTransform offset = MenuBar.RenderTransform as TranslateTransform;
            if (offset == null)
            {
                offset = new TranslateTransform();
                MenuBar.RenderTransform = offset;
            }

            if (!animate)
            {
                // Pas d'animation (par exemple la barre reconstruite pour « Oui / Non ») : si une
                // apparition est encore en cours, on la laisse finir.
                return;
            }

            Duration duration = new Duration(TimeSpan.FromMilliseconds(MenuAppearDurationMs));
            CubicEase slowed = new CubicEase();
            slowed.EasingMode = EasingMode.EaseOut;

            DoubleAnimation rise = new DoubleAnimation(ScreenMetrics.Units(MenuAppearRise), 0, duration);
            rise.EasingFunction = slowed;
            DoubleAnimation fade = new DoubleAnimation(0, 1, duration);
            fade.EasingFunction = slowed;

            // BeginAnimation remplace une animation déjà en cours sur la même propriété.
            offset.BeginAnimation(TranslateTransform.YProperty, rise);
            MenuBar.BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>Durée de l'animation de fermeture de la barre de menu, en millisecondes.</summary>
        private const double MenuCloseDurationMs = 200;

        // L'animation de fermeture en cours, ou null. Sert à reconnaître, quand une animation se
        // termine, si c'est bien la dernière fermeture demandée (et pas une fermeture annulée
        // parce que le menu a été rouvert entre-temps).
        private DoubleAnimation _menuClosing;

        /// <summary>
        /// Cache la barre de menu (son contenu est gardé jusqu'au prochain PreparerBarreMenu).
        /// anime = vrai : petite animation de fermeture (demande de l'utilisateur du 28/10/2026),
        /// l'inverse de l'apparition : la barre redescend de [MonteeApparitionMenu] unités en
        /// devenant transparente, en [DureeFermetureMenuMs] ms, en accélérant (« EaseIn »).
        /// Les boutons ne réagissent déjà plus au regard : seul le dessin s'efface.
        /// anime = faux (par défaut) : la barre disparaît tout de suite. C'est le cas de
        /// MasquerTout, appelé par exemple avant une photo d'écran : là, il ne faut rien attendre.
        /// </summary>
        public void HideMenuBar(bool animate = false)
        {
            if (!animate || MenuBar.Visibility != Visibility.Visible)
            {
                _menuClosing = null;
                MenuBar.Visibility = Visibility.Collapsed;
                RestoreMenuBar();
                return;
            }

            TranslateTransform offset = MenuBar.RenderTransform as TranslateTransform;
            if (offset == null)
            {
                offset = new TranslateTransform();
                MenuBar.RenderTransform = offset;
            }

            Duration duration = new Duration(TimeSpan.FromMilliseconds(MenuCloseDurationMs));
            CubicEase accelerated = new CubicEase();
            accelerated.EasingMode = EasingMode.EaseIn;

            // « null » comme point de départ : l'animation part de la valeur actuelle (utile si
            // l'apparition n'était pas finie).
            DoubleAnimation descent = new DoubleAnimation();
            descent.To = ScreenMetrics.Units(MenuAppearRise);
            descent.Duration = duration;
            descent.EasingFunction = accelerated;
            DoubleAnimation fade = new DoubleAnimation();
            fade.To = 0;
            fade.Duration = duration;
            fade.EasingFunction = accelerated;

            // À la fin du fondu : la barre est cachée pour de bon, sauf si le menu a été
            // rouvert entre-temps (alors _fermetureMenu a changé).
            _menuClosing = fade;
            fade.Completed += (s, e) =>
            {
                if (_menuClosing == fade)
                {
                    _menuClosing = null;
                    MenuBar.Visibility = Visibility.Collapsed;
                    RestoreMenuBar();
                }
            };

            offset.BeginAnimation(TranslateTransform.YProperty, descent);
            MenuBar.BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>
        /// Arrête les animations de la barre et la remet à sa place, opaque. Sans cela, la fin
        /// d'une animation « garde » sa dernière valeur (barre transparente et décalée).
        /// BeginAnimation(…, null) retire l'animation : la propriété retrouve sa vraie valeur.
        /// </summary>
        private void RestoreMenuBar()
        {
            MenuBar.BeginAnimation(OpacityProperty, null);
            TranslateTransform offset = MenuBar.RenderTransform as TranslateTransform;
            if (offset != null)
            {
                offset.BeginAnimation(TranslateTransform.YProperty, null);
                offset.Y = 0;
            }
            MenuBar.Opacity = 1;
        }

        // ───────────────────────── Surface du glisser-déposer (étape 6) ─────────────────────────

        /// <summary>
        /// Vide la surface du glisser-déposer et lui donne la taille du calque.
        /// Elle reste cachée : le mode la remplit, puis appelle AfficherSurfaceGlisser.
        /// </summary>
        public void PrepareDragSurface()
        {
            DragSurface.Children.Clear();
            DragSurface.Width = ActualWidth;
            DragSurface.Height = ActualHeight;
            Canvas.SetLeft(DragSurface, 0);
            Canvas.SetTop(DragSurface, 0);
        }

        public void ShowDragSurface()
        {
            DragSurface.Visibility = Visibility.Visible;
        }

        /// <summary>Cache la surface du glisser-déposer (son contenu est gardé jusqu'au prochain PreparerSurfaceGlisser).</summary>
        public void HideDragSurface()
        {
            DragSurface.Visibility = Visibility.Collapsed;
        }

        // ───────────────────────── Surface du clavier au regard (étape 12) ─────────────────────────

        /// <summary>
        /// Vide la surface du clavier et lui donne la taille du calque.
        /// Elle reste cachée : ClavierRegard la remplit, puis appelle AfficherSurfaceClavier.
        /// </summary>
        public void PrepareKeyboardSurface()
        {
            KeyboardSurface.Children.Clear();
            KeyboardSurface.Width = ActualWidth;
            KeyboardSurface.Height = ActualHeight;
            Canvas.SetLeft(KeyboardSurface, 0);
            Canvas.SetTop(KeyboardSurface, 0);
        }

        public void ShowKeyboardSurface()
        {
            KeyboardSurface.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Fait glisser le clavier jusqu'à sa nouvelle place (demande de l'utilisateur du
        /// 02/11/2026) : il vient d'être redessiné à sa place finale, et on le décale d'abord
        /// de « depuisPixels » (pixels d'écran, positif = plus bas), puis ce décalage revient à
        /// zéro. Monter : à la vitesse de l'apparition de la barre de menu, en ralentissant à la
        /// fin. Descendre : à la vitesse de sa fermeture, en accélérant. Comme pour la barre, un
        /// TranslateTransform décale le dessin sans changer les positions calculées.
        /// </summary>
        public void AnimateKeyboardSurface(double fromPixels)
        {
            TranslateTransform offset = KeyboardSurface.RenderTransform as TranslateTransform;
            if (offset == null)
            {
                offset = new TranslateTransform();
                KeyboardSurface.RenderTransform = offset;
            }

            bool rising = fromPixels > 0;
            Duration duration = new Duration(TimeSpan.FromMilliseconds(rising ? MenuAppearDurationMs : MenuCloseDurationMs));
            CubicEase curve = new CubicEase();
            curve.EasingMode = rising ? EasingMode.EaseOut : EasingMode.EaseIn;

            DoubleAnimation drag = new DoubleAnimation(LengthToWpf(fromPixels), 0, duration);
            drag.EasingFunction = curve;
            // BeginAnimation remplace une animation déjà en cours (barre rouverte très vite).
            offset.BeginAnimation(TranslateTransform.YProperty, drag);
        }

        /// <summary>Cache le clavier (son contenu est gardé jusqu'au prochain PreparerSurfaceClavier).</summary>
        public void HideKeyboardSurface()
        {
            KeyboardSurface.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Convertit un point de l'écran (pixels réels) en position dans le calque (unités WPF).
        /// À n'utiliser que si Pret est vrai.
        /// </summary>
        public Point ToWpf(double xPixels, double yPixels)
        {
            return PointFromScreen(new Point(xPixels, yPixels));
        }

        /// <summary>Convertit une longueur en pixels réels en longueur en unités WPF.</summary>
        public double LengthToWpf(double pixels)
        {
            return PixelsToWpf(pixels);
        }

        /// <summary>
        /// Dessine dans « chemin » un arc de cercle : il part du haut et tourne dans le sens
        /// des aiguilles d'une montre, sur une fraction « progression » du tour.
        ///
        /// En WPF, un arc se décrit par un point de départ et un ArcSegment (point d'arrivée,
        /// rayons, sens de rotation, et « grand arc » si l'on dépasse un demi-tour).
        /// Un tour complet ne peut pas s'écrire avec un seul arc (départ = arrivée) : on dessine
        /// alors simplement un cercle entier.
        /// </summary>
        private void DrawArc(Path path, double xPixels, double yPixels, double radiusPixels, double progress)
        {
            if (progress <= 0 || PresentationSource.FromVisual(this) == null)
            {
                path.Visibility = Visibility.Collapsed;
                return;
            }

            Point center = PointFromScreen(new Point(xPixels, yPixels));
            double radius = PixelsToWpf(radiusPixels);

            path.Data = ArcShape(center, radius, progress);
            path.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// La forme d'un arc qui part du haut et tourne dans le sens des aiguilles d'une montre,
        /// sur une fraction « progression » du tour (centre et rayon en unités WPF).
        /// « static » et « internal » : les boutons du choix d'action (BoutonRegard) s'en
        /// servent aussi pour leur anneau de progression.
        /// </summary>
        internal static Geometry ArcShape(Point center, double radius, double progress)
        {
            Geometry shape;
            if (progress >= 0.999)
            {
                shape = new EllipseGeometry(center, radius, radius);
            }
            else
            {
                double angle = progress * 2 * Math.PI;   // en radians, 0 = en haut
                Point startPoint = new Point(center.X, center.Y - radius);
                Point arrival = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));

                PathFigure figure = new PathFigure();
                figure.StartPoint = startPoint;
                figure.IsClosed = false;
                figure.IsFilled = false;
                figure.Segments.Add(new ArcSegment(
                    arrival, new Size(radius, radius), 0, angle > Math.PI, SweepDirection.Clockwise, true));

                PathGeometry paths = new PathGeometry();
                paths.Figures.Add(figure);
                shape = paths;
            }

            shape.Freeze();
            return shape;
        }

        /// <summary>Positionne une ellipse : son coin en haut à gauche est à (centre − rayon).</summary>
        private static void PlaceEllipse(Ellipse ellipse, Point center, double radius)
        {
            ellipse.Width = radius * 2;
            ellipse.Height = radius * 2;
            Canvas.SetLeft(ellipse, center.X - radius);
            Canvas.SetTop(ellipse, center.Y - radius);
            ellipse.Visibility = Visibility.Visible;
        }

        /// <summary>Convertit une longueur en pixels réels en longueur en unités WPF.</summary>
        private double PixelsToWpf(double pixels)
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null || source.CompositionTarget == null)
            {
                return pixels;
            }
            // M11 est le facteur de conversion horizontal (0,8 à 125 %, 0,667 à 150 %…).
            return pixels * source.CompositionTarget.TransformFromDevice.M11;
        }
    }
}
