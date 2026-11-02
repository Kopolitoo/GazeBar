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
    /// On y arrive de trois façons :
    ///  - depuis le mode choix d'action, avec l'action « Loupe » de la couronne
    ///    (emplacement 4 par défaut) ;
    ///  - depuis le mode regard fixe, directement, si l'option [LoupeAvantAction] est activée
    ///    (onglet « Loupe, glisser » du mode paramètres, étape 7) ;
    ///  - pendant un glisser-déposer (étape 6), pour affiner le point d'arrivée (2e temps,
    ///    voir Glissement.cs). La fixation lance alors l'exécution du glisser
    ///    (ModeGlisserExecution) au lieu du choix d'action, et l'icône « retour », placée au
    ///    bord droit de l'écran, annule le glisser-déposer (retour au mode mouvement).
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
    ///    EXCEPTION (demande de l'utilisateur du 28/10/2026) : si, à l'ouverture, le regard est
    ///    déjà sur l'endroit de la photo où apparaît P (le centre de l'écran, sauf près d'un
    ///    bord), il n'y a pas de zone ignorée. C'est le cas quand l'icône d'options du regard
    ///    fixe se trouvait près du centre de l'écran : on regarde déjà le bon endroit, il
    ///    serait absurde d'obliger à bouger le regard pour valider la position ;
    ///  - seule une fixation SUR la photo compte. Le regard posé sur l'icône « retour » ou sur
    ///    le fond noir ne place jamais le curseur.
    /// </summary>
    internal sealed class MagnifierMode : Mode
    {
        /// <summary>Les deux temps du mode (voir le commentaire de la classe).</summary>
        private enum Step
        {
            Capture,
            Aim
        }

        /// <summary>
        /// Attente avant la photo, en millisecondes : le temps que Windows efface réellement ce
        /// qu'on vient de cacher (même valeur que dans le choix d'action).
        /// </summary>
        private const double WaitMs = 60;

        /// <summary>
        /// Diamètre du petit cercle qui suit le regard, en pixels de l'écran 4K (cahier des
        /// charges : 50 px), converti avec Ecran.Px (étape 11).
        /// </summary>
        private const double GazeCircleDiameter = 50;

        // Le point visé P (position du curseur à l'entrée), en pixels d'écran.
        private readonly double _px;
        private readonly double _py;

        // Vrai si l'on vient du choix d'action : l'icône « retour » y ramène.
        private readonly bool _backToActionChoice;

        // Le glisser-déposer dont on affine l'arrivée, ou null pour une loupe ordinaire.
        private readonly DragGesture _drag;

        private Step _step;
        private double _stepStartMs;

        // La zone photographiée (pixels d'écran) : coin haut-gauche et côté.
        private int _ox;
        private int _oy;
        private int _side;

        // La photo agrandie sur l'écran noir (pixels d'écran) : coin haut-gauche et zoom.
        private double _left;
        private double _top;
        private double _zoom;

        // Le bouton « retour » (seul bouton de ce mode) et ce qui décide s'il est activé.
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly ButtonAimer _aimer;

        // La détection de fixation, la même qu'en mode mouvement.
        private readonly FixationDetector _fixation = new FixationDetector();

        // Zone ignorée : là où était le regard à l'ouverture de la loupe (voir le commentaire
        // de la classe). _zoneIgnoreeConnue = faux tant qu'on n'a pas eu de mesure sur l'écran.
        private bool _ignoredZone;
        private bool _ignoredZoneKnown;
        private double _zoneX;
        private double _zoneY;

        // Où apparaît le point P sur la photo agrandie (pixels d'écran), calculé avec la photo.
        // _cibleConnue = faux tant que la photo n'est pas construite.
        private bool _knownTarget;
        private double _targetX;
        private double _targetY;

        /// <summary>
        /// Zone d'activation du bouton « retour », par rapport au bouton dessiné : 30 % plus
        /// grande (demande de l'utilisateur du 28/10/2026).
        /// </summary>
        private const double BackZoneFactor = 1.3;

        // Le petit cercle qui suit le regard : deux ellipses (trait noir dessous, vert dessus).
        private Ellipse _gazeCircleBackground;
        private Ellipse _gazeCircle;
        private readonly Smoother _smoothed = new Smoother();
        private bool _gazeCirclePlaced;

        /// <summary>
        /// Ouvre la loupe autour du point (x, y), en pixels d'écran.
        /// retourAuChoixAction : vrai si l'on vient du choix d'action (l'icône « retour » y
        /// ramène), faux si l'on vient du mode regard fixe (retour au mode mouvement).
        /// </summary>
        public MagnifierMode(double x, double y, bool backToActionChoice)
        {
            _px = x;
            _py = y;
            _backToActionChoice = backToActionChoice;
            _aimer = new ButtonAimer(_buttons);
        }

        /// <summary>
        /// Ouvre la loupe autour du point d'arrivée (x, y) d'un glisser-déposer (étape 6).
        /// La fixation lance l'exécution du glisser ; l'icône « retour » l'annule.
        /// </summary>
        public MagnifierMode(double x, double y, DragGesture drag)
        {
            _px = x;
            _py = y;
            _drag = drag;
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get { return "Loupe"; }
        }

        public override void Enter(GazeState gaze)
        {
            // On repart de zéro : nouvelle photo, aucune fixation en cours.
            _aimer.Reset();
            _fixation.Reset();

            // La zone ignorée est centrée sur le regard actuel (s'il est sur l'écran),
            // sinon sur la première mesure sur l'écran qui arrivera.
            _ignoredZone = true;
            _ignoredZoneKnown = gaze.OnScreen;
            _zoneX = gaze.X;
            _zoneY = gaze.Y;
            _knownTarget = false;

            // 1. CAPTURE : on cache tout ce que GazeBar dessine, et on attend un peu avant la photo.
            Manager.Layer.HideAll();
            _step = Step.Capture;
            _stepStartMs = gaze.NowMs;
        }

        public override void Update(GazeState gaze)
        {
            if (_step == Step.Capture)
            {
                if (gaze.NowMs - _stepStartMs >= WaitMs)
                {
                    bool succeeded;
                    try
                    {
                        succeeded = BuildScreen();
                    }
                    catch (Exception ex)
                    {
                        App.Log("Loupe : erreur pendant la construction de l'écran : " + ex);
                        succeeded = false;
                    }

                    if (!succeeded)
                    {
                        // Photo impossible : on ne reste pas bloqué devant un écran noir.
                        Manager.ChangeMode(new MovementMode());
                    }
                }
                return;
            }

            UpdateAim(gaze);
        }

        public override void Quit()
        {
            // L'écran noir est caché et vidé (la photo n'est plus utile).
            Manager.Layer.HideChoiceScreen();
            Manager.Layer.ChoiceScreen.Children.Clear();
            _buttons.Clear();
            _gazeCircle = null;
            _gazeCircleBackground = null;
        }

        // ───────────────────────── 2. La visée ─────────────────────────

        private void UpdateAim(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // a. L'icône « retour » vient-elle d'être activée ?
            int active = _aimer.Update(gaze, p.ActivationDelay, p.ShowLabels);
            if (active >= 0)
            {
                Back();
                return;
            }

            // b. Le petit cercle du regard.
            MoveGazeCircle(gaze, p);

            // c. La zone ignorée : le regard en est-il sorti ?
            if (gaze.OnScreen)
            {
                if (!_ignoredZoneKnown)
                {
                    _zoneX = gaze.X;
                    _zoneY = gaze.Y;
                    _ignoredZoneKnown = true;
                    LiftZoneIfAlreadyOnTarget(p);
                }
                else if (_ignoredZone && Distance(gaze.X, gaze.Y, _zoneX, _zoneY) > p.CircleRadiusPx)
                {
                    _ignoredZone = false;
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
            _fixation.Add(gaze);
            if (_ignoredZone || (gaze.OnScreen && !OnPhoto(gaze.X, gaze.Y)))
            {
                _fixation.Reset();
            }

            if (_fixation.Reached && OnPhoto(_fixation.X, _fixation.Y))
            {
                PlaceAndContinue(_fixation.X, _fixation.Y);
            }
        }

        /// <summary>
        /// Le regard s'est fixé en (fx, fy) sur la photo agrandie : on calcule le point réel
        /// correspondant, on y place le curseur, et on passe au choix d'action.
        /// </summary>
        private void PlaceAndContinue(double fx, double fy)
        {
            // Exemple avec un zoom de 4 : un point à 100 px à droite du bord gauche de la photo
            // agrandie est à 100 ÷ 4 = 25 px à droite du bord gauche de la zone photographiée.
            double x = _ox + (fx - _left) / _zoom;
            double y = _oy + (fy - _top) / _zoom;

            // Par sécurité, on reste dans la zone photographiée (le dernier pixel compris).
            x = Clamp(x, _ox, _ox + _side - 1);
            y = Clamp(y, _oy, _oy + _side - 1);

            if (_drag != null)
            {
                // Glisser-déposer : (x, y) est le point d'arrivée. Le curseur reste sur le point
                // de départ ; c'est l'exécution qui l'emmènera, bouton enfoncé.
                App.Log("Loupe : arrivée du glisser affinée en (" + x.ToString("F0") + ", " +
                        y.ToString("F0") + "), au lieu de (" + _px.ToString("F0") + ", " + _py.ToString("F0") + ")");
                Manager.ChangeMode(new DragExecutionMode(_drag, x, y));
                return;
            }

            NativeMethods.SetCursorPos((int)Math.Round(x), (int)Math.Round(y));
            App.Log("Loupe : fixation en (" + fx.ToString("F0") + ", " + fy.ToString("F0") +
                    ") sur la photo → curseur en (" + x.ToString("F0") + ", " + y.ToString("F0") +
                    "), au lieu de (" + _px.ToString("F0") + ", " + _py.ToString("F0") + ")");

            Manager.ChangeMode(new ActionChoiceMode(x, y));
        }

        /// <summary>
        /// Icône « retour » : on revient d'où l'on vient, sans rien faire. Le curseur n'a pas
        /// bougé (il est toujours sur P). Pendant un glisser-déposer, « retour » l'annule.
        /// </summary>
        private void Back()
        {
            App.Log(_drag != null ? "Loupe : glisser-déposer annulé (retour)" : "Loupe : retour");
            if (_drag != null)
            {
                Manager.ChangeMode(new MovementMode());
            }
            else if (_backToActionChoice)
            {
                Manager.ChangeMode(new ActionChoiceMode(_px, _py));
            }
            else
            {
                Manager.ChangeMode(new MovementMode());
            }
        }

        /// <summary>
        /// Pas de zone ignorée si, à l'ouverture, le regard était déjà sur l'endroit de la photo
        /// où apparaît P (à moins de [RayonCercle]) : voir l'exception dans le commentaire de la
        /// classe. Ne fait rien tant que la photo ou la position du regard ne sont pas connues.
        /// </summary>
        private void LiftZoneIfAlreadyOnTarget(AppSettings p)
        {
            if (!_ignoredZone || !_ignoredZoneKnown || !_knownTarget)
            {
                return;
            }
            if (Distance(_zoneX, _zoneY, _targetX, _targetY) <= p.CircleRadiusPx)
            {
                _ignoredZone = false;
                App.Log("Loupe : regard déjà sur le point visé, pas besoin de bouger le regard");
            }
        }

        /// <summary>Vrai si ce point (pixels d'écran) est sur la photo agrandie.</summary>
        private bool OnPhoto(double x, double y)
        {
            double enlargedSide = _side * _zoom;
            return x >= _left && x <= _left + enlargedSide &&
                   y >= _top && y <= _top + enlargedSide;
        }

        // ───────────────────────── 1. La photo et l'écran noir ─────────────────────────

        /// <summary>
        /// Prend la photo et remplit l'écran noir. Renvoie faux si c'est impossible.
        /// Toutes les positions sont calculées en pixels d'écran, puis converties par le calque.
        /// </summary>
        private bool BuildScreen()
        {
            OverlayWindow layer = Manager.Layer;
            if (!layer.Ready)
            {
                return false;
            }

            AppSettings p = AppSettings.Current;
            double width = Manager.ScreenWidth;
            double height = Manager.ScreenHeight;
            double size = p.IconSizePx;
            _zoom = p.MagnifierZoom;

            // ── La zone photographiée : le carré qui englobe le cercle, centré sur P.
            //    La photo agrandie doit tenir sur l'écran (avec une marge de 40 px de l'écran 4K,
            //    convertie avec Ecran.Px, étape 11) : sur un
            //    petit écran, ou avec un grand zoom, on photographie donc un carré plus petit.
            //    Exemple : écran de 1080 px de haut, zoom 4 → au plus (1080 − 40) ÷ 4 = 260 px.
            int side = (int)Math.Round(2 * p.CircleRadiusPx);
            int maxSide = (int)Math.Floor((Math.Min(width, height) - ScreenMetrics.Px(40)) / _zoom);
            side = Math.Max(10, Math.Min(side, maxSide));
            //    Près d'un bord de l'écran, le carré est décalé pour rester entièrement sur
            //    l'écran (P n'est alors plus au centre de la photo).
            _ox = (int)Clamp(Math.Round(_px - side / 2.0), 0, width - side);
            _oy = (int)Clamp(Math.Round(_py - side / 2.0), 0, height - side);
            _side = side;

            BitmapSource photo = ScreenCapture.Capture(_ox, _oy, side, side);
            if (photo == null)
            {
                return false;
            }

            layer.PrepareChoiceScreen();
            Canvas screen = layer.ChoiceScreen;

            // ── La photo agrandie, au centre de l'écran.
            double cxScreen = width / 2;
            double cyScreen = height / 2;
            double enlargedSide = side * _zoom;
            _left = cxScreen - enlargedSide / 2;
            _top = cyScreen - enlargedSide / 2;

            // Où apparaît P sur la photo agrandie (le contraire du calcul de PlacerEtContinuer).
            _targetX = _left + (_px - _ox) * _zoom;
            _targetY = _top + (_py - _oy) * _zoom;
            _knownTarget = true;
            LiftZoneIfAlreadyOnTarget(p);

            Image image = new Image();
            image.Source = photo;
            image.Stretch = Stretch.Fill;
            // « Plus proche voisin » : chaque pixel devient un carré net de 4 × 4, au lieu
            // d'être adouci (flou). On voit ainsi exactement où tombe chaque pixel réel.
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Point corner = layer.ToWpf(_left, _top);
            image.Width = layer.LengthToWpf(enlargedSide);
            image.Height = image.Width;
            Canvas.SetLeft(image, corner.X);
            Canvas.SetTop(image, corner.Y);
            screen.Children.Add(image);

            // Un fin cadre gris autour de la photo.
            Rectangle frame = new Rectangle();
            frame.Stroke = Brushes.Gray;
            // Épaisseur en unités de l'écran de référence, convertie (étape 11, voir Ecran.cs).
            double stroke = ScreenMetrics.Units(2);
            frame.StrokeThickness = stroke;
            frame.Width = image.Width + 2 * stroke;
            frame.Height = image.Height + 2 * stroke;
            Canvas.SetLeft(frame, corner.X - stroke);
            Canvas.SetTop(frame, corner.Y - stroke);
            screen.Children.Add(frame);

            // ── L'icône « retour », à droite,
            //    à la même hauteur que dans le choix d'action (milieu de l'écran).
            //    Pendant un glisser-déposer, elle est collée au bord droit de l'écran, à la même
            //    place que pendant le choix de l'arrivée (choix de l'utilisateur du 25/10/2026).
            //    Symbole E72B = flèche « retour » de la police Windows.
            double xBack = width - size;
            string backLabel = _drag != null ? "Annuler le glisser-déposer" : "Retour";
            GazeButton back = new GazeButton("", null, backLabel);
            back.ZoneFactor = BackZoneFactor;
            back.Place(layer, screen, xBack, cyScreen, size);
            _buttons.Add(back);

            // ── Le petit cercle du regard, ajouté EN DERNIER pour être dessiné par-dessus.
            //    Il apparaîtra à la première mesure sur l'écran.
            _gazeCircleBackground = new Ellipse();
            _gazeCircleBackground.Stroke = Brushes.Black;
            _gazeCircleBackground.StrokeThickness = ScreenMetrics.Units(4);
            _gazeCircleBackground.Opacity = 0.6;
            _gazeCircleBackground.Visibility = Visibility.Collapsed;
            _gazeCircle = new Ellipse();
            _gazeCircle.Stroke = Brushes.LimeGreen;
            _gazeCircle.StrokeThickness = ScreenMetrics.Units(2);
            _gazeCircle.Visibility = Visibility.Collapsed;
            screen.Children.Add(_gazeCircleBackground);
            screen.Children.Add(_gazeCircle);
            _gazeCirclePlaced = false;

            layer.ShowChoiceScreen();
            _step = Step.Aim;
            App.Log("Loupe : photo de " + side + " × " + side + " px en (" + _ox + ", " + _oy +
                    "), agrandie ×" + _zoom.ToString("0.##"));
            return true;
        }

        /// <summary>
        /// Déplace le petit cercle du regard : il suit le regard en douceur ([Lissage], voir
        /// Lisseur.cs). Regard hors de l'écran : il glisse le long du bord. Regard perdu : il reste où il est.
        /// </summary>
        private void MoveGazeCircle(GazeState gaze, AppSettings p)
        {
            if (_gazeCircle == null || !gaze.Available)
            {
                return;
            }

            // Position ramenée dans l'écran : regard hors de l'écran → le cercle glisse le
            // long du bord (voir EtatRegard.XEcran).
            if (!_gazeCirclePlaced)
            {
                _smoothed.Place(gaze.XScreen, gaze.YScreen);
                _gazeCirclePlaced = true;
            }
            _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);

            OverlayWindow layer = Manager.Layer;
            Point center = layer.ToWpf(_smoothed.X, _smoothed.Y);
            double radius = layer.LengthToWpf(ScreenMetrics.Px(GazeCircleDiameter) / 2);
            PlaceEllipse(_gazeCircleBackground, center, radius);
            PlaceEllipse(_gazeCircle, center, radius);
        }

        private static void PlaceEllipse(Ellipse ellipse, Point center, double radius)
        {
            ellipse.Width = radius * 2;
            ellipse.Height = radius * 2;
            Canvas.SetLeft(ellipse, center.X - radius);
            Canvas.SetTop(ellipse, center.Y - radius);
            ellipse.Visibility = Visibility.Visible;
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double Clamp(double val, double min, double max)
        {
            return Math.Max(min, Math.Min(max, val));
        }
    }
}
