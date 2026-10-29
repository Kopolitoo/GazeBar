using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : tous les réglages de GazeBar, au même endroit.
    ///
    /// Ils remplacent les constantes qui étaient écrites en haut des premiers fichiers
    /// (GazeMouse.cs et HoldSession.cs, supprimés à l'étape 6). Ils sont lus au démarrage et
    /// enregistrés dans le fichier :
    ///     %APPDATA%\GazeBar\parametres.json
    /// (par exemple C:\Users\Nom\AppData\Roaming\GazeBar\parametres.json).
    ///
    /// Le format JSON est du texte lisible, qu'on peut ouvrir avec le Bloc-notes. Attention :
    /// dans ce fichier, les nombres décimaux s'écrivent avec un POINT (0.3) et non une virgule.
    ///
    /// Pour lire et écrire le JSON, on utilise DataContractJsonSerializer, fourni avec
    /// .NET Framework 4.8 : aucune bibliothèque supplémentaire n'est nécessaire.
    /// Le principe : on marque la classe avec [DataContract] et chaque réglage à enregistrer
    /// avec [DataMember]. Le sérialiseur s'occupe du reste.
    ///
    /// Les noms des réglages reprennent ceux du cahier des charges, SANS accents
    /// (DelaiFixation au lieu de DélaiFixation) : ils sont plus faciles à taper et à chercher.
    ///
    /// Utilisation dans le code : Parametres.Courants.Lissage, Parametres.Courants.RayonCercle, etc.
    /// </summary>
    [DataContract(Name = "Parametres", Namespace = "")]
    internal sealed class Parametres
    {
        // ───────────────────────── Accès global ─────────────────────────

        /// <summary>
        /// Les réglages en cours d'utilisation, partagés par tout le programme.
        /// « static » : il n'en existe qu'un seul exemplaire. Remplacé au démarrage par
        /// Initialiser(), qui lit le fichier.
        /// </summary>
        public static Parametres Courants { get; private set; } = new Parametres();

        /// <summary>
        /// Version actuelle du fichier de réglages.
        ///   1 : étape 1 ;
        ///   2 : étape 2 — ClicDirect et SuiviSouris passent à « non » par défaut (choix de
        ///       l'utilisateur du 22/10/2026). Un fichier de version 1 est mis à jour une fois
        ///       (voir MettreAJour) ;
        ///   3 : étape 6 — nouveau glisser-déposer : DureePauseAndroid et DureeGlisser
        ///       remplacent DureeAppuiLong, DelaiDepot, DistanceMinGlisser et DureeMaxAppui,
        ///       supprimés (règle de l'utilisateur : un réglage qui ne sert plus est retiré) ;
        ///   4 : étape 9 — LissageJeu passe de 0.7 à 0.16 par défaut (essai de l'utilisateur
        ///       du 28/10/2026 : à 0.7, le curseur saccadait en souris directe) ;
        ///   5 : VitesseMaxDefilement passe de 10 à 15 crans/s par défaut (vitesse maximale du
        ///       défilement augmentée de moitié, demande de l'utilisateur du 28/10/2026) ;
        ///   6 : étape 10 — disposition des icônes du choix d'action (IconesCouronne,
        ///       IconesModificateurs, IconesUtilitaires). Rien à corriger : un fichier plus
        ///       ancien n'a pas ces listes, elles gardent donc la disposition par défaut ;
        ///   7 : étape 11 — les tailles passent des pixels au POURCENTAGE DE LA HAUTEUR DE
        ///       L'ÉCRAN (ZoneMorteCurseur, RayonFixation, VitesseMaxFixation, RayonCercle,
        ///       TailleIcones), et nouveau réglage TailleInterface. Les anciennes valeurs, réglées
        ///       sur l'écran 4K de l'utilisateur (2160 px de haut), sont converties une fois.
        /// </summary>
        public const int VersionActuelle = 7;

        /// <summary>Numéro du format du fichier, pour pouvoir le faire évoluer un jour sans tout casser.</summary>
        [DataMember(Order = 0)]
        public int VersionFichier { get; set; }

        // ───────────────────────── Regard ─────────────────────────

        /// <summary>Fraction du chemin parcourue en 16 ms (un tic), entre 0 et 1 (0 = immobile, 1 = regard brut, tremblant). Lissage double, en temps réel : voir Modes/Lisseur.cs.</summary>
        [DataMember(Order = 10)]
        public double Lissage { get; set; }

        /// <summary>
        /// Anti-tremblement du curseur, en % DE LA HAUTEUR DE L'ÉCRAN (étape 11 ; en pixels
        /// avant) : en dessous, le curseur ne bouge pas. 0.4 % = 4 px sur un écran de 1080 px.
        /// Dans le code, utiliser ZoneMorteCurseurPx (la valeur en pixels de l'écran actuel).
        /// </summary>
        [DataMember(Order = 11)]
        public double ZoneMorteCurseur { get; set; }

        /// <summary>Âge maximal d'une mesure du regard, en MILLISECONDES. Au-delà, le regard est considéré comme perdu.</summary>
        [DataMember(Order = 12)]
        public double AgeMaxMesure { get; set; }

        // ───────────────────────── Fixation ─────────────────────────

        /// <summary>
        /// Le regard est « immobile » tant qu'il reste dans ce rayon, en % de la hauteur de
        /// l'écran (étape 11). 1.4 % = 15 px sur un écran de 1080 px. Dans le code : RayonFixationPx.
        /// </summary>
        [DataMember(Order = 20)]
        public double RayonFixation { get; set; }

        /// <summary>Durée d'immobilité avant le passage en mode regard fixe, en secondes.</summary>
        [DataMember(Order = 21)]
        public double DelaiFixation { get; set; }

        /// <summary>
        /// Vitesse maximale du regard pendant une fixation, en % DE LA HAUTEUR DE L'ÉCRAN PAR
        /// SECONDE (étape 11 ; en pixels par seconde avant). Dans le code : VitesseMaxFixationPx.
        /// Entre deux mesures, on calcule la vitesse du regard (distance ÷ temps écoulé) :
        /// au-dessus de cette valeur, l'œil est en train de sauter vers un autre point
        /// (une « saccade »), ce n'est pas une fixation.
        /// Réglage ajouté à l'étape 2 (il ne figure pas dans la première version du cahier des
        /// charges). 42 %/s = 907 px/s sur l'écran 4K (l'ancienne valeur, 900 px/s), soit
        /// 30 pixels entre deux mesures du PCEye (≈ 30 par seconde).
        /// </summary>
        [DataMember(Order = 24)]
        public double VitesseMaxFixation { get; set; }

        /// <summary>Clic gauche automatique si la fixation se prolonge (oui / non). Non par défaut.</summary>
        [DataMember(Order = 22)]
        public bool ClicDirect { get; set; }

        /// <summary>Délai supplémentaire après l'entrée en regard fixe avant le clic direct, en secondes.</summary>
        [DataMember(Order = 23)]
        public double DelaiClicDirect { get; set; }

        // ───────────────────────── Affichage ─────────────────────────

        /// <summary>
        /// Rayon du cercle du mode mouvement (et zone de sortie du regard fixe), en % de la
        /// hauteur de l'écran (étape 11). 8.8 % = 190 px en 4K, 95 px en 1080p.
        /// Dans le code : RayonCerclePx (qui tient compte aussi de [TailleInterface]).
        /// </summary>
        [DataMember(Order = 30)]
        public double RayonCercle { get; set; }

        /// <summary>
        /// Côté des icônes activables au regard, en % de la hauteur de l'écran (étape 11).
        /// 5.6 % = 121 px en 4K, 60 px en 1080p. Dans le code : TailleIconesPx.
        /// </summary>
        [DataMember(Order = 31)]
        public double TailleIcones { get; set; }

        /// <summary>
        /// Taille de l'interface, en % (étape 11) : 75 (petite), 100 (normale, par défaut),
        /// 125 (grande) ou 150 (très grande). Agrandit ou réduit tout ce qui est dessiné
        /// sur le calque (cercle, icônes, barre de menu, textes…), mais pas les réglages du
        /// regard (rayon de fixation, vitesse maximale, anti-tremblement). Voir Ecran.cs.
        /// </summary>
        [DataMember(Order = 33)]
        public double TailleInterface { get; set; }

        // ── Les mêmes réglages en PIXELS de l'écran actuel (étape 11). Calculés à chaque
        //    lecture (l'écran peut changer pendant que GazeBar tourne) et pas enregistrés dans
        //    le fichier (pas de [DataMember]). C'est eux que les modes utilisent.

        /// <summary>[ZoneMorteCurseur] en pixels de l'écran actuel.</summary>
        public double ZoneMorteCurseurPx
        {
            get { return Ecran.Pourcent(ZoneMorteCurseur); }
        }

        /// <summary>[RayonFixation] en pixels de l'écran actuel.</summary>
        public double RayonFixationPx
        {
            get { return Ecran.Pourcent(RayonFixation); }
        }

        /// <summary>[VitesseMaxFixation] en pixels par seconde sur l'écran actuel.</summary>
        public double VitesseMaxFixationPx
        {
            get { return Ecran.Pourcent(VitesseMaxFixation); }
        }

        /// <summary>[RayonCercle] en pixels de l'écran actuel, multiplié par [TailleInterface].</summary>
        public double RayonCerclePx
        {
            get { return Ecran.PourcentInterface(RayonCercle); }
        }

        /// <summary>[TailleIcones] en pixels de l'écran actuel, multiplié par [TailleInterface].</summary>
        public double TailleIconesPx
        {
            get { return Ecran.PourcentInterface(TailleIcones); }
        }

        /// <summary>Afficher le libellé d'une action au survol de son icône (mode apprentissage).</summary>
        [DataMember(Order = 32)]
        public bool AfficherLibelles { get; set; }

        // ───────────────────────── Délais ─────────────────────────

        /// <summary>Durée du regard sur une icône avant son activation, en secondes.</summary>
        [DataMember(Order = 40)]
        public double DelaiActivation { get; set; }

        /// <summary>
        /// Durée du regard sur l'icône d'options du mode regard fixe avant son activation, en
        /// secondes. Beaucoup plus courte que [DelaiActivation] (demande de l'utilisateur du
        /// 22/10/2026) : c'est l'icône qu'on regarde le plus souvent, et la regarder par erreur
        /// n'a pas de conséquence (le choix d'action propose toujours « retour »).
        /// </summary>
        [DataMember(Order = 43)]
        public double DelaiIconeOptions { get; set; }

        /// <summary>Durée du regard en bas hors de l'écran avant l'ouverture du menu, en secondes.</summary>
        [DataMember(Order = 41)]
        public double DelaiMenu { get; set; }

        /// <summary>Durée du regard hors de la barre de menu avant sa fermeture, en secondes.</summary>
        [DataMember(Order = 42)]
        public double DelaiFermetureMenu { get; set; }

        // ───────────────────────── Loupe ─────────────────────────

        /// <summary>Passer par la loupe avant le choix d'action.</summary>
        [DataMember(Order = 50)]
        public bool LoupeAvantAction { get; set; }

        /// <summary>Agrandissement de la capture en mode loupe (4 = quatre fois plus grand).</summary>
        [DataMember(Order = 51)]
        public double ZoomLoupe { get; set; }

        // ───────────────────────── Actions ─────────────────────────

        /// <summary>
        /// Glisser-déposer « android » : pause entre l'appui du bouton et le début du
        /// déplacement, en secondes (comme pour déplacer une icône sur un téléphone).
        /// Remplace l'ancien DureeAppuiLong (étape 6).
        /// </summary>
        [DataMember(Order = 60)]
        public double DureePauseAndroid { get; set; }

        /// <summary>
        /// Durée du déplacement du curseur pendant un glisser-déposer, en secondes : le curseur
        /// va du point de départ au point d'arrivée en ce temps-là, bouton enfoncé.
        /// Remplace l'ancien DelaiDepot (étape 6) : l'arrivée se fixe maintenant avec [DelaiFixation].
        /// </summary>
        [DataMember(Order = 61)]
        public double DureeGlisser { get; set; }

        // ───────────────────────── Défilement ─────────────────────────

        // (Étape 8 : l'ancien réglage ZoneNeutreDefilement est supprimé, la zone où rien ne
        //  défile est maintenant le cercle lui-même, de rayon [RayonCercle]. S'il est encore
        //  dans parametres.json, il est simplement ignoré à la lecture.)

        /// <summary>
        /// Vitesse maximale du mode défilement, en crans de molette par seconde (étape 8).
        /// La vitesse vaut 0 au bord de la zone morte (la moitié de [RayonCercle]) et atteint
        /// cette valeur trois [RayonCercle] plus loin ; au-delà, elle ne monte plus.
        /// 15 par défaut depuis le 28/10/2026 (10 avant).
        /// </summary>
        [DataMember(Order = 71)]
        public double VitesseMaxDefilement { get; set; }

        // ───────────────────────── Jeu ─────────────────────────

        /// <summary>
        /// Lissage du mode jeu, même sens et même calcul que [Lissage] (voir Modes/Lisseur.cs),
        /// mais réglé à part (demande de l'utilisateur du 28/10/2026). 0.16 par défaut, comme
        /// le mode mouvement (0.7 au départ : le curseur saccadait beaucoup trop).
        /// </summary>
        [DataMember(Order = 80)]
        public double LissageJeu { get; set; }

        /// <summary>Multiplicateur des déplacements relatifs en mode jeu.</summary>
        [DataMember(Order = 81)]
        public double SensibiliteJeu { get; set; }

        /// <summary>
        /// Variante du mode jeu (étape 9, choix de l'utilisateur du 27/10/2026) :
        ///   « directe »  : le curseur suit le regard (par défaut, pour Mindball Play) ;
        ///   « relative » : des déplacements sont envoyés au jeu (pour d'autres jeux).
        /// Écrit en toutes lettres dans parametres.json, pour qu'on le lise facilement.
        /// </summary>
        [DataMember(Order = 82)]
        public string SourisJeu { get; set; }

        /// <summary>
        /// Souris relative du mode jeu : comment traduire le regard en déplacement (étape 9).
        ///   « suivi »   : le déplacement suit celui du regard (par défaut) ;
        ///   « manette » : l'écart entre le regard et le centre de l'écran donne la vitesse.
        /// Réglage ajouté à l'étape 9 : le cahier des charges propose ces deux façons « à tester ».
        /// </summary>
        [DataMember(Order = 83)]
        public string RelativeJeu { get; set; }

        // Les valeurs possibles de SourisJeu et RelativeJeu, écrites une seule fois.
        public const string SourisDirecteTexte = "directe";
        public const string SourisRelativeTexte = "relative";
        public const string RelativeSuiviTexte = "suivi";
        public const string RelativeManetteTexte = "manette";

        /// <summary>Vrai si le mode jeu envoie des déplacements (SourisJeu = « relative »). Pas enregistré dans le fichier.</summary>
        public bool SourisRelative
        {
            get { return SourisJeu == SourisRelativeTexte; }
            set { SourisJeu = value ? SourisRelativeTexte : SourisDirecteTexte; }
        }

        /// <summary>Vrai si la souris relative fonctionne comme une manette (RelativeJeu = « manette »). Pas enregistré.</summary>
        public bool RelativeManette
        {
            get { return RelativeJeu == RelativeManetteTexte; }
            set { RelativeJeu = value ? RelativeManetteTexte : RelativeSuiviTexte; }
        }

        // ───────────────────────── Général ─────────────────────────

        /// <summary>
        /// En mode mouvement, le curseur de la souris suit aussi le regard. Non par défaut :
        /// le curseur ne rejoint alors le regard qu'une fois celui-ci fixé (mode regard fixe).
        /// </summary>
        [DataMember(Order = 90)]
        public bool SuiviSouris { get; set; }

        /// <summary>
        /// Lancer GazeBar à l'ouverture de la session Windows (étape 7). Non par défaut.
        /// Le fichier de réglages garde le choix ; c'est DemarrageWindows qui l'applique
        /// réellement, dans le registre de Windows.
        /// </summary>
        [DataMember(Order = 91)]
        public bool DemarrerAvecWindows { get; set; }

        // ───────────────────────── Icônes du choix d'action (étape 10) ─────────────────────────

        /// <summary>
        /// Les actions placées sur les huit emplacements de la couronne du choix d'action, en
        /// partant du haut à droite dans le sens des aiguilles d'une montre (voir
        /// DispositionChoixAction). Chaque action est écrite par son nom (par exemple
        /// "ClicGauche", voir ActionChoix) ; "" = emplacement vide. Se règle dans l'écran de
        /// configuration des icônes (onglet « Icônes » du mode paramètres).
        /// </summary>
        [DataMember(Order = 100)]
        public string[] IconesCouronne { get; set; }

        /// <summary>Les trois emplacements du bord gauche, de haut en bas ("Ctrl", "Maj", "Alt" ou "").</summary>
        [DataMember(Order = 101)]
        public string[] IconesModificateurs { get; set; }

        /// <summary>Les trois emplacements du bord haut, de gauche à droite ("Copier", "Couper", "Coller" ou "").</summary>
        [DataMember(Order = 102)]
        public string[] IconesUtilitaires { get; set; }

        /// <summary>
        /// La disposition d'un groupe, lue dans les listes ci-dessus : un tableau avec une
        /// case par emplacement, null pour un emplacement vide. Pas enregistré tel quel.
        /// </summary>
        public ActionChoix?[] Disposition(GroupeIcones groupe)
        {
            string[] noms = Noms(groupe) ?? new string[0];
            ActionChoix?[] resultat = new ActionChoix?[CatalogueActions.NombreEmplacements(groupe)];
            for (int i = 0; i < resultat.Length && i < noms.Length; i++)
            {
                resultat[i] = CatalogueActions.Lire(noms[i], groupe);
            }
            return resultat;
        }

        /// <summary>
        /// Place une action sur un emplacement (null = le vider). La même action peut être
        /// placée sur plusieurs emplacements du groupe (demande de l'utilisateur du
        /// 28/10/2026). N'enregistre pas le fichier (voir Enregistrer).
        /// </summary>
        public void DefinirEmplacement(GroupeIcones groupe, int numero, ActionChoix? action)
        {
            ActionChoix?[] disposition = Disposition(groupe);
            if (numero < 0 || numero >= disposition.Length)
            {
                return;
            }
            disposition[numero] = action;
            EcrireDisposition(groupe, disposition);
        }

        /// <summary>Remet la disposition d'origine des trois groupes (bouton « Défaut » de l'onglet « Icônes »).</summary>
        public void IconesParDefaut()
        {
            IconesCouronne = NomsParDefaut(GroupeIcones.Couronne);
            IconesModificateurs = NomsParDefaut(GroupeIcones.Modificateurs);
            IconesUtilitaires = NomsParDefaut(GroupeIcones.Utilitaires);
        }

        /// <summary>La liste de noms d'un groupe, telle qu'enregistrée dans le fichier.</summary>
        private string[] Noms(GroupeIcones groupe)
        {
            switch (groupe)
            {
                case GroupeIcones.Couronne:
                    return IconesCouronne;
                case GroupeIcones.Modificateurs:
                    return IconesModificateurs;
                default:
                    return IconesUtilitaires;
            }
        }

        /// <summary>Écrit une disposition (une case par emplacement) dans la liste de noms du groupe.</summary>
        private void EcrireDisposition(GroupeIcones groupe, ActionChoix?[] disposition)
        {
            string[] noms = new string[disposition.Length];
            for (int i = 0; i < noms.Length; i++)
            {
                noms[i] = CatalogueActions.Ecrire(disposition[i]);
            }
            switch (groupe)
            {
                case GroupeIcones.Couronne:
                    IconesCouronne = noms;
                    break;
                case GroupeIcones.Modificateurs:
                    IconesModificateurs = noms;
                    break;
                default:
                    IconesUtilitaires = noms;
                    break;
            }
        }

        private static string[] NomsParDefaut(GroupeIcones groupe)
        {
            ActionChoix[] actions = CatalogueActions.DispositionParDefaut(groupe);
            string[] noms = new string[actions.Length];
            for (int i = 0; i < noms.Length; i++)
            {
                noms[i] = actions[i].ToString();
            }
            return noms;
        }

        /// <summary>
        /// Remet en ordre la liste d'un groupe lue dans le fichier : le bon nombre de cases,
        /// et les noms inconnus ou d'un autre groupe vidés (une action peut figurer plusieurs
        /// fois). Une liste absente reprend la disposition par défaut.
        /// </summary>
        private void CorrigerIcones(GroupeIcones groupe)
        {
            if (Noms(groupe) == null)
            {
                EcrireDisposition(groupe, Convertir(CatalogueActions.DispositionParDefaut(groupe)));
                return;
            }
            EcrireDisposition(groupe, Disposition(groupe));
        }

        private static ActionChoix?[] Convertir(ActionChoix[] actions)
        {
            ActionChoix?[] resultat = new ActionChoix?[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                resultat[i] = actions[i];
            }
            return resultat;
        }

        // ───────────────────────── Valeurs par défaut ─────────────────────────

        /// <summary>Le constructeur : un objet neuf contient les valeurs par défaut.</summary>
        public Parametres()
        {
            ValeursParDefaut();
        }

        /// <summary>
        /// Remet tous les réglages à leur valeur de départ (celles du cahier des charges).
        /// Le bouton « Défaut » de chaque onglet du mode paramètres (étape 7) ne remet que
        /// les réglages de son onglet : il lit ces valeurs dans un objet neuf (new Parametres()).
        /// </summary>
        public void ValeursParDefaut()
        {
            VersionFichier = VersionActuelle;

            Lissage = 0.16;
            // Tailles en % de la hauteur de l'écran (étape 11) : ce sont les anciennes valeurs
            // en pixels de l'écran 4K (8, 30, 900, 190, 120) divisées par 21.6, arrondies.
            ZoneMorteCurseur = 0.4;
            AgeMaxMesure = 300;

            RayonFixation = 1.4;
            DelaiFixation = 0.8;
            VitesseMaxFixation = 42;
            ClicDirect = false;
            DelaiClicDirect = 1.5;

            RayonCercle = 8.8;
            TailleIcones = 5.6;
            TailleInterface = 100;
            AfficherLibelles = true;

            DelaiActivation = 0.6;
            DelaiIconeOptions = 0.3;
            DelaiMenu = 0.5;
            DelaiFermetureMenu = 1.0;

            LoupeAvantAction = true;
            ZoomLoupe = 4;

            DureePauseAndroid = 1.0;
            DureeGlisser = 0.5;

            VitesseMaxDefilement = 15;

            LissageJeu = 0.16;
            SensibiliteJeu = 1.0;
            SourisJeu = SourisDirecteTexte;
            RelativeJeu = RelativeSuiviTexte;

            SuiviSouris = false;
            DemarrerAvecWindows = false;

            IconesParDefaut();
        }

        /// <summary>
        /// Appelée par le sérialiseur JUSTE AVANT de lire le fichier.
        ///
        /// Piège à connaître : à la lecture, DataContractJsonSerializer n'appelle PAS le
        /// constructeur. Un réglage absent du fichier (par exemple un réglage ajouté dans une
        /// nouvelle version de GazeBar) vaudrait alors 0 ou « non ». En remplissant d'abord les
        /// valeurs par défaut ici, un réglage absent garde sa valeur par défaut.
        /// </summary>
        [OnDeserializing]
        private void AvantLecture(StreamingContext contexte)
        {
            ValeursParDefaut();
        }

        /// <summary>
        /// Ramène chaque valeur dans des limites raisonnables. Protège contre une faute de
        /// frappe dans le fichier (un lissage de 30 au lieu de 0.30, un délai négatif…).
        /// </summary>
        public void Corriger()
        {
            Lissage = Borner(Lissage, 0.01, 1);
            // Limites des tailles en % de la hauteur de l'écran (étape 11).
            ZoneMorteCurseur = Borner(ZoneMorteCurseur, 0, 5);
            AgeMaxMesure = Borner(AgeMaxMesure, 50, 5000);

            RayonFixation = Borner(RayonFixation, 0.2, 14);
            DelaiFixation = Borner(DelaiFixation, 0.1, 10);
            VitesseMaxFixation = Borner(VitesseMaxFixation, 2, 900);
            DelaiClicDirect = Borner(DelaiClicDirect, 0.1, 10);

            RayonCercle = Borner(RayonCercle, 1, 23);
            TailleIcones = Borner(TailleIcones, 2, 19);
            TailleInterface = Borner(TailleInterface, 75, 150);

            DelaiActivation = Borner(DelaiActivation, 0.1, 10);
            DelaiIconeOptions = Borner(DelaiIconeOptions, 0.1, 10);
            DelaiMenu = Borner(DelaiMenu, 0.1, 10);
            DelaiFermetureMenu = Borner(DelaiFermetureMenu, 0.1, 10);

            ZoomLoupe = Borner(ZoomLoupe, 1, 10);

            DureePauseAndroid = Borner(DureePauseAndroid, 0, 10);
            DureeGlisser = Borner(DureeGlisser, 0.05, 10);

            VitesseMaxDefilement = Borner(VitesseMaxDefilement, 1, 100);

            LissageJeu = Borner(LissageJeu, 0.01, 1);
            SensibiliteJeu = Borner(SensibiliteJeu, 0.1, 10);

            // Un mot inconnu (faute de frappe, majuscules…) redevient la valeur par défaut.
            string souris = (SourisJeu ?? "").Trim().ToLowerInvariant();
            SourisJeu = souris == SourisRelativeTexte ? SourisRelativeTexte : SourisDirecteTexte;
            string relative = (RelativeJeu ?? "").Trim().ToLowerInvariant();
            RelativeJeu = relative == RelativeManetteTexte ? RelativeManetteTexte : RelativeSuiviTexte;

            CorrigerIcones(GroupeIcones.Couronne);
            CorrigerIcones(GroupeIcones.Modificateurs);
            CorrigerIcones(GroupeIcones.Utilitaires);
        }

        /// <summary>
        /// Met à jour un fichier écrit par une version plus ancienne de GazeBar.
        /// Renvoie vrai si quelque chose a changé (il faut alors réenregistrer le fichier).
        ///
        /// Pourquoi ? Changer une valeur par défaut ne suffit pas : le fichier déjà présent sur
        /// l'ordinateur contient l'ancienne valeur (par exemple ClicDirect = true), et c'est
        /// elle qui serait lue. On la corrige donc une seule fois, puis le numéro de version
        /// est augmenté : ensuite, un choix fait par l'utilisateur dans le fichier est respecté.
        /// </summary>
        public bool MettreAJour()
        {
            if (VersionFichier >= VersionActuelle)
            {
                return false;
            }

            if (VersionFichier < 2)
            {
                ClicDirect = false;
                SuiviSouris = false;
                App.Log("Paramètres : mise à jour en version 2 (ClicDirect et SuiviSouris désactivés)");
            }

            if (VersionFichier < 3)
            {
                // Rien à corriger : les anciens réglages du glisser (DureeAppuiLong, DelaiDepot…)
                // sont simplement ignorés à la lecture, et les nouveaux ont déjà leur valeur par
                // défaut (voir AvantLecture). Réenregistrer le fichier les fait disparaître.
                App.Log("Paramètres : mise à jour en version 3 (réglages du glisser-déposer remplacés)");
            }

            if (VersionFichier < 4)
            {
                // Seul l'ancien défaut est remplacé : une valeur choisie par l'utilisateur reste.
                if (Math.Abs(LissageJeu - 0.7) < 0.001)
                {
                    LissageJeu = 0.16;
                }
                App.Log("Paramètres : mise à jour en version 4 (LissageJeu 0.7 → 0.16)");
            }

            if (VersionFichier < 5)
            {
                // Seul l'ancien défaut est remplacé : une valeur choisie par l'utilisateur reste.
                if (Math.Abs(VitesseMaxDefilement - 10) < 0.001)
                {
                    VitesseMaxDefilement = 15;
                }
                App.Log("Paramètres : mise à jour en version 5 (VitesseMaxDefilement 10 → 15)");
            }

            if (VersionFichier < 6)
            {
                // Rien à corriger : les listes d'icônes absentes du fichier ont gardé la
                // disposition par défaut (voir AvantLecture). Réenregistrer le fichier les ajoute.
                App.Log("Paramètres : mise à jour en version 6 (disposition des icônes du choix d'action)");
            }

            if (VersionFichier < 7)
            {
                // Les tailles étaient en pixels, réglées sur l'écran 4K de l'utilisateur
                // (2160 px de haut). Elles deviennent des % de la hauteur de l'écran :
                // pourcentage = pixels ÷ 2160 × 100 = pixels ÷ 21.6.
                // On divise TOUJOURS par 2160, et non par la hauteur de l'écran actuel : les
                // valeurs ont été réglées en 4K, même si l'écran est maintenant en 1920 × 1080.
                // Exemple : RayonCercle 190 px → 8.8 %.
                ZoneMorteCurseur = PixelsVersPourcent(ZoneMorteCurseur, 1);
                RayonFixation = PixelsVersPourcent(RayonFixation, 1);
                VitesseMaxFixation = PixelsVersPourcent(VitesseMaxFixation, 0);
                RayonCercle = PixelsVersPourcent(RayonCercle, 1);
                TailleIcones = PixelsVersPourcent(TailleIcones, 1);
                App.Log("Paramètres : mise à jour en version 7 (tailles en % de la hauteur de l'écran)");
            }
            VersionFichier = VersionActuelle;
            return true;
        }

        /// <summary>
        /// Conversion de la version 7 : des pixels de l'écran 4K en % de sa hauteur, arrondi
        /// à « decimales » chiffres après le point (comme dans le mode paramètres).
        /// </summary>
        private static double PixelsVersPourcent(double pixels, int decimales)
        {
            return Math.Round(pixels / Ecran.HauteurReferencePixels * 100, decimales);
        }

        /// <summary>Renvoie la valeur, ramenée entre min et max. Une valeur illisible (NaN) devient min.</summary>
        private static double Borner(double valeur, double min, double max)
        {
            if (double.IsNaN(valeur) || valeur < min)
            {
                return min;
            }
            if (valeur > max)
            {
                return max;
            }
            return valeur;
        }

        // ───────────────────────── Lecture et enregistrement ─────────────────────────

        /// <summary>Chemin complet du fichier de réglages.</summary>
        public static string CheminFichier
        {
            get
            {
                string dossier = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GazeBar");
                return Path.Combine(dossier, "parametres.json");
            }
        }

        /// <summary>À appeler une fois au démarrage : lit le fichier et remplit Parametres.Courants.</summary>
        public static void Initialiser()
        {
            Courants = Charger();
        }

        /// <summary>
        /// Lit le fichier de réglages.
        ///  - S'il n'existe pas encore (premier lancement), on le crée avec les valeurs par défaut.
        ///  - S'il est illisible (fichier abîmé, faute de syntaxe), on le met de côté sous le nom
        ///    parametres.json.illisible, et on repart des valeurs par défaut.
        /// Dans tous les cas, le programme démarre.
        /// </summary>
        public static Parametres Charger()
        {
            string chemin = CheminFichier;
            Parametres resultat;

            if (!File.Exists(chemin))
            {
                App.Log("Paramètres : premier lancement, création de " + chemin);
                resultat = new Parametres();
                resultat.Enregistrer();
                return resultat;
            }

            try
            {
                DataContractJsonSerializer serialiseur = new DataContractJsonSerializer(typeof(Parametres));
                using (FileStream fichier = File.OpenRead(chemin))
                {
                    resultat = (Parametres)serialiseur.ReadObject(fichier);
                }
                App.Log("Paramètres : lus depuis " + chemin);
                // La mise à jour d'un ancien fichier passe AVANT Corriger (étape 11) : les
                // anciennes tailles en pixels (190…) dépasseraient les nouvelles limites en %
                // (23 au plus) et seraient abîmées avant d'être converties.
                bool misAJour = resultat.MettreAJour();
                resultat.Corriger();
                if (misAJour)
                {
                    resultat.Enregistrer();
                }
                return resultat;
            }
            catch (Exception ex)
            {
                App.Log("Paramètres : fichier illisible, valeurs par défaut utilisées. " + ex.Message);
                try
                {
                    File.Copy(chemin, chemin + ".illisible", true);
                }
                catch
                {
                    // Tant pis pour la copie de secours : l'important est de démarrer.
                }
                resultat = new Parametres();
                resultat.Enregistrer();
                return resultat;
            }
        }

        /// <summary>
        /// Enregistre les réglages dans le fichier, joliment présenté (un réglage par ligne).
        ///
        /// On écrit d'abord dans un fichier temporaire, puis on le met à la place de l'ancien :
        /// si l'ordinateur s'éteint en pleine écriture, l'ancien fichier reste intact.
        /// </summary>
        public void Enregistrer()
        {
            string chemin = CheminFichier;
            string temporaire = chemin + ".tmp";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(chemin));

                DataContractJsonSerializer serialiseur = new DataContractJsonSerializer(typeof(Parametres));
                using (FileStream fichier = File.Create(temporaire))
                using (XmlDictionaryWriter ecrivain = JsonReaderWriterFactory.CreateJsonWriter(
                           fichier, new UTF8Encoding(false), false, true, "  "))
                {
                    // Paramètres de CreateJsonWriter : le fichier, l'encodage (UTF-8 sans BOM),
                    // « ne pas fermer le fichier nous-mêmes », « indenter », et l'indentation (2 espaces).
                    serialiseur.WriteObject(ecrivain, this);
                    ecrivain.Flush();
                }

                if (File.Exists(chemin))
                {
                    File.Replace(temporaire, chemin, null);
                }
                else
                {
                    File.Move(temporaire, chemin);
                }
            }
            catch (Exception ex)
            {
                // Ne jamais faire planter le programme pour un fichier de réglages.
                App.Log("Paramètres : échec de l'enregistrement : " + ex.Message);
            }
        }
    }
}
