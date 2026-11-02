using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace GazeBar
{
    /// <summary>
    /// MODEL: starts the calibration tool of the eyetracker engine (step 4, "Calibration"
    /// button of the "Tools" settings tab; before 2026-10-29, of the menu bar).
    ///
    /// The problem: the Tobii.Interaction 0.7.3 package has almost no documentation, and
    /// this code could not be compiled or tried on the PCEye 5. According to known examples,
    /// the command is called "LaunchConfigurationTool" and takes a "tool" (a value of an
    /// enumeration, for example "Recalibrate"), but this is not certain.
    ///
    /// The solution: "reflection". It is a .NET mechanism that lets a program look, WHILE it
    /// runs, at which methods an object has, and call them by name (a simple string).
    /// Advantages here:
    ///  - if the name is wrong, the program still compiles: calibration simply fails, with a
    ///    message in the log, instead of breaking the whole build;
    ///  - the log lists the methods and tools really offered by the eyetracker: reading it
    ///    will be enough to fix the name if needed, then write the call directly.
    /// Once the right call is found, this whole file can be replaced by a normal line.
    /// </summary>
    internal static class Calibration
    {
        /// <summary>The tool names tried, in order (values of the Tobii.Interaction enumeration).</summary>
        private static readonly string[] ToolNames = { "Recalibrate", "RetailCalibration", "Calibrate", "Calibration" };

        /// <summary>
        /// Tries to start calibration. Returns true if a call was made without error.
        /// message: what happened, in plain words (shown in the menu bar on failure).
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
                // Where to look: the Host object itself, then its "Context" (if it exists).
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

                // 1. For diagnosis: log all methods that look like what we are looking for
                //    ("Launch...", "...Calibrat...", "...Configuration...").
                foreach (object target in targets)
                {
                    foreach (MethodInfo method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (Resembles(method.Name))
                        {
                            App.Log("Calibration: method found: " + target.GetType().Name + "." + Describe(method));
                        }
                    }
                }

                // 2. The most likely call: LaunchConfigurationTool(tool, ...).
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

                // 3. Otherwise: a method whose name contains "Recalibrat" or "Calibrat".
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
                App.Log("Calibration: error: " + ex);
                return false;
            }
        }

        /// <summary>
        /// Tries to call a method like LaunchConfigurationTool(tool, ...) with each of the tool
        /// names of ToolNames. Returns true on the first successful call.
        /// </summary>
        private static bool TryTool(object target, MethodInfo method)
        {
            // The "tool" parameter is an enumeration: find which one.
            ParameterInfo toolParameter = method.GetParameters().FirstOrDefault(pi => pi.ParameterType.IsEnum);
            if (toolParameter == null)
            {
                return false;
            }

            Type toolType = toolParameter.ParameterType;
            App.Log("Calibration: available tools (" + toolType.Name + ") : " +
                    string.Join(", ", Enum.GetNames(toolType)));

            foreach (string name in ToolNames)
            {
                if (Enum.GetNames(toolType).Contains(name))
                {
                    object tool = Enum.Parse(toolType, name);
                    if (Call(target, method, tool))
                    {
                        App.Log("Calibration: started with " + toolType.Name + "." + name);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Calls the method, filling in its parameters as well as possible:
        ///  - the enumeration receives "tool";
        ///  - a callback function ("delegate") receives an empty function that does nothing;
        ///  - an optional parameter receives its default value;
        ///  - the rest receives the "empty" value of its type (0, false, null...).
        /// Returns true if the call did not cause an error.
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
                // The "real" error is inside (InnerException).
                App.Log("Calibration: " + Describe(method) + " failed: " + ex.InnerException);
                return false;
            }
            catch (Exception ex)
            {
                App.Log("Calibration: cannot call " + Describe(method) + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Builds a function that does nothing, of the requested delegate type. For this we use
        /// .NET "expressions": we describe the function (its parameters, an empty body) then
        /// compile it while the program runs.
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

        /// <summary>Writes a method as "Name(ParamType1, ParamType2)", for the log.</summary>
        private static string Describe(MethodInfo method)
        {
            return method.Name + "(" +
                   string.Join(", ", method.GetParameters().Select(pi => pi.ParameterType.Name)) + ")";
        }
    }
}
