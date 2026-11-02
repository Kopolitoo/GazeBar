using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : simule l'appui et le relâchement des touches du clavier (étape 6).
    ///
    /// Il sert aux touches modificatrices Ctrl, Maj et Alt du choix d'action (voir
    /// Modificateurs.cs) et, depuis l'étape 12, au clavier au regard (TaperTexte, TaperTouche).
    /// Il fonctionne comme MouseInjector, avec SendInput.
    ///
    /// Sécurité : comme pour la souris, la classe se souvient des touches qu'elle a enfoncées,
    /// pour pouvoir toutes les relâcher (RelacherTout) en cas d'erreur, d'appui sur Échap ou à
    /// la fermeture. Une touche Ctrl restée enfoncée transformerait chaque clic en Ctrl + clic.
    /// </summary>
    internal static class KeyboardInjector
    {
        private static readonly object LockState = new object();

        // Les codes des touches actuellement enfoncées par GazeBar.
        private static readonly List<ushort> Pressed = new List<ushort>();

        /// <summary>Enfonce la touche (si GazeBar ne l'a pas déjà enfoncée).</summary>
        public static void PressDown(ushort keyCode)
        {
            lock (LockState)
            {
                if (Pressed.Contains(keyCode))
                {
                    return;
                }
                if (Send(keyCode, 0))
                {
                    Pressed.Add(keyCode);
                }
            }
        }

        /// <summary>Relâche la touche (si GazeBar l'a enfoncée).</summary>
        public static void Release(ushort keyCode)
        {
            lock (LockState)
            {
                if (!Pressed.Contains(keyCode))
                {
                    return;
                }
                // Même si Windows refuse, on l'oublie : on ne réessaiera pas sans fin.
                Send(keyCode, NativeMethods.KEYEVENTF_KEYUP);
                Pressed.Remove(keyCode);
            }
        }

        /// <summary>Relâche toutes les touches enfoncées par GazeBar, dans l'ordre inverse de l'appui.</summary>
        public static void ReleaseAll()
        {
            lock (LockState)
            {
                for (int i = Pressed.Count - 1; i >= 0; i--)
                {
                    Send(Pressed[i], NativeMethods.KEYEVENTF_KEYUP);
                }
                Pressed.Clear();
            }
        }

        // ───────────────────────── Clavier au regard (étape 12) ─────────────────────────
        //
        // Ces méthodes envoient une touche complète (appui ET relâchement) en un seul appel à
        // SendInput : Windows les traite d'un bloc, sans qu'une vraie touche puisse se glisser
        // au milieu, et rien ne reste enfoncé après. Elles n'ont donc pas besoin de la liste
        // « Enfoncees » (qui sert aux modificateurs du choix d'action).

        /// <summary>
        /// Écrit un texte, caractère par caractère, en UNICODE : l'application reçoit
        /// exactement « é », « œ » ou « € », que Windows soit réglé en clavier français,
        /// anglais ou autre. C'est ce qui permettra de changer de langue sans souci.
        /// </summary>
        public static void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            List<NativeMethods.INPUT> entries = new List<NativeMethods.INPUT>();
            foreach (char c in text)
            {
                entries.Add(Character(c, 0));
                entries.Add(Character(c, NativeMethods.KEYEVENTF_KEYUP));
            }
            SendAll(entries, "texte");
        }

        /// <summary>
        /// Appuie puis relâche une vraie touche (Tab, Entrée, flèches, F1…), avec des
        /// modificateurs éventuels (Ctrl, Maj, Alt, Windows), enfoncés avant et relâchés après,
        /// dans l'ordre inverse. etendue : touche « étendue » (flèches, Début, Fin, Suppr…).
        /// </summary>
        public static void TypeKey(ushort code, bool extent, IList<ushort> modifiers)
        {
            List<NativeMethods.INPUT> entries = new List<NativeMethods.INPUT>();
            if (modifiers != null)
            {
                foreach (ushort m in modifiers)
                {
                    entries.Add(Key(m, m == NativeMethods.VK_LWIN, false));
                }
            }
            entries.Add(Key(code, extent, false));
            entries.Add(Key(code, extent, true));
            if (modifiers != null)
            {
                for (int i = modifiers.Count - 1; i >= 0; i--)
                {
                    ushort m = modifiers[i];
                    entries.Add(Key(m, m == NativeMethods.VK_LWIN, true));
                }
            }
            SendAll(entries, "touche " + code);
        }

        /// <summary>Une entrée « caractère Unicode » (appui ou relâchement).</summary>
        private static NativeMethods.INPUT Character(char c, uint flagsValue)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.u.ki.wVk = 0;
            input.u.ki.wScan = c;
            input.u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE | flagsValue;
            return input;
        }

        /// <summary>
        /// Une entrée « touche » (appui ou relâchement), avec son code matériel (wScan) : certaines
        /// applications lisent ce code plutôt que le code de la touche.
        /// </summary>
        private static NativeMethods.INPUT Key(ushort code, bool extent, bool release)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.u.ki.wVk = code;
            input.u.ki.wScan = (ushort)NativeMethods.MapVirtualKeyW(code, 0);
            uint flagsValue = 0;
            if (extent)
            {
                flagsValue |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
            }
            if (release)
            {
                flagsValue |= NativeMethods.KEYEVENTF_KEYUP;
            }
            input.u.ki.dwFlags = flagsValue;
            return input;
        }

        /// <summary>Envoie toutes les entrées d'un coup. quoi : pour le journal en cas d'échec.</summary>
        private static void SendAll(List<NativeMethods.INPUT> entries, string what)
        {
            NativeMethods.INPUT[] array = entries.ToArray();
            uint sentCount = NativeMethods.SendInput((uint)array.Length, array, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            if (sentCount != array.Length)
            {
                // Cas connu : une fenêtre lancée en administrateur refuse les touches simulées
                // par un programme normal (étape « ui-access » de la feuille de route).
                App.Log("SendInput (" + what + ") : " + sentCount + " entrées sur " + array.Length +
                        " acceptées (erreur Windows " + Marshal.GetLastWin32Error() + ")");
            }
        }

        /// <summary>Envoie un appui (drapeaux = 0) ou un relâchement (KEYEVENTF_KEYUP) à Windows.</summary>
        private static bool Send(ushort keyCode, uint flagsValue)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.u.ki.wVk = keyCode;
            input.u.ki.dwFlags = flagsValue;

            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
            uint sentCount = NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            if (sentCount == 0)
            {
                App.Log("SendInput (clavier) a échoué (erreur Windows " + Marshal.GetLastWin32Error() + ")");
                return false;
            }
            return true;
        }
    }
}
