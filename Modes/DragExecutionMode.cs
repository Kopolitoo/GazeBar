using System;

namespace GazeBar
{
    /// <summary>
    /// DRAG AND DROP MODE, part 3: the execution (step 6).
    ///
    /// We get here from the magnifier, once the drop point has been refined. Everything
    /// happens on its own, in a few tenths of a second, without looking anywhere:
    ///
    ///  1. HIDING.       Everything GazeBar draws is hidden, and we wait [WaitMs] for
    ///                   Windows to erase it (otherwise the drag would start on the black screen).
    ///  2. PRESS.        The active modifiers (Ctrl, Shift, Alt) are pressed, the cursor is
    ///                   placed on the start point, then the button is pressed.
    ///  3. PAUSE.        Only for the "android" drag and drop: we wait [AndroidPauseDuration]
    ///                   without moving, button pressed, like a finger resting on a phone icon
    ///                   before moving it.
    ///  4. MOVE.         The cursor goes from the start to the drop point in a straight line,
    ///                   in [DragDuration], a small step on each tick (about 60 per second),
    ///                   like a real hand. Moving in one go would not work: many applications
    ///                   only recognize a drag after several small movements.
    ///  5. DROP.         The cursor is put back exactly on the drop point, we wait
    ///                   [DropWaitMs] (the time for the target application to notice that the
    ///                   target is hovered), then the button is released, and the modifiers too.
    ///
    /// Then: back to movement mode. A new fixation on the drop point is ignored until the
    /// gaze has left it, as after a click. The modifiers are deactivated ("they deactivate
    /// after each action").
    ///
    /// Safety: if this mode is left before the end (caregiver's Escape key, error), the
    /// button and keys are released in Quit. The menu does not open during this mode.
    /// </summary>
    internal sealed class DragExecutionMode : Mode
    {
        /// <summary>The phases of the mode (see the class comment).</summary>
        private enum Step
        {
            Hiding,
            Pause,
            Move,
            Drop,
            Finished
        }

        /// <summary>Wait after hiding everything, in milliseconds (same value as the action choice).</summary>
        private const double WaitMs = 60;

        /// <summary>
        /// Wait between the cursor's arrival and the release, in milliseconds. It is not a
        /// setting of the specification: it is a technical precaution, because some
        /// applications (Explorer, for example) need to "see" the cursor still on the target
        /// for a short moment before accepting the drop.
        /// </summary>
        private const double DropWaitMs = 100;

        private readonly DragGesture _drag;

        // The drop point, in screen pixels.
        private readonly double _ax;
        private readonly double _ay;

        private Step _step;
        private double _stepStartMs;

        public DragExecutionMode(DragGesture drag, double arrivalX, double arrivalY)
        {
            _drag = drag;
            _ax = arrivalX;
            _ay = arrivalY;
        }

        public override string Name
        {
            get { return "Drag and drop (execution)"; }
        }

        public override void Enter(GazeState gaze)
        {
            // 1. HIDING: hide everything, and wait a bit before pressing the button.
            Manager.Layer.HideAll();
            _step = Step.Hiding;
            _stepStartMs = gaze.NowMs;
        }

        public override void Update(GazeState gaze)
        {
            double now = gaze.NowMs;
            double elapsedMs = now - _stepStartMs;
            AppSettings p = AppSettings.Current;

            switch (_step)
            {
                case Step.Hiding:
                    if (elapsedMs >= WaitMs)
                    {
                        Press();
                        // With the "android" pause, wait before moving; otherwise, start
                        // moving on the next tick.
                        _step = _drag.WithPause ? Step.Pause : Step.Move;
                        _stepStartMs = now;
                    }
                    break;

                case Step.Pause:
                    if (elapsedMs >= p.AndroidPauseDuration * 1000.0)
                    {
                        _step = Step.Move;
                        _stepStartMs = now;
                    }
                    break;

                case Step.Move:
                    {
                        // Fraction of the path already covered, from 0 to 1.
                        double fraction = Math.Min(1.0, elapsedMs / (p.DragDuration * 1000.0));
                        double x = _drag.StartX + (_ax - _drag.StartX) * fraction;
                        double y = _drag.StartY + (_ay - _drag.StartY) * fraction;
                        MouseInjector.MoveTo(x, y);

                        if (fraction >= 1.0)
                        {
                            // Arrived: the cursor is put back exactly on the point (the rounding
                            // in MoveTo can shift it by one pixel).
                            NativeMethods.SetCursorPos((int)Math.Round(_ax), (int)Math.Round(_ay));
                            _step = Step.Drop;
                            _stepStartMs = now;
                        }
                    }
                    break;

                case Step.Drop:
                    if (elapsedMs >= DropWaitMs)
                    {
                        Drop();
                    }
                    break;
            }
        }

        public override void Quit()
        {
            // If we leave before the end (Escape, error), nothing must stay pressed.
            if (_step != Step.Finished)
            {
                MouseInjector.Release(_drag.Button);
                Modifiers.ReleaseAll();
                if (_step != Step.Hiding)
                {
                    App.Log("Drag: interrupted before the end, button and keys released");
                }
            }
        }

        /// <summary>2. PRESS: modifiers, cursor on the start point, button pressed.</summary>
        private void Press()
        {
            Modifiers.PressActive();

            int dx = (int)Math.Round(_drag.StartX);
            int dy = (int)Math.Round(_drag.StartY);
            NativeMethods.SetCursorPos(dx, dy);
            // A "real" mouse movement on the spot: the application then knows the mouse is
            // over the element to grab before the button goes down.
            MouseInjector.MoveTo(dx, dy);
            NativeMethods.SetCursorPos(dx, dy);

            MouseInjector.Press(_drag.Button);
            App.Log("Drag: " + _drag.Name + ", button pressed at (" + dx + ", " + dy +
                    "), modifiers: " + Modifiers.Description());
        }

        /// <summary>5. DROP: button released, then modifiers, then back to movement mode.</summary>
        private void Drop()
        {
            MouseInjector.Release(_drag.Button);
            Modifiers.ReleaseAll();
            _step = Step.Finished;

            App.Log("Drag: dropped at (" + _ax.ToString("F0") + ", " + _ay.ToString("F0") + ")");

            // Movement mode deactivates the modifiers and ignores a new fixation around the
            // drop point until the gaze has left it.
            Manager.ChangeMode(new MovementMode(_ax, _ay));
        }
    }
}
