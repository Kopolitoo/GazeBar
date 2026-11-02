using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace GazeBar
{
    /// <summary>
    /// MODEL: takes a picture of a rectangle of the screen (step 3, action choice mode).
    ///
    /// We go through GDI, the drawing library built into Windows (see NativeMethods),
    /// then convert the resulting image into a WPF image (BitmapSource) to display it.
    /// No extra library is needed.
    ///
    /// Note: the capture contains everything displayed at that moment, including GazeBar's
    /// windows. That is why action choice mode hides the overlay's circle and icon, and
    /// waits a moment, BEFORE calling Capture.
    /// </summary>
    internal static class ScreenCapture
    {
        /// <summary>
        /// Captures the rectangle (x, y, width, height), in screen pixels.
        /// Returns null on failure (the error is written to the log).
        /// </summary>
        public static BitmapSource Capture(int x, int y, int width, int height)
        {
            IntPtr screen = IntPtr.Zero;
            IntPtr memory = IntPtr.Zero;
            IntPtr image = IntPtr.Zero;
            IntPtr oldOne = IntPtr.Zero;

            try
            {
                // 1. The drawing context of the whole screen (0 = "the whole screen").
                screen = NativeMethods.GetDC(IntPtr.Zero);
                if (screen == IntPtr.Zero)
                {
                    App.Log("Capture: GetDC failed");
                    return null;
                }

                // 2. An empty image of the right size, and an in-memory context to draw into it.
                memory = NativeMethods.CreateCompatibleDC(screen);
                image = NativeMethods.CreateCompatibleBitmap(screen, width, height);
                if (memory == IntPtr.Zero || image == IntPtr.Zero)
                {
                    App.Log("Capture: cannot prepare the image in memory");
                    return null;
                }

                // 3. Put the image into the in-memory context (keep the old one to restore it).
                oldOne = NativeMethods.SelectObject(memory, image);

                // 4. Copy the pixels: from the screen (x, y) to the image (0, 0).
                if (!NativeMethods.BitBlt(memory, 0, 0, width, height, screen, x, y, NativeMethods.SRCCOPY))
                {
                    App.Log("Capture: BitBlt failed (Windows error " +
                            System.Runtime.InteropServices.Marshal.GetLastWin32Error() + ")");
                    return null;
                }

                // Remove the image from the context before converting it.
                NativeMethods.SelectObject(memory, oldOne);
                oldOne = IntPtr.Zero;

                // Conversion to a WPF image. "Freeze" makes it read-only: it can then be
                // displayed faster and shared safely.
                BitmapSource result = Imaging.CreateBitmapSourceFromHBitmap(
                    image, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                result.Freeze();
                return result;
            }
            catch (Exception ex)
            {
                App.Log("Capture: error " + ex.Message);
                return null;
            }
            finally
            {
                // 5. Give everything back to Windows, in reverse order, even on error.
                if (oldOne != IntPtr.Zero)
                {
                    NativeMethods.SelectObject(memory, oldOne);
                }
                if (image != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(image);
                }
                if (memory != IntPtr.Zero)
                {
                    NativeMethods.DeleteDC(memory);
                }
                if (screen != IntPtr.Zero)
                {
                    NativeMethods.ReleaseDC(IntPtr.Zero, screen);
                }
            }
        }
    }
}
