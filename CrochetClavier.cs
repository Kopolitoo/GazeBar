using System;
using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : surveille la touche Échap, dans tous les programmes (étape 4).
    ///
    /// Le cahier des charges demande « toujours une sortie » : une touche du clavier (Échap)
    /// ramène au mode mouvement, pour l'aidant. C'est d'autant plus utile depuis que
    /// l'ancienne barre d'outils (qu'on pouvait cliquer à la souris) a disparu.
    ///
    /// Comment : on installe un « crochet clavier bas niveau » (WH_KEYBOARD_LL). Windows
    /// appelle alors notre fonction SurTouche à chaque touche appuyée, où qu'on soit.
    /// On se contente d'observer : la touche est toujours transmise au programme actif
    /// (CallNextHookEx). Échap continue donc de fermer une boîte de dialogue, par exemple.
    ///
    /// Windows appelle SurTouche sur le fil de l'interface (celui qui a installé le crochet),
    /// pendant qu'il traite ses messages : on peut donc changer de mode directement. Mais il
    /// faut répondre vite (sinon Windows retire le crochet) : l'événement est déclenché après
    /// coup, avec BeginInvoke.
    /// </summary>
    internal sealed class CrochetClavier : IDisposable
    {
        // Garder une référence à la fonction : sinon le « ramasse-miettes » de .NET pourrait
        // la détruire alors que Windows l'appelle encore (plantage).
        private readonly NativeMethods.LowLevelKeyboardProc _fonction;
        private IntPtr _crochet = IntPtr.Zero;

        public CrochetClavier()
        {
            _fonction = SurTouche;
        }

        /// <summary>Déclenché (sur le fil de l'interface) quand la touche Échap est enfoncée.</summary>
        public event Action EchapAppuye;

        /// <summary>Installe le crochet. Renvoie faux si Windows le refuse (noté dans le journal).</summary>
        public bool Installer()
        {
            if (_crochet != IntPtr.Zero)
            {
                return true;
            }

            // Pour un crochet bas niveau, Windows demande le « module » du programme ;
            // GetModuleHandle(null) donne celui de GazeBar.exe.
            _crochet = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL, _fonction, NativeMethods.GetModuleHandle(null), 0);

            if (_crochet == IntPtr.Zero)
            {
                App.Log("Touche Échap : crochet clavier refusé (erreur " + Marshal.GetLastWin32Error() + ")");
                return false;
            }
            App.Log("Touche Échap : surveillée");
            return true;
        }

        public void Dispose()
        {
            if (_crochet != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_crochet);
                _crochet = IntPtr.Zero;
            }
        }

        /// <summary>Appelée par Windows à chaque touche. Doit répondre très vite.</summary>
        private IntPtr SurTouche(int code, IntPtr wParam, IntPtr lParam)
        {
            // code ≥ 0 : le message est à traiter. wParam dit « enfoncée » ou « relâchée ».
            // lParam pointe vers une structure dont le premier champ est le code de la touche.
            if (code >= 0)
            {
                int message = wParam.ToInt32();
                if (message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN)
                {
                    int touche = Marshal.ReadInt32(lParam);
                    if (touche == NativeMethods.VK_ESCAPE)
                    {
                        Action abonnes = EchapAppuye;
                        if (abonnes != null)
                        {
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(abonnes);
                        }
                    }
                }
            }

            // Toujours transmettre la touche au programme suivant.
            return NativeMethods.CallNextHookEx(_crochet, code, wParam, lParam);
        }
    }
}
