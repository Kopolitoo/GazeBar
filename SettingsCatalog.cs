using System;
using System.Collections.Generic;
using System.Globalization;

namespace GazeBar
{
    /// <summary>
    /// Un réglage tel qu'il est AFFICHÉ dans le mode paramètres (étape 7) : son nom en
    /// français, une phrase d'explication, son unité, le pas des boutons « − » et « + »,
    /// et la façon de le lire et de l'écrire dans Parametres.
    ///
    /// Deux sortes de réglages :
    ///   - un NOMBRE (Lissage, DelaiFixation…) : boutons « − », valeur, « + » ;
    ///   - un OUI / NON (ClicDirect, SuiviSouris…) : un INTERRUPTEUR arrondi, comme ceux de
    ///     Bootstrap (02/11/2026, remplace les deux boutons « Non » et « Oui »).
    ///     Variante (étape 9) : un choix entre deux mots, par exemple « directe » ou
    ///     « relative » pour SourisJeu. Là, un interrupteur ne dirait pas ce que chaque côté
    ///     veut dire : on garde les deux boutons, un par mot (voir DeuxChoix).
    ///
    /// Lire et Ecrire sont des « fonctions rangées dans une variable » (délégués). Par exemple
    ///     p => p.Lissage                 lit le lissage d'un objet Parametres p ;
    ///     (p, v) => p.Lissage = v        écrit la valeur v dans le lissage de p.
    /// Ainsi, une seule classe sait afficher et modifier n'importe quel réglage.
    ///
    /// Les limites (minimum, maximum) ne sont PAS répétées ici : après chaque changement,
    /// Parametres.Corriger() ramène la valeur dans ses limites. Elles restent écrites à un
    /// seul endroit.
    /// </summary>
    internal sealed class Setting
    {
        /// <summary>
        /// Les nombres sont affichés avec un POINT (0.16), comme dans parametres.json
        /// (demande de l'utilisateur du 26/10/2026). CultureInfo.InvariantCulture est la
        /// présentation « neutre » de .NET, qui utilise le point.
        /// </summary>
        private static readonly CultureInfo Format = CultureInfo.InvariantCulture;

        private Setting()
        {
        }

        /// <summary>Le nom affiché (par exemple « Délai de fixation »).</summary>
        public string Name { get; private set; }

        /// <summary>Une phrase courte, affichée sous le nom.</summary>
        public string Explanation { get; private set; }

        /// <summary>L'unité, avec son espace devant (par exemple " s", " px") ou "" pour aucune.</summary>
        public string Unit { get; private set; }

        /// <summary>De combien « − » et « + » changent la valeur.</summary>
        public double Step { get; private set; }

        /// <summary>Nombre de chiffres après le point, déduit du pas (0.05 → 2 chiffres).</summary>
        public int Decimals { get; private set; }

        /// <summary>Vrai pour un réglage oui / non (ou un choix entre deux mots).</summary>
        public bool YesNo { get; private set; }

        /// <summary>
        /// Vrai pour un vrai oui / non (créé par OuiOuNon) : il s'affiche en interrupteur.
        /// Faux pour un choix entre deux autres mots (DeuxChoix) : deux boutons.
        /// </summary>
        public bool Switch { get; private set; }

        /// <summary>Le mot du bouton de gauche (valeur « faux ») : « Non » d'habitude.</summary>
        public string WrongWord { get; private set; }

        /// <summary>Le mot du bouton de droite (valeur « vrai ») : « Oui » d'habitude.</summary>
        public string RightWord { get; private set; }

        private Func<AppSettings, double> _readNumber;
        private Action<AppSettings, double> _writeNumber;
        private Func<AppSettings, bool> _readYesNo;
        private Action<AppSettings, bool> _writeYesNo;

        /// <summary>Appelée après chaque changement (null = rien de spécial à faire).</summary>
        public Action<AppSettings> AfterChange { get; private set; }

        /// <summary>Crée un réglage numérique.</summary>
        public static Setting Number(string name, string explanation, string unit, double step,
                                     Func<AppSettings, double> read, Action<AppSettings, double> write)
        {
            Setting r = new Setting();
            r.Name = name;
            r.Explanation = explanation;
            r.Unit = unit;
            r.Step = step;
            r.Decimals = CountDecimals(step);
            r._readNumber = read;
            r._writeNumber = write;
            return r;
        }

        /// <summary>Crée un réglage oui / non. apres : facultatif, appelé après chaque changement.</summary>
        public static Setting YesOrNo(string name, string explanation,
                                       Func<AppSettings, bool> read, Action<AppSettings, bool> write,
                                       Action<AppSettings> after = null)
        {
            Setting r = TwoChoices(name, explanation, "Non", "Oui", read, write, after);
            r.Switch = true;   // affiché en interrupteur (02/11/2026)
            return r;
        }

        /// <summary>
        /// Crée un choix entre deux mots (étape 9). Le réglage est rangé comme un oui / non :
        /// « faux » = motFaux (bouton de gauche), « vrai » = motVrai (bouton de droite).
        /// Exemple : DeuxChoix("Souris", "…", "Directe", "Relative", p => p.SourisRelative, …).
        /// </summary>
        public static Setting TwoChoices(string name, string explanation, string wrongWord, string rightWord,
                                        Func<AppSettings, bool> read, Action<AppSettings, bool> write,
                                        Action<AppSettings> after = null)
        {
            Setting r = new Setting();
            r.Name = name;
            r.Explanation = explanation;
            r.Unit = "";
            r.YesNo = true;
            r.WrongWord = wrongWord;
            r.RightWord = rightWord;
            r._readYesNo = read;
            r._writeYesNo = write;
            r.AfterChange = after;
            return r;
        }

        public double ReadNumber(AppSettings p)
        {
            return _readNumber(p);
        }

        public bool ReadYesNo(AppSettings p)
        {
            return _readYesNo(p);
        }

        /// <summary>
        /// Écrit une nouvelle valeur, la ramène dans ses limites (Corriger) et l'arrondit au
        /// nombre de décimales affiché (pour éviter 0.30000000000000004 après plusieurs « + »).
        /// </summary>
        public void WriteNumber(AppSettings p, double val)
        {
            _writeNumber(p, Math.Round(val, Decimals));
            p.Fix();
        }

        public void WriteYesNo(AppSettings p, bool val)
        {
            _writeYesNo(p, val);
        }

        /// <summary>« − » (sens = -1) ou « + » (sens = +1) : un pas de plus ou de moins.</summary>
        public void Change(AppSettings p, int direction)
        {
            WriteNumber(p, ReadNumber(p) + direction * Step);
        }

        /// <summary>Copie la valeur de « source » dans « cible » (pour le bouton « Défaut » d'un onglet).</summary>
        public void Copy(AppSettings source, AppSettings target)
        {
            if (YesNo)
            {
                WriteYesNo(target, ReadYesNo(source));
            }
            else
            {
                WriteNumber(target, ReadNumber(source));
            }
        }

        /// <summary>La valeur prête à afficher, avec son unité : « 0.16 », « 0.8 s », « oui », « directe ».</summary>
        public string Text(AppSettings p)
        {
            if (YesNo)
            {
                return (ReadYesNo(p) ? RightWord : WrongWord).ToLowerInvariant();
            }
            return NumberText(ReadNumber(p)) + Unit;
        }

        /// <summary>Le nombre seul, avec un point et le bon nombre de décimales (pour la saisie).</summary>
        public string NumberText(double val)
        {
            return val.ToString("F" + Decimals, Format);
        }

        /// <summary>
        /// Lit un nombre tapé par l'aidant. Accepte le point ET la virgule (0.16 ou 0,16),
        /// et ignore l'unité si elle a été tapée (« 0.8 s »). Renvoie faux si ce n'est pas un nombre.
        /// </summary>
        public static bool ReadInput(string text, out double val)
        {
            string own = (text ?? "").Trim().Replace(',', '.');
            // On ne garde que le début : chiffres, signe moins et point.
            int end = 0;
            while (end < own.Length && ("0123456789.-".IndexOf(own[end]) >= 0))
            {
                end++;
            }
            return double.TryParse(own.Substring(0, end), NumberStyles.Float, Format, out val);
        }

        private static int CountDecimals(double step)
        {
            string text = step.ToString("0.########", Format);
            int point = text.IndexOf('.');
            return point < 0 ? 0 : text.Length - point - 1;
        }
    }

    /// <summary>Un onglet du mode paramètres : son nom, son symbole et ses réglages (cinq au plus).</summary>
    internal sealed class SettingsTab
    {
        public SettingsTab(string name, string symbol, params Setting[] settingsList)
        {
            Name = name;
            Symbol = symbol;
            Settings = new List<Setting>(settingsList);
        }

        public string Name { get; private set; }

        /// <summary>Symbole de la police d'icônes de Windows (Segoe Fluent Icons / Segoe MDL2 Assets).</summary>
        public string Symbol { get; private set; }

        public List<Setting> Settings { get; private set; }
    }

    /// <summary>
    /// Une CATÉGORIE de la colonne de gauche des paramètres (demande de l'utilisateur du
    /// 02/11/2026 : « moins de boutons sur la gauche »). Une catégorie regroupe quelques
    /// onglets ; seule la catégorie ouverte montre ses onglets, en dessous d'elle et en retrait
    /// (« catégories dépliantes », choix de l'utilisateur du 02/11/2026).
    /// </summary>
    internal sealed class SettingsCategory
    {
        public SettingsCategory(string name, string symbol, params string[] tabNames)
        {
            Name = name;
            Symbol = symbol;
            TabNames = new List<string>(tabNames);
        }

        public string Name { get; private set; }

        /// <summary>Symbole de la police d'icônes de Windows (Segoe Fluent Icons / Segoe MDL2 Assets).</summary>
        public string Symbol { get; private set; }

        /// <summary>Les noms des onglets de la catégorie, dans l'ordre d'affichage.</summary>
        public List<string> TabNames { get; private set; }
    }

    /// <summary>
    /// LA LISTE DES ONGLETS ET DES RÉGLAGES du mode paramètres (étape 7).
    ///
    /// Depuis le 02/11/2026, l'ordre d'affichage à gauche est celui des CATÉGORIES (voir
    /// « Categories » plus bas) : la liste « Onglets » sert seulement à retrouver un onglet par
    /// son numéro. Le premier onglet de la liste (« Regard ») est celui qui s'ouvre par défaut.
    ///
    /// Étape 11 : les tailles (anti-tremblement, rayon de fixation, vitesse max, rayon du
    /// cercle, taille des icônes) sont en % DE LA HAUTEUR DE L'ÉCRAN (voir Ecran.cs), et
    /// l'onglet « Affichage » gagne « Taille de l'interface ».
    ///
    /// Regroupement validé par l'utilisateur le 26/10/2026 (six onglets ; « Défilement » ajouté à l'étape 8,
    /// « Jeu » à l'étape 9).
    ///
    /// Pour ajouter un réglage plus tard : l'ajouter dans Parametres.cs (avec sa valeur par
    /// défaut et ses limites dans Corriger), puis ici, dans l'onglet voulu (cinq au plus par
    /// onglet, pour que rien ne défile). L'onglet « Défilement » est ajouté à l'étape 8,
    /// l'onglet « Jeu » à l'étape 9, l'onglet « Icônes » à l'étape 10, l'onglet « Clavier » à l'étape 12.
    ///
    /// L'onglet « Icônes » est particulier : il n'a pas de réglage « − / + », mais un gros
    /// bouton qui ouvre l'écran de configuration des icônes du choix d'action
    /// (ModeConfigIcones). Son bouton « Défaut » remet la disposition d'origine des icônes.
    ///
    /// L'onglet « Outils » (dernier) n'a pas de réglage non plus : deux gros boutons,
    /// « Calibration » et « Diagnostic », qui étaient avant dans la barre de menu. Il n'a pas
    /// de bouton « Défaut » (il n'y a rien à remettre).
    ///
    /// L'onglet « Profils » (étape 15, avant « Outils ») n'a pas de réglage ni de « Défaut » :
    /// deux gros boutons pour enregistrer les réglages actuels et charger un profil.
    /// </summary>
    internal static class SettingsCatalog
    {
        /// <summary>Le nom de l'onglet « Icônes » (étape 10), reconnu par FenetreParametres.</summary>
        public const string IconsTabName = "Icônes";

        /// <summary>Le numéro de l'onglet « Icônes » dans la liste (pour y revenir après la configuration).</summary>
        public static int IconsTabIndex
        {
            get { return Math.Max(0, Tabs.FindIndex(o => o.Name == IconsTabName)); }
        }

        /// <summary>
        /// Le nom de l'onglet « Général », reconnu par FenetreParametres (qui y ajoute le bouton
        /// « Mes mots », étape 14, demande de l'utilisateur du 02/11/2026).
        /// </summary>
        public const string GeneralTabName = "Général";

        /// <summary>Le numéro de l'onglet « Général » (pour y revenir après la liste « Mes mots »).</summary>
        public static int GeneralTabIndex
        {
            get { return Math.Max(0, Tabs.FindIndex(o => o.Name == GeneralTabName)); }
        }

        /// <summary>
        /// Le nom de l'onglet « Outils » (calibration et diagnostic, déplacés de la barre de
        /// menu le 29/10/2026), reconnu par FenetreParametres.
        /// </summary>
        public const string ToolsTabName = "Outils";

        /// <summary>
        /// Le nom de l'onglet « Profils » (étape 15), reconnu par FenetreParametres, qui y met
        /// deux gros boutons : « Enregistrer les réglages actuels » et « Charger un profil ».
        /// </summary>
        public const string ProfilesTabName = "Profils";

        /// <summary>Le numéro de l'onglet « Profils » (pour y revenir après la liste des profils).</summary>
        public static int ProfilesTabIndex
        {
            get { return Math.Max(0, Tabs.FindIndex(o => o.Name == ProfilesTabName)); }
        }

        /// <summary>
        /// LES CATÉGORIES de la colonne de gauche (02/11/2026), dans l'ordre d'affichage, avec
        /// les noms de leurs onglets. Chaque onglet de « Onglets » doit être dans une catégorie
        /// et une seule (sinon il ne serait plus atteignable au regard).
        /// Symboles : E890 = œil ouvert (« vue »), E7C9 = main qui touche (« actions »),
        /// E7F4 = écran, E770 = ordinateur (« système »).
        /// </summary>
        public static readonly List<SettingsCategory> Categories = new List<SettingsCategory>
        {
            new SettingsCategory("Regard", "\uE890", "Regard", "Fixation", "Délais"),
            new SettingsCategory("Actions", "\uE7C9", "Loupe, glisser", "Défilement", IconsTabName),
            new SettingsCategory("Affichage", "\uE7F4", "Affichage", "Clavier", "Jeu"),
            new SettingsCategory("Système", "\uE770", GeneralTabName, ProfilesTabName, ToolsTabName)
        };

        /// <summary>
        /// Le numéro de la catégorie qui contient l'onglet numéro « onglet » (0 si aucune,
        /// ce qui ne devrait pas arriver).
        /// </summary>
        public static int CategoryOf(int tab)
        {
            if (tab < 0 || tab >= Tabs.Count)
            {
                return 0;
            }
            string name = Tabs[tab].Name;
            return Math.Max(0, Categories.FindIndex(c => c.TabNames.Contains(name)));
        }

        /// <summary>
        /// Les numéros (dans « Onglets ») des onglets de la catégorie « categorie », dans
        /// l'ordre d'affichage. Un nom introuvable est ignoré.
        /// </summary>
        public static List<int> TabsOf(int category)
        {
            List<int> numbers = new List<int>();
            foreach (string name in Categories[category].TabNames)
            {
                int num = Tabs.FindIndex(o => o.Name == name);
                if (num >= 0)
                {
                    numbers.Add(num);
                }
            }
            return numbers;
        }

        public static readonly List<SettingsTab> Tabs = new List<SettingsTab>
        {
            // E7B3 = œil (« Regard »)
            new SettingsTab("Regard", "",
                Setting.Number("Lissage", "Plus petit = plus calme, plus grand = plus réactif", "", 0.01,
                               p => p.Smoothing, (p, v) => p.Smoothing = v),
                Setting.Number("Anti-tremblement", "Le curseur ne bouge pas en dessous", " %", 0.1,
                               p => p.CursorDeadZone, (p, v) => p.CursorDeadZone = v),
                Setting.Number("Regard perdu après", "Sans nouvelle mesure depuis ce temps", " ms", 50,
                               p => p.MaxMeasureAge, (p, v) => p.MaxMeasureAge = v),
                Setting.YesOrNo("Suivi souris", "Le curseur suit le regard en mode mouvement",
                                 p => p.MouseTracking, (p, v) => p.MouseTracking = v)),

            // E707 = épingle (« Fixation » : un point fixé)
            new SettingsTab("Fixation", "",
                Setting.Number("Rayon de fixation", "Le regard est immobile dans ce rayon", " %", 0.1,
                               p => p.FixationRadius, (p, v) => p.FixationRadius = v),
                Setting.Number("Délai de fixation", "Avant de passer en regard fixe", " s", 0.1,
                               p => p.FixationDelay, (p, v) => p.FixationDelay = v),
                Setting.Number("Vitesse max", "Au-delà, l'œil saute ailleurs", " %/s", 2,
                               p => p.MaxFixationSpeed, (p, v) => p.MaxFixationSpeed = v),
                Setting.YesOrNo("Clic direct", "Clic gauche si la fixation se prolonge",
                                 p => p.DirectClick, (p, v) => p.DirectClick = v),
                Setting.Number("Délai du clic direct", "Après l'entrée en regard fixe", " s", 0.1,
                               p => p.DirectClickDelay, (p, v) => p.DirectClickDelay = v)),

            // E916 = chronomètre (« Délais »)
            new SettingsTab("Délais", "",
                Setting.Number("Activation des boutons", "Regard sur une icône avant activation", " s", 0.1,
                               p => p.ActivationDelay, (p, v) => p.ActivationDelay = v),
                Setting.Number("Icône d'options", "Regard fixe : icône vers le choix d'action", " s", 0.1,
                               p => p.OptionsIconDelay, (p, v) => p.OptionsIconDelay = v),
                Setting.Number("Ouverture du menu", "Regard en bas, hors de l'écran", " s", 0.1,
                               p => p.MenuDelay, (p, v) => p.MenuDelay = v),
                Setting.Number("Fermeture du menu", "Regard hors de la barre", " s", 0.1,
                               p => p.MenuCloseDelay, (p, v) => p.MenuCloseDelay = v)),

            // E7F4 = écran (« Affichage »)
            new SettingsTab("Affichage", "",
                Setting.Number("Rayon du cercle", "Mode mouvement et zone de sortie", " %", 0.2,
                               p => p.CircleRadius, (p, v) => p.CircleRadius = v),
                Setting.Number("Taille des icônes", "Côté des boutons au regard", " %", 0.2,
                               p => p.IconSize, (p, v) => p.IconSize = v),
                Setting.Number("Taille de l'interface", "75 petite, 100 normale, 125 grande, 150 très grande", " %", 25,
                               p => p.InterfaceSize, (p, v) => p.InterfaceSize = v),
                Setting.YesOrNo("Libellés", "Nom de l'action au survol d'une icône",
                                 p => p.ShowLabels, (p, v) => p.ShowLabels = v)),

            // E71E = loupe (« Loupe, glisser »)
            new SettingsTab("Loupe, glisser", "",
                Setting.YesOrNo("Loupe avant l'action", "Passer par la loupe avant le choix",
                                 p => p.MagnifierBeforeAction, (p, v) => p.MagnifierBeforeAction = v),
                Setting.Number("Zoom de la loupe", "Agrandissement de la capture", " ×", 0.5,
                               p => p.MagnifierZoom, (p, v) => p.MagnifierZoom = v),
                Setting.Number("Pause android", "Entre l'appui et le déplacement", " s", 0.1,
                               p => p.AndroidPauseDuration, (p, v) => p.AndroidPauseDuration = v),
                Setting.Number("Durée du glisser", "Trajet du curseur, bouton enfoncé", " s", 0.05,
                               p => p.DragDuration, (p, v) => p.DragDuration = v)),

            // EC8F = flèches haut et bas (« Défilement », étape 8)
            new SettingsTab("Défilement", "\uEC8F",
                Setting.Number("Vitesse maximale", "Atteinte trois rayons de cercle au-delà du bord", " crans/s", 1,
                               p => p.MaxScrollSpeed, (p, v) => p.MaxScrollSpeed = v),
                // Réglage de Windows, appliqué par MoletteWindows (02/11/2026).
                Setting.YesOrNo("Défiler les fenêtres inactives", "Réglage Windows : la molette va à la fenêtre sous le curseur",
                                 p => p.ScrollInactiveWindows, (p, v) => p.ScrollInactiveWindows = v,
                                 p => WheelScroller.Apply(p.ScrollInactiveWindows))),

            // E7FC = manette (« Jeu », étape 9)
            new SettingsTab("Jeu", "\uE7FC",
                Setting.TwoChoices("Souris du jeu", "Directe pour Mindball, relative pour d'autres jeux",
                                  "Directe", "Relative",
                                  p => p.RelativeMouse, (p, v) => p.RelativeMouse = v),
                Setting.TwoChoices("Souris relative", "Suivi : comme le regard. Manette : écart au centre",
                                  "Suivi", "Manette",
                                  p => p.GamepadRelative, (p, v) => p.GamepadRelative = v),
                Setting.Number("Lissage du jeu", "Plus petit = plus calme, plus grand = plus réactif", "", 0.01,
                               p => p.GameSmoothing, (p, v) => p.GameSmoothing = v),
                Setting.Number("Sensibilité", "Souris relative : multiplie les déplacements", " ×", 0.1,
                               p => p.GameSensitivity, (p, v) => p.GameSensitivity = v)),

            // E71D = grille de carrés (« Icônes », étape 10) : pas de réglage, un bouton
            // « Configurer les icônes » (voir FenetreParametres.ConstruireOngletIcones).
            new SettingsTab(IconsTabName, "\uE71D"),

            // E765 = clavier (« Clavier », étape 12)
            new SettingsTab("Clavier", "\uE765",
                Setting.Number("Délai de frappe", "Regard sur une touche avant qu'elle s'active", " s", 0.1,
                               p => p.KeyDelay, (p, v) => p.KeyDelay = v),
                Setting.YesOrNo("Répétition", "Garder le regard sur une touche la retape",
                                 p => p.KeyRepeat, (p, v) => p.KeyRepeat = v),
                Setting.YesOrNo("Majuscule automatique", "Après . ? ! suivi d'une espace",
                                 p => p.AutoCapitalize, (p, v) => p.AutoCapitalize = v),
                Setting.Number("Hauteur du clavier", "En pourcentage de la hauteur de l'écran", " %", 5,
                               p => p.KeyboardHeight, (p, v) => p.KeyboardHeight = v),
                // 50, 75 ou 100 % (Corriger arrondit au quart) ; aussi dans la barre du clavier.
                Setting.Number("Taille du clavier", "Largeur et hauteur ensemble : 50, 75 ou 100 %", " %", 25,
                               p => p.KeyboardSize, (p, v) => p.KeyboardSize = v)),

            // E713 = engrenage (« Général »)
            new SettingsTab(GeneralTabName, "",
                Setting.YesOrNo("Démarrer avec Windows", "Lancer GazeBar à l'ouverture de session",
                                 p => p.StartWithWindows, (p, v) => p.StartWithWindows = v,
                                 p => WindowsStartup.Apply(p.StartWithWindows))),

            // E77B = personne (« Profils », étape 15) : pas de réglage, deux gros boutons
            // « Enregistrer les réglages actuels » et « Charger un profil » (voir
            // FenetreParametres.ConstruireOngletProfils).
            new SettingsTab(ProfilesTabName, "\uE77B"),

            // E90F = clé à molette (« Outils », 29/10/2026) : pas de réglage, deux gros boutons
            // « Calibration » et « Diagnostic » (voir FenetreParametres.ConstruireOngletOutils).
            new SettingsTab(ToolsTabName, "\uE90F")
        };
    }
}
