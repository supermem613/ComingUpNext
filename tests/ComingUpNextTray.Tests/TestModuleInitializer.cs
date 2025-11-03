using System;
using System.Runtime.CompilerServices;

namespace ComingUpNextTray.Tests;

/// <summary>
/// Ensures tests never write to the real install config path by enabling the no-writes flag
/// for any TrayApplication instance that does not use an explicit override path.
/// </summary>
internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        // Only set if not already explicitly overridden (allow developers to debug writes if desired).
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COMINGUPNEXT_TEST_NO_WRITES")))
        {
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_NO_WRITES", "1");
        }

        // Provide an isolated fake roaming AppData root so legacy migration logic does not touch the real user profile.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COMINGUPNEXT_TEST_LEGACY_APPDATA_DIR")))
        {
            string tempLegacyRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cun_legacy_appdata");
            try
            {
                System.IO.Directory.CreateDirectory(tempLegacyRoot);
                Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_LEGACY_APPDATA_DIR", tempLegacyRoot);
            }
            catch
            {
                // Ignore failures; tests will fall back to real roaming if this fails, but best-effort isolation.
            }
        }
    }
}
