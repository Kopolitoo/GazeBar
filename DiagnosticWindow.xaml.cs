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
    /// INTERFACE: diagnostic window. Shows live what the cursor sampler "sees", and can
    /// record a CSV file to analyze the signal quality.
    /// </summary>
    internal partial class DiagnosticWindow : Window
    {
        /// <summary>Delay before recording starts, to give time to get ready.</summary>
        private const double RecordCountdownSeconds = 3.0;

        /// <summary>Recording duration.</summary>
        private const double RecordSeconds = 20.0;

        /// <summary>Width of the screen thumbnail, in WPF units.</summary>
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

        private readonly Polyline _trailLine;   // the trail of the latest positions
        private readonly Ellipse _dot;          // the dot = current cursor position
        private readonly Ellipse _gazeDot;      // the green dot = real gaze position

        private readonly double _screenWidth;
        private readonly double _screenHeight;
        private readonly double _mapHeight;

        private RecordPhase _recordPhase = RecordPhase.None;

        public DiagnosticWindow(CursorSampler sampler, GazeSource gaze)
        {
            InitializeComponent();

            _sampler = sampler;
            _gaze = gaze;

            // Screen size in pixels (same unit as the cursor position).
            NativeMethods.PrimaryScreenSize(out _screenWidth, out _screenHeight);
            if (_screenWidth <= 0 || _screenHeight <= 0)
            {
                _screenWidth = 1920;
                _screenHeight = 1080;
            }

            // The thumbnail keeps the screen's proportions.
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

            // Refreshes the display 20 times per second.
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

        // ───────────────────────── Live display ─────────────────────────

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
                GazeReadText.Text = "Regard de l'eyetracker indisponible : " + _gaze.ErrorMessage;
            }
            else if (!_gaze.HasData)
            {
                GazeReadText.Text = "Regard de l'eyetracker : en attente de mesures…";
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

            // The trail: each screen position is converted to a position in the thumbnail.
            CursorSample[] trail = _sampler.GetTrail();
            PointCollection points = new PointCollection(trail.Length);
            foreach (CursorSample sample in trail)
            {
                points.Add(new Point(sample.X * scaleX, sample.Y * scaleY));
            }
            _trailLine.Points = points;

            // The dot: subtract half its size so that its CENTER is on the position.
            int x;
            int y;
            _sampler.GetLatest(out x, out y);
            Canvas.SetLeft(_dot, x * scaleX - _dot.Width / 2);
            Canvas.SetTop(_dot, y * scaleY - _dot.Height / 2);

            // The real gaze: brought back into the thumbnail (a red dot on the edge = off screen).
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

        // ───────────────────────── Recording ─────────────────────────

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

        /// <summary>Advances the recording (called 20 times per second).</summary>
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

                SystemSounds.Asterisk.Play();   // short sound: recording starts
                _recorder.Start(_sampler);
                _recordPhase = RecordPhase.Recording;
                _recordClock.Restart();
                return;
            }

            // Recording phase.
            if (elapsed < RecordSeconds)
            {
                RecordText.Text = "Enregistrement en cours, encore " +
                                  Math.Ceiling(RecordSeconds - elapsed).ToString("F0") +
                                  " s : parcourez tout l'écran du regard, puis fixez le centre.";
                return;
            }

            _recordPhase = RecordPhase.None;
            RecordButton.IsEnabled = true;
            SystemSounds.Exclamation.Play();    // different sound: it is finished

            try
            {
                string file = _recorder.StopAndSave(_sampler, _screenWidth, _screenHeight);
                RecordText.Text = "Enregistré dans : " + file;
            }
            catch (Exception ex)
            {
                App.Log("Recording failed: " + ex);
                RecordText.Text = "Échec de l'enregistrement : " + ex.Message;
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
