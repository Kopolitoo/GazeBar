using System;

namespace GazeBar
{
    /// <summary>
    /// LISSAGE DU REGARD, partagé par tous les modes qui font suivre le regard à un cercle.
    ///
    /// ─── Pourquoi le mouvement « saccadait » ───
    ///
    /// Avant, chaque mode faisait à chaque tic : position += (regard − position) × [Lissage].
    /// C'est un lissage « à un étage ». Il a deux défauts :
    ///
    ///  1. Le PCEye 5 n'envoie qu'environ 30 mesures par seconde, alors que l'écran se redessine
    ///     60 fois par seconde. Le regard avance donc par MARCHES d'escalier. À chaque nouvelle
    ///     marche, le cercle repart brusquement vite, puis ralentit, puis repart… : sa vitesse
    ///     fait des à-coups environ 30 fois par seconde. L'œil voit ces à-coups.
    ///
    ///  2. La fraction était appliquée « par tic ». Si un tic arrive en retard (Windows n'est pas
    ///     parfaitement régulier), le cercle avance quand même de la même fraction : son mouvement
    ///     dépend des irrégularités de l'horloge.
    ///
    /// ─── Ce que fait ce lisseur ───
    ///
    ///  1. DEUX ÉTAGES à la suite (« lissage double ») : le premier étage suit le regard, le second
    ///     suit le premier. Le second étage reçoit une courbe déjà arrondie : la vitesse du cercle
    ///     ne fait plus d'à-coups, elle varie en douceur. Le tremblement du regard est aussi mieux
    ///     filtré. Chaque étage est deux fois plus rapide que l'ancien lissage unique : le retard
    ///     moyen sur le regard reste le même qu'avant, à réglage [Lissage] égal.
    ///
    ///  2. Le lissage dépend du TEMPS RÉEL écoulé, et non du nombre de tics : un tic en retard fait
    ///     avancer le cercle un peu plus, un tic en avance un peu moins. Le mouvement est régulier.
    ///
    /// Le réglage [Lissage] garde le même sens qu'avant : « la fraction du chemin parcourue en
    /// 16 millisecondes (un tic de référence) ». Plus petit = plus calme, plus grand = plus réactif.
    /// À 1, le cercle est directement sur le regard (pas de lissage).
    ///
    /// ─── Un peu de calcul, pour comprendre ───
    ///
    /// Avec une fraction L par tic de 16 ms, la distance restante est multipliée par (1 − L) à
    /// chaque tic. On peut écrire (1 − L) = e^(−16 / τ), où τ (« tau ») est la CONSTANTE DE TEMPS :
    /// τ = −16 / ln(1 − L). Exemple : L = 0,16 → τ ≈ 92 ms. Pour une durée quelconque dt, la
    /// distance restante est alors multipliée par e^(−dt / τ) : c'est la même chose, mais valable
    /// quel que soit dt. Chacun des deux étages utilise τ / 2.
    ///
    /// Utilisation dans un mode :
    ///     private readonly Lisseur _lisse = new Lisseur();
    ///     _lisse.Placer(x, y);                                  // départ, sans glissement
    ///     _lisse.Suivre(regard.X, regard.Y, regard.MaintenantMs); // à chaque tic
    ///     … _lisse.X, _lisse.Y …                                // la position lissée
    /// </summary>
    internal sealed class Lisseur
    {
        /// <summary>Durée de référence d'un tic, en ms : [Lissage] est « la fraction parcourue en 16 ms ».</summary>
        private const double TicReferenceMs = 16;

        /// <summary>
        /// Écart maximal pris en compte entre deux appels, en ms. Après une longue pause (regard
        /// perdu, mode suspendu…), on ne veut pas que le cercle saute d'un coup sur le regard :
        /// il repart en glissant, comme après un tic un peu long.
        /// </summary>
        private const double EcartMaxMs = 50;

        // Où lire le réglage de lissage : [Lissage] d'habitude, [LissageJeu] en mode jeu.
        private readonly Func<double> _lireLissage;

        /// <summary>Lisseur habituel, qui utilise le réglage [Lissage].</summary>
        public Lisseur()
            : this(() => Parametres.Courants.Lissage)
        {
        }

        /// <summary>
        /// Lisseur qui lit son lissage ailleurs (étape 9 : le mode jeu utilise [LissageJeu]).
        /// lireLissage est une fonction, appelée à chaque tic : un changement du réglage
        /// s'applique tout de suite.
        /// </summary>
        public Lisseur(Func<double> lireLissage)
        {
            _lireLissage = lireLissage;
        }

        // Premier étage (suit le regard).
        private double _x1;
        private double _y1;

        // Instant du dernier appel à Suivre, en ms (−1 = pas encore d'appel depuis Placer).
        private double _dernierMs = -1;

        /// <summary>Position lissée (second étage), en pixels d'écran.</summary>
        public double X { get; private set; }

        /// <summary>Position lissée (second étage), en pixels d'écran.</summary>
        public double Y { get; private set; }

        /// <summary>Place directement le cercle en (x, y), sans glissement (les deux étages).</summary>
        public void Placer(double x, double y)
        {
            _x1 = x;
            _y1 = y;
            X = x;
            Y = y;
            _dernierMs = -1;
        }

        /// <summary>
        /// Fait avancer la position lissée vers la cible (cibleX, cibleY), selon le temps écoulé
        /// depuis l'appel précédent. maintenantMs = regard.MaintenantMs.
        /// </summary>
        public void Suivre(double cibleX, double cibleY, double maintenantMs)
        {
            // 1. Temps écoulé depuis l'appel précédent (premier appel : un tic de référence).
            double dt = _dernierMs < 0 ? TicReferenceMs : maintenantMs - _dernierMs;
            _dernierMs = maintenantMs;
            if (dt <= 0)
            {
                return; // même instant : rien à faire
            }
            if (dt > EcartMaxMs)
            {
                dt = EcartMaxMs;
            }

            // 2. Lissage 1 (ou plus) : pas de lissage, le cercle est sur la cible.
            double lissage = _lireLissage();
            if (lissage >= 0.999)
            {
                Placer(cibleX, cibleY);
                _dernierMs = maintenantMs;
                return;
            }

            // 3. Constante de temps de chaque étage (la moitié de τ, voir l'explication en haut).
            double tau = -TicReferenceMs / Math.Log(1 - lissage);
            double tauEtage = tau / 2;

            // 4. Fraction du chemin parcourue pendant dt : 1 − e^(−dt / τ).
            double fraction = 1 - Math.Exp(-dt / tauEtage);

            // 5. Premier étage : vers la cible. Second étage : vers le premier.
            _x1 += (cibleX - _x1) * fraction;
            _y1 += (cibleY - _y1) * fraction;
            X += (_x1 - X) * fraction;
            Y += (_y1 - Y) * fraction;
        }
    }
}
