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
    /// l'utilisateur qu'il pourra y mettre une action (dans les paramètres, étape 9). Il ne
    /// s'active jamais ; seul son libellé apparaît au survol.
    ///
    /// Il ne sait pas lui-même s'il est regardé ni depuis combien de temps : c'est le mode
    /// (ModeChoixAction) qui le décide et qui lui dit quoi dessiner (méthode Dessiner).
    /// Cette classe servira aussi pour la barre de menu (étape 4).
    ///
    /// Les positions sont données en PIXELS D'ÉCRAN, comme le regard ; la conversion en unités
    /// WPF est faite ici, avec l'aide du calque.
    /// </summary>
    internal sealed class BoutonRegard
    {
        // Les éléments WPF qui composent le bouton.
        private readonly Border _fond;
        private readonly TextBlock _symbole;
        private readonly TextBlock _pastille;
        private readonly Path _anneau;
        private readonly Border _libelle;

        // Couleurs (figées une fois pour toutes, WPF les dessine alors plus vite).
        private static readonly Brush FondNormal = Figer(new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24)));
        private static readonly Brush FondSurvol = Figer(new SolidColorBrush(Color.FromArgb(0xFF, 0x3C, 0x40, 0x43)));
        private static readonly Brush Blanc = Figer(new SolidColorBrush(Colors.White));
        private static readonly Brush Vert = Figer(new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53)));
        private static readonly Brush FondVide = Figer(new SolidColorBrush(Color.FromArgb(0x80, 0x50, 0x50, 0x50)));
        private static readonly Brush FondVideSurvol = Figer(new SolidColorBrush(Color.FromArgb(0xA0, 0x60, 0x60, 0x60)));
        private static readonly Brush Gris = Figer(new SolidColorBrush(Color.FromArgb(0xFF, 0x90, 0x90, 0x90)));

        /// <summary>
        /// Crée un emplacement vide (grisé, jamais activé), avec ce libellé au survol.
        /// Symbole E710 = « + » (ajouter) de la police Windows.
        /// </summary>
        public static BoutonRegard Vide(string libelle)
        {
            BoutonRegard bouton = new BoutonRegard("\uE710", null, libelle);
            bouton.Actif = false;
            bouton._fond.Background = FondVide;
            bouton._fond.BorderBrush = Gris;
            bouton._symbole.Foreground = Gris;
            bouton._symbole.Opacity = 0.6;
            return bouton;
        }

        /// <summary>
        /// Crée le bouton.
        /// symbole : le caractère de la police d'icônes (par exemple "" pour « retour ») ;
        /// pastille : un texte court, ou null pour aucun ;
        /// libelle : le texte affiché au survol.
        /// </summary>
        public BoutonRegard(string symbole, string pastille, string libelle)
        {
            Libelle = libelle;
            Actif = true;

            _symbole = new TextBlock();
            _symbole.Text = symbole;
            _symbole.Foreground = Blanc;
            _symbole.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            _symbole.HorizontalAlignment = HorizontalAlignment.Center;
            _symbole.VerticalAlignment = VerticalAlignment.Center;

            _pastille = new TextBlock();
            _pastille.Text = pastille ?? "";
            _pastille.Foreground = Blanc;
            _pastille.FontWeight = FontWeights.Bold;
            _pastille.HorizontalAlignment = HorizontalAlignment.Right;
            _pastille.VerticalAlignment = VerticalAlignment.Bottom;
            _pastille.Margin = new Thickness(0, 0, 8, 4);
            _pastille.Visibility = pastille == null ? Visibility.Collapsed : Visibility.Visible;

            // Une grille permet de superposer le symbole (au centre) et la pastille (dans le coin).
            Grid contenu = new Grid();
            contenu.Children.Add(_symbole);
            contenu.Children.Add(_pastille);

            _fond = new Border();
            _fond.Background = FondNormal;
            _fond.BorderBrush = Blanc;
            _fond.BorderThickness = new Thickness(2);
            _fond.CornerRadius = new CornerRadius(12);
            _fond.Child = contenu;

            _anneau = new Path();
            _anneau.Stroke = Vert;
            _anneau.StrokeThickness = 6;
            _anneau.StrokeStartLineCap = PenLineCap.Round;
            _anneau.StrokeEndLineCap = PenLineCap.Round;
            _anneau.Visibility = Visibility.Collapsed;

            TextBlock texte = new TextBlock();
            texte.Text = libelle;
            texte.Foreground = Blanc;
            texte.FontSize = 20;
            _libelle = new Border();
            _libelle.Background = FondNormal;
            _libelle.CornerRadius = new CornerRadius(6);
            _libelle.Padding = new Thickness(8, 2, 8, 2);
            _libelle.Child = texte;
            _libelle.Visibility = Visibility.Collapsed;
        }

        /// <summary>Faux pour un emplacement vide : il ne s'active jamais (pas d'anneau).</summary>
        public bool Actif { get; private set; }

        /// <summary>Le texte affiché au survol (et écrit dans le journal à l'activation).</summary>
        public string Libelle { get; private set; }

        /// <summary>Centre du bouton, en pixels d'écran.</summary>
        public double X { get; private set; }

        public double Y { get; private set; }

        /// <summary>Côté du bouton, en pixels d'écran.</summary>
        public double Taille { get; private set; }

        /// <summary>
        /// Ajoute le bouton sur une surface du calque (par exemple l'écran noir) et le place,
        /// centré sur (x, y) en pixels d'écran, avec un côté de [taille] pixels.
        /// </summary>
        public void Placer(OverlayWindow calque, Canvas surface, double x, double y, double taille)
        {
            X = x;
            Y = y;
            Taille = taille;

            Point centre = calque.VersWpf(x, y);
            double cote = calque.LongueurVersWpf(taille);

            _fond.Width = cote;
            _fond.Height = cote;
            _symbole.FontSize = cote * 0.45;
            _pastille.FontSize = cote * 0.2;
            Canvas.SetLeft(_fond, centre.X - cote / 2);
            Canvas.SetTop(_fond, centre.Y - cote / 2);

            // L'anneau entoure le carré : rayon = demi-diagonale du carré, plus un petit écart.
            // Sa forme change à chaque tic (Dessiner) ; il est placé en (0, 0) de la surface.
            Canvas.SetLeft(_anneau, 0);
            Canvas.SetTop(_anneau, 0);
            _rayonAnneau = cote / 2 * Math.Sqrt(2) + 4;
            _centreWpf = centre;

            // Le libellé est centré sous le bouton.
            _libelle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(_libelle, centre.X - _libelle.DesiredSize.Width / 2);
            Canvas.SetTop(_libelle, centre.Y + cote / 2 + 12);

            surface.Children.Add(_fond);
            surface.Children.Add(_anneau);
            surface.Children.Add(_libelle);
        }

        private Point _centreWpf;
        private double _rayonAnneau;

        /// <summary>
        /// Vrai si le point du regard (pixels d'écran) est sur le bouton.
        /// La zone sensible est le carré du bouton, sans marge : les boutons sont assez grands
        /// ([TailleIcones] = 120 px) pour qu'on n'en ait pas besoin.
        /// </summary>
        public bool Contient(double x, double y)
        {
            return Math.Abs(x - X) <= Taille / 2 && Math.Abs(y - Y) <= Taille / 2;
        }

        /// <summary>
        /// Met à jour l'aspect du bouton.
        /// progression : de 0 (anneau vide, caché) à 1 (anneau plein) ;
        /// survol : vrai si on le regarde (fond plus clair) ;
        /// afficherLibelle : montre le texte sous le bouton.
        /// </summary>
        public void Dessiner(double progression, bool survol, bool afficherLibelle)
        {
            if (Actif)
            {
                _fond.Background = survol ? FondSurvol : FondNormal;
            }
            else
            {
                _fond.Background = survol ? FondVideSurvol : FondVide;
            }

            if (Actif && progression > 0)
            {
                _anneau.Data = OverlayWindow.FormeArc(_centreWpf, _rayonAnneau, Math.Min(progression, 1));
                _anneau.Visibility = Visibility.Visible;
            }
            else
            {
                _anneau.Visibility = Visibility.Collapsed;
            }

            _libelle.Visibility = afficherLibelle ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Brush Figer(SolidColorBrush pinceau)
        {
            pinceau.Freeze();
            return pinceau;
        }
    }
}
