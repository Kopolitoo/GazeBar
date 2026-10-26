namespace GazeBar
{
    /// <summary>Les trois touches modificatrices du choix d'action.</summary>
    internal enum Modificateur
    {
        Ctrl,
        Maj,
        Alt
    }

    /// <summary>
    /// MODÈLE : l'état des modificateurs Ctrl, Maj et Alt du choix d'action (étape 6).
    ///
    /// Fonctionnement (cahier des charges, « Actions et modificateurs ») :
    ///  - un regard sur l'icône d'un modificateur l'active (bord vert), un second le désactive ;
    ///  - à l'exécution d'une action, les touches actives sont enfoncées juste avant
    ///    (EnfoncerActifs) et relâchées juste après (RelacherTout), ou après le dépôt pour un
    ///    glisser-déposer ;
    ///  - ils se désactivent automatiquement après chaque action (ToutDesactiver).
    ///
    /// Pourquoi une classe « static », partagée, et pas un simple champ du choix d'action ?
    /// Parce qu'on peut quitter le choix d'action sans agir : par exemple Ctrl, puis « Loupe »,
    /// puis retour au choix d'action sur le point affiné. Un nouveau ModeChoixAction est alors
    /// créé, et il doit retrouver Ctrl allumé. De même, le glisser-déposer traverse trois
    /// modes (arrivée, loupe, exécution) avant d'utiliser les modificateurs.
    ///
    /// Les modificateurs sont aussi désactivés à chaque retour au mode mouvement (même sans
    /// action, par « retour » ou Échap) : ainsi, un Ctrl oublié ne reste jamais actif en
    /// cachette pour le prochain clic.
    /// </summary>
    internal static class Modificateurs
    {
        public static bool Ctrl { get; private set; }

        public static bool Maj { get; private set; }

        public static bool Alt { get; private set; }

        /// <summary>Vrai si au moins un modificateur est actif.</summary>
        public static bool AuMoinsUnActif
        {
            get { return Ctrl || Maj || Alt; }
        }

        /// <summary>Vrai si ce modificateur est actif.</summary>
        public static bool EstActif(Modificateur m)
        {
            switch (m)
            {
                case Modificateur.Ctrl:
                    return Ctrl;
                case Modificateur.Maj:
                    return Maj;
                default:
                    return Alt;
            }
        }

        /// <summary>Active le modificateur s'il est inactif, le désactive sinon. Renvoie le nouvel état.</summary>
        public static bool Basculer(Modificateur m)
        {
            switch (m)
            {
                case Modificateur.Ctrl:
                    Ctrl = !Ctrl;
                    break;
                case Modificateur.Maj:
                    Maj = !Maj;
                    break;
                default:
                    Alt = !Alt;
                    break;
            }
            App.Log("Modificateurs : " + Description());
            return EstActif(m);
        }

        /// <summary>Désactive les trois modificateurs (sans toucher au clavier).</summary>
        public static void ToutDesactiver()
        {
            if (AuMoinsUnActif)
            {
                App.Log("Modificateurs : tous désactivés");
            }
            Ctrl = false;
            Maj = false;
            Alt = false;
        }

        /// <summary>Enfonce les touches des modificateurs actifs (juste avant une action).</summary>
        public static void EnfoncerActifs()
        {
            if (Ctrl)
            {
                InjecteurClavier.Enfoncer(NativeMethods.VK_CONTROL);
            }
            if (Maj)
            {
                InjecteurClavier.Enfoncer(NativeMethods.VK_SHIFT);
            }
            if (Alt)
            {
                InjecteurClavier.Enfoncer(NativeMethods.VK_MENU);
            }
        }

        /// <summary>Relâche toutes les touches enfoncées (juste après une action).</summary>
        public static void RelacherTout()
        {
            InjecteurClavier.RelacherTout();
        }

        /// <summary>Texte pour le journal, par exemple « Ctrl + Maj » ou « aucun ».</summary>
        public static string Description()
        {
            string texte = "";
            if (Ctrl)
            {
                texte += "Ctrl";
            }
            if (Maj)
            {
                texte += (texte.Length > 0 ? " + " : "") + "Maj";
            }
            if (Alt)
            {
                texte += (texte.Length > 0 ? " + " : "") + "Alt";
            }
            return texte.Length > 0 ? texte : "aucun";
        }
    }
}
