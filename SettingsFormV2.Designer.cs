namespace RoesleinAddIn
{
    partial class SettingsFormV2
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsFormV2));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.GeneralSettings = new System.Windows.Forms.TabPage();
            this.chkCheckForLaser = new System.Windows.Forms.CheckBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.lblDXFTextMapping = new System.Windows.Forms.Label();
            this.btnAddNewRow = new System.Windows.Forms.Button();
            this.btnSaveMappings = new System.Windows.Forms.Button();
            this.dgvPropertyMappings = new System.Windows.Forms.DataGridView();
            this.txtBendLineLayer = new System.Windows.Forms.TextBox();
            this.lblBendLineLayer = new System.Windows.Forms.Label();
            this.txtTextLayer = new System.Windows.Forms.TextBox();
            this.lblTextLayer = new System.Windows.Forms.Label();
            this.cboTextLocation = new System.Windows.Forms.ComboBox();
            this.lblTextLocation = new System.Windows.Forms.Label();
            this.chkShowBendLines = new System.Windows.Forms.CheckBox();
            this.chkEnableDebugLogging = new System.Windows.Forms.CheckBox();
            this.lblDebugLogFile = new System.Windows.Forms.Label();
            this.txtDebugLogFile = new System.Windows.Forms.TextBox();
            this.btnBrowseDebugLogFile = new System.Windows.Forms.Button();
            this.chkEnableLogging = new System.Windows.Forms.CheckBox();
            this.btnBrowseLogFile = new System.Windows.Forms.Button();
            this.txtLogFile = new System.Windows.Forms.TextBox();
            this.lblLogFile = new System.Windows.Forms.Label();
            this.lblDrawingTemplate = new System.Windows.Forms.Label();
            this.BtnBrowseDrawingTemplate = new System.Windows.Forms.Button();
            this.txtDrawingTemplate = new System.Windows.Forms.TextBox();
            this.btnUseDefaultHotFolder = new System.Windows.Forms.Button();
            this.lblHotFolderInfo = new System.Windows.Forms.Label();
            this.lblHotFolder = new System.Windows.Forms.Label();
            this.btnBrowseHotFolder = new System.Windows.Forms.Button();
            this.txtHotFolder = new System.Windows.Forms.TextBox();
            this.MaterialMappings = new System.Windows.Forms.TabPage();
            this.BtnAddNewRowMaterial = new System.Windows.Forms.Button();
            this.btnSaveMappingsMaterial = new System.Windows.Forms.Button();
            this.dgvMaterialMapping = new System.Windows.Forms.DataGridView();
            this.lblMaterialCrossRefernceTable = new System.Windows.Forms.Label();
            this.ThicknessMappings = new System.Windows.Forms.TabPage();
            this.BtnAddNewThickness = new System.Windows.Forms.Button();
            this.btnSaveThickness = new System.Windows.Forms.Button();
            this.dgvThicknessMapping = new System.Windows.Forms.DataGridView();
            this.lblThicknessCrossRefernceTable = new System.Windows.Forms.Label();
            this.tpStandardsSettings = new System.Windows.Forms.TabPage();
            this.lblDefautFileProp = new System.Windows.Forms.Label();
            this.BtnAddNewFileProp = new System.Windows.Forms.Button();
            this.btnSaveFileProp = new System.Windows.Forms.Button();
            this.dgvPropertyStandards = new System.Windows.Forms.DataGridView();
            this.tpRawMaterial = new System.Windows.Forms.TabPage();
            this.lblRawSheet = new System.Windows.Forms.Label();
            this.BtnAddNewRawSheet = new System.Windows.Forms.Button();
            this.btnSaveRawSheet = new System.Windows.Forms.Button();
            this.dgvRawLinear = new System.Windows.Forms.DataGridView();
            this.dgvRawSheet = new System.Windows.Forms.DataGridView();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.lblRosleinVersonLabel = new System.Windows.Forms.Label();
            this.lblVersionNumber = new System.Windows.Forms.Label();
            this.lblCopyright = new System.Windows.Forms.Label();
            this.txtSettingsVersion = new System.Windows.Forms.TextBox();
            this.lblSettingsVersion = new System.Windows.Forms.Label();
            this.lblSettingsVersionNote = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.GeneralSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPropertyMappings)).BeginInit();
            this.MaterialMappings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMaterialMapping)).BeginInit();
            this.ThicknessMappings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvThicknessMapping)).BeginInit();
            this.tpStandardsSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPropertyStandards)).BeginInit();
            this.tpRawMaterial.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRawLinear)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRawSheet)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.GeneralSettings);
            this.tabControl1.Controls.Add(this.MaterialMappings);
            this.tabControl1.Controls.Add(this.ThicknessMappings);
            this.tabControl1.Controls.Add(this.tpStandardsSettings);
            this.tabControl1.Controls.Add(this.tpRawMaterial);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(800, 500);
            this.tabControl1.TabIndex = 0;
            // 
            // GeneralSettings
            // 
            this.GeneralSettings.Controls.Add(this.chkCheckForLaser);
            this.GeneralSettings.Controls.Add(this.pictureBox1);
            this.GeneralSettings.Controls.Add(this.lblDXFTextMapping);
            this.GeneralSettings.Controls.Add(this.btnAddNewRow);
            this.GeneralSettings.Controls.Add(this.btnSaveMappings);
            this.GeneralSettings.Controls.Add(this.dgvPropertyMappings);
            this.GeneralSettings.Controls.Add(this.txtBendLineLayer);
            this.GeneralSettings.Controls.Add(this.lblBendLineLayer);
            this.GeneralSettings.Controls.Add(this.txtTextLayer);
            this.GeneralSettings.Controls.Add(this.lblTextLayer);
            this.GeneralSettings.Controls.Add(this.cboTextLocation);
            this.GeneralSettings.Controls.Add(this.lblTextLocation);
            this.GeneralSettings.Controls.Add(this.chkShowBendLines);
            this.GeneralSettings.Controls.Add(this.chkEnableDebugLogging);
            this.GeneralSettings.Controls.Add(this.lblDebugLogFile);
            this.GeneralSettings.Controls.Add(this.txtDebugLogFile);
            this.GeneralSettings.Controls.Add(this.btnBrowseDebugLogFile);
            this.GeneralSettings.Controls.Add(this.chkEnableLogging);
            this.GeneralSettings.Controls.Add(this.btnBrowseLogFile);
            this.GeneralSettings.Controls.Add(this.txtLogFile);
            this.GeneralSettings.Controls.Add(this.lblLogFile);
            this.GeneralSettings.Controls.Add(this.lblDrawingTemplate);
            this.GeneralSettings.Controls.Add(this.BtnBrowseDrawingTemplate);
            this.GeneralSettings.Controls.Add(this.txtDrawingTemplate);
            this.GeneralSettings.Controls.Add(this.btnUseDefaultHotFolder);
            this.GeneralSettings.Controls.Add(this.lblHotFolderInfo);
            this.GeneralSettings.Controls.Add(this.lblHotFolder);
            this.GeneralSettings.Controls.Add(this.btnBrowseHotFolder);
            this.GeneralSettings.Controls.Add(this.txtHotFolder);
            this.GeneralSettings.Location = new System.Drawing.Point(4, 22);
            this.GeneralSettings.Name = "GeneralSettings";
            this.GeneralSettings.Padding = new System.Windows.Forms.Padding(3);
            this.GeneralSettings.Size = new System.Drawing.Size(792, 474);
            this.GeneralSettings.TabIndex = 0;
            this.GeneralSettings.Text = "General Settings";
            this.GeneralSettings.UseVisualStyleBackColor = true;
            // 
            // chkCheckForLaser
            // 
            this.chkCheckForLaser.AutoSize = true;
            this.chkCheckForLaser.Checked = true;
            this.chkCheckForLaser.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCheckForLaser.Location = new System.Drawing.Point(446, 183);
            this.chkCheckForLaser.Name = "chkCheckForLaser";
            this.chkCheckForLaser.Size = new System.Drawing.Size(147, 17);
            this.chkCheckForLaser.TabIndex = 31;
            this.chkCheckForLaser.Text = "Check For Laser in Route";
            this.chkCheckForLaser.UseVisualStyleBackColor = true;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::RoesleinAddIn.Properties.Resources.RoesleinConnex_Small;
            this.pictureBox1.Location = new System.Drawing.Point(631, 53);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(115, 76);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 30;
            this.pictureBox1.TabStop = false;
            // 
            // lblDXFTextMapping
            // 
            this.lblDXFTextMapping.AutoSize = true;
            this.lblDXFTextMapping.ImageAlign = System.Drawing.ContentAlignment.BottomLeft;
            this.lblDXFTextMapping.Location = new System.Drawing.Point(167, 268);
            this.lblDXFTextMapping.Name = "lblDXFTextMapping";
            this.lblDXFTextMapping.Size = new System.Drawing.Size(177, 13);
            this.lblDXFTextMapping.TabIndex = 29;
            this.lblDXFTextMapping.Text = "File Properties to DXF Text Mapping";
            this.lblDXFTextMapping.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnAddNewRow
            // 
            this.btnAddNewRow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddNewRow.Location = new System.Drawing.Point(170, 445);
            this.btnAddNewRow.Name = "btnAddNewRow";
            this.btnAddNewRow.Size = new System.Drawing.Size(120, 23);
            this.btnAddNewRow.TabIndex = 28;
            this.btnAddNewRow.Text = "Add New Row";
            this.btnAddNewRow.UseVisualStyleBackColor = true;
            // 
            // btnSaveMappings
            // 
            this.btnSaveMappings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveMappings.Location = new System.Drawing.Point(505, 445);
            this.btnSaveMappings.Name = "btnSaveMappings";
            this.btnSaveMappings.Size = new System.Drawing.Size(120, 23);
            this.btnSaveMappings.TabIndex = 27;
            this.btnSaveMappings.Text = "Save Mappings";
            this.btnSaveMappings.UseVisualStyleBackColor = true;
            // 
            // dgvPropertyMappings
            // 
            this.dgvPropertyMappings.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPropertyMappings.Location = new System.Drawing.Point(170, 286);
            this.dgvPropertyMappings.Name = "dgvPropertyMappings";
            this.dgvPropertyMappings.Size = new System.Drawing.Size(455, 155);
            this.dgvPropertyMappings.TabIndex = 26;
            // 
            // txtBendLineLayer
            // 
            this.txtBendLineLayer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtBendLineLayer.Location = new System.Drawing.Point(170, 238);
            this.txtBendLineLayer.Name = "txtBendLineLayer";
            this.txtBendLineLayer.Size = new System.Drawing.Size(374, 20);
            this.txtBendLineLayer.TabIndex = 25;
            // 
            // lblBendLineLayer
            // 
            this.lblBendLineLayer.AutoSize = true;
            this.lblBendLineLayer.Location = new System.Drawing.Point(72, 241);
            this.lblBendLineLayer.Name = "lblBendLineLayer";
            this.lblBendLineLayer.Size = new System.Drawing.Size(87, 13);
            this.lblBendLineLayer.TabIndex = 24;
            this.lblBendLineLayer.Text = "Bend Line Layer:";
            this.lblBendLineLayer.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtTextLayer
            // 
            this.txtTextLayer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTextLayer.Location = new System.Drawing.Point(170, 208);
            this.txtTextLayer.Name = "txtTextLayer";
            this.txtTextLayer.Size = new System.Drawing.Size(374, 20);
            this.txtTextLayer.TabIndex = 23;
            // 
            // lblTextLayer
            // 
            this.lblTextLayer.AutoSize = true;
            this.lblTextLayer.Location = new System.Drawing.Point(99, 211);
            this.lblTextLayer.Name = "lblTextLayer";
            this.lblTextLayer.Size = new System.Drawing.Size(60, 13);
            this.lblTextLayer.TabIndex = 22;
            this.lblTextLayer.Text = "Text Layer:";
            this.lblTextLayer.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // cboTextLocation
            // 
            this.cboTextLocation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTextLocation.FormattingEnabled = true;
            this.cboTextLocation.Location = new System.Drawing.Point(170, 181);
            this.cboTextLocation.Name = "cboTextLocation";
            this.cboTextLocation.Size = new System.Drawing.Size(130, 21);
            this.cboTextLocation.TabIndex = 21;
            // 
            // lblTextLocation
            // 
            this.lblTextLocation.AutoSize = true;
            this.lblTextLocation.Location = new System.Drawing.Point(45, 185);
            this.lblTextLocation.Name = "lblTextLocation";
            this.lblTextLocation.Size = new System.Drawing.Size(114, 13);
            this.lblTextLocation.TabIndex = 20;
            this.lblTextLocation.Text = "Text Location on DXF:";
            this.lblTextLocation.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // chkShowBendLines
            // 
            this.chkShowBendLines.AutoSize = true;
            this.chkShowBendLines.Checked = true;
            this.chkShowBendLines.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkShowBendLines.Location = new System.Drawing.Point(316, 183);
            this.chkShowBendLines.Name = "chkShowBendLines";
            this.chkShowBendLines.Size = new System.Drawing.Size(109, 17);
            this.chkShowBendLines.TabIndex = 19;
            this.chkShowBendLines.Text = "Show Bend Lines";
            this.chkShowBendLines.UseVisualStyleBackColor = true;
            // 
            // chkEnableDebugLogging
            // 
            this.chkEnableDebugLogging.AutoSize = true;
            this.chkEnableDebugLogging.Location = new System.Drawing.Point(170, 127);
            this.chkEnableDebugLogging.Name = "chkEnableDebugLogging";
            this.chkEnableDebugLogging.Size = new System.Drawing.Size(135, 17);
            this.chkEnableDebugLogging.TabIndex = 15;
            this.chkEnableDebugLogging.Text = "Enable Debug Logging";
            this.chkEnableDebugLogging.UseVisualStyleBackColor = true;
            // 
            // lblDebugLogFile
            // 
            this.lblDebugLogFile.AutoSize = true;
            this.lblDebugLogFile.Location = new System.Drawing.Point(71, 154);
            this.lblDebugLogFile.Name = "lblDebugLogFile";
            this.lblDebugLogFile.Size = new System.Drawing.Size(88, 13);
            this.lblDebugLogFile.TabIndex = 16;
            this.lblDebugLogFile.Text = "Debug Log Path:";
            this.lblDebugLogFile.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtDebugLogFile
            // 
            this.txtDebugLogFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDebugLogFile.Location = new System.Drawing.Point(170, 150);
            this.txtDebugLogFile.Name = "txtDebugLogFile";
            this.txtDebugLogFile.Size = new System.Drawing.Size(374, 20);
            this.txtDebugLogFile.TabIndex = 17;
            // 
            // btnBrowseDebugLogFile
            // 
            this.btnBrowseDebugLogFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseDebugLogFile.Location = new System.Drawing.Point(550, 148);
            this.btnBrowseDebugLogFile.Name = "btnBrowseDebugLogFile";
            this.btnBrowseDebugLogFile.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseDebugLogFile.TabIndex = 18;
            this.btnBrowseDebugLogFile.Text = "Browse...";
            this.btnBrowseDebugLogFile.UseVisualStyleBackColor = true;
            // 
            // chkEnableLogging
            // 
            this.chkEnableLogging.AutoSize = true;
            this.chkEnableLogging.Location = new System.Drawing.Point(170, 81);
            this.chkEnableLogging.Name = "chkEnableLogging";
            this.chkEnableLogging.Size = new System.Drawing.Size(100, 17);
            this.chkEnableLogging.TabIndex = 14;
            this.chkEnableLogging.Text = "Enable Logging";
            this.chkEnableLogging.UseVisualStyleBackColor = true;
            // 
            // btnBrowseLogFile
            // 
            this.btnBrowseLogFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseLogFile.Location = new System.Drawing.Point(550, 98);
            this.btnBrowseLogFile.Name = "btnBrowseLogFile";
            this.btnBrowseLogFile.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseLogFile.TabIndex = 13;
            this.btnBrowseLogFile.Text = "Browse...";
            this.btnBrowseLogFile.UseVisualStyleBackColor = true;
            // 
            // txtLogFile
            // 
            this.txtLogFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLogFile.Location = new System.Drawing.Point(170, 99);
            this.txtLogFile.Name = "txtLogFile";
            this.txtLogFile.Size = new System.Drawing.Size(374, 20);
            this.txtLogFile.TabIndex = 12;
            // 
            // lblLogFile
            // 
            this.lblLogFile.AutoSize = true;
            this.lblLogFile.Location = new System.Drawing.Point(87, 103);
            this.lblLogFile.Name = "lblLogFile";
            this.lblLogFile.Size = new System.Drawing.Size(72, 13);
            this.lblLogFile.TabIndex = 11;
            this.lblLogFile.Text = "Log File Path:";
            this.lblLogFile.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDrawingTemplate
            // 
            this.lblDrawingTemplate.AutoSize = true;
            this.lblDrawingTemplate.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblDrawingTemplate.Location = new System.Drawing.Point(63, 58);
            this.lblDrawingTemplate.Name = "lblDrawingTemplate";
            this.lblDrawingTemplate.Size = new System.Drawing.Size(96, 13);
            this.lblDrawingTemplate.TabIndex = 10;
            this.lblDrawingTemplate.Text = "Drawing Template:";
            this.lblDrawingTemplate.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // BtnBrowseDrawingTemplate
            // 
            this.BtnBrowseDrawingTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.BtnBrowseDrawingTemplate.Location = new System.Drawing.Point(550, 53);
            this.BtnBrowseDrawingTemplate.Name = "BtnBrowseDrawingTemplate";
            this.BtnBrowseDrawingTemplate.Size = new System.Drawing.Size(75, 23);
            this.BtnBrowseDrawingTemplate.TabIndex = 9;
            this.BtnBrowseDrawingTemplate.Text = "Browse...";
            this.BtnBrowseDrawingTemplate.UseVisualStyleBackColor = true;
            // 
            // txtDrawingTemplate
            // 
            this.txtDrawingTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDrawingTemplate.Location = new System.Drawing.Point(170, 54);
            this.txtDrawingTemplate.Name = "txtDrawingTemplate";
            this.txtDrawingTemplate.Size = new System.Drawing.Size(374, 20);
            this.txtDrawingTemplate.TabIndex = 8;
            // 
            // btnUseDefaultHotFolder
            // 
            this.btnUseDefaultHotFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUseDefaultHotFolder.Location = new System.Drawing.Point(631, 26);
            this.btnUseDefaultHotFolder.Name = "btnUseDefaultHotFolder";
            this.btnUseDefaultHotFolder.Size = new System.Drawing.Size(90, 23);
            this.btnUseDefaultHotFolder.TabIndex = 7;
            this.btnUseDefaultHotFolder.Text = "Use Default";
            this.btnUseDefaultHotFolder.UseVisualStyleBackColor = true;
            // 
            // lblHotFolderInfo
            // 
            this.lblHotFolderInfo.AutoSize = true;
            this.lblHotFolderInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblHotFolderInfo.Location = new System.Drawing.Point(152, 6);
            this.lblHotFolderInfo.Name = "lblHotFolderInfo";
            this.lblHotFolderInfo.Size = new System.Drawing.Size(429, 13);
            this.lblHotFolderInfo.TabIndex = 6;
            this.lblHotFolderInfo.Text = "This is the network folder that the Laser Programming software watches for new DX" +
    "F files.";
            // 
            // lblHotFolder
            // 
            this.lblHotFolder.AutoSize = true;
            this.lblHotFolder.Location = new System.Drawing.Point(3, 31);
            this.lblHotFolder.Name = "lblHotFolder";
            this.lblHotFolder.Size = new System.Drawing.Size(156, 13);
            this.lblHotFolder.TabIndex = 5;
            this.lblHotFolder.Text = "DXF Output Folder (Hot Folder):";
            this.lblHotFolder.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // btnBrowseHotFolder
            // 
            this.btnBrowseHotFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseHotFolder.Location = new System.Drawing.Point(550, 26);
            this.btnBrowseHotFolder.Name = "btnBrowseHotFolder";
            this.btnBrowseHotFolder.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseHotFolder.TabIndex = 4;
            this.btnBrowseHotFolder.Text = "Browse...";
            this.btnBrowseHotFolder.UseVisualStyleBackColor = true;
            // 
            // txtHotFolder
            // 
            this.txtHotFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtHotFolder.Location = new System.Drawing.Point(170, 27);
            this.txtHotFolder.Name = "txtHotFolder";
            this.txtHotFolder.Size = new System.Drawing.Size(374, 20);
            this.txtHotFolder.TabIndex = 3;
            // 
            // MaterialMappings
            // 
            this.MaterialMappings.Controls.Add(this.BtnAddNewRowMaterial);
            this.MaterialMappings.Controls.Add(this.btnSaveMappingsMaterial);
            this.MaterialMappings.Controls.Add(this.dgvMaterialMapping);
            this.MaterialMappings.Controls.Add(this.lblMaterialCrossRefernceTable);
            this.MaterialMappings.Location = new System.Drawing.Point(4, 22);
            this.MaterialMappings.Name = "MaterialMappings";
            this.MaterialMappings.Padding = new System.Windows.Forms.Padding(3);
            this.MaterialMappings.Size = new System.Drawing.Size(792, 474);
            this.MaterialMappings.TabIndex = 1;
            this.MaterialMappings.Text = "Material Mappings";
            this.MaterialMappings.UseVisualStyleBackColor = true;
            // 
            // BtnAddNewRowMaterial
            // 
            this.BtnAddNewRowMaterial.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.BtnAddNewRowMaterial.Location = new System.Drawing.Point(28, 414);
            this.BtnAddNewRowMaterial.Name = "BtnAddNewRowMaterial";
            this.BtnAddNewRowMaterial.Size = new System.Drawing.Size(120, 23);
            this.BtnAddNewRowMaterial.TabIndex = 30;
            this.BtnAddNewRowMaterial.Text = "Add New Row";
            this.BtnAddNewRowMaterial.UseVisualStyleBackColor = true;
            // 
            // btnSaveMappingsMaterial
            // 
            this.btnSaveMappingsMaterial.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveMappingsMaterial.Location = new System.Drawing.Point(646, 414);
            this.btnSaveMappingsMaterial.Name = "btnSaveMappingsMaterial";
            this.btnSaveMappingsMaterial.Size = new System.Drawing.Size(120, 23);
            this.btnSaveMappingsMaterial.TabIndex = 29;
            this.btnSaveMappingsMaterial.Text = "Save Mappings";
            this.btnSaveMappingsMaterial.UseVisualStyleBackColor = true;
            // 
            // dgvMaterialMapping
            // 
            this.dgvMaterialMapping.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvMaterialMapping.Location = new System.Drawing.Point(28, 29);
            this.dgvMaterialMapping.Name = "dgvMaterialMapping";
            this.dgvMaterialMapping.Size = new System.Drawing.Size(738, 379);
            this.dgvMaterialMapping.TabIndex = 26;
            // 
            // lblMaterialCrossRefernceTable
            // 
            this.lblMaterialCrossRefernceTable.AutoSize = true;
            this.lblMaterialCrossRefernceTable.Location = new System.Drawing.Point(25, 12);
            this.lblMaterialCrossRefernceTable.Name = "lblMaterialCrossRefernceTable";
            this.lblMaterialCrossRefernceTable.Size = new System.Drawing.Size(150, 13);
            this.lblMaterialCrossRefernceTable.TabIndex = 25;
            this.lblMaterialCrossRefernceTable.Text = "Material Cross Refernce Table";
            this.lblMaterialCrossRefernceTable.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // ThicknessMappings
            // 
            this.ThicknessMappings.Controls.Add(this.BtnAddNewThickness);
            this.ThicknessMappings.Controls.Add(this.btnSaveThickness);
            this.ThicknessMappings.Controls.Add(this.dgvThicknessMapping);
            this.ThicknessMappings.Controls.Add(this.lblThicknessCrossRefernceTable);
            this.ThicknessMappings.Location = new System.Drawing.Point(4, 22);
            this.ThicknessMappings.Name = "ThicknessMappings";
            this.ThicknessMappings.Padding = new System.Windows.Forms.Padding(3);
            this.ThicknessMappings.Size = new System.Drawing.Size(792, 474);
            this.ThicknessMappings.TabIndex = 2;
            this.ThicknessMappings.Text = "Thickness Mappings";
            this.ThicknessMappings.UseVisualStyleBackColor = true;
            // 
            // BtnAddNewThickness
            // 
            this.BtnAddNewThickness.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.BtnAddNewThickness.Location = new System.Drawing.Point(23, 415);
            this.BtnAddNewThickness.Name = "BtnAddNewThickness";
            this.BtnAddNewThickness.Size = new System.Drawing.Size(120, 23);
            this.BtnAddNewThickness.TabIndex = 34;
            this.BtnAddNewThickness.Text = "Add New Row";
            this.BtnAddNewThickness.UseVisualStyleBackColor = true;
            // 
            // btnSaveThickness
            // 
            this.btnSaveThickness.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveThickness.Location = new System.Drawing.Point(641, 415);
            this.btnSaveThickness.Name = "btnSaveThickness";
            this.btnSaveThickness.Size = new System.Drawing.Size(120, 23);
            this.btnSaveThickness.TabIndex = 33;
            this.btnSaveThickness.Text = "Save Mappings";
            this.btnSaveThickness.UseVisualStyleBackColor = true;
            // 
            // dgvThicknessMapping
            // 
            this.dgvThicknessMapping.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvThicknessMapping.Location = new System.Drawing.Point(28, 29);
            this.dgvThicknessMapping.Name = "dgvThicknessMapping";
            this.dgvThicknessMapping.Size = new System.Drawing.Size(738, 379);
            this.dgvThicknessMapping.TabIndex = 32;
            // 
            // lblThicknessCrossRefernceTable
            // 
            this.lblThicknessCrossRefernceTable.AutoSize = true;
            this.lblThicknessCrossRefernceTable.Location = new System.Drawing.Point(20, 13);
            this.lblThicknessCrossRefernceTable.Name = "lblThicknessCrossRefernceTable";
            this.lblThicknessCrossRefernceTable.Size = new System.Drawing.Size(162, 13);
            this.lblThicknessCrossRefernceTable.TabIndex = 31;
            this.lblThicknessCrossRefernceTable.Text = "Thickness Cross Refernce Table";
            this.lblThicknessCrossRefernceTable.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // tpStandardsSettings
            // 
            this.tpStandardsSettings.Controls.Add(this.lblSettingsVersionNote);
            this.tpStandardsSettings.Controls.Add(this.lblSettingsVersion);
            this.tpStandardsSettings.Controls.Add(this.txtSettingsVersion);
            this.tpStandardsSettings.Controls.Add(this.lblDefautFileProp);
            this.tpStandardsSettings.Controls.Add(this.BtnAddNewFileProp);
            this.tpStandardsSettings.Controls.Add(this.btnSaveFileProp);
            this.tpStandardsSettings.Controls.Add(this.dgvPropertyStandards);
            this.tpStandardsSettings.Location = new System.Drawing.Point(4, 22);
            this.tpStandardsSettings.Name = "tpStandardsSettings";
            this.tpStandardsSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tpStandardsSettings.Size = new System.Drawing.Size(792, 474);
            this.tpStandardsSettings.TabIndex = 3;
            this.tpStandardsSettings.Text = "Standards Settings";
            this.tpStandardsSettings.UseVisualStyleBackColor = true;
            // 
            // lblDefautFileProp
            // 
            this.lblDefautFileProp.AutoSize = true;
            this.lblDefautFileProp.Location = new System.Drawing.Point(13, 30);
            this.lblDefautFileProp.Name = "lblDefautFileProp";
            this.lblDefautFileProp.Size = new System.Drawing.Size(113, 13);
            this.lblDefautFileProp.TabIndex = 31;
            this.lblDefautFileProp.Text = "Default File Properties:";
            // 
            // BtnAddNewFileProp
            // 
            this.BtnAddNewFileProp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.BtnAddNewFileProp.Location = new System.Drawing.Point(13, 407);
            this.BtnAddNewFileProp.Name = "BtnAddNewFileProp";
            this.BtnAddNewFileProp.Size = new System.Drawing.Size(120, 23);
            this.BtnAddNewFileProp.TabIndex = 30;
            this.BtnAddNewFileProp.Text = "Add New Row";
            this.BtnAddNewFileProp.UseVisualStyleBackColor = true;
            // 
            // btnSaveFileProp
            // 
            this.btnSaveFileProp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveFileProp.Location = new System.Drawing.Point(664, 407);
            this.btnSaveFileProp.Name = "btnSaveFileProp";
            this.btnSaveFileProp.Size = new System.Drawing.Size(120, 23);
            this.btnSaveFileProp.TabIndex = 29;
            this.btnSaveFileProp.Text = "Save File Property";
            this.btnSaveFileProp.UseVisualStyleBackColor = true;
            // 
            // dgvPropertyStandards
            // 
            this.dgvPropertyStandards.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPropertyStandards.Location = new System.Drawing.Point(12, 49);
            this.dgvPropertyStandards.Name = "dgvPropertyStandards";
            this.dgvPropertyStandards.Size = new System.Drawing.Size(772, 352);
            this.dgvPropertyStandards.TabIndex = 0;
            // 
            // tpRawMaterial
            // 
            this.tpRawMaterial.Controls.Add(this.lblRawSheet);
            this.tpRawMaterial.Controls.Add(this.BtnAddNewRawSheet);
            this.tpRawMaterial.Controls.Add(this.btnSaveRawSheet);
            this.tpRawMaterial.Controls.Add(this.dgvRawLinear);
            this.tpRawMaterial.Controls.Add(this.dgvRawSheet);
            this.tpRawMaterial.Location = new System.Drawing.Point(4, 22);
            this.tpRawMaterial.Name = "tpRawMaterial";
            this.tpRawMaterial.Size = new System.Drawing.Size(792, 474);
            this.tpRawMaterial.TabIndex = 4;
            this.tpRawMaterial.Text = "Raw Material";
            this.tpRawMaterial.UseVisualStyleBackColor = true;
            // 
            // lblRawSheet
            // 
            this.lblRawSheet.AutoSize = true;
            this.lblRawSheet.Location = new System.Drawing.Point(13, 4);
            this.lblRawSheet.Name = "lblRawSheet";
            this.lblRawSheet.Size = new System.Drawing.Size(208, 13);
            this.lblRawSheet.TabIndex = 33;
            this.lblRawSheet.Text = "Sheet Metal Raw Material Cross Ref Table";
            // 
            // BtnAddNewRawSheet
            // 
            this.BtnAddNewRawSheet.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.BtnAddNewRawSheet.Location = new System.Drawing.Point(13, 252);
            this.BtnAddNewRawSheet.Name = "BtnAddNewRawSheet";
            this.BtnAddNewRawSheet.Size = new System.Drawing.Size(120, 23);
            this.BtnAddNewRawSheet.TabIndex = 32;
            this.BtnAddNewRawSheet.Text = "Add New Row";
            this.BtnAddNewRawSheet.UseVisualStyleBackColor = true;
            // 
            // btnSaveRawSheet
            // 
            this.btnSaveRawSheet.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveRawSheet.Location = new System.Drawing.Point(664, 252);
            this.btnSaveRawSheet.Name = "btnSaveRawSheet";
            this.btnSaveRawSheet.Size = new System.Drawing.Size(120, 23);
            this.btnSaveRawSheet.TabIndex = 31;
            this.btnSaveRawSheet.Text = "Save Sheet Info";
            this.btnSaveRawSheet.UseVisualStyleBackColor = true;
            // 
            // dgvRawLinear
            // 
            this.dgvRawLinear.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRawLinear.Location = new System.Drawing.Point(12, 285);
            this.dgvRawLinear.Name = "dgvRawLinear";
            this.dgvRawLinear.Size = new System.Drawing.Size(772, 175);
            this.dgvRawLinear.TabIndex = 1;
            // 
            // dgvRawSheet
            // 
            this.dgvRawSheet.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRawSheet.Location = new System.Drawing.Point(12, 26);
            this.dgvRawSheet.Name = "dgvRawSheet";
            this.dgvRawSheet.Size = new System.Drawing.Size(772, 220);
            this.dgvRawSheet.TabIndex = 0;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(703, 506);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(87, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(610, 506);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(87, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "&OK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // lblRosleinVersonLabel
            // 
            this.lblRosleinVersonLabel.AutoSize = true;
            this.lblRosleinVersonLabel.Location = new System.Drawing.Point(13, 515);
            this.lblRosleinVersonLabel.Name = "lblRosleinVersonLabel";
            this.lblRosleinVersonLabel.Size = new System.Drawing.Size(120, 13);
            this.lblRosleinVersonLabel.TabIndex = 5;
            this.lblRosleinVersonLabel.Text = "Roeslein AddIn Version:";
            // 
            // lblVersionNumber
            // 
            this.lblVersionNumber.AutoSize = true;
            this.lblVersionNumber.Location = new System.Drawing.Point(131, 515);
            this.lblVersionNumber.Name = "lblVersionNumber";
            this.lblVersionNumber.Size = new System.Drawing.Size(31, 13);
            this.lblVersionNumber.TabIndex = 6;
            this.lblVersionNumber.Text = "0000";
            // 
            // lblCopyright
            // 
            this.lblCopyright.AutoSize = true;
            this.lblCopyright.Location = new System.Drawing.Point(223, 515);
            this.lblCopyright.Name = "lblCopyright";
            this.lblCopyright.Size = new System.Drawing.Size(51, 13);
            this.lblCopyright.TabIndex = 7;
            this.lblCopyright.Text = "Copyright";
            // 
            // txtSettingsVersion
            // 
            this.txtSettingsVersion.Location = new System.Drawing.Point(105, 440);
            this.txtSettingsVersion.Name = "txtSettingsVersion";
            this.txtSettingsVersion.Size = new System.Drawing.Size(100, 20);
            this.txtSettingsVersion.TabIndex = 32;
            // 
            // lblSettingsVersion
            // 
            this.lblSettingsVersion.AutoSize = true;
            this.lblSettingsVersion.Location = new System.Drawing.Point(13, 443);
            this.lblSettingsVersion.Name = "lblSettingsVersion";
            this.lblSettingsVersion.Size = new System.Drawing.Size(86, 13);
            this.lblSettingsVersion.TabIndex = 33;
            this.lblSettingsVersion.Text = "Settings Version:";
            // 
            // lblSettingsVersionNote
            // 
            this.lblSettingsVersionNote.AutoSize = true;
            this.lblSettingsVersionNote.ForeColor = System.Drawing.SystemColors.AppWorkspace;
            this.lblSettingsVersionNote.Location = new System.Drawing.Point(211, 447);
            this.lblSettingsVersionNote.Name = "lblSettingsVersionNote";
            this.lblSettingsVersionNote.Size = new System.Drawing.Size(389, 13);
            this.lblSettingsVersionNote.TabIndex = 34;
            this.lblSettingsVersionNote.Text = "This is places on each file and is used to see if the file has been checked alrea" +
    "dy";
            // 
            // SettingsFormV2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 536);
            this.Controls.Add(this.lblCopyright);
            this.Controls.Add(this.lblVersionNumber);
            this.Controls.Add(this.lblRosleinVersonLabel);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tabControl1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "SettingsFormV2";
            this.Text = "Settings Form V2";
            this.tabControl1.ResumeLayout(false);
            this.GeneralSettings.ResumeLayout(false);
            this.GeneralSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPropertyMappings)).EndInit();
            this.MaterialMappings.ResumeLayout(false);
            this.MaterialMappings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMaterialMapping)).EndInit();
            this.ThicknessMappings.ResumeLayout(false);
            this.ThicknessMappings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvThicknessMapping)).EndInit();
            this.tpStandardsSettings.ResumeLayout(false);
            this.tpStandardsSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPropertyStandards)).EndInit();
            this.tpRawMaterial.ResumeLayout(false);
            this.tpRawMaterial.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRawLinear)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRawSheet)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage GeneralSettings;
        private System.Windows.Forms.TabPage MaterialMappings;
        private System.Windows.Forms.TabPage ThicknessMappings;
        private System.Windows.Forms.Button btnBrowseHotFolder;
        private System.Windows.Forms.TextBox txtHotFolder;
        private System.Windows.Forms.Label lblHotFolder;
        private System.Windows.Forms.Label lblHotFolderInfo;
        private System.Windows.Forms.Button btnUseDefaultHotFolder;
        private System.Windows.Forms.TextBox txtDrawingTemplate;
        private System.Windows.Forms.Button BtnBrowseDrawingTemplate;
        private System.Windows.Forms.Label lblDrawingTemplate;
        private System.Windows.Forms.CheckBox chkEnableLogging;
        private System.Windows.Forms.Button btnBrowseLogFile;
        private System.Windows.Forms.TextBox txtLogFile;
        private System.Windows.Forms.Label lblLogFile;
        private System.Windows.Forms.CheckBox chkEnableDebugLogging;
        private System.Windows.Forms.Label lblDebugLogFile;
        private System.Windows.Forms.TextBox txtDebugLogFile;
        private System.Windows.Forms.Button btnBrowseDebugLogFile;
        private System.Windows.Forms.ComboBox cboTextLocation;
        private System.Windows.Forms.Label lblTextLocation;
        private System.Windows.Forms.CheckBox chkShowBendLines;
        private System.Windows.Forms.TextBox txtBendLineLayer;
        private System.Windows.Forms.Label lblBendLineLayer;
        private System.Windows.Forms.TextBox txtTextLayer;
        private System.Windows.Forms.Label lblTextLayer;
        private System.Windows.Forms.DataGridView dgvPropertyMappings;
        private System.Windows.Forms.DataGridView dgvMaterialMapping;
        private System.Windows.Forms.Label lblMaterialCrossRefernceTable;
        private System.Windows.Forms.Button btnSaveMappings;
        private System.Windows.Forms.Button btnAddNewRow;
        private System.Windows.Forms.Button BtnAddNewRowMaterial;
        private System.Windows.Forms.Button btnSaveMappingsMaterial;
        private System.Windows.Forms.Button BtnAddNewThickness;
        private System.Windows.Forms.Button btnSaveThickness;
        private System.Windows.Forms.DataGridView dgvThicknessMapping;
        private System.Windows.Forms.Label lblThicknessCrossRefernceTable;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label lblDXFTextMapping;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.CheckBox chkCheckForLaser;
        private System.Windows.Forms.Label lblRosleinVersonLabel;
        private System.Windows.Forms.Label lblVersionNumber;
        private System.Windows.Forms.Label lblCopyright;
        private System.Windows.Forms.TabPage tpStandardsSettings;
        private System.Windows.Forms.DataGridView dgvPropertyStandards;
        private System.Windows.Forms.Button BtnAddNewFileProp;
        private System.Windows.Forms.Button btnSaveFileProp;
        private System.Windows.Forms.Label lblDefautFileProp;
        private System.Windows.Forms.TabPage tpRawMaterial;
        private System.Windows.Forms.DataGridView dgvRawLinear;
        private System.Windows.Forms.DataGridView dgvRawSheet;
        private System.Windows.Forms.Button BtnAddNewRawSheet;
        private System.Windows.Forms.Button btnSaveRawSheet;
        private System.Windows.Forms.Label lblRawSheet;
        private System.Windows.Forms.TextBox txtSettingsVersion;
        private System.Windows.Forms.Label lblSettingsVersionNote;
        private System.Windows.Forms.Label lblSettingsVersion;
    }
}