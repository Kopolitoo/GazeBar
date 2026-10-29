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

        private FenetreParametres _fenetre;
        private ViseurBoutons _viseur;

        // Version de la page vue au tic précédent : si elle change, les cibles ont changé.
        private int _version = -1;

        // La cible en cours de répétition (« − » ou « + »), ou -1.
        private int _cibleRepetee = -1;

        // Le petit cercle du regard : position lissée, en pixels d'écran.
        private readonly Lisseur _lisse = new Lisseur();
        private bool _cerclePlace;

        /// <summary>Ouvre les paramètres sur l'onglet numéro « onglet » (le premier si on ne le donne pas).</summary>
        public ModeParametres(int onglet = 0)
        {
            _ongletDepart = onglet;
        }

        public override string Nom
        {
            get { return "Paramètres"; }
        }

        public override void Entrer(EtatRegard regard)
        {
            // Rien sur le calque pendant ce mode : tout est dans la fenêtre.
            Gestionnaire.Calque.MasquerTout();

            _fenetre = new FenetreParametres(_ongletDepart);
            _fenetre.RetourDemande += Retour;
            _fenetre.ConfigurationIconesDemandee += OuvrirConfigurationIcones;
            _fenetre.Show();
            // Activate : la fenêtre prend le clavier, pour que l'aidant puisse taper une valeur.
            // (Windows peut le refuser si un autre programme est au premier plan ; un clic de
            // l'aidant dans la fenêtre suffit alors.)
            _fenetre.Activate();

            _viseur = new ViseurBoutons(_fenetre.Cibles);
            _version = -1;
            _cibleRepetee = -1;

            _cerclePlace = regard.SurEcran;
            if (_cerclePlace)
            {
                _lisse.Placer(regard.X, regard.Y);
            }
        }

        public override void Actualiser(EtatRegard regard)
        {
            if (_fenetre == null)
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

        /// <summary>Bouton « Retour » : retour au mode mouvement.</summary>
        private void Retour()
        {
            App.Log("Paramètres : retour au mode mouvement");
            Gestionnaire.ChangerMode(new ModeMouvement());
        }
    }
}
