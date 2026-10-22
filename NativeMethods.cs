using System;
using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>
    /// Fonctions de Windows (écrites en C/C++) que l'on appelle depuis C#.
    ///
    /// Ce mécanisme s'appelle « P/Invoke » (Platform Invoke). On déclare la fonction avec
    /// [DllImport("nom_de_la_bibliothèque.dll")] et le mot-clé « extern » : c'est alors
    /// Windows qui fournit le code. La documentation de chaque fonction est sur le site
    /// de Microsoft (chercher par exemple « GetCursorPos Win32 »).
    ///
    /// « internal » = visible seulement dans ce projet. « static » = on n'a pas besoin
    /// de créer un objet NativeMethods pour s'en servir.
    /// </summary>
    internal static class NativeMethods
    {
        // ───────────────────────── Position du curseur ─────────────────────────

        /// <summary>Un point à l'écran, en pixels. Même disposition mémoire que la structure POINT de Windows.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>Lit la position actuelle du curseur de la souris (donc, ici, celle que TD Control pilote avec le regard).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out POINT point);

        /// <summary>Déplace le curseur à la position donnée (en pixels d'écran).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCursorPos(int x, int y);

        /// <summary>Taille de l'écran principal (index 0 = largeur, 1 = hauteur), en pixels.</summary>
        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int index);

        internal const int SM_CXSCREEN = 0;
        internal const int SM_CYSCREEN = 1;

        // ───────────────────────── Simulation de la souris ─────────────────────────

        /// <summary>Description d'une action de souris à simuler (structure MOUSEINPUT de Windows).</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int dx;             // déplacement en X (inutilisé ici : on ne bouge pas la souris)
            public int dy;             // déplacement en Y (inutilisé)
            public uint mouseData;     // données supplémentaires (molette...) : inutilisé
            public uint dwFlags;       // QUELLE action : bouton enfoncé, relâché...
            public uint time;          // 0 = laisser Windows mettre l'heure
            public UIntPtr dwExtraInfo;
        }

        /// <summary>Enveloppe d'une entrée simulée (structure INPUT de Windows). type = 0 pour une action de souris.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        internal const uint INPUT_MOUSE = 0;
        internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
        internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        /// <summary>Simule des actions de souris ou de clavier, comme si elles venaient du matériel.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint inputCount, INPUT[] inputs, int sizeOfInput);

        // ───────────────────────── Fenêtre qui ne prend pas le focus ─────────────────────────

        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_NOACTIVATE = 0x08000000; // la fenêtre ne devient jamais « active »
        internal const int WS_EX_TOOLWINDOW = 0x00000080; // fenêtre d'outils : absente d'Alt+Tab
        internal const int WM_MOUSEACTIVATE = 0x0021;     // message : « un clic va activer la fenêtre »
        internal const int MA_NOACTIVATE = 3;             // réponse : « non, ne l'active pas »

        // Pour la fenêtre plein écran (OverlayWindow) qui doit laisser passer les clics :
        internal const int WS_EX_TRANSPARENT = 0x00000020; // la souris « traverse » la fenêtre
        internal const int WS_EX_LAYERED = 0x00080000;     // fenêtre à transparence (déjà mis par WPF, on l'assure)

        /// <summary>
        /// Change la position, la taille ou la place « au-dessus / en dessous » d'une fenêtre.
        /// Ici on s'en sert seulement pour remettre le calque tout en haut de la pile des fenêtres.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1); // « au-dessus de tout »
        internal const uint SWP_NOSIZE = 0x0001;      // ne pas changer la taille
        internal const uint SWP_NOMOVE = 0x0002;      // ne pas déplacer
        internal const uint SWP_NOACTIVATE = 0x0010;  // ne pas donner le focus

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int SetWindowLong(IntPtr window, int index, int newValue);

        // ───────────────────────── Capture d'écran (étape 3) ─────────────────────────
        //
        // Pour photographier une partie de l'écran, on utilise GDI, la vieille bibliothèque
        // de dessin de Windows (toujours présente). Le principe, en 5 temps (voir CaptureEcran.cs) :
        //   1. GetDC(0)                       : on obtient le « contexte de dessin » de l'écran entier ;
        //   2. CreateCompatibleDC/Bitmap      : on prépare une image vide en mémoire ;
        //   3. SelectObject                   : on « pose » cette image dans un contexte en mémoire ;
        //   4. BitBlt                         : on copie le rectangle voulu de l'écran dans l'image ;
        //   5. DeleteObject/DeleteDC/ReleaseDC: on rend tout à Windows (sinon, fuite de mémoire).

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height,
                                           IntPtr source, int sourceX, int sourceY, uint operation);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr gdiObject);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(IntPtr dc);

        internal const uint SRCCOPY = 0x00CC0020;   // BitBlt : copie simple des pixels

        // ───────────────────────── Fenêtres des autres programmes (étape 4) ─────────────────────────
        //
        // Pendant la calibration, GazeBar surveille la fenêtre de l'outil Tobii pour savoir
        // quand elle se ferme (voir ModeCalibration).

        /// <summary>La fenêtre active (celle qui reçoit le clavier), ou IntPtr.Zero.</summary>
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        /// <summary>Vrai si cette fenêtre existe encore.</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        /// <summary>Vrai si cette fenêtre est visible (ni cachée, ni fermée).</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);

        /// <summary>Donne le numéro du programme (processus) auquel appartient une fenêtre.</summary>
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        /// <summary>Lit le titre d'une fenêtre (pour le journal).</summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int maxCount);

        // ───────────────────────── Touche Échap de l'aidant (étape 4) ─────────────────────────
        //
        // Un « crochet clavier » (hook) permet d'être prévenu de chaque touche appuyée, dans
        // n'importe quel programme. GazeBar se contente d'OBSERVER : la touche continue son
        // chemin normalement vers le programme actif (voir CrochetClavier.cs).

        /// <summary>La fonction appelée par Windows à chaque touche (signature imposée par Windows).</summary>
        internal delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string moduleName);

        internal const int WH_KEYBOARD_LL = 13;     // crochet clavier « bas niveau », pour tout le système
        internal const int WM_KEYDOWN = 0x0100;     // une touche est enfoncée
        internal const int WM_SYSKEYDOWN = 0x0104;  // idem, avec Alt
        internal const int VK_ESCAPE = 0x1B;        // code de la touche Échap

        // ───────────────────────── Précision de l'horloge ─────────────────────────

        /// <summary>Demande à Windows une horloge plus précise (1 ms au lieu de ~15 ms environ).</summary>
        [DllImport("winmm.dll")]
        internal static extern uint timeBeginPeriod(uint milliseconds);

        /// <summary>À appeler en fin d'utilisation, pour annuler timeBeginPeriod.</summary>
        [DllImport("winmm.dll")]
        internal static extern uint timeEndPeriod(uint milliseconds);
    }
}
