using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Serilog;
using VoiceToPaste.Models;
using VoiceToPaste.Resources;
using VoiceToPaste.Services;

namespace VoiceToPaste.Forms
{
    /// <summary>
    /// Editor for phrase replacement rules in transcription. It works on a copy of the list
    /// from AppSettings, so unsaved changes never reach the shared model.
    /// </summary>
    public partial class KeyWordsForm : Form
    {
        private static readonly ILogger Logger = Log.ForContext<KeyWordsForm>();

        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly BindingList<DGV_KeyWords> _keywords;
        private readonly System.Windows.Forms.Timer _saveStatusTimer = new() { Interval = 2000 };
        private bool _loading;
        private bool _isDirty;

        public KeyWordsForm(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _settings = settingsService.Settings;
            InitializeComponent();

            // A copy of the items so editing the grid does not mutate the shared settings before saving.
            _keywords = new BindingList<DGV_KeyWords>(
                _settings.KeyWords.Select(k => new DGV_KeyWords { Key = k.Key, Word = k.Word }).ToList())
            {
                AllowNew = true,
            };

            _loading = true;
            bindingSource.DataSource = _keywords;
            _loading = false;

            // Cell and row changes mark the dirty state and clear the last-save message.
            dataGridView.CellValueChanged += DataGridView_CellValueChanged;
            dataGridView.UserDeletedRow += DataGridView_UserDeletedRow;
            bindingSource.ListChanged += BindingSource_ListChanged;
            FormClosing += KeyWordsForm_FormClosing;
            _saveStatusTimer.Tick += SaveStatusTimer_Tick;
        }

        private void DataGridView_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                MarkDirty();
        }

        private void DataGridView_UserDeletedRow(object? sender, DataGridViewRowEventArgs e)
        {
            MarkDirty();
        }

        private void BindingSource_ListChanged(object? sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType is ListChangedType.ItemAdded or ListChangedType.ItemDeleted or ListChangedType.Reset)
                MarkDirty();
        }

        private void MarkDirty()
        {
            if (_loading)
                return;

            _isDirty = true;
            _saveStatusTimer.Stop();
            labelSaveStatus.Text = "";
        }

        private void SaveStatusTimer_Tick(object? sender, EventArgs e)
        {
            _saveStatusTimer.Stop();
            labelSaveStatus.Text = "";
        }

        /// <summary>
        /// Saves the rules: ends cell editing, normalizes entries, validates completeness and
        /// duplicates, and restores the previous list in the shared settings after a save error.
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            _saveStatusTimer.Stop();
            labelSaveStatus.Text = "";
            dataGridView.EndEdit();
            bindingSource.EndEdit();

            List<DGV_KeyWords> normalized;
            try
            {
                normalized = BuildNormalizedKeywords();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    this,
                    LocalizedExceptionFactory.GetUserMessage(ex),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var previous = _settings.KeyWords;
            _settings.KeyWords = normalized;
            try
            {
                _settingsService.Save();
            }
            catch (Exception ex)
            {
                // Rollback the shared model so a failed save does not change application behavior.
                _settings.KeyWords = previous;
                Logger.Error(ex, "Failed to save keywords.");
                MessageBox.Show(this, UiStrings.Format("SettingsSaveFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _isDirty = false;
            labelSaveStatus.Text = UiStrings.Get("KeywordChangesSaved");
            _saveStatusTimer.Start();
            Logger.Information("Saved {Count} keyword rules.", normalized.Count);
        }

        /// <summary>
        /// Builds the saveable list: trims surrounding spaces, skips empty rows, and rejects
        /// incomplete rows and duplicated phrases with a readable message.
        /// </summary>
        private List<DGV_KeyWords> BuildNormalizedKeywords()
        {
            var result = new List<DGV_KeyWords>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < _keywords.Count; i++)
            {
                var key = _keywords[i].Key?.Trim() ?? "";
                var word = _keywords[i].Word?.Trim() ?? "";

                if (key.Length == 0 && word.Length == 0)
                    continue;

                if (key.Length == 0 || word.Length == 0)
                    throw LocalizedExceptionFactory.InvalidOperation("KeywordRowIncomplete", i + 1);

                if (!seenKeys.Add(key))
                    throw LocalizedExceptionFactory.InvalidOperation("KeywordDuplicatePhrase", i + 1, key);

                result.Add(new DGV_KeyWords { Key = key, Word = word });
            }

            return result;
        }

        private void KeyWordsForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_isDirty)
                return;

            var choice = MessageBox.Show(this,
                UiStrings.Get("KeywordUnsavedChangesPrompt"),
                UiStrings.Get("ApplicationTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (choice == DialogResult.No)
                e.Cancel = true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _saveStatusTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
