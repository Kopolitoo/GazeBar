using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// MODE GLISSER-DÉPOSER, 1er temps : choisir le point d'arrivée (étape 6).
    ///
    /// On y arrive depuis le choix d'action, avec l'une des trois actions « glisser-déposer »
    /// de la couronne. Le point de départ est le point visé du choix d'action (la croix).
    ///
    /// Affichage, par-dessus l'écran réel (l'écran noir du choix d'action a disparu) :
    ///   - le cercle de [RayonCercle] suit le regard, lissé, comme en mode mouvement ;
    ///   - une ligne blanche en pointillés relie le point de départ au cercle : c'est le futur
    ///     trajet de la souris ;
    ///   - un petit point blanc marque le point de départ ;
    ///   - un bouton « retour », au bord droit de l'écran et centré verticalement (choix de
    ///     l'utilisateur du 25/10/2026), annule le glisser-déposer sans rien enfoncer et ramène
    ///     au mode mouvement.
    ///
    /// Quand le regard se fixe (même détection qu'en mode mouvement, [DelaiFixation]), on
    /// passe au 2e temps : la loupe s'ouvre autour du point fixé pour affiner l'arrivée
    /// (ModeLoupe, avec ce glisser-déposer en cours). Le curseur, lui, n'a pas bougé : il est
    /// toujours sur le point de départ.
    ///
    /// Deux précautions, comme dans la loupe :
    ///  - à l'entrée, le regard est encore là où se trouvait l'icône qu'on vient d'activer
    ///    dans la couronne. Une fixation n'est reconnue qu'une fois le regard sorti de cet
    ///    endroit (rayon [RayonCercle]) ;
    ///  - une fixation sur le bouton « retour » ne compte pas comme point d'arrivée.
    ///
    /// Le menu ne s'ouvre pas pendant ce mode (choix du 25/10/2026) : la sortie est le bouton
    /// « retour » (ou la touche Échap de l'aidant).
    /// </summary>
    internal sealed class ModeGlisserArrivee : Mode
    {
        /// <summary>Rayon du point qui marque le départ, en pixels de l'écran 4K (converti avec Ecran.Px, étape 11).</summary>
        private const double RayonPointDepart = 6;

        private readonly Glissement _glissement;

        private readonly DetecteurFixation _fixation = new DetecteurFixation();

        // Le bouton « retour » (seul bouton de ce mode) et ce qui décide s'il est activé.
        private readonly List<BoutonRegard> _boutons = new List<BoutonRegard>();
        private readonly ViseurBoutons _viseur;

        // Position (décimale) du cercle lissé, en pixels d'écran ; _place = faux tant qu'il
        // n'a pas de position.
        private readonly Lisseur _lisse = new Lisseur();
        private bool _place;

        // Zone ignorée : là où était le regard à l'entrée (voir le commentaire de la classe).
        private bool _zoneIgnoree;
        private bool _zoneIgnoreeConnue;
        private double _zoneX;
        private double _zoneY;

        // La ligne du trajet : un trait noir continu dessous et un trait blanc en pointillés
        // dessus, pour qu'elle se voie sur tous les fonds.
        private Line _ligneFond;
        private Line _ligne;

        public ModeGlisserArrivee(Glissement glissement)
        {
            _glissement = glissement;
            _viseur = new ViseurBoutons(_boutons);
        }

        public override string Nom
        {
            get { return "Glisser-déposer (arrivée)"; }
        }

        public override void Entrer(EtatRegard regard)
        {
            _viseur.Reinitialiser();
            _fixation.Reinitialiser();

            OverlayWindow calque = Gestionnaire.Calque;
            calque.MasquerTout();
            if (!calque.Pret)
            {
                Gestionnaire.ChangerMode(new ModeMouvement());
                return;
            }

            // La zone ignorée est centrée sur le regard actuel (s'il est sur l'écran),
            // sinon sur la première mesure sur l'écran qui arrivera.
            _zoneIgnoree = true;
            _zoneIgnoreeConnue = regard.SurEcran;
            _zoneX = regard.X;
            _zoneY = regard.Y;

            // Le cercle part du regard (le choix d'action l'avait caché : pas de saut visible),
            // ramené dans l'écran s'il en sort.
            _place = regard.Disponible;
            _lisse.Placer(regard.XEcran, regard.YEcran);

            ConstruireSurface();
            App.Log("Glisser : choix de l'arrivée (" + _glissement.Nom + ", départ en (" +
                    _glissement.DepartX.ToString("F0") + ", " + _glissement.DepartY.ToString("F0") +
                    "), modificateurs : " + Modificateurs.Description() + ")");
        }

        public override void Actualiser(EtatRegard regard)
        {
            Parametres p = Parametres.Courants;

            // 1. Le bouton « retour » vient-il d'être activé ?
            int active = _viseur.Actualiser(regard, p.DelaiActivation, p.AfficherLibelles);
            if (active >= 0)
            {
                App.Log("Glisser : annulé (retour)");
                Gestionnaire.ChangerMode(new ModeMouvement());
                return;
            }

            if (regard.Disponible)
            {
                // 2. Le cercle se rapproche du regard, en douceur (voir Lisseur.cs). Regard hors
                //    de l'écran : il reste au bord et glisse le long de ce bord (EtatRegard.XEcran).
                if (!_place)
                {
                    _lisse.Placer(regard.XEcran, regard.YEcran);
                    _place = true;
                }
                _lisse.Suivre(regard.XEcran, regard.YEcran, regard.MaintenantMs);
            }

            if (regard.SurEcran)
            {
                // 3. La zone ignorée : le regard en est-il sorti ? (Vrai regard sur l'écran seulement.)
                if (!_zoneIgnoreeConnue)
                {
                    _zoneX = regard.X;
                    _zoneY = regard.Y;
                    _zoneIgnoreeConnue = true;
                }
                else if (_zoneIgnoree && Distance(regard.X, regard.Y, _zoneX, _zoneY) > p.RayonCerclePx)
                {
                    _zoneIgnoree = false;
                }
            }

            // 4. La fixation, hors de la zone ignorée et hors du bouton « retour ».
            //    Pas d'anneau de progression, comme en mode mouvement.
            _fixation.Ajouter(regard);
            bool surRetour = regard.SurEcran && _boutons.Count > 0 && _boutons[0].Contient(regard.X, regard.Y);
            if (_zoneIgnoree || surRetour)
            {
                _fixation.Reinitialiser();
            }

            if (_fixation.Atteinte)
            {
                App.Log("Glisser : arrivée fixée en (" + _fixation.X.ToString("F0") + ", " +
                        _fixation.Y.ToString("F0") + "), ouverture de la loupe");
                Gestionnaire.ChangerMode(new ModeLoupe(_fixation.X, _fixation.Y, _glissement));
                return;
            }

            // 5. Le dessin : le cercle et la ligne du trajet.
            if (_place)
            {
                Gestionnaire.Calque.AfficherCercle(_lisse.X, _lisse.Y, p.RayonCerclePx, false);
                PlacerLigne(_lisse.X, _lisse.Y);
            }
        }

        public override void Quitter()
        {
            // La surface est cachée et vidée. Le cercle reste affiché : en cas d'annulation, le
            // mode mouvement le reprend là où il est (la loupe, elle, cache tout).
            Gestionnaire.Calque.MasquerSurfaceGlisser();
            Gestionnaire.Calque.SurfaceGlisser.Children.Clear();
            _boutons.Clear();
            _ligne = null;
            _ligneFond = null;
        }

        // ───────────────────────── Dessin ─────────────────────────

        /// <summary>Remplit la surface du glisser : ligne du trajet, point de départ, bouton « retour ».</summary>
        private void ConstruireSurface()
        {
            OverlayWindow calque = Gestionnaire.Calque;
            calque.PreparerSurfaceGlisser();
            Canvas surface = calque.SurfaceGlisser;
            double taille = Parametres.Courants.TailleIconesPx;

            // La ligne : elle partira du point de départ ; son autre bout suit le cercle.
            _ligneFond = new Line();
            _ligneFond.Stroke = Brushes.Black;
            _ligneFond.StrokeThickness = Ecran.Unites(5);
            _ligneFond.Opacity = 0.5;
            _ligneFond.Visibility = Visibility.Collapsed;
            _ligne = new Line();
            _ligne.Stroke = Brushes.White;
            _ligne.StrokeThickness = Ecran.Unites(3);
            // Pointillés : 3 épaisseurs de trait, puis 2 épaisseurs de vide, et ainsi de suite.
            _ligne.StrokeDashArray = new DoubleCollection(new double[] { 3, 2 });
            _ligne.Visibility = Visibility.Collapsed;
            surface.Children.Add(_ligneFond);
            surface.Children.Add(_ligne);

            // Le point de départ : un petit disque blanc bordé de noir.
            Point depart = calque.VersWpf(_glissement.DepartX, _glissement.DepartY);
            double r = calque.LongueurVersWpf(Ecran.Px(RayonPointDepart));
            Ellipse point = new Ellipse();
            point.Fill = Brushes.White;
            point.Stroke = Brushes.Black;
            point.StrokeThickness = Ecran.Unites(2);
            point.Width = r * 2;
            point.Height = r * 2;
            Canvas.SetLeft(point, depart.X - r);
            Canvas.SetTop(point, depart.Y - r);
            surface.Children.Add(point);

            // Le bouton « retour », au bord droit de l'écran, centré verticalement.
            // Symbole E72B = flèche « retour » de la police Windows.
            double xRetour = Gestionnaire.LargeurEcran - taille / 2 - Ecran.Px(10);
            double yRetour = Gestionnaire.HauteurEcran / 2;
            BoutonRegard retour = new BoutonRegard("", null, "Annuler le glisser-déposer");
            retour.Placer(calque, surface, xRetour, yRetour, taille);
            _boutons.Add(retour);

            calque.AfficherSurfaceGlisser();
        }

        /// <summary>Place la ligne du trajet entre le point de départ et (x, y), en pixels d'écran.</summary>
        private void PlacerLigne(double x, double y)
        {
            if (_ligne == null)
            {
                return;
            }

            OverlayWindow calque = Gestionnaire.Calque;
            Point depart = calque.VersWpf(_glissement.DepartX, _glissement.DepartY);
            Point arrivee = calque.VersWpf(x, y);

            foreach (Line trait in new Line[] { _ligneFond, _ligne })
            {
                trait.X1 = depart.X;
                trait.Y1 = depart.Y;
                trait.X2 = arrivee.X;
                trait.Y2 = arrivee.Y;
                trait.Visibility = Visibility.Visible;
            }
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
