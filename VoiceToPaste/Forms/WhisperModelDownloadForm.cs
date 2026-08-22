using Serilog;
using VoiceToPaste.Models;
using VoiceToPaste.Resources;
using VoiceToPaste.Services;

namespace VoiceToPaste.Forms
{
    /// <summary>Modalne okno pobierania jednego modelu Whisper wybranego w Ustawieniach.</summary>
    public partial class WhisperModelDownloadForm : Form
    {
        private static readonly ILogger Logger = Log.ForContext<WhisperModelDownloadForm>();
        private readonly WhisperModel _model;
        private readonly WhisperModelDownloadService _downloadService;
        private CancellationTokenSource? _downloadCancellation;
        private bool _isDownloading;
        private bool _closeAfterCancellation;

        /// <summary>Konstruktor wymagany przez WinForms Designer.</summary>
        public WhisperModelDownloadForm()
            : this(WhisperModelCatalog.GetById(WhisperModelCatalog.DefaultModelId), new WhisperModelDownloadService())
        {
        }

        internal WhisperModelDownloadForm(WhisperModel model, WhisperModelDownloadService downloadService)
        {
            _model = model;
            _downloadService = downloadService;
            InitializeComponent();

            labelDescription.Text = UiStrings.Format(
                "WhisperModelDownloadDescription",
                model.DisplayName,
                Environment.NewLine,
                model.Description,
                FormatMegabytes(model.SizeBytes),
                model.FileName);
            toolStripStatusLabel.Text = UiStrings.Get("DownloadWaitingToStart");
            btnDownload.Click += btnDownload_Click;
            FormClosing += WhisperModelDownloadForm_FormClosing;
        }

        /// <summary>Pobiera tylko model przekazany podczas utworzenia formularza.</summary>
        private async void btnDownload_Click(object? sender, EventArgs e)
        {
            if (_isDownloading)
                return;

            _isDownloading = true;
            btnDownload.Enabled = false;
            progressBarDownload.Value = 0;
            _downloadCancellation = new CancellationTokenSource();
            Logger.Information("The user started downloading the Whisper model {ModelId}.", _model.Id);

            var wasCancelled = false;
            var downloadSucceeded = false;
            try
            {
                var progress = new Progress<WhisperModelDownloadProgress>(UpdateProgress);
                await _downloadService.DownloadAsync(_model, progress, _downloadCancellation.Token);
                toolStripStatusLabel.Text = UiStrings.Format("WhisperModelDownloadCompleted", _model.DisplayName);
                Logger.Information("The Whisper model {ModelId} was downloaded successfully.", _model.Id);
                downloadSucceeded = true;
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
                toolStripStatusLabel.Text = UiStrings.Get("DownloadCancelled");
                Logger.Information("The Whisper model {ModelId} download was cancelled.", _model.Id);
            }
            catch (Exception ex)
            {
                toolStripStatusLabel.Text = UiStrings.Get("WhisperModelDownloadFailedStatus");
                Logger.Error(ex, "Failed to download the Whisper model {ModelId}.", _model.Id);
                MessageBox.Show(
                    this,
                    UiStrings.Format(
                        "WhisperModelDownloadFailedMessage",
                        _model.DisplayName,
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

            if (downloadSucceeded)
                DialogResult = DialogResult.OK;
        }

        private void UpdateProgress(WhisperModelDownloadProgress progress)
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

        private void WhisperModelDownloadForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_isDownloading)
                return;

            if (!_closeAfterCancellation && MessageBox.Show(
                    this,
                    UiStrings.Format("WhisperModelCancelConfirmation", _model.DisplayName),
                    UiStrings.Get("ApplicationName"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            // Formularz pozostaje otwarty do końca zadania, żeby raport postępu nie dotknął zwolnionych kontrolek.
            _closeAfterCancellation = true;
            _downloadCancellation?.Cancel();
            toolStripStatusLabel.Text = UiStrings.Get("DownloadCancelling");
            e.Cancel = true;
        }

        private static string FormatMegabytes(long bytes) => (bytes / 1024d / 1024d).ToString("F1");
    }
}
