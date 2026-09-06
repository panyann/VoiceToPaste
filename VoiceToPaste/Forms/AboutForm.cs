using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using VoiceToPaste.Resources;

namespace VoiceToPaste.Forms
{
    public partial class AboutForm : AppForm
    {
        private static readonly ILogger Logger = Log.ForContext<AboutForm>();

        public AboutForm()
        {
            InitializeComponent();
            ApplyLocalizedResources();
            label1.Text = GetApplicationVersion();
        }

        private void ApplyLocalizedResources()
        {
            var resources = new ComponentResourceManager(typeof(AboutForm));
            resources.ApplyResources(this, "$this");
            resources.ApplyResources(labelTitle, "labelTitle");
            resources.ApplyResources(btnKoFi, "btnKoFi");
            resources.ApplyResources(labelDonate, "labelDonate");
            resources.ApplyResources(label2, "label2");
            resources.ApplyResources(labelSource, "labelSource");
            resources.ApplyResources(label3, "label3");
            resources.ApplyResources(labelAuthor, "labelAuthor");
            resources.ApplyResources(labelVersion, "labelVersion");
            resources.ApplyResources(btnGitHub, "btnGitHub");
            resources.ApplyResources(linkLabelPolyFormInternalUse, "linkLabelPolyFormInternalUse");
            resources.ApplyResources(labelOr, "labelOr");
            resources.ApplyResources(linkLabelPolyFormNoncommercial, "linkLabelPolyFormNoncommercial");
        }

        private void btnGitHub_Click(object sender, EventArgs e)
        {
            OpenUrl("https://github.com/panyann/VoiceToPaste");
        }

        private void linkLabelPolyFormInternalUse_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenUrl("https://polyformproject.org/licenses/internal-use/1.0.0");
        }

        private void linkLabelPolyFormNoncommercial_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenUrl("https://polyformproject.org/licenses/noncommercial/1.0.0");
        }

        private void btnKoFi_Click(object sender, EventArgs e)
        {
            OpenUrl("https://ko-fi.com/panyann");
        }

        private static string GetApplicationVersion()
        {
            var productVersion = Application.ProductVersion;
            var metadataSeparatorIndex = productVersion.IndexOf('+');
            if (metadataSeparatorIndex < 0)
            {
                return productVersion;
            }

            return productVersion[..metadataSeparatorIndex];
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open external URL {Url}.", url);
                MessageBox.Show(
                    this,
                    UiStrings.Get("ExternalLinkOpenFailed"),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
