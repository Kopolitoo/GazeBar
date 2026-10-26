using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : l'option [DemarrerAvecWindows] (étape 7).
    ///
    /// Comment Windows lance-t-il un programme à l'ouverture de la session ? Il lit la liste
    /// rangée dans le registre (la base de réglages de Windows), à cet endroit :
    ///     HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
    /// Chaque valeur de cette « clé » est un programme à lancer : son nom (ici « GazeBar »)
    /// et le chemin complet du .exe, entre guillemets.
    ///
    /// HKEY_CURRENT_USER : seulement pour l'utilisateur actuel, et sans droits
    /// d'administrateur. Conséquence (voir le cahier des charges) : GazeBar lancé ainsi n'est
    /// pas administrateur, et les fenêtres administrateur continueront d'ignorer ses clics.
    ///
    /// Microsoft.Win32.Registry est fourni avec .NET Framework : aucune dépendance nouvelle.
    /// </summary>
    internal static class DemarrageWindows
    {
        private const string CleRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string NomValeur = "GazeBar";

        /// <summary>
        /// Inscrit GazeBar dans la liste de démarrage de Windows (actif = vrai) ou l'en retire.
        /// Ne fait jamais planter le programme : une erreur est seulement notée dans le journal.
        ///
        /// Appelée au démarrage de GazeBar (pour que le registre corresponde au fichier de
        /// réglages, et pour mettre à jour le chemin si le .exe a été déplacé) et à chaque
        /// changement de l'option dans le mode paramètres.
        /// </summary>
        public static void Appliquer(bool actif)
        {
            try
            {
                // « using » : la clé est refermée automatiquement à la fin du bloc.
                using (RegistryKey cle = Registry.CurrentUser.CreateSubKey(CleRun))
                {
                    if (cle == null)
                    {
                        App.Log("Démarrage avec Windows : clé du registre inaccessible");
                        return;
                    }

                    if (actif)
                    {
                        // Le chemin du programme en cours (par exemple
                        // D:\TRAVAIL\YEUX\GazeBar\bin\Debug\net48\GazeBar.exe).
                        string chemin = Process.GetCurrentProcess().MainModule.FileName;
                        string commande = "\"" + chemin + "\"";
                        if (!commande.Equals(cle.GetValue(NomValeur) as string, StringComparison.OrdinalIgnoreCase))
                        {
                            cle.SetValue(NomValeur, commande);
                            App.Log("Démarrage avec Windows : activé (" + commande + ")");
                        }
                    }
                    else if (cle.GetValue(NomValeur) != null)
                    {
                        cle.DeleteValue(NomValeur, false);
                        App.Log("Démarrage avec Windows : désactivé");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Démarrage avec Windows : échec : " + ex.Message);
            }
        }
    }
}
