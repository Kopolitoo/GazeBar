using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

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
        public OverlayWindow()
        {
            InitializeComponent();
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

        /// <summary>
        /// Affiche le cercle centré sur (x, y), avec le rayon donné.
        /// Tout est en PIXELS D'ÉCRAN, comme le regard et le curseur.
        /// </summary>
        public void AfficherCercle(double xPixels, double yPixels, double rayonPixels)
        {
            // Pas encore affichée à l'écran : rien à dessiner (PointFromScreen échouerait).
            if (PresentationSource.FromVisual(this) == null)
            {
                return;
            }

            // PointFromScreen convertit un point de l'écran (pixels réels) en position
            // dans cette fenêtre (unités WPF). Il tient compte de la mise à l'échelle.
            Point centre = PointFromScreen(new Point(xPixels, yPixels));
            double rayon = PixelsVersWpf(rayonPixels);

            PlacerEllipse(CercleFond, centre, rayon);
            PlacerEllipse(Cercle, centre, rayon);
        }

        /// <summary>Cache le cercle.</summary>
        public void MasquerCercle()
        {
            CercleFond.Visibility = Visibility.Collapsed;
            Cercle.Visibility = Visibility.Collapsed;
        }

        /// <summary>Positionne une ellipse : son coin en haut à gauche est à (centre − rayon).</summary>
        private static void PlacerEllipse(System.Windows.Shapes.Ellipse ellipse, Point centre, double rayon)
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
