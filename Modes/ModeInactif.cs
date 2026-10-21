namespace GazeBar
{
    /// <summary>
    /// Mode provisoire de l'étape 1 : le calque reste vide, rien ne suit le regard.
    /// Permet de cacher le cercle de test depuis la barre d'outils.
    /// (Le vrai mode pause, avec la sortie par le menu, arrivera à l'étape 4.)
    /// </summary>
    internal sealed class ModeInactif : Mode
    {
        public override string Nom
        {
            get { return "Inactif"; }
        }

        public override void Entrer()
        {
            Gestionnaire.Calque.MasquerCercle();
        }
    }
}
