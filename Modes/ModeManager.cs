using System;
using System.Windows.Threading;

namespace GazeBar
{
    /// <summary>
    /// LOGIQUE : le chef d'orchestre des modes.
    ///
    /// Il connaît le mode courant et le mode précédent, et c'est lui seul qui fait passer
    /// d'un mode à l'autre (méthode ChangerMode). Avoir un seul point de passage permet de
    /// toujours ranger proprement l'ancien mode, de tout noter dans le journal, et plus tard
    /// d'ajouter des animations de transition à un seul endroit.
    ///
    /// Il possède aussi l'horloge commune : environ 60 fois par seconde, il lit le regard,
    /// prépare un EtatRegard et le donne au mode courant (méthode Actualiser du mode).
    ///
    /// Les modes y trouvent tout ce dont ils ont besoin : le regard (Regard), le calque plein
    /// écran (Calque) et la taille de l'écran.
    ///
    /// Le premier mode est en général le mode mouvement ; c'est lui qui passe la main au mode
    /// regard fixe quand on fixe un point, et le mode regard fixe la lui rend.
    ///
    /// Étape 4 : c'est aussi lui qui ouvre le menu : quand le regard reste en bas, hors de
    /// l'écran, pendant [DelaiMenu], on passe au mode menu. Depuis l'étape 6, seuls les modes
    /// mouvement et pause (et plus tard jeu) l'autorisent (propriété Mode.MenuAutorise).
    /// </summary>
    internal sealed class ModeManager
    {
        /// <summary>Intervalle entre deux actualisations, en millisecondes (16 ms ≈ 60 fois par seconde, la cadence de l'écran).</summary>
        public const int IntervalleMs = 16;

        private readonly DispatcherTimer _horloge;

        // Instant (ms) où le regard est passé en bas hors de l'écran ; -1 = il n'y est pas.
        private double _debutEnBasMs = -1;

        // Pour le journal : où était le regard au tic précédent (voir NoterOuEstLeRegard).
        private string _situationRegard = "";
        private double _dernierX;
        private double _dernierY;

        public ModeManager(GazeSource regard, OverlayWindow calque)
        {
            Regard = regard;
            Calque = calque;

            _horloge = new DispatcherTimer();
            _horloge.Interval = TimeSpan.FromMilliseconds(IntervalleMs);
            _horloge.Tick += OnTick;
        }

        /// <summary>La source du regard Tobii.</summary>
        public GazeSource Regard { get; private set; }

        /// <summary>La fenêtre transparente plein écran où les modes dessinent.</summary>
        public OverlayWindow Calque { get; private set; }

        /// <summary>Le mode en cours (null avant Demarrer).</summary>
        public Mode ModeCourant { get; private set; }

        /// <summary>Le mode d'avant, pour pouvoir y revenir (par exemple en fermant le menu).</summary>
        public Mode ModePrecedent { get; private set; }

        /// <summary>Déclenché après chaque changement de mode. L'interface peut s'en servir pour s'afficher à jour.</summary>
        public event Action<Mode> ModeChange;

        /// <summary>Largeur de l'écran principal, en pixels réels.</summary>
        public double LargeurEcran
        {
            get { return NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN); }
        }

        /// <summary>Hauteur de l'écran principal, en pixels réels.</summary>
        public double HauteurEcran
        {
            get { return NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN); }
        }

        // ───────────────────────── Commandes ─────────────────────────

        /// <summary>Entre dans le premier mode et lance l'horloge.</summary>
        public void Demarrer(Mode premierMode)
        {
            ChangerMode(premierMode);
            _horloge.Start();
        }

        /// <summary>Arrête l'horloge et quitte proprement le mode courant (à la fermeture du programme).</summary>
        public void Arreter()
        {
            _horloge.Stop();
            if (ModeCourant != null)
            {
                ModeCourant.Quitter();
                ModeCourant = null;
            }
        }

        /// <summary>
        /// Passe au mode indiqué : l'ancien mode est rangé (Quitter), puis le nouveau
        /// commence (Entrer). Peut être appelée par un mode lui-même, depuis son Actualiser.
        /// </summary>
        public void ChangerMode(Mode nouveau)
        {
            if (nouveau == null || nouveau == ModeCourant)
            {
                return;
            }

            Mode ancien = ModeCourant;
            if (ancien != null)
            {
                ancien.Quitter();
            }

            ModePrecedent = ancien;
            ModeCourant = nouveau;
            nouveau.Attacher(this);

            App.Log("Mode : " + (ancien == null ? "(aucun)" : ancien.Nom) + " → " + nouveau.Nom);
            // Le nouveau mode reçoit la position actuelle du regard pour s'initialiser.
            nouveau.Entrer(LireRegard());

            Action<Mode> abonnes = ModeChange;
            if (abonnes != null)
            {
                abonnes(nouveau);
            }
        }

        /// <summary>Revient au mode d'avant, s'il existe.</summary>
        public void RevenirAuModePrecedent()
        {
            if (ModePrecedent != null)
            {
                ChangerMode(ModePrecedent);
            }
        }

        // ───────────────────────── Horloge ─────────────────────────

        private void OnTick(object sender, EventArgs e)
        {
            if (ModeCourant == null)
            {
                return;
            }

            EtatRegard etat = LireRegard();

            try
            {
                // Le menu s'ouvre-t-il ? Si oui, le mode courant n'est pas actualisé à ce tic.
                if (SurveillerMenu(etat))
                {
                    return;
                }

                ModeCourant.Actualiser(etat);
            }
            catch (Exception ex)
            {
                // Une erreur dans un mode ne doit pas tout bloquer : on la note, on relâche
                // les boutons de la souris et les touches du clavier par sécurité, et
                // l'horloge continue.
                MouseInjector.ReleaseAll();
                InjecteurClavier.RelacherTout();
                App.Log("ERREUR dans le mode " + ModeCourant.Nom + " : " + ex);
            }
        }

        /// <summary>
        /// Ouvre le menu quand le regard reste en bas, hors de l'écran, pendant [DelaiMenu].
        /// Le minuteur repart de zéro dès que le regard est ailleurs, ou perdu : on ne compte
        /// que ce qu'on voit vraiment. Renvoie vrai si le menu vient de s'ouvrir.
        /// </summary>
        private bool SurveillerMenu(EtatRegard etat)
        {
            NoterOuEstLeRegard(etat);

            if (!etat.Disponible || !etat.EnBasHorsEcran)
            {
                _debutEnBasMs = -1;
                return false;
            }

            if (_debutEnBasMs < 0)
            {
                _debutEnBasMs = etat.MaintenantMs;
                return false;
            }

            if (etat.MaintenantMs - _debutEnBasMs >= Parametres.Courants.DelaiMenu * 1000.0 &&
                ModeCourant.MenuAutorise)
            {
                _debutEnBasMs = -1;
                ChangerMode(new ModeMenu());
                return true;
            }
            return false;
        }

        /// <summary>
        /// Diagnostic du menu : note dans le journal chaque fois que le regard change de
        /// situation (sur l'écran, sous l'écran, hors de l'écran ailleurs, perdu), avec sa
        /// dernière position connue. Sert à vérifier que le PCEye 5 mesure bien le regard
        /// SOUS l'écran (condition pour ouvrir le menu), au lieu de le perdre.
        /// </summary>
        private void NoterOuEstLeRegard(EtatRegard etat)
        {
            string situation;
            if (!etat.Disponible)
            {
                situation = "perdu";
            }
            else if (etat.EnBasHorsEcran)
            {
                situation = "sous l'écran";
            }
            else if (etat.HorsEcran)
            {
                situation = "hors de l'écran (pas en bas)";
            }
            else
            {
                situation = "sur l'écran";
            }

            if (etat.Disponible)
            {
                _dernierX = etat.X;
                _dernierY = etat.Y;
            }

            if (situation != _situationRegard)
            {
                _situationRegard = situation;
                App.Log("Regard : " + situation + " (dernière position X " + _dernierX.ToString("F0") +
                        ", Y " + _dernierY.ToString("F0") + " ; écran " + LargeurEcran + " × " + HauteurEcran + ")");
            }
        }

        /// <summary>Prépare la « photo » du regard pour le mode courant.</summary>
        private EtatRegard LireRegard()
        {
            EtatRegard etat = new EtatRegard();
            etat.MaintenantMs = Regard.MaintenantMs;

            if (!Regard.IsRunning || !Regard.HasData ||
                Regard.MsSinceLastSample > Parametres.Courants.AgeMaxMesure)
            {
                etat.Disponible = false;
                return etat;
            }

            double x;
            double y;
            long numero;
            double instantMs;
            Regard.GetLatest(out x, out y, out numero, out instantMs);

            double largeur = LargeurEcran;
            double hauteur = HauteurEcran;

            etat.Disponible = true;
            etat.X = x;
            etat.Y = y;
            etat.Numero = numero;
            etat.InstantMesureMs = instantMs;
            etat.HorsEcran = GazeSource.IsOffScreen(x, y, largeur, hauteur);
            etat.EnBasHorsEcran = y >= hauteur;
            return etat;
        }
    }
}
