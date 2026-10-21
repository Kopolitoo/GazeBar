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
    /// </summary>
    internal sealed class ModeManager
    {
        /// <summary>Intervalle entre deux actualisations, en millisecondes (16 ms ≈ 60 fois par seconde, la cadence de l'écran).</summary>
        public const int IntervalleMs = 16;

        private readonly DispatcherTimer _horloge;

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
            nouveau.Entrer();

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
                ModeCourant.Actualiser(etat);
            }
            catch (Exception ex)
            {
                // Une erreur dans un mode ne doit pas tout bloquer : on la note, on relâche
                // les boutons de la souris par sécurité, et l'horloge continue.
                MouseInjector.ReleaseAll();
                App.Log("ERREUR dans le mode " + ModeCourant.Nom + " : " + ex);
            }
        }

        /// <summary>Prépare la « photo » du regard pour le mode courant.</summary>
        private EtatRegard LireRegard()
        {
            EtatRegard etat = new EtatRegard();

            if (!Regard.IsRunning || !Regard.HasData ||
                Regard.MsSinceLastSample > Parametres.Courants.AgeMaxMesure)
            {
                etat.Disponible = false;
                return etat;
            }

            double x;
            double y;
            Regard.GetLatest(out x, out y);

            double largeur = LargeurEcran;
            double hauteur = HauteurEcran;

            etat.Disponible = true;
            etat.X = x;
            etat.Y = y;
            etat.HorsEcran = GazeSource.IsOffScreen(x, y, largeur, hauteur);
            etat.EnBasHorsEcran = y >= hauteur;
            return etat;
        }
    }
}
