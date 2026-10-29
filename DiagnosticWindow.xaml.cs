using System;
using System.Diagnostics;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GazeBar
{
    /// <summary>
    /// INTERFACE : fenêtre de diagnostic. Affiche en direct ce que « voit » l'échantillonneur
    /// de curseur, et permet d'enregistrer un fichier CSV pour analyser la qualité du signal.
    /// </summary>
    internal partial class DiagnosticWindow : Window
    {
        /// <summary>Délai avant le début de l'enregistrement, pour avoir le temps de se préparer.</summary>
        private const double RecordCountdownSeconds = 3.0;

        /// <summary>Durée de l'enregistrement.</summary>
        private const double RecordSeconds = 20.0;

        /// <summary>Largeur de la miniature de l'écran, en unités WPF.</summary>
        private const double MapWidth = 680.0;

        private enum RecordPhase
        {
            None,
            Countdown,
            Recording
        }

        private readonly CursorSampler _sampler;
        private readonly GazeSource _gaze;
        private readonly CursorRecorder _recorder = new CursorRecorder();
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _recordClock = new Stopwatch();

        private readonly Polyline _trailLine;   // la trace des dernières positions
        private readonly Ellipse _dot;          // le point = position actuelle du curseur
        private readonly Ellipse _gazeDot;      // le point vert = position réelle du regard

        private readonly double _screenWidth;
        private readonly double _screenHeight;
        private readonly double _mapHeight;

        private RecordPhase _recordPhase = RecordPhase.None;

        public DiagnosticWindow(CursorSampler sampler, GazeSource gaze)
        {
            InitializeComponent();

            _sampler = sampler;
            _gaze = gaze;

            // Taille de l'écran en pixels (même unité que la position du curseur).
            NativeMethods.TailleEcranPrincipal(out _screenWidth, out _screenHeight);
            if (_screenWidth <= 0 || _screenHeight <= 0)
            {
                _screenWidth = 1920;
                _screenHeight = 1080;
            }

            // La miniature garde les proportions de l'écran.
            _mapHeight = MapWidth * _screenHeight / _screenWidth;
            MapCanvas.Width = MapWidth;
            MapCanvas.Height = _mapHeight;

            _trailLine = new Polyline();
            _trailLine.Stroke = Brushes.DodgerBlue;
            _trailLine.StrokeThickness = 2;
            _trailLine.Opacity = 0.7;

            _dot = new Ellipse();
            _dot.Width = 14;
            _dot.Height = 14;
            _dot.Fill = Brushes.OrangeRed;

            _gazeDot = new Ellipse();
            _gazeDot.Width = 18;
            _gazeDot.Height = 18;
            _gazeDot.Fill = Brushes.LimeGreen;
            _gazeDot.Opacity = 0.85;

            MapCanvas.Children.Add(_trailLine);
            MapCanvas.Children.Add(_dot);
            MapCanvas.Children.Add(_gazeDot);

            ScreenInfoText.Text = "Écran : " + _screenWidth + " x " + _screenHeight + " pixels";

            // Rafraîchit l'affichage 20 fois par seconde.
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(50);
            _timer.Tick += OnTick;
            _timer.Start();

            Closed += OnWindowClosed;
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            _timer.Stop();
            if (_recordPhase == RecordPhase.Recording)
            {
                _recorder.Cancel(_sampler);
            }
        }

        // ───────────────────────── Affichage en direct ─────────────────────────

        private void OnTick(object sender, EventArgs e)
        {
            UpdateReadouts();
            UpdateMap();
            UpdateRecording();
        }

        private void UpdateReadouts()
        {
            int x;
            int y;
            _sampler.GetLatest(out x, out y);
            CursorStats stats = _sampler.Stats;

            double percentX = 100.0 * x / _screenWidth;
            double percentY = 100.0 * y / _screenHeight;

            PositionText.Text = "X = " + x + "   Y = " + y +
                                "   (" + percentX.ToString("F0") + " %, " + percentY.ToString("F0") + " %)";

            RateText.Text = "Changements de position : " + stats.ChangesPerSecond.ToString("F0") +
                            " par seconde   (lectures : " + stats.PollsPerSecond.ToString("F0") + " par seconde)";

            if (!_gaze.IsRunning)
            {
                GazeReadText.Text = "Regard Tobii indisponible : " + _gaze.ErrorMessage;
            }
            else if (!_gaze.HasData)
            {
                GazeReadText.Text = "Regard Tobii : en attente de mesures…";
            }
            else
            {
                double gx;
                double gy;
                _gaze.GetLatest(out gx, out gy);
                bool outside = GazeSource.IsOffScreen(gx, gy, _screenWidth, _screenHeight);
                GazeReadText.Text = "Regard : X = " + gx.ToString("F0") + "   Y = " + gy.ToString("F0") +
                                    "   (" + _gaze.SamplesPerSecond.ToString("F0") + " mesures/s)" +
                                    (outside ? "   HORS ÉCRAN" : "");
            }

            double idle = _sampler.MsSinceLastChange;
            IdleText.Text = idle < 0
                ? "Aucun mouvement détecté pour l'instant"
                : "Dernier changement il y a " + idle.ToString("F0") + " ms";
        }

        private void UpdateMap()
        {
            double scaleX = MapWidth / _screenWidth;
            double scaleY = _mapHeight / _screenHeight;

            // La trace : on convertit chaque position d'écran en position dans la miniature.
            CursorSample[] trail = _sampler.GetTrail();
            PointCollection points = new PointCollection(trail.Length);
            foreach (CursorSample sample in trail)
            {
                points.Add(new Point(sample.X * scaleX, sample.Y * scaleY));
            }
            _trailLine.Points = points;

            // Le point : on retire la moitié de sa taille pour que son CENTRE soit sur la position.
            int x;
            int y;
            _sampler.GetLatest(out x, out y);
            Canvas.SetLeft(_dot, x * scaleX - _dot.Width / 2);
            Canvas.SetTop(_dot, y * scaleY - _dot.Height / 2);

            // Le regard réel : ramené dans la miniature (un point rouge sur le bord = hors écran).
            if (_gaze.HasData)
            {
                double gx;
                double gy;
                _gaze.GetLatest(out gx, out gy);
                bool outside = GazeSource.IsOffScreen(gx, gy, _screenWidth, _screenHeight);
                double mx = Math.Max(0, Math.Min(MapWidth, gx * scaleX));
                double my = Math.Max(0, Math.Min(_mapHeight, gy * scaleY));
                _gazeDot.Fill = outside ? Brushes.Red : Brushes.LimeGreen;
                Canvas.SetLeft(_gazeDot, mx - _gazeDot.Width / 2);
                Canvas.SetTop(_gazeDot, my - _gazeDot.Height / 2);
            }
        }

        // ───────────────────────── Enregistrement ─────────────────────────

        private void OnRecordClick(object sender, RoutedEventArgs e)
        {
            if (_recordPhase != RecordPhase.None)
            {
                return;
            }

            _recordPhase = RecordPhase.Countdown;
            _recordClock.Restart();
            RecordButton.IsEnabled = false;
            RecordText.Text = "Préparez-vous : début de l'enregistrement dans " +
                              RecordCountdownSeconds.ToString("F0") + " s…";
        }

        /// <summary>Fait avancer l'enregistrement (appelée 20 fois par seconde).</summary>
        private void UpdateRecording()
        {
            if (_recordPhase == RecordPhase.None)
            {
                return;
            }

            double elapsed = _recordClock.Elapsed.TotalSeconds;

            if (_recordPhase == RecordPhase.Countdown)
            {
                if (elapsed < RecordCountdownSeconds)
                {
                    RecordText.Text = "Préparez-vous : début dans " +
                                      Math.Ceiling(RecordCountdownSeconds - elapsed).ToString("F0") + " s…";
                    return;
                }

                SystemSounds.Asterisk.Play();   // petit son : l'enregistrement commence
                _recorder.Start(_sampler);
                _recordPhase = RecordPhase.Recording;
                _recordClock.Restart();
                return;
            }

            // Phase d'enregistrement.
            if (elapsed < RecordSeconds)
            {
                RecordText.Text = "Enregistrement en cours, encore " +
                                  Math.Ceiling(RecordSeconds - elapsed).ToString("F0") +
                                  " s : parcourez tout l'écran du regard, puis fixez le centre.";
                return;
            }

            _recordPhase = RecordPhase.None;
            RecordButton.IsEnabled = true;
            SystemSounds.Exclamation.Play();    // son différent : c'est terminé

            try
            {
                string file = _recorder.StopAndSave(_sampler, _screenWidth, _screenHeight);
                RecordText.Text = "Enregistré dans : " + file;
            }
            catch (Exception ex)
            {
                App.Log("Échec de l'enregistrement : " + ex);
                RecordText.Text = "Échec de l'enregistrement : " + ex.Message;
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
