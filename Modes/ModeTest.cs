using System;

namespace GazeBar
{
    /// <summary>
    /// Mode provisoire de l'étape 1 (socle-modes) : affiche un cercle de test sur le calque.
    ///
    /// Le cercle suit le regard, lissé par FiltreRegard (fluide quand l'œil se déplace,
    /// immobile quand il fixe). Il permet de vérifier :
    ///  1. qu'il s'affiche au-dessus de tout, et que les clics passent quand même à travers ;
    ///  2. qu'il est bien CENTRÉ sur l'endroit regardé, même avec une mise à l'échelle de Windows
    ///     à 125 % ou 150 % (sinon, il y a un problème de conversion DPI).
    /// Si le regard est perdu ou hors de l'écran, le cercle reste là où il était
    /// (au centre de l'écran tant qu'aucun regard n'a été reçu).
    ///
    /// Ce mode sera remplacé par le mode mouvement à l'étape 2.
    /// </summary>
    internal sealed class ModeTest : Mode
    {
        private double _x;
        private double _y;
        private double _ox;
        private double _oy;

        private int _slowCount; // Nombre de mesures à vitesse lente
        private readonly int _maxSlowCount = 30; // Nombre maximum de mesures à vitesse lente avant de déclencher le mode de regard fixe : 30 mesures = 0.5 seconde

        public override string Nom
        {
            get { return "Test (cercle)"; }
        }

        public override void Entrer()
        {
            // Point de départ : le centre de l'écran.
            _x = _ox = Gestionnaire.LargeurEcran / 2;
            _y = _oy = Gestionnaire.HauteurEcran / 2;
            _slowCount = 0;

        }

        public override void Actualiser(EtatRegard regard)
        {
            if (regard.SurEcran)
            {
                // Calculer la distance depuis la dernière mesure
                double dx = regard.X - _ox;
                double dy = regard.Y - _oy;
                int distance = (int)Math.Sqrt(dx * dx + dy * dy);

                _ox = regard.X;
                _oy = regard.Y;

                if (distance < Parametres.Courants.MinMoveDistance)
                {
                    _slowCount += 1;
                    if (_slowCount >= _maxSlowCount)
                    {
                        if (_slowCount == _maxSlowCount)
                        {
                            App.Log(" OK REGARD FIXE started");
                            // On positionne le curseur sur la position du regard
                            NativeMethods.SetCursorPos((int)Math.Round(_ox), (int)Math.Round(_oy));
                            // On peut passer au mode de regard fixe
                        }
                        return;
                    }
                }
                else
                {
                    if (_slowCount >= _maxSlowCount)
                    {
                        App.Log("RETOUR MOUVEMENT");
                    }
                    _slowCount = 0;
                }
                dx = _ox - _x;
                dy = _oy - _y;

                _x += dx * Parametres.Courants.Lissage;
                _y += dy * Parametres.Courants.Lissage;
            }

            Gestionnaire.Calque.AfficherCercle(_x, _y, Parametres.Courants.RayonCercle);
        }

        public override void Quitter()
        {
            Gestionnaire.Calque.MasquerCercle();
        }
    }
}
