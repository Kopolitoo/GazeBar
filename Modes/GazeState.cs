namespace GazeBar
{
    /// <summary>
    /// Une « photo » du regard à un instant donné, préparée par le ModeManager pour les modes.
    /// Ainsi, chaque mode n'a pas à refaire les mêmes vérifications (regard perdu ? hors écran ?).
    ///
    /// « struct » : un petit paquet de valeurs, copié tel quel (comme un nombre), et non un objet partagé.
    /// </summary>
    internal struct GazeState
    {
        /// <summary>Vrai si une mesure récente existe (moins de [AgeMaxMesure] ms). Sinon : regard perdu (yeux fermés, tête hors champ…).</summary>
        public bool Available;

        /// <summary>Position du regard, en pixels d'écran. Peut être négative ou dépasser l'écran.</summary>
        public double X;
        public double Y;

        /// <summary>
        /// Position du regard RAMENÉE DANS L'ÉCRAN (chaque axe bloqué séparément aux bords).
        /// Exemple : regard au-dessus de l'écran → YEcran = 0, mais XEcran suit toujours le
        /// regard de gauche à droite. Sert à faire glisser les cercles du regard le long des
        /// bords quand on regarde hors de l'écran (demande de l'utilisateur du 29/10/2026).
        /// Sert aussi à la fixation et au mode regard fixe, mais seulement quand le regard est
        /// tout près de l'écran (voir PresDeLEcran) ; jamais aux boutons.
        /// Valable seulement si Disponible est vrai.
        /// </summary>
        public double XScreen;
        public double YScreen;

        /// <summary>
        /// Numéro de la mesure (augmente de 1 à chaque mesure reçue du PCEye). Si le numéro
        /// n'a pas changé depuis le tic précédent, c'est la même mesure : il n'y a rien de neuf.
        /// </summary>
        public long Number;

        /// <summary>Instant où la mesure est arrivée, en millisecondes.</summary>
        public double MeasureTimeMs;

        /// <summary>
        /// L'heure de ce tic, en millisecondes, sur la même horloge que InstantMesureMs.
        /// Toujours renseignée, même quand le regard n'est pas disponible. Les modes s'en
        /// servent pour mesurer des durées (délai de fixation, délai d'activation…).
        /// </summary>
        public double NowMs;

        /// <summary>Vrai si le regard est disponible mais en dehors de l'écran.</summary>
        public bool OffScreen;

        /// <summary>Vrai si le regard est disponible et SOUS le bas de l'écran (utile pour ouvrir le menu, étape 4).</summary>
        public bool BelowScreen;

        /// <summary>
        /// Vrai si le regard est disponible et sur l'écran, OU juste à côté : à moins de
        /// [MargeBords4K] pixels (mesurés sur l'écran 4K) de ses bords (demande de l'utilisateur
        /// du 02/11/2026). Dans les coins, le PCEye est moins précis : en regardant le coin,
        /// une partie des mesures tombe un peu hors de l'écran, ce qui remettait la fixation à
        /// zéro sans arrêt. Dans cette bande, la fixation et le mode regard fixe utilisent la
        /// position ramenée dans l'écran (XEcran, YEcran) au lieu de tout oublier.
        /// </summary>
        public bool NearScreen;

        /// <summary>Vrai si le regard est disponible et sur l'écran : le cas « normal ».</summary>
        public bool OnScreen
        {
            get { return Available && !OffScreen; }
        }
    }
}
