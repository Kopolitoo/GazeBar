using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace Prunel
{
    /// <summary>
    /// Part of AppSettings: the default values, the limits of each value (Fix) and the
    /// update of a file written by an older version of Prunel (Refresh).
    /// </summary>
    internal sealed partial class AppSettings
    {
        // ───────────────────────── Default values ─────────────────────────

        /// <summary>The constructor: a new object holds the default values.</summary>
        public AppSettings()
        {
            DefaultValues();
        }

        /// <summary>
        /// Resets every setting to its initial value (those of the specification).
        /// The "Défaut" button of each settings mode tab only resets the settings of
        /// its tab: it reads these values from a new object (new AppSettings()).
        /// </summary>
        public void DefaultValues()
        {
            FileVersion = CurrentVersion;

            Smoothing = 0.16;
            // Sizes in % of the screen height: the old values in pixels of the 4K
            // screen (8, 30, 900, 190, 120) divided by 21.6, rounded.
            CursorDeadZone = 0.4;
            MaxMeasureAge = 300;

            FixationRadius = 1.4;
            FixationDelay = 0.8;
            MaxFixationSpeed = 42;
            DirectClick = false;
            DirectClickDelay = 1.5;

            CircleRadius = 8.8;
            IconSize = 5.6;
            InterfaceSize = 100;
            ShowLabels = true;

            ActivationDelay = 0.6;
            OptionsIconDelay = 0.3;
            MenuDelay = 0.5;
            MenuCloseDelay = 1.0;

            MagnifierBeforeAction = true;
            MagnifierZoom = 4;

            AndroidPauseDuration = 1.0;
            DragDuration = 0.5;

            MaxScrollSpeed = 5;
            ScrollInactiveWindows = true;

            GameSmoothing = 0.16;
            GameSensitivity = 1.0;
            GameMouse = DirectMouseText;
            GameRelative = TrackingRelativeText;

            MouseTracking = false;
            StartWithWindows = false;
            DetailedLog = false;

            KeySound = false;
            ButtonSound = false;

            KeyDelay = 0.6;
            KeyRepeat = true;
            AutoCapitalize = true;
            KeyboardHeight = 50;
            KeyboardSize = 100;
            KeyboardAtTop = false;
            KeyboardLayout = Prunel.KeyboardLayout.DefaultName;
            InputBox = false;
            KeyboardTransparency = 0;
            Language = Texts.FallbackLanguage;

            DefaultIcons();
        }

        /// <summary>
        /// Called by the serializer JUST BEFORE reading the file.
        ///
        /// Pitfall to know: when reading, DataContractJsonSerializer does NOT call the
        /// constructor. A setting missing from the file (for example one added in a new
        /// version of Prunel) would then be 0 or "no". By filling in the default values here
        /// first, a missing setting keeps its default value.
        /// </summary>
        [OnDeserializing]
        private void BeforeReading(StreamingContext context)
        {
            DefaultValues();
        }

        /// <summary>
        /// Brings each value back within reasonable limits. Protects against a typo in the
        /// file (a smoothing of 30 instead of 0.30, a negative delay…).
        /// </summary>
        public void Fix()
        {
            Smoothing = Clamp(Smoothing, 0.01, 1);
            // Size limits in % of the screen height.
            CursorDeadZone = Clamp(CursorDeadZone, 0, 5);
            MaxMeasureAge = Clamp(MaxMeasureAge, 50, 5000);

            FixationRadius = Clamp(FixationRadius, 0.2, 14);
            FixationDelay = Clamp(FixationDelay, 0.1, 10);
            MaxFixationSpeed = Clamp(MaxFixationSpeed, 2, 900);
            DirectClickDelay = Clamp(DirectClickDelay, 0.1, 10);

            CircleRadius = Clamp(CircleRadius, 1, 23);
            IconSize = Clamp(IconSize, 2, 19);
            InterfaceSize = Clamp(InterfaceSize, 75, 150);

            ActivationDelay = Clamp(ActivationDelay, 0.1, 10);
            OptionsIconDelay = Clamp(OptionsIconDelay, 0.1, 10);
            MenuDelay = Clamp(MenuDelay, 0.1, 10);
            MenuCloseDelay = Clamp(MenuCloseDelay, 0.1, 10);

            MagnifierZoom = Clamp(MagnifierZoom, 1, 10);

            AndroidPauseDuration = Clamp(AndroidPauseDuration, 0, 10);
            DragDuration = Clamp(DragDuration, 0.05, 10);

            MaxScrollSpeed = Clamp(MaxScrollSpeed, 1, 100);

            GameSmoothing = Clamp(GameSmoothing, 0.01, 1);
            GameSensitivity = Clamp(GameSensitivity, 0.1, 10);

            // An unknown word (typo, capitals…) becomes the default value again.
            string mouse = (GameMouse ?? "").Trim().ToLowerInvariant();
            GameMouse = mouse == RelativeMouseText ? RelativeMouseText : DirectMouseText;
            string relative = (GameRelative ?? "").Trim().ToLowerInvariant();
            GameRelative = relative == GamepadRelativeText ? GamepadRelativeText : TrackingRelativeText;

            KeyDelay = Clamp(KeyDelay, 0.1, 5);
            KeyboardHeight = Clamp(KeyboardHeight, 25, 75);
            // Only 50, 75 or 100: another value goes to the nearest (60 → 50, 90 → 100).
            KeyboardSize = Math.Round(Clamp(KeyboardSize, 50, 100) / 25) * 25;
            // Only 0, 25, 50 or 75: another value goes to the nearest (40 → 50, 90 → 75).
            KeyboardTransparency = Math.Round(Clamp(KeyboardTransparency, 0, 75) / 25) * 25;
            // An empty file name or one containing a path ("..\", "C:\"…) becomes the default
            // layout again: only files of the Keyboards folder are read.
            if (string.IsNullOrWhiteSpace(KeyboardLayout) ||
                KeyboardLayout.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                KeyboardLayout.Contains(".."))
            {
                KeyboardLayout = Prunel.KeyboardLayout.DefaultName;
            }
            KeyboardLayout = KeyboardLayout.Trim();

            // Same rule for the language: a plain file name of the Languages folder.
            if (string.IsNullOrWhiteSpace(Language) ||
                Language.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                Language.Contains(".."))
            {
                Language = Texts.FallbackLanguage;
            }
            Language = Language.Trim().ToLowerInvariant();

            FixIcons(IconGroup.Crown);
            FixIcons(IconGroup.Modifiers);
            FixIcons(IconGroup.Utilities);
        }

        /// <summary>
        /// Updates a file written by an older version of Prunel.
        /// Returns true if something changed (the file must then be saved again).
        ///
        /// Why? Changing a default value is not enough: the file already on the computer holds
        /// the old value (for example DirectClick = true), and that is what would be read. It
        /// is therefore fixed only once, then the version number is raised: afterwards, a
        /// choice made by the user in the file is respected.
        /// </summary>
        public bool Refresh()
        {
            if (FileVersion >= CurrentVersion)
            {
                return false;
            }

            if (FileVersion < 2)
            {
                DirectClick = false;
                MouseTracking = false;
                App.Log("Settings: updated to version 2 (DirectClick and MouseTracking turned off)");
            }

            if (FileVersion < 3)
            {
                // Nothing to fix: the old drag settings are simply ignored when reading, and
                // the new ones already have their default value (see BeforeReading). Saving
                // the file again makes the old ones disappear.
                App.Log("Settings: updated to version 3 (drag and drop settings replaced)");
            }

            if (FileVersion < 4)
            {
                // Only the old default is replaced: a value chosen by the user stays.
                if (Math.Abs(GameSmoothing - 0.7) < 0.001)
                {
                    GameSmoothing = 0.16;
                }
                App.Log("Settings: updated to version 4 (GameSmoothing 0.7 → 0.16)");
            }

            if (FileVersion < 5)
            {
                // Only the old default is replaced: a value chosen by the user stays.
                if (Math.Abs(MaxScrollSpeed - 10) < 0.001)
                {
                    MaxScrollSpeed = 15;
                }
                App.Log("Settings: updated to version 5 (MaxScrollSpeed 10 → 15)");
            }

            if (FileVersion < 6)
            {
                // Nothing to fix: icon lists missing from the file kept the default layout
                // (see BeforeReading). Saving the file again adds them.
                App.Log("Settings: updated to version 6 (action choice icon layout)");
            }

            if (FileVersion < 7)
            {
                // Sizes were in pixels, tuned on the user's 4K screen (2160 px high). They
                // become % of the screen height: percent = pixels ÷ 2160 × 100 = pixels ÷ 21.6.
                // ALWAYS divide by 2160, not by the current screen height: the values were
                // tuned in 4K, even if the screen is now 1920 × 1080.
                // Example: CircleRadius 190 px → 8.8 %.
                CursorDeadZone = PixelsToPercent(CursorDeadZone, 1);
                FixationRadius = PixelsToPercent(FixationRadius, 1);
                MaxFixationSpeed = PixelsToPercent(MaxFixationSpeed, 0);
                CircleRadius = PixelsToPercent(CircleRadius, 1);
                IconSize = PixelsToPercent(IconSize, 1);
                App.Log("Settings: updated to version 7 (sizes in % of the screen height)");
            }

            if (FileVersion < 8)
            {
                // Nothing to fix: keyboard settings missing from the file kept their default
                // value (see BeforeReading). Saving the file again adds them.
                App.Log("Settings: updated to version 8 (gaze keyboard settings)");
            }

            if (FileVersion < 9)
            {
                // Nothing to fix: KeyboardSize and KeyboardAtTop, missing from the file, kept
                // their default value (see BeforeReading).
                App.Log("Settings: updated to version 9 (keyboard size and position)");
            }

            if (FileVersion < 10)
            {
                // KeyboardLayout is missing from the file: it belongs to an existing French
                // user, who keeps the French keyboard (the default is English since step
                // clavier-anglais). InputBox kept its default value (see BeforeReading).
                KeyboardLayout = "fr-azerty";
                App.Log("Settings: updated to version 10 (keyboard language and input box)");
            }

            if (FileVersion < 11)
            {
                // Only the old default is replaced: a value chosen by the user stays.
                // (A version 4 or older file has just gone from 10 to 15 right above: so it
                // also ends at 5.)
                if (Math.Abs(MaxScrollSpeed - 15) < 0.001)
                {
                    MaxScrollSpeed = 5;
                }
                App.Log("Settings: updated to version 11 (MaxScrollSpeed 15 → 5)");
            }

            if (FileVersion < 12)
            {
                // Language is missing from the file: it belongs to an existing French user, who
                // keeps French (a new installation, with no file, starts in English).
                Language = "fr";
                App.Log("Settings: updated to version 12 (interface language)");
            }

            // Version 13 (DetailedLog), 14 (KeySound, ButtonSound) and 15
            // (KeyboardTransparency): nothing to fix, the settings keep their default value
            // (no, 0 %).
            FileVersion = CurrentVersion;
            return true;
        }

        /// <summary>
        /// Version 7 conversion: from pixels of the 4K screen to % of its height, rounded to
        /// "decimals" digits after the point (as in settings mode).
        /// </summary>
        private static double PixelsToPercent(double pixels, int decimals)
        {
            return Math.Round(pixels / ScreenMetrics.ReferenceHeightPixels * 100, decimals);
        }

        /// <summary>Returns the value, brought between min and max. An unreadable value (NaN) becomes min.</summary>
        private static double Clamp(double val, double min, double max)
        {
            if (double.IsNaN(val) || val < min)
            {
                return min;
            }
            if (val > max)
            {
                return max;
            }
            return val;
        }
    }
}
