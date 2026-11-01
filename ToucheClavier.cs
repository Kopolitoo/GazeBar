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
    ///   - au survol, elle s'éclaircit légèrement : PAS d'anneau de progression ;
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

        // Couleurs (figées une fois pour toutes, WPF les dessine alors plus vite).
        private static readonly Brush FondLettre = Figer(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
        private static readonly Brush FondFonction = Figer(Color.FromArgb(0xFF, 0x26, 0x27, 0x2A));
        private static readonly Brush FondClair = Figer(Color.FromArgb(0xFF, 0x3A, 0x3B, 0x3E));
        private static readonly Brush FondSurvol = Figer(Color.FromArgb(0xFF, 0x50, 0x53, 0x58));
        private static readonly Brush FondFlash = Figer(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53));
        private static readonly Brush Blanc = Figer(Colors.White);
        private static readonly Brush Gris = Figer(Color.FromArgb(0xFF, 0x90, 0x90, 0x90));
        private static readonly Brush Vert = Figer(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53));

        private readonly Border _fond;
        private readonly TextBlock _texte;
        private readonly Brush _fondNormal;

        // Instant (Environment.TickCount, en ms) où le flash se termine ; 0 = pas de flash.
        private int _finFlash;

        // Rectangle de la ZONE D'ACTIVATION, en pixels d'écran : la case entière de la grille,
        // espace autour du dessin compris. Ainsi, il n'y a pas de « trou » entre deux touches
        // où le regard ne viserait rien.
        private double _gauche;
        private double _haut;
        private double _droite;
        private double _bas;

        /// <summary>Crée la touche décrite par le fichier de disposition.</summary>
        public ToucheClavier(ToucheDisposition description)
        {
            Description = description;

            // Une case de proposition (étape 13) ne s'active pas encore : elle est vide.
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
            _texte.TextWrapping = TextWrapping.Wrap;
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

        /// <summary>Faux pour une case de proposition (vide jusqu'à l'étape 13).</summary>
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

            surface.Children.Add(_fond);
        }

        /// <summary>
        /// Écrit le libellé en majuscules (Maj ou Caps Lock actif) ou tel qu'il est dans le
        /// fichier. Seules les touches qui écrivent du texte changent (a → A, é → É).
        /// </summary>
        public void AfficherMajuscules(bool majuscules)
        {
            string libelle = Description.Libelle ?? "";
            if (majuscules && Description.Texte != null)
            {
                libelle = Description.TexteMaj ?? libelle.ToUpper(CultureInfo.CurrentCulture);
            }
            _texte.Text = libelle;
        }

        /// <summary>Bord vert pour un modificateur actif (Maj, Caps Lock, Ctrl, Alt, Win).</summary>
        public void DefinirAllume(bool allume)
        {
            _fond.BorderThickness = new Thickness(allume ? Ecran.Unites(3) : 0);
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
            _fond.Background = survol && Actif ? FondSurvol : _fondNormal;
        }

        private static Brush Figer(Color couleur)
        {
            SolidColorBrush pinceau = new SolidColorBrush(couleur);
            pinceau.Freeze();
            return pinceau;
        }
    }
}
