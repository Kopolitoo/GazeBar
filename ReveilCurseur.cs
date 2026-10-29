using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : fait réapparaître le curseur si Windows l'a caché (correction du 29/10/2026).
    ///
    /// Le problème observé : GazeBar lancé seul au démarrage de Windows, le curseur reste
    /// invisible ; il n'apparaît qu'après un glisser-déposer.
    ///
    /// L'explication : quand Windows pense qu'il n'y a pas de souris (aucune souris physique
    /// n'a bougé depuis le démarrage), il « supprime » le curseur. Il ne le remontre qu'au
    /// prochain mouvement d'une souris. Or le mode mouvement place le curseur avec
    /// SetCursorPos : pour Windows, ce n'est PAS un mouvement de souris, juste un changement
    /// de position. Le glisser-déposer, lui, passe par SendInput (MouseInjector.DeplacerVers),
    /// qui imite une vraie souris : voilà pourquoi il faisait réapparaître le curseur.
    ///
    /// La solution : au démarrage de GazeBar, on demande à Windows l'état du curseur
    /// (GetCursorInfo). S'il est « supprimé », on simule une toute petite poussée de souris
    /// par SendInput (1 pixel à droite puis 1 pixel à gauche), puis on remet le curseur
    /// exactement à sa place avec SetCursorPos. Le curseur réapparaît sans bouger.
    /// Une seule vérification, au démarrage (choix de l'utilisateur après essai, 29/10/2026).
    /// </summary>
    internal static class ReveilCurseur
    {
        /// <summary>Si Windows a supprimé le curseur, simule une petite poussée de souris.</summary>
        public static void Verifier()
        {
            NativeMethods.CURSORINFO info = new NativeMethods.CURSORINFO();
            info.cbSize = Marshal.SizeOf(typeof(NativeMethods.CURSORINFO));
            if (!NativeMethods.GetCursorInfo(ref info))
            {
                return;   // lecture impossible : on ne fait rien
            }

            // « & » teste si le drapeau CURSOR_SUPPRESSED fait partie de flags.
            bool supprime = (info.flags & NativeMethods.CURSOR_SUPPRESSED) != 0;
            if (!supprime)
            {
                return;
            }

            App.Log("Curseur caché par Windows : petite poussée de souris simulée pour le montrer");

            // La place actuelle du curseur, pour l'y remettre exactement après la poussée.
            NativeMethods.POINT avant;
            bool positionConnue = NativeMethods.GetCursorPos(out avant);

            // Poussée « relative » (comme une vraie souris) : 1 pixel à droite, puis retour.
            MouseInjector.DeplacerDe(1, 0);
            MouseInjector.DeplacerDe(-1, 0);

            // L'accélération de la souris de Windows peut fausser l'aller-retour d'un pixel :
            // on remet donc le curseur à sa place exacte.
            if (positionConnue)
            {
                NativeMethods.SetCursorPos(avant.X, avant.Y);
            }
        }
    }
}
