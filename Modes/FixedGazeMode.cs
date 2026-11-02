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
    ///
    /// Regard juste à côté de l'écran (EtatRegard.PresDeLEcran, 02/11/2026) : il est ramené
    /// au bord le plus proche au lieu de faire quitter ce mode. Plus loin hors de l'écran :
    /// retour au mode mouvement, comme avant.
    /// </summary>
    internal sealed class FixedGazeMode : Mode
    {
        /// <summary>Les zones où peut se trouver le regard.</summary>
        private enum Zone
        {
            Circle,
            Icon,
            Outside
        }

        // Nombre de mesures de suite qu'il faut dans une nouvelle zone pour y passer.
        private const int MeasuresToChange = 2;

        /// <summary>
        /// Zone d'activation de l'icône d'options, par rapport à l'icône dessinée : 50 % plus
        /// grande (demandes de l'utilisateur du 28/10/2026, 1,2, puis du 02/11/2026, 1,5).
        /// Le dessin, lui, ne change pas. La zone empiète un peu plus sur le cercle, du côté
        /// de l'icône : là, c'est l'icône qui l'emporte (voir ZoneDe).
        /// </summary>
        private const double IconZoneFactor = 1.5;

        // Le point fixé, en pixels d'écran.
        private readonly double _px;
        private readonly double _py;

        // Centre du cercle tel qu'il est dessiné : il glisse vers P (voir Entrer).
        private readonly Smoother _smoothed = new Smoother();

        // Centre de l'icône d'options, en pixels d'écran (calculé dans Entrer).
        private double _ix;
        private double _iy;

        // Zone retenue, et zone « candidate » vue sur les dernières mesures.
        private Zone _zone = Zone.Circle;
        private Zone _candidateShape = Zone.Circle;
        private int _candidateShapeMeasures;
        private long _lastNumber = -1;

        // Instants (ms) où l'on est entré dans le cercle ou sur l'icône ; -1 = minuteur arrêté.
        private double _circleStartMs = -1;
        private double _iconStartMs = -1;

        public FixedGazeMode(double x, double y)
        {
            _px = x;
            _py = y;
        }

        public override string Name
        {
            get { return "Regard fixe"; }
        }

        public override void Enter(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // Le cercle dessiné part de là où il est déjà (cercle du mode mouvement), sinon de P.
            if (Manager.Layer.CircleShown)
            {
                _smoothed.Place(Manager.Layer.CircleX, Manager.Layer.CircleY);
            }
            else
            {
                _smoothed.Place(_px, _py);
            }

            PlaceIcon(p.CircleRadiusPx, p.IconSizePx);

            // Le regard est fixé : le curseur le rejoint maintenant.
            PlaceCursor();

            App.Log("Regard fixe en (" + _px.ToString("F0") + ", " + _py.ToString("F0") + ")");
        }

        public override void Update(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;
            double now = gaze.NowMs;

            // Regard franchement hors de l'écran : on est forcément sorti du cercle et de
            // l'icône. Juste à côté de l'écran (EtatRegard.PresDeLEcran), on reste : dans un
            // coin, quelques mesures qui débordent ne doivent pas tout annuler (02/11/2026).
            if (gaze.Available && !gaze.NearScreen)
            {
                ReturnToMovement(false);
                return;
            }

            if (!gaze.Available)
            {
                // Regard perdu (yeux fermés…) : on reste en regard fixe, mais les minuteurs
                // repartent de zéro, pour ne jamais cliquer sur un regard qu'on ne voit plus.
                _circleStartMs = -1;
                _iconStartMs = -1;
            }
            else if (gaze.Number != _lastNumber)
            {
                // Nouvelle mesure : dans quelle zone est-elle ?
                _lastNumber = gaze.Number;
                // Position ramenée dans l'écran : identique au vrai regard sur l'écran ;
                // juste à côté, c'est le point du bord le plus proche (même règle que la
                // fixation, voir DetecteurFixation). Ainsi, une icône collée au bord de
                // l'écran se vise aussi en regardant un peu au-delà du bord.
                ChangeZoneIfNeeded(ZoneOf(gaze.XScreen, gaze.YScreen, p));

                if (_zone == Zone.Outside)
                {
                    ReturnToMovement(false);
                    return;
                }
            }

            // Les minuteurs de la zone retenue (seulement si le regard est disponible).
            if (gaze.Available)
            {
                if (_zone == Zone.Circle)
                {
                    _iconStartMs = -1;
                    if (_circleStartMs < 0)
                    {
                        _circleStartMs = now;
                    }
                }
                else
                {
                    _circleStartMs = -1;
                    if (_iconStartMs < 0)
                    {
                        _iconStartMs = now;
                    }
                }
            }

            double clickProgress = 0;
            if (p.DirectClick && _circleStartMs >= 0)
            {
                clickProgress = (now - _circleStartMs) / (p.DirectClickDelay * 1000.0);
            }

            double iconProgress = 0;
            if (_iconStartMs >= 0)
            {
                // Délai propre à l'icône d'options, plus court que celui des autres boutons.
                iconProgress = (now - _iconStartMs) / (p.OptionsIconDelay * 1000.0);
            }

            // Une action est-elle arrivée à son terme ?
            if (clickProgress >= 1)
            {
                DirectClick();
                return;
            }
            if (iconProgress >= 1)
            {
                ActivateIcon();
                return;
            }

            // Le dessin.
            // Le cercle glisse en douceur vers P (voir Lisseur.cs ; quelques tics suffisent).
            _smoothed.Follow(_px, _py, gaze.NowMs);
            Manager.Layer.ShowCircle(_smoothed.X, _smoothed.Y, p.CircleRadiusPx, true);
            if (clickProgress > 0)
            {
                Manager.Layer.ShowRing(_smoothed.X, _smoothed.Y, p.CircleRadiusPx, clickProgress);
            }
            else
            {
                Manager.Layer.HideRing();
            }
            Manager.Layer.ShowIcon(_ix, _iy, p.IconSizePx, iconProgress,
                                              p.ShowLabels && _zone == Zone.Icon);
        }

        public override void Quit()
        {
            // Le cercle reste affiché : le mode mouvement le reprend là où il est.
            Manager.Layer.HideRing();
            Manager.Layer.HideIcon();
        }

        // ───────────────────────── Actions ─────────────────────────

        /// <summary>
        /// Clic gauche direct sur le point fixé. Le calque laisse passer les clics : inutile de
        /// le cacher avant. Le curseur est replacé sur P par sécurité (il a pu être bougé).
        /// </summary>
        private void DirectClick()
        {
            PlaceCursor();
            MouseInjector.Press(MouseSide.Left);
            MouseInjector.Release(MouseSide.Left);
            App.Log("Clic direct en (" + _px.ToString("F0") + ", " + _py.ToString("F0") + ")");
            ReturnToMovement(true);
        }

        /// <summary>
        /// Activation de l'icône d'options : on passe au mode choix d'action (étape 3), qui
        /// photographie la zone autour de P et propose les actions.
        /// Si l'option [LoupeAvantAction] est activée, on passe d'abord par le mode loupe
        /// (étape 5), qui affine la position puis ouvre lui-même le choix d'action.
        /// Cette option se règle dans l'onglet « Loupe, glisser » du mode paramètres (étape 7).
        /// </summary>
        private void ActivateIcon()
        {
            PlaceCursor();
            if (AppSettings.Current.MagnifierBeforeAction)
            {
                // « false » : l'icône « retour » de la loupe ramènera au mode mouvement.
                Manager.ChangeMode(new MagnifierMode(_px, _py, false));
            }
            else
            {
                Manager.ChangeMode(new ActionChoiceMode(_px, _py));
            }
        }

        /// <summary>
        /// Retour au mode mouvement. ignorerIci = vrai après une action : une nouvelle fixation
        /// au même endroit ne sera pas reconnue tant que le regard n'en sera pas sorti.
        /// </summary>
        private void ReturnToMovement(bool ignoreHere)
        {
            Manager.ChangeMode(ignoreHere ? new MovementMode(_px, _py) : new MovementMode());
        }

        // ───────────────────────── Calculs ─────────────────────────

        /// <summary>
        /// Place l'icône en diagonale, du côté du cercle tourné vers le centre de l'écran.
        /// Le coin de l'icône le plus proche touche le cercle : à 45°, un point du cercle est
        /// décalé de rayon ÷ √2 sur chaque axe, auquel on ajoute la moitié du côté de l'icône.
        /// L'icône est ensuite gardée entièrement sur l'écran.
        /// </summary>
        private void PlaceIcon(double radius, double size)
        {
            double width = Manager.ScreenWidth;
            double height = Manager.ScreenHeight;

            // Sens : +1 = vers la droite / le bas, −1 = vers la gauche / le haut.
            // « <= » : au centre exact de l'écran, l'icône va en bas à droite.
            double directionX = _px <= width / 2 ? 1 : -1;
            double directionY = _py <= height / 2 ? 1 : -1;

            double offset = radius / Math.Sqrt(2) + size / 2;
            _ix = Clamp(_px + directionX * offset, size / 2, width - size / 2);
            _iy = Clamp(_py + directionY * offset, size / 2, height - size / 2);
        }

        /// <summary>Dans quelle zone se trouve ce point du regard ?</summary>
        private Zone ZoneOf(double x, double y, AppSettings p)
        {
            // La zone d'activation de l'icône est un carré [FacteurZoneIcone] fois plus grand
            // que l'icône dessinée. Elle déborde un peu sur le cercle, près du coin qui le
            // touche : là, c'est l'icône qui l'emporte (elle est testée en premier).
            double halfSide = p.IconSizePx * IconZoneFactor / 2;
            if (Math.Abs(x - _ix) <= halfSide && Math.Abs(y - _iy) <= halfSide)
            {
                return Zone.Icon;
            }

            double dx = x - _px;
            double dy = y - _py;
            if (Math.Sqrt(dx * dx + dy * dy) <= p.CircleRadiusPx)
            {
                return Zone.Circle;
            }

            return Zone.Outside;
        }

        /// <summary>
        /// Change de zone seulement si la nouvelle zone est confirmée par
        /// [MesuresPourChanger] mesures de suite. Une mesure isolée est ignorée.
        /// </summary>
        private void ChangeZoneIfNeeded(Zone view)
        {
            if (view == _zone)
            {
                _candidateShapeMeasures = 0;
                return;
            }

            if (view == _candidateShape && _candidateShapeMeasures > 0)
            {
                _candidateShapeMeasures++;
            }
            else
            {
                _candidateShape = view;
                _candidateShapeMeasures = 1;
            }

            if (_candidateShapeMeasures >= MeasuresToChange)
            {
                _zone = view;
                _candidateShapeMeasures = 0;
            }
        }

        private void PlaceCursor()
        {
            NativeMethods.SetCursorPos((int)Math.Round(_px), (int)Math.Round(_py));
        }

        private static double Clamp(double val, double min, double max)
        {
            return Math.Max(min, Math.Min(max, val));
        }
    }
}
