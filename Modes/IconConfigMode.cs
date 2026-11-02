using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// ICON CONFIGURATION MODE (step 10 of the specification).
    ///
    /// Entry: "Configure icons" button of the "Icons" tab of settings mode.
    /// Exit: "back" icon (in the middle of the right edge) or the caregiver's Escape key: back
    /// to settings mode, on the "Icons" tab. The bottom menu does not open here.
    ///
    /// Layout (user request of 2026-10-28): THE SAME as the action choice
    /// (ActionChoiceLayout), on the same black screen of the overlay. Instead of the photo, a
    /// gray frame that explains the screen. The fourteen slots are drawn: the eight of the
    /// crown, the three modifiers (left edge) and the three utility buttons (top edge). Each
    /// carries a round badge at the top right:
    ///   - red "−": an action is assigned. Looking at the icon REMOVES the action; the slot
    ///     becomes empty ("+" badge);
    ///   - green "+": the slot is empty. Looking at the icon opens THE LIST.
    ///
    /// The list: a rounded rectangle taking three quarters of the screen, with ALL the actions
    /// of the same group, each with its name underneath, and a "Cancel" button. An action can
    /// be placed several times in its group (user request of 2026-10-28), for example two left
    /// clicks in the crown. Once the action is chosen, it is placed and the list closes.
    ///
    /// Each change is applied at once (AppSettings.Current) and saved in settings.json: the
    /// next action choice uses the new layout.
    ///
    /// After each change, the screen is redrawn and the icon under the gaze is blocked until
    /// the gaze has left it (ButtonAimer.ResetAndBlock): otherwise, by keeping looking at an
    /// icon just emptied, the list would open again at once.
    ///
    /// This mode is drawn on the overlay, which lets the mouse through: it is driven by the
    /// gaze only (the caregiver can leave it with Escape).
    /// </summary>
    internal sealed class IconConfigMode : Mode
    {
        /// <summary>Proportion of the screen taken by the list (three quarters, in width and height).</summary>
        private const double ListRatio = 0.75;

        /// <summary>Maximum number of columns of the list (eight actions → two rows of four).</summary>
        private const int ListColumns = 4;

        // ── Colors.
        private static readonly Brush PreviewBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x20, 0x21, 0x24)));
        private static readonly Brush ListBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x26)));
        private static readonly Brush Soft = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAD)));
        private static readonly Brush Veil = Freeze(new SolidColorBrush(Color.FromArgb(0xC0, 0x00, 0x00, 0x00)));

        // The buttons shown, and what each one does (same order).
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly List<Action> _actions = new List<Action>();

        // Decides which button is looked at and when it activates (class shared with the menu).
        private readonly ButtonAimer _aimer;

        // True as long as the screen could not be built (overlay not ready yet).
        private bool _toBuild;

        // Is the list open? And for which slot?
        private bool _listOpen;
        private IconGroup _chosenGroup;
        private int _chosenSlot;

        // The small circle showing the gaze (as in the action choice).
        private Ellipse _gazeCircleBackground;
        private Ellipse _gazeCircle;
        private readonly Smoother _smoothed = new Smoother();
        private bool _gazeCirclePlaced;

        public IconConfigMode()
        {
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get { return "Icon configuration"; }
        }

        public override void Enter(GazeState gaze)
        {
            Manager.Layer.HideAll();
            Manager.Layer.BringToFront();
            _listOpen = false;
            _toBuild = true;
            Build();
        }

        public override void Update(GazeState gaze)
        {
            if (_toBuild)
            {
                Build();
                return;
            }

            AppSettings p = AppSettings.Current;

            // In the list, each action already has its name written underneath: the hover label
            // would overlap this name, so it is not shown.
            bool labels = p.ShowLabels && !_listOpen;
            int active = _aimer.Update(gaze, p.ActivationDelay, labels);
            if (active >= 0)
            {
                App.Log("Icon configuration: " + _buttons[active].Label);
                _actions[active]();
                return;
            }

            MoveGazeCircle(gaze, p);
        }

        /// <summary>
        /// The screen changed (step 11): we stay here, and the whole screen is redrawn on the
        /// next tick with the new sizes (the list stays open if it was).
        /// </summary>
        public override bool AdaptToScreen()
        {
            _toBuild = true;
            return true;
        }

        public override void Quit()
        {
            Manager.Layer.HideChoiceScreen();
            Manager.Layer.ChoiceScreen.Children.Clear();
            _buttons.Clear();
            _actions.Clear();
            _gazeCircle = null;
            _gazeCircleBackground = null;
        }

        /// <summary>
        /// "Back" icon or Escape key (also called by App): back to settings mode, on the "Icons"
        /// tab.
        /// </summary>
        public void BackToSettings()
        {
            App.Log("Icon configuration: back to settings");
            Manager.ChangeMode(new SettingsMode(SettingsCatalog.IconsTabIndex));
        }

        // ───────────────────────── Actions ─────────────────────────

        /// <summary>Assigned icon ("−" badge): the action is removed, the slot becomes empty.</summary>
        private void Remove(IconGroup group, int num)
        {
            AppSettings p = AppSettings.Current;
            p.SetSlot(group, num, null);
            p.Save();
            App.Log("Icon configuration: " + group + " " + num + " emptied");
            Build();
        }

        /// <summary>Empty icon ("+" badge): opens the list of actions to place.</summary>
        private void OpenList(IconGroup group, int num)
        {
            _listOpen = true;
            _chosenGroup = group;
            _chosenSlot = num;
            Build();
        }

        /// <summary>An action of the list: it is placed, the list closes.</summary>
        private void PlaceAction(ChoiceAction action)
        {
            AppSettings p = AppSettings.Current;
            p.SetSlot(_chosenGroup, _chosenSlot, action);
            p.Save();
            App.Log("Icon configuration: " + action + " placed at " + _chosenGroup + " " + _chosenSlot);
            _listOpen = false;
            Build();
        }

        /// <summary>"Cancel" in the list: nothing changes, the list closes.</summary>
        private void CloseList()
        {
            _listOpen = false;
            Build();
        }

        // ───────────────────────── Building the screen ─────────────────────────

        /// <summary>
        /// (Re)draws the whole screen: the layout, and the list on top if it is open.
        /// All positions are computed in screen pixels, then converted by the overlay.
        /// </summary>
        private void Build()
        {
            OverlayWindow layer = Manager.Layer;
            if (!layer.Ready)
            {
                _toBuild = true;   // we will try again on the next tick
                return;
            }
            _toBuild = false;

            AppSettings p = AppSettings.Current;
            double width = Manager.ScreenWidth;
            double height = Manager.ScreenHeight;
            double size = p.IconSizePx;

            // The photo of the action choice has the same side: 2 × [CircleRadius], at most the screen.
            double photoSide = Math.Min(Math.Round(2 * p.CircleRadiusPx), Math.Min(width, height));
            ActionChoiceLayout placed = new ActionChoiceLayout(width, height, size, photoSide);

            layer.PrepareChoiceScreen();
            Canvas screen = layer.ChoiceScreen;
            _buttons.Clear();
            _actions.Clear();

            // When the list is open, the layout is drawn behind it, but its icons are not gaze
            // targets (only those of the list are).
            bool targets = !_listOpen;

            BuildPreview(layer, screen, placed);

            foreach (IconGroup group in new[] { IconGroup.Crown, IconGroup.Utilities, IconGroup.Modifiers })
            {
                ChoiceAction?[] layout = p.Layout(group);
                Point[] slots = placed.Slots(group);
                for (int i = 0; i < layout.Length; i++)
                {
                    IconGroup g = group;   // copies for the functions below
                    int num = i;            // (otherwise they would see the last values)
                    GazeButton button;
                    Action action;
                    if (layout[i].HasValue)
                    {
                        button = ActionCatalog.CreateButton(layout[i].Value);
                        button.SetConfigBadge(false);
                        action = () => Remove(g, num);
                    }
                    else
                    {
                        button = GazeButton.ConfigurableEmpty(Texts.Get("iconConfig.emptySlot"));
                        button.SetConfigBadge(true);
                        action = () => OpenList(g, num);
                    }
                    AddButton(layer, screen, button, action, slots[i].X, slots[i].Y, size, targets);
                }
            }

            AddButton(layer, screen, ActionCatalog.CreateButton(ChoiceAction.Back), BackToSettings,
                          placed.Back.X, placed.Back.Y, size, targets);

            if (_listOpen)
            {
                BuildList(layer, screen, placed, p);
            }

            // The small gaze circle, added LAST to be on top of everything.
            _gazeCircleBackground = new Ellipse();
            _gazeCircleBackground.Stroke = Brushes.Black;
            _gazeCircleBackground.StrokeThickness = ScreenMetrics.Units(4);
            _gazeCircleBackground.Opacity = 0.6;
            _gazeCircleBackground.Visibility = Visibility.Collapsed;
            _gazeCircle = new Ellipse();
            _gazeCircle.Stroke = Brushes.LimeGreen;
            _gazeCircle.StrokeThickness = ScreenMetrics.Units(2);
            _gazeCircle.Visibility = Visibility.Collapsed;
            screen.Children.Add(_gazeCircleBackground);
            screen.Children.Add(_gazeCircle);

            layer.ShowChoiceScreen();

            // The buttons were just replaced: the one under the gaze does not activate until
            // the gaze has left it.
            _aimer.ResetAndBlock();
        }

        /// <summary>
        /// Instead of the photo: a gray square of the same size, with the screen title and a
        /// reminder of the role of the badges.
        /// </summary>
        private static void BuildPreview(OverlayWindow layer, Canvas screen, ActionChoiceLayout placed)
        {
            double side = placed.EnlargedPhotoSide;
            Point corner = layer.ToWpf(placed.CenterX - side / 2, placed.CenterY - side / 2);
            double sideWpf = layer.LengthToWpf(side);
            double titleFont = layer.LengthToWpf(placed.Size * 0.26);
            double textFont = layer.LengthToWpf(placed.Size * 0.18);

            StackPanel texts = new StackPanel();
            texts.VerticalAlignment = VerticalAlignment.Center;
            texts.Margin = new Thickness(sideWpf * 0.06);
            texts.Children.Add(Text(Texts.Get("iconConfig.title"), titleFont, Brushes.White, true));
            TextBlock plus = Text(Texts.Get("iconConfig.plusHelp"), textFont, Soft, false);
            plus.Margin = new Thickness(0, textFont, 0, 0);
            texts.Children.Add(plus);
            texts.Children.Add(Text(Texts.Get("iconConfig.minusHelp"), textFont, Soft, false));

            Border frame = new Border();
            frame.Width = sideWpf;
            frame.Height = sideWpf;
            frame.Background = PreviewBackground;
            frame.BorderBrush = Brushes.Gray;
            frame.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            frame.Child = texts;
            Canvas.SetLeft(frame, corner.X);
            Canvas.SetTop(frame, corner.Y);
            screen.Children.Add(frame);
        }

        /// <summary>
        /// The list: a dark veil over the whole screen, then a rounded rectangle (three quarters
        /// of the screen) with a title, all the actions of the group in a grid (four columns at
        /// most), each with its name, and "Cancel" at the bottom.
        /// </summary>
        private void BuildList(OverlayWindow layer, Canvas screen, ActionChoiceLayout placed, AppSettings p)
        {
            double size = placed.Size;
            double space = ActionChoiceLayout.Space;

            // ── The veil, which darkens the layout behind the list.
            Rectangle veil = new Rectangle();
            veil.Fill = Veil;
            veil.Width = screen.Width;
            veil.Height = screen.Height;
            Canvas.SetLeft(veil, 0);
            Canvas.SetTop(veil, 0);
            screen.Children.Add(veil);

            // ── The rounded rectangle, centered.
            double l = placed.Width * ListRatio;
            double h = placed.Height * ListRatio;
            double left = (placed.Width - l) / 2;
            double top = (placed.Height - h) / 2;
            Point corner = layer.ToWpf(left, top);
            Border frame = new Border();
            frame.Width = layer.LengthToWpf(l);
            frame.Height = layer.LengthToWpf(h);
            frame.CornerRadius = new CornerRadius(layer.LengthToWpf(size * 0.3));
            frame.Background = ListBackground;
            frame.BorderBrush = Brushes.White;
            frame.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            Canvas.SetLeft(frame, corner.X);
            Canvas.SetTop(frame, corner.Y);
            screen.Children.Add(frame);

            // ── The title, at the top of the rectangle.
            // The texts of the list also follow the Windows text size (step 11).
            double titleFont = size * 0.3 * ScreenMetrics.WindowsText;
            AddCenteredText(layer, screen, Texts.Get("iconConfig.chooseAction"), placed.CenterX, top + size * 0.35,
                               l, titleFont, Brushes.White, true);

            // ── "Cancel", at the bottom of the rectangle, with its name underneath.
            //    E711 = cross (cancel).
            double nameFont = size * 0.2 * ScreenMetrics.WindowsText;
            double yCancel = top + h - size / 2 - nameFont * 2.5;
            AddButton(layer, screen, new GazeButton("", null, Texts.Get("common.cancel")), CloseList,
                          placed.CenterX, yCancel, size, true);
            AddCenteredText(layer, screen, Texts.Get("common.cancel"), placed.CenterX, yCancel + size / 2 + nameFont * 0.4,
                               size * 3, nameFont, Soft, false);

            // ── All the actions of the group, even those already placed elsewhere: an action
            //    may appear several times (user request of 2026-10-28).
            List<ChoiceAction> free = new List<ChoiceAction>(ActionCatalog.Actions(_chosenGroup));

            // The grid zone: between the title and "Cancel".
            double gridTop = top + size * 0.35 + titleFont * 2;
            double gridBottom = yCancel - size / 2 - space / 2;

            if (free.Count == 0)
            {
                // Should not happen: each group has at least one action.
                AddCenteredText(layer, screen, Texts.Get("iconConfig.noAction"),
                                   placed.CenterX, (gridTop + gridBottom) / 2, l, nameFont * 1.3, Soft, false);
                return;
            }

            int columns = Math.Min(free.Count, ListColumns);
            int rows = (free.Count + columns - 1) / columns;
            // The grid takes the whole room of the rectangle (user request of 2026-10-28: the
            // icons looked small in the middle of a lot of space).
            // Each action gets a "cell": the width of the rectangle shared between the columns,
            // and the height between the title and "Cancel" shared between the rows.
            // The icon and its name are centered in their cell.
            double stepX = l / columns;
            double stepY = Math.Max((gridBottom - gridTop) / rows, size + nameFont * 2 + space * 0.5);
            // Center of the icon of the first row: the middle of the cell, raised by half the
            // height of the name (the name is written under the icon).
            double yFirst = (gridTop + gridBottom) / 2 - (rows - 1) * stepY / 2 - nameFont;

            for (int i = 0; i < free.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                // The last row, if incomplete, is centered too.
                int inRow = Math.Min(columns, free.Count - row * columns);
                double x = placed.CenterX + (column - (inRow - 1) / 2.0) * stepX;
                double y = yFirst + row * stepY;

                ChoiceAction action = free[i];   // copy for the function below
                AddButton(layer, screen, ActionCatalog.CreateButton(action), () => PlaceAction(action),
                              x, y, size, true);
                AddCenteredText(layer, screen, ActionCatalog.ShortName(action), x, y + size / 2 + nameFont * 0.4,
                                   stepX - space, nameFont, Brushes.White, false);
            }
        }

        // ───────────────────────── Small tools ─────────────────────────

        /// <summary>
        /// Places a button (centered on x, y in screen pixels). target = true: the gaze can
        /// activate it, and "action" is then called; false: it is only drawn.
        /// </summary>
        private void AddButton(OverlayWindow layer, Canvas screen, GazeButton button, Action action,
                                   double x, double y, double size, bool target)
        {
            button.ZoneFactor = ActionChoiceMode.ZoneFactor;
            button.Place(layer, screen, x, y, size);
            if (target)
            {
                _buttons.Add(button);
                _actions.Add(action);
            }
        }

        /// <summary>
        /// Writes a text horizontally centered on x, its top at y (screen pixels), over a width
        /// of "width" pixels (the text wraps beyond).
        /// </summary>
        private static void AddCenteredText(OverlayWindow layer, Canvas screen, string text, double x, double y,
                                               double width, double fontPixels, Brush color, bool bold)
        {
            TextBlock block = Text(text, layer.LengthToWpf(fontPixels), color, bold);
            block.Width = layer.LengthToWpf(width);
            block.TextAlignment = TextAlignment.Center;
            block.TextWrapping = TextWrapping.Wrap;
            Point point = layer.ToWpf(x, y);
            Canvas.SetLeft(block, point.X - block.Width / 2);
            Canvas.SetTop(block, point.Y);
            screen.Children.Add(block);
        }

        private static TextBlock Text(string text, double size, Brush color, bool bold)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontSize = size;
            block.Foreground = color;
            block.FontFamily = new FontFamily("Segoe UI");
            block.TextWrapping = TextWrapping.Wrap;
            if (bold)
            {
                block.FontWeight = FontWeights.Bold;
            }
            return block;
        }

        /// <summary>
        /// Moves the small gaze circle: it follows the gaze smoothly ([Smoothing]), as in the
        /// action choice. Its radius is half of [CircleRadius]. Gaze off screen: it slides
        /// along the edge. Gaze lost: it stays where it is.
        /// </summary>
        private void MoveGazeCircle(GazeState gaze, AppSettings p)
        {
            if (_gazeCircle == null || !gaze.Available)
            {
                return;
            }

            // Position brought back into the screen: gaze off screen → the circle slides along
            // the edge (see GazeState.XScreen).
            if (!_gazeCirclePlaced)
            {
                _smoothed.Place(gaze.XScreen, gaze.YScreen);
                _gazeCirclePlaced = true;
            }
            _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);

            OverlayWindow layer = Manager.Layer;
            Point center = layer.ToWpf(_smoothed.X, _smoothed.Y);
            double radius = layer.LengthToWpf(p.CircleRadiusPx / 2);
            PlaceEllipse(_gazeCircleBackground, center, radius);
            PlaceEllipse(_gazeCircle, center, radius);
        }

        private static void PlaceEllipse(Ellipse ellipse, Point center, double radius)
        {
            ellipse.Width = radius * 2;
            ellipse.Height = radius * 2;
            Canvas.SetLeft(ellipse, center.X - radius);
            Canvas.SetTop(ellipse, center.Y - radius);
            ellipse.Visibility = Visibility.Visible;
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
