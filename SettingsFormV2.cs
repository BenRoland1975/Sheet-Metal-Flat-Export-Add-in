using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using System.Reflection;
using SolidWorks.Interop.sldworks;
// Use our own PDM interface implementations
// (Remove EPDM.Interop.epdm import)

namespace RoesleinAddIn
{
    public partial class SettingsFormV2 : Form
    {
        private const string MainLogFileName = "RoesleinAddInLogger.log";
        private const string DebugLogFileName = "RoesleinAddInDebugLog.log";
        private EPDM.Interop.epdm.IEdmVault5 pdmVault;
        private ISldWorks swApp;
        private string pdmCsvFilePath = @"C:\PCSVAULT\SolidWorks Settings\Roeslein SW AddIn\Raw Material SheetMetal.csv";
        
        // currentSettings is primarily for general settings UI elements, not necessarily the DataGridViews
        // that have their own static Load methods in Settings.cs
        private Settings currentGeneralSettings; 

        // Context Menus for the other grids (restored from backup logic)
        private ContextMenuStrip dgvPropertyMappingsContextMenu;
        private ContextMenuStrip dgvMaterialMappingContextMenu;
        private ContextMenuStrip dgvThicknessMappingContextMenu;


        public SettingsFormV2(ISldWorks sldWorksApp, EPDM.Interop.epdm.IEdmVault5 vault)
        {
            InitializeComponent();
            // Display the version number and copyright (from backup)
            // Assuming lblVersionNumber and lblCopyright exist on SettingsFormV2.Designer.cs
            // If not, these lines will cause an error and should be removed or controls added.
            // lblVersionNumber.Text = "Version: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            // lblCopyright.Text = "Copyright © " + DateTime.Now.Year + " Roeslein & Associates, Inc.";


            this.swApp = sldWorksApp;
            this.pdmVault = vault;

            // Load general settings for UI controls (textboxes, checkboxes etc.)
            try
            {
                this.currentGeneralSettings = Settings.LoadSettings();
                if (this.currentGeneralSettings == null)
                {
                    Logger.DebugLog("Warning: Settings.LoadSettings() returned null. General UI fields may not populate correctly.");
                    this.currentGeneralSettings = new Settings(); // Ensure it's not null
                }
                // Set the Settings Version textbox from loaded settings
                txtSettingsVersion.Text = this.currentGeneralSettings.SettingsVersion ?? string.Empty;
                // Call a method to populate general UI fields from currentGeneralSettings (from backup logic)
                PopulateGeneralSettingsUI(); 
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"Error loading general settings: {ex.Message}. General UI fields may use defaults.");
                MessageBox.Show($"Error loading application settings: {ex.Message}\nGeneral settings fields may use defaults.", "Settings Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.currentGeneralSettings = new Settings(); // Fallback
            }

            // Initialize and Load DataGridViews
            InitializeContextMenus(); // From backup

            InitializePropertyMappingsGrid(); // Renamed from InitializeAndLoad...
            LoadPropertyMappingsData();       // Renamed and uses static Settings.LoadPropertyMappings()

            InitializeMaterialMappingsGrid(); // Renamed
            LoadMaterialMappingsData();       // Renamed and uses static Settings.LoadMaterialMappings()

            InitializeThicknessMappingsGrid(); // Renamed
            LoadThicknessMappingsData();       // Renamed and uses static Settings.LoadThicknessMappings()

            InitializePropertyStandardsGrid(); // Re-adding call for PropertyStandards
            LoadPropertyStandardsData();     // Re-adding call for PropertyStandards

            InitializeRawSheetGrid();
            LoadRawSheetData();
            
            // dgvPropertyStandards is NOT initialized or loaded here as per backup file.
            // If it needs to be, separate Initialize & Load methods are required.

            // Wire up event handlers from backup for General Settings tab and form-level buttons
            if (btnOK != null) btnOK.Click += btnOK_Click;
            if (btnCancel != null) btnCancel.Click += btnCancel_Click;
            if (btnBrowseHotFolder != null) btnBrowseHotFolder.Click += btnBrowseHotFolder_Click;
            if (btnUseDefaultHotFolder != null) btnUseDefaultHotFolder.Click += btnUseDefaultHotFolder_Click;
            if (btnBrowseLogFile != null) btnBrowseLogFile.Click += btnBrowseLogFile_Click;
            if (chkEnableLogging != null) chkEnableLogging.CheckedChanged += chkEnableLogging_CheckedChanged;
            if (chkEnableDebugLogging != null) chkEnableDebugLogging.CheckedChanged += chkEnableDebugLogging_CheckedChanged;
            if (btnBrowseDebugLogFile != null) btnBrowseDebugLogFile.Click += btnBrowseDebugLogFile_Click;
            if (BtnBrowseDrawingTemplate != null) BtnBrowseDrawingTemplate.Click += BtnBrowseDrawingTemplate_Click;
            
            // Event handlers for PropertyMappings DGV on General Settings Tab (if buttons are on this tab)
            // The backup shows btnSaveMappings and btnAddNewRow for dgvPropertyMappings.
            // These should be wired up if the controls exist on SettingsFormV2.Designer.cs
            if (btnSaveMappings != null) btnSaveMappings.Click += btnSavePropertyMappings_Click; 
            if (btnAddNewRow != null) btnAddNewRow.Click += btnAddNewPropertyRow_Click; 

            // Event handlers for Material Mappings Tab
            if (BtnAddNewRowMaterial != null) BtnAddNewRowMaterial.Click += BtnAddNewRowMaterial_Click;
            if (btnSaveMappingsMaterial != null) btnSaveMappingsMaterial.Click += btnSaveMappingsMaterial_Click;

            // Event handlers for Thickness Mappings Tab
            if (BtnAddNewThickness != null) BtnAddNewThickness.Click += BtnAddNewThickness_Click;
            if (btnSaveThickness != null) btnSaveThickness.Click += btnSaveThickness_Click;

            // Event handlers for Property Standards Tab
            if (BtnAddNewFileProp != null) BtnAddNewFileProp.Click += BtnAddNewFileProp_Click;
            if (btnSaveFileProp != null) btnSaveFileProp.Click += btnSaveFileProp_Click;

            if (pdmVault == null)
            {
                MessageBox.Show("PDM Vault not available. PDM features will be disabled.", "PDM Not Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // Optionally, disable PDM-dependent controls
            }
        }

        // Method to populate General Settings tab UI controls (from backup logic)
        private void PopulateGeneralSettingsUI()
        {
            if (this.currentGeneralSettings == null) 
            {
                Logger.DebugLog("PopulateGeneralSettingsUI: currentGeneralSettings is null. Cannot populate UI.");
                // Optionally, initialize to new Settings() here if it makes sense for your app flow
                // this.currentGeneralSettings = new Settings(); 
                return;
            }

            // Assuming control names like txtHotFolder, chkEnableLogging, etc., exist on the form designer.
            // If any control is null, a NullReferenceException will occur here.
            // It's good practice to check for null controls if there's any doubt.

            txtHotFolder.Text = this.currentGeneralSettings.LastExportFolder ?? "";
            txtDrawingTemplate.Text = this.currentGeneralSettings.DrawingTemplatePath ?? "";
            
            // For log file paths, the backup code stores the directory. We need to ensure this logic is consistent.
            // The Settings class (from backup) stores the full path including filename for LogFilePath and DebugLogFilePath.
            // The UI (txtLogFile, txtDebugLogFile in backup) seems to display only the directory path.
            // For populating, let's display the directory. Saving will recombine with filename.
            txtLogFile.Text = !string.IsNullOrEmpty(this.currentGeneralSettings.LogFilePath) ? Path.GetDirectoryName(this.currentGeneralSettings.LogFilePath) : "";
            txtDebugLogFile.Text = !string.IsNullOrEmpty(this.currentGeneralSettings.DebugLogFilePath) ? Path.GetDirectoryName(this.currentGeneralSettings.DebugLogFilePath) : "";

            chkEnableLogging.Checked = this.currentGeneralSettings.LoggingEnabled;
            chkEnableDebugLogging.Checked = this.currentGeneralSettings.DebugLoggingEnabled;

            txtTextLayer.Text = this.currentGeneralSettings.TextLayerName ?? "Notes";
            txtBendLineLayer.Text = this.currentGeneralSettings.BendLineLayerName ?? "Bend_Lines"; // Default from Settings backup
            chkShowBendLines.Checked = this.currentGeneralSettings.ShowBendLines;
            chkCheckForLaser.Checked = this.currentGeneralSettings.CheckForLaser; // This property exists in backup Settings.cs

            // Populate cboTextLocation (ComboBox)
            if (cboTextLocation.Items.Count == 0)
            {
                cboTextLocation.Items.Add("Centered");
                cboTextLocation.Items.Add("Top Left");
                cboTextLocation.Items.Add("Top Right");
                cboTextLocation.Items.Add("Bottom Left");
                cboTextLocation.Items.Add("Bottom Right");
            }

            if (!string.IsNullOrEmpty(this.currentGeneralSettings.TextLocation) && cboTextLocation.Items.Contains(this.currentGeneralSettings.TextLocation))
            {
                cboTextLocation.SelectedItem = this.currentGeneralSettings.TextLocation;
            }
            else
            {
                cboTextLocation.SelectedItem = "Centered"; // Default if not found or empty
            }

            // Set enabled state for log controls based on checkboxes (from backup logic)
            SetLogControlsEnabled(chkEnableLogging.Checked);
            SetDebugLogControlsEnabled(chkEnableDebugLogging.Checked);
            
            // Version and Copyright labels - these might be better set once in the constructor if static
            // Or if they are on the General Settings tab specifically:
            if (lblVersionNumber != null) lblVersionNumber.Text = "Version: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            if (lblCopyright != null) lblCopyright.Text = "Copyright © " + DateTime.Now.Year + " Roeslein & Associates, Inc.";
        }

        // Helper methods from backup to manage enabled state of log path controls
        private void SetLogControlsEnabled(bool enabled)
        {
            if (txtLogFile != null) txtLogFile.Enabled = enabled;
            if (btnBrowseLogFile != null) btnBrowseLogFile.Enabled = enabled;
            // Assuming lblLogFile exists, though not explicitly in the backup's SetLogControlsEnabled
            // if (lblLogFile != null) lblLogFile.Enabled = enabled; 
        }

        private void SetDebugLogControlsEnabled(bool enabled)
        {
            if (txtDebugLogFile != null) txtDebugLogFile.Enabled = enabled;
            if (btnBrowseDebugLogFile != null) btnBrowseDebugLogFile.Enabled = enabled;
            // Assuming lblDebugLogFile exists
            // if (lblDebugLogFile != null) lblDebugLogFile.Enabled = enabled; 
        }

        private void SaveGeneralSettings()
        {
            if (this.currentGeneralSettings == null)
            {
                Logger.DebugLog("SaveGeneralSettings: currentGeneralSettings is null. Cannot save.");
                // Potentially create a new Settings object if appropriate, or show error
                // this.currentGeneralSettings = new Settings(); 
                MessageBox.Show("Critical error: Settings object not initialized. Cannot save general settings.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                this.currentGeneralSettings.LastExportFolder = txtHotFolder.Text;
                this.currentGeneralSettings.DrawingTemplatePath = txtDrawingTemplate.Text;
                
                // When saving LogFilePath, combine directory from textbox with the fixed filename
                this.currentGeneralSettings.LogFilePath = !string.IsNullOrWhiteSpace(txtLogFile.Text) ? Path.Combine(txtLogFile.Text, MainLogFileName) : "";
                this.currentGeneralSettings.DebugLogFilePath = !string.IsNullOrWhiteSpace(txtDebugLogFile.Text) ? Path.Combine(txtDebugLogFile.Text, DebugLogFileName) : "";

                this.currentGeneralSettings.LoggingEnabled = chkEnableLogging.Checked;
                this.currentGeneralSettings.DebugLoggingEnabled = chkEnableDebugLogging.Checked;
                
                this.currentGeneralSettings.TextLayerName = txtTextLayer.Text;
                this.currentGeneralSettings.BendLineLayerName = txtBendLineLayer.Text;
                this.currentGeneralSettings.ShowBendLines = chkShowBendLines.Checked;
                this.currentGeneralSettings.CheckForLaser = chkCheckForLaser.Checked;
                this.currentGeneralSettings.TextLocation = cboTextLocation.SelectedItem?.ToString() ?? "Centered";

                // These properties are from Settings.cs but not on the General Settings UI in the screenshot
                // If they need to be preserved or have defaults, that logic is in Settings.cs constructor/load.
                // currentGeneralSettings.UseCustomScale = ...; 
                // currentGeneralSettings.DrawingScale = ...;
                // currentGeneralSettings.AddDimensions = ...;
                // currentGeneralSettings.ImportModelDimensions = ...;
                // currentGeneralSettings.ExportHiddenGeometry = ...;
                // currentGeneralSettings.AddTitleBlockInfo = ...;
                // currentGeneralSettings.TitleBlockText = ...;

                if (this.currentGeneralSettings.SaveSettings()) // SaveSettings in Settings.cs handles XML and Registry
                {
                    Logger.DebugLog("General settings saved successfully.");
                    // Optionally, provide user feedback
                    // MessageBox.Show("General settings saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information); 
                }
                else
                {
                    Logger.DebugLog("Failed to save general settings via currentGeneralSettings.SaveSettings().");
                    MessageBox.Show("Failed to save general settings. Check logs for details.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"Error saving general settings: {ex.Message}");
                MessageBox.Show($"Error saving general settings: {ex.Message}", "Settings Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeContextMenus() // From backup
        {
            dgvPropertyMappingsContextMenu = new ContextMenuStrip();
            var deletePropMapItem = new ToolStripMenuItem("Delete Row");
            // deletePropMapItem.Click += DgvPropertyMappingsDeleteMenuItem_Click; // Ensure this handler exists or is added
            dgvPropertyMappingsContextMenu.Items.Add(deletePropMapItem);

            dgvMaterialMappingContextMenu = new ContextMenuStrip();
            var deleteMatMapItem = new ToolStripMenuItem("Delete Row");
            // deleteMatMapItem.Click += DgvMaterialMappingDeleteMenuItem_Click; // Ensure this handler exists or is added
            dgvMaterialMappingContextMenu.Items.Add(deleteMatMapItem);

            dgvThicknessMappingContextMenu = new ContextMenuStrip();
            var deleteThickMapItem = new ToolStripMenuItem("Delete Row");
            // deleteThickMapItem.Click += DgvThicknessMappingDeleteMenuItem_Click; // Ensure this handler exists or is added
            dgvThicknessMappingContextMenu.Items.Add(deleteThickMapItem);
        }

        private void InitializePropertyMappingsGrid() // Adapted from backup
        {
            if (dgvPropertyMappings == null) { Logger.DebugLog("dgvPropertyMappings is null, skipping init."); return; }
            dgvPropertyMappings.Columns.Clear();
            dgvPropertyMappings.Columns.Add("RowNumber", "Row Number");
            dgvPropertyMappings.Columns.Add("DxfPropertyName", "DXF Property Name");
            dgvPropertyMappings.Columns.Add("SwCustomProperty", "SolidWorks Custom Property");

            dgvPropertyMappings.Columns["RowNumber"].Width = 80;
            dgvPropertyMappings.Columns["RowNumber"].ReadOnly = true;
            dgvPropertyMappings.AllowUserToAddRows = false; // As per backup, explicit add button is used
            dgvPropertyMappings.AllowUserToDeleteRows = false; // Deletion via context menu
            dgvPropertyMappings.RowHeadersVisible = false;
            dgvPropertyMappings.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPropertyMappings.ContextMenuStrip = dgvPropertyMappingsContextMenu;
            // dgvPropertyMappings.MouseDown += DgvPropertyMappings_MouseDown; // Ensure this handler exists

            dgvPropertyMappings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPropertyMappings.Columns["RowNumber"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        }

        private void LoadPropertyMappingsData() // Adapted from backup
        {
            if (dgvPropertyMappings == null) return;
            try
            {
                dgvPropertyMappings.Rows.Clear();
                var mappings = Settings.LoadPropertyMappings(); // Static call from Settings.cs
                if (mappings != null)
                {
                    // Assuming PropertyMapping class has: RowNumber, DxfPropertyName, SwCustomProperty
                    foreach (var mapping in mappings.OrderBy(m => m.RowNumber))
                    {
                        dgvPropertyMappings.Rows.Add(mapping.RowNumber, mapping.DxfPropertyName, mapping.SwCustomProperty);
                    }
                }
                // AddDefaultPropertyMappings(); // Call if needed, as per backup logic if list is empty
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"Error loading property mappings: {ex.Message}");
                MessageBox.Show($"Error loading property mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeMaterialMappingsGrid() // Adapted from backup
        {
            if (dgvMaterialMapping == null) { Logger.DebugLog("dgvMaterialMapping is null, skipping init."); return; }
            dgvMaterialMapping.Columns.Clear();
            dgvMaterialMapping.Columns.Add("RowNumber", "Row Number");
            dgvMaterialMapping.Columns.Add("SwMaterial", "SolidWorks Material");
            dgvMaterialMapping.Columns.Add("DxfMaterial", "DXF Output Material");

            dgvMaterialMapping.Columns["RowNumber"].Width = 80;
            dgvMaterialMapping.Columns["RowNumber"].ReadOnly = true;
            dgvMaterialMapping.AllowUserToAddRows = false;
            dgvMaterialMapping.AllowUserToDeleteRows = false;
            dgvMaterialMapping.RowHeadersVisible = false;
            dgvMaterialMapping.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMaterialMapping.ContextMenuStrip = dgvMaterialMappingContextMenu;
            // dgvMaterialMapping.MouseDown += DgvMaterialMappings_MouseDown; // Ensure this handler exists

            dgvMaterialMapping.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvMaterialMapping.Columns["RowNumber"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        }

        private void LoadMaterialMappingsData() // Adapted from backup
        {
            if (dgvMaterialMapping == null) return;
            try
            {
                dgvMaterialMapping.Rows.Clear();
                var mappings = Settings.LoadMaterialMappings(); // Static call
                if (mappings != null)
                {
                    // Assuming MaterialMapping class has: RowNumber, SwMaterial, DxfMaterial
                    foreach (var mapping in mappings.OrderBy(m => m.RowNumber))
                    {
                        dgvMaterialMapping.Rows.Add(mapping.RowNumber, mapping.SwMaterial, mapping.DxfMaterial);
                    }
                }
                // AddDefaultMaterialMappings(); // Call if needed
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"Error loading material mappings: {ex.Message}");
                MessageBox.Show($"Error loading material mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeThicknessMappingsGrid() // Adapted from backup
        {
            if (dgvThicknessMapping == null) { Logger.DebugLog("dgvThicknessMapping is null, skipping init."); return; }
            dgvThicknessMapping.Columns.Clear();
            dgvThicknessMapping.Columns.Add("RowNumber", "Row Number");
            dgvThicknessMapping.Columns.Add("SwThickness", "SolidWorks Thickness"); // String type from CSV/XML
            dgvThicknessMapping.Columns.Add("DxfThickness", "DXF Output Thickness"); // String type

            dgvThicknessMapping.Columns["RowNumber"].Width = 80;
            dgvThicknessMapping.Columns["RowNumber"].ReadOnly = true;
            dgvThicknessMapping.Columns["SwThickness"].DefaultCellStyle.Format = "N4"; 
            dgvThicknessMapping.Columns["DxfThickness"].DefaultCellStyle.Format = "N4";
            dgvThicknessMapping.AllowUserToAddRows = false;
            dgvThicknessMapping.AllowUserToDeleteRows = false;
            dgvThicknessMapping.RowHeadersVisible = false;
            dgvThicknessMapping.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvThicknessMapping.ContextMenuStrip = dgvThicknessMappingContextMenu;
            // dgvThicknessMapping.MouseDown += DgvThicknessMappings_MouseDown; // Ensure this handler exists

            dgvThicknessMapping.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvThicknessMapping.Columns["RowNumber"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        }

        private void LoadThicknessMappingsData() // Adapted from backup
        {
            if (dgvThicknessMapping == null) return;
            try
            {
                dgvThicknessMapping.Rows.Clear();
                // Settings.LoadThicknessMappings() returns List<ThicknessMapping> where SwThickness/DxfThickness are strings
                var mappings = Settings.LoadThicknessMappings(); // Static call
                if (mappings != null)
                {
                    // Assuming ThicknessMapping class has: RowNumber, SwThickness (string), DxfThickness (string)
                    foreach (var mapping in mappings.OrderBy(m => m.RowNumber))
                    {
                        dgvThicknessMapping.Rows.Add(mapping.RowNumber, mapping.SwThickness, mapping.DxfThickness);
                    }
                }
                // AddDefaultThicknessMappings(); // Call if needed
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"Error loading thickness mappings: {ex.Message}");
                MessageBox.Show($"Error loading thickness mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        // --- dgvPropertyStandards is intentionally not loaded as per backup ---
        // private void InitializeAndLoadPropertyStandardsGrid() { ... } 

        // --- Methods for dgvPropertyStandards ---
        // IMPORTANT: You MUST adapt these method based on your actual data class (e.g., PropertyStandardSetting)
        // and how its data is stored/loaded (e.g., via Settings.cs)
        private void InitializePropertyStandardsGrid()
        {
            if (dgvPropertyStandards == null) { Logger.DebugLog("dgvPropertyStandards is null, skipping init."); return; }
            dgvPropertyStandards.Columns.Clear();
            dgvPropertyStandards.AutoGenerateColumns = false; // Crucial for manual column definition

            // Use nameof for DataPropertyName for type safety if PropertyStandardSetting is an inner class or in the same namespace
            dgvPropertyStandards.Columns.Add(new DataGridViewCheckBoxColumn { Name = "UseDefaultValue", HeaderText = "Use Default Value", DataPropertyName = nameof(PropertyStandardSetting.UseDefaultValue), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            dgvPropertyStandards.Columns.Add(new DataGridViewTextBoxColumn { Name = "PropertyName", HeaderText = "Property Name", DataPropertyName = nameof(PropertyStandardSetting.PropertyName), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            dgvPropertyStandards.Columns.Add(new DataGridViewTextBoxColumn { Name = "DefaultValueExpr", HeaderText = "Default Value/Expr", DataPropertyName = nameof(PropertyStandardSetting.DefaultValueExpr), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvPropertyStandards.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsCustomProperty", HeaderText = "Custom Prop", DataPropertyName = nameof(PropertyStandardSetting.IsCustomProperty), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            dgvPropertyStandards.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsConfigSpecific", HeaderText = "Config Specific", DataPropertyName = nameof(PropertyStandardSetting.IsConfigSpecific), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            dgvPropertyStandards.Columns.Add(new DataGridViewCheckBoxColumn { Name = "UseOnPartFiles", HeaderText = "Use on Part Files", DataPropertyName = nameof(PropertyStandardSetting.UseOnPartFiles), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            dgvPropertyStandards.Columns.Add(new DataGridViewCheckBoxColumn { Name = "UseOnAssemblyFiles", HeaderText = "Use on Assembly Files", DataPropertyName = nameof(PropertyStandardSetting.UseOnAssemblyFiles), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            dgvPropertyStandards.Columns.Add(new DataGridViewCheckBoxColumn { Name = "UseOnDrawingFiles", HeaderText = "Use on Drawing Files", DataPropertyName = nameof(PropertyStandardSetting.UseOnDrawingFiles), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            
            dgvPropertyStandards.AllowUserToAddRows = false; // Using custom button
            dgvPropertyStandards.RowHeadersVisible = true; 
        }

        private void LoadPropertyStandardsData()
        {
            if (dgvPropertyStandards == null) { Logger.DebugLog("LoadPropertyStandardsData: dgvPropertyStandards is null."); return; }

            // MODIFIED: Load from currentGeneralSettings instance after it's loaded
            if (this.currentGeneralSettings == null)
            {
                Logger.Error("LoadPropertyStandardsData: currentGeneralSettings is null. Cannot load property standards. Ensure Settings.LoadSettings() was called and successful.");
                // Optionally, load settings here if not already done, or show an error.
                // this.currentGeneralSettings = Settings.LoadSettings(); // Example: Load if not already loaded
                // if (this.currentGeneralSettings == null) { /* Handle error */ return; }
                MessageBox.Show("Could not load property standards because general settings are not available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                dgvPropertyStandards.DataSource = new BindingList<PropertyStandardSetting>(); // Show empty grid
                return;
            }

            List<PropertyStandardSetting> standards = this.currentGeneralSettings.PropertyStandards;

            if (standards == null || standards.Count == 0)
            {
                Logger.DebugLog("No property standards found in loaded settings (PDM/XML/Defaults). Populating with hardcoded defaults for the grid display.");
                // If PropertyStandards list is empty after loading (e.g. from new PdmSharedSettings or failed load),
                // populate with defaults for the UI. Settings.cs handles default population for the actual settings data.
                standards = new List<PropertyStandardSetting>
                {
                    new PropertyStandardSetting { UseDefaultValue = true,  PropertyName = "Part Number",           DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Description",           DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Revision",              DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Status",                DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Shop Route",            DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Spare Part",            DefaultValueExpr = "0",                               IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "MFG Stocked Item",      DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = true,  PropertyName = "Weight",                DefaultValueExpr = "$PRP:\"SW-Mass\"",                 IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true },
                    new PropertyStandardSetting { UseDefaultValue = true,  PropertyName = "Material",              DefaultValueExpr = "$PRPMODEL:\"SW-Material\"",        IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = true,  PropertyName = "Sheet Metal Thickness", DefaultValueExpr = "$PRPSHEET:\"Thickness\"",          IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Vendor",                DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Vendor Part Number",    DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Raw Material Number",   DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Unit of Measurement",   DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Raw Mat Amount",        DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "legacy Part Number",    DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Legacy Unit of Measure",DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Legacy Raw Mat Amount", DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "NC Punch Programs",     DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "NC Punch Sheet Size",   DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "Raw Material Description", DefaultValueExpr = "", IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true, UseOnAssemblyFiles = false, UseOnDrawingFiles = false },
                    new PropertyStandardSetting { UseDefaultValue = false, PropertyName = "legacy Part Description",  DefaultValueExpr = "", IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true, UseOnAssemblyFiles = false, UseOnDrawingFiles = false }
                };
                // Optionally, save these defaults immediately if the XML was missing/empty
                // Settings.SavePropertyStandards(standards);
            }

            dgvPropertyStandards.DataSource = new BindingList<PropertyStandardSetting>(standards);
            Logger.DebugLog($"Loaded {standards.Count} property standards into dgvPropertyStandards.");
        }

        private void BtnAddNewFileProp_Click(object sender, EventArgs e)
        {
            if (dgvPropertyStandards == null) { Logger.DebugLog("BtnAddNewFileProp_Click: dgvPropertyStandards is null."); return; }

            if (dgvPropertyStandards.DataSource is BindingList<PropertyStandardSetting> bindingList)
            {
                // The PropertyStandardSetting constructor provides defaults for a new row.
                bindingList.Add(new PropertyStandardSetting()); 
                Logger.DebugLog("New PropertyStandardSetting row added to dgvPropertyStandards' BindingList.");
            }
            else
            {
                Logger.DebugLog("dgvPropertyStandards.DataSource is not a BindingList<PropertyStandardSetting>. Cannot add new typed row. This indicates an issue in LoadPropertyStandardsData.");
                MessageBox.Show("Could not add new row: Data source is not correctly configured.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // +++ Methods for dgvRawSheet +++
        private void InitializeRawSheetGrid()
        {
            Logger.DebugLog("InitializeRawSheetGrid started.");
            if (dgvRawSheet == null)
            {
                Logger.DebugLog("InitializeRawSheetGrid: dgvRawSheet is null. Aborting initialization.");
                return;
            }

            dgvRawSheet.AutoGenerateColumns = false;
            dgvRawSheet.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; // Allow manual/programmatic sizing
            dgvRawSheet.AllowUserToResizeColumns = true; // Allow user to resize
            dgvRawSheet.ScrollBars = ScrollBars.Both; // Enable both scrollbars

            // Define columns if not already defined in the designer
            // Clear existing columns first to avoid duplication if this method is called multiple times
            dgvRawSheet.Columns.Clear();

            // PartNumber
            var partNumberCol = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "PartNumber",
                HeaderText = "Part Number",
                Name = "PartNumber",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, // Adjust based on content
                MinimumWidth = 100 // Example minimum width
            };
            dgvRawSheet.Columns.Add(partNumberCol);

            // LegacyPartNumber
            var legacyPartNumberCol = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "LegacyPartNumber",
                HeaderText = "Legacy Part Number",
                Name = "LegacyPartNumber",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 100
            };
            dgvRawSheet.Columns.Add(legacyPartNumberCol);

            // Material
            var materialCol = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Material",
                HeaderText = "Material",
                Name = "Material",
                Width = 200, // Set desired width
                MinimumWidth = 150 // Ensure it's at least this wide
            };
            dgvRawSheet.Columns.Add(materialCol);

            // Thickness
            var thicknessCol = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Thickness",
                HeaderText = "Thickness (in)",
                Name = "Thickness",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N4" }, // Format as number with 4 decimal places
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 80
            };
            dgvRawSheet.Columns.Add(thicknessCol);

            // Description
            var descriptionCol = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Description",
                HeaderText = "Description",
                Name = "Description",
                Width = 250, // Set desired width
                MinimumWidth = 200 // Ensure it's at least this wide
            };
            dgvRawSheet.Columns.Add(descriptionCol);

            // TotalSQInch
            var totalSqInchCol = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TotalSQInch",
                HeaderText = "Total SQ Inch",
                Name = "TotalSQInch",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" },
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 80
            };
            dgvRawSheet.Columns.Add(totalSqInchCol);

            // SheetLengthInch
            var sheetLengthCol = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SheetLengthInch",
                HeaderText = "Sheet Length (in)",
                Name = "SheetLengthInch",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" },
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 80
            };
            dgvRawSheet.Columns.Add(sheetLengthCol);

            // SheetHeightInch
            var sheetHeightCol = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SheetHeightInch",
                HeaderText = "Sheet Height (in)",
                Name = "SheetHeightInch",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" },
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 80
            };
            dgvRawSheet.Columns.Add(sheetHeightCol);

            // LastCost
            var lastCostCol = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "LastCost",
                HeaderText = "Last Cost",
                Name = "LastCost",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" }, // Format as currency
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 80
            };
            dgvRawSheet.Columns.Add(lastCostCol);
            
            // Add context menu for adding/deleting rows (example)
            var contextMenu = new ContextMenuStrip();
            var addNewMenuItem = new ToolStripMenuItem("Add New Row");
            addNewMenuItem.Click += BtnAddNewRawSheet_Click; // Use existing handler
            contextMenu.Items.Add(addNewMenuItem);
            // Add more items like "Delete Selected Row(s)" if needed
            dgvRawSheet.ContextMenuStrip = contextMenu;

            Logger.DebugLog("InitializeRawSheetGrid completed.");
        }

        private void LoadRawSheetData()
        {
            try
            {
                dgvRawSheet.Rows.Clear();
                
                List<RawSheetData> rawSheets = LoadAndParsePdmCsv(pdmCsvFilePath);
                
                foreach (var sheet in rawSheets)
                {
                    int rowIndex = dgvRawSheet.Rows.Add();
                    dgvRawSheet.Rows[rowIndex].Cells["PartNumber"].Value = sheet.PartNumber;
                    dgvRawSheet.Rows[rowIndex].Cells["LegacyPartNumber"].Value = sheet.LegacyPartNumber;
                    dgvRawSheet.Rows[rowIndex].Cells["Material"].Value = sheet.Material;
                    dgvRawSheet.Rows[rowIndex].Cells["Thickness"].Value = sheet.Thickness;
                    dgvRawSheet.Rows[rowIndex].Cells["Description"].Value = sheet.Description;
                    dgvRawSheet.Rows[rowIndex].Cells["TotalSQInch"].Value = sheet.TotalSQInch;
                    dgvRawSheet.Rows[rowIndex].Cells["SheetLengthInch"].Value = sheet.SheetLengthInch;
                    dgvRawSheet.Rows[rowIndex].Cells["SheetHeightInch"].Value = sheet.SheetHeightInch;
                    dgvRawSheet.Rows[rowIndex].Cells["LastCost"].Value = sheet.LastCost;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading raw sheet data: {ex.Message}", ex);
                MessageBox.Show("Error loading raw sheet data: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<RawSheetData> LoadAndParsePdmCsv(string filePath)
        {
            try
            {
                if (pdmVault == null)
                {
                    MessageBox.Show("PDM Vault not available. Using null PDM implementation.", 
                        "PDM Not Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    pdmVault = (EPDM.Interop.epdm.IEdmVault5)PdmHelper.GetNullVault();
                }
                
                EPDM.Interop.epdm.IEdmFolder5 edmFolder = null;
                EPDM.Interop.epdm.IEdmFile5 edmFile = pdmVault.GetFileFromPath(filePath, out edmFolder);
                // edmFolder = (IEdmFolder5)folderObj; // This line is no longer needed

                if (edmFile == null)
                {
                    Logger.DebugLog($"PDM file not found at path: {filePath}");
                    MessageBox.Show($"The Raw Material CSV file was not found in PDM at the expected location:\n{filePath}\nPlease check the path and PDM availability.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return new List<RawSheetData>();
                }

                Logger.DebugLog($"PDM File found: {edmFile.Name}, ID: {edmFile.ID}, Version: {edmFile.CurrentVersion}");
                
                // Get the file to ensure we have the latest version locally
                edmFile.GetFileCopy((int)GetParentWindowHandle(), 0);
                Logger.DebugLog($"Local copy of PDM file '{edmFile.Name}' obtained/updated.");

                // Now read the CSV directly
                if (File.Exists(filePath))
                {
                    string csvData = File.ReadAllText(filePath);
                    Logger.DebugLog($"Successfully read {csvData.Length} characters from CSV file.");
                    return ParseRawSheetCsv(csvData);
                }
                else
                {
                    Logger.DebugLog($"File exists in PDM but not found locally after GetFileCopy: {filePath}");
                    MessageBox.Show($"Error: Failed to read local copy of the CSV file:\n{filePath}", "File Read Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return new List<RawSheetData>();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in LoadAndParsePdmCsv: {ex.Message}", ex);
                MessageBox.Show($"Error loading CSV from PDM: {ex.Message}", "PDM Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return new List<RawSheetData>();
            }
        }

        private List<RawSheetData> ParseRawSheetCsv(string csvData)
        {
            var entries = new List<RawSheetData>();
            if (string.IsNullOrWhiteSpace(csvData))
            {
                Logger.DebugLog("CSV data is empty or null in ParseRawSheetCsv.");
                return entries;
            }

            // Basic CSV parsing, handles quotes and commas within fields
            var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (!lines.Any()) return entries;

            // Skip header line by starting from index 1
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = new List<string>();
                var currentField = new StringBuilder();
                bool inQuotes = false;

                for (int j = 0; j < line.Length; j++)
                {
                    char c = line[j];
                    if (c == '\"')
                    {
                        // Handle double quotes "" as a single quote literal within a quoted field
                        if (inQuotes && j + 1 < line.Length && line[j+1] == '\"')
                        {
                            currentField.Append('\"');
                            j++; // Skip next quote
                        }
                        else
                        {
                            inQuotes = !inQuotes;
                        }
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        values.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                values.Add(currentField.ToString()); // Add the last field

                if (values.Count == 9) // Expecting 9 columns as per RawSheetData
                {
                    try
                    {
                        entries.Add(new RawSheetData
                        {
                            PartNumber = values[0].Trim(),
                            LegacyPartNumber = values[1].Trim(),
                            Material = values[2].Trim(),
                            Thickness = decimal.TryParse(values[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal thk) ? thk : 0m,
                            Description = values[4].Trim(),
                            TotalSQInch = decimal.TryParse(values[5].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal sqi) ? sqi : 0m,
                            SheetLengthInch = decimal.TryParse(values[6].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal slen) ? slen : 0m,
                            SheetHeightInch = decimal.TryParse(values[7].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal shgt) ? shgt : 0m,
                            // Handle currency symbols if present during parsing, though ideally they are not in the raw data for LastCost
                            LastCost = decimal.TryParse(values[8].Trim().Replace("$", "").Replace("£", "").Replace("Â", ""), NumberStyles.Currency, CultureInfo.InvariantCulture, out decimal cost) ? cost : 0m
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.DebugLog($"Error parsing CSV line {i+1}: '{line}'. Error: {ex.Message}");
                        // Optionally, skip this line or add a placeholder with error info
                    }
                }
                else
                {
                    Logger.DebugLog($"Skipping CSV line {i+1} due to incorrect number of columns ({values.Count}): '{line}'");
                }
            }
            Logger.DebugLog($"Parsed {entries.Count} entries from CSV data.");
            return entries;
        }

        private void BtnAddNewRawSheet_Click(object sender, EventArgs e)
        {
            if (dgvRawSheet.DataSource is BindingList<RawSheetData> bindingList)
            {
                bindingList.AddNew(); // This adds a new RawSheetData object to the list and grid
                                      // Default values from RawSheetData constructor (if any) or property initializers will be used.
                                      // Or, you can create an instance and add it:
                                      // var newSheet = new RawSheetData { PartNumber = "New", ... };
                                      // bindingList.Add(newSheet);
                Logger.DebugLog("New row added to dgvRawSheet via BtnAddNewRawSheet_Click.");
            }
            else
            {
                Logger.DebugLog("dgvRawSheet.DataSource is not a BindingList<RawSheetData>. Cannot add new row.");
            }
        }

        private void BtnSaveRawSheet_Click(object sender, EventArgs e)
        {
            Logger.DebugLog("BtnSaveRawSheet_Click triggered.");
            SaveRawSheetData();
        }
        
        private void SaveRawSheetData()
        {
            Logger.DebugLog("Attempting to save dgvRawSheet data to PDM CSV.");
            if (dgvRawSheet == null || pdmVault == null || swApp == null)
            {
                Logger.DebugLog("SaveRawSheetData: One or more critical objects (dgvRawSheet, pdmVault, swApp) are null. Aborting save.");
                MessageBox.Show("Cannot save raw sheet data. Essential components are missing.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!pdmVault.IsLoggedIn)
            {
                Logger.DebugLog("SaveRawSheetData: Not logged into PDM. Aborting save.");
                MessageBox.Show("Not logged into PDM. Please log in and try again.", "PDM Login Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Ensure any pending edits are committed to the DataSource
            try
            {
                dgvRawSheet.EndEdit(); 
            }
            catch (Exception ex)
            {
                 Logger.DebugLog($"Exception during dgvRawSheet.EndEdit(): {ex.Message}. May affect data accuracy for the currently edited cell.");
            }


            EPDM.Interop.epdm.IEdmFile5 edmFile = null;
            EPDM.Interop.epdm.IEdmFolder5 edmFolder = null;
            long parentWinHandle = GetParentWindowHandle();

            try
            {
                edmFile = pdmVault.GetFileFromPath(pdmCsvFilePath, out edmFolder);
                if (edmFile == null)
                {
                    Logger.DebugLog($"SaveRawSheetData: PDM file not found at {pdmCsvFilePath}. Save aborted.");
                    MessageBox.Show($"The Raw Material CSV file was not found in PDM at the expected location:\\n{pdmCsvFilePath}\\nCannot save changes.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Logger.DebugLog($"SaveRawSheetData: Found PDM file '{edmFile.Name}' (ID: {edmFile.ID}) for saving.");

                // Check out the file
                // EdmLockFlag: EdmLock_Simple, EdmLock_KeepHCopy (if you want to keep it checked out)
                // EdmLockFlag.EdmLock_Simple should be sufficient for checkout-edit-checkin
                edmFile.LockFile((int)parentWinHandle, (int)EdmLockFlag.EdmLock_Simple, 0); // 0 for no flags
                Logger.DebugLog($"SaveRawSheetData: PDM file '{edmFile.Name}' locked (checked out).");

                // Serialize DataGridView content to CSV string
                var csvLines = new List<string>();
                // Add header row - MUST match the order in RawSheetData and ParseRawSheetCsv
                csvLines.Add("PartNumber,Legacy Part Number,Material,Thickness,Description,Total SQ Inch,Sheet Length Inch,Sheet Height Inch,Last Cost");

                if (dgvRawSheet.DataSource is BindingList<RawSheetData> dataList)
                {
                    foreach (RawSheetData item in dataList)
                    {
                        // Ensure all fields are present and handle nulls for string fields if necessary
                        string pn = QuoteCsvField(item.PartNumber ?? "");
                        string lpn = QuoteCsvField(item.LegacyPartNumber ?? "");
                        string mat = QuoteCsvField(item.Material ?? ""); // Material from class
                        // Using InvariantCulture for decimal to string conversion to ensure '.' as decimal separator
                        string thk = item.Thickness.ToString(CultureInfo.InvariantCulture);
                        string desc = QuoteCsvField(item.Description ?? "");
                        string tsqi = item.TotalSQInch.ToString(CultureInfo.InvariantCulture);
                        string slen = item.SheetLengthInch.ToString(CultureInfo.InvariantCulture);
                        string shgt = item.SheetHeightInch.ToString(CultureInfo.InvariantCulture);
                        // Store LastCost as a plain number, formatting is for display. Using en-US for save consistency.
                        string lcost = item.LastCost.ToString("F2", CultureInfo.InvariantCulture); // "F2" for two decimal places, always.

                        csvLines.Add($"{pn},{lpn},{mat},{thk},{desc},{tsqi},{slen},{shgt},{lcost}");
                    }
                }
                else
                {
                     Logger.DebugLog("SaveRawSheetData: dgvRawSheet.DataSource is not BindingList<RawSheetData>. Cannot get data to save.");
                     // Should we still attempt to save an empty file or header only? Or just error out?
                     // For now, if there's no data source, we'll just save the header if it was the only thing.
                     // But if it *was* a binding list and somehow it's empty, that's fine.
                }


                string csvContent = string.Join(System.Environment.NewLine, csvLines) + System.Environment.NewLine; // Ensure trailing newline for last record
                
                string localPath = edmFile.GetLocalPath(edmFolder.ID);
                Logger.DebugLog($"SaveRawSheetData: Writing CSV content to local PDM path: {localPath}");
                File.WriteAllText(localPath, csvContent, Encoding.UTF8); // Use UTF-8 for writing

                // ---- START: Populate currentGeneralSettings.RawMaterials ----
                if (this.currentGeneralSettings != null)
                {
                    this.currentGeneralSettings.RawMaterials = new List<Settings.RawMaterial>(); // Clear or initialize
                    if (dgvRawSheet.DataSource is BindingList<RawSheetData> currentDataList)
                    {
                        Logger.DebugLog($"SaveRawSheetData: Populating currentGeneralSettings.RawMaterials with {currentDataList.Count} items.");
                        foreach (RawSheetData csvItem in currentDataList)
                        {
                            try
                            {
                                var settingsItem = new Settings.RawMaterial
                                {
                                    PartNumber = csvItem.PartNumber,
                                    LegacyPartNumber = csvItem.LegacyPartNumber,
                                    Material = csvItem.Material,
                                    Thickness = (double)csvItem.Thickness, // Convert decimal to double
                                    Description = csvItem.Description,
                                    TotalSqInch = (double)csvItem.TotalSQInch, // Convert decimal to double
                                    Length = (double)csvItem.SheetLengthInch, // Corrected: Map SheetLengthInch to Length
                                    Width = (double)csvItem.SheetHeightInch, // Corrected: Map SheetHeightInch to Width
                                    LastCost = csvItem.LastCost // decimal to decimal
                                    // UnitOfMeasure is now part of Settings.RawMaterial, ensure it's set if needed.
                                    // If RawSheetData doesn't have it, Settings.RawMaterial constructor default will be used.
                                };
                                this.currentGeneralSettings.RawMaterials.Add(settingsItem);
                            }
                            catch (Exception exConv)
                            {
                                Logger.DebugLog($"SaveRawSheetData: Error converting RawSheetData item '{csvItem.PartNumber}' to Settings.RawMaterial: {exConv.Message}");
                                // Optionally, decide if one bad row should stop all, or just skip this one.
                            }
                        }
                        Logger.DebugLog($"SaveRawSheetData: Successfully populated currentGeneralSettings.RawMaterials with {this.currentGeneralSettings.RawMaterials.Count} items.");
                    }
                    else
                    {
                        Logger.DebugLog("SaveRawSheetData: dgvRawSheet.DataSource was not BindingList<RawSheetData> when attempting to populate currentGeneralSettings.RawMaterials.");
                    }
                }
                else
                {
                    Logger.DebugLog("SaveRawSheetData: currentGeneralSettings was null. Cannot populate RawMaterials list in settings object.");
                    // This would be a more critical error, as SaveSettings() would later fail or save an empty object if it relies on this.
                }
                // ---- END: Populate currentGeneralSettings.RawMaterials ----

                // Check in the file
                // Comment for check-in: "Updated raw material data via Roeslein Add-in"
                edmFile.UnlockFile((int)parentWinHandle, "Updated raw material data via Roeslein Add-in", 0); // 0 for no flags
                Logger.DebugLog($"SaveRawSheetData: PDM file '{edmFile.Name}' unlocked (checked in). Save successful.");
                MessageBox.Show("Raw sheet data saved successfully to PDM.", "Save Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);

            }
            catch (System.Runtime.InteropServices.COMException cex)
            {
                string errorMsg = GetEdmErrorString(cex.ErrorCode);
                Logger.DebugLog($"SaveRawSheetData: PDM COM Exception: {cex.Message} (Code: {cex.ErrorCode}, PDM Msg: {errorMsg})\\nStackTrace: {cex.StackTrace}");
                MessageBox.Show($"A PDM error occurred while saving raw material data: {errorMsg} (Code: {cex.ErrorCode})", "PDM Operation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Attempt to undo checkout if file was locked
                if (edmFile != null && edmFile.IsLocked)
                {
                    try { edmFile.UndoLockFile((int)parentWinHandle, false); Logger.DebugLog("SaveRawSheetData: Checkout undone due to PDM error."); }
                    catch (Exception undoEx) { Logger.DebugLog($"SaveRawSheetData: Failed to undo checkout: {undoEx.Message}"); }
                }
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"SaveRawSheetData: General Exception: {ex.Message}\\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"An error occurred while saving raw material data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (edmFile != null && edmFile.IsLocked)
                {
                    try { edmFile.UndoLockFile((int)parentWinHandle, false); Logger.DebugLog("SaveRawSheetData: Checkout undone due to general error."); }
                    catch (Exception undoEx) { Logger.DebugLog($"SaveRawSheetData: Failed to undo checkout: {undoEx.Message}"); }
                }
            }
        }

        private string QuoteCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "\"\""; // Represent empty as ""
            // If field contains comma, quote, or newline, then quote it
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\\n") || field.Contains("\\r"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\""; // Escape quotes by doubling them
            }
            return field;
        }

        private void dgvRawSheet_CellValidated(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvRawSheet.Columns[e.ColumnIndex].Name == "LastCost")
            {
                var cell = dgvRawSheet[e.ColumnIndex, e.RowIndex];
                if (cell.Value != null)
                {
                    // Try to parse the current text value of the cell.
                    // This handles cases where the user types something that isn't yet a decimal.
                    if (decimal.TryParse(cell.Value.ToString().Replace("$", "").Replace("£", "").Replace("Â", ""), NumberStyles.Any, CultureInfo.CurrentCulture, out decimal validatedCost))
                    {
                        // Set the cell's Value to the decimal.
                        // This allows the DataGridView's DefaultCellStyle.Format = "c" to work correctly
                        // for display purposes, even if the underlying DataSource value is already a decimal.
                        // This is crucial for visual update after manual edit.
                        cell.Value = validatedCost; 
                    }
                    // else: If parsing fails, leave it as is. The save logic will handle TryParse again.
                    // Or, you could provide immediate feedback or clear the cell.
                }
            }
        }
        
        // Helper to get parent window handle for PDM operations
        private long GetParentWindowHandle()
        {
            if (swApp != null)
            {
                try
                {
                    // Check if swApp.Frame() returns an object and then try to cast/call GetHWnd()
                    object frameObj = swApp.Frame();
                    if (frameObj is SolidWorks.Interop.sldworks.IFrame swFrame)
                    {
                        return (long)swFrame.GetHWnd();
                    }
                    Logger.DebugLog("GetParentWindowHandle: swApp.Frame() did not return an IFrame object or was null.");
                }
                catch (Exception ex)
                {
                    Logger.DebugLog($"GetParentWindowHandle: Error getting SolidWorks frame HWnd: {ex.Message}");
                }
            }
            // Fallback to desktop or current process main window if SW app handle is not available
            // This might not be ideal for PDM dialogs but is better than 0.
            try
            {
                // Using 'this.Handle' from the Form itself as a fallback
                if (this.IsHandleCreated) return (long)this.Handle;
            } catch (Exception ex) {
                 Logger.DebugLog($"GetParentWindowHandle: Error getting form handle: {ex.Message}");
            }
            Logger.DebugLog("GetParentWindowHandle: Returning 0 as no valid window handle found.");
            return 0; // Default or error case
        }
        
        // Helper to get PDM error string
        private string GetEdmErrorString(int hResult)
        {
            if (pdmVault == null) return "PDM vault not available.";
            try
            {
                // Corrected signature for IEdmVault5.GetErrorString
                // void GetErrorString (int hresult, out string bstrErrorString, out string bstrMostRecentExternallyReferencedFile);
                string errorString;
                string mostRecentFile; // This will be populated by PDM if relevant
                pdmVault.GetErrorString(hResult, out errorString, out mostRecentFile); 
                
                if (!string.IsNullOrEmpty(mostRecentFile))
                {
                     return $"{errorString} (Related file: {mostRecentFile})";
                }
                return errorString ?? "Unknown PDM error.";
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"GetEdmErrorString: Exception while trying to get PDM error string for HRESULT {hResult}: {ex.Message}");
                return $"PDM Error HRESULT: {hResult} (Could not retrieve detailed message)";
            }
        }

        // --- Event Handlers for General Settings Tab and Form --- 

        private void btnOK_Click(object sender, EventArgs e)
        {
            Logger.DebugLog("btnOK_Click: Saving all settings.");
            SaveGeneralSettings();
            SavePropertyMappings();
            SaveMaterialMappings();
            SaveThicknessMappings();
            // SavePropertyStandardsData(); // Removed: This is handled by btnSaveFileProp_Click on its specific tab

            // --- BEGIN: Added logic to transfer RawSheetData to currentGeneralSettings.RawMaterials ---
            if (this.currentGeneralSettings != null && dgvRawSheet.DataSource is BindingList<RawSheetData> rawSheetDataList)
            {
                this.currentGeneralSettings.RawMaterials = new List<Settings.RawMaterial>(); // Clear existing
                foreach (RawSheetData sheetData in rawSheetDataList)
                {
                    this.currentGeneralSettings.RawMaterials.Add(new Settings.RawMaterial
                    {
                        PartNumber = sheetData.PartNumber,
                        LegacyPartNumber = sheetData.LegacyPartNumber,
                        Material = sheetData.Material,
                        Thickness = Convert.ToDouble(sheetData.Thickness), 
                        Description = sheetData.Description,
                        TotalSqInch = Convert.ToDouble(sheetData.TotalSQInch),
                        Length = Convert.ToDouble(sheetData.SheetLengthInch),
                        Width = Convert.ToDouble(sheetData.SheetHeightInch),
                        LastCost = sheetData.LastCost
                    });
                }
                Logger.DebugLog($"Transferred {this.currentGeneralSettings.RawMaterials.Count} entries from dgvRawSheet to currentGeneralSettings.RawMaterials.");
                if (!this.currentGeneralSettings.SaveSettings())
                {
                     Logger.Warning("btnOK_Click: Failed to save settings to XML after updating RawMaterials.");
                     MessageBox.Show("Failed to save updated raw material list to the main settings file. Please check logs.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    Logger.Info("btnOK_Click: Successfully saved settings, including updated RawMaterials, to XML.");
                }
            }
            else
            {
                Logger.Warning("btnOK_Click: Could not transfer RawSheetData. currentGeneralSettings is null or dgvRawSheet.DataSource is not BindingList<RawSheetData>.");
            }
            // --- END: Added logic ---

            // Reconfigure logger after saving settings
            if (this.currentGeneralSettings != null)
            {
                Logger.MainLogFilePath = this.currentGeneralSettings.LogFilePath;
                Logger.DebugLogFilePath = this.currentGeneralSettings.DebugLogFilePath;
                Logger.EnableLogging = this.currentGeneralSettings.LoggingEnabled;
                Logger.EnableDebugLogging = this.currentGeneralSettings.DebugLoggingEnabled;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnBrowseHotFolder_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                // Set initial path if txtHotFolder has a valid directory
                if (!string.IsNullOrWhiteSpace(txtHotFolder.Text) && Directory.Exists(txtHotFolder.Text))
                {
                    fbd.SelectedPath = txtHotFolder.Text;
                }
                if (fbd.ShowDialog(this) == DialogResult.OK) // Pass 'this' for proper parent window
                {
                    txtHotFolder.Text = fbd.SelectedPath;
                }
            }
        }

        private void btnUseDefaultHotFolder_Click(object sender, EventArgs e)
        {
            // Default from Settings.cs constructor
            txtHotFolder.Text = @"\\roeslein.com\locations$\RoesleinFabrication\Connex-Metamation Hot Folder\DXFs From PDM";
        }

        private void btnBrowseLogFile_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select Log File Directory";
                if (!string.IsNullOrEmpty(txtLogFile.Text) && Directory.Exists(txtLogFile.Text))
                {
                    fbd.SelectedPath = txtLogFile.Text;
                }
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    txtLogFile.Text = fbd.SelectedPath; 
                    if (this.currentGeneralSettings != null)
                    {
                        this.currentGeneralSettings.LogFilePath = Path.Combine(txtLogFile.Text, MainLogFileName);
                        Logger.MainLogFilePath = this.currentGeneralSettings.LogFilePath;
                    }
                }
            }
        }

        private void chkEnableLogging_CheckedChanged(object sender, EventArgs e)
        {
            SetLogControlsEnabled(chkEnableLogging.Checked);
            if (this.currentGeneralSettings != null)
            {
                Logger.EnableLogging = chkEnableLogging.Checked;
            }
        }

        private void chkEnableDebugLogging_CheckedChanged(object sender, EventArgs e)
        {
            SetDebugLogControlsEnabled(chkEnableDebugLogging.Checked);
            if (this.currentGeneralSettings != null)
            {
                Logger.EnableDebugLogging = chkEnableDebugLogging.Checked;
            }
        }

        private void btnBrowseDebugLogFile_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select Debug Log File Directory";
                if (!string.IsNullOrEmpty(txtDebugLogFile.Text) && Directory.Exists(txtDebugLogFile.Text))
                {
                    fbd.SelectedPath = txtDebugLogFile.Text;
                }
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    txtDebugLogFile.Text = fbd.SelectedPath;
                    if (this.currentGeneralSettings != null)
                    {
                        this.currentGeneralSettings.DebugLogFilePath = Path.Combine(txtDebugLogFile.Text, DebugLogFileName);
                        Logger.DebugLogFilePath = this.currentGeneralSettings.DebugLogFilePath;
                    }
                }
            }
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
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtDrawingTemplate.Text = dialog.FileName;
                }
            }
        }
        
        // --- Methods for dgvPropertyMappings (DXF Property Mappings on General Settings Tab) ---
        // These save/add methods are for the dgvPropertyMappings on the General tab.

        private void SavePropertyMappings() // Corresponds to btnSaveMappings_Click in backup
        {
            if (dgvPropertyMappings == null) return;
            try
            {
                var mappings = new List<PropertyMapping>();
                foreach (DataGridViewRow row in dgvPropertyMappings.Rows)
                {
                    // Ensure row is not the new row placeholder if AllowUserToAddRows is true at some point
                    if (row.IsNewRow) continue; 

                    // Check for nulls before accessing Value, especially for potentially empty new rows
                    string dxfPropName = row.Cells["DxfPropertyName"].Value?.ToString();
                    string swCustProp = row.Cells["SwCustomProperty"].Value?.ToString();
                    string rowNumStr = row.Cells["RowNumber"].Value?.ToString();

                    if (!string.IsNullOrWhiteSpace(dxfPropName) && !string.IsNullOrWhiteSpace(swCustProp) && int.TryParse(rowNumStr, out int rowNum))
                    {
                        mappings.Add(new PropertyMapping
                        {
                            RowNumber = rowNum,
                            DxfPropertyName = dxfPropName,
                            SwCustomProperty = swCustProp
                        });
                    }
                }
                Settings.SavePropertyMappings(mappings); // Static method in Settings.cs
                Logger.DebugLog("Property mappings saved.");
                // MessageBox.Show("Property mappings saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"Error saving property mappings: {ex.Message}");
                MessageBox.Show($"Error saving property mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSavePropertyMappings_Click(object sender, EventArgs e)
        {
            SavePropertyMappings();
        }

        private void btnAddNewPropertyRow_Click(object sender, EventArgs e) // Corresponds to btnAddNewRow_Click in backup
        {
            if (dgvPropertyMappings == null) return;
            // Calculate next row number
            int newRowNumber = 1;
            if (dgvPropertyMappings.Rows.Count > 0)
            {
                // Get the max row number from existing rows, excluding the new row placeholder if present
                newRowNumber = dgvPropertyMappings.Rows.Cast<DataGridViewRow>()
                                .Where(r => !r.IsNewRow && r.Cells["RowNumber"].Value != null)
                                .Max(r => Convert.ToInt32(r.Cells["RowNumber"].Value)) + 1;
            }
            dgvPropertyMappings.Rows.Add(newRowNumber, "", "");
        }

        // --- Methods for dgvMaterialMapping (on Material Mappings Tab) ---
        private void SaveMaterialMappings() 
        {
            if (dgvMaterialMapping == null) return;
            try
            {
                var mappings = new List<MaterialMapping>();
                foreach (DataGridViewRow row in dgvMaterialMapping.Rows)
                {
                    if (row.IsNewRow) continue;
                    string swMat = row.Cells["SwMaterial"].Value?.ToString();
                    string dxfMat = row.Cells["DxfMaterial"].Value?.ToString();
                    string rowNumStr = row.Cells["RowNumber"].Value?.ToString();

                    if (!string.IsNullOrWhiteSpace(swMat) && !string.IsNullOrWhiteSpace(dxfMat) && int.TryParse(rowNumStr, out int rowNum))
                    {
                        mappings.Add(new MaterialMapping
                        {
                            RowNumber = rowNum,
                            SwMaterial = swMat,
                            DxfMaterial = dxfMat
                        });
                    }
                }
                Settings.SaveMaterialMappings(mappings);
                Logger.DebugLog("Material mappings saved.");
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"Error saving material mappings: {ex.Message}");
                MessageBox.Show($"Error saving material mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- Methods for dgvThicknessMapping (on Thickness Mappings Tab) ---
        private void SaveThicknessMappings()
        {
            if (dgvThicknessMapping == null) return;
            try
            {
                var mappings = new List<ThicknessMapping>();
                foreach (DataGridViewRow row in dgvThicknessMapping.Rows)
                {
                    if (row.IsNewRow) continue;
                    string swThick = row.Cells["SwThickness"].Value?.ToString();
                    string dxfThick = row.Cells["DxfThickness"].Value?.ToString();
                    string rowNumStr = row.Cells["RowNumber"].Value?.ToString();

                    if (!string.IsNullOrWhiteSpace(swThick) && !string.IsNullOrWhiteSpace(dxfThick) && int.TryParse(rowNumStr, out int rowNum))
                    {
                        mappings.Add(new ThicknessMapping // Ensure ThicknessMapping class is defined as expected
                        {
                            RowNumber = rowNum,
                            SwThickness = swThick, // These are strings in ThicknessMapping class from backup
                            DxfThickness = dxfThick
                        });
                    }
                }
                Settings.SaveThicknessMappings(mappings); // Static method in Settings.cs
                Logger.DebugLog("Thickness mappings saved.");
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"Error saving thickness mappings: {ex.Message}");
                MessageBox.Show($"Error saving thickness mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- Context Menu Handlers (Example for Property Mappings, adapt for others) ---
        // You would need to add DgvPropertyMappingsDeleteMenuItem_Click, DgvMaterialMappingDeleteMenuItem_Click etc.
        // and wire them up in InitializeContextMenus() if you want delete functionality.
        // Example:
        // private void DgvPropertyMappingsDeleteMenuItem_Click(object sender, EventArgs e)
        // {
        //     if (dgvPropertyMappings.SelectedRows.Count > 0)
        //     {
        //         var selectedRow = dgvPropertyMappings.SelectedRows[0];
        //         if (!selectedRow.IsNewRow)
        //         {
        //             dgvPropertyMappings.Rows.Remove(selectedRow);
        //             SavePropertyMappings(); // Save after delete
        //         }
        //     }
        // }

        // Placeholders for MouseDown events for context menus (from InitializeContextMenus)
        // private void DgvPropertyMappings_MouseDown(object sender, MouseEventArgs e) { /* see backup */ }
        // private void DgvMaterialMappings_MouseDown(object sender, MouseEventArgs e) { /* see backup */ }
        // private void DgvThicknessMappings_MouseDown(object sender, MouseEventArgs e) { /* see backup */ }


        // --- Methods for dgvMaterialMapping (on Material Mappings Tab) ---
        private void BtnAddNewRowMaterial_Click(object sender, EventArgs e)
        {
            if (dgvMaterialMapping == null) return;
            int newRowNumber = 1;
            if (dgvMaterialMapping.Rows.Count > 0)
            {
                newRowNumber = dgvMaterialMapping.Rows.Cast<DataGridViewRow>()
                                .Where(r => !r.IsNewRow && r.Cells["RowNumber"].Value != null)
                                .Max(r => Convert.ToInt32(r.Cells["RowNumber"].Value)) + 1;
            }
            dgvMaterialMapping.Rows.Add(newRowNumber, "", "");
        }

        private void btnSaveMappingsMaterial_Click(object sender, EventArgs e)
        {
            SaveMaterialMappings();
            MessageBox.Show("Material mappings saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        // --- Methods for dgvThicknessMapping (on Thickness Mappings Tab) ---
        private void BtnAddNewThickness_Click(object sender, EventArgs e)
        {
            if (dgvThicknessMapping == null) return;
            int newRowNumber = 1;
            if (dgvThicknessMapping.Rows.Count > 0)
            {
                newRowNumber = dgvThicknessMapping.Rows.Cast<DataGridViewRow>()
                                .Where(r => !r.IsNewRow && r.Cells["RowNumber"].Value != null)
                                .Max(r => Convert.ToInt32(r.Cells["RowNumber"].Value)) + 1;
            }
            dgvThicknessMapping.Rows.Add(newRowNumber, "", "");
        }

        private void btnSaveThickness_Click(object sender, EventArgs e)
        {
            SaveThicknessMappings();
            MessageBox.Show("Thickness mappings saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // --- Methods for dgvPropertyStandards (Standards Settings Tab) ---
        private void btnSaveFileProp_Click(object sender, EventArgs e)
        {
            SavePropertyStandardsData();
        }

        private void SavePropertyStandardsData()
        {
            if (dgvPropertyStandards == null)
            {
                Logger.DebugLog("SavePropertyStandardsData: dgvPropertyStandards is null. Cannot save.");
                MessageBox.Show("Cannot save property standards: Grid is not available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Added null checks for pdmVault and currentGeneralSettings before proceeding
            if (this.pdmVault == null)
            {
                Logger.Error("SavePropertyStandardsData: pdmVault is null. Cannot save PDM shared settings.");
                MessageBox.Show("PDM Vault connection is not available. Cannot save property standards to PDM.", "PDM Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (this.currentGeneralSettings == null)
            {
                Logger.Error("SavePropertyStandardsData: currentGeneralSettings is null. Cannot get SettingsVersion to save PDM shared settings.");
                MessageBox.Show("General settings are not loaded. Cannot determine settings version to save property standards.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // Ensure any pending edits are committed to the DataSource
                dgvPropertyStandards.EndEdit();

                if (dgvPropertyStandards.DataSource is BindingList<PropertyStandardSetting> standardsListBinding)
                {
                    List<PropertyStandardSetting> standardsToSave = standardsListBinding.ToList();
                    string currentVersion = this.currentGeneralSettings.SettingsVersion;

                    if (string.IsNullOrEmpty(currentVersion))
                    {
                        Logger.Warning("SavePropertyStandardsData: SettingsVersion is empty in currentGeneralSettings. Using default '25.1' for saving.");
                        currentVersion = "25.1"; // Fallback, though LoadSettings should provide one.
                    }

                    // MODIFIED: Call the new static save method in Settings.cs
                    bool saveSuccess = Settings.SavePdmSharedSettings(this.pdmVault, currentVersion, standardsToSave);

                    if (saveSuccess)
                    {
                        Logger.Info("Property standards saved successfully to PDM.");
                        MessageBox.Show("Property standards saved successfully to PDM.", "Save Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        // After saving to PDM, update the currentGeneralSettings instance to reflect changes
                        this.currentGeneralSettings.PropertyStandards = standardsToSave;
                        this.currentGeneralSettings.SettingsVersion = currentVersion; // Ensure version is also consistent
                    }
                    else
                    {
                        Logger.Error("Failed to save property standards to PDM (SavePdmSharedSettings returned false).");
                        MessageBox.Show("Failed to save property standards to PDM. Please check logs for details.", "PDM Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    Logger.Warning("SavePropertyStandardsData: dgvPropertyStandards.DataSource is not a BindingList<PropertyStandardSetting>. Cannot save.");
                    MessageBox.Show("Cannot save property standards: Data source is not correctly configured.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error saving property standards", ex);
                MessageBox.Show($"An error occurred while saving property standards: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // The data classes (PropertyMapping, MaterialMapping, ThicknessMapping) are assumed to be defined
    // as they were in your original project structure (e.g., inside or alongside Settings.cs).
    // The example classes I put at the end of the file previously were illustrative.
    // You need your actual:
    // public class PropertyMapping { public int RowNumber {get;set;} public string DxfPropertyName { get; set; } public string SwCustomProperty { get; set; } }
    // public class MaterialMapping { public int RowNumber {get;set;} public string SwMaterial { get; set; } public string DxfMaterial { get; set; } }
    // public class ThicknessMapping { public int RowNumber {get;set;} public string SwThickness { get; set; } public string DxfThickness { get; set; } }
    // public class Settings { ... your full Settings class from backup ... }

    public class RawSheetData
    {
        public string PartNumber { get; set; }
        public string LegacyPartNumber { get; set; }
        public string Material { get; set; }
        public decimal Thickness { get; set; }
        public string Description { get; set; }
        public decimal TotalSQInch { get; set; }
        public decimal SheetLengthInch { get; set; }
        public decimal SheetHeightInch { get; set; }
        public decimal LastCost { get; set; }
    }
}
