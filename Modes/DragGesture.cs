namespace GazeBar
{
    /// <summary>
    /// MODEL: the description of a drag and drop being prepared (step 6).
    ///
    /// A drag and drop goes through three modes (see the specification):
    ///   1. DragTargetMode     the circle follows the gaze until a fixation on the drop
    ///                         point; a dotted line starts from the start point;
    ///   2. MagnifierMode      the magnifier refines the drop point;
    ///   3. DragExecutionMode  button pressed at the start, cursor moved to the drop point
    ///                         in [DragDuration], button released.
    /// This object is passed from one to the next: it keeps what does not change on the way.
    ///
    /// The three crown actions only differ by these settings:
    ///   left drag and drop     : left button, no pause;
    ///   right drag and drop    : right button, no pause;
    ///   android drag and drop  : left button, with a pause of [AndroidPauseDuration] between
    ///                            the press and the move (replaces the old long press).
    /// </summary>
    internal sealed class DragGesture
    {
        public DragGesture(double startX, double startY, MouseSide button, bool withPause)
        {
            StartX = startX;
            StartY = startY;
            Button = button;
            WithPause = withPause;
        }

        /// <summary>Start point (where the button will be pressed), in screen pixels.</summary>
        public double StartX { get; private set; }

        public double StartY { get; private set; }

        /// <summary>The mouse button held during the drag.</summary>
        public MouseSide Button { get; private set; }

        /// <summary>True for the "android" drag and drop: pause before the move.</summary>
        public bool WithPause { get; private set; }

        /// <summary>Readable name, for the log.</summary>
        public string Name
        {
            get
            {
                if (WithPause)
                {
                    return "glisser-déposer android";
                }
                return Button == MouseSide.Left ? "glisser-déposer gauche" : "glisser-déposer droit";
            }
        }
    }
}
