using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace Prunel
{
    /// <summary>
    /// Part of AppSettings: the icon layout of the action choice (crown, modifiers,
    /// utility buttons). See AppSettings.cs for the settings themselves.
    /// </summary>
    internal sealed partial class AppSettings
    {
        /// <summary>
        /// A group's layout, read from the lists above: an array with one cell per slot, null
        /// for an empty slot. Not saved as such.
        /// </summary>
        public ChoiceAction?[] Layout(IconGroup group)
        {
            string[] names = Names(group) ?? new string[0];
            ChoiceAction?[] result = new ChoiceAction?[ActionCatalog.SlotCount(group)];
            for (int i = 0; i < result.Length && i < names.Length; i++)
            {
                result[i] = ActionCatalog.Read(names[i], group);
            }
            return result;
        }

        /// <summary>
        /// Places an action on a slot (null = empty it). The same action may be placed on
        /// several slots of the group. Does not save the file
        /// (see Save).
        /// </summary>
        public void SetSlot(IconGroup group, int num, ChoiceAction? action)
        {
            ChoiceAction?[] layout = Layout(group);
            if (num < 0 || num >= layout.Length)
            {
                return;
            }
            layout[num] = action;
            WriteLayout(group, layout);
        }

        /// <summary>Restores the original layout of the three groups ("Défaut" button of the "Icônes" tab).</summary>
        public void DefaultIcons()
        {
            CrownIcons = DefaultNames(IconGroup.Crown);
            ModifierIcons = DefaultNames(IconGroup.Modifiers);
            UtilityIcons = DefaultNames(IconGroup.Utilities);
        }

        /// <summary>A group's list of names, as saved in the file.</summary>
        private string[] Names(IconGroup group)
        {
            switch (group)
            {
                case IconGroup.Crown:
                    return CrownIcons;
                case IconGroup.Modifiers:
                    return ModifierIcons;
                default:
                    return UtilityIcons;
            }
        }

        /// <summary>Writes a layout (one cell per slot) into the group's list of names.</summary>
        private void WriteLayout(IconGroup group, ChoiceAction?[] layout)
        {
            string[] names = new string[layout.Length];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = ActionCatalog.Write(layout[i]);
            }
            switch (group)
            {
                case IconGroup.Crown:
                    CrownIcons = names;
                    break;
                case IconGroup.Modifiers:
                    ModifierIcons = names;
                    break;
                default:
                    UtilityIcons = names;
                    break;
            }
        }

        private static string[] DefaultNames(IconGroup group)
        {
            ChoiceAction[] actions = ActionCatalog.DefaultLayout(group);
            string[] names = new string[actions.Length];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = actions[i].ToString();
            }
            return names;
        }

        /// <summary>
        /// Tidies a group's list read from the file: the right number of cells, and unknown
        /// names or names of another group emptied (an action may appear several times). A
        /// missing list takes the default layout.
        /// </summary>
        private void FixIcons(IconGroup group)
        {
            if (Names(group) == null)
            {
                WriteLayout(group, Convert(ActionCatalog.DefaultLayout(group)));
                return;
            }
            WriteLayout(group, Layout(group));
        }

        private static ChoiceAction?[] Convert(ChoiceAction[] actions)
        {
            ChoiceAction?[] result = new ChoiceAction?[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                result[i] = actions[i];
            }
            return result;
        }
    }
}
