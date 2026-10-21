namespace GazeBar
{
    /// <summary>
    /// Ce que tous les modes savent faire. C'est une classe « abstraite » : on ne crée jamais
    /// un « Mode » tout court, mais un mode précis (ModeMouvement, ModeRegardFixe, plus tard
    /// ModeMenu…) qui hérite de celle-ci et remplit les méthodes qui l'intéressent.
    ///
    /// Le ModeManager appelle ces méthodes au bon moment :
    ///   Entrer()        une fois, quand le mode devient le mode courant ;
    ///   Actualiser()    environ 60 fois par seconde, avec la dernière position du regard ;
    ///   Quitter()       une fois, quand on passe à un autre mode (pour tout ranger).
    ///
    /// « virtual » : la méthode a un comportement par défaut (ne rien faire), qu'un mode
    /// peut remplacer avec « override ».
    /// </summary>
    internal abstract class Mode
    {
        /// <summary>Nom lisible du mode, pour le journal et l'affichage.</summary>
        public abstract string Nom { get; }

        /// <summary>Le gestionnaire qui pilote ce mode. Donné par le ModeManager avant Entrer().</summary>
        protected ModeManager Gestionnaire { get; private set; }

        /// <summary>Appelée par le ModeManager uniquement (d'où « internal »).</summary>
        internal void Attacher(ModeManager gestionnaire)
        {
            Gestionnaire = gestionnaire;
        }

        public virtual void Entrer()
        {
        }

        public virtual void Actualiser(EtatRegard regard)
        {
        }

        public virtual void Quitter()
        {
        }
    }
}
