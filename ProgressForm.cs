using System;
using System.Drawing;
using System.Windows.Forms;

namespace RoesleinAddIn
{
    public partial class ProgressForm : Form
    {
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label detailLabel;
        private Button cancelButton;
        private bool isCancelled = false;

        public bool IsCancelled => isCancelled;

        public ProgressForm(string title = "Processing...")
        {
            InitializeComponent();
            this.Text = title;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;
            
            // Set the icon using the same approach as StandardsSummaryForm
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(ProgressForm));
            this.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            
            // Wire up the cancel button event if it exists
            if (cancelButton != null)
            {
                cancelButton.Click += CancelButton_Click;
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            isCancelled = true;
            if (statusLabel != null)
                statusLabel.Text = "Cancelling...";
            if (cancelButton != null)
                cancelButton.Enabled = false;
        }

        private void ProgressForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isCancelled && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // Prevent closing unless cancelled
            }
        }

        public void UpdateProgress(int percentage, string status, string detail = "")
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateProgress(percentage, status, detail)));
                return;
            }

            if (progressBar != null)
                progressBar.Value = Math.Max(0, Math.Min(100, percentage));
            if (statusLabel != null)
                statusLabel.Text = status;
            if (detailLabel != null)
                detailLabel.Text = detail;
            this.Refresh();
        }

        public void SetIndeterminate(bool indeterminate)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetIndeterminate(indeterminate)));
                return;
            }

            if (progressBar != null)
                progressBar.Style = indeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        }

        public void Complete(string finalMessage = "Complete!")
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => Complete(finalMessage)));
                return;
            }

            if (progressBar != null)
                progressBar.Value = 100;
            if (statusLabel != null)
                statusLabel.Text = finalMessage;
            if (detailLabel != null)
                detailLabel.Text = "";
            if (cancelButton != null)
            {
                cancelButton.Text = "Close";
                cancelButton.Enabled = true;
            }
            
            // Auto-close after 2 seconds
            Timer closeTimer = new Timer();
            closeTimer.Interval = 2000;
            closeTimer.Tick += (s, e) => {
                closeTimer.Stop();
                this.Close();
            };
            closeTimer.Start();
        }

        public void ShowError(string errorMessage)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowError(errorMessage)));
                return;
            }

            if (statusLabel != null)
            {
                statusLabel.Text = "Error occurred";
                statusLabel.ForeColor = Color.Red;
            }
            if (detailLabel != null)
            {
                detailLabel.Text = errorMessage;
                detailLabel.ForeColor = Color.Red;
            }
            if (cancelButton != null)
            {
                cancelButton.Text = "Close";
                cancelButton.Enabled = true;
            }
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ProgressForm));
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.statusLabel = new System.Windows.Forms.Label();
            this.detailLabel = new System.Windows.Forms.Label();
            this.cancelButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(20, 20);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(440, 25);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBar.TabIndex = 0;
            // 
            // statusLabel
            // 
            this.statusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.statusLabel.Location = new System.Drawing.Point(20, 60);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(440, 25);
            this.statusLabel.TabIndex = 1;
            this.statusLabel.Text = "Initializing...";
            // 
            // detailLabel
            // 
            this.detailLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.detailLabel.ForeColor = System.Drawing.Color.Gray;
            this.detailLabel.Location = new System.Drawing.Point(20, 90);
            this.detailLabel.Name = "detailLabel";
            this.detailLabel.Size = new System.Drawing.Size(440, 25);
            this.detailLabel.TabIndex = 2;
            // 
            // cancelButton
            // 
            this.cancelButton.Location = new System.Drawing.Point(385, 130);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 30);
            this.cancelButton.TabIndex = 3;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // ProgressForm
            // 
            this.ClientSize = new System.Drawing.Size(484, 181);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.detailLabel);
            this.Controls.Add(this.cancelButton);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ProgressForm";
            this.ResumeLayout(false);

        }
    }
} 