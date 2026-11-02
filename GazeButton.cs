using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// INTERFACE : un bouton qu'on active au regard, dessiné sur le calque (étape 3).
    ///
    /// C'est un carré sombre aux coins arrondis, avec :
    ///   - un symbole de la police Windows (Segoe Fluent Icons ou Segoe MDL2 Assets) ;
    ///   - éventuellement une petite « pastille » de texte en bas à droite (« G », « D », « 2× »)
    ///     pour distinguer des actions qui partagent le même symbole ;
    ///   - un anneau de progression qui se remplit pendant qu'on le regarde ;
    ///   - un libellé en texte, affiché au survol si [AfficherLibelles] est activé.
    ///
    /// Un bouton peut être « vide » (méthode Vide) : c'est un emplacement auquel aucune action
    /// n'est encore attribuée. Il est dessiné grisé, avec un « + » pâle, pour montrer à
    /// l'utilisateur qu'il pourra y mettre une action (écran de configuration des icônes, étape 10). Il ne
    /// s'active jamais ; seul son libellé apparaît au survol.
    ///
    /// Un bouton peut aussi être « indisponible » (méthode Indisponible, étape 4) : sa fonction
    /// existe dans le cahier des charges mais n'est pas encore programmée (par exemple
    /// « Paramètres » dans la barre de menu). Il garde son vrai symbole, mais grisé, et ne
    /// s'active jamais non plus.
    ///
    /// Un bouton peut être « allumé » (méthode DefinirAllume, étape 4) : un interrupteur en
    /// position « oui », comme « Suivi souris » dans la barre de menu. Son bord devient vert.
    ///
    /// Un bouton peut afficher un mot au lieu d'un symbole (méthode Texte, étape 6) : c'est le
    /// cas des modificateurs « Ctrl », « Maj » et « Alt », qui n'ont pas de symbole parlant
    /// dans la police d'icônes de Windows.
    ///
    /// Étape 10, pour l'écran de configuration des icônes :
    ///   - un emplacement vide peut être « configurable » (méthode VideConfigurable) : grisé
    ///     comme un emplacement vide, mais il s'active (pour y placer une action) ;
    ///   - une pastille ronde en haut à droite (méthode DefinirPastilleConfig) : « + » vert
    ///     sur un emplacement vide, « − » rouge sur un emplacement assigné.
    ///
    /// Il ne sait pas lui-même s'il est regardé ni depuis combien de temps : c'est le mode
    /// (ModeChoixAction, ModeMenu) qui le décide, avec l'aide d'un ViseurBoutons, et qui lui
    /// dit quoi dessiner (méthode Dessiner).
    ///
    /// Les positions sont données en PIXELS D'ÉCRAN, comme le regard ; la conversion en unités
    /// WPF est faite ici, avec l'aide du calque.
    ///
    /// Étape 11 : les épaisseurs, coins arrondis, marges et la police du libellé sont des
    /// constantes en unités de l'écran de référence, converties avec Ecran.Unites et
    /// Ecran.Police au moment de Placer (voir Ecran.cs).
    ///
    /// Depuis l'étape 7, il « implémente » ICibleRegard : ViseurBoutons sait ainsi viser aussi
    /// bien un BoutonRegard qu'une cible du mode paramètres (CibleParametre).
    /// </summary>
    internal sealed class GazeButton : IGazeTarget
    {
        // Les éléments WPF qui composent le bouton.
        private readonly Border _background;
        private readonly TextBlock _symbol;
        private readonly TextBlock _badge;
        private readonly Path _ring;
        private readonly Border _label;

        // La pastille ronde « + » / « − » de l'écran de configuration (null = aucune).
        private Border _configBadge;
        private TextBlock _configSign;

        // Vrai pour un bouton dessiné grisé (emplacement vide, fonction indisponible).
        private bool _grayed;

        // Taille du symbole par rapport au côté du bouton (plus petite pour un mot, voir Texte).
        private double _symbolFactor = 0.45;

        // Couleurs (figées une fois pour toutes, WPF les dessine alors plus vite).
        private static readonly Brush NormalBackground = Freeze(new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24)));
        private static readonly Brush HoverBackground = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x3C, 0x40, 0x43)));
        private static readonly Brush White = Freeze(new SolidColorBrush(Colors.White));
        private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53)));
        private static readonly Brush EmptyBackground = Freeze(new SolidColorBrush(Color.FromArgb(0x80, 0x50, 0x50, 0x50)));
        private static readonly Brush EmptyHoverBackground = Freeze(new SolidColorBrush(Color.FromArgb(0xA0, 0x60, 0x60, 0x60)));
        private static readonly Brush Gray = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x90, 0x90, 0x90)));
        private static readonly Brush Red = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0x39, 0x35)));

        /// <summary>
        /// Crée un emplacement vide (grisé, jamais activé), avec ce libellé au survol.
        /// Symbole E710 = « + » (ajouter) de la police Windows.
        /// </summary>
        public static GazeButton Empty(string label)
        {
            return Unavailable("\uE710", label);
        }

        /// <summary>
        /// Crée un bouton qui affiche un mot (par exemple « Ctrl ») au lieu d'un symbole :
        /// police ordinaire de Windows (Segoe UI), en gras, plus petite pour tenir dans le carré.
        /// </summary>
        public static GazeButton Text(string word, string label)
        {
            GazeButton button = new GazeButton(word, null, label);
            button._symbol.FontFamily = new FontFamily("Segoe UI");
            button._symbol.FontWeight = FontWeights.Bold;
            button._symbolFactor = 0.28;
            return button;
        }

        /// <summary>
        /// Crée un bouton grisé qui ne s'active jamais, avec son vrai symbole : pour une
        /// fonction prévue mais pas encore programmée (étape 4 : « Paramètres », « Jeu »).
        /// </summary>
        public static GazeButton Unavailable(string symbol, string label)
        {
            GazeButton button = new GazeButton(symbol, null, label);
            button.Active = false;
            button._grayed = true;
            button._background.Background = EmptyBackground;
            button._background.BorderBrush = Gray;
            button._symbol.Foreground = Gray;
            button._symbol.Opacity = 0.6;
            return button;
        }

        /// <summary>
        /// Crée un emplacement vide de l'écran de configuration (étape 10) : grisé, sans symbole
        /// (la pastille « + » suffit), mais qui s'active, pour y placer une action.
        /// </summary>
        public static GazeButton ConfigurableEmpty(string label)
        {
            GazeButton button = Unavailable("", label);
            button.Active = true;
            return button;
        }

        /// <summary>
        /// Ajoute la pastille ronde de l'écran de configuration (étape 10), en haut à droite :
        /// plus = vrai → « + » sur fond vert (emplacement vide, on peut y placer une action) ;
        /// plus = faux → « − » sur fond rouge (action assignée, on peut la retirer).
        /// À appeler AVANT Placer. (« − » est le vrai signe moins, U+2212.)
        /// </summary>
        public void SetConfigBadge(bool plus)
        {
            _configSign = new TextBlock();
            _configSign.Text = plus ? "+" : "\u2212";
            _configSign.Foreground = White;
            _configSign.FontFamily = new FontFamily("Segoe UI");
            _configSign.FontWeight = FontWeights.Bold;
            _configSign.HorizontalAlignment = HorizontalAlignment.Center;
            _configSign.VerticalAlignment = VerticalAlignment.Center;
            // Le signe est un peu trop bas dans sa ligne de texte : on le remonte légèrement.
            _configSign.Margin = new Thickness(0, 0, 0, ScreenMetrics.Units(4));

            _configBadge = new Border();
            _configBadge.Background = plus ? Green : Red;
            _configBadge.BorderBrush = White;
            _configBadge.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            _configBadge.Child = _configSign;
        }

        /// <summary>
        /// Crée le bouton.
        /// symbole : le caractère de la police d'icônes (par exemple "" pour « retour ») ;
        /// pastille : un texte court, ou null pour aucun ;
        /// libelle : le texte affiché au survol.
        /// </summary>
        public GazeButton(string symbol, string badge, string label)
        {
            Label = label;
            Active = true;

            _symbol = new TextBlock();
            _symbol.Text = symbol;
            _symbol.Foreground = White;
            _symbol.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            _symbol.HorizontalAlignment = HorizontalAlignment.Center;
            _symbol.VerticalAlignment = VerticalAlignment.Center;

            _badge = new TextBlock();
            _badge.Text = badge ?? "";
            _badge.Foreground = White;
            _badge.FontWeight = FontWeights.Bold;
            _badge.HorizontalAlignment = HorizontalAlignment.Right;
            _badge.VerticalAlignment = VerticalAlignment.Bottom;
            _badge.Margin = new Thickness(0, 0, ScreenMetrics.Units(8), ScreenMetrics.Units(4));
            _badge.Visibility = badge == null ? Visibility.Collapsed : Visibility.Visible;

            // Une grille permet de superposer le symbole (au centre) et la pastille (dans le coin).
            Grid content = new Grid();
            content.Children.Add(_symbol);
            content.Children.Add(_badge);

            _background = new Border();
            _background.Background = NormalBackground;
            _background.BorderBrush = White;
            _background.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            _background.CornerRadius = new CornerRadius(ScreenMetrics.Units(12));
            _background.Child = content;

            _ring = new Path();
            _ring.Stroke = Green;
            _ring.StrokeThickness = ScreenMetrics.Units(6);
            _ring.StrokeStartLineCap = PenLineCap.Round;
            _ring.StrokeEndLineCap = PenLineCap.Round;
            _ring.Visibility = Visibility.Collapsed;

            TextBlock text = new TextBlock();
            text.Text = label;
            text.Foreground = White;
            // La police du libellé suit aussi la taille du texte de Windows (étape 11).
            text.FontSize = ScreenMetrics.Font(20);
            _label = new Border();
            _label.Background = NormalBackground;
            _label.CornerRadius = new CornerRadius(ScreenMetrics.Units(6));
            _label.Padding = new Thickness(ScreenMetrics.Units(8), ScreenMetrics.Units(2), ScreenMetrics.Units(8), ScreenMetrics.Units(2));
            _label.Child = text;
            _label.Visibility = Visibility.Collapsed;
        }

        /// <summary>Faux pour un emplacement vide ou indisponible : il ne s'active jamais (pas d'anneau).</summary>
        public bool Active { get; private set; }

        /// <summary>Vrai pour un interrupteur en position « oui » (bord vert). Voir DefinirAllume.</summary>
        public bool Lit { get; private set; }

        /// <summary>
        /// Allume ou éteint le bouton : un bouton allumé a un bord vert épais, pour montrer
        /// qu'une option est activée (par exemple « Suivi souris »).
        /// </summary>
        public void SetLit(bool lit)
        {
            Lit = lit;
            if (Active)
            {
                _background.BorderBrush = lit ? Green : White;
                _background.BorderThickness = new Thickness(ScreenMetrics.Units(lit ? 5 : 2));
            }
        }

        /// <summary>Le texte affiché au survol (et écrit dans le journal à l'activation).</summary>
        public string Label { get; private set; }

        /// <summary>Centre du bouton, en pixels d'écran.</summary>
        public double X { get; private set; }

        public double Y { get; private set; }

        /// <summary>Côté du bouton, en pixels d'écran.</summary>
        public double Size { get; private set; }

        /// <summary>
        /// Ajoute le bouton sur une surface du calque (par exemple l'écran noir) et le place,
        /// centré sur (x, y) en pixels d'écran, avec un côté de [taille] pixels.
        /// libelleAuDessus : le libellé est affiché au-dessus du bouton au lieu d'en dessous
        /// (pour la barre de menu, collée au bas de l'écran : en dessous, il serait caché).
        /// « = false » : paramètre facultatif ; si on ne le donne pas, il vaut faux.
        /// </summary>
        public void Place(OverlayWindow layer, Canvas surface, double x, double y, double size,
                           bool labelAbove = false)
        {
            X = x;
            Y = y;
            Size = size;

            Point center = layer.ToWpf(x, y);
            double side = layer.LengthToWpf(size);

            _background.Width = side;
            _background.Height = side;
            _symbol.FontSize = side * _symbolFactor;
            _badge.FontSize = side * 0.2;
            Canvas.SetLeft(_background, center.X - side / 2);
            Canvas.SetTop(_background, center.Y - side / 2);

            // L'anneau entoure le carré : rayon = demi-diagonale du carré, plus un petit écart.
            // Sa forme change à chaque tic (Dessiner) ; il est placé en (0, 0) de la surface.
            Canvas.SetLeft(_ring, 0);
            Canvas.SetTop(_ring, 0);
            _ringRadius = side / 2 * Math.Sqrt(2) + ScreenMetrics.Units(4);
            _centerWpf = center;

            // Le libellé est centré sous le bouton (ou au-dessus, si demandé).
            _label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(_label, center.X - _label.DesiredSize.Width / 2);
            if (labelAbove)
            {
                Canvas.SetTop(_label, center.Y - side / 2 - ScreenMetrics.Units(12) - _label.DesiredSize.Height);
            }
            else
            {
                Canvas.SetTop(_label, center.Y + side / 2 + ScreenMetrics.Units(12));
            }

            surface.Children.Add(_background);

            // La pastille « + » / « − » (étape 10) : un rond d'environ un tiers du côté, dont le
            // centre est un peu à l'intérieur du coin en haut à droite (il déborde du carré).
            if (_configBadge != null)
            {
                double diameter = side * 0.34;
                _configBadge.Width = diameter;
                _configBadge.Height = diameter;
                _configBadge.CornerRadius = new CornerRadius(diameter / 2);
                _configSign.FontSize = diameter * 0.8;
                double xBadge = center.X + side / 2 - diameter * 0.2;
                double yBadge = center.Y - side / 2 + diameter * 0.2;
                Canvas.SetLeft(_configBadge, xBadge - diameter / 2);
                Canvas.SetTop(_configBadge, yBadge - diameter / 2);
                surface.Children.Add(_configBadge);
            }

            surface.Children.Add(_ring);
            surface.Children.Add(_label);
        }

        private Point _centerWpf;
        private double _ringRadius;

        /// <summary>
        /// Taille de la ZONE D'ACTIVATION (la zone où le regard « touche » le bouton), par
        /// rapport au carré dessiné. 1 = le carré exactement (valeur par défaut) ; 1.3 = un
        /// carré 30 % plus grand, centré sur le bouton. Le dessin ne change pas : seule la
        /// zone sensible grandit (demande de l'utilisateur du 28/10/2026 pour le choix
        /// d'action, la loupe…), ce qui pardonne mieux les petites erreurs du regard.
        /// Attention : la zone ne doit pas déborder sur un bouton voisin.
        /// </summary>
        public double ZoneFactor { get; set; } = 1.0;

        /// <summary>
        /// Vrai si le point du regard (pixels d'écran) est dans la zone d'activation du bouton :
        /// un carré de côté [Taille] × [FacteurZone], centré sur le bouton.
        /// </summary>
        public bool Contains(double x, double y)
        {
            double halfSide = Size * ZoneFactor / 2;
            return Math.Abs(x - X) <= halfSide && Math.Abs(y - Y) <= halfSide;
        }

        /// <summary>
        /// Met à jour l'aspect du bouton.
        /// progression : de 0 (anneau vide, caché) à 1 (anneau plein) ;
        /// survol : vrai si on le regarde (fond plus clair) ;
        /// afficherLibelle : montre le texte sous le bouton.
        /// </summary>
        public void Draw(double progress, bool hover, bool showLabel)
        {
            if (!_grayed)
            {
                _background.Background = hover ? HoverBackground : NormalBackground;
            }
            else
            {
                _background.Background = hover ? EmptyHoverBackground : EmptyBackground;
            }

            if (Active && progress > 0)
            {
                _ring.Data = OverlayWindow.ArcShape(_centerWpf, _ringRadius, Math.Min(progress, 1));
                _ring.Visibility = Visibility.Visible;
            }
            else
            {
                _ring.Visibility = Visibility.Collapsed;
            }

            _label.Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
