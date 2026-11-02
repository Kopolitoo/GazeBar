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
    internal sealed class DragGesture
    {
        public DragGesture(double startX, double startY, MouseSide button, bool withPause)
        {
            StartX = startX;
            StartY = startY;
            Button = button;
            WithPause = withPause;
        }

        /// <summary>Point de départ (là où le bouton sera enfoncé), en pixels d'écran.</summary>
        public double StartX { get; private set; }

        public double StartY { get; private set; }

        /// <summary>Le bouton de la souris maintenu pendant le glisser.</summary>
        public MouseSide Button { get; private set; }

        /// <summary>Vrai pour le glisser-déposer « android » : pause avant le déplacement.</summary>
        public bool WithPause { get; private set; }

        /// <summary>Nom lisible, pour le journal.</summary>
        public string Name
        {
            get
            {
                if (WithPause)
                {
                    return "glisser-déposer android";
                }
                return Button == MouseSide.Left ? "glisser-déposer gauche" : "glisser-déposer droit";
            }
        }
    }
}
