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

        /// <summary>Lit la position actuelle du curseur de la souris (donc, ici, celle que le logiciel de l'eyetracker pilote avec le regard).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out POINT point);

        /// <summary>Déplace le curseur à la position donnée (en pixels d'écran).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCursorPos(int x, int y);

        /// <summary>
        /// État du curseur (structure CURSORINFO de Windows). Correction du 29/10/2026 :
        /// on lit surtout « flags », pour savoir si Windows a caché le curseur.
        /// cbSize doit contenir la taille de la structure avant l'appel (Windows la vérifie).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        /// <summary>Lit l'état du curseur : visible, caché, « supprimé » par Windows…</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorInfo(ref CURSORINFO info);

        /// <summary>
        /// Drapeau de CURSORINFO.flags : Windows a « supprimé » le curseur (Windows 8 et plus).
        /// Cela arrive quand Windows pense qu'il n'y a pas de souris : après un toucher sur un
        /// écran tactile, ou au démarrage si aucune souris physique n'a encore bougé. Le
        /// curseur reste alors invisible jusqu'au prochain mouvement d'une « vraie » souris.
        /// À ne pas confondre avec un curseur caché volontairement par un logiciel (vidéo en
        /// plein écran, jeu) : dans ce cas, ce drapeau n'est pas mis.
        /// </summary>
        internal const int CURSOR_SUPPRESSED = 0x00000002;

        /// <summary>Taille de l'écran principal (index 0 = largeur, 1 = hauteur), en pixels.</summary>
        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int index);

        internal const int SM_CXSCREEN = 0;
        internal const int SM_CYSCREEN = 1;

        /// <summary>
        /// Taille RÉELLE de l'écran principal, en pixels (étape 11, correction du 29/10/2026).
        ///
        /// Pourquoi pas GetSystemMetrics ? Essai de l'utilisateur : GazeBar lancé en
        /// 1920 × 1080 à 100 %, puis écran passé en 3840 × 2160 à 200 % : GetSystemMetrics
        /// continuait de répondre 1920 × 1080. Windows « fige » ses réponses à la mise à
        /// l'échelle du démarrage du programme : 3840 pixels à 200 %, vus avec l'échelle du
        /// démarrage (100 %), font 3840 × 100 ÷ 200 = 1920. Le changement n'était donc pas vu,
        /// et le calque ne couvrait plus que le quart en haut à gauche de l'écran.
        ///
        /// EnumDisplaySettings, lui, donne le « mode d'affichage » en cours de l'écran : sa vraie
        /// résolution, quelle que soit la mise à l'échelle. En cas d'échec, on se rabat sur
        /// GetSystemMetrics.
        /// </summary>
        internal static void PrimaryScreenSize(out double width, out double height)
        {
            DEVMODE mode = new DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            // null = l'écran principal ; ENUM_CURRENT_SETTINGS = le mode utilisé en ce moment.
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref mode) &&
                mode.dmPelsWidth > 0 && mode.dmPelsHeight > 0)
            {
                width = mode.dmPelsWidth;
                height = mode.dmPelsHeight;
                return;
            }
            width = GetSystemMetrics(SM_CXSCREEN);
            height = GetSystemMetrics(SM_CYSCREEN);
        }

        private const int ENUM_CURRENT_SETTINGS = -1;

        /// <summary>
        /// La mise à l'échelle « système » du programme, en points par pouce (96 = 100 %,
        /// 144 = 150 %). Elle est fixée au démarrage du programme et ne change plus ensuite
        /// (étape 11, voir Ecran.EchelleDemarrage). Windows 10 version 1607 et plus.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern uint GetDpiForSystem();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettings(string screenName, int modeNumber, ref DEVMODE mode);

        /// <summary>
        /// Description d'un mode d'affichage (structure DEVMODE de Windows). Seuls dmSize (à
        /// remplir avant l'appel), dmPelsWidth et dmPelsHeight (la résolution) servent ici ;
        /// les autres champs doivent quand même être là, dans cet ordre, pour que la structure
        /// ait exactement la taille attendue par Windows.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        // ───────────────────────── Simulation de la souris et du clavier ─────────────────────────

        /// <summary>Description d'une action de souris à simuler (structure MOUSEINPUT de Windows).</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int dx;             // position ou déplacement en X (voir MOUSEEVENTF_ABSOLUTE)
            public int dy;             // position ou déplacement en Y
            public uint mouseData;     // données supplémentaires : la quantité de molette (étape 8), sinon 0
            public uint dwFlags;       // QUELLE action : bouton enfoncé, relâché, déplacement...
            public uint time;          // 0 = laisser Windows mettre l'heure
            public UIntPtr dwExtraInfo;
        }

        /// <summary>
        /// Description d'une touche du clavier à simuler (structure KEYBDINPUT de Windows).
        /// Ajoutée à l'étape 6 pour les modificateurs Ctrl, Maj et Alt.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            public ushort wVk;         // code de la touche (VK_CONTROL, VK_SHIFT…)
            public ushort wScan;       // code matériel : inutilisé ici (0)
            public uint dwFlags;       // 0 = touche enfoncée, KEYEVENTF_KEYUP = touche relâchée
            public uint time;          // 0 = laisser Windows mettre l'heure
            public UIntPtr dwExtraInfo;
        }

        /// <summary>
        /// Une « union » : la souris ET le clavier sont rangés au MÊME endroit de la mémoire
        /// (FieldOffset(0) pour les deux). Windows lit l'un ou l'autre selon INPUT.type.
        /// C'est ainsi que la structure INPUT est définie en C ; la reproduire exactement permet
        /// à Windows de trouver la bonne taille (SendInput la vérifie).
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        internal struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mid;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        /// <summary>
        /// Enveloppe d'une entrée simulée (structure INPUT de Windows).
        /// type = INPUT_MOUSE (0) pour la souris, INPUT_KEYBOARD (1) pour le clavier.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        internal const uint INPUT_MOUSE = 0;
        internal const uint INPUT_KEYBOARD = 1;
        internal const uint MOUSEEVENTF_MOVE = 0x0001;
        internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
        internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        internal const uint MOUSEEVENTF_WHEEL = 0x0800;    // molette verticale (étape 8) : quantité dans mouseData
        internal const uint MOUSEEVENTF_HWHEEL = 0x1000;   // molette horizontale (étape 8)
        internal const uint MOUSEEVENTF_ABSOLUTE = 0x8000; // dx, dy = position sur l'écran (0 à 65535), pas un écart
        internal const uint KEYEVENTF_KEYUP = 0x0002;

        // Codes des touches modificatrices (« virtual-key codes »).
        internal const ushort VK_SHIFT = 0x10;    // Maj
        internal const ushort VK_CONTROL = 0x11;  // Ctrl
        internal const ushort VK_MENU = 0x12;     // Alt (Windows l'appelle « menu »)
        internal const ushort VK_C = 0x43;        // touche C (copier, avec Ctrl)
        internal const ushort VK_V = 0x56;        // touche V (coller, avec Ctrl)
        internal const ushort VK_X = 0x58;        // touche X (couper, avec Ctrl)

        // Étape 12 (clavier au regard).
        //  KEYEVENTF_EXTENDEDKEY : touche « étendue » (flèches, Début, Fin, Suppr, Page…,
        //                          touche Windows) : sans ce drapeau, certaines applications
        //                          les confondent avec les touches du pavé numérique ;
        //  KEYEVENTF_UNICODE     : on envoie un CARACTÈRE (dans wScan) et non une touche.
        //                          Windows le livre tel quel à l'application, quelle que soit
        //                          la disposition du clavier réglée dans Windows.
        internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        internal const uint KEYEVENTF_UNICODE = 0x0004;
        internal const ushort VK_BACK = 0x08;     // Retour arrière (effacer)
        internal const ushort VK_LWIN = 0x5B;     // touche Windows de gauche

        /// <summary>
        /// Donne la touche (et les modificateurs) qui produit un caractère avec la disposition
        /// du clavier de Windows. Octet bas = code de la touche ; octet haut = 1 Maj, 2 Ctrl,
        /// 4 Alt. Renvoie -1 si aucune touche ne produit ce caractère.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern short VkKeyScanW(char character);

        /// <summary>Convertit un code de touche en « code matériel » (mode 0 = MAPVK_VK_TO_VSC).</summary>
        [DllImport("user32.dll")]
        internal static extern uint MapVirtualKeyW(uint code, uint mode);

        /// <summary>Simule des actions de souris ou de clavier, comme si elles venaient du matériel.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint inputCount, INPUT[] inputs, int sizeOfInput);

        // ───────────────────────── Fenêtre qui ne prend pas le focus ─────────────────────────

        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_NOACTIVATE = 0x08000000; // la fenêtre ne devient jamais « active »
        internal const int WS_EX_TOOLWINDOW = 0x00000080; // fenêtre d'outils : absente d'Alt+Tab
        internal const int WM_MOUSEACTIVATE = 0x0021;     // message : « un clic va activer la fenêtre »
        internal const int MA_NOACTIVATE = 3;             // réponse : « non, ne l'active pas »

        // Messages envoyés par Windows à toutes les fenêtres (étape 11, voir OverlayWindow.Crochet) :
        internal const int WM_SETTINGCHANGE = 0x001A;     // un réglage de Windows a changé
        internal const int WM_DISPLAYCHANGE = 0x007E;     // la résolution de l'écran a changé

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
        // Pendant la calibration, GazeBar surveille la fenêtre de l'outil de l'eyetracker pour savoir
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

        // ───────────────────────── Fenêtre sous le curseur (défilement, 02/11/2026) ─────────────────────────
        //
        // En entrant dans le mode défilement, GazeBar rend active la fenêtre qui est sous le
        // point visé : certains logiciels ignorent la molette tant que leur fenêtre n'est pas
        // active (l'utilisateur devait cliquer avant de pouvoir défiler).

        /// <summary>
        /// La fenêtre (ou la partie de fenêtre) qui se trouve à ce point de l'écran. Les fenêtres
        /// que la souris « traverse » (comme le calque de GazeBar) sont ignorées.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern IntPtr WindowFromPoint(POINT point);

        /// <summary>Remonte d'une partie de fenêtre à la fenêtre principale qui la contient.</summary>
        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr window, uint flags);

        internal const uint GA_ROOT = 2;   // GetAncestor : la fenêtre principale (tout en haut)

        /// <summary>Rend une fenêtre active (elle reçoit le clavier et, selon les réglages, la molette).</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr window);

        /// <summary>
        /// « Attache » temporairement deux fils d'exécution : ils partagent alors l'état du
        /// clavier, ce qui autorise GazeBar à changer la fenêtre active. Windows refuse sinon
        /// souvent SetForegroundWindow à un programme qui n'est pas lui-même au premier plan.
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachThreadInput(uint threadAttach, uint threadAttachTo, [MarshalAs(UnmanagedType.Bool)] bool attach);

        /// <summary>Le numéro du fil d'exécution qui appelle (ici, celui de l'affichage de GazeBar).</summary>
        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        /// <summary>Lit un réglage de Windows (ici : à quelle fenêtre va la molette).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SystemParametersInfo(uint action, uint param, ref uint value, uint winIni);

        /// <summary>
        /// Réglage Windows 10/11 « Faire défiler les fenêtres inactives lorsque je pointe dessus » :
        /// valeur 2 = la molette va à la fenêtre sous le curseur (activé), 0 = à la fenêtre active
        /// (désactivé).
        /// </summary>
        internal const uint SPI_GETMOUSEWHEELROUTING = 0x201C;

        /// <summary>Change ce même réglage (option « Défiler les fenêtres inactives », voir MoletteWindows).</summary>
        internal const uint SPI_SETMOUSEWHEELROUTING = 0x201D;

        /// <summary>Même fonction, la valeur étant passée directement à la place de son adresse (voir MoletteWindows).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SystemParametersInfo(uint action, uint param, IntPtr value, uint winIni);

        internal const uint SPIF_UPDATEINIFILE = 0x01;   // Windows enregistre le changement
        internal const uint SPIF_SENDCHANGE = 0x02;      // et prévient les programmes ouverts

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

        /// <summary>
        /// Dans la structure reçue par le crochet clavier, le champ « flags » (3e nombre, donc
        /// 8 octets après le début) : ce drapeau dit que la touche a été SIMULÉE par un
        /// programme (SendInput), et non tapée sur un vrai clavier (étape 12).
        /// </summary>
        internal const int HookFlagsOffset = 8;
        internal const int LLKHF_INJECTED = 0x10;

        // ───────────────────────── Précision de l'horloge ─────────────────────────

        /// <summary>Demande à Windows une horloge plus précise (1 ms au lieu de ~15 ms environ).</summary>
        [DllImport("winmm.dll")]
        internal static extern uint timeBeginPeriod(uint milliseconds);

        /// <summary>À appeler en fin d'utilisation, pour annuler timeBeginPeriod.</summary>
        [DllImport("winmm.dll")]
        internal static extern uint timeEndPeriod(uint milliseconds);

        // ───────────────────────── Chargement d'une DLL native ─────────────────────────

        /// <summary>
        /// Charge une DLL native (écrite en C) à partir de son chemin complet. Sert à charger à
        /// l'avance la DLL native de l'eyetracker, qui n'est pas livrée avec GazeBar (voir
        /// ChargeurEyetracker.cs). Renvoie IntPtr.Zero en cas d'échec (voir GetLastWin32Error).
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibrary(string dllPath);
    }
}
