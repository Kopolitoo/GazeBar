using System;

namespace GazeBar
{
    /// <summary>
    /// LOGIQUE : reconnaît une FIXATION, c'est-à-dire un regard qui reste immobile sur un point.
    ///
    /// On lui donne le regard à chaque tic (méthode Ajouter). Il répond :
    ///   - Progression : de 0 à 1, où en est la fixation (sert à remplir l'anneau) ;
    ///   - Atteinte    : vrai quand le regard est resté immobile pendant [DelaiFixation] ;
    ///   - X, Y        : le point fixé (la moyenne des mesures de la fixation).
    ///
    /// Une mesure est « immobile » si elle passe DEUX tests :
    ///  1. le test de VITESSE (la méthode de l'utilisateur, branche socle-mode-adjust) :
    ///     la vitesse du regard depuis la mesure précédente, en pixels par seconde, reste
    ///     sous [VitesseMaxFixation]. On divise la distance par le VRAI temps écoulé entre les
    ///     deux mesures, et on ne compte que les NOUVELLES mesures : le résultat ne dépend donc
    ///     ni de la cadence de l'horloge (60 tics/s) ni de celle du PCEye (≈ 30 mesures/s) ;
    ///  2. le test de DISPERSION : la mesure reste à moins de [RayonFixation] du point
    ///     d'ancrage. Sans ce test, un regard qui glisse lentement (lecture d'une ligne…)
    ///     passerait le test de vitesse et finirait par déclencher une fixation à côté.
    ///
    /// Le point d'ancrage est la MOYENNE des mesures acceptées depuis le début de la fixation :
    /// il est plus juste que la première mesure seule, qui peut tomber un peu à côté.
    ///
    /// Tolérance : le PCEye renvoie parfois UNE mesure aberrante au milieu d'une fixation
    /// (un clignement, un reflet). Une mesure qui échoue est donc mise de côté une fois,
    /// sans rien remettre à zéro. Si la mesure suivante échoue aussi, c'est un vrai
    /// déplacement : la fixation repart de zéro à partir de cette nouvelle mesure.
    ///
    /// Le temps est mesuré en millisecondes réelles (DelaiFixation), et non en nombre de tics.
    ///
    /// Bords et coins de l'écran (02/11/2026) : une mesure juste hors de l'écran (voir
    /// EtatRegard.PresDeLEcran) n'interrompt plus la fixation ; elle est ramenée au bord.
    /// </summary>
    internal sealed class FixationDetector
    {
        // Le point d'ancrage = somme des mesures acceptées ÷ leur nombre.
        private double _sumX;
        private double _sumY;
        private int _number;

        // Instant de la première mesure de la fixation en cours, en millisecondes.
        private double _startMs;

        // Dernière mesure ACCEPTÉE (sert au calcul de la vitesse).
        private double _prevX;
        private double _prevY;
        private double _prevMs;

        // Numéro de la dernière mesure traitée (pour ne traiter chaque mesure qu'une fois).
        private long _lastNumber = -1;

        // Vrai si la dernière mesure a été mise de côté (une seule tolérée d'affilée).
        private bool _setAsideMeasure;

        /// <summary>Avancement de la fixation, entre 0 (rien) et 1 (fixation atteinte).</summary>
        public double Progress { get; private set; }

        /// <summary>Vrai quand le regard est resté immobile pendant [DelaiFixation].</summary>
        public bool Reached
        {
            get { return Progress >= 1; }
        }

        /// <summary>Le point fixé (point d'ancrage), en pixels d'écran.</summary>
        public double X
        {
            get { return _number > 0 ? _sumX / _number : 0; }
        }

        public double Y
        {
            get { return _number > 0 ? _sumY / _number : 0; }
        }

        /// <summary>
        /// Oublie tout : la prochaine mesure commencera une nouvelle fixation.
        /// À appeler quand le regard est perdu ou hors de l'écran.
        /// </summary>
        public void Reset()
        {
            _number = 0;
            _sumX = 0;
            _sumY = 0;
            _setAsideMeasure = false;
            Progress = 0;
        }

        /// <summary>
        /// À appeler à chaque tic avec la « photo » du regard.
        /// Seules les nouvelles mesures sur l'écran sont analysées ; entre deux mesures, on met
        /// seulement à jour la progression (pour que l'anneau se remplisse sans à-coups).
        /// </summary>
        public void Add(GazeState gaze)
        {
            // Regard perdu, ou franchement hors de l'écran : on oublie la fixation.
            // Regard juste à côté de l'écran (dans la bande de PresDeLEcran) : on le garde.
            if (!gaze.NearScreen)
            {
                Reset();
                return;
            }

            if (gaze.Number != _lastNumber)
            {
                _lastNumber = gaze.Number;

                // On analyse la position RAMENÉE DANS L'ÉCRAN (XEcran, YEcran). Sur l'écran,
                // c'est exactement le vrai regard. Juste à côté, c'est le point du bord le plus
                // proche : dans un coin, les mesures qui débordent sont ramenées dans le coin,
                // elles restent groupées et la fixation peut aboutir. Le point fixé est donc
                // toujours sur l'écran (on peut cliquer tout au bord, ou dans le coin).
                AnalyzeMeasure(gaze.XScreen, gaze.YScreen, gaze.MeasureTimeMs);
            }

            ComputeProgress(gaze.NowMs);
        }

        // ───────────────────────── Détail du calcul ─────────────────────────

        private void AnalyzeMeasure(double x, double y, double instantMs)
        {
            // Aucune fixation en cours : cette mesure en devient le point de départ.
            if (_number == 0)
            {
                Restart(x, y, instantMs);
                return;
            }

            AppSettings p = AppSettings.Current;

            // Test 1 : la vitesse depuis la dernière mesure acceptée, en pixels par seconde.
            double duration = instantMs - _prevMs;
            bool speedOk = true;
            if (duration > 0)
            {
                double speed = Distance(x, y, _prevX, _prevY) / duration * 1000.0;
                speedOk = speed <= p.MaxFixationSpeedPx;
            }

            // Test 2 : la distance au point d'ancrage.
            bool dispersionOk = Distance(x, y, X, Y) <= p.FixationRadiusPx;

            if (speedOk && dispersionOk)
            {
                // Mesure immobile : elle rejoint la fixation et affine le point d'ancrage.
                _sumX += x;
                _sumY += y;
                _number++;
                _prevX = x;
                _prevY = y;
                _prevMs = instantMs;
                _setAsideMeasure = false;
            }
            else if (!_setAsideMeasure)
            {
                // Première mesure qui s'écarte : peut-être un simple accident. On l'ignore.
                _setAsideMeasure = true;
            }
            else
            {
                // Deuxième d'affilée : le regard est vraiment parti ailleurs.
                Restart(x, y, instantMs);
            }
        }

        /// <summary>Démarre une nouvelle fixation à partir de cette mesure.</summary>
        private void Restart(double x, double y, double instantMs)
        {
            _sumX = x;
            _sumY = y;
            _number = 1;
            _prevX = x;
            _prevY = y;
            _prevMs = instantMs;
            _startMs = instantMs;
            _setAsideMeasure = false;
        }

        private void ComputeProgress(double nowMs)
        {
            if (_number == 0)
            {
                Progress = 0;
                return;
            }

            double delayMs = AppSettings.Current.FixationDelay * 1000.0;
            Progress = Math.Min(1.0, Math.Max(0.0, (nowMs - _startMs) / delayMs));
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
