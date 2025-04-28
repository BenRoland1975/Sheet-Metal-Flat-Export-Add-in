using System.Windows.Forms;

namespace RoesleinAddIn
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
            this.lblHotFolder = new System.Windows.Forms.Label();
            this.txtHotFolder = new System.Windows.Forms.TextBox();
            this.btnBrowseHotFolder = new System.Windows.Forms.Button();
            this.lblHotFolderInfo = new System.Windows.Forms.Label();
            this.lblLogFile = new System.Windows.Forms.Label();
            this.txtLogFile = new System.Windows.Forms.TextBox();
            this.btnBrowseLogFile = new System.Windows.Forms.Button();
            this.chkEnableLogging = new System.Windows.Forms.CheckBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSaveMappings = new System.Windows.Forms.Button();
            this.chkShowBendLines = new System.Windows.Forms.CheckBox();
            this.lblTextLayer = new System.Windows.Forms.Label();
            this.txtTextLayer = new System.Windows.Forms.TextBox();
            this.lblBendLineLayer = new System.Windows.Forms.Label();
            this.txtBendLineLayer = new System.Windows.Forms.TextBox();
            this.lblTextLocation = new System.Windows.Forms.Label();
            this.cboTextLocation = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // lblHotFolder
            // 
            this.lblHotFolder.AutoSize = true;
            this.lblHotFolder.Location = new System.Drawing.Point(16, 20);
            this.lblHotFolder.Name = "lblHotFolder";
            this.lblHotFolder.Size = new System.Drawing.Size(156, 13);
            this.lblHotFolder.TabIndex = 0;
            this.lblHotFolder.Text = "DXF Output Folder (Hot Folder):";
            // 
            // txtHotFolder
            // 
            this.txtHotFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtHotFolder.Location = new System.Drawing.Point(19, 36);
            this.txtHotFolder.Name = "txtHotFolder";
            this.txtHotFolder.Size = new System.Drawing.Size(482, 20);
            this.txtHotFolder.TabIndex = 1;
            // 
            // btnBrowseHotFolder
            // 
            this.btnBrowseHotFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseHotFolder.Location = new System.Drawing.Point(507, 35);
            this.btnBrowseHotFolder.Name = "btnBrowseHotFolder";
            this.btnBrowseHotFolder.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseHotFolder.TabIndex = 2;
            this.btnBrowseHotFolder.Text = "Browse...";
            this.btnBrowseHotFolder.UseVisualStyleBackColor = true;
            this.btnBrowseHotFolder.Click += new System.EventHandler(this.btnBrowseHotFolder_Click);
            // 
            // lblHotFolderInfo
            // 
            this.lblHotFolderInfo.AutoSize = true;
            this.lblHotFolderInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblHotFolderInfo.Location = new System.Drawing.Point(16, 59);
            this.lblHotFolderInfo.Name = "lblHotFolderInfo";
            this.lblHotFolderInfo.Size = new System.Drawing.Size(429, 13);
            this.lblHotFolderInfo.TabIndex = 3;
            this.lblHotFolderInfo.Text = "This is the network folder that the Laser Programming software watches for new DX" +
    "F files.";
            // 
            // lblLogFile
            // 
            this.lblLogFile.AutoSize = true;
            this.lblLogFile.Location = new System.Drawing.Point(16, 100);
            this.lblLogFile.Name = "lblLogFile";
            this.lblLogFile.Size = new System.Drawing.Size(72, 13);
            this.lblLogFile.TabIndex = 4;
            this.lblLogFile.Text = "Log File Path:";
            // 
            // txtLogFile
            // 
            this.txtLogFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLogFile.Location = new System.Drawing.Point(19, 116);
            this.txtLogFile.Name = "txtLogFile";
            this.txtLogFile.Size = new System.Drawing.Size(482, 20);
            this.txtLogFile.TabIndex = 5;
            // 
            // btnBrowseLogFile
            // 
            this.btnBrowseLogFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseLogFile.Location = new System.Drawing.Point(507, 115);
            this.btnBrowseLogFile.Name = "btnBrowseLogFile";
            this.btnBrowseLogFile.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseLogFile.TabIndex = 6;
            this.btnBrowseLogFile.Text = "Browse...";
            this.btnBrowseLogFile.UseVisualStyleBackColor = true;
            this.btnBrowseLogFile.Click += new System.EventHandler(this.btnBrowseLogFile_Click);
            // 
            // chkEnableLogging
            // 
            this.chkEnableLogging.AutoSize = true;
            this.chkEnableLogging.Location = new System.Drawing.Point(19, 155);
            this.chkEnableLogging.Name = "chkEnableLogging";
            this.chkEnableLogging.Size = new System.Drawing.Size(100, 17);
            this.chkEnableLogging.TabIndex = 7;
            this.chkEnableLogging.Text = "Enable Logging";
            this.chkEnableLogging.UseVisualStyleBackColor = true;
            this.chkEnableLogging.CheckedChanged += new System.EventHandler(this.chkEnableLogging_CheckedChanged);
            // 
            // chkShowBendLines
            // 
            this.chkShowBendLines.AutoSize = true;
            this.chkShowBendLines.Location = new System.Drawing.Point(19, 180);
            this.chkShowBendLines.Name = "chkShowBendLines";
            this.chkShowBendLines.Size = new System.Drawing.Size(110, 17);
            this.chkShowBendLines.TabIndex = 11;
            this.chkShowBendLines.Text = "Show Bend Lines";
            this.chkShowBendLines.UseVisualStyleBackColor = true;
            // 
            // lblTextLayer
            // 
            this.lblTextLayer.AutoSize = true;
            this.lblTextLayer.Location = new System.Drawing.Point(16, 215);
            this.lblTextLayer.Name = "lblTextLayer";
            this.lblTextLayer.Size = new System.Drawing.Size(60, 13);
            this.lblTextLayer.TabIndex = 12;
            this.lblTextLayer.Text = "Text Layer:";
            // 
            // txtTextLayer
            // 
            this.txtTextLayer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTextLayer.Location = new System.Drawing.Point(110, 212);
            this.txtTextLayer.Name = "txtTextLayer";
            this.txtTextLayer.Size = new System.Drawing.Size(472, 20);
            this.txtTextLayer.TabIndex = 13;
            // 
            // lblBendLineLayer
            // 
            this.lblBendLineLayer.AutoSize = true;
            this.lblBendLineLayer.Location = new System.Drawing.Point(16, 245);
            this.lblBendLineLayer.Name = "lblBendLineLayer";
            this.lblBendLineLayer.Size = new System.Drawing.Size(88, 13);
            this.lblBendLineLayer.TabIndex = 14;
            this.lblBendLineLayer.Text = "Bend Line Layer:";
            // 
            // txtBendLineLayer
            // 
            this.txtBendLineLayer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtBendLineLayer.Location = new System.Drawing.Point(110, 242);
            this.txtBendLineLayer.Name = "txtBendLineLayer";
            this.txtBendLineLayer.Size = new System.Drawing.Size(472, 20);
            this.txtBendLineLayer.TabIndex = 15;
            // 
            // lblTextLocation
            // 
            this.lblTextLocation.AutoSize = true;
            this.lblTextLocation.Location = new System.Drawing.Point(190, 156);
            this.lblTextLocation.Name = "lblTextLocation";
            this.lblTextLocation.Size = new System.Drawing.Size(77, 13);
            this.lblTextLocation.TabIndex = 16;
            this.lblTextLocation.Text = "Text Location on DXF:";
            // 
            // cboTextLocation
            // 
            this.cboTextLocation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTextLocation.FormattingEnabled = true;
            this.cboTextLocation.Location = new System.Drawing.Point(273, 153);
            this.cboTextLocation.Name = "cboTextLocation";
            this.cboTextLocation.Size = new System.Drawing.Size(130, 21);
            this.cboTextLocation.TabIndex = 17;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(427, 615);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 18;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(508, 615);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 19;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnSaveMappings
            // 
            this.btnSaveMappings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveMappings.Location = new System.Drawing.Point(483, 580);
            this.btnSaveMappings.Name = "btnSaveMappings";
            this.btnSaveMappings.Size = new System.Drawing.Size(100, 23);
            this.btnSaveMappings.TabIndex = 17;
            this.btnSaveMappings.Text = "Save Mappings";
            this.btnSaveMappings.UseVisualStyleBackColor = true;
            this.btnSaveMappings.Click += new System.EventHandler(this.btnSaveMappings_Click);
            // 
            // SettingsForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(600, 650);
            this.Controls.Add(this.cboTextLocation);
            this.Controls.Add(this.lblTextLocation);
            this.Controls.Add(this.txtBendLineLayer);
            this.Controls.Add(this.lblBendLineLayer);
            this.Controls.Add(this.txtTextLayer);
            this.Controls.Add(this.lblTextLayer);
            this.Controls.Add(this.chkShowBendLines);
            this.Controls.Add(this.btnSaveMappings);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.chkEnableLogging);
            this.Controls.Add(this.btnBrowseLogFile);
            this.Controls.Add(this.txtLogFile);
            this.Controls.Add(this.lblLogFile);
            this.Controls.Add(this.lblHotFolderInfo);
            this.Controls.Add(this.btnBrowseHotFolder);
            this.Controls.Add(this.txtHotFolder);
            this.Controls.Add(this.lblHotFolder);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Roeslein Add-in Settings";
            this.ResumeLayout(false);
            this.PerformLayout();

            // Add a title label for the DataGridView
            Label lblGridTitle = new Label();
            lblGridTitle.Text = "File Properties Mapping for DXF";
            lblGridTitle.Location = new System.Drawing.Point(16, 280);
            lblGridTitle.Size = new System.Drawing.Size(200, 20);
            this.Controls.Add(lblGridTitle);
        }

        #endregion

        private System.Windows.Forms.Label lblHotFolder;
        private System.Windows.Forms.TextBox txtHotFolder;
        private System.Windows.Forms.Button btnBrowseHotFolder;
        private System.Windows.Forms.Label lblHotFolderInfo;
        private System.Windows.Forms.Label lblLogFile;
        private System.Windows.Forms.TextBox txtLogFile;
        private System.Windows.Forms.Button btnBrowseLogFile;
        private System.Windows.Forms.CheckBox chkEnableLogging;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSaveMappings;
        private System.Windows.Forms.CheckBox chkShowBendLines;
        private System.Windows.Forms.Label lblTextLayer;
        private System.Windows.Forms.TextBox txtTextLayer;
        private System.Windows.Forms.Label lblBendLineLayer;
        private System.Windows.Forms.TextBox txtBendLineLayer;
        private System.Windows.Forms.Label lblTextLocation;
        private System.Windows.Forms.ComboBox cboTextLocation;
    }
} 