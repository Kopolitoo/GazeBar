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
    /// MOT SUIVANT (demande de l'utilisateur du 02/11/2026) : la prédiction retient aussi les
    /// PAIRES de mots écrits l'un après l'autre (« je » → « suis », « l' » → « homme »), dans le
    /// dictionnaire personnel. Juste après une espace, les 3 cases montrent les mots qui ont le
    /// plus souvent suivi le mot précédent ; pendant la frappe, ces mots gagnent
    /// [BonusSuite] par fois. Lexique ne contient pas de paires : au début, il n'y a donc rien
    /// à proposer après une espace, la prédiction s'enrichit au fil des phrases écrites.
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
    internal sealed class WordPredictor
    {
        /// <summary>Nom du dossier des dictionnaires, à côté de GazeBar.exe.</summary>
        public const string FolderName = "Dictionaries";

        /// <summary>
        /// Ce que vaut UNE utilisation, en « apparitions par million » de Lexique. Avec 20, un
        /// mot rare utilisé 5 fois (score 100) passe devant la plupart des mots courants qui
        /// commencent pareil, mais pas devant « de » (64 000) ou « pas » (27 000).
        /// À ajuster après essai si les mots habituels montent trop vite ou trop lentement.
        /// </summary>
        private const double UsageBonus = 20;

        /// <summary>
        /// Ce que vaut UNE fois où le mot a suivi le mot précédent, pendant la frappe : 10 fois
        /// plus qu'une simple utilisation, car le mot d'avant en dit beaucoup. Exemple : après
        /// « je », en tapant « s », « suis » déjà écrit 5 fois après « je » gagne 1 000.
        /// À ajuster après essai.
        /// </summary>
        private const double FollowBonus = 200;

        /// <summary>Nombre de cases de proposition.</summary>
        public const int SuggestionCount = 3;

        /// <summary>Délai entre deux enregistrements des nombres d'utilisations.</summary>
        private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(1);

        // ── Le dictionnaire principal, rangé par clé. Les trois tableaux vont ensemble :
        //    _cles[i] est la clé du mot _mots[i], dont la fréquence est _frequences[i].
        //    « volatile » : ils sont remplis par un autre fil d'exécution (le chargement en
        //    arrière-plan), puis lus par celui de l'interface. On remplace le tout d'un coup
        //    (un seul objet Tableaux), pour ne jamais lire un mélange d'ancien et de nouveau.
        private sealed class Tables
        {
            public string[] Keys;
            public string[] Words;
            public double[] Frequencies;
        }

        private volatile Tables _primary;

        // La langue en cours de chargement (pour ne pas lancer deux fois le même chargement).
        private string _requestedLanguage;

        // ── Le dictionnaire personnel (utilisé seulement par le fil de l'interface).
        //    Clé de ces dictionnaires : le mot exact tel qu'il est rangé (« bonjour », « Paris »).
        private readonly Dictionary<string, int> _usages = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _addedWords = new List<string>();
        private readonly List<string> _addedKeys = new List<string>();

        // Les paires de mots : pour chaque mot (forme rangée), les mots écrits juste après lui
        // et le nombre de fois. Exemple : _suites["je"]["suis"] = 12.
        private readonly Dictionary<string, Dictionary<string, int>> _followers =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        // Vrai quand des nombres d'utilisations ont changé depuis le dernier enregistrement.
        private bool _toSave;

        private readonly DispatcherTimer _saveTimer;

        /// <summary>
        /// À créer depuis le fil de l'interface (le minuteur d'enregistrement en dépend).
        /// Lit tout de suite le dictionnaire personnel (petit fichier).
        /// </summary>
        public WordPredictor()
        {
            LoadPersonal();

            _saveTimer = new DispatcherTimer();
            _saveTimer.Interval = SaveInterval;
            _saveTimer.Tick += (s, e) =>
            {
                if (_toSave)
                {
                    SavePersonal();
                }
            };
            _saveTimer.Start();
        }

        /// <summary>Vrai quand le dictionnaire principal est chargé (sinon, aucune proposition).</summary>
        public bool Ready
        {
            get { return _primary != null; }
        }

        // ───────────────────────── Chargement du dictionnaire principal ─────────────────────────

        /// <summary>
        /// Lance, en arrière-plan, la lecture du dictionnaire d'une langue (« fr » →
        /// Dictionnaires/fr.txt). Ne fait rien si cette langue est déjà chargée ou en cours.
        /// En attendant, Proposer ne propose rien.
        /// </summary>
        public void LoadLanguage(string language)
        {
            language = string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
            if (language == _requestedLanguage)
            {
                return;
            }
            // Changement de langue (étape 14, bouton « Langue ») : le dictionnaire de l'ancienne
            // langue est oublié tout de suite. Sans cela, si le fichier de la nouvelle langue
            // manquait, on continuerait à proposer des mots français.
            bool changement = _requestedLanguage != null;
            _requestedLanguage = language;
            if (changement)
            {
                _primary = null;
            }
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName, language + ".txt");

            // Task.Run : la lecture se fait sur un autre fil d'exécution, le regard continue
            // d'être suivi pendant ce temps.
            Task.Run(() =>
            {
                Tables t = ReadFile(path);
                // On ne garde le résultat que si cette langue est toujours celle demandée (on a
                // pu changer encore de langue pendant la lecture).
                if (t != null && _requestedLanguage == language)
                {
                    _primary = t;
                }
            });
        }

        /// <summary>
        /// Lit un fichier « mot&lt;Tab&gt;fréquence » (une ligne par mot) et le range par clé.
        /// Renvoie null (et l'écrit dans le journal) si le fichier est absent ou illisible.
        /// </summary>
        private static Tables ReadFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    App.Log("Prédiction : dictionnaire introuvable : " + path + " (pas de propositions)");
                    return null;
                }

                DateTime start = DateTime.Now;
                List<string> words = new List<string>(130000);
                List<double> frequencies = new List<double>(130000);
                foreach (string row in File.ReadLines(path, Encoding.UTF8))
                {
                    if (row.Length == 0 || row[0] == '#')
                    {
                        continue;   // ligne vide ou commentaire
                    }
                    int tab = row.IndexOf('\t');
                    string word = (tab < 0 ? row : row.Substring(0, tab)).Trim();
                    if (word.Length == 0)
                    {
                        continue;
                    }
                    double frequency = 0;
                    if (tab >= 0)
                    {
                        // InvariantCulture : le fichier écrit « 12.5 » avec un point, quelle que
                        // soit la langue de Windows (en français, Windows attendrait « 12,5 »).
                        double.TryParse(row.Substring(tab + 1).Trim(), NumberStyles.Float,
                                        CultureInfo.InvariantCulture, out frequency);
                    }
                    words.Add(word);
                    frequencies.Add(frequency);
                }

                // On calcule les clés, puis on range les trois tableaux ensemble par clé.
                Tables t = new Tables();
                t.Words = words.ToArray();
                t.Frequencies = frequencies.ToArray();
                t.Keys = new string[t.Words.Length];
                for (int i = 0; i < t.Words.Length; i++)
                {
                    t.Keys[i] = Key(t.Words[i]);
                }
                int[] order = new int[t.Words.Length];
                for (int i = 0; i < order.Length; i++)
                {
                    order[i] = i;
                }
                string[] sortedKeys = (string[])t.Keys.Clone();
                Array.Sort(sortedKeys, order, StringComparer.Ordinal);
                string[] sortedWords = new string[order.Length];
                double[] sortedFrequencies = new double[order.Length];
                for (int i = 0; i < order.Length; i++)
                {
                    sortedWords[i] = t.Words[order[i]];
                    sortedFrequencies[i] = t.Frequencies[order[i]];
                }
                t.Keys = sortedKeys;
                t.Words = sortedWords;
                t.Frequencies = sortedFrequencies;

                App.Log("Prédiction : " + t.Words.Length + " mots lus depuis " + path + " en " +
                        (int)(DateTime.Now - start).TotalMilliseconds + " ms");
                return t;
            }
            catch (Exception ex)
            {
                App.Log("Prédiction : dictionnaire illisible " + path + " : " + ex.Message);
                return null;
            }
        }

        // ───────────────────────── Les propositions ─────────────────────────

        /// <summary>
        /// Les (au plus) 3 mots les plus probables qui commencent par « tape » (sans tenir
        /// compte des accents ni des majuscules), du plus probable au moins probable.
        /// Seuls les mots au moins aussi longs que ce qui est tapé sont proposés (« ete » propose
        /// « été », règle validée le 02/11/2026). Si ce qui est tapé est lui-même un mot connu,
        /// il est mis en 1re case (pour écrire l'espace après lui en le regardant). Les mots sont rendus tels qu'ils sont
        /// rangés : c'est le clavier qui ajoute la majuscule si besoin.
        ///
        /// motInconnu : vrai si « tape » (2 lettres ou plus) ne correspond à aucun mot connu,
        /// ni en entier ni en début de mot : le clavier propose alors de l'ajouter. Toujours
        /// faux tant que le dictionnaire principal n'est pas chargé.
        ///
        /// precedent : le mot écrit juste avant (forme rangée, rendue par NoterUtilisation),
        /// ou null (début de texte, après une ponctuation ou un déplacement du curseur). Avec
        /// « tape » vide, les propositions sont les mots qui l'ont le plus souvent suivi.
        /// </summary>
        public List<string> Suggest(string typed, string previous, out bool unknownWord)
        {
            List<string> result = new List<string>(SuggestionCount);
            unknownWord = false;
            Tables t = _primary;
            Dictionary<string, int> followers = null;
            if (previous != null)
            {
                _followers.TryGetValue(previous, out followers);
            }

            if (string.IsNullOrEmpty(typed))
            {
                // Mot suivant : rien de tapé, on propose ce qui a déjà suivi le mot précédent.
                if (followers != null)
                {
                    List<string> bestNext = new List<string>(SuggestionCount + 1);
                    List<double> nextScores = new List<double>(SuggestionCount + 1);
                    foreach (KeyValuePair<string, int> follower in followers)
                    {
                        Remember(follower.Key, FollowBonus * follower.Value, "", null, bestNext, nextScores);
                    }
                    result.AddRange(bestNext);
                }
                return result;
            }
            if (t == null)
            {
                return result;
            }

            string prefix = Key(typed);
            if (prefix.Length == 0)
            {
                return result;
            }

            // Les meilleurs candidats trouvés, rangés du meilleur au moins bon.
            List<string> best = new List<string>(SuggestionCount + 1);
            List<double> scores = new List<double>(SuggestionCount + 1);
            bool known = false;

            // 1. Le dictionnaire principal : la première clé qui commence par le préfixe,
            //    puis toutes les suivantes tant qu'elles commencent pareil.
            for (int i = FirstKey(t.Keys, prefix); i < t.Keys.Length; i++)
            {
                if (!t.Keys[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    break;
                }
                known = true;
                Remember(t.Words[i], t.Frequencies[i], typed, followers, best, scores);
            }

            // 2. Les mots ajoutés par l'utilisateur (quelques dizaines au plus : on les lit tous).
            for (int i = 0; i < _addedWords.Count; i++)
            {
                if (_addedKeys[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    known = true;
                    Remember(_addedWords[i], 0, typed, followers, best, scores);
                }
            }

            unknownWord = !known && prefix.Length >= 2;

            // Demande de l'utilisateur du 02/11/2026 : si ce qui est tapé est déjà un mot connu,
            // il vient EN PREMIER. Le regarder écrit simplement l'espace après lui (c'est plus
            // rapide que de chercher la touche Espace). Les autres cases gardent les meilleurs.
            string exactMatch = KnownShape(typed);
            if (exactMatch != null)
            {
                result.Add(exactMatch);
                for (int i = 0; i < best.Count && result.Count < SuggestionCount; i++)
                {
                    result.Add(best[i]);
                }
                return result;
            }

            result.AddRange(best);
            return result;
        }

        /// <summary>
        /// Ajoute un candidat à la liste des meilleurs s'il y a sa place (score = fréquence +
        /// bonus d'utilisation + bonus de suite si ce mot a déjà suivi le mot précédent). La
        /// liste reste triée et ne dépasse pas 3 mots. suites : les mots qui ont suivi le mot
        /// précédent (ou null).
        /// </summary>
        private void Remember(string word, double frequency, string typed, Dictionary<string, int> followers,
                             List<string> best, List<double> scores)
        {
            // Règle validée par l'utilisateur le 02/11/2026 : un mot AU MOINS AUSSI LONG que ce
            // qui est tapé (« été » pour « ete »). Le mot déjà tapé en entier est écarté ici :
            // Proposer le remet lui-même en 1re case.
            if (word.Length < typed.Length || string.Equals(word, typed, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int usages;
            _usages.TryGetValue(word, out usages);
            double score = frequency + UsageBonus * usages;
            int times;
            if (followers != null && followers.TryGetValue(word, out times))
            {
                score += FollowBonus * times;
            }

            // On cherche sa place : avant le premier qui a un score plus petit. À score égal,
            // le mot le plus court passe devant.
            int slot = scores.Count;
            for (int i = 0; i < scores.Count; i++)
            {
                if (score > scores[i] || (score == scores[i] && word.Length < best[i].Length))
                {
                    slot = i;
                    break;
                }
            }
            if (slot >= SuggestionCount)
            {
                return;
            }
            best.Insert(slot, word);
            scores.Insert(slot, score);
            if (best.Count > SuggestionCount)
            {
                best.RemoveAt(SuggestionCount);
                scores.RemoveAt(SuggestionCount);
            }
        }

        /// <summary>
        /// Dichotomie : l'indice de la première clé qui n'est pas avant « prefixe » dans
        /// l'ordre alphabétique (donc la première qui peut commencer par lui).
        /// </summary>
        private static int FirstKey(string[] keys, string prefix)
        {
            int bottom = 0;
            int top = keys.Length;   // la réponse est entre bas et haut
            while (bottom < top)
            {
                int middle = bottom + (top - bottom) / 2;
                if (string.CompareOrdinal(keys[middle], prefix) < 0)
                {
                    bottom = middle + 1;   // trop tôt : la réponse est après le milieu
                }
                else
                {
                    top = middle;      // le milieu convient peut-être : on cherche avant
                }
            }
            return bottom;
        }

        /// <summary>
        /// La « clé » de recherche d'un mot : en minuscules, sans accents (é → e, ç → c,
        /// œ → oe, ’ → '). « Été » et « ete » ont la même clé « ete ».
        /// </summary>
        public static string Key(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return "";
            }
            // FormD sépare chaque lettre accentuée en lettre + accent (é → e + ´) ; on garde
            // seulement les lettres.
            string decomposed = word.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(decomposed.Length + 2);
            foreach (char c in decomposed)
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
        ///
        /// precedent : le mot écrit juste avant (ce que rendait l'appel précédent), ou null.
        /// Si les deux sont connus, la paire « precedent → mot » est retenue (mot suivant).
        /// Renvoie la forme rangée du mot (« Bonjour » → « bonjour »), ou null s'il est
        /// inconnu : c'est le « precedent » du prochain appel.
        /// </summary>
        public string RecordUsage(string word, string previous)
        {
            string shape = KnownShape(word);
            if (shape == null)
            {
                return null;
            }
            int n;
            _usages.TryGetValue(shape, out n);
            _usages[shape] = n + 1;

            if (previous != null)
            {
                Dictionary<string, int> followers;
                if (!_followers.TryGetValue(previous, out followers))
                {
                    followers = new Dictionary<string, int>(StringComparer.Ordinal);
                    _followers[previous] = followers;
                }
                int times;
                followers.TryGetValue(shape, out times);
                followers[shape] = times + 1;
            }
            _toSave = true;
            return shape;
        }

        /// <summary>
        /// Ajoute un mot au dictionnaire personnel (case « + »), tel qu'il a été tapé, et
        /// enregistre le fichier tout de suite. Ne fait rien s'il est déjà connu.
        /// </summary>
        public void Add(string word)
        {
            if (string.IsNullOrWhiteSpace(word) || KnownShape(word) != null)
            {
                return;
            }
            _addedWords.Add(word);
            _addedKeys.Add(Key(word));
            App.Log("Prédiction : un mot ajouté au dictionnaire personnel (" + _addedWords.Count + " en tout)");
            SavePersonal();
        }

        /// <summary>
        /// Étape 14 (bouton « Mes mots ») : une copie de la liste des mots ajoutés par la case
        /// « + », dans l'ordre alphabétique (sans tenir compte des accents ni des majuscules).
        /// </summary>
        public List<string> AddedWords()
        {
            List<string> copyItem = new List<string>(_addedWords);
            copyItem.Sort((a, b) => string.Compare(a, b, CultureInfo.CurrentCulture,
                                                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
            return copyItem;
        }

        /// <summary>
        /// Étape 14 (bouton « Mes mots ») : retire un mot ajouté par erreur. On oublie aussi son
        /// nombre d'utilisations et les paires où il apparaît (mot suivant), pour qu'il ne soit
        /// plus jamais proposé. Le fichier est enregistré tout de suite. Ne touche pas aux mots
        /// de Lexique. Le mot n'est pas écrit dans le journal (vie privée).
        /// </summary>
        public void Delete(string word)
        {
            int idx = _addedWords.IndexOf(word);
            if (idx < 0)
            {
                return;
            }
            _addedWords.RemoveAt(idx);
            _addedKeys.RemoveAt(idx);
            _usages.Remove(word);
            _followers.Remove(word);
            foreach (Dictionary<string, int> followers in _followers.Values)
            {
                followers.Remove(word);
            }
            App.Log("Prédiction : un mot retiré du dictionnaire personnel (" + _addedWords.Count + " restants)");
            SavePersonal();
        }

        /// <summary>
        /// La forme rangée d'un mot tapé (« Bonjour » → « bonjour »), en comparant sans tenir
        /// compte des majuscules mais AVEC les accents ; null si le mot n'est dans aucun des
        /// deux dictionnaires.
        /// </summary>
        private string KnownShape(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return null;
            }
            foreach (string added in _addedWords)
            {
                if (string.Equals(added, word, StringComparison.OrdinalIgnoreCase))
                {
                    return added;
                }
            }
            Tables t = _primary;
            if (t == null)
            {
                return null;
            }
            string key = Key(word);
            for (int i = FirstKey(t.Keys, key); i < t.Keys.Length && t.Keys[i] == key; i++)
            {
                if (string.Equals(t.Words[i], word, StringComparison.OrdinalIgnoreCase))
                {
                    return t.Words[i];
                }
            }
            return null;
        }

        // ───────────────────────── Le fichier du dictionnaire personnel ─────────────────────────

        /// <summary>Chemin complet du dictionnaire personnel, à côté de parametres.json.</summary>
        public static string PersonalPath
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(AppSettings.FilePath), "personal-dictionary.json");
            }
        }

        /// <summary>À appeler à la fermeture de GazeBar : enregistre les utilisations en attente.</summary>
        public void Stop()
        {
            _saveTimer.Stop();
            if (_toSave)
            {
                SavePersonal();
            }
        }

        /// <summary>
        /// Lit le dictionnaire personnel. Absent : on part de zéro. Illisible : on le met de
        /// côté (dictionnaire-perso.json.illisible) et on part de zéro, comme pour les réglages.
        /// </summary>
        private void LoadPersonal()
        {
            string path = PersonalPath;
            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PersonalFile));
                PersonalFile file;
                using (FileStream stream = File.OpenRead(path))
                {
                    file = (PersonalFile)serializer.ReadObject(stream);
                }
                if (file != null && file.Words != null)
                {
                    foreach (PersonalWord m in file.Words)
                    {
                        if (m == null || string.IsNullOrWhiteSpace(m.Word))
                        {
                            continue;
                        }
                        if (m.Added && !_addedWords.Contains(m.Word))
                        {
                            _addedWords.Add(m.Word);
                            _addedKeys.Add(Key(m.Word));
                        }
                        if (m.Usages > 0)
                        {
                            _usages[m.Word] = m.Usages;
                        }
                    }
                }
                if (file != null && file.Followers != null)
                {
                    foreach (WordPair pair in file.Followers)
                    {
                        if (pair == null || string.IsNullOrEmpty(pair.Before) || string.IsNullOrEmpty(pair.After) ||
                            pair.Number <= 0)
                        {
                            continue;
                        }
                        Dictionary<string, int> followers;
                        if (!_followers.TryGetValue(pair.Before, out followers))
                        {
                            followers = new Dictionary<string, int>(StringComparer.Ordinal);
                            _followers[pair.Before] = followers;
                        }
                        followers[pair.After] = pair.Number;
                    }
                }
                App.Log("Prédiction : dictionnaire personnel lu (" + _addedWords.Count + " mots ajoutés, " +
                        _usages.Count + " mots avec des utilisations, " + _followers.Count +
                        " mots avec des suites)");
            }
            catch (Exception ex)
            {
                App.Log("Prédiction : dictionnaire personnel illisible, on repart de zéro. " + ex.Message);
                try
                {
                    File.Copy(path, path + ".illisible", true);
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
        private void SavePersonal()
        {
            _toSave = false;
            PersonalFile file = new PersonalFile();
            // Version 2 (02/11/2026) : ajout des paires de mots (Suites). Un fichier de
            // version 1 se lit sans rien faire : il n'a simplement pas de paires.
            file.FileVersion = 2;
            file.Words = new List<PersonalWord>();
            foreach (string word in _addedWords)
            {
                int n;
                _usages.TryGetValue(word, out n);
                file.Words.Add(new PersonalWord { Word = word, Added = true, Usages = n });
            }
            foreach (KeyValuePair<string, int> u in _usages)
            {
                if (!_addedWords.Contains(u.Key))
                {
                    file.Words.Add(new PersonalWord { Word = u.Key, Added = false, Usages = u.Value });
                }
            }

            file.Followers = new List<WordPair>();
            foreach (KeyValuePair<string, Dictionary<string, int>> before in _followers)
            {
                foreach (KeyValuePair<string, int> after in before.Value)
                {
                    file.Followers.Add(new WordPair { Before = before.Key, After = after.Key, Number = after.Value });
                }
            }

            string path = PersonalPath;
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PersonalFile));
                using (FileStream stream = File.Create(temp))
                using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(
                           stream, new UTF8Encoding(false), false, true, "  "))
                {
                    serializer.WriteObject(writer, file);
                    writer.Flush();
                }
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
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
    ///   ],
    ///   "Suites": [
    ///     { "Avant": "je", "Apres": "suis", "Nombre": 7 }
    ///   ]
    /// }
    /// « Ajoute » : vrai pour un mot ajouté par la case « + » (absent de Lexique).
    /// </summary>
    [DataContract]
    internal sealed class PersonalFile
    {
        [DataMember(Order = 1)]
        public int FileVersion { get; set; }

        [DataMember(Order = 2)]
        public List<PersonalWord> Words { get; set; }

        /// <summary>Les paires de mots (mot suivant), depuis la version 2.</summary>
        [DataMember(Order = 3)]
        public List<WordPair> Followers { get; set; }
    }

    /// <summary>Une paire de mots : « Apres » a été écrit juste après « Avant », « Nombre » fois.</summary>
    [DataContract]
    internal sealed class WordPair
    {
        [DataMember(Order = 1)]
        public string Before { get; set; }

        [DataMember(Order = 2)]
        public string After { get; set; }

        [DataMember(Order = 3)]
        public int Number { get; set; }
    }

    /// <summary>Un mot du dictionnaire personnel.</summary>
    [DataContract]
    internal sealed class PersonalWord
    {
        [DataMember(Order = 1)]
        public string Word { get; set; }

        [DataMember(Order = 2)]
        public bool Added { get; set; }

        [DataMember(Order = 3)]
        public int Usages { get; set; }
    }
}
