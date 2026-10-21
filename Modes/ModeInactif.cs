namespace GazeBar
{
    /// <summary>
    /// Mode provisoire : le calque reste vide, rien ne suit le regard.
    /// Le bouton « Mode mouvement » de la barre d'outils y passe pour tout arrêter ; on y passe
    /// aussi pendant un appui long, un glisser ou la « souris regard » de la barre.
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
            Gestionnaire.Calque.MasquerTout();
        }
    }
}
