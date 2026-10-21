namespace GazeBar
{
    /// <summary>
    /// Une « photo » du regard à un instant donné, préparée par le ModeManager pour les modes.
    /// Ainsi, chaque mode n'a pas à refaire les mêmes vérifications (regard perdu ? hors écran ?).
    ///
    /// « struct » : un petit paquet de valeurs, copié tel quel (comme un nombre), et non un objet partagé.
    /// </summary>
    internal struct EtatRegard
    {
        /// <summary>Vrai si une mesure récente existe (moins de [AgeMaxMesure] ms). Sinon : regard perdu (yeux fermés, tête hors champ…).</summary>
        public bool Disponible;

        /// <summary>Position du regard, en pixels d'écran. Peut être négative ou dépasser l'écran.</summary>
        public double X;
        public double Y;

        /// <summary>Vrai si le regard est disponible mais en dehors de l'écran.</summary>
        public bool HorsEcran;

        /// <summary>Vrai si le regard est disponible et SOUS le bas de l'écran (utile pour ouvrir le menu, étape 4).</summary>
        public bool EnBasHorsEcran;

        /// <summary>Vrai si le regard est disponible et sur l'écran : le cas « normal ».</summary>
        public bool SurEcran
        {
            get { return Disponible && !HorsEcran; }
        }
    }
}
