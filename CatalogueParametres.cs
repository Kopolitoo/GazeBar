using System;
using System.Collections.Generic;
using System.Globalization;

namespace GazeBar
{
    /// <summary>
    /// Un réglage tel qu'il est AFFICHÉ dans le mode paramètres (étape 7) : son nom en
    /// français, une phrase d'explication, son unité, le pas des boutons « − » et « + »,
    /// et la façon de le lire et de l'écrire dans Parametres.
    ///
    /// Deux sortes de réglages :
    ///   - un NOMBRE (Lissage, DelaiFixation…) : boutons « − », valeur, « + » ;
    ///   - un OUI / NON (ClicDirect, SuiviSouris…) : boutons « Non » et « Oui ».
    ///     Variante (étape 9) : un choix entre deux mots, par exemple « directe » ou
    ///     « relative » pour SourisJeu. C'est le même affichage, avec d'autres mots sur les
    ///     deux boutons (voir DeuxChoix).
    ///
    /// Lire et Ecrire sont des « fonctions rangées dans une variable » (délégués). Par exemple
    ///     p => p.Lissage                 lit le lissage d'un objet Parametres p ;
    ///     (p, v) => p.Lissage = v        écrit la valeur v dans le lissage de p.
    /// Ainsi, une seule classe sait afficher et modifier n'importe quel réglage.
    ///
    /// Les limites (minimum, maximum) ne sont PAS répétées ici : après chaque changement,
    /// Parametres.Corriger() ramène la valeur dans ses limites. Elles restent écrites à un
    /// seul endroit.
    /// </summary>
    internal sealed class Reglage
    {
        /// <summary>
        /// Les nombres sont affichés avec un POINT (0.16), comme dans parametres.json
        /// (demande de l'utilisateur du 26/10/2026). CultureInfo.InvariantCulture est la
        /// présentation « neutre » de .NET, qui utilise le point.
        /// </summary>
        private static readonly CultureInfo Format = CultureInfo.InvariantCulture;

        private Reglage()
        {
        }

        /// <summary>Le nom affiché (par exemple « Délai de fixation »).</summary>
        public string Nom { get; private set; }

        /// <summary>Une phrase courte, affichée sous le nom.</summary>
        public string Explication { get; private set; }

        /// <summary>L'unité, avec son espace devant (par exemple " s", " px") ou "" pour aucune.</summary>
        public string Unite { get; private set; }

        /// <summary>De combien « − » et « + » changent la valeur.</summary>
        public double Pas { get; private set; }

        /// <summary>Nombre de chiffres après le point, déduit du pas (0.05 → 2 chiffres).</summary>
        public int Decimales { get; private set; }

        /// <summary>Vrai pour un réglage oui / non (ou un choix entre deux mots).</summary>
        public bool OuiNon { get; private set; }

        /// <summary>Le mot du bouton de gauche (valeur « faux ») : « Non » d'habitude.</summary>
        public string MotFaux { get; private set; }

        /// <summary>Le mot du bouton de droite (valeur « vrai ») : « Oui » d'habitude.</summary>
        public string MotVrai { get; private set; }

        private Func<Parametres, double> _lireNombre;
        private Action<Parametres, double> _ecrireNombre;
        private Func<Parametres, bool> _lireOuiNon;
        private Action<Parametres, bool> _ecrireOuiNon;

        /// <summary>Appelée après chaque changement (null = rien de spécial à faire).</summary>
        public Action<Parametres> ApresChangement { get; private set; }

        /// <summary>Crée un réglage numérique.</summary>
        public static Reglage Nombre(string nom, string explication, string unite, double pas,
                                     Func<Parametres, double> lire, Action<Parametres, double> ecrire)
        {
            Reglage r = new Reglage();
            r.Nom = nom;
            r.Explication = explication;
            r.Unite = unite;
            r.Pas = pas;
            r.Decimales = CompterDecimales(pas);
            r._lireNombre = lire;
            r._ecrireNombre = ecrire;
            return r;
        }

        /// <summary>Crée un réglage oui / non. apres : facultatif, appelé après chaque changement.</summary>
        public static Reglage OuiOuNon(string nom, string explication,
                                       Func<Parametres, bool> lire, Action<Parametres, bool> ecrire,
                                       Action<Parametres> apres = null)
        {
            return DeuxChoix(nom, explication, "Non", "Oui", lire, ecrire, apres);
        }

        /// <summary>
        /// Crée un choix entre deux mots (étape 9). Le réglage est rangé comme un oui / non :
        /// « faux » = motFaux (bouton de gauche), « vrai » = motVrai (bouton de droite).
        /// Exemple : DeuxChoix("Souris", "…", "Directe", "Relative", p => p.SourisRelative, …).
        /// </summary>
        public static Reglage DeuxChoix(string nom, string explication, string motFaux, string motVrai,
                                        Func<Parametres, bool> lire, Action<Parametres, bool> ecrire,
                                        Action<Parametres> apres = null)
        {
            Reglage r = new Reglage();
            r.Nom = nom;
            r.Explication = explication;
            r.Unite = "";
            r.OuiNon = true;
            r.MotFaux = motFaux;
            r.MotVrai = motVrai;
            r._lireOuiNon = lire;
            r._ecrireOuiNon = ecrire;
            r.ApresChangement = apres;
            return r;
        }

        public double LireNombre(Parametres p)
        {
            return _lireNombre(p);
        }

        public bool LireOuiNon(Parametres p)
        {
            return _lireOuiNon(p);
        }

        /// <summary>
        /// Écrit une nouvelle valeur, la ramène dans ses limites (Corriger) et l'arrondit au
        /// nombre de décimales affiché (pour éviter 0.30000000000000004 après plusieurs « + »).
        /// </summary>
        public void EcrireNombre(Parametres p, double valeur)
        {
            _ecrireNombre(p, Math.Round(valeur, Decimales));
            p.Corriger();
        }

        public void EcrireOuiNon(Parametres p, bool valeur)
        {
            _ecrireOuiNon(p, valeur);
        }

        /// <summary>« − » (sens = -1) ou « + » (sens = +1) : un pas de plus ou de moins.</summary>
        public void Changer(Parametres p, int sens)
        {
            EcrireNombre(p, LireNombre(p) + sens * Pas);
        }

        /// <summary>Copie la valeur de « source » dans « cible » (pour le bouton « Défaut » d'un onglet).</summary>
        public void Copier(Parametres source, Parametres cible)
        {
            if (OuiNon)
            {
                EcrireOuiNon(cible, LireOuiNon(source));
            }
            else
            {
                EcrireNombre(cible, LireNombre(source));
            }
        }

        /// <summary>La valeur prête à afficher, avec son unité : « 0.16 », « 0.8 s », « oui », « directe ».</summary>
        public string Texte(Parametres p)
        {
            if (OuiNon)
            {
                return (LireOuiNon(p) ? MotVrai : MotFaux).ToLowerInvariant();
            }
            return TexteNombre(LireNombre(p)) + Unite;
        }

        /// <summary>Le nombre seul, avec un point et le bon nombre de décimales (pour la saisie).</summary>
        public string TexteNombre(double valeur)
        {
            return valeur.ToString("F" + Decimales, Format);
        }

        /// <summary>
        /// Lit un nombre tapé par l'aidant. Accepte le point ET la virgule (0.16 ou 0,16),
        /// et ignore l'unité si elle a été tapée (« 0.8 s »). Renvoie faux si ce n'est pas un nombre.
        /// </summary>
        public static bool LireSaisie(string texte, out double valeur)
        {
            string propre = (texte ?? "").Trim().Replace(',', '.');
            // On ne garde que le début : chiffres, signe moins et point.
            int fin = 0;
            while (fin < propre.Length && ("0123456789.-".IndexOf(propre[fin]) >= 0))
            {
                fin++;
            }
            return double.TryParse(propre.Substring(0, fin), NumberStyles.Float, Format, out valeur);
        }

        private static int CompterDecimales(double pas)
        {
            string texte = pas.ToString("0.########", Format);
            int point = texte.IndexOf('.');
            return point < 0 ? 0 : texte.Length - point - 1;
        }
    }

    /// <summary>Un onglet du mode paramètres : son nom, son symbole et ses réglages (cinq au plus).</summary>
    internal sealed class Onglet
    {
        public Onglet(string nom, string symbole, params Reglage[] reglages)
        {
            Nom = nom;
            Symbole = symbole;
            Reglages = new List<Reglage>(reglages);
        }

        public string Nom { get; private set; }

        /// <summary>Symbole de la police d'icônes de Windows (Segoe Fluent Icons / Segoe MDL2 Assets).</summary>
        public string Symbole { get; private set; }

        public List<Reglage> Reglages { get; private set; }
    }

    /// <summary>
    /// LA LISTE DES ONGLETS ET DES RÉGLAGES du mode paramètres (étape 7), dans l'ordre
    /// d'affichage. Regroupement validé par l'utilisateur le 26/10/2026 (six onglets ; « Défilement » ajouté à l'étape 8,
    /// « Jeu » à l'étape 9).
    ///
    /// Pour ajouter un réglage plus tard : l'ajouter dans Parametres.cs (avec sa valeur par
    /// défaut et ses limites dans Corriger), puis ici, dans l'onglet voulu (cinq au plus par
    /// onglet, pour que rien ne défile). L'onglet « Défilement » est ajouté à l'étape 8,
    /// l'onglet « Jeu » à l'étape 9.
    /// </summary>
    internal static class CatalogueParametres
    {
        public static readonly List<Onglet> Onglets = new List<Onglet>
        {
            // E7B3 = œil (« Regard »)
            new Onglet("Regard", "",
                Reglage.Nombre("Lissage", "Plus petit = plus calme, plus grand = plus réactif", "", 0.01,
                               p => p.Lissage, (p, v) => p.Lissage = v),
                Reglage.Nombre("Anti-tremblement", "Le curseur ne bouge pas en dessous", " px", 1,
                               p => p.ZoneMorteCurseur, (p, v) => p.ZoneMorteCurseur = v),
                Reglage.Nombre("Regard perdu après", "Sans nouvelle mesure depuis ce temps", " ms", 50,
                               p => p.AgeMaxMesure, (p, v) => p.AgeMaxMesure = v),
                Reglage.OuiOuNon("Suivi souris", "Le curseur suit le regard en mode mouvement",
                                 p => p.SuiviSouris, (p, v) => p.SuiviSouris = v)),

            // E707 = épingle (« Fixation » : un point fixé)
            new Onglet("Fixation", "",
                Reglage.Nombre("Rayon de fixation", "Le regard est immobile dans ce rayon", " px", 5,
                               p => p.RayonFixation, (p, v) => p.RayonFixation = v),
                Reglage.Nombre("Délai de fixation", "Avant de passer en regard fixe", " s", 0.1,
                               p => p.DelaiFixation, (p, v) => p.DelaiFixation = v),
                Reglage.Nombre("Vitesse max", "Au-delà, l'œil saute ailleurs", " px/s", 50,
                               p => p.VitesseMaxFixation, (p, v) => p.VitesseMaxFixation = v),
                Reglage.OuiOuNon("Clic direct", "Clic gauche si la fixation se prolonge",
                                 p => p.ClicDirect, (p, v) => p.ClicDirect = v),
                Reglage.Nombre("Délai du clic direct", "Après l'entrée en regard fixe", " s", 0.1,
                               p => p.DelaiClicDirect, (p, v) => p.DelaiClicDirect = v)),

            // E916 = chronomètre (« Délais »)
            new Onglet("Délais", "",
                Reglage.Nombre("Activation des boutons", "Regard sur une icône avant activation", " s", 0.1,
                               p => p.DelaiActivation, (p, v) => p.DelaiActivation = v),
                Reglage.Nombre("Icône d'options", "Regard fixe : icône vers le choix d'action", " s", 0.1,
                               p => p.DelaiIconeOptions, (p, v) => p.DelaiIconeOptions = v),
                Reglage.Nombre("Ouverture du menu", "Regard en bas, hors de l'écran", " s", 0.1,
                               p => p.DelaiMenu, (p, v) => p.DelaiMenu = v),
                Reglage.Nombre("Fermeture du menu", "Regard hors de la barre", " s", 0.1,
                               p => p.DelaiFermetureMenu, (p, v) => p.DelaiFermetureMenu = v)),

            // E7F4 = écran (« Affichage »)
            new Onglet("Affichage", "",
                Reglage.Nombre("Rayon du cercle", "Mode mouvement et zone de sortie", " px", 10,
                               p => p.RayonCercle, (p, v) => p.RayonCercle = v),
                Reglage.Nombre("Taille des icônes", "Côté des boutons au regard", " px", 10,
                               p => p.TailleIcones, (p, v) => p.TailleIcones = v),
                Reglage.OuiOuNon("Libellés", "Nom de l'action au survol d'une icône",
                                 p => p.AfficherLibelles, (p, v) => p.AfficherLibelles = v)),

            // E71E = loupe (« Loupe, glisser »)
            new Onglet("Loupe, glisser", "",
                Reglage.OuiOuNon("Loupe avant l'action", "Passer par la loupe avant le choix",
                                 p => p.LoupeAvantAction, (p, v) => p.LoupeAvantAction = v),
                Reglage.Nombre("Zoom de la loupe", "Agrandissement de la capture", " ×", 0.5,
                               p => p.ZoomLoupe, (p, v) => p.ZoomLoupe = v),
                Reglage.Nombre("Pause android", "Entre l'appui et le déplacement", " s", 0.1,
                               p => p.DureePauseAndroid, (p, v) => p.DureePauseAndroid = v),
                Reglage.Nombre("Durée du glisser", "Trajet du curseur, bouton enfoncé", " s", 0.05,
                               p => p.DureeGlisser, (p, v) => p.DureeGlisser = v)),

            // EC8F = flèches haut et bas (« Défilement », étape 8)
            new Onglet("Défilement", "\uEC8F",
                Reglage.Nombre("Vitesse maximale", "Atteinte trois rayons de cercle au-delà du bord", " crans/s", 1,
                               p => p.VitesseMaxDefilement, (p, v) => p.VitesseMaxDefilement = v)),

            // E7FC = manette (« Jeu », étape 9)
            new Onglet("Jeu", "\uE7FC",
                Reglage.DeuxChoix("Souris du jeu", "Directe pour Mindball, relative pour d'autres jeux",
                                  "Directe", "Relative",
                                  p => p.SourisRelative, (p, v) => p.SourisRelative = v),
                Reglage.DeuxChoix("Souris relative", "Suivi : comme le regard. Manette : écart au centre",
                                  "Suivi", "Manette",
                                  p => p.RelativeManette, (p, v) => p.RelativeManette = v),
                Reglage.Nombre("Lissage du jeu", "Plus petit = plus calme, plus grand = plus réactif", "", 0.01,
                               p => p.LissageJeu, (p, v) => p.LissageJeu = v),
                Reglage.Nombre("Sensibilité", "Souris relative : multiplie les déplacements", " ×", 0.1,
                               p => p.SensibiliteJeu, (p, v) => p.SensibiliteJeu = v)),

            // E713 = engrenage (« Général »)
            new Onglet("Général", "",
                Reglage.OuiOuNon("Démarrer avec Windows", "Lancer GazeBar à l'ouverture de session",
                                 p => p.DemarrerAvecWindows, (p, v) => p.DemarrerAvecWindows = v,
                                 p => DemarrageWindows.Appliquer(p.DemarrerAvecWindows)))
        };
    }
}
