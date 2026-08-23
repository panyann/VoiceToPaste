using DarkModeForms;
using Serilog;
using VoiceToPaste.Resources;
using VoiceToPaste.Services;

namespace VoiceToPaste.Forms
{
    public partial class CudaRuntimeDownloadForm : Form
    {
        private static readonly ILogger Logger = Log.ForContext<CudaRuntimeDownloadForm>();
        private readonly CudaRuntimeService _cudaRuntimeService;
        private CancellationTokenSource? _downloadCancellation;
        private bool _isDownloading;
        private bool _closeAfterCancellation;

        private DarkModeCS dm = null;

        public CudaRuntimeDownloadForm()
            : this(new CudaRuntimeService())
        {
            dm = new DarkModeCS(this)
            {
                //[Optional] Choose your preferred color mode here:
                ColorMode = DarkModeCS.DisplayMode.SystemDefault
            };
        }

        internal CudaRuntimeDownloadForm(CudaRuntimeService cudaRuntimeService)
        {
            _cudaRuntimeService = cudaRuntimeService;
            InitializeComponent();

            var missingDownloadSize = _cudaRuntimeService.GetMissingDownloadSize();
            labelDescription.Text = UiStrings.Format(
                "CudaRuntimeDownloadDescription",
                Environment.NewLine,
                FormatMegabytes(missingDownloadSize),
                519);
            toolStripStatusLabel.Text = UiStrings.Get("DownloadWaitingToStart");

            dm = new DarkModeCS(this)
            {
                //[Optional] Choose your preferred color mode here:
                ColorMode = DarkModeCS.DisplayMode.SystemDefault
            };
        }

        /// <summary>Pobiera CUDA wyłącznie po świadomym kliknięciu użytkownika.</summary>
        private async void btnDownload_Click(object? sender, EventArgs e)
        {
            if (_isDownloading)
                return;

            _isDownloading = true;
            btnDownload.Enabled = false;
            progressBarDownload.Value = 0;
            _downloadCancellation = new CancellationTokenSource();
            Logger.Information("The user started downloading the CUDA libraries.");

            var wasCancelled = false;
            var installationSucceeded = false;
            try
            {
                var progress = new Progress<CudaRuntimeProgress>(UpdateProgress);
                await _cudaRuntimeService.InstallAsync(progress, _downloadCancellation.Token);
                toolStripStatusLabel.Text = UiStrings.Get("CudaRuntimeInstalled");
                Logger.Information("The CUDA libraries were downloaded successfully.");
                installationSucceeded = true;
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
                toolStripStatusLabel.Text = UiStrings.Get("DownloadCancelled");
                Logger.Information("The CUDA libraries download was cancelled.");
            }
            catch (Exception ex)
            {
                toolStripStatusLabel.Text = UiStrings.Get("CudaRuntimeInstallationFailedStatus");
                Logger.Error(ex, "Failed to download the CUDA libraries.");
                MessageBox.Show(
                    this,
                    UiStrings.Format(
                        "CudaRuntimeDownloadFailedMessage",
                        Environment.NewLine,
                        LocalizedExceptionFactory.GetUserMessage(ex),
                        Program.LogDirectory),
                    UiStrings.Get("ApplicationName"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _isDownloading = false;
                _downloadCancellation?.Dispose();
                _downloadCancellation = null;
                btnDownload.Enabled = true;
            }

            if (wasCancelled && _closeAfterCancellation)
                Close();

            if (installationSucceeded)
                DialogResult = DialogResult.OK;
        }

        private void UpdateProgress(CudaRuntimeProgress progress)
        {
            toolStripStatusLabel.Text = progress.Message;

            if (progress.TotalBytes is not long totalBytes || totalBytes <= 0)
                return;

            var percent = (int)Math.Clamp(progress.DownloadedBytes * 100 / totalBytes, 0, 100);
            progressBarDownload.Value = percent;
            toolStripStatusLabel.Text =
                UiStrings.Format(
                    "DownloadProgressWithSize",
                    progress.Message,
                    FormatMegabytes(progress.DownloadedBytes),
                    FormatMegabytes(totalBytes),
                    percent);
        }

        private void CudaRuntimeDownloadForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_isDownloading)
                return;

            if (!_closeAfterCancellation && MessageBox.Show(
                    this,
                    UiStrings.Get("CudaRuntimeCancelConfirmation"),
                    UiStrings.Get("ApplicationName"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            // Formularz pozostaje otwarty do chwili zakończenia zadania, aby nie aktualizować
            // zwolnionych kontrolek z raportu postępu.
            _closeAfterCancellation = true;
            _downloadCancellation?.Cancel();
            Logger.Information("The user confirmed cancellation of the CUDA download.");
            toolStripStatusLabel.Text = UiStrings.Get("DownloadCancelling");
            e.Cancel = true;
        }

        private static string FormatMegabytes(long bytes) => (bytes / 1024d / 1024d).ToString("F1");

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Logger.Information("The CUDA installation window was closed.");
            base.OnFormClosed(e);
        }
    }
}
