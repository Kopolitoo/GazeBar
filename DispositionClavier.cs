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
    ///                          Fin, PageHaut, PageBas, Gauche, Droite, Haut, Bas, F1 à F12) ;
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
    internal sealed class DispositionClavier
    {
        /// <summary>Le nom du dossier des dispositions, à côté de GazeBar.exe.</summary>
        public const string NomDossier = "Claviers";

        /// <summary>La disposition utilisée tant qu'on ne peut pas en choisir une autre (étape 14).</summary>
        public const string NomParDefaut = "fr-azerty";

        /// <summary>Le nom affiché, par exemple « Français AZERTY ».</summary>
        [DataMember(Name = "nom", Order = 0)]
        public string Nom { get; set; }

        /// <summary>La langue du dictionnaire de mots (« fr »), pour la prédiction (étape 13).</summary>
        [DataMember(Name = "langue", Order = 1)]
        public string Langue { get; set; }

        /// <summary>Nombre de colonnes de la grille (12 pour le français).</summary>
        [DataMember(Name = "colonnes", Order = 2)]
        public int Colonnes { get; set; }

        /// <summary>Nombre de lignes de la grille, ligne du haut comprise (4 pour le français).</summary>
        [DataMember(Name = "lignes", Order = 3)]
        public int Lignes { get; set; }

        /// <summary>Le nom de la page affichée à l'ouverture du clavier.</summary>
        [DataMember(Name = "pageDepart", Order = 4)]
        public string PageDepart { get; set; }

        [DataMember(Name = "pages", Order = 5)]
        public List<PageClavier> Pages { get; set; }

        /// <summary>La page qui porte ce nom, ou null.</summary>
        public PageClavier Page(string nom)
        {
            if (Pages == null)
            {
                return null;
            }
            foreach (PageClavier page in Pages)
            {
                if (string.Equals(page.Nom, nom, StringComparison.OrdinalIgnoreCase))
                {
                    return page;
                }
            }
            return null;
        }

        /// <summary>Le chemin complet du fichier d'une disposition (à côté de GazeBar.exe).</summary>
        public static string Chemin(string nom)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NomDossier, nom + ".json");
        }

        /// <summary>
        /// Lit une disposition. Renvoie null si le fichier est absent ou illisible : erreur
        /// expliquée dans « message » (affiché dans la barre de menu) et notée dans le journal.
        /// </summary>
        public static DispositionClavier Charger(string nom, out string message)
        {
            string chemin = Chemin(nom);
            message = null;
            if (!File.Exists(chemin))
            {
                message = "Disposition du clavier introuvable : " + nom + ".json";
                App.Log("Clavier : fichier introuvable : " + chemin);
                return null;
            }

            DispositionClavier disposition;
            try
            {
                DataContractJsonSerializer serialiseur = new DataContractJsonSerializer(typeof(DispositionClavier));
                using (FileStream flux = File.OpenRead(chemin))
                {
                    disposition = (DispositionClavier)serialiseur.ReadObject(flux);
                }
            }
            catch (Exception ex)
            {
                // Une faute de frappe dans le JSON (virgule oubliée, guillemet…) : on le dit.
                message = "Disposition du clavier illisible : " + nom + ".json (voir le journal)";
                App.Log("Clavier : fichier illisible " + chemin + " : " + ex.Message);
                return null;
            }

            string erreur = disposition.Verifier();
            if (erreur != null)
            {
                message = "Disposition du clavier incorrecte (voir le journal)";
                App.Log("Clavier : " + nom + ".json : " + erreur);
                return null;
            }

            App.Log("Clavier : disposition « " + disposition.Nom + " » lue (" + disposition.Pages.Count + " pages)");
            return disposition;
        }

        /// <summary>
        /// Vérifie que la disposition est utilisable. Renvoie null si tout va bien, sinon une
        /// phrase qui explique le problème (pour le journal). Une touche hors de la grille est
        /// seulement signalée dans le journal et ignorée : le reste du clavier reste utilisable.
        /// </summary>
        private string Verifier()
        {
            if (Colonnes < 1 || Colonnes > 30 || Lignes < 1 || Lignes > 10)
            {
                return "taille de grille impossible (" + Colonnes + " × " + Lignes + ")";
            }
            if (Pages == null || Pages.Count == 0)
            {
                return "aucune page";
            }
            if (Page(PageDepart) == null)
            {
                return "la page de départ « " + PageDepart + " » n'existe pas";
            }
            foreach (PageClavier page in Pages)
            {
                if (page.Touches == null)
                {
                    page.Touches = new List<ToucheDisposition>();
                }
                page.Touches.RemoveAll(t =>
                {
                    bool dehors = t == null || t.Ligne < 0 || t.Ligne >= Lignes || t.Col < 0 ||
                                  t.Col + t.LargeurCases > Colonnes;
                    if (dehors && t != null)
                    {
                        App.Log("Clavier : touche « " + t.Libelle + " » de la page " + page.Nom +
                                " hors de la grille (ligne " + t.Ligne + ", col " + t.Col + ") : ignorée");
                    }
                    return dehors;
                });
            }
            return null;
        }
    }

    /// <summary>Une page du clavier : son nom et ses touches.</summary>
    [DataContract(Name = "Page", Namespace = "")]
    internal sealed class PageClavier
    {
        [DataMember(Name = "nom", Order = 0)]
        public string Nom { get; set; }

        [DataMember(Name = "touches", Order = 1)]
        public List<ToucheDisposition> Touches { get; set; }
    }

    /// <summary>
    /// Une touche telle qu'elle est écrite dans le fichier (voir DispositionClavier pour le
    /// sens de chaque champ). Les champs absents du fichier valent null.
    /// « int? » : un nombre qui peut être absent (null).
    /// </summary>
    [DataContract(Name = "Touche", Namespace = "")]
    internal sealed class ToucheDisposition
    {
        [DataMember(Name = "ligne", Order = 0)]
        public int Ligne { get; set; }

        [DataMember(Name = "col", Order = 1)]
        public int Col { get; set; }

        [DataMember(Name = "largeur", Order = 2, EmitDefaultValue = false)]
        public int? Largeur { get; set; }

        [DataMember(Name = "libelle", Order = 3, EmitDefaultValue = false)]
        public string Libelle { get; set; }

        [DataMember(Name = "texte", Order = 4, EmitDefaultValue = false)]
        public string Texte { get; set; }

        [DataMember(Name = "texteMaj", Order = 5, EmitDefaultValue = false)]
        public string TexteMaj { get; set; }

        [DataMember(Name = "touche", Order = 6, EmitDefaultValue = false)]
        public string Touche { get; set; }

        [DataMember(Name = "raccourci", Order = 7, EmitDefaultValue = false)]
        public string Raccourci { get; set; }

        [DataMember(Name = "page", Order = 8, EmitDefaultValue = false)]
        public string Page { get; set; }

        [DataMember(Name = "modificateur", Order = 9, EmitDefaultValue = false)]
        public string Modificateur { get; set; }

        [DataMember(Name = "prediction", Order = 10, EmitDefaultValue = false)]
        public int? Prediction { get; set; }

        [DataMember(Name = "commande", Order = 11, EmitDefaultValue = false)]
        public string Commande { get; set; }

        [DataMember(Name = "style", Order = 12, EmitDefaultValue = false)]
        public string Style { get; set; }

        /// <summary>La largeur en cases (1 si absente ou incorrecte).</summary>
        public int LargeurCases
        {
            get { return Largeur.HasValue && Largeur.Value >= 1 ? Largeur.Value : 1; }
        }
    }
}
