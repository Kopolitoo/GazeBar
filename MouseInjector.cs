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
    /// MODÈLE : simule l'appui et le relâchement des boutons de la souris, et (depuis
    /// l'étape 6) le déplacement du curseur pendant un glisser-déposer.
    ///
    /// Les clics sont faits à l'endroit où se trouve déjà le curseur (les modes le placent
    /// avant avec SetCursorPos).
    ///
    /// Pour le glisser-déposer (étape 6), le curseur est déplacé par DeplacerVers, qui passe
    /// par SendInput comme une vraie souris : les applications reçoivent alors les mêmes
    /// messages « la souris bouge » qu'avec une souris physique, ce qui est nécessaire pour
    /// que l'Explorateur ou Paint reconnaissent un glisser.
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

        /// <summary>Un clic simple : appui puis relâchement, à l'endroit où est le curseur.</summary>
        public static void Clic(MouseSide side)
        {
            Press(side);
            Release(side);
        }

        /// <summary>
        /// Un double clic gauche : deux clics envoyés aussitôt l'un après l'autre.
        /// Windows les reconnaît comme un double clic s'ils arrivent dans le « délai de double
        /// clic » réglé dans Windows (500 ms par défaut) et au même endroit : c'est le cas ici,
        /// puisqu'ils partent à quelques microsecondes d'intervalle sans bouger le curseur.
        /// </summary>
        public static void DoubleClic()
        {
            Clic(MouseSide.Left);
            Clic(MouseSide.Left);
        }

        /// <summary>Relâche tous les boutons que nous avons enfoncés.</summary>
        public static void ReleaseAll()
        {
            Release(MouseSide.Left);
            Release(MouseSide.Right);
        }

        /// <summary>Vrai si l'un des deux boutons est enfoncé par GazeBar.</summary>
        public static bool AuMoinsUnEnfonce
        {
            get
            {
                lock (Lock)
                {
                    return _leftDown || _rightDown;
                }
            }
        }

        /// <summary>
        /// Déplace le curseur jusqu'au point (x, y), en pixels d'écran, comme le ferait une
        /// vraie souris (étape 6, glisser-déposer).
        ///
        /// Avec MOUSEEVENTF_ABSOLUTE, Windows n'attend pas des pixels mais une position entre
        /// 0 et 65535 sur chaque axe (0 = bord gauche ou haut, 65535 = bord droit ou bas de
        /// l'écran principal). On convertit donc : position = x × 65535 ÷ (largeur − 1).
        /// L'arrondi peut décaler le curseur d'un pixel : à la fin d'un glisser, le mode
        /// remet le curseur exactement en place avec SetCursorPos avant de relâcher.
        /// </summary>
        public static void DeplacerVers(double x, double y)
        {
            double largeur = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            double hauteur = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
            if (largeur < 2 || hauteur < 2)
            {
                return;
            }

            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_MOUSE;
            input.u.mi.dx = (int)Math.Round(x * 65535.0 / (largeur - 1));
            input.u.mi.dy = (int)Math.Round(y * 65535.0 / (hauteur - 1));
            input.u.mi.dwFlags = NativeMethods.MOUSEEVENTF_MOVE | NativeMethods.MOUSEEVENTF_ABSOLUTE;
            Envoyer(input);
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
            input.u.mi.dwFlags = buttonFlag;   // les autres champs restent à 0 : pas de déplacement
            return Envoyer(input);
        }

        /// <summary>Envoie une entrée simulée à Windows. Renvoie true si Windows l'a acceptée.</summary>
        private static bool Envoyer(NativeMethods.INPUT input)
        {
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
