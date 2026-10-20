using System;
using System.Diagnostics;
using System.Threading;

namespace GazeBar
{
    /// <summary>
    /// Une mesure : où était le curseur, et à quel moment.
    ///
    /// « struct » = petit paquet de valeurs (contrairement à une « class », il est copié
    /// quand on le passe d'une méthode à l'autre).
    /// </summary>
    internal struct CursorSample
    {
        /// <summary>Millisecondes écoulées depuis le démarrage de l'échantillonneur.</summary>
        public double Ms;

        /// <summary>Position horizontale, en pixels (0 = bord gauche de l'écran).</summary>
        public int X;

        /// <summary>Position verticale, en pixels (0 = bord haut de l'écran).</summary>
        public int Y;
    }

    /// <summary>Statistiques calculées sur la dernière seconde écoulée.</summary>
    internal struct CursorStats
    {
        /// <summary>Combien de fois par seconde on a LU le curseur (doit être bien supérieur au rythme du suivi oculaire).</summary>
        public double PollsPerSecond;

        /// <summary>Combien de fois par seconde la position a CHANGÉ : c'est la cadence réelle du « regard » obtenu par le curseur.</summary>
        public double ChangesPerSecond;
    }

    /// <summary>
    /// MODÈLE : lit en permanence la position du curseur de la souris.
    ///
    /// Rappel du principe : TD Control déplace le curseur de Windows là où vous regardez.
    /// Lire le curseur revient donc à lire un « regard » filtré par TD Control, sur tout
    /// l'écran (contrairement à l'API UWP, limitée à sa propre fenêtre).
    ///
    /// Fonctionnement : un fil d'exécution (« thread ») dédié interroge Windows environ
    /// 500 à 1000 fois par seconde. Le thread de l'interface graphique n'est pas
    /// ralenti, et on voit chaque changement de position avec son heure précise.
    /// </summary>
    internal sealed class CursorSampler : IDisposable
    {
        /// <summary>Nombre de positions gardées en mémoire pour dessiner la trace du regard.</summary>
        private const int TrailLength = 150;

        // L'horloge : mesure le temps écoulé de façon précise et régulière.
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        // Les données ci-dessous sont lues par l'interface ET écrites par le thread
        // d'échantillonnage. Le « verrou » (lock) garantit qu'un seul des deux y touche
        // à la fois, pour qu'on ne lise jamais une valeur à moitié écrite.
        private readonly object _lock = new object();
        private readonly CursorSample[] _trail = new CursorSample[TrailLength];

        private Thread _thread;
        private volatile bool _running;

        private int _x;
        private int _y;
        private bool _hasPosition;
        private double _lastChangeMs = -1;
        private int _trailCount;
        private int _trailNext;
        private int _polls;
        private int _changes;
        private double _windowStartMs;
        private CursorStats _stats;

        /// <summary>
        /// Déclenché à CHAQUE changement de position du curseur.
        /// ATTENTION : il est déclenché depuis le thread d'échantillonnage, pas depuis
        /// celui de l'interface. Un abonné ne doit donc pas toucher directement aux
        /// éléments d'écran (textes, boutons...).
        /// </summary>
        public event Action<CursorSample> PositionChanged;

        /// <summary>Heure actuelle de l'horloge de l'échantillonneur, en millisecondes.</summary>
        public double NowMs
        {
            get { return _clock.Elapsed.TotalMilliseconds; }
        }

        /// <summary>Statistiques de la dernière seconde complète.</summary>
        public CursorStats Stats
        {
            get { lock (_lock) { return _stats; } }
        }

        /// <summary>Temps écoulé depuis le dernier changement de position, en ms (-1 si aucune position n'a encore changé).</summary>
        public double MsSinceLastChange
        {
            get
            {
                lock (_lock)
                {
                    return _lastChangeMs < 0 ? -1 : NowMs - _lastChangeMs;
                }
            }
        }

        /// <summary>Démarre la surveillance du curseur.</summary>
        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _thread = new Thread(Loop);
            _thread.Name = "CursorSampler";
            _thread.IsBackground = true;                   // ne bloque pas la fermeture du programme
            _thread.Priority = ThreadPriority.AboveNormal; // lectures régulières même si l'ordinateur est chargé
            _thread.Start();
        }

        /// <summary>Position la plus récente. « out » : la méthode renvoie ses deux résultats par ces paramètres.</summary>
        public void GetLatest(out int x, out int y)
        {
            lock (_lock)
            {
                x = _x;
                y = _y;
            }
        }

        /// <summary>Copie des dernières positions, de la plus ancienne à la plus récente (pour dessiner la trace).</summary>
        public CursorSample[] GetTrail()
        {
            lock (_lock)
            {
                CursorSample[] copy = new CursorSample[_trailCount];
                int start = (_trailNext - _trailCount + TrailLength) % TrailLength;
                for (int i = 0; i < _trailCount; i++)
                {
                    copy[i] = _trail[(start + i) % TrailLength];
                }
                return copy;
            }
        }

        /// <summary>Boucle du thread : lire le curseur, attendre 1 ms, recommencer.</summary>
        private void Loop()
        {
            // Sans cet appel, Thread.Sleep(1) dormirait ~15 ms. Windows nous accorde ici 1 ms.
            NativeMethods.timeBeginPeriod(1);
            try
            {
                while (_running)
                {
                    PollOnce();
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                App.Log("CursorSampler : arrêt sur erreur : " + ex);
            }
            finally
            {
                NativeMethods.timeEndPeriod(1);
            }
        }

        /// <summary>Une lecture du curseur : met à jour la position, la trace et les statistiques.</summary>
        private void PollOnce()
        {
            NativeMethods.POINT point;
            if (!NativeMethods.GetCursorPos(out point))
            {
                return; // lecture impossible à cet instant (rare) : on réessaiera
            }

            double now = NowMs;
            bool changed;
            CursorSample sample = new CursorSample();

            lock (_lock)
            {
                _polls++;
                changed = !_hasPosition || point.X != _x || point.Y != _y;

                if (changed)
                {
                    _hasPosition = true;
                    _x = point.X;
                    _y = point.Y;
                    _lastChangeMs = now;
                    _changes++;

                    sample.Ms = now;
                    sample.X = point.X;
                    sample.Y = point.Y;

                    // « Tampon circulaire » : quand il est plein, la nouvelle position remplace la plus ancienne.
                    _trail[_trailNext] = sample;
                    _trailNext = (_trailNext + 1) % TrailLength;
                    if (_trailCount < TrailLength)
                    {
                        _trailCount++;
                    }
                }

                // Toutes les secondes : on fige les statistiques et on repart de zéro.
                if (now - _windowStartMs >= 1000.0)
                {
                    double seconds = (now - _windowStartMs) / 1000.0;
                    _stats.PollsPerSecond = _polls / seconds;
                    _stats.ChangesPerSecond = _changes / seconds;
                    _polls = 0;
                    _changes = 0;
                    _windowStartMs = now;
                }
            }

            // On prévient les abonnés HORS du verrou, pour ne jamais le garder trop longtemps.
            if (changed)
            {
                Action<CursorSample> handler = PositionChanged;
                if (handler != null)
                {
                    try
                    {
                        handler(sample);
                    }
                    catch (Exception ex)
                    {
                        App.Log("CursorSampler : erreur chez un abonné : " + ex);
                    }
                }
            }
        }

        /// <summary>Arrête le thread.</summary>
        public void Dispose()
        {
            _running = false;
            if (_thread != null)
            {
                _thread.Join(500);
                _thread = null;
            }
        }
    }
}
