namespace VoiceToPaste.Forms
{
    partial class AboutForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutForm));
            tableLayoutPanel = new TableLayoutPanel();
            pictureBoxIcon = new PictureBox();
            labelTitle = new Label();
            subTableLayoutPanel = new TableLayoutPanel();
            btnKoFi = new Button();
            imageListGitHub = new ImageList(components);
            labelDonate = new Label();
            labelLicense = new Label();
            labelSource = new Label();
            labelAuthorValue = new Label();
            labelAuthor = new Label();
            labelVersionValue = new Label();
            labelVersion = new Label();
            btnGitHub = new Button();
            tableLayoutPanel1 = new TableLayoutPanel();
            linkLabelPolyFormInternalUse = new LinkLabel();
            labelOr = new Label();
            linkLabelPolyFormNoncommercial = new LinkLabel();
            tableLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxIcon).BeginInit();
            subTableLayoutPanel.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel.Controls.Add(pictureBoxIcon, 0, 0);
            tableLayoutPanel.Controls.Add(labelTitle, 0, 1);
            tableLayoutPanel.Controls.Add(subTableLayoutPanel, 0, 2);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(0, 0);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.RowCount = 3;
            tableLayoutPanel.RowStyles.Add(new RowStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 12.4726477F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 87.52735F));
            tableLayoutPanel.Size = new Size(794, 639);
            tableLayoutPanel.TabIndex = 0;
            // 
            // pictureBoxIcon
            // 
            pictureBoxIcon.Anchor = AnchorStyles.None;
            pictureBoxIcon.Image = (Image)resources.GetObject("pictureBoxIcon.Image");
            pictureBoxIcon.Location = new Point(333, 20);
            pictureBoxIcon.Margin = new Padding(20, 20, 20, 0);
            pictureBoxIcon.Name = "pictureBoxIcon";
            pictureBoxIcon.Size = new Size(128, 128);
            pictureBoxIcon.SizeMode = PictureBoxSizeMode.CenterImage;
            pictureBoxIcon.TabIndex = 1;
            pictureBoxIcon.TabStop = false;
            // 
            // labelTitle
            // 
            labelTitle.Anchor = AnchorStyles.None;
            labelTitle.AutoSize = true;
            labelTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 238);
            labelTitle.Location = new Point(289, 156);
            labelTitle.Name = "labelTitle";
            labelTitle.Size = new Size(215, 45);
            labelTitle.TabIndex = 2;
            labelTitle.Text = "VoiceToPaste";
            // 
            // subTableLayoutPanel
            // 
            subTableLayoutPanel.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            subTableLayoutPanel.ColumnCount = 2;
            subTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            subTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            subTableLayoutPanel.Controls.Add(btnKoFi, 1, 4);
            subTableLayoutPanel.Controls.Add(labelDonate, 0, 4);
            subTableLayoutPanel.Controls.Add(labelLicense, 0, 3);
            subTableLayoutPanel.Controls.Add(labelSource, 0, 2);
            subTableLayoutPanel.Controls.Add(labelAuthorValue, 1, 1);
            subTableLayoutPanel.Controls.Add(labelAuthor, 0, 1);
            subTableLayoutPanel.Controls.Add(labelVersionValue, 1, 0);
            subTableLayoutPanel.Controls.Add(labelVersion, 0, 0);
            subTableLayoutPanel.Controls.Add(btnGitHub, 1, 2);
            subTableLayoutPanel.Controls.Add(tableLayoutPanel1, 1, 3);
            subTableLayoutPanel.Dock = DockStyle.Fill;
            subTableLayoutPanel.Location = new Point(3, 212);
            subTableLayoutPanel.Name = "subTableLayoutPanel";
            subTableLayoutPanel.RowCount = 5;
            subTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            subTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            subTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            subTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            subTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            subTableLayoutPanel.Size = new Size(788, 424);
            subTableLayoutPanel.TabIndex = 3;
            // 
            // btnKoFi
            // 
            btnKoFi.Dock = DockStyle.Fill;
            btnKoFi.ImageKey = "iconoir--coffee-cup.png";
            btnKoFi.ImageList = imageListGitHub;
            btnKoFi.Location = new Point(409, 358);
            btnKoFi.Margin = new Padding(15);
            btnKoFi.Name = "btnKoFi";
            btnKoFi.Size = new Size(363, 50);
            btnKoFi.TabIndex = 10;
            btnKoFi.Text = "Ko-Fi";
            btnKoFi.TextAlign = ContentAlignment.MiddleRight;
            btnKoFi.TextImageRelation = TextImageRelation.ImageBeforeText;
            btnKoFi.UseVisualStyleBackColor = true;
            btnKoFi.Click += btnKoFi_Click;
            // 
            // imageListGitHub
            // 
            imageListGitHub.ColorDepth = ColorDepth.Depth32Bit;
            imageListGitHub.ImageStream = (ImageListStreamer)resources.GetObject("imageListGitHub.ImageStream");
            imageListGitHub.TransparentColor = Color.Transparent;
            imageListGitHub.Images.SetKeyName(0, "selfhst--github-dark.png");
            imageListGitHub.Images.SetKeyName(1, "iconoir--coffee-cup.png");
            // 
            // labelDonate
            // 
            labelDonate.Anchor = AnchorStyles.None;
            labelDonate.AutoSize = true;
            labelDonate.Location = new Point(148, 367);
            labelDonate.Name = "labelDonate";
            labelDonate.Size = new Size(97, 32);
            labelDonate.TabIndex = 8;
            labelDonate.Text = "Donate:";
            // 
            // labelLicense
            // 
            labelLicense.Anchor = AnchorStyles.None;
            labelLicense.AutoSize = true;
            labelLicense.Location = new Point(148, 257);
            labelLicense.Name = "labelLicense";
            labelLicense.Size = new Size(97, 32);
            labelLicense.TabIndex = 6;
            labelLicense.Text = "License:";
            // 
            // labelSource
            // 
            labelSource.Anchor = AnchorStyles.None;
            labelSource.AutoSize = true;
            labelSource.Location = new Point(151, 147);
            labelSource.Name = "labelSource";
            labelSource.Size = new Size(92, 32);
            labelSource.TabIndex = 4;
            labelSource.Text = "Source:";
            // 
            // labelAuthorValue
            // 
            labelAuthorValue.Anchor = AnchorStyles.None;
            labelAuthorValue.AutoSize = true;
            labelAuthorValue.Location = new Point(451, 76);
            labelAuthorValue.Name = "labelAuthorValue";
            labelAuthorValue.Size = new Size(278, 32);
            labelAuthorValue.TabIndex = 3;
            labelAuthorValue.Text = "Jan Grabowski (panyann)";
            // 
            // labelAuthor
            // 
            labelAuthor.Anchor = AnchorStyles.None;
            labelAuthor.AutoSize = true;
            labelAuthor.Location = new Point(151, 76);
            labelAuthor.Name = "labelAuthor";
            labelAuthor.Size = new Size(92, 32);
            labelAuthor.TabIndex = 2;
            labelAuthor.Text = "Author:";
            // 
            // labelVersionValue
            // 
            labelVersionValue.Anchor = AnchorStyles.None;
            labelVersionValue.AutoSize = true;
            labelVersionValue.Location = new Point(576, 15);
            labelVersionValue.Name = "labelVersionValue";
            labelVersionValue.Size = new Size(29, 32);
            labelVersionValue.TabIndex = 1;
            labelVersionValue.Text = "...";
            // 
            // labelVersion
            // 
            labelVersion.Anchor = AnchorStyles.None;
            labelVersion.AutoSize = true;
            labelVersion.Location = new Point(148, 15);
            labelVersion.Name = "labelVersion";
            labelVersion.Size = new Size(97, 32);
            labelVersion.TabIndex = 0;
            labelVersion.Text = "Version:";
            // 
            // btnGitHub
            // 
            btnGitHub.Dock = DockStyle.Fill;
            btnGitHub.ImageKey = "selfhst--github-dark.png";
            btnGitHub.ImageList = imageListGitHub;
            btnGitHub.Location = new Point(409, 138);
            btnGitHub.Margin = new Padding(15);
            btnGitHub.Name = "btnGitHub";
            btnGitHub.Size = new Size(363, 50);
            btnGitHub.TabIndex = 5;
            btnGitHub.Text = "GitHub";
            btnGitHub.TextAlign = ContentAlignment.MiddleRight;
            btnGitHub.TextImageRelation = TextImageRelation.ImageBeforeText;
            btnGitHub.UseVisualStyleBackColor = true;
            btnGitHub.Click += btnGitHub_Click;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(linkLabelPolyFormInternalUse, 0, 0);
            tableLayoutPanel1.Controls.Add(labelOr, 0, 1);
            tableLayoutPanel1.Controls.Add(linkLabelPolyFormNoncommercial, 0, 2);
            tableLayoutPanel1.Location = new Point(397, 207);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(387, 132);
            tableLayoutPanel1.TabIndex = 9;
            // 
            // linkLabelPolyFormInternalUse
            // 
            linkLabelPolyFormInternalUse.Anchor = AnchorStyles.None;
            linkLabelPolyFormInternalUse.AutoSize = true;
            linkLabelPolyFormInternalUse.Location = new Point(70, 9);
            linkLabelPolyFormInternalUse.Name = "linkLabelPolyFormInternalUse";
            linkLabelPolyFormInternalUse.Size = new Size(247, 32);
            linkLabelPolyFormInternalUse.TabIndex = 0;
            linkLabelPolyFormInternalUse.TabStop = true;
            linkLabelPolyFormInternalUse.Text = "PolyForm Internal Use";
            linkLabelPolyFormInternalUse.LinkClicked += linkLabelPolyFormInternalUse_LinkClicked;
            // 
            // labelOr
            // 
            labelOr.Anchor = AnchorStyles.None;
            labelOr.AutoSize = true;
            labelOr.Location = new Point(175, 51);
            labelOr.Name = "labelOr";
            labelOr.Size = new Size(36, 30);
            labelOr.TabIndex = 1;
            labelOr.Text = "or";
            // 
            // linkLabelPolyFormNoncommercial
            // 
            linkLabelPolyFormNoncommercial.Anchor = AnchorStyles.None;
            linkLabelPolyFormNoncommercial.AutoSize = true;
            linkLabelPolyFormNoncommercial.Location = new Point(49, 90);
            linkLabelPolyFormNoncommercial.Name = "linkLabelPolyFormNoncommercial";
            linkLabelPolyFormNoncommercial.Size = new Size(289, 32);
            linkLabelPolyFormNoncommercial.TabIndex = 2;
            linkLabelPolyFormNoncommercial.TabStop = true;
            linkLabelPolyFormNoncommercial.Text = "PolyForm Noncommercial";
            linkLabelPolyFormNoncommercial.LinkClicked += linkLabelPolyFormNoncommercial_LinkClicked;
            // 
            // AboutForm
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(794, 639);
            Controls.Add(tableLayoutPanel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Name = "AboutForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "About VoiceToPaste";
            Shown += AboutForm_Shown;
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxIcon).EndInit();
            subTableLayoutPanel.ResumeLayout(false);
            subTableLayoutPanel.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel;
        private PictureBox pictureBoxIcon;
        private Label labelTitle;
        private TableLayoutPanel subTableLayoutPanel;
        private Label labelVersion;
        private Label labelSource;
        private Label labelAuthorValue;
        private Label labelAuthor;
        private Label labelVersionValue;
        private Button btnGitHub;
        private Label labelDonate;
        private Label labelLicense;
        private TableLayoutPanel tableLayoutPanel1;
        private LinkLabel linkLabelPolyFormInternalUse;
        private Label labelOr;
        private LinkLabel linkLabelPolyFormNoncommercial;
        private Button btnKoFi;
        private ImageList imageListGitHub;
    }
}