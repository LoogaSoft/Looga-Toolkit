using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Logging.Editor
{
    internal static class LoogaLoggerZStringSupportProvider
    {
        private const string DefineSymbol = "LOOGA_LOGGER_ZSTRING_SUPPORT";

        private static readonly string[] RequiredAssemblies =
        {
            "ZString"
        };

        public static string ProviderId => "looga-logger.zstring";
        public static string PackageName => "Looga Toolkit";
        public static string IntegrationName => "ZString";
        public static string Description => "Uses ZString for lower-allocation formatted log messages.";

        public static bool IsEnabled()
        {
            return LoogaLoggerOptionalSupportUtility.DefineIsEnabled(DefineSymbol);
        }

        public static string GetUnavailableReason()
        {
            return LoogaLoggerOptionalSupportUtility.AllAssembliesAreAvailable(RequiredAssemblies, out string missingAssemblies)
                ? string.Empty
                : "Install ZString. Missing assemblies: " + missingAssemblies;
        }

        public static void SetEnabled(bool enabled)
        {
            if (enabled)
                Enable();
            else
                Disable();
        }

        private static void Enable()
        {
            LoogaLoggerOptionalSupportUtility.AddDefineSymbol(DefineSymbol);
            AssetDatabase.Refresh();
            Debug.Log("Looga Toolkit logging ZString support enabled.");
        }

        private static void Disable()
        {
            LoogaLoggerOptionalSupportUtility.RemoveDefineSymbol(DefineSymbol);
            AssetDatabase.Refresh();
            Debug.Log("Looga Toolkit logging ZString support disabled.");
        }
    }
}
