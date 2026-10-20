using System;
using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>Quel bouton de la souris.</summary>
    internal enum MouseSide
    {
        Left,
        Right
    }

    /// <summary>
    /// MODÈLE : simule l'appui et le relâchement des boutons de la souris.
    ///
    /// Important : on n'envoie AUCUN déplacement. Seuls les boutons sont simulés, à
    /// l'endroit où se trouve déjà le curseur. C'est TD Control qui le déplace avec le
    /// regard : on peut donc « maintenir le bouton enfoncé » pendant que le regard
    /// se déplace, ce qui donne le glisser-déposer.
    ///
    /// Sécurité : la classe se souvient des boutons qu'elle a enfoncés, pour pouvoir
    /// les relâcher tous (ReleaseAll) à la fermeture ou en cas d'erreur. Un bouton
    /// resté coincé serait très gênant.
    ///
    /// Limite connue de Windows : un programme normal ne peut pas simuler la souris
    /// au-dessus d'une fenêtre lancée « en administrateur ». Pour agir sur une telle
    /// fenêtre, il faudrait lancer GazeBar lui aussi en administrateur.
    /// </summary>
    internal static class MouseInjector
    {
        private static readonly object Lock = new object();
        private static bool _leftDown;
        private static bool _rightDown;

        /// <summary>Enfonce le bouton (s'il ne l'est pas déjà).</summary>
        public static void Press(MouseSide side)
        {
            lock (Lock)
            {
                if (IsDown(side))
                {
                    return;
                }

                uint flag = side == MouseSide.Left
                    ? NativeMethods.MOUSEEVENTF_LEFTDOWN
                    : NativeMethods.MOUSEEVENTF_RIGHTDOWN;

                if (Send(flag))
                {
                    SetDown(side, true);
                }
            }
        }

        /// <summary>Relâche le bouton (s'il est enfoncé).</summary>
        public static void Release(MouseSide side)
        {
            lock (Lock)
            {
                if (!IsDown(side))
                {
                    return;
                }

                uint flag = side == MouseSide.Left
                    ? NativeMethods.MOUSEEVENTF_LEFTUP
                    : NativeMethods.MOUSEEVENTF_RIGHTUP;

                if (Send(flag))
                {
                    SetDown(side, false);
                }
            }
        }

        /// <summary>Relâche tous les boutons que nous avons enfoncés.</summary>
        public static void ReleaseAll()
        {
            Release(MouseSide.Left);
            Release(MouseSide.Right);
        }

        private static bool IsDown(MouseSide side)
        {
            return side == MouseSide.Left ? _leftDown : _rightDown;
        }

        private static void SetDown(MouseSide side, bool value)
        {
            if (side == MouseSide.Left)
            {
                _leftDown = value;
            }
            else
            {
                _rightDown = value;
            }
        }

        /// <summary>Envoie une action de bouton à Windows. Renvoie true si Windows l'a acceptée.</summary>
        private static bool Send(uint buttonFlag)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_MOUSE;
            input.mi.dwFlags = buttonFlag;   // les autres champs restent à 0 : pas de déplacement

            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
            uint sent = NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));

            if (sent == 0)
            {
                App.Log("SendInput a échoué (erreur Windows " + Marshal.GetLastWin32Error() + ")");
                return false;
            }
            return true;
        }
    }
}
