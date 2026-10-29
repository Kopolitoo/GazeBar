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
    ///     (ou « Non » / « Oui ») ; la valeur par défaut est rappelée sous la valeur ;
    ///   - à droite : « Défaut » en haut (remet les valeurs de l'onglet affiché, après
    ///     confirmation) et « Retour » au milieu du bord (retour au mode mouvement).
    ///
    /// Étape 10 : un neuvième onglet, « Icônes », avec un seul gros bouton « Configurer les
    /// icônes » qui demande au mode d'ouvrir l'écran de configuration (ModeConfigIcones).
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
    internal sealed class FenetreParametres : Window
    {
        public const double LargeurPage = 1920;
        public const double HauteurPage = 1080;

        // ── Mise en page (en unités de la page 1920 × 1080).
        private const double OngletGauche = 60;
        private const double OngletLargeur = 340;
        private const double OngletHauteurMax = 140;   // hauteur d'un onglet quand il y a la place
        private const double OngletEcart = 20;         // espace entre deux onglets
        private const double OngletsHaut = 60;         // haut du premier onglet
        private const double OngletsBas = 1040;        // le dernier onglet s'arrête avant cette ligne
        private const double ContenuGauche = 460;
        private const double LigneHaut = 92;
        private const double LignePas = 184;           // d'une ligne de réglage à la suivante
        private const double LigneHauteur = 160;
        private const double LibelleLargeur = 520;
        private const double Ecart = 18;
        private const double BoutonCote = 120;         // « − », « + », hauteur des boutons
        private const double ValeurLargeur = 290;
        private const double ChoixLargeur = 260;       // « Non », « Oui »
        private const double DroiteGauche = 1700;      // colonne de droite (Défaut, Retour)
        private const double DroiteCote = 160;
        private const double DiametrePointRegard = 50;

        // ── Couleurs.
        private static readonly Brush Blanc = Figer(new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF4)));
        private static readonly Brush Doux = Figer(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAD)));
        private static readonly Brush Bord = Figer(new SolidColorBrush(Color.FromRgb(0x4A, 0x52, 0x5C)));
        private static readonly Brush FondValeur = Figer(new SolidColorBrush(Color.FromRgb(0x1A, 0x1D, 0x22)));

        private const string PoliceIcones = "Segoe Fluent Icons, Segoe MDL2 Assets";

        // La page, son contenu (refait à chaque changement d'onglet) et le point du regard.
        private readonly Canvas _page;
        private readonly Canvas _contenu;
        private readonly Ellipse _pointRegard;

        // Les cibles du regard, dans l'ordre où elles ont été créées.
        private readonly List<CibleParametre> _cibles = new List<CibleParametre>();

        // Pour chaque réglage numérique affiché : son cadre de valeur (pour la saisie au clavier).
        private readonly Dictionary<Reglage, CadreValeur> _cadres = new Dictionary<Reglage, CadreValeur>();

        private int _onglet;
        private bool _confirmation;

        // La saisie au clavier en cours (null = aucune).
        private Reglage _reglageEdite;
        private TextBox _saisie;

        // Vrai quand c'est le mode qui ferme la fenêtre (et non Alt+F4, par exemple).
        private bool _fermetureDemandee;

        /// <summary>
        /// Crée la fenêtre, ouverte sur l'onglet numéro « onglet » (0 = le premier). Au retour
        /// de l'écran de configuration des icônes, elle s'ouvre sur l'onglet « Icônes ».
        /// </summary>
        public FenetreParametres(int onglet = 0)
        {
            _onglet = Math.Max(0, Math.Min(onglet, CatalogueParametres.Onglets.Count - 1));

            Title = "GazeBar — Paramètres";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            Background = Brushes.Black;

            _page = new Canvas();
            _page.Width = LargeurPage;
            _page.Height = HauteurPage;
            _page.Background = Brushes.Black;

            _contenu = new Canvas();
            _page.Children.Add(_contenu);

            // Le petit cercle gris du regard, par-dessus tout, et transparent à la souris.
            _pointRegard = new Ellipse();
            _pointRegard.Width = DiametrePointRegard;
            _pointRegard.Height = DiametrePointRegard;
            _pointRegard.Stroke = Brushes.LightGray;
            _pointRegard.StrokeThickness = 4;
            _pointRegard.IsHitTestVisible = false;
            _pointRegard.Visibility = Visibility.Collapsed;
            _page.Children.Add(_pointRegard);

            // La Viewbox met la page de 1920 × 1080 à l'échelle de l'écran, sans la déformer.
            Viewbox echelle = new Viewbox();
            echelle.Stretch = Stretch.Uniform;
            echelle.Child = _page;
            Content = echelle;

            Construire();
        }

        // ───────────────────────── Ce que le mode utilise ─────────────────────────

        /// <summary>Demande de retour au mode mouvement (bouton « Retour », ou fermeture par Windows).</summary>
        public event Action RetourDemande;

        /// <summary>Bouton « Configurer les icônes » de l'onglet « Icônes » (étape 10).</summary>
        public event Action ConfigurationIconesDemandee;

        /// <summary>
        /// Les cibles du regard. La même liste est gardée d'un bout à l'autre : elle est
        /// seulement vidée et remplie de nouveau quand la page est reconstruite.
        /// </summary>
        public IReadOnlyList<ICibleRegard> Cibles
        {
            get { return _cibles; }
        }

        /// <summary>
        /// Augmente à chaque reconstruction de la page (nouvel onglet, confirmation…). Le mode
        /// s'en sert pour savoir que les cibles ont changé. « − » et « + » ne reconstruisent
        /// pas la page (seul le texte de la valeur change) : la répétition peut continuer.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>Vrai si l'activation de cette cible peut se répéter (« − » et « + »).</summary>
        public bool CibleRepetable(int numero)
        {
            return _cibles[numero].Repetition;
        }

        /// <summary>Active la cible indiquée (le regard s'y est posé assez longtemps).</summary>
        public void Activer(int numero)
        {
            _cibles[numero].Action();
        }

        /// <summary>
        /// Convertit un point de l'écran (en pixels réels, comme le regard) en point de la page
        /// 1920 × 1080. PointFromScreen tient compte de la mise à l'échelle de Windows ET de
        /// celle de la Viewbox. Avant l'affichage de la fenêtre, renvoie un point très loin.
        /// </summary>
        public Point VersPage(double xEcran, double yEcran)
        {
            if (PresentationSource.FromVisual(_page) == null)
            {
                return new Point(-10000, -10000);
            }
            return _page.PointFromScreen(new Point(xEcran, yEcran));
        }

        /// <summary>Place le petit cercle du regard (coordonnées de la page).</summary>
        public void PlacerPointRegard(Point point)
        {
            Canvas.SetLeft(_pointRegard, point.X - DiametrePointRegard / 2);
            Canvas.SetTop(_pointRegard, point.Y - DiametrePointRegard / 2);
            _pointRegard.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Touche Échap pendant une saisie au clavier : la saisie est annulée (la valeur ne
        /// change pas) et on reste dans les paramètres. Renvoie vrai si une saisie était en
        /// cours ; sinon, Échap garde son rôle habituel (retour au mode mouvement).
        /// </summary>
        public bool AnnulerSaisie()
        {
            if (_reglageEdite == null)
            {
                return false;
            }
            Reglage reglage = _reglageEdite;
            _reglageEdite = null;   // d'abord, pour que la perte du focus ne valide pas la saisie
            RetablirAffichage(reglage);
            App.Log("Paramètres : saisie annulée (" + reglage.Nom + ")");
            return true;
        }

        /// <summary>Ferme la fenêtre (appelée par le mode quand on le quitte).</summary>
        public void Fermer()
        {
            // Une saisie en cours est validée, comme si l'aidant avait appuyé sur Entrée.
            ValiderSaisie();
            _fermetureDemandee = true;
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
            CouvrirEcran();
        }

        /// <summary>
        /// L'écran a changé pendant que la fenêtre est ouverte (étape 11, appelée par le mode) :
        /// elle reprend la taille de l'écran, et la page est reconstruite (une saisie en cours
        /// est validée).
        /// </summary>
        public void AdapterEcran()
        {
            CouvrirEcran();
            Construire();
        }

        /// <summary>Donne à la fenêtre la taille exacte de l'écran principal.</summary>
        private void CouvrirEcran()
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null || source.CompositionTarget == null)
            {
                return;
            }
            double largeurPixels;
            double hauteurPixels;
            NativeMethods.TailleEcranPrincipal(out largeurPixels, out hauteurPixels);
            Point taille = source.CompositionTarget.TransformFromDevice.Transform(new Point(largeurPixels, hauteurPixels));
            Left = 0;
            Top = 0;
            Width = taille.X;
            Height = taille.Y;
        }

        /// <summary>
        /// Fermeture demandée par Windows (Alt+F4…) et non par le mode : on l'annule, et on
        /// demande plutôt le retour au mode mouvement, qui fermera la fenêtre proprement.
        /// « BeginInvoke » : un peu plus tard, car on ne peut pas fermer une fenêtre pendant
        /// qu'elle est en train de se fermer.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_fermetureDemandee)
            {
                e.Cancel = true;
                Dispatcher.BeginInvoke(new Action(DemanderRetour));
            }
            base.OnClosing(e);
        }

        private void DemanderRetour()
        {
            Action abonnes = RetourDemande;
            if (abonnes != null)
            {
                abonnes();
            }
        }

        // ───────────────────────── Construction de la page ─────────────────────────

        private static Parametres P
        {
            get { return Parametres.Courants; }
        }

        /// <summary>(Re)construit toute la page pour l'onglet choisi.</summary>
        private void Construire()
        {
            // Une saisie en cours est validée avant de tout refaire.
            ValiderSaisie();

            _contenu.Children.Clear();
            _cibles.Clear();
            _cadres.Clear();

            Onglet onglet = CatalogueParametres.Onglets[_onglet];

            // ── Le titre, en haut.
            TextBlock titre = Texte("Paramètres — " + onglet.Nom, 30, Doux, false);
            Placer(titre, ContenuGauche, 26);

            // ── Les onglets, à gauche. Six onglets tiennent avec leur hauteur maximale (140) ;
            //    à partir de sept (étape 8), ils se resserrent pour rester sur la page :
            //    7 onglets → environ 123 de haut, 8 onglets → 105.
            int nombreOnglets = CatalogueParametres.Onglets.Count;
            double pas = Math.Min(OngletHauteurMax + OngletEcart,
                                  (OngletsBas - OngletsHaut + OngletEcart) / Math.Max(1, nombreOnglets));
            for (int i = 0; i < CatalogueParametres.Onglets.Count; i++)
            {
                Onglet o = CatalogueParametres.Onglets[i];
                StackPanel ligne = new StackPanel();
                ligne.Orientation = Orientation.Horizontal;
                ligne.VerticalAlignment = VerticalAlignment.Center;
                ligne.Margin = new Thickness(28, 0, 0, 0);
                TextBlock symbole = Texte(o.Symbole, 46, i == _onglet ? CibleParametre.Accent : Doux, false);
                symbole.FontFamily = new FontFamily(PoliceIcones);
                symbole.Width = 70;
                symbole.VerticalAlignment = VerticalAlignment.Center;
                ligne.Children.Add(symbole);
                TextBlock nom = Texte(o.Nom, 36, Blanc, true);
                nom.VerticalAlignment = VerticalAlignment.Center;
                ligne.Children.Add(nom);

                int numero = i;   // copie pour la fonction ci-dessous (sinon elle verrait le dernier i)
                AjouterCible(OngletGauche, OngletsHaut + i * pas, OngletLargeur, pas - OngletEcart, ligne,
                             () => ChoisirOnglet(numero), false, i == _onglet);
            }

            // ── Le contenu : les réglages, ou la confirmation de « Défaut ».
            if (_confirmation)
            {
                ConstruireConfirmation(onglet);
            }
            else
            {
                for (int i = 0; i < onglet.Reglages.Count; i++)
                {
                    ConstruireLigne(onglet.Reglages[i], LigneHaut + i * LignePas);
                }
                if (onglet.Nom == "Général")
                {
                    ConstruireInfoFichier(LigneHaut + onglet.Reglages.Count * LignePas);
                }
                if (onglet.Nom == NomOngletAffichage)
                {
                    ConstruireInfoEcran(LigneHaut + onglet.Reglages.Count * LignePas);
                }
                if (onglet.Nom == CatalogueParametres.NomOngletIcones)
                {
                    ConstruireOngletIcones();
                }
            }

            // ── La colonne de droite : « Défaut » en haut, « Retour » au milieu du bord.
            //    E72C = flèches circulaires (remettre), E72B = flèche de retour.
            AjouterCible(DroiteGauche, 60, DroiteCote, DroiteCote, SymboleEtMot("", "Défaut"),
                         DemanderDefaut, false, false);
            AjouterCible(DroiteGauche, (HauteurPage - DroiteCote) / 2, DroiteCote, DroiteCote,
                         SymboleEtMot("", "Retour"), DemanderRetour, false, false);

            Version++;
        }

        /// <summary>Une ligne de réglage : nom et explication, puis les boutons.</summary>
        private void ConstruireLigne(Reglage reglage, double haut)
        {
            // Nom et explication, centrés verticalement dans la hauteur de la ligne.
            StackPanel textes = new StackPanel();
            textes.Width = LibelleLargeur;
            textes.Height = LigneHauteur;
            StackPanel centre = new StackPanel();
            centre.VerticalAlignment = VerticalAlignment.Center;
            centre.Children.Add(Texte(reglage.Nom, 36, Blanc, true));
            TextBlock explication = Texte(reglage.Explication, 23, Doux, false);
            explication.TextWrapping = TextWrapping.Wrap;
            explication.Margin = new Thickness(0, 6, 0, 0);
            centre.Children.Add(explication);
            Grid cadreTextes = new Grid();
            cadreTextes.Width = LibelleLargeur;
            cadreTextes.Height = LigneHauteur;
            cadreTextes.Children.Add(centre);
            Placer(cadreTextes, ContenuGauche, haut);

            double x = ContenuGauche + LibelleLargeur + Ecart;
            double y = haut + (LigneHauteur - BoutonCote) / 2;

            if (reglage.OuiNon)
            {
                bool valeur = reglage.LireOuiNon(P);
                AjouterCible(x, y, ChoixLargeur, BoutonCote, Mot(reglage.MotFaux, 42), () => ChangerOuiNon(reglage, false), false, !valeur);
                AjouterCible(x + ChoixLargeur + Ecart, y, ChoixLargeur, BoutonCote, Mot(reglage.MotVrai, 42), () => ChangerOuiNon(reglage, true), false, valeur);
                return;
            }

            // « − » (U+2212, le vrai signe moins), la valeur, « + ». Les deux boutons se répètent.
            AjouterCible(x, y, BoutonCote, BoutonCote, Mot("−", 72), () => ChangerNombre(reglage, -1), true, false);
            AjouterCible(x + BoutonCote + Ecart + ValeurLargeur + Ecart, y, BoutonCote, BoutonCote, Mot("+", 72),
                         () => ChangerNombre(reglage, +1), true, false);

            // La valeur : un cadre qui n'est PAS une cible du regard. L'aidant clique dessus
            // pour taper une valeur au clavier.
            CadreValeur cadre = new CadreValeur();
            cadre.Valeur = Texte(reglage.Texte(P), 50, Blanc, true);
            cadre.Valeur.HorizontalAlignment = HorizontalAlignment.Center;
            Parametres defaut = new Parametres();
            TextBlock rappel = Texte("défaut " + reglage.Texte(defaut), 20, Doux, false);
            rappel.HorizontalAlignment = HorizontalAlignment.Center;
            StackPanel affichage = new StackPanel();
            affichage.VerticalAlignment = VerticalAlignment.Center;
            affichage.Children.Add(cadre.Valeur);
            affichage.Children.Add(rappel);
            cadre.Affichage = affichage;

            cadre.Bord = new Border();
            cadre.Bord.Width = ValeurLargeur;
            cadre.Bord.Height = BoutonCote;
            cadre.Bord.CornerRadius = new CornerRadius(14);
            cadre.Bord.BorderThickness = new Thickness(2);
            cadre.Bord.BorderBrush = Bord;
            cadre.Bord.Background = FondValeur;
            cadre.Bord.Cursor = Cursors.IBeam;
            cadre.Bord.ToolTip = "Cliquer pour taper une valeur (Entrée valide, Échap annule)";
            cadre.Bord.Child = affichage;
            cadre.Bord.MouseLeftButtonUp += (s, e) =>
            {
                CommencerSaisie(reglage);
                e.Handled = true;
            };
            Placer(cadre.Bord, x + BoutonCote + Ecart, y);
            _cadres[reglage] = cadre;
        }

        /// <summary>Le nom de l'onglet « Affichage », sous lequel l'écran détecté est rappelé (étape 11).</summary>
        private const string NomOngletAffichage = "Affichage";

        /// <summary>
        /// Onglet « Affichage » (étape 11) : l'écran détecté par GazeBar (résolution, mise à
        /// l'échelle et taille du texte de Windows), pour vérifier que le changement a été vu.
        /// </summary>
        private void ConstruireInfoEcran(double haut)
        {
            StackPanel info = new StackPanel();
            info.Children.Add(Texte("Écran détecté", 36, Blanc, true));
            TextBlock description = Texte(Ecran.Description(), 24, Doux, false);
            description.Margin = new Thickness(0, 6, 0, 0);
            info.Children.Add(description);
            info.Children.Add(Texte("Tailles en % de la hauteur de l'écran.", 23, Doux, false));
            Placer(info, ContenuGauche, haut + 30);
        }

        /// <summary>Onglet Général : rappel de l'endroit où les réglages sont enregistrés.</summary>
        private void ConstruireInfoFichier(double haut)
        {
            StackPanel info = new StackPanel();
            info.Children.Add(Texte("Fichier des réglages", 36, Blanc, true));
            TextBlock chemin = Texte(Parametres.CheminFichier, 24, Doux, false);
            chemin.FontFamily = new FontFamily("Consolas");
            chemin.TextWrapping = TextWrapping.Wrap;
            chemin.Width = 1140;
            chemin.Margin = new Thickness(0, 6, 0, 0);
            info.Children.Add(chemin);
            info.Children.Add(Texte("Enregistré à chaque changement.", 23, Doux, false));
            Placer(info, ContenuGauche, haut + 30);
        }

        /// <summary>
        /// Onglet « Icônes » (étape 10) : une explication, puis un gros bouton qui ouvre l'écran
        /// de configuration des icônes du choix d'action. E71D = grille de carrés.
        /// </summary>
        private void ConstruireOngletIcones()
        {
            StackPanel textes = new StackPanel();
            textes.Children.Add(Texte("Icônes du choix d'action", 36, Blanc, true));
            TextBlock explication = Texte(
                "Choisir les actions de la couronne, des modificateurs (bord gauche) et des boutons " +
                "Copier, Couper, Coller (bord haut). Un emplacement peut rester vide.", 23, Doux, false);
            explication.TextWrapping = TextWrapping.Wrap;
            explication.Width = 1140;
            explication.Margin = new Thickness(0, 6, 0, 0);
            textes.Children.Add(explication);
            Placer(textes, ContenuGauche, LigneHaut + 20);

            StackPanel bouton = new StackPanel();
            bouton.Orientation = Orientation.Horizontal;
            bouton.HorizontalAlignment = HorizontalAlignment.Center;
            bouton.VerticalAlignment = VerticalAlignment.Center;
            TextBlock symbole = Texte("\uE71D", 60, Blanc, false);
            symbole.FontFamily = new FontFamily(PoliceIcones);
            symbole.VerticalAlignment = VerticalAlignment.Center;
            symbole.Margin = new Thickness(0, 0, 30, 0);
            bouton.Children.Add(symbole);
            TextBlock mot = Texte("Configurer les icônes", 42, Blanc, true);
            mot.VerticalAlignment = VerticalAlignment.Center;
            bouton.Children.Add(mot);

            AjouterCible(ContenuGauche + 170, 420, 800, 180, bouton, DemanderConfigurationIcones, false, false);
        }

        private void DemanderConfigurationIcones()
        {
            ValiderSaisie();
            Action abonnes = ConfigurationIconesDemandee;
            if (abonnes != null)
            {
                abonnes();
            }
        }

        /// <summary>
        /// La confirmation de « Défaut ». « Oui » et « Non » sont au milieu de la page, loin du
        /// bouton « Défaut » (en haut à droite) : on ne confirme jamais en continuant
        /// simplement à regarder au même endroit.
        /// </summary>
        private void ConstruireConfirmation(Onglet onglet)
        {
            string texteQuestion = onglet.Nom == CatalogueParametres.NomOngletIcones
                ? "Remettre la disposition d'origine des icônes du choix d'action ?"
                : "Remettre les valeurs par défaut de l'onglet « " + onglet.Nom + " » ?";
            TextBlock question = Texte(texteQuestion, 40, Blanc, true);
            question.TextWrapping = TextWrapping.Wrap;
            question.Width = 1140;
            Placer(question, ContenuGauche, 300);

            AjouterCible(640, 480, 300, BoutonCote, Mot("Oui", 42), () => RemettreDefaut(onglet), false, false);
            AjouterCible(1040, 480, 300, BoutonCote, Mot("Non", 42), AnnulerDefaut, false, false);
        }

        // ───────────────────────── Les actions ─────────────────────────

        private void ChoisirOnglet(int numero)
        {
            _onglet = numero;
            _confirmation = false;
            App.Log("Paramètres : onglet " + CatalogueParametres.Onglets[numero].Nom);
            Construire();
        }

        /// <summary>« − » ou « + » : seul le texte de la valeur change (pas de reconstruction).</summary>
        private void ChangerNombre(Reglage reglage, int sens)
        {
            ValiderSaisie();
            reglage.Changer(P, sens);
            Enregistrer(reglage);
            CadreValeur cadre;
            if (_cadres.TryGetValue(reglage, out cadre))
            {
                cadre.Valeur.Text = reglage.Texte(P);
            }
        }

        private void ChangerOuiNon(Reglage reglage, bool valeur)
        {
            if (reglage.LireOuiNon(P) == valeur)
            {
                return;   // déjà choisi : rien à faire
            }
            reglage.EcrireOuiNon(P, valeur);
            Enregistrer(reglage);
            Construire();   // pour encadrer le nouveau choix
        }

        private void DemanderDefaut()
        {
            _confirmation = true;
            Construire();
        }

        private void AnnulerDefaut()
        {
            _confirmation = false;
            Construire();
        }

        /// <summary>Remet les réglages de l'onglet à leur valeur de départ (lue dans un objet neuf).</summary>
        private void RemettreDefaut(Onglet onglet)
        {
            Parametres defaut = new Parametres();
            if (onglet.Nom == CatalogueParametres.NomOngletIcones)
            {
                P.IconesParDefaut();   // l'onglet « Icônes » n'a pas de réglage, mais une disposition
            }
            foreach (Reglage reglage in onglet.Reglages)
            {
                reglage.Copier(defaut, P);
                if (reglage.ApresChangement != null)
                {
                    reglage.ApresChangement(P);
                }
            }
            P.Enregistrer();
            App.Log("Paramètres : valeurs par défaut de l'onglet " + onglet.Nom);
            _confirmation = false;
            Construire();
        }

        /// <summary>Enregistre le fichier après un changement, et le note dans le journal.</summary>
        private static void Enregistrer(Reglage reglage)
        {
            if (reglage.ApresChangement != null)
            {
                reglage.ApresChangement(P);
            }
            P.Enregistrer();
            App.Log("Paramètres : " + reglage.Nom + " = " + reglage.Texte(P));
        }

        // ───────────────────────── Saisie au clavier (pour l'aidant) ─────────────────────────

        /// <summary>
        /// Remplace l'affichage de la valeur par une zone de texte où l'aidant tape (ou dicte)
        /// une valeur. Entrée valide ; cliquer ailleurs valide aussi ; Échap annule (voir
        /// AnnulerSaisie, appelée par App quand la touche Échap est enfoncée).
        /// </summary>
        private void CommencerSaisie(Reglage reglage)
        {
            if (_reglageEdite == reglage)
            {
                return;
            }
            ValiderSaisie();

            CadreValeur cadre;
            if (!_cadres.TryGetValue(reglage, out cadre))
            {
                return;
            }

            _saisie = new TextBox();
            _saisie.Text = reglage.TexteNombre(reglage.LireNombre(P));
            _saisie.FontSize = 46;
            _saisie.Width = ValeurLargeur - 30;
            _saisie.HorizontalContentAlignment = HorizontalAlignment.Center;
            _saisie.VerticalAlignment = VerticalAlignment.Center;
            _saisie.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ValiderSaisie();
                    e.Handled = true;
                }
            };
            _saisie.LostKeyboardFocus += (s, e) => ValiderSaisie();

            _reglageEdite = reglage;
            cadre.Bord.Child = _saisie;

            // La fenêtre doit avoir le clavier pour que la frappe arrive dans la zone de texte.
            Activate();
            _saisie.Focus();
            Keyboard.Focus(_saisie);
            _saisie.SelectAll();
        }

        /// <summary>Valide la saisie en cours, s'il y en a une.</summary>
        private void ValiderSaisie()
        {
            if (_reglageEdite == null)
            {
                return;
            }
            Reglage reglage = _reglageEdite;
            string texte = _saisie.Text;
            _reglageEdite = null;   // d'abord, pour ne pas valider deux fois (perte du focus)

            double valeur;
            if (Reglage.LireSaisie(texte, out valeur))
            {
                reglage.EcrireNombre(P, valeur);   // ramenée dans ses limites par Corriger
                Enregistrer(reglage);
            }
            else
            {
                App.Log("Paramètres : saisie illisible pour " + reglage.Nom + " : « " + texte + " »");
            }
            RetablirAffichage(reglage);
        }

        /// <summary>Remet l'affichage normal de la valeur à la place de la zone de texte.</summary>
        private void RetablirAffichage(Reglage reglage)
        {
            CadreValeur cadre;
            if (_cadres.TryGetValue(reglage, out cadre))
            {
                cadre.Valeur.Text = reglage.Texte(P);
                cadre.Bord.Child = cadre.Affichage;
            }
            _saisie = null;
        }

        // ───────────────────────── Petits outils ─────────────────────────

        /// <summary>
        /// Crée une cible du regard à cette place, avec ce contenu. Elle répond aussi au clic
        /// de souris (pour l'aidant). repetition : vrai pour « − » et « + » ; choisie : vrai
        /// pour l'onglet affiché ou le choix actuel (Non / Oui), encadré en bleu.
        /// </summary>
        private void AjouterCible(double x, double y, double largeur, double hauteur, UIElement contenu,
                                  Action action, bool repetition, bool choisie)
        {
            CibleParametre cible = new CibleParametre(new Rect(x, y, largeur, hauteur), contenu, action, repetition, choisie);
            cible.Fond.MouseLeftButtonUp += (s, e) =>
            {
                action();
                e.Handled = true;
            };
            Placer(cible.Fond, x, y);
            _cibles.Add(cible);
        }

        private void Placer(UIElement element, double x, double y)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            _contenu.Children.Add(element);
        }

        private static TextBlock Texte(string texte, double taille, Brush couleur, bool gras)
        {
            TextBlock bloc = new TextBlock();
            bloc.Text = texte;
            bloc.FontSize = taille;
            bloc.Foreground = couleur;
            bloc.FontFamily = new FontFamily("Segoe UI");
            if (gras)
            {
                bloc.FontWeight = FontWeights.Bold;
            }
            return bloc;
        }

        /// <summary>Un mot (ou un signe) centré, pour le contenu d'un bouton.</summary>
        private static UIElement Mot(string mot, double taille)
        {
            TextBlock bloc = Texte(mot, taille, Blanc, true);
            bloc.HorizontalAlignment = HorizontalAlignment.Center;
            bloc.VerticalAlignment = VerticalAlignment.Center;
            return bloc;
        }

        /// <summary>Un symbole de la police d'icônes au-dessus d'un mot (boutons Défaut et Retour).</summary>
        private static UIElement SymboleEtMot(string symbole, string mot)
        {
            StackPanel pile = new StackPanel();
            pile.HorizontalAlignment = HorizontalAlignment.Center;
            pile.VerticalAlignment = VerticalAlignment.Center;
            TextBlock icone = Texte(symbole, 60, Blanc, false);
            icone.FontFamily = new FontFamily(PoliceIcones);
            icone.HorizontalAlignment = HorizontalAlignment.Center;
            pile.Children.Add(icone);
            TextBlock texte = Texte(mot, 24, Blanc, false);
            texte.HorizontalAlignment = HorizontalAlignment.Center;
            texte.Margin = new Thickness(0, 6, 0, 0);
            pile.Children.Add(texte);
            return pile;
        }

        private static Brush Figer(SolidColorBrush pinceau)
        {
            pinceau.Freeze();
            return pinceau;
        }

        /// <summary>Le cadre de la valeur d'un réglage numérique, et ce qu'il contient.</summary>
        private sealed class CadreValeur
        {
            public Border Bord;
            public UIElement Affichage;
            public TextBlock Valeur;
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
    internal sealed class CibleParametre : ICibleRegard
    {
        public static readonly Brush Accent = Figer(new SolidColorBrush(Color.FromRgb(0x6F, 0xB4, 0xFF)));
        private static readonly Brush FondNormal = Figer(new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x36)));
        private static readonly Brush FondSurvol = Figer(new SolidColorBrush(Color.FromRgb(0x45, 0x4B, 0x54)));
        private static readonly Brush FondChoisi = Figer(new SolidColorBrush(Color.FromRgb(0x12, 0x26, 0x3D)));
        private static readonly Brush Bord = Figer(new SolidColorBrush(Color.FromRgb(0x4A, 0x52, 0x5C)));
        private static readonly Brush Vert = Figer(new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53)));

        private const double HauteurBarre = 10;

        private readonly Rect _zone;
        private readonly bool _choisie;
        private readonly Rectangle _barre;

        public CibleParametre(Rect zone, UIElement contenu, Action action, bool repetition, bool choisie)
        {
            _zone = zone;
            _choisie = choisie;
            Action = action;
            Repetition = repetition;

            _barre = new Rectangle();
            _barre.Fill = Vert;
            _barre.Height = HauteurBarre;
            _barre.HorizontalAlignment = HorizontalAlignment.Left;
            _barre.VerticalAlignment = VerticalAlignment.Bottom;
            _barre.Width = 0;
            _barre.Visibility = Visibility.Collapsed;
            _barre.IsHitTestVisible = false;

            // Une grille superpose le contenu (au centre) et la barre (en bas).
            Grid grille = new Grid();
            grille.Children.Add(contenu);
            grille.Children.Add(_barre);

            Fond = new Border();
            Fond.Width = zone.Width;
            Fond.Height = zone.Height;
            Fond.CornerRadius = new CornerRadius(14);
            Fond.BorderThickness = new Thickness(choisie ? 4 : 2);
            Fond.BorderBrush = choisie ? Accent : Bord;
            Fond.Background = choisie ? FondChoisi : FondNormal;
            Fond.ClipToBounds = true;
            Fond.Cursor = System.Windows.Input.Cursors.Hand;
            Fond.Child = grille;
        }

        /// <summary>L'élément affiché (à placer sur la page).</summary>
        public Border Fond { get; private set; }

        /// <summary>Ce qui se passe quand la cible s'active.</summary>
        public Action Action { get; private set; }

        /// <summary>Vrai si l'activation se répète tant que le regard reste dessus (« − » et « + »).</summary>
        public bool Repetition { get; private set; }

        public bool Actif
        {
            get { return true; }
        }

        public bool Contient(double x, double y)
        {
            return _zone.Contains(x, y);
        }

        public void Dessiner(double progression, bool survol, bool afficherLibelle)
        {
            if (survol)
            {
                Fond.Background = FondSurvol;
            }
            else
            {
                Fond.Background = _choisie ? FondChoisi : FondNormal;
            }

            if (progression > 0)
            {
                // Largeur intérieure : la largeur de la cible moins ses deux bords.
                double interieur = _zone.Width - 2 * Fond.BorderThickness.Left;
                _barre.Width = Math.Max(0, interieur * Math.Min(progression, 1));
                _barre.Visibility = Visibility.Visible;
            }
            else
            {
                _barre.Visibility = Visibility.Collapsed;
            }
        }

        private static Brush Figer(SolidColorBrush pinceau)
        {
            pinceau.Freeze();
            return pinceau;
        }
    }
}
