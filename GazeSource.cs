using System;
using System.Diagnostics;
using Tobii.Interaction;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : la vraie position du regard, fournie par le moteur Tobii (« Tobii Interaction »),
    /// le même que celui utilisé par Mill Mouse. Elle ne dépend plus du curseur de TD Control.
    ///
    /// Les coordonnées sont en pixels d'écran. Elles peuvent être NÉGATIVES ou dépasser la
    /// taille de l'écran quand on regarde en dehors : c'est utile (menu hors écran, etc.).
    ///
    /// Si le moteur Tobii n'est pas disponible, le programme continue de fonctionner :
    /// IsRunning reste faux et ErrorMessage explique pourquoi.
    /// </summary>
    internal sealed class GazeSource : IDisposable
    {
        private readonly object _lock = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private Host _host;
        private GazePointDataStream _stream;

        private double _x;
        private double _y;
        private bool _hasData;
        private double _lastSampleMs = -1;
        private long _numeroMesure;   // compteur : +1 à chaque nouvelle mesure reçue

        // Calcul de la cadence : on compte les mesures reçues pendant une fenêtre d'une seconde.
        private int _countInWindow;
        private double _windowStartMs;
        private double _samplesPerSecond;

        /// <summary>Vrai si la connexion au moteur Tobii a réussi.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Message d'erreur si la connexion a échoué (sinon vide).</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// L'objet Host de Tobii (null si la connexion a échoué). Sert uniquement à lancer
        /// l'outil de calibration (voir CalibrationTobii.cs, étape 4). Son type est « object »
        /// pour que le reste du programme n'ait pas à connaître Tobii.Interaction.
        /// </summary>
        public object Hote
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

        /// <summary>Se connecte au moteur Tobii et commence à recevoir le regard.</summary>
        public void Start()
        {
            try
            {
                _host = new Host();
                _stream = _host.Streams.CreateGazePointDataStream();

                // Appelée par Tobii, sur l'un de ses threads, à chaque nouvelle mesure.
                _stream.Next += (sender, mesure) => OnGaze(mesure.Data.X, mesure.Data.Y);

                IsRunning = true;
                App.Log("Regard Tobii : connecté");
            }
            catch (Exception ex)
            {
                IsRunning = false;
                ErrorMessage = ex.Message;
                App.Log("Regard Tobii : échec de la connexion : " + ex);
            }
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
                _numeroMesure++;

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
        public void GetLatest(out double x, out double y, out long numero, out double instantMs)
        {
            lock (_lock)
            {
                x = _x;
                y = _y;
                numero = _numeroMesure;
                instantMs = _lastSampleMs;
            }
        }

        /// <summary>L'heure actuelle sur l'horloge de GazeSource, en millisecondes (même échelle que instantMs).</summary>
        public double MaintenantMs
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
