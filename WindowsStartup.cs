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
    internal static class WindowsStartup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "GazeBar";

        /// <summary>
        /// Inscrit GazeBar dans la liste de démarrage de Windows (actif = vrai) ou l'en retire.
        /// Ne fait jamais planter le programme : une erreur est seulement notée dans le journal.
        ///
        /// Appelée au démarrage de GazeBar (pour que le registre corresponde au fichier de
        /// réglages, et pour mettre à jour le chemin si le .exe a été déplacé) et à chaque
        /// changement de l'option dans le mode paramètres.
        /// </summary>
        public static void Apply(bool active)
        {
            try
            {
                // « using » : la clé est refermée automatiquement à la fin du bloc.
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null)
                    {
                        App.Log("Démarrage avec Windows : clé du registre inaccessible");
                        return;
                    }

                    if (active)
                    {
                        // Le chemin du programme en cours (par exemple
                        // D:\TRAVAIL\YEUX\GazeBar\bin\Debug\net48\GazeBar.exe).
                        string path = Process.GetCurrentProcess().MainModule.FileName;
                        string command = "\"" + path + "\"";
                        if (!command.Equals(key.GetValue(ValueName) as string, StringComparison.OrdinalIgnoreCase))
                        {
                            key.SetValue(ValueName, command);
                            App.Log("Démarrage avec Windows : activé (" + command + ")");
                        }
                    }
                    else if (key.GetValue(ValueName) != null)
                    {
                        key.DeleteValue(ValueName, false);
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
