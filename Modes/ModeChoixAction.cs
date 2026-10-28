using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// MODE CHOIX D'ACTION (étape 3 du cahier des charges).
    ///
    /// On y arrive depuis le mode regard fixe, en regardant l'icône d'options. Le curseur est
    /// déjà sur le point fixé P (placé à l'entrée en regard fixe).
    ///
    /// Le mode se déroule en trois temps (« étapes ») :
    ///
    ///  1. CAPTURE. Le cercle et l'icône du calque sont cachés, on attend [AttenteMs] pour que
    ///     Windows ait fini de les effacer, puis on photographie le carré qui englobe le cercle
    ///     (côté 2 × [RayonCercle], soit 200 × 200 px). Sans cette attente, la photo
    ///     contiendrait le cercle.
    ///
    ///  2. CHOIX. Un écran noir couvre tout l'écran. Au centre : la photo agrandie ×2
    ///     (400 × 400 px) et une croix à la place du curseur. Autour : la couronne de huit
    ///     emplacements d'action. À gauche : les trois emplacements des modificateurs.
    ///     À droite : l'icône « retour ». Un emplacement sans action est quand même dessiné,
    ///     grisé, pour montrer qu'on pourra y mettre une action (demande de l'utilisateur
    ///     du 22/10/2026). Par-dessus tout, un petit cercle (moitié de [RayonCercle]) montre
    ///     où est le regard.
    ///     Regarder une icône pendant [DelaiActivation] l'active (son anneau se remplit).
    ///
    ///  3. EXÉCUTION. L'écran noir est caché, on attend [AttenteMs] qu'il ait disparu, puis le
    ///     clic est injecté au curseur. Ainsi, l'application visée est bien visible (et à jour)
    ///     au moment du clic. On revient ensuite au mode mouvement.
    ///
    /// L'icône « retour » ramène au mode mouvement sans rien faire.
    ///
    /// L'action « Loupe » (étape 5, emplacement 4 de la couronne) ouvre le mode loupe sur le
    /// même point P, pour affiner la position du curseur. La loupe revient ensuite ici, sur le
    /// point affiné (ou sur P si l'on y choisit « retour »).
    ///
    /// Étape 6 :
    ///   - les trois actions « glisser-déposer » (gauche, droit, android) ouvrent le choix du
    ///     point d'arrivée (ModeGlisserArrivee), avec P comme point de départ ;
    ///   - les trois emplacements de gauche sont les modificateurs Ctrl, Maj et Alt. Un regard
    ///     en active un (bord vert), un second le désactive ; le choix d'action reste ouvert.
    ///     Les touches actives sont enfoncées juste avant l'action et relâchées juste après
    ///     (voir Modificateurs.cs).
    ///
    /// Actions spéciales (demande de l'utilisateur du 28/10/2026) : Copier, Couper, Coller,
    /// trois boutons tout en haut de l'écran, centrés (voir DispositionSpeciales). Ils
    /// envoient Ctrl + C, Ctrl + X et Ctrl + V à la fenêtre active ; « Coller » fait d'abord
    /// un clic gauche sur P, pour placer le point d'insertion là où l'on regardait.
    ///
    /// Étape 8 : l'action « Défilement » (emplacement 0 depuis le 28/10/2026) ouvre le mode défilement, centré sur P.
    ///
    /// Ce qui viendra plus tard :
    ///   - étape 9 : le bouton de configuration de la disposition.
    /// </summary>
    internal sealed class ModeChoixAction : Mode
    {
        /// <summary>Les actions possibles depuis ce mode.</summary>
        private enum ActionChoix
        {
            ClicGauche,
            ClicDroit,
            DoubleClic,
            Loupe,
            GlisserGauche,
            GlisserDroit,
            GlisserAndroid,
            Defilement,
            Ctrl,
            Maj,
            Alt,
            Copier,
            Couper,
            Coller,
            Retour
        }

        /// <summary>Les trois temps du mode (voir le commentaire de la classe).</summary>
        private enum Etape
        {
            Capture,
            Choix,
            Execution
        }

        /// <summary>Agrandissement de la photo (le cahier des charges fixe ×2).</summary>
        private const double Zoom = 2;

        /// <summary>
        /// Attente avant la photo, et avant le clic, en millisecondes : le temps que Windows
        /// efface réellement ce qu'on vient de cacher (le cahier des charges dit « environ 50 ms »).
        /// </summary>
        private const double AttenteMs = 60;

        /// <summary>
        /// Disposition de la couronne : les huit emplacements, deux par côté de la photo, en
        /// partant du haut et en tournant dans le sens des aiguilles d'une montre (voir
        /// PlaceEmplacement). null = emplacement vide. À l'étape 9, cette disposition sera
        /// modifiable et enregistrée avec les paramètres ; pour l'instant elle est écrite ici.
        /// Ordre choisi par l'utilisateur le 28/10/2026 (défilement et clic gauche échangés,
        /// double clic déplacé) :
        ///   0 : en haut, à droite        → défilement (étape 8)
        ///   1 : à droite, en haut        → clic droit
        ///   2 : à droite, en bas         → glisser-déposer gauche (étape 6)
        ///   3 : en bas, à droite         → glisser-déposer droit (étape 6)
        ///   4 : en bas, à gauche         → loupe (étape 5, choix de l'utilisateur du 23/10/2026)
        ///   5 : à gauche, en bas         → glisser-déposer android (étape 6)
        ///   6 : à gauche, en haut        → double clic
        ///   7 : en haut, à gauche        → clic gauche
        /// </summary>
        private static readonly ActionChoix?[] Disposition =
        {
            ActionChoix.Defilement,
            ActionChoix.ClicDroit,
            ActionChoix.GlisserGauche,
            ActionChoix.GlisserDroit,
            ActionChoix.Loupe,
            ActionChoix.GlisserAndroid,
            ActionChoix.DoubleClic,
            ActionChoix.ClicGauche
        };

        /// <summary>
        /// Zone d'activation de TOUS les boutons de ce mode (couronne, modificateurs, retour),
        /// par rapport au carré dessiné : 30 % plus grande (demande de l'utilisateur du
        /// 28/10/2026). Avec des boutons de 120 px séparés de [Espace] = 60 px, chaque zone
        /// déborde de 18 px de chaque côté : il reste 24 px entre deux zones voisines.
        /// </summary>
        private const double FacteurZone = 1.3;

        /// <summary>
        /// Les actions spéciales, de gauche à droite, tout en haut de l'écran et centrées
        /// horizontalement, sur la même ligne que le haut de la couronne (demande de
        /// l'utilisateur du 28/10/2026). Pour leur faire de la place, les deux emplacements du
        /// haut de la couronne (0 et 7) sont écartés vers l'extérieur : la ligne du haut compte
        /// alors cinq boutons régulièrement espacés : 7, Copier, Couper, Coller, 0.
        /// </summary>
        private static readonly ActionChoix[] DispositionSpeciales =
        {
            ActionChoix.Copier,
            ActionChoix.Couper,
            ActionChoix.Coller
        };

        /// <summary>
        /// Espace, en pixels, entre deux boutons voisins, entre les boutons et la photo, et entre
        /// les modificateurs et le bord gauche de l'écran (demande de l'utilisateur du 26/10/2026 :
        /// « environ 60 pixels »).
        /// </summary>
        private const double Espace = 60;

        /// <summary>
        /// Les trois emplacements de gauche, de haut en bas : les modificateurs (étape 6).
        /// Comme la couronne, cette disposition sera modifiable à l'étape 9.
        /// </summary>
        private static readonly ActionChoix?[] DispositionModificateurs =
        {
            ActionChoix.Ctrl,
            ActionChoix.Maj,
            ActionChoix.Alt
        };

        // Le point visé P (position du curseur), en pixels d'écran.
        private readonly double _px;
        private readonly double _py;

        private Etape _etape;
        private double _debutEtapeMs;

        /// <summary>Libellé d'un emplacement vide, affiché au survol.</summary>
        private const string LibelleVide = "Emplacement libre (à configurer dans les paramètres)";

        // Les boutons affichés, et l'action de chacun (même ordre ; null = emplacement vide).
        private readonly List<BoutonRegard> _boutons = new List<BoutonRegard>();
        private readonly List<ActionChoix?> _actions = new List<ActionChoix?>();

        // Le petit cercle qui montre le regard : deux ellipses (trait noir dessous, vert dessus),
        // ajoutées en dernier sur l'écran noir pour être par-dessus tout. Position lissée en
        // pixels d'écran ; _cercleRegardPlace = faux tant qu'il n'a pas de position.
        private Ellipse _cercleRegardFond;
        private Ellipse _cercleRegard;
        private readonly Lisseur _lisse = new Lisseur();
        private bool _cercleRegardPlace;

        // Décide quel bouton est regardé et quand il s'active (classe partagée avec le menu).
        private readonly ViseurBoutons _viseur;

        // L'action choisie, en attente d'exécution (étape Execution).
        private ActionChoix _actionChoisie;

        public ModeChoixAction(double x, double y)
        {
            _px = x;
            _py = y;
            _viseur = new ViseurBoutons(_boutons);
        }

        public override string Nom
        {
            get { return "Choix d'action"; }
        }

        public override void Entrer(EtatRegard regard)
        {
            _viseur.Reinitialiser();

            // 1. CAPTURE : on cache tout ce que GazeBar dessine, et on attend un peu avant la photo.
            Gestionnaire.Calque.MasquerTout();
            _etape = Etape.Capture;
            _debutEtapeMs = regard.MaintenantMs;
        }

        public override void Actualiser(EtatRegard regard)
        {
            double maintenant = regard.MaintenantMs;

            switch (_etape)
            {
                case Etape.Capture:
                    if (maintenant - _debutEtapeMs >= AttenteMs)
                    {
                        bool reussi;
                        try
                        {
                            reussi = ConstruireEcran();
                        }
                        catch (Exception ex)
                        {
                            App.Log("Choix d'action : erreur pendant la construction de l'écran : " + ex);
                            reussi = false;
                        }

                        if (!reussi)
                        {
                            // Photo impossible : on ne reste pas bloqué devant un écran noir.
                            Gestionnaire.ChangerMode(new ModeMouvement());
                        }
                    }
                    break;

                case Etape.Choix:
                    ActualiserChoix(regard);
                    break;

                case Etape.Execution:
                    if (maintenant - _debutEtapeMs >= AttenteMs)
                    {
                        Executer();
                    }
                    break;
            }
        }

        public override void Quitter()
        {
            // L'écran noir est caché et vidé (la photo n'est plus utile).
            Gestionnaire.Calque.MasquerEcranChoix();
            Gestionnaire.Calque.EcranChoix.Children.Clear();
            _boutons.Clear();
            _actions.Clear();
            _cercleRegard = null;
            _cercleRegardFond = null;
        }

        // ───────────────────────── 2. Le choix ─────────────────────────

        private void ActualiserChoix(EtatRegard regard)
        {
            Parametres p = Parametres.Courants;

            // Quel bouton est regardé ? L'un d'eux vient-il de s'activer ?
            // (Un emplacement vide ne s'active jamais : voir ViseurBoutons.)
            int active = _viseur.Actualiser(regard, p.DelaiActivation, p.AfficherLibelles);
            if (active >= 0)
            {
                Activer(_actions[active].Value);
                return;
            }

            // Le petit cercle du regard (seulement quand le regard est sur l'écran).
            DeplacerCercleRegard(regard, p);
        }

        /// <summary>Un bouton vient d'être activé.</summary>
        private void Activer(ActionChoix action)
        {
            App.Log("Choix d'action : " + action);

            if (action == ActionChoix.Retour)
            {
                // Retour sans rien faire. Le mode mouvement repart du regard (le cercle a été
                // caché pendant le choix, il n'y a donc pas de saut visible).
                Gestionnaire.ChangerMode(new ModeMouvement());
                return;
            }

            if (EstModificateur(action))
            {
                // Ctrl, Maj ou Alt : on l'allume ou on l'éteint, et on reste dans le choix.
                // Le bouton est bloqué tant que le regard ne l'a pas quitté : sans cela, en
                // continuant à le regarder, on l'allumerait et l'éteindrait sans fin.
                bool actif = Modificateurs.Basculer(VersModificateur(action));
                _boutons[_viseur.Vise].DefinirAllume(actif);
                _viseur.BloquerJusquASortie();
                return;
            }

            if (action == ActionChoix.GlisserGauche || action == ActionChoix.GlisserDroit ||
                action == ActionChoix.GlisserAndroid)
            {
                // Le glisser-déposer part du point visé P. L'écran noir sera caché par Quitter,
                // juste avant que le mode suivant ne montre l'écran réel.
                MouseSide bouton = action == ActionChoix.GlisserDroit ? MouseSide.Right : MouseSide.Left;
                bool avecPause = action == ActionChoix.GlisserAndroid;
                Gestionnaire.ChangerMode(new ModeGlisserArrivee(new Glissement(_px, _py, bouton, avecPause)));
                return;
            }

            if (action == ActionChoix.Defilement)
            {
                // Le défilement se fait autour de P, sur l'écran réel (l'écran noir sera caché
                // par Quitter, juste avant).
                Gestionnaire.ChangerMode(new ModeDefilement(_px, _py));
                return;
            }

            if (action == ActionChoix.Loupe)
            {
                // La loupe photographie elle-même la zone autour de P (l'écran noir sera caché
                // par Quitter, juste avant). « true » : son icône « retour » ramènera ici.
                Gestionnaire.ChangerMode(new ModeLoupe(_px, _py, true));
                return;
            }

            // 3. EXÉCUTION : on cache l'écran noir, et on attend qu'il ait disparu avant le clic.
            _actionChoisie = action;
            Gestionnaire.Calque.MasquerEcranChoix();
            _etape = Etape.Execution;
            _debutEtapeMs = Gestionnaire.Regard.MaintenantMs;
        }

        // ───────────────────────── 3. L'exécution ─────────────────────────

        private void Executer()
        {
            // Le curseur est remis sur P par sécurité (il a pu être bougé entre-temps).
            NativeMethods.SetCursorPos((int)Math.Round(_px), (int)Math.Round(_py));

            // Les modificateurs actifs sont enfoncés juste avant le clic…
            string modificateurs = Modificateurs.Description();
            Modificateurs.EnfoncerActifs();

            switch (_actionChoisie)
            {
                case ActionChoix.ClicGauche:
                    MouseInjector.Clic(MouseSide.Left);
                    break;
                case ActionChoix.ClicDroit:
                    MouseInjector.Clic(MouseSide.Right);
                    break;
                case ActionChoix.DoubleClic:
                    MouseInjector.DoubleClic();
                    break;
                case ActionChoix.Copier:
                    RaccourciCtrl(NativeMethods.VK_C);
                    break;
                case ActionChoix.Couper:
                    RaccourciCtrl(NativeMethods.VK_X);
                    break;
                case ActionChoix.Coller:
                    // Clic d'abord : le point d'insertion se place là où l'on regardait.
                    MouseInjector.Clic(MouseSide.Left);
                    RaccourciCtrl(NativeMethods.VK_V);
                    break;
            }

            // … et relâchés juste après. (Le mode mouvement les désactivera ensuite.)
            Modificateurs.RelacherTout();

            App.Log(_actionChoisie + " en (" + _px.ToString("F0") + ", " + _py.ToString("F0") +
                    "), modificateurs : " + modificateurs);

            // Retour au mode mouvement (choix de l'utilisateur). Une nouvelle fixation au même
            // endroit est ignorée tant que le regard n'en est pas sorti, comme après un clic direct.
            Gestionnaire.ChangerMode(new ModeMouvement(_px, _py));
        }

        /// <summary>
        /// Envoie Ctrl + touche (copier, couper, coller) à la fenêtre active. Pas de clic pour
        /// copier et couper : un clic effacerait la sélection. Si Ctrl était déjà enfoncé par
        /// les modificateurs, Enfoncer ne fait rien ; Maj ou Alt actifs restent ajoutés (par
        /// exemple Ctrl + Maj + V colle sans mise en forme dans beaucoup de logiciels).
        /// </summary>
        private static void RaccourciCtrl(ushort touche)
        {
            InjecteurClavier.Enfoncer(NativeMethods.VK_CONTROL);
            InjecteurClavier.Enfoncer(touche);
            InjecteurClavier.Relacher(touche);
            InjecteurClavier.Relacher(NativeMethods.VK_CONTROL);
        }

        // ───────────────────────── 1. La photo et l'écran noir ─────────────────────────

        /// <summary>
        /// Prend la photo et remplit l'écran noir. Renvoie faux si c'est impossible.
        /// Toutes les positions sont calculées en pixels d'écran, puis converties par le calque.
        /// </summary>
        private bool ConstruireEcran()
        {
            OverlayWindow calque = Gestionnaire.Calque;
            if (!calque.Pret)
            {
                return false;
            }

            Parametres p = Parametres.Courants;
            double largeur = Gestionnaire.LargeurEcran;
            double hauteur = Gestionnaire.HauteurEcran;
            double taille = p.TailleIcones;

            // ── La photo : le carré qui englobe le cercle, centré sur P. Près d'un bord de
            //    l'écran, le carré est décalé pour rester entièrement sur l'écran (P n'est alors
            //    plus au centre de la photo, et la croix non plus).
            int cote = (int)Math.Round(2 * p.RayonCercle);
            cote = Math.Min(cote, (int)Math.Min(largeur, hauteur));
            int ox = (int)Borner(Math.Round(_px - cote / 2.0), 0, largeur - cote);
            int oy = (int)Borner(Math.Round(_py - cote / 2.0), 0, hauteur - cote);

            BitmapSource photo = CaptureEcran.Capturer(ox, oy, cote, cote);
            if (photo == null)
            {
                return false;
            }

            calque.PreparerEcranChoix();
            Canvas ecran = calque.EcranChoix;

            // ── La photo agrandie, au centre de l'écran.
            double cxEcran = largeur / 2;
            double cyEcran = hauteur / 2;
            double coteAgrandi = cote * Zoom;
            double gauche = cxEcran - coteAgrandi / 2;
            double haut = cyEcran - coteAgrandi / 2;

            Image image = new Image();
            image.Source = photo;
            image.Stretch = Stretch.Fill;
            // « Plus proche voisin » : chaque pixel devient un carré de 2 × 2 bien net, au lieu
            // d'être adouci (flou). Le texte agrandi reste ainsi lisible.
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Point coin = calque.VersWpf(gauche, haut);
            image.Width = calque.LongueurVersWpf(coteAgrandi);
            image.Height = image.Width;
            Canvas.SetLeft(image, coin.X);
            Canvas.SetTop(image, coin.Y);
            ecran.Children.Add(image);

            // Un fin cadre gris autour de la photo.
            Rectangle cadre = new Rectangle();
            cadre.Stroke = Brushes.Gray;
            cadre.StrokeThickness = 2;
            cadre.Width = image.Width + 4;
            cadre.Height = image.Height + 4;
            Canvas.SetLeft(cadre, coin.X - 2);
            Canvas.SetTop(cadre, coin.Y - 2);
            ecran.Children.Add(cadre);

            // ── La croix, à la place du curseur dans la photo agrandie.
            DessinerCroix(calque, ecran, gauche + (_px - ox) * Zoom, haut + (_py - oy) * Zoom);

            // ── La couronne : huit emplacements, DEUX PAR CÔTÉ de la photo (demande de
            //    l'utilisateur du 26/10/2026, pour laisser une place au défilement de l'étape 8).
            //    Sur chaque côté, les deux boutons sont séparés de [Espace] pixels et centrés
            //    sur le milieu du côté. Ils sont à [Espace] pixels de la photo, sauf si l'écran
            //    est trop petit (en haut et en bas sur un écran 1080p) : ils se rapprochent alors
            //    de la photo pour rester entièrement sur l'écran.
            double demiPhoto = coteAgrandi / 2;
            double distanceVoulue = demiPhoto + Espace + taille / 2;
            double distanceX = Math.Min(distanceVoulue, largeur / 2 - taille / 2 - 10);
            double distanceY = Math.Min(distanceVoulue, hauteur / 2 - taille / 2 - 10);
            double decalage = taille + Espace;   // du milieu du côté au centre de chaque bouton

            // En haut, les actions spéciales occupent le milieu de la ligne : les emplacements
            // 0 et 7 sont repoussés juste après elles, au même écart [Espace].
            int nombreSpeciales = DispositionSpeciales.Length;
            double decalageHaut = (nombreSpeciales + 1) / 2.0 * (taille + Espace);

            for (int i = 0; i < Disposition.Length; i++)
            {
                double x;
                double y;
                PlaceEmplacement(i, cxEcran, cyEcran, distanceX, distanceY, decalage, decalageHaut, out x, out y);
                AjouterBouton(calque, ecran, Disposition[i], x, y, taille);
            }

            // ── Les actions spéciales : tout en haut, centrées horizontalement.
            for (int i = 0; i < nombreSpeciales; i++)
            {
                double x = cxEcran + (i - (nombreSpeciales - 1) / 2.0) * (taille + Espace);
                AjouterBouton(calque, ecran, DispositionSpeciales[i], x, cyEcran - distanceY, taille);
            }

            // ── Les modificateurs : une colonne de trois, collée au bord gauche de l'écran
            //    (demande du 26/10/2026), à [Espace] pixels du bord, et [Espace] pixels entre
            //    eux. Un modificateur déjà actif (par exemple Ctrl choisi avant un passage par
            //    la loupe) est dessiné allumé.
            double xModificateurs = Espace + taille / 2;
            double ecart = (hauteur - taille * 3) / 3;
            int nombre = DispositionModificateurs.Length;
            for (int i = 0; i < nombre; i++)
            {
                double y = cyEcran + (i - (nombre - 1) / 2.0) * ecart;
                ActionChoix? action = DispositionModificateurs[i];
                BoutonRegard bouton = AjouterBouton(calque, ecran, action, xModificateurs, y, taille);
                if (action.HasValue && EstModificateur(action.Value))
                {
                    bouton.DefinirAllume(Modificateurs.EstActif(VersModificateur(action.Value)));
                }
            }

            // ── L'icône « retour », à droite
            double xRetour = largeur - taille;
            AjouterBouton(calque, ecran, ActionChoix.Retour, xRetour, cyEcran, taille);

            // ── Le petit cercle du regard, ajouté EN DERNIER : il est ainsi dessiné par-dessus
            //    la photo et les boutons. Il apparaîtra à la première mesure sur l'écran.
            _cercleRegardFond = new Ellipse();
            _cercleRegardFond.Stroke = Brushes.Black;
            _cercleRegardFond.StrokeThickness = 4;
            _cercleRegardFond.Opacity = 0.6;
            _cercleRegardFond.Visibility = Visibility.Collapsed;
            _cercleRegard = new Ellipse();
            _cercleRegard.Stroke = Brushes.LimeGreen;
            _cercleRegard.StrokeThickness = 2;
            _cercleRegard.Visibility = Visibility.Collapsed;
            ecran.Children.Add(_cercleRegardFond);
            ecran.Children.Add(_cercleRegard);
            _cercleRegardPlace = false;

            calque.AfficherEcranChoix();
            _etape = Etape.Choix;
            App.Log("Choix d'action : photo de " + cote + " × " + cote + " px en (" + ox + ", " + oy + ")");
            return true;
        }

        /// <summary>
        /// Déplace le petit cercle du regard : il suit le regard en douceur ([Lissage], voir
        /// Lisseur.cs), comme le cercle du mode mouvement. Son rayon est la moitié
        /// de [RayonCercle]. Regard perdu ou hors de l'écran : il reste où il est.
        /// </summary>
        private void DeplacerCercleRegard(EtatRegard regard, Parametres p)
        {
            if (_cercleRegard == null || !regard.SurEcran)
            {
                return;
            }

            if (!_cercleRegardPlace)
            {
                _lisse.Placer(regard.X, regard.Y);
                _cercleRegardPlace = true;
            }
            _lisse.Suivre(regard.X, regard.Y, regard.MaintenantMs);

            OverlayWindow calque = Gestionnaire.Calque;
            Point centre = calque.VersWpf(_lisse.X, _lisse.Y);
            double rayon = calque.LongueurVersWpf(p.RayonCercle / 2);
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

        /// <summary>
        /// Crée le bouton d'une action (ou un emplacement vide si action est null), le place
        /// sur l'écran noir, et le renvoie.
        /// </summary>
        private BoutonRegard AjouterBouton(OverlayWindow calque, Canvas ecran, ActionChoix? action,
                                           double x, double y, double taille)
        {
            BoutonRegard bouton = action.HasValue ? CreerBouton(action.Value) : BoutonRegard.Vide(LibelleVide);
            bouton.FacteurZone = FacteurZone;
            bouton.Placer(calque, ecran, x, y, taille);
            _boutons.Add(bouton);
            _actions.Add(action);
            return bouton;
        }

        /// <summary>
        /// Centre de l'emplacement numéro « numero » de la couronne, en pixels d'écran.
        /// Deux emplacements par côté de la photo, dans le sens des aiguilles d'une montre en
        /// partant du haut :
        ///         7   0
        ///      6         1
        ///         photo
        ///      5         2
        ///         4   3
        /// distanceX, distanceY : écart entre le centre de l'écran et la ligne des boutons des
        /// côtés gauche/droit (X) et haut/bas (Y) ; decalage : écart entre le milieu d'un côté
        /// et le centre de chacun de ses deux boutons ; decalageHaut : le même écart pour le
        /// côté du haut, plus grand, car les actions spéciales sont au milieu.
        /// </summary>
        private static void PlaceEmplacement(int numero, double cx, double cy, double distanceX,
                                             double distanceY, double decalage, double decalageHaut,
                                             out double x, out double y)
        {
            switch (numero)
            {
                case 0: x = cx + decalageHaut; y = cy - distanceY; break;
                case 1: x = cx + distanceX; y = cy - decalage; break;
                case 2: x = cx + distanceX; y = cy + decalage; break;
                case 3: x = cx + decalage; y = cy + distanceY; break;
                case 4: x = cx - decalage; y = cy + distanceY; break;
                case 5: x = cx - distanceX; y = cy + decalage; break;
                case 6: x = cx - distanceX; y = cy - decalage; break;
                default: x = cx - decalageHaut; y = cy - distanceY; break;
            }
        }

        /// <summary>Vrai pour les actions des emplacements de gauche (Ctrl, Maj, Alt).</summary>
        private static bool EstModificateur(ActionChoix action)
        {
            return action == ActionChoix.Ctrl || action == ActionChoix.Maj || action == ActionChoix.Alt;
        }

        /// <summary>Le modificateur qui correspond à l'action Ctrl, Maj ou Alt.</summary>
        private static Modificateur VersModificateur(ActionChoix action)
        {
            switch (action)
            {
                case ActionChoix.Ctrl:
                    return Modificateur.Ctrl;
                case ActionChoix.Maj:
                    return Modificateur.Maj;
                default:
                    return Modificateur.Alt;
            }
        }

        /// <summary>
        /// Le symbole, la pastille et le libellé de chaque action.
        /// Symboles de la police Windows (Segoe MDL2 Assets / Segoe Fluent Icons) :
        ///   E962 = souris, E71E = loupe, E7C2 = déplacer (quatre flèches), E72B = flèche « retour »,
    ///   EC8F = défilement (flèches haut et bas ; symbole à vérifier à l'écran).
        /// Les trois clics partagent le symbole de la souris, les trois glisser-déposer celui des
        /// quatre flèches ; la pastille les distingue (G = gauche, D = droit, A = android).
        /// Les modificateurs affichent simplement leur nom (« Ctrl », « Maj », « Alt »).
        /// </summary>
        private static BoutonRegard CreerBouton(ActionChoix action)
        {
            switch (action)
            {
                case ActionChoix.ClicGauche:
                    return new BoutonRegard("", "G", "Clic gauche");
                case ActionChoix.ClicDroit:
                    return new BoutonRegard("", "D", "Clic droit");
                case ActionChoix.DoubleClic:
                    return new BoutonRegard("", "2×", "Double clic");
                case ActionChoix.Loupe:
                    return new BoutonRegard("\uE71E", null, "Loupe");
                case ActionChoix.GlisserGauche:
                    return new BoutonRegard("\uE7C2", "G", "Glisser-déposer gauche");
                case ActionChoix.GlisserDroit:
                    return new BoutonRegard("\uE7C2", "D", "Glisser-déposer droit");
                case ActionChoix.GlisserAndroid:
                    return new BoutonRegard("\uE7C2", "A", "Glisser-déposer android (pause avant de déplacer)");
                case ActionChoix.Defilement:
                    return new BoutonRegard("\uEC8F", null, "Défilement");
                case ActionChoix.Ctrl:
                    return BoutonRegard.Texte("Ctrl", "Ctrl (activer / désactiver)");
                case ActionChoix.Maj:
                    return BoutonRegard.Texte("Maj", "Maj (activer / désactiver)");
                case ActionChoix.Alt:
                    return BoutonRegard.Texte("Alt", "Alt (activer / désactiver)");
                // E8C8 = copier, E8C6 = ciseaux (couper), E77F = presse-papiers (coller).
                case ActionChoix.Copier:
                    return new BoutonRegard("\uE8C8", null, "Copier (Ctrl + C)");
                case ActionChoix.Couper:
                    return new BoutonRegard("\uE8C6", null, "Couper (Ctrl + X)");
                case ActionChoix.Coller:
                    return new BoutonRegard("\uE77F", null, "Coller ici (clic, puis Ctrl + V)");
                default:
                    return new BoutonRegard("", null, "Retour");
            }
        }

        /// <summary>
        /// Dessine une croix centrée sur (x, y) en pixels d'écran : un trait noir épais dessous,
        /// un trait blanc fin dessus, pour qu'elle se voie sur tous les fonds.
        /// </summary>
        private static void DessinerCroix(OverlayWindow calque, Canvas ecran, double x, double y)
        {
            Point centre = calque.VersWpf(x, y);
            double demi = calque.LongueurVersWpf(20);

            AjouterTrait(ecran, centre.X - demi, centre.Y, centre.X + demi, centre.Y, Brushes.Black, 5);
            AjouterTrait(ecran, centre.X, centre.Y - demi, centre.X, centre.Y + demi, Brushes.Black, 5);
            AjouterTrait(ecran, centre.X - demi, centre.Y, centre.X + demi, centre.Y, Brushes.White, 2);
            AjouterTrait(ecran, centre.X, centre.Y - demi, centre.X, centre.Y + demi, Brushes.White, 2);
        }

        private static void AjouterTrait(Canvas ecran, double x1, double y1, double x2, double y2,
                                         Brush couleur, double epaisseur)
        {
            Line trait = new Line();
            trait.X1 = x1;
            trait.Y1 = y1;
            trait.X2 = x2;
            trait.Y2 = y2;
            trait.Stroke = couleur;
            trait.StrokeThickness = epaisseur;
            ecran.Children.Add(trait);
        }

        private static double Borner(double valeur, double min, double max)
        {
            return Math.Max(min, Math.Min(max, valeur));
        }
    }
}
