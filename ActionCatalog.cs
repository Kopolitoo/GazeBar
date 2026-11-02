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
    internal enum ChoiceAction
    {
        LeftClick,
        RightClick,
        DoubleClick,
        Magnifier,
        DragLeft,
        DragRight,
        DragAndroid,
        Scroll,
        Ctrl,
        Shift,
        Alt,
        Copy,
        Cut,
        Paste,
        Back,

        /// <summary>
        /// Ouvre (ou ferme) le clavier au regard (étape 12, demande de l'utilisateur du
        /// 01/11/2026). Ajouté à la fin : les noms déjà écrits dans parametres.json ne changent pas.
        /// </summary>
        Keyboard
    }

    /// <summary>
    /// Les trois groupes d'emplacements du choix d'action. Chaque groupe a sa propre liste
    /// d'actions : une action de la couronne ne peut pas aller à gauche, par exemple (le
    /// cahier des charges sépare les listes : « les trois emplacements de gauche pour les
    /// modificateurs, les huit emplacements du centre pour les actions »).
    /// </summary>
    internal enum IconGroup
    {
        /// <summary>Les huit emplacements autour de la photo.</summary>
        Crown,

        /// <summary>Les trois emplacements collés au bord gauche (Ctrl, Maj, Alt).</summary>
        Modifiers,

        /// <summary>Les trois emplacements collés au bord haut (Copier, Couper, Coller).</summary>
        Utilities
    }

    /// <summary>
    /// LE CATALOGUE DES ACTIONS : pour chaque groupe, les actions qu'on peut y placer, leur
    /// disposition par défaut, et pour chaque action son bouton et son nom.
    ///
    /// Pour ajouter une action plus tard : l'ajouter dans ActionChoix (à la fin, de
    /// préférence), dans la liste de son groupe (Actions), dans CreerBouton et NomCourt,
    /// puis programmer ce qu'elle fait dans ModeChoixAction.
    /// </summary>
    internal static class ActionCatalog
    {
        /// <summary>Nombre d'emplacements de chaque groupe.</summary>
        public static int SlotCount(IconGroup group)
        {
            return group == IconGroup.Crown ? 8 : 3;
        }

        /// <summary>
        /// Les actions qu'on peut placer dans ce groupe, dans l'ordre où la liste de
        /// l'écran de configuration les présente.
        /// </summary>
        public static ChoiceAction[] Actions(IconGroup group)
        {
            switch (group)
            {
                case IconGroup.Crown:
                    return new[]
                    {
                        ChoiceAction.LeftClick,
                        ChoiceAction.RightClick,
                        ChoiceAction.DoubleClick,
                        ChoiceAction.Magnifier,
                        ChoiceAction.DragLeft,
                        ChoiceAction.DragRight,
                        ChoiceAction.DragAndroid,
                        ChoiceAction.Scroll,
                        // Étape 12 (01/11/2026) : proposé dans la liste de l'écran de
                        // configuration, mais pas placé par défaut (les huit places sont prises).
                        ChoiceAction.Keyboard
                    };
                case IconGroup.Modifiers:
                    return new[] { ChoiceAction.Ctrl, ChoiceAction.Shift, ChoiceAction.Alt };
                default:
                    return new[] { ChoiceAction.Copy, ChoiceAction.Cut, ChoiceAction.Paste };
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
        /// L'action Clavier (étape 12) n'est pas placée par défaut : on l'ajoute soi-même à la
        /// place d'une autre, dans l'écran de configuration des icônes.
        /// </summary>
        public static ChoiceAction[] DefaultLayout(IconGroup group)
        {
            if (group == IconGroup.Crown)
            {
                return new[]
                {
                    ChoiceAction.Scroll,
                    ChoiceAction.RightClick,
                    ChoiceAction.DragLeft,
                    ChoiceAction.DragRight,
                    ChoiceAction.Magnifier,
                    ChoiceAction.DragAndroid,
                    ChoiceAction.DoubleClick,
                    ChoiceAction.LeftClick
                };
            }
            // Pour les deux autres groupes, la disposition par défaut est la liste elle-même.
            return Actions(group);
        }

        /// <summary>Vrai si cette action peut être placée dans ce groupe.</summary>
        public static bool BelongsTo(ChoiceAction action, IconGroup group)
        {
            return Array.IndexOf(Actions(group), action) >= 0;
        }

        /// <summary>
        /// Lit un nom d'action écrit dans parametres.json (par exemple "ClicGauche").
        /// Renvoie null pour un emplacement vide ("" ou null), un nom inconnu (faute de
        /// frappe, action supprimée) ou une action qui n'appartient pas à ce groupe.
        /// </summary>
        public static ChoiceAction? Read(string name, IconGroup group)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            ChoiceAction action;
            // « true » : les majuscules ne comptent pas (« clicgauche » est accepté).
            if (!Enum.TryParse(name.Trim(), true, out action) || !Enum.IsDefined(typeof(ChoiceAction), action))
            {
                return null;
            }
            return BelongsTo(action, group) ? action : (ChoiceAction?)null;
        }

        /// <summary>Le nom écrit dans parametres.json : "" pour un emplacement vide.</summary>
        public static string Write(ChoiceAction? action)
        {
            return action.HasValue ? action.Value.ToString() : "";
        }

        /// <summary>Vrai pour les modificateurs Ctrl, Maj, Alt.</summary>
        public static bool IsModifier(ChoiceAction action)
        {
            return action == ChoiceAction.Ctrl || action == ChoiceAction.Shift || action == ChoiceAction.Alt;
        }

        /// <summary>Le modificateur qui correspond à l'action Ctrl, Maj ou Alt.</summary>
        public static Modifier ToModifier(ChoiceAction action)
        {
            switch (action)
            {
                case ChoiceAction.Ctrl:
                    return Modifier.Ctrl;
                case ChoiceAction.Shift:
                    return Modifier.Shift;
                default:
                    return Modifier.Alt;
            }
        }

        /// <summary>
        /// Un nom court, affiché sous chaque action dans la liste de l'écran de configuration
        /// (le libellé du bouton, plus long, est parfois trop large pour la grille).
        /// </summary>
        public static string ShortName(ChoiceAction action)
        {
            switch (action)
            {
                case ChoiceAction.LeftClick: return "Clic gauche";
                case ChoiceAction.RightClick: return "Clic droit";
                case ChoiceAction.DoubleClick: return "Double clic";
                case ChoiceAction.Magnifier: return "Loupe";
                case ChoiceAction.DragLeft: return "Glisser gauche";
                case ChoiceAction.DragRight: return "Glisser droit";
                case ChoiceAction.DragAndroid: return "Glisser android";
                case ChoiceAction.Scroll: return "Défilement";
                case ChoiceAction.Ctrl: return "Ctrl";
                case ChoiceAction.Shift: return "Maj";
                case ChoiceAction.Alt: return "Alt";
                case ChoiceAction.Copy: return "Copier";
                case ChoiceAction.Cut: return "Couper";
                case ChoiceAction.Paste: return "Coller";
                case ChoiceAction.Keyboard: return "Clavier";
                default: return "Retour";
            }
        }

        /// <summary>
        /// Le bouton d'une action : son symbole, sa pastille et son libellé.
        /// Symboles de la police Windows (Segoe MDL2 Assets / Segoe Fluent Icons) :
        ///   E962 = souris, E71E = loupe, E7C2 = déplacer (quatre flèches), E72B = flèche « retour »,
        ///   EC8F = défilement (flèches haut et bas ; symbole à vérifier à l'écran),
        ///   E8C8 = copier, E8C6 = ciseaux (couper), E77F = presse-papiers (coller),
        ///   E765 = clavier (étape 12).
        /// Les trois clics partagent le symbole de la souris, les trois glisser-déposer celui des
        /// quatre flèches ; la pastille les distingue (G = gauche, D = droit, A = android).
        /// Les modificateurs affichent simplement leur nom (« Ctrl », « Maj », « Alt »).
        /// (Déplacé depuis ModeChoixAction à l'étape 10, sans changement.)
        /// </summary>
        public static GazeButton CreateButton(ChoiceAction action)
        {
            switch (action)
            {
                case ChoiceAction.LeftClick:
                    return new GazeButton("", "G", "Clic gauche");
                case ChoiceAction.RightClick:
                    return new GazeButton("", "D", "Clic droit");
                case ChoiceAction.DoubleClick:
                    return new GazeButton("", "2×", "Double clic");
                case ChoiceAction.Magnifier:
                    return new GazeButton("", null, "Loupe");
                case ChoiceAction.DragLeft:
                    return new GazeButton("", "G", "Glisser-déposer gauche");
                case ChoiceAction.DragRight:
                    return new GazeButton("", "D", "Glisser-déposer droit");
                case ChoiceAction.DragAndroid:
                    return new GazeButton("", "A", "Glisser-déposer android (pause avant de déplacer)");
                case ChoiceAction.Scroll:
                    return new GazeButton("", null, "Défilement");
                case ChoiceAction.Ctrl:
                    return GazeButton.Text("Ctrl", "Ctrl (activer / désactiver)");
                case ChoiceAction.Shift:
                    return GazeButton.Text("Maj", "Maj (activer / désactiver)");
                case ChoiceAction.Alt:
                    return GazeButton.Text("Alt", "Alt (activer / désactiver)");
                case ChoiceAction.Copy:
                    return new GazeButton("", null, "Copier (Ctrl + C)");
                case ChoiceAction.Cut:
                    return new GazeButton("", null, "Couper (Ctrl + X)");
                case ChoiceAction.Paste:
                    return new GazeButton("", null, "Coller ici (clic, puis Ctrl + V)");
                case ChoiceAction.Keyboard:
                    // Pastille vide : le même bouton ouvre le clavier, ou le ferme s'il est ouvert.
                    return new GazeButton("\uE765", null, "Clavier (ouvrir / fermer)");
                default:
                    return new GazeButton("", null, "Retour");
            }
        }
    }
}
