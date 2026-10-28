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
    internal sealed class ModeJeu : Mode
    {
        /// <summary>
        /// Mode manette : vitesse maximale du curseur, en pixels par seconde, atteinte quand le
        /// regard est au bord de l'écran (avant multiplication par [SensibiliteJeu]).
        /// Valeur de départ, à ajuster après essai.
        /// </summary>
        private const double VitesseMaxManette = 1500;

        // Le regard lissé avec [LissageJeu] (réglage séparé de [Lissage], celui du mode mouvement).
        private readonly Lisseur _lisse = new Lisseur(() => Parametres.Courants.LissageJeu);

        // Vrai quand _lisse a une position valable (sinon on la place sur le regard).
        private bool _place;

        // Souris directe : vrai quand le curseur est « en route » derrière le regard lissé
        // (même principe que le suivi souris du mode mouvement).
        private bool _curseurEnRoute;

        // Souris relative, mode suivi : position lissée au tic précédent.
        private double _precedentX;
        private double _precedentY;

        // Souris relative : les morceaux de pixel pas encore envoyés. SendInput n'accepte que
        // des pixels entiers ; sans ces restes, les petits mouvements lents seraient perdus.
        private double _resteX;
        private double _resteY;

        // Souris relative, mode manette : instant du tic précédent (−1 = aucun).
        private double _dernierMs = -1;

        public override string Nom
        {
            get { return "Jeu (" + Parametres.Courants.SourisJeu + ")"; }
        }

        /// <summary>Le menu s'ouvre depuis le jeu : c'est la sortie au regard (choix du 25/10/2026).</summary>
        public override bool MenuAutorise
        {
            get { return true; }
        }

        public override void Entrer(EtatRegard regard)
        {
            // Aucun dessin dans ce mode : le jeu doit rester entièrement visible.
            Gestionnaire.Calque.MasquerTout();
            Modificateurs.ToutDesactiver();

            // On repart de zéro à chaque entrée (aussi au retour du menu) : le lissage repart
            // du regard actuel, sans glisser depuis une vieille position.
            _place = false;
            _curseurEnRoute = false;
            _resteX = 0;
            _resteY = 0;
            _dernierMs = -1;

            App.Log("Mode jeu : souris " + Parametres.Courants.SourisJeu +
                    (Parametres.Courants.SourisRelative ? " (" + Parametres.Courants.RelativeJeu + ")" : ""));
        }

        public override void Actualiser(EtatRegard regard)
        {
            Parametres p = Parametres.Courants;

            // Regard perdu ou hors de l'écran : on ne bouge plus rien. Au retour du regard, le
            // lissage repartira du regard (pas de déplacement brusque en mode relatif).
            if (!regard.SurEcran)
            {
                _place = false;
                _dernierMs = -1;
                _resteX = 0;
                _resteY = 0;
                return;
            }

            if (!_place)
            {
                // Point de départ du lissage. En souris directe, on part du CURSEUR (comme le
                // cercle du mode mouvement part de sa place) : il glisse vers le regard au lieu
                // de sauter. En souris relative, on part du regard (aucun déplacement envoyé).
                NativeMethods.POINT curseur;
                if (!p.SourisRelative && NativeMethods.GetCursorPos(out curseur))
                {
                    _lisse.Placer(curseur.X, curseur.Y);
                }
                else
                {
                    _lisse.Placer(regard.X, regard.Y);
                }
                _precedentX = _lisse.X;
                _precedentY = _lisse.Y;
                _place = true;
            }
            _lisse.Suivre(regard.X, regard.Y, regard.MaintenantMs);

            if (!p.SourisRelative)
            {
                SourisDirecte();
            }
            else if (p.RelativeManette)
            {
                SourisManette(regard.MaintenantMs);
            }
            else
            {
                SourisSuivi();
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
        private void SourisDirecte()
        {
            NativeMethods.POINT curseur;
            if (!NativeMethods.GetCursorPos(out curseur))
            {
                return;
            }

            double ecartX = curseur.X - _lisse.X;
            double ecartY = curseur.Y - _lisse.Y;
            double distance = Math.Sqrt(ecartX * ecartX + ecartY * ecartY);
            if (!_curseurEnRoute && distance >= Parametres.Courants.ZoneMorteCurseur)
            {
                _curseurEnRoute = true;
            }
            if (!_curseurEnRoute)
            {
                return;
            }

            NativeMethods.SetCursorPos((int)Math.Round(_lisse.X), (int)Math.Round(_lisse.Y));
            if (distance < 1)
            {
                _curseurEnRoute = false; // rattrapé : retour au repos
            }
        }

        /// <summary>Souris relative « suivi » : on envoie le déplacement du regard lissé depuis le tic précédent.</summary>
        private void SourisSuivi()
        {
            double dx = (_lisse.X - _precedentX) * Parametres.Courants.SensibiliteJeu;
            double dy = (_lisse.Y - _precedentY) * Parametres.Courants.SensibiliteJeu;
            _precedentX = _lisse.X;
            _precedentY = _lisse.Y;
            EnvoyerDeplacement(dx, dy);
        }

        /// <summary>
        /// Souris relative « manette » : l'écart entre le regard lissé et le centre de l'écran
        /// donne une vitesse. Le déplacement envoyé = vitesse × temps écoulé depuis le tic précédent.
        /// </summary>
        private void SourisManette(double maintenantMs)
        {
            double dt = _dernierMs < 0 ? 0 : (maintenantMs - _dernierMs) / 1000.0;
            _dernierMs = maintenantMs;
            if (dt <= 0)
            {
                return;
            }
            if (dt > 0.05)
            {
                dt = 0.05; // après un tic très en retard, pas de grand bond
            }

            double centreX = Gestionnaire.LargeurEcran / 2;
            double centreY = Gestionnaire.HauteurEcran / 2;
            double ecartX = _lisse.X - centreX;
            double ecartY = _lisse.Y - centreY;
            double distance = Math.Sqrt(ecartX * ecartX + ecartY * ecartY);

            // Zone neutre au centre : le cercle de rayon [RayonCercle], comme pour le défilement.
            double zoneNeutre = Parametres.Courants.RayonCercle;
            // Distance où la vitesse est au maximum : le bord le plus proche (demi-hauteur).
            double distanceMax = Math.Min(centreX, centreY);
            if (distance <= zoneNeutre || distanceMax <= zoneNeutre)
            {
                return;
            }

            // De 0 (bord de la zone neutre) à 1 (bord de l'écran), sans dépasser 1.
            double force = Math.Min(1.0, (distance - zoneNeutre) / (distanceMax - zoneNeutre));
            double vitesse = force * VitesseMaxManette * Parametres.Courants.SensibiliteJeu;

            // Même direction que l'écart (ecart / distance = direction de longueur 1).
            EnvoyerDeplacement(ecartX / distance * vitesse * dt, ecartY / distance * vitesse * dt);
        }

        /// <summary>Ajoute le déplacement aux restes et envoie la partie entière (en pixels).</summary>
        private void EnvoyerDeplacement(double dx, double dy)
        {
            _resteX += dx;
            _resteY += dy;
            int entierX = (int)Math.Truncate(_resteX);
            int entierY = (int)Math.Truncate(_resteY);
            if (entierX == 0 && entierY == 0)
            {
                return;
            }
            _resteX -= entierX;
            _resteY -= entierY;
            MouseInjector.DeplacerDe(entierX, entierY);
        }
    }
}
