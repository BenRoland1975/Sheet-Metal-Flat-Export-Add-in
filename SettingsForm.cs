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
        private bool loggingEnabled;
        private DataGridView dgvPropertyMappings;
        private ContextMenuStrip dgvContextMenu;

        public SettingsForm()
        {
            InitializeComponent();
            
            // Load icon directly from embedded resource
            try
            {
                System.Reflection.Assembly executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                // Construct the resource name: DefaultNamespace.FileName
                string resourceName = "RoesleinAddIn.Roeslein_32x32.ico"; 
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

            InitializePropertyMappings();
            LoadSettings();
            LoadPropertyMappings();
        }

        private void LoadSettings()
        {
            try
            {
                var settings = Settings.LoadSettings();
                hotFolderPath = settings.LastExportFolder;
                logFilePath = settings.LogFilePath;
                loggingEnabled = settings.LoggingEnabled;
                txtTextLayer.Text = settings.TextLayerName;
                txtBendLineLayer.Text = settings.BendLineLayerName;
                chkShowBendLines.Checked = settings.ShowBendLines;
                
                // Initialize text location dropdown options
                cboTextLocation.Items.Clear();
                cboTextLocation.Items.AddRange(new string[] { "Centered", "Upper Right", "Upper Left", "Lower Right", "Lower Left" });
                
                // Set selected text location from settings
                if (!string.IsNullOrEmpty(settings.TextLocation) && cboTextLocation.Items.Contains(settings.TextLocation))
                {
                    cboTextLocation.SelectedItem = settings.TextLocation;
                }
                else
                {
                    cboTextLocation.SelectedItem = "Centered"; // Default
                }

                txtHotFolder.Text = hotFolderPath;
                txtLogFile.Text = logFilePath;
                chkEnableLogging.Checked = loggingEnabled;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    LoggingEnabled = loggingEnabled,
                    TextLayerName = txtTextLayer.Text,
                    BendLineLayerName = txtBendLineLayer.Text,
                    ShowBendLines = chkShowBendLines.Checked,
                    TextLocation = cboTextLocation.SelectedItem?.ToString() ?? "Centered"
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
                             Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
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
            hotFolderPath = txtHotFolder.Text;
            logFilePath = txtLogFile.Text;
            loggingEnabled = chkEnableLogging.Checked;

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

            SavePropertyMappings();
            SaveSettings();
            DialogResult = DialogResult.OK;
            Close();
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
        }

        private void InitializePropertyMappings()
        {
            dgvContextMenu = new ContextMenuStrip();
            var deleteMenuItem = new ToolStripMenuItem("Delete Row");
            deleteMenuItem.Click += DgvDeleteMenuItem_Click;
            dgvContextMenu.Items.Add(deleteMenuItem);

            dgvPropertyMappings = new DataGridView
            {
                ColumnCount = 3,
                Columns =
                {
                    [0] = { Name = "Row Number", Width = 80 },
                    [1] = { Name = "DXF Property Name" },
                    [2] = { Name = "SolidWorks Custom Property" }
                },
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Location = new Point(16, 305),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                ContextMenuStrip = dgvContextMenu
            };

            // Set the Row Number column to read-only
            dgvPropertyMappings.Columns[0].ReadOnly = true;

            int bottomOfGrid = btnSaveMappings.Location.Y - 10;
            int topOfGrid = dgvPropertyMappings.Location.Y;
            dgvPropertyMappings.Size = new Size(this.ClientSize.Width - 32, bottomOfGrid - topOfGrid);

            dgvPropertyMappings.MouseDown += DgvPropertyMappings_MouseDown;
            dgvPropertyMappings.RowsAdded += DgvPropertyMappings_RowsAdded;

            Controls.Add(dgvPropertyMappings);
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

        private void DgvPropertyMappings_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            // Auto-number the new row
            if (e.RowIndex < dgvPropertyMappings.Rows.Count)
            {
                int maxRowNumber = 0;
                
                // Find the highest row number currently in use
                foreach (DataGridViewRow row in dgvPropertyMappings.Rows)
                {
                    if (row.IsNewRow) continue;
                    
                    if (row.Cells[0].Value != null && int.TryParse(row.Cells[0].Value.ToString(), out int rowNum))
                    {
                        maxRowNumber = Math.Max(maxRowNumber, rowNum);
                    }
                }
                
                // Set the new row's number to maxRowNumber + 1
                dgvPropertyMappings.Rows[e.RowIndex].Cells[0].Value = (maxRowNumber + 1).ToString();
            }
        }

        private void LoadPropertyMappings()
        {
            try
            {
                dgvPropertyMappings.Rows.Clear();
                var mappings = Settings.LoadPropertyMappings();

                if (mappings == null || mappings.Count == 0)
                {
                    // Create default mappings with proper row numbers
                    dgvPropertyMappings.Rows.Add("1", "Part Number", "Part Number");
                    dgvPropertyMappings.Rows.Add("2", "Description", "Description");
                    dgvPropertyMappings.Rows.Add("3", "Revision", "Revision");
                    dgvPropertyMappings.Rows.Add("4", "Material", "Material");
                    dgvPropertyMappings.Rows.Add("5", "Thickness", "Sheet Metal Thickness");
                    dgvPropertyMappings.Rows.Add("6", "Shop Route", "Shop Route");
                }
                else
                {
                    // Sort mappings by row number
                    mappings = mappings.OrderBy(m => m.RowNumber).ToList();
                    
                    foreach (var mapping in mappings)
                    {
                         if (!string.IsNullOrWhiteSpace(mapping.DxfPropertyName) || !string.IsNullOrWhiteSpace(mapping.SwCustomProperty))
                         {
                            dgvPropertyMappings.Rows.Add(
                                mapping.RowNumber.ToString(), 
                                mapping.DxfPropertyName, 
                                mapping.SwCustomProperty);
                         }
                    }
                }
            }
             catch (Exception ex)
             {
                  MessageBox.Show($"Error loading property mappings: {ex.Message}", "Settings Error", 
                      MessageBoxButtons.OK, MessageBoxIcon.Error);
             }
        }

        private void SavePropertyMappings()
        {
            try
            {
                var mappings = new List<PropertyMapping>();
                bool invalidRowFound = false;
                foreach (DataGridViewRow row in dgvPropertyMappings.Rows)
                {
                    if (row.IsNewRow) continue;

                    string rowNumberStr = row.Cells[0].Value?.ToString() ?? string.Empty;
                    string dxfProp = row.Cells[1].Value?.ToString() ?? string.Empty;
                    string swProp = row.Cells[2].Value?.ToString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(dxfProp))
                    {
                        if (!string.IsNullOrWhiteSpace(swProp))
                        {
                            invalidRowFound = true;
                        }
                        continue;
                    }

                    // Parse row number, default to 999 if invalid
                    int rowNumber = 999;
                    if (!int.TryParse(rowNumberStr, out rowNumber))
                    {
                        rowNumber = 999; // Default high number for invalid rows
                    }

                    var mapping = new PropertyMapping
                    {
                        RowNumber = rowNumber,
                        DxfPropertyName = dxfProp,
                        SwCustomProperty = swProp
                    };
                    mappings.Add(mapping);
                }

                if (invalidRowFound)
                {
                    MessageBox.Show("One or more mappings were ignored because the 'DXF Property Name' was empty.", 
                                    "Save Mappings Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                Settings.SavePropertyMappings(mappings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving property mappings: {ex.Message}", "Settings Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSaveMappings_Click(object sender, EventArgs e)
        {
            SavePropertyMappings();
            MessageBox.Show("Mappings saved successfully.", "Save Mappings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    public class PropertyMapping
    {
        public string DxfPropertyName { get; set; }
        public string SwCustomProperty { get; set; }
        public int RowNumber { get; set; }
    }
} 