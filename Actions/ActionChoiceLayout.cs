using System;
using System.Windows;

namespace GazeBar
{
    /// <summary>
    /// THE LAYOUT of the action choice screen: where the photo, the eight crown
    /// slots, the three modifiers, the three utility buttons and the "back" icon are.
    ///
    /// The action choice (ActionChoiceMode) and the icon configuration screen
    /// (IconConfigMode) must show EXACTLY the same layout: so these computations
    /// live here, and both modes use them.
    ///
    /// All positions are CENTERS, in screen pixels (like the gaze).
    ///
    /// [Space] and [EdgeMargin] are pixels of the user's 4K screen, converted for
    /// the current screen with ScreenMetrics.Px (60 px in 4K → 30 px in 1920 × 1080).
    /// </summary>
    internal sealed class ActionChoiceLayout
    {
        /// <summary>Photo enlargement (the specification sets ×2).</summary>
        public const double Zoom = 2;

        /// <summary>
        /// Space, in 4K screen pixels, between two neighbouring buttons, between the buttons and
        /// the photo, and between the modifiers and the left edge of the screen (user request:
        /// "about 60 pixels").
        /// </summary>
        public const double Space4K = 60;

        /// <summary>[Space4K] in pixels of the current screen (with [InterfaceSize]).</summary>
        public static double Space
        {
            get { return ScreenMetrics.Px(Space4K); }
        }

        /// <summary>Margin, in 4K screen pixels, between a button and the screen edge when it is stuck to it.</summary>
        private const double EdgeMargin4K = 10;

        private static double EdgeMargin
        {
            get { return ScreenMetrics.Px(EdgeMargin4K); }
        }

        /// <summary>
        /// Computes the layout for a screen of this size, buttons of this side ([IconSize]) and
        /// a photo of this side BEFORE enlargement (2 × [CircleRadius]).
        /// </summary>
        public ActionChoiceLayout(double width, double height, double size, double photoSide)
        {
            Width = width;
            Height = height;
            Size = size;

            double cx = width / 2;
            double cy = height / 2;
            CenterX = cx;
            CenterY = cy;

            // ── The enlarged photo, in the center of the screen.
            EnlargedPhotoSide = photoSide * Zoom;

            // ── The crown: eight slots, TWO PER SIDE of the photo.
            //    On each side, the two buttons are [Space] pixels apart and centered
            //    on the middle of the side. They are [Space] pixels from the photo, unless the
            //    screen is too small (top and bottom on a 1080p screen): they then move closer
            //    to the photo to stay fully on the screen.
            double halfPhoto = EnlargedPhotoSide / 2;
            double wantedDistance = halfPhoto + Space + size / 2;
            double distanceX = Math.Min(wantedDistance, width / 2 - size / 2 - EdgeMargin);
            double distanceY = Math.Min(wantedDistance, height / 2 - size / 2 - EdgeMargin);
            double offset = size + Space;   // from the middle of the side to the center of each button

            Crown = new Point[8];
            for (int i = 0; i < Crown.Length; i++)
            {
                Crown[i] = CrownSlot(i, cx, cy, distanceX, distanceY, offset);
            }

            // ── The utility buttons (Copy, Cut, Paste): a row stuck to the top edge of the
            //    screen, centered, spread over the width like the modifiers over the height. On a
            //    1920 × 1080 screen: x = 440, 960 and 1480.
            int utilityCount = ActionCatalog.SlotCount(IconGroup.Utilities);
            double utilitiesGap = (width - size * utilityCount) / utilityCount;
            double yUtilities = size / 2 + EdgeMargin;
            Utilities = new Point[utilityCount];
            for (int i = 0; i < utilityCount; i++)
            {
                double x = cx + (i - (utilityCount - 1) / 2.0) * (size + utilitiesGap);
                Utilities[i] = new Point(x, yUtilities);
            }

            // ── The modifiers: a column of three, stuck to the left edge of the screen, [Space]
            // pixels from the edge.
            int modifierCount = ActionCatalog.SlotCount(IconGroup.Modifiers);
            double xModifiers = Space + size / 2;
            double modifiersGap = (height - size * modifierCount) / modifierCount;
            Modifiers = new Point[modifierCount];
            for (int i = 0; i < modifierCount; i++)
            {
                double y = cy + (i - (modifierCount - 1) / 2.0) * modifiersGap;
                Modifiers[i] = new Point(xModifiers, y);
            }

            // ── The "back" icon, in the middle of the right edge.
            Back = new Point(width - size, cy);
        }

        public double Width { get; private set; }

        public double Height { get; private set; }

        /// <summary>Button side, in pixels.</summary>
        public double Size { get; private set; }

        /// <summary>Center of the screen (and of the photo).</summary>
        public double CenterX { get; private set; }

        public double CenterY { get; private set; }

        /// <summary>Side of the enlarged photo, in pixels.</summary>
        public double EnlargedPhotoSide { get; private set; }

        /// <summary>The eight crown slots (see CrownSlot for the order).</summary>
        public Point[] Crown { get; private set; }

        /// <summary>The three modifiers, from top to bottom.</summary>
        public Point[] Modifiers { get; private set; }

        /// <summary>The three utility buttons, from left to right.</summary>
        public Point[] Utilities { get; private set; }

        /// <summary>The "back" icon.</summary>
        public Point Back { get; private set; }

        /// <summary>The centers of the slots of a group.</summary>
        public Point[] Slots(IconGroup group)
        {
            switch (group)
            {
                case IconGroup.Crown:
                    return Crown;
                case IconGroup.Modifiers:
                    return Modifiers;
                default:
                    return Utilities;
            }
        }

        /// <summary>
        /// Center of crown slot number "num", in screen pixels.
        /// Two slots per side of the photo, clockwise starting from the top:
        ///         7   0
        ///      6         1
        ///         photo
        ///      5         2
        ///         4   3
        /// distanceX, distanceY: distance between the center of the screen and the line of the
        /// buttons of the left/right sides (X) and top/bottom sides (Y); offset: distance
        /// between the middle of a side and the center of each of its two buttons.
        /// </summary>
        private static Point CrownSlot(int num, double cx, double cy, double distanceX,
                                           double distanceY, double offset)
        {
            switch (num)
            {
                case 0: return new Point(cx + offset, cy - distanceY);
                case 1: return new Point(cx + distanceX, cy - offset);
                case 2: return new Point(cx + distanceX, cy + offset);
                case 3: return new Point(cx + offset, cy + distanceY);
                case 4: return new Point(cx - offset, cy + distanceY);
                case 5: return new Point(cx - distanceX, cy + offset);
                case 6: return new Point(cx - distanceX, cy - offset);
                default: return new Point(cx - offset, cy - distanceY);
            }
        }
    }
}
