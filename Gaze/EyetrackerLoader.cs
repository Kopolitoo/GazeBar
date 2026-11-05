using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Prunel
{
    /// <summary>
    /// MODEL: finds and loads the eyetracker DLLs at startup, WITHOUT shipping them with
    /// Prunel.
    ///
    /// The eyetracker is read with the Tobii.Interaction library (Tobii Core SDK): the file
    /// and folder names below are therefore Tobii's, they cannot change.
    ///
    /// Why? The eyetracker DLLs are proprietary code: we do not want to distribute them with
    /// Prunel. So the Tobii.Interaction NuGet package is only used to COMPILE (see
    /// Prunel.csproj: its DLLs are not copied next to Prunel.exe). At startup, this
    /// file goes and gets them where they already are on the PC.
    ///
    /// There are three DLLs:
    ///  - Tobii.Interaction.Net.dll and Tobii.Interaction.Model.dll: ".NET" DLLs (written in
    ///    C#). When .NET does not find them next to Prunel.exe, it raises the
    ///    AppDomain.AssemblyResolve event: we answer by loading the file found elsewhere
    ///    (Assembly.LoadFrom).
    ///  - Tobii.EyeX.Client.dll: a "native" DLL (written in C), called by
    ///    Tobii.Interaction.Net.dll with [DllImport]. We load it ourselves in advance with the
    ///    Windows LoadLibrary function: when Tobii.Interaction.Net.dll later asks for it by
    ///    name, Windows sees it is already loaded and reuses it.
    ///
    /// Where to look, in order:
    ///  1. the "Eyetracker" subfolder next to Prunel.exe (if the user copied the DLLs there
    ///     himself, see docs/user-guide.md, "Eyetracker files");
    ///  2. the installation folders of the eyetracker vendor's software (Program Files);
    ///  3. the Visual Studio "NuGet cache" (%USERPROFILE%\.nuget\packages): this is where
    ///     Visual Studio stores the downloaded package, so it is always present on the
    ///     development PC.
    /// </summary>
    internal static class EyetrackerLoader
    {
        /// <summary>The two .NET DLLs of the eyetracker (without the extension).</summary>
        private static readonly string[] NetDll = { "Tobii.Interaction.Net", "Tobii.Interaction.Model" };

        /// <summary>The native DLL of the eyetracker.</summary>
        private const string NativeDll = "Tobii.EyeX.Client.dll";

        /// <summary>The folder where the .NET DLLs were found (null if not found).</summary>
        private static string _netFolder;

        /// <summary>The .NET DLLs already loaded, by name (so as not to load them twice).</summary>
        private static readonly Dictionary<string, Assembly> _loaded =
            new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private static bool _installed;

        /// <summary>True if the eyetracker DLLs were found.</summary>
        public static bool Available { get; private set; }

        /// <summary>What happened, in plain words (shown in Diagnostic if the DLLs are missing).</summary>
        public static string Message { get; private set; }

        /// <summary>
        /// To call ONCE, as early as possible (App constructor), before any use of the gaze:
        /// looks for the DLLs and hooks up the AssemblyResolve event.
        /// </summary>
        public static void Install()
        {
            if (_installed)
            {
                return;
            }
            _installed = true;

            try
            {
                // 1. The .NET DLLs: the first folder containing Tobii.Interaction.Net.dll.
                foreach (string folder in CandidateFolders())
                {
                    if (File.Exists(Path.Combine(folder, NetDll[0] + ".dll")))
                    {
                        _netFolder = folder;
                        break;
                    }
                }

                if (_netFolder == null)
                {
                    Available = false;
                    Message = Texts.Get("eyetracker.dllNotFound");
                    App.Log("Eyetracker: " + Message);
                    return;
                }
                App.Log("Eyetracker: .NET DLLs found in " + _netFolder);

                // 2. The native DLL, in 32 bits (Prunel is a 32-bit program: a 64-bit DLL
                //    cannot be loaded into it). We look for it first next to the .NET DLLs
                //    (same version), then in the other folders.
                string native = FindNative32();
                if (native == null)
                {
                    // Not necessarily serious: Windows will also look for it by itself (system
                    // folders, PATH). If it is really missing, the gaze connection will fail
                    // with a message in the log.
                    App.Log("Eyetracker: " + NativeDll + " (32-bit) not found, Windows will look for it itself");
                }
                else if (NativeMethods.LoadLibrary(native) == IntPtr.Zero)
                {
                    App.Log("Eyetracker: failed to load " + native + " (Windows error " +
                            Marshal.GetLastWin32Error() + ")");
                }
                else
                {
                    App.Log("Eyetracker: native DLL loaded: " + native);
                }

                // 3. From now on, answer .NET when it looks for an eyetracker DLL.
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                Available = true;
                Message = "";
            }
            catch (Exception ex)
            {
                Available = false;
                Message = Texts.Format("eyetracker.searchError", ex.Message);
                App.Log("Eyetracker: " + Message + ": " + ex);
            }
        }

        /// <summary>
        /// Called by .NET when it does not find a .NET DLL next to Prunel.exe.
        /// We only answer for the eyetracker DLLs; for the others we return null ("I don't
        /// know"), and .NET carries on as usual.
        /// </summary>
        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            // args.Name looks like "Tobii.Interaction.Net, Version=0.7.3.0, Culture=...":
            // only the short name is kept. The version is not checked: the one installed on
            // the PC may be slightly different from 0.7.3.
            string name = new AssemblyName(args.Name).Name;
            if (Array.IndexOf(NetDll, name) < 0 || _netFolder == null)
            {
                return null;
            }

            lock (_loaded)
            {
                Assembly already;
                if (_loaded.TryGetValue(name, out already))
                {
                    return already;
                }

                string path = Path.Combine(_netFolder, name + ".dll");
                if (!File.Exists(path))
                {
                    App.Log("Eyetracker: " + name + ".dll missing from " + _netFolder);
                    return null;
                }

                Assembly assembly = Assembly.LoadFrom(path);
                _loaded[name] = assembly;
                App.Log("Eyetracker: loaded " + path + " (version " + assembly.GetName().Version + ")");
                return assembly;
            }
        }

        /// <summary>
        /// The folders where to look for Tobii.Interaction.Net.dll, in order of preference.
        /// </summary>
        private static List<string> CandidateFolders()
        {
            List<string> folders = new List<string>();

            // 1. The "Eyetracker" subfolder next to Prunel.exe.
            folders.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Eyetracker"));

            // 2. The vendor's installations (a few levels of subfolders are searched).
            foreach (string root in VendorRoots())
            {
                SearchFolders(root, NetDll[0] + ".dll", 4, folders);
            }

            // 3. The NuGet cache (development PC).
            folders.Add(Path.Combine(NuGetPackageFolder(), "lib", "net45"));

            return folders;
        }

        /// <summary>
        /// Looks for the 32-bit Tobii.EyeX.Client.dll: next to the .NET DLLs, in Prunel's
        /// "Eyetracker" folder, in the vendor's installations, then in the NuGet cache.
        /// </summary>
        private static string FindNative32()
        {
            List<string> folders = new List<string>();
            folders.Add(_netFolder);
            folders.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Eyetracker"));
            foreach (string root in VendorRoots())
            {
                SearchFolders(root, NativeDll, 4, folders);
            }
            // In the NuGet package, the 32-bit versions are stored in build\x86.
            folders.Add(Path.Combine(NuGetPackageFolder(), "build", "x86"));

            foreach (string folder in folders)
            {
                string path = Path.Combine(folder, NativeDll);
                if (File.Exists(path) && Is32Bit(path))
                {
                    return path;
                }
            }
            return null;
        }

        /// <summary>The folders where the vendor's software is usually installed.</summary>
        private static IEnumerable<string> VendorRoots()
        {
            string[] programs =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetEnvironmentVariable("ProgramW6432"),   // 64-bit "Program Files"
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };
            HashSet<string> seenSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string program in programs)
            {
                if (string.IsNullOrEmpty(program))
                {
                    continue;
                }
                foreach (string editor in new[] { "Tobii", "Tobii Dynavox" })
                {
                    string root = Path.Combine(program, editor);
                    if (seenSet.Add(root) && Directory.Exists(root))
                    {
                        yield return root;
                    }
                }
            }
        }

        /// <summary>
        /// Adds to "results" each folder, under "folder", that contains the file "fileName"
        /// (going down at most "depth" levels). Folders that cannot be read are simply
        /// skipped.
        /// </summary>
        private static void SearchFolders(string folder, string fileName, int depth, List<string> results)
        {
            try
            {
                if (File.Exists(Path.Combine(folder, fileName)))
                {
                    results.Add(folder);
                }
                if (depth <= 0)
                {
                    return;
                }
                foreach (string subFolder in Directory.GetDirectories(folder))
                {
                    SearchFolders(subFolder, fileName, depth - 1, results);
                }
            }
            catch (Exception)
            {
                // Access denied, folder deleted in the meantime...: move on to the next one.
            }
        }

        /// <summary>
        /// The folder of the Tobii.Interaction 0.7.3 package in the NuGet cache. By default
        /// %USERPROFILE%\.nuget\packages, unless the NUGET_PACKAGES variable says otherwise.
        /// </summary>
        private static string NuGetPackageFolder()
        {
            string cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (string.IsNullOrEmpty(cache))
            {
                cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                     ".nuget", "packages");
            }
            return Path.Combine(cache, "tobii.interaction", "0.7.3");
        }

        /// <summary>
        /// True if the native DLL is 32-bit. We read the file header (Windows "PE" format): at
        /// address 0x3C is the position of the "PE\0\0" signature, followed by the machine
        /// code: 0x014C = 32-bit processor (x86), 0x8664 = 64-bit.
        /// </summary>
        private static bool Is32Bit(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    stream.Seek(0x3C, SeekOrigin.Begin);
                    int positionPe = reader.ReadInt32();
                    stream.Seek(positionPe, SeekOrigin.Begin);
                    if (reader.ReadUInt32() != 0x00004550)   // "PE\0\0"
                    {
                        return false;
                    }
                    return reader.ReadUInt16() == 0x014C;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
