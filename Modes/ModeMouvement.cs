using System;

namespace GazeBar
{
    /// <summary>
    /// MODE MOUVEMENT (mode par défaut, étape 2 du cahier des charges).
    ///
    /// Un cercle de [RayonCercle] suit le regard sur le calque, lissé : à chaque tic, il
    /// parcourt une fraction [Lissage] du chemin qui le sépare du regard.
    /// Si [SuiviSouris] est activé, le curseur de la souris suit le cercle (sauf s'il en est
    /// à moins de [ZoneMorteCurseur] pixels : cela évite qu'il tremble).
    ///
    /// En même temps, un DetecteurFixation surveille le regard. Pendant que le regard reste
    /// immobile, un anneau se remplit sur le bord du cercle : on voit que la fixation est en
    /// cours, et l'on peut encore regarder ailleurs pour l'annuler. Quand l'anneau est plein
    /// (après [DelaiFixation]), on passe au mode regard fixe sur le point fixé.
    ///
    /// Regard perdu ou hors de l'écran : le cercle et le curseur restent où ils sont, et la
    /// fixation en cours est oubliée.
    /// </summary>
    internal sealed class ModeMouvement : Mode
    {
        /// <summary>
        /// En dessous de cette progression, l'anneau n'est pas dessiné. Quand l'œil se déplace,
        /// la fixation repart de zéro à chaque mesure : sans ce seuil, de tout petits bouts
        /// d'anneau clignoteraient sans arrêt sur le cercle.
        /// </summary>
        private const double ProgressionMinAffichee = 0.1;

        private readonly DetecteurFixation _fixation = new DetecteurFixation();

        // Position (décimale) du cercle lissé, en pixels d'écran.
        private double _x;
        private double _y;

        // Zone « déjà traitée » : après un clic direct, on ne relance pas une fixation au même
        // endroit tant que le regard n'en est pas sorti. Sinon, en continuant à regarder le
        // bouton qu'on vient de cliquer, on cliquerait dessus encore et encore.
        private bool _zoneIgnoree;
        private readonly double _zoneX;
        private readonly double _zoneY;

        /// <summary>Mode mouvement ordinaire.</summary>
        public ModeMouvement()
        {
        }

        /// <summary>
        /// Mode mouvement qui ignore une nouvelle fixation autour de (x, y), dans un rayon de
        /// [RayonCercle], tant que le regard n'est pas sorti de cette zone.
        /// Utilisé après un clic direct, ou une action faite à cet endroit.
        /// </summary>
        public ModeMouvement(double xIgnore, double yIgnore)
        {
            _zoneIgnoree = true;
            _zoneX = xIgnore;
            _zoneY = yIgnore;
        }

        public override string Nom
        {
            get { return "Mouvement"; }
        }

        public override void Entrer()
        {
            // Le cercle part de l'endroit où se trouve le curseur (au centre de l'écran si on
            // ne peut pas le lire). Il rejoindra le regard en douceur.
            NativeMethods.POINT curseur;
            if (NativeMethods.GetCursorPos(out curseur))
            {
                _x = curseur.X;
                _y = curseur.Y;
            }
            else
            {
                _x = Gestionnaire.LargeurEcran / 2;
                _y = Gestionnaire.HauteurEcran / 2;
            }

            _fixation.Reinitialiser();
            Gestionnaire.Calque.MasquerIcone();
        }

        public override void Actualiser(EtatRegard regard)
        {
            Parametres p = Parametres.Courants;

            if (regard.SurEcran)
            {
                // 1. Le cercle se rapproche du regard d'une fraction [Lissage] du chemin.
                _x += (regard.X - _x) * p.Lissage;
                _y += (regard.Y - _y) * p.Lissage;

                // 2. Le curseur suit le cercle, si l'option est activée.
                if (p.SuiviSouris)
                {
                    DeplacerCurseur(p.ZoneMorteCurseur);
                }

                // 3. Le regard est-il sorti de la zone ignorée ?
                if (_zoneIgnoree && Distance(regard.X, regard.Y, _zoneX, _zoneY) > p.RayonCercle)
                {
                    _zoneIgnoree = false;
                }
            }

            // 4. La fixation. Dans la zone ignorée, on ne la laisse pas progresser.
            _fixation.Ajouter(regard);
            if (_zoneIgnoree)
            {
                _fixation.Reinitialiser();
            }

            if (_fixation.Atteinte)
            {
                Gestionnaire.ChangerMode(new ModeRegardFixe(_fixation.X, _fixation.Y));
                return;
            }

            // 5. Le dessin : le cercle, et l'anneau s'il y a une fixation en cours.
            Gestionnaire.Calque.AfficherCercle(_x, _y, p.RayonCercle, false);
            if (_fixation.Progression >= ProgressionMinAffichee)
            {
                Gestionnaire.Calque.AfficherAnneau(_x, _y, p.RayonCercle, _fixation.Progression);
            }
            else
            {
                Gestionnaire.Calque.MasquerAnneau();
            }
        }

        public override void Quitter()
        {
            Gestionnaire.Calque.MasquerTout();
        }

        /// <summary>
        /// Place le curseur sur le cercle, sauf s'il en est déjà à moins de zoneMorte pixels.
        /// On lit la vraie position du curseur : s'il a été déplacé par la souris entre-temps,
        /// on en tient compte.
        /// </summary>
        private void DeplacerCurseur(double zoneMorte)
        {
            NativeMethods.POINT curseur;
            if (!NativeMethods.GetCursorPos(out curseur))
            {
                return;
            }

            if (Distance(curseur.X, curseur.Y, _x, _y) < zoneMorte)
            {
                return;
            }

            NativeMethods.SetCursorPos((int)Math.Round(_x), (int)Math.Round(_y));
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
