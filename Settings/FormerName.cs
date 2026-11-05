using System;
using System.Collections.Generic;
using System.IO;

namespace Prunel
{
    /// <summary>
    /// Prunel was called GazeBar until version 0.2.0. Its folders had the old name:
    ///     %APPDATA%\GazeBar          settings.json, personal-dictionary.json
    ///     %LOCALAPPDATA%\GazeBar     the log
    ///     Documents\GazeBar          the Profiles folder
    /// At the first start of Prunel, each of these folders is renamed to "Prunel", so that the
    /// user finds the settings, the personal words and the profiles again.
    ///
    /// A folder is only moved when the new one does not exist yet: nothing is ever overwritten.
    /// If the move is refused (a file still open, for example), the files are copied instead
    /// and the old folder stays where it is.
    ///
    /// Called by App.OnStartup BEFORE the first line of log (the log would otherwise create the
    /// new %LOCALAPPDATA%\Prunel folder first). The messages are kept and written to the log
    /// afterwards by WriteLog.
    /// </summary>
    internal static class FormerName
    {
        /// <summary>The name of the program before version 0.3.0.</summary>
        public const string Name = "GazeBar";

        // What happened, written to the log by WriteLog (the log is not ready during Migrate).
        private static readonly List<string> Messages = new List<string>();

        /// <summary>Renames the three old folders, when they exist. Never throws.</summary>
        public static void Migrate()
        {
            MoveFolder(Environment.SpecialFolder.ApplicationData);
            MoveFolder(Environment.SpecialFolder.LocalApplicationData);
            MoveFolder(Environment.SpecialFolder.MyDocuments);
        }

        /// <summary>Writes to the log what Migrate did (nothing when there was nothing to do).</summary>
        public static void WriteLog()
        {
            foreach (string message in Messages)
            {
                App.Log(message);
            }
            Messages.Clear();
        }

        private static void MoveFolder(Environment.SpecialFolder parent)
        {
            try
            {
                string root = Environment.GetFolderPath(parent);
                if (string.IsNullOrEmpty(root))
                {
                    return;
                }
                string oldFolder = Path.Combine(root, Name);
                string newFolder = Path.Combine(root, "Prunel");
                if (!Directory.Exists(oldFolder) || Directory.Exists(newFolder))
                {
                    return;
                }

                try
                {
                    Directory.Move(oldFolder, newFolder);
                    Messages.Add("Former name: " + parent + " folder renamed " + Name + " -> Prunel");
                }
                catch (Exception moveError)
                {
                    // The move failed: copy the files instead (the old folder stays).
                    CopyFolder(oldFolder, newFolder);
                    Messages.Add("Former name: " + parent + " folder copied " + Name + " -> Prunel (move refused: "
                                 + moveError.Message + ")");
                }
            }
            catch (Exception ex)
            {
                Messages.Add("Former name: " + parent + " folder not taken over: " + ex.Message);
            }
        }

        // Copies a folder with its subfolders. An existing file is never replaced.
        private static void CopyFolder(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(file));
                if (!File.Exists(target))
                {
                    File.Copy(file, target);
                }
            }
            foreach (string folder in Directory.GetDirectories(source))
            {
                CopyFolder(folder, Path.Combine(destination, Path.GetFileName(folder)));
            }
        }
    }
}
