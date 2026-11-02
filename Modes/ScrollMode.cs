using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// (emplacement 0 depuis le 28/10/2026). Le curseur est sur le point visé P ; il n'en bouge plus.
    ///
    /// Affichage, par-dessus l'écran réel :
    ///   - le cercle de la zone morte, FIXÉ sur P (le curseur). Son rayon est la moitié de
    ///     [RayonCercle] (voir FacteurZoneMorte) ;
    ///   - un petit arc de cercle coloré, posé sur le cercle, du côté où se trouve le regard :
    ///     il indique la direction du défilement. Depuis le 28/10/2026 (demande de
    ///     l'utilisateur), il est affiché EN PERMANENCE, même quand le regard est dans la zone
    ///     morte (rien ne défile alors) : il montre toujours où GazeBar voit le regard. Regard
    ///     perdu ou sur le bouton retour : il reste à sa dernière place ;
    ///   - cercle et arc sont plus transparents que dans les autres modes (demande du
    ///     28/10/2026), pour mieux voir ce qui défile dessous ;
    ///   - un bouton « retour » au bord droit de l'écran, centré verticalement : il ramène au
    ///     mode mouvement.
    ///
    /// Le défilement :
    ///   - ZONE MORTE = LE CERCLE DESSINÉ, de rayon [RayonCercle] × [FacteurZoneMorte] (la
    ///     moitié, demande de l'utilisateur du 28/10/2026) : regard dedans → rien ne défile ;
    ///   - DIRECTION : regard au-dessus du cercle → vers le haut, en dessous → vers le bas,
    ///     à droite → vers la droite, à gauche → vers la gauche. Un seul sens à la fois :
    ///     celui où l'écart avec le centre est le plus grand (pas de diagonale) ;
    ///   - VITESSE : 0 au bord de la zone morte, puis elle augmente avec la distance, jusqu'à
    ///     [VitesseMaxDefilement] crans par seconde à [RayonsVitesseMax] × [RayonCercle] au-delà
    ///     du bord (trois rayons, demande de l'utilisateur du 26/10/2026). Cette distance de
    ///     montée en vitesse se compte toujours en rayons ENTIERS de [RayonCercle] : réduire la
    ///     zone morte a seulement rapproché le début du défilement, sans rendre la montée plus
    ///     brusque.
    ///     Plus loin, elle ne monte plus ;
    ///   - on envoie des « événements de molette » Windows : c'est la fenêtre sous le curseur
    ///     qui défile (réglage Windows « Faire défiler les fenêtres inactives lorsque je pointe
    ///     dessus », activé par défaut).
    ///
    /// DÉFILEMENT FIN (demande de l'utilisateur du 02/11/2026 : « le défilement est très saccadé ») :
    ///   - avant, la molette partait par quarts de cran (30 unités) : à vitesse lente, 3 ou 4
    ///     petits sauts par seconde. Maintenant, elle part par toutes petites quantités, jusqu'à
    ///     une fois par image (environ 60 fois par seconde), dès qu'on a [PasMinimal] unités.
    ///     Les logiciels récents (navigateurs, Word, Explorateur) additionnent ces petites
    ///     quantités et défilent presque en continu. Les très vieux logiciels, qui attendent un
    ///     cran entier (120), restent par crans : on n'y peut rien de ce côté-là ;
    ///   - la VITESSE elle-même est lissée ([ConstanteVitesseMs]) : elle monte et descend en
    ///     douceur au lieu de changer d'un coup, à l'entrée et à la sortie de la zone morte
    ///     comme à chaque petit tremblement du regard.
    ///
    /// FENÊTRE ACTIVE (même demande : « souvent il me faut cliquer avant de pouvoir faire
    /// défiler ») : en entrant dans ce mode, GazeBar rend active la fenêtre qui est sous P, sans
    /// cliquer (donc sans risque d'ouvrir un lien). Certains logiciels ignorent la molette tant
    /// que leur fenêtre n'est pas active, et si le réglage Windows ci-dessus est désactivé, la
    /// molette va toujours à la fenêtre active. Le journal note l'état de ce réglage, que
    /// l'on peut changer dans l'onglet « Défilement » des paramètres (voir MoletteWindows).
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
    /// Regard légèrement hors de l'écran : il compte quand même, jusqu'à [MargeHorsEcran4K]
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
    internal sealed class ScrollMode : Mode
    {
        /// <summary>
        /// Plus petit envoi de molette, en unités Windows (120 = un cran entier) : 8 unités =
        /// 1/15 de cran. Dès que la réserve atteint cette quantité, on envoie TOUT ce qu'elle
        /// contient (en unités entières), à chaque image s'il le faut. Avant le 02/11/2026, on
        /// envoyait par quarts de cran (30), ce qui faisait des sauts visibles à vitesse lente.
        /// Si un logiciel défile beaucoup trop vite (certains vieux logiciels comptent chaque
        /// envoi comme un cran entier), revenir à 120 ici.
        /// </summary>
        private const int MinStep = 8;

        /// <summary>
        /// Constante de temps du lissage de la vitesse, en millisecondes : en 150 ms, la vitesse
        /// fait environ les deux tiers du chemin vers la vitesse voulue (même principe que
        /// Lisseur.cs, à un seul étage). Plus grand = démarrages et arrêts plus doux mais plus
        /// lents ; plus petit = plus vif.
        /// </summary>
        private const double SpeedTimeConstantMs = 150;

        /// <summary>Sous cette vitesse (crans par seconde), on la considère comme nulle.</summary>
        private const double NegligibleSpeed = 0.05;

        /// <summary>Un cran de molette, en unités Windows (WHEEL_DELTA).</summary>
        private const int Notch = 120;

        /// <summary>Demi-ouverture de l'arc qui indique la direction, en degrés (arc de 50°).</summary>
        private const double ArcHalfAngle = 25;

        /// <summary>
        /// Distance, comptée en rayons de cercle à partir du bord, où la vitesse atteint
        /// [VitesseMaxDefilement]. D'abord 1, triplée à la demande de l'utilisateur (26/10/2026)
        /// pour une montée en vitesse plus progressive.
        /// </summary>
        private const double MaxSpeedRadii = 3;

        /// <summary>
        /// Jusqu'où, en pixels de l'écran 4K au-delà des bords de l'écran, le regard compte
        /// encore pour le défilement. Plus loin, rien ne défile. Étape 11 : convertie pour
        /// l'écran actuel avec Ecran.PxRegard (200 px en 4K → 100 px en 1920 × 1080).
        /// </summary>
        private const double OffScreenMargin4K = 200;

        /// <summary>
        /// Rayon de la zone morte (et du cercle dessiné), par rapport à [RayonCercle] : la moitié
        /// (demande de l'utilisateur du 28/10/2026). Ne change que ce mode.
        /// </summary>
        private const double DeadZoneFactor = 0.5;

        /// <summary>
        /// Opacité du cercle et de l'arc, par rapport à leur opacité habituelle : 0.5 = deux fois
        /// plus transparents (demande du 28/10/2026 : « augmenter la transparence »).
        /// </summary>
        private const double Opacity = 0.5;

        // Le point visé P (le curseur et le centre du cercle), en pixels d'écran.
        private readonly double _px;
        private readonly double _py;

        // Le bouton « retour » (seul bouton de ce mode) et ce qui décide s'il est activé.
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly ButtonAimer _aimer;

        // Le regard lissé, en pixels d'écran ; _place = faux tant qu'il n'a pas de position.
        private readonly Smoother _smoothed = new Smoother();
        private bool _placed;

        // Vrai une fois que le regard est passé dans le cercle (voir les précautions).
        private bool _armed;

        // Quantité de molette pas encore envoyée (en unités Windows). La vitesse donne, à chaque
        // tic, une petite quantité (par exemple 10 crans/s × 120 × 0,016 s = 19 unités) ; on
        // l'ajoute ici, et on envoie dès qu'on a au moins [PasMinimal] unités.
        private double _reserve;
        private bool _horizontalReserve;
        private int _reserveDirection;

        // La vitesse lissée, en crans par seconde (toujours positive ; le sens est _reserveSens).
        private double _speed;

        private double _lastTickMs = -1;

        // L'arc de direction : un trait noir dessous et un trait vert dessus (lisible sur tous les fonds).
        private Path _arcBackground;
        private Path _arc;

        public ScrollMode(double x, double y)
        {
            _px = x;
            _py = y;
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get { return "Défilement"; }
        }

        public override void Enter(GazeState gaze)
        {
            _aimer.Reset();

            OverlayWindow layer = Manager.Layer;
            layer.HideAll();
            if (!layer.Ready)
            {
                Manager.ChangeMode(new MovementMode());
                return;
            }

            // Le curseur est remis sur P par sécurité : c'est la fenêtre sous lui qui défilera.
            NativeMethods.SetCursorPos((int)Math.Round(_px), (int)Math.Round(_py));

            // Cette fenêtre devient la fenêtre active (voir le commentaire de la classe).
            ActivateWindowUnderP();

            _placed = Usable(gaze);
            _smoothed.Place(gaze.X, gaze.Y);
            _armed = false;
            _reserve = 0;
            _speed = 0;
            _lastTickMs = gaze.NowMs;

            BuildSurface();
            layer.ShowCircle(_px, _py, AppSettings.Current.CircleRadiusPx * DeadZoneFactor, true, Opacity);

            App.Log("Défilement : début en (" + _px.ToString("F0") + ", " + _py.ToString("F0") +
                    "), modificateurs : " + Modifiers.Description());
        }

        public override void Update(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // Temps écoulé depuis le tic précédent, en secondes (borné : après un blocage de
            // Windows, on ne veut pas envoyer d'un coup une seconde entière de défilement).
            double now = gaze.NowMs;
            double dt = _lastTickMs < 0 ? 0 : (now - _lastTickMs) / 1000.0;
            dt = Math.Max(0, Math.Min(dt, 0.1));
            _lastTickMs = now;

            // 1. Le bouton « retour » vient-il d'être activé ?
            int active = _aimer.Update(gaze, p.ActivationDelay, p.ShowLabels);
            if (active >= 0)
            {
                App.Log("Défilement : fin (retour)");
                Manager.ChangeMode(new MovementMode());
                return;
            }

            // 2. Pas de regard utilisable (perdu, ou trop loin hors de l'écran), ou regard sur
            //    le bouton : rien ne défile. Un regard un peu hors de l'écran est utilisable.
            bool onBack = gaze.OnScreen && _buttons.Count > 0 && _buttons[0].Contains(gaze.X, gaze.Y);
            //    L'arc reste affiché à sa dernière place.
            //    Arrêt immédiat (pas de ralentissement en douceur) : c'est une sécurité.
            if (!Usable(gaze) || onBack)
            {
                _reserve = 0;
                _speed = 0;
                return;
            }

            // 3. Le regard lissé se rapproche du regard, en douceur (voir Lisseur.cs).
            if (!_placed)
            {
                _smoothed.Place(gaze.X, gaze.Y);
                _placed = true;
            }
            _smoothed.Follow(gaze.X, gaze.Y, gaze.NowMs);

            // 4. Où est le regard par rapport au cercle ?
            double dx = _smoothed.X - _px;
            double dy = _smoothed.Y - _py;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double radius = p.CircleRadiusPx * DeadZoneFactor;

            // L'arc est dessiné dans tous les cas, même dans la zone morte. Regard exactement
            // au centre : pas de direction, l'arc reste où il était.
            if (distance >= 1)
            {
                DrawArc(dx, dy, radius);
            }

            if (distance <= radius)
            {
                // Dans le cercle (la zone morte) : la vitesse voulue est nulle. Le défilement est
                // désormais permis (voir les précautions).
                _armed = true;
            }

            if (!_armed)
            {
                return;
            }

            // 5. La vitesse voulue et, hors de la zone morte, la direction (un seul axe : celui
            //    du plus grand écart).
            //    Vitesse : de 0 au bord de la zone morte à [VitesseMaxDefilement] trois
            //    [RayonCercle] entiers plus loin (voir le commentaire de la classe).
            //    Dans la zone morte, on garde la direction précédente : la vitesse y redescend
            //    en douceur vers 0, dans le même sens.
            double wantedSpeed = 0;
            if (distance > radius)
            {
                bool horizontal = Math.Abs(dx) > Math.Abs(dy);
                // Molette : vers le haut et vers la droite = positif (voir MouseInjector.Molette).
                int direction = horizontal ? (dx > 0 ? 1 : -1) : (dy < 0 ? 1 : -1);
                double fraction = Math.Min(1.0, (distance - radius) / (MaxSpeedRadii * p.CircleRadiusPx));
                wantedSpeed = fraction * p.MaxScrollSpeed;

                // Changement de direction : on repart de zéro (vitesse et réserve), pour ne
                // pas emporter dans le nouveau sens l'élan de l'ancien.
                if (horizontal != _horizontalReserve || direction != _reserveDirection)
                {
                    _reserve = 0;
                    _speed = 0;
                    _horizontalReserve = horizontal;
                    _reserveDirection = direction;
                }
            }

            // 6. La vitesse lissée se rapproche de la vitesse voulue, selon le temps écoulé
            //    (même calcul que Lisseur.cs : la fraction parcourue est 1 − e^(−dt / τ)).
            _speed += (wantedSpeed - _speed) * (1 - Math.Exp(-dt * 1000 / SpeedTimeConstantMs));
            if (wantedSpeed == 0 && _speed < NegligibleSpeed)
            {
                _speed = 0;
                _reserve = 0;
                return;
            }

            // 7. La réserve de molette : on y ajoute la quantité de ce tic, et on envoie tout ce
            //    qu'elle contient (en unités entières) dès qu'elle atteint [PasMinimal].
            _reserve += _speed * Notch * dt;
            if (_reserve >= MinStep)
            {
                int amount = (int)_reserve;
                _reserve -= amount;
                Send(_reserveDirection * amount, _horizontalReserve);
            }
        }

        public override void Quit()
        {
            // La surface est cachée et vidée. Le cercle reste affiché : le mode mouvement le
            // reprend là où il est (sur P) et le fait glisser vers le regard, sans saut.
            Manager.Layer.HideDragSurface();
            Manager.Layer.DragSurface.Children.Clear();
            _buttons.Clear();
            _arc = null;
            _arcBackground = null;
        }

        /// <summary>
        /// Vrai si la mesure du regard peut servir au défilement : disponible, et sur l'écran
        /// ou à moins de [MargeHorsEcran4K] pixels de ses bords.
        /// </summary>
        private bool Usable(GazeState gaze)
        {
            if (!gaze.Available)
            {
                return false;
            }
            double margin = ScreenMetrics.GazePx(OffScreenMargin4K);
            return gaze.X >= -margin && gaze.X < Manager.ScreenWidth + margin &&
                   gaze.Y >= -margin && gaze.Y < Manager.ScreenHeight + margin;
        }

        /// <summary>
        /// Rend active la fenêtre qui est sous P (sans cliquer). Si elle l'est déjà, ou si c'est
        /// une fenêtre de GazeBar, on ne fait rien. Le résultat est noté dans le journal.
        ///
        /// Windows n'autorise pas n'importe quel programme à changer la fenêtre active (pour
        /// éviter qu'une fenêtre « vole » le clavier pendant qu'on tape). GazeBar n'est pas au
        /// premier plan (son calque ne prend jamais le focus) : on « attache » donc un instant
        /// GazeBar à la fenêtre active actuelle (AttachThreadInput), ce qui lui donne ce droit.
        /// </summary>
        private void ActivateWindowUnderP()
        {
            try
            {
                // À quelle fenêtre va la molette ? (réglage Windows, voir MoletteWindows)
                App.Log("Défilement : réglage Windows « défiler les fenêtres inactives » = " + WheelScroller.Description());

                NativeMethods.POINT point;
                point.X = (int)Math.Round(_px);
                point.Y = (int)Math.Round(_py);
                IntPtr underP = NativeMethods.WindowFromPoint(point);
                IntPtr window = underP == IntPtr.Zero ? IntPtr.Zero : NativeMethods.GetAncestor(underP, NativeMethods.GA_ROOT);
                if (window == IntPtr.Zero)
                {
                    App.Log("Défilement : aucune fenêtre sous le point visé");
                    return;
                }

                // Une fenêtre de GazeBar lui-même (par exemple le clavier) : on n'y touche pas.
                uint process;
                NativeMethods.GetWindowThreadProcessId(window, out process);
                if (process == (uint)Process.GetCurrentProcess().Id)
                {
                    return;
                }

                IntPtr active = NativeMethods.GetForegroundWindow();
                if (active == window)
                {
                    return;   // déjà active : rien à faire
                }

                uint activeThread = active == IntPtr.Zero ? 0 : NativeMethods.GetWindowThreadProcessId(active, out process);
                uint gazeBarThread = NativeMethods.GetCurrentThreadId();
                bool attached = activeThread != 0 && activeThread != gazeBarThread &&
                               NativeMethods.AttachThreadInput(gazeBarThread, activeThread, true);
                bool succeeded = NativeMethods.SetForegroundWindow(window);
                if (attached)
                {
                    NativeMethods.AttachThreadInput(gazeBarThread, activeThread, false);
                }

                System.Text.StringBuilder title = new System.Text.StringBuilder(128);
                NativeMethods.GetWindowText(window, title, title.Capacity);
                App.Log("Défilement : fenêtre « " + title + " » rendue active : " + (succeeded ? "oui" : "non (refusé par Windows)"));
            }
            catch (Exception ex)
            {
                // Un échec ici n'empêche pas de défiler : on le note seulement.
                App.Log("Défilement : impossible d'activer la fenêtre sous le point visé : " + ex.Message);
            }
        }

        /// <summary>Envoie la molette, avec les modificateurs actifs enfoncés autour.</summary>
        private static void Send(int amount, bool horizontal)
        {
            bool modifiers = Modifiers.AnyActive;
            if (modifiers)
            {
                Modifiers.PressActive();
            }

            MouseInjector.Wheel(amount, horizontal);

            if (modifiers)
            {
                Modifiers.ReleaseAll();
            }
        }

        // ───────────────────────── Dessin ─────────────────────────

        /// <summary>
        /// Remplit la surface : l'arc de direction (caché pour l'instant) et le bouton « retour ».
        /// La surface du glisser-déposer (SurfaceGlisser) est réutilisée : c'est une surface
        /// transparente par-dessus l'écran réel, exactement ce qu'il faut ici aussi.
        /// </summary>
        private void BuildSurface()
        {
            OverlayWindow layer = Manager.Layer;
            layer.PrepareDragSurface();
            Canvas surface = layer.DragSurface;
            double size = AppSettings.Current.IconSizePx;

            _arcBackground = new Path();
            _arcBackground.Stroke = Brushes.Black;
            _arcBackground.StrokeThickness = ScreenMetrics.Units(10);
            _arcBackground.Opacity = 0.5 * Opacity;
            _arcBackground.StrokeStartLineCap = PenLineCap.Round;
            _arcBackground.StrokeEndLineCap = PenLineCap.Round;
            _arcBackground.Visibility = Visibility.Collapsed;
            _arc = new Path();
            _arc.Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53));   // le vert des anneaux
            _arc.StrokeThickness = ScreenMetrics.Units(6);
            _arc.Opacity = Opacity;
            _arc.StrokeStartLineCap = PenLineCap.Round;
            _arc.StrokeEndLineCap = PenLineCap.Round;
            _arc.Visibility = Visibility.Collapsed;
            surface.Children.Add(_arcBackground);
            surface.Children.Add(_arc);

            // Le bouton « retour », au bord droit de l'écran, centré verticalement.
            // Symbole E72B = flèche « retour » de la police Windows.
            double xBack = Manager.ScreenWidth - size / 2 - ScreenMetrics.Px(10);
            double yBack = Manager.ScreenHeight / 2;
            GazeButton back = new GazeButton("", null, "Quitter le défilement");
            back.Place(layer, surface, xBack, yBack, size);
            _buttons.Add(back);

            layer.ShowDragSurface();
        }

        /// <summary>
        /// Place l'arc sur le cercle, centré sur la direction du regard (dx, dy : écart entre le
        /// regard et le centre, en pixels d'écran).
        ///
        /// Atan2 donne l'angle de cette direction. À l'écran, l'axe Y descend : un angle qui
        /// augmente tourne donc dans le sens des aiguilles d'une montre, d'où « Clockwise ».
        /// </summary>
        private void DrawArc(double dx, double dy, double radiusPixels)
        {
            if (_arc == null)
            {
                return;
            }

            OverlayWindow layer = Manager.Layer;
            Point center = layer.ToWpf(_px, _py);
            double radius = layer.LengthToWpf(radiusPixels);

            double angle = Math.Atan2(dy, dx);
            double half = ArcHalfAngle * Math.PI / 180;
            Point startPoint = new Point(center.X + radius * Math.Cos(angle - half), center.Y + radius * Math.Sin(angle - half));
            Point arrival = new Point(center.X + radius * Math.Cos(angle + half), center.Y + radius * Math.Sin(angle + half));

            PathFigure figure = new PathFigure();
            figure.StartPoint = startPoint;
            figure.IsClosed = false;
            figure.IsFilled = false;
            figure.Segments.Add(new ArcSegment(arrival, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            PathGeometry shape = new PathGeometry();
            shape.Figures.Add(figure);
            shape.Freeze();

            _arcBackground.Data = shape;
            _arc.Data = shape;
            _arcBackground.Visibility = Visibility.Visible;
            _arc.Visibility = Visibility.Visible;
        }
    }
}
