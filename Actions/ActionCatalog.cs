using System;
using System.Collections.Generic;

namespace Prunel
{
    /// <summary>
    /// The actions that can be placed on the icons of the action choice.
    ///
    /// The action choice (ActionChoiceMode), the icon configuration
    /// screen (IconConfigMode) and the settings (settings.json) all need this list: so it is
    /// here, with everything that describes an action (its group, its button, its name).
    ///
    /// Note: the NAMES of this list are written as is in settings.json (for example
    /// "LeftClick"). Renaming a value would make the matching slot of an already saved file
    /// unreadable (it would become empty).
    /// </summary>
    internal enum ChoiceAction
    {
        LeftClick,
        RightClick,
        DoubleClick,
        Magnifier,
        DragLeft,
        DragRight,
        DragAndroid,
        Scroll,
        Ctrl,
        Shift,
        Alt,
        Copy,
        Cut,
        Paste,
        Back,

        /// <summary>
        /// Opens (or closes) the gaze keyboard. Added at
        /// the end: the names already written in settings.json do not change.
        /// </summary>
        Keyboard
    }

    /// <summary>
    /// The three groups of slots of the action choice. Each group has its own list of
    /// actions: a crown action cannot go on the left, for example (the specification
    /// separates the lists: "the three slots on the left for the modifiers, the eight slots
    /// in the center for the actions").
    /// </summary>
    internal enum IconGroup
    {
        /// <summary>The eight slots around the photo.</summary>
        Crown,

        /// <summary>The three slots stuck to the left edge (Ctrl, Shift, Alt).</summary>
        Modifiers,

        /// <summary>The three slots stuck to the top edge (Copy, Cut, Paste).</summary>
        Utilities
    }

    /// <summary>
    /// THE ACTION CATALOG: for each group, the actions that can be placed there, their default
    /// layout, and for each action its button and its name.
    ///
    /// To add an action later: add it to ChoiceAction (preferably at the end), to the list of
    /// its group (Actions), to CreateButton and ShortName, then program what it does in
    /// ActionChoiceMode.
    /// </summary>
    internal static class ActionCatalog
    {
        /// <summary>Number of slots of each group.</summary>
        public static int SlotCount(IconGroup group)
        {
            return group == IconGroup.Crown ? 8 : 3;
        }

        /// <summary>
        /// The actions that can be placed in this group, in the order the list of the
        /// configuration screen shows them.
        /// </summary>
        public static ChoiceAction[] Actions(IconGroup group)
        {
            switch (group)
            {
                case IconGroup.Crown:
                    return new[]
                    {
                        ChoiceAction.LeftClick,
                        ChoiceAction.RightClick,
                        ChoiceAction.DoubleClick,
                        ChoiceAction.Magnifier,
                        ChoiceAction.DragLeft,
                        ChoiceAction.DragRight,
                        ChoiceAction.DragAndroid,
                        ChoiceAction.Scroll,
                        // Offered in the list of the configuration
                        // screen, but not placed by default (the eight slots are taken).
                        ChoiceAction.Keyboard
                    };
                case IconGroup.Modifiers:
                    return new[] { ChoiceAction.Ctrl, ChoiceAction.Shift, ChoiceAction.Alt };
                default:
                    return new[] { ChoiceAction.Copy, ChoiceAction.Cut, ChoiceAction.Paste };
            }
        }

        /// <summary>
        /// The default layout of each group (order chosen by the user for the
        /// crown). Crown, starting from the top right, clockwise (see ActionChoiceLayout):
        ///   0: top, right      → scroll
        ///   1: right, top      → right click
        ///   2: right, bottom   → left drag and drop
        ///   3: bottom, right   → right drag and drop
        ///   4: bottom, left    → magnifier
        ///   5: left, bottom    → android drag and drop
        ///   6: left, top       → double click
        ///   7: top, left       → left click
        /// Modifiers, from top to bottom: Ctrl, Shift, Alt.
        /// Utilities, from left to right: Copy, Cut, Paste.
        /// The Keyboard action is not placed by default: one adds it oneself instead
        /// of another one, in the icon configuration screen.
        /// </summary>
        public static ChoiceAction[] DefaultLayout(IconGroup group)
        {
            if (group == IconGroup.Crown)
            {
                return new[]
                {
                    ChoiceAction.Scroll,
                    ChoiceAction.RightClick,
                    ChoiceAction.DragLeft,
                    ChoiceAction.DragRight,
                    ChoiceAction.Magnifier,
                    ChoiceAction.DragAndroid,
                    ChoiceAction.DoubleClick,
                    ChoiceAction.LeftClick
                };
            }
            // For the two other groups, the default layout is the list itself.
            return Actions(group);
        }

        /// <summary>True if this action can be placed in this group.</summary>
        public static bool BelongsTo(ChoiceAction action, IconGroup group)
        {
            return Array.IndexOf(Actions(group), action) >= 0;
        }

        /// <summary>
        /// Reads an action name written in settings.json (for example "LeftClick").
        /// Returns null for an empty slot ("" or null), an unknown name (typo, removed action)
        /// or an action that does not belong to this group.
        /// </summary>
        public static ChoiceAction? Read(string name, IconGroup group)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            ChoiceAction action;
            // "true": case does not matter ("leftclick" is accepted).
            if (!Enum.TryParse(name.Trim(), true, out action) || !Enum.IsDefined(typeof(ChoiceAction), action))
            {
                return null;
            }
            return BelongsTo(action, group) ? action : (ChoiceAction?)null;
        }

        /// <summary>The name written in settings.json: "" for an empty slot.</summary>
        public static string Write(ChoiceAction? action)
        {
            return action.HasValue ? action.Value.ToString() : "";
        }

        /// <summary>True for the Ctrl, Shift, Alt modifiers.</summary>
        public static bool IsModifier(ChoiceAction action)
        {
            return action == ChoiceAction.Ctrl || action == ChoiceAction.Shift || action == ChoiceAction.Alt;
        }

        /// <summary>The modifier matching the Ctrl, Shift or Alt action.</summary>
        public static Modifier ToModifier(ChoiceAction action)
        {
            switch (action)
            {
                case ChoiceAction.Ctrl:
                    return Modifier.Ctrl;
                case ChoiceAction.Shift:
                    return Modifier.Shift;
                default:
                    return Modifier.Alt;
            }
        }

        /// <summary>
        /// A short name, shown under each action in the list of the configuration screen (the
        /// button label, longer, is sometimes too wide for the grid).
        /// </summary>
        public static string ShortName(ChoiceAction action)
        {
            switch (action)
            {
                case ChoiceAction.LeftClick: return Texts.Get("action.leftClick.short");
                case ChoiceAction.RightClick: return Texts.Get("action.rightClick.short");
                case ChoiceAction.DoubleClick: return Texts.Get("action.doubleClick.short");
                case ChoiceAction.Magnifier: return Texts.Get("action.magnifier.short");
                case ChoiceAction.DragLeft: return Texts.Get("action.dragLeft.short");
                case ChoiceAction.DragRight: return Texts.Get("action.dragRight.short");
                case ChoiceAction.DragAndroid: return Texts.Get("action.dragAndroid.short");
                case ChoiceAction.Scroll: return Texts.Get("action.scroll.short");
                case ChoiceAction.Ctrl: return Texts.Get("action.ctrl.short");
                case ChoiceAction.Shift: return Texts.Get("action.shift.short");
                case ChoiceAction.Alt: return Texts.Get("action.alt.short");
                case ChoiceAction.Copy: return Texts.Get("action.copy.short");
                case ChoiceAction.Cut: return Texts.Get("action.cut.short");
                case ChoiceAction.Paste: return Texts.Get("action.paste.short");
                case ChoiceAction.Keyboard: return Texts.Get("action.keyboard.short");
                default: return Texts.Get("action.back.short");
            }
        }

        /// <summary>
        /// The button of an action: its symbol, its badge and its label.
        /// Symbols of the Windows font (Segoe MDL2 Assets / Segoe Fluent Icons):
        ///   E962 = mouse, E71E = magnifier, E7C2 = move (four arrows), E72B = "back" arrow,
        ///   EC8F = scroll (up and down arrows; symbol to check on screen),
        ///   E8C8 = copy, E8C6 = scissors (cut), E77F = clipboard (paste),
        ///   E765 = keyboard.
        /// The three clicks share the mouse symbol, the three drag and drops the four arrows;
        /// the badge tells them apart (G = left, D = right, A = android).
        /// The modifiers simply show their name ("Ctrl", "Maj", "Alt").
        /// </summary>
        public static GazeButton CreateButton(ChoiceAction action)
        {
            switch (action)
            {
                case ChoiceAction.LeftClick:
                    return new GazeButton("", Texts.Get("action.badge.left"), Texts.Get("action.leftClick.label"));
                case ChoiceAction.RightClick:
                    return new GazeButton("", Texts.Get("action.badge.right"), Texts.Get("action.rightClick.label"));
                case ChoiceAction.DoubleClick:
                    return new GazeButton("", "2×", Texts.Get("action.doubleClick.label"));
                case ChoiceAction.Magnifier:
                    return new GazeButton("", null, Texts.Get("action.magnifier.label"));
                case ChoiceAction.DragLeft:
                    return new GazeButton("", Texts.Get("action.badge.left"), Texts.Get("action.dragLeft.label"));
                case ChoiceAction.DragRight:
                    return new GazeButton("", Texts.Get("action.badge.right"), Texts.Get("action.dragRight.label"));
                case ChoiceAction.DragAndroid:
                    return new GazeButton("", Texts.Get("action.badge.android"), Texts.Get("action.dragAndroid.label"));
                case ChoiceAction.Scroll:
                    return new GazeButton("", null, Texts.Get("action.scroll.label"));
                case ChoiceAction.Ctrl:
                    return GazeButton.Text(Texts.Get("action.ctrl.short"), Texts.Get("action.ctrl.label"));
                case ChoiceAction.Shift:
                    return GazeButton.Text(Texts.Get("action.shift.short"), Texts.Get("action.shift.label"));
                case ChoiceAction.Alt:
                    return GazeButton.Text(Texts.Get("action.alt.short"), Texts.Get("action.alt.label"));
                case ChoiceAction.Copy:
                    return new GazeButton("", null, Texts.Get("action.copy.label"));
                case ChoiceAction.Cut:
                    return new GazeButton("", null, Texts.Get("action.cut.label"));
                case ChoiceAction.Paste:
                    return new GazeButton("", null, Texts.Get("action.paste.label"));
                case ChoiceAction.Keyboard:
                    // Empty badge: the same button opens the keyboard, or closes it if it is open.
                    return new GazeButton("\uE765", null, Texts.Get("action.keyboard.label"));
                default:
                    return new GazeButton("", null, Texts.Get("action.back.label"));
            }
        }
    }
}
