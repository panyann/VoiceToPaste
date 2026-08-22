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
    /// Edytor reguł podmiany fraz w transkrypcji. Pracuje na kopii listy z AppSettings,
    /// więc niezapisane zmiany nigdy nie trafiają do wspólnego modelu.
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

        public KeyWordsForm(SettingsService settingsService, AppSettings settings)
        {
            _settingsService = settingsService;
            _settings = settings;
            InitializeComponent();

            // Kopia elementów, aby edycja w siatce nie modyfikowała wspólnych ustawień przed zapisem.
            _keywords = new BindingList<DGV_KeyWords>(
                settings.KeyWords.Select(k => new DGV_KeyWords { Key = k.Key, Word = k.Word }).ToList())
            {
                AllowNew = true,
            };

            _loading = true;
            bindingSource.DataSource = _keywords;
            _loading = false;

            // Zmiany komórek i wierszy oznaczają brudny stan i kasują komunikat o ostatnim zapisie.
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
        /// Zapisuje reguły: kończy edycję komórki, normalizuje wpisy, waliduje kompletność
        /// i duplikaty, a po błędzie zapisu przywraca poprzednią listę we wspólnych ustawieniach.
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
                _settingsService.Save(_settings);
            }
            catch (Exception ex)
            {
                // Rollback wspólnego modelu, aby nieudany zapis nie zmienił zachowania aplikacji.
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
        /// Buduje zapisywalną listę: przycina brzegowe spacje, pomija puste wiersze,
        /// a wiersze niepełne i zduplikowane frazy odrzuca z czytelnym komunikatem.
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
