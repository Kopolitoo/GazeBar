using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace GazeBar
{
    /// <summary>
    /// MODEL: the [StartWithWindows] option (step 7).
    ///
    /// How does Windows start a program when the session opens? It reads the list stored
    /// in the registry (the Windows settings database), at this location:
    ///     HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
    /// Each value of this "key" is a program to start: its name (here "GazeBar") and the
    /// full path of the .exe, in quotes.
    ///
    /// HKEY_CURRENT_USER: only for the current user, and without administrator rights.
    /// Consequence (see the specification): GazeBar started this way is not administrator,
    /// and administrator windows will keep ignoring its clicks.
    ///
    /// Microsoft.Win32.Registry ships with .NET Framework: no new dependency.
    /// </summary>
    internal static class WindowsStartup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "GazeBar";

        /// <summary>
        /// Adds GazeBar to the Windows startup list (active = true) or removes it.
        /// Never crashes the program: an error is only written to the log.
        ///
        /// Called when GazeBar starts (so that the registry matches the settings file, and
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

                    if (active)
                    {
                        // The path of the running program (for example
                        // D:\TRAVAIL\YEUX\GazeBar\bin\Debug\net48\GazeBar.exe).
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
