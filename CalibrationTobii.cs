using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : lance l'outil de calibration du moteur Tobii (étape 4, bouton « Calibration »
    /// de la barre de menu).
    ///
    /// Le problème : le paquet Tobii.Interaction 0.7.3 n'a presque pas de documentation, et
    /// ce code n'a pas pu être compilé ni essayé sur le PCEye 5. D'après les exemples connus,
    /// la commande s'appelle « LaunchConfigurationTool » et prend un « outil » (une valeur
    /// d'une énumération, par exemple « Recalibrate »), mais ce n'est pas certain.
    ///
    /// La solution : la « réflexion ». C'est un mécanisme de .NET qui permet à un programme
    /// de regarder, PENDANT qu'il tourne, quelles méthodes un objet possède, et de les appeler
    /// par leur nom (une simple chaîne de caractères). Avantages ici :
    ///  - si le nom est faux, le programme compile quand même : la calibration échoue
    ///    simplement, avec un message dans le journal, au lieu d'empêcher toute la compilation ;
    ///  - le journal liste les méthodes et les outils réellement proposés par Tobii : il
    ///    suffira de le lire pour corriger le nom si besoin, puis d'écrire l'appel directement.
    /// Une fois le bon appel trouvé, on pourra remplacer tout ce fichier par une ligne normale.
    /// </summary>
    internal static class CalibrationTobii
    {
        /// <summary>Les noms d'outil essayés, dans l'ordre (valeurs de l'énumération de Tobii).</summary>
        private static readonly string[] NomsOutil = { "Recalibrate", "RetailCalibration", "Calibrate", "Calibration" };

        /// <summary>
        /// Essaie de lancer la calibration. Renvoie vrai si un appel a été fait sans erreur.
        /// message : ce qui s'est passé, en clair (affiché dans la barre de menu si échec).
        /// </summary>
        public static bool Lancer(GazeSource regard, out string message)
        {
            object hote = regard == null ? null : regard.Hote;
            if (hote == null)
            {
                message = "Calibration impossible : le moteur Tobii n'est pas connecté.";
                App.Log(message);
                return false;
            }

            try
            {
                // Où chercher : l'objet Host lui-même, puis son « Context » (s'il existe).
                List<object> cibles = new List<object>();
                cibles.Add(hote);
                PropertyInfo proprieteContexte = hote.GetType().GetProperty("Context");
                if (proprieteContexte != null)
                {
                    object contexte = proprieteContexte.GetValue(hote, null);
                    if (contexte != null)
                    {
                        cibles.Add(contexte);
                    }
                }

                // 1. Pour le diagnostic : on note toutes les méthodes qui ressemblent à ce
                //    qu'on cherche (« Launch… », « …Calibrat… », « …Configuration… »).
                foreach (object cible in cibles)
                {
                    foreach (MethodInfo methode in cible.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (Ressemble(methode.Name))
                        {
                            App.Log("Calibration : méthode Tobii trouvée : " + cible.GetType().Name + "." + Decrire(methode));
                        }
                    }
                }

                // 2. L'appel le plus probable : LaunchConfigurationTool(outil, …).
                foreach (object cible in cibles)
                {
                    foreach (MethodInfo methode in cible.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (methode.Name == "LaunchConfigurationTool" && EssayerOutil(cible, methode))
                        {
                            message = "Calibration lancée.";
                            return true;
                        }
                    }
                }

                // 3. À défaut : une méthode dont le nom contient « Recalibrat » ou « Calibrat ».
                foreach (object cible in cibles)
                {
                    foreach (MethodInfo methode in cible.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (methode.Name.IndexOf("Calibrat", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            methode.Name.StartsWith("Launch", StringComparison.OrdinalIgnoreCase) &&
                            Appeler(cible, methode, null))
                        {
                            message = "Calibration lancée.";
                            return true;
                        }
                    }
                }

                message = "Calibration : commande Tobii introuvable (voir le journal).";
                App.Log(message);
                return false;
            }
            catch (Exception ex)
            {
                message = "Calibration : erreur (voir le journal).";
                App.Log("Calibration : erreur : " + ex);
                return false;
            }
        }

        /// <summary>
        /// Essaie d'appeler une méthode du type LaunchConfigurationTool(outil, …) avec chacun
        /// des noms d'outil de NomsOutil. Renvoie vrai au premier appel réussi.
        /// </summary>
        private static bool EssayerOutil(object cible, MethodInfo methode)
        {
            // Le paramètre « outil » est une énumération : on cherche lequel.
            ParameterInfo parametreOutil = methode.GetParameters().FirstOrDefault(pi => pi.ParameterType.IsEnum);
            if (parametreOutil == null)
            {
                return false;
            }

            Type typeOutil = parametreOutil.ParameterType;
            App.Log("Calibration : outils proposés par Tobii (" + typeOutil.Name + ") : " +
                    string.Join(", ", Enum.GetNames(typeOutil)));

            foreach (string nom in NomsOutil)
            {
                if (Enum.GetNames(typeOutil).Contains(nom))
                {
                    object outil = Enum.Parse(typeOutil, nom);
                    if (Appeler(cible, methode, outil))
                    {
                        App.Log("Calibration : lancée avec " + typeOutil.Name + "." + nom);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Appelle la méthode en remplissant ses paramètres au mieux :
        ///  - l'énumération reçoit « outil » ;
        ///  - une fonction de rappel (« délégué ») reçoit une fonction vide, qui ne fait rien ;
        ///  - un paramètre facultatif reçoit sa valeur par défaut ;
        ///  - le reste reçoit la valeur « vide » de son type (0, faux, null…).
        /// Renvoie vrai si l'appel n'a pas provoqué d'erreur.
        /// </summary>
        private static bool Appeler(object cible, MethodInfo methode, object outil)
        {
            ParameterInfo[] parametres = methode.GetParameters();
            object[] valeurs = new object[parametres.Length];

            for (int i = 0; i < parametres.Length; i++)
            {
                Type type = parametres[i].ParameterType;
                if (type.IsEnum && outil != null && type == outil.GetType())
                {
                    valeurs[i] = outil;
                }
                else if (typeof(Delegate).IsAssignableFrom(type))
                {
                    valeurs[i] = CreerFonctionVide(type);
                }
                else if (parametres[i].HasDefaultValue)
                {
                    valeurs[i] = parametres[i].DefaultValue;
                }
                else if (type.IsValueType)
                {
                    valeurs[i] = Activator.CreateInstance(type);
                }
                else
                {
                    valeurs[i] = null;
                }
            }

            try
            {
                methode.Invoke(cible, valeurs);
                return true;
            }
            catch (TargetInvocationException ex)
            {
                // L'erreur « vraie » est à l'intérieur (InnerException).
                App.Log("Calibration : " + Decrire(methode) + " a échoué : " + ex.InnerException);
                return false;
            }
            catch (Exception ex)
            {
                App.Log("Calibration : appel impossible de " + Decrire(methode) + " : " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Fabrique une fonction qui ne fait rien, du type de délégué demandé. On utilise pour
        /// cela les « expressions » de .NET : on décrit la fonction (ses paramètres, un corps
        /// vide) puis on la compile pendant que le programme tourne.
        /// </summary>
        private static Delegate CreerFonctionVide(Type typeDelegue)
        {
            MethodInfo invoke = typeDelegue.GetMethod("Invoke");
            ParameterExpression[] parametres = invoke.GetParameters()
                .Select(pi => Expression.Parameter(pi.ParameterType, pi.Name))
                .ToArray();
            Expression corps = invoke.ReturnType == typeof(void)
                ? (Expression)Expression.Empty()
                : Expression.Default(invoke.ReturnType);
            return Expression.Lambda(typeDelegue, corps, parametres).Compile();
        }

        private static bool Ressemble(string nom)
        {
            return nom.StartsWith("Launch", StringComparison.OrdinalIgnoreCase) ||
                   nom.IndexOf("Calibrat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   nom.IndexOf("Configuration", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Écrit une méthode comme « Nom(TypeParam1, TypeParam2) », pour le journal.</summary>
        private static string Decrire(MethodInfo methode)
        {
            return methode.Name + "(" +
                   string.Join(", ", methode.GetParameters().Select(pi => pi.ParameterType.Name)) + ")";
        }
    }
}
