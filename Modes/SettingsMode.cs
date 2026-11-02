using System;
using System.Windows;

namespace GazeBar
{
    /// <summary>
    /// MODE PARAMÈTRES (étape 7 du cahier des charges).
    ///
    /// Entrée : bouton « Paramètres » de la barre de menu.
    ///
    /// Affichage : la fenêtre FenetreParametres, plein écran, fond noir (voir ce fichier pour
    /// la mise en page). Le calque est vidé : rien n'y est dessiné pendant ce mode.
    ///
    /// Le regard : ce mode fait le lien entre le regard et la fenêtre.
    ///   - à chaque tic, la position du regard (pixels d'écran) est convertie en position sur
    ///     la page 1920 × 1080 de la fenêtre, puis donnée à un ViseurBoutons, le même que
    ///     pour le menu et le choix d'action ;
    ///   - une cible regardée pendant [DelaiActivation] s'active ;
    ///   - RÉPÉTITION (choix de l'utilisateur du 26/10/2026) : si le regard reste sur « − » ou
    ///     « + », la valeur continue de changer, d'un pas toutes les [DelaiRepetition]
    ///     secondes. Les autres cibles (onglets, Non / Oui…) ne s'activent qu'une fois : il
    ///     faut quitter la cible du regard pour l'activer de nouveau ;
    ///   - un petit cercle gris (50 px) montre où est le regard.
    ///
    /// Étape 10 : le bouton « Configurer les icônes » de l'onglet « Icônes » ouvre l'écran de
    /// configuration des icônes (ModeConfigIcones), qui revient ici, sur cet onglet.
    ///
    /// Étape 14 : le bouton « Mes mots » de l'onglet « Général » ouvre la liste des mots ajoutés
    /// au clavier (ModeListeClavier), qui revient ici, sur cet onglet.
    ///
    /// Onglet « Outils » (29/10/2026, boutons déplacés de la barre de menu) :
    ///   - « Calibration » lance l'outil de l'eyetracker puis passe en mode calibration (qui revient au
    ///     mode mouvement quand l'outil se ferme). Si le lancement échoue, on reste ici et le
    ///     message s'affiche dans l'onglet ;
    ///   - « Diagnostic » revient au mode mouvement et ouvre la fenêtre Diagnostic.
    ///
    /// Étape 15 (profils) : onglet « Profils ». « Charger un profil » ouvre la liste des
    /// profils (ModeListeClavier, sorte Profils), qui revient ici avec le profil choisi : la
    /// fenêtre demande alors confirmation. « Oui » : ChargerProfil (ci-dessous) applique le
    /// profil, puis le clavier relit sa disposition (la langue a pu changer).
    ///
    /// Sortie : bouton « Retour » (au milieu du bord droit) ou touche Échap de l'aidant,
    /// vers le mode mouvement. Le menu du bas ne s'ouvre pas ici (Mode.MenuAutorise reste
    /// « non », règle du 25/10/2026).
    ///
    /// Chaque changement est appliqué tout de suite (Parametres.Courants) et enregistré dans
    /// parametres.json.
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal sealed class SettingsMode : Mode
    {
        /// <summary>Intervalle de la répétition sur « − » et « + », en secondes.</summary>
        private const double RepeatDelay = 0.4;

        // L'onglet affiché à l'ouverture (0 = le premier).
        private readonly int _startTab;

        // Étape 15 : le profil choisi dans la liste, à confirmer à l'ouverture (ou null).
        private readonly FoundProfile _profileToConfirm;

        private SettingsWindow _window;
        private ButtonAimer _aimer;

        // Version de la page vue au tic précédent : si elle change, les cibles ont changé.
        private int _version = -1;

        // La cible en cours de répétition (« − » ou « + »), ou -1.
        private int _repeatedTarget = -1;

        // Le petit cercle du regard : position lissée, en pixels d'écran.
        private readonly Smoother _smoothed = new Smoother();
        private bool _circlePlaced;

        /// <summary>
        /// Ouvre les paramètres sur l'onglet numéro « onglet » (le premier si on ne le donne
        /// pas). Étape 15 : « profilAConfirmer » = profil choisi dans la liste des profils ;
        /// la page s'ouvre alors sur la question « Charger le profil … ? ».
        /// </summary>
        public SettingsMode(int tab = 0, FoundProfile profileToConfirm = null)
        {
            _startTab = tab;
            _profileToConfirm = profileToConfirm;
        }

        public override string Name
        {
            get { return "Paramètres"; }
        }

        public override void Enter(GazeState gaze)
        {
            // Rien sur le calque pendant ce mode : tout est dans la fenêtre.
            Manager.Layer.HideAll();

            // La fenêtre est rangée dans _fenetre SEULEMENT quand tout est prêt (viseur
            // compris) : Actualiser ne fait rien tant que _fenetre est vide (bogue du
            // 02/11/2026 : un tic arrivé pendant Show trouvait un viseur vide).
            SettingsWindow window = new SettingsWindow(_startTab, _profileToConfirm);
            window.BackRequested += Back;
            window.IconConfigRequested += OpenIconConfig;
            window.CalibrationRequested += LaunchCalibration;
            window.DiagnosticRequested += OpenDiagnostic;
            window.MyWordsRequested += OpenMyWords;
            window.ProfileListRequested += OpenProfileList;
            window.ProfileLoadRequested += LoadProfile;
            _aimer = new ButtonAimer(window.Targets);
            _version = -1;
            _repeatedTarget = -1;

            window.Show();
            // Activate : la fenêtre prend le clavier, pour que l'aidant puisse taper une valeur.
            // (Windows peut le refuser si un autre programme est au premier plan ; un clic de
            // l'aidant dans la fenêtre suffit alors.)
            window.Activate();
            _window = window;

            _circlePlaced = gaze.OnScreen;
            if (_circlePlaced)
            {
                _smoothed.Place(gaze.X, gaze.Y);
            }
        }

        public override void Update(GazeState gaze)
        {
            if (_window == null || _aimer == null)
            {
                return;
            }
            AppSettings p = AppSettings.Current;

            // 1. La page a-t-elle été reconstruite (nouvel onglet, confirmation…) ? Le viseur
            //    repart de zéro, et la cible qui se trouve sous le regard ne s'active pas tant
            //    que le regard ne l'a pas quittée (comme dans le menu).
            if (_window.Version != _version)
            {
                _version = _window.Version;
                _aimer.ResetAndBlock();
                _repeatedTarget = -1;
            }

            // 2. Le regard, converti en coordonnées de la page. EtatRegard est une « struct » :
            //    surPage est une copie, on peut changer X et Y sans toucher à l'original.
            GazeState onPage = gaze;
            if (gaze.Available)
            {
                Point point = _window.ToPage(gaze.X, gaze.Y);
                onPage.X = point.X;
                onPage.Y = point.Y;
            }

            // 3. Les cibles. Pendant une répétition, le délai est plus court.
            double delay = (_repeatedTarget >= 0 && _aimer.Aimed == _repeatedTarget) ? RepeatDelay : p.ActivationDelay;
            int active = _aimer.Update(onPage, delay, false);
            if (_aimer.Aimed != _repeatedTarget)
            {
                _repeatedTarget = -1;   // le regard a quitté « − » ou « + » : fin de la répétition
            }

            if (active >= 0)
            {
                if (_window.RepeatableTarget(active))
                {
                    _repeatedTarget = active;
                }
                else
                {
                    _aimer.BlockUntilExit();
                }

                _window.Activate(active);
                if (Manager.CurrentMode != this)
                {
                    return;   // « Retour » : on a déjà changé de mode
                }
            }

            // 4. Le petit cercle du regard. Regard hors de l'écran : il reste au bord et glisse
            //    le long de ce bord (EtatRegard.XEcran). Regard perdu : il reste où il est.
            if (gaze.Available)
            {
                if (!_circlePlaced)
                {
                    _smoothed.Place(gaze.XScreen, gaze.YScreen);
                    _circlePlaced = true;
                }
                _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);
                _window.PlaceGazeDot(_window.ToPage(_smoothed.X, _smoothed.Y));
            }
        }

        public override void Quit()
        {
            if (_window != null)
            {
                SettingsWindow window = _window;
                _window = null;
                window.BackRequested -= Back;
                window.IconConfigRequested -= OpenIconConfig;
                window.CalibrationRequested -= LaunchCalibration;
                window.DiagnosticRequested -= OpenDiagnostic;
                window.MyWordsRequested -= OpenMyWords;
                window.ProfileListRequested -= OpenProfileList;
                window.ProfileLoadRequested -= LoadProfile;
                window.CloseSettings();
            }
        }

        /// <summary>
        /// L'écran a changé (étape 11) : on reste dans les paramètres. La fenêtre reprend la
        /// taille de l'écran et la page est redessinée (l'onglet « Affichage » montre l'écran
        /// détecté). La page de 1920 × 1080 unités s'adapte toute seule (Viewbox).
        /// </summary>
        public override bool AdaptToScreen()
        {
            if (_window != null)
            {
                _window.AdaptToScreen();
            }
            return true;
        }

        /// <summary>
        /// Touche Échap de l'aidant (appelée par App) : si une valeur est en cours de saisie,
        /// la saisie est annulée et on reste ici. Renvoie vrai dans ce cas.
        /// </summary>
        public bool CancelInput()
        {
            return _window != null && _window.CancelInput();
        }

        /// <summary>Bouton « Configurer les icônes » : ouvre l'écran de configuration des icônes.</summary>
        private void OpenIconConfig()
        {
            App.Log("Paramètres : configuration des icônes");
            Manager.ChangeMode(new IconConfigMode());
        }

        /// <summary>
        /// Bouton « Mes mots » de l'onglet « Général » (étape 14) : la liste des mots ajoutés
        /// au clavier, qui revient ici, sur cet onglet.
        /// </summary>
        private void OpenMyWords()
        {
            App.Log("Paramètres : mes mots");
            Manager.ChangeMode(new KeyboardListMode(KeyboardListMode.ListKind.MyWords));
        }

        /// <summary>
        /// Bouton « Charger un profil » de l'onglet « Profils » (étape 15) : la liste des
        /// profils en plein écran, qui revient ici (onglet Profils) avec ou sans choix.
        /// </summary>
        private void OpenProfileList()
        {
            App.Log("Paramètres : liste des profils");
            Manager.ChangeMode(new KeyboardListMode(KeyboardListMode.ListKind.Profiles));
        }

        /// <summary>
        /// « Oui » à « Charger le profil … ? » (étape 15) : le profil devient les réglages en
        /// cours (ProfilsParametres.Charger). Le clavier, s'il est ouvert, relit sa
        /// disposition, car le profil peut avoir une autre langue. On reste dans les
        /// paramètres : la page est reconstruite avec les nouvelles valeurs et le message.
        /// </summary>
        private void LoadProfile(FoundProfile profile)
        {
            string message;
            if (SettingsProfiles.Load(profile.Path, out message))
            {
                Manager.Keyboard.SettingsReplaced();
            }
            if (_window != null)
            {
                _window.ShowMessage(message);
            }
        }

        /// <summary>
        /// Bouton « Calibration » de l'onglet « Outils ».
        ///
        /// Difficulté : la fenêtre des paramètres est plein écran et « toujours devant »
        /// (Topmost). Si on lançait l'outil de l'eyetracker sans rien faire, sa fenêtre serait cachée
        /// derrière. On CACHE donc d'abord la fenêtre des paramètres (Hide : elle existe
        /// toujours, elle n'est simplement plus affichée).
        ///
        /// Ensuite, comme depuis la barre de menu, on note la fenêtre active AVANT de lancer
        /// l'outil : le mode calibration saura reconnaître la fenêtre de l'outil, qui sera la
        /// suivante à devenir active (voir ModeCalibration). En cachant la nôtre, Windows a
        /// rendu active une autre fenêtre (l'application d'avant) : c'est elle qu'on note.
        ///
        /// Si le lancement échoue, on réaffiche la fenêtre et on montre le message.
        /// </summary>
        private void LaunchCalibration()
        {
            if (_window == null)
            {
                return;
            }
            App.Log("Paramètres : calibration");
            _window.Hide();
            IntPtr previousWindow = NativeMethods.GetForegroundWindow();

            string message;
            if (Calibration.Launch(Manager.Gaze, out message))
            {
                // Quitter() fermera la fenêtre des paramètres (déjà cachée).
                Manager.ChangeMode(new CalibrationMode(previousWindow));
            }
            else
            {
                _window.Show();
                _window.Activate();
                _window.ShowMessage(message);
            }
        }

        /// <summary>
        /// Bouton « Diagnostic » de l'onglet « Outils » : retour au mode mouvement (la fenêtre
        /// des paramètres, toujours devant, cacherait la fenêtre Diagnostic), puis ouverture de
        /// la fenêtre Diagnostic.
        /// </summary>
        private void OpenDiagnostic()
        {
            App.Log("Paramètres : diagnostic");
            Manager.ChangeMode(new MovementMode());
            ((App)Application.Current).OpenDiagnostic();
        }

        /// <summary>Bouton « Retour » : retour au mode mouvement.</summary>
        private void Back()
        {
            App.Log("Paramètres : retour au mode mouvement");
            Manager.ChangeMode(new MovementMode());
        }
    }
}
