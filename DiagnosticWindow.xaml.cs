using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GazeBar
{
    /// <summary>
    /// INTERFACE: diagnostic window. Shows live what the cursor sampler "sees".
    /// Nothing is written to disk: the eyetracker licence forbids storing gaze data.
    /// </summary>
    internal partial class DiagnosticWindow : Window
    {
        /// <summary>Width of the screen thumbnail, in WPF units.</summary>
        private const double MapWidth = 680.0;

        private readonly CursorSampler _sampler;
        private readonly GazeSource _gaze;
        private readonly DispatcherTimer _timer;

        private readonly Polyline _trailLine;   // the trail of the latest positions
        private readonly Ellipse _dot;          // the dot = current cursor position
        private readonly Ellipse _gazeDot;      // the green dot = real gaze position

        private readonly double _screenWidth;
        private readonly double _screenHeight;
        private readonly double _mapHeight;

        public DiagnosticWindow(CursorSampler sampler, GazeSource gaze)
        {
            InitializeComponent();

            // The fixed texts, in the current language (step langues-socle).
            Title = Texts.Get("diagnostic.title");
            CloseButton.Content = Texts.Get("diagnostic.close");

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

            ScreenInfoText.Text = Texts.Format("diagnostic.screen", _screenWidth, _screenHeight);

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
        }

        // ───────────────────────── Live display ─────────────────────────

        private void OnTick(object sender, EventArgs e)
        {
            UpdateReadouts();
            UpdateMap();
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

            RateText.Text = Texts.Format("diagnostic.rate", stats.ChangesPerSecond.ToString("F0"),
                                         stats.PollsPerSecond.ToString("F0"));

            if (!_gaze.IsRunning)
            {
                GazeReadText.Text = Texts.Format("diagnostic.gazeUnavailable", _gaze.ErrorMessage);
            }
            else if (!_gaze.HasData)
            {
                GazeReadText.Text = Texts.Get("diagnostic.gazeWaiting");
            }
            else
            {
                double gx;
                double gy;
                _gaze.GetLatest(out gx, out gy);
                bool outside = GazeSource.IsOffScreen(gx, gy, _screenWidth, _screenHeight);
                GazeReadText.Text = Texts.Format("diagnostic.gaze", gx.ToString("F0"), gy.ToString("F0"),
                                                 _gaze.SamplesPerSecond.ToString("F0")) +
                                    (outside ? Texts.Get("diagnostic.offScreen") : "");
            }

            double idle = _sampler.MsSinceLastChange;
            IdleText.Text = idle < 0
                ? Texts.Get("diagnostic.noMovement")
                : Texts.Format("diagnostic.lastChange", idle.ToString("F0"));
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

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
