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
    /// ouvre le calque plein écran et le gestionnaire de modes, puis la barre d'outils.
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

        /// <summary>
        /// Appelée par WPF au démarrage du programme.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Log("Démarrage de GazeBar");

            // Filets de sécurité : quelle que soit l'erreur, on relâche les boutons de la
            // souris (un bouton resté enfoncé gênerait tout le reste de l'ordinateur),
            // et on note l'erreur dans le journal.
            DispatcherUnhandledException += (sender, args) =>
            {
                MouseInjector.ReleaseAll();
                Log("ERREUR NON GÉRÉE (interface) : " + args.Exception);
                args.Handled = true;   // on évite que le programme se ferme brutalement
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                MouseInjector.ReleaseAll();
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

            // La barre d'outils reste disponible jusqu'à l'étape 4 (mode menu).
            // Le calque est créé ici mais affiché APRÈS elle, pour être au-dessus.
            _calque = new OverlayWindow();
            _modes = new ModeManager(_gaze, _calque);

            ToolbarWindow toolbar = new ToolbarWindow(_sampler, _gaze, _modes);
            MainWindow = toolbar;
            // Fermer la barre ferme tout le programme, même si le calque ou le
            // diagnostic sont encore ouverts.
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            toolbar.Show();

            // Le calque plein écran, transparent aux clics, et le gestionnaire de modes.
            // Pour l'étape 1, on démarre dans le mode de test (un cercle sur le regard).
            _calque.Show();
            _calque.RemettreAuPremierPlan();
            _modes.Demarrer(new ModeTest());
        }

        /// <summary>
        /// Appelée par WPF à la fermeture du programme.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            MouseInjector.ReleaseAll();
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
