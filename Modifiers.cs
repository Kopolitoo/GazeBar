namespace GazeBar
{
    /// <summary>Les trois touches modificatrices du choix d'action.</summary>
    internal enum Modifier
    {
        Ctrl,
        Shift,
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
    internal static class Modifiers
    {
        public static bool Ctrl { get; private set; }

        public static bool Shift { get; private set; }

        public static bool Alt { get; private set; }

        /// <summary>Vrai si au moins un modificateur est actif.</summary>
        public static bool AnyActive
        {
            get { return Ctrl || Shift || Alt; }
        }

        /// <summary>Vrai si ce modificateur est actif.</summary>
        public static bool IsActive(Modifier m)
        {
            switch (m)
            {
                case Modifier.Ctrl:
                    return Ctrl;
                case Modifier.Shift:
                    return Shift;
                default:
                    return Alt;
            }
        }

        /// <summary>Active le modificateur s'il est inactif, le désactive sinon. Renvoie le nouvel état.</summary>
        public static bool Toggle(Modifier m)
        {
            switch (m)
            {
                case Modifier.Ctrl:
                    Ctrl = !Ctrl;
                    break;
                case Modifier.Shift:
                    Shift = !Shift;
                    break;
                default:
                    Alt = !Alt;
                    break;
            }
            App.Log("Modificateurs : " + Description());
            return IsActive(m);
        }

        /// <summary>Désactive les trois modificateurs (sans toucher au clavier).</summary>
        public static void DisableAll()
        {
            if (AnyActive)
            {
                App.Log("Modificateurs : tous désactivés");
            }
            Ctrl = false;
            Shift = false;
            Alt = false;
        }

        /// <summary>Enfonce les touches des modificateurs actifs (juste avant une action).</summary>
        public static void PressActive()
        {
            if (Ctrl)
            {
                KeyboardInjector.PressDown(NativeMethods.VK_CONTROL);
            }
            if (Shift)
            {
                KeyboardInjector.PressDown(NativeMethods.VK_SHIFT);
            }
            if (Alt)
            {
                KeyboardInjector.PressDown(NativeMethods.VK_MENU);
            }
        }

        /// <summary>Relâche toutes les touches enfoncées (juste après une action).</summary>
        public static void ReleaseAll()
        {
            KeyboardInjector.ReleaseAll();
        }

        /// <summary>Texte pour le journal, par exemple « Ctrl + Maj » ou « aucun ».</summary>
        public static string Description()
        {
            string text = "";
            if (Ctrl)
            {
                text += "Ctrl";
            }
            if (Shift)
            {
                text += (text.Length > 0 ? " + " : "") + "Maj";
            }
            if (Alt)
            {
                text += (text.Length > 0 ? " + " : "") + "Alt";
            }
            return text.Length > 0 ? text : "aucun";
        }
    }
}
