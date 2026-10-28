using System;
using System.Collections.Generic;

namespace GazeBar
{
    /// <summary>
    /// Les actions qu'on peut placer sur les icônes du choix d'action (étape 10).
    ///
    /// Jusqu'à l'étape 9, cette liste était cachée à l'intérieur de ModeChoixAction. L'écran
    /// de configuration des icônes (ModeConfigIcones) et les paramètres (parametres.json) en
    /// ont besoin aussi : elle est donc sortie ici, avec tout ce qui décrit une action
    /// (son groupe, son bouton, son nom).
    ///
    /// Attention : les NOMS de cette liste sont écrits tels quels dans parametres.json
    /// (par exemple "ClicGauche"). Renommer une valeur rendrait illisible l'emplacement
    /// correspondant d'un fichier déjà enregistré (il deviendrait vide).
    /// </summary>
    internal enum ActionChoix
    {
        ClicGauche,
        ClicDroit,
        DoubleClic,
        Loupe,
        GlisserGauche,
        GlisserDroit,
        GlisserAndroid,
        Defilement,
        Ctrl,
        Maj,
        Alt,
        Copier,
        Couper,
        Coller,
        Retour
    }

    /// <summary>
    /// Les trois groupes d'emplacements du choix d'action. Chaque groupe a sa propre liste
    /// d'actions : une action de la couronne ne peut pas aller à gauche, par exemple (le
    /// cahier des charges sépare les listes : « les trois emplacements de gauche pour les
    /// modificateurs, les huit emplacements du centre pour les actions »).
    /// </summary>
    internal enum GroupeIcones
    {
        /// <summary>Les huit emplacements autour de la photo.</summary>
        Couronne,

        /// <summary>Les trois emplacements collés au bord gauche (Ctrl, Maj, Alt).</summary>
        Modificateurs,

        /// <summary>Les trois emplacements collés au bord haut (Copier, Couper, Coller).</summary>
        Utilitaires
    }

    /// <summary>
    /// LE CATALOGUE DES ACTIONS : pour chaque groupe, les actions qu'on peut y placer, leur
    /// disposition par défaut, et pour chaque action son bouton et son nom.
    ///
    /// Pour ajouter une action plus tard : l'ajouter dans ActionChoix (à la fin, de
    /// préférence), dans la liste de son groupe (Actions), dans CreerBouton et NomCourt,
    /// puis programmer ce qu'elle fait dans ModeChoixAction.
    /// </summary>
    internal static class CatalogueActions
    {
        /// <summary>Nombre d'emplacements de chaque groupe.</summary>
        public static int NombreEmplacements(GroupeIcones groupe)
        {
            return groupe == GroupeIcones.Couronne ? 8 : 3;
        }

        /// <summary>
        /// Les actions qu'on peut placer dans ce groupe, dans l'ordre où la liste de
        /// l'écran de configuration les présente.
        /// </summary>
        public static ActionChoix[] Actions(GroupeIcones groupe)
        {
            switch (groupe)
            {
                case GroupeIcones.Couronne:
                    return new[]
                    {
                        ActionChoix.ClicGauche,
                        ActionChoix.ClicDroit,
                        ActionChoix.DoubleClic,
                        ActionChoix.Loupe,
                        ActionChoix.GlisserGauche,
                        ActionChoix.GlisserDroit,
                        ActionChoix.GlisserAndroid,
                        ActionChoix.Defilement
                    };
                case GroupeIcones.Modificateurs:
                    return new[] { ActionChoix.Ctrl, ActionChoix.Maj, ActionChoix.Alt };
                default:
                    return new[] { ActionChoix.Copier, ActionChoix.Couper, ActionChoix.Coller };
            }
        }

        /// <summary>
        /// La disposition par défaut de chaque groupe (ordre choisi par l'utilisateur le
        /// 28/10/2026 pour la couronne). Couronne, en partant du haut à droite, dans le sens
        /// des aiguilles d'une montre (voir DispositionChoixAction) :
        ///   0 : en haut, à droite   → défilement
        ///   1 : à droite, en haut   → clic droit
        ///   2 : à droite, en bas    → glisser-déposer gauche
        ///   3 : en bas, à droite    → glisser-déposer droit
        ///   4 : en bas, à gauche    → loupe
        ///   5 : à gauche, en bas    → glisser-déposer android
        ///   6 : à gauche, en haut   → double clic
        ///   7 : en haut, à gauche   → clic gauche
        /// Modificateurs, de haut en bas : Ctrl, Maj, Alt.
        /// Utilitaires, de gauche à droite : Copier, Couper, Coller.
        /// </summary>
        public static ActionChoix[] DispositionParDefaut(GroupeIcones groupe)
        {
            if (groupe == GroupeIcones.Couronne)
            {
                return new[]
                {
                    ActionChoix.Defilement,
                    ActionChoix.ClicDroit,
                    ActionChoix.GlisserGauche,
                    ActionChoix.GlisserDroit,
                    ActionChoix.Loupe,
                    ActionChoix.GlisserAndroid,
                    ActionChoix.DoubleClic,
                    ActionChoix.ClicGauche
                };
            }
            // Pour les deux autres groupes, la disposition par défaut est la liste elle-même.
            return Actions(groupe);
        }

        /// <summary>Vrai si cette action peut être placée dans ce groupe.</summary>
        public static bool AppartientA(ActionChoix action, GroupeIcones groupe)
        {
            return Array.IndexOf(Actions(groupe), action) >= 0;
        }

        /// <summary>
        /// Lit un nom d'action écrit dans parametres.json (par exemple "ClicGauche").
        /// Renvoie null pour un emplacement vide ("" ou null), un nom inconnu (faute de
        /// frappe, action supprimée) ou une action qui n'appartient pas à ce groupe.
        /// </summary>
        public static ActionChoix? Lire(string nom, GroupeIcones groupe)
        {
            if (string.IsNullOrWhiteSpace(nom))
            {
                return null;
            }
            ActionChoix action;
            // « true » : les majuscules ne comptent pas (« clicgauche » est accepté).
            if (!Enum.TryParse(nom.Trim(), true, out action) || !Enum.IsDefined(typeof(ActionChoix), action))
            {
                return null;
            }
            return AppartientA(action, groupe) ? action : (ActionChoix?)null;
        }

        /// <summary>Le nom écrit dans parametres.json : "" pour un emplacement vide.</summary>
        public static string Ecrire(ActionChoix? action)
        {
            return action.HasValue ? action.Value.ToString() : "";
        }

        /// <summary>Vrai pour les modificateurs Ctrl, Maj, Alt.</summary>
        public static bool EstModificateur(ActionChoix action)
        {
            return action == ActionChoix.Ctrl || action == ActionChoix.Maj || action == ActionChoix.Alt;
        }

        /// <summary>Le modificateur qui correspond à l'action Ctrl, Maj ou Alt.</summary>
        public static Modificateur VersModificateur(ActionChoix action)
        {
            switch (action)
            {
                case ActionChoix.Ctrl:
                    return Modificateur.Ctrl;
                case ActionChoix.Maj:
                    return Modificateur.Maj;
                default:
                    return Modificateur.Alt;
            }
        }

        /// <summary>
        /// Un nom court, affiché sous chaque action dans la liste de l'écran de configuration
        /// (le libellé du bouton, plus long, est parfois trop large pour la grille).
        /// </summary>
        public static string NomCourt(ActionChoix action)
        {
            switch (action)
            {
                case ActionChoix.ClicGauche: return "Clic gauche";
                case ActionChoix.ClicDroit: return "Clic droit";
                case ActionChoix.DoubleClic: return "Double clic";
                case ActionChoix.Loupe: return "Loupe";
                case ActionChoix.GlisserGauche: return "Glisser gauche";
                case ActionChoix.GlisserDroit: return "Glisser droit";
                case ActionChoix.GlisserAndroid: return "Glisser android";
                case ActionChoix.Defilement: return "Défilement";
                case ActionChoix.Ctrl: return "Ctrl";
                case ActionChoix.Maj: return "Maj";
                case ActionChoix.Alt: return "Alt";
                case ActionChoix.Copier: return "Copier";
                case ActionChoix.Couper: return "Couper";
                case ActionChoix.Coller: return "Coller";
                default: return "Retour";
            }
        }

        /// <summary>
        /// Le bouton d'une action : son symbole, sa pastille et son libellé.
        /// Symboles de la police Windows (Segoe MDL2 Assets / Segoe Fluent Icons) :
        ///   E962 = souris, E71E = loupe, E7C2 = déplacer (quatre flèches), E72B = flèche « retour »,
        ///   EC8F = défilement (flèches haut et bas ; symbole à vérifier à l'écran),
        ///   E8C8 = copier, E8C6 = ciseaux (couper), E77F = presse-papiers (coller).
        /// Les trois clics partagent le symbole de la souris, les trois glisser-déposer celui des
        /// quatre flèches ; la pastille les distingue (G = gauche, D = droit, A = android).
        /// Les modificateurs affichent simplement leur nom (« Ctrl », « Maj », « Alt »).
        /// (Déplacé depuis ModeChoixAction à l'étape 10, sans changement.)
        /// </summary>
        public static BoutonRegard CreerBouton(ActionChoix action)
        {
            switch (action)
            {
                case ActionChoix.ClicGauche:
                    return new BoutonRegard("", "G", "Clic gauche");
                case ActionChoix.ClicDroit:
                    return new BoutonRegard("", "D", "Clic droit");
                case ActionChoix.DoubleClic:
                    return new BoutonRegard("", "2×", "Double clic");
                case ActionChoix.Loupe:
                    return new BoutonRegard("", null, "Loupe");
                case ActionChoix.GlisserGauche:
                    return new BoutonRegard("", "G", "Glisser-déposer gauche");
                case ActionChoix.GlisserDroit:
                    return new BoutonRegard("", "D", "Glisser-déposer droit");
                case ActionChoix.GlisserAndroid:
                    return new BoutonRegard("", "A", "Glisser-déposer android (pause avant de déplacer)");
                case ActionChoix.Defilement:
                    return new BoutonRegard("", null, "Défilement");
                case ActionChoix.Ctrl:
                    return BoutonRegard.Texte("Ctrl", "Ctrl (activer / désactiver)");
                case ActionChoix.Maj:
                    return BoutonRegard.Texte("Maj", "Maj (activer / désactiver)");
                case ActionChoix.Alt:
                    return BoutonRegard.Texte("Alt", "Alt (activer / désactiver)");
                case ActionChoix.Copier:
                    return new BoutonRegard("", null, "Copier (Ctrl + C)");
                case ActionChoix.Couper:
                    return new BoutonRegard("", null, "Couper (Ctrl + X)");
                case ActionChoix.Coller:
                    return new BoutonRegard("", null, "Coller ici (clic, puis Ctrl + V)");
                default:
                    return new BoutonRegard("", null, "Retour");
            }
        }
    }
}
