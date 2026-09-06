using System.ComponentModel;
using DarkModeForms;
using VoiceToPaste.Models;
using VoiceToPaste.Services;

namespace VoiceToPaste.Forms
{
    /// <summary>
    /// Shared base window for all application forms. It owns the only DarkModeCS
    /// instance, so derived forms contain no theme code at all. The theme comes
    /// exclusively from application settings and never follows the Windows theme.
    /// </summary>
    public class AppForm : Form
    {
        private DarkModeCS? _darkMode;

        public AppForm()
        {
        }

        /// <summary>
        /// Creates DarkModeCS after a derived form created its controls, but before the
        /// base implementation raises HandleCreated. This lets the library subscribe to
        /// the event without exposing theme setup to derived forms.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            if (_darkMode == null && !IsInDesignMode())
            {
                _darkMode = new DarkModeCS(this)
                {
                    ColorMode = ToDisplayMode(SettingsService.Settings.SelectedTheme),
                };
            }

            base.OnHandleCreated(e);
        }

        /// <summary>
        /// Applies the current global selection immediately. The DisplayMode overload
        /// updates ColorMode, so a Windows theme change cannot override this setting.
        /// </summary>
        protected void ApplySelectedTheme()
        {
            if (_darkMode == null)
                return;

            _darkMode.ApplyTheme(ToDisplayMode(SettingsService.Settings.SelectedTheme));
        }

        private static DarkModeCS.DisplayMode ToDisplayMode(string selectedTheme)
        {
            if (selectedTheme == AppThemes.Light)
                return DarkModeCS.DisplayMode.ClearMode;

            return DarkModeCS.DisplayMode.DarkMode;
        }

        private bool IsInDesignMode()
        {
            if (DesignMode)
                return true;

            return LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        }
    }
}
