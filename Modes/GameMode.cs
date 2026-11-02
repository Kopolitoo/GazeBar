using System;

namespace GazeBar
{
    /// <summary>
    /// MODE JEU (étape 9 de la feuille de route, section « 10. Mode jeu » du cahier des charges).
    ///
    /// Entrée : bouton « Jeu » du menu. Sortie : regarder en bas, hors de l'écran, pendant
    /// [DelaiMenu] ouvre le menu (MenuAutorise = oui), où le bouton devient « Quitter le jeu ».
    /// La touche Échap de l'aidant ramène aussi au mode mouvement.
    ///
    /// Dans ce mode, rien n'est dessiné : pas de cercle, pas de fixation, pas de regard fixe,
    /// pas de clic au regard. L'utilisateur clique avec son contacteur ; aucune touche du
    /// clavier n'est envoyée.
    ///
    /// Deux variantes, choisies par le réglage [SourisJeu] :
    ///
    ///   « directe » (par défaut, pour Mindball Play) : le curseur bouge EXACTEMENT comme en
    ///   mode mouvement avec le suivi souris, mais sans cercle (demande de l'utilisateur du
    ///   28/10/2026 : avec [LissageJeu] = 0.7 puis 0.3, le curseur saccadait trop). Donc :
    ///   même lissage double que le mode mouvement, mais avec son propre réglage [LissageJeu]
    ///   (0.16 par défaut, comme [Lissage] : l'utilisateur veut deux réglages séparés), et
    ///   anti-tremblement [ZoneMorteCurseur] :
    ///   au repos, le curseur ne bouge pas tant que le regard lissé en est à moins de
    ///   [ZoneMorteCurseur] pixels ; au-delà, il le suit en continu jusqu'à l'avoir rattrapé.
    ///   Regard perdu ou hors de l'écran : le curseur reste où il est.
    ///
    ///   « relative » (pour d'autres jeux) : beaucoup de jeux ignorent la position du curseur
    ///   et ne lisent que les DÉPLACEMENTS de la souris (« entrée brute »). On envoie donc des
    ///   déplacements avec SendInput. Deux façons de traduire le regard, choisies par
    ///   [RelativeJeu] (le cahier des charges dit « à tester ») :
    ///     « suivi »   : le déplacement envoyé suit le déplacement du regard lissé
    ///                   (le regard bouge de 50 px → la souris bouge de 50 px × [SensibiliteJeu]) ;
    ///     « manette » : l'écart entre le regard et le CENTRE de l'écran donne une vitesse,
    ///                   comme le petit levier d'une manette. Au centre, dans un cercle de rayon
    ///                   [RayonCercle], rien ne bouge ; la vitesse augmente ensuite jusqu'au bord
    ///                   de l'écran.
    ///   Regard perdu ou hors de l'écran : plus aucun déplacement n'est envoyé.
    ///
    /// Attention, limites connues : un jeu en « plein écran exclusif » peut cacher le calque de
    /// GazeBar (le menu du bas pourrait alors ne pas se voir), et rien n'est garanti face aux
    /// systèmes anti-triche de certains jeux en ligne, qui peuvent refuser les entrées simulées.
    /// </summary>
    internal sealed class GameMode : Mode
    {
        /// <summary>
        /// Mode manette : vitesse maximale du curseur, en pixels de l'écran 4K par seconde
        /// (convertie avec Ecran.PxRegard, étape 11), atteinte quand le
        /// regard est au bord de l'écran (avant multiplication par [SensibiliteJeu]).
        /// Valeur de départ, à ajuster après essai.
        /// </summary>
        private const double MaxGamepadSpeed = 1500;

        // Le regard lissé avec [LissageJeu] (réglage séparé de [Lissage], celui du mode mouvement).
        private readonly Smoother _smoothed = new Smoother(() => AppSettings.Current.GameSmoothing);

        // Vrai quand _lisse a une position valable (sinon on la place sur le regard).
        private bool _placed;

        // Souris directe : vrai quand le curseur est « en route » derrière le regard lissé
        // (même principe que le suivi souris du mode mouvement).
        private bool _cursorOnTheWay;

        // Souris relative, mode suivi : position lissée au tic précédent.
        private double _previousX;
        private double _previousY;

        // Souris relative : les morceaux de pixel pas encore envoyés. SendInput n'accepte que
        // des pixels entiers ; sans ces restes, les petits mouvements lents seraient perdus.
        private double _remainderX;
        private double _remainderY;

        // Souris relative, mode manette : instant du tic précédent (−1 = aucun).
        private double _lastMs = -1;

        public override string Name
        {
            get { return "Jeu (" + AppSettings.Current.GameMouse + ")"; }
        }

        /// <summary>Le menu s'ouvre depuis le jeu : c'est la sortie au regard (choix du 25/10/2026).</summary>
        public override bool MenuAllowed
        {
            get { return true; }
        }

        /// <summary>
        /// L'écran a changé (étape 11) : ce mode continue, car un jeu ne doit pas être interrompu, et la position
        /// du curseur est recalculée à chaque tic.
        /// </summary>
        public override bool AdaptToScreen()
        {
            return true;
        }

        public override void Enter(GazeState gaze)
        {
            // Aucun dessin dans ce mode : le jeu doit rester entièrement visible.
            Manager.Layer.HideAll();
            Modifiers.DisableAll();

            // On repart de zéro à chaque entrée (aussi au retour du menu) : le lissage repart
            // du regard actuel, sans glisser depuis une vieille position.
            _placed = false;
            _cursorOnTheWay = false;
            _remainderX = 0;
            _remainderY = 0;
            _lastMs = -1;

            App.Log("Mode jeu : souris " + AppSettings.Current.GameMouse +
                    (AppSettings.Current.RelativeMouse ? " (" + AppSettings.Current.GameRelative + ")" : ""));
        }

        public override void Update(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // Regard perdu ou hors de l'écran : on ne bouge plus rien. Au retour du regard, le
            // lissage repartira du regard (pas de déplacement brusque en mode relatif).
            if (!gaze.OnScreen)
            {
                _placed = false;
                _lastMs = -1;
                _remainderX = 0;
                _remainderY = 0;
                return;
            }

            if (!_placed)
            {
                // Point de départ du lissage. En souris directe, on part du CURSEUR (comme le
                // cercle du mode mouvement part de sa place) : il glisse vers le regard au lieu
                // de sauter. En souris relative, on part du regard (aucun déplacement envoyé).
                NativeMethods.POINT cursor;
                if (!p.RelativeMouse && NativeMethods.GetCursorPos(out cursor))
                {
                    _smoothed.Place(cursor.X, cursor.Y);
                }
                else
                {
                    _smoothed.Place(gaze.X, gaze.Y);
                }
                _previousX = _smoothed.X;
                _previousY = _smoothed.Y;
                _placed = true;
            }
            _smoothed.Follow(gaze.X, gaze.Y, gaze.NowMs);

            if (!p.RelativeMouse)
            {
                DirectMouse();
            }
            else if (p.GamepadRelative)
            {
                GamepadMouse(gaze.NowMs);
            }
            else
            {
                TrackingMouse();
            }
        }

        /// <summary>
        /// Souris directe : le curseur suit le regard lissé, comme le suivi souris du mode
        /// mouvement (ModeMouvement.DeplacerCurseur) :
        ///  - au repos, le curseur ne bouge pas tant que le regard lissé en est à moins de
        ///    [ZoneMorteCurseur] pixels (le petit tremblement du regard est ignoré) ;
        ///  - au-delà, il se met « en route » et suit à chaque image, en continu, jusqu'à
        ///    l'avoir rattrapé (moins d'un pixel), puis revient au repos.
        /// On lit la vraie position du curseur : si le jeu l'a déplacé, on en tient compte.
        /// </summary>
        private void DirectMouse()
        {
            NativeMethods.POINT cursor;
            if (!NativeMethods.GetCursorPos(out cursor))
            {
                return;
            }

            double gapX = cursor.X - _smoothed.X;
            double gapY = cursor.Y - _smoothed.Y;
            double distance = Math.Sqrt(gapX * gapX + gapY * gapY);
            if (!_cursorOnTheWay && distance >= AppSettings.Current.CursorDeadZonePx)
            {
                _cursorOnTheWay = true;
            }
            if (!_cursorOnTheWay)
            {
                return;
            }

            NativeMethods.SetCursorPos((int)Math.Round(_smoothed.X), (int)Math.Round(_smoothed.Y));
            if (distance < 1)
            {
                _cursorOnTheWay = false; // rattrapé : retour au repos
            }
        }

        /// <summary>Souris relative « suivi » : on envoie le déplacement du regard lissé depuis le tic précédent.</summary>
        private void TrackingMouse()
        {
            double dx = (_smoothed.X - _previousX) * AppSettings.Current.GameSensitivity;
            double dy = (_smoothed.Y - _previousY) * AppSettings.Current.GameSensitivity;
            _previousX = _smoothed.X;
            _previousY = _smoothed.Y;
            SendMove(dx, dy);
        }

        /// <summary>
        /// Souris relative « manette » : l'écart entre le regard lissé et le centre de l'écran
        /// donne une vitesse. Le déplacement envoyé = vitesse × temps écoulé depuis le tic précédent.
        /// </summary>
        private void GamepadMouse(double nowMs)
        {
            double dt = _lastMs < 0 ? 0 : (nowMs - _lastMs) / 1000.0;
            _lastMs = nowMs;
            if (dt <= 0)
            {
                return;
            }
            if (dt > 0.05)
            {
                dt = 0.05; // après un tic très en retard, pas de grand bond
            }

            double centerX = Manager.ScreenWidth / 2;
            double centerY = Manager.ScreenHeight / 2;
            double gapX = _smoothed.X - centerX;
            double gapY = _smoothed.Y - centerY;
            double distance = Math.Sqrt(gapX * gapX + gapY * gapY);

            // Zone neutre au centre : le cercle de rayon [RayonCercle], comme pour le défilement.
            double neutralZone = AppSettings.Current.CircleRadiusPx;
            // Distance où la vitesse est au maximum : le bord le plus proche (demi-hauteur).
            double maxDistance = Math.Min(centerX, centerY);
            if (distance <= neutralZone || maxDistance <= neutralZone)
            {
                return;
            }

            // De 0 (bord de la zone neutre) à 1 (bord de l'écran), sans dépasser 1.
            double force = Math.Min(1.0, (distance - neutralZone) / (maxDistance - neutralZone));
            // VitesseMaxManette est en pixels de l'écran 4K par seconde : convertie (étape 11).
            double speed = force * ScreenMetrics.GazePx(MaxGamepadSpeed) * AppSettings.Current.GameSensitivity;

            // Même direction que l'écart (ecart / distance = direction de longueur 1).
            SendMove(gapX / distance * speed * dt, gapY / distance * speed * dt);
        }

        /// <summary>Ajoute le déplacement aux restes et envoie la partie entière (en pixels).</summary>
        private void SendMove(double dx, double dy)
        {
            _remainderX += dx;
            _remainderY += dy;
            int wholeX = (int)Math.Truncate(_remainderX);
            int wholeY = (int)Math.Truncate(_remainderY);
            if (wholeX == 0 && wholeY == 0)
            {
                return;
            }
            _remainderX -= wholeX;
            _remainderY -= wholeY;
            MouseInjector.MoveBy(wholeX, wholeY);
        }
    }
}
