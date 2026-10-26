namespace GazeBar
{
    /// <summary>
    /// Ce qu'une « cible » activable au regard doit savoir faire, pour que ViseurBoutons
    /// puisse décider si on la regarde et quand elle s'active (étape 7).
    ///
    /// Jusqu'à l'étape 6, ViseurBoutons ne connaissait que les BoutonRegard (les carrés du
    /// choix d'action et du menu, dessinés sur le calque). Le mode paramètres a d'autres
    /// cibles : les onglets, les boutons « − » et « + », « Non » et « Oui »… (CibleParametre).
    /// Plutôt que d'écrire un deuxième viseur, on décrit ici ce qu'ils ont en commun :
    /// c'est une « interface ». Une classe qui « implémente » l'interface (écrit
    /// « class BoutonRegard : ICibleRegard ») promet de fournir ces trois éléments.
    ///
    /// Attention : Contient reçoit le point du regard dans le repère de la cible. Pour un
    /// BoutonRegard, ce sont des pixels d'écran ; pour une CibleParametre, ce sont les
    /// coordonnées de la page des paramètres (voir ModeParametres).
    /// </summary>
    internal interface ICibleRegard
    {
        /// <summary>Faux pour une cible qui ne s'active jamais (emplacement vide, fonction à venir).</summary>
        bool Actif { get; }

        /// <summary>Vrai si le point du regard est sur la cible.</summary>
        bool Contient(double x, double y);

        /// <summary>
        /// Met à jour l'aspect de la cible : progression de 0 à 1 (anneau ou barre), survol,
        /// et libellé affiché ou non.
        /// </summary>
        void Dessiner(double progression, bool survol, bool afficherLibelle);
    }
}
