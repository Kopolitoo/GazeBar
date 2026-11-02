using System;
using System.Collections.Generic;
using System.IO;

namespace GazeBar
{
    /// <summary>
    /// Un profil trouvé dans le dossier des profils : son nom affiché (le nom du fichier sans
    /// « .json »), son chemin complet et la date de sa dernière modification.
    /// </summary>
    internal sealed class ProfilTrouve
    {
        public string Nom;
        public string Chemin;
        public DateTime Date;

        /// <summary>Vrai pour le profil « Sauvegarde automatique » (fait avant chaque chargement).</summary>
        public bool EstSauvegarde
        {
            get { return string.Equals(Nom, ProfilsParametres.NomSauvegarde, StringComparison.OrdinalIgnoreCase); }
        }
    }

    /// <summary>
    /// PROFILS DE PARAMÈTRES (étape 15 du cahier des charges, « profils-parametres »).
    ///
    /// Un PROFIL est simplement une copie du fichier parametres.json, rangée dans le dossier
    ///     Documents\GazeBar\Profils
    /// (par exemple C:\Users\Nom\Documents\GazeBar\Profils\Réglages 2026-11-02 16h45.json).
    /// On peut ainsi garder plusieurs jeux de réglages (« Soir », « Fatigue »…) et passer de
    /// l'un à l'autre au regard, depuis l'onglet « Profils » des paramètres.
    ///
    /// Ce que fait cette classe (conception validée par l'utilisateur le 02/11/2026) :
    ///   - Lister : les fichiers .json du dossier, le plus récent en premier ;
    ///   - EnregistrerActuels : écrit les réglages en cours sous un nom daté automatiquement.
    ///     Pour lui donner un vrai nom, l'aidant renomme le fichier dans l'Explorateur ;
    ///   - Charger : lit un profil (un fichier d'une ancienne version est converti, comme
    ///     parametres.json au démarrage), garde d'abord les réglages actuels dans le profil
    ///     « Sauvegarde automatique » (remplacé à chaque chargement : on peut ainsi revenir en
    ///     arrière au regard), puis applique et enregistre le profil.
    ///
    /// Le réglage « Démarrer avec Windows » n'est PAS pris dans le profil : il concerne
    /// l'ordinateur plutôt que la personne. Le dictionnaire personnel (« Mes mots ») est un
    /// autre fichier : il ne change pas non plus.
    ///
    /// Pas de suppression ni de renommage au regard : l'aidant le fait dans l'Explorateur.
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal static class ProfilsParametres
    {
        /// <summary>Le nom du profil écrit automatiquement avant chaque chargement.</summary>
        public const string NomSauvegarde = "Sauvegarde automatique";

        /// <summary>Le dossier des profils : Documents\GazeBar\Profils.</summary>
        public static string Dossier
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents, "GazeBar", "Profils");
            }
        }

        /// <summary>
        /// Les profils du dossier, le plus récemment modifié en premier (le dernier enregistré
        /// est donc toujours sur la première page). Liste vide si le dossier n'existe pas
        /// encore ou ne peut pas être lu.
        /// </summary>
        public static List<ProfilTrouve> Lister()
        {
            List<ProfilTrouve> profils = new List<ProfilTrouve>();
            try
            {
                if (!Directory.Exists(Dossier))
                {
                    return profils;
                }
                foreach (string chemin in Directory.GetFiles(Dossier, "*.json"))
                {
                    ProfilTrouve profil = new ProfilTrouve();
                    profil.Chemin = chemin;
                    profil.Nom = Path.GetFileNameWithoutExtension(chemin);
                    profil.Date = File.GetLastWriteTime(chemin);
                    profils.Add(profil);
                }
            }
            catch (Exception ex)
            {
                App.Log("Profils : dossier illisible : " + ex.Message);
            }
            // Tri : la date la plus récente d'abord (b avant a).
            profils.Sort((a, b) => b.Date.CompareTo(a.Date));
            return profils;
        }

        /// <summary>
        /// Bouton « Enregistrer les réglages actuels » : écrit Parametres.Courants dans un
        /// nouveau profil nommé avec la date et l'heure, par exemple « Réglages 2026-11-02
        /// 16h45 ». Si ce nom existe déjà (deux enregistrements dans la même minute), on ajoute
        /// « (2) », « (3) »…
        /// Renvoie vrai si c'est réussi ; « message » est la phrase à afficher dans l'onglet.
        /// </summary>
        public static bool EnregistrerActuels(out string message)
        {
            string nom = "Réglages " + DateTime.Now.ToString("yyyy-MM-dd HH'h'mm");
            string chemin = Path.Combine(Dossier, nom + ".json");
            for (int numero = 2; File.Exists(chemin); numero++)
            {
                chemin = Path.Combine(Dossier, nom + " (" + numero + ").json");
            }
            nom = Path.GetFileNameWithoutExtension(chemin);

            if (Parametres.Courants.EnregistrerDans(chemin))
            {
                App.Log("Profils : réglages actuels enregistrés dans « " + nom + " »");
                message = "Réglages enregistrés dans le profil « " + nom + " ».";
                return true;
            }
            message = "Impossible d'enregistrer le profil (voir le journal).";
            return false;
        }

        /// <summary>
        /// Charge le profil « chemin » après confirmation (voir FenetreParametres).
        ///   1. Le profil est LU d'abord (et converti s'il vient d'une ancienne version). S'il
        ///      est illisible, rien ne change et on le dit.
        ///   2. Les réglages actuels sont gardés dans « Sauvegarde automatique ». On lit le
        ///      profil AVANT d'écrire cette sauvegarde : ainsi, recharger la sauvegarde
        ///      elle-même fonctionne (on échange simplement les réglages d'avant et d'après).
        ///   3. « Démarrer avec Windows » garde sa valeur actuelle (réglage de l'ordinateur).
        ///   4. Le profil devient les réglages en cours, il est enregistré dans parametres.json
        ///      et le réglage Windows « défiler les fenêtres inactives » est réappliqué.
        /// Le clavier, s'il est ouvert, relit sa disposition : c'est ModeParametres qui s'en
        /// charge (il a accès au gestionnaire des modes).
        /// Renvoie vrai si le profil est chargé ; « message » est la phrase à afficher.
        /// </summary>
        public static bool Charger(string chemin, out string message)
        {
            string nom = Path.GetFileNameWithoutExtension(chemin);

            // 1. Lire le profil.
            Parametres nouveaux;
            bool misAJour;
            try
            {
                nouveaux = Parametres.LireFichier(chemin, out misAJour);
            }
            catch (Exception ex)
            {
                App.Log("Profils : « " + nom + " » illisible : " + ex.Message);
                message = "Le profil « " + nom + " » est illisible : rien n'a changé.";
                return false;
            }
            if (misAJour)
            {
                App.Log("Profils : « " + nom + " » venait d'une ancienne version, converti");
            }
            else if (nouveaux.VersionFichier > Parametres.VersionActuelle)
            {
                // Profil écrit par une version plus récente de GazeBar : les réglages inconnus
                // de cette version-ci sont simplement ignorés.
                App.Log("Profils : « " + nom + " » vient d'une version plus récente (" + nouveaux.VersionFichier + ")");
            }

            // 2. Garder les réglages actuels.
            Parametres actuels = Parametres.Courants;
            string cheminSauvegarde = Path.Combine(Dossier, NomSauvegarde + ".json");
            if (!actuels.EnregistrerDans(cheminSauvegarde))
            {
                // Sans sauvegarde, on ne pourrait pas revenir en arrière : on n'insiste pas.
                message = "Impossible de garder les réglages actuels : le profil n'a pas été chargé.";
                return false;
            }

            // 3. Le réglage de l'ordinateur ne change pas.
            nouveaux.DemarrerAvecWindows = actuels.DemarrerAvecWindows;

            // 4. Appliquer et enregistrer.
            Parametres.Remplacer(nouveaux);
            nouveaux.Enregistrer();
            MoletteWindows.Appliquer(nouveaux.DefilerFenetresInactives);

            App.Log("Profils : profil « " + nom + " » chargé");
            message = "Profil « " + nom + " » chargé. Les réglages d'avant sont dans « " + NomSauvegarde + " ».";
            return true;
        }
    }
}
