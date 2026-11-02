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
    internal sealed class ToucheClavier : ICibleRegard
    {
        /// <summary>Durée du flash vert à l'activation, en millisecondes.</summary>
        private const int DureeFlashMs = 150;

        /// <summary>Durée du fondu du contour de survol, en millisecondes.</summary>
        private const int DureeContourMs = 200;

        /// <summary>Opacité finale du contour de survol (« légèrement coloré »).</summary>
        private const double OpaciteContour = 0.8;

        // Couleurs (figées une fois pour toutes, WPF les dessine alors plus vite).
        private static readonly Brush FondLettre = Figer(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
        private static readonly Brush FondFonction = Figer(Color.FromArgb(0xFF, 0x26, 0x27, 0x2A));
        private static readonly Brush FondClair = Figer(Color.FromArgb(0xFF, 0x3A, 0x3B, 0x3E));
        // Fond de survol : #505358 au début, un peu moins clair depuis le 02/11/2026.
        private static readonly Brush FondSurvol = Figer(Color.FromArgb(0xFF, 0x46, 0x48, 0x4D));
        private static readonly Brush FondFlash = Figer(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53));
        private static readonly Brush Blanc = Figer(Colors.White);
        private static readonly Brush Gris = Figer(Color.FromArgb(0xFF, 0x90, 0x90, 0x90));
        private static readonly Brush Vert = Figer(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53));
        // Étape 13 : couleur du texte de la case « + monmot » (mot inconnu à ajouter), pour la
        // distinguer d'une proposition ordinaire (ambre).
        private static readonly Brush Ambre = Figer(Color.FromArgb(0xFF, 0xFF, 0xC8, 0x57));

        private readonly Border _fond;
        private readonly TextBlock _texte;
        private readonly Brush _fondNormal;

        // Le contour de survol, bleu clair. Pas figé : son opacité change pendant le fondu.
        private readonly SolidColorBrush _contour = new SolidColorBrush(Color.FromRgb(0x7F, 0xB8, 0xF0));

        // Vrai pour un modificateur actif : son bord vert passe avant le contour de survol.
        private bool _allume;

        // Instant (Environment.TickCount) du début du survol ; null = pas survolée.
        private int? _debutSurvol;

        // Instant (Environment.TickCount, en ms) où le flash se termine ; 0 = pas de flash.
        private int _finFlash;

        // Rectangle de la ZONE D'ACTIVATION, en pixels d'écran : la case entière de la grille,
        // espace autour du dessin compris. Ainsi, il n'y a pas de « trou » entre deux touches
        // où le regard ne viserait rien.
        private double _gauche;
        private double _haut;
        private double _droite;
        private double _bas;

        // Taille du dessin de la touche, en unités WPF (retenue par Placer) : sert à choisir la
        // taille du texte d'une proposition, qui change à chaque lettre tapée (étape 13).
        private double _largeurWpf;
        private double _hauteurCaseWpf;

        /// <summary>Crée la touche décrite par le fichier de disposition.</summary>
        public ToucheClavier(ToucheDisposition description)
        {
            Description = description;

            // Une case de proposition (étape 13) est vide, donc inactive, tant qu'elle ne
            // montre pas de mot (voir AfficherProposition).
            Actif = !description.Prediction.HasValue;

            string style = (description.Style ?? "").Trim().ToLowerInvariant();
            if (style == "")
            {
                style = description.Texte != null ? "lettre" : "fonction";
            }
            if (description.Prediction.HasValue)
            {
                style = "fonction";
            }
            _fondNormal = style == "clair" ? FondClair : style == "lettre" ? FondLettre : FondFonction;

            _texte = new TextBlock();
            _texte.Foreground = Actif ? Blanc : Gris;
            // « Segoe UI Symbol » en second : WPF y cherche les symboles absents de Segoe UI
            // (⌫, ⇧, ␣, ↺…).
            _texte.FontFamily = new FontFamily("Segoe UI, Segoe UI Symbol");
            _texte.TextAlignment = TextAlignment.Center;
            // Une proposition reste sur une ligne (un long mot est écrit plus petit, voir
            // AfficherProposition) ; les autres libellés peuvent tenir sur deux lignes.
            _texte.TextWrapping = description.Prediction.HasValue ? TextWrapping.NoWrap : TextWrapping.Wrap;
            _texte.HorizontalAlignment = HorizontalAlignment.Center;
            _texte.VerticalAlignment = VerticalAlignment.Center;

            _fond = new Border();
            _fond.Background = _fondNormal;
            _fond.BorderBrush = Vert;
            _fond.BorderThickness = new Thickness(0);
            _fond.CornerRadius = new CornerRadius(Ecran.Unites(4));
            _fond.Child = _texte;

            AfficherMajuscules(false);
        }

        /// <summary>Ce que dit le fichier de disposition pour cette touche.</summary>
        public ToucheDisposition Description { get; private set; }

        /// <summary>Faux pour une case de proposition vide (aucun mot à proposer).</summary>
        public bool Actif { get; private set; }

        /// <summary>
        /// Place la touche sur la surface du clavier.
        /// gauche, haut, largeur, hauteur : la CASE de la grille, en pixels d'écran ;
        /// ecart : l'espace laissé autour du dessin, en pixels d'écran.
        /// </summary>
        public void Placer(OverlayWindow calque, Canvas surface, double gauche, double haut,
                           double largeur, double hauteur, double ecart)
        {
            _gauche = gauche;
            _haut = haut;
            _droite = gauche + largeur;
            _bas = haut + hauteur;

            Point coin = calque.VersWpf(gauche + ecart / 2, haut + ecart / 2);
            double l = calque.LongueurVersWpf(Math.Max(1, largeur - ecart));
            double h = calque.LongueurVersWpf(Math.Max(1, hauteur - ecart));
            _fond.Width = l;
            _fond.Height = h;
            Canvas.SetLeft(_fond, coin.X);
            Canvas.SetTop(_fond, coin.Y);

            // Taille du texte : grande pour une seule lettre, plus petite pour un mot
            // (« Caps Lock », « Enter »…), qui peut alors tenir sur deux lignes.
            string libelle = _texte.Text ?? "";
            double hauteurCase = calque.LongueurVersWpf(hauteur);
            _texte.FontSize = Math.Max(1, hauteurCase * (libelle.Length <= 2 ? 0.30 : 0.20));
            _texte.Margin = new Thickness(Ecran.Unites(4));
            _largeurWpf = l;
            _hauteurCaseWpf = hauteurCase;
            if (Description.Prediction.HasValue)
            {
                AjusterTaillePolice();
            }

            surface.Children.Add(_fond);
        }

        /// <summary>
        /// Écrit le libellé en majuscules (Maj ou Caps Lock actif) ou tel qu'il est dans le
        /// fichier. Seules les touches qui écrivent du texte changent (a → A, é → É).
        /// </summary>
        public void AfficherMajuscules(bool majuscules)
        {
            if (Description.Prediction.HasValue)
            {
                return;   // une case de proposition a son propre texte (AfficherProposition)
            }
            string libelle = Description.Libelle ?? "";
            if (majuscules && Description.Texte != null)
            {
                libelle = Description.TexteMaj ?? libelle.ToUpper(CultureInfo.CurrentCulture);
            }
            _texte.Text = libelle;
        }

        /// <summary>
        /// Étape 13 : le mot montré dans une case de proposition. null = case vide (elle ne
        /// s'active pas). nouveau = vrai pour la case « + monmot » (mot inconnu à ajouter au
        /// dictionnaire personnel), écrite en ambre.
        /// </summary>
        public void AfficherProposition(string texte, bool nouveau)
        {
            if (!Description.Prediction.HasValue)
            {
                return;
            }
            Actif = !string.IsNullOrEmpty(texte);
            _texte.Text = texte ?? "";
            _texte.Foreground = nouveau ? Ambre : Blanc;
            AjusterTaillePolice();
        }

        /// <summary>
        /// Taille du texte d'une proposition : comme un mot sur une touche (20 % de la hauteur
        /// de la case… un peu plus : 24 %), mais plus petite si le mot est trop long pour la
        /// largeur. Une lettre de Segoe UI fait en moyenne un peu plus de la moitié de la taille
        /// du texte en largeur : on compte 0,55.
        /// </summary>
        private void AjusterTaillePolice()
        {
            if (_hauteurCaseWpf <= 0)
            {
                return;   // pas encore placée : Placer s'en chargera
            }
            int nombre = Math.Max(1, (_texte.Text ?? "").Length);
            double largeurUtile = Math.Max(1, _largeurWpf - 2 * Ecran.Unites(8));
            double taille = Math.Min(_hauteurCaseWpf * 0.24, largeurUtile / (0.55 * nombre));
            _texte.FontSize = Math.Max(1, taille);
        }

        /// <summary>Bord vert pour un modificateur actif (Maj, Caps Lock, Ctrl, Alt, Win).</summary>
        public void DefinirAllume(bool allume)
        {
            _allume = allume;
            _debutSurvol = null;
            AppliquerBord(false);
        }

        /// <summary>
        /// Le bord de la touche : vert (épais) pour un modificateur actif ; sinon, au survol,
        /// le contour bleu clair dont l'opacité monte de 0 à [OpaciteContour] en [DureeContourMs].
        /// </summary>
        private void AppliquerBord(bool survol)
        {
            if (_allume)
            {
                _fond.BorderBrush = Vert;
                _fond.BorderThickness = new Thickness(Ecran.Unites(3));
                return;
            }
            if (!survol)
            {
                _debutSurvol = null;
                _fond.BorderThickness = new Thickness(0);
                return;
            }
            if (!_debutSurvol.HasValue)
            {
                _debutSurvol = Environment.TickCount;
            }
            double ecoule = Environment.TickCount - _debutSurvol.Value;
            _contour.Opacity = OpaciteContour * Math.Max(0, Math.Min(1, ecoule / DureeContourMs));
            _fond.BorderBrush = _contour;
            _fond.BorderThickness = new Thickness(Ecran.Unites(2));
        }

        /// <summary>Lance le flash vert de l'activation.</summary>
        public void Flasher()
        {
            _finFlash = Environment.TickCount + DureeFlashMs;
            _fond.Background = FondFlash;
        }

        /// <summary>Vrai si le point du regard (pixels d'écran) est dans la case de la touche.</summary>
        public bool Contient(double x, double y)
        {
            return x >= _gauche && x < _droite && y >= _haut && y < _bas;
        }

        /// <summary>
        /// Met à jour l'aspect de la touche. La progression est ignorée : pas d'anneau sur le
        /// clavier (choix de l'utilisateur). Le libellé au survol non plus : il est déjà écrit
        /// sur la touche.
        /// </summary>
        public void Dessiner(double progression, bool survol, bool afficherLibelle)
        {
            // « Environment.TickCount - _finFlash < 0 » : encore dans le flash. Cette façon de
            // comparer reste juste même quand le compteur de Windows repasse par zéro.
            if (_finFlash != 0 && Environment.TickCount - _finFlash < 0)
            {
                _fond.Background = FondFlash;
                return;
            }
            _finFlash = 0;
            bool surligner = survol && Actif;
            _fond.Background = surligner ? FondSurvol : _fondNormal;
            AppliquerBord(surligner);
        }

        private static Brush Figer(Color couleur)
        {
            SolidColorBrush pinceau = new SolidColorBrush(couleur);
            pinceau.Freeze();
            return pinceau;
        }
    }
}
