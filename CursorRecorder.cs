using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GazeBar
{
    /// <summary>
    /// MODEL: records the cursor positions for a few seconds and writes them to a CSV
    /// file (a table readable with Excel or a script).
    ///
    /// What it is for: objectively measuring the quality of the "gaze" obtained through
    /// the cursor: real rate, regularity, precision, covered area. Each line of the file
    /// is a change of position (so the time gaps between lines give the real rate).
    /// </summary>
    internal sealed class CursorRecorder
    {
        private readonly object _lock = new object();
        private readonly List<CursorSample> _samples = new List<CursorSample>();
        private bool _recording;

        /// <summary>Starts recording (the current position is written as the first line).</summary>
        public void Start(CursorSampler sampler)
        {
            int x;
            int y;
            sampler.GetLatest(out x, out y);

            CursorSample first = new CursorSample();
            first.Ms = sampler.NowMs;
            first.X = x;
            first.Y = y;

            lock (_lock)
            {
                _samples.Clear();
                _samples.Add(first);
                _recording = true;
            }

            // "+=": on every position change, the OnPositionChanged method will be called.
            sampler.PositionChanged += OnPositionChanged;
        }

        /// <summary>Called for each position change (from the sampling thread).</summary>
        private void OnPositionChanged(CursorSample sample)
        {
            lock (_lock)
            {
                if (_recording)
                {
                    _samples.Add(sample);
                }
            }
        }

        /// <summary>Stops recording without saving anything.</summary>
        public void Cancel(CursorSampler sampler)
        {
            sampler.PositionChanged -= OnPositionChanged;
            lock (_lock)
            {
                _recording = false;
            }
        }

        /// <summary>Ends recording and writes the CSV file. Returns the file path.</summary>
        public string StopAndSave(CursorSampler sampler, double screenWidth, double screenHeight)
        {
            Cancel(sampler);

            List<CursorSample> copy;
            lock (_lock)
            {
                copy = new List<CursorSample>(_samples);
            }

            // The "Documents\GazeBar" folder.
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GazeBar");
            Directory.CreateDirectory(folder);

            string file = Path.Combine(
                folder, "positions-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");

            // InvariantCulture: the decimal separator is always a point, whatever the
            // Windows language (otherwise the French comma would break the CSV format).
            CultureInfo inv = CultureInfo.InvariantCulture;

            StringBuilder text = new StringBuilder();
            text.AppendLine("# ecran=" + screenWidth.ToString("F0", inv) + "x" + screenHeight.ToString("F0", inv));
            text.AppendLine("t_ms,x,y");

            double t0 = copy.Count > 0 ? copy[0].Ms : 0;
            foreach (CursorSample s in copy)
            {
                text.AppendLine(
                    (s.Ms - t0).ToString("F2", inv) + "," +
                    s.X.ToString(inv) + "," +
                    s.Y.ToString(inv));
            }

            File.WriteAllText(file, text.ToString(), Encoding.UTF8);
            App.Log("Recording: " + copy.Count + " positions in " + file);
            return file;
        }
    }
}
