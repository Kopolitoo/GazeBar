using System;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// LOGIQUE : le chef d'orchestre des modes.
    ///
    /// Il connaît le mode courant et le mode précédent, et c'est lui seul qui fait passer
    /// d'un mode à l'autre (méthode ChangerMode). Avoir un seul point de passage permet de
    /// toujours ranger proprement l'ancien mode, de tout noter dans le journal, et plus tard
    /// d'ajouter des animations de transition à un seul endroit.
    ///
    /// Il possède aussi l'horloge commune : à chaque image dessinée par l'écran (en général
    /// 60 fois par seconde), il lit le regard, prépare un EtatRegard et le donne au mode courant
    /// (méthode Actualiser du mode).
    ///
    /// Pourquoi « à chaque image » ? Avant, un minuteur (DispatcherTimer) tournait toutes les
    /// 16 ms. Mais ce minuteur n'est pas précis sous Windows (il arrive souvent avec 15 ou 31 ms),
    /// et il n'est pas synchronisé avec l'écran : certaines images montraient deux déplacements
    /// du cercle, d'autres aucun. Résultat : un mouvement qui « accroche ». L'événement
    /// CompositionTarget.Rendering de WPF est appelé juste avant que WPF dessine chaque image :
    /// le cercle avance exactement une fois par image, régulièrement.
    ///
    /// Les modes y trouvent tout ce dont ils ont besoin : le regard (Regard), le calque plein
    /// écran (Calque) et la taille de l'écran.
    ///
    /// Le premier mode est en général le mode mouvement ; c'est lui qui passe la main au mode
    /// regard fixe quand on fixe un point, et le mode regard fixe la lui rend.
    ///
    /// Étape 4 : c'est aussi lui qui ouvre le menu : quand le regard reste en bas, hors de
    /// l'écran, pendant [DelaiMenu], on passe au mode menu. Depuis l'étape 6, seuls les modes
    /// mouvement, pause et jeu (étape 9) l'autorisent (propriété Mode.MenuAutorise).
    /// </summary>
    internal sealed class ModeManager
    {
        /// <summary>
        /// Intervalle habituel entre deux actualisations, en millisecondes (16 ms ≈ 60 images par
        /// seconde, la cadence de la plupart des écrans). Ce n'est qu'un ordre de grandeur :
        /// l'horloge suit l'écran, et les modes mesurent toujours le temps réel (MaintenantMs).
        /// </summary>
        public const int IntervalMs = 16;

        // Vrai entre Demarrer et Arreter (l'horloge est branchée sur CompositionTarget.Rendering).
        private bool _running;

        // Heure de l'image déjà traitée : WPF appelle parfois Rendering deux fois pour la même
        // image ; on ne la traite qu'une fois.
        private TimeSpan _lastImage = TimeSpan.Zero;

        // Instant (ms) où le regard est passé en bas hors de l'écran ; -1 = il n'y est pas.
        private double _belowStartMs = -1;

        // Pour le journal : où était le regard au tic précédent (voir NoterOuEstLeRegard).
        private string _gazeSituation = "";
        private double _lastX;
        private double _lastY;

        public ModeManager(GazeSource gaze, OverlayWindow layer)
        {
            Gaze = gaze;
            Layer = layer;
            Keyboard = new GazeKeyboard(layer);

            // Étape 11 : l'écran peut changer pendant que GazeBar tourne.
            ScreenMetrics.Change += OnScreenChanged;
        }

        /// <summary>
        /// La résolution, la mise à l'échelle ou la taille du texte de Windows a changé
        /// (étape 11). Le mode courant s'adapte s'il le peut (Mode.AdapterEcran) ; sinon on
        /// revient au mode mouvement, comme avec la touche Échap : boutons de la souris et
        /// touches Ctrl, Maj, Alt relâchés par sécurité (un glisser-déposer est interrompu).
        /// </summary>
        private void OnScreenChanged()
        {
            if (CurrentMode == null)
            {
                return;
            }
            // Le clavier au regard (étape 12) se redessine aux nouvelles tailles.
            Keyboard.AdaptToScreen();
            if (CurrentMode.AdaptToScreen())
            {
                App.Log("Écran changé : le mode " + CurrentMode.Name + " continue avec les nouvelles tailles");
                return;
            }
            App.Log("Écran changé : retour au mode mouvement (le mode " + CurrentMode.Name + " était placé pour l'ancien écran)");
            MouseInjector.ReleaseAll();
            KeyboardInjector.ReleaseAll();
            ChangeMode(new MovementMode());
        }

        /// <summary>La source du regard (eyetracker).</summary>
        public GazeSource Gaze { get; private set; }

        /// <summary>La fenêtre transparente plein écran où les modes dessinent.</summary>
        public OverlayWindow Layer { get; private set; }

        /// <summary>
        /// Le clavier au regard (étape 12). Ce n'est pas un mode : il reste affiché pendant les
        /// modes mouvement et regard fixe (voir ClavierRegard).
        /// </summary>
        public GazeKeyboard Keyboard { get; private set; }

        /// <summary>Le mode en cours (null avant Demarrer).</summary>
        public Mode CurrentMode { get; private set; }

        /// <summary>Le mode d'avant, pour pouvoir y revenir (par exemple en fermant le menu).</summary>
        public Mode PreviousMode { get; private set; }

        /// <summary>Déclenché après chaque changement de mode. L'interface peut s'en servir pour s'afficher à jour.</summary>
        public event Action<Mode> ModeChanged;

        /// <summary>
        /// Largeur de l'écran principal, en pixels réels, mesurée par Ecran (étape 11 :
        /// GetSystemMetrics ne voyait pas un changement de résolution fait pendant que GazeBar
        /// tourne, voir NativeMethods.TailleEcranPrincipal).
        /// </summary>
        public double ScreenWidth
        {
            get { return ScreenMetrics.WidthPixels; }
        }

        /// <summary>Hauteur de l'écran principal, en pixels réels, mesurée par Ecran.</summary>
        public double ScreenHeight
        {
            get { return ScreenMetrics.HeightPixels; }
        }

        // ───────────────────────── Commandes ─────────────────────────

        /// <summary>Entre dans le premier mode et lance l'horloge.</summary>
        public void Start(Mode firstMode)
        {
            ChangeMode(firstMode);
            if (!_running)
            {
                CompositionTarget.Rendering += OnImage;
                _running = true;
            }
        }

        /// <summary>Arrête l'horloge et quitte proprement le mode courant (à la fermeture du programme).</summary>
        public void Stop()
        {
            if (_running)
            {
                CompositionTarget.Rendering -= OnImage;
                _running = false;
            }
            if (CurrentMode != null)
            {
                CurrentMode.Quit();
                CurrentMode = null;
            }
        }

        /// <summary>
        /// Passe au mode indiqué : l'ancien mode est rangé (Quitter), puis le nouveau
        /// commence (Entrer). Peut être appelée par un mode lui-même, depuis son Actualiser.
        /// </summary>
        public void ChangeMode(Mode newVal)
        {
            if (newVal == null || newVal == CurrentMode)
            {
                return;
            }

            Mode old = CurrentMode;
            if (old != null)
            {
                old.Quit();
            }

            PreviousMode = old;
            CurrentMode = newVal;
            newVal.Attach(this);

            // Le clavier au regard s'affiche ou se cache selon le nouveau mode (étape 12).
            // AVANT Entrer : la loupe photographie l'écran dès son entrée, sans le clavier.
            Keyboard.OnModeChange(newVal);

            App.Log("Mode : " + (old == null ? "(aucun)" : old.Name) + " → " + newVal.Name);
            // Le nouveau mode reçoit la position actuelle du regard pour s'initialiser.
            newVal.Enter(ReadGaze());

            Action<Mode> subscribers = ModeChanged;
            if (subscribers != null)
            {
                subscribers(newVal);
            }
        }

        /// <summary>Revient au mode d'avant, s'il existe.</summary>
        public void ReturnToPreviousMode()
        {
            if (PreviousMode != null)
            {
                ChangeMode(PreviousMode);
            }
        }

        // ───────────────────────── Horloge ─────────────────────────

        /// <summary>
        /// Appelée par WPF juste avant de dessiner chaque image. On ne traite chaque image
        /// qu'une seule fois (voir _derniereImage), puis on fait un tic.
        /// </summary>
        private void OnImage(object sender, EventArgs e)
        {
            RenderingEventArgs image = e as RenderingEventArgs;
            if (image != null)
            {
                if (image.RenderingTime == _lastImage)
                {
                    return;
                }
                _lastImage = image.RenderingTime;
            }
            OnTick();
        }

        private void OnTick()
        {
            if (CurrentMode == null)
            {
                return;
            }

            // Pas de tic À L'INTÉRIEUR d'un autre tic (bogue du 02/11/2026). Certaines
            // opérations faites pendant un tic, comme l'ouverture de la fenêtre des paramètres
            // (Show, Activate), laissent WPF traiter d'autres messages en attendant : l'image
            // suivante pouvait alors arriver, et actualiser le nouveau mode avant la fin de son
            // Entrer (ModeParametres : viseur encore vide → NullReferenceException).
            if (_inTick)
            {
                return;
            }
            _inTick = true;
            try
            {
                Tick();
            }
            finally
            {
                _inTick = false;
            }
        }

        // Vrai pendant un tic (voir OnTick).
        private bool _inTick;

        private void Tick()
        {

            GazeState state = ReadGaze();

            try
            {
                // Le menu s'ouvre-t-il ? Si oui, le mode courant n'est pas actualisé à ce tic.
                if (WatchMenu(state))
                {
                    return;
                }

                CurrentMode.Update(state);
            }
            catch (Exception ex)
            {
                // Une erreur dans un mode ne doit pas tout bloquer : on la note, on relâche
                // les boutons de la souris et les touches du clavier par sécurité, et
                // l'horloge continue.
                MouseInjector.ReleaseAll();
                KeyboardInjector.ReleaseAll();
                App.Log("ERREUR dans le mode " + CurrentMode.Name + " : " + ex);
            }
        }

        /// <summary>
        /// Ouvre le menu quand le regard reste en bas, hors de l'écran, pendant [DelaiMenu].
        /// Le minuteur repart de zéro dès que le regard est ailleurs, ou perdu : on ne compte
        /// que ce qu'on voit vraiment. Renvoie vrai si le menu vient de s'ouvrir.
        /// </summary>
        private bool WatchMenu(GazeState state)
        {
            NoteGazePosition(state);

            if (!state.Available || !state.BelowScreen)
            {
                _belowStartMs = -1;
                return false;
            }

            if (_belowStartMs < 0)
            {
                _belowStartMs = state.NowMs;
                return false;
            }

            if (state.NowMs - _belowStartMs >= AppSettings.Current.MenuDelay * 1000.0 &&
                CurrentMode.MenuAllowed)
            {
                _belowStartMs = -1;
                // Étape 12 : clavier affiché (donc mode mouvement) → la barre de menu du
                // clavier, et non la barre classique (choix de l'utilisateur du 31/10/2026).
                bool keyboardMenu = Keyboard.IsOpen && CurrentMode is MovementMode;
                ChangeMode(new MenuMode(keyboardMenu));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Diagnostic du menu : note dans le journal chaque fois que le regard change de
        /// situation (sur l'écran, sous l'écran, hors de l'écran ailleurs, perdu), avec sa
        /// dernière position connue. Sert à vérifier que le PCEye 5 mesure bien le regard
        /// SOUS l'écran (condition pour ouvrir le menu), au lieu de le perdre.
        /// </summary>
        private void NoteGazePosition(GazeState state)
        {
            string situation;
            if (!state.Available)
            {
                situation = "perdu";
            }
            else if (state.BelowScreen)
            {
                situation = "sous l'écran";
            }
            else if (state.OffScreen)
            {
                situation = "hors de l'écran (pas en bas)";
            }
            else
            {
                situation = "sur l'écran";
            }

            if (state.Available)
            {
                _lastX = state.X;
                _lastY = state.Y;
            }

            if (situation != _gazeSituation)
            {
                _gazeSituation = situation;
                App.Log("Regard : " + situation + " (dernière position X " + _lastX.ToString("F0") +
                        ", Y " + _lastY.ToString("F0") + " ; écran " + ScreenWidth + " × " + ScreenHeight + ")");
            }
        }

        /// <summary>
        /// Largeur de la bande, autour de l'écran, où un regard « juste à côté » compte encore
        /// pour la fixation (voir EtatRegard.PresDeLEcran). En pixels de l'écran 4K (2160 px de
        /// haut) : 120 px sur l'écran 4K, soit 60 px sur un écran de 1080 px.
        /// Assez large pour les mesures qui débordent dans les coins, assez étroite pour qu'un
        /// regard franchement hors de l'écran (pour ouvrir le menu, par exemple) ne déclenche
        /// pas de regard fixe.
        /// </summary>
        private const double EdgesMargin4K = 120;

        /// <summary>Prépare la « photo » du regard pour le mode courant.</summary>
        private GazeState ReadGaze()
        {
            GazeState state = new GazeState();
            state.NowMs = Gaze.NowMs;

            if (!Gaze.IsRunning || !Gaze.HasData ||
                Gaze.MsSinceLastSample > AppSettings.Current.MaxMeasureAge)
            {
                state.Available = false;
                return state;
            }

            double x;
            double y;
            long num;
            double instantMs;
            Gaze.GetLatest(out x, out y, out num, out instantMs);

            double width = ScreenWidth;
            double height = ScreenHeight;

            state.Available = true;
            state.X = x;
            state.Y = y;
            state.Number = num;
            state.MeasureTimeMs = instantMs;
            state.OffScreen = GazeSource.IsOffScreen(x, y, width, height);
            state.BelowScreen = y >= height;

            // La même position, mais ramenée dans l'écran (de 0 à largeur − 1, de 0 à hauteur − 1).
            // Math.Max / Math.Min « bloquent » la valeur entre deux bornes.
            state.XScreen = Math.Max(0, Math.Min(width - 1, x));
            state.YScreen = Math.Max(0, Math.Min(height - 1, y));

            // Tout près de l'écran ? (Sur l'écran, ou hors de l'écran de moins de la marge.)
            double margin = ScreenMetrics.GazePx(EdgesMargin4K);
            state.NearScreen = x >= -margin && y >= -margin &&
                                x < width + margin && y < height + margin;
            return state;
        }
    }
}
