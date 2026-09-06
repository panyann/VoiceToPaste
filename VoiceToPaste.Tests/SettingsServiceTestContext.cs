using VoiceToPaste.Models;
using VoiceToPaste.Services;

namespace VoiceToPaste.Tests
{
    /// <summary>
    /// Redirects the process-wide settings service to an isolated test directory.
    /// Tests using it belong to the non-parallel SettingsServiceCollection.
    /// </summary>
    internal sealed class SettingsServiceTestContext
    {
        internal SettingsServiceTestContext(string settingsDirectory)
        {
            SettingsService.ResetForTests(settingsDirectory);
        }

        internal string SettingsPath => SettingsService.SettingsPath;
        internal AppSettings Settings => SettingsService.Settings;

        internal AppSettings Load()
        {
            return SettingsService.Load();
        }

        internal void Save()
        {
            SettingsService.Save();
        }
    }
}
