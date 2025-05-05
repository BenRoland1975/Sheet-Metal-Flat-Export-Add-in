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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            this.lblHotFolder = new System.Windows.Forms.Label();
            this.txtHotFolder = new System.Windows.Forms.TextBox();
            this.btnBrowseHotFolder = new System.Windows.Forms.Button();
            this.btnUseDefaultHotFolder = new System.Windows.Forms.Button();
            this.lblHotFolderInfo = new System.Windows.Forms.Label();
            this.lblLogFile = new System.Windows.Forms.Label();
            this.txtLogFile = new System.Windows.Forms.TextBox();
            this.btnBrowseLogFile = new System.Windows.Forms.Button();
            this.chkEnableLogging = new System.Windows.Forms.CheckBox();
            this.chkEnableDebugLogging = new System.Windows.Forms.CheckBox();
            this.lblDebugLogFile = new System.Windows.Forms.Label();
            this.txtDebugLogFile = new System.Windows.Forms.TextBox();
            this.btnBrowseDebugLogFile = new System.Windows.Forms.Button();
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
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.tabMaterials = new System.Windows.Forms.TabPage();
            this.tabThickness = new System.Windows.Forms.TabPage();
            this.btnSaveMaterialMappings = new System.Windows.Forms.Button();
            this.btnSaveThicknessMappings = new System.Windows.Forms.Button();
            this.chkCheckForLaser = new System.Windows.Forms.CheckBox();
            this.tabControl.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblHotFolder
            // 
            this.lblHotFolder.AutoSize = true;
            this.lblHotFolder.Location = new System.Drawing.Point(6, 5);
            this.lblHotFolder.Name = "lblHotFolder";
            this.lblHotFolder.Size = new System.Drawing.Size(156, 13);
            this.lblHotFolder.TabIndex = 0;
            this.lblHotFolder.Text = "DXF Output Folder (Hot Folder):";
            // 
            // txtHotFolder
            // 
            this.txtHotFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtHotFolder.Location = new System.Drawing.Point(89, 36);
            this.txtHotFolder.Name = "txtHotFolder";
            this.txtHotFolder.Size = new System.Drawing.Size(412, 20);
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
            // btnUseDefaultHotFolder
            // 
            this.btnUseDefaultHotFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUseDefaultHotFolder.Location = new System.Drawing.Point(595, 35);
            this.btnUseDefaultHotFolder.Name = "btnUseDefaultHotFolder";
            this.btnUseDefaultHotFolder.Size = new System.Drawing.Size(90, 23);
            this.btnUseDefaultHotFolder.TabIndex = 3;
            this.btnUseDefaultHotFolder.Text = "Use Default";
            this.btnUseDefaultHotFolder.UseVisualStyleBackColor = true;
            this.btnUseDefaultHotFolder.Click += new System.EventHandler(this.btnUseDefaultHotFolder_Click);
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
            this.lblLogFile.Location = new System.Drawing.Point(16, 105);
            this.lblLogFile.Name = "lblLogFile";
            this.lblLogFile.Size = new System.Drawing.Size(72, 13);
            this.lblLogFile.TabIndex = 4;
            this.lblLogFile.Text = "Log File Path:";
            // 
            // txtLogFile
            // 
            this.txtLogFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLogFile.Location = new System.Drawing.Point(19, 121);
            this.txtLogFile.Name = "txtLogFile";
            this.txtLogFile.Size = new System.Drawing.Size(482, 20);
            this.txtLogFile.TabIndex = 5;
            // 
            // btnBrowseLogFile
            // 
            this.btnBrowseLogFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseLogFile.Location = new System.Drawing.Point(507, 120);
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
            this.chkEnableLogging.Location = new System.Drawing.Point(19, 85);
            this.chkEnableLogging.Name = "chkEnableLogging";
            this.chkEnableLogging.Size = new System.Drawing.Size(100, 17);
            this.chkEnableLogging.TabIndex = 7;
            this.chkEnableLogging.Text = "Enable Logging";
            this.chkEnableLogging.UseVisualStyleBackColor = true;
            this.chkEnableLogging.CheckedChanged += new System.EventHandler(this.chkEnableLogging_CheckedChanged);
            // 
            // chkEnableDebugLogging
            // 
            this.chkEnableDebugLogging.AutoSize = true;
            this.chkEnableDebugLogging.Location = new System.Drawing.Point(19, 160);
            this.chkEnableDebugLogging.Name = "chkEnableDebugLogging";
            this.chkEnableDebugLogging.Size = new System.Drawing.Size(135, 17);
            this.chkEnableDebugLogging.TabIndex = 8;
            this.chkEnableDebugLogging.Text = "Enable Debug Logging";
            this.chkEnableDebugLogging.UseVisualStyleBackColor = true;
            this.chkEnableDebugLogging.CheckedChanged += new System.EventHandler(this.chkEnableDebugLogging_CheckedChanged);
            // 
            // lblDebugLogFile
            // 
            this.lblDebugLogFile.AutoSize = true;
            this.lblDebugLogFile.Location = new System.Drawing.Point(16, 180);
            this.lblDebugLogFile.Name = "lblDebugLogFile";
            this.lblDebugLogFile.Size = new System.Drawing.Size(88, 13);
            this.lblDebugLogFile.TabIndex = 9;
            this.lblDebugLogFile.Text = "Debug Log Path:";
            // 
            // txtDebugLogFile
            // 
            this.txtDebugLogFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDebugLogFile.Location = new System.Drawing.Point(19, 196);
            this.txtDebugLogFile.Name = "txtDebugLogFile";
            this.txtDebugLogFile.Size = new System.Drawing.Size(482, 20);
            this.txtDebugLogFile.TabIndex = 10;
            // 
            // btnBrowseDebugLogFile
            // 
            this.btnBrowseDebugLogFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseDebugLogFile.Location = new System.Drawing.Point(507, 195);
            this.btnBrowseDebugLogFile.Name = "btnBrowseDebugLogFile";
            this.btnBrowseDebugLogFile.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseDebugLogFile.TabIndex = 11;
            this.btnBrowseDebugLogFile.Text = "Browse...";
            this.btnBrowseDebugLogFile.UseVisualStyleBackColor = true;
            this.btnBrowseDebugLogFile.Click += new System.EventHandler(this.btnBrowseDebugLogFile_Click);
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(560, 715);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(87, 23);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "&OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(653, 715);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(87, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnSaveMappings
            // 
            this.btnSaveMappings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveMappings.Location = new System.Drawing.Point(578, 620);
            this.btnSaveMappings.Name = "btnSaveMappings";
            this.btnSaveMappings.Size = new System.Drawing.Size(120, 23);
            this.btnSaveMappings.TabIndex = 17;
            this.btnSaveMappings.Text = "Save Mappings";
            this.btnSaveMappings.UseVisualStyleBackColor = true;
            this.btnSaveMappings.Click += new System.EventHandler(this.btnSaveMappings_Click);
            // 
            // chkShowBendLines
            // 
            this.chkShowBendLines.AutoSize = true;
            this.chkShowBendLines.Location = new System.Drawing.Point(19, 230);
            this.chkShowBendLines.Name = "chkShowBendLines";
            this.chkShowBendLines.Size = new System.Drawing.Size(109, 17);
            this.chkShowBendLines.TabIndex = 11;
            this.chkShowBendLines.Text = "Show Bend Lines";
            this.chkShowBendLines.UseVisualStyleBackColor = true;
            // 
            // lblTextLayer
            // 
            this.lblTextLayer.AutoSize = true;
            this.lblTextLayer.Location = new System.Drawing.Point(16, 260);
            this.lblTextLayer.Name = "lblTextLayer";
            this.lblTextLayer.Size = new System.Drawing.Size(60, 13);
            this.lblTextLayer.TabIndex = 12;
            this.lblTextLayer.Text = "Text Layer:";
            // 
            // txtTextLayer
            // 
            this.txtTextLayer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTextLayer.Location = new System.Drawing.Point(209, 257);
            this.txtTextLayer.Name = "txtTextLayer";
            this.txtTextLayer.Size = new System.Drawing.Size(292, 20);
            this.txtTextLayer.TabIndex = 13;
            // 
            // lblBendLineLayer
            // 
            this.lblBendLineLayer.AutoSize = true;
            this.lblBendLineLayer.Location = new System.Drawing.Point(16, 290);
            this.lblBendLineLayer.Name = "lblBendLineLayer";
            this.lblBendLineLayer.Size = new System.Drawing.Size(87, 13);
            this.lblBendLineLayer.TabIndex = 14;
            this.lblBendLineLayer.Text = "Bend Line Layer:";
            // 
            // txtBendLineLayer
            // 
            this.txtBendLineLayer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtBendLineLayer.Location = new System.Drawing.Point(209, 287);
            this.txtBendLineLayer.Name = "txtBendLineLayer";
            this.txtBendLineLayer.Size = new System.Drawing.Size(292, 20);
            this.txtBendLineLayer.TabIndex = 15;
            // 
            // lblTextLocation
            // 
            this.lblTextLocation.AutoSize = true;
            this.lblTextLocation.Location = new System.Drawing.Point(160, 231);
            this.lblTextLocation.Name = "lblTextLocation";
            this.lblTextLocation.Size = new System.Drawing.Size(114, 13);
            this.lblTextLocation.TabIndex = 16;
            this.lblTextLocation.Text = "Text Location on DXF:";
            // 
            // cboTextLocation
            // 
            this.cboTextLocation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTextLocation.FormattingEnabled = true;
            this.cboTextLocation.Location = new System.Drawing.Point(273, 228);
            this.cboTextLocation.Name = "cboTextLocation";
            this.cboTextLocation.Size = new System.Drawing.Size(130, 21);
            this.cboTextLocation.TabIndex = 17;
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.tabSettings);
            this.tabControl.Controls.Add(this.tabMaterials);
            this.tabControl.Controls.Add(this.tabThickness);
            this.tabControl.Location = new System.Drawing.Point(5, 12);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(733, 690);
            this.tabControl.TabIndex = 0;
            // 
            // tabSettings
            // 
            this.tabSettings.Controls.Add(this.btnUseDefaultHotFolder);
            this.tabSettings.Controls.Add(this.lblHotFolder);
            this.tabSettings.Location = new System.Drawing.Point(4, 22);
            this.tabSettings.Name = "tabSettings";
            this.tabSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabSettings.Size = new System.Drawing.Size(725, 664);
            this.tabSettings.TabIndex = 0;
            this.tabSettings.Text = "General Settings";
            this.tabSettings.UseVisualStyleBackColor = true;
            // 
            // tabMaterials
            // 
            this.tabMaterials.Location = new System.Drawing.Point(4, 22);
            this.tabMaterials.Name = "tabMaterials";
            this.tabMaterials.Padding = new System.Windows.Forms.Padding(3);
            this.tabMaterials.Size = new System.Drawing.Size(725, 664);
            this.tabMaterials.TabIndex = 1;
            this.tabMaterials.Text = "Material Mappings";
            this.tabMaterials.UseVisualStyleBackColor = true;
            // 
            // tabThickness
            // 
            this.tabThickness.Location = new System.Drawing.Point(4, 22);
            this.tabThickness.Name = "tabThickness";
            this.tabThickness.Padding = new System.Windows.Forms.Padding(3);
            this.tabThickness.Size = new System.Drawing.Size(718, 664);
            this.tabThickness.TabIndex = 2;
            this.tabThickness.Text = "Thickness Mappings";
            this.tabThickness.UseVisualStyleBackColor = true;
            // 
            // btnSaveMaterialMappings
            // 
            this.btnSaveMaterialMappings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveMaterialMappings.Location = new System.Drawing.Point(400, 620);
            this.btnSaveMaterialMappings.Name = "btnSaveMaterialMappings";
            this.btnSaveMaterialMappings.Size = new System.Drawing.Size(140, 30);
            this.btnSaveMaterialMappings.TabIndex = 1;
            this.btnSaveMaterialMappings.Text = "Save Material Mappings";
            this.btnSaveMaterialMappings.UseVisualStyleBackColor = true;
            this.btnSaveMaterialMappings.Click += new System.EventHandler(this.btnSaveMaterialMappings_Click);
            // 
            // btnSaveThicknessMappings
            // 
            this.btnSaveThicknessMappings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveThicknessMappings.Location = new System.Drawing.Point(400, 620);
            this.btnSaveThicknessMappings.Name = "btnSaveThicknessMappings";
            this.btnSaveThicknessMappings.Size = new System.Drawing.Size(140, 30);
            this.btnSaveThicknessMappings.TabIndex = 1;
            this.btnSaveThicknessMappings.Text = "Save Thickness Mappings";
            this.btnSaveThicknessMappings.UseVisualStyleBackColor = true;
            this.btnSaveThicknessMappings.Click += new System.EventHandler(this.btnSaveThicknessMappings_Click);
            // 
            // chkCheckForLaser
            // 
            this.chkCheckForLaser.AutoSize = true;
            this.chkCheckForLaser.Location = new System.Drawing.Point(24, 400);
            this.chkCheckForLaser.Name = "chkCheckForLaser";
            this.chkCheckForLaser.Size = new System.Drawing.Size(200, 17);
            this.chkCheckForLaser.TabIndex = 20;
            this.chkCheckForLaser.Text = "Check for Laser Cutting in Shop Route";
            this.chkCheckForLaser.UseVisualStyleBackColor = true;
            // 
            // SettingsForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(750, 750);
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
            this.Controls.Add(this.chkEnableDebugLogging);
            this.Controls.Add(this.btnBrowseLogFile);
            this.Controls.Add(this.txtLogFile);
            this.Controls.Add(this.lblLogFile);
            this.Controls.Add(this.lblDebugLogFile);
            this.Controls.Add(this.txtDebugLogFile);
            this.Controls.Add(this.btnBrowseDebugLogFile);
            this.Controls.Add(this.lblHotFolderInfo);
            this.Controls.Add(this.btnBrowseHotFolder);
            this.Controls.Add(this.txtHotFolder);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.chkCheckForLaser);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Roeslein Add-in Settings";
            this.tabControl.ResumeLayout(false);
            this.tabSettings.ResumeLayout(false);
            this.tabSettings.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblHotFolder;
        private System.Windows.Forms.TextBox txtHotFolder;
        private System.Windows.Forms.Button btnBrowseHotFolder;
        private System.Windows.Forms.Button btnUseDefaultHotFolder;
        private System.Windows.Forms.Label lblHotFolderInfo;
        private System.Windows.Forms.Label lblLogFile;
        private System.Windows.Forms.TextBox txtLogFile;
        private System.Windows.Forms.Button btnBrowseLogFile;
        private System.Windows.Forms.CheckBox chkEnableLogging;
        private System.Windows.Forms.CheckBox chkEnableDebugLogging;
        private System.Windows.Forms.Label lblDebugLogFile;
        private System.Windows.Forms.TextBox txtDebugLogFile;
        private System.Windows.Forms.Button btnBrowseDebugLogFile;
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
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabSettings;
        private System.Windows.Forms.TabPage tabMaterials;
        private System.Windows.Forms.Button btnSaveMaterialMappings;
        private System.Windows.Forms.TabPage tabThickness;
        private System.Windows.Forms.Button btnSaveThicknessMappings;
        private System.Windows.Forms.CheckBox chkCheckForLaser;
    }
} 