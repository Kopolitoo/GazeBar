using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;

namespace GazeBar
{
    /// <summary>
    /// Classe de l'application : point d'entrée du programme.
    ///
    /// Étape 7 : une seule instance de GazeBar à la fois, et l'option [DemarrerAvecWindows].
    ///
    /// Elle lit les réglages, crée les objets « modèle » partagés (curseur, regard),
    /// ouvre le calque plein écran et le gestionnaire de modes, et surveille la touche Échap.
    /// Depuis l'étape 4, il n'y a plus de barre d'outils : tout passe par le regard, et la
    /// barre de menu s'ouvre en regardant en bas, hors de l'écran.
    /// Elle s'assure aussi, en toute circonstance, que les boutons de la souris sont
    /// relâchés à la fermeture.
    ///
    /// Rappel : « partial » signifie que la classe est écrite en deux morceaux, celui-ci
    /// et un autre généré automatiquement à partir de App.xaml.
    /// </summary>
    public partial class App : Application
    {
        private CursorSampler _sampler;
        private GazeSource _gaze;
        private OverlayWindow _calque;
        private ModeManager _modes;
        private CrochetClavier _clavier;
        private DiagnosticWindow _diagnostic;
        private ReveilCurseur _reveilCurseur;

        /// <summary>
        /// Le « verrou » d'instance unique (étape 7). Un Mutex est un objet de Windows qui porte
        /// un nom ; un seul programme à la fois peut le posséder. Le premier GazeBar le prend ;
        /// un deuxième GazeBar voit qu'il est déjà pris, et se ferme aussitôt.
        /// « Local\ » : le nom vaut pour la session Windows de l'utilisateur.
        /// </summary>
        private const string NomVerrou = "Local\\GazeBar-InstanceUnique";
        private Mutex _verrou;

        // Vrai pour un deuxième GazeBar, qui se ferme sans rien faire (voir OnStartup / OnExit).
        private bool _instanceEnTrop;

        /// <summary>
        /// Appelée par WPF au démarrage du programme.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Une seule instance (cahier des charges, « Fonctionnement général ») : deux GazeBar
            // en même temps dessineraient deux cercles et enverraient des clics en double.
            // Si GazeBar tourne déjà, celui-ci se ferme aussitôt, sans rien afficher.
            bool premier;
            _verrou = new Mutex(true, NomVerrou, out premier);
            if (!premier)
            {
                _instanceEnTrop = true;
                Log("Deuxième lancement de GazeBar : une instance tourne déjà, fermeture");
                Shutdown();
                return;
            }

            Log("Démarrage de GazeBar");

            // Filets de sécurité : quelle que soit l'erreur, on relâche les boutons de la
            // souris et les touches Ctrl, Maj, Alt (un bouton ou une touche resté enfoncé
            // gênerait tout le reste de l'ordinateur),
            // et on note l'erreur dans le journal.
            DispatcherUnhandledException += (sender, args) =>
            {
                MouseInjector.ReleaseAll();
                InjecteurClavier.RelacherTout();
                Log("ERREUR NON GÉRÉE (interface) : " + args.Exception);
                args.Handled = true;   // on évite que le programme se ferme brutalement
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                MouseInjector.ReleaseAll();
                InjecteurClavier.RelacherTout();
                Log("ERREUR NON GÉRÉE (.NET) : " + args.ExceptionObject);
            };

            // Les réglages d'abord : tout le reste s'en sert.
            Parametres.Initialiser();

            // Démarrage avec Windows : le registre est mis d'accord avec le fichier de réglages
            // (et le chemin du programme est mis à jour s'il a été déplacé).
            DemarrageWindows.Appliquer(Parametres.Courants.DemarrerAvecWindows);

            _sampler = new CursorSampler();
            _sampler.Start();

            // Le regard réel, fourni par le moteur Tobii. S'il est indisponible,
            // le programme démarre quand même (voir GazeSource.ErrorMessage).
            _gaze = new GazeSource();
            _gaze.Start();

            // Le programme ne s'arrête que sur demande (bouton « Quitter GazeBar » du menu) :
            // fermer la fenêtre Diagnostic ne doit pas tout fermer.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Le calque plein écran, transparent aux clics, et le gestionnaire de modes.
            // On démarre dans le mode mouvement (le mode par défaut du cahier des charges).
            _calque = new OverlayWindow();
            _modes = new ModeManager(_gaze, _calque);
            _calque.Show();
            _calque.RemettreAuPremierPlan();
            _modes.Demarrer(new ModeMouvement());

            // Correction du 29/10/2026 : si Windows a caché le curseur (GazeBar lancé seul au
            // démarrage, sans souris physique), on le fait réapparaître (voir ReveilCurseur).
            _reveilCurseur = new ReveilCurseur();
            _reveilCurseur.Demarrer();

            // La touche Échap de l'aidant : retour au mode mouvement, depuis n'importe quel mode.
            _clavier = new CrochetClavier();
            _clavier.EchapAppuye += OnEchap;
            _clavier.Installer();
        }

        /// <summary>
        /// Touche Échap (pour l'aidant) : on relâche les boutons de la souris et les touches
        /// modificatrices par sécurité (un glisser-déposer en cours est interrompu) et on
        /// revient au mode mouvement, sauf si l'on y est déjà. La touche arrive aussi normalement
        /// au programme actif (elle n'est pas « mangée » par GazeBar).
        /// </summary>
        private void OnEchap()
        {
            if (_modes == null || _modes.ModeCourant is ModeMouvement)
            {
                return;
            }

            // Mode paramètres : pendant une saisie au clavier, Échap annule seulement la saisie.
            ModeParametres parametres = _modes.ModeCourant as ModeParametres;
            if (parametres != null && parametres.AnnulerSaisie())
            {
                return;
            }

            // Écran de configuration des icônes (étape 10) : Échap ramène aux paramètres.
            ModeConfigIcones configuration = _modes.ModeCourant as ModeConfigIcones;
            if (configuration != null)
            {
                configuration.RetourParametres();
                return;
            }
            App.Log("Touche Échap : retour au mode mouvement");
            MouseInjector.ReleaseAll();
            InjecteurClavier.RelacherTout();
            _modes.ChangerMode(new ModeMouvement());
        }

        /// <summary>
        /// Ouvre la fenêtre Diagnostic (bouton du menu). Une seule à la fois : si elle est déjà
        /// ouverte, on la remet simplement devant.
        /// </summary>
        internal void OuvrirDiagnostic()
        {
            if (_diagnostic == null)
            {
                _diagnostic = new DiagnosticWindow(_sampler, _gaze);
                _diagnostic.Closed += (sender, args) => { _diagnostic = null; };
            }
            _diagnostic.Show();
            _diagnostic.Activate();
        }

        /// <summary>
        /// Appelée par WPF à la fermeture du programme.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceEnTrop)
            {
                // Deuxième GazeBar : il n'a rien démarré, il ne touche à rien (surtout pas aux
                // boutons de la souris, que le premier GazeBar est peut-être en train de tenir).
                _verrou.Dispose();
                base.OnExit(e);
                return;
            }

            MouseInjector.ReleaseAll();
            InjecteurClavier.RelacherTout();
            if (_reveilCurseur != null)
            {
                _reveilCurseur.Arreter();
            }
            if (_clavier != null)
            {
                _clavier.Dispose();
            }
            if (_modes != null)
            {
                _modes.Arreter();
            }
            if (_sampler != null)
            {
                _sampler.Dispose();
            }
            if (_gaze != null)
            {
                _gaze.Dispose();
            }
            Log("Arrêt de GazeBar");
            if (_verrou != null)
            {
                _verrou.ReleaseMutex();
                _verrou.Dispose();
            }
            base.OnExit(e);
        }

        /// <summary>
        /// Écrit un message dans le journal, à deux endroits :
        ///  1. la fenêtre « Sortie » de Visual Studio (préfixe [GazeBar]) ;
        ///  2. le fichier gazebar.log dans %LOCALAPPDATA%\GazeBar
        ///     (utile quand le programme est lancé sans le débogueur).
        /// </summary>
        internal static void Log(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message;
            Debug.WriteLine("[GazeBar] " + line);

            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GazeBar");
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "gazebar.log"), line + Environment.NewLine);
            }
            catch
            {
                // Le journal ne doit jamais lui-même faire planter le programme :
                // si l'écriture échoue, on ignore silencieusement.
            }
        }
    }
}
