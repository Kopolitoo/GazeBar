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
    ///     (400 × 400 px) et une croix à la place du curseur. Autour : la couronne de sept
    ///     emplacements d'action. À droite : l'icône « retour ».
    ///     Regarder une icône pendant [DelaiActivation] l'active (son anneau se remplit).
    ///
    ///  3. EXÉCUTION. L'écran noir est caché, on attend [AttenteMs] qu'il ait disparu, puis le
    ///     clic est injecté au curseur. Ainsi, l'application visée est bien visible (et à jour)
    ///     au moment du clic. On revient ensuite au mode mouvement.
    ///
    /// L'icône « retour » ramène au mode mouvement sans rien faire.
    ///
    /// Ce qui viendra plus tard (cahier des charges) :
    ///   - étape 5 : les trois modificateurs Ctrl, Maj, Alt à gauche, l'appui long et le
    ///     glisser-déposer dans la couronne ;
    ///   - étapes 7 et 8 : la loupe et le défilement dans la couronne ;
    ///   - étape 9 : le bouton de configuration de la disposition.
    /// Pour l'instant, la couronne ne contient donc que trois actions sur sept emplacements.
    /// </summary>
    internal sealed class ModeChoixAction : Mode
    {
        /// <summary>Les actions possibles depuis ce mode.</summary>
        private enum ActionChoix
        {
            ClicGauche,
            ClicDroit,
            DoubleClic,
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

        /// <summary>Nombre de mesures de suite qu'il faut sur un nouveau bouton pour le viser (comme en regard fixe).</summary>
        private const int MesuresPourChanger = 2;

        /// <summary>
        /// Disposition de la couronne : les sept emplacements, en partant du haut et en tournant
        /// dans le sens des aiguilles d'une montre. null = emplacement vide (actions des étapes
        /// suivantes). À l'étape 9, cette disposition sera modifiable et enregistrée avec les
        /// paramètres ; pour l'instant elle est écrite ici.
        ///   0 : en haut                  → clic gauche (l'action la plus courante)
        ///   1 : en haut à droite         → clic droit
        ///   6 : en haut à gauche         → double clic
        /// </summary>
        private static readonly ActionChoix?[] Disposition =
        {
            ActionChoix.ClicGauche,
            ActionChoix.ClicDroit,
            null,
            null,
            null,
            null,
            ActionChoix.DoubleClic
        };

        // Le point visé P (position du curseur), en pixels d'écran.
        private readonly double _px;
        private readonly double _py;

        private Etape _etape;
        private double _debutEtapeMs;

        // Les boutons affichés, et l'action de chacun (même ordre).
        private readonly List<BoutonRegard> _boutons = new List<BoutonRegard>();
        private readonly List<ActionChoix> _actions = new List<ActionChoix>();

        // Le bouton regardé (-1 = aucun), et le « candidat » vu sur les dernières mesures.
        private int _vise = -1;
        private int _candidat = -1;
        private int _mesuresCandidat;
        private long _dernierNumero = -1;

        // Instant (ms) où le regard est arrivé sur le bouton visé ; -1 = minuteur arrêté.
        private double _debutViseMs = -1;

        // L'action choisie, en attente d'exécution (étape Execution).
        private ActionChoix _actionChoisie;

        public ModeChoixAction(double x, double y)
        {
            _px = x;
            _py = y;
        }

        public override string Nom
        {
            get { return "Choix d'action"; }
        }

        public override void Entrer(EtatRegard regard)
        {
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
        }

        // ───────────────────────── 2. Le choix ─────────────────────────

        private void ActualiserChoix(EtatRegard regard)
        {
            Parametres p = Parametres.Courants;
            double maintenant = regard.MaintenantMs;

            if (!regard.Disponible)
            {
                // Regard perdu : on ne vise plus rien, pour ne jamais activer un bouton
                // qu'on ne regarde plus.
                _debutViseMs = -1;
            }
            else if (regard.Numero != _dernierNumero)
            {
                // Nouvelle mesure : sur quel bouton est-elle ? (hors écran → aucun)
                _dernierNumero = regard.Numero;
                int vu = regard.HorsEcran ? -1 : BoutonSous(regard.X, regard.Y);
                ChangerViseSiBesoin(vu);
            }

            // Le minuteur du bouton visé.
            if (regard.Disponible && _vise >= 0 && _debutViseMs < 0)
            {
                _debutViseMs = maintenant;
            }
            if (_vise < 0)
            {
                _debutViseMs = -1;
            }

            double progression = 0;
            if (_vise >= 0 && _debutViseMs >= 0)
            {
                progression = (maintenant - _debutViseMs) / (p.DelaiActivation * 1000.0);
            }

            if (progression >= 1)
            {
                Activer(_actions[_vise]);
                return;
            }

            // Le dessin des boutons : anneau sur le bouton visé seulement.
            for (int i = 0; i < _boutons.Count; i++)
            {
                bool survol = i == _vise;
                _boutons[i].Dessiner(survol ? progression : 0, survol, survol && p.AfficherLibelles);
            }
        }

        /// <summary>
        /// Change de bouton visé seulement si le nouveau est confirmé par
        /// [MesuresPourChanger] mesures de suite. Une mesure isolée est ignorée.
        /// Changer de bouton remet le minuteur à zéro.
        /// </summary>
        private void ChangerViseSiBesoin(int vu)
        {
            if (vu == _vise)
            {
                _mesuresCandidat = 0;
                return;
            }

            if (vu == _candidat && _mesuresCandidat > 0)
            {
                _mesuresCandidat++;
            }
            else
            {
                _candidat = vu;
                _mesuresCandidat = 1;
            }

            if (_mesuresCandidat >= MesuresPourChanger)
            {
                _vise = vu;
                _mesuresCandidat = 0;
                _debutViseMs = -1;
            }
        }

        /// <summary>Le numéro du bouton sous ce point du regard, ou -1.</summary>
        private int BoutonSous(double x, double y)
        {
            for (int i = 0; i < _boutons.Count; i++)
            {
                if (_boutons[i].Contient(x, y))
                {
                    return i;
                }
            }
            return -1;
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
            }

            App.Log(_actionChoisie + " en (" + _px.ToString("F0") + ", " + _py.ToString("F0") + ")");

            // Retour au mode mouvement (choix de l'utilisateur). Une nouvelle fixation au même
            // endroit est ignorée tant que le regard n'en est pas sorti, comme après un clic direct.
            Gestionnaire.ChangerMode(new ModeMouvement(_px, _py));
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

            // ── La couronne : sept emplacements sur un cercle autour de la photo.
            //    Rayon : assez grand pour que les icônes ne touchent pas les coins de la photo
            //    (demi-diagonale de la photo + demi-icône + marge), mais sans sortir de l'écran.
            double rayonVoulu = coteAgrandi / 2 * Math.Sqrt(2) + taille / 2 + 20;
            double rayonMax = Math.Min(largeur, hauteur) / 2 - taille / 2 - 10;
            double rayonCouronne = Math.Min(rayonVoulu, rayonMax);

            for (int i = 0; i < Disposition.Length; i++)
            {
                if (!Disposition[i].HasValue)
                {
                    continue;   // emplacement vide pour l'instant
                }

                // Angle : -90° = en haut, puis un septième de tour par emplacement.
                double angle = (-90 + i * 360.0 / Disposition.Length) * Math.PI / 180;
                double x = cxEcran + rayonCouronne * Math.Cos(angle);
                double y = cyEcran + rayonCouronne * Math.Sin(angle);
                AjouterBouton(calque, ecran, Disposition[i].Value, x, y, taille);
            }

            // ── L'icône « retour », à droite : à mi-chemin entre la couronne et le bord droit.
            double xRetour = (cxEcran + rayonCouronne + taille / 2 + largeur) / 2;
            xRetour = Math.Min(xRetour, largeur - taille / 2 - 10);
            AjouterBouton(calque, ecran, ActionChoix.Retour, xRetour, cyEcran, taille);

            calque.AfficherEcranChoix();
            _etape = Etape.Choix;
            App.Log("Choix d'action : photo de " + cote + " × " + cote + " px en (" + ox + ", " + oy + ")");
            return true;
        }

        /// <summary>Crée le bouton d'une action et le place sur l'écran noir.</summary>
        private void AjouterBouton(OverlayWindow calque, Canvas ecran, ActionChoix action,
                                   double x, double y, double taille)
        {
            BoutonRegard bouton = CreerBouton(action);
            bouton.Placer(calque, ecran, x, y, taille);
            _boutons.Add(bouton);
            _actions.Add(action);
        }

        /// <summary>
        /// Le symbole, la pastille et le libellé de chaque action.
        /// Symboles de la police Windows (Segoe MDL2 Assets / Segoe Fluent Icons) :
        ///   E962 = souris, E72B = flèche « retour ».
        /// Les trois clics partagent le symbole de la souris ; la pastille les distingue.
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
