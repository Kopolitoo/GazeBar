using System;
using System.Diagnostics;
using System.Text;

namespace GazeBar
{
    /// <summary>
    /// MODE CALIBRATION (étape 4 du cahier des charges).
    ///
    /// On y arrive depuis la barre de menu, une fois l'outil de calibration Tobii lancé (voir
    /// CalibrationTobii.cs). Pendant la calibration, GazeBar ne doit rien faire : le calque
    /// est vide, rien ne suit le regard (sinon le cercle gênerait et une fixation sur un point
    /// de calibration pourrait déclencher une action).
    ///
    /// « Retour au mode mouvement à la fermeture de l'outil » : Tobii ne nous prévient pas de
    /// cette fermeture. On la devine ainsi :
    ///  1. au lancement, l'outil Tobii ouvre sa fenêtre, qui devient la fenêtre active. On
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
    internal sealed class ModeCalibration : Mode
    {
        /// <summary>Attente maximale de la fenêtre de l'outil, en millisecondes.</summary>
        private const double AttenteFenetreMs = 15000;

        /// <summary>Durée maximale de la calibration, en millisecondes (5 minutes).</summary>
        private const double DureeMaxMs = 5 * 60 * 1000;

        private double _debutMs;
        private IntPtr _fenetreAvant = IntPtr.Zero;
        private IntPtr _fenetreOutil = IntPtr.Zero;
        private uint _monProcessus;

        /// <summary>
        /// fenetreAvant : la fenêtre active juste AVANT le lancement de l'outil (notée par la
        /// barre de menu). Ce n'est donc pas la fenêtre de l'outil.
        /// </summary>
        public ModeCalibration(IntPtr fenetreAvant)
        {
            _fenetreAvant = fenetreAvant;
        }

        public override string Nom
        {
            get { return "Calibration"; }
        }

        /// <summary>
        /// L'écran a changé (étape 11) : ce mode continue, car la calibration de Tobii ne doit pas être
        /// interrompue.
        /// </summary>
        public override bool AdapterEcran()
        {
            return true;
        }

        public override void Entrer(EtatRegard regard)
        {
            Gestionnaire.Calque.MasquerTout();
            _debutMs = regard.MaintenantMs;
            _fenetreOutil = IntPtr.Zero;
            _monProcessus = (uint)Process.GetCurrentProcess().Id;
        }

        public override void Actualiser(EtatRegard regard)
        {
            double ecoule = regard.MaintenantMs - _debutMs;

            if (_fenetreOutil == IntPtr.Zero)
            {
                // 1. On attend la fenêtre de l'outil.
                IntPtr active = NativeMethods.GetForegroundWindow();
                if (active != IntPtr.Zero && active != _fenetreAvant && !EstAGazeBar(active))
                {
                    _fenetreOutil = active;
                    App.Log("Calibration : fenêtre de l'outil « " + Titre(active) + " »");
                }
                else if (ecoule > AttenteFenetreMs)
                {
                    App.Log("Calibration : aucune fenêtre d'outil vue, retour au mode mouvement");
                    Gestionnaire.ChangerMode(new ModeMouvement());
                }
                return;
            }

            // 2. On attend sa fermeture.
            if (!NativeMethods.IsWindow(_fenetreOutil) || !NativeMethods.IsWindowVisible(_fenetreOutil))
            {
                App.Log("Calibration : outil fermé, retour au mode mouvement");
                Gestionnaire.ChangerMode(new ModeMouvement());
            }
            else if (ecoule > DureeMaxMs)
            {
                App.Log("Calibration : durée maximale dépassée, retour au mode mouvement");
                Gestionnaire.ChangerMode(new ModeMouvement());
            }
        }

        /// <summary>Vrai si la fenêtre appartient à GazeBar lui-même.</summary>
        private bool EstAGazeBar(IntPtr fenetre)
        {
            uint processus;
            NativeMethods.GetWindowThreadProcessId(fenetre, out processus);
            return processus == _monProcessus;
        }

        private static string Titre(IntPtr fenetre)
        {
            StringBuilder texte = new StringBuilder(256);
            NativeMethods.GetWindowText(fenetre, texte, texte.Capacity);
            return texte.ToString();
        }
    }
}
