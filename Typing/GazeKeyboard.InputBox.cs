using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// Part of GazeKeyboard: the input box above the keys, which shows the text being typed.
    /// </summary>
    internal sealed partial class GazeKeyboard
    {
        // ───────────────────────── Input box ─────────────────────────

        /// <summary>
        /// Updates the input box preview as the application did: "deleted" Backspaces, then
        /// "addition" written. The preview is kept up to date even when the box is hidden: it
        /// is right as soon as it is shown. Only the end is kept.
        /// </summary>
        private void Preview(int deleted, string addition)
        {
            _preview.Length = Math.Max(0, _preview.Length - deleted);
            _preview.Append(addition);
            if (_preview.Length > PreviewMaxLength)
            {
                _preview.Remove(0, _preview.Length - PreviewMaxLength);
            }
        }

        /// <summary>
        /// Like Ctrl + Backspace in most applications: removes trailing spaces, then the
        /// letters of the last word.
        /// </summary>
        private void RemovePreviewWord()
        {
            int end = _preview.Length;
            while (end > 0 && char.IsWhiteSpace(_preview[end - 1]))
            {
                end--;
            }
            while (end > 0 && !char.IsWhiteSpace(_preview[end - 1]))
            {
                end--;
            }
            _preview.Length = end;
        }

        /// <summary>
        /// Writes the preview in the input box (if shown). A line end is shown as "↵". When
        /// the text is too long, only the end is shown, preceded by "…". Empty box: a small
        /// grey text explains what it is for.
        /// </summary>
        private void ShowPreview()
        {
            if (_previewText == null)
            {
                return;
            }
            if (_preview.Length == 0)
            {
                _previewText.Text = Texts.Get("keyboard.inputBoxEmpty");
                _previewText.Foreground = InputBoxEmptyText;
                return;
            }
            string text = _preview.ToString().Replace("\n", " ↵ ");
            _previewText.Foreground = InputBoxText;
            _previewText.Text = FittingEnd(text);
        }

        /// <summary>
        /// The longest end of "text" that fits in the box width (preceded by "…" if cut). The
        /// cut point is found by binary search: about ten measures instead of trying every
        /// character.
        /// </summary>
        private string FittingEnd(string text)
        {
            if (TextWidth(text) <= _previewWidthWpf)
            {
                return text;
            }
            // Find the smallest "start" for which "…" + the end from start fits.
            int bottom = 1;
            int top = text.Length;   // empty text: always fits
            while (bottom < top)
            {
                int middle = bottom + (top - bottom) / 2;
                if (TextWidth("…" + text.Substring(middle)) <= _previewWidthWpf)
                {
                    top = middle;     // it fits: maybe more text can be kept
                }
                else
                {
                    bottom = middle + 1;  // too long: cut further
                }
            }
            return "…" + text.Substring(bottom);
        }

        /// <summary>Width of a text in the input box font, in WPF units.</summary>
        private double TextWidth(string text)
        {
            FormattedText measure = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(_previewText.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                _previewText.FontSize,
                InputBoxText,
                VisualTreeHelper.GetDpi(_layer).PixelsPerDip);
            return measure.WidthIncludingTrailingWhitespace;
        }
    }
}
