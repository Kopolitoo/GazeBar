using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// MODE LOUPE (étape 5 du cahier des charges) : affiner la position du curseur.
    ///
    /// Pourquoi ? Le regard se trompe souvent d'une trentaine de pixels. C'est trop pour viser
    /// une petite case à cocher. Dans la loupe, la zone autour du point visé est agrandie
    /// [ZoomLoupe] fois (×4 par défaut) : une erreur de 30 px sur l'écran ne vaut plus que
    /// 30 ÷ 4 ≈ 7 à 8 px réels.
    ///
    /// On y arrive de deux façons :
    ///  - depuis le mode choix d'action, avec l'action « Loupe » de la couronne
    ///    (emplacement 4 par défaut) ;
    ///  - depuis le mode regard fixe, directement, si l'option [LoupeAvantAction] est activée
    ///    (à mettre à true dans parametres.json en attendant la fenêtre des paramètres, étape 7).
    ///
    /// Comme le choix d'action, le mode se déroule en deux temps (« étapes ») :
    ///
    ///  1. CAPTURE. Tout ce que GazeBar dessine est caché, on attend [AttenteMs] pour que
    ///     Windows l'ait effacé, puis on photographie le carré qui englobe le cercle autour du
    ///     point visé P (côté 2 × [RayonCercle], soit 200 × 200 px).
    ///
    ///  2. VISÉE. Un écran noir couvre tout l'écran. Au centre : la photo agrandie ×[ZoomLoupe]
    ///     (800 × 800 px). Un petit cercle de 50 px de diamètre suit le regard. À droite :
    ///     l'icône « retour ».
    ///     Quand le regard se fixe sur la photo (même détection qu'en mode mouvement), le point
    ///     fixé est converti en coordonnées réelles : on retire le coin de la photo agrandie,
    ///     on divise par le zoom, et on ajoute le coin de la zone photographiée. Le curseur y
    ///     est placé, puis on passe au mode choix d'action sur ce point précis.
    ///
    /// L'icône « retour » revient au mode d'où l'on vient, sans rien faire (demande de
    /// l'utilisateur dans le cahier des charges) : le choix d'action (sur le même point P)
    /// si l'on vient de la couronne, sinon le mode mouvement.
    ///
    /// Deux précautions :
    ///  - à l'ouverture de la loupe, le regard est encore sur l'icône qu'on vient d'activer,
    ///    qui peut se trouver au-dessus de la photo. Une fixation n'est donc reconnue qu'une
    ///    fois le regard sorti de cet endroit (même principe que la « zone ignorée » du mode
    ///    mouvement après un clic) ;
    ///  - seule une fixation SUR la photo compte. Le regard posé sur l'icône « retour » ou sur
    ///    le fond noir ne place jamais le curseur.
    /// </summary>
    internal sealed class ModeLoupe : Mode
    {
        /// <summary>Les deux temps du mode (voir le commentaire de la classe).</summary>
        private enum Etape
        {
            Capture,
            Visee
        }

        /// <summary>
        /// Attente avant la photo, en millisecondes : le temps que Windows efface réellement ce
        /// qu'on vient de cacher (même valeur que dans le choix d'action).
        /// </summary>
        private const double AttenteMs = 60;

        /// <summary>Diamètre du petit cercle qui suit le regard, en pixels (cahier des charges : 50 px).</summary>
        private const double DiametreCercleRegard = 50;

        // Le point visé P (position du curseur à l'entrée), en pixels d'écran.
        private readonly double _px;
        private readonly double _py;

        // Vrai si l'on vient du choix d'action : l'icône « retour » y ramène.
        private readonly bool _retourAuChoixAction;

        private Etape _etape;
        private double _debutEtapeMs;

        // La zone photographiée (pixels d'écran) : coin haut-gauche et côté.
        private int _ox;
        private int _oy;
        private int _cote;

        // La photo agrandie sur l'écran noir (pixels d'écran) : coin haut-gauche et zoom.
        private double _gauche;
        private double _haut;
        private double _zoom;

        // Le bouton « retour » (seul bouton de ce mode) et ce qui décide s'il est activé.
        private readonly List<BoutonRegard> _boutons = new List<BoutonRegard>();
        private readonly ViseurBoutons _viseur;

        // La détection de fixation, la même qu'en mode mouvement.
        private readonly DetecteurFixation _fixation = new DetecteurFixation();

        // Zone ignorée : là où était le regard à l'ouverture de la loupe (voir le commentaire
        // de la classe). _zoneIgnoreeConnue = faux tant qu'on n'a pas eu de mesure sur l'écran.
        private bool _zoneIgnoree;
        private bool _zoneIgnoreeConnue;
        private double _zoneX;
        private double _zoneY;

        // Le petit cercle qui suit le regard : deux ellipses (trait noir dessous, vert dessus).
        private Ellipse _cercleRegardFond;
        private Ellipse _cercleRegard;
        private double _rx;
        private double _ry;
        private bool _cercleRegardPlace;

        /// <summary>
        /// Ouvre la loupe autour du point (x, y), en pixels d'écran.
        /// retourAuChoixAction : vrai si l'on vient du choix d'action (l'icône « retour » y
        /// ramène), faux si l'on vient du mode regard fixe (retour au mode mouvement).
        /// </summary>
        public ModeLoupe(double x, double y, bool retourAuChoixAction)
        {
            _px = x;
            _py = y;
            _retourAuChoixAction = retourAuChoixAction;
            _viseur = new ViseurBoutons(_boutons);
        }

        public override string Nom
        {
            get { return "Loupe"; }
        }

        /// <summary>Comme le choix d'action : pas de menu pendant l'instant de la photo.</summary>
        public override bool MenuAutorise
        {
            get { return _etape == Etape.Visee; }
        }

        public override void Entrer(EtatRegard regard)
        {
            // On peut revenir dans ce mode en fermant le menu : on repart alors de zéro
            // (nouvelle photo, aucune fixation en cours).
            _viseur.Reinitialiser();
            _fixation.Reinitialiser();

            // La zone ignorée est centrée sur le regard actuel (s'il est sur l'écran),
            // sinon sur la première mesure sur l'écran qui arrivera.
            _zoneIgnoree = true;
            _zoneIgnoreeConnue = regard.SurEcran;
            _zoneX = regard.X;
            _zoneY = regard.Y;

            // 1. CAPTURE : on cache tout ce que GazeBar dessine, et on attend un peu avant la photo.
            Gestionnaire.Calque.MasquerTout();
            _etape = Etape.Capture;
            _debutEtapeMs = regard.MaintenantMs;
        }

        public override void Actualiser(EtatRegard regard)
        {
            if (_etape == Etape.Capture)
            {
                if (regard.MaintenantMs - _debutEtapeMs >= AttenteMs)
                {
                    bool reussi;
                    try
                    {
                        reussi = ConstruireEcran();
                    }
                    catch (Exception ex)
                    {
                        App.Log("Loupe : erreur pendant la construction de l'écran : " + ex);
                        reussi = false;
                    }

                    if (!reussi)
                    {
                        // Photo impossible : on ne reste pas bloqué devant un écran noir.
                        Gestionnaire.ChangerMode(new ModeMouvement());
                    }
                }
                return;
            }

            ActualiserVisee(regard);
        }

        public override void Quitter()
        {
            // L'écran noir est caché et vidé (la photo n'est plus utile).
            Gestionnaire.Calque.MasquerEcranChoix();
            Gestionnaire.Calque.EcranChoix.Children.Clear();
            _boutons.Clear();
            _cercleRegard = null;
            _cercleRegardFond = null;
        }

        // ───────────────────────── 2. La visée ─────────────────────────

        private void ActualiserVisee(EtatRegard regard)
        {
            Parametres p = Parametres.Courants;

            // a. L'icône « retour » vient-elle d'être activée ?
            int active = _viseur.Actualiser(regard, p.DelaiActivation, p.AfficherLibelles);
            if (active >= 0)
            {
                Retour();
                return;
            }

            // b. Le petit cercle du regard.
            DeplacerCercleRegard(regard, p);

            // c. La zone ignorée : le regard en est-il sorti ?
            if (regard.SurEcran)
            {
                if (!_zoneIgnoreeConnue)
                {
                    _zoneX = regard.X;
                    _zoneY = regard.Y;
                    _zoneIgnoreeConnue = true;
                }
                else if (_zoneIgnoree && Distance(regard.X, regard.Y, _zoneX, _zoneY) > p.RayonCercle)
                {
                    _zoneIgnoree = false;
                }
            }

            // d. La fixation. Elle ne compte que sur la photo, et hors de la zone ignorée.
            //    Pas d'anneau de progression (comme en mode mouvement) : le petit cercle suffit.
            //
            //    Remarque sur le rayon : le cahier des charges parle d'un « rayon adapté à
            //    l'agrandissement ». On garde ici [RayonFixation] tel quel, en pixels d'écran,
            //    car il mesure le tremblement du regard, et celui-ci ne change pas avec le zoom :
            //    l'œil tremble de la même façon devant une photo agrandie. C'est la conversion
            //    finale (division par le zoom) qui apporte la précision.
            _fixation.Ajouter(regard);
            if (_zoneIgnoree || (regard.SurEcran && !SurLaPhoto(regard.X, regard.Y)))
            {
                _fixation.Reinitialiser();
            }

            if (_fixation.Atteinte && SurLaPhoto(_fixation.X, _fixation.Y))
            {
                PlacerEtContinuer(_fixation.X, _fixation.Y);
            }
        }

        /// <summary>
        /// Le regard s'est fixé en (fx, fy) sur la photo agrandie : on calcule le point réel
        /// correspondant, on y place le curseur, et on passe au choix d'action.
        /// </summary>
        private void PlacerEtContinuer(double fx, double fy)
        {
            // Exemple avec un zoom de 4 : un point à 100 px à droite du bord gauche de la photo
            // agrandie est à 100 ÷ 4 = 25 px à droite du bord gauche de la zone photographiée.
            double x = _ox + (fx - _gauche) / _zoom;
            double y = _oy + (fy - _haut) / _zoom;

            // Par sécurité, on reste dans la zone photographiée (le dernier pixel compris).
            x = Borner(x, _ox, _ox + _cote - 1);
            y = Borner(y, _oy, _oy + _cote - 1);

            NativeMethods.SetCursorPos((int)Math.Round(x), (int)Math.Round(y));
            App.Log("Loupe : fixation en (" + fx.ToString("F0") + ", " + fy.ToString("F0") +
                    ") sur la photo → curseur en (" + x.ToString("F0") + ", " + y.ToString("F0") +
                    "), au lieu de (" + _px.ToString("F0") + ", " + _py.ToString("F0") + ")");

            Gestionnaire.ChangerMode(new ModeChoixAction(x, y));
        }

        /// <summary>
        /// Icône « retour » : on revient d'où l'on vient, sans rien faire. Le curseur n'a pas
        /// bougé (il est toujours sur P).
        /// </summary>
        private void Retour()
        {
            App.Log("Loupe : retour");
            if (_retourAuChoixAction)
            {
                Gestionnaire.ChangerMode(new ModeChoixAction(_px, _py));
            }
            else
            {
                Gestionnaire.ChangerMode(new ModeMouvement());
            }
        }

        /// <summary>Vrai si ce point (pixels d'écran) est sur la photo agrandie.</summary>
        private bool SurLaPhoto(double x, double y)
        {
            double coteAgrandi = _cote * _zoom;
            return x >= _gauche && x <= _gauche + coteAgrandi &&
                   y >= _haut && y <= _haut + coteAgrandi;
        }

        // ───────────────────────── 1. La photo et l'écran noir ─────────────────────────

        /// <summary>
        /// Prend la photo et remplit l'écran noir. Renvoie faux si c'est impossible.
        /// Toutes les positions sont calculées en pixels d'écran, puis converties par le calque.
        /// </summary>
        private bool ConstruireEcran()
        {
            OverlayWindow calque = Gestionnaire.Calque;
            if (!calque.Pret)
            {
                return false;
            }

            Parametres p = Parametres.Courants;
            double largeur = Gestionnaire.LargeurEcran;
            double hauteur = Gestionnaire.HauteurEcran;
            double taille = p.TailleIcones;
            _zoom = p.ZoomLoupe;

            // ── La zone photographiée : le carré qui englobe le cercle, centré sur P.
            //    La photo agrandie doit tenir sur l'écran (avec une marge de 40 px) : sur un
            //    petit écran, ou avec un grand zoom, on photographie donc un carré plus petit.
            //    Exemple : écran de 1080 px de haut, zoom 4 → au plus (1080 − 40) ÷ 4 = 260 px.
            int cote = (int)Math.Round(2 * p.RayonCercle);
            int coteMax = (int)Math.Floor((Math.Min(largeur, hauteur) - 40) / _zoom);
            cote = Math.Max(10, Math.Min(cote, coteMax));
            //    Près d'un bord de l'écran, le carré est décalé pour rester entièrement sur
            //    l'écran (P n'est alors plus au centre de la photo).
            _ox = (int)Borner(Math.Round(_px - cote / 2.0), 0, largeur - cote);
            _oy = (int)Borner(Math.Round(_py - cote / 2.0), 0, hauteur - cote);
            _cote = cote;

            BitmapSource photo = CaptureEcran.Capturer(_ox, _oy, cote, cote);
            if (photo == null)
            {
                return false;
            }

            calque.PreparerEcranChoix();
            Canvas ecran = calque.EcranChoix;

            // ── La photo agrandie, au centre de l'écran.
            double cxEcran = largeur / 2;
            double cyEcran = hauteur / 2;
            double coteAgrandi = cote * _zoom;
            _gauche = cxEcran - coteAgrandi / 2;
            _haut = cyEcran - coteAgrandi / 2;

            Image image = new Image();
            image.Source = photo;
            image.Stretch = Stretch.Fill;
            // « Plus proche voisin » : chaque pixel devient un carré net de 4 × 4, au lieu
            // d'être adouci (flou). On voit ainsi exactement où tombe chaque pixel réel.
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Point coin = calque.VersWpf(_gauche, _haut);
            image.Width = calque.LongueurVersWpf(coteAgrandi);
            image.Height = image.Width;
            Canvas.SetLeft(image, coin.X);
            Canvas.SetTop(image, coin.Y);
            ecran.Children.Add(image);

            // Un fin cadre gris autour de la photo.
            Rectangle cadre = new Rectangle();
            cadre.Stroke = Brushes.Gray;
            cadre.StrokeThickness = 2;
            cadre.Width = image.Width + 4;
            cadre.Height = image.Height + 4;
            Canvas.SetLeft(cadre, coin.X - 2);
            Canvas.SetTop(cadre, coin.Y - 2);
            ecran.Children.Add(cadre);

            // ── L'icône « retour », à droite : à mi-chemin entre la photo et le bord droit,
            //    à la même hauteur que dans le choix d'action (milieu de l'écran).
            //    Symbole E72B = flèche « retour » de la police Windows.
            double xRetour = (_gauche + coteAgrandi + largeur) / 2;
            xRetour = Math.Min(xRetour, largeur - taille / 2 - 10);
            BoutonRegard retour = new BoutonRegard("", null, "Retour");
            retour.Placer(calque, ecran, xRetour, cyEcran, taille);
            _boutons.Add(retour);

            // ── Le petit cercle du regard, ajouté EN DERNIER pour être dessiné par-dessus.
            //    Il apparaîtra à la première mesure sur l'écran.
            _cercleRegardFond = new Ellipse();
            _cercleRegardFond.Stroke = Brushes.Black;
            _cercleRegardFond.StrokeThickness = 4;
            _cercleRegardFond.Opacity = 0.6;
            _cercleRegardFond.Visibility = Visibility.Collapsed;
            _cercleRegard = new Ellipse();
            _cercleRegard.Stroke = Brushes.LimeGreen;
            _cercleRegard.StrokeThickness = 2;
            _cercleRegard.Visibility = Visibility.Collapsed;
            ecran.Children.Add(_cercleRegardFond);
            ecran.Children.Add(_cercleRegard);
            _cercleRegardPlace = false;

            calque.AfficherEcranChoix();
            _etape = Etape.Visee;
            App.Log("Loupe : photo de " + cote + " × " + cote + " px en (" + _ox + ", " + _oy +
                    "), agrandie ×" + _zoom.ToString("0.##"));
            return true;
        }

        /// <summary>
        /// Déplace le petit cercle du regard : à chaque tic, il parcourt une fraction [Lissage]
        /// du chemin vers le regard. Regard perdu ou hors de l'écran : il reste où il est.
        /// </summary>
        private void DeplacerCercleRegard(EtatRegard regard, Parametres p)
        {
            if (_cercleRegard == null || !regard.SurEcran)
            {
                return;
            }

            if (!_cercleRegardPlace)
            {
                _rx = regard.X;
                _ry = regard.Y;
                _cercleRegardPlace = true;
            }
            _rx += (regard.X - _rx) * p.Lissage;
            _ry += (regard.Y - _ry) * p.Lissage;

            OverlayWindow calque = Gestionnaire.Calque;
            Point centre = calque.VersWpf(_rx, _ry);
            double rayon = calque.LongueurVersWpf(DiametreCercleRegard / 2);
            PlacerEllipse(_cercleRegardFond, centre, rayon);
            PlacerEllipse(_cercleRegard, centre, rayon);
        }

        private static void PlacerEllipse(Ellipse ellipse, Point centre, double rayon)
        {
            ellipse.Width = rayon * 2;
            ellipse.Height = rayon * 2;
            Canvas.SetLeft(ellipse, centre.X - rayon);
            Canvas.SetTop(ellipse, centre.Y - rayon);
            ellipse.Visibility = Visibility.Visible;
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double Borner(double valeur, double min, double max)
        {
            return Math.Max(min, Math.Min(max, valeur));
        }
    }
}
