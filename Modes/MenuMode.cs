using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// MODE MENU (étape 4 du cahier des charges).
    ///
    /// Entrée : regarder en bas, hors de l'écran, pendant [DelaiMenu]. C'est le ModeManager
    /// qui surveille cela, uniquement depuis les modes mouvement, pause et jeu :
    /// voir Mode.MenuAutorise (étape 6).
    ///
    /// Affichage : une barre centrée en bas de l'écran, avec des boutons activés au regard
    /// (BoutonRegard) : on regarde un bouton pendant [DelaiActivation], son anneau se remplit,
    /// puis il s'active. De gauche à droite :
    ///   Paramètres   ouvre le mode paramètres (étape 7) ;
    ///   Jeu          passe en mode jeu (étape 9). Si l'on vient du mode jeu, ce bouton devient
    ///                « Quitter le jeu » et ramène au mode mouvement ;
    ///   Clavier      ouvre le clavier au regard de GazeBar (étape 12, voir ClavierRegard),
    ///                puis ferme le menu. S'il est déjà ouvert (caché par le mode jeu), le ferme ;
    ///   Pause        passe en mode pause ;
    ///   Suivi souris interrupteur : bord vert quand l'option est activée. Le menu reste
    ///                ouvert ; il faut quitter le bouton du regard pour pouvoir l'actionner
    ///                de nouveau ;
    ///   Quitter      demande une confirmation (« Oui, quitter » / « Non »), puis ferme GazeBar.
    ///
    /// Calibration et Diagnostic ne sont plus dans la barre (demande de l'utilisateur du
    /// 29/10/2026) : ils sont dans l'onglet « Outils » des paramètres (voir FenetreParametres).
    ///
    /// Depuis le mode jeu, le bouton « Quitter le jeu » a un bord vert, comme un interrupteur
    /// allumé : il montre que le mode jeu est actif (demande de l'utilisateur du 28/10/2026).
    ///
    /// Depuis le mode PAUSE (demande de l'utilisateur du 28/10/2026) : la barre ne montre
    /// d'abord QUE le bouton « Reprendre » (lecture). Ainsi, pendant la pause, un regard vers
    /// le bas ne peut rien déclencher d'autre. Activé, il met fin à la pause et ramène
    /// directement au mode mouvement. Pour les autres boutons, on rouvre ensuite le menu.
    ///
    /// Le cercle du regard garde sa taille normale ([RayonCercle], comme en mode mouvement :
    /// demande de l'utilisateur du 28/10/2026 ; avant, il était réduit de moitié). Il est
    /// caché tant que le regard est dans la zone de la barre.
    ///
    /// BARRE DE MENU DU CLAVIER (étape 12, choix de l'utilisateur du 31/10/2026) : quand le
    /// clavier au regard est affiché, regarder sous l'écran n'ouvre PAS la barre classique,
    /// mais celle du clavier (MenuClavier = vrai). Depuis le 01/11/2026 (remarques de
    /// l'utilisateur), de gauche à droite :
    ///   Haut / bas   met le clavier en haut de l'écran, ou le ramène en bas (retenu) ;
    ///   Taille       100 % → 75 % → 50 % → 100 %… (le bouton affiche la taille actuelle) ;
    ///   Zone de saisie  interrupteur (bord vert) : la bande des derniers mots tapés (étape 14) ;
    ///   Langue       ouvre la liste des dispositions du dossier Claviers (étape 14) ;
    ///   Pause        passe en mode pause (le clavier est caché, et revient à la reprise).
    /// Pas de bouton « Fermer » : il faisait double emploi avec celui placé au-dessus du
    /// clavier (retiré à la demande de l'utilisateur du 02/11/2026).
    /// Haut / bas, Taille et Zone de saisie laissent le menu ouvert, comme « Suivi souris ».
    /// Langue ouvre une liste en plein écran (ModeListeClavier). « Mes mots » est dans l'onglet
    /// Général des paramètres (demande de l'utilisateur du 02/11/2026). Le clavier reste
    /// affiché, et s'il est en bas il REMONTE au-dessus de la barre au lieu d'être recouvert
    /// (ClavierRegard.DefinirReserveBas) ; il redescend à la fermeture du menu.
    ///
    /// Sortie : quand le regard quitte la barre pendant [DelaiFermetureMenu], elle disparaît
    /// et on revient au mode précédent. Le regard en bas, hors de l'écran, compte comme
    /// « dans la barre » : c'est là qu'il est au moment où le menu s'ouvre. Un regard perdu ne
    /// compte pas (ni dedans, ni dehors).
    /// </summary>
    internal sealed class MenuMode : Mode
    {
        /// <summary>Les actions des boutons de la barre.</summary>
        private enum MenuAction
        {
            Settings,
            Game,
            QuitGame,
            Keyboard,
            KeyboardPosition,
            KeyboardSize,
            InputBox,
            Language,
            Pause,
            Resume,
            MouseTracking,
            Quit,
            ConfirmQuit,
            CancelQuit
        }

        /// <summary>
        /// Espace entre deux boutons, et entre les boutons et le bord de la barre, en pixels.
        /// Assez grand pour que l'anneau de progression d'un bouton ne touche pas son voisin.
        /// Étape 11 : ces trois distances sont des pixels de l'écran 4K, converties pour
        /// l'écran actuel (Ecran.Px, ou Ecran.PxRegard pour la marge du regard).
        /// </summary>
        private const double Gap4K = 60;

        private static double Gap
        {
            get { return ScreenMetrics.Px(Gap4K); }
        }

        /// <summary>Espace entre la barre et le bas de l'écran, en pixels de l'écran 4K.</summary>
        private const double BottomMargin4K = 30;

        /// <summary>
        /// Marge autour de la barre, en pixels de l'écran 4K : le regard y est encore « dans la
        /// barre ». Évite de fermer le menu quand le regard frôle le bord.
        /// </summary>
        private const double ZoneMargin4K = 60;

        // Les boutons affichés, et l'action de chacun (même ordre ; null = bouton indisponible).
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly List<MenuAction?> _actions = new List<MenuAction?>();
        private readonly ButtonAimer _aimer;

        // Le mode d'où l'on vient, pour y revenir à la fermeture.
        private Mode _back;

        // Vrai pendant la confirmation de « Quitter ».
        private bool _confirmation;

        // Un message affiché au-dessus de la barre (null = aucun).
        private string _message;

        // Vrai si la barre a pu être construite (le calque est affiché).
        private bool _built;

        // Vrai une fois la barre affichée depuis l'entrée dans le menu : seule la première
        // apparition est animée (pas la barre « Oui / Non » de la confirmation).
        private bool _alreadyShown;

        // La zone de la barre (avec sa marge), en pixels d'écran.
        private double _leftZone;
        private double _rightZone;
        private double _topZone;

        // Instant (ms) où le regard a quitté la barre ; -1 = il est dedans.
        private double _outsideBarStartMs = -1;

        // Le cercle du regard : position lissée en pixels d'écran.
        private readonly Smoother _smoothed = new Smoother();
        private bool _circlePlaced;

        /// <summary>La barre de menu classique.</summary>
        public MenuMode() : this(false)
        {
        }

        /// <summary>
        /// menuClavier = vrai : la barre de menu du clavier au regard (étape 12) au lieu de la
        /// barre classique.
        /// </summary>
        public MenuMode(bool keyboardMenu)
        {
            KeyboardMenu = keyboardMenu;
            _aimer = new ButtonAimer(_buttons);
        }

        /// <summary>Vrai pour la barre de menu du clavier (le clavier reste alors affiché dessous).</summary>
        public bool KeyboardMenu { get; private set; }

        public override string Name
        {
            get { return "Menu"; }
        }

        public override void Enter(GazeState gaze)
        {
            // Le ModeManager a déjà noté le mode d'avant : c'est là qu'on reviendra.
            _back = Manager.PreviousMode;
            _confirmation = false;
            _message = null;
            _outsideBarStartMs = -1;
            _alreadyShown = false;

            OverlayWindow layer = Manager.Layer;
            layer.HideRing();
            layer.HideIcon();
            layer.HideChoiceScreen();

            // Le cercle part de là où est le cercle actuel (pas de saut), sinon il
            // apparaîtra à la première mesure du regard sur l'écran.
            _circlePlaced = layer.CircleShown;
            if (_circlePlaced)
            {
                _smoothed.Place(layer.CircleX, layer.CircleY);
            }

            Build();
        }

        public override void Update(GazeState gaze)
        {
            if (!_built)
            {
                // Calque pas encore affiché (cas improbable) : on ne reste pas dans un menu invisible.
                Return();
                return;
            }

            AppSettings p = AppSettings.Current;
            double now = gaze.NowMs;

            // 1. Les boutons : l'un d'eux vient-il de s'activer ?
            int active = _aimer.Update(gaze, p.ActivationDelay, p.ShowLabels);
            if (active >= 0)
            {
                Activate(active);
                return;
            }

            // 2. La fermeture : regard hors de la barre pendant [DelaiFermetureMenu].
            if (gaze.Available)
            {
                if (InBar(gaze))
                {
                    _outsideBarStartMs = -1;
                }
                else if (_outsideBarStartMs < 0)
                {
                    _outsideBarStartMs = now;
                }
                else if (now - _outsideBarStartMs >= p.MenuCloseDelay * 1000.0)
                {
                    Return();
                    return;
                }
            }

            // 3. Le cercle du regard. Il suit la position ramenée dans l'écran : regard hors de
            //    l'écran → il glisse le long du bord (demande de l'utilisateur du 29/10/2026).
            //    Il est caché quand le regard est dans la zone de la barre (avec sa marge), ou en
            //    bas hors de l'écran : il gênerait la lecture des boutons (demande du 28/10/2026).
            //    Sa position continue d'être suivie : il réapparaît sans saut en sortant de la zone.
            //    Regard perdu : il reste où il est.
            if (gaze.Available)
            {
                if (!_circlePlaced)
                {
                    _smoothed.Place(gaze.XScreen, gaze.YScreen);
                    _circlePlaced = true;
                }
                _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);
                // Barre du clavier (étape 12) : le cercle est aussi caché au-dessus du clavier.
                if (InBar(gaze) || (KeyboardMenu && Manager.Keyboard.Contains(_smoothed.X, _smoothed.Y)))
                {
                    Manager.Layer.HideCircle();
                }
                else
                {
                    Manager.Layer.ShowCircle(_smoothed.X, _smoothed.Y, p.CircleRadiusPx, false);
                }
            }
        }

        public override void Quit()
        {
            // La barre est cachée. Le cercle reste : le mode suivant le reprend là où il est.
            // Fermeture animée (voir OverlayWindow.MasquerBarreMenu).
            Manager.Layer.HideMenuBar(true);
            _buttons.Clear();
            _actions.Clear();

            // Barre du clavier : le clavier peut redescendre (il était remonté au-dessus d'elle).
            if (KeyboardMenu)
            {
                Manager.Keyboard.SetBottomReserve(0);
            }
        }

        // ───────────────────────── Les actions ─────────────────────────

        private void Activate(int num)
        {
            MenuAction action = _actions[num].Value;
            App.Log("Menu : " + action);

            switch (action)
            {
                case MenuAction.Settings:
                    Manager.ChangeMode(new SettingsMode());
                    break;

                case MenuAction.Game:
                    Manager.ChangeMode(new GameMode());
                    break;

                case MenuAction.QuitGame:
                    Manager.ChangeMode(new MovementMode());
                    break;

                case MenuAction.Keyboard:
                    // Le clavier au regard (étape 12). Déjà ouvert (caché pendant le jeu) :
                    // le bouton le ferme. Sinon, il s'ouvre et apparaît au retour en mode
                    // mouvement. Fichier de disposition absent ou incorrect : message.
                    if (Manager.Keyboard.IsOpen)
                    {
                        Manager.Keyboard.Close();
                        Return();
                        break;
                    }
                    string keyboardMessage;
                    if (Manager.Keyboard.Open(out keyboardMessage))
                    {
                        // Depuis le jeu, on revient au mode mouvement : c'est là que le clavier s'affiche.
                        Manager.ChangeMode(new MovementMode());
                    }
                    else
                    {
                        ShowMessage(keyboardMessage);
                    }
                    break;

                case MenuAction.KeyboardPosition:
                    // Le menu reste ouvert ; la barre est reconstruite (le symbole change) et le
                    // bouton attend que le regard le quitte avant de pouvoir resservir.
                    Manager.Keyboard.TogglePosition();
                    Build();
                    break;

                case MenuAction.KeyboardSize:
                    Manager.Keyboard.ChangeSize();
                    Build();
                    break;

                case MenuAction.InputBox:
                    // Interrupteur, comme « Suivi souris » : le menu reste ouvert. La barre est
                    // reconstruite (bord vert) ; le bouton attend que le regard le quitte.
                    Manager.Keyboard.ToggleInputBox();
                    Build();
                    break;

                case MenuAction.Language:
                    Manager.ChangeMode(new KeyboardListMode(KeyboardListMode.ListKind.Language));
                    break;

                case MenuAction.Pause:
                    Manager.ChangeMode(new PauseMode());
                    break;

                case MenuAction.Resume:
                    // Fin de la pause : retour direct au mode mouvement, sans afficher les
                    // autres boutons (choix de l'utilisateur du 28/10/2026).
                    Manager.ChangeMode(new MovementMode());
                    break;

                case MenuAction.MouseTracking:
                    // Interrupteur : on inverse l'option, on l'enregistre, et on reste dans le menu.
                    AppSettings.Current.MouseTracking = !AppSettings.Current.MouseTracking;
                    AppSettings.Current.Save();
                    _buttons[num].SetLit(AppSettings.Current.MouseTracking);
                    App.Log("Suivi souris : " + (AppSettings.Current.MouseTracking ? "oui" : "non"));
                    _aimer.BlockUntilExit();
                    break;

                case MenuAction.Quit:
                    _confirmation = true;
                    _message = "Quitter GazeBar ?";
                    Build();
                    break;

                case MenuAction.ConfirmQuit:
                    App.Log("Menu : fermeture de GazeBar demandée");
                    Application.Current.Shutdown();
                    break;

                case MenuAction.CancelQuit:
                    _confirmation = false;
                    _message = null;
                    Build();
                    break;
            }
        }

        /// <summary>Ferme le menu et revient au mode d'avant (mode mouvement par défaut).</summary>
        private void Return()
        {
            Mode back = _back;
            if (back == null || back is MenuMode)
            {
                back = new MovementMode();
            }
            Manager.ChangeMode(back);
        }

        /// <summary>Affiche un message au-dessus de la barre (par exemple une erreur).</summary>
        private void ShowMessage(string message)
        {
            _message = message;
            Build();
        }

        // ───────────────────────── La barre ─────────────────────────

        /// <summary>
        /// (Re)construit la barre : les boutons du menu, ou ceux de la confirmation de
        /// « Quitter ». Toutes les positions sont en pixels d'écran, converties par le calque.
        /// </summary>
        private void Build()
        {
            OverlayWindow layer = Manager.Layer;
            _built = false;
            _buttons.Clear();
            _actions.Clear();
            if (!layer.Ready)
            {
                return;
            }

            AppSettings p = AppSettings.Current;
            double width = Manager.ScreenWidth;
            double height = Manager.ScreenHeight;

            layer.PrepareMenuBar();
            Canvas surface = layer.MenuBar;

            // ── Les boutons à afficher.
            List<GazeButton> buttons = new List<GazeButton>();
            List<MenuAction?> actions = new List<MenuAction?>();
            if (_back is PauseMode && !_confirmation)
            {
                // Pendant la pause : seul le bouton « Reprendre » (E768 = lecture).
                Add(buttons, actions, new GazeButton("", null, "Reprendre"), MenuAction.Resume);
            }
            else if (KeyboardMenu && !_confirmation)
            {
                // Barre de menu du clavier (étape 12, voir le commentaire de la classe).
                //   E70E / E70D = chevron vers le haut / vers le bas, E769 = pause,
                //   E70F = crayon (zone de saisie), E774 = globe (langue).
                if (p.KeyboardAtTop)
                {
                    Add(buttons, actions, new GazeButton("\uE70D", null, "Clavier en bas de l'écran"), MenuAction.KeyboardPosition);
                }
                else
                {
                    Add(buttons, actions, new GazeButton("\uE70E", null, "Clavier en haut de l'écran"), MenuAction.KeyboardPosition);
                }
                Add(buttons, actions,
                        GazeButton.Text(p.KeyboardSize.ToString("F0") + " %", "Taille du clavier (100, 75, 50 %)"),
                        MenuAction.KeyboardSize);
                // Étape 14 : zone de saisie (interrupteur) et langue (« Mes mots » est dans les paramètres).
                GazeButton zone = new GazeButton("\uE70F", null, "Zone de saisie");
                zone.SetLit(p.InputBox);
                Add(buttons, actions, zone, MenuAction.InputBox);
                Add(buttons, actions, new GazeButton("\uE774", null, "Langue du clavier"), MenuAction.Language);
                Add(buttons, actions, new GazeButton("\uE769", null, "Pause"), MenuAction.Pause);
            }
            else if (_confirmation)
            {
                // « Non » est à droite, « Oui » à gauche. La barre est plus courte et centrée :
                // aucun des deux n'est à la place de l'ancien bouton « Quitter » (tout à droite),
                // pour qu'on ne confirme jamais en continuant simplement à regarder au même endroit.
                Add(buttons, actions, new GazeButton("", null, "Oui, quitter GazeBar"), MenuAction.ConfirmQuit);
                Add(buttons, actions, new GazeButton("", null, "Non, ne pas quitter"), MenuAction.CancelQuit);
            }
            else
            {
                // Symboles de la police Windows (Segoe MDL2 Assets / Segoe Fluent Icons) :
                //   E713 engrenage, E7FC manette, E765 clavier, E769 pause,
                //   E768 lecture, E962 souris, E7E8 bouton marche/arrêt.
                Add(buttons, actions, new GazeButton("", null, "Paramètres"), MenuAction.Settings);
                if (_back is GameMode)
                {
                    // Bord vert : le mode jeu est actif.
                    GazeButton quitGame = new GazeButton("\uE7FC", null, "Quitter le jeu");
                    quitGame.SetLit(true);
                    Add(buttons, actions, quitGame, MenuAction.QuitGame);
                }
                else
                {
                    Add(buttons, actions, new GazeButton("\uE7FC", null, "Jeu"), MenuAction.Game);
                }
                Add(buttons, actions, new GazeButton("", null, "Clavier"), MenuAction.Keyboard);
                // (Depuis la pause, seul « Reprendre » est affiché : ici, on n'est donc
                // jamais en pause.)
                Add(buttons, actions, new GazeButton("", null, "Pause"), MenuAction.Pause);
                GazeButton tracking = new GazeButton("", null, "Suivi souris");
                tracking.SetLit(p.MouseTracking);
                Add(buttons, actions, tracking, MenuAction.MouseTracking);
                Add(buttons, actions, new GazeButton("", null, "Quitter GazeBar"), MenuAction.Quit);
            }

            // ── Taille des boutons : [TailleIcones], réduite si la barre ne tient pas dans
            //    la largeur de l'écran.
            int n = buttons.Count;
            // Les boutons du menu sont deux fois plus grands que les autres icônes
            // (demande de l'utilisateur du 22/10/2026).
            double size = 2 * p.IconSizePx;
            double maxSize = (width - ScreenMetrics.Px(20) - (n + 1) * Gap) / n;
            size = Math.Max(ScreenMetrics.Px(20), Math.Min(size, maxSize));

            // ── Le fond de la barre : un rectangle sombre aux coins arrondis, centré en bas.
            double barWidth = n * size + (n + 1) * Gap;
            double barHeight = size + 2 * Gap;
            double left = (width - barWidth) / 2;
            double top = height - ScreenMetrics.Px(BottomMargin4K) - barHeight;

            Border background = new Border();
            background.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24));
            background.BorderBrush = Brushes.Gray;
            background.BorderThickness = new Thickness(ScreenMetrics.Units(1));
            background.CornerRadius = new CornerRadius(ScreenMetrics.Units(16));
            background.Width = layer.LengthToWpf(barWidth);
            background.Height = layer.LengthToWpf(barHeight);
            Point corner = layer.ToWpf(left, top);
            Canvas.SetLeft(background, corner.X);
            Canvas.SetTop(background, corner.Y);
            surface.Children.Add(background);

            // ── Les boutons, de gauche à droite (libellé au-dessus : en dessous, il serait
            //    hors de l'écran).
            for (int i = 0; i < n; i++)
            {
                double x = left + Gap + size / 2 + i * (size + Gap);
                double y = top + Gap + size / 2;
                buttons[i].Place(layer, surface, x, y, size, true);
                _buttons.Add(buttons[i]);
                _actions.Add(actions[i]);
            }

            // ── Le message éventuel, centré au-dessus de la barre.
            double zoneTop = top;
            if (!string.IsNullOrEmpty(_message))
            {
                TextBlock text = new TextBlock();
                text.Text = _message;
                text.Foreground = Brushes.White;
                // Le message suit la taille du texte de Windows (étape 11).
                text.FontSize = ScreenMetrics.Font(28);
                Border frame = new Border();
                frame.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24));
                frame.CornerRadius = new CornerRadius(ScreenMetrics.Units(8));
                frame.Padding = new Thickness(ScreenMetrics.Units(16), ScreenMetrics.Units(6), ScreenMetrics.Units(16), ScreenMetrics.Units(6));
                frame.Child = text;
                frame.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Point topCenter = layer.ToWpf(width / 2, top - ScreenMetrics.Px(20));
                Canvas.SetLeft(frame, topCenter.X - frame.DesiredSize.Width / 2);
                Canvas.SetTop(frame, topCenter.Y - frame.DesiredSize.Height);
                surface.Children.Add(frame);

                // Le message fait partie de la barre pour la fermeture. Sa hauteur mesurée est
                // en unités WPF : on la convertit en pixels d'écran (étape 11 ; c'était une
                // estimation fixe de 80 px avant).
                double messageHeight = frame.DesiredSize.Height / layer.LengthToWpf(1);
                zoneTop = top - ScreenMetrics.Px(20) - messageHeight;
            }

            // ── La zone « dans la barre », avec sa marge.
            double zoneMargin = ScreenMetrics.GazePx(ZoneMargin4K);
            _leftZone = left - zoneMargin;
            _rightZone = left + barWidth + zoneMargin;
            _topZone = zoneTop - zoneMargin;

            // Barre du clavier : le clavier (s'il est en bas) remonte au-dessus de la barre et
            // de son message éventuel, avec un petit espace (demande du 01/11/2026).
            if (KeyboardMenu)
            {
                Manager.Keyboard.SetBottomReserve(height - zoneTop + ScreenMetrics.Px(20));
            }

            // Les boutons ont changé : on repart de zéro, et le bouton éventuellement sous le
            // regard ne s'active pas tant que le regard ne l'a pas quitté.
            _aimer.ResetAndBlock();

            // Première apparition depuis l'entrée dans le menu : animée (voir AfficherBarreMenu).
            layer.ShowMenuBar(!_alreadyShown);
            _alreadyShown = true;
            _built = true;
        }

        private static void Add(List<GazeButton> buttons, List<MenuAction?> actions,
                                    GazeButton button, MenuAction? action)
        {
            buttons.Add(button);
            actions.Add(action);
        }

        /// <summary>Vrai si le regard est sur la barre (avec sa marge), ou en bas hors de l'écran.</summary>
        private bool InBar(GazeState gaze)
        {
            if (gaze.BelowScreen)
            {
                return true;
            }
            return gaze.X >= _leftZone && gaze.X <= _rightZone && gaze.Y >= _topZone;
        }
    }
}
