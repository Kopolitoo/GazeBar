namespace GazeBar
{
    /// <summary>
    /// MODÈLE : la description d'un glisser-déposer en cours de préparation (étape 6).
    ///
    /// Un glisser-déposer traverse trois modes (voir le cahier des charges) :
    ///   1. ModeGlisserArrivee   le cercle suit le regard jusqu'à une fixation sur le point
    ///                           d'arrivée ; une ligne en pointillés part du point de départ ;
    ///   2. ModeLoupe            la loupe affine le point d'arrivée ;
    ///   3. ModeGlisserExecution bouton enfoncé au départ, curseur déplacé jusqu'à l'arrivée
    ///                           en [DureeGlisser], bouton relâché.
    /// Cet objet passe de l'un à l'autre : il garde ce qui ne change pas en chemin.
    ///
    /// Les trois actions de la couronne ne diffèrent que par ces réglages :
    ///   glisser-déposer gauche   : bouton gauche, sans pause ;
    ///   glisser-déposer droit    : bouton droit, sans pause ;
    ///   glisser-déposer android  : bouton gauche, avec une pause de [DureePauseAndroid] entre
    ///                              l'appui et le déplacement (remplace l'ancien appui long).
    /// </summary>
    internal sealed class Glissement
    {
        public Glissement(double departX, double departY, MouseSide bouton, bool avecPause)
        {
            DepartX = departX;
            DepartY = departY;
            Bouton = bouton;
            AvecPause = avecPause;
        }

        /// <summary>Point de départ (là où le bouton sera enfoncé), en pixels d'écran.</summary>
        public double DepartX { get; private set; }

        public double DepartY { get; private set; }

        /// <summary>Le bouton de la souris maintenu pendant le glisser.</summary>
        public MouseSide Bouton { get; private set; }

        /// <summary>Vrai pour le glisser-déposer « android » : pause avant le déplacement.</summary>
        public bool AvecPause { get; private set; }

        /// <summary>Nom lisible, pour le journal.</summary>
        public string Nom
        {
            get
            {
                if (AvecPause)
                {
                    return "glisser-déposer android";
                }
                return Bouton == MouseSide.Left ? "glisser-déposer gauche" : "glisser-déposer droit";
            }
        }
    }
}
