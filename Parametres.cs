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
    /// Ils remplacent les constantes qui étaient écrites en haut des premiers fichiers
    /// (GazeMouse.cs et HoldSession.cs, supprimés à l'étape 6). Ils sont lus au démarrage et
    /// enregistrés dans le fichier :
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

        /// <summary>
        /// Version actuelle du fichier de réglages.
        ///   1 : étape 1 ;
        ///   2 : étape 2 — ClicDirect et SuiviSouris passent à « non » par défaut (choix de
        ///       l'utilisateur du 22/10/2026). Un fichier de version 1 est mis à jour une fois
        ///       (voir MettreAJour) ;
        ///   3 : étape 6 — nouveau glisser-déposer : DureePauseAndroid et DureeGlisser
        ///       remplacent DureeAppuiLong, DelaiDepot, DistanceMinGlisser et DureeMaxAppui,
        ///       supprimés (règle de l'utilisateur : un réglage qui ne sert plus est retiré).
        /// </summary>
        public const int VersionActuelle = 3;

        /// <summary>Numéro du format du fichier, pour pouvoir le faire évoluer un jour sans tout casser.</summary>
        [DataMember(Order = 0)]
        public int VersionFichier { get; set; }

        // ───────────────────────── Regard ─────────────────────────

        /// <summary>Fraction du chemin parcourue en 16 ms (un tic), entre 0 et 1 (0 = immobile, 1 = regard brut, tremblant). Lissage double, en temps réel : voir Modes/Lisseur.cs.</summary>
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

        /// <summary>Clic gauche automatique si la fixation se prolonge (oui / non). Non par défaut.</summary>
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

        /// <summary>
        /// Durée du regard sur l'icône d'options du mode regard fixe avant son activation, en
        /// secondes. Beaucoup plus courte que [DelaiActivation] (demande de l'utilisateur du
        /// 22/10/2026) : c'est l'icône qu'on regarde le plus souvent, et la regarder par erreur
        /// n'a pas de conséquence (le choix d'action propose toujours « retour »).
        /// </summary>
        [DataMember(Order = 43)]
        public double DelaiIconeOptions { get; set; }

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

        /// <summary>
        /// Glisser-déposer « android » : pause entre l'appui du bouton et le début du
        /// déplacement, en secondes (comme pour déplacer une icône sur un téléphone).
        /// Remplace l'ancien DureeAppuiLong (étape 6).
        /// </summary>
        [DataMember(Order = 60)]
        public double DureePauseAndroid { get; set; }

        /// <summary>
        /// Durée du déplacement du curseur pendant un glisser-déposer, en secondes : le curseur
        /// va du point de départ au point d'arrivée en ce temps-là, bouton enfoncé.
        /// Remplace l'ancien DelaiDepot (étape 6) : l'arrivée se fixe maintenant avec [DelaiFixation].
        /// </summary>
        [DataMember(Order = 61)]
        public double DureeGlisser { get; set; }

        // ───────────────────────── Défilement ─────────────────────────

        // (Étape 8 : l'ancien réglage ZoneNeutreDefilement est supprimé, la zone où rien ne
        //  défile est maintenant le cercle lui-même, de rayon [RayonCercle]. S'il est encore
        //  dans parametres.json, il est simplement ignoré à la lecture.)

        /// <summary>
        /// Vitesse maximale du mode défilement, en crans de molette par seconde (étape 8).
        /// La vitesse vaut 0 au bord du cercle et atteint cette valeur trois [RayonCercle] plus
        /// loin ; au-delà, elle ne monte plus.
        /// </summary>
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

        /// <summary>
        /// En mode mouvement, le curseur de la souris suit aussi le regard. Non par défaut :
        /// le curseur ne rejoint alors le regard qu'une fois celui-ci fixé (mode regard fixe).
        /// </summary>
        [DataMember(Order = 90)]
        public bool SuiviSouris { get; set; }

        /// <summary>
        /// Lancer GazeBar à l'ouverture de la session Windows (étape 7). Non par défaut.
        /// Le fichier de réglages garde le choix ; c'est DemarrageWindows qui l'applique
        /// réellement, dans le registre de Windows.
        /// </summary>
        [DataMember(Order = 91)]
        public bool DemarrerAvecWindows { get; set; }

        // ───────────────────────── Valeurs par défaut ─────────────────────────

        /// <summary>Le constructeur : un objet neuf contient les valeurs par défaut.</summary>
        public Parametres()
        {
            ValeursParDefaut();
        }

        /// <summary>
        /// Remet tous les réglages à leur valeur de départ (celles du cahier des charges).
        /// Le bouton « Défaut » de chaque onglet du mode paramètres (étape 7) ne remet que
        /// les réglages de son onglet : il lit ces valeurs dans un objet neuf (new Parametres()).
        /// </summary>
        public void ValeursParDefaut()
        {
            VersionFichier = VersionActuelle;

            Lissage = 0.16;
            ZoneMorteCurseur = 8;
            AgeMaxMesure = 300;

            RayonFixation = 30;
            DelaiFixation = 0.8;
            VitesseMaxFixation = 900;
            ClicDirect = false;
            DelaiClicDirect = 1.5;

            RayonCercle = 190;
            TailleIcones = 120;
            AfficherLibelles = true;

            DelaiActivation = 0.6;
            DelaiIconeOptions = 0.3;
            DelaiMenu = 0.5;
            DelaiFermetureMenu = 1.0;

            LoupeAvantAction = true;
            ZoomLoupe = 4;

            DureePauseAndroid = 1.0;
            DureeGlisser = 0.5;

            VitesseMaxDefilement = 10;

            LissageJeu = 0.7;
            SensibiliteJeu = 1.0;

            SuiviSouris = false;
            DemarrerAvecWindows = false;
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
            DelaiIconeOptions = Borner(DelaiIconeOptions, 0.1, 10);
            DelaiMenu = Borner(DelaiMenu, 0.1, 10);
            DelaiFermetureMenu = Borner(DelaiFermetureMenu, 0.1, 10);

            ZoomLoupe = Borner(ZoomLoupe, 1, 10);

            DureePauseAndroid = Borner(DureePauseAndroid, 0, 10);
            DureeGlisser = Borner(DureeGlisser, 0.05, 10);

            VitesseMaxDefilement = Borner(VitesseMaxDefilement, 1, 100);

            LissageJeu = Borner(LissageJeu, 0.01, 1);
            SensibiliteJeu = Borner(SensibiliteJeu, 0.1, 10);
        }

        /// <summary>
        /// Met à jour un fichier écrit par une version plus ancienne de GazeBar.
        /// Renvoie vrai si quelque chose a changé (il faut alors réenregistrer le fichier).
        ///
        /// Pourquoi ? Changer une valeur par défaut ne suffit pas : le fichier déjà présent sur
        /// l'ordinateur contient l'ancienne valeur (par exemple ClicDirect = true), et c'est
        /// elle qui serait lue. On la corrige donc une seule fois, puis le numéro de version
        /// est augmenté : ensuite, un choix fait par l'utilisateur dans le fichier est respecté.
        /// </summary>
        public bool MettreAJour()
        {
            if (VersionFichier >= VersionActuelle)
            {
                return false;
            }

            if (VersionFichier < 2)
            {
                ClicDirect = false;
                SuiviSouris = false;
                App.Log("Paramètres : mise à jour en version 2 (ClicDirect et SuiviSouris désactivés)");
            }

            if (VersionFichier < 3)
            {
                // Rien à corriger : les anciens réglages du glisser (DureeAppuiLong, DelaiDepot…)
                // sont simplement ignorés à la lecture, et les nouveaux ont déjà leur valeur par
                // défaut (voir AvantLecture). Réenregistrer le fichier les fait disparaître.
                App.Log("Paramètres : mise à jour en version 3 (réglages du glisser-déposer remplacés)");
            }

            VersionFichier = VersionActuelle;
            return true;
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
                if (resultat.MettreAJour())
                {
                    resultat.Enregistrer();
                }
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
