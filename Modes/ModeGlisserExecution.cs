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
    internal sealed class ModeGlisserExecution : Mode
    {
        /// <summary>Les temps du mode (voir le commentaire de la classe).</summary>
        private enum Etape
        {
            Masquage,
            Pause,
            Deplacement,
            Depot,
            Termine
        }

        /// <summary>Attente après avoir tout caché, en millisecondes (même valeur que le choix d'action).</summary>
        private const double AttenteMs = 60;

        /// <summary>
        /// Attente entre l'arrivée du curseur et le relâchement, en millisecondes. Ce n'est pas
        /// un réglage du cahier des charges : c'est une précaution technique, car certaines
        /// applications (l'Explorateur, par exemple) ont besoin de « voir » le curseur immobile
        /// sur la cible un court instant avant d'accepter le dépôt.
        /// </summary>
        private const double AttenteDepotMs = 100;

        private readonly Glissement _glissement;

        // Le point d'arrivée, en pixels d'écran.
        private readonly double _ax;
        private readonly double _ay;

        private Etape _etape;
        private double _debutEtapeMs;

        public ModeGlisserExecution(Glissement glissement, double arriveeX, double arriveeY)
        {
            _glissement = glissement;
            _ax = arriveeX;
            _ay = arriveeY;
        }

        public override string Nom
        {
            get { return "Glisser-déposer (exécution)"; }
        }

        public override void Entrer(EtatRegard regard)
        {
            // 1. MASQUAGE : on cache tout, et on attend un peu avant d'enfoncer le bouton.
            Gestionnaire.Calque.MasquerTout();
            _etape = Etape.Masquage;
            _debutEtapeMs = regard.MaintenantMs;
        }

        public override void Actualiser(EtatRegard regard)
        {
            double maintenant = regard.MaintenantMs;
            double ecoule = maintenant - _debutEtapeMs;
            Parametres p = Parametres.Courants;

            switch (_etape)
            {
                case Etape.Masquage:
                    if (ecoule >= AttenteMs)
                    {
                        Appuyer();
                        // Avec la pause « android », on attend avant de bouger ; sinon, on
                        // commence le déplacement dès le prochain tic.
                        _etape = _glissement.AvecPause ? Etape.Pause : Etape.Deplacement;
                        _debutEtapeMs = maintenant;
                    }
                    break;

                case Etape.Pause:
                    if (ecoule >= p.DureePauseAndroid * 1000.0)
                    {
                        _etape = Etape.Deplacement;
                        _debutEtapeMs = maintenant;
                    }
                    break;

                case Etape.Deplacement:
                    {
                        // Fraction du trajet déjà parcourue, de 0 à 1.
                        double fraction = Math.Min(1.0, ecoule / (p.DureeGlisser * 1000.0));
                        double x = _glissement.DepartX + (_ax - _glissement.DepartX) * fraction;
                        double y = _glissement.DepartY + (_ay - _glissement.DepartY) * fraction;
                        MouseInjector.DeplacerVers(x, y);

                        if (fraction >= 1.0)
                        {
                            // Arrivé : le curseur est remis exactement sur le point (l'arrondi
                            // de DeplacerVers peut le décaler d'un pixel).
                            NativeMethods.SetCursorPos((int)Math.Round(_ax), (int)Math.Round(_ay));
                            _etape = Etape.Depot;
                            _debutEtapeMs = maintenant;
                        }
                    }
                    break;

                case Etape.Depot:
                    if (ecoule >= AttenteDepotMs)
                    {
                        Deposer();
                    }
                    break;
            }
        }

        public override void Quitter()
        {
            // Si l'on part avant la fin (Échap, erreur), rien ne doit rester enfoncé.
            if (_etape != Etape.Termine)
            {
                MouseInjector.Release(_glissement.Bouton);
                Modificateurs.RelacherTout();
                if (_etape != Etape.Masquage)
                {
                    App.Log("Glisser : interrompu avant la fin, bouton et touches relâchés");
                }
            }
        }

        /// <summary>2. APPUI : modificateurs, curseur sur le départ, bouton enfoncé.</summary>
        private void Appuyer()
        {
            Modificateurs.EnfoncerActifs();

            int dx = (int)Math.Round(_glissement.DepartX);
            int dy = (int)Math.Round(_glissement.DepartY);
            NativeMethods.SetCursorPos(dx, dy);
            // Un « vrai » mouvement de souris sur place : l'application sait ainsi que la
            // souris est au-dessus de l'élément à saisir avant que le bouton ne s'enfonce.
            MouseInjector.DeplacerVers(dx, dy);
            NativeMethods.SetCursorPos(dx, dy);

            MouseInjector.Press(_glissement.Bouton);
            App.Log("Glisser : " + _glissement.Nom + ", bouton enfoncé en (" + dx + ", " + dy +
                    "), modificateurs : " + Modificateurs.Description());
        }

        /// <summary>5. DÉPÔT : bouton relâché, puis modificateurs, puis retour au mode mouvement.</summary>
        private void Deposer()
        {
            MouseInjector.Release(_glissement.Bouton);
            Modificateurs.RelacherTout();
            _etape = Etape.Termine;

            App.Log("Glisser : déposé en (" + _ax.ToString("F0") + ", " + _ay.ToString("F0") + ")");

            // Le mode mouvement désactive les modificateurs et ignore une nouvelle fixation
            // autour du point d'arrivée tant que le regard n'en est pas sorti.
            Gestionnaire.ChangerMode(new ModeMouvement(_ax, _ay));
        }
    }
}
