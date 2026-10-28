using System;
using System.Windows;

namespace GazeBar
{
    /// <summary>
    /// LA MISE EN PAGE de l'écran du choix d'action (étape 10) : où se trouvent la photo, les
    /// huit emplacements de la couronne, les trois modificateurs, les trois boutons
    /// utilitaires et l'icône « retour ».
    ///
    /// Ces calculs étaient faits dans ModeChoixAction. L'écran de configuration des icônes
    /// (ModeConfigIcones) doit montrer EXACTEMENT la même mise en page (demande de
    /// l'utilisateur du 28/10/2026) : ils sont donc rangés ici, et les deux modes s'en servent.
    ///
    /// Toutes les positions sont des CENTRES, en pixels d'écran (comme le regard).
    /// </summary>
    internal sealed class DispositionChoixAction
    {
        /// <summary>Agrandissement de la photo (le cahier des charges fixe ×2).</summary>
        public const double Zoom = 2;

        /// <summary>
        /// Espace, en pixels, entre deux boutons voisins, entre les boutons et la photo, et entre
        /// les modificateurs et le bord gauche de l'écran (demande de l'utilisateur du 26/10/2026 :
        /// « environ 60 pixels »).
        /// </summary>
        public const double Espace = 60;

        /// <summary>Marge, en pixels, entre un bouton et le bord de l'écran quand il y est collé.</summary>
        private const double MargeBord = 10;

        /// <summary>
        /// Calcule la mise en page pour un écran de cette taille, des boutons de ce côté
        /// ([TailleIcones]) et une photo de ce côté AVANT agrandissement (2 × [RayonCercle]).
        /// </summary>
        public DispositionChoixAction(double largeur, double hauteur, double taille, double cotePhoto)
        {
            Largeur = largeur;
            Hauteur = hauteur;
            Taille = taille;

            double cx = largeur / 2;
            double cy = hauteur / 2;
            CentreX = cx;
            CentreY = cy;

            // ── La photo agrandie, au centre de l'écran.
            CotePhotoAgrandie = cotePhoto * Zoom;

            // ── La couronne : huit emplacements, DEUX PAR CÔTÉ de la photo (demande de
            //    l'utilisateur du 26/10/2026). Sur chaque côté, les deux boutons sont séparés de
            //    [Espace] pixels et centrés sur le milieu du côté. Ils sont à [Espace] pixels de
            //    la photo, sauf si l'écran est trop petit (en haut et en bas sur un écran 1080p) :
            //    ils se rapprochent alors de la photo pour rester entièrement sur l'écran.
            double demiPhoto = CotePhotoAgrandie / 2;
            double distanceVoulue = demiPhoto + Espace + taille / 2;
            double distanceX = Math.Min(distanceVoulue, largeur / 2 - taille / 2 - MargeBord);
            double distanceY = Math.Min(distanceVoulue, hauteur / 2 - taille / 2 - MargeBord);
            double decalage = taille + Espace;   // du milieu du côté au centre de chaque bouton

            Couronne = new Point[8];
            for (int i = 0; i < Couronne.Length; i++)
            {
                Couronne[i] = PlaceCouronne(i, cx, cy, distanceX, distanceY, decalage);
            }

            // ── Les boutons utilitaires (Copier, Couper, Coller) : une ligne collée au bord
            //    haut de l'écran, centrée, répartie sur la largeur comme les modificateurs sur la
            //    hauteur (demande du 28/10/2026). Sur un écran 1920 × 1080 : x = 440, 960 et 1480.
            int nombreUtilitaires = CatalogueActions.NombreEmplacements(GroupeIcones.Utilitaires);
            double ecartUtilitaires = (largeur - taille * nombreUtilitaires) / nombreUtilitaires;
            double yUtilitaires = taille / 2 + MargeBord;
            Utilitaires = new Point[nombreUtilitaires];
            for (int i = 0; i < nombreUtilitaires; i++)
            {
                double x = cx + (i - (nombreUtilitaires - 1) / 2.0) * (taille + ecartUtilitaires);
                Utilitaires[i] = new Point(x, yUtilitaires);
            }

            // ── Les modificateurs : une colonne de trois, collée au bord gauche de l'écran
            //    (demande du 26/10/2026), à [Espace] pixels du bord.
            int nombreModificateurs = CatalogueActions.NombreEmplacements(GroupeIcones.Modificateurs);
            double xModificateurs = Espace + taille / 2;
            double ecartModificateurs = (hauteur - taille * nombreModificateurs) / nombreModificateurs;
            Modificateurs = new Point[nombreModificateurs];
            for (int i = 0; i < nombreModificateurs; i++)
            {
                double y = cy + (i - (nombreModificateurs - 1) / 2.0) * ecartModificateurs;
                Modificateurs[i] = new Point(xModificateurs, y);
            }

            // ── L'icône « retour », au milieu du bord droit.
            Retour = new Point(largeur - taille, cy);
        }

        public double Largeur { get; private set; }

        public double Hauteur { get; private set; }

        /// <summary>Côté des boutons, en pixels.</summary>
        public double Taille { get; private set; }

        /// <summary>Centre de l'écran (et de la photo).</summary>
        public double CentreX { get; private set; }

        public double CentreY { get; private set; }

        /// <summary>Côté de la photo agrandie, en pixels.</summary>
        public double CotePhotoAgrandie { get; private set; }

        /// <summary>Les huit emplacements de la couronne (voir PlaceCouronne pour l'ordre).</summary>
        public Point[] Couronne { get; private set; }

        /// <summary>Les trois modificateurs, de haut en bas.</summary>
        public Point[] Modificateurs { get; private set; }

        /// <summary>Les trois boutons utilitaires, de gauche à droite.</summary>
        public Point[] Utilitaires { get; private set; }

        /// <summary>L'icône « retour ».</summary>
        public Point Retour { get; private set; }

        /// <summary>Les centres des emplacements d'un groupe.</summary>
        public Point[] Places(GroupeIcones groupe)
        {
            switch (groupe)
            {
                case GroupeIcones.Couronne:
                    return Couronne;
                case GroupeIcones.Modificateurs:
                    return Modificateurs;
                default:
                    return Utilitaires;
            }
        }

        /// <summary>
        /// Centre de l'emplacement numéro « numero » de la couronne, en pixels d'écran.
        /// Deux emplacements par côté de la photo, dans le sens des aiguilles d'une montre en
        /// partant du haut :
        ///         7   0
        ///      6         1
        ///         photo
        ///      5         2
        ///         4   3
        /// distanceX, distanceY : écart entre le centre de l'écran et la ligne des boutons des
        /// côtés gauche/droit (X) et haut/bas (Y) ; decalage : écart entre le milieu d'un côté
        /// et le centre de chacun de ses deux boutons.
        /// </summary>
        private static Point PlaceCouronne(int numero, double cx, double cy, double distanceX,
                                           double distanceY, double decalage)
        {
            switch (numero)
            {
                case 0: return new Point(cx + decalage, cy - distanceY);
                case 1: return new Point(cx + distanceX, cy - decalage);
                case 2: return new Point(cx + distanceX, cy + decalage);
                case 3: return new Point(cx + decalage, cy + distanceY);
                case 4: return new Point(cx - decalage, cy + distanceY);
                case 5: return new Point(cx - distanceX, cy + decalage);
                case 6: return new Point(cx - distanceX, cy - decalage);
                default: return new Point(cx - decalage, cy - distanceY);
            }
        }
    }
}
