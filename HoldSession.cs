using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace GazeBar
{
    /// <summary>Les trois actions proposées par la barre d'outils.</summary>
    internal enum HoldKind
    {
        /// <summary>Appui long : bouton gauche maintenu pendant une durée fixe, puis relâché.</summary>
        LongPressLeft,

        /// <summary>Glisser-déposer avec le bouton gauche.</summary>
        DragLeft,

        /// <summary>Glisser-déposer avec le bouton droit.</summary>
        DragRight
    }

    /// <summary>
    /// LOGIQUE : déroule une action « bouton maintenu » du début à la fin.
    ///
    /// Pourquoi un compte à rebours ? Pour cliquer sur le bouton de la barre d'outils,
    /// votre regard (donc le curseur) est sur la barre. Après le clic, vous avez
    /// quelques secondes pour regarder l'objet visé. À zéro, le bouton de la souris
    /// est enfoncé À L'ENDROIT OÙ SE TROUVE LE CURSEUR.
    ///
    /// Déroulement :
    ///   Compte à rebours  →  bouton enfoncé  →  bouton relâché
    ///
    /// Comment le bouton est-il relâché ?
    ///  - Appui long : après une durée fixe.
    ///  - Glisser : quand le regard s'est déplacé d'au moins MinDragDistancePixels,
    ///    puis reste IMMOBILE (dans un rayon de FixationRadiusPixels) pendant
    ///    DropFixationSeconds. C'est la « fixation » : on dépose là où l'on regarde.
    ///  - Dans tous les cas : après MaxHoldSeconds (sécurité), ou par le bouton Annuler.
    ///
    /// Tous les réglages sont ci-dessous. Les bonnes valeurs dépendent de la précision
    /// de votre regard : on les ajustera d'après les enregistrements de diagnostic.
    /// </summary>
    internal sealed class HoldSession
    {
        // ───────────────────────── Réglages ─────────────────────────

        /// <summary>Délai entre le clic sur la barre d'outils et l'appui du bouton de la souris.</summary>
        public const double CountdownSeconds = 3.0;

        /// <summary>Durée de l'appui long.</summary>
        public const double LongPressSeconds = 1.5;

        /// <summary>Durée d'immobilité du regard qui déclenche le dépôt, pour un glisser.</summary>
        public const double DropFixationSeconds = 1.2;

        /// <summary>Rayon (en pixels) dans lequel le regard est considéré comme immobile. Le regard tremble toujours un peu.</summary>
        public const double FixationRadiusPixels = 30.0;

        /// <summary>Distance minimale parcourue avant que le dépôt par fixation soit possible (évite de déposer tout de suite).</summary>
        public const double MinDragDistancePixels = 60.0;

        /// <summary>Sécurité : le bouton est relâché au plus tard après cette durée, quoi qu'il arrive.</summary>
        public const double MaxHoldSeconds = 20.0;

        // ───────────────────────── État interne ─────────────────────────

        private enum Phase
        {
            Idle,       // rien en cours
            Countdown,  // compte à rebours avant l'appui
            Holding     // bouton enfoncé
        }

        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private Phase _phase = Phase.Idle;
        private HoldKind _kind;
        private MouseSide _side;
        private double _phaseStartSeconds;

        private int _originX;           // où le bouton a été enfoncé
        private int _originY;
        private int _anchorX;           // dernière position « stable » du regard
        private int _anchorY;
        private double _anchorSeconds;  // depuis quand le regard est près de cette position
        private bool _hasMoved;        // le regard s'est-il assez éloigné de l'origine ?

        private string _lastMessage = "";

        /// <summary>Déclenché quand le message d'état change (« Appui dans 3 s… », etc.). L'interface l'affiche.</summary>
        public event Action<string> StatusChanged;

        /// <summary>Vrai si une action est en cours (compte à rebours ou bouton enfoncé).</summary>
        public bool IsActive
        {
            get { return _phase != Phase.Idle; }
        }

        public HoldSession()
        {
            // Le minuteur appelle OnTick toutes les 50 ms tant qu'une action est en cours.
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(50);
            _timer.Tick += OnTick;
        }

        private double Now
        {
            get { return _clock.Elapsed.TotalSeconds; }
        }

        // ───────────────────────── Commandes ─────────────────────────

        /// <summary>Démarre une action (lance le compte à rebours).</summary>
        public void Start(HoldKind kind)
        {
            if (IsActive)
            {
                Report("Une action est déjà en cours. Utilisez Annuler.");
                return;
            }

            _kind = kind;
            _side = kind == HoldKind.DragRight ? MouseSide.Right : MouseSide.Left;
            _phase = Phase.Countdown;
            _phaseStartSeconds = Now;
            _timer.Start();

            Report("Regardez l'endroit visé : appui dans " + CountdownSeconds.ToString("F0") + " s");
        }

        /// <summary>Annule l'action en cours et relâche le bouton s'il était enfoncé.</summary>
        public void Cancel()
        {
            if (_phase == Phase.Idle)
            {
                Report("Rien à annuler.");
                return;
            }

            Finish("Annulé.");
        }

        // ───────────────────────── Déroulement ─────────────────────────

        /// <summary>Appelée toutes les 50 ms pendant une action.</summary>
        private void OnTick(object sender, EventArgs e)
        {
            if (_phase == Phase.Countdown)
            {
                TickCountdown();
            }
            else if (_phase == Phase.Holding)
            {
                TickHolding();
            }
        }

        private void TickCountdown()
        {
            double remaining = CountdownSeconds - (Now - _phaseStartSeconds);

            if (remaining > 0)
            {
                int secondsLeft = (int)Math.Ceiling(remaining);
                Report("Regardez l'endroit visé : appui dans " + secondsLeft + " s");
                return;
            }

            // Zéro : on enfonce le bouton à l'endroit actuel du curseur.
            int x;
            int y;
            ReadCursor(out x, out y);

            MouseInjector.Press(_side);

            _phase = Phase.Holding;
            _phaseStartSeconds = Now;
            _originX = x;
            _originY = y;
            _anchorX = x;
            _anchorY = y;
            _anchorSeconds = Now;
            _hasMoved = false;
        }

        private void TickHolding()
        {
            double held = Now - _phaseStartSeconds;

            // Sécurité absolue : jamais plus de MaxHoldSeconds.
            if (held >= MaxHoldSeconds)
            {
                Finish("Durée maximale atteinte : bouton relâché.");
                return;
            }

            if (_kind == HoldKind.LongPressLeft)
            {
                double remaining = LongPressSeconds - held;
                if (remaining <= 0)
                {
                    Finish("Appui long terminé.");
                }
                else
                {
                    Report("Appui long en cours : encore " + remaining.ToString("F1") + " s");
                }
                return;
            }

            // Glisser : on suit le regard.
            int x;
            int y;
            ReadCursor(out x, out y);

            if (Distance(x, y, _originX, _originY) >= MinDragDistancePixels)
            {
                _hasMoved = true;
            }

            // Si le regard s'est éloigné de la dernière position stable, on repart de cette nouvelle position.
            if (Distance(x, y, _anchorX, _anchorY) > FixationRadiusPixels)
            {
                _anchorX = x;
                _anchorY = y;
                _anchorSeconds = Now;
            }

            double stillFor = Now - _anchorSeconds;

            if (!_hasMoved)
            {
                Report("Bouton enfoncé : déplacez le regard vers la destination");
            }
            else if (stillFor >= DropFixationSeconds)
            {
                Finish("Déposé.");
            }
            else
            {
                Report("Fixez la destination : dépôt dans " + (DropFixationSeconds - stillFor).ToString("F1") + " s");
            }
        }

        /// <summary>Termine l'action : relâche le bouton, arrête le minuteur, prévient l'interface.</summary>
        private void Finish(string message)
        {
            MouseInjector.Release(_side);   // ne fait rien si le bouton n'était pas enfoncé
            _phase = Phase.Idle;
            _timer.Stop();
            Report(message);
        }

        // ───────────────────────── Utilitaires ─────────────────────────

        private static void ReadCursor(out int x, out int y)
        {
            NativeMethods.POINT point;
            NativeMethods.GetCursorPos(out point);
            x = point.X;
            y = point.Y;
        }

        /// <summary>Distance entre deux points (théorème de Pythagore).</summary>
        private static double Distance(int x1, int y1, int x2, int y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Envoie un message d'état à l'interface, seulement s'il a changé.</summary>
        private void Report(string message)
        {
            if (message == _lastMessage)
            {
                return;
            }

            _lastMessage = message;
            Action<string> handler = StatusChanged;
            if (handler != null)
            {
                handler(message);
            }
        }
    }
}
