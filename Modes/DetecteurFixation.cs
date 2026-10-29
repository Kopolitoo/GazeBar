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
    /// </summary>
    internal sealed class DetecteurFixation
    {
        // Le point d'ancrage = somme des mesures acceptées ÷ leur nombre.
        private double _sommeX;
        private double _sommeY;
        private int _nombre;

        // Instant de la première mesure de la fixation en cours, en millisecondes.
        private double _debutMs;

        // Dernière mesure ACCEPTÉE (sert au calcul de la vitesse).
        private double _precX;
        private double _precY;
        private double _precMs;

        // Numéro de la dernière mesure traitée (pour ne traiter chaque mesure qu'une fois).
        private long _dernierNumero = -1;

        // Vrai si la dernière mesure a été mise de côté (une seule tolérée d'affilée).
        private bool _mesureMiseDeCote;

        /// <summary>Avancement de la fixation, entre 0 (rien) et 1 (fixation atteinte).</summary>
        public double Progression { get; private set; }

        /// <summary>Vrai quand le regard est resté immobile pendant [DelaiFixation].</summary>
        public bool Atteinte
        {
            get { return Progression >= 1; }
        }

        /// <summary>Le point fixé (point d'ancrage), en pixels d'écran.</summary>
        public double X
        {
            get { return _nombre > 0 ? _sommeX / _nombre : 0; }
        }

        public double Y
        {
            get { return _nombre > 0 ? _sommeY / _nombre : 0; }
        }

        /// <summary>
        /// Oublie tout : la prochaine mesure commencera une nouvelle fixation.
        /// À appeler quand le regard est perdu ou hors de l'écran.
        /// </summary>
        public void Reinitialiser()
        {
            _nombre = 0;
            _sommeX = 0;
            _sommeY = 0;
            _mesureMiseDeCote = false;
            Progression = 0;
        }

        /// <summary>
        /// À appeler à chaque tic avec la « photo » du regard.
        /// Seules les nouvelles mesures sur l'écran sont analysées ; entre deux mesures, on met
        /// seulement à jour la progression (pour que l'anneau se remplisse sans à-coups).
        /// </summary>
        public void Ajouter(EtatRegard regard)
        {
            if (!regard.SurEcran)
            {
                Reinitialiser();
                return;
            }

            if (regard.Numero != _dernierNumero)
            {
                _dernierNumero = regard.Numero;
                AnalyserMesure(regard.X, regard.Y, regard.InstantMesureMs);
            }

            CalculerProgression(regard.MaintenantMs);
        }

        // ───────────────────────── Détail du calcul ─────────────────────────

        private void AnalyserMesure(double x, double y, double instantMs)
        {
            // Aucune fixation en cours : cette mesure en devient le point de départ.
            if (_nombre == 0)
            {
                Recommencer(x, y, instantMs);
                return;
            }

            Parametres p = Parametres.Courants;

            // Test 1 : la vitesse depuis la dernière mesure acceptée, en pixels par seconde.
            double duree = instantMs - _precMs;
            bool vitesseOk = true;
            if (duree > 0)
            {
                double vitesse = Distance(x, y, _precX, _precY) / duree * 1000.0;
                vitesseOk = vitesse <= p.VitesseMaxFixationPx;
            }

            // Test 2 : la distance au point d'ancrage.
            bool dispersionOk = Distance(x, y, X, Y) <= p.RayonFixationPx;

            if (vitesseOk && dispersionOk)
            {
                // Mesure immobile : elle rejoint la fixation et affine le point d'ancrage.
                _sommeX += x;
                _sommeY += y;
                _nombre++;
                _precX = x;
                _precY = y;
                _precMs = instantMs;
                _mesureMiseDeCote = false;
            }
            else if (!_mesureMiseDeCote)
            {
                // Première mesure qui s'écarte : peut-être un simple accident. On l'ignore.
                _mesureMiseDeCote = true;
            }
            else
            {
                // Deuxième d'affilée : le regard est vraiment parti ailleurs.
                Recommencer(x, y, instantMs);
            }
        }

        /// <summary>Démarre une nouvelle fixation à partir de cette mesure.</summary>
        private void Recommencer(double x, double y, double instantMs)
        {
            _sommeX = x;
            _sommeY = y;
            _nombre = 1;
            _precX = x;
            _precY = y;
            _precMs = instantMs;
            _debutMs = instantMs;
            _mesureMiseDeCote = false;
        }

        private void CalculerProgression(double maintenantMs)
        {
            if (_nombre == 0)
            {
                Progression = 0;
                return;
            }

            double delaiMs = Parametres.Courants.DelaiFixation * 1000.0;
            Progression = Math.Min(1.0, Math.Max(0.0, (maintenantMs - _debutMs) / delaiMs));
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
