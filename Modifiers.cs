namespace GazeBar
{
    /// <summary>The three modifier keys of the action choice.</summary>
    internal enum Modifier
    {
        Ctrl,
        Shift,
        Alt
    }

    /// <summary>
    /// MODEL: the state of the Ctrl, Shift and Alt modifiers of the action choice (step 6).
    ///
    /// How it works (specification, "Actions and modifiers"):
    ///  - a gaze on a modifier's icon activates it (green border), a second one deactivates it;
    ///  - when an action runs, the active keys are pressed just before (PressActive) and
    ///    released just after (ReleaseAll), or after the drop for a drag and drop;
    ///  - they are deactivated automatically after each action (DisableAll).
    ///
    /// Why a shared "static" class, and not a simple field of the action choice?
    /// Because the action choice can be left without acting: for example Ctrl, then
    /// "Magnifier", then back to the action choice on the refined point. A new
    /// ActionChoiceMode is then created, and it must find Ctrl still lit. Likewise, drag and
    /// drop goes through three modes (target, magnifier, execution) before using the modifiers.
    ///
    /// The modifiers are also deactivated on every return to movement mode (even without an
    /// action, through "back" or Escape): this way, a forgotten Ctrl never stays secretly
    /// active for the next click.
    /// </summary>
    internal static class Modifiers
    {
        public static bool Ctrl { get; private set; }

        public static bool Shift { get; private set; }

        public static bool Alt { get; private set; }

        /// <summary>True if at least one modifier is active.</summary>
        public static bool AnyActive
        {
            get { return Ctrl || Shift || Alt; }
        }

        /// <summary>True if this modifier is active.</summary>
        public static bool IsActive(Modifier m)
        {
            switch (m)
            {
                case Modifier.Ctrl:
                    return Ctrl;
                case Modifier.Shift:
                    return Shift;
                default:
                    return Alt;
            }
        }

        /// <summary>Activates the modifier if it is inactive, deactivates it otherwise. Returns the new state.</summary>
        public static bool Toggle(Modifier m)
        {
            switch (m)
            {
                case Modifier.Ctrl:
                    Ctrl = !Ctrl;
                    break;
                case Modifier.Shift:
                    Shift = !Shift;
                    break;
                default:
                    Alt = !Alt;
                    break;
            }
            App.Log("Modifiers: " + Description());
            return IsActive(m);
        }

        /// <summary>Deactivates the three modifiers (without touching the keyboard).</summary>
        public static void DisableAll()
        {
            if (AnyActive)
            {
                App.Log("Modifiers: all off");
            }
            Ctrl = false;
            Shift = false;
            Alt = false;
        }

        /// <summary>Presses the keys of the active modifiers (just before an action).</summary>
        public static void PressActive()
        {
            if (Ctrl)
            {
                KeyboardInjector.PressDown(NativeMethods.VK_CONTROL);
            }
            if (Shift)
            {
                KeyboardInjector.PressDown(NativeMethods.VK_SHIFT);
            }
            if (Alt)
            {
                KeyboardInjector.PressDown(NativeMethods.VK_MENU);
            }
        }

        /// <summary>Releases all pressed keys (just after an action).</summary>
        public static void ReleaseAll()
        {
            KeyboardInjector.ReleaseAll();
        }

        /// <summary>Text for the log, for example "Ctrl + Shift" or "none".</summary>
        public static string Description()
        {
            string text = "";
            if (Ctrl)
            {
                text += "Ctrl";
            }
            if (Shift)
            {
                text += (text.Length > 0 ? " + " : "") + "Maj";
            }
            if (Alt)
            {
                text += (text.Length > 0 ? " + " : "") + "Alt";
            }
            return text.Length > 0 ? text : "aucun";
        }
    }
}
