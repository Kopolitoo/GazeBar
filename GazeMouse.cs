using System;
using System.Windows.Threading;

namespace GazeBar
{
    /// <summary>
    /// LOGIQUE : mode « souris regard ». Quand il est activé, le curseur de Windows suit
    /// directement le regard (lu par GazeSource), sans attendre que l'on fixe un point.
    ///
    /// Trois protections :
    ///  - lissage : le curseur se rapproche du regard par petites étapes, ce qui efface
    ///    le tremblement naturel de l'œil ;
    ///  - zone morte : si le regard est à moins de quelques pixels du curseur, on ne bouge pas ;
    ///  - pause : quand on regarde EN DEHORS de l'écran (ou si les mesures s'arrêtent, par
    ///    exemple yeux fermés), le curseur reste où il est. Regarder hors de l'écran est
    ///    donc un moyen sûr de « lâcher » la souris.
    /// </summary>
    internal sealed class GazeMouse
    {
        // ───────────────────────── Réglages ─────────────────────────
        //
        // Le lissage, la zone morte et l'âge maximal d'une mesure ne sont plus des constantes
        // écrites ici : ils viennent de Parametres.Courants (Lissage, ZoneMorteCurseur,
        // AgeMaxMesure), donc du fichier parametres.json.

        /// <summary>Fréquence de mise à jour du curseur, en millisecondes entre deux étapes.</summary>
        public const int UpdateIntervalMs = 10;

        // ───────────────────────── Fonctionnement ─────────────────────────

        private readonly GazeSource _gaze;
        private readonly DispatcherTimer _timer;
        private readonly double _screenWidth;
        private readonly double _screenHeight;

        // Position (décimale) du curseur lissé. « _synced » dit si elle est à jour.
        private double _x;
        private double _y;
        private bool _synced;

        public GazeMouse(GazeSource gaze)
        {
            _gaze = gaze;
            _screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            _screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(UpdateIntervalMs);
            _timer.Tick += OnTick;
        }

        /// <summary>Vrai si le mode est actif.</summary>
        public bool IsEnabled
        {
            get { return _timer.IsEnabled; }
        }

        /// <summary>Active ou désactive le mode. Renvoie le nouvel état.</summary>
        public bool Toggle()
        {
            if (IsEnabled)
            {
                _timer.Stop();
            }
            else
            {
                _synced = false;   // on repart de la position actuelle du curseur
                _timer.Start();
            }
            return IsEnabled;
        }

        public void Disable()
        {
            _timer.Stop();
        }

        private void OnTick(object sender, EventArgs e)
        {
            // Pas de regard disponible, ou mesure trop ancienne : pause.
            if (!_gaze.IsRunning || !_gaze.HasData || _gaze.MsSinceLastSample > Parametres.Courants.AgeMaxMesure)
            {
                _synced = false;
                return;
            }

            double gx;
            double gy;
            _gaze.GetLatest(out gx, out gy);

            // Regard hors de l'écran : pause.
            if (GazeSource.IsOffScreen(gx, gy, _screenWidth, _screenHeight))
            {
                _synced = false;
                return;
            }

            // Après une pause, on repart de l'endroit où se trouve réellement le curseur
            // (il a pu être déplacé par TD Control ou par la souris entre-temps).
            if (!_synced)
            {
                NativeMethods.POINT current;
                if (!NativeMethods.GetCursorPos(out current))
                {
                    return;
                }
                _x = current.X;
                _y = current.Y;
                _synced = true;
            }

            double dx = gx - _x;
            double dy = gy - _y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < Parametres.Courants.ZoneMorteCurseur)
            {
                return;
            }

            // Le curseur parcourt une fraction (Lissage) de la distance qui le sépare du regard.
            double lissage = Parametres.Courants.Lissage;
            _x += dx * lissage;
            _y += dy * lissage;

            NativeMethods.SetCursorPos((int)Math.Round(_x), (int)Math.Round(_y));
        }
    }
}
