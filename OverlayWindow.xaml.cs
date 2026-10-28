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
    /// le regard Tobii et le curseur). C'est cette fenêtre qui fait la conversion vers les
    /// unités de WPF.
    ///
    /// Pourquoi une conversion ? Quand Windows agrandit l'affichage (125 %, 150 %…), WPF ne
    /// compte plus en pixels mais en « unités indépendantes » (1 unité = 1/96 de pouce).
    /// À 150 %, 100 pixels réels ne valent que 66,7 unités WPF. Sans conversion, le cercle
    /// serait décalé par rapport au regard, d'autant plus qu'on s'éloigne du coin en haut à gauche.
    /// </summary>
    internal partial class OverlayWindow : Window
    {
        private readonly DispatcherTimer _premierPlan;

        public OverlayWindow()
        {
            InitializeComponent();

            // Plusieurs fenêtres peuvent être « toujours au premier plan » (la barre d'outils,
            // la fenêtre Diagnostic, d'autres logiciels…). Entre elles, c'est la dernière
            // affichée ou activée qui gagne. Pour que le calque reste AU-DESSUS DE TOUT, on le
            // remet en haut de la pile deux fois par seconde.
            _premierPlan = new DispatcherTimer();
            _premierPlan.Interval = TimeSpan.FromMilliseconds(500);
            _premierPlan.Tick += (sender, e) => RemettreAuPremierPlan();
            _premierPlan.Start();
        }

        /// <summary>
        /// Place le calque tout en haut des fenêtres « toujours au premier plan », sans le
        /// déplacer, sans changer sa taille et sans lui donner le focus.
        /// </summary>
        public void RemettreAuPremierPlan()
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

            CouvrirEcran();
        }

        /// <summary>
        /// Appelée par WPF si la mise à l'échelle de Windows change pendant que GazeBar tourne
        /// (par exemple de 100 % à 125 % dans les paramètres d'affichage). On recalcule la taille.
        /// </summary>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            CouvrirEcran();
        }

        /// <summary>
        /// Place la fenêtre sur tout l'écran principal.
        ///
        /// GetSystemMetrics donne la taille de l'écran en pixels réels (GazeBar est déclaré
        /// « sensible au DPI » dans app.manifest). TransformFromDevice est la conversion
        /// « pixels réels → unités WPF » fournie par WPF pour cette fenêtre.
        /// </summary>
        private void CouvrirEcran()
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null || source.CompositionTarget == null)
            {
                return;
            }

            double largeurPixels = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            double hauteurPixels = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

            Matrix versWpf = source.CompositionTarget.TransformFromDevice;
            Point taille = versWpf.Transform(new Point(largeurPixels, hauteurPixels));

            Left = 0;
            Top = 0;
            Width = taille.X;
            Height = taille.Y;

            App.Log("Calque : écran " + largeurPixels + " × " + hauteurPixels + " pixels = " +
                    taille.X.ToString("F0") + " × " + taille.Y.ToString("F0") + " unités WPF (échelle " +
                    (largeurPixels / taille.X * 100).ToString("F0") + " %)");
        }

        // ───────────────────────── Dessin ─────────────────────────

        // Couleurs du trait de dessus du cercle : vert discret en mouvement, blanc en regard fixe.
        // « Freeze » fige le pinceau : WPF le dessine alors plus vite.
        private static readonly SolidColorBrush PinceauMouvement = Figer(new SolidColorBrush(Colors.DarkGray));
        private static readonly SolidColorBrush PinceauFixe = Figer(new SolidColorBrush(Colors.LightGray));

        private static SolidColorBrush Figer(SolidColorBrush pinceau)
        {
            pinceau.Freeze();
            return pinceau;
        }

        /// <summary>
        /// Affiche le cercle du mode mouvement (vert, discret) centré sur (x, y).
        /// Tout est en PIXELS D'ÉCRAN, comme le regard et le curseur.
        /// </summary>
        public void AfficherCercle(double xPixels, double yPixels, double rayonPixels)
        {
            AfficherCercle(xPixels, yPixels, rayonPixels, false);
        }

        /// <summary>
        /// Affiche le cercle centré sur (x, y), avec le rayon donné.
        /// fixe = vrai : cercle du mode regard fixe (trait blanc et opaque).
        /// </summary>
        public void AfficherCercle(double xPixels, double yPixels, double rayonPixels, bool fixe)
        {
            AfficherCercle(xPixels, yPixels, rayonPixels, fixe, 1.0);
        }

        /// <summary>
        /// Comme ci-dessus, avec une opacité réduite : attenuation = 1 → opacité habituelle,
        /// 0.5 → deux fois plus transparent (mode défilement, demande du 28/10/2026).
        /// </summary>
        public void AfficherCercle(double xPixels, double yPixels, double rayonPixels, bool fixe,
                                   double attenuation)
        {
            CercleX = xPixels;
            CercleY = yPixels;
            CercleAffiche = true;

            Cercle.Stroke = fixe ? PinceauFixe : PinceauMouvement;
            Cercle.StrokeThickness = fixe ? 1.5 : 1;
            Cercle.Opacity = (fixe ? 0.6 : 0.3) * attenuation;
            CercleFond.StrokeThickness = fixe ? 2.5 : 2;
            CercleFond.Opacity = (fixe ? 0.7 : 0.5) * attenuation;

            // Pas encore affichée à l'écran : rien à dessiner (PointFromScreen échouerait).
            if (PresentationSource.FromVisual(this) == null)
            {
                return;
            }

            // PointFromScreen convertit un point de l'écran (pixels réels) en position
            // dans cette fenêtre (unités WPF). Il tient compte de la mise à l'échelle.
            Point centre = PointFromScreen(new Point(xPixels, yPixels));
            double rayon = PixelsVersWpf(rayonPixels);

            PlacerEllipse(CercleFond, centre, PixelsVersWpf(rayonPixels + 2));
            PlacerEllipse(Cercle, centre, rayon);
        }

        /// <summary>Centre du cercle tel qu'il est dessiné, en pixels d'écran (valable si CercleAffiche).</summary>
        public double CercleX { get; private set; }

        public double CercleY { get; private set; }

        /// <summary>
        /// Vrai si le cercle est visible. Un mode qui commence s'en sert pour reprendre le
        /// cercle là où il est, au lieu de le faire sauter ailleurs.
        /// </summary>
        public bool CercleAffiche { get; private set; }

        /// <summary>Cache le cercle.</summary>
        public void MasquerCercle()
        {
            CercleAffiche = false;
            CercleFond.Visibility = Visibility.Collapsed;
            Cercle.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Affiche l'anneau de progression sur le bord d'un cercle centré sur (x, y).
        /// progression va de 0 (rien) à 1 (tour complet).
        /// </summary>
        public void AfficherAnneau(double xPixels, double yPixels, double rayonPixels, double progression)
        {
            DessinerArc(Anneau, xPixels, yPixels, rayonPixels, progression);
        }

        public void MasquerAnneau()
        {
            Anneau.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Affiche l'icône d'options : un carré de côté [taille] centré sur (x, y), en pixels
        /// d'écran. progression (0 à 1) remplit l'anneau autour d'elle ; à 0, pas d'anneau.
        /// afficherLibelle : montre le texte « Options » sous l'icône.
        /// </summary>
        public void AfficherIcone(double xPixels, double yPixels, double taillePixels,
                                  double progression, bool afficherLibelle)
        {
            if (PresentationSource.FromVisual(this) == null)
            {
                return;
            }

            Point centre = PointFromScreen(new Point(xPixels, yPixels));
            double cote = PixelsVersWpf(taillePixels);

            Icone.Width = cote;
            Icone.Height = cote;
            IconeSymbole.FontSize = cote * 0.5;
            Canvas.SetLeft(Icone, centre.X - cote / 2);
            Canvas.SetTop(Icone, centre.Y - cote / 2);
            Icone.Visibility = Visibility.Visible;

            // L'anneau entoure le carré : rayon = demi-diagonale du carré.
            DessinerArc(AnneauIcone, xPixels, yPixels, taillePixels / 2 * Math.Sqrt(2) + 4, progression);

            if (afficherLibelle)
            {
                // Mesure le libellé pour le centrer sous l'icône.
                Libelle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(Libelle, centre.X - Libelle.DesiredSize.Width / 2);
                Canvas.SetTop(Libelle, centre.Y + cote / 2 + 12);
                Libelle.Visibility = Visibility.Visible;
            }
            else
            {
                Libelle.Visibility = Visibility.Collapsed;
            }
        }

        public void MasquerIcone()
        {
            Icone.Visibility = Visibility.Collapsed;
            AnneauIcone.Visibility = Visibility.Collapsed;
            Libelle.Visibility = Visibility.Collapsed;
        }

        /// <summary>Cache tout ce qui est dessiné sur le calque (écran noir compris).</summary>
        public void MasquerTout()
        {
            MasquerCercle();
            MasquerAnneau();
            MasquerIcone();
            MasquerEcranChoix();
            MasquerBarreMenu();
            MasquerSurfaceGlisser();
        }

        // ───────────────────────── Écran noir du choix d'action (étape 3) ─────────────────────────

        /// <summary>
        /// Vrai quand le calque est réellement affiché à l'écran. Avant, aucune conversion de
        /// coordonnées n'est possible (PointFromScreen échouerait).
        /// </summary>
        public bool Pret
        {
            get { return PresentationSource.FromVisual(this) != null; }
        }

        /// <summary>
        /// Vide l'écran noir de son contenu précédent et lui donne la taille du calque.
        /// Il reste caché : le mode le remplit, puis appelle AfficherEcranChoix.
        /// </summary>
        public void PreparerEcranChoix()
        {
            EcranChoix.Children.Clear();
            EcranChoix.Width = ActualWidth;
            EcranChoix.Height = ActualHeight;
            Canvas.SetLeft(EcranChoix, 0);
            Canvas.SetTop(EcranChoix, 0);
        }

        public void AfficherEcranChoix()
        {
            EcranChoix.Visibility = Visibility.Visible;
        }

        /// <summary>Cache l'écran noir (son contenu est gardé jusqu'au prochain PreparerEcranChoix).</summary>
        public void MasquerEcranChoix()
        {
            EcranChoix.Visibility = Visibility.Collapsed;
        }

        // ───────────────────────── Barre de menu (étape 4) ─────────────────────────

        /// <summary>
        /// Vide la surface de la barre de menu et lui donne la taille du calque.
        /// Elle reste cachée : le mode menu la remplit, puis appelle AfficherBarreMenu.
        /// </summary>
        public void PreparerBarreMenu()
        {
            BarreMenu.Children.Clear();
            BarreMenu.Width = ActualWidth;
            BarreMenu.Height = ActualHeight;
            Canvas.SetLeft(BarreMenu, 0);
            Canvas.SetTop(BarreMenu, 0);
        }

        /// <summary>Durée de l'animation d'apparition de la barre de menu, en millisecondes.</summary>
        private const double DureeApparitionMenuMs = 250;

        /// <summary>
        /// Distance parcourue par la barre pendant l'apparition, en unités WPF : elle monte
        /// depuis un peu plus bas que sa place finale.
        /// </summary>
        private const double MonteeApparitionMenu = 80;

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
        public void AfficherBarreMenu(bool anime = false)
        {
            // Une fermeture animée encore en cours est annulée : sa fin ne doit pas cacher
            // la barre qu'on est en train d'afficher.
            _fermetureMenu = null;
            BarreMenu.Visibility = Visibility.Visible;

            // Un TranslateTransform décale le dessin de la surface sans changer sa position.
            TranslateTransform decalage = BarreMenu.RenderTransform as TranslateTransform;
            if (decalage == null)
            {
                decalage = new TranslateTransform();
                BarreMenu.RenderTransform = decalage;
            }

            if (!anime)
            {
                // Pas d'animation (par exemple la barre reconstruite pour « Oui / Non ») : si une
                // apparition est encore en cours, on la laisse finir.
                return;
            }

            Duration duree = new Duration(TimeSpan.FromMilliseconds(DureeApparitionMenuMs));
            CubicEase ralenti = new CubicEase();
            ralenti.EasingMode = EasingMode.EaseOut;

            DoubleAnimation montee = new DoubleAnimation(MonteeApparitionMenu, 0, duree);
            montee.EasingFunction = ralenti;
            DoubleAnimation fondu = new DoubleAnimation(0, 1, duree);
            fondu.EasingFunction = ralenti;

            // BeginAnimation remplace une animation déjà en cours sur la même propriété.
            decalage.BeginAnimation(TranslateTransform.YProperty, montee);
            BarreMenu.BeginAnimation(OpacityProperty, fondu);
        }

        /// <summary>Durée de l'animation de fermeture de la barre de menu, en millisecondes.</summary>
        private const double DureeFermetureMenuMs = 200;

        // L'animation de fermeture en cours, ou null. Sert à reconnaître, quand une animation se
        // termine, si c'est bien la dernière fermeture demandée (et pas une fermeture annulée
        // parce que le menu a été rouvert entre-temps).
        private DoubleAnimation _fermetureMenu;

        /// <summary>
        /// Cache la barre de menu (son contenu est gardé jusqu'au prochain PreparerBarreMenu).
        /// anime = vrai : petite animation de fermeture (demande de l'utilisateur du 28/10/2026),
        /// l'inverse de l'apparition : la barre redescend de [MonteeApparitionMenu] unités en
        /// devenant transparente, en [DureeFermetureMenuMs] ms, en accélérant (« EaseIn »).
        /// Les boutons ne réagissent déjà plus au regard : seul le dessin s'efface.
        /// anime = faux (par défaut) : la barre disparaît tout de suite. C'est le cas de
        /// MasquerTout, appelé par exemple avant une photo d'écran : là, il ne faut rien attendre.
        /// </summary>
        public void MasquerBarreMenu(bool anime = false)
        {
            if (!anime || BarreMenu.Visibility != Visibility.Visible)
            {
                _fermetureMenu = null;
                BarreMenu.Visibility = Visibility.Collapsed;
                RemettreBarreMenuEnPlace();
                return;
            }

            TranslateTransform decalage = BarreMenu.RenderTransform as TranslateTransform;
            if (decalage == null)
            {
                decalage = new TranslateTransform();
                BarreMenu.RenderTransform = decalage;
            }

            Duration duree = new Duration(TimeSpan.FromMilliseconds(DureeFermetureMenuMs));
            CubicEase accelere = new CubicEase();
            accelere.EasingMode = EasingMode.EaseIn;

            // « null » comme point de départ : l'animation part de la valeur actuelle (utile si
            // l'apparition n'était pas finie).
            DoubleAnimation descente = new DoubleAnimation();
            descente.To = MonteeApparitionMenu;
            descente.Duration = duree;
            descente.EasingFunction = accelere;
            DoubleAnimation fondu = new DoubleAnimation();
            fondu.To = 0;
            fondu.Duration = duree;
            fondu.EasingFunction = accelere;

            // À la fin du fondu : la barre est cachée pour de bon, sauf si le menu a été
            // rouvert entre-temps (alors _fermetureMenu a changé).
            _fermetureMenu = fondu;
            fondu.Completed += (s, e) =>
            {
                if (_fermetureMenu == fondu)
                {
                    _fermetureMenu = null;
                    BarreMenu.Visibility = Visibility.Collapsed;
                    RemettreBarreMenuEnPlace();
                }
            };

            decalage.BeginAnimation(TranslateTransform.YProperty, descente);
            BarreMenu.BeginAnimation(OpacityProperty, fondu);
        }

        /// <summary>
        /// Arrête les animations de la barre et la remet à sa place, opaque. Sans cela, la fin
        /// d'une animation « garde » sa dernière valeur (barre transparente et décalée).
        /// BeginAnimation(…, null) retire l'animation : la propriété retrouve sa vraie valeur.
        /// </summary>
        private void RemettreBarreMenuEnPlace()
        {
            BarreMenu.BeginAnimation(OpacityProperty, null);
            TranslateTransform decalage = BarreMenu.RenderTransform as TranslateTransform;
            if (decalage != null)
            {
                decalage.BeginAnimation(TranslateTransform.YProperty, null);
                decalage.Y = 0;
            }
            BarreMenu.Opacity = 1;
        }

        // ───────────────────────── Surface du glisser-déposer (étape 6) ─────────────────────────

        /// <summary>
        /// Vide la surface du glisser-déposer et lui donne la taille du calque.
        /// Elle reste cachée : le mode la remplit, puis appelle AfficherSurfaceGlisser.
        /// </summary>
        public void PreparerSurfaceGlisser()
        {
            SurfaceGlisser.Children.Clear();
            SurfaceGlisser.Width = ActualWidth;
            SurfaceGlisser.Height = ActualHeight;
            Canvas.SetLeft(SurfaceGlisser, 0);
            Canvas.SetTop(SurfaceGlisser, 0);
        }

        public void AfficherSurfaceGlisser()
        {
            SurfaceGlisser.Visibility = Visibility.Visible;
        }

        /// <summary>Cache la surface du glisser-déposer (son contenu est gardé jusqu'au prochain PreparerSurfaceGlisser).</summary>
        public void MasquerSurfaceGlisser()
        {
            SurfaceGlisser.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Convertit un point de l'écran (pixels réels) en position dans le calque (unités WPF).
        /// À n'utiliser que si Pret est vrai.
        /// </summary>
        public Point VersWpf(double xPixels, double yPixels)
        {
            return PointFromScreen(new Point(xPixels, yPixels));
        }

        /// <summary>Convertit une longueur en pixels réels en longueur en unités WPF.</summary>
        public double LongueurVersWpf(double pixels)
        {
            return PixelsVersWpf(pixels);
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
        private void DessinerArc(Path chemin, double xPixels, double yPixels, double rayonPixels, double progression)
        {
            if (progression <= 0 || PresentationSource.FromVisual(this) == null)
            {
                chemin.Visibility = Visibility.Collapsed;
                return;
            }

            Point centre = PointFromScreen(new Point(xPixels, yPixels));
            double rayon = PixelsVersWpf(rayonPixels);

            chemin.Data = FormeArc(centre, rayon, progression);
            chemin.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// La forme d'un arc qui part du haut et tourne dans le sens des aiguilles d'une montre,
        /// sur une fraction « progression » du tour (centre et rayon en unités WPF).
        /// « static » et « internal » : les boutons du choix d'action (BoutonRegard) s'en
        /// servent aussi pour leur anneau de progression.
        /// </summary>
        internal static Geometry FormeArc(Point centre, double rayon, double progression)
        {
            Geometry forme;
            if (progression >= 0.999)
            {
                forme = new EllipseGeometry(centre, rayon, rayon);
            }
            else
            {
                double angle = progression * 2 * Math.PI;   // en radians, 0 = en haut
                Point depart = new Point(centre.X, centre.Y - rayon);
                Point arrivee = new Point(centre.X + rayon * Math.Sin(angle), centre.Y - rayon * Math.Cos(angle));

                PathFigure figure = new PathFigure();
                figure.StartPoint = depart;
                figure.IsClosed = false;
                figure.IsFilled = false;
                figure.Segments.Add(new ArcSegment(
                    arrivee, new Size(rayon, rayon), 0, angle > Math.PI, SweepDirection.Clockwise, true));

                PathGeometry chemins = new PathGeometry();
                chemins.Figures.Add(figure);
                forme = chemins;
            }

            forme.Freeze();
            return forme;
        }

        /// <summary>Positionne une ellipse : son coin en haut à gauche est à (centre − rayon).</summary>
        private static void PlacerEllipse(Ellipse ellipse, Point centre, double rayon)
        {
            ellipse.Width = rayon * 2;
            ellipse.Height = rayon * 2;
            Canvas.SetLeft(ellipse, centre.X - rayon);
            Canvas.SetTop(ellipse, centre.Y - rayon);
            ellipse.Visibility = Visibility.Visible;
        }

        /// <summary>Convertit une longueur en pixels réels en longueur en unités WPF.</summary>
        private double PixelsVersWpf(double pixels)
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
