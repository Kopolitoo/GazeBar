using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : une disposition du clavier au regard (étape 12, « clavier-base »), lue dans un
    /// fichier JSON du dossier Claviers (par exemple Claviers/fr-azerty.json).
    ///
    /// Pourquoi un fichier et pas du code ? Pour pouvoir ajouter une langue ou changer une
    /// touche plus tard SANS recompiler : il suffit de modifier ou d'ajouter un fichier
    /// (demande de l'utilisateur du 31/10/2026). Le fichier est relu à chaque ouverture du
    /// clavier : une modification se voit dès la prochaine ouverture.
    ///
    /// Le clavier est une GRILLE : [Colonnes] colonnes sur [Lignes] lignes (12 × 4 pour le
    /// français). La ligne 0 est celle du haut (Tab, propositions de mots, effacer…).
    /// Chaque page (lettres, accents, chiffres, symboles, fonctions) liste ses touches ; chaque
    /// touche dit où elle est (ligne, col, largeur en cases) et ce qu'elle fait.
    ///
    /// Ce qu'une touche fait (une seule de ces lignes par touche) :
    ///   "texte": "é"           écrit ce texte (en Unicode : voir InjecteurClavier.TaperTexte) ;
    ///   "touche": "Tab"        appuie sur une vraie touche (Tab, Entree, Echap, Suppr, Debut,
    ///                          Fin, PageHaut, PageBas, Gauche, Droite, Haut, Bas, F1 à F12,
    ///                          Windows = la touche Windows seule, qui ouvre le menu Démarrer) ;
    ///   "raccourci": "Ctrl+F"  un raccourci (Ctrl, Alt, Maj ou Win + une touche) ;
    ///   "page": "accents"      affiche une autre page ("retour" = la page d'avant) ;
    ///   "modificateur": "Maj"  Maj, VerrMaj (Caps Lock), Ctrl, Alt ou Win ;
    ///   "prediction": 1        case de proposition de mot n° 1, 2 ou 3 (remplie par la prédiction, étape 13) ;
    ///   "commande": "EffacerMot"  EffacerMot ou EffacerLettre.
    /// Et pour l'aspect :
    ///   "libelle"   le texte affiché sur la touche ;
    ///   "texteMaj"  facultatif : le texte écrit avec Maj ou Caps Lock (sinon : le texte en
    ///               majuscules, é → É) ;
    ///   "style"     facultatif : "clair" (fond gris clair, comme les chiffres), "fonction"
    ///               (fond sombre) ou "lettre" (pas de fond). Par défaut : "lettre" pour une
    ///               touche qui écrit du texte, "fonction" pour les autres.
    ///
    /// Le fichier est lu avec DataContractJsonSerializer, comme parametres.json (fourni avec
    /// .NET Framework : aucune dépendance en plus). Attention : le JSON n'accepte pas de
    /// commentaires ; c'est pourquoi le format est expliqué ici.
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    [DataContract(Name = "Disposition", Namespace = "")]
    internal sealed class KeyboardLayout
    {
        /// <summary>Le nom du dossier des dispositions, à côté de GazeBar.exe.</summary>
        public const string FolderName = "Keyboards";

        /// <summary>
        /// La disposition par défaut. Depuis l'étape 14, on en choisit une autre avec le bouton
        /// « Langue » de la barre du clavier ([DispositionClavier] dans parametres.json).
        /// </summary>
        public const string DefaultName = "fr-azerty";

        /// <summary>Le nom affiché, par exemple « Français AZERTY ».</summary>
        [DataMember(Name = "nom", Order = 0)]
        public string Name { get; set; }

        /// <summary>La langue du dictionnaire de mots (« fr »), pour la prédiction (étape 13).</summary>
        [DataMember(Name = "langue", Order = 1)]
        public string Language { get; set; }

        /// <summary>Nombre de colonnes de la grille (12 pour le français).</summary>
        [DataMember(Name = "colonnes", Order = 2)]
        public int Columns { get; set; }

        /// <summary>Nombre de lignes de la grille, ligne du haut comprise (4 pour le français).</summary>
        [DataMember(Name = "lignes", Order = 3)]
        public int Rows { get; set; }

        /// <summary>Le nom de la page affichée à l'ouverture du clavier.</summary>
        [DataMember(Name = "pageDepart", Order = 4)]
        public string StartPage { get; set; }

        [DataMember(Name = "pages", Order = 5)]
        public List<KeyboardPage> Pages { get; set; }

        /// <summary>La page qui porte ce nom, ou null.</summary>
        public KeyboardPage Page(string name)
        {
            if (Pages == null)
            {
                return null;
            }
            foreach (KeyboardPage page in Pages)
            {
                if (string.Equals(page.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return page;
                }
            }
            return null;
        }

        /// <summary>Le chemin complet du fichier d'une disposition (à côté de GazeBar.exe).</summary>
        public static string PathOf(string name)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName, name + ".json");
        }

        /// <summary>
        /// Lit une disposition. Renvoie null si le fichier est absent ou illisible : erreur
        /// expliquée dans « message » (affiché dans la barre de menu) et notée dans le journal.
        /// </summary>
        public static KeyboardLayout Load(string name, out string message)
        {
            string path = PathOf(name);
            message = null;
            if (!File.Exists(path))
            {
                message = "Disposition du clavier introuvable : " + name + ".json";
                App.Log("Clavier : fichier introuvable : " + path);
                return null;
            }

            KeyboardLayout layout;
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(KeyboardLayout));
                using (FileStream stream = File.OpenRead(path))
                {
                    layout = (KeyboardLayout)serializer.ReadObject(stream);
                }
            }
            catch (Exception ex)
            {
                // Une faute de frappe dans le JSON (virgule oubliée, guillemet…) : on le dit.
                message = "Disposition du clavier illisible : " + name + ".json (voir le journal)";
                App.Log("Clavier : fichier illisible " + path + " : " + ex.Message);
                return null;
            }

            string error = layout.Check();
            if (error != null)
            {
                message = "Disposition du clavier incorrecte (voir le journal)";
                App.Log("Clavier : " + name + ".json : " + error);
                return null;
            }

            App.Log("Clavier : disposition « " + layout.Name + " » lue (" + layout.Pages.Count + " pages)");
            return layout;
        }

        /// <summary>
        /// Étape 14 (bouton « Langue ») : toutes les dispositions du dossier Claviers qui se
        /// lisent correctement, triées par nom affiché. Une disposition illisible ou incorrecte
        /// n'est pas proposée (la raison est écrite dans le journal par Charger).
        /// Le nom du fichier (sans « .json ») est celui qu'on enregistre dans parametres.json.
        /// </summary>
        public static List<FoundLayout> List()
        {
            List<FoundLayout> result = new List<FoundLayout>();
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName);
            string[] files;
            try
            {
                files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.json") : new string[0];
            }
            catch (Exception ex)
            {
                App.Log("Clavier : dossier des dispositions illisible : " + ex.Message);
                return result;
            }

            foreach (string path in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                string message;
                KeyboardLayout layout = Load(fileName, out message);
                if (layout != null)
                {
                    result.Add(new FoundLayout
                    {
                        File = fileName,
                        // Sans nom dans le fichier : on affiche le nom du fichier.
                        Name = string.IsNullOrWhiteSpace(layout.Name) ? fileName : layout.Name,
                        Language = string.IsNullOrWhiteSpace(layout.Language) ? "?" : layout.Language.Trim()
                    });
                }
            }
            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        /// <summary>
        /// Vérifie que la disposition est utilisable. Renvoie null si tout va bien, sinon une
        /// phrase qui explique le problème (pour le journal). Une touche hors de la grille est
        /// seulement signalée dans le journal et ignorée : le reste du clavier reste utilisable.
        /// </summary>
        private string Check()
        {
            if (Columns < 1 || Columns > 30 || Rows < 1 || Rows > 10)
            {
                return "taille de grille impossible (" + Columns + " × " + Rows + ")";
            }
            if (Pages == null || Pages.Count == 0)
            {
                return "aucune page";
            }
            if (Page(StartPage) == null)
            {
                return "la page de départ « " + StartPage + " » n'existe pas";
            }
            foreach (KeyboardPage page in Pages)
            {
                if (page.Keys == null)
                {
                    page.Keys = new List<LayoutKey>();
                }
                page.Keys.RemoveAll(t =>
                {
                    bool outside = t == null || t.Row < 0 || t.Row >= Rows || t.Col < 0 ||
                                  t.Col + t.CellWidth > Columns;
                    if (outside && t != null)
                    {
                        App.Log("Clavier : touche « " + t.Label + " » de la page " + page.Name +
                                " hors de la grille (ligne " + t.Row + ", col " + t.Col + ") : ignorée");
                    }
                    return outside;
                });
            }
            return null;
        }
    }

    /// <summary>Une disposition trouvée dans le dossier Claviers (étape 14, liste « Langue »).</summary>
    internal sealed class FoundLayout
    {
        /// <summary>Le nom du fichier sans « .json » (« fr-azerty »).</summary>
        public string File { get; set; }

        /// <summary>Le nom affiché (« Français AZERTY »).</summary>
        public string Name { get; set; }

        /// <summary>La langue du dictionnaire (« fr »).</summary>
        public string Language { get; set; }
    }

    /// <summary>Une page du clavier : son nom et ses touches.</summary>
    [DataContract(Name = "Page", Namespace = "")]
    internal sealed class KeyboardPage
    {
        [DataMember(Name = "nom", Order = 0)]
        public string Name { get; set; }

        [DataMember(Name = "touches", Order = 1)]
        public List<LayoutKey> Keys { get; set; }
    }

    /// <summary>
    /// Une touche telle qu'elle est écrite dans le fichier (voir DispositionClavier pour le
    /// sens de chaque champ). Les champs absents du fichier valent null.
    /// « int? » : un nombre qui peut être absent (null).
    /// </summary>
    [DataContract(Name = "Touche", Namespace = "")]
    internal sealed class LayoutKey
    {
        [DataMember(Name = "ligne", Order = 0)]
        public int Row { get; set; }

        [DataMember(Name = "col", Order = 1)]
        public int Col { get; set; }

        [DataMember(Name = "largeur", Order = 2, EmitDefaultValue = false)]
        public int? Width { get; set; }

        [DataMember(Name = "libelle", Order = 3, EmitDefaultValue = false)]
        public string Label { get; set; }

        [DataMember(Name = "texte", Order = 4, EmitDefaultValue = false)]
        public string Text { get; set; }

        [DataMember(Name = "texteMaj", Order = 5, EmitDefaultValue = false)]
        public string ShiftText { get; set; }

        [DataMember(Name = "touche", Order = 6, EmitDefaultValue = false)]
        public string Key { get; set; }

        [DataMember(Name = "raccourci", Order = 7, EmitDefaultValue = false)]
        public string Shortcut { get; set; }

        [DataMember(Name = "page", Order = 8, EmitDefaultValue = false)]
        public string Page { get; set; }

        [DataMember(Name = "modificateur", Order = 9, EmitDefaultValue = false)]
        public string Modifier { get; set; }

        [DataMember(Name = "prediction", Order = 10, EmitDefaultValue = false)]
        public int? Prediction { get; set; }

        [DataMember(Name = "commande", Order = 11, EmitDefaultValue = false)]
        public string Command { get; set; }

        [DataMember(Name = "style", Order = 12, EmitDefaultValue = false)]
        public string Style { get; set; }

        /// <summary>La largeur en cases (1 si absente ou incorrecte).</summary>
        public int CellWidth
        {
            get { return Width.HasValue && Width.Value >= 1 ? Width.Value : 1; }
        }
    }
}
