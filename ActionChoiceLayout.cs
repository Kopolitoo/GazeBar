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
    ///
    /// Étape 11 : [Espace] et [MargeBord] sont des pixels de l'écran 4K de l'utilisateur,
    /// convertis pour l'écran actuel avec Ecran.Px (60 px en 4K → 30 px en 1920 × 1080).
    /// </summary>
    internal sealed class ActionChoiceLayout
    {
        /// <summary>Agrandissement de la photo (le cahier des charges fixe ×2).</summary>
        public const double Zoom = 2;

        /// <summary>
        /// Espace, en pixels de l'écran 4K, entre deux boutons voisins, entre les boutons et la photo, et entre
        /// les modificateurs et le bord gauche de l'écran (demande de l'utilisateur du 26/10/2026 :
        /// « environ 60 pixels »).
        /// </summary>
        public const double Space4K = 60;

        /// <summary>[Espace4K] en pixels de l'écran actuel (avec [TailleInterface]).</summary>
        public static double Space
        {
            get { return ScreenMetrics.Px(Space4K); }
        }

        /// <summary>Marge, en pixels de l'écran 4K, entre un bouton et le bord de l'écran quand il y est collé.</summary>
        private const double EdgeMargin4K = 10;

        private static double EdgeMargin
        {
            get { return ScreenMetrics.Px(EdgeMargin4K); }
        }

        /// <summary>
        /// Calcule la mise en page pour un écran de cette taille, des boutons de ce côté
        /// ([TailleIcones]) et une photo de ce côté AVANT agrandissement (2 × [RayonCercle]).
        /// </summary>
        public ActionChoiceLayout(double width, double height, double size, double photoSide)
        {
            Width = width;
            Height = height;
            Size = size;

            double cx = width / 2;
            double cy = height / 2;
            CenterX = cx;
            CenterY = cy;

            // ── La photo agrandie, au centre de l'écran.
            EnlargedPhotoSide = photoSide * Zoom;

            // ── La couronne : huit emplacements, DEUX PAR CÔTÉ de la photo (demande de
            //    l'utilisateur du 26/10/2026). Sur chaque côté, les deux boutons sont séparés de
            //    [Espace] pixels et centrés sur le milieu du côté. Ils sont à [Espace] pixels de
            //    la photo, sauf si l'écran est trop petit (en haut et en bas sur un écran 1080p) :
            //    ils se rapprochent alors de la photo pour rester entièrement sur l'écran.
            double halfPhoto = EnlargedPhotoSide / 2;
            double wantedDistance = halfPhoto + Space + size / 2;
            double distanceX = Math.Min(wantedDistance, width / 2 - size / 2 - EdgeMargin);
            double distanceY = Math.Min(wantedDistance, height / 2 - size / 2 - EdgeMargin);
            double offset = size + Space;   // du milieu du côté au centre de chaque bouton

            Crown = new Point[8];
            for (int i = 0; i < Crown.Length; i++)
            {
                Crown[i] = CrownSlot(i, cx, cy, distanceX, distanceY, offset);
            }

            // ── Les boutons utilitaires (Copier, Couper, Coller) : une ligne collée au bord
            //    haut de l'écran, centrée, répartie sur la largeur comme les modificateurs sur la
            //    hauteur (demande du 28/10/2026). Sur un écran 1920 × 1080 : x = 440, 960 et 1480.
            int utilityCount = ActionCatalog.SlotCount(IconGroup.Utilities);
            double utilitiesGap = (width - size * utilityCount) / utilityCount;
            double yUtilities = size / 2 + EdgeMargin;
            Utilities = new Point[utilityCount];
            for (int i = 0; i < utilityCount; i++)
            {
                double x = cx + (i - (utilityCount - 1) / 2.0) * (size + utilitiesGap);
                Utilities[i] = new Point(x, yUtilities);
            }

            // ── Les modificateurs : une colonne de trois, collée au bord gauche de l'écran
            //    (demande du 26/10/2026), à [Espace] pixels du bord.
            int modifierCount = ActionCatalog.SlotCount(IconGroup.Modifiers);
            double xModifiers = Space + size / 2;
            double modifiersGap = (height - size * modifierCount) / modifierCount;
            Modifiers = new Point[modifierCount];
            for (int i = 0; i < modifierCount; i++)
            {
                double y = cy + (i - (modifierCount - 1) / 2.0) * modifiersGap;
                Modifiers[i] = new Point(xModifiers, y);
            }

            // ── L'icône « retour », au milieu du bord droit.
            Back = new Point(width - size, cy);
        }

        public double Width { get; private set; }

        public double Height { get; private set; }

        /// <summary>Côté des boutons, en pixels.</summary>
        public double Size { get; private set; }

        /// <summary>Centre de l'écran (et de la photo).</summary>
        public double CenterX { get; private set; }

        public double CenterY { get; private set; }

        /// <summary>Côté de la photo agrandie, en pixels.</summary>
        public double EnlargedPhotoSide { get; private set; }

        /// <summary>Les huit emplacements de la couronne (voir PlaceCouronne pour l'ordre).</summary>
        public Point[] Crown { get; private set; }

        /// <summary>Les trois modificateurs, de haut en bas.</summary>
        public Point[] Modifiers { get; private set; }

        /// <summary>Les trois boutons utilitaires, de gauche à droite.</summary>
        public Point[] Utilities { get; private set; }

        /// <summary>L'icône « retour ».</summary>
        public Point Back { get; private set; }

        /// <summary>Les centres des emplacements d'un groupe.</summary>
        public Point[] Slots(IconGroup group)
        {
            switch (group)
            {
                case IconGroup.Crown:
                    return Crown;
                case IconGroup.Modifiers:
                    return Modifiers;
                default:
                    return Utilities;
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
        private static Point CrownSlot(int num, double cx, double cy, double distanceX,
                                           double distanceY, double offset)
        {
            switch (num)
            {
                case 0: return new Point(cx + offset, cy - distanceY);
                case 1: return new Point(cx + distanceX, cy - offset);
                case 2: return new Point(cx + distanceX, cy + offset);
                case 3: return new Point(cx + offset, cy + distanceY);
                case 4: return new Point(cx - offset, cy + distanceY);
                case 5: return new Point(cx - distanceX, cy + offset);
                case 6: return new Point(cx - distanceX, cy - offset);
                default: return new Point(cx - offset, cy - distanceY);
            }
        }
    }
}
