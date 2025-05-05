using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Microsoft.Win32;

namespace RoesleinAddIn
{
    public partial class SettingsForm : Form
    {
        private string hotFolderPath;
        private string logFilePath;
        private string debugLogFilePath;
        private bool loggingEnabled;
        private bool debugLoggingEnabled;
        private DataGridView dgvPropertyMappings;
        private DataGridView dgvMaterialMappings;
        private DataGridView dgvThicknessMappings;
        private ContextMenuStrip dgvContextMenu;
        private ContextMenuStrip dgvMaterialContextMenu;
        private ContextMenuStrip dgvThicknessContextMenu;
        private TextBox txtDrawingTemplate;
        private Label lblDrawingTemplate;
        private Button btnBrowseDrawingTemplate;
        
        public SettingsForm()
        {
            try
            {
                InitializeComponent();
                // --- Layout constants ---
                int minFormWidth = 1100;
                int minFormHeight = 900;
                int rowSpacing = 16;
                int fieldWidth = 300;
                int buttonWidth = 90;
                int gridWidth = 950;
                int leftMargin = 24;
                int labelWidth = 180;
                int currentY = 24;

                // Helper for adding a row of controls
                void AddRow(Control label, Control field, Control button1 = null, Control button2 = null)
                {
                    if (label != null)
                    {
                        label.Location = new Point(leftMargin, currentY + 4);
                        label.Size = new Size(labelWidth, 20);
                        tabSettings.Controls.Add(label);
                    }
                    if (field != null)
                    {
                        field.Location = new Point(leftMargin + labelWidth, currentY);
                        field.Size = new Size(fieldWidth, 24);
                        tabSettings.Controls.Add(field);
                    }
                    int right = field != null ? field.Right : leftMargin + labelWidth;
                    if (button1 != null)
                    {
                        button1.Location = new Point(right + 8, currentY);
                        button1.Size = new Size(buttonWidth, 24);
                        tabSettings.Controls.Add(button1);
                        right = button1.Right;
                    }
                    if (button2 != null)
                    {
                        button2.Location = new Point(right + 8, currentY);
                        button2.Size = new Size(buttonWidth, 24);
                        tabSettings.Controls.Add(button2);
                    }
                    currentY += 24 + rowSpacing;
                }

                // DXF Output Folder row
                AddRow(lblHotFolder, txtHotFolder, btnBrowseHotFolder, btnUseDefaultHotFolder);
                int dxfOutputY = currentY - (24 + rowSpacing); // Store the Y position of DXF output row
                int browseButtonX = btnBrowseHotFolder.Location.X; // Store X position of first Browse button
                int textBoxWidth = txtHotFolder.Width; // Store width of first text box

                // Drawing Template row - start 20px lower
                currentY += 20;
                lblDrawingTemplate = new Label();
                lblDrawingTemplate.Text = "Drawing Template:";
                txtDrawingTemplate = new TextBox();
                txtDrawingTemplate.Location = new Point(19, currentY); // Align X with DXF Output Folder
                txtDrawingTemplate.Width = 482; // Match DXF output width
                btnBrowseDrawingTemplate = new Button();
                btnBrowseDrawingTemplate.Text = "Browse...";
                btnBrowseDrawingTemplate.Click += BtnBrowseDrawingTemplate_Click;
                AddRow(lblDrawingTemplate, txtDrawingTemplate, btnBrowseDrawingTemplate);
                txtDrawingTemplate.Location = new Point(txtHotFolder.Location.X, lblDrawingTemplate.Location.Y); // Align X with DXF Output Folder, Y with label
                btnBrowseDrawingTemplate.Location = new Point(btnBrowseHotFolder.Location.X, txtDrawingTemplate.Location.Y); // Align Browse button
                // Set width so right edge is 8px to the left of the Browse button
                int textBoxRight = btnBrowseDrawingTemplate.Location.X - 8;
                txtDrawingTemplate.Width = textBoxRight - txtDrawingTemplate.Location.X;

                // Log File Path row
                lblLogFile.Location = new Point(lblLogFile.Location.X, lblLogFile.Location.Y + 20);
                txtLogFile.Location = new Point(19, txtLogFile.Location.Y + 20); // Align X
                txtLogFile.Width = 482;
                btnBrowseLogFile.Location = new Point(browseButtonX, btnBrowseLogFile.Location.Y + 20);

                // Enable Logging
                if (chkEnableLogging != null)
                {
                    chkEnableLogging.Location = new Point(leftMargin, chkEnableLogging.Location.Y + 20);
                }

                // Debug Log File Path row
                lblDebugLogFile.Location = new Point(lblDebugLogFile.Location.X, lblDebugLogFile.Location.Y + 20);
                txtDebugLogFile.Location = new Point(19, txtDebugLogFile.Location.Y + 20); // Align X
                txtDebugLogFile.Width = 482;
                btnBrowseDebugLogFile.Location = new Point(browseButtonX, btnBrowseDebugLogFile.Location.Y + 20);

                // Enable Debug Logging
                if (chkEnableDebugLogging != null)
                {
                    chkEnableDebugLogging.Location = new Point(leftMargin, chkEnableDebugLogging.Location.Y + 20);
                }

                // Show Bend Lines and Text Location
                if (chkShowBendLines != null)
                {
                    chkShowBendLines.Location = new Point(leftMargin, chkShowBendLines.Location.Y + 20);
                }
                if (lblTextLocation != null)
                {
                    lblTextLocation.Location = new Point(lblTextLocation.Location.X, lblTextLocation.Location.Y + 20);
                }
                if (cboTextLocation != null)
                {
                    cboTextLocation.Location = new Point(cboTextLocation.Location.X, cboTextLocation.Location.Y + 20);
                }

                // Text Layer
                lblTextLayer.Location = new Point(lblTextLayer.Location.X, lblTextLayer.Location.Y + 20);
                txtTextLayer.Location = new Point(109, txtTextLayer.Location.Y + 20); // Align X
                txtTextLayer.Width = 382;

                // Bend Line Layer
                lblBendLineLayer.Location = new Point(lblBendLineLayer.Location.X, lblBendLineLayer.Location.Y + 20);
                txtBendLineLayer.Location = new Point(109, txtBendLineLayer.Location.Y + 20); // Align X
                txtBendLineLayer.Width = 382;

                // Property Mappings Grid
                if (dgvPropertyMappings != null)
                {
                    currentY += 60; // Add 60 pixels of vertical space before the DataGridView
                    dgvPropertyMappings.Top = currentY;
                    dgvPropertyMappings.Left = leftMargin;
                    dgvPropertyMappings.Width = gridWidth;
                    dgvPropertyMappings.Height = 180;
                    tabSettings.Controls.Add(dgvPropertyMappings);
                    currentY = dgvPropertyMappings.Bottom + 40;
                }

                // Add New Row and Save Mappings buttons - align them horizontally
                foreach (Control ctrl in tabSettings.Controls)
                {
                    if (ctrl is Button btn)
                    {
                        if (btn.Text == "Add New Row")
                        {
                            btn.Location = new Point(leftMargin, currentY);
                        }
                        else if (btn.Text == "Save Mappings")
                        {
                            btn.Location = new Point(leftMargin + gridWidth - btn.Width, currentY);
                        }
                    }
                }
                currentY += 32; // Space after buttons

                // Grow the tab and form if needed
                tabSettings.Width = minFormWidth - 40;
                tabSettings.Height = currentY + 40;
                this.MinimumSize = new Size(minFormWidth, minFormHeight);
                this.Width = minFormWidth;
                this.Height = Math.Max(minFormHeight, tabSettings.Bottom + 40);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing settings form: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
            // The tab controls are already initialized by the designer
            // Let's just make sure the tabs have the right text
            tabSettings.Text = "General Settings";
            tabMaterials.Text = "Material Mappings";
            tabThickness.Text = "Thickness Mappings";
            
            // Make sure we only have exactly 3 tabs - remove any extra tabs
            while (tabControl.TabPages.Count > 3)
            {
                // Remove any tab that isn't one of our main tabs
                for (int i = tabControl.TabPages.Count - 1; i >= 0; i--)
                {
                    TabPage tab = tabControl.TabPages[i];
                    if (tab != tabSettings && tab != tabMaterials && tab != tabThickness)
                    {
                        tabControl.TabPages.RemoveAt(i);
                    }
                }
            }
            
            // Ensure tabs are in the right order
            tabControl.TabPages.Clear();
            tabControl.TabPages.Add(tabSettings);
            tabControl.TabPages.Add(tabMaterials);
            tabControl.TabPages.Add(tabThickness);
            
            // Load icon directly from embedded resource
            try
            {
                System.Reflection.Assembly executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                // Construct the resource name: DefaultNamespace.FolderName.FileName
                string resourceName = "RoesleinAddIn.Resources.Roeslein_Connex.ico"; 
                using (System.IO.Stream iconStream = executingAssembly.GetManifestResourceStream(resourceName))
                {
                    if (iconStream != null)
                    {
                        this.Icon = new System.Drawing.Icon(iconStream);
                    }
                    else
                    {
                        // Log or Debug that the resource wasn't found
                        System.Diagnostics.Debug.WriteLine($"[ERROR] Icon resource not found: {resourceName}");
                    }
                }
            }
            catch (Exception ex)
            {
                 // Log or Debug any exception during icon loading
                 System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load icon: {ex.Message}");
            }

            // Move controls to tab pages
            MoveControlsToTabs();
            
            InitializePropertyMappings();
            InitializeMaterialMappings();
            InitializeThicknessMappings();
            LoadSettings();
            LoadPropertyMappings();
            LoadMaterialMappings();
            LoadThicknessMappings();
            
            // Force renumbering of all rows in both grids
            SavePropertyMappings();
            SaveMaterialMappings();
            SaveThicknessMappings();
            
            // Make sure the thickness tab is visible
            tabControl.TabPages[2].Visible = true;
            this.tabThickness.Visible = true;

            // Ensure Drawing Template row aligns with DXF Output Folder row
            if (txtHotFolder != null && btnBrowseHotFolder != null && txtDrawingTemplate != null && btnBrowseDrawingTemplate != null)
            {
                txtDrawingTemplate.Location = new Point(txtHotFolder.Location.X, lblDrawingTemplate.Location.Y);
                txtDrawingTemplate.Width = btnBrowseHotFolder.Location.X - txtHotFolder.Location.X - 8; // 8px gap before button
                btnBrowseDrawingTemplate.Location = new Point(btnBrowseHotFolder.Location.X, txtDrawingTemplate.Location.Y);
            }
        }
        
        private void MoveControlsToTabs()
        {
            // Create a list of controls to move to the settings tab
            List<Control> controlsToMove = new List<Control>();
            foreach (Control ctrl in this.Controls)
            {
                if (ctrl != tabControl && ctrl != btnOK && ctrl != btnCancel)
                {
                    controlsToMove.Add(ctrl);
                }
            }
            
            foreach (Control ctrl in controlsToMove)
            {
                this.Controls.Remove(ctrl);
                tabSettings.Controls.Add(ctrl);
            }
            
            // Make sure the property mappings grid is added to the settings tab
            if (dgvPropertyMappings != null && !tabSettings.Controls.Contains(dgvPropertyMappings))
            {
                if (this.Controls.Contains(dgvPropertyMappings))
                {
                    this.Controls.Remove(dgvPropertyMappings);
                }
                tabSettings.Controls.Add(dgvPropertyMappings);
            }
            
            // Make sure all tabs are accessible and initialized
            if (tabControl.TabPages.Count < 3)
            {
                // Make sure all three tabs are added
                if (!tabControl.TabPages.Contains(tabSettings))
                    tabControl.TabPages.Add(tabSettings);
                if (!tabControl.TabPages.Contains(tabMaterials))
                    tabControl.TabPages.Add(tabMaterials);
                if (!tabControl.TabPages.Contains(tabThickness))
                {
                    // Create the Thickness tab if it doesn't exist
                    tabThickness = new TabPage("Thickness Mappings");
                    tabControl.TabPages.Add(tabThickness);
                }
            }
            
            // Ensure proper tab titles
            tabSettings.Text = "General Settings";
            tabMaterials.Text = "Material Mappings";
            tabThickness.Text = "Thickness Mappings";
            
            // Add material mappings button to the material tab
            tabMaterials.Controls.Add(btnSaveMaterialMappings);
            
            // Make all tabs visible
            foreach (TabPage tab in tabControl.TabPages)
            {
                tab.Visible = true;
            }
        }

        private void LoadSettings()
        {
            try
            {
                var settings = Settings.LoadSettings();
                if (settings == null)
                {
                    MessageBox.Show("Settings could not be loaded (null). Using defaults.", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    settings = new Settings();
                }
                // Fix null reference for DrawingTemplatePath
                if (string.IsNullOrEmpty(settings.DrawingTemplatePath))
                    settings.DrawingTemplatePath = "C:\\PCSVAULT\\SolidWorks Settings\\Document Templates\\Blank.DRWDOT";
                if (txtDrawingTemplate != null)
                    txtDrawingTemplate.Text = settings.DrawingTemplatePath;
                // Add similar null checks for all other controls
                if (txtHotFolder != null) txtHotFolder.Text = settings.LastExportFolder ?? "";
                if (txtLogFile != null) txtLogFile.Text = settings.LogFilePath ?? "";
                if (txtDebugLogFile != null) txtDebugLogFile.Text = settings.DebugLogFilePath ?? "";
                if (chkEnableLogging != null) chkEnableLogging.Checked = settings.LoggingEnabled;
                if (chkEnableDebugLogging != null) chkEnableDebugLogging.Checked = settings.DebugLoggingEnabled;
                if (txtTextLayer != null) txtTextLayer.Text = settings.TextLayerName ?? "Notes";
                if (txtBendLineLayer != null) txtBendLineLayer.Text = settings.BendLineLayerName ?? "Bend";
                if (chkShowBendLines != null) chkShowBendLines.Checked = settings.ShowBendLines;
                if (chkCheckForLaser != null) chkCheckForLaser.Checked = settings.CheckForLaser;
                // Always add 'Centered' to the Text Location dropdown and select it by default if blank
                if (cboTextLocation != null)
                {
                    if (!cboTextLocation.Items.Contains("Centered"))
                        cboTextLocation.Items.Insert(0, "Centered");
                    if (!string.IsNullOrEmpty(settings.TextLocation) && cboTextLocation.Items.Contains(settings.TextLocation))
                        cboTextLocation.SelectedItem = settings.TextLocation;
                    else
                        cboTextLocation.SelectedItem = "Centered";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new Settings
                {
                    LastExportFolder = hotFolderPath,
                    LogFilePath = logFilePath,
                    DebugLogFilePath = debugLogFilePath,
                    LoggingEnabled = loggingEnabled,
                    DebugLoggingEnabled = debugLoggingEnabled,
                    TextLayerName = txtTextLayer.Text,
                    BendLineLayerName = txtBendLineLayer.Text,
                    ShowBendLines = chkShowBendLines.Checked,
                    TextLocation = cboTextLocation.SelectedItem?.ToString() ?? "Centered",
                    DrawingTemplatePath = txtDrawingTemplate.Text, // Save from textbox
                    CheckForLaser = chkCheckForLaser.Checked
                };
                settings.SaveSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Settings Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnBrowseHotFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Hot Folder for DXF Output";
                dialog.SelectedPath = txtHotFolder.Text;
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtHotFolder.Text = dialog.SelectedPath;
                }
            }
        }

        private void btnUseDefaultHotFolder_Click(object sender, EventArgs e)
        {
            string defaultNetworkPath = @"\\roeslein.com\locations$\RoesleinFabrication\Connex-Metamation Hot Folder\DXFs From PDM";
            
            // Set the text field to the default path
            txtHotFolder.Text = defaultNetworkPath;
            
            // Check if the directory exists and show a message if it doesn't
            if (!Directory.Exists(defaultNetworkPath))
            {
                MessageBox.Show(
                    "The default network path does not appear to be accessible.\n\n" +
                    "This may be because you are not connected to the network or do not have permission to access this folder.\n\n" +
                    "The path has been set, but you may need to contact IT for assistance if you cannot access the folder.",
                    "Network Path Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void btnBrowseLogFile_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Select or Create Log File";
                dialog.Filter = "Log files (*.log)|*.log|All files (*.*)|*.*";
                dialog.DefaultExt = "log";
                try
                {
                     if (!string.IsNullOrEmpty(txtLogFile.Text) && Directory.Exists(Path.GetDirectoryName(txtLogFile.Text)))
                     {
                         dialog.InitialDirectory = Path.GetDirectoryName(txtLogFile.Text);
                         dialog.FileName = Path.GetFileName(txtLogFile.Text);
                     }
                     else
                     {
                         string defaultLogDir = Path.Combine(
                             System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                             "RoesleinAddIn", "logs");
                         if (!Directory.Exists(defaultLogDir)) Directory.CreateDirectory(defaultLogDir);
                         dialog.InitialDirectory = defaultLogDir;
                         dialog.FileName = "RoesleinAddIn.log";
                     }
                }
                catch { }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtLogFile.Text = dialog.FileName;
                }
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Update paths from text boxes
            hotFolderPath = txtHotFolder.Text;
            logFilePath = txtLogFile.Text;
            debugLogFilePath = txtDebugLogFile.Text;
            
            // Update checkboxes
            loggingEnabled = chkEnableLogging.Checked;
            debugLoggingEnabled = chkEnableDebugLogging.Checked;
            
            string textLayer = txtTextLayer.Text.Trim();
            string bendLayer = txtBendLineLayer.Text.Trim();

            if (string.IsNullOrWhiteSpace(hotFolderPath) || !Directory.Exists(hotFolderPath))
            {
                 MessageBox.Show("Please select a valid export folder.", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
            }

            if (loggingEnabled && string.IsNullOrWhiteSpace(logFilePath))
            {
                 MessageBox.Show("Please specify a log file path if logging is enabled.", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
            }
            
            if (debugLoggingEnabled && string.IsNullOrWhiteSpace(debugLogFilePath))
            {
                 MessageBox.Show("Please specify a debug log file path if debug logging is enabled.", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
            }
            
            // Save all settings
            SaveSettings();
            SavePropertyMappings();
            SaveMaterialMappings();
            SaveThicknessMappings();
            
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void chkEnableLogging_CheckedChanged(object sender, EventArgs e)
        {
            txtLogFile.Enabled = chkEnableLogging.Checked;
            btnBrowseLogFile.Enabled = chkEnableLogging.Checked;
            lblLogFile.Enabled = chkEnableLogging.Checked;
            
            // Debugging logging requires normal logging to be enabled
            chkEnableDebugLogging.Enabled = chkEnableLogging.Checked;
            
            // If logging is disabled, also disable debug logging
            if (!chkEnableLogging.Checked)
            {
                chkEnableDebugLogging.Checked = false;
            }

            // Update Logger settings immediately
            Logger.Instance.SetSettingsEnabled(chkEnableLogging.Checked);
            if (!string.IsNullOrWhiteSpace(txtLogFile.Text))
            {
                Logger.Instance.SetLogFilePath(txtLogFile.Text);
            }
        }
        
        private void chkEnableDebugLogging_CheckedChanged(object sender, EventArgs e)
        {
            // Enable or disable debug log path controls based on checkbox
            txtDebugLogFile.Enabled = chkEnableDebugLogging.Checked;
            btnBrowseDebugLogFile.Enabled = chkEnableDebugLogging.Checked;
            lblDebugLogFile.Enabled = chkEnableDebugLogging.Checked;

            // Update Logger debug settings immediately
            Logger.Instance.SetDebugEnabled(chkEnableDebugLogging.Checked);
            if (!string.IsNullOrWhiteSpace(txtDebugLogFile.Text))
            {
                Logger.Instance.SetDebugLogPath(txtDebugLogFile.Text);
            }
        }
        
        private void btnBrowseDebugLogFile_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Select or Create Debug Log File";
                dialog.Filter = "Log files (*.log)|*.log|All files (*.*)|*.*";
                dialog.DefaultExt = "log";
                try
                {
                     if (!string.IsNullOrEmpty(txtDebugLogFile.Text) && Directory.Exists(Path.GetDirectoryName(txtDebugLogFile.Text)))
                     {
                         dialog.InitialDirectory = Path.GetDirectoryName(txtDebugLogFile.Text);
                         dialog.FileName = Path.GetFileName(txtDebugLogFile.Text);
                     }
                     else
                     {
                         string defaultLogDir = Path.Combine(
                             System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                             "RoesleinAddIn", "logs");
                         if (!Directory.Exists(defaultLogDir)) Directory.CreateDirectory(defaultLogDir);
                         dialog.InitialDirectory = defaultLogDir;
                         dialog.FileName = "RoesleinAddIn_Debug.log";
                     }
                }
                catch { }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtDebugLogFile.Text = dialog.FileName;
                }
            }
        }

        private void InitializePropertyMappings()
        {
            dgvContextMenu = new ContextMenuStrip();
            var deleteMenuItem = new ToolStripMenuItem("Delete Row");
            deleteMenuItem.Click += DgvDeleteMenuItem_Click;
            dgvContextMenu.Items.Add(deleteMenuItem);

            // Create the DataGridView for property mappings
            dgvPropertyMappings = new DataGridView();
            dgvPropertyMappings.Name = "dgvPropertyMappings";
            dgvPropertyMappings.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            dgvPropertyMappings.Location = new Point(19, 410); // Position below the Bend Line Layer textbox
            dgvPropertyMappings.Size = new Size(670, 290);
            dgvPropertyMappings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPropertyMappings.BackgroundColor = SystemColors.Window;
            dgvPropertyMappings.BorderStyle = BorderStyle.Fixed3D;
            dgvPropertyMappings.RowHeadersWidth = 25;
            dgvPropertyMappings.AllowUserToAddRows = false;
            dgvPropertyMappings.AllowUserToDeleteRows = false;
            dgvPropertyMappings.AllowUserToResizeRows = false;
            dgvPropertyMappings.MultiSelect = false;
            dgvPropertyMappings.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPropertyMappings.ContextMenuStrip = dgvContextMenu;
            dgvPropertyMappings.CellMouseDown += DgvPropertyMappings_MouseDown;

            // Create a "Row Number" column
            DataGridViewTextBoxColumn colRowNumber = new DataGridViewTextBoxColumn();
            colRowNumber.HeaderText = "Row Number";
            colRowNumber.ReadOnly = true;
            colRowNumber.Name = "RowNumber";
            colRowNumber.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgvPropertyMappings.Columns.Add(colRowNumber);

            // Create a "DXF Property Name" column
            DataGridViewTextBoxColumn colDxfProperty = new DataGridViewTextBoxColumn();
            colDxfProperty.HeaderText = "DXF Property Name";
            colDxfProperty.ReadOnly = false;
            colDxfProperty.Name = "DxfPropertyName";
            dgvPropertyMappings.Columns.Add(colDxfProperty);

            // Create a "SolidWorks Custom Property" column
            DataGridViewTextBoxColumn colSwProperty = new DataGridViewTextBoxColumn();
            colSwProperty.HeaderText = "SolidWorks Custom Property";
            colSwProperty.ReadOnly = false;
            colSwProperty.Name = "SwCustomProperty";
            dgvPropertyMappings.Columns.Add(colSwProperty);

            // Add the DataGridView to the form
            if (!tabSettings.Controls.Contains(dgvPropertyMappings))
            {
                tabSettings.Controls.Add(dgvPropertyMappings);
            }

            // Create and position the "Add New Row" button
            Button btnAddNewRow = new Button();
            btnAddNewRow.Text = "Add New Row";
            btnAddNewRow.Size = new Size(120, 23);
            btnAddNewRow.Location = new Point(19, 710);
            btnAddNewRow.Click += (sender, e) => AddNewPropertyRow();
            if (!tabSettings.Controls.Contains(btnAddNewRow))
            {
                tabSettings.Controls.Add(btnAddNewRow);
            }
        }

        private void AddNewPropertyRow()
        {
            int newRowIndex = dgvPropertyMappings.Rows.Count;
            dgvPropertyMappings.Rows.Add();
            dgvPropertyMappings.Rows[newRowIndex].Cells["RowNumber"].Value = newRowIndex + 1;
            dgvPropertyMappings.Rows[newRowIndex].Cells["DxfPropertyName"].Value = "";
            dgvPropertyMappings.Rows[newRowIndex].Cells["SwCustomProperty"].Value = "";
            dgvPropertyMappings.CurrentCell = dgvPropertyMappings.Rows[newRowIndex].Cells["DxfPropertyName"];
        }

        private void DgvPropertyMappings_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTestInfo = dgvPropertyMappings.HitTest(e.X, e.Y);
                if (hitTestInfo.RowIndex >= 0 && hitTestInfo.RowIndex < dgvPropertyMappings.RowCount - 1)
                {
                    dgvPropertyMappings.ClearSelection();
                    dgvPropertyMappings.Rows[hitTestInfo.RowIndex].Selected = true;
                    dgvContextMenu.Show(dgvPropertyMappings, e.Location);
                }
            }
        }

        private void DgvDeleteMenuItem_Click(object sender, EventArgs e)
        {
            if (dgvPropertyMappings.SelectedRows.Count > 0)
            {
                var selectedRow = dgvPropertyMappings.SelectedRows[0];
                if (!selectedRow.IsNewRow)
                {
                    dgvPropertyMappings.Rows.Remove(selectedRow);
                    SavePropertyMappings();
                }
            }
        }

        private void LoadPropertyMappings()
        {
            try
            {
                dgvPropertyMappings.Rows.Clear();
                
                List<PropertyMapping> mappings = Settings.LoadPropertyMappings();
                if (mappings != null && mappings.Count > 0)
                {
                    // Sort by row number
                    mappings = mappings.OrderBy(m => m.RowNumber).ToList();
                    
                    foreach (var mapping in mappings)
                    {
                        int rowIndex = dgvPropertyMappings.Rows.Add();
                        dgvPropertyMappings.Rows[rowIndex].Cells["RowNumber"].Value = mapping.RowNumber;
                        dgvPropertyMappings.Rows[rowIndex].Cells["DxfPropertyName"].Value = mapping.DxfPropertyName;
                        dgvPropertyMappings.Rows[rowIndex].Cells["SwCustomProperty"].Value = mapping.SwCustomProperty;
                    }
                }
                else
                {
                    // Add default mappings if none exist
                    AddDefaultMappings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading property mappings: {ex.Message}", "Mapping Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                
                // Add default mappings on error
                AddDefaultMappings();
            }
        }
        
        private void AddDefaultMappings()
        {
            // Add default property mappings
            int rowIndex = dgvPropertyMappings.Rows.Add();
            dgvPropertyMappings.Rows[rowIndex].Cells["RowNumber"].Value = 1;
            dgvPropertyMappings.Rows[rowIndex].Cells["DxfPropertyName"].Value = "Part Number";
            dgvPropertyMappings.Rows[rowIndex].Cells["SwCustomProperty"].Value = "Part Number";
            
            rowIndex = dgvPropertyMappings.Rows.Add();
            dgvPropertyMappings.Rows[rowIndex].Cells["RowNumber"].Value = 2;
            dgvPropertyMappings.Rows[rowIndex].Cells["DxfPropertyName"].Value = "Description";
            dgvPropertyMappings.Rows[rowIndex].Cells["SwCustomProperty"].Value = "Description";
            
            rowIndex = dgvPropertyMappings.Rows.Add();
            dgvPropertyMappings.Rows[rowIndex].Cells["RowNumber"].Value = 3;
            dgvPropertyMappings.Rows[rowIndex].Cells["DxfPropertyName"].Value = "Revision";
            dgvPropertyMappings.Rows[rowIndex].Cells["SwCustomProperty"].Value = "Revision";
            
            rowIndex = dgvPropertyMappings.Rows.Add();
            dgvPropertyMappings.Rows[rowIndex].Cells["RowNumber"].Value = 4;
            dgvPropertyMappings.Rows[rowIndex].Cells["DxfPropertyName"].Value = "Material";
            dgvPropertyMappings.Rows[rowIndex].Cells["SwCustomProperty"].Value = "Material";
            
            rowIndex = dgvPropertyMappings.Rows.Add();
            dgvPropertyMappings.Rows[rowIndex].Cells["RowNumber"].Value = 5;
            dgvPropertyMappings.Rows[rowIndex].Cells["DxfPropertyName"].Value = "Thickness";
            dgvPropertyMappings.Rows[rowIndex].Cells["SwCustomProperty"].Value = "Sheet Metal Thickness";
            
            rowIndex = dgvPropertyMappings.Rows.Add();
            dgvPropertyMappings.Rows[rowIndex].Cells["RowNumber"].Value = 6;
            dgvPropertyMappings.Rows[rowIndex].Cells["DxfPropertyName"].Value = "Shop Route";
            dgvPropertyMappings.Rows[rowIndex].Cells["SwCustomProperty"].Value = "Shop Route";
        }

        private void SavePropertyMappings()
        {
            try
            {
                List<PropertyMapping> mappings = new List<PropertyMapping>();
                
                foreach (DataGridViewRow row in dgvPropertyMappings.Rows)
                {
                    if (row.Cells["DxfPropertyName"].Value == null && row.Cells["SwCustomProperty"].Value == null)
                    {
                        continue; // Skip empty rows
                    }
                    
                    int rowNumber;
                    if (row.Cells["RowNumber"].Value != null && int.TryParse(row.Cells["RowNumber"].Value.ToString(), out rowNumber))
                    {
                        PropertyMapping mapping = new PropertyMapping
                        {
                            RowNumber = rowNumber,
                            DxfPropertyName = row.Cells["DxfPropertyName"].Value?.ToString() ?? "",
                            SwCustomProperty = row.Cells["SwCustomProperty"].Value?.ToString() ?? ""
                        };
                        mappings.Add(mapping);
                    }
                }
                
                // Ensure mappings are sorted and numbered sequentially
                mappings = mappings.OrderBy(m => m.RowNumber).ToList();
                for (int i = 0; i < mappings.Count; i++)
                {
                    mappings[i].RowNumber = i + 1;
                }
                
                // Save to file
                Settings.SavePropertyMappings(mappings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving property mappings: {ex.Message}", "Mapping Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSaveMappings_Click(object sender, EventArgs e)
        {
            SavePropertyMappings();
            MessageBox.Show("Mappings saved successfully.", "Save Mappings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void InitializeMaterialMappings()
        {
            dgvMaterialContextMenu = new ContextMenuStrip();
            var deleteMaterialMenuItem = new ToolStripMenuItem("Delete Row");
            deleteMaterialMenuItem.Click += DgvMaterialDeleteMenuItem_Click;
            dgvMaterialContextMenu.Items.Add(deleteMaterialMenuItem);

            dgvMaterialMappings = new DataGridView
            {
                ColumnCount = 3,
                Columns =
                {
                    [0] = { Name = "Row Number", Width = 80 },
                    [1] = { Name = "SolidWorks Material" },
                    [2] = { Name = "DXF Output Material" }
                },
                AllowUserToAddRows = false, // Disable auto-generated new row
                AllowUserToDeleteRows = false,
                RowHeadersVisible = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Location = new Point(20, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ContextMenuStrip = dgvMaterialContextMenu,
                Size = new Size(tabMaterials.Width - 40, 250) // Fixed height to leave space for the button
            };

            // Set the Row Number column to read-only
            dgvMaterialMappings.Columns[0].ReadOnly = true;

            dgvMaterialMappings.MouseDown += DgvMaterialMappings_MouseDown;
            
            // Add button to add new row
            Button btnAddMaterialRow = new Button
            {
                Text = "Add New Row",
                Location = new Point(20, dgvMaterialMappings.Bottom + 5),
                Size = new Size(100, 25)
            };
            btnAddMaterialRow.Click += (s, e) => AddNewMaterialRow();

            // Add a title label for the DataGridView
            Label lblMaterialGridTitle = new Label();
            lblMaterialGridTitle.Text = "Material Cross Reference Table";
            lblMaterialGridTitle.Location = new System.Drawing.Point(20, 15);
            lblMaterialGridTitle.Size = new System.Drawing.Size(200, 20);
            
            // Position the Save Material Mappings button below the grid
            btnSaveMaterialMappings.Location = new Point(tabMaterials.Width - 160, dgvMaterialMappings.Bottom + 5);
            btnSaveMaterialMappings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            
            tabMaterials.Controls.Add(lblMaterialGridTitle);
            tabMaterials.Controls.Add(dgvMaterialMappings);
            tabMaterials.Controls.Add(btnAddMaterialRow);
            tabMaterials.Controls.Add(btnSaveMaterialMappings);
        }
        
        private void AddNewMaterialRow()
        {
            int nextRowNum = dgvMaterialMappings.Rows.Count + 1;
            dgvMaterialMappings.Rows.Add(nextRowNum.ToString(), "", "");
        }

        private void DgvMaterialMappings_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTestInfo = dgvMaterialMappings.HitTest(e.X, e.Y);
                if (hitTestInfo.RowIndex >= 0 && hitTestInfo.RowIndex < dgvMaterialMappings.RowCount - 1)
                {
                    dgvMaterialMappings.ClearSelection();
                    dgvMaterialMappings.Rows[hitTestInfo.RowIndex].Selected = true;
                    dgvMaterialContextMenu.Show(dgvMaterialMappings, e.Location);
                }
            }
        }

        private void DgvMaterialDeleteMenuItem_Click(object sender, EventArgs e)
        {
            if (dgvMaterialMappings.SelectedRows.Count > 0)
            {
                var selectedRow = dgvMaterialMappings.SelectedRows[0];
                if (!selectedRow.IsNewRow)
                {
                    dgvMaterialMappings.Rows.Remove(selectedRow);
                    SaveMaterialMappings();
                }
            }
        }

        private void LoadMaterialMappings()
        {
            try
            {
                dgvMaterialMappings.Rows.Clear();
                var mappings = Settings.LoadMaterialMappings();

                if (mappings != null && mappings.Count > 0)
                {
                    // Sort mappings by row number first
                    mappings = mappings.OrderBy(m => m.RowNumber).ToList();
                    
                    // Add mappings to grid with sequential row numbers
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        var mapping = mappings[i];
                        if (!string.IsNullOrWhiteSpace(mapping.SwMaterial) || !string.IsNullOrWhiteSpace(mapping.DxfMaterial))
                        {
                            dgvMaterialMappings.Rows.Add(
                                (i + 1).ToString(), 
                                mapping.SwMaterial, 
                                mapping.DxfMaterial);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading material mappings: {ex.Message}", "Settings Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveMaterialMappings()
        {
            try
            {
                var mappings = new List<MaterialMapping>();
                bool invalidRowFound = false;
                int rowNumber = 1; // Start numbering at 1
                
                // Create a list of non-empty rows, excluding the new row
                var nonEmptyRows = new List<DataGridViewRow>();
                foreach (DataGridViewRow row in dgvMaterialMappings.Rows)
                {
                    if (row.IsNewRow) continue;
                    
                    string swMaterial = row.Cells[1].Value?.ToString() ?? string.Empty;
                    string dxfMaterial = row.Cells[2].Value?.ToString() ?? string.Empty;
                    
                    if (!string.IsNullOrWhiteSpace(swMaterial) || !string.IsNullOrWhiteSpace(dxfMaterial))
                    {
                        nonEmptyRows.Add(row);
                    }
                }
                
                // Process rows in order
                foreach (var row in nonEmptyRows)
                {
                    string swMaterial = row.Cells[1].Value?.ToString() ?? string.Empty;
                    string dxfMaterial = row.Cells[2].Value?.ToString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(swMaterial))
                    {
                        if (!string.IsNullOrWhiteSpace(dxfMaterial))
                        {
                            invalidRowFound = true;
                        }
                        continue;
                    }

                    var mapping = new MaterialMapping
                    {
                        RowNumber = rowNumber++, // Assign sequential row numbers
                        SwMaterial = swMaterial,
                        DxfMaterial = dxfMaterial
                    };
                    mappings.Add(mapping);
                }

                if (invalidRowFound)
                {
                    MessageBox.Show("One or more mappings were ignored because the 'SolidWorks Material' was empty.", 
                                    "Save Mappings Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                Settings.SaveMaterialMappings(mappings);
                
                // Reload the grid to show correct numbering
                dgvMaterialMappings.Rows.Clear();
                foreach (var mapping in mappings)
                {
                    dgvMaterialMappings.Rows.Add(mapping.RowNumber.ToString(), mapping.SwMaterial, mapping.DxfMaterial);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving material mappings: {ex.Message}", "Settings Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSaveMaterialMappings_Click(object sender, EventArgs e)
        {
            SaveMaterialMappings();
            // No message box - consistent with thickness mappings
            // MessageBox.Show("Material mappings saved successfully.", "Save Mappings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void InitializeThicknessMappings()
        {
            try
            {
                // Make sure the tab is visible
                tabThickness.Visible = true;
                
                // Make sure the tab has the right text
                tabThickness.Text = "Thickness Mappings";

                // Clear any existing controls to avoid duplicates
                tabThickness.Controls.Clear();

                dgvThicknessContextMenu = new ContextMenuStrip();
                var deleteThicknessMenuItem = new ToolStripMenuItem("Delete Row");
                deleteThicknessMenuItem.Click += DgvThicknessDeleteMenuItem_Click;
                dgvThicknessContextMenu.Items.Add(deleteThicknessMenuItem);

                dgvThicknessMappings = new DataGridView
                {
                    ColumnCount = 3,
                    Columns =
                    {
                        [0] = { Name = "Row Number", Width = 80 },
                        [1] = { Name = "SolidWorks Thickness" },
                        [2] = { Name = "DXF Output Thickness" }
                    },
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    RowHeadersVisible = true,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    Location = new Point(20, 40),
                    Size = new Size(680, 250), // Fixed size instead of using tabThickness.Width
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    ContextMenuStrip = dgvThicknessContextMenu
                };

                // Set the Row Number column to read-only
                dgvThicknessMappings.Columns[0].ReadOnly = true;
                dgvThicknessMappings.MouseDown += DgvThicknessMappings_MouseDown;
                
                // Add button to add new row
                Button btnAddThicknessRow = new Button
                {
                    Text = "Add New Row",
                    Location = new Point(20, 300), // Fixed position
                    Size = new Size(100, 25)
                };
                btnAddThicknessRow.Click += (s, e) => AddNewThicknessRow();

                // Add a title label for the DataGridView
                Label lblThicknessGridTitle = new Label
                {
                    Text = "Thickness Cross Reference Table",
                    Location = new Point(20, 15),
                    Size = new Size(200, 20)
                };
                
                // Create Save Thickness Mappings button
                Button btnSaveThicknessMappings = new Button
                {
                    Text = "Save Thickness Mappings",
                    Size = new Size(160, 30),
                    Location = new Point(540, 300), // Fixed position
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };
                btnSaveThicknessMappings.Click += new EventHandler(btnSaveThicknessMappings_Click);
                
                // Add controls to the tab
                tabThickness.Controls.Add(lblThicknessGridTitle);
                tabThickness.Controls.Add(dgvThicknessMappings);
                tabThickness.Controls.Add(btnAddThicknessRow);
                tabThickness.Controls.Add(btnSaveThicknessMappings);
                
                // Make sure tab is visible
                tabThickness.Visible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing thickness mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void AddNewThicknessRow()
        {
            try
            {
                int rowCount = dgvThicknessMappings.Rows.Count;
                dgvThicknessMappings.Rows.Add(rowCount + 1, 0.0, 0.0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding new thickness row: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvThicknessMappings_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTestInfo = dgvThicknessMappings.HitTest(e.X, e.Y);
                if (hitTestInfo.RowIndex >= 0 && hitTestInfo.RowIndex < dgvThicknessMappings.RowCount)
                {
                    dgvThicknessMappings.ClearSelection();
                    dgvThicknessMappings.Rows[hitTestInfo.RowIndex].Selected = true;
                    dgvThicknessContextMenu.Show(dgvThicknessMappings, e.Location);
                }
            }
        }

        private void DgvThicknessDeleteMenuItem_Click(object sender, EventArgs e)
        {
            if (dgvThicknessMappings.SelectedRows.Count > 0)
            {
                var selectedRow = dgvThicknessMappings.SelectedRows[0];
                if (!selectedRow.IsNewRow)
                {
                    dgvThicknessMappings.Rows.Remove(selectedRow);
                    SaveThicknessMappings();
                }
            }
        }

        private void LoadThicknessMappings()
        {
            try
            {
                // Clear existing rows
                dgvThicknessMappings.Rows.Clear();

                if (Settings.ThicknessMappings == null)
                {
                    Settings.ThicknessMappings = new Dictionary<double, double>();
                }

                int rowIndex = 0;
                foreach (var mapping in Settings.ThicknessMappings)
                {
                    dgvThicknessMappings.Rows.Add(rowIndex + 1, mapping.Key, mapping.Value);
                    rowIndex++;
                }

                // If no mappings exist, add a default row
                if (dgvThicknessMappings.Rows.Count == 0)
                {
                    AddNewThicknessRow();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading thickness mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveThicknessMappings()
        {
            try
            {
                // Create a new dictionary to store thickness mappings
                var thicknessMappings = new Dictionary<double, double>();

                foreach (DataGridViewRow row in dgvThicknessMappings.Rows)
                {
                    // Skip empty rows or rows that don't have valid numbers
                    if (row.Cells[1].Value == null || row.Cells[2].Value == null)
                        continue;

                    if (!double.TryParse(row.Cells[1].Value.ToString(), out double swThickness))
                        continue;

                    if (!double.TryParse(row.Cells[2].Value.ToString(), out double dxfThickness))
                        continue;

                    // Add to dictionary if not already present
                    if (!thicknessMappings.ContainsKey(swThickness))
                    {
                        thicknessMappings.Add(swThickness, dxfThickness);
                    }
                }

                // Update static ThicknessMappings dictionary
                Settings.ThicknessMappings.Clear();
                foreach (var pair in thicknessMappings)
                {
                    Settings.ThicknessMappings[pair.Key] = pair.Value;
                }
                
                // Save mappings to file
                Settings.SaveThicknessMappings();
                
                // No message box here
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving thickness mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Wire up the save button click handler
        private void btnSaveThicknessMappings_Click(object sender, EventArgs e)
        {
            SaveThicknessMappings();
            // No message box - we want to remove this message
            // MessageBox.Show("Thickness mappings saved successfully.", "Save Mappings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnBrowseDrawingTemplate_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select Drawing Template (.drwdot)";
                dialog.Filter = "SolidWorks Drawing Template (*.drwdot)|*.drwdot|All files (*.*)|*.*";
                dialog.DefaultExt = "drwdot";
                if (!string.IsNullOrEmpty(txtDrawingTemplate.Text) && File.Exists(txtDrawingTemplate.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(txtDrawingTemplate.Text);
                    dialog.FileName = Path.GetFileName(txtDrawingTemplate.Text);
                }
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtDrawingTemplate.Text = dialog.FileName;
                }
            }
        }
    }

    public class PropertyMapping
    {
        public string DxfPropertyName { get; set; }
        public string SwCustomProperty { get; set; }
        public int RowNumber { get; set; }
    }

    public class MaterialMapping
    {
        public string SwMaterial { get; set; }
        public string DxfMaterial { get; set; }
        public int RowNumber { get; set; }
    }
} 