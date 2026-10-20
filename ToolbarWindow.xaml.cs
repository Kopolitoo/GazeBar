using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace GazeBar
{
    /// <summary>
    /// INTERFACE : la barre d'outils toujours visible, en haut de l'écran.
    ///
    /// Elle ne contient presque pas de logique : elle relaie les clics vers la classe
    /// HoldSession (qui sait faire un appui long ou un glisser) et affiche l'état.
    /// Séparer l'interface de la logique permet de modifier l'une sans toucher l'autre.
    ///
    /// Elle est « non activante » : cliquer dessus ne lui donne pas le focus, la fenêtre
    /// que vous étiez en train d'utiliser reste donc active.
    ///
    /// « internal » : la classe n'est utilisée que dans ce projet (nécessaire car elle
    /// reçoit un CursorSampler, lui-même interne).
    /// </summary>
    internal partial class ToolbarWindow : Window
    {
        private readonly CursorSampler _sampler;
        private readonly GazeSource _gaze;
        private readonly GazeMouse _gazeMouse;
        private readonly double _screenWidth;
        private readonly double _screenHeight;
        private readonly HoldSession _hold;
        private readonly DispatcherTimer _refreshTimer;
        private DiagnosticWindow _diagnosticWindow;

        public ToolbarWindow(CursorSampler sampler, GazeSource gaze)
        {
            // Construit les éléments décrits dans ToolbarWindow.xaml. Obligatoire en premier.
            InitializeComponent();

            _sampler = sampler;
            _gaze = gaze;
            _gazeMouse = new GazeMouse(gaze);
            _screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            _screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

            // Quand HoldSession change de message d'état, on l'affiche.
            _hold = new HoldSession();
            _hold.StatusChanged += OnHoldStatusChanged;

            // Rafraîchit l'affichage de la position 5 fois par seconde.
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(200);
            _refreshTimer.Tick += OnRefreshTick;
            _refreshTimer.Start();

            // Une fois la fenêtre mesurée, on la centre en haut de l'écran.
            Loaded += OnLoaded;
        }

        // ───────────────────────── Fenêtre qui ne prend pas le focus ─────────────────────────

        /// <summary>
        /// Appelée quand Windows a créé la fenêtre « réelle » (son handle, appelé HWND).
        /// C'est le moment de lui donner les styles qui l'empêchent de prendre le focus.
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr handle = new WindowInteropHelper(this).Handle;

            // On ajoute deux styles à ceux existants (le « | » combine les options) :
            //  - NOACTIVATE : la fenêtre n'est jamais activée ;
            //  - TOOLWINDOW : elle n'apparaît pas dans Alt+Tab.
            int style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(
                handle,
                NativeMethods.GWL_EXSTYLE,
                style | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);

            // En plus, on répond « ne pas activer » au message que Windows envoie quand
            // un clic va activer la fenêtre.
            HwndSource source = HwndSource.FromHwnd(handle);
            source.AddHook(WindowMessageHook);
        }

        /// <summary>
        /// Reçoit les messages bruts envoyés par Windows à la fenêtre.
        /// </summary>
        private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == NativeMethods.WM_MOUSEACTIVATE)
            {
                handled = true;   // « ce message est traité »
                return new IntPtr(NativeMethods.MA_NOACTIVATE);
            }
            return IntPtr.Zero;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // La largeur de l'écran (en unités WPF) moins celle de la fenêtre, divisée par deux.
            Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
            Top = 0;
        }

        // ───────────────────────── Affichage ─────────────────────────

        private void OnRefreshTick(object sender, EventArgs e)
        {
            int x;
            int y;
            _sampler.GetLatest(out x, out y);
            CursorStats stats = _sampler.Stats;

            CursorText.Text = "Curseur : X " + x + "   Y " + y +
                              "   |   " + stats.ChangesPerSecond.ToString("F0") + " changements/s";

            // Le regard réel (moteur Tobii).
            if (!_gaze.IsRunning)
            {
                GazeText.Text = "Regard : indisponible (" + _gaze.ErrorMessage + ")";
            }
            else if (!_gaze.HasData)
            {
                GazeText.Text = "Regard : en attente de mesures…";
            }
            else
            {
                double gx;
                double gy;
                _gaze.GetLatest(out gx, out gy);
                bool outside = GazeSource.IsOffScreen(gx, gy, _screenWidth, _screenHeight);
                GazeText.Text = "Regard : X " + gx.ToString("F0") + "   Y " + gy.ToString("F0") +
                                "   |   " + _gaze.SamplesPerSecond.ToString("F0") + " mesures/s" +
                                (outside ? "   [HORS ÉCRAN]" : "");
            }
        }

        private void OnHoldStatusChanged(string message)
        {
            StatusText.Text = message;
        }

        // ───────────────────────── Clics sur les boutons ─────────────────────────

        private void OnLongPressClick(object sender, RoutedEventArgs e)
        {
            _hold.Start(HoldKind.LongPressLeft);
        }

        private void OnDragLeftClick(object sender, RoutedEventArgs e)
        {
            _hold.Start(HoldKind.DragLeft);
        }

        private void OnDragRightClick(object sender, RoutedEventArgs e)
        {
            _hold.Start(HoldKind.DragRight);
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            _hold.Cancel();
        }

        private void OnMouseModeClick(object sender, RoutedEventArgs e)
        {
            bool on = _gazeMouse.Toggle();
            if (on)
            {
                MouseModeButton.Content = "Souris regard : OUI";
                MouseModeButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x1E, 0x8E, 0x3E));
                StatusText.Text = "Souris regard activée. Regardez hors de l'écran pour la mettre en pause.";
            }
            else
            {
                MouseModeButton.Content = "Souris regard : NON";
                MouseModeButton.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
                StatusText.Text = "Souris regard désactivée.";
            }
        }

        private void OnDiagnosticClick(object sender, RoutedEventArgs e)
        {
            // Une seule fenêtre de diagnostic à la fois.
            if (_diagnosticWindow == null)
            {
                _diagnosticWindow = new DiagnosticWindow(_sampler, _gaze);
                _diagnosticWindow.Closed += (s, args) => { _diagnosticWindow = null; };
            }
            _diagnosticWindow.Show();
        }

        private void OnQuitClick(object sender, RoutedEventArgs e)
        {
            _hold.Cancel();                 // relâche le bouton si besoin
            _gazeMouse.Disable();
            Application.Current.Shutdown(); // ferme tout le programme
        }
    }
}
