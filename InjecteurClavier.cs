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
    internal static class InjecteurClavier
    {
        private static readonly object Verrou = new object();

        // Les codes des touches actuellement enfoncées par GazeBar.
        private static readonly List<ushort> Enfoncees = new List<ushort>();

        /// <summary>Enfonce la touche (si GazeBar ne l'a pas déjà enfoncée).</summary>
        public static void Enfoncer(ushort codeTouche)
        {
            lock (Verrou)
            {
                if (Enfoncees.Contains(codeTouche))
                {
                    return;
                }
                if (Envoyer(codeTouche, 0))
                {
                    Enfoncees.Add(codeTouche);
                }
            }
        }

        /// <summary>Relâche la touche (si GazeBar l'a enfoncée).</summary>
        public static void Relacher(ushort codeTouche)
        {
            lock (Verrou)
            {
                if (!Enfoncees.Contains(codeTouche))
                {
                    return;
                }
                // Même si Windows refuse, on l'oublie : on ne réessaiera pas sans fin.
                Envoyer(codeTouche, NativeMethods.KEYEVENTF_KEYUP);
                Enfoncees.Remove(codeTouche);
            }
        }

        /// <summary>Relâche toutes les touches enfoncées par GazeBar, dans l'ordre inverse de l'appui.</summary>
        public static void RelacherTout()
        {
            lock (Verrou)
            {
                for (int i = Enfoncees.Count - 1; i >= 0; i--)
                {
                    Envoyer(Enfoncees[i], NativeMethods.KEYEVENTF_KEYUP);
                }
                Enfoncees.Clear();
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
        public static void TaperTexte(string texte)
        {
            if (string.IsNullOrEmpty(texte))
            {
                return;
            }
            List<NativeMethods.INPUT> entrees = new List<NativeMethods.INPUT>();
            foreach (char c in texte)
            {
                entrees.Add(Caractere(c, 0));
                entrees.Add(Caractere(c, NativeMethods.KEYEVENTF_KEYUP));
            }
            EnvoyerTout(entrees, "texte");
        }

        /// <summary>
        /// Appuie puis relâche une vraie touche (Tab, Entrée, flèches, F1…), avec des
        /// modificateurs éventuels (Ctrl, Maj, Alt, Windows), enfoncés avant et relâchés après,
        /// dans l'ordre inverse. etendue : touche « étendue » (flèches, Début, Fin, Suppr…).
        /// </summary>
        public static void TaperTouche(ushort code, bool etendue, IList<ushort> modificateurs)
        {
            List<NativeMethods.INPUT> entrees = new List<NativeMethods.INPUT>();
            if (modificateurs != null)
            {
                foreach (ushort m in modificateurs)
                {
                    entrees.Add(Touche(m, m == NativeMethods.VK_LWIN, false));
                }
            }
            entrees.Add(Touche(code, etendue, false));
            entrees.Add(Touche(code, etendue, true));
            if (modificateurs != null)
            {
                for (int i = modificateurs.Count - 1; i >= 0; i--)
                {
                    ushort m = modificateurs[i];
                    entrees.Add(Touche(m, m == NativeMethods.VK_LWIN, true));
                }
            }
            EnvoyerTout(entrees, "touche " + code);
        }

        /// <summary>Une entrée « caractère Unicode » (appui ou relâchement).</summary>
        private static NativeMethods.INPUT Caractere(char c, uint drapeaux)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.u.ki.wVk = 0;
            input.u.ki.wScan = c;
            input.u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE | drapeaux;
            return input;
        }

        /// <summary>
        /// Une entrée « touche » (appui ou relâchement), avec son code matériel (wScan) : certaines
        /// applications lisent ce code plutôt que le code de la touche.
        /// </summary>
        private static NativeMethods.INPUT Touche(ushort code, bool etendue, bool relachement)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.u.ki.wVk = code;
            input.u.ki.wScan = (ushort)NativeMethods.MapVirtualKeyW(code, 0);
            uint drapeaux = 0;
            if (etendue)
            {
                drapeaux |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
            }
            if (relachement)
            {
                drapeaux |= NativeMethods.KEYEVENTF_KEYUP;
            }
            input.u.ki.dwFlags = drapeaux;
            return input;
        }

        /// <summary>Envoie toutes les entrées d'un coup. quoi : pour le journal en cas d'échec.</summary>
        private static void EnvoyerTout(List<NativeMethods.INPUT> entrees, string quoi)
        {
            NativeMethods.INPUT[] tableau = entrees.ToArray();
            uint envoyes = NativeMethods.SendInput((uint)tableau.Length, tableau, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            if (envoyes != tableau.Length)
            {
                // Cas connu : une fenêtre lancée en administrateur refuse les touches simulées
                // par un programme normal (étape « ui-access » de la feuille de route).
                App.Log("SendInput (" + quoi + ") : " + envoyes + " entrées sur " + tableau.Length +
                        " acceptées (erreur Windows " + Marshal.GetLastWin32Error() + ")");
            }
        }

        /// <summary>Envoie un appui (drapeaux = 0) ou un relâchement (KEYEVENTF_KEYUP) à Windows.</summary>
        private static bool Envoyer(ushort codeTouche, uint drapeaux)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.u.ki.wVk = codeTouche;
            input.u.ki.dwFlags = drapeaux;

            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
            uint envoyes = NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            if (envoyes == 0)
            {
                App.Log("SendInput (clavier) a échoué (erreur Windows " + Marshal.GetLastWin32Error() + ")");
                return false;
            }
            return true;
        }
    }
}
