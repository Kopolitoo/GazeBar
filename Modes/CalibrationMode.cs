using System;
using System.Diagnostics;
using System.Text;

namespace GazeBar
{
    /// <summary>
    /// MODE CALIBRATION (étape 4 du cahier des charges).
    ///
    /// On y arrive depuis l'onglet « Outils » des paramètres (avant le 29/10/2026 : depuis la
    /// barre de menu), une fois l'outil de calibration de l'eyetracker lancé (voir
    /// Calibration.cs). Pendant la calibration, GazeBar ne doit rien faire : le calque
    /// est vide, rien ne suit le regard (sinon le cercle gênerait et une fixation sur un point
    /// de calibration pourrait déclencher une action).
    ///
    /// « Retour au mode mouvement à la fermeture de l'outil » : l'eyetracker ne nous prévient pas de
    /// cette fermeture. On la devine ainsi :
    ///  1. au lancement, l'outil de l'eyetracker ouvre sa fenêtre, qui devient la fenêtre active. On
    ///     note donc la fenêtre active au début (celle de l'application qu'on utilisait), puis
    ///     on attend qu'une AUTRE fenêtre devienne active, qui n'est pas à GazeBar, et on la
    ///     retient (son titre est noté dans le journal) ;
    ///  2. quand cette fenêtre n'existe plus ou est cachée, la calibration est finie : retour
    ///     au mode mouvement.
    /// Sécurités : si aucune fenêtre n'apparaît en [AttenteFenetreMs], ou si la calibration
    /// dure plus de [DureeMaxMs], on revient au mode mouvement. Regarder en bas hors de
    /// l'écran ouvre toujours le menu, et la touche Échap ramène au mode mouvement.
    ///
    /// À VÉRIFIER sur le PCEye 5 : que la fenêtre de calibration devient bien la fenêtre
    /// active (le journal le dira).
    /// </summary>
    internal sealed class CalibrationMode : Mode
    {
        /// <summary>Attente maximale de la fenêtre de l'outil, en millisecondes.</summary>
        private const double WindowWaitMs = 15000;

        /// <summary>Durée maximale de la calibration, en millisecondes (5 minutes).</summary>
        private const double MaxDurationMs = 5 * 60 * 1000;

        private double _startMs;
        private IntPtr _previousWindow = IntPtr.Zero;
        private IntPtr _toolWindow = IntPtr.Zero;
        private uint _myProcess;

        /// <summary>
        /// fenetreAvant : la fenêtre active juste AVANT le lancement de l'outil (notée par
        /// ModeParametres). Ce n'est donc pas la fenêtre de l'outil.
        /// </summary>
        public CalibrationMode(IntPtr previousWindow)
        {
            _previousWindow = previousWindow;
        }

        public override string Name
        {
            get { return "Calibration"; }
        }

        /// <summary>
        /// L'écran a changé (étape 11) : ce mode continue, car la calibration de l'eyetracker ne doit pas être
        /// interrompue.
        /// </summary>
        public override bool AdaptToScreen()
        {
            return true;
        }

        public override void Enter(GazeState gaze)
        {
            Manager.Layer.HideAll();
            _startMs = gaze.NowMs;
            _toolWindow = IntPtr.Zero;
            _myProcess = (uint)Process.GetCurrentProcess().Id;
        }

        public override void Update(GazeState gaze)
        {
            double elapsedMs = gaze.NowMs - _startMs;

            if (_toolWindow == IntPtr.Zero)
            {
                // 1. On attend la fenêtre de l'outil.
                IntPtr active = NativeMethods.GetForegroundWindow();
                if (active != IntPtr.Zero && active != _previousWindow && !BelongsToGazeBar(active))
                {
                    _toolWindow = active;
                    App.Log("Calibration : fenêtre de l'outil « " + Title(active) + " »");
                }
                else if (elapsedMs > WindowWaitMs)
                {
                    App.Log("Calibration : aucune fenêtre d'outil vue, retour au mode mouvement");
                    Manager.ChangeMode(new MovementMode());
                }
                return;
            }

            // 2. On attend sa fermeture.
            if (!NativeMethods.IsWindow(_toolWindow) || !NativeMethods.IsWindowVisible(_toolWindow))
            {
                App.Log("Calibration : outil fermé, retour au mode mouvement");
                Manager.ChangeMode(new MovementMode());
            }
            else if (elapsedMs > MaxDurationMs)
            {
                App.Log("Calibration : durée maximale dépassée, retour au mode mouvement");
                Manager.ChangeMode(new MovementMode());
            }
        }

        /// <summary>Vrai si la fenêtre appartient à GazeBar lui-même.</summary>
        private bool BelongsToGazeBar(IntPtr window)
        {
            uint process;
            NativeMethods.GetWindowThreadProcessId(window, out process);
            return process == _myProcess;
        }

        private static string Title(IntPtr window)
        {
            StringBuilder text = new StringBuilder(256);
            NativeMethods.GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }
    }
}
