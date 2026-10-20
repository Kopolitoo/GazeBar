namespace GazeBar
{
    /// <summary>
    /// Mode provisoire de l'étape 1 (socle-modes) : affiche un cercle de test sur le calque.
    ///
    /// Le cercle suit le regard, lissé par FiltreRegard (fluide quand l'œil se déplace,
    /// immobile quand il fixe). Il permet de vérifier :
    ///  1. qu'il s'affiche au-dessus de tout, et que les clics passent quand même à travers ;
    ///  2. qu'il est bien CENTRÉ sur l'endroit regardé, même avec une mise à l'échelle de Windows
    ///     à 125 % ou 150 % (sinon, il y a un problème de conversion DPI).
    /// Si le regard est perdu ou hors de l'écran, le cercle reste là où il était
    /// (au centre de l'écran tant qu'aucun regard n'a été reçu).
    ///
    /// Ce mode sera remplacé par le mode mouvement à l'étape 2.
    /// </summary>
    internal sealed class ModeTest : Mode
    {
        private readonly FiltreRegard _filtre = new FiltreRegard();

        public override string Nom
        {
            get { return "Test (cercle)"; }
        }

        public override void Entrer()
        {
            // Point de départ : le centre de l'écran.
            _filtre.Placer(Gestionnaire.LargeurEcran / 2, Gestionnaire.HauteurEcran / 2);
        }

        public override void Actualiser(EtatRegard regard)
        {
            if (regard.SurEcran)
            {
                _filtre.Avancer(regard.X, regard.Y);
            }

            Gestionnaire.Calque.AfficherCercle(_filtre.X, _filtre.Y, Parametres.Courants.RayonCercle);
        }

        public override void Quitter()
        {
            Gestionnaire.Calque.MasquerCercle();
        }
    }
}
