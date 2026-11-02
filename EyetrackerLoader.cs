using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : trouve et charge les DLL de l'eyetracker au démarrage, SANS qu'elles soient livrées
    /// avec GazeBar (02/11/2026).
    ///
    /// L'eyetracker est lu avec la bibliothèque Tobii.Interaction (Tobii Core SDK) : les noms de
    /// fichiers et de dossiers ci-dessous sont donc ceux de Tobii, ils ne peuvent pas changer.
    ///
    /// Pourquoi ? Les DLL de l'eyetracker sont du code propriétaire : on ne veut pas les distribuer
    /// avec GazeBar. Le paquet NuGet Tobii.Interaction sert donc seulement à COMPILER
    /// (voir GazeBar.csproj : ses DLL ne sont plus copiées à côté de GazeBar.exe). Au
    /// lancement, c'est ce fichier qui va les chercher là où elles sont déjà sur le PC.
    ///
    /// Il y a trois DLL :
    ///  - Tobii.Interaction.Net.dll et Tobii.Interaction.Model.dll : des DLL « .NET » (écrites
    ///    en C#). Quand .NET ne les trouve pas à côté de GazeBar.exe, il déclenche l'événement
    ///    AppDomain.AssemblyResolve : on lui répond en chargeant le fichier trouvé ailleurs
    ///    (Assembly.LoadFrom).
    ///  - Tobii.EyeX.Client.dll : une DLL « native » (écrite en C), appelée par
    ///    Tobii.Interaction.Net.dll avec [DllImport]. On la charge nous-mêmes à l'avance avec
    ///    la fonction LoadLibrary de Windows : quand Tobii.Interaction.Net.dll la demandera
    ///    ensuite par son nom, Windows verra qu'elle est déjà chargée et la réutilisera.
    ///
    /// Où chercher, dans l'ordre :
    ///  1. le sous-dossier « Eyetracker » à côté de GazeBar.exe (si l'utilisateur y a copié les DLL
    ///     lui-même, voir le README) ;
    ///  2. les dossiers d'installation des logiciels du fabricant de l'eyetracker (Program Files) ;
    ///  3. le « cache NuGet » de Visual Studio (%USERPROFILE%\.nuget\packages) : c'est là que
    ///     Visual Studio range le paquet téléchargé, donc toujours présent sur le PC de
    ///     développement.
    ///
    /// Important : ce code n'a pas pu être compilé ni essayé.
    /// </summary>
    internal static class EyetrackerLoader
    {
        /// <summary>Les deux DLL .NET de l'eyetracker (sans l'extension).</summary>
        private static readonly string[] NetDll = { "Tobii.Interaction.Net", "Tobii.Interaction.Model" };

        /// <summary>La DLL native de l'eyetracker.</summary>
        private const string NativeDll = "Tobii.EyeX.Client.dll";

        /// <summary>Le dossier où les DLL .NET ont été trouvées (null si introuvables).</summary>
        private static string _netFolder;

        /// <summary>Les DLL .NET déjà chargées, par nom (pour ne pas les charger deux fois).</summary>
        private static readonly Dictionary<string, Assembly> _loaded =
            new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private static bool _installed;

        /// <summary>Vrai si les DLL de l'eyetracker ont été trouvées.</summary>
        public static bool Available { get; private set; }

        /// <summary>Ce qui s'est passé, en clair (affiché dans Diagnostic si les DLL manquent).</summary>
        public static string Message { get; private set; }

        /// <summary>
        /// À appeler UNE fois, le plus tôt possible (constructeur de App), avant toute
        /// utilisation du regard : cherche les DLL et branche l'événement AssemblyResolve.
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
                // 1. Les DLL .NET : le premier dossier qui contient Tobii.Interaction.Net.dll.
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
                    Message = "DLL de l'eyetracker introuvables (Tobii.Interaction.Net.dll). Voir le README, " +
                              "section « Installation ».";
                    App.Log("Eyetracker : " + Message);
                    return;
                }
                App.Log("Eyetracker : DLL .NET trouvées dans " + _netFolder);

                // 2. La DLL native, en 32 bits (GazeBar est un programme 32 bits : une DLL
                //    64 bits ne peut pas s'y charger). On la cherche d'abord à côté des DLL .NET
                //    (même version), puis dans les autres dossiers.
                string native = FindNative32();
                if (native == null)
                {
                    // Pas forcément grave : Windows la cherchera aussi de lui-même (dossiers
                    // système, PATH). Si elle manque vraiment, la connexion au regard échouera
                    // avec un message dans le journal.
                    App.Log("Eyetracker : " + NativeDll + " (32 bits) introuvable, Windows la cherchera lui-même");
                }
                else if (NativeMethods.LoadLibrary(native) == IntPtr.Zero)
                {
                    App.Log("Eyetracker : échec du chargement de " + native + " (erreur Windows " +
                            Marshal.GetLastWin32Error() + ")");
                }
                else
                {
                    App.Log("Eyetracker : DLL native chargée : " + native);
                }

                // 3. On répond désormais à .NET quand il cherche une DLL de l'eyetracker.
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                Available = true;
                Message = "";
            }
            catch (Exception ex)
            {
                Available = false;
                Message = "Erreur en cherchant les DLL de l'eyetracker : " + ex.Message;
                App.Log("Eyetracker : " + Message + " : " + ex);
            }
        }

        /// <summary>
        /// Appelée par .NET quand il ne trouve pas une DLL .NET à côté de GazeBar.exe.
        /// On ne répond que pour les DLL de l'eyetracker ; pour les autres on renvoie null
        /// (« je ne sais pas »), et .NET continue comme d'habitude.
        /// </summary>
        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            // args.Name ressemble à « Tobii.Interaction.Net, Version=0.7.3.0, Culture=… » :
            // on ne garde que le nom court. On ne vérifie pas la version : celle installée
            // sur le PC peut être un peu différente de 0.7.3.
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
                    App.Log("Eyetracker : " + name + ".dll absente de " + _netFolder);
                    return null;
                }

                Assembly assembly = Assembly.LoadFrom(path);
                _loaded[name] = assembly;
                App.Log("Eyetracker : chargé " + path + " (version " + assembly.GetName().Version + ")");
                return assembly;
            }
        }

        /// <summary>
        /// Les dossiers où chercher Tobii.Interaction.Net.dll, dans l'ordre de préférence.
        /// </summary>
        private static List<string> CandidateFolders()
        {
            List<string> folders = new List<string>();

            // 1. Le sous-dossier « Eyetracker » à côté de GazeBar.exe.
            folders.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Eyetracker"));

            // 2. Les installations du fabricant (on fouille quelques niveaux de sous-dossiers).
            foreach (string root in VendorRoots())
            {
                SearchFolders(root, NetDll[0] + ".dll", 4, folders);
            }

            // 3. Le cache NuGet (PC de développement).
            folders.Add(Path.Combine(NuGetPackageFolder(), "lib", "net45"));

            return folders;
        }

        /// <summary>
        /// Cherche Tobii.EyeX.Client.dll en 32 bits : à côté des DLL .NET, dans le dossier
        /// « Eyetracker » de GazeBar, dans les installations du fabricant, puis dans le cache NuGet.
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
            // Dans le paquet NuGet, les versions 32 bits sont rangées dans build\x86.
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

        /// <summary>Les dossiers où les logiciels du fabricant s'installent d'habitude.</summary>
        private static IEnumerable<string> VendorRoots()
        {
            string[] programs =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetEnvironmentVariable("ProgramW6432"),   // « Program Files » 64 bits
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
        /// Ajoute à « resultats » chaque dossier, sous « dossier », qui contient le fichier
        /// « nomFichier » (en descendant de « profondeur » niveaux au plus). Les dossiers
        /// interdits en lecture sont simplement sautés.
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
                // Accès refusé, dossier supprimé entre-temps… : on passe au suivant.
            }
        }

        /// <summary>
        /// Le dossier du paquet Tobii.Interaction 0.7.3 dans le cache NuGet. Par défaut
        /// %USERPROFILE%\.nuget\packages, sauf si la variable NUGET_PACKAGES dit autre chose.
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
        /// Vrai si la DLL native est en 32 bits. On lit l'en-tête du fichier (format « PE » de
        /// Windows) : à l'adresse 0x3C se trouve la position de la signature « PE\0\0 », suivie
        /// du code de la machine : 0x014C = processeur 32 bits (x86), 0x8664 = 64 bits.
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
                    if (reader.ReadUInt32() != 0x00004550)   // « PE\0\0 »
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
