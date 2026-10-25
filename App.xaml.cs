using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GazeBar
{
    /// <summary>
    /// Classe de l'application : point d'entrée du programme.
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

        /// <summary>
        /// Appelée par WPF au démarrage du programme.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
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
            MouseInjector.ReleaseAll();
            InjecteurClavier.RelacherTout();
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
