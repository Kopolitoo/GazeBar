using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : tous les réglages de GazeBar, au même endroit.
    ///
    /// Ils remplacent les constantes qui étaient écrites en haut de GazeMouse.cs et de
    /// HoldSession.cs. Ils sont lus au démarrage et enregistrés dans le fichier :
    ///     %APPDATA%\GazeBar\parametres.json
    /// (par exemple C:\Users\Nom\AppData\Roaming\GazeBar\parametres.json).
    ///
    /// Le format JSON est du texte lisible, qu'on peut ouvrir avec le Bloc-notes. Attention :
    /// dans ce fichier, les nombres décimaux s'écrivent avec un POINT (0.3) et non une virgule.
    ///
    /// Pour lire et écrire le JSON, on utilise DataContractJsonSerializer, fourni avec
    /// .NET Framework 4.8 : aucune bibliothèque supplémentaire n'est nécessaire.
    /// Le principe : on marque la classe avec [DataContract] et chaque réglage à enregistrer
    /// avec [DataMember]. Le sérialiseur s'occupe du reste.
    ///
    /// Les noms des réglages reprennent ceux du cahier des charges, SANS accents
    /// (DelaiFixation au lieu de DélaiFixation) : ils sont plus faciles à taper et à chercher.
    ///
    /// Utilisation dans le code : Parametres.Courants.Lissage, Parametres.Courants.RayonCercle, etc.
    /// </summary>
    [DataContract(Name = "Parametres", Namespace = "")]
    internal sealed class Parametres
    {
        // ───────────────────────── Accès global ─────────────────────────

        /// <summary>
        /// Les réglages en cours d'utilisation, partagés par tout le programme.
        /// « static » : il n'en existe qu'un seul exemplaire. Remplacé au démarrage par
        /// Initialiser(), qui lit le fichier.
        /// </summary>
        public static Parametres Courants { get; private set; } = new Parametres();

        /// <summary>Numéro du format du fichier, pour pouvoir le faire évoluer un jour sans tout casser.</summary>
        [DataMember(Order = 0)]
        public int VersionFichier { get; set; }

        // ───────────────────────── Regard ─────────────────────────

        /// <summary>Fraction du chemin parcourue à chaque pas, entre 0 et 1 (0 = immobile, 1 = regard brut, tremblant).</summary>
        [DataMember(Order = 10)]
        public double Lissage { get; set; }

        /// <summary>Anti-tremblement du curseur, en pixels : en dessous, le curseur ne bouge pas.</summary>
        [DataMember(Order = 11)]
        public double ZoneMorteCurseur { get; set; }

        /// <summary>Âge maximal d'une mesure du regard, en MILLISECONDES. Au-delà, le regard est considéré comme perdu.</summary>
        [DataMember(Order = 12)]
        public double AgeMaxMesure { get; set; }

        // ───────────────────────── Fixation ─────────────────────────

        /// <summary>Le regard est « immobile » tant qu'il reste dans ce rayon, en pixels.</summary>
        [DataMember(Order = 20)]
        public double RayonFixation { get; set; }

        /// <summary>Durée d'immobilité avant le passage en mode regard fixe, en secondes.</summary>
        [DataMember(Order = 21)]
        public double DelaiFixation { get; set; }

        /// <summary>
        /// Vitesse maximale du regard pendant une fixation, en PIXELS PAR SECONDE.
        /// Entre deux mesures, on calcule la vitesse du regard (distance ÷ temps écoulé) :
        /// au-dessus de cette valeur, l'œil est en train de sauter vers un autre point
        /// (une « saccade »), ce n'est pas une fixation.
        /// Réglage ajouté à l'étape 2 (il ne figure pas dans la première version du cahier des
        /// charges). 900 px/s correspond à 30 pixels entre deux mesures du PCEye (≈ 30 par seconde).
        /// </summary>
        [DataMember(Order = 24)]
        public double VitesseMaxFixation { get; set; }

        /// <summary>Clic gauche automatique si la fixation se prolonge (oui / non).</summary>
        [DataMember(Order = 22)]
        public bool ClicDirect { get; set; }

        /// <summary>Délai supplémentaire après l'entrée en regard fixe avant le clic direct, en secondes.</summary>
        [DataMember(Order = 23)]
        public double DelaiClicDirect { get; set; }

        // ───────────────────────── Affichage ─────────────────────────

        /// <summary>Rayon du cercle du mode mouvement (et zone de sortie du regard fixe), en pixels.</summary>
        [DataMember(Order = 30)]
        public double RayonCercle { get; set; }

        /// <summary>Côté des icônes activables au regard, en pixels.</summary>
        [DataMember(Order = 31)]
        public double TailleIcones { get; set; }

        /// <summary>Afficher le libellé d'une action au survol de son icône (mode apprentissage).</summary>
        [DataMember(Order = 32)]
        public bool AfficherLibelles { get; set; }

        // ───────────────────────── Délais ─────────────────────────

        /// <summary>Durée du regard sur une icône avant son activation, en secondes.</summary>
        [DataMember(Order = 40)]
        public double DelaiActivation { get; set; }

        /// <summary>Durée du regard en bas hors de l'écran avant l'ouverture du menu, en secondes.</summary>
        [DataMember(Order = 41)]
        public double DelaiMenu { get; set; }

        /// <summary>Durée du regard hors de la barre de menu avant sa fermeture, en secondes.</summary>
        [DataMember(Order = 42)]
        public double DelaiFermetureMenu { get; set; }

        // ───────────────────────── Loupe ─────────────────────────

        /// <summary>Passer par la loupe avant le choix d'action.</summary>
        [DataMember(Order = 50)]
        public bool LoupeAvantAction { get; set; }

        /// <summary>Agrandissement de la capture en mode loupe (4 = quatre fois plus grand).</summary>
        [DataMember(Order = 51)]
        public double ZoomLoupe { get; set; }

        // ───────────────────────── Actions ─────────────────────────

        /// <summary>Durée de l'appui long, en secondes.</summary>
        [DataMember(Order = 60)]
        public double DureeAppuiLong { get; set; }

        /// <summary>Durée de la fixation qui dépose un glisser, en secondes.</summary>
        [DataMember(Order = 61)]
        public double DelaiDepot { get; set; }

        /// <summary>Distance minimale parcourue avant qu'un dépôt soit possible, en pixels.</summary>
        [DataMember(Order = 62)]
        public double DistanceMinGlisser { get; set; }

        /// <summary>Sécurité : un bouton n'est jamais maintenu plus longtemps que cela, en secondes.</summary>
        [DataMember(Order = 63)]
        public double DureeMaxAppui { get; set; }

        // ───────────────────────── Défilement ─────────────────────────

        /// <summary>Rayon de la zone centrale où rien ne défile, en pixels.</summary>
        [DataMember(Order = 70)]
        public double ZoneNeutreDefilement { get; set; }

        /// <summary>Vitesse de défilement au bord du cercle, en crans de molette par seconde.</summary>
        [DataMember(Order = 71)]
        public double VitesseMaxDefilement { get; set; }

        // ───────────────────────── Jeu ─────────────────────────

        /// <summary>Lissage du mode jeu (plus proche de 1 = plus réactif).</summary>
        [DataMember(Order = 80)]
        public double LissageJeu { get; set; }

        /// <summary>Multiplicateur des déplacements relatifs en mode jeu.</summary>
        [DataMember(Order = 81)]
        public double SensibiliteJeu { get; set; }

        // ───────────────────────── Général ─────────────────────────

        /// <summary>En mode mouvement, le curseur de la souris suit aussi le regard.</summary>
        [DataMember(Order = 90)]
        public bool SuiviSouris { get; set; }

        // ───────────────────────── Valeurs par défaut ─────────────────────────

        /// <summary>Le constructeur : un objet neuf contient les valeurs par défaut.</summary>
        public Parametres()
        {
            ValeursParDefaut();
        }

        /// <summary>
        /// Remet tous les réglages à leur valeur de départ (celles du cahier des charges).
        /// Servira au bouton « valeurs par défaut » du mode paramètres.
        /// </summary>
        public void ValeursParDefaut()
        {
            VersionFichier = 1;

            Lissage = 0.30;
            ZoneMorteCurseur = 8;
            AgeMaxMesure = 300;

            RayonFixation = 30;
            DelaiFixation = 0.8;
            VitesseMaxFixation = 900;
            ClicDirect = true;
            DelaiClicDirect = 1.5;

            RayonCercle = 100;
            TailleIcones = 120;
            AfficherLibelles = true;

            DelaiActivation = 0.8;
            DelaiMenu = 0.5;
            DelaiFermetureMenu = 1.0;

            LoupeAvantAction = false;
            ZoomLoupe = 4;

            DureeAppuiLong = 1.5;
            DelaiDepot = 1.2;
            DistanceMinGlisser = 60;
            DureeMaxAppui = 20;

            ZoneNeutreDefilement = 40;
            VitesseMaxDefilement = 10;

            LissageJeu = 0.7;
            SensibiliteJeu = 1.0;

            SuiviSouris = true;
        }

        /// <summary>
        /// Appelée par le sérialiseur JUSTE AVANT de lire le fichier.
        ///
        /// Piège à connaître : à la lecture, DataContractJsonSerializer n'appelle PAS le
        /// constructeur. Un réglage absent du fichier (par exemple un réglage ajouté dans une
        /// nouvelle version de GazeBar) vaudrait alors 0 ou « non ». En remplissant d'abord les
        /// valeurs par défaut ici, un réglage absent garde sa valeur par défaut.
        /// </summary>
        [OnDeserializing]
        private void AvantLecture(StreamingContext contexte)
        {
            ValeursParDefaut();
        }

        /// <summary>
        /// Ramène chaque valeur dans des limites raisonnables. Protège contre une faute de
        /// frappe dans le fichier (un lissage de 30 au lieu de 0.30, un délai négatif…).
        /// </summary>
        public void Corriger()
        {
            Lissage = Borner(Lissage, 0.01, 1);
            ZoneMorteCurseur = Borner(ZoneMorteCurseur, 0, 100);
            AgeMaxMesure = Borner(AgeMaxMesure, 50, 5000);

            RayonFixation = Borner(RayonFixation, 5, 300);
            DelaiFixation = Borner(DelaiFixation, 0.1, 10);
            VitesseMaxFixation = Borner(VitesseMaxFixation, 50, 20000);
            DelaiClicDirect = Borner(DelaiClicDirect, 0.1, 10);

            RayonCercle = Borner(RayonCercle, 20, 500);
            TailleIcones = Borner(TailleIcones, 40, 400);

            DelaiActivation = Borner(DelaiActivation, 0.1, 10);
            DelaiMenu = Borner(DelaiMenu, 0.1, 10);
            DelaiFermetureMenu = Borner(DelaiFermetureMenu, 0.1, 10);

            ZoomLoupe = Borner(ZoomLoupe, 1, 10);

            DureeAppuiLong = Borner(DureeAppuiLong, 0.1, 30);
            DelaiDepot = Borner(DelaiDepot, 0.1, 10);
            DistanceMinGlisser = Borner(DistanceMinGlisser, 0, 1000);
            DureeMaxAppui = Borner(DureeMaxAppui, 1, 120);

            ZoneNeutreDefilement = Borner(ZoneNeutreDefilement, 0, 500);
            VitesseMaxDefilement = Borner(VitesseMaxDefilement, 1, 100);

            LissageJeu = Borner(LissageJeu, 0.01, 1);
            SensibiliteJeu = Borner(SensibiliteJeu, 0.1, 10);
        }

        /// <summary>Renvoie la valeur, ramenée entre min et max. Une valeur illisible (NaN) devient min.</summary>
        private static double Borner(double valeur, double min, double max)
        {
            if (double.IsNaN(valeur) || valeur < min)
            {
                return min;
            }
            if (valeur > max)
            {
                return max;
            }
            return valeur;
        }

        // ───────────────────────── Lecture et enregistrement ─────────────────────────

        /// <summary>Chemin complet du fichier de réglages.</summary>
        public static string CheminFichier
        {
            get
            {
                string dossier = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GazeBar");
                return Path.Combine(dossier, "parametres.json");
            }
        }

        /// <summary>À appeler une fois au démarrage : lit le fichier et remplit Parametres.Courants.</summary>
        public static void Initialiser()
        {
            Courants = Charger();
        }

        /// <summary>
        /// Lit le fichier de réglages.
        ///  - S'il n'existe pas encore (premier lancement), on le crée avec les valeurs par défaut.
        ///  - S'il est illisible (fichier abîmé, faute de syntaxe), on le met de côté sous le nom
        ///    parametres.json.illisible, et on repart des valeurs par défaut.
        /// Dans tous les cas, le programme démarre.
        /// </summary>
        public static Parametres Charger()
        {
            string chemin = CheminFichier;
            Parametres resultat;

            if (!File.Exists(chemin))
            {
                App.Log("Paramètres : premier lancement, création de " + chemin);
                resultat = new Parametres();
                resultat.Enregistrer();
                return resultat;
            }

            try
            {
                DataContractJsonSerializer serialiseur = new DataContractJsonSerializer(typeof(Parametres));
                using (FileStream fichier = File.OpenRead(chemin))
                {
                    resultat = (Parametres)serialiseur.ReadObject(fichier);
                }
                resultat.Corriger();
                App.Log("Paramètres : lus depuis " + chemin);
                return resultat;
            }
            catch (Exception ex)
            {
                App.Log("Paramètres : fichier illisible, valeurs par défaut utilisées. " + ex.Message);
                try
                {
                    File.Copy(chemin, chemin + ".illisible", true);
                }
                catch
                {
                    // Tant pis pour la copie de secours : l'important est de démarrer.
                }
                resultat = new Parametres();
                resultat.Enregistrer();
                return resultat;
            }
        }

        /// <summary>
        /// Enregistre les réglages dans le fichier, joliment présenté (un réglage par ligne).
        ///
        /// On écrit d'abord dans un fichier temporaire, puis on le met à la place de l'ancien :
        /// si l'ordinateur s'éteint en pleine écriture, l'ancien fichier reste intact.
        /// </summary>
        public void Enregistrer()
        {
            string chemin = CheminFichier;
            string temporaire = chemin + ".tmp";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(chemin));

                DataContractJsonSerializer serialiseur = new DataContractJsonSerializer(typeof(Parametres));
                using (FileStream fichier = File.Create(temporaire))
                using (XmlDictionaryWriter ecrivain = JsonReaderWriterFactory.CreateJsonWriter(
                           fichier, new UTF8Encoding(false), false, true, "  "))
                {
                    // Paramètres de CreateJsonWriter : le fichier, l'encodage (UTF-8 sans BOM),
                    // « ne pas fermer le fichier nous-mêmes », « indenter », et l'indentation (2 espaces).
                    serialiseur.WriteObject(ecrivain, this);
                    ecrivain.Flush();
                }

                if (File.Exists(chemin))
                {
                    File.Replace(temporaire, chemin, null);
                }
                else
                {
                    File.Move(temporaire, chemin);
                }
            }
            catch (Exception ex)
            {
                // Ne jamais faire planter le programme pour un fichier de réglages.
                App.Log("Paramètres : échec de l'enregistrement : " + ex.Message);
            }
        }
    }
}
