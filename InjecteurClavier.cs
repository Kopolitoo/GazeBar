using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : simule l'appui et le relâchement des touches du clavier (étape 6).
    ///
    /// Pour l'instant, il ne sert qu'aux touches modificatrices Ctrl, Maj et Alt du choix
    /// d'action (voir Modificateurs.cs). Il fonctionne comme MouseInjector, avec SendInput.
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
