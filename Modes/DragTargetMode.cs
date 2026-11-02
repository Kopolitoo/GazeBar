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
    internal sealed class DragTargetMode : Mode
    {
        /// <summary>Rayon du point qui marque le départ, en pixels de l'écran 4K (converti avec Ecran.Px, étape 11).</summary>
        private const double StartDotRadius = 6;

        private readonly DragGesture _drag;

        private readonly FixationDetector _fixation = new FixationDetector();

        // Le bouton « retour » (seul bouton de ce mode) et ce qui décide s'il est activé.
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly ButtonAimer _aimer;

        // Position (décimale) du cercle lissé, en pixels d'écran ; _place = faux tant qu'il
        // n'a pas de position.
        private readonly Smoother _smoothed = new Smoother();
        private bool _placed;

        // Zone ignorée : là où était le regard à l'entrée (voir le commentaire de la classe).
        private bool _ignoredZone;
        private bool _ignoredZoneKnown;
        private double _zoneX;
        private double _zoneY;

        // La ligne du trajet : un trait noir continu dessous et un trait blanc en pointillés
        // dessus, pour qu'elle se voie sur tous les fonds.
        private Line _rowBackground;
        private Line _row;

        public DragTargetMode(DragGesture drag)
        {
            _drag = drag;
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get { return "Glisser-déposer (arrivée)"; }
        }

        public override void Enter(GazeState gaze)
        {
            _aimer.Reset();
            _fixation.Reset();

            OverlayWindow layer = Manager.Layer;
            layer.HideAll();
            if (!layer.Ready)
            {
                Manager.ChangeMode(new MovementMode());
                return;
            }

            // La zone ignorée est centrée sur le regard actuel (s'il est sur l'écran),
            // sinon sur la première mesure sur l'écran qui arrivera.
            _ignoredZone = true;
            _ignoredZoneKnown = gaze.OnScreen;
            _zoneX = gaze.X;
            _zoneY = gaze.Y;

            // Le cercle part du regard (le choix d'action l'avait caché : pas de saut visible),
            // ramené dans l'écran s'il en sort.
            _placed = gaze.Available;
            _smoothed.Place(gaze.XScreen, gaze.YScreen);

            BuildSurface();
            App.Log("Glisser : choix de l'arrivée (" + _drag.Name + ", départ en (" +
                    _drag.StartX.ToString("F0") + ", " + _drag.StartY.ToString("F0") +
                    "), modificateurs : " + Modifiers.Description() + ")");
        }

        public override void Update(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // 1. Le bouton « retour » vient-il d'être activé ?
            int active = _aimer.Update(gaze, p.ActivationDelay, p.ShowLabels);
            if (active >= 0)
            {
                App.Log("Glisser : annulé (retour)");
                Manager.ChangeMode(new MovementMode());
                return;
            }

            if (gaze.Available)
            {
                // 2. Le cercle se rapproche du regard, en douceur (voir Lisseur.cs). Regard hors
                //    de l'écran : il reste au bord et glisse le long de ce bord (EtatRegard.XEcran).
                if (!_placed)
                {
                    _smoothed.Place(gaze.XScreen, gaze.YScreen);
                    _placed = true;
                }
                _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);
            }

            if (gaze.OnScreen)
            {
                // 3. La zone ignorée : le regard en est-il sorti ? (Vrai regard sur l'écran seulement.)
                if (!_ignoredZoneKnown)
                {
                    _zoneX = gaze.X;
                    _zoneY = gaze.Y;
                    _ignoredZoneKnown = true;
                }
                else if (_ignoredZone && Distance(gaze.X, gaze.Y, _zoneX, _zoneY) > p.CircleRadiusPx)
                {
                    _ignoredZone = false;
                }
            }

            // 4. La fixation, hors de la zone ignorée et hors du bouton « retour ».
            //    Pas d'anneau de progression, comme en mode mouvement.
            _fixation.Add(gaze);
            bool onBack = gaze.OnScreen && _buttons.Count > 0 && _buttons[0].Contains(gaze.X, gaze.Y);
            if (_ignoredZone || onBack)
            {
                _fixation.Reset();
            }

            if (_fixation.Reached)
            {
                App.Log("Glisser : arrivée fixée en (" + _fixation.X.ToString("F0") + ", " +
                        _fixation.Y.ToString("F0") + "), ouverture de la loupe");
                Manager.ChangeMode(new MagnifierMode(_fixation.X, _fixation.Y, _drag));
                return;
            }

            // 5. Le dessin : le cercle et la ligne du trajet.
            if (_placed)
            {
                Manager.Layer.ShowCircle(_smoothed.X, _smoothed.Y, p.CircleRadiusPx, false);
                PlaceRow(_smoothed.X, _smoothed.Y);
            }
        }

        public override void Quit()
        {
            // La surface est cachée et vidée. Le cercle reste affiché : en cas d'annulation, le
            // mode mouvement le reprend là où il est (la loupe, elle, cache tout).
            Manager.Layer.HideDragSurface();
            Manager.Layer.DragSurface.Children.Clear();
            _buttons.Clear();
            _row = null;
            _rowBackground = null;
        }

        // ───────────────────────── Dessin ─────────────────────────

        /// <summary>Remplit la surface du glisser : ligne du trajet, point de départ, bouton « retour ».</summary>
        private void BuildSurface()
        {
            OverlayWindow layer = Manager.Layer;
            layer.PrepareDragSurface();
            Canvas surface = layer.DragSurface;
            double size = AppSettings.Current.IconSizePx;

            // La ligne : elle partira du point de départ ; son autre bout suit le cercle.
            _rowBackground = new Line();
            _rowBackground.Stroke = Brushes.Black;
            _rowBackground.StrokeThickness = ScreenMetrics.Units(5);
            _rowBackground.Opacity = 0.5;
            _rowBackground.Visibility = Visibility.Collapsed;
            _row = new Line();
            _row.Stroke = Brushes.White;
            _row.StrokeThickness = ScreenMetrics.Units(3);
            // Pointillés : 3 épaisseurs de trait, puis 2 épaisseurs de vide, et ainsi de suite.
            _row.StrokeDashArray = new DoubleCollection(new double[] { 3, 2 });
            _row.Visibility = Visibility.Collapsed;
            surface.Children.Add(_rowBackground);
            surface.Children.Add(_row);

            // Le point de départ : un petit disque blanc bordé de noir.
            Point startPoint = layer.ToWpf(_drag.StartX, _drag.StartY);
            double r = layer.LengthToWpf(ScreenMetrics.Px(StartDotRadius));
            Ellipse point = new Ellipse();
            point.Fill = Brushes.White;
            point.Stroke = Brushes.Black;
            point.StrokeThickness = ScreenMetrics.Units(2);
            point.Width = r * 2;
            point.Height = r * 2;
            Canvas.SetLeft(point, startPoint.X - r);
            Canvas.SetTop(point, startPoint.Y - r);
            surface.Children.Add(point);

            // Le bouton « retour », au bord droit de l'écran, centré verticalement.
            // Symbole E72B = flèche « retour » de la police Windows.
            double xBack = Manager.ScreenWidth - size / 2 - ScreenMetrics.Px(10);
            double yBack = Manager.ScreenHeight / 2;
            GazeButton back = new GazeButton("", null, "Annuler le glisser-déposer");
            back.Place(layer, surface, xBack, yBack, size);
            _buttons.Add(back);

            layer.ShowDragSurface();
        }

        /// <summary>Place la ligne du trajet entre le point de départ et (x, y), en pixels d'écran.</summary>
        private void PlaceRow(double x, double y)
        {
            if (_row == null)
            {
                return;
            }

            OverlayWindow layer = Manager.Layer;
            Point startPoint = layer.ToWpf(_drag.StartX, _drag.StartY);
            Point arrival = layer.ToWpf(x, y);

            foreach (Line stroke in new Line[] { _rowBackground, _row })
            {
                stroke.X1 = startPoint.X;
                stroke.Y1 = startPoint.Y;
                stroke.X2 = arrival.X;
                stroke.Y2 = arrival.Y;
                stroke.Visibility = Visibility.Visible;
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
