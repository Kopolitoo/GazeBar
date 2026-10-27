using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// MODE MENU (étape 4 du cahier des charges).
    ///
    /// Entrée : regarder en bas, hors de l'écran, pendant [DelaiMenu]. C'est le ModeManager
    /// qui surveille cela, uniquement depuis les modes mouvement et pause (et plus tard jeu) :
    /// voir Mode.MenuAutorise (étape 6).
    ///
    /// Affichage : une barre centrée en bas de l'écran, avec des boutons activés au regard
    /// (BoutonRegard) : on regarde un bouton pendant [DelaiActivation], son anneau se remplit,
    /// puis il s'active. De gauche à droite :
    ///   Paramètres   ouvre le mode paramètres (étape 7) ;
    ///   Jeu          grisé : le mode jeu arrivera à l'étape 10 ;
    ///   Calibration  lance l'outil de calibration Tobii (mode calibration) ;
    ///   Clavier      ouvre le clavier visuel de Windows, puis ferme le menu ;
    ///   Pause        passe en mode pause. Si l'on vient du mode pause, ce bouton devient
    ///                « Reprendre » et ramène au mode mouvement ;
    ///   Suivi souris interrupteur : bord vert quand l'option est activée. Le menu reste
    ///                ouvert ; il faut quitter le bouton du regard pour pouvoir l'actionner
    ///                de nouveau ;
    ///   Diagnostic   ouvre la fenêtre Diagnostic (ajout : elle était dans l'ancienne barre) ;
    ///   Quitter      demande une confirmation (« Oui, quitter » / « Non »), puis ferme GazeBar.
    ///
    /// Un petit cercle (moitié de [RayonCercle]) montre où est le regard, comme dans le choix
    /// d'action.
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
            Calibration,
            Clavier,
            Pause,
            Reprendre,
            SuiviSouris,
            Diagnostic,
            Quitter,
            ConfirmerQuitter,
            AnnulerQuitter
        }

        /// <summary>
        /// Espace entre deux boutons, et entre les boutons et le bord de la barre, en pixels.
        /// Assez grand pour que l'anneau de progression d'un bouton ne touche pas son voisin.
        /// </summary>
        private const double Ecart = 60;

        /// <summary>Espace entre la barre et le bas de l'écran, en pixels.</summary>
        private const double MargeBas = 30;

        /// <summary>
        /// Marge autour de la barre, en pixels : le regard y est encore « dans la barre ».
        /// Évite de fermer le menu quand le regard frôle le bord.
        /// </summary>
        private const double MargeZone = 60;

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

        // La zone de la barre (avec sa marge), en pixels d'écran.
        private double _zoneGauche;
        private double _zoneDroite;
        private double _zoneHaut;

        // Instant (ms) où le regard a quitté la barre ; -1 = il est dedans.
        private double _debutHorsBarreMs = -1;

        // Le petit cercle du regard : position lissée en pixels d'écran.
        private readonly Lisseur _lisse = new Lisseur();
        private bool _cerclePlace;

        public ModeMenu()
        {
            _viseur = new ViseurBoutons(_boutons);
        }

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

            OverlayWindow calque = Gestionnaire.Calque;
            calque.MasquerAnneau();
            calque.MasquerIcone();
            calque.MasquerEcranChoix();

            // Le petit cercle part de là où est le cercle actuel (pas de saut), sinon il
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

            // 3. Le petit cercle du regard (seulement quand le regard est sur l'écran).
            if (regard.SurEcran)
            {
                if (!_cerclePlace)
                {
                    _lisse.Placer(regard.X, regard.Y);
                    _cerclePlace = true;
                }
                _lisse.Suivre(regard.X, regard.Y, regard.MaintenantMs);
                Gestionnaire.Calque.AfficherCercle(_lisse.X, _lisse.Y, p.RayonCercle / 2, false);
            }
        }

        public override void Quitter()
        {
            // La barre est cachée. Le petit cercle reste : le mode suivant le reprend là où il est.
            Gestionnaire.Calque.MasquerBarreMenu();
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

                case ActionMenu.Calibration:
                    // On note la fenêtre active AVANT de lancer l'outil : le mode calibration
                    // saura ainsi reconnaître la fenêtre de l'outil (voir ModeCalibration).
                    IntPtr fenetreAvant = NativeMethods.GetForegroundWindow();
                    string messageCalibration;
                    if (CalibrationTobii.Lancer(Gestionnaire.Regard, out messageCalibration))
                    {
                        Gestionnaire.ChangerMode(new ModeCalibration(fenetreAvant));
                    }
                    else
                    {
                        AfficherMessage(messageCalibration);
                    }
                    break;

                case ActionMenu.Clavier:
                    string messageClavier;
                    if (OuvrirClavierVisuel(out messageClavier))
                    {
                        Revenir();
                    }
                    else
                    {
                        AfficherMessage(messageClavier);
                    }
                    break;

                case ActionMenu.Pause:
                    Gestionnaire.ChangerMode(new ModePause());
                    break;

                case ActionMenu.Reprendre:
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

                case ActionMenu.Diagnostic:
                    ((App)Application.Current).OuvrirDiagnostic();
                    Revenir();
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

        /// <summary>
        /// Ouvre le clavier visuel de Windows (osk.exe), en attendant le clavier de GazeBar.
        ///
        /// Piège : GazeBar est un programme 32 bits. Sur un Windows 64 bits, quand un programme
        /// 32 bits demande le dossier System32, Windows le redirige en douce vers SysWOW64… où
        /// osk.exe n'existe pas. Le dossier « Sysnative » (invisible dans l'Explorateur) donne
        /// aux programmes 32 bits l'accès au vrai System32. On essaie donc d'abord Sysnative.
        /// </summary>
        private static bool OuvrirClavierVisuel(out string message)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string[] chemins =
            {
                Path.Combine(windows, "Sysnative", "osk.exe"),
                Path.Combine(windows, "System32", "osk.exe")
            };

            foreach (string chemin in chemins)
            {
                if (!File.Exists(chemin))
                {
                    continue;
                }

                try
                {
                    ProcessStartInfo demarrage = new ProcessStartInfo(chemin);
                    demarrage.UseShellExecute = true;
                    Process.Start(demarrage);
                    App.Log("Clavier visuel ouvert : " + chemin);
                    message = null;
                    return true;
                }
                catch (Exception ex)
                {
                    App.Log("Clavier visuel : échec avec " + chemin + " : " + ex.Message);
                }
            }

            message = "Clavier visuel introuvable (voir le journal).";
            App.Log(message);
            return false;
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
            if (_confirmation)
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
                //   E713 engrenage, E7FC manette, E890 œil, E765 clavier, E769 pause,
                //   E768 lecture, E962 souris, E9D9 diagnostic, E7E8 bouton marche/arrêt.
                Ajouter(boutons, actions, new BoutonRegard("", null, "Paramètres"), ActionMenu.Parametres);
                Ajouter(boutons, actions, BoutonRegard.Indisponible("", "Jeu (à venir, étape 10)"), null);
                Ajouter(boutons, actions, new BoutonRegard("", null, "Calibration"), ActionMenu.Calibration);
                Ajouter(boutons, actions, new BoutonRegard("", null, "Clavier visuel"), ActionMenu.Clavier);
                if (_retour is ModePause)
                {
                    Ajouter(boutons, actions, new BoutonRegard("", null, "Reprendre"), ActionMenu.Reprendre);
                }
                else
                {
                    Ajouter(boutons, actions, new BoutonRegard("", null, "Pause"), ActionMenu.Pause);
                }
                BoutonRegard suivi = new BoutonRegard("", null, "Suivi souris");
                suivi.DefinirAllume(p.SuiviSouris);
                Ajouter(boutons, actions, suivi, ActionMenu.SuiviSouris);
                Ajouter(boutons, actions, new BoutonRegard("", null, "Diagnostic"), ActionMenu.Diagnostic);
                Ajouter(boutons, actions, new BoutonRegard("", null, "Quitter GazeBar"), ActionMenu.Quitter);
            }

            // ── Taille des boutons : [TailleIcones], réduite si la barre ne tient pas dans
            //    la largeur de l'écran.
            int n = boutons.Count;
            // Les boutons du menu sont deux fois plus grands que les autres icônes
            // (demande de l'utilisateur du 22/10/2026).
            double taille = 2 * p.TailleIcones;
            double tailleMax = (largeur - 20 - (n + 1) * Ecart) / n;
            taille = Math.Max(20, Math.Min(taille, tailleMax));

            // ── Le fond de la barre : un rectangle sombre aux coins arrondis, centré en bas.
            double largeurBarre = n * taille + (n + 1) * Ecart;
            double hauteurBarre = taille + 2 * Ecart;
            double gauche = (largeur - largeurBarre) / 2;
            double haut = hauteur - MargeBas - hauteurBarre;

            Border fond = new Border();
            fond.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24));
            fond.BorderBrush = Brushes.Gray;
            fond.BorderThickness = new Thickness(1);
            fond.CornerRadius = new CornerRadius(16);
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
                texte.FontSize = 28;
                Border cadre = new Border();
                cadre.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24));
                cadre.CornerRadius = new CornerRadius(8);
                cadre.Padding = new Thickness(16, 6, 16, 6);
                cadre.Child = texte;
                cadre.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Point centreHaut = calque.VersWpf(largeur / 2, haut - 20);
                Canvas.SetLeft(cadre, centreHaut.X - cadre.DesiredSize.Width / 2);
                Canvas.SetTop(cadre, centreHaut.Y - cadre.DesiredSize.Height);
                surface.Children.Add(cadre);

                // Le message fait partie de la barre pour la fermeture (hauteur convertie
                // en pixels d'écran : environ 80 px).
                hautZone = haut - 20 - 80;
            }

            // ── La zone « dans la barre », avec sa marge.
            _zoneGauche = gauche - MargeZone;
            _zoneDroite = gauche + largeurBarre + MargeZone;
            _zoneHaut = hautZone - MargeZone;

            // Les boutons ont changé : on repart de zéro, et le bouton éventuellement sous le
            // regard ne s'active pas tant que le regard ne l'a pas quitté.
            _viseur.ReinitialiserEtBloquer();

            calque.AfficherBarreMenu();
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
