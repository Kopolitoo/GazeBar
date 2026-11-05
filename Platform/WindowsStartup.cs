using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace Prunel
{
    /// <summary>
    /// MODEL: the [StartWithWindows] option.
    ///
    /// How does Windows start a program when the session opens? It reads the list stored
    /// in the registry (the Windows settings database), at this location:
    ///     HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
    /// Each value of this "key" is a program to start: its name (here "Prunel") and the
    /// full path of the .exe, in quotes.
    ///
    /// HKEY_CURRENT_USER: only for the current user, and without administrator rights.
    /// Consequence (see the specification): Prunel started this way is not administrator,
    /// and administrator windows will keep ignoring its clicks.
    ///
    /// Microsoft.Win32.Registry ships with .NET Framework: no new dependency.
    /// </summary>
    internal static class WindowsStartup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Prunel";

        /// <summary>
        /// Adds Prunel to the Windows startup list (active = true) or removes it.
        /// Never crashes the program: an error is only written to the log.
        ///
        /// Called when Prunel starts (so that the registry matches the settings file, and
        /// to update the path if the .exe was moved) and each time the option is changed
        /// in settings mode.
        /// </summary>
        public static void Apply(bool active)
        {
            try
            {
                // "using": the key is closed automatically at the end of the block.
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null)
                    {
                        App.Log("Start with Windows: registry key unreachable");
                        return;
                    }

                    // The value of the former name (GazeBar, before version 0.3.0) would start
                    // the old program: it is always removed.
                    if (key.GetValue(FormerName.Name) != null)
                    {
                        key.DeleteValue(FormerName.Name, false);
                        App.Log("Start with Windows: old " + FormerName.Name + " value removed");
                    }

                    if (active)
                    {
                        // The path of the running program (for example
                        // D:\TRAVAIL\YEUX\Prunel\bin\Debug\net48\Prunel.exe).
                        string path = Process.GetCurrentProcess().MainModule.FileName;
                        string command = "\"" + path + "\"";
                        if (!command.Equals(key.GetValue(ValueName) as string, StringComparison.OrdinalIgnoreCase))
                        {
                            key.SetValue(ValueName, command);
                            App.Log("Start with Windows: on (" + command + ")");
                        }
                    }
                    else if (key.GetValue(ValueName) != null)
                    {
                        key.DeleteValue(ValueName, false);
                        App.Log("Start with Windows: off");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Start with Windows: failed: " + ex.Message);
            }
        }
    }
}
