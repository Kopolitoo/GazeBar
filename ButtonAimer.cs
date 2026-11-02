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
    ///  - pour passer à un autre bouton (ou à « aucun »), il faut [_mesuresPourChanger] mesures
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
    internal sealed class ButtonAimer
    {
        /// <summary>
        /// Nombre de mesures de suite qu'il faut sur un nouveau bouton pour le viser : 2 par
        /// défaut ; 1 pour le clavier au regard (survol plus rapide, 02/11/2026).
        /// </summary>
        private readonly int _measuresToChange;

        // La liste des boutons, partagée avec le mode (qui peut la remplir après coup).
        // IReadOnlyList<ICibleRegard> (étape 7) : une liste qu'on peut lire mais pas modifier,
        // de n'importe quelles cibles (BoutonRegard, CibleParametre…). Grâce à une règle de C#
        // appelée « covariance », une List<BoutonRegard> peut être donnée telle quelle.
        private readonly IReadOnlyList<IGazeTarget> _buttons;

        // Le bouton « candidat » vu sur les dernières mesures, et combien de fois de suite.
        private int _candidate;
        private int _candidateMeasures;
        private long _lastNumber;

        // Instant (ms) où le regard est arrivé sur le bouton visé ; -1 = minuteur arrêté.
        private double _aimStartMs;

        // Vrai après BloquerJusquASortie, jusqu'à ce que le regard change de bouton.
        private bool _blocked;

        // Vrai après ReinitialiserEtBloquer, jusqu'à la première nouvelle mesure.
        private bool _blockFirst;

        public ButtonAimer(IReadOnlyList<IGazeTarget> buttons) : this(buttons, 2)
        {
        }

        /// <summary>mesuresPourChanger : voir _mesuresPourChanger (au moins 1).</summary>
        public ButtonAimer(IReadOnlyList<IGazeTarget> buttons, int measuresToChange)
        {
            _buttons = buttons;
            _measuresToChange = measuresToChange < 1 ? 1 : measuresToChange;
            Reset();
        }

        /// <summary>Le numéro du bouton visé dans la liste, ou -1 si aucun.</summary>
        public int Aimed { get; private set; }

        /// <summary>Oublie tout (à l'entrée dans un mode).</summary>
        public void Reset()
        {
            Aimed = -1;
            _candidate = -1;
            _candidateMeasures = 0;
            _lastNumber = -1;
            _aimStartMs = -1;
            _blocked = false;
            _blockFirst = false;
        }

        /// <summary>
        /// Oublie tout (les boutons viennent d'être remplacés), et bloque le bouton qui sera
        /// sous le regard à la prochaine mesure, jusqu'à ce que le regard le quitte.
        /// </summary>
        public void ResetAndBlock()
        {
            Reset();
            _blockFirst = true;
        }

        /// <summary>
        /// Empêche le bouton visé de s'activer de nouveau tant que le regard ne l'a pas quitté.
        /// </summary>
        public void BlockUntilExit()
        {
            _blocked = true;
            _aimStartMs = -1;
        }

        /// <summary>
        /// À appeler à chaque tic. Met à jour le bouton visé et son minuteur, puis dessine les
        /// boutons (anneau sur le bouton visé seulement).
        /// delaiSecondes : le délai d'activation ([DelaiActivation] en général) ;
        /// afficherLibelles : montre le libellé du bouton visé ([AfficherLibelles]).
        /// Renvoie le numéro du bouton qui vient de s'activer, ou -1. Quand un bouton s'active,
        /// les boutons ne sont pas redessinés : le mode va de toute façon changer quelque chose.
        /// </summary>
        public int Update(GazeState gaze, double delaySeconds, bool showLabels)
        {
            double now = gaze.NowMs;

            if (!gaze.Available)
            {
                // Regard perdu : le minuteur repart de zéro.
                _aimStartMs = -1;
            }
            else if (gaze.Number != _lastNumber)
            {
                // Nouvelle mesure : sur quel bouton est-elle ? (hors écran → aucun)
                _lastNumber = gaze.Number;
                int seen = gaze.OffScreen ? -1 : ButtonUnder(gaze.X, gaze.Y);
                if (_blockFirst)
                {
                    // Première mesure après le remplacement des boutons : celui qui est sous
                    // le regard est visé tout de suite, mais bloqué.
                    _blockFirst = false;
                    Aimed = seen;
                    _blocked = seen >= 0;
                }
                else
                {
                    ChangeAimedIfNeeded(seen);
                }
            }

            // Le minuteur du bouton visé (pas pendant un blocage).
            if (gaze.Available && Aimed >= 0 && !_blocked && _aimStartMs < 0)
            {
                _aimStartMs = now;
            }
            if (Aimed < 0)
            {
                _aimStartMs = -1;
            }

            // Un bouton vide ou indisponible ne s'active jamais : pas de progression.
            double progress = 0;
            if (Aimed >= 0 && _aimStartMs >= 0 && _buttons[Aimed].Active)
            {
                progress = (now - _aimStartMs) / (delaySeconds * 1000.0);
            }

            if (progress >= 1)
            {
                int active = Aimed;
                _aimStartMs = -1;
                return active;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool hover = i == Aimed;
                _buttons[i].Draw(hover ? progress : 0, hover, hover && showLabels);
            }
            return -1;
        }

        /// <summary>
        /// Change de bouton visé seulement si le nouveau est confirmé par
        /// [_mesuresPourChanger] mesures de suite. Changer de bouton remet le minuteur à zéro
        /// et lève le blocage.
        /// </summary>
        private void ChangeAimedIfNeeded(int seen)
        {
            if (seen == Aimed)
            {
                _candidateMeasures = 0;
                return;
            }

            if (seen == _candidate && _candidateMeasures > 0)
            {
                _candidateMeasures++;
            }
            else
            {
                _candidate = seen;
                _candidateMeasures = 1;
            }

            if (_candidateMeasures >= _measuresToChange)
            {
                Aimed = seen;
                _candidateMeasures = 0;
                _aimStartMs = -1;
                _blocked = false;
            }
        }

        /// <summary>Le numéro du bouton sous ce point du regard, ou -1.</summary>
        private int ButtonUnder(double x, double y)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].Contains(x, y))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
