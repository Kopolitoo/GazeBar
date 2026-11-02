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
        private OverlayWindow _layer;
        private ModeManager _modes;
        private KeyboardHook _keyboard;
        private DiagnosticWindow _diagnostic;

        /// <summary>
        /// Le « verrou » d'instance unique (étape 7). Un Mutex est un objet de Windows qui porte
        /// un nom ; un seul programme à la fois peut le posséder. Le premier GazeBar le prend ;
        /// un deuxième GazeBar voit qu'il est déjà pris, et se ferme aussitôt.
        /// « Local\ » : le nom vaut pour la session Windows de l'utilisateur.
        /// </summary>
        private const string LockName = "Local\\GazeBar-InstanceUnique";
        private Mutex _singleInstanceMutex;

        // Vrai pour un deuxième GazeBar, qui se ferme sans rien faire (voir OnStartup / OnExit).
        private bool _extraInstance;

        /// <summary>
        /// Appelée par WPF au démarrage du programme.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Une seule instance (cahier des charges, « Fonctionnement général ») : deux GazeBar
            // en même temps dessineraient deux cercles et enverraient des clics en double.
            // Si GazeBar tourne déjà, celui-ci se ferme aussitôt, sans rien afficher.
            bool isFirst;
            _singleInstanceMutex = new Mutex(true, LockName, out isFirst);
            if (!isFirst)
            {
                _extraInstance = true;
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
                KeyboardInjector.ReleaseAll();
                Log("ERREUR NON GÉRÉE (interface) : " + args.Exception);
                args.Handled = true;   // on évite que le programme se ferme brutalement
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                MouseInjector.ReleaseAll();
                KeyboardInjector.ReleaseAll();
                Log("ERREUR NON GÉRÉE (.NET) : " + args.ExceptionObject);
            };

            // Les réglages d'abord : tout le reste s'en sert.
            AppSettings.Initialize();

            // Démarrage avec Windows : le registre est mis d'accord avec le fichier de réglages
            // (et le chemin du programme est mis à jour s'il a été déplacé).
            WindowsStartup.Apply(AppSettings.Current.StartWithWindows);

            // Défilement des fenêtres inactives (02/11/2026) : le réglage de Windows est mis
            // d'accord avec le fichier de réglages (voir MoletteWindows).
            WheelScroller.Apply(AppSettings.Current.ScrollInactiveWindows);

            _sampler = new CursorSampler();
            _sampler.Start();

            // Le regard réel, fourni par le moteur de l'eyetracker. S'il est indisponible,
            // le programme démarre quand même (voir GazeSource.ErrorMessage).
            // Depuis le 02/11/2026, les DLL de l'eyetracker ne sont plus livrées avec GazeBar : on les
            // cherche d'abord sur le PC (voir ChargeurEyetracker.cs), avant tout usage du regard.
            EyetrackerLoader.Install();
            _gaze = new GazeSource();
            _gaze.Start();

            // Le programme ne s'arrête que sur demande (bouton « Quitter GazeBar » du menu) :
            // fermer la fenêtre Diagnostic ne doit pas tout fermer.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Le calque plein écran, transparent aux clics, et le gestionnaire de modes.
            // On démarre dans le mode mouvement (le mode par défaut du cahier des charges).
            _layer = new OverlayWindow();
            _modes = new ModeManager(_gaze, _layer);
            _layer.Show();
            _layer.BringToFront();
            _modes.Start(new MovementMode());

            // Correction du 29/10/2026 : si Windows a caché le curseur (GazeBar lancé seul au
            // démarrage, sans souris physique), on le fait réapparaître (voir ReveilCurseur).
            CursorWaker.Check();

            // La touche Échap de l'aidant : retour au mode mouvement, depuis n'importe quel mode.
            _keyboard = new KeyboardHook();
            _keyboard.EscapePressed += OnEscape;
            _keyboard.Install();
        }

        /// <summary>
        /// Touche Échap (pour l'aidant) : on relâche les boutons de la souris et les touches
        /// modificatrices par sécurité (un glisser-déposer en cours est interrompu) et on
        /// revient au mode mouvement, sauf si l'on y est déjà. Dans le mode mouvement, elle
        /// ferme le clavier au regard (étape 12). La touche arrive aussi normalement
        /// au programme actif (elle n'est pas « mangée » par GazeBar).
        /// </summary>
        private void OnEscape()
        {
            if (_modes == null)
            {
                return;
            }

            // Étape 12 : en mode mouvement, Échap ferme le clavier au regard s'il est ouvert.
            // (La touche Échap du clavier au regard, elle, est simulée : elle n'arrive pas ici,
            // voir CrochetClavier.)
            if (_modes.CurrentMode is MovementMode)
            {
                if (_modes.Keyboard.IsOpen)
                {
                    App.Log("Touche Échap : fermeture du clavier au regard");
                    _modes.Keyboard.Close();
                }
                return;
            }

            // Mode paramètres : pendant une saisie au clavier, Échap annule seulement la saisie.
            SettingsMode settings = _modes.CurrentMode as SettingsMode;
            if (settings != null && settings.CancelInput())
            {
                return;
            }

            // Écran de configuration des icônes (étape 10) : Échap ramène aux paramètres.
            IconConfigMode configuration = _modes.CurrentMode as IconConfigMode;
            if (configuration != null)
            {
                configuration.BackToSettings();
                return;
            }
            // Listes du clavier (étape 14) : Échap fait comme « Retour » (Mes mots → paramètres).
            KeyboardListMode list = _modes.CurrentMode as KeyboardListMode;
            if (list != null)
            {
                list.Back();
                return;
            }
            App.Log("Touche Échap : retour au mode mouvement");
            MouseInjector.ReleaseAll();
            KeyboardInjector.ReleaseAll();
            _modes.ChangeMode(new MovementMode());
        }

        /// <summary>
        /// Ouvre la fenêtre Diagnostic (bouton du menu). Une seule à la fois : si elle est déjà
        /// ouverte, on la remet simplement devant.
        /// </summary>
        internal void OpenDiagnostic()
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
            if (_extraInstance)
            {
                // Deuxième GazeBar : il n'a rien démarré, il ne touche à rien (surtout pas aux
                // boutons de la souris, que le premier GazeBar est peut-être en train de tenir).
                _singleInstanceMutex.Dispose();
                base.OnExit(e);
                return;
            }

            MouseInjector.ReleaseAll();
            KeyboardInjector.ReleaseAll();
            if (_keyboard != null)
            {
                _keyboard.Dispose();
            }
            if (_modes != null)
            {
                // Étape 13 : les nombres d'utilisations des mots du clavier en attente sont
                // enregistrés dans le dictionnaire personnel.
                _modes.Keyboard.Stop();
                _modes.Stop();
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
            if (_singleInstanceMutex != null)
            {
                _singleInstanceMutex.ReleaseMutex();
                _singleInstanceMutex.Dispose();
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
