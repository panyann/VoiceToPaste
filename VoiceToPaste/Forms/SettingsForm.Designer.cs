namespace VoiceToPaste.Forms
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            btnTester = new Button();
            comboBoxEngine = new ComboBox();
            labelEngine = new Label();
            labelLanguage = new Label();
            comboBoxLanguage = new ComboBox();
            checkBoxStartInTray = new CheckBox();
            labelHotkey = new Label();
            textBoxHotKey = new TextBox();
            labelDescription = new Label();
            labelModel = new Label();
            comboBoxModel = new ComboBox();
            textBoxRecordLimit = new TextBox();
            labelRecordLimit = new Label();
            labelSeconds = new Label();
            btnKeyWords = new Button();
            checkBoxAutoStart = new CheckBox();
            toolTipAutoStart = new ToolTip(components);
            labelUiLanguage = new Label();
            comboBoxUiLanguage = new ComboBox();
            menuStrip = new MenuStrip();
            menuItemAbout = new ToolStripMenuItem();
            menuStrip.SuspendLayout();
            SuspendLayout();
            // 
            // btnTester
            // 
            resources.ApplyResources(btnTester, "btnTester");
            btnTester.Name = "btnTester";
            btnTester.UseVisualStyleBackColor = true;
            btnTester.Click += btnTester_Click;
            // 
            // comboBoxEngine
            // 
            comboBoxEngine.FormattingEnabled = true;
            resources.ApplyResources(comboBoxEngine, "comboBoxEngine");
            comboBoxEngine.Name = "comboBoxEngine";
            comboBoxEngine.SelectedIndexChanged += comboBoxEngine_SelectedIndexChanged;
            // 
            // labelEngine
            // 
            resources.ApplyResources(labelEngine, "labelEngine");
            labelEngine.Name = "labelEngine";
            // 
            // labelLanguage
            // 
            resources.ApplyResources(labelLanguage, "labelLanguage");
            labelLanguage.Name = "labelLanguage";
            // 
            // comboBoxLanguage
            // 
            comboBoxLanguage.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBoxLanguage.AutoCompleteSource = AutoCompleteSource.ListItems;
            comboBoxLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxLanguage.FormattingEnabled = true;
            resources.ApplyResources(comboBoxLanguage, "comboBoxLanguage");
            comboBoxLanguage.Name = "comboBoxLanguage";
            comboBoxLanguage.SelectionChangeCommitted += comboBoxLanguage_SelectionChangeCommitted;
            // 
            // checkBoxStartInTray
            // 
            resources.ApplyResources(checkBoxStartInTray, "checkBoxStartInTray");
            checkBoxStartInTray.Name = "checkBoxStartInTray";
            checkBoxStartInTray.UseVisualStyleBackColor = true;
            checkBoxStartInTray.CheckedChanged += checkBoxStartInTray_CheckedChanged;
            // 
            // labelHotkey
            // 
            resources.ApplyResources(labelHotkey, "labelHotkey");
            labelHotkey.Name = "labelHotkey";
            // 
            // textBoxHotKey
            // 
            resources.ApplyResources(textBoxHotKey, "textBoxHotKey");
            textBoxHotKey.Name = "textBoxHotKey";
            textBoxHotKey.ReadOnly = true;
            // 
            // labelDescription
            // 
            resources.ApplyResources(labelDescription, "labelDescription");
            labelDescription.Name = "labelDescription";
            // 
            // labelModel
            // 
            resources.ApplyResources(labelModel, "labelModel");
            labelModel.Name = "labelModel";
            // 
            // comboBoxModel
            // 
            comboBoxModel.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBoxModel.AutoCompleteSource = AutoCompleteSource.ListItems;
            comboBoxModel.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxModel.FormattingEnabled = true;
            resources.ApplyResources(comboBoxModel, "comboBoxModel");
            comboBoxModel.Name = "comboBoxModel";
            comboBoxModel.SelectionChangeCommitted += comboBoxModel_SelectionChangeCommitted;
            // 
            // textBoxRecordLimit
            // 
            resources.ApplyResources(textBoxRecordLimit, "textBoxRecordLimit");
            textBoxRecordLimit.Name = "textBoxRecordLimit";
            textBoxRecordLimit.TextChanged += textBoxRecordLimit_TextChanged;
            textBoxRecordLimit.KeyDown += textBoxRecordingTime_KeyDown;
            textBoxRecordLimit.Leave += textBoxRecordLimit_Leave;
            // 
            // labelRecordLimit
            // 
            resources.ApplyResources(labelRecordLimit, "labelRecordLimit");
            labelRecordLimit.Name = "labelRecordLimit";
            // 
            // labelSeconds
            // 
            resources.ApplyResources(labelSeconds, "labelSeconds");
            labelSeconds.Name = "labelSeconds";
            // 
            // btnKeyWords
            // 
            resources.ApplyResources(btnKeyWords, "btnKeyWords");
            btnKeyWords.Name = "btnKeyWords";
            btnKeyWords.UseVisualStyleBackColor = true;
            btnKeyWords.Click += btnKeyWords_Click;
            // 
            // checkBoxAutoStart
            // 
            resources.ApplyResources(checkBoxAutoStart, "checkBoxAutoStart");
            checkBoxAutoStart.Name = "checkBoxAutoStart";
            checkBoxAutoStart.UseVisualStyleBackColor = true;
            checkBoxAutoStart.CheckedChanged += checkBoxAutoStart_CheckedChanged;
            // 
            // labelUiLanguage
            // 
            resources.ApplyResources(labelUiLanguage, "labelUiLanguage");
            labelUiLanguage.Name = "labelUiLanguage";
            // 
            // comboBoxUiLanguage
            // 
            comboBoxUiLanguage.FormattingEnabled = true;
            resources.ApplyResources(comboBoxUiLanguage, "comboBoxUiLanguage");
            comboBoxUiLanguage.Name = "comboBoxUiLanguage";
            comboBoxUiLanguage.SelectionChangeCommitted += comboBoxUiLanguage_SelectionChangeCommitted;
            // 
            // menuStrip
            // 
            menuStrip.ImageScalingSize = new Size(32, 32);
            menuStrip.Items.AddRange(new ToolStripItem[] { menuItemAbout });
            resources.ApplyResources(menuStrip, "menuStrip");
            menuStrip.Name = "menuStrip";
            // 
            // menuItemAbout
            // 
            menuItemAbout.Alignment = ToolStripItemAlignment.Right;
            menuItemAbout.Name = "menuItemAbout";
            resources.ApplyResources(menuItemAbout, "menuItemAbout");
            menuItemAbout.Click += menuItemAbout_Click;
            // 
            // SettingsForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(labelUiLanguage);
            Controls.Add(comboBoxUiLanguage);
            Controls.Add(checkBoxAutoStart);
            Controls.Add(btnKeyWords);
            Controls.Add(labelSeconds);
            Controls.Add(textBoxRecordLimit);
            Controls.Add(labelRecordLimit);
            Controls.Add(labelModel);
            Controls.Add(comboBoxModel);
            Controls.Add(labelDescription);
            Controls.Add(textBoxHotKey);
            Controls.Add(labelHotkey);
            Controls.Add(checkBoxStartInTray);
            Controls.Add(labelLanguage);
            Controls.Add(comboBoxLanguage);
            Controls.Add(labelEngine);
            Controls.Add(comboBoxEngine);
            Controls.Add(btnTester);
            Controls.Add(menuStrip);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MainMenuStrip = menuStrip;
            Name = "SettingsForm";
            Shown += SettingsForm_Shown;
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnTester;
        private ComboBox comboBoxEngine;
        private Label labelEngine;
        private Label labelLanguage;
        private ComboBox comboBoxLanguage;
        private CheckBox checkBoxStartInTray;
        private Label labelHotkey;
        private TextBox textBoxHotKey;
        private Label labelDescription;
        private Label labelModel;
        private ComboBox comboBoxModel;
        private TextBox textBoxRecordLimit;
        private Label labelRecordLimit;
        private Label labelSeconds;
        private Button btnKeyWords;
        private CheckBox checkBoxAutoStart;
        private ToolTip toolTipAutoStart;
        private Label labelUiLanguage;
        private ComboBox comboBoxUiLanguage;
        private MenuStrip menuStrip;
        private ToolStripMenuItem menuItemAbout;
    }
}