using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// MODE CONFIGURATION DES ICÔNES (étape 10 du cahier des charges).
    ///
    /// Entrée : bouton « Configurer les icônes » de l'onglet « Icônes » du mode paramètres.
    /// Sortie : icône « retour » (au milieu du bord droit) ou touche Échap de l'aidant : retour
    /// au mode paramètres, sur l'onglet « Icônes ». Le menu du bas ne s'ouvre pas ici.
    ///
    /// Mise en page (demande de l'utilisateur du 28/10/2026) : LA MÊME que le choix d'action
    /// (DispositionChoixAction), sur le même écran noir du calque. À la place de la photo, un
    /// cadre gris qui explique l'écran. Les quatorze emplacements sont dessinés : les huit de la
    /// couronne, les trois modificateurs (bord gauche) et les trois boutons utilitaires (bord
    /// haut). Chacun porte une pastille ronde en haut à droite :
    ///   - « − » rouge : une action est assignée. Regarder l'icône RETIRE l'action ;
    ///     l'emplacement devient vide (pastille « + ») ;
    ///   - « + » verte : l'emplacement est vide. Regarder l'icône ouvre LA LISTE.
    ///
    /// La liste : un rectangle aux bords arrondis qui occupe les trois quarts de l'écran, avec
    /// TOUTES les actions du même groupe, chacune avec son nom en dessous, et un bouton
    /// « Annuler ». Une action peut être placée plusieurs fois dans son groupe (demande de
    /// l'utilisateur du 28/10/2026), par exemple deux clics gauches dans la couronne. Une fois
    /// l'action choisie, elle est placée et la liste se ferme.
    ///
    /// Chaque changement est appliqué tout de suite (Parametres.Courants) et enregistré dans
    /// parametres.json : le prochain choix d'action utilise la nouvelle disposition.
    ///
    /// Après chaque changement, l'écran est redessiné et l'icône qui se trouve sous le regard
    /// est bloquée tant que le regard ne l'a pas quittée (ViseurBoutons.ReinitialiserEtBloquer) :
    /// sans cela, en continuant à regarder une icône qu'on vient de vider, on rouvrirait
    /// aussitôt la liste.
    ///
    /// Ce mode est dessiné sur le calque, qui laisse passer la souris : il se commande au
    /// regard uniquement (l'aidant peut en sortir avec Échap).
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal sealed class ModeConfigIcones : Mode
    {
        /// <summary>Proportion de l'écran occupée par la liste (les trois quarts, en largeur et en hauteur).</summary>
        private const double ProportionListe = 0.75;

        /// <summary>Nombre maximal de colonnes de la liste (huit actions → deux lignes de quatre).</summary>
        private const int ColonnesListe = 4;

        // ── Couleurs.
        private static readonly Brush FondApercu = Figer(new SolidColorBrush(Color.FromRgb(0x20, 0x21, 0x24)));
        private static readonly Brush FondListe = Figer(new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x26)));
        private static readonly Brush Doux = Figer(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAD)));
        private static readonly Brush Voile = Figer(new SolidColorBrush(Color.FromArgb(0xC0, 0x00, 0x00, 0x00)));

        // Les boutons affichés, et ce que fait chacun (même ordre).
        private readonly List<BoutonRegard> _boutons = new List<BoutonRegard>();
        private readonly List<Action> _actions = new List<Action>();

        // Décide quel bouton est regardé et quand il s'active (classe partagée avec le menu).
        private readonly ViseurBoutons _viseur;

        // Vrai tant que l'écran n'a pas pu être construit (calque pas encore prêt).
        private bool _aConstruire;

        // La liste est-elle ouverte ? Et pour quel emplacement ?
        private bool _listeOuverte;
        private GroupeIcones _groupeChoisi;
        private int _emplacementChoisi;

        // Le petit cercle qui montre le regard (comme dans le choix d'action).
        private Ellipse _cercleRegardFond;
        private Ellipse _cercleRegard;
        private readonly Lisseur _lisse = new Lisseur();
        private bool _cercleRegardPlace;

        public ModeConfigIcones()
        {
            _viseur = new ViseurBoutons(_boutons);
        }

        public override string Nom
        {
            get { return "Configuration des icônes"; }
        }

        public override void Entrer(EtatRegard regard)
        {
            Gestionnaire.Calque.MasquerTout();
            Gestionnaire.Calque.RemettreAuPremierPlan();
            _listeOuverte = false;
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

            // Dans la liste, chaque action a déjà son nom écrit en dessous : le libellé au
            // survol se superposerait à ce nom, il n'est donc pas affiché.
            bool libelles = p.AfficherLibelles && !_listeOuverte;
            int active = _viseur.Actualiser(regard, p.DelaiActivation, libelles);
            if (active >= 0)
            {
                App.Log("Configuration des icônes : " + _boutons[active].Libelle);
                _actions[active]();
                return;
            }

            DeplacerCercleRegard(regard, p);
        }

        /// <summary>
        /// L'écran a changé (étape 11) : on reste ici, et tout l'écran est redessiné au
        /// prochain tic avec les nouvelles tailles (la liste reste ouverte si elle l'était).
        /// </summary>
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

        /// <summary>
        /// Icône « retour » ou touche Échap (appelée aussi par App) : retour au mode
        /// paramètres, sur l'onglet « Icônes ».
        /// </summary>
        public void RetourParametres()
        {
            App.Log("Configuration des icônes : retour aux paramètres");
            Gestionnaire.ChangerMode(new ModeParametres(CatalogueParametres.NumeroOngletIcones));
        }

        // ───────────────────────── Les actions ─────────────────────────

        /// <summary>Icône assignée (pastille « − ») : l'action est retirée, l'emplacement devient vide.</summary>
        private void Retirer(GroupeIcones groupe, int numero)
        {
            Parametres p = Parametres.Courants;
            p.DefinirEmplacement(groupe, numero, null);
            p.Enregistrer();
            App.Log("Configuration des icônes : " + groupe + " " + numero + " vidé");
            Construire();
        }

        /// <summary>Icône vide (pastille « + ») : ouvre la liste des actions à placer.</summary>
        private void OuvrirListe(GroupeIcones groupe, int numero)
        {
            _listeOuverte = true;
            _groupeChoisi = groupe;
            _emplacementChoisi = numero;
            Construire();
        }

        /// <summary>Une action de la liste : elle est placée, la liste se ferme.</summary>
        private void PlacerAction(ActionChoix action)
        {
            Parametres p = Parametres.Courants;
            p.DefinirEmplacement(_groupeChoisi, _emplacementChoisi, action);
            p.Enregistrer();
            App.Log("Configuration des icônes : " + action + " placé en " + _groupeChoisi + " " + _emplacementChoisi);
            _listeOuverte = false;
            Construire();
        }

        /// <summary>« Annuler » dans la liste : rien ne change, la liste se ferme.</summary>
        private void FermerListe()
        {
            _listeOuverte = false;
            Construire();
        }

        // ───────────────────────── Construction de l'écran ─────────────────────────

        /// <summary>
        /// (Re)dessine tout l'écran : la disposition, et la liste par-dessus si elle est ouverte.
        /// Toutes les positions sont calculées en pixels d'écran, puis converties par le calque.
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

            // La photo du choix d'action a le même côté : 2 × [RayonCercle], au plus l'écran.
            double cotePhoto = Math.Min(Math.Round(2 * p.RayonCerclePx), Math.Min(largeur, hauteur));
            DispositionChoixAction mise = new DispositionChoixAction(largeur, hauteur, taille, cotePhoto);

            calque.PreparerEcranChoix();
            Canvas ecran = calque.EcranChoix;
            _boutons.Clear();
            _actions.Clear();

            // Quand la liste est ouverte, la disposition est dessinée derrière, mais ses icônes
            // ne sont pas des cibles du regard (seules celles de la liste le sont).
            bool cibles = !_listeOuverte;

            ConstruireApercu(calque, ecran, mise);

            foreach (GroupeIcones groupe in new[] { GroupeIcones.Couronne, GroupeIcones.Utilitaires, GroupeIcones.Modificateurs })
            {
                ActionChoix?[] disposition = p.Disposition(groupe);
                Point[] places = mise.Places(groupe);
                for (int i = 0; i < disposition.Length; i++)
                {
                    GroupeIcones g = groupe;   // copies pour les fonctions ci-dessous
                    int numero = i;            // (sinon elles verraient les dernières valeurs)
                    BoutonRegard bouton;
                    Action action;
                    if (disposition[i].HasValue)
                    {
                        bouton = CatalogueActions.CreerBouton(disposition[i].Value);
                        bouton.DefinirPastilleConfig(false);
                        action = () => Retirer(g, numero);
                    }
                    else
                    {
                        bouton = BoutonRegard.VideConfigurable("Emplacement libre : choisir une action");
                        bouton.DefinirPastilleConfig(true);
                        action = () => OuvrirListe(g, numero);
                    }
                    AjouterBouton(calque, ecran, bouton, action, places[i].X, places[i].Y, taille, cibles);
                }
            }

            AjouterBouton(calque, ecran, CatalogueActions.CreerBouton(ActionChoix.Retour), RetourParametres,
                          mise.Retour.X, mise.Retour.Y, taille, cibles);

            if (_listeOuverte)
            {
                ConstruireListe(calque, ecran, mise, p);
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
            // pas tant que le regard ne l'a pas quitté.
            _viseur.ReinitialiserEtBloquer();
        }

        /// <summary>
        /// À la place de la photo : un carré gris de la même taille, avec le titre de l'écran
        /// et un rappel du rôle des pastilles.
        /// </summary>
        private static void ConstruireApercu(OverlayWindow calque, Canvas ecran, DispositionChoixAction mise)
        {
            double cote = mise.CotePhotoAgrandie;
            Point coin = calque.VersWpf(mise.CentreX - cote / 2, mise.CentreY - cote / 2);
            double coteWpf = calque.LongueurVersWpf(cote);
            double policeTitre = calque.LongueurVersWpf(mise.Taille * 0.26);
            double policeTexte = calque.LongueurVersWpf(mise.Taille * 0.18);

            StackPanel textes = new StackPanel();
            textes.VerticalAlignment = VerticalAlignment.Center;
            textes.Margin = new Thickness(coteWpf * 0.06);
            textes.Children.Add(Texte("Configuration des icônes", policeTitre, Brushes.White, true));
            TextBlock plus = Texte("+ : placer une action", policeTexte, Doux, false);
            plus.Margin = new Thickness(0, policeTexte, 0, 0);
            textes.Children.Add(plus);
            textes.Children.Add(Texte("− : retirer l'action", policeTexte, Doux, false));

            Border cadre = new Border();
            cadre.Width = coteWpf;
            cadre.Height = coteWpf;
            cadre.Background = FondApercu;
            cadre.BorderBrush = Brushes.Gray;
            cadre.BorderThickness = new Thickness(Ecran.Unites(2));
            cadre.Child = textes;
            Canvas.SetLeft(cadre, coin.X);
            Canvas.SetTop(cadre, coin.Y);
            ecran.Children.Add(cadre);
        }

        /// <summary>
        /// La liste : un voile sombre sur tout l'écran, puis un rectangle aux bords arrondis
        /// (les trois quarts de l'écran) avec un titre, toutes les actions du groupe en grille
        /// (quatre colonnes au plus), chacune avec son nom, et « Annuler » en bas.
        /// </summary>
        private void ConstruireListe(OverlayWindow calque, Canvas ecran, DispositionChoixAction mise, Parametres p)
        {
            double taille = mise.Taille;
            double espace = DispositionChoixAction.Espace;

            // ── Le voile, qui assombrit la disposition derrière la liste.
            Rectangle voile = new Rectangle();
            voile.Fill = Voile;
            voile.Width = ecran.Width;
            voile.Height = ecran.Height;
            Canvas.SetLeft(voile, 0);
            Canvas.SetTop(voile, 0);
            ecran.Children.Add(voile);

            // ── Le rectangle aux bords arrondis, centré.
            double l = mise.Largeur * ProportionListe;
            double h = mise.Hauteur * ProportionListe;
            double gauche = (mise.Largeur - l) / 2;
            double haut = (mise.Hauteur - h) / 2;
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

            // ── Le titre, en haut du rectangle.
            // Les textes de la liste suivent aussi la taille du texte de Windows (étape 11).
            double policeTitre = taille * 0.3 * Ecran.TexteWindows;
            AjouterTexteCentre(calque, ecran, "Choisir l'action à placer", mise.CentreX, haut + taille * 0.35,
                               l, policeTitre, Brushes.White, true);

            // ── « Annuler », en bas du rectangle, avec son nom en dessous.
            //    E711 = croix (annuler).
            double policeNom = taille * 0.2 * Ecran.TexteWindows;
            double yAnnuler = haut + h - taille / 2 - policeNom * 2.5;
            AjouterBouton(calque, ecran, new BoutonRegard("", null, "Annuler"), FermerListe,
                          mise.CentreX, yAnnuler, taille, true);
            AjouterTexteCentre(calque, ecran, "Annuler", mise.CentreX, yAnnuler + taille / 2 + policeNom * 0.4,
                               taille * 3, policeNom, Doux, false);

            // ── Toutes les actions du groupe, même celles déjà placées ailleurs : une action
            //    peut apparaître plusieurs fois (demande de l'utilisateur du 28/10/2026).
            List<ActionChoix> libres = new List<ActionChoix>(CatalogueActions.Actions(_groupeChoisi));

            // La zone de la grille : entre le titre et « Annuler ».
            double hautGrille = haut + taille * 0.35 + policeTitre * 2;
            double basGrille = yAnnuler - taille / 2 - espace / 2;

            if (libres.Count == 0)
            {
                // Ne devrait pas arriver : chaque groupe a au moins une action.
                AjouterTexteCentre(calque, ecran, "Aucune action pour ce groupe.",
                                   mise.CentreX, (hautGrille + basGrille) / 2, l, policeNom * 1.3, Doux, false);
                return;
            }

            int colonnes = Math.Min(libres.Count, ColonnesListe);
            int lignes = (libres.Count + colonnes - 1) / colonnes;
            // La grille occupe toute la place du rectangle (demande de l'utilisateur du
            // 28/10/2026 : les icônes paraissaient petites au milieu de beaucoup d'espace).
            // Chaque action reçoit une « case » : la largeur du rectangle partagée entre les
            // colonnes, et la hauteur entre le titre et « Annuler » partagée entre les lignes.
            // L'icône et son nom sont centrés dans leur case.
            double pasX = l / colonnes;
            double pasY = Math.Max((basGrille - hautGrille) / lignes, taille + policeNom * 2 + espace * 0.5);
            // Centre de l'icône de la première ligne : le milieu de la case, remonté de la
            // moitié de la hauteur du nom (le nom est écrit sous l'icône).
            double yPremiere = (hautGrille + basGrille) / 2 - (lignes - 1) * pasY / 2 - policeNom;

            for (int i = 0; i < libres.Count; i++)
            {
                int ligne = i / colonnes;
                int colonne = i % colonnes;
                // La dernière ligne, si elle est incomplète, est centrée elle aussi.
                int dansLaLigne = Math.Min(colonnes, libres.Count - ligne * colonnes);
                double x = mise.CentreX + (colonne - (dansLaLigne - 1) / 2.0) * pasX;
                double y = yPremiere + ligne * pasY;

                ActionChoix action = libres[i];   // copie pour la fonction ci-dessous
                AjouterBouton(calque, ecran, CatalogueActions.CreerBouton(action), () => PlacerAction(action),
                              x, y, taille, true);
                AjouterTexteCentre(calque, ecran, CatalogueActions.NomCourt(action), x, y + taille / 2 + policeNom * 0.4,
                                   pasX - espace, policeNom, Brushes.White, false);
            }
        }

        // ───────────────────────── Petits outils ─────────────────────────

        /// <summary>
        /// Place un bouton (centré sur x, y en pixels d'écran). cible = vrai : le regard peut
        /// l'activer, et « action » est alors appelée ; faux : il est seulement dessiné.
        /// </summary>
        private void AjouterBouton(OverlayWindow calque, Canvas ecran, BoutonRegard bouton, Action action,
                                   double x, double y, double taille, bool cible)
        {
            bouton.FacteurZone = ModeChoixAction.FacteurZone;
            bouton.Placer(calque, ecran, x, y, taille);
            if (cible)
            {
                _boutons.Add(bouton);
                _actions.Add(action);
            }
        }

        /// <summary>
        /// Écrit un texte centré horizontalement sur x, son haut en y (pixels d'écran), sur une
        /// largeur de « largeur » pixels (le texte passe à la ligne au-delà).
        /// </summary>
        private static void AjouterTexteCentre(OverlayWindow calque, Canvas ecran, string texte, double x, double y,
                                               double largeur, double policePixels, Brush couleur, bool gras)
        {
            TextBlock bloc = Texte(texte, calque.LongueurVersWpf(policePixels), couleur, gras);
            bloc.Width = calque.LongueurVersWpf(largeur);
            bloc.TextAlignment = TextAlignment.Center;
            bloc.TextWrapping = TextWrapping.Wrap;
            Point point = calque.VersWpf(x, y);
            Canvas.SetLeft(bloc, point.X - bloc.Width / 2);
            Canvas.SetTop(bloc, point.Y);
            ecran.Children.Add(bloc);
        }

        private static TextBlock Texte(string texte, double taille, Brush couleur, bool gras)
        {
            TextBlock bloc = new TextBlock();
            bloc.Text = texte;
            bloc.FontSize = taille;
            bloc.Foreground = couleur;
            bloc.FontFamily = new FontFamily("Segoe UI");
            bloc.TextWrapping = TextWrapping.Wrap;
            if (gras)
            {
                bloc.FontWeight = FontWeights.Bold;
            }
            return bloc;
        }

        /// <summary>
        /// Déplace le petit cercle du regard : il suit le regard en douceur ([Lissage]), comme
        /// dans le choix d'action. Son rayon est la moitié de [RayonCercle]. Regard hors de
        /// l'écran : il glisse le long du bord. Regard perdu : il reste où il est.
        /// </summary>
        private void DeplacerCercleRegard(EtatRegard regard, Parametres p)
        {
            if (_cercleRegard == null || !regard.Disponible)
            {
                return;
            }

            // Position ramenée dans l'écran : regard hors de l'écran → le cercle glisse le
            // long du bord (voir EtatRegard.XEcran).
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
