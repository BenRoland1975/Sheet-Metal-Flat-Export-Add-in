using System;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;

namespace RoesleinAddIn
{
    public class StandardsSummaryForm : Form
    {
        public StandardsSummaryForm(string summaryText, string title = "Standards Version Check")
        {
            this.Text = title;
            this.Width = 800;
            this.Height = 600;
            this.MinimumSize = new System.Drawing.Size(400, 300);
            this.StartPosition = FormStartPosition.CenterScreen;

            var resources = new ComponentResourceManager(typeof(StandardsSummaryForm));
            this.Icon = (Icon)resources.GetObject("$this.Icon");

            var textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 10),
                Text = summaryText
            };

            // Prevent text from being highlighted when the form appears
            textBox.SelectionStart = 0;
            textBox.SelectionLength = 0;

            var okButton = new Button
            {
                Text = "OK",
                Dock = DockStyle.Bottom,
                DialogResult = DialogResult.OK,
                Height = 40
            };

            this.Controls.Add(textBox);
            this.Controls.Add(okButton);
            this.AcceptButton = okButton;
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(StandardsSummaryForm));
            this.SuspendLayout();
            // 
            // StandardsSummaryForm
            // 
            this.BackgroundImage = global::RoesleinAddIn.Properties.Resources.RoesleinConnex_Small;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "StandardsSummaryForm";
            this.ResumeLayout(false);

        }
    }
} 