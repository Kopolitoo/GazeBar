using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Xml;

namespace GazeBar
{
    /// <summary>
    /// LOGIQUE : la prédiction de mots du clavier au regard (étape 13, « clavier-prediction »).
    ///
    /// Elle répond à une seule question : « l'utilisateur a tapé ces lettres, quels sont les
    /// 3 mots les plus probables qui commencent ainsi ? » (méthode Proposer).
    ///
    /// Deux sources de mots :
    ///   1. le DICTIONNAIRE PRINCIPAL, Dictionnaires/fr.txt (tiré de Lexique 3.83, licence
    ///      CC BY-SA, voir Dictionnaires/LICENCE-Lexique.txt) : environ 125 000 mots, chacun
    ///      avec sa fréquence (nombre d'apparitions par million de mots dans des films et des
    ///      livres). Il est copié à côté de GazeBar.exe et lu une fois, au démarrage, en
    ///      arrière-plan (environ une demi-seconde, sans bloquer le regard) ;
    ///   2. le DICTIONNAIRE PERSONNEL, %APPDATA%\GazeBar\dictionnaire-perso.json, à côté de
    ///      parametres.json. Il n'est JAMAIS dans le dépôt public : il contient les mots
    ///      ajoutés par l'utilisateur et le nombre de fois où il a utilisé chaque mot
    ///      (l'« apprentissage »).
    ///
    /// Classement d'un mot (cahier des charges, validé le 01/11/2026) :
    ///     score = fréquence dans Lexique + [BonusUtilisation] × nombre d'utilisations
    /// Les mots habituels remontent donc vite. Un mot ajouté (absent de Lexique) part de 0.
    ///
    /// Recherche SANS tenir compte des accents ni des majuscules : chaque mot a une « clé »
    /// (minuscules, sans accents, voir Cle) ; « ete » trouve « été ». Les clés sont rangées
    /// dans l'ordre alphabétique : on trouve la première qui commence par ce qui est tapé par
    /// DICHOTOMIE (on coupe la liste en deux, puis encore en deux… : 17 essais pour 125 000
    /// mots), puis on lit les suivantes tant qu'elles commencent pareil.
    ///
    /// Enregistrement du dictionnaire personnel : juste après chaque ajout de mot ; les
    /// nombres d'utilisations, eux, une fois par minute s'ils ont changé, et à la fermeture
    /// de GazeBar (pour ne pas écrire sur le disque à chaque mot).
    ///
    /// Le texte tapé n'est jamais écrit dans le journal (vie privée).
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal sealed class PredicteurMots
    {
        /// <summary>Nom du dossier des dictionnaires, à côté de GazeBar.exe.</summary>
        public const string NomDossier = "Dictionnaires";

        /// <summary>
        /// Ce que vaut UNE utilisation, en « apparitions par million » de Lexique. Avec 20, un
        /// mot rare utilisé 5 fois (score 100) passe devant la plupart des mots courants qui
        /// commencent pareil, mais pas devant « de » (64 000) ou « pas » (27 000).
        /// À ajuster après essai si les mots habituels montent trop vite ou trop lentement.
        /// </summary>
        private const double BonusUtilisation = 20;

        /// <summary>Nombre de cases de proposition.</summary>
        public const int NombrePropositions = 3;

        /// <summary>Délai entre deux enregistrements des nombres d'utilisations.</summary>
        private static readonly TimeSpan IntervalleEnregistrement = TimeSpan.FromMinutes(1);

        // ── Le dictionnaire principal, rangé par clé. Les trois tableaux vont ensemble :
        //    _cles[i] est la clé du mot _mots[i], dont la fréquence est _frequences[i].
        //    « volatile » : ils sont remplis par un autre fil d'exécution (le chargement en
        //    arrière-plan), puis lus par celui de l'interface. On remplace le tout d'un coup
        //    (un seul objet Tableaux), pour ne jamais lire un mélange d'ancien et de nouveau.
        private sealed class Tableaux
        {
            public string[] Cles;
            public string[] Mots;
            public double[] Frequences;
        }

        private volatile Tableaux _principal;

        // La langue en cours de chargement (pour ne pas lancer deux fois le même chargement).
        private string _langueDemandee;

        // ── Le dictionnaire personnel (utilisé seulement par le fil de l'interface).
        //    Clé de ces dictionnaires : le mot exact tel qu'il est rangé (« bonjour », « Paris »).
        private readonly Dictionary<string, int> _utilisations = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _motsAjoutes = new List<string>();
        private readonly List<string> _clesAjoutees = new List<string>();

        // Vrai quand des nombres d'utilisations ont changé depuis le dernier enregistrement.
        private bool _aEnregistrer;

        private readonly DispatcherTimer _minuteur;

        /// <summary>
        /// À créer depuis le fil de l'interface (le minuteur d'enregistrement en dépend).
        /// Lit tout de suite le dictionnaire personnel (petit fichier).
        /// </summary>
        public PredicteurMots()
        {
            ChargerPersonnel();

            _minuteur = new DispatcherTimer();
            _minuteur.Interval = IntervalleEnregistrement;
            _minuteur.Tick += (s, e) =>
            {
                if (_aEnregistrer)
                {
                    EnregistrerPersonnel();
                }
            };
            _minuteur.Start();
        }

        /// <summary>Vrai quand le dictionnaire principal est chargé (sinon, aucune proposition).</summary>
        public bool Pret
        {
            get { return _principal != null; }
        }

        // ───────────────────────── Chargement du dictionnaire principal ─────────────────────────

        /// <summary>
        /// Lance, en arrière-plan, la lecture du dictionnaire d'une langue (« fr » →
        /// Dictionnaires/fr.txt). Ne fait rien si cette langue est déjà chargée ou en cours.
        /// En attendant, Proposer ne propose rien.
        /// </summary>
        public void ChargerLangue(string langue)
        {
            langue = string.IsNullOrWhiteSpace(langue) ? "fr" : langue.Trim().ToLowerInvariant();
            if (langue == _langueDemandee)
            {
                return;
            }
            _langueDemandee = langue;
            string chemin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NomDossier, langue + ".txt");

            // Task.Run : la lecture se fait sur un autre fil d'exécution, le regard continue
            // d'être suivi pendant ce temps.
            Task.Run(() =>
            {
                Tableaux t = LireFichier(chemin);
                if (t != null)
                {
                    _principal = t;
                }
            });
        }

        /// <summary>
        /// Lit un fichier « mot&lt;Tab&gt;fréquence » (une ligne par mot) et le range par clé.
        /// Renvoie null (et l'écrit dans le journal) si le fichier est absent ou illisible.
        /// </summary>
        private static Tableaux LireFichier(string chemin)
        {
            try
            {
                if (!File.Exists(chemin))
                {
                    App.Log("Prédiction : dictionnaire introuvable : " + chemin + " (pas de propositions)");
                    return null;
                }

                DateTime debut = DateTime.Now;
                List<string> mots = new List<string>(130000);
                List<double> frequences = new List<double>(130000);
                foreach (string ligne in File.ReadLines(chemin, Encoding.UTF8))
                {
                    if (ligne.Length == 0 || ligne[0] == '#')
                    {
                        continue;   // ligne vide ou commentaire
                    }
                    int tab = ligne.IndexOf('\t');
                    string mot = (tab < 0 ? ligne : ligne.Substring(0, tab)).Trim();
                    if (mot.Length == 0)
                    {
                        continue;
                    }
                    double frequence = 0;
                    if (tab >= 0)
                    {
                        // InvariantCulture : le fichier écrit « 12.5 » avec un point, quelle que
                        // soit la langue de Windows (en français, Windows attendrait « 12,5 »).
                        double.TryParse(ligne.Substring(tab + 1).Trim(), NumberStyles.Float,
                                        CultureInfo.InvariantCulture, out frequence);
                    }
                    mots.Add(mot);
                    frequences.Add(frequence);
                }

                // On calcule les clés, puis on range les trois tableaux ensemble par clé.
                Tableaux t = new Tableaux();
                t.Mots = mots.ToArray();
                t.Frequences = frequences.ToArray();
                t.Cles = new string[t.Mots.Length];
                for (int i = 0; i < t.Mots.Length; i++)
                {
                    t.Cles[i] = Cle(t.Mots[i]);
                }
                int[] ordre = new int[t.Mots.Length];
                for (int i = 0; i < ordre.Length; i++)
                {
                    ordre[i] = i;
                }
                string[] clesTriees = (string[])t.Cles.Clone();
                Array.Sort(clesTriees, ordre, StringComparer.Ordinal);
                string[] motsTries = new string[ordre.Length];
                double[] frequencesTriees = new double[ordre.Length];
                for (int i = 0; i < ordre.Length; i++)
                {
                    motsTries[i] = t.Mots[ordre[i]];
                    frequencesTriees[i] = t.Frequences[ordre[i]];
                }
                t.Cles = clesTriees;
                t.Mots = motsTries;
                t.Frequences = frequencesTriees;

                App.Log("Prédiction : " + t.Mots.Length + " mots lus depuis " + chemin + " en " +
                        (int)(DateTime.Now - debut).TotalMilliseconds + " ms");
                return t;
            }
            catch (Exception ex)
            {
                App.Log("Prédiction : dictionnaire illisible " + chemin + " : " + ex.Message);
                return null;
            }
        }

        // ───────────────────────── Les propositions ─────────────────────────

        /// <summary>
        /// Les (au plus) 3 mots les plus probables qui commencent par « tape » (sans tenir
        /// compte des accents ni des majuscules), du plus probable au moins probable.
        /// Le mot exactement tapé n'est pas proposé (cahier des charges : seuls les mots plus
        /// longs le sont ; un mot de même longueur qui ne diffère que par les accents, comme
        /// « été » pour « ete », l'est quand même). Les mots sont rendus tels qu'ils sont
        /// rangés : c'est le clavier qui ajoute la majuscule si besoin.
        ///
        /// motInconnu : vrai si « tape » (2 lettres ou plus) ne correspond à aucun mot connu,
        /// ni en entier ni en début de mot : le clavier propose alors de l'ajouter. Toujours
        /// faux tant que le dictionnaire principal n'est pas chargé.
        /// </summary>
        public List<string> Proposer(string tape, out bool motInconnu)
        {
            List<string> resultat = new List<string>(NombrePropositions);
            motInconnu = false;
            Tableaux t = _principal;
            if (t == null || string.IsNullOrEmpty(tape))
            {
                return resultat;
            }

            string prefixe = Cle(tape);
            if (prefixe.Length == 0)
            {
                return resultat;
            }

            // Les meilleurs candidats trouvés, rangés du meilleur au moins bon.
            List<string> meilleurs = new List<string>(NombrePropositions + 1);
            List<double> scores = new List<double>(NombrePropositions + 1);
            bool connu = false;

            // 1. Le dictionnaire principal : la première clé qui commence par le préfixe,
            //    puis toutes les suivantes tant qu'elles commencent pareil.
            for (int i = PremiereCle(t.Cles, prefixe); i < t.Cles.Length; i++)
            {
                if (!t.Cles[i].StartsWith(prefixe, StringComparison.Ordinal))
                {
                    break;
                }
                connu = true;
                Retenir(t.Mots[i], t.Frequences[i], tape, meilleurs, scores);
            }

            // 2. Les mots ajoutés par l'utilisateur (quelques dizaines au plus : on les lit tous).
            for (int i = 0; i < _motsAjoutes.Count; i++)
            {
                if (_clesAjoutees[i].StartsWith(prefixe, StringComparison.Ordinal))
                {
                    connu = true;
                    Retenir(_motsAjoutes[i], 0, tape, meilleurs, scores);
                }
            }

            motInconnu = !connu && prefixe.Length >= 2;
            resultat.AddRange(meilleurs);
            return resultat;
        }

        /// <summary>
        /// Ajoute un candidat à la liste des meilleurs s'il y a sa place (score = fréquence +
        /// bonus d'utilisation). La liste reste triée et ne dépasse pas 3 mots.
        /// </summary>
        private void Retenir(string mot, double frequence, string tape, List<string> meilleurs, List<double> scores)
        {
            // Le mot déjà tapé en entier n'est pas proposé (il n'apporterait rien).
            if (string.Equals(mot, tape, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int utilisations;
            _utilisations.TryGetValue(mot, out utilisations);
            double score = frequence + BonusUtilisation * utilisations;

            // On cherche sa place : avant le premier qui a un score plus petit. À score égal,
            // le mot le plus court passe devant.
            int place = scores.Count;
            for (int i = 0; i < scores.Count; i++)
            {
                if (score > scores[i] || (score == scores[i] && mot.Length < meilleurs[i].Length))
                {
                    place = i;
                    break;
                }
            }
            if (place >= NombrePropositions)
            {
                return;
            }
            meilleurs.Insert(place, mot);
            scores.Insert(place, score);
            if (meilleurs.Count > NombrePropositions)
            {
                meilleurs.RemoveAt(NombrePropositions);
                scores.RemoveAt(NombrePropositions);
            }
        }

        /// <summary>
        /// Dichotomie : l'indice de la première clé qui n'est pas avant « prefixe » dans
        /// l'ordre alphabétique (donc la première qui peut commencer par lui).
        /// </summary>
        private static int PremiereCle(string[] cles, string prefixe)
        {
            int bas = 0;
            int haut = cles.Length;   // la réponse est entre bas et haut
            while (bas < haut)
            {
                int milieu = bas + (haut - bas) / 2;
                if (string.CompareOrdinal(cles[milieu], prefixe) < 0)
                {
                    bas = milieu + 1;   // trop tôt : la réponse est après le milieu
                }
                else
                {
                    haut = milieu;      // le milieu convient peut-être : on cherche avant
                }
            }
            return bas;
        }

        /// <summary>
        /// La « clé » de recherche d'un mot : en minuscules, sans accents (é → e, ç → c,
        /// œ → oe, ’ → '). « Été » et « ete » ont la même clé « ete ».
        /// </summary>
        public static string Cle(string mot)
        {
            if (string.IsNullOrEmpty(mot))
            {
                return "";
            }
            // FormD sépare chaque lettre accentuée en lettre + accent (é → e + ´) ; on garde
            // seulement les lettres.
            string decompose = mot.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(decompose.Length + 2);
            foreach (char c in decompose)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }
                switch (c)
                {
                    case 'œ': sb.Append("oe"); break;
                    case 'æ': sb.Append("ae"); break;
                    case '’': sb.Append('\''); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // ───────────────────────── Apprentissage et mots ajoutés ─────────────────────────

        /// <summary>
        /// Un mot vient d'être écrit en entier (choisi dans une case, ou tapé puis suivi d'une
        /// espace, d'une ponctuation ou d'Entrée). S'il est connu, son nombre d'utilisations
        /// augmente de 1 (enregistré dans la minute). Un mot inconnu n'est PAS retenu : seul le
        /// choix de la case « + » ajoute un mot (cahier des charges).
        /// </summary>
        public void NoterUtilisation(string mot)
        {
            string forme = FormeConnue(mot);
            if (forme == null)
            {
                return;
            }
            int n;
            _utilisations.TryGetValue(forme, out n);
            _utilisations[forme] = n + 1;
            _aEnregistrer = true;
        }

        /// <summary>
        /// Ajoute un mot au dictionnaire personnel (case « + »), tel qu'il a été tapé, et
        /// enregistre le fichier tout de suite. Ne fait rien s'il est déjà connu.
        /// </summary>
        public void Ajouter(string mot)
        {
            if (string.IsNullOrWhiteSpace(mot) || FormeConnue(mot) != null)
            {
                return;
            }
            _motsAjoutes.Add(mot);
            _clesAjoutees.Add(Cle(mot));
            App.Log("Prédiction : un mot ajouté au dictionnaire personnel (" + _motsAjoutes.Count + " en tout)");
            EnregistrerPersonnel();
        }

        /// <summary>
        /// La forme rangée d'un mot tapé (« Bonjour » → « bonjour »), en comparant sans tenir
        /// compte des majuscules mais AVEC les accents ; null si le mot n'est dans aucun des
        /// deux dictionnaires.
        /// </summary>
        private string FormeConnue(string mot)
        {
            if (string.IsNullOrEmpty(mot))
            {
                return null;
            }
            foreach (string ajoute in _motsAjoutes)
            {
                if (string.Equals(ajoute, mot, StringComparison.OrdinalIgnoreCase))
                {
                    return ajoute;
                }
            }
            Tableaux t = _principal;
            if (t == null)
            {
                return null;
            }
            string cle = Cle(mot);
            for (int i = PremiereCle(t.Cles, cle); i < t.Cles.Length && t.Cles[i] == cle; i++)
            {
                if (string.Equals(t.Mots[i], mot, StringComparison.OrdinalIgnoreCase))
                {
                    return t.Mots[i];
                }
            }
            return null;
        }

        // ───────────────────────── Le fichier du dictionnaire personnel ─────────────────────────

        /// <summary>Chemin complet du dictionnaire personnel, à côté de parametres.json.</summary>
        public static string CheminPersonnel
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(Parametres.CheminFichier), "dictionnaire-perso.json");
            }
        }

        /// <summary>À appeler à la fermeture de GazeBar : enregistre les utilisations en attente.</summary>
        public void Arreter()
        {
            _minuteur.Stop();
            if (_aEnregistrer)
            {
                EnregistrerPersonnel();
            }
        }

        /// <summary>
        /// Lit le dictionnaire personnel. Absent : on part de zéro. Illisible : on le met de
        /// côté (dictionnaire-perso.json.illisible) et on part de zéro, comme pour les réglages.
        /// </summary>
        private void ChargerPersonnel()
        {
            string chemin = CheminPersonnel;
            if (!File.Exists(chemin))
            {
                return;
            }
            try
            {
                DataContractJsonSerializer serialiseur = new DataContractJsonSerializer(typeof(FichierPersonnel));
                FichierPersonnel fichier;
                using (FileStream flux = File.OpenRead(chemin))
                {
                    fichier = (FichierPersonnel)serialiseur.ReadObject(flux);
                }
                if (fichier != null && fichier.Mots != null)
                {
                    foreach (MotPersonnel m in fichier.Mots)
                    {
                        if (m == null || string.IsNullOrWhiteSpace(m.Mot))
                        {
                            continue;
                        }
                        if (m.Ajoute && !_motsAjoutes.Contains(m.Mot))
                        {
                            _motsAjoutes.Add(m.Mot);
                            _clesAjoutees.Add(Cle(m.Mot));
                        }
                        if (m.Utilisations > 0)
                        {
                            _utilisations[m.Mot] = m.Utilisations;
                        }
                    }
                }
                App.Log("Prédiction : dictionnaire personnel lu (" + _motsAjoutes.Count + " mots ajoutés, " +
                        _utilisations.Count + " mots avec des utilisations)");
            }
            catch (Exception ex)
            {
                App.Log("Prédiction : dictionnaire personnel illisible, on repart de zéro. " + ex.Message);
                try
                {
                    File.Copy(chemin, chemin + ".illisible", true);
                }
                catch
                {
                    // Tant pis pour la copie de secours.
                }
            }
        }

        /// <summary>
        /// Enregistre le dictionnaire personnel : d'abord dans un fichier temporaire, puis mis
        /// à la place de l'ancien (si l'ordinateur s'éteint en pleine écriture, l'ancien reste).
        /// </summary>
        private void EnregistrerPersonnel()
        {
            _aEnregistrer = false;
            FichierPersonnel fichier = new FichierPersonnel();
            fichier.VersionFichier = 1;
            fichier.Mots = new List<MotPersonnel>();
            foreach (string mot in _motsAjoutes)
            {
                int n;
                _utilisations.TryGetValue(mot, out n);
                fichier.Mots.Add(new MotPersonnel { Mot = mot, Ajoute = true, Utilisations = n });
            }
            foreach (KeyValuePair<string, int> u in _utilisations)
            {
                if (!_motsAjoutes.Contains(u.Key))
                {
                    fichier.Mots.Add(new MotPersonnel { Mot = u.Key, Ajoute = false, Utilisations = u.Value });
                }
            }

            string chemin = CheminPersonnel;
            string temporaire = chemin + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(chemin));
                DataContractJsonSerializer serialiseur = new DataContractJsonSerializer(typeof(FichierPersonnel));
                using (FileStream flux = File.Create(temporaire))
                using (XmlDictionaryWriter ecrivain = JsonReaderWriterFactory.CreateJsonWriter(
                           flux, new UTF8Encoding(false), false, true, "  "))
                {
                    serialiseur.WriteObject(ecrivain, fichier);
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
                // Ne jamais faire planter le programme pour ce fichier.
                App.Log("Prédiction : échec de l'enregistrement du dictionnaire personnel : " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Le contenu de dictionnaire-perso.json. Exemple :
    /// {
    ///   "VersionFichier": 1,
    ///   "Mots": [
    ///     { "Mot": "Kopolito", "Ajoute": true, "Utilisations": 4 },
    ///     { "Mot": "bonjour", "Ajoute": false, "Utilisations": 12 }
    ///   ]
    /// }
    /// « Ajoute » : vrai pour un mot ajouté par la case « + » (absent de Lexique).
    /// </summary>
    [DataContract]
    internal sealed class FichierPersonnel
    {
        [DataMember(Order = 1)]
        public int VersionFichier { get; set; }

        [DataMember(Order = 2)]
        public List<MotPersonnel> Mots { get; set; }
    }

    /// <summary>Un mot du dictionnaire personnel.</summary>
    [DataContract]
    internal sealed class MotPersonnel
    {
        [DataMember(Order = 1)]
        public string Mot { get; set; }

        [DataMember(Order = 2)]
        public bool Ajoute { get; set; }

        [DataMember(Order = 3)]
        public int Utilisations { get; set; }
    }
}
