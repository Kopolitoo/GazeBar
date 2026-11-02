using System;
using System.Windows;

namespace GazeBar
{
    /// <summary>
    /// MODE PARAMÈTRES (étape 7 du cahier des charges).
    ///
    /// Entrée : bouton « Paramètres » de la barre de menu.
    ///
    /// Affichage : la fenêtre FenetreParametres, plein écran, fond noir (voir ce fichier pour
    /// la mise en page). Le calque est vidé : rien n'y est dessiné pendant ce mode.
    ///
    /// Le regard : ce mode fait le lien entre le regard et la fenêtre.
    ///   - à chaque tic, la position du regard (pixels d'écran) est convertie en position sur
    ///     la page 1920 × 1080 de la fenêtre, puis donnée à un ViseurBoutons, le même que
    ///     pour le menu et le choix d'action ;
    ///   - une cible regardée pendant [DelaiActivation] s'active ;
    ///   - RÉPÉTITION (choix de l'utilisateur du 26/10/2026) : si le regard reste sur « − » ou
    ///     « + », la valeur continue de changer, d'un pas toutes les [DelaiRepetition]
    ///     secondes. Les autres cibles (onglets, Non / Oui…) ne s'activent qu'une fois : il
    ///     faut quitter la cible du regard pour l'activer de nouveau ;
    ///   - un petit cercle gris (50 px) montre où est le regard.
    ///
    /// Étape 10 : le bouton « Configurer les icônes » de l'onglet « Icônes » ouvre l'écran de
    /// configuration des icônes (ModeConfigIcones), qui revient ici, sur cet onglet.
    ///
    /// Étape 14 : le bouton « Mes mots » de l'onglet « Général » ouvre la liste des mots ajoutés
    /// au clavier (ModeListeClavier), qui revient ici, sur cet onglet.
    ///
    /// Onglet « Outils » (29/10/2026, boutons déplacés de la barre de menu) :
    ///   - « Calibration » lance l'outil Tobii puis passe en mode calibration (qui revient au
    ///     mode mouvement quand l'outil se ferme). Si le lancement échoue, on reste ici et le
    ///     message s'affiche dans l'onglet ;
    ///   - « Diagnostic » revient au mode mouvement et ouvre la fenêtre Diagnostic.
    ///
    /// Étape 15 (profils) : onglet « Profils ». « Charger un profil » ouvre la liste des
    /// profils (ModeListeClavier, sorte Profils), qui revient ici avec le profil choisi : la
    /// fenêtre demande alors confirmation. « Oui » : ChargerProfil (ci-dessous) applique le
    /// profil, puis le clavier relit sa disposition (la langue a pu changer).
    ///
    /// Sortie : bouton « Retour » (au milieu du bord droit) ou touche Échap de l'aidant,
    /// vers le mode mouvement. Le menu du bas ne s'ouvre pas ici (Mode.MenuAutorise reste
    /// « non », règle du 25/10/2026).
    ///
    /// Chaque changement est appliqué tout de suite (Parametres.Courants) et enregistré dans
    /// parametres.json.
    ///
    /// Code non compilé au moment de l'écriture : à vérifier dans Visual Studio.
    /// </summary>
    internal sealed class ModeParametres : Mode
    {
        /// <summary>Intervalle de la répétition sur « − » et « + », en secondes.</summary>
        private const double DelaiRepetition = 0.4;

        // L'onglet affiché à l'ouverture (0 = le premier).
        private readonly int _ongletDepart;

        // Étape 15 : le profil choisi dans la liste, à confirmer à l'ouverture (ou null).
        private readonly ProfilTrouve _profilAConfirmer;

        private FenetreParametres _fenetre;
        private ViseurBoutons _viseur;

        // Version de la page vue au tic précédent : si elle change, les cibles ont changé.
        private int _version = -1;

        // La cible en cours de répétition (« − » ou « + »), ou -1.
        private int _cibleRepetee = -1;

        // Le petit cercle du regard : position lissée, en pixels d'écran.
        private readonly Lisseur _lisse = new Lisseur();
        private bool _cerclePlace;

        /// <summary>
        /// Ouvre les paramètres sur l'onglet numéro « onglet » (le premier si on ne le donne
        /// pas). Étape 15 : « profilAConfirmer » = profil choisi dans la liste des profils ;
        /// la page s'ouvre alors sur la question « Charger le profil … ? ».
        /// </summary>
        public ModeParametres(int onglet = 0, ProfilTrouve profilAConfirmer = null)
        {
            _ongletDepart = onglet;
            _profilAConfirmer = profilAConfirmer;
        }

        public override string Nom
        {
            get { return "Paramètres"; }
        }

        public override void Entrer(EtatRegard regard)
        {
            // Rien sur le calque pendant ce mode : tout est dans la fenêtre.
            Gestionnaire.Calque.MasquerTout();

            // La fenêtre est rangée dans _fenetre SEULEMENT quand tout est prêt (viseur
            // compris) : Actualiser ne fait rien tant que _fenetre est vide (bogue du
            // 02/11/2026 : un tic arrivé pendant Show trouvait un viseur vide).
            FenetreParametres fenetre = new FenetreParametres(_ongletDepart, _profilAConfirmer);
            fenetre.RetourDemande += Retour;
            fenetre.ConfigurationIconesDemandee += OuvrirConfigurationIcones;
            fenetre.CalibrationDemandee += LancerCalibration;
            fenetre.DiagnosticDemande += OuvrirDiagnostic;
            fenetre.MesMotsDemandes += OuvrirMesMots;
            fenetre.ListeProfilsDemandee += OuvrirListeProfils;
            fenetre.ChargementProfilDemande += ChargerProfil;
            _viseur = new ViseurBoutons(fenetre.Cibles);
            _version = -1;
            _cibleRepetee = -1;

            fenetre.Show();
            // Activate : la fenêtre prend le clavier, pour que l'aidant puisse taper une valeur.
            // (Windows peut le refuser si un autre programme est au premier plan ; un clic de
            // l'aidant dans la fenêtre suffit alors.)
            fenetre.Activate();
            _fenetre = fenetre;

            _cerclePlace = regard.SurEcran;
            if (_cerclePlace)
            {
                _lisse.Placer(regard.X, regard.Y);
            }
        }

        public override void Actualiser(EtatRegard regard)
        {
            if (_fenetre == null || _viseur == null)
            {
                return;
            }
            Parametres p = Parametres.Courants;

            // 1. La page a-t-elle été reconstruite (nouvel onglet, confirmation…) ? Le viseur
            //    repart de zéro, et la cible qui se trouve sous le regard ne s'active pas tant
            //    que le regard ne l'a pas quittée (comme dans le menu).
            if (_fenetre.Version != _version)
            {
                _version = _fenetre.Version;
                _viseur.ReinitialiserEtBloquer();
                _cibleRepetee = -1;
            }

            // 2. Le regard, converti en coordonnées de la page. EtatRegard est une « struct » :
            //    surPage est une copie, on peut changer X et Y sans toucher à l'original.
            EtatRegard surPage = regard;
            if (regard.Disponible)
            {
                Point point = _fenetre.VersPage(regard.X, regard.Y);
                surPage.X = point.X;
                surPage.Y = point.Y;
            }

            // 3. Les cibles. Pendant une répétition, le délai est plus court.
            double delai = (_cibleRepetee >= 0 && _viseur.Vise == _cibleRepetee) ? DelaiRepetition : p.DelaiActivation;
            int active = _viseur.Actualiser(surPage, delai, false);
            if (_viseur.Vise != _cibleRepetee)
            {
                _cibleRepetee = -1;   // le regard a quitté « − » ou « + » : fin de la répétition
            }

            if (active >= 0)
            {
                if (_fenetre.CibleRepetable(active))
                {
                    _cibleRepetee = active;
                }
                else
                {
                    _viseur.BloquerJusquASortie();
                }

                _fenetre.Activer(active);
                if (Gestionnaire.ModeCourant != this)
                {
                    return;   // « Retour » : on a déjà changé de mode
                }
            }

            // 4. Le petit cercle du regard. Regard hors de l'écran : il reste au bord et glisse
            //    le long de ce bord (EtatRegard.XEcran). Regard perdu : il reste où il est.
            if (regard.Disponible)
            {
                if (!_cerclePlace)
                {
                    _lisse.Placer(regard.XEcran, regard.YEcran);
                    _cerclePlace = true;
                }
                _lisse.Suivre(regard.XEcran, regard.YEcran, regard.MaintenantMs);
                _fenetre.PlacerPointRegard(_fenetre.VersPage(_lisse.X, _lisse.Y));
            }
        }

        public override void Quitter()
        {
            if (_fenetre != null)
            {
                FenetreParametres fenetre = _fenetre;
                _fenetre = null;
                fenetre.RetourDemande -= Retour;
                fenetre.ConfigurationIconesDemandee -= OuvrirConfigurationIcones;
                fenetre.CalibrationDemandee -= LancerCalibration;
                fenetre.DiagnosticDemande -= OuvrirDiagnostic;
                fenetre.MesMotsDemandes -= OuvrirMesMots;
                fenetre.ListeProfilsDemandee -= OuvrirListeProfils;
                fenetre.ChargementProfilDemande -= ChargerProfil;
                fenetre.Fermer();
            }
        }

        /// <summary>
        /// L'écran a changé (étape 11) : on reste dans les paramètres. La fenêtre reprend la
        /// taille de l'écran et la page est redessinée (l'onglet « Affichage » montre l'écran
        /// détecté). La page de 1920 × 1080 unités s'adapte toute seule (Viewbox).
        /// </summary>
        public override bool AdapterEcran()
        {
            if (_fenetre != null)
            {
                _fenetre.AdapterEcran();
            }
            return true;
        }

        /// <summary>
        /// Touche Échap de l'aidant (appelée par App) : si une valeur est en cours de saisie,
        /// la saisie est annulée et on reste ici. Renvoie vrai dans ce cas.
        /// </summary>
        public bool AnnulerSaisie()
        {
            return _fenetre != null && _fenetre.AnnulerSaisie();
        }

        /// <summary>Bouton « Configurer les icônes » : ouvre l'écran de configuration des icônes.</summary>
        private void OuvrirConfigurationIcones()
        {
            App.Log("Paramètres : configuration des icônes");
            Gestionnaire.ChangerMode(new ModeConfigIcones());
        }

        /// <summary>
        /// Bouton « Mes mots » de l'onglet « Général » (étape 14) : la liste des mots ajoutés
        /// au clavier, qui revient ici, sur cet onglet.
        /// </summary>
        private void OuvrirMesMots()
        {
            App.Log("Paramètres : mes mots");
            Gestionnaire.ChangerMode(new ModeListeClavier(ModeListeClavier.Sorte.MesMots));
        }

        /// <summary>
        /// Bouton « Charger un profil » de l'onglet « Profils » (étape 15) : la liste des
        /// profils en plein écran, qui revient ici (onglet Profils) avec ou sans choix.
        /// </summary>
        private void OuvrirListeProfils()
        {
            App.Log("Paramètres : liste des profils");
            Gestionnaire.ChangerMode(new ModeListeClavier(ModeListeClavier.Sorte.Profils));
        }

        /// <summary>
        /// « Oui » à « Charger le profil … ? » (étape 15) : le profil devient les réglages en
        /// cours (ProfilsParametres.Charger). Le clavier, s'il est ouvert, relit sa
        /// disposition, car le profil peut avoir une autre langue. On reste dans les
        /// paramètres : la page est reconstruite avec les nouvelles valeurs et le message.
        /// </summary>
        private void ChargerProfil(ProfilTrouve profil)
        {
            string message;
            if (ProfilsParametres.Charger(profil.Chemin, out message))
            {
                Gestionnaire.Clavier.ReglagesRemplaces();
            }
            if (_fenetre != null)
            {
                _fenetre.AfficherMessage(message);
            }
        }

        /// <summary>
        /// Bouton « Calibration » de l'onglet « Outils ».
        ///
        /// Difficulté : la fenêtre des paramètres est plein écran et « toujours devant »
        /// (Topmost). Si on lançait l'outil Tobii sans rien faire, sa fenêtre serait cachée
        /// derrière. On CACHE donc d'abord la fenêtre des paramètres (Hide : elle existe
        /// toujours, elle n'est simplement plus affichée).
        ///
        /// Ensuite, comme depuis la barre de menu, on note la fenêtre active AVANT de lancer
        /// l'outil : le mode calibration saura reconnaître la fenêtre de l'outil, qui sera la
        /// suivante à devenir active (voir ModeCalibration). En cachant la nôtre, Windows a
        /// rendu active une autre fenêtre (l'application d'avant) : c'est elle qu'on note.
        ///
        /// Si le lancement échoue, on réaffiche la fenêtre et on montre le message.
        /// </summary>
        private void LancerCalibration()
        {
            if (_fenetre == null)
            {
                return;
            }
            App.Log("Paramètres : calibration");
            _fenetre.Hide();
            IntPtr fenetreAvant = NativeMethods.GetForegroundWindow();

            string message;
            if (CalibrationTobii.Lancer(Gestionnaire.Regard, out message))
            {
                // Quitter() fermera la fenêtre des paramètres (déjà cachée).
                Gestionnaire.ChangerMode(new ModeCalibration(fenetreAvant));
            }
            else
            {
                _fenetre.Show();
                _fenetre.Activate();
                _fenetre.AfficherMessage(message);
            }
        }

        /// <summary>
        /// Bouton « Diagnostic » de l'onglet « Outils » : retour au mode mouvement (la fenêtre
        /// des paramètres, toujours devant, cacherait la fenêtre Diagnostic), puis ouverture de
        /// la fenêtre Diagnostic.
        /// </summary>
        private void OuvrirDiagnostic()
        {
            App.Log("Paramètres : diagnostic");
            Gestionnaire.ChangerMode(new ModeMouvement());
            ((App)Application.Current).OuvrirDiagnostic();
        }

        /// <summary>Bouton « Retour » : retour au mode mouvement.</summary>
        private void Retour()
        {
            App.Log("Paramètres : retour au mode mouvement");
            Gestionnaire.ChangerMode(new ModeMouvement());
        }
    }
}
