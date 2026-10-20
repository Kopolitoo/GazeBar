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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int SetWindowLong(IntPtr window, int index, int newValue);

        // ───────────────────────── Précision de l'horloge ─────────────────────────

        /// <summary>Demande à Windows une horloge plus précise (1 ms au lieu de ~15 ms environ).</summary>
        [DllImport("winmm.dll")]
        internal static extern uint timeBeginPeriod(uint milliseconds);

        /// <summary>À appeler en fin d'utilisation, pour annuler timeBeginPeriod.</summary>
        [DllImport("winmm.dll")]
        internal static extern uint timeEndPeriod(uint milliseconds);
    }
}
