using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// LOGIQUE : le clavier au regard (étape 12, « clavier-base »).
    ///
    /// Ce n'est pas un mode : le clavier reste affiché PENDANT que GazeBar fonctionne
    /// normalement au-dessus de lui (choix de l'utilisateur du 31/10/2026) :
    ///   - regard EN DEHORS du clavier : le mode mouvement s'applique (regard fixe, choix
    ///     d'action, loupe…) ;
    ///   - regard SUR le clavier : le curseur de la souris ne bouge pas, le cercle du regard
    ///     est masqué, et c'est ce clavier qui réagit : la touche regardée s'éclaircit, puis
    ///     flashe et fait son action après [DelaiTouche] (pas d'anneau de progression).
    /// C'est le mode mouvement qui lui passe le regard (ModeMouvement.Actualiser).
    ///
    /// Il est affiché dans les modes mouvement et regard fixe, et pendant la barre de menu du
    /// clavier ; il est masqué dans tous les autres (loupe, choix d'action, paramètres, pause…)
    /// puis réapparaît au retour (SurChangementMode, appelée par le ModeManager).
    ///
    /// Il est dessiné sur le calque (surface SurfaceClavier), en bas de l'écran, sur toute la
    /// largeur, par-dessus tout et SANS réserver de place : les autres fenêtres gardent leur
    /// taille. Depuis le 01/11/2026 (remarques de l'utilisateur), la barre de menu du clavier
    /// propose aussi : le mettre en haut de l'écran (position retenue, [ClavierEnHaut]), et
    /// le réduire à 75 % ou 50 % ([TailleClavier], centré). Quand cette barre est ouverte, le
    /// clavier remonte au-dessus d'elle (DefinirReserveBas). Le calque ne prend jamais le focus : l'application où l'on écrit garde son
    /// curseur de texte, et les touches simulées lui arrivent bien.
    ///
    /// Un bouton « Fermer le clavier » est placé au-dessus du clavier, à droite (en dessous
    /// quand le clavier est en haut). On peut aussi le fermer avec la touche Échap d'un vrai
    /// clavier (pour l'aidant), ou avec le bouton « Clavier » de la barre classique s'il est
    /// caché par le mode jeu. (Le bouton Fermer de la barre du clavier a été retiré à l'étape
    /// 14 : il faisait double emploi.)
    ///
    /// Les touches viennent d'un fichier de disposition (DispositionClavier, relu à chaque
    /// ouverture). Le texte tapé n'est JAMAIS écrit dans le journal (vie privée) : seules les
    /// ouvertures, fermetures et erreurs le sont.
    ///
    /// Étape 13 (« clavier-prediction ») : les 3 cases du haut proposent les mots les plus
    /// probables qui commencent par le mot en cours (PredicteurMots) ; en regarder une écrit
    /// le mot entier suivi d'une espace. Un mot inconnu (2 lettres ou plus) s'affiche dans la
    /// 1re case avec un « + » : la regarder l'ajoute au dictionnaire personnel.
    /// Juste après une espace, les cases proposent les mots qui ont déjà suivi le mot d'avant
    /// (« mot suivant », appris au fil des phrases, demande du 02/11/2026).
    ///
    /// Étape 14 (« clavier-options ») : la disposition est celle choisie avec le bouton
    /// « Langue » ([DispositionClavier], voir ChangerDisposition), et une ZONE DE SAISIE
    /// facultative ([ZoneSaisie]) montre, dans une bande au-dessus des touches, les derniers
    /// mots tapés. C'est un simple aperçu : le texte part toujours directement dans
    /// l'application. Le clavier ne peut pas lire le texte de l'application : l'aperçu est
    /// donc ce que LE CLAVIER a tapé, et il se vide quand le curseur de texte a pu bouger
    /// (flèche, Tab, raccourci, clic ailleurs…), comme le mot en cours.
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal sealed class GazeKeyboard
    {
        // Distances en pixels de l'écran 4K, converties pour l'écran actuel (Ecran.Px).
        private const double Margin4K = 16;          // entre le bord du clavier et les touches
        private const double KeyGap4K = 12;   // espace entre deux touches
        // Entre le bouton Fermer et le clavier : 24 px au début, 60 px depuis le 01/11/2026
        // (demande de l'utilisateur : bouton trop près des touches).
        private const double CloseGap4K = 60;

        /// <summary>Zone d'activation du bouton Fermer : 20 % plus grande que son dessin.</summary>
        private const double CloseZoneFactor = 1.2;

        /// <summary>Hauteur de la zone de saisie (étape 14), par rapport à une rangée de touches.</summary>
        private const double InputBoxRatio = 0.7;

        /// <summary>Nombre de caractères gardés pour l'aperçu de la zone de saisie (on n'en montre que la fin).</summary>
        private const int PreviewMaxLength = 400;

        // Couleurs de la zone de saisie (figées : WPF les dessine plus vite).
        private static readonly Brush InputBoxBackground = Freeze(Color.FromArgb(0xFF, 0x26, 0x27, 0x2A));
        private static readonly Brush InputBoxText = Freeze(Colors.White);
        private static readonly Brush InputBoxEmptyText = Freeze(Color.FromArgb(0xFF, 0x80, 0x80, 0x80));

        private readonly OverlayWindow _layer;

        // La disposition lue à l'ouverture, la page affichée et la page d'avant (touche « ↺ »).
        private KeyboardLayout _layout;
        private string _page;
        private string _previousPage;

        // Les cibles visées au regard : les touches de la page, puis le bouton Fermer (le dernier).
        private readonly List<IGazeTarget> _targets = new List<IGazeTarget>();
        private readonly List<KeyboardKey> _keys = new List<KeyboardKey>();
        private GazeButton _close;
        private readonly ButtonAimer _aimer;

        // Vrai quand le clavier est réellement dessiné à l'écran.
        private bool _visible;

        // Place gardée libre en bas de l'écran, en pixels : la hauteur de la barre de menu du
        // clavier quand elle est ouverte (voir DefinirReserveBas), 0 sinon.
        private double _bottomReserve;

        // Le rectangle du clavier, en pixels d'écran.
        private double _left;
        private double _top;
        private double _right;
        private double _bottom;

        // L'état des modificateurs du clavier. Ils sont à part de ceux du choix d'action
        // (Modificateurs.cs), qui sont remis à zéro à chaque retour au mode mouvement.
        private bool _shift;       // pour la prochaine touche seulement
        private bool _capsLock;   // Caps Lock : reste allumé
        private bool _ctrl;
        private bool _alt;
        private bool _win;

        // Le « mot en cours » : les lettres tapées depuis le dernier espace ou signe (voir
        // Suivre). Sert à « Effacer le mot » et à la prédiction (étape 13).
        private readonly StringBuilder _currentWord = new StringBuilder();

        // Étape 13 : la prédiction, et ce que montrent les cases de proposition (calculé par
        // MettreAJourEtat après chaque touche). _motInconnu : vrai quand la 1re case montre
        // « + monmot » (mot inconnu à ajouter).
        private readonly WordPredictor _predictor;
        private List<string> _suggestions = new List<string>();
        private bool _unknownWord;

        // Le mot écrit juste avant le mot en cours (forme rangée du dictionnaire), pour la
        // prédiction du mot suivant (02/11/2026) ; null au début, après une ponctuation, ou
        // quand le curseur de texte a pu bouger (le mot d'avant n'est alors plus sûr).
        private string _previousWord;

        // Les deux derniers caractères tapés, pour la majuscule automatique après « . ».
        private string _end = "";

        // Étape 14 : la zone de saisie. _apercu : ce que le clavier a tapé depuis que le
        // curseur de texte n'a plus bougé (gardé en mémoire seulement, jamais dans le journal) ;
        // _texteApercu : son texte affiché (null quand la zone n'est pas affichée) ;
        // _largeurApercuWpf : la largeur disponible pour ce texte, en unités WPF.
        private readonly StringBuilder _preview = new StringBuilder();
        private TextBlock _previewText;
        private double _previewWidthWpf;

        public GazeKeyboard(OverlayWindow layer)
        {
            _layer = layer;
            // « 1 » : une seule mesure du regard suffit pour qu'une touche s'éclaire (2 pour
            // les autres boutons de GazeBar). Le survol réagit ainsi une mesure plus tôt
            // (environ 30 ms) ; demande de l'utilisateur du 02/11/2026.
            _aimer = new ButtonAimer(_targets, 1);

            // Le dictionnaire français est lu dès le démarrage de GazeBar, en arrière-plan
            // (cahier des charges) : il est prêt quand on ouvre le clavier.
            _predictor = new WordPredictor();
            _predictor.LoadLanguage("fr");
        }

        /// <summary>
        /// À appeler à la fermeture de GazeBar : enregistre les nombres d'utilisations des mots
        /// qui ne l'ont pas encore été (ils le sont sinon une fois par minute).
        /// </summary>
        public void Stop()
        {
            _predictor.Stop();
        }

        /// <summary>Vrai entre Ouvrir et Fermer (même quand le clavier est masqué par un autre mode).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Vrai quand le clavier est dessiné à l'écran.</summary>
        public bool Visible
        {
            get { return _visible; }
        }

        // ───────────────────────── Ouverture, fermeture, affichage ─────────────────────────

        /// <summary>
        /// Ouvre le clavier : lit le fichier de disposition et part de sa page de départ.
        /// Il apparaîtra au retour en mode mouvement (SurChangementMode).
        /// Renvoie faux si le fichier est absent ou incorrect (message pour la barre de menu).
        /// </summary>
        public bool Open(out string message)
        {
            // Étape 14 : la disposition choisie avec le bouton « Langue ». Si son fichier a
            // disparu ou contient une erreur, on essaie la disposition par défaut (fr-azerty)
            // plutôt que de ne pas ouvrir le clavier du tout ; le choix enregistré ne change pas.
            string name = AppSettings.Current.KeyboardLayout;
            KeyboardLayout layout = KeyboardLayout.Load(name, out message);
            if (layout == null &&
                !string.Equals(name, KeyboardLayout.DefaultName, StringComparison.OrdinalIgnoreCase))
            {
                string defaultMessage;
                layout = KeyboardLayout.Load(KeyboardLayout.DefaultName, out defaultMessage);
                if (layout != null)
                {
                    App.Log("Clavier : disposition « " + name + " » inutilisable, on prend " + KeyboardLayout.DefaultName);
                    message = null;
                }
            }
            if (layout == null)
            {
                return false;
            }

            _layout = layout;
            // Le dictionnaire de la langue de la disposition (« fr » pour fr-azerty.json) :
            // déjà chargé au démarrage dans le cas habituel.
            _predictor.LoadLanguage(layout.Language);
            _page = layout.StartPage;
            _previousPage = null;
            IsOpen = true;
            ForgetState();
            App.Log("Clavier : ouvert");
            return true;
        }

        /// <summary>Ferme le clavier (bouton Fermer, barre de menu du clavier, touche Échap).</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            Hide();
            ForgetState();
            App.Log("Clavier : fermé");
        }

        /// <summary>
        /// Appelée par le ModeManager à chaque changement de mode, AVANT l'entrée dans le
        /// nouveau mode : le clavier est affiché dans les modes mouvement et regard fixe, et
        /// pendant la barre de menu du clavier ; il est masqué dans tous les autres.
        /// (Avant l'entrée : la loupe fait une photo de l'écran dès son entrée, le clavier ne
        /// doit pas y être.)
        /// </summary>
        public void OnModeChange(Mode newVal)
        {
            MenuMode menu = newVal as MenuMode;
            bool show = IsOpen &&
                            (newVal is MovementMode || newVal is FixedGazeMode ||
                             (menu != null && menu.KeyboardMenu));

            if (show && !_visible)
            {
                Build();
            }
            else if (!show && _visible)
            {
                Hide();
                // On est parti faire autre chose (un clic ailleurs, les paramètres…) : le texte
                // autour du curseur a pu changer, le mot en cours n'est plus sûr.
                ForgetPosition();
            }
            else if (_visible)
            {
                // Toujours affiché (mouvement ↔ regard fixe, menu du clavier) : plus aucune
                // touche n'est survolée, et celle qui serait sous le regard attend qu'il la quitte.
                _aimer.ResetAndBlock();
                RedrawWithoutHover();
            }
        }

        /// <summary>L'écran a changé (étape 11) : le clavier est redessiné aux nouvelles tailles.</summary>
        public void AdaptToScreen()
        {
            if (_visible)
            {
                Build();
            }
        }

        /// <summary>
        /// La barre de menu du clavier s'ouvre (pixels = sa hauteur, depuis le bas de l'écran)
        /// ou se ferme (0). Le clavier, s'il est en bas, remonte au-dessus d'elle au lieu
        /// d'être recouvert (demande de l'utilisateur du 01/11/2026). En haut, rien ne change.
        /// </summary>
        public void SetBottomReserve(double pixels)
        {
            pixels = Math.Max(0, pixels);
            if (Math.Abs(pixels - _bottomReserve) < 0.5)
            {
                return;
            }
            _bottomReserve = pixels;
            if (_visible && !AppSettings.Current.KeyboardAtTop)
            {
                // Redessiné à sa nouvelle place, puis glissé depuis l'ancienne (02/11/2026).
                double oldTop = _top;
                Build();
                if (_visible)
                {
                    _layer.AnimateKeyboardSurface(oldTop - _top);
                }
            }
        }

        /// <summary>
        /// Bouton « Haut / bas » de la barre du clavier : le clavier passe en haut de l'écran
        /// (ou revient en bas). La position est enregistrée : le clavier se rouvre au même endroit.
        /// </summary>
        public void TogglePosition()
        {
            AppSettings p = AppSettings.Current;
            p.KeyboardAtTop = !p.KeyboardAtTop;
            p.Save();
            App.Log("Clavier : " + (p.KeyboardAtTop ? "en haut" : "en bas") + " de l'écran");
            if (_visible)
            {
                Build();
            }
        }

        /// <summary>
        /// Bouton « Taille » de la barre du clavier : 100 % → 75 % → 50 % → 100 %… (le même
        /// réglage que « Taille du clavier » dans l'onglet Clavier des paramètres). Enregistré.
        /// </summary>
        public void ChangeSize()
        {
            AppSettings p = AppSettings.Current;
            p.KeyboardSize = p.KeyboardSize > 75 ? 75 : p.KeyboardSize > 50 ? 50 : 100;
            p.Save();
            App.Log("Clavier : taille " + p.KeyboardSize + " %");
            if (_visible)
            {
                Build();
            }
        }

        /// <summary>
        /// Bouton « Zone de saisie » de la barre du clavier (étape 14) : affiche ou cache la
        /// bande des derniers mots tapés. Enregistré. Le clavier est redessiné (il grandit ou
        /// rapetisse de la hauteur de la bande).
        /// </summary>
        public void ToggleInputBox()
        {
            AppSettings p = AppSettings.Current;
            p.InputBox = !p.InputBox;
            p.Save();
            App.Log("Clavier : zone de saisie " + (p.InputBox ? "affichée" : "cachée"));
            if (_visible)
            {
                Build();
            }
        }

        /// <summary>
        /// La prédiction de mots, pour la liste « Mes mots » (étape 14, voir ModeListeClavier).
        /// </summary>
        public WordPredictor Predictor
        {
            get { return _predictor; }
        }

        /// <summary>
        /// Bouton « Langue » (étape 14) : passe à une autre disposition du dossier Claviers
        /// (nom du fichier sans « .json »). Elle est lue tout de suite ; si elle est absente ou
        /// incorrecte, rien ne change et « message » explique pourquoi. Sinon le choix est
        /// enregistré, le dictionnaire de sa langue est chargé (en arrière-plan), et le clavier
        /// repart de la page de départ de la nouvelle disposition.
        /// </summary>
        public bool ChangeLayout(string name, out string message)
        {
            KeyboardLayout layout = KeyboardLayout.Load(name, out message);
            if (layout == null)
            {
                return false;
            }
            AppSettings p = AppSettings.Current;
            p.KeyboardLayout = name;
            p.Save();

            _layout = layout;
            _predictor.LoadLanguage(layout.Language);
            _page = layout.StartPage;
            _previousPage = null;
            ForgetState();
            App.Log("Clavier : disposition choisie : " + name);
            if (_visible)
            {
                Build();
            }
            return true;
        }

        /// <summary>
        /// Étape 15 : un profil de paramètres vient d'être chargé (appelée par ModeParametres).
        /// La taille, la position et la zone de saisie sont relues à chaque dessin : rien à
        /// faire pour elles. Mais la disposition (et donc la langue du dictionnaire) a pu
        /// changer : si le clavier est ouvert, on le rouvre, ce qui relit la disposition
        /// enregistrée dans les réglages. S'il ne peut pas l'être (fichier absent), il se ferme.
        /// Pendant les paramètres, le clavier est masqué : il réapparaîtra au retour en mode
        /// mouvement.
        /// </summary>
        public void SettingsReplaced()
        {
            if (!IsOpen)
            {
                return;
            }
            string message;
            if (!Open(out message))
            {
                App.Log("Clavier : disposition du profil inutilisable, clavier fermé (" + message + ")");
                Close();
            }
        }

        /// <summary>
        /// Vrai si le point du regard (pixels d'écran, VRAI regard et non ramené dans l'écran)
        /// est sur le clavier ou sur son bouton Fermer. Faux si le clavier n'est pas affiché.
        /// </summary>
        public bool Contains(double x, double y)
        {
            if (!_visible)
            {
                return false;
            }
            if (x >= _left && x < _right && y >= _top && y < _bottom)
            {
                return true;
            }
            return _close != null && _close.Contains(x, y);
        }

        private void Hide()
        {
            _layer.HideKeyboardSurface();
            _visible = false;
        }

        /// <summary>Remet à zéro les modificateurs, le mot en cours et la fin du texte.</summary>
        private void ForgetState()
        {
            _shift = false;
            _capsLock = false;
            _ctrl = false;
            _alt = false;
            _win = false;
            ForgetPosition();
        }

        /// <summary>
        /// Le curseur de texte a pu bouger (flèche, Tab, raccourci, clic ailleurs…) : on oublie
        /// le mot en cours, le mot d'avant, la fin du texte et l'aperçu de la zone de saisie,
        /// qui ne correspondent peut-être plus au texte autour du curseur.
        /// </summary>
        private void ForgetPosition()
        {
            _currentWord.Clear();
            _previousWord = null;
            _end = "";
            _preview.Clear();
        }

        // ───────────────────────── Le regard ─────────────────────────

        /// <summary>
        /// À appeler à chaque tic du mode mouvement, tant que le clavier est affiché : met à
        /// jour la touche survolée et l'active si le regard y est resté [DelaiTouche].
        /// </summary>
        public void Update(GazeState gaze)
        {
            if (!_visible)
            {
                return;
            }

            AppSettings p = AppSettings.Current;
            int num = _aimer.Update(gaze, p.KeyDelay, p.ShowLabels);
            if (num < 0)
            {
                return;
            }

            if (num >= _keys.Count)
            {
                // Le dernier de la liste : le bouton Fermer.
                Close();
                return;
            }
            Activate(_keys[num]);
        }

        /// <summary>Fait l'action d'une touche.</summary>
        private void Activate(KeyboardKey key)
        {
            LayoutKey d = key.Description;
            key.Flash();

            // Vrai pour une touche qu'on peut répéter en gardant le regard dessus
            // ([RepetitionTouche]) : lettres, effacer, flèches… Pas pour un modificateur
            // ou un raccourci : en continuant à regarder, on l'allumerait et l'éteindrait sans fin.
            bool repeatable = true;

            if (d.Prediction.HasValue)
            {
                // Étape 13 : une case de proposition. Pas de répétition : en continuant à la
                // regarder, on n'écrirait pas deux fois le mot (la case est d'ailleurs vide
                // juste après, le mot en cours repartant de zéro).
                ChooseSuggestion(d.Prediction.Value);
                repeatable = false;
            }
            else if (d.Text != null)
            {
                Write(d);
            }
            else if (d.Key != null)
            {
                PressKey(d.Key);
            }
            else if (d.Shortcut != null)
            {
                Shortcut(d.Shortcut);
                repeatable = false;
            }
            else if (d.Page != null)
            {
                // La page change : les touches sont remplacées (voir Construire).
                ChangePage(d.Page);
                return;
            }
            else if (d.Modifier != null)
            {
                ToggleModifier(d.Modifier);
                repeatable = false;
            }
            else if (d.Command == "EffacerLettre")
            {
                DeleteLetter();
            }
            else if (d.Command == "EffacerMot")
            {
                DeleteWord();
            }
            else
            {
                App.Log("Clavier : touche « " + d.Label + " » sans action connue");
            }

            if (!repeatable || !AppSettings.Current.KeyRepeat)
            {
                _aimer.BlockUntilExit();
            }
            RefreshState();
        }

        // ───────────────────────── Les actions ─────────────────────────

        /// <summary>Écrit le texte d'une touche, en majuscules si Maj ou Caps Lock (mais pas les deux).</summary>
        private void Write(LayoutKey d)
        {
            // Maj et Caps Lock ensemble donnent des minuscules, comme sur un vrai clavier.
            bool uppercaseLetters = _shift ^ _capsLock;
            string text = uppercaseLetters ? (d.ShiftText ?? d.Text.ToUpper(CultureInfo.CurrentCulture)) : d.Text;

            if ((_ctrl || _alt || _win) && text.Length == 1)
            {
                // Avec Ctrl, Alt ou Win, il faut la VRAIE touche (Ctrl + c = copier) : on demande à
                // Windows quelle touche produit ce caractère. Octet haut : 1 = il faut aussi Maj.
                short result = NativeMethods.VkKeyScanW(text[0]);
                if (result != -1)
                {
                    ushort code = (ushort)(result & 0xFF);
                    List<ushort> mods = ActiveModifiers(false);
                    if (((result >> 8) & 1) != 0)
                    {
                        mods.Add(NativeMethods.VK_SHIFT);
                    }
                    KeyboardInjector.TypeKey(code, false, mods);
                    _ctrl = false;
                    _alt = false;
                    _win = false;
                    _shift = false;
                    // Un raccourci n'écrit pas de texte : le mot en cours n'est plus sûr.
                    ForgetPosition();
                    return;
                }
            }

            KeyboardInjector.TypeText(text);
            Preview(0, text);
            _ctrl = false;
            _alt = false;
            _win = false;
            if (ContainsLetter(text))
            {
                _shift = false;   // Maj ne vaut que pour une lettre
            }
            Follow(text);
        }

        /// <summary>Appuie sur une vraie touche (Tab, Entree, flèches…), avec les modificateurs actifs.</summary>
        private void PressKey(string name)
        {
            ushort code;
            bool extent;
            if (!KeyCode(name, out code, out extent))
            {
                App.Log("Clavier : touche inconnue « " + name + " » dans la disposition");
                return;
            }

            // Ici, Maj compte (Maj + Tab, Maj + flèche pour sélectionner…).
            List<ushort> mods = ActiveModifiers(true);
            // Ctrl, Alt ou Win + Entrée n'est pas une simple fin de ligne (Maj + Entrée, si).
            bool withModifier = _ctrl || _alt || _win;
            KeyboardInjector.TypeKey(code, extent, mods);
            _ctrl = false;
            _alt = false;
            _win = false;
            _shift = false;

            if (code == 0x0D && !withModifier)
            {
                Follow("\n");   // Entrée : fin de ligne (compte pour la majuscule automatique)
                Preview(0, "\n");
            }
            else
            {
                // Tab, flèches, Début, Ctrl + Entrée… : le curseur de texte a pu bouger, le mot
                // en cours n'est plus sûr.
                ForgetPosition();
            }
        }

        /// <summary>
        /// Un raccourci écrit dans la disposition, par exemple « Ctrl+F » : les mots avant la
        /// dernière « + » sont les modificateurs (Ctrl, Alt, Maj, Win), le dernier est la touche.
        /// </summary>
        private void Shortcut(string shortcut)
        {
            string[] pieces = shortcut.Split('+');
            List<ushort> mods = new List<ushort>();
            for (int i = 0; i < pieces.Length - 1; i++)
            {
                switch (pieces[i].Trim().ToLowerInvariant())
                {
                    case "ctrl":
                        mods.Add(NativeMethods.VK_CONTROL);
                        break;
                    case "alt":
                        mods.Add(NativeMethods.VK_MENU);
                        break;
                    case "maj":
                    case "shift":
                        mods.Add(NativeMethods.VK_SHIFT);
                        break;
                    case "win":
                        mods.Add(NativeMethods.VK_LWIN);
                        break;
                    default:
                        App.Log("Clavier : modificateur inconnu dans le raccourci « " + shortcut + " »");
                        return;
                }
            }

            ushort code;
            bool extent;
            if (!KeyCode(pieces[pieces.Length - 1].Trim(), out code, out extent))
            {
                App.Log("Clavier : touche inconnue dans le raccourci « " + shortcut + " »");
                return;
            }
            KeyboardInjector.TypeKey(code, extent, mods);
            ForgetPosition();
        }

        /// <summary>Effacer une lettre : Retour arrière (avec Ctrl, Alt, Maj s'ils sont allumés).</summary>
        private void DeleteLetter()
        {
            // Avec Ctrl, Retour arrière efface un mot entier dans la plupart des applications :
            // l'aperçu fait de même ; avec Alt ou Win, on ne sait pas ce qui se passe : il se vide.
            if (_alt || _win)
            {
                _preview.Clear();
            }
            else if (_ctrl)
            {
                RemovePreviewWord();
            }
            else
            {
                Preview(1, "");
            }
            KeyboardInjector.TypeKey(NativeMethods.VK_BACK, false, ActiveModifiers(true));
            _ctrl = false;
            _alt = false;
            _win = false;
            _shift = false;
            if (_currentWord.Length > 0)
            {
                _currentWord.Length--;
            }
            else
            {
                // On efface l'espace ou la fin du mot d'avant : il n'est plus sûr.
                _previousWord = null;
            }
            _end = _end.Length > 0 ? _end.Substring(0, _end.Length - 1) : "";
        }

        /// <summary>
        /// Effacer le mot : autant de Retour arrière que de lettres dans le mot en cours. Si le
        /// clavier ne connaît pas le mot (juste après une espace, ou après un clic ailleurs), il
        /// envoie Ctrl + Retour arrière, que la plupart des applications comprennent comme
        /// « effacer le mot d'avant ».
        /// </summary>
        private void DeleteWord()
        {
            if (_currentWord.Length > 0)
            {
                for (int i = 0; i < _currentWord.Length; i++)
                {
                    KeyboardInjector.TypeKey(NativeMethods.VK_BACK, false, null);
                }
                Preview(_currentWord.Length, "");
                _currentWord.Clear();
            }
            else
            {
                KeyboardInjector.TypeKey(NativeMethods.VK_BACK, false, new List<ushort> { NativeMethods.VK_CONTROL });
                _previousWord = null;   // c'est le mot d'avant qui vient d'être effacé
                RemovePreviewWord();
            }
            _end = "";
        }

        /// <summary>Allume ou éteint un modificateur (Maj, VerrMaj, Ctrl, Alt, Win).</summary>
        private void ToggleModifier(string name)
        {
            switch (name.Trim().ToLowerInvariant())
            {
                case "maj":
                    _shift = !_shift;
                    break;
                case "verrmaj":
                    _capsLock = !_capsLock;
                    break;
                case "ctrl":
                    _ctrl = !_ctrl;
                    break;
                case "alt":
                    _alt = !_alt;
                    break;
                case "win":
                    _win = !_win;
                    break;
                default:
                    App.Log("Clavier : modificateur inconnu « " + name + " » dans la disposition");
                    break;
            }
        }

        /// <summary>Affiche une autre page du clavier (« retour » = la page d'avant).</summary>
        private void ChangePage(string name)
        {
            if (string.Equals(name, "retour", StringComparison.OrdinalIgnoreCase))
            {
                name = _previousPage ?? _layout.StartPage;
            }
            if (_layout.Page(name) == null)
            {
                App.Log("Clavier : page « " + name + " » introuvable dans la disposition");
                return;
            }
            _previousPage = _page;
            _page = name;
            Build();
        }

        /// <summary>
        /// Garde la trace de ce qui vient d'être écrit :
        ///  - le mot en cours : lettres, chiffres et tiret (« peut-être ») ; tout autre caractère
        ///    (espace, ponctuation, apostrophe, fin de ligne) termine le mot ;
        ///  - les deux derniers caractères, pour la majuscule automatique : après « . », « ? »
        ///    ou « ! » suivi d'une espace ou d'une fin de ligne, Maj s'allume tout seul pour la
        ///    lettre suivante ([MajusculeAuto]). On voit Maj allumé : on peut l'éteindre.
        /// </summary>
        private void Follow(string text)
        {
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) || c == '-')
                {
                    _currentWord.Append(c);
                }
                else
                {
                    // Le mot en cours est terminé (étape 13) : s'il est connu, la prédiction
                    // retient qu'il a été utilisé une fois de plus, et qu'il a suivi le mot
                    // d'avant (apprentissage). Avec une apostrophe, c'est la forme élidée qui
                    // compte : « l' », « qu' ».
                    bool apostrophe = c == '\'' || c == '’';
                    if (_currentWord.Length > 0)
                    {
                        string word = _currentWord.ToString();
                        _previousWord = _predictor.RecordUsage(apostrophe ? word + "'" : word, _previousWord);
                    }
                    // Mot suivant : seuls une espace ou une apostrophe gardent le lien avec le
                    // mot d'avant ; une ponctuation (. , ? ! …) ou une fin de ligne le coupent.
                    if (c != ' ' && !apostrophe)
                    {
                        _previousWord = null;
                    }
                    _currentWord.Clear();
                }
            }
            KeepEnd(text);
        }

        /// <summary>
        /// Retient les deux derniers caractères écrits, et allume Maj tout seul après « . »,
        /// « ? » ou « ! » suivi d'une espace ou d'une fin de ligne ([MajusculeAuto]).
        /// </summary>
        private void KeepEnd(string text)
        {
            _end += text;
            if (_end.Length > 2)
            {
                _end = _end.Substring(_end.Length - 2);
            }

            if (AppSettings.Current.AutoCapitalize && _end.Length == 2 &&
                (_end[1] == ' ' || _end[1] == '\n') && ".?!".IndexOf(_end[0]) >= 0)
            {
                _shift = true;
            }
        }

        /// <summary>
        /// Étape 13 : l'utilisateur a regardé la case de proposition n° « numero » (1, 2 ou 3).
        ///  - case « + monmot » : le mot tapé est ajouté au dictionnaire personnel, puis une
        ///    espace est écrite (le mot est déjà écrit en entier) ;
        ///  - sinon : le mot proposé remplace les lettres tapées, suivi d'une espace. Pour aller
        ///    vite et sans clignotement, on n'efface que la fin qui diffère : « bon » → « bonjour »
        ///    écrit seulement « jour » ; « ete » → « été » efface « te » et écrit « té ».
        ///    Après un mot élidé (m' t' n' s' c' l' qu' d' j'…), pas d'espace : le mot suivant
        ///    s'y colle (demande de l'utilisateur du 02/11/2026).
        /// </summary>
        private void ChooseSuggestion(int num)
        {
            string typed = _currentWord.ToString();
            // Maj ou Caps Lock allumé avant un mot suivant : sa 1re lettre en majuscule.
            bool uppercaseRequested = _shift ^ _capsLock;
            // Les autres modificateurs ne s'appliquent pas à un mot entier : on les éteint.
            _shift = false;
            _ctrl = false;
            _alt = false;
            _win = false;

            if (num == 1 && _unknownWord)
            {
                _predictor.Add(typed);
                KeyboardInjector.TypeText(" ");
                Preview(0, " ");
                Follow(" ");   // termine le mot : il compte déjà une utilisation
                return;
            }

            int idx = num - 1;
            if (idx < 0 || idx >= _suggestions.Count)
            {
                return;   // case vide (ne devrait pas arriver : elle est inactive)
            }

            // (Avec « tape » vide, c'est une proposition de mot suivant : rien à effacer.)
            string word = ShownShape(_suggestions[idx], typed, uppercaseRequested);

            // Les premières lettres identiques restent en place.
            int common = 0;
            while (common < typed.Length && common < word.Length && typed[common] == word[common])
            {
                common++;
            }
            for (int i = common; i < typed.Length; i++)
            {
                KeyboardInjector.TypeKey(NativeMethods.VK_BACK, false, null);
            }

            bool elision = word.EndsWith("'") || word.EndsWith("’");
            string addition = word.Substring(common) + (elision ? "" : " ");
            KeyboardInjector.TypeText(addition);
            Preview(typed.Length - common, addition);

            _currentWord.Clear();
            _previousWord = _predictor.RecordUsage(word, _previousWord);
            KeepEnd(addition);
        }

        /// <summary>
        /// Le mot tel qu'il s'affiche dans sa case et s'écrit : avec les majuscules de ce qui a
        /// été tapé (AvecMajuscule) ; pour un mot suivant (rien de tapé), avec une majuscule
        /// au début si Maj ou Caps Lock est allumé.
        /// </summary>
        private static string ShownShape(string word, string typed, bool uppercase)
        {
            if (string.IsNullOrEmpty(typed) && uppercase && !string.IsNullOrEmpty(word))
            {
                return char.ToUpper(word[0], CultureInfo.CurrentCulture) + word.Substring(1);
            }
            return WithUppercase(word, typed);
        }

        /// <summary>
        /// Le mot proposé avec les majuscules de ce qui a été tapé : « Bo » → « Bonjour » (le
        /// mot garde sa majuscule de départ, cahier des charges), « BON » (Caps Lock) →
        /// « BONJOUR ». Sinon, le mot tel qu'il est rangé (« Paris » reste « Paris »).
        /// </summary>
        private static string WithUppercase(string word, string typed)
        {
            if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(typed))
            {
                return word;
            }
            int letters = 0;
            int uppercaseLetters = 0;
            foreach (char c in typed)
            {
                if (char.IsLetter(c))
                {
                    letters++;
                    if (char.IsUpper(c))
                    {
                        uppercaseLetters++;
                    }
                }
            }
            if (letters >= 2 && uppercaseLetters == letters)
            {
                return word.ToUpper(CultureInfo.CurrentCulture);
            }
            if (char.IsUpper(typed[0]))
            {
                return char.ToUpper(word[0], CultureInfo.CurrentCulture) + word.Substring(1);
            }
            return word;
        }

        // ───────────────────────── La zone de saisie (étape 14) ─────────────────────────

        /// <summary>
        /// Met à jour l'aperçu de la zone de saisie comme l'application l'a fait : « effaces »
        /// Retour arrière, puis « ajout » écrit. L'aperçu est tenu à jour même quand la zone est
        /// cachée : on la voit juste en l'affichant. Seule la fin est gardée.
        /// </summary>
        private void Preview(int deleted, string addition)
        {
            _preview.Length = Math.Max(0, _preview.Length - deleted);
            _preview.Append(addition);
            if (_preview.Length > PreviewMaxLength)
            {
                _preview.Remove(0, _preview.Length - PreviewMaxLength);
            }
        }

        /// <summary>
        /// Comme Ctrl + Retour arrière dans la plupart des applications : retire les espaces de
        /// la fin, puis les lettres du dernier mot.
        /// </summary>
        private void RemovePreviewWord()
        {
            int end = _preview.Length;
            while (end > 0 && char.IsWhiteSpace(_preview[end - 1]))
            {
                end--;
            }
            while (end > 0 && !char.IsWhiteSpace(_preview[end - 1]))
            {
                end--;
            }
            _preview.Length = end;
        }

        /// <summary>
        /// Écrit l'aperçu dans la zone de saisie (si elle est affichée). Une fin de ligne est
        /// montrée par « ↵ ». Quand le texte est trop long, seule la fin est montrée, précédée
        /// de « … ». Zone vide : un petit texte gris explique à quoi elle sert.
        /// </summary>
        private void ShowPreview()
        {
            if (_previewText == null)
            {
                return;
            }
            if (_preview.Length == 0)
            {
                _previewText.Text = "Les derniers mots tapés s'affichent ici";
                _previewText.Foreground = InputBoxEmptyText;
                return;
            }
            string text = _preview.ToString().Replace("\n", " ↵ ");
            _previewText.Foreground = InputBoxText;
            _previewText.Text = FittingEnd(text);
        }

        /// <summary>
        /// La plus longue fin de « texte » qui tient dans la largeur de la zone (précédée de
        /// « … » si elle est coupée). On cherche le point de coupure par dichotomie : on mesure
        /// une dizaine de fois au lieu d'essayer chaque caractère.
        /// </summary>
        private string FittingEnd(string text)
        {
            if (TextWidth(text) <= _previewWidthWpf)
            {
                return text;
            }
            // On cherche le plus petit « debut » pour lequel « … » + la fin depuis debut tient.
            int bottom = 1;
            int top = text.Length;   // texte vide : tient toujours
            while (bottom < top)
            {
                int middle = bottom + (top - bottom) / 2;
                if (TextWidth("…" + text.Substring(middle)) <= _previewWidthWpf)
                {
                    top = middle;     // ça tient : on peut peut-être garder plus de texte
                }
                else
                {
                    bottom = middle + 1;  // trop long : il faut couper plus loin
                }
            }
            return "…" + text.Substring(bottom);
        }

        /// <summary>Largeur d'un texte dans la police de la zone de saisie, en unités WPF.</summary>
        private double TextWidth(string text)
        {
            FormattedText measure = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(_previewText.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                _previewText.FontSize,
                InputBoxText,
                VisualTreeHelper.GetDpi(_layer).PixelsPerDip);
            return measure.WidthIncludingTrailingWhitespace;
        }

        // ───────────────────────── Petits outils ─────────────────────────

        private static Brush Freeze(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>Les codes des modificateurs allumés (Maj seulement si avecMaj).</summary>
        private List<ushort> ActiveModifiers(bool withShift)
        {
            List<ushort> mods = new List<ushort>();
            if (_ctrl)
            {
                mods.Add(NativeMethods.VK_CONTROL);
            }
            if (_alt)
            {
                mods.Add(NativeMethods.VK_MENU);
            }
            if (_win)
            {
                mods.Add(NativeMethods.VK_LWIN);
            }
            if (withShift && _shift)
            {
                mods.Add(NativeMethods.VK_SHIFT);
            }
            return mods;
        }

        private static bool ContainsLetter(string text)
        {
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Le code Windows (« virtual-key code ») d'une touche nommée dans la disposition.
        /// etendue : vrai pour les touches « étendues » (flèches, Début, Fin, Suppr, Page…).
        /// Accepte aussi une lettre seule (A à Z) ou un chiffre seul, pour les raccourcis.
        /// </summary>
        private static bool KeyCode(string name, out ushort code, out bool extent)
        {
            extent = false;
            code = 0;
            switch (name.Trim().ToLowerInvariant())
            {
                case "tab": code = 0x09; return true;
                case "entree": code = 0x0D; return true;
                case "echap": code = 0x1B; return true;
                case "espace": code = 0x20; return true;
                case "retourarriere": code = NativeMethods.VK_BACK; return true;
                case "pagehaut": code = 0x21; extent = true; return true;
                case "pagebas": code = 0x22; extent = true; return true;
                case "fin": code = 0x23; extent = true; return true;
                case "debut": code = 0x24; extent = true; return true;
                case "gauche": code = 0x25; extent = true; return true;
                case "haut": code = 0x26; extent = true; return true;
                case "droite": code = 0x27; extent = true; return true;
                case "bas": code = 0x28; extent = true; return true;
                case "inser": code = 0x2D; extent = true; return true;
                case "suppr": code = 0x2E; extent = true; return true;
                // La touche Windows seule (02/11/2026) : ouvre ou ferme le menu Démarrer.
                // C'est une touche « étendue » pour Windows.
                case "windows": code = NativeMethods.VK_LWIN; extent = true; return true;
            }

            string n = name.Trim().ToUpperInvariant();
            // F1 à F24 : codes 0x70 à 0x87.
            int num;
            if (n.Length >= 2 && n[0] == 'F' && int.TryParse(n.Substring(1), out num) && num >= 1 && num <= 24)
            {
                code = (ushort)(0x70 + num - 1);
                return true;
            }
            // Une lettre ou un chiffre seul : son code est celui du caractère en majuscule.
            if (n.Length == 1 && ((n[0] >= 'A' && n[0] <= 'Z') || (n[0] >= '0' && n[0] <= '9')))
            {
                code = n[0];
                return true;
            }
            return false;
        }

        // ───────────────────────── Le dessin ─────────────────────────

        /// <summary>
        /// (Re)dessine le clavier : fond, touches de la page affichée et bouton Fermer.
        /// Toutes les positions sont en pixels d'écran, converties par le calque.
        /// </summary>
        private void Build()
        {
            _targets.Clear();
            _keys.Clear();
            _close = null;
            if (!IsOpen || _layout == null || !_layer.Ready)
            {
                Hide();
                return;
            }
            KeyboardPage page = _layout.Page(_page) ?? _layout.Page(_layout.StartPage);

            AppSettings p = AppSettings.Current;
            double width = ScreenMetrics.WidthPixels;
            double height = ScreenMetrics.HeightPixels;

            // Taille (01/11/2026) : à 75 % ou 50 %, largeur ET hauteur sont réduites ensemble,
            // les touches gardent donc leur forme ; le clavier est centré horizontalement.
            double sizePercent = p.KeyboardSize / 100.0;
            double keyboardWidth = width * sizePercent;
            double keysHeight = height * p.KeyboardHeight / 100.0 * sizePercent;

            // La grille des touches.
            double margin = ScreenMetrics.Px(Margin4K);
            double cellWidth = (keyboardWidth - 2 * margin) / _layout.Columns;
            double cellHeight = (keysHeight - 2 * margin) / _layout.Rows;
            double gap = Math.Min(ScreenMetrics.Px(KeyGap4K), Math.Min(cellWidth, cellHeight) * 0.15);

            // Étape 14 : la zone de saisie ajoute une bande au-dessus des touches. Le clavier
            // grandit de sa hauteur : les touches gardent leur taille.
            double zoneHeight = p.InputBox ? Math.Round(cellHeight * InputBoxRatio) : 0;
            double keyboardHeight = keysHeight + zoneHeight;

            _left = (width - keyboardWidth) / 2;
            _right = _left + keyboardWidth;
            if (p.KeyboardAtTop)
            {
                // En haut de l'écran (bouton « Haut / bas » de la barre du clavier).
                _top = 0;
                _bottom = keyboardHeight;
            }
            else
            {
                // En bas, au-dessus de la barre de menu du clavier si elle est ouverte.
                _bottom = Math.Max(keyboardHeight, height - _bottomReserve);
                _top = _bottom - keyboardHeight;
            }

            _layer.PrepareKeyboardSurface();
            Canvas surface = _layer.KeyboardSurface;

            // ── Le fond : un rectangle presque noir, comme sur les captures de l'utilisateur.
            Border background = new Border();
            background.Background = new SolidColorBrush(Color.FromArgb(0xF5, 0x12, 0x12, 0x12));
            background.Width = _layer.LengthToWpf(keyboardWidth);
            background.Height = _layer.LengthToWpf(keyboardHeight);
            Point corner = _layer.ToWpf(_left, _top);
            Canvas.SetLeft(background, corner.X);
            Canvas.SetTop(background, corner.Y);
            surface.Children.Add(background);

            // ── La zone de saisie (étape 14) : une bande sombre, sur toute la largeur des
            //    touches, avec le texte aligné à gauche. Ce n'est pas une cible du regard : le
            //    regard dessus ne fait rien (le curseur de la souris reste immobile).
            _previewText = null;
            if (zoneHeight > 0)
            {
                Border zone = new Border();
                zone.Background = InputBoxBackground;
                zone.CornerRadius = new CornerRadius(ScreenMetrics.Units(4));
                double zoneWidth = keyboardWidth - 2 * margin - gap;
                zone.Width = _layer.LengthToWpf(zoneWidth);
                zone.Height = _layer.LengthToWpf(zoneHeight - gap);
                double indent = _layer.LengthToWpf(zoneHeight * 0.25);
                zone.Padding = new Thickness(indent, 0, indent, 0);

                _previewText = new TextBlock();
                _previewText.FontFamily = new FontFamily("Segoe UI, Segoe UI Symbol");
                _previewText.FontSize = _layer.LengthToWpf(zoneHeight * 0.45);
                _previewText.TextWrapping = TextWrapping.NoWrap;
                _previewText.VerticalAlignment = VerticalAlignment.Center;
                _previewText.HorizontalAlignment = HorizontalAlignment.Left;
                zone.Child = _previewText;
                _previewWidthWpf = Math.Max(0, zone.Width - 2 * indent);

                Point zoneCorner = _layer.ToWpf(_left + margin + gap / 2, _top + margin + gap / 2);
                Canvas.SetLeft(zone, zoneCorner.X);
                Canvas.SetTop(zone, zoneCorner.Y);
                surface.Children.Add(zone);
            }

            // ── Les touches, sous la zone de saisie si elle est affichée.
            double keysTop = _top + zoneHeight;
            foreach (LayoutKey d in page.Keys)
            {
                KeyboardKey key = new KeyboardKey(d);
                key.Place(_layer, surface,
                              _left + margin + d.Col * cellWidth,
                              keysTop + margin + d.Row * cellHeight,
                              d.CellWidth * cellWidth,
                              cellHeight,
                              gap);
                _keys.Add(key);
                _targets.Add(key);
            }

            // ── Le bouton Fermer : à droite, AU-DESSUS du clavier quand il est en bas, EN
            //    DESSOUS quand il est en haut (son libellé est alors sous le bouton). Il reste
            //    sur l'écran même si le clavier est très haut. (E711 = croix « annuler ».)
            //    Demande de l'utilisateur du 02/11/2026 : il reste contre le BORD DROIT DE
            //    L'ÉCRAN même quand le clavier est réduit (75 %, 50 %, clavier centré), et sa
            //    taille ne dépend pas de celle du clavier : c'est toujours [TailleIcones].
            double size = p.IconSizePx;
            double closeGap = ScreenMetrics.Px(CloseGap4K);
            double yClose = p.KeyboardAtTop
                ? _bottom + closeGap + size / 2
                : _top - closeGap - size / 2;
            yClose = Math.Max(size / 2 + margin, Math.Min(height - size / 2 - margin, yClose));
            _close = new GazeButton("", null, "Fermer le clavier");
            _close.ZoneFactor = CloseZoneFactor;
            _close.Place(_layer, surface,
                           width - margin - size / 2,
                           yClose,
                           size, !p.KeyboardAtTop);
            _targets.Add(_close);

            // Les touches ont changé : celle qui serait sous le regard ne s'active pas tant
            // que le regard ne l'a pas quittée (par exemple après un changement de page).
            _aimer.ResetAndBlock();
            RefreshState();

            _layer.ShowKeyboardSurface();
            _visible = true;
        }

        /// <summary>
        /// Met à jour les libellés (majuscules), les bords verts des modificateurs et, depuis
        /// l'étape 13, les mots des cases de proposition (recalculés d'après le mot en cours :
        /// moins d'une milliseconde).
        /// </summary>
        private void RefreshState()
        {
            string typed = _currentWord.ToString();
            _suggestions = _predictor.Suggest(typed, _previousWord, out _unknownWord);
            ShowPreview();

            bool uppercaseLetters = _shift ^ _capsLock;
            foreach (KeyboardKey key in _keys)
            {
                key.ShowUppercase(uppercaseLetters);
                string m = key.Description.Modifier;
                if (m != null)
                {
                    key.SetLit(IsLit(m));
                }

                int? num = key.Description.Prediction;
                if (num.HasValue)
                {
                    int idx = num.Value - 1;
                    if (_unknownWord && idx == 0)
                    {
                        key.ShowSuggestion("+ " + typed, true);
                    }
                    else if (!_unknownWord && idx >= 0 && idx < _suggestions.Count)
                    {
                        key.ShowSuggestion(ShownShape(_suggestions[idx], typed, _shift ^ _capsLock), false);
                    }
                    else
                    {
                        key.ShowSuggestion(null, false);
                    }
                }
            }
        }

        private bool IsLit(string modifier)
        {
            switch (modifier.Trim().ToLowerInvariant())
            {
                case "maj":
                    return _shift;
                case "verrmaj":
                    return _capsLock;
                case "ctrl":
                    return _ctrl;
                case "alt":
                    return _alt;
                case "win":
                    return _win;
                default:
                    return false;
            }
        }

        /// <summary>Dessine toutes les cibles sans survol (par exemple au passage en regard fixe).</summary>
        private void RedrawWithoutHover()
        {
            foreach (IGazeTarget target in _targets)
            {
                target.Draw(0, false, false);
            }
        }
    }
}
