using System;

namespace Prunel
{
    /// <summary>
    /// Anything that provides a gaze position: the eyetracker (EyetrackerGazeSource) or, later,
    /// another source used to test Prunel without an eyetracker.
    ///
    /// An "interface" is a contract: it lists what a source must offer, without saying how.
    /// The modes, the mode manager and the Diagnostic window only know this contract, so they
    /// work the same whatever the source.
    ///
    /// Coordinates are in screen pixels. They can be NEGATIVE or beyond the screen size when
    /// looking outside it (off-screen menu, etc.).
    ///
    /// A source keeps only the latest position in memory: gaze data is never saved
    /// (eyetracker licence).
    /// </summary>
    internal interface IGazeSource : IDisposable
    {
        /// <summary>True if the source started correctly.</summary>
        bool IsRunning { get; }

        /// <summary>Error message if the source could not start (empty otherwise).</summary>
        string ErrorMessage { get; }

        /// <summary>True as soon as at least one measure has been received.</summary>
        bool HasData { get; }

        /// <summary>Measure rate: number per second (0 if nothing arrives any more).</summary>
        double SamplesPerSecond { get; }

        /// <summary>Time elapsed since the last measure, in ms (very large if none).</summary>
        double MsSinceLastSample { get; }

        /// <summary>Current time on the source's clock, in milliseconds (same scale as instantMs).</summary>
        double NowMs { get; }

        /// <summary>Starts the source. Never throws: on failure, IsRunning stays false.</summary>
        void Start();

        /// <summary>Latest known gaze position (screen pixels).</summary>
        void GetLatest(out double x, out double y);

        /// <summary>
        /// Latest gaze position, with its number (+1 for each new measure) and the time it
        /// arrived (in milliseconds, on the source's clock).
        /// </summary>
        void GetLatest(out double x, out double y, out long num, out double instantMs);
    }
}
