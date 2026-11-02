using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : lance l'outil de calibration du moteur de l'eyetracker (étape 4, bouton « Calibration »
    /// de l'onglet « Outils » des paramètres ; avant le 29/10/2026, de la barre de menu).
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
    ///  - le journal liste les méthodes et les outils réellement proposés par l'eyetracker : il
    ///    suffira de le lire pour corriger le nom si besoin, puis d'écrire l'appel directement.
    /// Une fois le bon appel trouvé, on pourra remplacer tout ce fichier par une ligne normale.
    /// </summary>
    internal static class Calibration
    {
        /// <summary>Les noms d'outil essayés, dans l'ordre (valeurs de l'énumération de Tobii.Interaction).</summary>
        private static readonly string[] ToolNames = { "Recalibrate", "RetailCalibration", "Calibrate", "Calibration" };

        /// <summary>
        /// Essaie de lancer la calibration. Renvoie vrai si un appel a été fait sans erreur.
        /// message : ce qui s'est passé, en clair (affiché dans la barre de menu si échec).
        /// </summary>
        public static bool Launch(GazeSource gaze, out string message)
        {
            object host = gaze == null ? null : gaze.Host;
            if (host == null)
            {
                message = "Calibration impossible : le moteur de l'eyetracker n'est pas connecté.";
                App.Log(message);
                return false;
            }

            try
            {
                // Où chercher : l'objet Host lui-même, puis son « Context » (s'il existe).
                List<object> targets = new List<object>();
                targets.Add(host);
                PropertyInfo contextProperty = host.GetType().GetProperty("Context");
                if (contextProperty != null)
                {
                    object context = contextProperty.GetValue(host, null);
                    if (context != null)
                    {
                        targets.Add(context);
                    }
                }

                // 1. Pour le diagnostic : on note toutes les méthodes qui ressemblent à ce
                //    qu'on cherche (« Launch… », « …Calibrat… », « …Configuration… »).
                foreach (object target in targets)
                {
                    foreach (MethodInfo method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (Resembles(method.Name))
                        {
                            App.Log("Calibration : méthode trouvée : " + target.GetType().Name + "." + Describe(method));
                        }
                    }
                }

                // 2. L'appel le plus probable : LaunchConfigurationTool(outil, …).
                foreach (object target in targets)
                {
                    foreach (MethodInfo method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (method.Name == "LaunchConfigurationTool" && TryTool(target, method))
                        {
                            message = "Calibration lancée.";
                            return true;
                        }
                    }
                }

                // 3. À défaut : une méthode dont le nom contient « Recalibrat » ou « Calibrat ».
                foreach (object target in targets)
                {
                    foreach (MethodInfo method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (method.Name.IndexOf("Calibrat", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            method.Name.StartsWith("Launch", StringComparison.OrdinalIgnoreCase) &&
                            Call(target, method, null))
                        {
                            message = "Calibration lancée.";
                            return true;
                        }
                    }
                }

                message = "Calibration : commande de calibration introuvable (voir le journal).";
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
        private static bool TryTool(object target, MethodInfo method)
        {
            // Le paramètre « outil » est une énumération : on cherche lequel.
            ParameterInfo toolParameter = method.GetParameters().FirstOrDefault(pi => pi.ParameterType.IsEnum);
            if (toolParameter == null)
            {
                return false;
            }

            Type toolType = toolParameter.ParameterType;
            App.Log("Calibration : outils proposés (" + toolType.Name + ") : " +
                    string.Join(", ", Enum.GetNames(toolType)));

            foreach (string name in ToolNames)
            {
                if (Enum.GetNames(toolType).Contains(name))
                {
                    object tool = Enum.Parse(toolType, name);
                    if (Call(target, method, tool))
                    {
                        App.Log("Calibration : lancée avec " + toolType.Name + "." + name);
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
        private static bool Call(object target, MethodInfo method, object tool)
        {
            ParameterInfo[] settings = method.GetParameters();
            object[] values = new object[settings.Length];

            for (int i = 0; i < settings.Length; i++)
            {
                Type type = settings[i].ParameterType;
                if (type.IsEnum && tool != null && type == tool.GetType())
                {
                    values[i] = tool;
                }
                else if (typeof(Delegate).IsAssignableFrom(type))
                {
                    values[i] = CreateEmptyFunction(type);
                }
                else if (settings[i].HasDefaultValue)
                {
                    values[i] = settings[i].DefaultValue;
                }
                else if (type.IsValueType)
                {
                    values[i] = Activator.CreateInstance(type);
                }
                else
                {
                    values[i] = null;
                }
            }

            try
            {
                method.Invoke(target, values);
                return true;
            }
            catch (TargetInvocationException ex)
            {
                // L'erreur « vraie » est à l'intérieur (InnerException).
                App.Log("Calibration : " + Describe(method) + " a échoué : " + ex.InnerException);
                return false;
            }
            catch (Exception ex)
            {
                App.Log("Calibration : appel impossible de " + Describe(method) + " : " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Fabrique une fonction qui ne fait rien, du type de délégué demandé. On utilise pour
        /// cela les « expressions » de .NET : on décrit la fonction (ses paramètres, un corps
        /// vide) puis on la compile pendant que le programme tourne.
        /// </summary>
        private static Delegate CreateEmptyFunction(Type delegateType)
        {
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            ParameterExpression[] settings = invoke.GetParameters()
                .Select(pi => Expression.Parameter(pi.ParameterType, pi.Name))
                .ToArray();
            Expression body = invoke.ReturnType == typeof(void)
                ? (Expression)Expression.Empty()
                : Expression.Default(invoke.ReturnType);
            return Expression.Lambda(delegateType, body, settings).Compile();
        }

        private static bool Resembles(string name)
        {
            return name.StartsWith("Launch", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("Calibrat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Configuration", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Écrit une méthode comme « Nom(TypeParam1, TypeParam2) », pour le journal.</summary>
        private static string Describe(MethodInfo method)
        {
            return method.Name + "(" +
                   string.Join(", ", method.GetParameters().Select(pi => pi.ParameterType.Name)) + ")";
        }
    }
}
