using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Tobii.Interaction;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : la vraie position du regard, fournie par l'eyetracker via la
    /// bibliothèque Tobii.Interaction (la même que Mill Mouse). Elle ne dépend plus du curseur
    /// piloté par le logiciel du fabricant.
    ///
    /// Les coordonnées sont en pixels d'écran. Elles peuvent être NÉGATIVES ou dépasser la
    /// taille de l'écran quand on regarde en dehors : c'est utile (menu hors écran, etc.).
    ///
    /// Si le moteur de l'eyetracker n'est pas disponible, le programme continue de fonctionner :
    /// IsRunning reste faux et ErrorMessage explique pourquoi.
    ///
    /// Depuis le 02/11/2026, les DLL de l'eyetracker ne sont plus livrées avec GazeBar : elles sont
    /// cherchées sur le PC au démarrage (voir ChargeurEyetracker.cs). Pour que GazeBar démarre
    /// même quand elles sont introuvables, les types de Tobii.Interaction (Host, GazePointDataStream)
    /// n'apparaissent plus que dans la méthode ConnecterEyetracker : .NET ne cherche une DLL qu'au
    /// moment où il prépare (« compile à la volée ») une méthode qui s'en sert. Si la DLL
    /// manque, l'erreur arrive donc à l'appel de ConnecterEyetracker, à l'intérieur du try de Start,
    /// au lieu de faire planter tout le programme.
    /// </summary>
    internal sealed class GazeSource : IDisposable
    {
        private readonly object _lock = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        // L'objet Host et le flux de regard (types de Tobii.Interaction). Volontairement rangés sous des types
        // « neutres » (IDisposable, object) : voir le commentaire en tête de la classe.
        private IDisposable _host;
        private object _stream;

        private double _x;
        private double _y;
        private bool _hasData;
        private double _lastSampleMs = -1;
        private long _measureNumber;   // compteur : +1 à chaque nouvelle mesure reçue

        // Calcul de la cadence : on compte les mesures reçues pendant une fenêtre d'une seconde.
        private int _countInWindow;
        private double _windowStartMs;
        private double _samplesPerSecond;

        /// <summary>Vrai si la connexion au moteur de l'eyetracker a réussi.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Message d'erreur si la connexion a échoué (sinon vide).</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// L'objet Host (Tobii.Interaction) (null si la connexion a échoué). Sert uniquement à lancer
        /// l'outil de calibration (voir Calibration.cs, étape 4). Son type est « object »
        /// pour que le reste du programme n'ait pas à connaître Tobii.Interaction.
        /// </summary>
        public object Host
        {
            get { return _host; }
        }

        /// <summary>Vrai dès qu'au moins une mesure a été reçue.</summary>
        public bool HasData
        {
            get { lock (_lock) { return _hasData; } }
        }

        /// <summary>Cadence des mesures : nombre par seconde (0 si plus rien n'arrive).</summary>
        public double SamplesPerSecond
        {
            get
            {
                lock (_lock)
                {
                    bool silent = _lastSampleMs < 0 || _clock.Elapsed.TotalMilliseconds - _lastSampleMs > 1500;
                    return silent ? 0 : _samplesPerSecond;
                }
            }
        }

        /// <summary>Temps écoulé depuis la dernière mesure, en ms (très grand si aucune).</summary>
        public double MsSinceLastSample
        {
            get
            {
                lock (_lock)
                {
                    return _lastSampleMs < 0 ? double.MaxValue : _clock.Elapsed.TotalMilliseconds - _lastSampleMs;
                }
            }
        }

        /// <summary>Se connecte au moteur de l'eyetracker et commence à recevoir le regard.</summary>
        public void Start()
        {
            // Les DLL de l'eyetracker ont-elles été trouvées sur le PC ? (voir ChargeurEyetracker.cs)
            if (!EyetrackerLoader.Available)
            {
                IsRunning = false;
                ErrorMessage = EyetrackerLoader.Message;
                App.Log("Regard : pas de connexion : " + ErrorMessage);
                return;
            }

            try
            {
                ConnectEyetracker();
                IsRunning = true;
                App.Log("Regard : connecté");
            }
            catch (Exception ex)
            {
                IsRunning = false;
                ErrorMessage = ex.Message;
                App.Log("Regard : échec de la connexion : " + ex);
            }
        }

        /// <summary>
        /// Le seul endroit qui utilise directement les types de Tobii.Interaction. « NoInlining » interdit
        /// au compilateur de recopier cette méthode à l'intérieur de Start : sinon, une DLL
        /// manquante ferait échouer Start elle-même, hors de son try.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ConnectEyetracker()
        {
            Host host = new Host();
            _host = host;
            GazePointDataStream stream = host.Streams.CreateGazePointDataStream();
            _stream = stream;

            // Appelée par Tobii.Interaction, sur l'un de ses threads, à chaque nouvelle mesure.
            stream.Next += (sender, measure) => OnGaze(measure.Data.X, measure.Data.Y);
        }

        private void OnGaze(double x, double y)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            lock (_lock)
            {
                _x = x;
                _y = y;
                _hasData = true;
                _lastSampleMs = now;
                _measureNumber++;

                _countInWindow++;
                double elapsed = now - _windowStartMs;
                if (elapsed >= 1000)
                {
                    _samplesPerSecond = _countInWindow * 1000.0 / elapsed;
                    _countInWindow = 0;
                    _windowStartMs = now;
                }
            }
        }

        /// <summary>Dernière position du regard connue (pixels d'écran).</summary>
        public void GetLatest(out double x, out double y)
        {
            lock (_lock)
            {
                x = _x;
                y = _y;
            }
            // Correction de l'échelle (étape 11, voir Ecran.FacteurRegard) : 1 d'habitude.
            double factor = ScreenMetrics.GazeFactor;
            x *= factor;
            y *= factor;
        }

        /// <summary>
        /// Dernière position du regard, avec son numéro et l'instant où elle est arrivée.
        ///
        /// Pourquoi le numéro ? L'horloge des modes tourne à 60 tics par seconde, mais le
        /// PCEye 5 n'envoie qu'environ 30 mesures par seconde : un tic sur deux voit donc la
        /// MÊME mesure qu'au tic précédent. En comparant les numéros, un mode sait si la mesure
        /// est nouvelle, et ne la compte qu'une seule fois.
        /// L'instant (en millisecondes, sur l'horloge de GazeSource) permet de calculer une
        /// vitesse ou une durée en vrai temps, quelle que soit la cadence.
        /// </summary>
        public void GetLatest(out double x, out double y, out long num, out double instantMs)
        {
            lock (_lock)
            {
                x = _x;
                y = _y;
                num = _measureNumber;
                instantMs = _lastSampleMs;
            }
            // Correction de l'échelle (étape 11, voir Ecran.FacteurRegard) : 1 d'habitude.
            double factor = ScreenMetrics.GazeFactor;
            x *= factor;
            y *= factor;
        }

        /// <summary>L'heure actuelle sur l'horloge de GazeSource, en millisecondes (même échelle que instantMs).</summary>
        public double NowMs
        {
            get { return _clock.Elapsed.TotalMilliseconds; }
        }

        /// <summary>Vrai si la position est en dehors de l'écran principal.</summary>
        public static bool IsOffScreen(double x, double y, double screenWidth, double screenHeight)
        {
            return x < 0 || y < 0 || x >= screenWidth || y >= screenHeight;
        }

        public void Dispose()
        {
            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }
        }
    }
}
