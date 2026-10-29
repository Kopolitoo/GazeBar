using System;

namespace GazeBar
{
    /// <summary>
    /// MODE MOUVEMENT (mode par défaut, étape 2 du cahier des charges).
    ///
    /// Un cercle de [RayonCercle] suit le regard sur le calque, lissé ([Lissage], voir Lisseur.cs).
    /// Le curseur de la souris ne bouge pas, sauf si [SuiviSouris] est activé : il suit alors
    /// le cercle. Pour ne pas trembler, il ne se met en route que quand le cercle s'est éloigné
    /// de plus de [ZoneMorteCurseur] pixels ; ensuite il le suit en continu jusqu'à le rattraper.
    ///
    /// En même temps, un DetecteurFixation surveille le regard. Quand le regard est resté
    /// immobile pendant [DelaiFixation], on passe au mode regard fixe sur le point fixé.
    /// (Pas d'anneau de progression pendant cette attente.)
    ///
    /// Regard hors de l'écran : le cercle continue de suivre le regard, mais bloqué aux bords
    /// de l'écran (chaque axe séparément, voir EtatRegard.XEcran). Regard au-dessus de l'écran :
    /// le cercle glisse de gauche à droite le long du bord du haut ; regard à droite : il glisse
    /// de haut en bas le long du bord droit. La fixation, elle, est oubliée : on ne passe
    /// jamais en regard fixe sur un bord à cause d'un regard hors de l'écran.
    /// Regard perdu (pas de mesure) : le cercle et le curseur restent où ils sont.
    ///
    /// C'est l'un des seuls modes d'où le menu peut s'ouvrir (avec la pause, étape 6).
    /// </summary>
    internal sealed class ModeMouvement : Mode
    {
        private readonly DetecteurFixation _fixation = new DetecteurFixation();

        // Position (décimale) du cercle lissé, en pixels d'écran (voir Lisseur.cs).
        private readonly Lisseur _lisse = new Lisseur();

        // Vrai quand le curseur est « en route » derrière le cercle ([SuiviSouris]).
        private bool _curseurEnRoute;

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

        /// <summary>Le menu s'ouvre depuis le mode mouvement (regard en bas, hors de l'écran).</summary>
        public override bool MenuAutorise
        {
            get { return true; }
        }

        /// <summary>
        /// L'écran a changé (étape 11) : ce mode continue, car il redessine le cercle à chaque tic,
        /// avec les tailles du nouvel écran.
        /// </summary>
        public override bool AdapterEcran()
        {
            return true;
        }

        public override void Entrer(EtatRegard regard)
        {
            // Retour au mode mouvement = fin de l'action (ou abandon) : les modificateurs
            // Ctrl, Maj, Alt du choix d'action sont désactivés (voir Modificateurs.cs).
            Modificateurs.ToutDesactiver();

            // Point de départ du cercle, sans jamais le placer « par défaut » au centre :
            //  - si un cercle est déjà affiché (on revient du mode regard fixe), on part de
            //    là où il est : il glissera en douceur vers le regard, sans saut ;
            //  - sinon, on part directement du regard (ramené dans l'écran s'il en sort) ;
            //  - si le regard n'est pas encore disponible, rien n'est dessiné : le cercle
            //    apparaîtra à la première mesure.
            if (Gestionnaire.Calque.CercleAffiche)
            {
                Placer(Gestionnaire.Calque.CercleX, Gestionnaire.Calque.CercleY);
            }
            else if (regard.Disponible)
            {
                Placer(regard.XEcran, regard.YEcran);
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

            if (regard.Disponible)
            {
                // 1. Le cercle se rapproche du regard, en douceur (voir Lisseur.cs).
                //    (Première mesure reçue : il apparaît directement sur le regard.)
                //    On vise la position RAMENÉE DANS L'ÉCRAN : si le regard sort de l'écran,
                //    le cercle reste au bord et glisse le long de ce bord.
                if (!_place)
                {
                    Placer(regard.XEcran, regard.YEcran);
                }
                _lisse.Suivre(regard.XEcran, regard.YEcran, regard.MaintenantMs);

                // 2. Le curseur suit le cercle, seulement si l'option [SuiviSouris] est
                //    activée. Sinon, il ne bouge pas : il rejoindra le regard une fois fixé.
                if (p.SuiviSouris)
                {
                    DeplacerCurseur(p.ZoneMorteCurseurPx);
                }
            }

            // 3. Le regard est-il sorti de la zone ignorée ? (Seulement avec un regard sur
            //    l'écran, comme avant : un regard hors de l'écran ne compte pas.)
            if (regard.SurEcran && _zoneIgnoree &&
                Distance(regard.X, regard.Y, _zoneX, _zoneY) > p.RayonCerclePx)
            {
                _zoneIgnoree = false;
            }

            // 4. La fixation. Dans la zone ignorée, on ne la laisse pas progresser.
            //    Elle utilise le VRAI regard (pas la position ramenée dans l'écran) : un regard
            //    hors de l'écran remet la fixation à zéro (voir DetecteurFixation.Ajouter).
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
                Gestionnaire.Calque.AfficherCercle(_lisse.X, _lisse.Y, p.RayonCerclePx, false);
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
            _lisse.Placer(x, y);
            _place = true;
        }

        /// <summary>
        /// Fait suivre le cercle au curseur, sans le faire trembler :
        ///  - au repos, le curseur ne bouge pas tant que le cercle en est à moins de zoneMorte
        ///    pixels (le petit tremblement du regard est ignoré) ;
        ///  - dès que le cercle s'en éloigne davantage, le curseur se met « en route » et suit le
        ///    cercle à CHAQUE tic, en continu, jusqu'à l'avoir rattrapé (moins d'un pixel).
        /// Avant, le curseur restait immobile puis sautait de zoneMorte pixels d'un coup, encore
        /// et encore : il avançait par petits bonds, ce qui donnait un mouvement saccadé.
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

            double distance = Distance(curseur.X, curseur.Y, _lisse.X, _lisse.Y);
            if (!_curseurEnRoute && distance >= zoneMorte)
            {
                _curseurEnRoute = true;
            }
            if (!_curseurEnRoute)
            {
                return;
            }

            NativeMethods.SetCursorPos((int)Math.Round(_lisse.X), (int)Math.Round(_lisse.Y));
            if (distance < 1)
            {
                _curseurEnRoute = false; // rattrapé : retour au repos
            }
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
