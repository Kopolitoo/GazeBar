using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// INTERFACE : une touche du clavier au regard, dessinée sur le calque (étape 12).
    ///
    /// C'est un rectangle aux coins légèrement arrondis, avec son libellé au centre. Comme
    /// sur les captures de l'utilisateur :
    ///   - les touches qui écrivent une lettre n'ont pas de fond (style « lettre ») ;
    ///   - les autres ont un fond sombre (style « fonction ») ;
    ///   - les chiffres, F1 à F12 et les flèches ont un fond plus clair (style « clair »).
    ///
    /// Ce qu'on voit quand on la regarde (choix de l'utilisateur du 31/10/2026) :
    ///   - au survol, elle s'éclaircit légèrement : PAS d'anneau de progression. Depuis le
    ///     02/11/2026 (demande de l'utilisateur), le fond de survol est un peu moins clair, et
    ///     un fin contour bleu clair apparaît en fondu ([DureeContourMs]) ;
    ///   - quand elle s'active, elle « flashe » en vert un court instant (Flasher).
    /// Un modificateur actif (Maj, Caps Lock, Ctrl, Alt, Win) a un bord vert, comme les
    /// interrupteurs allumés de la barre de menu.
    ///
    /// Comme BoutonRegard, elle « implémente » ICibleRegard : c'est ViseurBoutons qui décide
    /// si elle est regardée et quand elle s'active, et qui lui dit quoi dessiner.
    /// Les positions sont en PIXELS D'ÉCRAN (comme le regard), converties par le calque.
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal sealed class KeyboardKey : IGazeTarget
    {
        /// <summary>Durée du flash vert à l'activation, en millisecondes.</summary>
        private const int FlashDurationMs = 150;

        /// <summary>Durée du fondu du contour de survol, en millisecondes.</summary>
        private const int OutlineDurationMs = 200;

        /// <summary>Opacité finale du contour de survol (« légèrement coloré »).</summary>
        private const double OutlineOpacity = 0.8;

        // Couleurs (figées une fois pour toutes, WPF les dessine alors plus vite).
        private static readonly Brush LetterBackground = Freeze(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
        private static readonly Brush FunctionBackground = Freeze(Color.FromArgb(0xFF, 0x26, 0x27, 0x2A));
        private static readonly Brush LightBackground = Freeze(Color.FromArgb(0xFF, 0x3A, 0x3B, 0x3E));
        // Fond de survol : #505358 au début, un peu moins clair depuis le 02/11/2026.
        private static readonly Brush HoverBackground = Freeze(Color.FromArgb(0xFF, 0x46, 0x48, 0x4D));
        private static readonly Brush FlashBackground = Freeze(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53));
        private static readonly Brush White = Freeze(Colors.White);
        private static readonly Brush Gray = Freeze(Color.FromArgb(0xFF, 0x90, 0x90, 0x90));
        private static readonly Brush Green = Freeze(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53));
        // Étape 13 : couleur du texte de la case « + monmot » (mot inconnu à ajouter), pour la
        // distinguer d'une proposition ordinaire (ambre).
        private static readonly Brush Amber = Freeze(Color.FromArgb(0xFF, 0xFF, 0xC8, 0x57));

        private readonly Border _background;
        private readonly TextBlock _text;
        private readonly Brush _normalBackground;

        // Le contour de survol, bleu clair. Pas figé : son opacité change pendant le fondu.
        private readonly SolidColorBrush _outline = new SolidColorBrush(Color.FromRgb(0x7F, 0xB8, 0xF0));

        // Vrai pour un modificateur actif : son bord vert passe avant le contour de survol.
        private bool _lit;

        // Instant (Environment.TickCount) du début du survol ; null = pas survolée.
        private int? _hoverStart;

        // Instant (Environment.TickCount, en ms) où le flash se termine ; 0 = pas de flash.
        private int _flashEnd;

        // Rectangle de la ZONE D'ACTIVATION, en pixels d'écran : la case entière de la grille,
        // espace autour du dessin compris. Ainsi, il n'y a pas de « trou » entre deux touches
        // où le regard ne viserait rien.
        private double _left;
        private double _top;
        private double _right;
        private double _bottom;

        // Taille du dessin de la touche, en unités WPF (retenue par Placer) : sert à choisir la
        // taille du texte d'une proposition, qui change à chaque lettre tapée (étape 13).
        private double _widthWpf;
        private double _cellHeightWpf;

        /// <summary>Crée la touche décrite par le fichier de disposition.</summary>
        public KeyboardKey(LayoutKey description)
        {
            Description = description;

            // Une case de proposition (étape 13) est vide, donc inactive, tant qu'elle ne
            // montre pas de mot (voir AfficherProposition).
            Active = !description.Prediction.HasValue;

            string style = (description.Style ?? "").Trim().ToLowerInvariant();
            if (style == "")
            {
                style = description.Text != null ? "lettre" : "fonction";
            }
            if (description.Prediction.HasValue)
            {
                style = "fonction";
            }
            _normalBackground = style == "clair" ? LightBackground : style == "lettre" ? LetterBackground : FunctionBackground;

            _text = new TextBlock();
            _text.Foreground = Active ? White : Gray;
            // « Segoe UI Symbol » en second : WPF y cherche les symboles absents de Segoe UI
            // (⌫, ⇧, ␣, ↺…).
            _text.FontFamily = new FontFamily("Segoe UI, Segoe UI Symbol");
            _text.TextAlignment = TextAlignment.Center;
            // Une proposition reste sur une ligne (un long mot est écrit plus petit, voir
            // AfficherProposition) ; les autres libellés peuvent tenir sur deux lignes.
            _text.TextWrapping = description.Prediction.HasValue ? TextWrapping.NoWrap : TextWrapping.Wrap;
            _text.HorizontalAlignment = HorizontalAlignment.Center;
            _text.VerticalAlignment = VerticalAlignment.Center;

            _background = new Border();
            _background.Background = _normalBackground;
            _background.BorderBrush = Green;
            _background.BorderThickness = new Thickness(0);
            _background.CornerRadius = new CornerRadius(ScreenMetrics.Units(4));
            _background.Child = _text;

            ShowUppercase(false);
        }

        /// <summary>Ce que dit le fichier de disposition pour cette touche.</summary>
        public LayoutKey Description { get; private set; }

        /// <summary>Faux pour une case de proposition vide (aucun mot à proposer).</summary>
        public bool Active { get; private set; }

        /// <summary>
        /// Place la touche sur la surface du clavier.
        /// gauche, haut, largeur, hauteur : la CASE de la grille, en pixels d'écran ;
        /// ecart : l'espace laissé autour du dessin, en pixels d'écran.
        /// </summary>
        public void Place(OverlayWindow layer, Canvas surface, double left, double top,
                           double width, double height, double gap)
        {
            _left = left;
            _top = top;
            _right = left + width;
            _bottom = top + height;

            Point corner = layer.ToWpf(left + gap / 2, top + gap / 2);
            double l = layer.LengthToWpf(Math.Max(1, width - gap));
            double h = layer.LengthToWpf(Math.Max(1, height - gap));
            _background.Width = l;
            _background.Height = h;
            Canvas.SetLeft(_background, corner.X);
            Canvas.SetTop(_background, corner.Y);

            // Taille du texte : grande pour une seule lettre, plus petite pour un mot
            // (« Caps Lock », « Enter »…), qui peut alors tenir sur deux lignes.
            string label = _text.Text ?? "";
            double cellHeight = layer.LengthToWpf(height);
            _text.FontSize = Math.Max(1, cellHeight * (label.Length <= 2 ? 0.30 : 0.20));
            _text.Margin = new Thickness(ScreenMetrics.Units(4));
            _widthWpf = l;
            _cellHeightWpf = cellHeight;
            if (Description.Prediction.HasValue)
            {
                FitFontSize();
            }

            surface.Children.Add(_background);
        }

        /// <summary>
        /// Écrit le libellé en majuscules (Maj ou Caps Lock actif) ou tel qu'il est dans le
        /// fichier. Seules les touches qui écrivent du texte changent (a → A, é → É).
        /// </summary>
        public void ShowUppercase(bool uppercaseLetters)
        {
            if (Description.Prediction.HasValue)
            {
                return;   // une case de proposition a son propre texte (AfficherProposition)
            }
            string label = Description.Label ?? "";
            if (uppercaseLetters && Description.Text != null)
            {
                label = Description.ShiftText ?? label.ToUpper(CultureInfo.CurrentCulture);
            }
            _text.Text = label;
        }

        /// <summary>
        /// Étape 13 : le mot montré dans une case de proposition. null = case vide (elle ne
        /// s'active pas). nouveau = vrai pour la case « + monmot » (mot inconnu à ajouter au
        /// dictionnaire personnel), écrite en ambre.
        /// </summary>
        public void ShowSuggestion(string text, bool newVal)
        {
            if (!Description.Prediction.HasValue)
            {
                return;
            }
            Active = !string.IsNullOrEmpty(text);
            _text.Text = text ?? "";
            _text.Foreground = newVal ? Amber : White;
            FitFontSize();
        }

        /// <summary>
        /// Taille du texte d'une proposition : comme un mot sur une touche (20 % de la hauteur
        /// de la case… un peu plus : 24 %), mais plus petite si le mot est trop long pour la
        /// largeur. Une lettre de Segoe UI fait en moyenne un peu plus de la moitié de la taille
        /// du texte en largeur : on compte 0,55.
        /// </summary>
        private void FitFontSize()
        {
            if (_cellHeightWpf <= 0)
            {
                return;   // pas encore placée : Placer s'en chargera
            }
            int number = Math.Max(1, (_text.Text ?? "").Length);
            double usableWidth = Math.Max(1, _widthWpf - 2 * ScreenMetrics.Units(8));
            double size = Math.Min(_cellHeightWpf * 0.24, usableWidth / (0.55 * number));
            _text.FontSize = Math.Max(1, size);
        }

        /// <summary>Bord vert pour un modificateur actif (Maj, Caps Lock, Ctrl, Alt, Win).</summary>
        public void SetLit(bool lit)
        {
            _lit = lit;
            _hoverStart = null;
            ApplyBorder(false);
        }

        /// <summary>
        /// Le bord de la touche : vert (épais) pour un modificateur actif ; sinon, au survol,
        /// le contour bleu clair dont l'opacité monte de 0 à [OpaciteContour] en [DureeContourMs].
        /// </summary>
        private void ApplyBorder(bool hover)
        {
            if (_lit)
            {
                _background.BorderBrush = Green;
                _background.BorderThickness = new Thickness(ScreenMetrics.Units(3));
                return;
            }
            if (!hover)
            {
                _hoverStart = null;
                _background.BorderThickness = new Thickness(0);
                return;
            }
            if (!_hoverStart.HasValue)
            {
                _hoverStart = Environment.TickCount;
            }
            double elapsedMs = Environment.TickCount - _hoverStart.Value;
            _outline.Opacity = OutlineOpacity * Math.Max(0, Math.Min(1, elapsedMs / OutlineDurationMs));
            _background.BorderBrush = _outline;
            _background.BorderThickness = new Thickness(ScreenMetrics.Units(2));
        }

        /// <summary>Lance le flash vert de l'activation.</summary>
        public void Flash()
        {
            _flashEnd = Environment.TickCount + FlashDurationMs;
            _background.Background = FlashBackground;
        }

        /// <summary>Vrai si le point du regard (pixels d'écran) est dans la case de la touche.</summary>
        public bool Contains(double x, double y)
        {
            return x >= _left && x < _right && y >= _top && y < _bottom;
        }

        /// <summary>
        /// Met à jour l'aspect de la touche. La progression est ignorée : pas d'anneau sur le
        /// clavier (choix de l'utilisateur). Le libellé au survol non plus : il est déjà écrit
        /// sur la touche.
        /// </summary>
        public void Draw(double progress, bool hover, bool showLabel)
        {
            // « Environment.TickCount - _finFlash < 0 » : encore dans le flash. Cette façon de
            // comparer reste juste même quand le compteur de Windows repasse par zéro.
            if (_flashEnd != 0 && Environment.TickCount - _flashEnd < 0)
            {
                _background.Background = FlashBackground;
                return;
            }
            _flashEnd = 0;
            bool highlight = hover && Active;
            _background.Background = highlight ? HoverBackground : _normalBackground;
            ApplyBorder(highlight);
        }

        private static Brush Freeze(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
