namespace RoesleinAddIn
{
    partial class StructureFbdiOptionsForm
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
            this.groupBoxOrganization = new System.Windows.Forms.GroupBox();
            this.checkBoxLifeCycleKaskaskia = new System.Windows.Forms.CheckBox();
            this.checkBoxRBRandolph = new System.Windows.Forms.CheckBox();
            this.checkBoxRBKaskaskia = new System.Windows.Forms.CheckBox();
            this.checkBoxRoesleinMaster = new System.Windows.Forms.CheckBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelInstruction = new System.Windows.Forms.Label();
            this.groupBoxOrganization.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxOrganization
            // 
            this.groupBoxOrganization.Controls.Add(this.checkBoxLifeCycleKaskaskia);
            this.groupBoxOrganization.Controls.Add(this.checkBoxRBRandolph);
            this.groupBoxOrganization.Controls.Add(this.checkBoxRBKaskaskia);
            this.groupBoxOrganization.Controls.Add(this.checkBoxRoesleinMaster);
            this.groupBoxOrganization.Location = new System.Drawing.Point(20, 50);
            this.groupBoxOrganization.Name = "groupBoxOrganization";
            this.groupBoxOrganization.Size = new System.Drawing.Size(350, 140);
            this.groupBoxOrganization.TabIndex = 0;
            this.groupBoxOrganization.TabStop = false;
            this.groupBoxOrganization.Text = "Organization (Select one or more)";
            // 
            // checkBoxLifeCycleKaskaskia
            // 
            this.checkBoxLifeCycleKaskaskia.AutoSize = true;
            this.checkBoxLifeCycleKaskaskia.Location = new System.Drawing.Point(20, 105);
            this.checkBoxLifeCycleKaskaskia.Name = "checkBoxLifeCycleKaskaskia";
            this.checkBoxLifeCycleKaskaskia.Size = new System.Drawing.Size(176, 17);
            this.checkBoxLifeCycleKaskaskia.TabIndex = 3;
            this.checkBoxLifeCycleKaskaskia.Text = "Life Cycle Kaskaskia (LCYKSK)";
            this.checkBoxLifeCycleKaskaskia.UseVisualStyleBackColor = true;
            // 
            // checkBoxRBRandolph
            // 
            this.checkBoxRBRandolph.AutoSize = true;
            this.checkBoxRBRandolph.Location = new System.Drawing.Point(20, 80);
            this.checkBoxRBRandolph.Name = "checkBoxRBRandolph";
            this.checkBoxRBRandolph.Size = new System.Drawing.Size(140, 17);
            this.checkBoxRBRandolph.TabIndex = 2;
            this.checkBoxRBRandolph.Text = "RB Randolph (RMF401)";
            this.checkBoxRBRandolph.UseVisualStyleBackColor = true;
            // 
            // checkBoxRBKaskaskia
            // 
            this.checkBoxRBKaskaskia.AutoSize = true;
            this.checkBoxRBKaskaskia.Location = new System.Drawing.Point(20, 55);
            this.checkBoxRBKaskaskia.Name = "checkBoxRBKaskaskia";
            this.checkBoxRBKaskaskia.Size = new System.Drawing.Size(137, 17);
            this.checkBoxRBKaskaskia.TabIndex = 1;
            this.checkBoxRBKaskaskia.Text = "RB Kaskaskia (RMFKK)";
            this.checkBoxRBKaskaskia.UseVisualStyleBackColor = true;
            // 
            // checkBoxRoesleinMaster
            // 
            this.checkBoxRoesleinMaster.AutoSize = true;
            this.checkBoxRoesleinMaster.Location = new System.Drawing.Point(20, 30);
            this.checkBoxRoesleinMaster.Name = "checkBoxRoesleinMaster";
            this.checkBoxRoesleinMaster.Size = new System.Drawing.Size(184, 17);
            this.checkBoxRoesleinMaster.TabIndex = 0;
            this.checkBoxRoesleinMaster.Text = "Roeslein Master (ROESLEIN_MASTER)";
            this.checkBoxRoesleinMaster.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(215, 210);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 30);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(295, 210);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 30);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // labelInstruction
            // 
            this.labelInstruction.AutoSize = true;
            this.labelInstruction.Location = new System.Drawing.Point(20, 20);
            this.labelInstruction.Name = "labelInstruction";
            this.labelInstruction.Size = new System.Drawing.Size(320, 13);
            this.labelInstruction.TabIndex = 3;
            this.labelInstruction.Text = "Select one or more organizations for the Structure FBDI export:";
            // 
            // StructureFbdiOptionsForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(390, 260);
            this.Controls.Add(this.labelInstruction);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.groupBoxOrganization);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StructureFbdiOptionsForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Oracle FBDI Structure Export Options";
            this.groupBoxOrganization.ResumeLayout(false);
            this.groupBoxOrganization.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxOrganization;
        private System.Windows.Forms.CheckBox checkBoxLifeCycleKaskaskia;
        private System.Windows.Forms.CheckBox checkBoxRBRandolph;
        private System.Windows.Forms.CheckBox checkBoxRBKaskaskia;
        private System.Windows.Forms.CheckBox checkBoxRoesleinMaster;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label labelInstruction;
    }
} 