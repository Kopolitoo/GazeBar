using System;
using System.Runtime.InteropServices;

namespace Prunel
{
    /// <summary>
    /// Tells whether Prunel runs with "UI access" (uiAccess).
    ///
    /// UI access is a right given by Windows to accessibility programs (on-screen keyboards,
    /// screen readers, gaze control). With it:
    /// - the "always on top" windows of Prunel (the gaze circle) are shown ABOVE the Start
    ///   menu, the search and the notification centre;
    /// - the clicks and keys simulated by Prunel also reach the windows started as
    ///   administrator.
    ///
    /// Windows gives this right only if three conditions are met:
    /// 1. the program asks for it in its manifest (uiAccess="true", see app.uiaccess.manifest);
    /// 2. the program is digitally signed by a certificate trusted by the computer;
    /// 3. the program is in a protected folder, such as C:\Program Files.
    ///
    /// The normal build (Visual Studio, simple .zip) does not ask for it: a program that asks
    /// for it without meeting conditions 2 and 3 does not start at all. Only the "full
    /// installation" build asks for it (MSBuild property UiAccess=true, see Prunel.csproj).
    ///
    /// This class only reads the result, for the log: "UI access: yes" or "no".
    /// </summary>
    internal static class UiAccess
    {
        /// <summary>True if Windows gave UI access to Prunel. False if not, or if unknown.</summary>
        internal static bool IsActive()
        {
            IntPtr token;
            if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(), NativeMethods.TOKEN_QUERY, out token))
            {
                App.Log("UI access: cannot read the process token (Windows error " + Marshal.GetLastWin32Error() + ")");
                return false;
            }

            try
            {
                uint value;
                uint length;
                if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenUIAccess, out value, sizeof(uint), out length))
                {
                    App.Log("UI access: cannot read the information (Windows error " + Marshal.GetLastWin32Error() + ")");
                    return false;
                }
                return value != 0;
            }
            finally
            {
                NativeMethods.CloseHandle(token);
            }
        }
    }
}
