namespace GazeBar
{
    /// <summary>
    /// Mode provisoire de l'étape 1 (socle-modes) : affiche un cercle de test sur le calque.
    ///
    /// Le cercle est centré sur le regard BRUT (sans lissage), ce qui permet deux vérifications :
    ///  1. il s'affiche au-dessus de tout, et les clics passent quand même à travers ;
    ///  2. il est bien CENTRÉ sur l'endroit regardé, même avec une mise à l'échelle de Windows
    ///     à 125 % ou 150 % (sinon, il y a un problème de conversion DPI).
    /// Si le regard est indisponible ou hors de l'écran, le cercle reste au centre de l'écran.
    ///
    /// Ce mode sera remplacé par le mode mouvement à l'étape 2.
    /// </summary>
    internal sealed class ModeTest : Mode
    {
        public override string Nom
        {
            get { return "Test (cercle)"; }
        }

        public override void Actualiser(EtatRegard regard)
        {
            double x;
            double y;

            if (regard.SurEcran)
            {
                x = regard.X;
                y = regard.Y;
            }
            else
            {
                x = Gestionnaire.LargeurEcran / 2;
                y = Gestionnaire.HauteurEcran / 2;
            }

            Gestionnaire.Calque.AfficherCercle(x, y, Parametres.Courants.RayonCercle);
        }

        public override void Quitter()
        {
            Gestionnaire.Calque.MasquerCercle();
        }
    }
}
