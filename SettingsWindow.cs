using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// INTERFACE : la fenêtre du mode paramètres (étape 7), plein écran, fond noir.
    ///
    /// Mise en page validée par l'utilisateur le 26/10/2026 :
    ///   - sept onglets à gauche (CatalogueParametres ; six au départ, « Défilement » ajouté à
    ///     l'étape 8) ; un seul onglet affiché à la fois,
    ///     cinq réglages au plus, rien ne défile ;
    ///   - chaque réglage : son nom, une phrase d'explication, puis « − », la valeur, « + »
    ///     (ou un interrupteur pour un oui / non, voir plus bas) ; la valeur par défaut est
    ///     rappelée sous la valeur ;
    ///   - à droite : « Défaut » en haut (remet les valeurs de l'onglet affiché, après
    ///     confirmation) et « Retour » au milieu du bord (retour au mode mouvement).
    ///
    /// Étape 10 : un neuvième onglet, « Icônes », avec un seul gros bouton « Configurer les
    /// icônes » qui demande au mode d'ouvrir l'écran de configuration (ModeConfigIcones).
    ///
    /// 29/10/2026 : un dixième onglet, « Outils », avec deux gros boutons « Calibration » et
    /// « Diagnostic » (ils étaient avant dans la barre de menu). Comme pour « Configurer les
    /// icônes », la fenêtre ne fait que PRÉVENIR le mode (événements CalibrationDemandee et
    /// DiagnosticDemande) : c'est ModeParametres qui lance l'outil et change de mode.
    ///
    /// Étape 15 : un onglet « Profils » (catégorie Système), avec deux gros boutons :
    ///   - « Enregistrer les réglages actuels » : fait ici même (ProfilsParametres), un
    ///     message donne le nom du profil créé ;
    ///   - « Charger un profil » : prévient le mode (ListeProfilsDemandee), qui ouvre la liste
    ///     des profils en plein écran. Le profil choisi revient ici sous forme d'une
    ///     confirmation « Oui / Non » (constructeur, « profilAConfirmer ») ; « Oui » prévient
    ///     le mode (ChargementProfilDemande), qui charge le profil.
    ///
    /// POURQUOI UNE VRAIE FENÊTRE, et pas un dessin sur le calque comme les autres modes ?
    /// Le calque laisse passer les clics et ne prend jamais le clavier. Or l'aidant doit
    /// pouvoir cliquer à la souris et taper une valeur au clavier (ou la dicter). Cette
    /// fenêtre-ci accepte donc la souris et le clavier. Le regard, lui, est traité par
    /// ModeParametres, qui demande à la fenêtre où sont ses cibles.
    ///
    /// LA PAGE : tout est dessiné sur une surface de 1920 × 1080 unités (_page), placée dans
    /// une « Viewbox » qui l'agrandit ou la réduit à la taille réelle de l'écran. Les
    /// positions écrites ici sont donc celles d'un écran 1920 × 1080, quel que soit l'écran.
    /// Les boutons gardent une taille fixe : changer [TailleIcones] ne déplace pas ce qu'on
    /// est en train de regarder.
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal sealed class SettingsWindow : Window
    {
        public const double PageWidth = 1920;
        public const double PageHeight = 1080;

        // ── Mise en page (en unités de la page 1920 × 1080).
        private const double LeftTab = 60;
        private const double TabWidth = 340;
        private const double TabMaxHeight = 140;   // hauteur d'un onglet quand il y a la place
        private const double TabGap = 20;         // espace entre deux onglets
        private const double TopTabs = 60;         // haut du premier onglet
        private const double BottomTabs = 1040;        // le dernier onglet s'arrête avant cette ligne
        private const double TabIndent = 40;       // retrait des onglets sous leur catégorie (02/11/2026)
        private const double LeftContent = 460;
        private const double TopRow = 92;
        private const double StepRow = 184;           // d'une ligne de réglage à la suivante
        private const double RowHeight = 160;
        private const double LabelWidth = 520;
        private const double Gap = 18;
        private const double SideButton = 120;         // « − », « + », hauteur des boutons
        private const double ValueWidth = 290;
        private const double ChoiceWidth = 260;       // les deux boutons d'un choix entre deux mots

        // ── L'interrupteur d'un réglage oui / non (02/11/2026, style Bootstrap « switch »).
        //    La CIBLE du regard reste grande (toute la place des anciens boutons « Non » et
        //    « Oui » : 2 × 260 + 18 = 538 × 120) ; l'interrupteur dessiné est dedans.
        private const double SwitchWidth = 150;   // la piste (la « pilule »)
        private const double SwitchHeight = 76;
        private const double SwitchButton = 60;     // le rond qui glisse
        private static readonly Brush ActiveTrack = Freeze(new SolidColorBrush(Color.FromRgb(0x0D, 0x6E, 0xFD)));   // le bleu de Bootstrap
        private const double RightLeft = 1700;      // colonne de droite (Défaut, Retour)
        private const double RightSide = 160;
        private const double GazeDotDiameter = 50;

        // ── Couleurs.
        private static readonly Brush White = Freeze(new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF4)));
        private static readonly Brush Soft = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAD)));
        private static readonly Brush Border = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0x52, 0x5C)));
        private static readonly Brush ValueBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x1D, 0x22)));

        private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";

        // La page, son contenu (refait à chaque changement d'onglet) et le point du regard.
        private readonly Canvas _page;
        private readonly Canvas _content;
        private readonly Ellipse _gazeDot;

        // Les cibles du regard, dans l'ordre où elles ont été créées.
        private readonly List<SettingTarget> _targets = new List<SettingTarget>();

        // Pour chaque réglage numérique affiché : son cadre de valeur (pour la saisie au clavier).
        private readonly Dictionary<Setting, ValueFrame> _frames = new Dictionary<Setting, ValueFrame>();

        private int _tab;
        private bool _confirmation;

        // Un message affiché dans l'onglet « Outils » (par exemple « Calibration impossible… »)
        // ou « Profils » (« Profil chargé… »), ou null. Effacé quand on change d'onglet.
        private string _message;

        // Étape 15 : le profil choisi dans la liste, en attente de confirmation (« Charger le
        // profil … ? Oui / Non »), ou null.
        private FoundProfile _profileToConfirm;

        // La saisie au clavier en cours (null = aucune).
        private Setting _editedSetting;
        private TextBox _input;

        // Vrai quand c'est le mode qui ferme la fenêtre (et non Alt+F4, par exemple).
        private bool _closeRequested;

        /// <summary>
        /// Crée la fenêtre, ouverte sur l'onglet numéro « onglet » (0 = le premier). Au retour
        /// de l'écran de configuration des icônes, elle s'ouvre sur l'onglet « Icônes ».
        /// Étape 15 : au retour de la liste des profils, « profilAConfirmer » est le profil
        /// choisi ; la page montre alors la question « Charger le profil … ? ».
        /// </summary>
        public SettingsWindow(int tab = 0, FoundProfile profileToConfirm = null)
        {
            _tab = Math.Max(0, Math.Min(tab, SettingsCatalog.Tabs.Count - 1));
            _profileToConfirm = profileToConfirm;

            Title = "GazeBar — Paramètres";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            Background = Brushes.Black;

            _page = new Canvas();
            _page.Width = PageWidth;
            _page.Height = PageHeight;
            _page.Background = Brushes.Black;

            _content = new Canvas();
            _page.Children.Add(_content);

            // Le petit cercle gris du regard, par-dessus tout, et transparent à la souris.
            _gazeDot = new Ellipse();
            _gazeDot.Width = GazeDotDiameter;
            _gazeDot.Height = GazeDotDiameter;
            _gazeDot.Stroke = Brushes.LightGray;
            _gazeDot.StrokeThickness = 4;
            _gazeDot.IsHitTestVisible = false;
            _gazeDot.Visibility = Visibility.Collapsed;
            _page.Children.Add(_gazeDot);

            // La Viewbox met la page de 1920 × 1080 à l'échelle de l'écran, sans la déformer.
            Viewbox scale = new Viewbox();
            scale.Stretch = Stretch.Uniform;
            scale.Child = _page;
            Content = scale;

            Build();
        }

        // ───────────────────────── Ce que le mode utilise ─────────────────────────

        /// <summary>Demande de retour au mode mouvement (bouton « Retour », ou fermeture par Windows).</summary>
        public event Action BackRequested;

        /// <summary>Bouton « Configurer les icônes » de l'onglet « Icônes » (étape 10).</summary>
        public event Action IconConfigRequested;

        /// <summary>Bouton « Calibration » de l'onglet « Outils » (29/10/2026).</summary>
        public event Action CalibrationRequested;

        /// <summary>Bouton « Diagnostic » de l'onglet « Outils » (29/10/2026).</summary>
        public event Action DiagnosticRequested;

        /// <summary>Bouton « Mes mots » de l'onglet « Général » (étape 14).</summary>
        public event Action MyWordsRequested;

        /// <summary>Bouton « Charger un profil » de l'onglet « Profils » (étape 15).</summary>
        public event Action ProfileListRequested;

        /// <summary>« Oui » à la question « Charger le profil … ? » (étape 15) : le profil à charger.</summary>
        public event Action<FoundProfile> ProfileLoadRequested;

        /// <summary>
        /// Affiche un message dans l'onglet ouvert (« Outils » : la calibration n'a pas pu être
        /// lancée ; « Profils » : le profil est chargé…). La page est reconstruite pour le
        /// montrer.
        /// </summary>
        public void ShowMessage(string message)
        {
            _message = message;
            Build();
        }

        /// <summary>
        /// Les cibles du regard. La même liste est gardée d'un bout à l'autre : elle est
        /// seulement vidée et remplie de nouveau quand la page est reconstruite.
        /// </summary>
        public IReadOnlyList<IGazeTarget> Targets
        {
            get { return _targets; }
        }

        /// <summary>
        /// Augmente à chaque reconstruction de la page (nouvel onglet, confirmation…). Le mode
        /// s'en sert pour savoir que les cibles ont changé. « − » et « + » ne reconstruisent
        /// pas la page (seul le texte de la valeur change) : la répétition peut continuer.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>Vrai si l'activation de cette cible peut se répéter (« − » et « + »).</summary>
        public bool RepeatableTarget(int num)
        {
            return _targets[num].Repeat;
        }

        /// <summary>Active la cible indiquée (le regard s'y est posé assez longtemps).</summary>
        public void Activate(int num)
        {
            _targets[num].Action();
        }

        /// <summary>
        /// Convertit un point de l'écran (en pixels réels, comme le regard) en point de la page
        /// 1920 × 1080. PointFromScreen tient compte de la mise à l'échelle de Windows ET de
        /// celle de la Viewbox. Avant l'affichage de la fenêtre, renvoie un point très loin.
        /// </summary>
        public Point ToPage(double xScreen, double yScreen)
        {
            if (PresentationSource.FromVisual(_page) == null)
            {
                return new Point(-10000, -10000);
            }
            return _page.PointFromScreen(new Point(xScreen, yScreen));
        }

        /// <summary>Place le petit cercle du regard (coordonnées de la page).</summary>
        public void PlaceGazeDot(Point point)
        {
            Canvas.SetLeft(_gazeDot, point.X - GazeDotDiameter / 2);
            Canvas.SetTop(_gazeDot, point.Y - GazeDotDiameter / 2);
            _gazeDot.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Touche Échap pendant une saisie au clavier : la saisie est annulée (la valeur ne
        /// change pas) et on reste dans les paramètres. Renvoie vrai si une saisie était en
        /// cours ; sinon, Échap garde son rôle habituel (retour au mode mouvement).
        /// </summary>
        public bool CancelInput()
        {
            if (_editedSetting == null)
            {
                return false;
            }
            Setting setting = _editedSetting;
            _editedSetting = null;   // d'abord, pour que la perte du focus ne valide pas la saisie
            RestoreDisplay(setting);
            App.Log("Paramètres : saisie annulée (" + setting.Name + ")");
            return true;
        }

        /// <summary>Ferme la fenêtre (appelée par le mode quand on le quitte).</summary>
        public void CloseSettings()
        {
            // Une saisie en cours est validée, comme si l'aidant avait appuyé sur Entrée.
            ConfirmInput();
            _closeRequested = true;
            Close();
        }

        // ───────────────────────── Fenêtre ─────────────────────────

        /// <summary>
        /// Une fois la fenêtre créée par Windows, on lui donne la taille exacte de l'écran
        /// principal, comme pour le calque (voir OverlayWindow.CouvrirEcran).
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            CoverScreen();
        }

        /// <summary>
        /// L'écran a changé pendant que la fenêtre est ouverte (étape 11, appelée par le mode) :
        /// elle reprend la taille de l'écran, et la page est reconstruite (une saisie en cours
        /// est validée).
        /// </summary>
        public void AdaptToScreen()
        {
            CoverScreen();
            Build();
        }

        /// <summary>Donne à la fenêtre la taille exacte de l'écran principal.</summary>
        private void CoverScreen()
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null || source.CompositionTarget == null)
            {
                return;
            }
            double widthPixels;
            double heightPixels;
            NativeMethods.PrimaryScreenSize(out widthPixels, out heightPixels);
            Point size = source.CompositionTarget.TransformFromDevice.Transform(new Point(widthPixels, heightPixels));
            Left = 0;
            Top = 0;
            Width = size.X;
            Height = size.Y;
        }

        /// <summary>
        /// Fermeture demandée par Windows (Alt+F4…) et non par le mode : on l'annule, et on
        /// demande plutôt le retour au mode mouvement, qui fermera la fenêtre proprement.
        /// « BeginInvoke » : un peu plus tard, car on ne peut pas fermer une fenêtre pendant
        /// qu'elle est en train de se fermer.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_closeRequested)
            {
                e.Cancel = true;
                Dispatcher.BeginInvoke(new Action(RequestBack));
            }
            base.OnClosing(e);
        }

        private void RequestBack()
        {
            Action subscribers = BackRequested;
            if (subscribers != null)
            {
                subscribers();
            }
        }

        // ───────────────────────── Construction de la page ─────────────────────────

        private static AppSettings P
        {
            get { return AppSettings.Current; }
        }

        /// <summary>(Re)construit toute la page pour l'onglet choisi.</summary>
        private void Build()
        {
            // Une saisie en cours est validée avant de tout refaire.
            ConfirmInput();

            _content.Children.Clear();
            _targets.Clear();
            _frames.Clear();

            SettingsTab tab = SettingsCatalog.Tabs[_tab];

            // ── Le titre, en haut.
            TextBlock title = Text("Paramètres — " + tab.Name, 30, Soft, false);
            Place(title, LeftContent, 26);

            // ── La colonne de gauche : les CATÉGORIES « dépliantes » (02/11/2026, choix de
            //    l'utilisateur). On voit les quatre catégories ; seule celle qui contient
            //    l'onglet ouvert montre ses onglets, juste en dessous d'elle et en retrait.
            //    Au plus 4 + 3 = 7 boutons (au lieu de 11 avant), donc des boutons plus hauts.
            BuildLeftColumn();

            // ── Le contenu : les réglages, ou la confirmation de « Défaut », ou celle du
            //    chargement d'un profil (étape 15).
            if (_confirmation)
            {
                BuildConfirmation(tab);
            }
            else if (_profileToConfirm != null)
            {
                BuildProfileConfirmation();
            }
            else
            {
                for (int i = 0; i < tab.Settings.Count; i++)
                {
                    BuildRow(tab.Settings[i], TopRow + i * StepRow);
                }
                if (tab.Name == SettingsCatalog.GeneralTabName)
                {
                    BuildFileInfo(TopRow + tab.Settings.Count * StepRow);
                    BuildMyWordsButton();
                }
                if (tab.Name == DisplayTabName)
                {
                    BuildScreenInfo(TopRow + tab.Settings.Count * StepRow);
                }
                if (tab.Name == SettingsCatalog.IconsTabName)
                {
                    BuildIconsTab();
                }
                if (tab.Name == SettingsCatalog.ToolsTabName)
                {
                    BuildToolsTab();
                }
                if (tab.Name == SettingsCatalog.ProfilesTabName)
                {
                    BuildProfilesTab();
                }
            }

            // ── La colonne de droite : « Défaut » en haut, « Retour » au milieu du bord.
            //    E72C = flèches circulaires (remettre), E72B = flèche de retour.
            //    Pas de « Défaut » pour un onglet sans rien à remettre (« Outils », « Profils ») : un bouton
            //    qui ne fait rien serait déroutant.
            bool hasDefaults = tab.Settings.Count > 0 || tab.Name == SettingsCatalog.IconsTabName;
            if (hasDefaults)
            {
                AddTarget(RightLeft, 60, RightSide, RightSide, SymbolAndWord("", "Défaut"),
                             RequestDefaults, false, false);
            }
            AddTarget(RightLeft, (PageHeight - RightSide) / 2, RightSide, RightSide,
                         SymbolAndWord("", "Retour"), RequestBack, false, false);

            Version++;
        }

        /// <summary>
        /// La colonne de gauche : les catégories, et sous la catégorie ouverte ses onglets.
        ///
        /// Regarder une autre catégorie l'ouvre directement sur son premier onglet (un seul
        /// regard, comme avant pour changer d'onglet). Regarder la catégorie déjà ouverte ne
        /// fait rien. L'onglet ouvert est encadré comme avant ; la catégorie ouverte a son
        /// symbole en couleur.
        /// </summary>
        private void BuildLeftColumn()
        {
            int openCategory = SettingsCatalog.CategoryOf(_tab);
            List<int> openTabs = SettingsCatalog.TabsOf(openCategory);

            // Le nombre de lignes de la colonne : toutes les catégories + les onglets de la
            // catégorie ouverte. Les lignes se partagent la hauteur de la page, sans dépasser
            // la hauteur maximale (140) : avec 7 lignes, chacune fait environ 120 de haut.
            int rowCount = SettingsCatalog.Categories.Count + openTabs.Count;
            double step = Math.Min(TabMaxHeight + TabGap,
                                  (BottomTabs - TopTabs + TabGap) / Math.Max(1, rowCount));

            double y = TopTabs;
            for (int c = 0; c < SettingsCatalog.Categories.Count; c++)
            {
                SettingsCategory category = SettingsCatalog.Categories[c];
                bool open = c == openCategory;

                // La catégorie : symbole (en couleur si elle est ouverte) et nom.
                UIElement categoryRow = RowColumn(category.Symbol, category.Name, open, 46, 36, 24);
                int categoryIndex = c;   // copie pour la fonction ci-dessous
                AddTarget(LeftTab, y, TabWidth, step - TabGap, categoryRow,
                             () => ChooseCategory(categoryIndex), false, false);
                y += step;

                if (!open)
                {
                    continue;
                }

                // Les onglets de la catégorie ouverte, en retrait vers la droite (le retrait
                // montre qu'ils « appartiennent » à la catégorie du dessus).
                foreach (int tabIndex in openTabs)
                {
                    SettingsTab o = SettingsCatalog.Tabs[tabIndex];
                    UIElement tabRow = RowColumn(o.Symbol, o.Name, tabIndex == _tab, 34, 30, 14);
                    int num = tabIndex;   // copie pour la fonction ci-dessous
                    AddTarget(LeftTab + TabIndent, y, TabWidth - TabIndent, step - TabGap,
                                 tabRow, () => ChooseTab(num), false, tabIndex == _tab);
                    y += step;
                }
            }
        }

        /// <summary>
        /// Le contenu d'un bouton de la colonne de gauche : un symbole puis un nom. Les onglets
        /// ont un texte un peu plus petit et une marge plus courte que les catégories, pour que
        /// « Loupe, glisser » tienne dans le bouton en retrait.
        /// </summary>
        private static UIElement RowColumn(string symbol, string name, bool inColor,
                                              double symbolSize, double nameSize, double margin)
        {
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Margin = new Thickness(margin, 0, 0, 0);
            TextBlock symbolBlock = Text(symbol, symbolSize, inColor ? SettingTarget.Accent : Soft, false);
            symbolBlock.FontFamily = new FontFamily(IconFont);
            symbolBlock.Width = symbolSize + 22;
            symbolBlock.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(symbolBlock);
            TextBlock nameBlock = Text(name, nameSize, White, true);
            nameBlock.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(nameBlock);
            return row;
        }

        /// <summary>Une ligne de réglage : nom et explication, puis les boutons.</summary>
        private void BuildRow(Setting setting, double top)
        {
            // Nom et explication, centrés verticalement dans la hauteur de la ligne.
            StackPanel texts = new StackPanel();
            texts.Width = LabelWidth;
            texts.Height = RowHeight;
            StackPanel center = new StackPanel();
            center.VerticalAlignment = VerticalAlignment.Center;
            center.Children.Add(Text(setting.Name, 36, White, true));
            TextBlock explanation = Text(setting.Explanation, 23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            center.Children.Add(explanation);
            Grid textFrame = new Grid();
            textFrame.Width = LabelWidth;
            textFrame.Height = RowHeight;
            textFrame.Children.Add(center);
            Place(textFrame, LeftContent, top);

            double x = LeftContent + LabelWidth + Gap;
            double y = top + (RowHeight - SideButton) / 2;

            if (setting.Switch)
            {
                // Un oui / non : une seule grande cible ; la regarder inverse la valeur.
                // ModeParametres bloque la cible jusqu'à ce que le regard la quitte : rester
                // dessus ne fait donc pas basculer l'interrupteur en boucle.
                bool current = setting.ReadYesNo(P);
                AddTarget(x, y, 2 * ChoiceWidth + Gap, SideButton, SwitchDrawing(current, current ? setting.RightWord : setting.WrongWord),
                             () => ChangeYesNo(setting, !current), false, false);
                return;
            }

            if (setting.YesNo)
            {
                // Un choix entre deux mots (« Directe » / « Relative »…) : deux boutons.
                bool val = setting.ReadYesNo(P);
                AddTarget(x, y, ChoiceWidth, SideButton, Word(setting.WrongWord, 42), () => ChangeYesNo(setting, false), false, !val);
                AddTarget(x + ChoiceWidth + Gap, y, ChoiceWidth, SideButton, Word(setting.RightWord, 42), () => ChangeYesNo(setting, true), false, val);
                return;
            }

            // « − » (U+2212, le vrai signe moins), la valeur, « + ». Les deux boutons se répètent.
            AddTarget(x, y, SideButton, SideButton, Word("−", 72), () => ChangeNumber(setting, -1), true, false);
            AddTarget(x + SideButton + Gap + ValueWidth + Gap, y, SideButton, SideButton, Word("+", 72),
                         () => ChangeNumber(setting, +1), true, false);

            // La valeur : un cadre qui n'est PAS une cible du regard. L'aidant clique dessus
            // pour taper une valeur au clavier.
            ValueFrame frame = new ValueFrame();
            frame.Value = Text(setting.Text(P), 50, White, true);
            frame.Value.HorizontalAlignment = HorizontalAlignment.Center;
            AppSettings defaults = new AppSettings();
            TextBlock callbackFn = Text("défaut " + setting.Text(defaults), 20, Soft, false);
            callbackFn.HorizontalAlignment = HorizontalAlignment.Center;
            StackPanel display = new StackPanel();
            display.VerticalAlignment = VerticalAlignment.Center;
            display.Children.Add(frame.Value);
            display.Children.Add(callbackFn);
            frame.Display = display;

            frame.Border = new Border();
            frame.Border.Width = ValueWidth;
            frame.Border.Height = SideButton;
            frame.Border.CornerRadius = new CornerRadius(14);
            frame.Border.BorderThickness = new Thickness(2);
            frame.Border.BorderBrush = Border;
            frame.Border.Background = ValueBackground;
            frame.Border.Cursor = Cursors.IBeam;
            frame.Border.ToolTip = "Cliquer pour taper une valeur (Entrée valide, Échap annule)";
            frame.Border.Child = display;
            frame.Border.MouseLeftButtonUp += (s, e) =>
            {
                StartInput(setting);
                e.Handled = true;
            };
            Place(frame.Border, x + SideButton + Gap, y);
            _frames[setting] = frame;
        }

        /// <summary>Le nom de l'onglet « Affichage », sous lequel l'écran détecté est rappelé (étape 11).</summary>
        private const string DisplayTabName = "Affichage";

        /// <summary>
        /// Onglet « Affichage » (étape 11) : l'écran détecté par GazeBar (résolution, mise à
        /// l'échelle et taille du texte de Windows), pour vérifier que le changement a été vu.
        /// </summary>
        private void BuildScreenInfo(double top)
        {
            StackPanel info = new StackPanel();
            info.Children.Add(Text("Écran détecté", 36, White, true));
            TextBlock description = Text(ScreenMetrics.Description(), 24, Soft, false);
            description.Margin = new Thickness(0, 6, 0, 0);
            info.Children.Add(description);
            info.Children.Add(Text("Tailles en % de la hauteur de l'écran.", 23, Soft, false));
            Place(info, LeftContent, top + 30);
        }

        /// <summary>Onglet Général : rappel de l'endroit où les réglages sont enregistrés.</summary>
        private void BuildFileInfo(double top)
        {
            StackPanel info = new StackPanel();
            info.Children.Add(Text("Fichier des réglages", 36, White, true));
            TextBlock path = Text(AppSettings.FilePath, 24, Soft, false);
            path.FontFamily = new FontFamily("Consolas");
            path.TextWrapping = TextWrapping.Wrap;
            path.Width = 1140;
            path.Margin = new Thickness(0, 6, 0, 0);
            info.Children.Add(path);
            info.Children.Add(Text("Enregistré à chaque changement.", 23, Soft, false));
            Place(info, LeftContent, top + 30);
        }

        /// <summary>
        /// Onglet « Général » (étape 14, demande de l'utilisateur du 02/11/2026) : un gros bouton
        /// « Mes mots », sous le rappel du fichier des réglages. Il ouvre la liste des mots
        /// ajoutés au dictionnaire personnel du clavier (ModeListeClavier), pour en retirer.
        /// L'onglet Clavier n'avait plus de place (cinq réglages). E82D = dictionnaire.
        /// </summary>
        private void BuildMyWordsButton()
        {
            AddTarget(LeftContent + 170, 640, 800, 180, BigButton("\uE82D", "Mes mots"),
                         () => Notify(MyWordsRequested), false, false);
            TextBlock explanation = Text("Les mots ajoutés au clavier avec « + » : regarder un mot le retire.",
                                          23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            Place(explanation, LeftContent, 840);
        }

        /// <summary>
        /// Onglet « Icônes » (étape 10) : une explication, puis un gros bouton qui ouvre l'écran
        /// de configuration des icônes du choix d'action. E71D = grille de carrés.
        /// </summary>
        private void BuildIconsTab()
        {
            StackPanel texts = new StackPanel();
            texts.Children.Add(Text("Icônes du choix d'action", 36, White, true));
            TextBlock explanation = Text(
                "Choisir les actions de la couronne, des modificateurs (bord gauche) et des boutons " +
                "Copier, Couper, Coller (bord haut). Un emplacement peut rester vide.", 23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            texts.Children.Add(explanation);
            Place(texts, LeftContent, TopRow + 20);

            StackPanel button = new StackPanel();
            button.Orientation = Orientation.Horizontal;
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.VerticalAlignment = VerticalAlignment.Center;
            TextBlock symbol = Text("\uE71D", 60, White, false);
            symbol.FontFamily = new FontFamily(IconFont);
            symbol.VerticalAlignment = VerticalAlignment.Center;
            symbol.Margin = new Thickness(0, 0, 30, 0);
            button.Children.Add(symbol);
            TextBlock word = Text("Configurer les icônes", 42, White, true);
            word.VerticalAlignment = VerticalAlignment.Center;
            button.Children.Add(word);

            AddTarget(LeftContent + 170, 420, 800, 180, button, RequestIconConfig, false, false);
        }

        /// <summary>
        /// Onglet « Outils » (29/10/2026) : deux gros boutons l'un au-dessus de l'autre, avec un
        /// grand écart entre eux pour ne pas activer l'un en visant l'autre.
        ///   E890 = œil (Calibration), E9D9 = diagnostic (courbe).
        /// Sous les boutons : le message éventuel (calibration impossible…).
        /// </summary>
        private void BuildToolsTab()
        {
            StackPanel texts = new StackPanel();
            texts.Children.Add(Text("Outils", 36, White, true));
            TextBlock explanation = Text(
                "Calibration : lance l'outil de calibration de l'eyetracker ; GazeBar revient en mode mouvement " +
                "quand il se ferme. Diagnostic : ouvre la fenêtre qui suit le regard et le curseur.",
                23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            texts.Children.Add(explanation);
            Place(texts, LeftContent, TopRow + 20);

            AddTarget(LeftContent + 170, 320, 800, 180, BigButton("\uE890", "Calibration"),
                         () => Notify(CalibrationRequested), false, false);
            AddTarget(LeftContent + 170, 620, 800, 180, BigButton("\uE9D9", "Diagnostic"),
                         () => Notify(DiagnosticRequested), false, false);

            BuildMessage();
        }

        /// <summary>Le message éventuel de l'onglet (sous les gros boutons), en bleu.</summary>
        private void BuildMessage()
        {
            if (!string.IsNullOrEmpty(_message))
            {
                TextBlock message = Text(_message, 30, SettingTarget.Accent, true);
                message.TextWrapping = TextWrapping.Wrap;
                message.Width = 1140;
                Place(message, LeftContent, 870);
            }
        }

        /// <summary>
        /// Onglet « Profils » (étape 15) : une explication avec le dossier des profils, puis
        /// deux gros boutons l'un au-dessus de l'autre (même hauteur que ceux de « Outils »,
        /// mais plus larges pour le long texte,
        /// avec le même grand écart). E74E = disquette (Enregistrer), E8E5 = dossier ouvert
        /// (Charger). Sous les boutons : le message (« Réglages enregistrés dans… »).
        /// </summary>
        private void BuildProfilesTab()
        {
            StackPanel texts = new StackPanel();
            texts.Children.Add(Text("Profils de réglages", 36, White, true));
            TextBlock explanation = Text(
                "Garder plusieurs jeux de réglages et passer de l'un à l'autre. Pour renommer ou " +
                "supprimer un profil, l'aidant le fait dans l'Explorateur, dans le dossier :",
                23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            texts.Children.Add(explanation);
            TextBlock folder = Text(SettingsProfiles.Folder, 22, Soft, false);
            folder.FontFamily = new FontFamily("Consolas");
            folder.TextWrapping = TextWrapping.Wrap;
            folder.Width = 1140;
            folder.Margin = new Thickness(0, 4, 0, 0);
            texts.Children.Add(folder);
            Place(texts, LeftContent, TopRow + 20);

            AddTarget(LeftContent + 70, 320, 1000, 180, BigButton("\uE74E", "Enregistrer les réglages actuels"),
                         SaveProfile, false, false);
            AddTarget(LeftContent + 70, 620, 1000, 180, BigButton("\uE8E5", "Charger un profil"),
                         () => Notify(ProfileListRequested), false, false);

            BuildMessage();
        }

        /// <summary>
        /// La question « Charger le profil … ? » (étape 15), comme la confirmation de
        /// « Défaut » : « Oui » et « Non » au milieu de la page. Pour la sauvegarde
        /// automatique, la question dit plutôt qu'on revient aux réglages d'avant.
        /// </summary>
        private void BuildProfileConfirmation()
        {
            string questionText = _profileToConfirm.IsBackup
                ? "Revenir aux réglages d'avant le dernier chargement de profil ?"
                : "Charger le profil « " + _profileToConfirm.Name + " » ?";
            TextBlock question = Text(questionText, 40, White, true);
            question.TextWrapping = TextWrapping.Wrap;
            question.Width = 1140;
            Place(question, LeftContent, 260);

            TextBlock precision = Text(
                "Tous les réglages changent tout de suite. Les réglages actuels sont d'abord gardés " +
                "dans le profil « " + SettingsProfiles.BackupName + " ».", 23, Soft, false);
            precision.TextWrapping = TextWrapping.Wrap;
            precision.Width = 1140;
            Place(precision, LeftContent, 380);

            AddTarget(640, 480, 300, SideButton, Word("Oui", 42), ConfirmProfile, false, false);
            AddTarget(1040, 480, 300, SideButton, Word("Non", 42), CancelProfile, false, false);
        }

        /// <summary>Bouton « Enregistrer les réglages actuels » : un nouveau profil daté.</summary>
        private void SaveProfile()
        {
            ConfirmInput();
            string message;
            SettingsProfiles.SaveCurrent(out message);
            ShowMessage(message);
        }

        /// <summary>« Oui » : le mode charge le profil (puis affiche le message ici).</summary>
        private void ConfirmProfile()
        {
            FoundProfile profile = _profileToConfirm;
            _profileToConfirm = null;
            Action<FoundProfile> subscribers = ProfileLoadRequested;
            if (subscribers != null && profile != null)
            {
                subscribers(profile);
            }
            else
            {
                Build();
            }
        }

        /// <summary>« Non » : on revient à l'onglet « Profils », rien ne change.</summary>
        private void CancelProfile()
        {
            App.Log("Profils : chargement annulé");
            _profileToConfirm = null;
            Build();
        }

        /// <summary>Le contenu d'un gros bouton : un symbole à gauche, puis un mot.</summary>
        private static UIElement BigButton(string symbol, string word)
        {
            StackPanel button = new StackPanel();
            button.Orientation = Orientation.Horizontal;
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.VerticalAlignment = VerticalAlignment.Center;
            TextBlock icon = Text(symbol, 60, White, false);
            icon.FontFamily = new FontFamily(IconFont);
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 30, 0);
            button.Children.Add(icon);
            TextBlock text = Text(word, 42, White, true);
            text.VerticalAlignment = VerticalAlignment.Center;
            button.Children.Add(text);
            return button;
        }

        /// <summary>
        /// Prévient le mode (déclenche l'événement donné, s'il a des abonnés). Une saisie en
        /// cours est d'abord validée.
        /// </summary>
        private void Notify(Action evt)
        {
            ConfirmInput();
            if (evt != null)
            {
                evt();
            }
        }

        private void RequestIconConfig()
        {
            ConfirmInput();
            Action subscribers = IconConfigRequested;
            if (subscribers != null)
            {
                subscribers();
            }
        }

        /// <summary>
        /// La confirmation de « Défaut ». « Oui » et « Non » sont au milieu de la page, loin du
        /// bouton « Défaut » (en haut à droite) : on ne confirme jamais en continuant
        /// simplement à regarder au même endroit.
        /// </summary>
        private void BuildConfirmation(SettingsTab tab)
        {
            string questionText = tab.Name == SettingsCatalog.IconsTabName
                ? "Remettre la disposition d'origine des icônes du choix d'action ?"
                : "Remettre les valeurs par défaut de l'onglet « " + tab.Name + " » ?";
            TextBlock question = Text(questionText, 40, White, true);
            question.TextWrapping = TextWrapping.Wrap;
            question.Width = 1140;
            Place(question, LeftContent, 300);

            AddTarget(640, 480, 300, SideButton, Word("Oui", 42), () => RestoreDefaults(tab), false, false);
            AddTarget(1040, 480, 300, SideButton, Word("Non", 42), CancelDefaults, false, false);
        }

        // ───────────────────────── Les actions ─────────────────────────

        /// <summary>
        /// Regard sur une catégorie : si elle n'est pas déjà ouverte, elle s'ouvre sur son
        /// premier onglet. Si elle est déjà ouverte, rien ne change (on ne quitte pas l'onglet
        /// en cours par erreur).
        /// </summary>
        private void ChooseCategory(int category)
        {
            if (category == SettingsCatalog.CategoryOf(_tab))
            {
                return;
            }
            List<int> tabs = SettingsCatalog.TabsOf(category);
            if (tabs.Count > 0)
            {
                App.Log("Paramètres : catégorie " + SettingsCatalog.Categories[category].Name);
                ChooseTab(tabs[0]);
            }
        }

        private void ChooseTab(int num)
        {
            _tab = num;
            _confirmation = false;
            _profileToConfirm = null;
            _message = null;
            App.Log("Paramètres : onglet " + SettingsCatalog.Tabs[num].Name);
            Build();
        }

        /// <summary>« − » ou « + » : seul le texte de la valeur change (pas de reconstruction).</summary>
        private void ChangeNumber(Setting setting, int direction)
        {
            ConfirmInput();
            setting.Change(P, direction);
            Save(setting);
            ValueFrame frame;
            if (_frames.TryGetValue(setting, out frame))
            {
                frame.Value.Text = setting.Text(P);
            }
        }

        private void ChangeYesNo(Setting setting, bool val)
        {
            if (setting.ReadYesNo(P) == val)
            {
                return;   // déjà choisi : rien à faire
            }
            setting.WriteYesNo(P, val);
            Save(setting);
            Build();   // pour redessiner l'interrupteur (ou encadrer le nouveau choix)
        }

        private void RequestDefaults()
        {
            _confirmation = true;
            Build();
        }

        private void CancelDefaults()
        {
            _confirmation = false;
            Build();
        }

        /// <summary>Remet les réglages de l'onglet à leur valeur de départ (lue dans un objet neuf).</summary>
        private void RestoreDefaults(SettingsTab tab)
        {
            AppSettings defaults = new AppSettings();
            if (tab.Name == SettingsCatalog.IconsTabName)
            {
                P.DefaultIcons();   // l'onglet « Icônes » n'a pas de réglage, mais une disposition
            }
            foreach (Setting setting in tab.Settings)
            {
                setting.Copy(defaults, P);
                if (setting.AfterChange != null)
                {
                    setting.AfterChange(P);
                }
            }
            P.Save();
            App.Log("Paramètres : valeurs par défaut de l'onglet " + tab.Name);
            _confirmation = false;
            Build();
        }

        /// <summary>Enregistre le fichier après un changement, et le note dans le journal.</summary>
        private static void Save(Setting setting)
        {
            if (setting.AfterChange != null)
            {
                setting.AfterChange(P);
            }
            P.Save();
            App.Log("Paramètres : " + setting.Name + " = " + setting.Text(P));
        }

        // ───────────────────────── Saisie au clavier (pour l'aidant) ─────────────────────────

        /// <summary>
        /// Remplace l'affichage de la valeur par une zone de texte où l'aidant tape (ou dicte)
        /// une valeur. Entrée valide ; cliquer ailleurs valide aussi ; Échap annule (voir
        /// AnnulerSaisie, appelée par App quand la touche Échap est enfoncée).
        /// </summary>
        private void StartInput(Setting setting)
        {
            if (_editedSetting == setting)
            {
                return;
            }
            ConfirmInput();

            ValueFrame frame;
            if (!_frames.TryGetValue(setting, out frame))
            {
                return;
            }

            _input = new TextBox();
            _input.Text = setting.NumberText(setting.ReadNumber(P));
            _input.FontSize = 46;
            _input.Width = ValueWidth - 30;
            _input.HorizontalContentAlignment = HorizontalAlignment.Center;
            _input.VerticalAlignment = VerticalAlignment.Center;
            _input.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ConfirmInput();
                    e.Handled = true;
                }
            };
            _input.LostKeyboardFocus += (s, e) => ConfirmInput();

            _editedSetting = setting;
            frame.Border.Child = _input;

            // La fenêtre doit avoir le clavier pour que la frappe arrive dans la zone de texte.
            Activate();
            _input.Focus();
            Keyboard.Focus(_input);
            _input.SelectAll();
        }

        /// <summary>Valide la saisie en cours, s'il y en a une.</summary>
        private void ConfirmInput()
        {
            if (_editedSetting == null)
            {
                return;
            }
            Setting setting = _editedSetting;
            string text = _input.Text;
            _editedSetting = null;   // d'abord, pour ne pas valider deux fois (perte du focus)

            double val;
            if (Setting.ReadInput(text, out val))
            {
                setting.WriteNumber(P, val);   // ramenée dans ses limites par Corriger
                Save(setting);
            }
            else
            {
                App.Log("Paramètres : saisie illisible pour " + setting.Name + " : « " + text + " »");
            }
            RestoreDisplay(setting);
        }

        /// <summary>Remet l'affichage normal de la valeur à la place de la zone de texte.</summary>
        private void RestoreDisplay(Setting setting)
        {
            ValueFrame frame;
            if (_frames.TryGetValue(setting, out frame))
            {
                frame.Value.Text = setting.Text(P);
                frame.Border.Child = frame.Display;
            }
            _input = null;
        }

        // ───────────────────────── Petits outils ─────────────────────────

        /// <summary>
        /// Crée une cible du regard à cette place, avec ce contenu. Elle répond aussi au clic
        /// de souris (pour l'aidant). repetition : vrai pour « − » et « + » ; choisie : vrai
        /// pour l'onglet affiché ou le choix actuel (Non / Oui), encadré en bleu.
        /// </summary>
        private void AddTarget(double x, double y, double width, double height, UIElement content,
                                  Action action, bool repeat, bool chosen)
        {
            SettingTarget target = new SettingTarget(new Rect(x, y, width, height), content, action, repeat, chosen);
            target.Background.MouseLeftButtonUp += (s, e) =>
            {
                action();
                e.Handled = true;
            };
            Place(target.Background, x, y);
            _targets.Add(target);
        }

        private void Place(UIElement element, double x, double y)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            _content.Children.Add(element);
        }

        private static TextBlock Text(string text, double size, Brush color, bool bold)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontSize = size;
            block.Foreground = color;
            block.FontFamily = new FontFamily("Segoe UI");
            if (bold)
            {
                block.FontWeight = FontWeights.Bold;
            }
            return block;
        }

        /// <summary>Un mot (ou un signe) centré, pour le contenu d'un bouton.</summary>
        private static UIElement Word(string word, double size)
        {
            TextBlock block = Text(word, size, White, true);
            block.HorizontalAlignment = HorizontalAlignment.Center;
            block.VerticalAlignment = VerticalAlignment.Center;
            return block;
        }

        /// <summary>
        /// Le dessin d'un interrupteur (comme le « switch » de Bootstrap), suivi du mot
        /// « Oui » ou « Non ». La piste est une Border aux bouts tout ronds (CornerRadius =
        /// la moitié de la hauteur), le rond est une Ellipse posée à gauche ou à droite :
        ///   - activé : piste bleue pleine, rond blanc à DROITE ;
        ///   - désactivé : piste sombre bordée de gris, rond gris à GAUCHE.
        /// Ce n'est qu'un dessin : c'est la cible qui le contient qui réagit au regard.
        /// </summary>
        private static UIElement SwitchDrawing(bool active, string word)
        {
            Border track = new Border();
            track.Width = SwitchWidth;
            track.Height = SwitchHeight;
            track.CornerRadius = new CornerRadius(SwitchHeight / 2);
            track.BorderThickness = new Thickness(3);
            track.BorderBrush = active ? ActiveTrack : Soft;
            track.Background = active ? ActiveTrack : ValueBackground;
            track.VerticalAlignment = VerticalAlignment.Center;

            // Le rond : marge pour qu'il ne touche pas le bord de la piste.
            double margin = (SwitchHeight - 6 - SwitchButton) / 2;
            Ellipse round = new Ellipse();
            round.Width = SwitchButton;
            round.Height = SwitchButton;
            round.Fill = active ? White : Soft;
            round.HorizontalAlignment = active ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            round.VerticalAlignment = VerticalAlignment.Center;
            round.Margin = new Thickness(margin);
            track.Child = round;

            TextBlock text = Text(word, 42, active ? White : Soft, true);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Margin = new Thickness(30, 0, 0, 0);
            text.MinWidth = 90;   // « Oui » et « Non » : l'interrupteur ne bouge pas

            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.HorizontalAlignment = HorizontalAlignment.Center;
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(track);
            row.Children.Add(text);
            return row;
        }

        /// <summary>Un symbole de la police d'icônes au-dessus d'un mot (boutons Défaut et Retour).</summary>
        private static UIElement SymbolAndWord(string symbol, string word)
        {
            StackPanel stack = new StackPanel();
            stack.HorizontalAlignment = HorizontalAlignment.Center;
            stack.VerticalAlignment = VerticalAlignment.Center;
            TextBlock icon = Text(symbol, 60, White, false);
            icon.FontFamily = new FontFamily(IconFont);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(icon);
            TextBlock text = Text(word, 24, White, false);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(text);
            return stack;
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }

        /// <summary>Le cadre de la valeur d'un réglage numérique, et ce qu'il contient.</summary>
        private sealed class ValueFrame
        {
            public Border Border;
            public UIElement Display;
            public TextBlock Value;
        }
    }

    /// <summary>
    /// INTERFACE : une cible du regard du mode paramètres (onglet, « − », « + », « Non »,
    /// « Oui », « Défaut », « Retour »). Un rectangle sombre aux coins arrondis ; pendant
    /// qu'on le regarde, son fond s'éclaircit et une barre verte se remplit en bas, de
    /// gauche à droite (l'équivalent de l'anneau des boutons ronds, plus lisible sur un
    /// rectangle large comme un onglet).
    ///
    /// Sa zone est en coordonnées de la page 1920 × 1080 (voir FenetreParametres).
    /// </summary>
    internal sealed class SettingTarget : IGazeTarget
    {
        public static readonly Brush Accent = Freeze(new SolidColorBrush(Color.FromRgb(0x6F, 0xB4, 0xFF)));
        private static readonly Brush NormalBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x36)));
        private static readonly Brush HoverBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x45, 0x4B, 0x54)));
        private static readonly Brush ChosenBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x12, 0x26, 0x3D)));
        private static readonly Brush Border = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0x52, 0x5C)));
        private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53)));

        private const double BarHeight = 10;

        private readonly Rect _zone;
        private readonly bool _chosen;
        private readonly Rectangle _bar;

        public SettingTarget(Rect zone, UIElement content, Action action, bool repeat, bool chosen)
        {
            _zone = zone;
            _chosen = chosen;
            Action = action;
            Repeat = repeat;

            _bar = new Rectangle();
            _bar.Fill = Green;
            _bar.Height = BarHeight;
            _bar.HorizontalAlignment = HorizontalAlignment.Left;
            _bar.VerticalAlignment = VerticalAlignment.Bottom;
            _bar.Width = 0;
            _bar.Visibility = Visibility.Collapsed;
            _bar.IsHitTestVisible = false;

            // Une grille superpose le contenu (au centre) et la barre (en bas).
            Grid grid = new Grid();
            grid.Children.Add(content);
            grid.Children.Add(_bar);

            Background = new Border();
            Background.Width = zone.Width;
            Background.Height = zone.Height;
            Background.CornerRadius = new CornerRadius(14);
            Background.BorderThickness = new Thickness(chosen ? 4 : 2);
            Background.BorderBrush = chosen ? Accent : Border;
            Background.Background = chosen ? ChosenBackground : NormalBackground;
            Background.ClipToBounds = true;
            Background.Cursor = System.Windows.Input.Cursors.Hand;
            Background.Child = grid;
        }

        /// <summary>L'élément affiché (à placer sur la page).</summary>
        public Border Background { get; private set; }

        /// <summary>Ce qui se passe quand la cible s'active.</summary>
        public Action Action { get; private set; }

        /// <summary>Vrai si l'activation se répète tant que le regard reste dessus (« − » et « + »).</summary>
        public bool Repeat { get; private set; }

        public bool Active
        {
            get { return true; }
        }

        public bool Contains(double x, double y)
        {
            return _zone.Contains(x, y);
        }

        public void Draw(double progress, bool hover, bool showLabel)
        {
            if (hover)
            {
                Background.Background = HoverBackground;
            }
            else
            {
                Background.Background = _chosen ? ChosenBackground : NormalBackground;
            }

            if (progress > 0)
            {
                // Largeur intérieure : la largeur de la cible moins ses deux bords.
                double inner = _zone.Width - 2 * Background.BorderThickness.Left;
                _bar.Width = Math.Max(0, inner * Math.Min(progress, 1));
                _bar.Visibility = Visibility.Visible;
            }
            else
            {
                _bar.Visibility = Visibility.Collapsed;
            }
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
