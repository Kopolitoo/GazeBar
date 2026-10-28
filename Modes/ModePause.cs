namespace GazeBar
{
    /// <summary>
    /// MODE PAUSE (étape 4 du cahier des charges).
    ///
    /// Ni le cercle ni le curseur ne suivent le regard : le calque est vide, GazeBar ne fait
    /// plus rien. Pratique pour regarder une vidéo ou lire sans rien déclencher.
    ///
    /// Une seule chose reste active : regarder en bas, hors de l'écran, pendant [DelaiMenu]
    /// ouvre la barre de menu (c'est le ModeManager qui s'en charge).
    /// Depuis la pause, la barre ne montre que le bouton « Reprendre » ; activé, il met fin à
    /// la pause et affiche la barre complète (voir ModeMenu). La touche Échap de l'aidant ramène aussi au mode mouvement.
    ///
    /// Ce mode remplace le « mode inactif » provisoire des étapes 1 à 3, qui servait à la
    /// barre d'outils (retirée à l'étape 4).
    /// </summary>
    internal sealed class ModePause : Mode
    {
        public override string Nom
        {
            get { return "Pause"; }
        }

        /// <summary>Le menu s'ouvre depuis la pause : c'est la seule façon d'en sortir au regard.</summary>
        public override bool MenuAutorise
        {
            get { return true; }
        }

        public override void Entrer(EtatRegard regard)
        {
            Gestionnaire.Calque.MasquerTout();
        }
    }
}
