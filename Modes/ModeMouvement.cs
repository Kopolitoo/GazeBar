using System;

namespace GazeBar
{
    /// <summary>
    /// MODE MOUVEMENT (mode par défaut, étape 2 du cahier des charges).
    ///
    /// Un cercle de [RayonCercle] suit le regard sur le calque, lissé : à chaque tic, il
    /// parcourt une fraction [Lissage] du chemin qui le sépare du regard.
    /// Le curseur de la souris ne bouge pas, sauf si [SuiviSouris] est activé : il suit alors
    /// le cercle (sauf s'il en est à moins de [ZoneMorteCurseur] pixels, pour ne pas trembler).
    ///
    /// En même temps, un DetecteurFixation surveille le regard. Quand le regard est resté
    /// immobile pendant [DelaiFixation], on passe au mode regard fixe sur le point fixé.
    /// (Pas d'anneau de progression pendant cette attente.)
    ///
    /// Regard perdu ou hors de l'écran : le cercle et le curseur restent où ils sont, et la
    /// fixation en cours est oubliée.
    /// </summary>
    internal sealed class ModeMouvement : Mode
    {
        private readonly DetecteurFixation _fixation = new DetecteurFixation();

        // Position (décimale) du cercle lissé, en pixels d'écran.
        private double _x;
        private double _y;

        // Vrai dès que le cercle a une position (sinon, il n'est pas encore dessiné).
        private bool _place;

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

        public override void Entrer(EtatRegard regard)
        {
            // Point de départ du cercle, sans jamais le placer « par défaut » au centre :
            //  - si un cercle est déjà affiché (on revient du mode regard fixe), on part de
            //    là où il est : il glissera en douceur vers le regard, sans saut ;
            //  - sinon, on part directement du regard ;
            //  - si le regard n'est pas encore disponible, rien n'est dessiné : le cercle
            //    apparaîtra à la première mesure sur l'écran.
            if (Gestionnaire.Calque.CercleAffiche)
            {
                Placer(Gestionnaire.Calque.CercleX, Gestionnaire.Calque.CercleY);
            }
            else if (regard.SurEcran)
            {
                Placer(regard.X, regard.Y);
            }
            else
            {
                _place = false;
            }

            _fixation.Reinitialiser();
            Gestionnaire.Calque.MasquerAnneau();
            Gestionnaire.Calque.MasquerIcone();
        }

        public override void Actualiser(EtatRegard regard)
        {
            Parametres p = Parametres.Courants;

            if (regard.SurEcran)
            {
                // 1. Le cercle se rapproche du regard d'une fraction [Lissage] du chemin.
                //    (Première mesure reçue : il apparaît directement sur le regard.)
                if (!_place)
                {
                    Placer(regard.X, regard.Y);
                }
                _x += (regard.X - _x) * p.Lissage;
                _y += (regard.Y - _y) * p.Lissage;

                // 2. Le curseur suit le cercle, seulement si l'option [SuiviSouris] est
                //    activée. Sinon, il ne bouge pas : il rejoindra le regard une fois fixé.
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
            //    Pas d'anneau de progression ici (choix de l'utilisateur) : l'anneau est
            //    réservé aux boutons (icône d'options, choix d'action, barre de menu).
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

            // 5. Le dessin du cercle (dès qu'on a une position).
            if (_place)
            {
                Gestionnaire.Calque.AfficherCercle(_x, _y, p.RayonCercle, false);
            }
        }

        public override void Quitter()
        {
            // Le cercle reste affiché : le mode suivant le reprend là où il est.
            Gestionnaire.Calque.MasquerAnneau();
        }

        /// <summary>Place le cercle directement en (x, y), sans lissage.</summary>
        private void Placer(double x, double y)
        {
            _x = x;
            _y = y;
            _place = true;
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
