using System;
using System.Runtime.CompilerServices;
using Tobii.Interaction;

namespace Prunel
{
    /// <summary>
    /// The gaze source used normally (see IGazeSource): the real gaze position, provided by
    /// the eyetracker through the Tobii.Interaction library (the same as Mill Mouse). It does
    /// not depend on the cursor driven by the vendor's software.
    ///
    /// Coordinates are in screen pixels. They can be NEGATIVE or beyond the screen size when
    /// looking outside it: this is useful (off-screen menu, etc.).
    ///
    /// If the eyetracker engine is not available, the program keeps working: IsRunning stays
    /// false and ErrorMessage explains why.
    ///
    /// The eyetracker DLLs are not shipped with Prunel: they are
    /// searched for on the PC at startup (see EyetrackerLoader.cs). So that Prunel starts
    /// even when they cannot be found, the Tobii.Interaction types (Host, GazePointDataStream)
    /// only appear in the ConnectEyetracker method: .NET only looks for a DLL when it prepares
    /// ("just-in-time compiles") a method that uses it. If the DLL is missing, the error
    /// therefore happens when ConnectEyetracker is called, inside the try of Start, instead of
    /// crashing the whole program.
    /// </summary>
    internal sealed class EyetrackerGazeSource : GazeSource
    {
        // The Host object and the gaze stream (Tobii.Interaction types). Deliberately stored
        // under "neutral" types (IDisposable, object): see the comment at the top of the class.
        private IDisposable _host;
        private object _stream;

        /// <summary>
        /// The Host object (Tobii.Interaction) (null if the connection failed). Only used to start
        /// the calibration tool (see Calibration.cs). Its type is "object" so that the
        /// rest of the program does not need to know Tobii.Interaction.
        /// </summary>
        public object Host
        {
            get { return _host; }
        }

        /// <summary>Scale correction (see ScreenMetrics.GazeFactor): usually 1.</summary>
        protected override double ScaleFactor
        {
            get { return ScreenMetrics.GazeFactor; }
        }

        /// <summary>Connects to the eyetracker engine and starts receiving the gaze.</summary>
        public override void Start()
        {
            // Were the eyetracker DLLs found on the PC? (see EyetrackerLoader.cs)
            if (!EyetrackerLoader.Available)
            {
                IsRunning = false;
                ErrorMessage = EyetrackerLoader.Message;
                App.Log("Gaze: no connection: " + ErrorMessage);
                return;
            }

            try
            {
                ConnectEyetracker();
                IsRunning = true;
                App.Log("Gaze: connected");
            }
            catch (Exception ex)
            {
                IsRunning = false;
                ErrorMessage = ex.Message;
                App.Log("Gaze: connection failed: " + ex);
            }
        }

        /// <summary>
        /// The only place that uses the Tobii.Interaction types directly. "NoInlining" forbids
        /// the compiler from copying this method inside Start: otherwise, a missing DLL would
        /// make Start itself fail, outside its try.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ConnectEyetracker()
        {
            Host host = new Host();
            _host = host;
            GazePointDataStream stream = host.Streams.CreateGazePointDataStream();
            _stream = stream;

            // Called by Tobii.Interaction, on one of its threads, for each new measure.
            stream.Next += (sender, measure) => OnGaze(measure.Data.X, measure.Data.Y);
        }

        public override void Dispose()
        {
            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }
        }
    }
}
