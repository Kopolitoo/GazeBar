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
    /// MODE CHOIX D'ACTION (étape 3 du cahier des charges).
    ///
    /// On y arrive depuis le mode regard fixe, en regardant l'icône d'options. Le curseur est
    /// déjà sur le point fixé P (placé à l'entrée en regard fixe).
    ///
    /// Le mode se déroule en trois temps (« étapes ») :
    ///
    ///  1. CAPTURE. Le cercle et l'icône du calque sont cachés, on attend [AttenteMs] pour que
    ///     Windows ait fini de les effacer, puis on photographie le carré qui englobe le cercle
    ///     (côté 2 × [RayonCercle], soit 200 × 200 px). Sans cette attente, la photo
    ///     contiendrait le cercle.
    ///
    ///  2. CHOIX. Un écran noir couvre tout l'écran. Au centre : la photo agrandie ×2
    ///     (400 × 400 px) et une croix à la place du curseur. Autour : la couronne de huit
    ///     emplacements d'action. À gauche : les trois emplacements des modificateurs.
    ///     À droite : l'icône « retour ». Un emplacement sans action est quand même dessiné,
    ///     grisé, pour montrer qu'on pourra y mettre une action (demande de l'utilisateur
    ///     du 22/10/2026). Par-dessus tout, un petit cercle (moitié de [RayonCercle]) montre
    ///     où est le regard.
    ///     Regarder une icône pendant [DelaiActivation] l'active (son anneau se remplit).
    ///
    ///  3. EXÉCUTION. L'écran noir est caché, on attend [AttenteMs] qu'il ait disparu, puis le
    ///     clic est injecté au curseur. Ainsi, l'application visée est bien visible (et à jour)
    ///     au moment du clic. On revient ensuite au mode mouvement.
    ///
    /// L'icône « retour » ramène au mode mouvement sans rien faire.
    ///
    /// L'action « Loupe » (étape 5, emplacement 4 de la couronne) ouvre le mode loupe sur le
    /// même point P, pour affiner la position du curseur. La loupe revient ensuite ici, sur le
    /// point affiné (ou sur P si l'on y choisit « retour »).
    ///
    /// Étape 6 :
    ///   - les trois actions « glisser-déposer » (gauche, droit, android) ouvrent le choix du
    ///     point d'arrivée (ModeGlisserArrivee), avec P comme point de départ ;
    ///   - les trois emplacements de gauche sont les modificateurs Ctrl, Maj et Alt. Un regard
    ///     en active un (bord vert), un second le désactive ; le choix d'action reste ouvert.
    ///     Les touches actives sont enfoncées juste avant l'action et relâchées juste après
    ///     (voir Modificateurs.cs).
    ///
    /// Actions spéciales (demande de l'utilisateur du 28/10/2026) : Copier, Couper, Coller,
    /// trois boutons collés en haut de l'écran, hors de la couronne, comme les modificateurs
    /// le sont au bord gauche (voir DispositionChoixAction). Ils
    /// envoient Ctrl + C, Ctrl + X et Ctrl + V à la fenêtre active ; « Coller » fait d'abord
    /// un clic gauche sur P, pour placer le point d'insertion là où l'on regardait.
    ///
    /// Étape 12 : l'action « Clavier » (de la couronne, à placer soi-même dans l'écran de
    /// configuration des icônes, demande du 01/11/2026) ouvre le clavier au regard, ou le
    /// ferme s'il est déjà ouvert.
    ///
    /// Étape 8 : l'action « Défilement » (emplacement 0 depuis le 28/10/2026) ouvre le mode défilement, centré sur P.
    ///
    /// Étape 10 : la disposition des icônes (couronne, modificateurs, boutons utilitaires)
    /// n'est plus écrite ici ; elle est lue dans les paramètres (Parametres.Disposition) et
    /// se règle dans l'écran de configuration des icônes (ModeConfigIcones), ouvert depuis
    /// l'onglet « Icônes » du mode paramètres. Un emplacement peut être vide : il est dessiné
    /// grisé et ne s'active pas. La mise en page elle-même est calculée par
    /// DispositionChoixAction, partagée avec l'écran de configuration.
    /// </summary>
    internal sealed class ActionChoiceMode : Mode
    {
        /// <summary>Les trois temps du mode (voir le commentaire de la classe).</summary>
        private enum Step
        {
            Capture,
            Choice,
            Execution
        }

        /// <summary>
        /// Attente avant la photo, et avant le clic, en millisecondes : le temps que Windows
        /// efface réellement ce qu'on vient de cacher (le cahier des charges dit « environ 50 ms »).
        /// </summary>
        private const double WaitMs = 60;

        /// <summary>
        /// Zone d'activation de TOUS les boutons de ce mode (couronne, modificateurs, retour),
        /// par rapport au carré dessiné : 30 % plus grande (demande de l'utilisateur du
        /// 28/10/2026). Avec des boutons de 120 px séparés de 60 px (DispositionChoixAction.Espace), chaque zone
        /// déborde de 18 px de chaque côté : il reste 24 px entre deux zones voisines.
        /// </summary>
        public const double ZoneFactor = 1.3;

        // Le point visé P (position du curseur), en pixels d'écran.
        private readonly double _px;
        private readonly double _py;

        private Step _step;
        private double _stepStartMs;

        /// <summary>Libellé d'un emplacement vide, affiché au survol.</summary>
        private const string EmptyLabel = "Emplacement libre (à configurer dans les paramètres)";

        // Les boutons affichés, et l'action de chacun (même ordre ; null = emplacement vide).
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly List<ChoiceAction?> _actions = new List<ChoiceAction?>();

        // Le petit cercle qui montre le regard : deux ellipses (trait noir dessous, vert dessus),
        // ajoutées en dernier sur l'écran noir pour être par-dessus tout. Position lissée en
        // pixels d'écran ; _cercleRegardPlace = faux tant qu'il n'a pas de position.
        private Ellipse _gazeCircleBackground;
        private Ellipse _gazeCircle;
        private readonly Smoother _smoothed = new Smoother();
        private bool _gazeCirclePlaced;

        // Décide quel bouton est regardé et quand il s'active (classe partagée avec le menu).
        private readonly ButtonAimer _aimer;

        // L'action choisie, en attente d'exécution (étape Execution).
        private ChoiceAction _chosenAction;

        public ActionChoiceMode(double x, double y)
        {
            _px = x;
            _py = y;
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get { return "Choix d'action"; }
        }

        public override void Enter(GazeState gaze)
        {
            _aimer.Reset();

            // 1. CAPTURE : on cache tout ce que GazeBar dessine, et on attend un peu avant la photo.
            Manager.Layer.HideAll();
            _step = Step.Capture;
            _stepStartMs = gaze.NowMs;
        }

        public override void Update(GazeState gaze)
        {
            double now = gaze.NowMs;

            switch (_step)
            {
                case Step.Capture:
                    if (now - _stepStartMs >= WaitMs)
                    {
                        bool succeeded;
                        try
                        {
                            succeeded = BuildScreen();
                        }
                        catch (Exception ex)
                        {
                            App.Log("Choix d'action : erreur pendant la construction de l'écran : " + ex);
                            succeeded = false;
                        }

                        if (!succeeded)
                        {
                            // Photo impossible : on ne reste pas bloqué devant un écran noir.
                            Manager.ChangeMode(new MovementMode());
                        }
                    }
                    break;

                case Step.Choice:
                    UpdateChoice(gaze);
                    break;

                case Step.Execution:
                    if (now - _stepStartMs >= WaitMs)
                    {
                        Execute();
                    }
                    break;
            }
        }

        public override void Quit()
        {
            // L'écran noir est caché et vidé (la photo n'est plus utile).
            Manager.Layer.HideChoiceScreen();
            Manager.Layer.ChoiceScreen.Children.Clear();
            _buttons.Clear();
            _actions.Clear();
            _gazeCircle = null;
            _gazeCircleBackground = null;
        }

        // ───────────────────────── 2. Le choix ─────────────────────────

        private void UpdateChoice(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // Quel bouton est regardé ? L'un d'eux vient-il de s'activer ?
            // (Un emplacement vide ne s'active jamais : voir ViseurBoutons.)
            int active = _aimer.Update(gaze, p.ActivationDelay, p.ShowLabels);
            if (active >= 0)
            {
                Activate(_actions[active].Value);
                return;
            }

            // Le petit cercle du regard (seulement quand le regard est sur l'écran).
            MoveGazeCircle(gaze, p);
        }

        /// <summary>Un bouton vient d'être activé.</summary>
        private void Activate(ChoiceAction action)
        {
            App.Log("Choix d'action : " + action);

            if (action == ChoiceAction.Back)
            {
                // Retour sans rien faire. Le mode mouvement repart du regard (le cercle a été
                // caché pendant le choix, il n'y a donc pas de saut visible).
                Manager.ChangeMode(new MovementMode());
                return;
            }

            if (ActionCatalog.IsModifier(action))
            {
                // Ctrl, Maj ou Alt : on l'allume ou on l'éteint, et on reste dans le choix.
                // Le bouton est bloqué tant que le regard ne l'a pas quitté : sans cela, en
                // continuant à le regarder, on l'allumerait et l'éteindrait sans fin.
                bool active = Modifiers.Toggle(ActionCatalog.ToModifier(action));
                // Le même modificateur peut être placé sur plusieurs emplacements (étape 10) :
                // toutes ses icônes s'allument ou s'éteignent ensemble.
                for (int i = 0; i < _actions.Count; i++)
                {
                    if (_actions[i] == action)
                    {
                        _buttons[i].SetLit(active);
                    }
                }
                _aimer.BlockUntilExit();
                return;
            }

            if (action == ChoiceAction.DragLeft || action == ChoiceAction.DragRight ||
                action == ChoiceAction.DragAndroid)
            {
                // Le glisser-déposer part du point visé P. L'écran noir sera caché par Quitter,
                // juste avant que le mode suivant ne montre l'écran réel.
                MouseSide button = action == ChoiceAction.DragRight ? MouseSide.Right : MouseSide.Left;
                bool withPause = action == ChoiceAction.DragAndroid;
                Manager.ChangeMode(new DragTargetMode(new DragGesture(_px, _py, button, withPause)));
                return;
            }

            if (action == ChoiceAction.Scroll)
            {
                // Le défilement se fait autour de P, sur l'écran réel (l'écran noir sera caché
                // par Quitter, juste avant).
                Manager.ChangeMode(new ScrollMode(_px, _py));
                return;
            }

            if (action == ChoiceAction.Keyboard)
            {
                // Le clavier au regard (étape 12, demande de l'utilisateur du 01/11/2026) :
                // fermé, il s'ouvre ; déjà ouvert (caché pendant ce choix), il se ferme. Il
                // apparaît au retour en mode mouvement (ClavierRegard.SurChangementMode).
                // Fichier de disposition absent ou incorrect : rien ne s'ouvre, l'erreur est
                // notée dans le journal (et affichée si l'on passe par la barre de menu).
                if (Manager.Keyboard.IsOpen)
                {
                    Manager.Keyboard.Close();
                }
                else
                {
                    string message;
                    Manager.Keyboard.Open(out message);
                }
                Manager.ChangeMode(new MovementMode());
                return;
            }

            if (action == ChoiceAction.Magnifier)
            {
                // La loupe photographie elle-même la zone autour de P (l'écran noir sera caché
                // par Quitter, juste avant). « true » : son icône « retour » ramènera ici.
                Manager.ChangeMode(new MagnifierMode(_px, _py, true));
                return;
            }

            // 3. EXÉCUTION : on cache l'écran noir, et on attend qu'il ait disparu avant le clic.
            _chosenAction = action;
            Manager.Layer.HideChoiceScreen();
            _step = Step.Execution;
            _stepStartMs = Manager.Gaze.NowMs;
        }

        // ───────────────────────── 3. L'exécution ─────────────────────────

        private void Execute()
        {
            // Le curseur est remis sur P par sécurité (il a pu être bougé entre-temps).
            NativeMethods.SetCursorPos((int)Math.Round(_px), (int)Math.Round(_py));

            // Les modificateurs actifs sont enfoncés juste avant le clic…
            string modifiers = Modifiers.Description();
            Modifiers.PressActive();

            switch (_chosenAction)
            {
                case ChoiceAction.LeftClick:
                    MouseInjector.Click(MouseSide.Left);
                    break;
                case ChoiceAction.RightClick:
                    MouseInjector.Click(MouseSide.Right);
                    break;
                case ChoiceAction.DoubleClick:
                    MouseInjector.DoubleClick();
                    break;
                case ChoiceAction.Copy:
                    CtrlShortcut(NativeMethods.VK_C);
                    break;
                case ChoiceAction.Cut:
                    CtrlShortcut(NativeMethods.VK_X);
                    break;
                case ChoiceAction.Paste:
                    // Clic d'abord : le point d'insertion se place là où l'on regardait.
                    MouseInjector.Click(MouseSide.Left);
                    CtrlShortcut(NativeMethods.VK_V);
                    break;
            }

            // … et relâchés juste après. (Le mode mouvement les désactivera ensuite.)
            Modifiers.ReleaseAll();

            App.Log(_chosenAction + " en (" + _px.ToString("F0") + ", " + _py.ToString("F0") +
                    "), modificateurs : " + modifiers);

            // Retour au mode mouvement (choix de l'utilisateur). Une nouvelle fixation au même
            // endroit est ignorée tant que le regard n'en est pas sorti, comme après un clic direct.
            Manager.ChangeMode(new MovementMode(_px, _py));
        }

        /// <summary>
        /// Envoie Ctrl + touche (copier, couper, coller) à la fenêtre active. Pas de clic pour
        /// copier et couper : un clic effacerait la sélection. Si Ctrl était déjà enfoncé par
        /// les modificateurs, Enfoncer ne fait rien ; Maj ou Alt actifs restent ajoutés (par
        /// exemple Ctrl + Maj + V colle sans mise en forme dans beaucoup de logiciels).
        /// </summary>
        private static void CtrlShortcut(ushort key)
        {
            KeyboardInjector.PressDown(NativeMethods.VK_CONTROL);
            KeyboardInjector.PressDown(key);
            KeyboardInjector.Release(key);
            KeyboardInjector.Release(NativeMethods.VK_CONTROL);
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

            // ── La photo : le carré qui englobe le cercle, centré sur P. Près d'un bord de
            //    l'écran, le carré est décalé pour rester entièrement sur l'écran (P n'est alors
            //    plus au centre de la photo, et la croix non plus).
            int side = (int)Math.Round(2 * p.CircleRadiusPx);
            side = Math.Min(side, (int)Math.Min(width, height));
            int ox = (int)Clamp(Math.Round(_px - side / 2.0), 0, width - side);
            int oy = (int)Clamp(Math.Round(_py - side / 2.0), 0, height - side);

            BitmapSource photo = ScreenCapture.Capture(ox, oy, side, side);
            if (photo == null)
            {
                return false;
            }

            layer.PrepareChoiceScreen();
            Canvas screen = layer.ChoiceScreen;

            // ── La mise en page (la même que celle de l'écran de configuration des icônes).
            ActionChoiceLayout placed = new ActionChoiceLayout(width, height, size, side);
            double zoom = ActionChoiceLayout.Zoom;

            // ── La photo agrandie, au centre de l'écran.
            double enlargedSide = placed.EnlargedPhotoSide;
            double left = placed.CenterX - enlargedSide / 2;
            double top = placed.CenterY - enlargedSide / 2;

            Image image = new Image();
            image.Source = photo;
            image.Stretch = Stretch.Fill;
            // « Plus proche voisin » : chaque pixel devient un carré de 2 × 2 bien net, au lieu
            // d'être adouci (flou). Le texte agrandi reste ainsi lisible.
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Point corner = layer.ToWpf(left, top);
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

            // ── La croix, à la place du curseur dans la photo agrandie.
            DrawCross(layer, screen, left + (_px - ox) * zoom, top + (_py - oy) * zoom);

            // ── Les trois groupes d'emplacements (couronne, boutons utilitaires en haut,
            //    modificateurs à gauche), avec la disposition choisie dans les paramètres.
            //    Un emplacement vide est dessiné grisé. Un modificateur déjà actif (par exemple
            //    Ctrl choisi avant un passage par la loupe) est dessiné allumé.
            foreach (IconGroup group in new[] { IconGroup.Crown, IconGroup.Utilities, IconGroup.Modifiers })
            {
                ChoiceAction?[] layout = p.Layout(group);
                Point[] slots = placed.Slots(group);
                for (int i = 0; i < layout.Length; i++)
                {
                    ChoiceAction? action = layout[i];
                    GazeButton button = AddButton(layer, screen, action, slots[i].X, slots[i].Y, size);
                    if (action.HasValue && ActionCatalog.IsModifier(action.Value))
                    {
                        button.SetLit(Modifiers.IsActive(ActionCatalog.ToModifier(action.Value)));
                    }
                }
            }

            // ── L'icône « retour », à droite
            AddButton(layer, screen, ChoiceAction.Back, placed.Back.X, placed.Back.Y, size);

            // ── Le petit cercle du regard, ajouté EN DERNIER : il est ainsi dessiné par-dessus
            //    la photo et les boutons. Il apparaîtra à la première mesure sur l'écran.
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
            _step = Step.Choice;
            App.Log("Choix d'action : photo de " + side + " × " + side + " px en (" + ox + ", " + oy + ")");
            return true;
        }

        /// <summary>
        /// Déplace le petit cercle du regard : il suit le regard en douceur ([Lissage], voir
        /// Lisseur.cs), comme le cercle du mode mouvement. Son rayon est la moitié
        /// de [RayonCercle]. Regard hors de l'écran : il glisse le long du bord. Regard perdu : il reste où il est.
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
            double radius = layer.LengthToWpf(p.CircleRadiusPx / 2);
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

        /// <summary>
        /// Crée le bouton d'une action (ou un emplacement vide si action est null), le place
        /// sur l'écran noir, et le renvoie.
        /// </summary>
        private GazeButton AddButton(OverlayWindow layer, Canvas screen, ChoiceAction? action,
                                           double x, double y, double size)
        {
            GazeButton button = action.HasValue ? ActionCatalog.CreateButton(action.Value) : GazeButton.Empty(EmptyLabel);
            button.ZoneFactor = ZoneFactor;
            button.Place(layer, screen, x, y, size);
            _buttons.Add(button);
            _actions.Add(action);
            return button;
        }

        /// <summary>
        /// Dessine une croix centrée sur (x, y) en pixels d'écran : un trait noir épais dessous,
        /// un trait blanc fin dessus, pour qu'elle se voie sur tous les fonds.
        /// </summary>
        private static void DrawCross(OverlayWindow layer, Canvas screen, double x, double y)
        {
            Point center = layer.ToWpf(x, y);
            // Demi-longueur : 20 px de l'écran 4K ; épaisseurs : unités de l'écran de référence
            // (étape 11, voir Ecran.cs).
            double half = layer.LengthToWpf(ScreenMetrics.Px(20));
            double thick = ScreenMetrics.Units(5);
            double end = ScreenMetrics.Units(2);

            AddLine(screen, center.X - half, center.Y, center.X + half, center.Y, Brushes.Black, thick);
            AddLine(screen, center.X, center.Y - half, center.X, center.Y + half, Brushes.Black, thick);
            AddLine(screen, center.X - half, center.Y, center.X + half, center.Y, Brushes.White, end);
            AddLine(screen, center.X, center.Y - half, center.X, center.Y + half, Brushes.White, end);
        }

        private static void AddLine(Canvas screen, double x1, double y1, double x2, double y2,
                                         Brush color, double thickness)
        {
            Line stroke = new Line();
            stroke.X1 = x1;
            stroke.Y1 = y1;
            stroke.X2 = x2;
            stroke.Y2 = y2;
            stroke.Stroke = color;
            stroke.StrokeThickness = thickness;
            screen.Children.Add(stroke);
        }

        private static double Clamp(double val, double min, double max)
        {
            return Math.Max(min, Math.Min(max, val));
        }
    }
}
