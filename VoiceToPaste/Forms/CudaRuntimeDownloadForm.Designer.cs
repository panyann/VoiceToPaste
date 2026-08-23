namespace VoiceToPaste.Forms
{
    partial class CudaRuntimeDownloadForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CudaRuntimeDownloadForm));
            labelDescription = new Label();
            progressBarDownload = new ProgressBar();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel = new ToolStripStatusLabel();
            btnDownload = new Button();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // labelDescription
            // 
            resources.ApplyResources(labelDescription, "labelDescription");
            labelDescription.Name = "labelDescription";
            // 
            // progressBarDownload
            // 
            resources.ApplyResources(progressBarDownload, "progressBarDownload");
            progressBarDownload.Name = "progressBarDownload";
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new Size(32, 32);
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel });
            resources.ApplyResources(statusStrip1, "statusStrip1");
            statusStrip1.Name = "statusStrip1";
            // 
            // toolStripStatusLabel
            // 
            toolStripStatusLabel.Name = "toolStripStatusLabel";
            resources.ApplyResources(toolStripStatusLabel, "toolStripStatusLabel");
            // 
            // btnDownload
            // 
            resources.ApplyResources(btnDownload, "btnDownload");
            btnDownload.Name = "btnDownload";
            btnDownload.UseVisualStyleBackColor = true;
            btnDownload.Click += btnDownload_Click;
            // 
            // CudaRuntimeDownloadForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(btnDownload);
            Controls.Add(statusStrip1);
            Controls.Add(progressBarDownload);
            Controls.Add(labelDescription);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Name = "CudaRuntimeDownloadForm";
            FormClosing += CudaRuntimeDownloadForm_FormClosing;
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelDescription;
        private ProgressBar progressBarDownload;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel toolStripStatusLabel;
        private Button btnDownload;
    }
}
