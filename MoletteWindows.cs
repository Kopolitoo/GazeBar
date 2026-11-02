using System;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : l'option [DefilerFenetresInactives] (demande de l'utilisateur du 02/11/2026,
    /// onglet « Défilement » des paramètres).
    ///
    /// C'est le réglage de Windows 10/11 « Faire défiler les fenêtres inactives lorsque je
    /// pointe dessus » (Paramètres Windows → Bluetooth et appareils → Souris). Il décide à
    /// quelle fenêtre Windows envoie la molette :
    ///   - activé : à la fenêtre SOUS LE CURSEUR, même si elle n'est pas active. C'est ce
    ///     qu'il faut au mode défilement de GazeBar, qui pose le curseur sur le point visé ;
    ///   - désactivé : toujours à la fenêtre ACTIVE, même si le curseur est ailleurs. Il faut
    ///     alors cliquer dans une fenêtre avant de pouvoir la faire défiler.
    ///
    /// GazeBar ne fait que modifier ce réglage de Windows, avec la fonction
    /// SystemParametersInfo (fournie par Windows : aucune dépendance nouvelle). Le
    /// changement est enregistré par Windows : il reste valable même quand GazeBar est fermé.
    ///
    /// Valeurs du réglage dans Windows : 0 = fenêtre active, 1 = mixte (fenêtre active pour
    /// les programmes classiques, fenêtre sous le curseur pour les applications du Store),
    /// 2 = fenêtre sous le curseur.
    /// </summary>
    internal static class MoletteWindows
    {
        private const uint FenetreActive = 0;          // MOUSEWHEEL_ROUTING_FOCUS
        private const uint SousLeCurseur = 2;          // MOUSEWHEEL_ROUTING_MOUSE_POS

        /// <summary>
        /// Lit le réglage actuel de Windows. Renvoie null s'il est illisible (Windows trop
        /// ancien, par exemple).
        /// </summary>
        public static uint? Lire()
        {
            try
            {
                uint valeur = 0;
                if (NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETMOUSEWHEELROUTING, 0, ref valeur, 0))
                {
                    return valeur;
                }
            }
            catch (Exception ex)
            {
                App.Log("Molette : lecture du réglage Windows impossible : " + ex.Message);
            }
            return null;
        }

        /// <summary>Décrit le réglage actuel de Windows, pour le journal.</summary>
        public static string Description()
        {
            uint? valeur = Lire();
            if (valeur == null)
            {
                return "inconnu";
            }
            if (valeur.Value == SousLeCurseur)
            {
                return "activé";
            }
            return "désactivé (" + valeur.Value + ")";
        }

        /// <summary>
        /// Met le réglage de Windows d'accord avec l'option de GazeBar (actif = vrai : la molette
        /// va à la fenêtre sous le curseur). Ne fait rien s'il l'est déjà. Ne fait jamais planter
        /// le programme : une erreur est seulement notée dans le journal.
        ///
        /// Appelée au démarrage de GazeBar et à chaque changement de l'option dans le mode
        /// paramètres (comme DemarrageWindows.Appliquer).
        /// </summary>
        public static void Appliquer(bool actif)
        {
            try
            {
                uint voulu = actif ? SousLeCurseur : FenetreActive;
                uint? actuel = Lire();
                if (actuel == voulu)
                {
                    return;
                }

                // SPIF_UPDATEINIFILE : Windows enregistre le changement (il reste après un
                // redémarrage). SPIF_SENDCHANGE : les programmes ouverts sont prévenus.
                uint drapeaux = NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE;

                // La documentation de Microsoft dit que la valeur se donne par son ADRESSE
                // (« pointeur »). Certains programmes la passent directement à la place de
                // l'adresse : si la première façon n'a pas pris, on essaie la seconde. Dans les
                // deux cas, on relit le réglage pour vérifier.
                uint valeur = voulu;
                NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETMOUSEWHEELROUTING, 0, ref valeur, drapeaux);
                if (Lire() != voulu)
                {
                    NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETMOUSEWHEELROUTING, 0, new IntPtr(voulu), drapeaux);
                }

                App.Log("Molette : réglage Windows « défiler les fenêtres inactives » " +
                        (actif ? "activé" : "désactivé") + " par GazeBar, résultat : " + Description());
            }
            catch (Exception ex)
            {
                App.Log("Molette : impossible de changer le réglage Windows : " + ex.Message);
            }
        }
    }
}
