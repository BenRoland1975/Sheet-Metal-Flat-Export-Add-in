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

namespace RoesleinAddIn
{
    public partial class SettingsFormV2 : Form
    {
        private ContextMenuStrip dgvContextMenu;
        private ContextMenuStrip dgvMaterialContextMenu;
        private ContextMenuStrip dgvThicknessContextMenu;
        private const string MainLogFileName = "RoesleinAddInLogger.log";
        private const string DebugLogFileName = "RoesleinAddInDebugLog.log";

        public SettingsFormV2()
        {
            InitializeComponent();
            
            // Display the version number dynamically
            lblVersionNumber.Text = "Version: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            
            // Display the copyright dynamically
            lblCopyright.Text = "Copyright © " + DateTime.Now.Year + " Roeslein & Associates, Inc.";
            
            // Initialize context menus
            InitializeContextMenus();
            
            // Initialize data grids
            InitializePropertyMappings();
            InitializeMaterialMappings();
            InitializeThicknessMappings();
            
            // Wire up event handlers
            btnOK.Click += btnOK_Click;
            btnCancel.Click += btnCancel_Click;
            btnBrowseHotFolder.Click += btnBrowseHotFolder_Click;
            btnUseDefaultHotFolder.Click += btnUseDefaultHotFolder_Click;
            btnBrowseLogFile.Click += btnBrowseLogFile_Click;
            chkEnableLogging.CheckedChanged += chkEnableLogging_CheckedChanged;
            chkEnableDebugLogging.CheckedChanged += chkEnableDebugLogging_CheckedChanged;
            btnBrowseDebugLogFile.Click += btnBrowseDebugLogFile_Click;
            btnSaveMappings.Click += btnSaveMappings_Click;
            btnAddNewRow.Click += btnAddNewRow_Click;
            BtnAddNewRowMaterial.Click += BtnAddNewRowMaterial_Click;
            BtnAddNewThickness.Click += BtnAddNewThickness_Click;
            btnSaveMappingsMaterial.Click += btnSaveMappingsMaterial_Click;
            btnSaveThickness.Click += btnSaveThickness_Click;
            BtnBrowseDrawingTemplate.Click += BtnBrowseDrawingTemplate_Click;

            // Load settings and mappings
            LoadSettings();
            LoadPropertyMappings();
            LoadMaterialMappings();
            LoadThicknessMappings();
        }

        private void InitializeContextMenus()
        {
            // Property mappings context menu
            dgvContextMenu = new ContextMenuStrip();
            var deleteMenuItem = new ToolStripMenuItem("Delete Row");
            deleteMenuItem.Click += DgvDeleteMenuItem_Click;
            dgvContextMenu.Items.Add(deleteMenuItem);

            // Material mappings context menu
            dgvMaterialContextMenu = new ContextMenuStrip();
            var deleteMaterialMenuItem = new ToolStripMenuItem("Delete Row");
            deleteMaterialMenuItem.Click += DgvMaterialDeleteMenuItem_Click;
            dgvMaterialContextMenu.Items.Add(deleteMaterialMenuItem);

            // Thickness mappings context menu
            dgvThicknessContextMenu = new ContextMenuStrip();
            var deleteThicknessMenuItem = new ToolStripMenuItem("Delete Row");
            deleteThicknessMenuItem.Click += DgvThicknessDeleteMenuItem_Click;
            dgvThicknessContextMenu.Items.Add(deleteThicknessMenuItem);
        }

        private void InitializePropertyMappings()
        {
            dgvPropertyMappings.Columns.Clear();
            dgvPropertyMappings.Columns.Add("RowNumber", "Row Number");
            dgvPropertyMappings.Columns.Add("DxfPropertyName", "DXF Property Name");
            dgvPropertyMappings.Columns.Add("SwCustomProperty", "SolidWorks Custom Property");

            dgvPropertyMappings.Columns["RowNumber"].Width = 80;
            dgvPropertyMappings.Columns["RowNumber"].ReadOnly = true;
            dgvPropertyMappings.AllowUserToAddRows = false;
            dgvPropertyMappings.AllowUserToDeleteRows = false;
            dgvPropertyMappings.RowHeadersVisible = false;
            dgvPropertyMappings.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPropertyMappings.ContextMenuStrip = dgvContextMenu;
            dgvPropertyMappings.MouseDown += DgvPropertyMappings_MouseDown;
            
            // Auto-size columns
            dgvPropertyMappings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPropertyMappings.Columns["RowNumber"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        }

        private void InitializeMaterialMappings()
        {
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
            dgvMaterialMapping.ContextMenuStrip = dgvMaterialContextMenu;
            dgvMaterialMapping.MouseDown += DgvMaterialMappings_MouseDown;
            
            // Auto-size columns
            dgvMaterialMapping.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvMaterialMapping.Columns["RowNumber"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        }

        private void InitializeThicknessMappings()
        {
            dgvThicknessMapping.Columns.Clear();
            dgvThicknessMapping.Columns.Add("RowNumber", "Row Number");
            dgvThicknessMapping.Columns.Add("SwThickness", "SolidWorks Thickness");
            dgvThicknessMapping.Columns.Add("DxfThickness", "DXF Output Thickness");

            dgvThicknessMapping.Columns["RowNumber"].Width = 80;
            dgvThicknessMapping.Columns["RowNumber"].ReadOnly = true;
            dgvThicknessMapping.AllowUserToAddRows = false;
            dgvThicknessMapping.AllowUserToDeleteRows = false;
            dgvThicknessMapping.RowHeadersVisible = false;
            dgvThicknessMapping.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvThicknessMapping.ContextMenuStrip = dgvThicknessContextMenu;
            dgvThicknessMapping.MouseDown += DgvThicknessMappings_MouseDown;
            
            // Auto-size columns
            dgvThicknessMapping.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvThicknessMapping.Columns["RowNumber"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
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

                txtHotFolder.Text = settings.LastExportFolder ?? "";
                txtLogFile.Text = string.IsNullOrEmpty(settings.LogFilePath) ? "" : Path.GetDirectoryName(settings.LogFilePath);
                txtDebugLogFile.Text = string.IsNullOrEmpty(settings.DebugLogFilePath) ? "" : Path.GetDirectoryName(settings.DebugLogFilePath);
                chkEnableLogging.Checked = settings.LoggingEnabled;
                chkEnableDebugLogging.Checked = settings.DebugLoggingEnabled;
                txtTextLayer.Text = settings.TextLayerName ?? "Notes";
                txtBendLineLayer.Text = settings.BendLineLayerName ?? "Bend";
                chkShowBendLines.Checked = settings.ShowBendLines;
                txtDrawingTemplate.Text = settings.DrawingTemplatePath ?? "";
                // Set Check For Laser default to true if not present in settings
                if (settings == null || !settings.GetType().GetProperties().Any(p => p.Name == "CheckForLaser"))
                    chkCheckForLaser.Checked = true;
                else
                    chkCheckForLaser.Checked = settings.CheckForLaser;

                if (cboTextLocation.Items.Count == 0)
                {
                    cboTextLocation.Items.Add("Centered");
                    cboTextLocation.Items.Add("Top Left");
                    cboTextLocation.Items.Add("Top Right");
                    cboTextLocation.Items.Add("Bottom Left");
                    cboTextLocation.Items.Add("Bottom Right");
                }

                if (!string.IsNullOrEmpty(settings.TextLocation) && cboTextLocation.Items.Contains(settings.TextLocation))
                    cboTextLocation.SelectedItem = settings.TextLocation;
                else
                    cboTextLocation.SelectedItem = "Centered";

                // Set enabled state for log controls
                SetLogControlsEnabled(chkEnableLogging.Checked);
                SetDebugLogControlsEnabled(chkEnableDebugLogging.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetLogControlsEnabled(bool enabled)
        {
            txtLogFile.Enabled = enabled;
            btnBrowseLogFile.Enabled = enabled;
            lblLogFile.Enabled = enabled;
        }

        private void SetDebugLogControlsEnabled(bool enabled)
        {
            txtDebugLogFile.Enabled = enabled;
            btnBrowseDebugLogFile.Enabled = enabled;
            lblDebugLogFile.Enabled = enabled;
        }

        private void LoadPropertyMappings()
        {
            try
            {
                dgvPropertyMappings.Rows.Clear();
                var mappings = Settings.LoadPropertyMappings();
                if (mappings != null)
                {
                    foreach (var mapping in mappings.OrderBy(m => m.RowNumber))
                    {
                        dgvPropertyMappings.Rows.Add(mapping.RowNumber, mapping.DxfPropertyName, mapping.SwCustomProperty);
                    }
                }

                // If no mappings exist, add default mappings
                if (dgvPropertyMappings.Rows.Count == 0)
                {
                    AddDefaultPropertyMappings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading property mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadMaterialMappings()
        {
            try
            {
                dgvMaterialMapping.Rows.Clear();
                var mappings = Settings.LoadMaterialMappings();
                if (mappings != null)
                {
                    foreach (var mapping in mappings.OrderBy(m => m.RowNumber))
                    {
                        dgvMaterialMapping.Rows.Add(mapping.RowNumber, mapping.SwMaterial, mapping.DxfMaterial);
                    }
                }

                // If no mappings exist, add default mappings
                if (dgvMaterialMapping.Rows.Count == 0)
                {
                    AddDefaultMaterialMappings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading material mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadThicknessMappings()
        {
            try
            {
                dgvThicknessMapping.Rows.Clear();
                var mappings = Settings.LoadThicknessMappings();
                if (mappings != null)
                {
                    foreach (var mapping in mappings.OrderBy(m => m.RowNumber))
                    {
                        dgvThicknessMapping.Rows.Add(mapping.RowNumber, mapping.SwThickness, mapping.DxfThickness);
                    }
                }

                // If no mappings exist, add default mappings
                if (dgvThicknessMapping.Rows.Count == 0)
                {
                    AddDefaultThicknessMappings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading thickness mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddDefaultPropertyMappings()
        {
            dgvPropertyMappings.Rows.Add(1, "Part Number", "Part Number");
            dgvPropertyMappings.Rows.Add(2, "Description", "Description");
            dgvPropertyMappings.Rows.Add(3, "Revision", "Revision");
            dgvPropertyMappings.Rows.Add(4, "Material", "Material");
            dgvPropertyMappings.Rows.Add(5, "Thickness", "Sheet Metal Thickness");
            dgvPropertyMappings.Rows.Add(6, "Shop Route", "Shop Route");
        }

        private void AddDefaultMaterialMappings()
        {
            dgvMaterialMapping.Rows.Add(1, "ASTM A1008 Steel- Cold Rolled Sheet", "ASTM A1008 Steel- Cold Rolled Sheet");
            dgvMaterialMapping.Rows.Add(2, "AISI 304 Stainless Steel Sheet", "AISI 304 Stainless Steel Sheet");
            dgvMaterialMapping.Rows.Add(3, "ASTM A36 Steel- Hot Rolled Sheet", "ASTM A36 Steel- Hot Rolled Sheet");
            dgvMaterialMapping.Rows.Add(4, "ASTM A572 Steel- Hot Rolled plate", "ASTM A572 Steel- Hot Rolled plate");
        }

        private void AddDefaultThicknessMappings()
        {
            dgvThicknessMapping.Rows.Add(1, ".1196", ".12");
            dgvThicknessMapping.Rows.Add(2, ".0897", ".09");
            dgvThicknessMapping.Rows.Add(3, ".0598", ".06");
            dgvThicknessMapping.Rows.Add(4, ".0478", ".048");
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new Settings
                {
                    LastExportFolder = txtHotFolder.Text,
                    // Always append the fixed file name
                    LogFilePath = string.IsNullOrWhiteSpace(txtLogFile.Text) ? "" : Path.Combine(txtLogFile.Text, MainLogFileName),
                    DebugLogFilePath = string.IsNullOrWhiteSpace(txtDebugLogFile.Text) ? "" : Path.Combine(txtDebugLogFile.Text, DebugLogFileName),
                    LoggingEnabled = chkEnableLogging.Checked,
                    DebugLoggingEnabled = chkEnableDebugLogging.Checked,
                    TextLayerName = txtTextLayer.Text,
                    BendLineLayerName = txtBendLineLayer.Text,
                    ShowBendLines = chkShowBendLines.Checked,
                    TextLocation = cboTextLocation.SelectedItem?.ToString() ?? "Centered",
                    DrawingTemplatePath = txtDrawingTemplate.Text,
                    CheckForLaser = chkCheckForLaser.Checked
                };
                settings.SaveSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SavePropertyMappings()
        {
            try
            {
                var mappings = new List<PropertyMapping>();
                foreach (DataGridViewRow row in dgvPropertyMappings.Rows)
                {
                    if (row.Cells["DxfPropertyName"].Value != null && row.Cells["SwCustomProperty"].Value != null)
                    {
                        mappings.Add(new PropertyMapping
                        {
                            RowNumber = Convert.ToInt32(row.Cells["RowNumber"].Value),
                            DxfPropertyName = row.Cells["DxfPropertyName"].Value.ToString(),
                            SwCustomProperty = row.Cells["SwCustomProperty"].Value.ToString()
                        });
                    }
                }
                Settings.SavePropertyMappings(mappings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving property mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveMaterialMappings()
        {
            try
            {
                var mappings = new List<MaterialMapping>();
                foreach (DataGridViewRow row in dgvMaterialMapping.Rows)
                {
                    if (row.Cells["SwMaterial"].Value != null && row.Cells["DxfMaterial"].Value != null)
                    {
                        mappings.Add(new MaterialMapping
                        {
                            RowNumber = Convert.ToInt32(row.Cells["RowNumber"].Value),
                            SwMaterial = row.Cells["SwMaterial"].Value.ToString(),
                            DxfMaterial = row.Cells["DxfMaterial"].Value.ToString()
                        });
                    }
                }
                Settings.SaveMaterialMappings(mappings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving material mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveThicknessMappings()
        {
            try
            {
                var mappings = new List<ThicknessMapping>();
                foreach (DataGridViewRow row in dgvThicknessMapping.Rows)
                {
                    if (row.Cells["SwThickness"].Value != null && row.Cells["DxfThickness"].Value != null)
                    {
                        mappings.Add(new ThicknessMapping
                        {
                            RowNumber = Convert.ToInt32(row.Cells["RowNumber"].Value),
                            SwThickness = row.Cells["SwThickness"].Value.ToString(),
                            DxfThickness = row.Cells["DxfThickness"].Value.ToString()
                        });
                    }
                }
                Settings.SaveThicknessMappings(mappings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving thickness mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Event Handlers
        private void btnOK_Click(object sender, EventArgs e)
        {
            SaveSettings();
            SavePropertyMappings();
            SaveMaterialMappings();
            SaveThicknessMappings();

            // Reconfigure logger after saving settings
            var settings = Settings.LoadSettings();
            if (settings != null)
            {
                Logger.Instance.SetSettingsEnabled(settings.LoggingEnabled);
                Logger.Instance.SetDebugEnabled(settings.DebugLoggingEnabled);
                if (!string.IsNullOrWhiteSpace(settings.LogFilePath))
                    Logger.Instance.SetLogFilePath(settings.LogFilePath);
                if (!string.IsNullOrWhiteSpace(settings.DebugLogFilePath))
                    Logger.Instance.SetDebugLogPath(settings.DebugLogFilePath);
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
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtHotFolder.Text = fbd.SelectedPath;
                }
            }
        }

        private void btnUseDefaultHotFolder_Click(object sender, EventArgs e)
        {
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
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtLogFile.Text = fbd.SelectedPath;
                }
            }
        }

        private void chkEnableLogging_CheckedChanged(object sender, EventArgs e)
        {
            SetLogControlsEnabled(chkEnableLogging.Checked);
        }

        private void chkEnableDebugLogging_CheckedChanged(object sender, EventArgs e)
        {
            SetDebugLogControlsEnabled(chkEnableDebugLogging.Checked);
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
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtDebugLogFile.Text = fbd.SelectedPath;
                }
            }
        }

        private void btnSaveMappings_Click(object sender, EventArgs e)
        {
            SavePropertyMappings();
        }

        private void btnSaveMappingsMaterial_Click(object sender, EventArgs e)
        {
            SaveMaterialMappings();
        }

        private void btnSaveThickness_Click(object sender, EventArgs e)
        {
            SaveThicknessMappings();
        }

        private void btnAddNewRow_Click(object sender, EventArgs e)
        {
            int newRowNumber = dgvPropertyMappings.Rows.Count + 1;
            dgvPropertyMappings.Rows.Add(newRowNumber, "", "");
        }

        private void BtnAddNewRowMaterial_Click(object sender, EventArgs e)
        {
            int newRowNumber = dgvMaterialMapping.Rows.Count + 1;
            dgvMaterialMapping.Rows.Add(newRowNumber, "", "");
        }

        private void BtnAddNewThickness_Click(object sender, EventArgs e)
        {
            int newRowNumber = dgvThicknessMapping.Rows.Count + 1;
            dgvThicknessMapping.Rows.Add(newRowNumber, "", "");
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

        // Context menu event handlers
        private void DgvPropertyMappings_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTestInfo = dgvPropertyMappings.HitTest(e.X, e.Y);
                if (hitTestInfo.RowIndex >= 0 && hitTestInfo.RowIndex < dgvPropertyMappings.RowCount)
                {
                    dgvPropertyMappings.ClearSelection();
                    dgvPropertyMappings.Rows[hitTestInfo.RowIndex].Selected = true;
                    dgvContextMenu.Show(dgvPropertyMappings, e.Location);
                }
            }
        }

        private void DgvMaterialMappings_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTestInfo = dgvMaterialMapping.HitTest(e.X, e.Y);
                if (hitTestInfo.RowIndex >= 0 && hitTestInfo.RowIndex < dgvMaterialMapping.RowCount)
                {
                    dgvMaterialMapping.ClearSelection();
                    dgvMaterialMapping.Rows[hitTestInfo.RowIndex].Selected = true;
                    dgvMaterialContextMenu.Show(dgvMaterialMapping, e.Location);
                }
            }
        }

        private void DgvThicknessMappings_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTestInfo = dgvThicknessMapping.HitTest(e.X, e.Y);
                if (hitTestInfo.RowIndex >= 0 && hitTestInfo.RowIndex < dgvThicknessMapping.RowCount)
                {
                    dgvThicknessMapping.ClearSelection();
                    dgvThicknessMapping.Rows[hitTestInfo.RowIndex].Selected = true;
                    dgvThicknessContextMenu.Show(dgvThicknessMapping, e.Location);
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

        private void DgvMaterialDeleteMenuItem_Click(object sender, EventArgs e)
        {
            if (dgvMaterialMapping.SelectedRows.Count > 0)
            {
                var selectedRow = dgvMaterialMapping.SelectedRows[0];
                if (!selectedRow.IsNewRow)
                {
                    dgvMaterialMapping.Rows.Remove(selectedRow);
                    SaveMaterialMappings();
                }
            }
        }

        private void DgvThicknessDeleteMenuItem_Click(object sender, EventArgs e)
        {
            if (dgvThicknessMapping.SelectedRows.Count > 0)
            {
                var selectedRow = dgvThicknessMapping.SelectedRows[0];
                if (!selectedRow.IsNewRow)
                {
                    dgvThicknessMapping.Rows.Remove(selectedRow);
                    SaveThicknessMappings();
                }
            }
        }
    }
}
