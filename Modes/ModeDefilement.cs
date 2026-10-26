using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// MODE DÉFILEMENT (étape 8 du cahier des charges, description de l'utilisateur du 26/10/2026).
    ///
    /// On y arrive depuis le choix d'action, avec l'action « Défilement » de la couronne
    /// (emplacement 6). Le curseur est sur le point visé P ; il n'en bouge plus.
    ///
    /// Affichage, par-dessus l'écran réel :
    ///   - le cercle blanc de [RayonCercle], FIXÉ sur P (le curseur) ;
    ///   - un petit arc de cercle coloré, posé sur le cercle, du côté où se trouve le regard :
    ///     il indique la direction du défilement. Il n'apparaît que quand le regard est hors
    ///     du cercle, c'est-à-dire quand ça défile ;
    ///   - un bouton « retour » au bord droit de l'écran, centré verticalement : il ramène au
    ///     mode mouvement.
    ///
    /// Le défilement :
    ///   - ZONE MORTE = LE CERCLE : regard dans le cercle → rien ne défile (on peut lire) ;
    ///   - DIRECTION : regard au-dessus du cercle → vers le haut, en dessous → vers le bas,
    ///     à droite → vers la droite, à gauche → vers la gauche. Un seul sens à la fois :
    ///     celui où l'écart avec le centre est le plus grand (pas de diagonale) ;
    ///   - VITESSE : 0 au bord du cercle, puis elle augmente avec la distance, jusqu'à
    ///     [VitesseMaxDefilement] crans par seconde à [RayonsVitesseMax] × [RayonCercle] au-delà
    ///     du bord (trois rayons, demande de l'utilisateur du 26/10/2026).
    ///     Plus loin, elle ne monte plus ;
    ///   - on envoie des « événements de molette » Windows : c'est la fenêtre sous le curseur
    ///     qui défile (réglage Windows « Faire défiler les fenêtres inactives lorsque je pointe
    ///     dessus », activé par défaut).
    ///
    /// Le regard utilisé est lissé ([Lissage]), comme le cercle du mode mouvement : sans cela,
    /// le petit tremblement naturel de l'œil ferait hésiter la vitesse et la direction.
    ///
    /// Précautions :
    ///   - à l'entrée, le regard est encore sur l'icône de la couronne, souvent hors du
    ///     cercle : rien ne défile tant que le regard n'est pas passé une première fois dans
    ///     le cercle ;
    ///   - rien ne défile quand le regard est perdu, trop loin hors de l'écran ou sur le
    ///     bouton retour (sinon, en allant regarder le bouton, on ferait défiler vers la droite).
    ///
    /// Regard légèrement hors de l'écran : il compte quand même, jusqu'à [MargeHorsEcran]
    /// pixels au-delà des bords (demande de l'utilisateur du 26/10/2026). Cela agrandit la
    /// surface où l'on règle la vitesse : vers le haut et le bas, l'écran seul est trop court
    /// pour atteindre la vitesse maximale. Le PCEye 5 mesure le regard hors de l'écran, mais
    /// moins précisément ; au-delà de la marge, on considère la mesure comme inutilisable.
    ///
    /// Les modificateurs Ctrl, Maj, Alt choisis dans le choix d'action sont enfoncés pendant
    /// chaque mouvement de molette, puis relâchés : Ctrl + molette zoome dans la plupart des
    /// logiciels, Maj + molette fait souvent défiler de côté.
    ///
    /// Le menu ne s'ouvre pas pendant ce mode (choix du 25/10/2026) : la sortie est le bouton
    /// « retour » (ou la touche Échap de l'aidant).
    /// </summary>
    internal sealed class ModeDefilement : Mode
    {
        /// <summary>
        /// Taille d'un envoi de molette, en unités Windows (120 = un cran entier). Un quart de
        /// cran donne un défilement plus doux. Si un logiciel défile beaucoup trop vite (certains
        /// vieux logiciels comptent chaque envoi comme un cran entier), mettre 120 ici.
        /// </summary>
        private const int PasMolette = 30;

        /// <summary>Un cran de molette, en unités Windows (WHEEL_DELTA).</summary>
        private const int Cran = 120;

        /// <summary>Demi-ouverture de l'arc qui indique la direction, en degrés (arc de 50°).</summary>
        private const double DemiAngleArc = 25;

        /// <summary>
        /// Distance, comptée en rayons de cercle à partir du bord, où la vitesse atteint
        /// [VitesseMaxDefilement]. D'abord 1, triplée à la demande de l'utilisateur (26/10/2026)
        /// pour une montée en vitesse plus progressive.
        /// </summary>
        private const double RayonsVitesseMax = 3;

        /// <summary>
        /// Jusqu'où, en pixels au-delà des bords de l'écran, le regard compte encore pour le
        /// défilement (environ 5 cm sur un écran 24 pouces en 1080p). Plus loin, rien ne défile.
        /// </summary>
        private const double MargeHorsEcran = 200;

        // Le point visé P (le curseur et le centre du cercle), en pixels d'écran.
        private readonly double _px;
        private readonly double _py;

        // Le bouton « retour » (seul bouton de ce mode) et ce qui décide s'il est activé.
        private readonly List<BoutonRegard> _boutons = new List<BoutonRegard>();
        private readonly ViseurBoutons _viseur;

        // Le regard lissé, en pixels d'écran ; _place = faux tant qu'il n'a pas de position.
        private double _rx;
        private double _ry;
        private bool _place;

        // Vrai une fois que le regard est passé dans le cercle (voir les précautions).
        private bool _arme;

        // Quantité de molette pas encore envoyée (en unités Windows). La vitesse donne, à chaque
        // tic, une petite quantité (par exemple 10 crans/s × 120 × 0,016 s = 19 unités) ; on
        // l'ajoute ici, et on envoie dès qu'on a un [PasMolette] entier.
        private double _reserve;
        private bool _reserveHorizontale;
        private int _reserveSens;

        private double _dernierTicMs = -1;

        // L'arc de direction : un trait noir dessous et un trait vert dessus (lisible sur tous les fonds).
        private Path _arcFond;
        private Path _arc;

        public ModeDefilement(double x, double y)
        {
            _px = x;
            _py = y;
            _viseur = new ViseurBoutons(_boutons);
        }

        public override string Nom
        {
            get { return "Défilement"; }
        }

        public override void Entrer(EtatRegard regard)
        {
            _viseur.Reinitialiser();

            OverlayWindow calque = Gestionnaire.Calque;
            calque.MasquerTout();
            if (!calque.Pret)
            {
                Gestionnaire.ChangerMode(new ModeMouvement());
                return;
            }

            // Le curseur est remis sur P par sécurité : c'est la fenêtre sous lui qui défilera.
            NativeMethods.SetCursorPos((int)Math.Round(_px), (int)Math.Round(_py));

            _place = Utilisable(regard);
            _rx = regard.X;
            _ry = regard.Y;
            _arme = false;
            _reserve = 0;
            _dernierTicMs = regard.MaintenantMs;

            ConstruireSurface();
            calque.AfficherCercle(_px, _py, Parametres.Courants.RayonCercle, true);

            App.Log("Défilement : début en (" + _px.ToString("F0") + ", " + _py.ToString("F0") +
                    "), modificateurs : " + Modificateurs.Description());
        }

        public override void Actualiser(EtatRegard regard)
        {
            Parametres p = Parametres.Courants;

            // Temps écoulé depuis le tic précédent, en secondes (borné : après un blocage de
            // Windows, on ne veut pas envoyer d'un coup une seconde entière de défilement).
            double maintenant = regard.MaintenantMs;
            double dt = _dernierTicMs < 0 ? 0 : (maintenant - _dernierTicMs) / 1000.0;
            dt = Math.Max(0, Math.Min(dt, 0.1));
            _dernierTicMs = maintenant;

            // 1. Le bouton « retour » vient-il d'être activé ?
            int active = _viseur.Actualiser(regard, p.DelaiActivation, p.AfficherLibelles);
            if (active >= 0)
            {
                App.Log("Défilement : fin (retour)");
                Gestionnaire.ChangerMode(new ModeMouvement());
                return;
            }

            // 2. Pas de regard utilisable (perdu, ou trop loin hors de l'écran), ou regard sur
            //    le bouton : rien ne défile. Un regard un peu hors de l'écran est utilisable.
            bool surRetour = regard.SurEcran && _boutons.Count > 0 && _boutons[0].Contient(regard.X, regard.Y);
            if (!Utilisable(regard) || surRetour)
            {
                _reserve = 0;
                MasquerArc();
                return;
            }

            // 3. Le regard lissé se rapproche du regard d'une fraction [Lissage] du chemin.
            if (!_place)
            {
                _rx = regard.X;
                _ry = regard.Y;
                _place = true;
            }
            _rx += (regard.X - _rx) * p.Lissage;
            _ry += (regard.Y - _ry) * p.Lissage;

            // 4. Où est le regard par rapport au cercle ?
            double dx = _rx - _px;
            double dy = _ry - _py;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double rayon = p.RayonCercle;

            if (distance <= rayon)
            {
                // Dans le cercle (la zone morte) : rien ne défile. Le défilement est désormais
                // permis (voir les précautions).
                _arme = true;
                _reserve = 0;
                MasquerArc();
                return;
            }

            if (!_arme)
            {
                return;
            }

            // 5. La direction (un seul axe : celui du plus grand écart) et la vitesse.
            //    Vitesse : de 0 au bord du cercle à [VitesseMaxDefilement] trois rayons plus loin.
            bool horizontale = Math.Abs(dx) > Math.Abs(dy);
            // Molette : vers le haut et vers la droite = positif (voir MouseInjector.Molette).
            int sens = horizontale ? (dx > 0 ? 1 : -1) : (dy < 0 ? 1 : -1);
            double fraction = Math.Min(1.0, (distance - rayon) / (RayonsVitesseMax * rayon));
            double cransParSeconde = fraction * p.VitesseMaxDefilement;

            DessinerArc(dx, dy, rayon);

            // 6. La réserve de molette : on repart de zéro si la direction a changé.
            if (horizontale != _reserveHorizontale || sens != _reserveSens)
            {
                _reserve = 0;
                _reserveHorizontale = horizontale;
                _reserveSens = sens;
            }
            _reserve += cransParSeconde * Cran * dt;

            if (_reserve >= PasMolette)
            {
                int nombre = (int)(_reserve / PasMolette);
                _reserve -= nombre * PasMolette;
                Envoyer(sens * nombre * PasMolette, horizontale);
            }
        }

        public override void Quitter()
        {
            // La surface est cachée et vidée. Le cercle reste affiché : le mode mouvement le
            // reprend là où il est (sur P) et le fait glisser vers le regard, sans saut.
            Gestionnaire.Calque.MasquerSurfaceGlisser();
            Gestionnaire.Calque.SurfaceGlisser.Children.Clear();
            _boutons.Clear();
            _arc = null;
            _arcFond = null;
        }

        /// <summary>
        /// Vrai si la mesure du regard peut servir au défilement : disponible, et sur l'écran
        /// ou à moins de [MargeHorsEcran] pixels de ses bords.
        /// </summary>
        private bool Utilisable(EtatRegard regard)
        {
            if (!regard.Disponible)
            {
                return false;
            }
            return regard.X >= -MargeHorsEcran && regard.X < Gestionnaire.LargeurEcran + MargeHorsEcran &&
                   regard.Y >= -MargeHorsEcran && regard.Y < Gestionnaire.HauteurEcran + MargeHorsEcran;
        }

        /// <summary>Envoie la molette, avec les modificateurs actifs enfoncés autour.</summary>
        private static void Envoyer(int quantite, bool horizontale)
        {
            bool modificateurs = Modificateurs.AuMoinsUnActif;
            if (modificateurs)
            {
                Modificateurs.EnfoncerActifs();
            }

            MouseInjector.Molette(quantite, horizontale);

            if (modificateurs)
            {
                Modificateurs.RelacherTout();
            }
        }

        // ───────────────────────── Dessin ─────────────────────────

        /// <summary>
        /// Remplit la surface : l'arc de direction (caché pour l'instant) et le bouton « retour ».
        /// La surface du glisser-déposer (SurfaceGlisser) est réutilisée : c'est une surface
        /// transparente par-dessus l'écran réel, exactement ce qu'il faut ici aussi.
        /// </summary>
        private void ConstruireSurface()
        {
            OverlayWindow calque = Gestionnaire.Calque;
            calque.PreparerSurfaceGlisser();
            Canvas surface = calque.SurfaceGlisser;
            double taille = Parametres.Courants.TailleIcones;

            _arcFond = new Path();
            _arcFond.Stroke = Brushes.Black;
            _arcFond.StrokeThickness = 10;
            _arcFond.Opacity = 0.5;
            _arcFond.StrokeStartLineCap = PenLineCap.Round;
            _arcFond.StrokeEndLineCap = PenLineCap.Round;
            _arcFond.Visibility = Visibility.Collapsed;
            _arc = new Path();
            _arc.Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53));   // le vert des anneaux
            _arc.StrokeThickness = 6;
            _arc.StrokeStartLineCap = PenLineCap.Round;
            _arc.StrokeEndLineCap = PenLineCap.Round;
            _arc.Visibility = Visibility.Collapsed;
            surface.Children.Add(_arcFond);
            surface.Children.Add(_arc);

            // Le bouton « retour », au bord droit de l'écran, centré verticalement.
            // Symbole E72B = flèche « retour » de la police Windows.
            double xRetour = Gestionnaire.LargeurEcran - taille / 2 - 10;
            double yRetour = Gestionnaire.HauteurEcran / 2;
            BoutonRegard retour = new BoutonRegard("", null, "Quitter le défilement");
            retour.Placer(calque, surface, xRetour, yRetour, taille);
            _boutons.Add(retour);

            calque.AfficherSurfaceGlisser();
        }

        /// <summary>
        /// Place l'arc sur le cercle, centré sur la direction du regard (dx, dy : écart entre le
        /// regard et le centre, en pixels d'écran).
        ///
        /// Atan2 donne l'angle de cette direction. À l'écran, l'axe Y descend : un angle qui
        /// augmente tourne donc dans le sens des aiguilles d'une montre, d'où « Clockwise ».
        /// </summary>
        private void DessinerArc(double dx, double dy, double rayonPixels)
        {
            if (_arc == null)
            {
                return;
            }

            OverlayWindow calque = Gestionnaire.Calque;
            Point centre = calque.VersWpf(_px, _py);
            double rayon = calque.LongueurVersWpf(rayonPixels);

            double angle = Math.Atan2(dy, dx);
            double demi = DemiAngleArc * Math.PI / 180;
            Point depart = new Point(centre.X + rayon * Math.Cos(angle - demi), centre.Y + rayon * Math.Sin(angle - demi));
            Point arrivee = new Point(centre.X + rayon * Math.Cos(angle + demi), centre.Y + rayon * Math.Sin(angle + demi));

            PathFigure figure = new PathFigure();
            figure.StartPoint = depart;
            figure.IsClosed = false;
            figure.IsFilled = false;
            figure.Segments.Add(new ArcSegment(arrivee, new Size(rayon, rayon), 0, false, SweepDirection.Clockwise, true));
            PathGeometry forme = new PathGeometry();
            forme.Figures.Add(figure);
            forme.Freeze();

            _arcFond.Data = forme;
            _arc.Data = forme;
            _arcFond.Visibility = Visibility.Visible;
            _arc.Visibility = Visibility.Visible;
        }

        private void MasquerArc()
        {
            if (_arc != null)
            {
                _arc.Visibility = Visibility.Collapsed;
                _arcFond.Visibility = Visibility.Collapsed;
            }
        }
    }
}
