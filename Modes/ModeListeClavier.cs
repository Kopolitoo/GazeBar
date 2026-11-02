using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// MODE LISTE DU CLAVIER (étape 14, « clavier-options »).
    ///
    /// Deux listes : « Langue » s'ouvre depuis la barre de menu du clavier au regard, « Mes
    /// mots » depuis l'onglet Général des paramètres (demande de l'utilisateur du 02/11/2026) :
    ///   - LANGUE : les dispositions trouvées dans le dossier Claviers (une par fichier JSON).
    ///     Celle qu'on utilise a un bord vert. En regarder une autre la choisit (retenu dans
    ///     parametres.json), puis on revient au clavier ;
    ///   - MES MOTS : les mots ajoutés au dictionnaire personnel avec la case « + », dans
    ///     l'ordre alphabétique. Chaque mot a une pastille rouge « − » : le regarder le RETIRE
    ///     tout de suite (comme pour retirer une icône à l'étape 10), et on reste dans la liste
    ///     pour pouvoir en retirer d'autres. Les mots de Lexique ne sont jamais concernés.
    ///
    /// Étape 15 (« profils-parametres ») : une troisième liste, qui n'a rien à voir avec le
    /// clavier mais réutilise la même mise en page :
    ///   - PROFILS : les profils de réglages du dossier Documents\GazeBar\Profils, le plus
    ///     récent en premier (voir ProfilsParametres). En regarder un revient aux paramètres,
    ///     onglet Profils, avec la question « Charger le profil … ? » : rien ne change sans
    ///     ce « Oui ». « Retour aux paramètres » revient au même onglet sans rien changer.
    ///
    /// Mise en page : la même que la liste de la configuration des icônes (étape 10) : écran
    /// noir, rectangle aux bords arrondis sur les trois quarts de l'écran, titre en haut,
    /// grille d'au plus 4 colonnes × 3 rangées (12 par page). « Retour » (au clavier pour
    /// Langue, aux paramètres pour Mes mots) est à droite de l'écran, centré verticalement, hors
    /// du rectangle (02/11/2026). En bas du rectangle : « Page précédente » et « Page
    /// suivante » quand la liste ne tient pas sur une page.
    ///
    /// Le clavier est caché pendant ce mode (ce n'est ni le mode mouvement ni le regard fixe)
    /// et réapparaît au retour en mode mouvement. Le menu du bas ne s'ouvre pas ici ; la
    /// touche Échap de l'aidant fait comme « Retour » (App.OnEchap).
    ///
    /// Les mots sont affichés à l'écran mais jamais écrits dans le journal (vie privée).
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal sealed class ModeListeClavier : Mode
    {
        /// <summary>Les deux listes possibles.</summary>
        public enum Sorte
        {
            Langue,
            MesMots,
            Profils
        }

        /// <summary>Proportion de l'écran occupée par la liste (comme à l'étape 10).</summary>
        private const double ProportionListe = 0.75;

        /// <summary>Grille d'une page : 4 colonnes × 3 rangées.</summary>
        private const int Colonnes = 4;
        private const int Rangees = 3;
        private const int ParPage = Colonnes * Rangees;

        // Couleurs (les mêmes que la liste de la configuration des icônes).
        private static readonly Brush FondListe = Figer(new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x26)));
        private static readonly Brush Doux = Figer(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAD)));

        private readonly Sorte _sorte;

        // Les boutons affichés et ce que fait chacun (même ordre).
        private readonly List<BoutonRegard> _boutons = new List<BoutonRegard>();
        private readonly List<Action> _actions = new List<Action>();
        private readonly ViseurBoutons _viseur;

        // Le contenu de la liste : les dispositions (Langue) ou les mots (Mes mots).
        private List<DispositionTrouvee> _dispositions = new List<DispositionTrouvee>();
        private List<string> _mots = new List<string>();
        private List<ProfilTrouve> _profils = new List<ProfilTrouve>();

        // La page affichée (0 = la première).
        private int _page;

        // Un message affiché sous le titre (erreur), ou null.
        private string _message;

        // Vrai tant que l'écran n'a pas pu être construit (calque pas encore prêt).
        private bool _aConstruire;

        // Le petit cercle qui montre le regard (comme dans le choix d'action).
        private Ellipse _cercleRegardFond;
        private Ellipse _cercleRegard;
        private readonly Lisseur _lisse = new Lisseur();
        private bool _cercleRegardPlace;

        public ModeListeClavier(Sorte sorte)
        {
            _sorte = sorte;
            _viseur = new ViseurBoutons(_boutons);
        }

        public override string Nom
        {
            get
            {
                switch (_sorte)
                {
                    case Sorte.Langue: return "Langue du clavier";
                    case Sorte.Profils: return "Profils";
                    default: return "Mes mots";
                }
            }
        }

        public override void Entrer(EtatRegard regard)
        {
            OverlayWindow calque = Gestionnaire.Calque;
            calque.MasquerTout();
            calque.RemettreAuPremierPlan();
            _page = 0;
            _message = null;

            if (_sorte == Sorte.Langue)
            {
                _dispositions = DispositionClavier.Lister();
                // On commence à la page de la disposition utilisée.
                int actuelle = IndiceDispositionActuelle();
                _page = actuelle < 0 ? 0 : actuelle / ParPage;
            }
            else if (_sorte == Sorte.Profils)
            {
                _profils = ProfilsParametres.Lister();
            }
            else
            {
                _mots = Gestionnaire.Clavier.Predicteur.MotsAjoutes();
            }

            _aConstruire = true;
            Construire();
        }

        public override void Actualiser(EtatRegard regard)
        {
            if (_aConstruire)
            {
                Construire();
                return;
            }

            Parametres p = Parametres.Courants;
            // Chaque élément a déjà son nom écrit en dessous : pas de libellé au survol, il se
            // superposerait à ce nom (comme dans la liste de l'étape 10).
            int active = _viseur.Actualiser(regard, p.DelaiActivation, false);
            if (active >= 0)
            {
                _actions[active]();
                return;
            }

            DeplacerCercleRegard(regard, p);
        }

        /// <summary>L'écran a changé (étape 11) : on reste ici, tout est redessiné au prochain tic.</summary>
        public override bool AdapterEcran()
        {
            _aConstruire = true;
            return true;
        }

        public override void Quitter()
        {
            Gestionnaire.Calque.MasquerEcranChoix();
            Gestionnaire.Calque.EcranChoix.Children.Clear();
            _boutons.Clear();
            _actions.Clear();
            _cercleRegard = null;
            _cercleRegardFond = null;
        }

        // ───────────────────────── Les actions ─────────────────────────

        /// <summary>
        /// Bouton « Retour » ou touche Échap (appelée aussi par App) : Langue → le mode
        /// mouvement, où le clavier réapparaît ; Mes mots → les paramètres, onglet Général ;
        /// Profils → les paramètres, onglet Profils.
        /// </summary>
        public void Retour()
        {
            App.Log(Nom + " : retour");
            if (_sorte == Sorte.MesMots)
            {
                Gestionnaire.ChangerMode(new ModeParametres(CatalogueParametres.NumeroOngletGeneral));
            }
            else if (_sorte == Sorte.Profils)
            {
                Gestionnaire.ChangerMode(new ModeParametres(CatalogueParametres.NumeroOngletProfils));
            }
            else
            {
                Gestionnaire.ChangerMode(new ModeMouvement());
            }
        }

        private void ChangerPage(int decalage)
        {
            _page += decalage;
            Construire();
        }

        /// <summary>Langue : une disposition est choisie ; on revient au clavier si elle se lit bien.</summary>
        private void ChoisirDisposition(DispositionTrouvee disposition)
        {
            string message;
            if (Gestionnaire.Clavier.ChangerDisposition(disposition.Fichier, out message))
            {
                Gestionnaire.ChangerMode(new ModeMouvement());
            }
            else
            {
                // Le fichier a pu être modifié depuis l'ouverture de la liste : on le dit.
                _message = message;
                Construire();
            }
        }

        /// <summary>
        /// Profils : un profil est choisi. On revient aux paramètres, onglet Profils, qui
        /// demande confirmation avant de le charger.
        /// </summary>
        private void ChoisirProfil(ProfilTrouve profil)
        {
            App.Log("Profils : profil choisi « " + profil.Nom + " », confirmation demandée");
            Gestionnaire.ChangerMode(new ModeParametres(CatalogueParametres.NumeroOngletProfils, profil));
        }

        /// <summary>Mes mots : le mot est retiré du dictionnaire personnel, la liste est redessinée.</summary>
        private void RetirerMot(string mot)
        {
            Gestionnaire.Clavier.Predicteur.Supprimer(mot);
            _mots.Remove(mot);
            Construire();
        }

        private int IndiceDispositionActuelle()
        {
            string actuelle = Parametres.Courants.DispositionClavier;
            for (int i = 0; i < _dispositions.Count; i++)
            {
                if (string.Equals(_dispositions[i].Fichier, actuelle, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        // ───────────────────────── Construction de l'écran ─────────────────────────

        /// <summary>
        /// (Re)dessine tout l'écran. Toutes les positions sont calculées en pixels d'écran,
        /// puis converties par le calque.
        /// </summary>
        private void Construire()
        {
            OverlayWindow calque = Gestionnaire.Calque;
            if (!calque.Pret)
            {
                _aConstruire = true;   // on réessaiera au prochain tic
                return;
            }
            _aConstruire = false;

            Parametres p = Parametres.Courants;
            double largeur = Gestionnaire.LargeurEcran;
            double hauteur = Gestionnaire.HauteurEcran;
            double taille = p.TailleIconesPx;
            double espace = DispositionChoixAction.Espace;

            calque.PreparerEcranChoix();
            Canvas ecran = calque.EcranChoix;
            _boutons.Clear();
            _actions.Clear();

            // ── Le rectangle aux bords arrondis, centré.
            double l = largeur * ProportionListe;
            double h = hauteur * ProportionListe;
            double gauche = (largeur - l) / 2;
            double haut = (hauteur - h) / 2;
            double centreX = largeur / 2;
            Point coin = calque.VersWpf(gauche, haut);
            Border cadre = new Border();
            cadre.Width = calque.LongueurVersWpf(l);
            cadre.Height = calque.LongueurVersWpf(h);
            cadre.CornerRadius = new CornerRadius(calque.LongueurVersWpf(taille * 0.3));
            cadre.Background = FondListe;
            cadre.BorderBrush = Brushes.White;
            cadre.BorderThickness = new Thickness(Ecran.Unites(2));
            Canvas.SetLeft(cadre, coin.X);
            Canvas.SetTop(cadre, coin.Y);
            ecran.Children.Add(cadre);

            // ── Le nombre d'éléments et de pages.
            int nombre = _sorte == Sorte.Langue ? _dispositions.Count
                       : _sorte == Sorte.Profils ? _profils.Count
                       : _mots.Count;
            int pages = Math.Max(1, (nombre + ParPage - 1) / ParPage);
            // Après une suppression, la dernière page peut être vide : on recule.
            _page = Math.Max(0, Math.Min(_page, pages - 1));

            // ── Le titre, et sous lui une explication (ou le message d'erreur).
            //    Les textes suivent la taille du texte de Windows (étape 11).
            double policeTitre = taille * 0.3 * Ecran.TexteWindows;
            double policeNom = taille * 0.2 * Ecran.TexteWindows;
            string titre = _sorte == Sorte.Profils ? "Charger un profil" : Nom;
            if (pages > 1)
            {
                titre += "  (page " + (_page + 1) + " sur " + pages + ")";
            }
            double yTitre = haut + taille * 0.35;
            AjouterTexteCentre(calque, ecran, titre, centreX, yTitre, l, policeTitre, Brushes.White, true);
            string explication = _message ?? (_sorte == Sorte.Langue
                ? "Regarde une disposition pour la choisir."
                : _sorte == Sorte.Profils
                ? "Regarde un profil pour le choisir (une confirmation suit)."
                : "Regarde un mot pour le retirer de ton dictionnaire.");
            double yExplication = yTitre + policeTitre * 1.5;
            AjouterTexteCentre(calque, ecran, explication, centreX, yExplication, l, policeNom,
                               _message != null ? Brushes.Orange : Doux, false);

            // ── « Retour » (croix E711) : à droite de l'écran, centré verticalement, EN DEHORS
            //    du rectangle de la liste (au milieu de la marge de droite), pour ne pas le viser
            //    en regardant un mot (demande de l'utilisateur du 02/11/2026).
            string nomRetour = _sorte == Sorte.Langue ? "Retour au clavier" : "Retour aux paramètres";
            double margeDroite = largeur - (gauche + l);
            double xRetour = gauche + l + margeDroite / 2;
            double yRetour = hauteur / 2;
            AjouterBouton(calque, ecran, new BoutonRegard("", null, nomRetour), Retour,
                          xRetour, yRetour, taille);
            AjouterTexteCentre(calque, ecran, nomRetour, xRetour, yRetour + taille / 2 + policeNom * 0.4,
                               margeDroite - espace / 2, policeNom, Doux, false);

            // ── La rangée du bas : Page précédente · Page suivante (E72B / E72A = flèches).
            double yBas = haut + h - taille / 2 - policeNom * 2.5;
            double pasBas = taille + 2 * espace;
            if (_page > 0)
            {
                AjouterBouton(calque, ecran, new BoutonRegard("", null, "Page précédente"), () => ChangerPage(-1),
                              centreX - pasBas * 1.5, yBas, taille);
                AjouterTexteCentre(calque, ecran, "Page précédente", centreX - pasBas * 1.5,
                                   yBas + taille / 2 + policeNom * 0.4, pasBas, policeNom, Doux, false);
            }
            if (_page < pages - 1)
            {
                AjouterBouton(calque, ecran, new BoutonRegard("", null, "Page suivante"), () => ChangerPage(1),
                              centreX + pasBas * 1.5, yBas, taille);
                AjouterTexteCentre(calque, ecran, "Page suivante", centreX + pasBas * 1.5,
                                   yBas + taille / 2 + policeNom * 0.4, pasBas, policeNom, Doux, false);
            }

            // ── La grille de la page : entre l'explication et la rangée du bas.
            double hautGrille = yExplication + policeNom * 2.5;
            double basGrille = yBas - taille / 2 - espace / 2;
            int premier = _page * ParPage;
            int dansLaPage = Math.Max(0, Math.Min(ParPage, nombre - premier));

            if (dansLaPage == 0)
            {
                string vide = _sorte == Sorte.Langue
                    ? "Aucune disposition trouvée dans le dossier Claviers."
                    : _sorte == Sorte.Profils
                    ? "Aucun profil pour l'instant. Le bouton « Enregistrer les réglages actuels » en crée un."
                    : "Aucun mot ajouté pour l'instant. Un mot inconnu s'ajoute avec la case « + » du clavier.";
                AjouterTexteCentre(calque, ecran, vide, centreX, (hautGrille + basGrille) / 2 - policeNom,
                                   l * 0.8, policeNom * 1.3, Doux, false);
            }
            else
            {
                int colonnes = Math.Min(dansLaPage, Colonnes);
                int lignes = (dansLaPage + colonnes - 1) / colonnes;
                // Comme à l'étape 10 : chaque élément reçoit une case, et l'icône avec son nom
                // dessous sont centrés dans la case.
                double pasX = l / colonnes;
                double pasY = Math.Max((basGrille - hautGrille) / lignes, taille + policeNom * 2 + espace * 0.5);
                double yPremiere = (hautGrille + basGrille) / 2 - (lignes - 1) * pasY / 2 - policeNom;

                for (int i = 0; i < dansLaPage; i++)
                {
                    int ligne = i / colonnes;
                    int colonne = i % colonnes;
                    int dansLaLigne = Math.Min(colonnes, dansLaPage - ligne * colonnes);
                    double x = centreX + (colonne - (dansLaLigne - 1) / 2.0) * pasX;
                    double y = yPremiere + ligne * pasY;

                    BoutonRegard bouton;
                    Action action;
                    string nom;
                    if (_sorte == Sorte.Langue)
                    {
                        DispositionTrouvee d = _dispositions[premier + i];   // copie pour la fonction ci-dessous
                        // Le bouton montre le code de la langue (« FR ») ; la disposition utilisée a un bord vert.
                        bouton = BoutonRegard.Texte(d.Langue.ToUpperInvariant(), d.Nom);
                        bouton.DefinirAllume(premier + i == IndiceDispositionActuelle());
                        action = () => ChoisirDisposition(d);
                        nom = d.Nom;
                    }
                    else if (_sorte == Sorte.Profils)
                    {
                        ProfilTrouve profil = _profils[premier + i];   // copie pour la fonction ci-dessous
                        // E77B = personne ; E7A7 = flèche « annuler » pour la sauvegarde
                        // automatique (revenir aux réglages d'avant le dernier chargement).
                        bouton = new BoutonRegard(profil.EstSauvegarde ? "\uE7A7" : "\uE77B", null, "Charger ce profil");
                        action = () => ChoisirProfil(profil);
                        nom = profil.Nom;
                    }
                    else
                    {
                        string mot = _mots[premier + i];   // copie pour la fonction ci-dessous
                        // E74D = corbeille, avec la pastille rouge « − » de l'étape 10.
                        bouton = new BoutonRegard("", null, "Retirer ce mot");
                        bouton.DefinirPastilleConfig(false);
                        action = () => RetirerMot(mot);
                        nom = mot;
                    }
                    AjouterBouton(calque, ecran, bouton, action, x, y, taille);
                    AjouterTexteCentre(calque, ecran, nom, x, y + taille / 2 + policeNom * 0.4,
                                       pasX - espace, policeNom, Brushes.White, false);
                }
            }

            // Le petit cercle du regard, ajouté EN DERNIER pour être par-dessus tout.
            _cercleRegardFond = new Ellipse();
            _cercleRegardFond.Stroke = Brushes.Black;
            _cercleRegardFond.StrokeThickness = Ecran.Unites(4);
            _cercleRegardFond.Opacity = 0.6;
            _cercleRegardFond.Visibility = Visibility.Collapsed;
            _cercleRegard = new Ellipse();
            _cercleRegard.Stroke = Brushes.LimeGreen;
            _cercleRegard.StrokeThickness = Ecran.Unites(2);
            _cercleRegard.Visibility = Visibility.Collapsed;
            ecran.Children.Add(_cercleRegardFond);
            ecran.Children.Add(_cercleRegard);

            calque.AfficherEcranChoix();

            // Les boutons viennent d'être remplacés : celui qui est sous le regard ne s'active
            // pas tant que le regard ne l'a pas quitté (sinon, après avoir retiré un mot, on
            // retirerait aussitôt celui qui prend sa place).
            _viseur.ReinitialiserEtBloquer();
        }

        // ───────────────────────── Petits outils ─────────────────────────

        /// <summary>Place un bouton (centré sur x, y en pixels d'écran) que le regard peut activer.</summary>
        private void AjouterBouton(OverlayWindow calque, Canvas ecran, BoutonRegard bouton, Action action,
                                   double x, double y, double taille)
        {
            bouton.FacteurZone = ModeChoixAction.FacteurZone;
            bouton.Placer(calque, ecran, x, y, taille);
            _boutons.Add(bouton);
            _actions.Add(action);
        }

        /// <summary>
        /// Écrit un texte centré horizontalement sur x, son haut en y (pixels d'écran), sur une
        /// largeur de « largeur » pixels (le texte passe à la ligne au-delà).
        /// </summary>
        private static void AjouterTexteCentre(OverlayWindow calque, Canvas ecran, string texte, double x, double y,
                                               double largeur, double policePixels, Brush couleur, bool gras)
        {
            TextBlock bloc = new TextBlock();
            bloc.Text = texte;
            bloc.FontSize = calque.LongueurVersWpf(policePixels);
            bloc.Foreground = couleur;
            bloc.FontFamily = new FontFamily("Segoe UI");
            bloc.TextWrapping = TextWrapping.Wrap;
            if (gras)
            {
                bloc.FontWeight = FontWeights.Bold;
            }
            bloc.Width = calque.LongueurVersWpf(largeur);
            bloc.TextAlignment = TextAlignment.Center;
            Point point = calque.VersWpf(x, y);
            Canvas.SetLeft(bloc, point.X - bloc.Width / 2);
            Canvas.SetTop(bloc, point.Y);
            ecran.Children.Add(bloc);
        }

        /// <summary>
        /// Déplace le petit cercle du regard, comme dans la configuration des icônes : il suit
        /// le regard en douceur, son rayon est la moitié de [RayonCercle]. Regard hors de
        /// l'écran : il glisse le long du bord. Regard perdu : il reste où il est.
        /// </summary>
        private void DeplacerCercleRegard(EtatRegard regard, Parametres p)
        {
            if (_cercleRegard == null || !regard.Disponible)
            {
                return;
            }
            if (!_cercleRegardPlace)
            {
                _lisse.Placer(regard.XEcran, regard.YEcran);
                _cercleRegardPlace = true;
            }
            _lisse.Suivre(regard.XEcran, regard.YEcran, regard.MaintenantMs);

            OverlayWindow calque = Gestionnaire.Calque;
            Point centre = calque.VersWpf(_lisse.X, _lisse.Y);
            double rayon = calque.LongueurVersWpf(p.RayonCerclePx / 2);
            PlacerEllipse(_cercleRegardFond, centre, rayon);
            PlacerEllipse(_cercleRegard, centre, rayon);
        }

        private static void PlacerEllipse(Ellipse ellipse, Point centre, double rayon)
        {
            ellipse.Width = rayon * 2;
            ellipse.Height = rayon * 2;
            Canvas.SetLeft(ellipse, centre.X - rayon);
            Canvas.SetTop(ellipse, centre.Y - rayon);
            ellipse.Visibility = Visibility.Visible;
        }

        private static Brush Figer(SolidColorBrush pinceau)
        {
            pinceau.Freeze();
            return pinceau;
        }
    }
}
