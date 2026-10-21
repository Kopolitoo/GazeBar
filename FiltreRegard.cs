using System;

namespace GazeBar
{
    /// <summary>
    /// LOGIQUE : rend fluide et stable une position qui suit le regard (le cercle, et plus
    /// tard le curseur).
    ///
    /// Le problème : l'œil n'est jamais parfaitement immobile, et le PCEye 5 ne donne
    /// qu'environ 30 mesures par seconde. Afficher le regard brut donne un cercle qui saute
    /// d'une mesure à l'autre, même quand on fixe un point.
    ///
    /// La solution : un lissage « adaptatif », c'est-à-dire qui change selon la situation.
    /// À chaque pas (environ 60 fois par seconde), la position affichée se rapproche du
    /// regard d'une fraction de la distance qui les sépare :
    ///  - regard très proche (moins de [ZoneMorteCurseur]) : on ne bouge pas du tout ;
    ///  - regard proche (moins de [RayonFixation]) : c'est le tremblement d'une fixation,
    ///    on avance TRÈS lentement (fraction = Lissage × FacteurFixation), ce qui fait la
    ///    moyenne des petites erreurs et immobilise le cercle ;
    ///  - regard loin : c'est un vrai déplacement de l'œil, on avance vite
    ///    (fraction = Lissage) pour rester réactif.
    /// Comme la fonction est appelée 60 fois par seconde alors que le regard n'arrive que
    /// 30 fois, le cercle glisse entre deux mesures au lieu de sauter.
    /// </summary>
    internal sealed class FiltreRegard
    {
        /// <summary>
        /// Pendant une fixation, la fraction utilisée est Lissage multiplié par ce facteur.
        /// 0,15 avec un Lissage de 0,30 donne 0,045 par pas : le cercle met environ 0,4 s à
        /// rattraper un petit écart, ce qui gomme le tremblement. Plus petit = plus stable mais
        /// plus lent à se recentrer. (Pourra devenir un paramètre réglable si besoin.)
        /// </summary>
        public const double FacteurFixation = 0.15;

        private double _x;
        private double _y;
        private bool _initialise;

        /// <summary>Position filtrée actuelle, en pixels d'écran.</summary>
        public double X
        {
            get { return _x; }
        }

        public double Y
        {
            get { return _y; }
        }

        /// <summary>Vrai dès que le filtre a reçu au moins une position.</summary>
        public bool Initialise
        {
            get { return _initialise; }
        }

        /// <summary>Oublie la position : la prochaine sera reprise telle quelle (après une perte du regard, par exemple).</summary>
        public void Reinitialiser()
        {
            _initialise = false;
        }

        /// <summary>Place directement la position filtrée, sans lissage.</summary>
        public void Placer(double x, double y)
        {
            _x = x;
            _y = y;
            _initialise = true;
        }

        /// <summary>
        /// Fait avancer la position filtrée d'un pas vers le regard (gx, gy).
        /// À appeler à chaque tic de l'horloge, même si le regard n'a pas changé.
        /// </summary>
        public void Avancer(double gx, double gy)
        {
            if (!_initialise)
            {
                Placer(gx, gy);
                return;
            }

            Parametres p = Parametres.Courants;

            double dx = gx - _x;
            double dy = gy - _y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < p.ZoneMorteCurseur)
            {
                return;
            }

            double fraction = distance < p.RayonFixation
                ? p.Lissage * FacteurFixation
                : p.Lissage;

            _x += dx * fraction;
            _y += dy * fraction;
        }
    }
}
