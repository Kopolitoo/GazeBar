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
    /// de haut en bas le long du bord droit. La fixation, elle, est oubliée dès que le regard
    /// s'éloigne de l'écran ; mais un regard JUSTE à côté de l'écran (bande étroite, voir
    /// EtatRegard.PresDeLEcran) compte encore, ramené au bord : sinon, dans les coins, les
    /// mesures qui débordent un peu empêchaient le regard fixe (02/11/2026).
    /// Regard perdu (pas de mesure) : le cercle et le curseur restent où ils sont.
    ///
    /// C'est l'un des seuls modes d'où le menu peut s'ouvrir (avec la pause, étape 6).
    ///
    /// Étape 12, clavier au regard (voir ClavierRegard) : quand il est affiché et que le
    /// regard est DESSUS, ce mode lui passe la main : le curseur ne bouge pas, le cercle est
    /// masqué, aucune fixation ne démarre, et ce sont les touches qui réagissent. Le cercle
    /// continue de suivre le regard en cachette : il réapparaît sans saut en sortant du clavier.
    /// </summary>
    internal sealed class MovementMode : Mode
    {
        private readonly FixationDetector _fixation = new FixationDetector();

        // Position (décimale) du cercle lissé, en pixels d'écran (voir Lisseur.cs).
        private readonly Smoother _smoothed = new Smoother();

        // Vrai quand le curseur est « en route » derrière le cercle ([SuiviSouris]).
        private bool _cursorOnTheWay;

        // Vrai dès que le cercle a une position (sinon, il n'est pas encore dessiné).
        private bool _placed;

        // Zone « déjà traitée » : après un clic direct, on ne relance pas une fixation au même
        // endroit tant que le regard n'en est pas sorti. Sinon, en continuant à regarder le
        // bouton qu'on vient de cliquer, on cliquerait dessus encore et encore.
        private bool _ignoredZone;
        private readonly double _zoneX;
        private readonly double _zoneY;

        /// <summary>Mode mouvement ordinaire.</summary>
        public MovementMode()
        {
        }

        /// <summary>
        /// Mode mouvement qui ignore une nouvelle fixation autour de (x, y), dans un rayon de
        /// [RayonCercle], tant que le regard n'est pas sorti de cette zone.
        /// Utilisé après un clic direct, ou une action faite à cet endroit.
        /// </summary>
        public MovementMode(double xIgnored, double yIgnored)
        {
            _ignoredZone = true;
            _zoneX = xIgnored;
            _zoneY = yIgnored;
        }

        public override string Name
        {
            get { return "Mouvement"; }
        }

        /// <summary>Le menu s'ouvre depuis le mode mouvement (regard en bas, hors de l'écran).</summary>
        public override bool MenuAllowed
        {
            get { return true; }
        }

        /// <summary>
        /// L'écran a changé (étape 11) : ce mode continue, car il redessine le cercle à chaque tic,
        /// avec les tailles du nouvel écran.
        /// </summary>
        public override bool AdaptToScreen()
        {
            return true;
        }

        public override void Enter(GazeState gaze)
        {
            // Retour au mode mouvement = fin de l'action (ou abandon) : les modificateurs
            // Ctrl, Maj, Alt du choix d'action sont désactivés (voir Modificateurs.cs).
            Modifiers.DisableAll();

            // Point de départ du cercle, sans jamais le placer « par défaut » au centre :
            //  - si un cercle est déjà affiché (on revient du mode regard fixe), on part de
            //    là où il est : il glissera en douceur vers le regard, sans saut ;
            //  - sinon, on part directement du regard (ramené dans l'écran s'il en sort) ;
            //  - si le regard n'est pas encore disponible, rien n'est dessiné : le cercle
            //    apparaîtra à la première mesure.
            if (Manager.Layer.CircleShown)
            {
                Place(Manager.Layer.CircleX, Manager.Layer.CircleY);
            }
            else if (gaze.Available)
            {
                Place(gaze.XScreen, gaze.YScreen);
            }
            else
            {
                _placed = false;
            }

            _fixation.Reset();
            Manager.Layer.HideRing();
            Manager.Layer.HideIcon();
        }

        public override void Update(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // Le regard est-il sur le clavier au regard (étape 12) ? Vrai regard, sur l'écran.
            GazeKeyboard keyboard = Manager.Keyboard;
            bool onKeyboard = gaze.OnScreen && keyboard.Contains(gaze.X, gaze.Y);

            if (gaze.Available)
            {
                // 1. Le cercle se rapproche du regard, en douceur (voir Lisseur.cs).
                //    (Première mesure reçue : il apparaît directement sur le regard.)
                //    On vise la position RAMENÉE DANS L'ÉCRAN : si le regard sort de l'écran,
                //    le cercle reste au bord et glisse le long de ce bord.
                if (!_placed)
                {
                    Place(gaze.XScreen, gaze.YScreen);
                }
                _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);

                // 2. Le curseur suit le cercle, seulement si l'option [SuiviSouris] est
                //    activée. Sinon, il ne bouge pas : il rejoindra le regard une fois fixé.
                if (p.MouseTracking && !onKeyboard)
                {
                    MoveCursor(p.CursorDeadZonePx);
                }
            }

            // 3. Le regard est-il sorti de la zone ignorée ? (Seulement avec un regard sur
            //    l'écran, comme avant : un regard hors de l'écran ne compte pas.)
            if (gaze.OnScreen && _ignoredZone &&
                Distance(gaze.X, gaze.Y, _zoneX, _zoneY) > p.CircleRadiusPx)
            {
                _ignoredZone = false;
            }

            // 4. La fixation. Dans la zone ignorée, on ne la laisse pas progresser.
            //    Un regard franchement hors de l'écran la remet à zéro ; juste à côté de
            //    l'écran, il est ramené au bord (voir DetecteurFixation.Ajouter).
            //    Pas d'anneau de progression ici (choix de l'utilisateur) : l'anneau est
            //    réservé aux boutons (icône d'options, choix d'action, barre de menu).
            _fixation.Add(gaze);
            if (_ignoredZone || onKeyboard)
            {
                // Sur le clavier, pas de fixation : ce sont les touches qui réagissent.
                _fixation.Reset();
            }

            // 4 bis. Le clavier au regard (étape 12) : touche survolée, activée… S'il n'est pas
            //        affiché, il ne fait rien.
            keyboard.Update(gaze);

            if (_fixation.Reached)
            {
                Manager.ChangeMode(new FixedGazeMode(_fixation.X, _fixation.Y));
                return;
            }

            // 5. Le dessin du cercle (dès qu'on a une position).
            //    Il est masqué au-dessus du clavier (étape 12), y compris quand le regard est
            //    sous l'écran : le cercle, bloqué au bord du bas, serait alors sur le clavier.
            if (_placed)
            {
                if (onKeyboard || keyboard.Contains(_smoothed.X, _smoothed.Y))
                {
                    Manager.Layer.HideCircle();
                }
                else
                {
                    Manager.Layer.ShowCircle(_smoothed.X, _smoothed.Y, p.CircleRadiusPx, false);
                }
            }
        }

        public override void Quit()
        {
            // Le cercle reste affiché : le mode suivant le reprend là où il est.
            Manager.Layer.HideRing();
        }

        /// <summary>Place le cercle directement en (x, y), sans lissage.</summary>
        private void Place(double x, double y)
        {
            _smoothed.Place(x, y);
            _placed = true;
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
        private void MoveCursor(double deadZone)
        {
            NativeMethods.POINT cursor;
            if (!NativeMethods.GetCursorPos(out cursor))
            {
                return;
            }

            double distance = Distance(cursor.X, cursor.Y, _smoothed.X, _smoothed.Y);
            if (!_cursorOnTheWay && distance >= deadZone)
            {
                _cursorOnTheWay = true;
            }
            if (!_cursorOnTheWay)
            {
                return;
            }

            NativeMethods.SetCursorPos((int)Math.Round(_smoothed.X), (int)Math.Round(_smoothed.Y));
            if (distance < 1)
            {
                _cursorOnTheWay = false; // rattrapé : retour au repos
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
