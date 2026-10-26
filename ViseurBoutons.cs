using System.Collections.Generic;

namespace GazeBar
{
    /// <summary>
    /// LOGIQUE : décide quel bouton (BoutonRegard) est regardé, et quand il s'active (étape 4).
    /// Depuis l'étape 7, il vise toute cible qui implémente ICibleRegard (aussi les cibles du
    /// mode paramètres).
    ///
    /// Ce travail était fait directement dans ModeChoixAction à l'étape 3. La barre de menu en
    /// a besoin aussi : on l'a donc sorti dans cette petite classe, partagée par les deux modes.
    ///
    /// Les règles :
    ///  - à chaque NOUVELLE mesure du regard, on cherche le bouton qui est dessous ;
    ///  - pour passer à un autre bouton (ou à « aucun »), il faut [MesuresPourChanger] mesures
    ///    de suite au même endroit : une mesure isolée (un petit sursaut du regard) est ignorée ;
    ///  - le regard posé sur un bouton actif pendant le délai d'activation l'active ; son anneau
    ///    de progression se remplit pendant ce temps ;
    ///  - un bouton vide ou indisponible ne s'active jamais (seul son libellé apparaît) ;
    ///  - regard perdu : le minuteur repart de zéro (on n'active jamais un bouton qu'on ne
    ///    regarde plus) ;
    ///  - après BloquerJusquASortie, le bouton regardé ne peut pas s'activer de nouveau tant que
    ///    le regard ne l'a pas quitté. Utile pour un interrupteur (« Suivi souris ») : sans cela,
    ///    en continuant à le regarder, on l'allumerait et l'éteindrait sans fin ;
    ///  - après ReinitialiserEtBloquer (les boutons viennent d'être remplacés), le bouton qui
    ///    se trouve sous le regard à la première mesure est bloqué de la même façon. Ainsi, un
    ///    nouveau bouton apparu sous le regard ne s'active pas tout seul.
    /// </summary>
    internal sealed class ViseurBoutons
    {
        /// <summary>Nombre de mesures de suite qu'il faut sur un nouveau bouton pour le viser.</summary>
        private const int MesuresPourChanger = 2;

        // La liste des boutons, partagée avec le mode (qui peut la remplir après coup).
        // IReadOnlyList<ICibleRegard> (étape 7) : une liste qu'on peut lire mais pas modifier,
        // de n'importe quelles cibles (BoutonRegard, CibleParametre…). Grâce à une règle de C#
        // appelée « covariance », une List<BoutonRegard> peut être donnée telle quelle.
        private readonly IReadOnlyList<ICibleRegard> _boutons;

        // Le bouton « candidat » vu sur les dernières mesures, et combien de fois de suite.
        private int _candidat;
        private int _mesuresCandidat;
        private long _dernierNumero;

        // Instant (ms) où le regard est arrivé sur le bouton visé ; -1 = minuteur arrêté.
        private double _debutViseMs;

        // Vrai après BloquerJusquASortie, jusqu'à ce que le regard change de bouton.
        private bool _bloque;

        // Vrai après ReinitialiserEtBloquer, jusqu'à la première nouvelle mesure.
        private bool _bloquerPremier;

        public ViseurBoutons(IReadOnlyList<ICibleRegard> boutons)
        {
            _boutons = boutons;
            Reinitialiser();
        }

        /// <summary>Le numéro du bouton visé dans la liste, ou -1 si aucun.</summary>
        public int Vise { get; private set; }

        /// <summary>Oublie tout (à l'entrée dans un mode).</summary>
        public void Reinitialiser()
        {
            Vise = -1;
            _candidat = -1;
            _mesuresCandidat = 0;
            _dernierNumero = -1;
            _debutViseMs = -1;
            _bloque = false;
            _bloquerPremier = false;
        }

        /// <summary>
        /// Oublie tout (les boutons viennent d'être remplacés), et bloque le bouton qui sera
        /// sous le regard à la prochaine mesure, jusqu'à ce que le regard le quitte.
        /// </summary>
        public void ReinitialiserEtBloquer()
        {
            Reinitialiser();
            _bloquerPremier = true;
        }

        /// <summary>
        /// Empêche le bouton visé de s'activer de nouveau tant que le regard ne l'a pas quitté.
        /// </summary>
        public void BloquerJusquASortie()
        {
            _bloque = true;
            _debutViseMs = -1;
        }

        /// <summary>
        /// À appeler à chaque tic. Met à jour le bouton visé et son minuteur, puis dessine les
        /// boutons (anneau sur le bouton visé seulement).
        /// delaiSecondes : le délai d'activation ([DelaiActivation] en général) ;
        /// afficherLibelles : montre le libellé du bouton visé ([AfficherLibelles]).
        /// Renvoie le numéro du bouton qui vient de s'activer, ou -1. Quand un bouton s'active,
        /// les boutons ne sont pas redessinés : le mode va de toute façon changer quelque chose.
        /// </summary>
        public int Actualiser(EtatRegard regard, double delaiSecondes, bool afficherLibelles)
        {
            double maintenant = regard.MaintenantMs;

            if (!regard.Disponible)
            {
                // Regard perdu : le minuteur repart de zéro.
                _debutViseMs = -1;
            }
            else if (regard.Numero != _dernierNumero)
            {
                // Nouvelle mesure : sur quel bouton est-elle ? (hors écran → aucun)
                _dernierNumero = regard.Numero;
                int vu = regard.HorsEcran ? -1 : BoutonSous(regard.X, regard.Y);
                if (_bloquerPremier)
                {
                    // Première mesure après le remplacement des boutons : celui qui est sous
                    // le regard est visé tout de suite, mais bloqué.
                    _bloquerPremier = false;
                    Vise = vu;
                    _bloque = vu >= 0;
                }
                else
                {
                    ChangerViseSiBesoin(vu);
                }
            }

            // Le minuteur du bouton visé (pas pendant un blocage).
            if (regard.Disponible && Vise >= 0 && !_bloque && _debutViseMs < 0)
            {
                _debutViseMs = maintenant;
            }
            if (Vise < 0)
            {
                _debutViseMs = -1;
            }

            // Un bouton vide ou indisponible ne s'active jamais : pas de progression.
            double progression = 0;
            if (Vise >= 0 && _debutViseMs >= 0 && _boutons[Vise].Actif)
            {
                progression = (maintenant - _debutViseMs) / (delaiSecondes * 1000.0);
            }

            if (progression >= 1)
            {
                int active = Vise;
                _debutViseMs = -1;
                return active;
            }

            for (int i = 0; i < _boutons.Count; i++)
            {
                bool survol = i == Vise;
                _boutons[i].Dessiner(survol ? progression : 0, survol, survol && afficherLibelles);
            }
            return -1;
        }

        /// <summary>
        /// Change de bouton visé seulement si le nouveau est confirmé par
        /// [MesuresPourChanger] mesures de suite. Changer de bouton remet le minuteur à zéro
        /// et lève le blocage.
        /// </summary>
        private void ChangerViseSiBesoin(int vu)
        {
            if (vu == Vise)
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
                Vise = vu;
                _mesuresCandidat = 0;
                _debutViseMs = -1;
                _bloque = false;
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
    }
}
