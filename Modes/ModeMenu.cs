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
    /// mais celle du clavier (MenuClavier = vrai). Pour l'instant, elle n'a qu'un bouton :
    /// « Fermer le clavier ». Les autres (langue, haut/bas, zone de saisie, taille, mes mots)
    /// viendront à l'étape 14. Le clavier reste affiché sous la barre.
    ///
    /// Sortie : quand le regard quitte la barre pendant [DelaiFermetureMenu], elle disparaît
    /// et on revient au mode précédent. Le regard en bas, hors de l'écran, compte comme
    /// « dans la barre » : c'est là qu'il est au moment où le menu s'ouvre. Un regard perdu ne
    /// compte pas (ni dedans, ni dehors).
    /// </summary>
    internal sealed class ModeMenu : Mode
    {
        /// <summary>Les actions des boutons de la barre.</summary>
        private enum ActionMenu
        {
            Parametres,
            Jeu,
            QuitterJeu,
            Clavier,
            FermerClavier,
            Pause,
            Reprendre,
            SuiviSouris,
            Quitter,
            ConfirmerQuitter,
            AnnulerQuitter
        }

        /// <summary>
        /// Espace entre deux boutons, et entre les boutons et le bord de la barre, en pixels.
        /// Assez grand pour que l'anneau de progression d'un bouton ne touche pas son voisin.
        /// Étape 11 : ces trois distances sont des pixels de l'écran 4K, converties pour
        /// l'écran actuel (Ecran.Px, ou Ecran.PxRegard pour la marge du regard).
        /// </summary>
        private const double Ecart4K = 60;

        private static double Ecart
        {
            get { return Ecran.Px(Ecart4K); }
        }

        /// <summary>Espace entre la barre et le bas de l'écran, en pixels de l'écran 4K.</summary>
        private const double MargeBas4K = 30;

        /// <summary>
        /// Marge autour de la barre, en pixels de l'écran 4K : le regard y est encore « dans la
        /// barre ». Évite de fermer le menu quand le regard frôle le bord.
        /// </summary>
        private const double MargeZone4K = 60;

        // Les boutons affichés, et l'action de chacun (même ordre ; null = bouton indisponible).
        private readonly List<BoutonRegard> _boutons = new List<BoutonRegard>();
        private readonly List<ActionMenu?> _actions = new List<ActionMenu?>();
        private readonly ViseurBoutons _viseur;

        // Le mode d'où l'on vient, pour y revenir à la fermeture.
        private Mode _retour;

        // Vrai pendant la confirmation de « Quitter ».
        private bool _confirmation;

        // Un message affiché au-dessus de la barre (null = aucun).
        private string _message;

        // Vrai si la barre a pu être construite (le calque est affiché).
        private bool _construite;

        // Vrai une fois la barre affichée depuis l'entrée dans le menu : seule la première
        // apparition est animée (pas la barre « Oui / Non » de la confirmation).
        private bool _dejaAffichee;

        // La zone de la barre (avec sa marge), en pixels d'écran.
        private double _zoneGauche;
        private double _zoneDroite;
        private double _zoneHaut;

        // Instant (ms) où le regard a quitté la barre ; -1 = il est dedans.
        private double _debutHorsBarreMs = -1;

        // Le cercle du regard : position lissée en pixels d'écran.
        private readonly Lisseur _lisse = new Lisseur();
        private bool _cerclePlace;

        /// <summary>La barre de menu classique.</summary>
        public ModeMenu() : this(false)
        {
        }

        /// <summary>
        /// menuClavier = vrai : la barre de menu du clavier au regard (étape 12) au lieu de la
        /// barre classique.
        /// </summary>
        public ModeMenu(bool menuClavier)
        {
            MenuClavier = menuClavier;
            _viseur = new ViseurBoutons(_boutons);
        }

        /// <summary>Vrai pour la barre de menu du clavier (le clavier reste alors affiché dessous).</summary>
        public bool MenuClavier { get; private set; }

        public override string Nom
        {
            get { return "Menu"; }
        }

        public override void Entrer(EtatRegard regard)
        {
            // Le ModeManager a déjà noté le mode d'avant : c'est là qu'on reviendra.
            _retour = Gestionnaire.ModePrecedent;
            _confirmation = false;
            _message = null;
            _debutHorsBarreMs = -1;
            _dejaAffichee = false;

            OverlayWindow calque = Gestionnaire.Calque;
            calque.MasquerAnneau();
            calque.MasquerIcone();
            calque.MasquerEcranChoix();

            // Le cercle part de là où est le cercle actuel (pas de saut), sinon il
            // apparaîtra à la première mesure du regard sur l'écran.
            _cerclePlace = calque.CercleAffiche;
            if (_cerclePlace)
            {
                _lisse.Placer(calque.CercleX, calque.CercleY);
            }

            Construire();
        }

        public override void Actualiser(EtatRegard regard)
        {
            if (!_construite)
            {
                // Calque pas encore affiché (cas improbable) : on ne reste pas dans un menu invisible.
                Revenir();
                return;
            }

            Parametres p = Parametres.Courants;
            double maintenant = regard.MaintenantMs;

            // 1. Les boutons : l'un d'eux vient-il de s'activer ?
            int active = _viseur.Actualiser(regard, p.DelaiActivation, p.AfficherLibelles);
            if (active >= 0)
            {
                Activer(active);
                return;
            }

            // 2. La fermeture : regard hors de la barre pendant [DelaiFermetureMenu].
            if (regard.Disponible)
            {
                if (DansLaBarre(regard))
                {
                    _debutHorsBarreMs = -1;
                }
                else if (_debutHorsBarreMs < 0)
                {
                    _debutHorsBarreMs = maintenant;
                }
                else if (maintenant - _debutHorsBarreMs >= p.DelaiFermetureMenu * 1000.0)
                {
                    Revenir();
                    return;
                }
            }

            // 3. Le cercle du regard. Il suit la position ramenée dans l'écran : regard hors de
            //    l'écran → il glisse le long du bord (demande de l'utilisateur du 29/10/2026).
            //    Il est caché quand le regard est dans la zone de la barre (avec sa marge), ou en
            //    bas hors de l'écran : il gênerait la lecture des boutons (demande du 28/10/2026).
            //    Sa position continue d'être suivie : il réapparaît sans saut en sortant de la zone.
            //    Regard perdu : il reste où il est.
            if (regard.Disponible)
            {
                if (!_cerclePlace)
                {
                    _lisse.Placer(regard.XEcran, regard.YEcran);
                    _cerclePlace = true;
                }
                _lisse.Suivre(regard.XEcran, regard.YEcran, regard.MaintenantMs);
                // Barre du clavier (étape 12) : le cercle est aussi caché au-dessus du clavier.
                if (DansLaBarre(regard) || (MenuClavier && Gestionnaire.Clavier.Contient(_lisse.X, _lisse.Y)))
                {
                    Gestionnaire.Calque.MasquerCercle();
                }
                else
                {
                    Gestionnaire.Calque.AfficherCercle(_lisse.X, _lisse.Y, p.RayonCerclePx, false);
                }
            }
        }

        public override void Quitter()
        {
            // La barre est cachée. Le cercle reste : le mode suivant le reprend là où il est.
            // Fermeture animée (voir OverlayWindow.MasquerBarreMenu).
            Gestionnaire.Calque.MasquerBarreMenu(true);
            _boutons.Clear();
            _actions.Clear();
        }

        // ───────────────────────── Les actions ─────────────────────────

        private void Activer(int numero)
        {
            ActionMenu action = _actions[numero].Value;
            App.Log("Menu : " + action);

            switch (action)
            {
                case ActionMenu.Parametres:
                    Gestionnaire.ChangerMode(new ModeParametres());
                    break;

                case ActionMenu.Jeu:
                    Gestionnaire.ChangerMode(new ModeJeu());
                    break;

                case ActionMenu.QuitterJeu:
                    Gestionnaire.ChangerMode(new ModeMouvement());
                    break;

                case ActionMenu.Clavier:
                    // Le clavier au regard (étape 12). Déjà ouvert (caché pendant le jeu) :
                    // le bouton le ferme. Sinon, il s'ouvre et apparaît au retour en mode
                    // mouvement. Fichier de disposition absent ou incorrect : message.
                    if (Gestionnaire.Clavier.Ouvert)
                    {
                        Gestionnaire.Clavier.Fermer();
                        Revenir();
                        break;
                    }
                    string messageClavier;
                    if (Gestionnaire.Clavier.Ouvrir(out messageClavier))
                    {
                        // Depuis le jeu, on revient au mode mouvement : c'est là que le clavier s'affiche.
                        Gestionnaire.ChangerMode(new ModeMouvement());
                    }
                    else
                    {
                        AfficherMessage(messageClavier);
                    }
                    break;

                case ActionMenu.FermerClavier:
                    Gestionnaire.Clavier.Fermer();
                    Gestionnaire.ChangerMode(new ModeMouvement());
                    break;

                case ActionMenu.Pause:
                    Gestionnaire.ChangerMode(new ModePause());
                    break;

                case ActionMenu.Reprendre:
                    // Fin de la pause : retour direct au mode mouvement, sans afficher les
                    // autres boutons (choix de l'utilisateur du 28/10/2026).
                    Gestionnaire.ChangerMode(new ModeMouvement());
                    break;

                case ActionMenu.SuiviSouris:
                    // Interrupteur : on inverse l'option, on l'enregistre, et on reste dans le menu.
                    Parametres.Courants.SuiviSouris = !Parametres.Courants.SuiviSouris;
                    Parametres.Courants.Enregistrer();
                    _boutons[numero].DefinirAllume(Parametres.Courants.SuiviSouris);
                    App.Log("Suivi souris : " + (Parametres.Courants.SuiviSouris ? "oui" : "non"));
                    _viseur.BloquerJusquASortie();
                    break;

                case ActionMenu.Quitter:
                    _confirmation = true;
                    _message = "Quitter GazeBar ?";
                    Construire();
                    break;

                case ActionMenu.ConfirmerQuitter:
                    App.Log("Menu : fermeture de GazeBar demandée");
                    Application.Current.Shutdown();
                    break;

                case ActionMenu.AnnulerQuitter:
                    _confirmation = false;
                    _message = null;
                    Construire();
                    break;
            }
        }

        /// <summary>Ferme le menu et revient au mode d'avant (mode mouvement par défaut).</summary>
        private void Revenir()
        {
            Mode retour = _retour;
            if (retour == null || retour is ModeMenu)
            {
                retour = new ModeMouvement();
            }
            Gestionnaire.ChangerMode(retour);
        }

        /// <summary>Affiche un message au-dessus de la barre (par exemple une erreur).</summary>
        private void AfficherMessage(string message)
        {
            _message = message;
            Construire();
        }

        // ───────────────────────── La barre ─────────────────────────

        /// <summary>
        /// (Re)construit la barre : les boutons du menu, ou ceux de la confirmation de
        /// « Quitter ». Toutes les positions sont en pixels d'écran, converties par le calque.
        /// </summary>
        private void Construire()
        {
            OverlayWindow calque = Gestionnaire.Calque;
            _construite = false;
            _boutons.Clear();
            _actions.Clear();
            if (!calque.Pret)
            {
                return;
            }

            Parametres p = Parametres.Courants;
            double largeur = Gestionnaire.LargeurEcran;
            double hauteur = Gestionnaire.HauteurEcran;

            calque.PreparerBarreMenu();
            Canvas surface = calque.BarreMenu;

            // ── Les boutons à afficher.
            List<BoutonRegard> boutons = new List<BoutonRegard>();
            List<ActionMenu?> actions = new List<ActionMenu?>();
            if (_retour is ModePause && !_confirmation)
            {
                // Pendant la pause : seul le bouton « Reprendre » (E768 = lecture).
                Ajouter(boutons, actions, new BoutonRegard("", null, "Reprendre"), ActionMenu.Reprendre);
            }
            else if (MenuClavier && !_confirmation)
            {
                // Barre de menu du clavier (étape 12) : pour l'instant, seulement « Fermer le
                // clavier » (E765 = clavier, avec une petite croix « × » en pastille).
                Ajouter(boutons, actions, new BoutonRegard("\uE765", "\u00D7", "Fermer le clavier"), ActionMenu.FermerClavier);
            }
            else if (_confirmation)
            {
                // « Non » est à droite, « Oui » à gauche. La barre est plus courte et centrée :
                // aucun des deux n'est à la place de l'ancien bouton « Quitter » (tout à droite),
                // pour qu'on ne confirme jamais en continuant simplement à regarder au même endroit.
                Ajouter(boutons, actions, new BoutonRegard("", null, "Oui, quitter GazeBar"), ActionMenu.ConfirmerQuitter);
                Ajouter(boutons, actions, new BoutonRegard("", null, "Non, ne pas quitter"), ActionMenu.AnnulerQuitter);
            }
            else
            {
                // Symboles de la police Windows (Segoe MDL2 Assets / Segoe Fluent Icons) :
                //   E713 engrenage, E7FC manette, E765 clavier, E769 pause,
                //   E768 lecture, E962 souris, E7E8 bouton marche/arrêt.
                Ajouter(boutons, actions, new BoutonRegard("", null, "Paramètres"), ActionMenu.Parametres);
                if (_retour is ModeJeu)
                {
                    // Bord vert : le mode jeu est actif.
                    BoutonRegard quitterJeu = new BoutonRegard("\uE7FC", null, "Quitter le jeu");
                    quitterJeu.DefinirAllume(true);
                    Ajouter(boutons, actions, quitterJeu, ActionMenu.QuitterJeu);
                }
                else
                {
                    Ajouter(boutons, actions, new BoutonRegard("\uE7FC", null, "Jeu"), ActionMenu.Jeu);
                }
                Ajouter(boutons, actions, new BoutonRegard("", null, "Clavier"), ActionMenu.Clavier);
                // (Depuis la pause, seul « Reprendre » est affiché : ici, on n'est donc
                // jamais en pause.)
                Ajouter(boutons, actions, new BoutonRegard("", null, "Pause"), ActionMenu.Pause);
                BoutonRegard suivi = new BoutonRegard("", null, "Suivi souris");
                suivi.DefinirAllume(p.SuiviSouris);
                Ajouter(boutons, actions, suivi, ActionMenu.SuiviSouris);
                Ajouter(boutons, actions, new BoutonRegard("", null, "Quitter GazeBar"), ActionMenu.Quitter);
            }

            // ── Taille des boutons : [TailleIcones], réduite si la barre ne tient pas dans
            //    la largeur de l'écran.
            int n = boutons.Count;
            // Les boutons du menu sont deux fois plus grands que les autres icônes
            // (demande de l'utilisateur du 22/10/2026).
            double taille = 2 * p.TailleIconesPx;
            double tailleMax = (largeur - Ecran.Px(20) - (n + 1) * Ecart) / n;
            taille = Math.Max(Ecran.Px(20), Math.Min(taille, tailleMax));

            // ── Le fond de la barre : un rectangle sombre aux coins arrondis, centré en bas.
            double largeurBarre = n * taille + (n + 1) * Ecart;
            double hauteurBarre = taille + 2 * Ecart;
            double gauche = (largeur - largeurBarre) / 2;
            double haut = hauteur - Ecran.Px(MargeBas4K) - hauteurBarre;

            Border fond = new Border();
            fond.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24));
            fond.BorderBrush = Brushes.Gray;
            fond.BorderThickness = new Thickness(Ecran.Unites(1));
            fond.CornerRadius = new CornerRadius(Ecran.Unites(16));
            fond.Width = calque.LongueurVersWpf(largeurBarre);
            fond.Height = calque.LongueurVersWpf(hauteurBarre);
            Point coin = calque.VersWpf(gauche, haut);
            Canvas.SetLeft(fond, coin.X);
            Canvas.SetTop(fond, coin.Y);
            surface.Children.Add(fond);

            // ── Les boutons, de gauche à droite (libellé au-dessus : en dessous, il serait
            //    hors de l'écran).
            for (int i = 0; i < n; i++)
            {
                double x = gauche + Ecart + taille / 2 + i * (taille + Ecart);
                double y = haut + Ecart + taille / 2;
                boutons[i].Placer(calque, surface, x, y, taille, true);
                _boutons.Add(boutons[i]);
                _actions.Add(actions[i]);
            }

            // ── Le message éventuel, centré au-dessus de la barre.
            double hautZone = haut;
            if (!string.IsNullOrEmpty(_message))
            {
                TextBlock texte = new TextBlock();
                texte.Text = _message;
                texte.Foreground = Brushes.White;
                // Le message suit la taille du texte de Windows (étape 11).
                texte.FontSize = Ecran.Police(28);
                Border cadre = new Border();
                cadre.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24));
                cadre.CornerRadius = new CornerRadius(Ecran.Unites(8));
                cadre.Padding = new Thickness(Ecran.Unites(16), Ecran.Unites(6), Ecran.Unites(16), Ecran.Unites(6));
                cadre.Child = texte;
                cadre.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Point centreHaut = calque.VersWpf(largeur / 2, haut - Ecran.Px(20));
                Canvas.SetLeft(cadre, centreHaut.X - cadre.DesiredSize.Width / 2);
                Canvas.SetTop(cadre, centreHaut.Y - cadre.DesiredSize.Height);
                surface.Children.Add(cadre);

                // Le message fait partie de la barre pour la fermeture. Sa hauteur mesurée est
                // en unités WPF : on la convertit en pixels d'écran (étape 11 ; c'était une
                // estimation fixe de 80 px avant).
                double hauteurMessage = cadre.DesiredSize.Height / calque.LongueurVersWpf(1);
                hautZone = haut - Ecran.Px(20) - hauteurMessage;
            }

            // ── La zone « dans la barre », avec sa marge.
            double margeZone = Ecran.PxRegard(MargeZone4K);
            _zoneGauche = gauche - margeZone;
            _zoneDroite = gauche + largeurBarre + margeZone;
            _zoneHaut = hautZone - margeZone;

            // Les boutons ont changé : on repart de zéro, et le bouton éventuellement sous le
            // regard ne s'active pas tant que le regard ne l'a pas quitté.
            _viseur.ReinitialiserEtBloquer();

            // Première apparition depuis l'entrée dans le menu : animée (voir AfficherBarreMenu).
            calque.AfficherBarreMenu(!_dejaAffichee);
            _dejaAffichee = true;
            _construite = true;
        }

        private static void Ajouter(List<BoutonRegard> boutons, List<ActionMenu?> actions,
                                    BoutonRegard bouton, ActionMenu? action)
        {
            boutons.Add(bouton);
            actions.Add(action);
        }

        /// <summary>Vrai si le regard est sur la barre (avec sa marge), ou en bas hors de l'écran.</summary>
        private bool DansLaBarre(EtatRegard regard)
        {
            if (regard.EnBasHorsEcran)
            {
                return true;
            }
            return regard.X >= _zoneGauche && regard.X <= _zoneDroite && regard.Y >= _zoneHaut;
        }
    }
}
