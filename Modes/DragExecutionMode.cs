using System;

namespace GazeBar
{
    /// <summary>
    /// MODE GLISSER-DÉPOSER, 3e temps : l'exécution (étape 6).
    ///
    /// On y arrive depuis la loupe, quand le point d'arrivée a été affiné. Tout se fait tout
    /// seul, en quelques dixièmes de seconde, sans regarder nulle part :
    ///
    ///  1. MASQUAGE.     Tout ce que GazeBar dessine est caché, et on attend [AttenteMs] que
    ///                   Windows l'ait effacé (sinon le glisser partirait sur l'écran noir).
    ///  2. APPUI.        Les modificateurs actifs (Ctrl, Maj, Alt) sont enfoncés, le curseur est
    ///                   placé sur le point de départ, puis le bouton est enfoncé.
    ///  3. PAUSE.        Seulement pour le glisser-déposer « android » : on attend
    ///                   [DureePauseAndroid] sans bouger, bouton enfoncé, comme un doigt posé
    ///                   sur une icône de téléphone avant de la déplacer.
    ///  4. DÉPLACEMENT.  Le curseur va du départ à l'arrivée en ligne droite, en [DureeGlisser],
    ///                   un petit pas à chaque tic (environ 60 par seconde), comme une vraie main.
    ///                   Déplacer d'un seul coup ne marcherait pas : beaucoup d'applications ne
    ///                   reconnaissent un glisser qu'après plusieurs petits mouvements.
    ///  5. DÉPÔT.        Le curseur est remis exactement sur l'arrivée, on attend
    ///                   [AttenteDepotMs] (le temps que l'application visée remarque qu'on
    ///                   survole la cible), puis le bouton est relâché, et les modificateurs aussi.
    ///
    /// Ensuite : retour au mode mouvement. Une nouvelle fixation au point d'arrivée est
    /// ignorée tant que le regard n'en est pas sorti, comme après un clic. Les modificateurs
    /// sont désactivés (« ils se désactivent après chaque action »).
    ///
    /// Sécurité : si l'on quitte ce mode avant la fin (touche Échap de l'aidant, erreur), le
    /// bouton et les touches sont relâchés dans Quitter. Le menu ne s'ouvre pas pendant ce mode.
    /// </summary>
    internal sealed class DragExecutionMode : Mode
    {
        /// <summary>Les temps du mode (voir le commentaire de la classe).</summary>
        private enum Step
        {
            Hiding,
            Pause,
            Move,
            Drop,
            Finished
        }

        /// <summary>Attente après avoir tout caché, en millisecondes (même valeur que le choix d'action).</summary>
        private const double WaitMs = 60;

        /// <summary>
        /// Attente entre l'arrivée du curseur et le relâchement, en millisecondes. Ce n'est pas
        /// un réglage du cahier des charges : c'est une précaution technique, car certaines
        /// applications (l'Explorateur, par exemple) ont besoin de « voir » le curseur immobile
        /// sur la cible un court instant avant d'accepter le dépôt.
        /// </summary>
        private const double DropWaitMs = 100;

        private readonly DragGesture _drag;

        // Le point d'arrivée, en pixels d'écran.
        private readonly double _ax;
        private readonly double _ay;

        private Step _step;
        private double _stepStartMs;

        public DragExecutionMode(DragGesture drag, double arrivalX, double arrivalY)
        {
            _drag = drag;
            _ax = arrivalX;
            _ay = arrivalY;
        }

        public override string Name
        {
            get { return "Glisser-déposer (exécution)"; }
        }

        public override void Enter(GazeState gaze)
        {
            // 1. MASQUAGE : on cache tout, et on attend un peu avant d'enfoncer le bouton.
            Manager.Layer.HideAll();
            _step = Step.Hiding;
            _stepStartMs = gaze.NowMs;
        }

        public override void Update(GazeState gaze)
        {
            double now = gaze.NowMs;
            double elapsedMs = now - _stepStartMs;
            AppSettings p = AppSettings.Current;

            switch (_step)
            {
                case Step.Hiding:
                    if (elapsedMs >= WaitMs)
                    {
                        Press();
                        // Avec la pause « android », on attend avant de bouger ; sinon, on
                        // commence le déplacement dès le prochain tic.
                        _step = _drag.WithPause ? Step.Pause : Step.Move;
                        _stepStartMs = now;
                    }
                    break;

                case Step.Pause:
                    if (elapsedMs >= p.AndroidPauseDuration * 1000.0)
                    {
                        _step = Step.Move;
                        _stepStartMs = now;
                    }
                    break;

                case Step.Move:
                    {
                        // Fraction du trajet déjà parcourue, de 0 à 1.
                        double fraction = Math.Min(1.0, elapsedMs / (p.DragDuration * 1000.0));
                        double x = _drag.StartX + (_ax - _drag.StartX) * fraction;
                        double y = _drag.StartY + (_ay - _drag.StartY) * fraction;
                        MouseInjector.MoveTo(x, y);

                        if (fraction >= 1.0)
                        {
                            // Arrivé : le curseur est remis exactement sur le point (l'arrondi
                            // de DeplacerVers peut le décaler d'un pixel).
                            NativeMethods.SetCursorPos((int)Math.Round(_ax), (int)Math.Round(_ay));
                            _step = Step.Drop;
                            _stepStartMs = now;
                        }
                    }
                    break;

                case Step.Drop:
                    if (elapsedMs >= DropWaitMs)
                    {
                        Drop();
                    }
                    break;
            }
        }

        public override void Quit()
        {
            // Si l'on part avant la fin (Échap, erreur), rien ne doit rester enfoncé.
            if (_step != Step.Finished)
            {
                MouseInjector.Release(_drag.Button);
                Modifiers.ReleaseAll();
                if (_step != Step.Hiding)
                {
                    App.Log("Glisser : interrompu avant la fin, bouton et touches relâchés");
                }
            }
        }

        /// <summary>2. APPUI : modificateurs, curseur sur le départ, bouton enfoncé.</summary>
        private void Press()
        {
            Modifiers.PressActive();

            int dx = (int)Math.Round(_drag.StartX);
            int dy = (int)Math.Round(_drag.StartY);
            NativeMethods.SetCursorPos(dx, dy);
            // Un « vrai » mouvement de souris sur place : l'application sait ainsi que la
            // souris est au-dessus de l'élément à saisir avant que le bouton ne s'enfonce.
            MouseInjector.MoveTo(dx, dy);
            NativeMethods.SetCursorPos(dx, dy);

            MouseInjector.Press(_drag.Button);
            App.Log("Glisser : " + _drag.Name + ", bouton enfoncé en (" + dx + ", " + dy +
                    "), modificateurs : " + Modifiers.Description());
        }

        /// <summary>5. DÉPÔT : bouton relâché, puis modificateurs, puis retour au mode mouvement.</summary>
        private void Drop()
        {
            MouseInjector.Release(_drag.Button);
            Modifiers.ReleaseAll();
            _step = Step.Finished;

            App.Log("Glisser : déposé en (" + _ax.ToString("F0") + ", " + _ay.ToString("F0") + ")");

            // Le mode mouvement désactive les modificateurs et ignore une nouvelle fixation
            // autour du point d'arrivée tant que le regard n'en est pas sorti.
            Manager.ChangeMode(new MovementMode(_ax, _ay));
        }
    }
}
