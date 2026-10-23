using System;

namespace GazeBar
{
    /// <summary>
    /// MODE REGARD FIXE (étape 2 du cahier des charges).
    ///
    /// On y arrive depuis le mode mouvement, quand le regard s'est fixé sur un point P.
    ///  - Le cercle se fige autour de P, avec un bord blanc. Il part de là où était le cercle
    ///    du mode mouvement et glisse jusqu'à P en quelques tics : pas de saut visuel.
    ///  - Une icône d'options est collée au cercle, du côté tourné vers le centre de l'écran
    ///    (curseur en haut à gauche → icône en bas à droite, etc. ; au centre : en bas à droite).
    ///  - Curseur : il est placé sur P dès l'entrée dans ce mode. C'est le seul moment où il
    ///    rejoint le regard quand [SuiviSouris] est désactivé (choix de l'utilisateur du
    ///    22/10/2026 ; le cahier des charges prévoyait de le placer à l'activation de l'icône).
    ///
    /// Ensuite, selon l'endroit où l'on regarde :
    ///  - dans le cercle : si [ClicDirect] est activé (non par défaut), l'anneau se remplit pendant
    ///    [DelaiClicDirect] ; plein, un clic gauche est fait sur P, puis retour au mode
    ///    mouvement. Regarder l'icône remet ce délai à zéro ;
    ///  - sur l'icône : son anneau se remplit pendant [DelaiIconeOptions] (plus court que
    ///    [DelaiActivation]) ; plein, l'icône
    ///    s'active et ouvre le mode choix d'action (étape 3), ou d'abord le mode loupe
    ///    (étape 5) si [LoupeAvantAction] est activé ;
    ///  - hors du cercle et de l'icône : cercle et icône disparaissent, retour au mode mouvement.
    ///
    /// Une seule mesure isolée dans une autre zone est ignorée (même tolérance que pour la
    /// fixation) : il faut deux mesures de suite pour changer de zone.
    /// </summary>
    internal sealed class ModeRegardFixe : Mode
    {
        /// <summary>Les zones où peut se trouver le regard.</summary>
        private enum Zone
        {
            Cercle,
            Icone,
            Dehors
        }

        // Nombre de mesures de suite qu'il faut dans une nouvelle zone pour y passer.
        private const int MesuresPourChanger = 2;

        // Le point fixé, en pixels d'écran.
        private readonly double _px;
        private readonly double _py;

        // Centre du cercle tel qu'il est dessiné : il glisse vers P (voir Entrer).
        private double _cx;
        private double _cy;

        // Centre de l'icône d'options, en pixels d'écran (calculé dans Entrer).
        private double _ix;
        private double _iy;

        // Zone retenue, et zone « candidate » vue sur les dernières mesures.
        private Zone _zone = Zone.Cercle;
        private Zone _candidate = Zone.Cercle;
        private int _mesuresCandidate;
        private long _dernierNumero = -1;

        // Instants (ms) où l'on est entré dans le cercle ou sur l'icône ; -1 = minuteur arrêté.
        private double _debutCercleMs = -1;
        private double _debutIconeMs = -1;

        public ModeRegardFixe(double x, double y)
        {
            _px = x;
            _py = y;
        }

        public override string Nom
        {
            get { return "Regard fixe"; }
        }

        public override void Entrer(EtatRegard regard)
        {
            Parametres p = Parametres.Courants;

            // Le cercle dessiné part de là où il est déjà (cercle du mode mouvement), sinon de P.
            if (Gestionnaire.Calque.CercleAffiche)
            {
                _cx = Gestionnaire.Calque.CercleX;
                _cy = Gestionnaire.Calque.CercleY;
            }
            else
            {
                _cx = _px;
                _cy = _py;
            }

            PlacerIcone(p.RayonCercle, p.TailleIcones);

            // Le regard est fixé : le curseur le rejoint maintenant.
            PlacerCurseur();

            App.Log("Regard fixe en (" + _px.ToString("F0") + ", " + _py.ToString("F0") + ")");
        }

        public override void Actualiser(EtatRegard regard)
        {
            Parametres p = Parametres.Courants;
            double maintenant = regard.MaintenantMs;

            // Regard hors de l'écran : on est forcément sorti du cercle et de l'icône.
            if (regard.Disponible && regard.HorsEcran)
            {
                RevenirAuMouvement(false);
                return;
            }

            if (!regard.Disponible)
            {
                // Regard perdu (yeux fermés…) : on reste en regard fixe, mais les minuteurs
                // repartent de zéro, pour ne jamais cliquer sur un regard qu'on ne voit plus.
                _debutCercleMs = -1;
                _debutIconeMs = -1;
            }
            else if (regard.Numero != _dernierNumero)
            {
                // Nouvelle mesure : dans quelle zone est-elle ?
                _dernierNumero = regard.Numero;
                ChangerZoneSiBesoin(ZoneDe(regard.X, regard.Y, p));

                if (_zone == Zone.Dehors)
                {
                    RevenirAuMouvement(false);
                    return;
                }
            }

            // Les minuteurs de la zone retenue (seulement si le regard est disponible).
            if (regard.Disponible)
            {
                if (_zone == Zone.Cercle)
                {
                    _debutIconeMs = -1;
                    if (_debutCercleMs < 0)
                    {
                        _debutCercleMs = maintenant;
                    }
                }
                else
                {
                    _debutCercleMs = -1;
                    if (_debutIconeMs < 0)
                    {
                        _debutIconeMs = maintenant;
                    }
                }
            }

            double progressionClic = 0;
            if (p.ClicDirect && _debutCercleMs >= 0)
            {
                progressionClic = (maintenant - _debutCercleMs) / (p.DelaiClicDirect * 1000.0);
            }

            double progressionIcone = 0;
            if (_debutIconeMs >= 0)
            {
                // Délai propre à l'icône d'options, plus court que celui des autres boutons.
                progressionIcone = (maintenant - _debutIconeMs) / (p.DelaiIconeOptions * 1000.0);
            }

            // Une action est-elle arrivée à son terme ?
            if (progressionClic >= 1)
            {
                ClicDirect();
                return;
            }
            if (progressionIcone >= 1)
            {
                ActiverIcone();
                return;
            }

            // Le dessin.
            // Le cercle glisse vers P d'une fraction [Lissage] à chaque tic (quelques tics suffisent).
            _cx += (_px - _cx) * p.Lissage;
            _cy += (_py - _cy) * p.Lissage;
            Gestionnaire.Calque.AfficherCercle(_cx, _cy, p.RayonCercle, true);
            if (progressionClic > 0)
            {
                Gestionnaire.Calque.AfficherAnneau(_cx, _cy, p.RayonCercle, progressionClic);
            }
            else
            {
                Gestionnaire.Calque.MasquerAnneau();
            }
            Gestionnaire.Calque.AfficherIcone(_ix, _iy, p.TailleIcones, progressionIcone,
                                              p.AfficherLibelles && _zone == Zone.Icone);
        }

        public override void Quitter()
        {
            // Le cercle reste affiché : le mode mouvement le reprend là où il est.
            Gestionnaire.Calque.MasquerAnneau();
            Gestionnaire.Calque.MasquerIcone();
        }

        // ───────────────────────── Actions ─────────────────────────

        /// <summary>
        /// Clic gauche direct sur le point fixé. Le calque laisse passer les clics : inutile de
        /// le cacher avant. Le curseur est replacé sur P par sécurité (il a pu être bougé).
        /// </summary>
        private void ClicDirect()
        {
            PlacerCurseur();
            MouseInjector.Press(MouseSide.Left);
            MouseInjector.Release(MouseSide.Left);
            App.Log("Clic direct en (" + _px.ToString("F0") + ", " + _py.ToString("F0") + ")");
            RevenirAuMouvement(true);
        }

        /// <summary>
        /// Activation de l'icône d'options : on passe au mode choix d'action (étape 3), qui
        /// photographie la zone autour de P et propose les actions.
        /// Si l'option [LoupeAvantAction] est activée, on passe d'abord par le mode loupe
        /// (étape 5), qui affine la position puis ouvre lui-même le choix d'action.
        /// En attendant la fenêtre des paramètres (étape 7), cette option se règle à la main
        /// dans %APPDATA%\GazeBar\parametres.json ("LoupeAvantAction": true).
        /// </summary>
        private void ActiverIcone()
        {
            PlacerCurseur();
            if (Parametres.Courants.LoupeAvantAction)
            {
                // « false » : l'icône « retour » de la loupe ramènera au mode mouvement.
                Gestionnaire.ChangerMode(new ModeLoupe(_px, _py, false));
            }
            else
            {
                Gestionnaire.ChangerMode(new ModeChoixAction(_px, _py));
            }
        }

        /// <summary>
        /// Retour au mode mouvement. ignorerIci = vrai après une action : une nouvelle fixation
        /// au même endroit ne sera pas reconnue tant que le regard n'en sera pas sorti.
        /// </summary>
        private void RevenirAuMouvement(bool ignorerIci)
        {
            Gestionnaire.ChangerMode(ignorerIci ? new ModeMouvement(_px, _py) : new ModeMouvement());
        }

        // ───────────────────────── Calculs ─────────────────────────

        /// <summary>
        /// Place l'icône en diagonale, du côté du cercle tourné vers le centre de l'écran.
        /// Le coin de l'icône le plus proche touche le cercle : à 45°, un point du cercle est
        /// décalé de rayon ÷ √2 sur chaque axe, auquel on ajoute la moitié du côté de l'icône.
        /// L'icône est ensuite gardée entièrement sur l'écran.
        /// </summary>
        private void PlacerIcone(double rayon, double taille)
        {
            double largeur = Gestionnaire.LargeurEcran;
            double hauteur = Gestionnaire.HauteurEcran;

            // Sens : +1 = vers la droite / le bas, −1 = vers la gauche / le haut.
            // « <= » : au centre exact de l'écran, l'icône va en bas à droite.
            double sensX = _px <= largeur / 2 ? 1 : -1;
            double sensY = _py <= hauteur / 2 ? 1 : -1;

            double decalage = rayon / Math.Sqrt(2) + taille / 2;
            _ix = Borner(_px + sensX * decalage, taille / 2, largeur - taille / 2);
            _iy = Borner(_py + sensY * decalage, taille / 2, hauteur - taille / 2);
        }

        /// <summary>Dans quelle zone se trouve ce point du regard ?</summary>
        private Zone ZoneDe(double x, double y, Parametres p)
        {
            double demiCote = p.TailleIcones / 2;
            if (Math.Abs(x - _ix) <= demiCote && Math.Abs(y - _iy) <= demiCote)
            {
                return Zone.Icone;
            }

            double dx = x - _px;
            double dy = y - _py;
            if (Math.Sqrt(dx * dx + dy * dy) <= p.RayonCercle)
            {
                return Zone.Cercle;
            }

            return Zone.Dehors;
        }

        /// <summary>
        /// Change de zone seulement si la nouvelle zone est confirmée par
        /// [MesuresPourChanger] mesures de suite. Une mesure isolée est ignorée.
        /// </summary>
        private void ChangerZoneSiBesoin(Zone vue)
        {
            if (vue == _zone)
            {
                _mesuresCandidate = 0;
                return;
            }

            if (vue == _candidate && _mesuresCandidate > 0)
            {
                _mesuresCandidate++;
            }
            else
            {
                _candidate = vue;
                _mesuresCandidate = 1;
            }

            if (_mesuresCandidate >= MesuresPourChanger)
            {
                _zone = vue;
                _mesuresCandidate = 0;
            }
        }

        private void PlacerCurseur()
        {
            NativeMethods.SetCursorPos((int)Math.Round(_px), (int)Math.Round(_py));
        }

        private static double Borner(double valeur, double min, double max)
        {
            return Math.Max(min, Math.Min(max, valeur));
        }
    }
}
