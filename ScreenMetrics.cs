using System;
using Microsoft.Win32;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : les mesures de l'écran, et la conversion des tailles de GazeBar en pixels
    /// (étape 11, « mesures-ecran »).
    ///
    /// LE PROBLÈME : jusqu'à l'étape 10, les tailles étaient écrites en pixels (cercle de
    /// 190 px, icônes de 120 px…), réglées sur l'écran 4K de l'utilisateur (3840 × 2160).
    /// Sur un écran 1920 × 1080, le même cercle de 190 px paraît deux fois plus grand.
    ///
    /// LA SOLUTION (cahier des charges, étape 11) : les tailles sont exprimées en
    /// POURCENTAGE DE LA HAUTEUR DE L'ÉCRAN. Un cercle de 8.8 % mesure 190 px sur un écran
    /// de 2160 px de haut, et 95 px sur un écran de 1080 px : sur le même écran physique, il
    /// garde la même taille réelle. C'est ce qui compte pour le regard, dont la précision ne
    /// dépend pas de la résolution choisie dans Windows.
    ///
    /// Trois sortes de valeurs passent par ici :
    ///   1. les RÉGLAGES de parametres.json, en % de la hauteur (RayonCercle, TailleIcones…) :
    ///      méthodes Pourcent et PourcentInterface ;
    ///   2. les CONSTANTES écrites en pixels dans le code (par exemple l'espace de 60 px entre
    ///      les boutons du choix d'action). Elles ont été réglées sur l'écran 4K de
    ///      l'utilisateur : on les garde telles quelles, comme « pixels d'un écran de 2160 px
    ///      de haut », et on les convertit : méthodes Px et PxRegard ;
    ///   3. les CONSTANTES écrites en unités WPF (épaisseur d'un trait, taille d'un texte…).
    ///      Sur l'écran 4K à 200 %, l'écran mesure 1080 unités WPF de haut : on les garde comme
    ///      « unités d'un écran de 1080 unités de haut » : méthodes Unites et Police.
    ///
    /// Deux réglages de Windows s'y ajoutent :
    ///   - la MISE À L'ÉCHELLE (100 %, 125 %, 200 %…) : elle ne change que la conversion entre
    ///     pixels et unités WPF, la taille réelle des éléments de GazeBar reste la même ;
    ///   - la TAILLE DU TEXTE (Paramètres de Windows › Accessibilité › Taille du texte, de 100 à
    ///     225 %) : elle agrandit les textes du calque (libellés, barre de menu…), méthode Police.
    /// Et un réglage de GazeBar : [TailleInterface] (75, 100, 125 ou 150 %), qui agrandit ou
    /// réduit tout ce qui est dessiné, mais pas les réglages du regard (rayon de fixation,
    /// vitesse maximale, anti-tremblement) : le tremblement de l'œil ne dépend pas du goût
    /// de chacun pour les grandes icônes.
    ///
    /// LES CHANGEMENTS PENDANT QUE GAZEBAR TOURNE : le calque (OverlayWindow) appelle Mesurer
    /// quand Windows annonce un changement d'affichage, et aussi toutes les deux secondes par
    /// précaution. Si quelque chose a changé, l'événement Change est déclenché : le
    /// gestionnaire de modes redessine ce qu'il faut (voir ModeManager.OnEcranChange).
    ///
    /// « static » : il n'y a qu'un écran principal, ces valeurs sont partagées par tout le programme.
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal static class ScreenMetrics
    {
        /// <summary>Hauteur, en pixels, de l'écran sur lequel les constantes en pixels ont été réglées (4K).</summary>
        public const double ReferenceHeightPixels = 2160;

        /// <summary>Hauteur, en unités WPF, de ce même écran (4K à 200 %).</summary>
        public const double ReferenceHeightUnits = 1080;

        /// <summary>Largeur de l'écran principal, en pixels réels.</summary>
        public static double WidthPixels { get; private set; } = 1920;

        /// <summary>Hauteur de l'écran principal, en pixels réels.</summary>
        public static double HeightPixels { get; private set; } = 1080;

        /// <summary>Mise à l'échelle de Windows : 1 = 100 %, 1.25 = 125 %, 2 = 200 %.</summary>
        public static double WindowsScale { get; private set; } = 1;

        /// <summary>Taille du texte de Windows (Accessibilité) : 1 = 100 %, 2.25 = 225 %.</summary>
        public static double WindowsText { get; private set; } = 1;

        /// <summary>
        /// La mise à l'échelle de Windows AU DÉMARRAGE de GazeBar (1 = 100 %). Windows la
        /// « fige » pour le programme : elle ne change plus ensuite, même si l'on change la
        /// mise à l'échelle dans les paramètres de Windows. Lue avec GetDpiForSystem.
        /// </summary>
        public static double StartupScale { get; } = ReadStartupScale();

        /// <summary>
        /// Correction du regard de l'eyetracker (étape 11, correction du 29/10/2026).
        ///
        /// Essai de l'utilisateur : GazeBar lancé en 1920 × 1080 à 100 %, puis écran passé en
        /// 3840 × 2160 à 150 %. Le journal montre que le regard reçu de l'eyetracker ne dépassait plus
        /// X 1830 environ (au lieu de 3840) ; revenu à 100 %, il atteignait de nouveau 3802.
        /// L'eyetracker donne donc le regard dans les unités de l'échelle du DÉMARRAGE : il est réduit
        /// de 150 ÷ 100 = 1.5. On le multiplie par ce rapport pour retrouver les vrais pixels.
        /// Quand l'échelle n'a pas changé depuis le démarrage (cas habituel), le rapport vaut 1
        /// et rien ne change (c'est pourquoi tout marchait en 4K à 200 % dès le lancement).
        /// </summary>
        public static double GazeFactor
        {
            // Avant la première mesure (au tout début), pas de correction.
            get { return _measure ? WindowsScale / StartupScale : 1; }
        }

        /// <summary>Hauteur de l'écran en unités WPF (1080 pour un écran 4K à 200 % ou 1080p à 100 %).</summary>
        public static double HeightUnits
        {
            get { return HeightPixels / WindowsScale; }
        }

        /// <summary>Déclenché quand la résolution, la mise à l'échelle ou la taille du texte a changé.</summary>
        public static event Action Change;

        // Vrai après la première mesure (au démarrage, rien n'a « changé » : pas d'événement).
        private static bool _measure;

        /// <summary>[TailleInterface] en facteur : 100 % → 1, 125 % → 1.25.</summary>
        private static double InterfaceFactor
        {
            get { return AppSettings.Current.InterfaceSize / 100.0; }
        }

        // ───────────────────────── Conversions ─────────────────────────

        /// <summary>
        /// Un pourcentage de la hauteur de l'écran, en pixels. Pour les réglages du REGARD
        /// (rayon de fixation, vitesse maximale, anti-tremblement) : [TailleInterface] ne
        /// s'applique pas. Exemple : 1.4 % sur un écran de 1080 px → 15 px.
        /// </summary>
        public static double Percent(double percent)
        {
            return percent / 100.0 * HeightPixels;
        }

        /// <summary>
        /// Un pourcentage de la hauteur de l'écran, en pixels, multiplié par [TailleInterface].
        /// Pour ce qui est DESSINÉ (rayon du cercle, taille des icônes).
        /// Exemple : 8.8 % sur un écran de 1080 px, interface à 100 % → 95 px.
        /// </summary>
        public static double InterfacePercent(double percent)
        {
            return Percent(percent) * InterfaceFactor;
        }

        /// <summary>
        /// Une constante du code écrite en pixels de l'écran 4K (2160 px de haut), convertie en
        /// pixels de l'écran actuel, multipliée par [TailleInterface]. Pour ce qui est dessiné.
        /// Exemple : 60 px sur l'écran 4K → 30 px sur un écran de 1080 px.
        /// </summary>
        public static double Px(double pixels4K)
        {
            return GazePx(pixels4K) * InterfaceFactor;
        }

        /// <summary>
        /// Comme Px, mais sans [TailleInterface] : pour une distance du REGARD (par exemple la
        /// marge hors de l'écran du mode défilement).
        /// </summary>
        public static double GazePx(double pixels4K)
        {
            return pixels4K * HeightPixels / ReferenceHeightPixels;
        }

        /// <summary>
        /// Une constante du code écrite en unités WPF de l'écran 4K à 200 % (1080 unités de
        /// haut), convertie en unités WPF de l'écran actuel, multipliée par [TailleInterface].
        /// Pour les épaisseurs de trait, les coins arrondis, les marges des textes…
        /// Sur un écran 1080p à 100 % comme sur un écran 4K à 200 %, elle ne change pas.
        /// </summary>
        public static double Units(double units)
        {
            return units * HeightUnits / ReferenceHeightUnits * InterfaceFactor;
        }

        /// <summary>
        /// Une taille de police, comme Unites, multipliée en plus par la taille du texte de
        /// Windows. Pour les textes du calque (libellés au survol, barre de menu…).
        /// </summary>
        public static double Font(double units)
        {
            return Units(units) * WindowsText;
        }

        // ───────────────────────── Mesure ─────────────────────────

        /// <summary>
        /// Lit la taille de l'écran principal et la taille du texte de Windows.
        /// echelle : la mise à l'échelle de Windows, donnée par le calque (1 = 100 %).
        /// La première fois, ne déclenche rien ; ensuite, déclenche Change si une valeur a changé.
        /// Renvoie vrai si quelque chose a changé.
        /// </summary>
        public static bool Measure(double scale)
        {
            // La vraie résolution (GetSystemMetrics ne voit pas toujours le changement : voir
            // NativeMethods.TailleEcranPrincipal).
            double width;
            double height;
            NativeMethods.PrimaryScreenSize(out width, out height);
            if (width <= 0 || height <= 0)
            {
                return false;   // mesure impossible pour l'instant (écran en train de changer)
            }
            if (double.IsNaN(scale) || scale <= 0)
            {
                scale = WindowsScale;
            }
            double text = ReadTextSize();

            bool change = width != WidthPixels || height != HeightPixels ||
                          Math.Abs(scale - WindowsScale) > 0.001 ||
                          Math.Abs(text - WindowsText) > 0.001;
            bool firstOne = !_measure;
            _measure = true;
            if (!change && !firstOne)
            {
                return false;
            }

            WidthPixels = width;
            HeightPixels = height;
            WindowsScale = scale;
            WindowsText = text;
            App.Log("Écran : " + Description() + (firstOne ? "" : " (changement détecté)") +
                    " ; échelle au démarrage " + (StartupScale * 100).ToString("F0") +
                    " %, regard × " + GazeFactor.ToString("0.###"));

            if (firstOne)
            {
                return false;
            }
            Action subscribers = Change;
            if (subscribers != null)
            {
                subscribers();
            }
            return true;
        }

        /// <summary>Résumé lisible, pour le journal et l'onglet « Affichage » : « 1920 × 1080 px, échelle 100 %, texte 100 % ».</summary>
        public static string Description()
        {
            return WidthPixels.ToString("F0") + " × " + HeightPixels.ToString("F0") + " px, échelle " +
                   (WindowsScale * 100).ToString("F0") + " %, texte " + (WindowsText * 100).ToString("F0") + " %";
        }

        /// <summary>La mise à l'échelle figée au démarrage (voir EchelleDemarrage). 1 si elle est illisible.</summary>
        private static double ReadStartupScale()
        {
            try
            {
                uint dpi = NativeMethods.GetDpiForSystem();
                if (dpi > 0)
                {
                    return dpi / 96.0;   // 96 points par pouce = 100 %
                }
            }
            catch (EntryPointNotFoundException)
            {
                // Windows 10 trop ancien (avant la version 1607) : pas de GetDpiForSystem.
            }
            return 1;
        }

        /// <summary>
        /// La taille du texte de Windows (Accessibilité › Taille du texte). Windows la range
        /// dans le registre, sous HKEY_CURRENT_USER\Software\Microsoft\Accessibility, valeur
        /// « TextScaleFactor », de 100 à 225 (en %). Absente = jamais changée = 100 %.
        /// WPF ne l'applique pas tout seul : c'est GazeBar qui la lit (méthode Police).
        /// </summary>
        private static double ReadTextSize()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility"))
                {
                    object val = key == null ? null : key.GetValue("TextScaleFactor");
                    if (val is int)
                    {
                        int percent = (int)val;
                        return Math.Max(100, Math.Min(225, percent)) / 100.0;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Écran : taille du texte illisible (" + ex.Message + "), 100 % utilisé");
            }
            return 1;
        }
    }
}
