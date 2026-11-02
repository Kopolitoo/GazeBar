using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : photographie un rectangle de l'écran (étape 3, mode choix d'action).
    ///
    /// On passe par GDI, la bibliothèque de dessin intégrée à Windows (voir NativeMethods),
    /// puis on convertit l'image obtenue en image WPF (BitmapSource) pour pouvoir l'afficher.
    /// Aucune bibliothèque supplémentaire n'est nécessaire.
    ///
    /// Attention : la capture contient tout ce qui est affiché à ce moment, y compris les
    /// fenêtres de GazeBar. C'est pourquoi le mode choix d'action cache le cercle et l'icône
    /// du calque, et attend un instant, AVANT d'appeler Capturer.
    /// </summary>
    internal static class ScreenCapture
    {
        /// <summary>
        /// Capture le rectangle (x, y, largeur, hauteur), en pixels d'écran.
        /// Renvoie null en cas d'échec (l'erreur est notée dans le journal).
        /// </summary>
        public static BitmapSource Capture(int x, int y, int width, int height)
        {
            IntPtr screen = IntPtr.Zero;
            IntPtr memory = IntPtr.Zero;
            IntPtr image = IntPtr.Zero;
            IntPtr oldOne = IntPtr.Zero;

            try
            {
                // 1. Le contexte de dessin de l'écran entier (0 = « tout l'écran »).
                screen = NativeMethods.GetDC(IntPtr.Zero);
                if (screen == IntPtr.Zero)
                {
                    App.Log("Capture : GetDC a échoué");
                    return null;
                }

                // 2. Une image vide de la bonne taille, et un contexte en mémoire pour y dessiner.
                memory = NativeMethods.CreateCompatibleDC(screen);
                image = NativeMethods.CreateCompatibleBitmap(screen, width, height);
                if (memory == IntPtr.Zero || image == IntPtr.Zero)
                {
                    App.Log("Capture : impossible de préparer l'image en mémoire");
                    return null;
                }

                // 3. On pose l'image dans le contexte en mémoire (on garde l'ancienne pour la remettre).
                oldOne = NativeMethods.SelectObject(memory, image);

                // 4. La copie des pixels : de l'écran (x, y) vers l'image (0, 0).
                if (!NativeMethods.BitBlt(memory, 0, 0, width, height, screen, x, y, NativeMethods.SRCCOPY))
                {
                    App.Log("Capture : BitBlt a échoué (erreur Windows " +
                            System.Runtime.InteropServices.Marshal.GetLastWin32Error() + ")");
                    return null;
                }

                // On retire l'image du contexte avant de la convertir.
                NativeMethods.SelectObject(memory, oldOne);
                oldOne = IntPtr.Zero;

                // Conversion en image WPF. « Freeze » la rend non modifiable : elle peut alors
                // être affichée plus vite et partagée sans risque.
                BitmapSource result = Imaging.CreateBitmapSourceFromHBitmap(
                    image, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                result.Freeze();
                return result;
            }
            catch (Exception ex)
            {
                App.Log("Capture : erreur " + ex.Message);
                return null;
            }
            finally
            {
                // 5. On rend tout à Windows, dans l'ordre inverse, même en cas d'erreur.
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
