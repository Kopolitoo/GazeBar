namespace GazeBar
{
    /// <summary>
    /// Ce que tous les modes savent faire. C'est une classe « abstraite » : on ne crée jamais
    /// un « Mode » tout court, mais un mode précis (ModeMouvement, ModeRegardFixe, plus tard
    /// ModeMenu…) qui hérite de celle-ci et remplit les méthodes qui l'intéressent.
    ///
    /// Le ModeManager appelle ces méthodes au bon moment :
    ///   Entrer(regard)  une fois, quand le mode devient le mode courant. Il reçoit la
    ///                   position actuelle du regard, pour s'initialiser au bon endroit
    ///                   (et non à une place par défaut comme le centre de l'écran) ;
    ///   Actualiser()    environ 60 fois par seconde, avec la dernière position du regard ;
    ///   Quitter()       une fois, quand on passe à un autre mode (pour tout ranger).
    ///
    /// Pour éviter un saut visuel d'un mode à l'autre, le cercle n'est PAS caché dans
    /// Quitter : le mode suivant le reprend là où il est (Calque.CercleX, Calque.CercleY)
    /// et le déplace en douceur. Un mode qui ne veut pas de cercle le cache dans son Entrer.
    ///
    /// « virtual » : la méthode a un comportement par défaut (ne rien faire), qu'un mode
    /// peut remplacer avec « override ».
    /// </summary>
    internal abstract class Mode
    {
        /// <summary>Nom lisible du mode, pour le journal et l'affichage.</summary>
        public abstract string Nom { get; }

        /// <summary>
        /// Vrai si le regard en bas hors de l'écran peut ouvrir le menu pendant ce mode (étape 4).
        /// C'est le cas presque partout (« toujours une sortie », dit le cahier des charges) ;
        /// un mode peut l'interdire, par exemple le menu lui-même, ou le choix d'action
        /// pendant le court instant où il prend sa photo.
        /// </summary>
        public virtual bool MenuAutorise
        {
            get { return true; }
        }

        /// <summary>Le gestionnaire qui pilote ce mode. Donné par le ModeManager avant Entrer(regard).</summary>
        protected ModeManager Gestionnaire { get; private set; }

        /// <summary>Appelée par le ModeManager uniquement (d'où « internal »).</summary>
        internal void Attacher(ModeManager gestionnaire)
        {
            Gestionnaire = gestionnaire;
        }

        public virtual void Entrer(EtatRegard regard)
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
