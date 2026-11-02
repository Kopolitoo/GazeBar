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
    /// quand le clavier est en haut). On peut aussi le fermer depuis la barre de menu du clavier (regard sous
    /// l'écran), ou avec la touche Échap d'un vrai clavier (pour l'aidant).
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
    internal sealed class ClavierRegard
    {
        // Distances en pixels de l'écran 4K, converties pour l'écran actuel (Ecran.Px).
        private const double Marge4K = 16;          // entre le bord du clavier et les touches
        private const double EcartTouches4K = 12;   // espace entre deux touches
        // Entre le bouton Fermer et le clavier : 24 px au début, 60 px depuis le 01/11/2026
        // (demande de l'utilisateur : bouton trop près des touches).
        private const double EcartFermer4K = 60;

        /// <summary>Zone d'activation du bouton Fermer : 20 % plus grande que son dessin.</summary>
        private const double FacteurZoneFermer = 1.2;

        /// <summary>Hauteur de la zone de saisie (étape 14), par rapport à une rangée de touches.</summary>
        private const double ProportionZoneSaisie = 0.7;

        /// <summary>Nombre de caractères gardés pour l'aperçu de la zone de saisie (on n'en montre que la fin).</summary>
        private const int LongueurMaxApercu = 400;

        // Couleurs de la zone de saisie (figées : WPF les dessine plus vite).
        private static readonly Brush FondZoneSaisie = Figer(Color.FromArgb(0xFF, 0x26, 0x27, 0x2A));
        private static readonly Brush TexteZoneSaisie = Figer(Colors.White);
        private static readonly Brush TexteZoneSaisieVide = Figer(Color.FromArgb(0xFF, 0x80, 0x80, 0x80));

        private readonly OverlayWindow _calque;

        // La disposition lue à l'ouverture, la page affichée et la page d'avant (touche « ↺ »).
        private DispositionClavier _disposition;
        private string _page;
        private string _pagePrecedente;

        // Les cibles visées au regard : les touches de la page, puis le bouton Fermer (le dernier).
        private readonly List<ICibleRegard> _cibles = new List<ICibleRegard>();
        private readonly List<ToucheClavier> _touches = new List<ToucheClavier>();
        private BoutonRegard _fermer;
        private readonly ViseurBoutons _viseur;

        // Vrai quand le clavier est réellement dessiné à l'écran.
        private bool _visible;

        // Place gardée libre en bas de l'écran, en pixels : la hauteur de la barre de menu du
        // clavier quand elle est ouverte (voir DefinirReserveBas), 0 sinon.
        private double _reserveBas;

        // Le rectangle du clavier, en pixels d'écran.
        private double _gauche;
        private double _haut;
        private double _droite;
        private double _bas;

        // L'état des modificateurs du clavier. Ils sont à part de ceux du choix d'action
        // (Modificateurs.cs), qui sont remis à zéro à chaque retour au mode mouvement.
        private bool _maj;       // pour la prochaine touche seulement
        private bool _verrMaj;   // Caps Lock : reste allumé
        private bool _ctrl;
        private bool _alt;
        private bool _win;

        // Le « mot en cours » : les lettres tapées depuis le dernier espace ou signe (voir
        // Suivre). Sert à « Effacer le mot » et à la prédiction (étape 13).
        private readonly StringBuilder _motEnCours = new StringBuilder();

        // Étape 13 : la prédiction, et ce que montrent les cases de proposition (calculé par
        // MettreAJourEtat après chaque touche). _motInconnu : vrai quand la 1re case montre
        // « + monmot » (mot inconnu à ajouter).
        private readonly PredicteurMots _predicteur;
        private List<string> _propositions = new List<string>();
        private bool _motInconnu;

        // Le mot écrit juste avant le mot en cours (forme rangée du dictionnaire), pour la
        // prédiction du mot suivant (02/11/2026) ; null au début, après une ponctuation, ou
        // quand le curseur de texte a pu bouger (le mot d'avant n'est alors plus sûr).
        private string _motPrecedent;

        // Les deux derniers caractères tapés, pour la majuscule automatique après « . ».
        private string _fin = "";

        // Étape 14 : la zone de saisie. _apercu : ce que le clavier a tapé depuis que le
        // curseur de texte n'a plus bougé (gardé en mémoire seulement, jamais dans le journal) ;
        // _texteApercu : son texte affiché (null quand la zone n'est pas affichée) ;
        // _largeurApercuWpf : la largeur disponible pour ce texte, en unités WPF.
        private readonly StringBuilder _apercu = new StringBuilder();
        private TextBlock _texteApercu;
        private double _largeurApercuWpf;

        public ClavierRegard(OverlayWindow calque)
        {
            _calque = calque;
            // « 1 » : une seule mesure du regard suffit pour qu'une touche s'éclaire (2 pour
            // les autres boutons de GazeBar). Le survol réagit ainsi une mesure plus tôt
            // (environ 30 ms) ; demande de l'utilisateur du 02/11/2026.
            _viseur = new ViseurBoutons(_cibles, 1);

            // Le dictionnaire français est lu dès le démarrage de GazeBar, en arrière-plan
            // (cahier des charges) : il est prêt quand on ouvre le clavier.
            _predicteur = new PredicteurMots();
            _predicteur.ChargerLangue("fr");
        }

        /// <summary>
        /// À appeler à la fermeture de GazeBar : enregistre les nombres d'utilisations des mots
        /// qui ne l'ont pas encore été (ils le sont sinon une fois par minute).
        /// </summary>
        public void Arreter()
        {
            _predicteur.Arreter();
        }

        /// <summary>Vrai entre Ouvrir et Fermer (même quand le clavier est masqué par un autre mode).</summary>
        public bool Ouvert { get; private set; }

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
        public bool Ouvrir(out string message)
        {
            // Étape 14 : la disposition choisie avec le bouton « Langue ». Si son fichier a
            // disparu ou contient une erreur, on essaie la disposition par défaut (fr-azerty)
            // plutôt que de ne pas ouvrir le clavier du tout ; le choix enregistré ne change pas.
            string nom = Parametres.Courants.DispositionClavier;
            DispositionClavier disposition = DispositionClavier.Charger(nom, out message);
            if (disposition == null &&
                !string.Equals(nom, DispositionClavier.NomParDefaut, StringComparison.OrdinalIgnoreCase))
            {
                string messageDefaut;
                disposition = DispositionClavier.Charger(DispositionClavier.NomParDefaut, out messageDefaut);
                if (disposition != null)
                {
                    App.Log("Clavier : disposition « " + nom + " » inutilisable, on prend " + DispositionClavier.NomParDefaut);
                    message = null;
                }
            }
            if (disposition == null)
            {
                return false;
            }

            _disposition = disposition;
            // Le dictionnaire de la langue de la disposition (« fr » pour fr-azerty.json) :
            // déjà chargé au démarrage dans le cas habituel.
            _predicteur.ChargerLangue(disposition.Langue);
            _page = disposition.PageDepart;
            _pagePrecedente = null;
            Ouvert = true;
            OublierEtat();
            App.Log("Clavier : ouvert");
            return true;
        }

        /// <summary>Ferme le clavier (bouton Fermer, barre de menu du clavier, touche Échap).</summary>
        public void Fermer()
        {
            if (!Ouvert)
            {
                return;
            }
            Ouvert = false;
            Masquer();
            OublierEtat();
            App.Log("Clavier : fermé");
        }

        /// <summary>
        /// Appelée par le ModeManager à chaque changement de mode, AVANT l'entrée dans le
        /// nouveau mode : le clavier est affiché dans les modes mouvement et regard fixe, et
        /// pendant la barre de menu du clavier ; il est masqué dans tous les autres.
        /// (Avant l'entrée : la loupe fait une photo de l'écran dès son entrée, le clavier ne
        /// doit pas y être.)
        /// </summary>
        public void SurChangementMode(Mode nouveau)
        {
            ModeMenu menu = nouveau as ModeMenu;
            bool afficher = Ouvert &&
                            (nouveau is ModeMouvement || nouveau is ModeRegardFixe ||
                             (menu != null && menu.MenuClavier));

            if (afficher && !_visible)
            {
                Construire();
            }
            else if (!afficher && _visible)
            {
                Masquer();
                // On est parti faire autre chose (un clic ailleurs, les paramètres…) : le texte
                // autour du curseur a pu changer, le mot en cours n'est plus sûr.
                OublierPosition();
            }
            else if (_visible)
            {
                // Toujours affiché (mouvement ↔ regard fixe, menu du clavier) : plus aucune
                // touche n'est survolée, et celle qui serait sous le regard attend qu'il la quitte.
                _viseur.ReinitialiserEtBloquer();
                RedessinerSansSurvol();
            }
        }

        /// <summary>L'écran a changé (étape 11) : le clavier est redessiné aux nouvelles tailles.</summary>
        public void AdapterEcran()
        {
            if (_visible)
            {
                Construire();
            }
        }

        /// <summary>
        /// La barre de menu du clavier s'ouvre (pixels = sa hauteur, depuis le bas de l'écran)
        /// ou se ferme (0). Le clavier, s'il est en bas, remonte au-dessus d'elle au lieu
        /// d'être recouvert (demande de l'utilisateur du 01/11/2026). En haut, rien ne change.
        /// </summary>
        public void DefinirReserveBas(double pixels)
        {
            pixels = Math.Max(0, pixels);
            if (Math.Abs(pixels - _reserveBas) < 0.5)
            {
                return;
            }
            _reserveBas = pixels;
            if (_visible && !Parametres.Courants.ClavierEnHaut)
            {
                // Redessiné à sa nouvelle place, puis glissé depuis l'ancienne (02/11/2026).
                double ancienHaut = _haut;
                Construire();
                if (_visible)
                {
                    _calque.AnimerSurfaceClavier(ancienHaut - _haut);
                }
            }
        }

        /// <summary>
        /// Bouton « Haut / bas » de la barre du clavier : le clavier passe en haut de l'écran
        /// (ou revient en bas). La position est enregistrée : le clavier se rouvre au même endroit.
        /// </summary>
        public void BasculerPosition()
        {
            Parametres p = Parametres.Courants;
            p.ClavierEnHaut = !p.ClavierEnHaut;
            p.Enregistrer();
            App.Log("Clavier : " + (p.ClavierEnHaut ? "en haut" : "en bas") + " de l'écran");
            if (_visible)
            {
                Construire();
            }
        }

        /// <summary>
        /// Bouton « Taille » de la barre du clavier : 100 % → 75 % → 50 % → 100 %… (le même
        /// réglage que « Taille du clavier » dans l'onglet Clavier des paramètres). Enregistré.
        /// </summary>
        public void ChangerTaille()
        {
            Parametres p = Parametres.Courants;
            p.TailleClavier = p.TailleClavier > 75 ? 75 : p.TailleClavier > 50 ? 50 : 100;
            p.Enregistrer();
            App.Log("Clavier : taille " + p.TailleClavier + " %");
            if (_visible)
            {
                Construire();
            }
        }

        /// <summary>
        /// Bouton « Zone de saisie » de la barre du clavier (étape 14) : affiche ou cache la
        /// bande des derniers mots tapés. Enregistré. Le clavier est redessiné (il grandit ou
        /// rapetisse de la hauteur de la bande).
        /// </summary>
        public void BasculerZoneSaisie()
        {
            Parametres p = Parametres.Courants;
            p.ZoneSaisie = !p.ZoneSaisie;
            p.Enregistrer();
            App.Log("Clavier : zone de saisie " + (p.ZoneSaisie ? "affichée" : "cachée"));
            if (_visible)
            {
                Construire();
            }
        }

        /// <summary>
        /// La prédiction de mots, pour la liste « Mes mots » (étape 14, voir ModeListeClavier).
        /// </summary>
        public PredicteurMots Predicteur
        {
            get { return _predicteur; }
        }

        /// <summary>
        /// Bouton « Langue » (étape 14) : passe à une autre disposition du dossier Claviers
        /// (nom du fichier sans « .json »). Elle est lue tout de suite ; si elle est absente ou
        /// incorrecte, rien ne change et « message » explique pourquoi. Sinon le choix est
        /// enregistré, le dictionnaire de sa langue est chargé (en arrière-plan), et le clavier
        /// repart de la page de départ de la nouvelle disposition.
        /// </summary>
        public bool ChangerDisposition(string nom, out string message)
        {
            DispositionClavier disposition = DispositionClavier.Charger(nom, out message);
            if (disposition == null)
            {
                return false;
            }
            Parametres p = Parametres.Courants;
            p.DispositionClavier = nom;
            p.Enregistrer();

            _disposition = disposition;
            _predicteur.ChargerLangue(disposition.Langue);
            _page = disposition.PageDepart;
            _pagePrecedente = null;
            OublierEtat();
            App.Log("Clavier : disposition choisie : " + nom);
            if (_visible)
            {
                Construire();
            }
            return true;
        }

        /// <summary>
        /// Vrai si le point du regard (pixels d'écran, VRAI regard et non ramené dans l'écran)
        /// est sur le clavier ou sur son bouton Fermer. Faux si le clavier n'est pas affiché.
        /// </summary>
        public bool Contient(double x, double y)
        {
            if (!_visible)
            {
                return false;
            }
            if (x >= _gauche && x < _droite && y >= _haut && y < _bas)
            {
                return true;
            }
            return _fermer != null && _fermer.Contient(x, y);
        }

        private void Masquer()
        {
            _calque.MasquerSurfaceClavier();
            _visible = false;
        }

        /// <summary>Remet à zéro les modificateurs, le mot en cours et la fin du texte.</summary>
        private void OublierEtat()
        {
            _maj = false;
            _verrMaj = false;
            _ctrl = false;
            _alt = false;
            _win = false;
            OublierPosition();
        }

        /// <summary>
        /// Le curseur de texte a pu bouger (flèche, Tab, raccourci, clic ailleurs…) : on oublie
        /// le mot en cours, le mot d'avant, la fin du texte et l'aperçu de la zone de saisie,
        /// qui ne correspondent peut-être plus au texte autour du curseur.
        /// </summary>
        private void OublierPosition()
        {
            _motEnCours.Clear();
            _motPrecedent = null;
            _fin = "";
            _apercu.Clear();
        }

        // ───────────────────────── Le regard ─────────────────────────

        /// <summary>
        /// À appeler à chaque tic du mode mouvement, tant que le clavier est affiché : met à
        /// jour la touche survolée et l'active si le regard y est resté [DelaiTouche].
        /// </summary>
        public void Actualiser(EtatRegard regard)
        {
            if (!_visible)
            {
                return;
            }

            Parametres p = Parametres.Courants;
            int numero = _viseur.Actualiser(regard, p.DelaiTouche, p.AfficherLibelles);
            if (numero < 0)
            {
                return;
            }

            if (numero >= _touches.Count)
            {
                // Le dernier de la liste : le bouton Fermer.
                Fermer();
                return;
            }
            Activer(_touches[numero]);
        }

        /// <summary>Fait l'action d'une touche.</summary>
        private void Activer(ToucheClavier touche)
        {
            ToucheDisposition d = touche.Description;
            touche.Flasher();

            // Vrai pour une touche qu'on peut répéter en gardant le regard dessus
            // ([RepetitionTouche]) : lettres, effacer, flèches… Pas pour un modificateur
            // ou un raccourci : en continuant à regarder, on l'allumerait et l'éteindrait sans fin.
            bool repetable = true;

            if (d.Prediction.HasValue)
            {
                // Étape 13 : une case de proposition. Pas de répétition : en continuant à la
                // regarder, on n'écrirait pas deux fois le mot (la case est d'ailleurs vide
                // juste après, le mot en cours repartant de zéro).
                ChoisirProposition(d.Prediction.Value);
                repetable = false;
            }
            else if (d.Texte != null)
            {
                Ecrire(d);
            }
            else if (d.Touche != null)
            {
                AppuyerTouche(d.Touche);
            }
            else if (d.Raccourci != null)
            {
                Raccourci(d.Raccourci);
                repetable = false;
            }
            else if (d.Page != null)
            {
                // La page change : les touches sont remplacées (voir Construire).
                ChangerPage(d.Page);
                return;
            }
            else if (d.Modificateur != null)
            {
                BasculerModificateur(d.Modificateur);
                repetable = false;
            }
            else if (d.Commande == "EffacerLettre")
            {
                EffacerLettre();
            }
            else if (d.Commande == "EffacerMot")
            {
                EffacerMot();
            }
            else
            {
                App.Log("Clavier : touche « " + d.Libelle + " » sans action connue");
            }

            if (!repetable || !Parametres.Courants.RepetitionTouche)
            {
                _viseur.BloquerJusquASortie();
            }
            MettreAJourEtat();
        }

        // ───────────────────────── Les actions ─────────────────────────

        /// <summary>Écrit le texte d'une touche, en majuscules si Maj ou Caps Lock (mais pas les deux).</summary>
        private void Ecrire(ToucheDisposition d)
        {
            // Maj et Caps Lock ensemble donnent des minuscules, comme sur un vrai clavier.
            bool majuscules = _maj ^ _verrMaj;
            string texte = majuscules ? (d.TexteMaj ?? d.Texte.ToUpper(CultureInfo.CurrentCulture)) : d.Texte;

            if ((_ctrl || _alt || _win) && texte.Length == 1)
            {
                // Avec Ctrl, Alt ou Win, il faut la VRAIE touche (Ctrl + c = copier) : on demande à
                // Windows quelle touche produit ce caractère. Octet haut : 1 = il faut aussi Maj.
                short resultat = NativeMethods.VkKeyScanW(texte[0]);
                if (resultat != -1)
                {
                    ushort code = (ushort)(resultat & 0xFF);
                    List<ushort> mods = ModificateursActifs(false);
                    if (((resultat >> 8) & 1) != 0)
                    {
                        mods.Add(NativeMethods.VK_SHIFT);
                    }
                    InjecteurClavier.TaperTouche(code, false, mods);
                    _ctrl = false;
                    _alt = false;
                    _win = false;
                    _maj = false;
                    // Un raccourci n'écrit pas de texte : le mot en cours n'est plus sûr.
                    OublierPosition();
                    return;
                }
            }

            InjecteurClavier.TaperTexte(texte);
            Apercu(0, texte);
            _ctrl = false;
            _alt = false;
            _win = false;
            if (ContientUneLettre(texte))
            {
                _maj = false;   // Maj ne vaut que pour une lettre
            }
            Suivre(texte);
        }

        /// <summary>Appuie sur une vraie touche (Tab, Entree, flèches…), avec les modificateurs actifs.</summary>
        private void AppuyerTouche(string nom)
        {
            ushort code;
            bool etendue;
            if (!CodeTouche(nom, out code, out etendue))
            {
                App.Log("Clavier : touche inconnue « " + nom + " » dans la disposition");
                return;
            }

            // Ici, Maj compte (Maj + Tab, Maj + flèche pour sélectionner…).
            List<ushort> mods = ModificateursActifs(true);
            // Ctrl, Alt ou Win + Entrée n'est pas une simple fin de ligne (Maj + Entrée, si).
            bool avecModificateur = _ctrl || _alt || _win;
            InjecteurClavier.TaperTouche(code, etendue, mods);
            _ctrl = false;
            _alt = false;
            _win = false;
            _maj = false;

            if (code == 0x0D && !avecModificateur)
            {
                Suivre("\n");   // Entrée : fin de ligne (compte pour la majuscule automatique)
                Apercu(0, "\n");
            }
            else
            {
                // Tab, flèches, Début, Ctrl + Entrée… : le curseur de texte a pu bouger, le mot
                // en cours n'est plus sûr.
                OublierPosition();
            }
        }

        /// <summary>
        /// Un raccourci écrit dans la disposition, par exemple « Ctrl+F » : les mots avant la
        /// dernière « + » sont les modificateurs (Ctrl, Alt, Maj, Win), le dernier est la touche.
        /// </summary>
        private void Raccourci(string raccourci)
        {
            string[] morceaux = raccourci.Split('+');
            List<ushort> mods = new List<ushort>();
            for (int i = 0; i < morceaux.Length - 1; i++)
            {
                switch (morceaux[i].Trim().ToLowerInvariant())
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
                        App.Log("Clavier : modificateur inconnu dans le raccourci « " + raccourci + " »");
                        return;
                }
            }

            ushort code;
            bool etendue;
            if (!CodeTouche(morceaux[morceaux.Length - 1].Trim(), out code, out etendue))
            {
                App.Log("Clavier : touche inconnue dans le raccourci « " + raccourci + " »");
                return;
            }
            InjecteurClavier.TaperTouche(code, etendue, mods);
            OublierPosition();
        }

        /// <summary>Effacer une lettre : Retour arrière (avec Ctrl, Alt, Maj s'ils sont allumés).</summary>
        private void EffacerLettre()
        {
            // Avec Ctrl, Retour arrière efface un mot entier dans la plupart des applications :
            // l'aperçu fait de même ; avec Alt ou Win, on ne sait pas ce qui se passe : il se vide.
            if (_alt || _win)
            {
                _apercu.Clear();
            }
            else if (_ctrl)
            {
                RetirerMotApercu();
            }
            else
            {
                Apercu(1, "");
            }
            InjecteurClavier.TaperTouche(NativeMethods.VK_BACK, false, ModificateursActifs(true));
            _ctrl = false;
            _alt = false;
            _win = false;
            _maj = false;
            if (_motEnCours.Length > 0)
            {
                _motEnCours.Length--;
            }
            else
            {
                // On efface l'espace ou la fin du mot d'avant : il n'est plus sûr.
                _motPrecedent = null;
            }
            _fin = _fin.Length > 0 ? _fin.Substring(0, _fin.Length - 1) : "";
        }

        /// <summary>
        /// Effacer le mot : autant de Retour arrière que de lettres dans le mot en cours. Si le
        /// clavier ne connaît pas le mot (juste après une espace, ou après un clic ailleurs), il
        /// envoie Ctrl + Retour arrière, que la plupart des applications comprennent comme
        /// « effacer le mot d'avant ».
        /// </summary>
        private void EffacerMot()
        {
            if (_motEnCours.Length > 0)
            {
                for (int i = 0; i < _motEnCours.Length; i++)
                {
                    InjecteurClavier.TaperTouche(NativeMethods.VK_BACK, false, null);
                }
                Apercu(_motEnCours.Length, "");
                _motEnCours.Clear();
            }
            else
            {
                InjecteurClavier.TaperTouche(NativeMethods.VK_BACK, false, new List<ushort> { NativeMethods.VK_CONTROL });
                _motPrecedent = null;   // c'est le mot d'avant qui vient d'être effacé
                RetirerMotApercu();
            }
            _fin = "";
        }

        /// <summary>Allume ou éteint un modificateur (Maj, VerrMaj, Ctrl, Alt, Win).</summary>
        private void BasculerModificateur(string nom)
        {
            switch (nom.Trim().ToLowerInvariant())
            {
                case "maj":
                    _maj = !_maj;
                    break;
                case "verrmaj":
                    _verrMaj = !_verrMaj;
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
                    App.Log("Clavier : modificateur inconnu « " + nom + " » dans la disposition");
                    break;
            }
        }

        /// <summary>Affiche une autre page du clavier (« retour » = la page d'avant).</summary>
        private void ChangerPage(string nom)
        {
            if (string.Equals(nom, "retour", StringComparison.OrdinalIgnoreCase))
            {
                nom = _pagePrecedente ?? _disposition.PageDepart;
            }
            if (_disposition.Page(nom) == null)
            {
                App.Log("Clavier : page « " + nom + " » introuvable dans la disposition");
                return;
            }
            _pagePrecedente = _page;
            _page = nom;
            Construire();
        }

        /// <summary>
        /// Garde la trace de ce qui vient d'être écrit :
        ///  - le mot en cours : lettres, chiffres et tiret (« peut-être ») ; tout autre caractère
        ///    (espace, ponctuation, apostrophe, fin de ligne) termine le mot ;
        ///  - les deux derniers caractères, pour la majuscule automatique : après « . », « ? »
        ///    ou « ! » suivi d'une espace ou d'une fin de ligne, Maj s'allume tout seul pour la
        ///    lettre suivante ([MajusculeAuto]). On voit Maj allumé : on peut l'éteindre.
        /// </summary>
        private void Suivre(string texte)
        {
            foreach (char c in texte)
            {
                if (char.IsLetterOrDigit(c) || c == '-')
                {
                    _motEnCours.Append(c);
                }
                else
                {
                    // Le mot en cours est terminé (étape 13) : s'il est connu, la prédiction
                    // retient qu'il a été utilisé une fois de plus, et qu'il a suivi le mot
                    // d'avant (apprentissage). Avec une apostrophe, c'est la forme élidée qui
                    // compte : « l' », « qu' ».
                    bool apostrophe = c == '\'' || c == '’';
                    if (_motEnCours.Length > 0)
                    {
                        string mot = _motEnCours.ToString();
                        _motPrecedent = _predicteur.NoterUtilisation(apostrophe ? mot + "'" : mot, _motPrecedent);
                    }
                    // Mot suivant : seuls une espace ou une apostrophe gardent le lien avec le
                    // mot d'avant ; une ponctuation (. , ? ! …) ou une fin de ligne le coupent.
                    if (c != ' ' && !apostrophe)
                    {
                        _motPrecedent = null;
                    }
                    _motEnCours.Clear();
                }
            }
            GarderFin(texte);
        }

        /// <summary>
        /// Retient les deux derniers caractères écrits, et allume Maj tout seul après « . »,
        /// « ? » ou « ! » suivi d'une espace ou d'une fin de ligne ([MajusculeAuto]).
        /// </summary>
        private void GarderFin(string texte)
        {
            _fin += texte;
            if (_fin.Length > 2)
            {
                _fin = _fin.Substring(_fin.Length - 2);
            }

            if (Parametres.Courants.MajusculeAuto && _fin.Length == 2 &&
                (_fin[1] == ' ' || _fin[1] == '\n') && ".?!".IndexOf(_fin[0]) >= 0)
            {
                _maj = true;
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
        private void ChoisirProposition(int numero)
        {
            string tape = _motEnCours.ToString();
            // Maj ou Caps Lock allumé avant un mot suivant : sa 1re lettre en majuscule.
            bool majusculeDemandee = _maj ^ _verrMaj;
            // Les autres modificateurs ne s'appliquent pas à un mot entier : on les éteint.
            _maj = false;
            _ctrl = false;
            _alt = false;
            _win = false;

            if (numero == 1 && _motInconnu)
            {
                _predicteur.Ajouter(tape);
                InjecteurClavier.TaperTexte(" ");
                Apercu(0, " ");
                Suivre(" ");   // termine le mot : il compte déjà une utilisation
                return;
            }

            int indice = numero - 1;
            if (indice < 0 || indice >= _propositions.Count)
            {
                return;   // case vide (ne devrait pas arriver : elle est inactive)
            }

            // (Avec « tape » vide, c'est une proposition de mot suivant : rien à effacer.)
            string mot = FormeAffichee(_propositions[indice], tape, majusculeDemandee);

            // Les premières lettres identiques restent en place.
            int communes = 0;
            while (communes < tape.Length && communes < mot.Length && tape[communes] == mot[communes])
            {
                communes++;
            }
            for (int i = communes; i < tape.Length; i++)
            {
                InjecteurClavier.TaperTouche(NativeMethods.VK_BACK, false, null);
            }

            bool elision = mot.EndsWith("'") || mot.EndsWith("’");
            string ajout = mot.Substring(communes) + (elision ? "" : " ");
            InjecteurClavier.TaperTexte(ajout);
            Apercu(tape.Length - communes, ajout);

            _motEnCours.Clear();
            _motPrecedent = _predicteur.NoterUtilisation(mot, _motPrecedent);
            GarderFin(ajout);
        }

        /// <summary>
        /// Le mot tel qu'il s'affiche dans sa case et s'écrit : avec les majuscules de ce qui a
        /// été tapé (AvecMajuscule) ; pour un mot suivant (rien de tapé), avec une majuscule
        /// au début si Maj ou Caps Lock est allumé.
        /// </summary>
        private static string FormeAffichee(string mot, string tape, bool majuscule)
        {
            if (string.IsNullOrEmpty(tape) && majuscule && !string.IsNullOrEmpty(mot))
            {
                return char.ToUpper(mot[0], CultureInfo.CurrentCulture) + mot.Substring(1);
            }
            return AvecMajuscule(mot, tape);
        }

        /// <summary>
        /// Le mot proposé avec les majuscules de ce qui a été tapé : « Bo » → « Bonjour » (le
        /// mot garde sa majuscule de départ, cahier des charges), « BON » (Caps Lock) →
        /// « BONJOUR ». Sinon, le mot tel qu'il est rangé (« Paris » reste « Paris »).
        /// </summary>
        private static string AvecMajuscule(string mot, string tape)
        {
            if (string.IsNullOrEmpty(mot) || string.IsNullOrEmpty(tape))
            {
                return mot;
            }
            int lettres = 0;
            int majuscules = 0;
            foreach (char c in tape)
            {
                if (char.IsLetter(c))
                {
                    lettres++;
                    if (char.IsUpper(c))
                    {
                        majuscules++;
                    }
                }
            }
            if (lettres >= 2 && majuscules == lettres)
            {
                return mot.ToUpper(CultureInfo.CurrentCulture);
            }
            if (char.IsUpper(tape[0]))
            {
                return char.ToUpper(mot[0], CultureInfo.CurrentCulture) + mot.Substring(1);
            }
            return mot;
        }

        // ───────────────────────── La zone de saisie (étape 14) ─────────────────────────

        /// <summary>
        /// Met à jour l'aperçu de la zone de saisie comme l'application l'a fait : « effaces »
        /// Retour arrière, puis « ajout » écrit. L'aperçu est tenu à jour même quand la zone est
        /// cachée : on la voit juste en l'affichant. Seule la fin est gardée.
        /// </summary>
        private void Apercu(int effaces, string ajout)
        {
            _apercu.Length = Math.Max(0, _apercu.Length - effaces);
            _apercu.Append(ajout);
            if (_apercu.Length > LongueurMaxApercu)
            {
                _apercu.Remove(0, _apercu.Length - LongueurMaxApercu);
            }
        }

        /// <summary>
        /// Comme Ctrl + Retour arrière dans la plupart des applications : retire les espaces de
        /// la fin, puis les lettres du dernier mot.
        /// </summary>
        private void RetirerMotApercu()
        {
            int fin = _apercu.Length;
            while (fin > 0 && char.IsWhiteSpace(_apercu[fin - 1]))
            {
                fin--;
            }
            while (fin > 0 && !char.IsWhiteSpace(_apercu[fin - 1]))
            {
                fin--;
            }
            _apercu.Length = fin;
        }

        /// <summary>
        /// Écrit l'aperçu dans la zone de saisie (si elle est affichée). Une fin de ligne est
        /// montrée par « ↵ ». Quand le texte est trop long, seule la fin est montrée, précédée
        /// de « … ». Zone vide : un petit texte gris explique à quoi elle sert.
        /// </summary>
        private void AfficherApercu()
        {
            if (_texteApercu == null)
            {
                return;
            }
            if (_apercu.Length == 0)
            {
                _texteApercu.Text = "Les derniers mots tapés s'affichent ici";
                _texteApercu.Foreground = TexteZoneSaisieVide;
                return;
            }
            string texte = _apercu.ToString().Replace("\n", " ↵ ");
            _texteApercu.Foreground = TexteZoneSaisie;
            _texteApercu.Text = FinQuiTient(texte);
        }

        /// <summary>
        /// La plus longue fin de « texte » qui tient dans la largeur de la zone (précédée de
        /// « … » si elle est coupée). On cherche le point de coupure par dichotomie : on mesure
        /// une dizaine de fois au lieu d'essayer chaque caractère.
        /// </summary>
        private string FinQuiTient(string texte)
        {
            if (LargeurTexte(texte) <= _largeurApercuWpf)
            {
                return texte;
            }
            // On cherche le plus petit « debut » pour lequel « … » + la fin depuis debut tient.
            int bas = 1;
            int haut = texte.Length;   // texte vide : tient toujours
            while (bas < haut)
            {
                int milieu = bas + (haut - bas) / 2;
                if (LargeurTexte("…" + texte.Substring(milieu)) <= _largeurApercuWpf)
                {
                    haut = milieu;     // ça tient : on peut peut-être garder plus de texte
                }
                else
                {
                    bas = milieu + 1;  // trop long : il faut couper plus loin
                }
            }
            return "…" + texte.Substring(bas);
        }

        /// <summary>Largeur d'un texte dans la police de la zone de saisie, en unités WPF.</summary>
        private double LargeurTexte(string texte)
        {
            FormattedText mesure = new FormattedText(
                texte,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(_texteApercu.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                _texteApercu.FontSize,
                TexteZoneSaisie,
                VisualTreeHelper.GetDpi(_calque).PixelsPerDip);
            return mesure.WidthIncludingTrailingWhitespace;
        }

        // ───────────────────────── Petits outils ─────────────────────────

        private static Brush Figer(Color couleur)
        {
            SolidColorBrush pinceau = new SolidColorBrush(couleur);
            pinceau.Freeze();
            return pinceau;
        }

        /// <summary>Les codes des modificateurs allumés (Maj seulement si avecMaj).</summary>
        private List<ushort> ModificateursActifs(bool avecMaj)
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
            if (avecMaj && _maj)
            {
                mods.Add(NativeMethods.VK_SHIFT);
            }
            return mods;
        }

        private static bool ContientUneLettre(string texte)
        {
            foreach (char c in texte)
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
        private static bool CodeTouche(string nom, out ushort code, out bool etendue)
        {
            etendue = false;
            code = 0;
            switch (nom.Trim().ToLowerInvariant())
            {
                case "tab": code = 0x09; return true;
                case "entree": code = 0x0D; return true;
                case "echap": code = 0x1B; return true;
                case "espace": code = 0x20; return true;
                case "retourarriere": code = NativeMethods.VK_BACK; return true;
                case "pagehaut": code = 0x21; etendue = true; return true;
                case "pagebas": code = 0x22; etendue = true; return true;
                case "fin": code = 0x23; etendue = true; return true;
                case "debut": code = 0x24; etendue = true; return true;
                case "gauche": code = 0x25; etendue = true; return true;
                case "haut": code = 0x26; etendue = true; return true;
                case "droite": code = 0x27; etendue = true; return true;
                case "bas": code = 0x28; etendue = true; return true;
                case "inser": code = 0x2D; etendue = true; return true;
                case "suppr": code = 0x2E; etendue = true; return true;
                // La touche Windows seule (02/11/2026) : ouvre ou ferme le menu Démarrer.
                // C'est une touche « étendue » pour Windows.
                case "windows": code = NativeMethods.VK_LWIN; etendue = true; return true;
            }

            string n = nom.Trim().ToUpperInvariant();
            // F1 à F24 : codes 0x70 à 0x87.
            int numero;
            if (n.Length >= 2 && n[0] == 'F' && int.TryParse(n.Substring(1), out numero) && numero >= 1 && numero <= 24)
            {
                code = (ushort)(0x70 + numero - 1);
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
        private void Construire()
        {
            _cibles.Clear();
            _touches.Clear();
            _fermer = null;
            if (!Ouvert || _disposition == null || !_calque.Pret)
            {
                Masquer();
                return;
            }
            PageClavier page = _disposition.Page(_page) ?? _disposition.Page(_disposition.PageDepart);

            Parametres p = Parametres.Courants;
            double largeur = Ecran.LargeurPixels;
            double hauteur = Ecran.HauteurPixels;

            // Taille (01/11/2026) : à 75 % ou 50 %, largeur ET hauteur sont réduites ensemble,
            // les touches gardent donc leur forme ; le clavier est centré horizontalement.
            double taillePourcent = p.TailleClavier / 100.0;
            double largeurClavier = largeur * taillePourcent;
            double hauteurTouches = hauteur * p.HauteurClavier / 100.0 * taillePourcent;

            // La grille des touches.
            double marge = Ecran.Px(Marge4K);
            double largeurCase = (largeurClavier - 2 * marge) / _disposition.Colonnes;
            double hauteurCase = (hauteurTouches - 2 * marge) / _disposition.Lignes;
            double ecart = Math.Min(Ecran.Px(EcartTouches4K), Math.Min(largeurCase, hauteurCase) * 0.15);

            // Étape 14 : la zone de saisie ajoute une bande au-dessus des touches. Le clavier
            // grandit de sa hauteur : les touches gardent leur taille.
            double hauteurZone = p.ZoneSaisie ? Math.Round(hauteurCase * ProportionZoneSaisie) : 0;
            double hauteurClavier = hauteurTouches + hauteurZone;

            _gauche = (largeur - largeurClavier) / 2;
            _droite = _gauche + largeurClavier;
            if (p.ClavierEnHaut)
            {
                // En haut de l'écran (bouton « Haut / bas » de la barre du clavier).
                _haut = 0;
                _bas = hauteurClavier;
            }
            else
            {
                // En bas, au-dessus de la barre de menu du clavier si elle est ouverte.
                _bas = Math.Max(hauteurClavier, hauteur - _reserveBas);
                _haut = _bas - hauteurClavier;
            }

            _calque.PreparerSurfaceClavier();
            Canvas surface = _calque.SurfaceClavier;

            // ── Le fond : un rectangle presque noir, comme sur les captures de l'utilisateur.
            Border fond = new Border();
            fond.Background = new SolidColorBrush(Color.FromArgb(0xF5, 0x12, 0x12, 0x12));
            fond.Width = _calque.LongueurVersWpf(largeurClavier);
            fond.Height = _calque.LongueurVersWpf(hauteurClavier);
            Point coin = _calque.VersWpf(_gauche, _haut);
            Canvas.SetLeft(fond, coin.X);
            Canvas.SetTop(fond, coin.Y);
            surface.Children.Add(fond);

            // ── La zone de saisie (étape 14) : une bande sombre, sur toute la largeur des
            //    touches, avec le texte aligné à gauche. Ce n'est pas une cible du regard : le
            //    regard dessus ne fait rien (le curseur de la souris reste immobile).
            _texteApercu = null;
            if (hauteurZone > 0)
            {
                Border zone = new Border();
                zone.Background = FondZoneSaisie;
                zone.CornerRadius = new CornerRadius(Ecran.Unites(4));
                double largeurZone = largeurClavier - 2 * marge - ecart;
                zone.Width = _calque.LongueurVersWpf(largeurZone);
                zone.Height = _calque.LongueurVersWpf(hauteurZone - ecart);
                double retrait = _calque.LongueurVersWpf(hauteurZone * 0.25);
                zone.Padding = new Thickness(retrait, 0, retrait, 0);

                _texteApercu = new TextBlock();
                _texteApercu.FontFamily = new FontFamily("Segoe UI, Segoe UI Symbol");
                _texteApercu.FontSize = _calque.LongueurVersWpf(hauteurZone * 0.45);
                _texteApercu.TextWrapping = TextWrapping.NoWrap;
                _texteApercu.VerticalAlignment = VerticalAlignment.Center;
                _texteApercu.HorizontalAlignment = HorizontalAlignment.Left;
                zone.Child = _texteApercu;
                _largeurApercuWpf = Math.Max(0, zone.Width - 2 * retrait);

                Point coinZone = _calque.VersWpf(_gauche + marge + ecart / 2, _haut + marge + ecart / 2);
                Canvas.SetLeft(zone, coinZone.X);
                Canvas.SetTop(zone, coinZone.Y);
                surface.Children.Add(zone);
            }

            // ── Les touches, sous la zone de saisie si elle est affichée.
            double hautTouches = _haut + hauteurZone;
            foreach (ToucheDisposition d in page.Touches)
            {
                ToucheClavier touche = new ToucheClavier(d);
                touche.Placer(_calque, surface,
                              _gauche + marge + d.Col * largeurCase,
                              hautTouches + marge + d.Ligne * hauteurCase,
                              d.LargeurCases * largeurCase,
                              hauteurCase,
                              ecart);
                _touches.Add(touche);
                _cibles.Add(touche);
            }

            // ── Le bouton Fermer : à droite, AU-DESSUS du clavier quand il est en bas, EN
            //    DESSOUS quand il est en haut (son libellé est alors sous le bouton). Il reste
            //    sur l'écran même si le clavier est très haut. (E711 = croix « annuler ».)
            double taille = p.TailleIconesPx;
            double ecartFermer = Ecran.Px(EcartFermer4K);
            double yFermer = p.ClavierEnHaut
                ? _bas + ecartFermer + taille / 2
                : _haut - ecartFermer - taille / 2;
            yFermer = Math.Max(taille / 2 + marge, Math.Min(hauteur - taille / 2 - marge, yFermer));
            _fermer = new BoutonRegard("", null, "Fermer le clavier");
            _fermer.FacteurZone = FacteurZoneFermer;
            _fermer.Placer(_calque, surface,
                           _droite - marge - taille / 2,
                           yFermer,
                           taille, !p.ClavierEnHaut);
            _cibles.Add(_fermer);

            // Les touches ont changé : celle qui serait sous le regard ne s'active pas tant
            // que le regard ne l'a pas quittée (par exemple après un changement de page).
            _viseur.ReinitialiserEtBloquer();
            MettreAJourEtat();

            _calque.AfficherSurfaceClavier();
            _visible = true;
        }

        /// <summary>
        /// Met à jour les libellés (majuscules), les bords verts des modificateurs et, depuis
        /// l'étape 13, les mots des cases de proposition (recalculés d'après le mot en cours :
        /// moins d'une milliseconde).
        /// </summary>
        private void MettreAJourEtat()
        {
            string tape = _motEnCours.ToString();
            _propositions = _predicteur.Proposer(tape, _motPrecedent, out _motInconnu);
            AfficherApercu();

            bool majuscules = _maj ^ _verrMaj;
            foreach (ToucheClavier touche in _touches)
            {
                touche.AfficherMajuscules(majuscules);
                string m = touche.Description.Modificateur;
                if (m != null)
                {
                    touche.DefinirAllume(EstAllume(m));
                }

                int? numero = touche.Description.Prediction;
                if (numero.HasValue)
                {
                    int indice = numero.Value - 1;
                    if (_motInconnu && indice == 0)
                    {
                        touche.AfficherProposition("+ " + tape, true);
                    }
                    else if (!_motInconnu && indice >= 0 && indice < _propositions.Count)
                    {
                        touche.AfficherProposition(FormeAffichee(_propositions[indice], tape, _maj ^ _verrMaj), false);
                    }
                    else
                    {
                        touche.AfficherProposition(null, false);
                    }
                }
            }
        }

        private bool EstAllume(string modificateur)
        {
            switch (modificateur.Trim().ToLowerInvariant())
            {
                case "maj":
                    return _maj;
                case "verrmaj":
                    return _verrMaj;
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
        private void RedessinerSansSurvol()
        {
            foreach (ICibleRegard cible in _cibles)
            {
                cible.Dessiner(0, false, false);
            }
        }
    }
}
