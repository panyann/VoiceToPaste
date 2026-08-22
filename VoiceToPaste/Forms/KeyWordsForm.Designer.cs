namespace VoiceToPaste.Forms
{
    partial class KeyWordsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(KeyWordsForm));
            dataGridView = new DataGridView();
            keyDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            wordDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            bindingSource = new BindingSource(components);
            btnSave = new Button();
            labelSaveStatus = new Label();
            ((System.ComponentModel.ISupportInitialize)dataGridView).BeginInit();
            ((System.ComponentModel.ISupportInitialize)bindingSource).BeginInit();
            SuspendLayout();
            // 
            // dataGridView
            // 
            dataGridView.AutoGenerateColumns = false;
            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView.Columns.AddRange(new DataGridViewColumn[] { keyDataGridViewTextBoxColumn, wordDataGridViewTextBoxColumn });
            dataGridView.DataSource = bindingSource;
            resources.ApplyResources(dataGridView, "dataGridView");
            dataGridView.Name = "dataGridView";
            // 
            // keyDataGridViewTextBoxColumn
            // 
            keyDataGridViewTextBoxColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            keyDataGridViewTextBoxColumn.DataPropertyName = "Key";
            resources.ApplyResources(keyDataGridViewTextBoxColumn, "keyDataGridViewTextBoxColumn");
            keyDataGridViewTextBoxColumn.Name = "keyDataGridViewTextBoxColumn";
            // 
            // wordDataGridViewTextBoxColumn
            // 
            wordDataGridViewTextBoxColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            wordDataGridViewTextBoxColumn.DataPropertyName = "Word";
            resources.ApplyResources(wordDataGridViewTextBoxColumn, "wordDataGridViewTextBoxColumn");
            wordDataGridViewTextBoxColumn.Name = "wordDataGridViewTextBoxColumn";
            // 
            // bindingSource
            // 
            bindingSource.DataSource = typeof(Models.DGV_KeyWords);
            // 
            // btnSave
            // 
            resources.ApplyResources(btnSave, "btnSave");
            btnSave.Name = "btnSave";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // labelSaveStatus
            // 
            resources.ApplyResources(labelSaveStatus, "labelSaveStatus");
            labelSaveStatus.Name = "labelSaveStatus";
            // 
            // KeyWordsForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(labelSaveStatus);
            Controls.Add(btnSave);
            Controls.Add(dataGridView);
            Name = "KeyWordsForm";
            ((System.ComponentModel.ISupportInitialize)dataGridView).EndInit();
            ((System.ComponentModel.ISupportInitialize)bindingSource).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private DataGridView dataGridView;
        private BindingSource bindingSource;
        private Button btnSave;
        private Label labelSaveStatus;
        private DataGridViewTextBoxColumn keyDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn wordDataGridViewTextBoxColumn;
    }
}