using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : fait réapparaître le curseur quand Windows l'a caché (correction du 29/10/2026).
    ///
    /// Le problème observé : GazeBar lancé seul au démarrage de Windows, le curseur reste
    /// invisible ; il n'apparaît qu'après un glisser-déposer.
    ///
    /// L'explication : quand Windows pense qu'il n'y a pas de souris (aucune souris physique
    /// n'a bougé depuis le démarrage, ou on vient de toucher un écran tactile), il « supprime »
    /// le curseur. Il ne le remontre qu'au prochain mouvement d'une souris. Or le mode
    /// mouvement place le curseur avec SetCursorPos : pour Windows, ce n'est PAS un mouvement
    /// de souris, juste un changement de position. Le glisser-déposer, lui, passe par
    /// SendInput (MouseInjector.DeplacerVers), qui imite une vraie souris : voilà pourquoi
    /// il faisait réapparaître le curseur.
    ///
    /// La solution : toutes les deux secondes, on demande à Windows l'état du curseur
    /// (GetCursorInfo). S'il est « supprimé », on simule une toute petite poussée de souris
    /// par SendInput (1 pixel à droite puis 1 pixel à gauche), puis on remet le curseur
    /// exactement à sa place avec SetCursorPos. Le curseur réapparaît sans bouger.
    ///
    /// Pourquoi surveiller en continu et pas seulement au démarrage ? À l'ouverture de la
    /// session, le bureau n'est pas toujours prêt quand GazeBar démarre : une poussée trop tôt
    /// pourrait ne servir à rien. Et si le curseur est supprimé plus tard (écran tactile par
    /// exemple), il réapparaît aussi.
    ///
    /// Un logiciel qui cache volontairement le curseur (lecteur vidéo en plein écran, jeu)
    /// n'est pas concerné : Windows ne met pas le drapeau « supprimé » dans ce cas, on ne
    /// pousse donc rien (sinon, par exemple, les commandes de la vidéo réapparaîtraient
    /// toutes les deux secondes).
    /// </summary>
    internal sealed class ReveilCurseur
    {
        private readonly DispatcherTimer _minuteur;

        // Vrai si l'on a déjà noté « curseur supprimé » dans le journal : on ne l'écrit qu'une
        // fois par épisode, pour ne pas remplir le journal toutes les deux secondes.
        private bool _dejaNote;

        public ReveilCurseur()
        {
            _minuteur = new DispatcherTimer();
            _minuteur.Interval = TimeSpan.FromSeconds(2);
            _minuteur.Tick += (sender, e) => Verifier();
        }

        /// <summary>Vérifie une première fois tout de suite, puis toutes les deux secondes.</summary>
        public void Demarrer()
        {
            Verifier();
            _minuteur.Start();
        }

        public void Arreter()
        {
            _minuteur.Stop();
        }

        /// <summary>Si Windows a supprimé le curseur, simule une petite poussée de souris.</summary>
        private void Verifier()
        {
            NativeMethods.CURSORINFO info = new NativeMethods.CURSORINFO();
            info.cbSize = Marshal.SizeOf(typeof(NativeMethods.CURSORINFO));
            if (!NativeMethods.GetCursorInfo(ref info))
            {
                return;   // lecture impossible (par exemple écran de verrouillage) : on réessaiera
            }

            // « & » teste si le drapeau CURSOR_SUPPRESSED fait partie de flags.
            bool supprime = (info.flags & NativeMethods.CURSOR_SUPPRESSED) != 0;
            if (!supprime)
            {
                _dejaNote = false;
                return;
            }

            if (!_dejaNote)
            {
                App.Log("Curseur caché par Windows : petite poussée de souris simulée pour le montrer");
                _dejaNote = true;
            }

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
