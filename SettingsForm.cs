using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RoesleinAddIn
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
            
            // Load event handler
            this.Load += SettingsForm_Load;
        }

        private void InitializeComponent()
        {
            // ... existing initialization code ...

            // Add Raw Material tab
            this.tpRawMaterial = new System.Windows.Forms.TabPage();
            this.tpRawMaterial.Text = "Raw Material Mappings";
            this.tpRawMaterial.Name = "tpRawMaterial";
            this.tpRawMaterial.UseVisualStyleBackColor = true;
            this.tabControlSettings.Controls.Add(this.tpRawMaterial);

            // Add Raw Sheet Material data grid and buttons
            this.dgvRawSheet = new System.Windows.Forms.DataGridView();
            this.dgvRawSheet.AllowUserToAddRows = false;
            this.dgvRawSheet.AllowUserToDeleteRows = true;
            this.dgvRawSheet.Dock = System.Windows.Forms.DockStyle.Top;
            this.dgvRawSheet.Size = new System.Drawing.Size(950, 300);
            this.dgvRawSheet.Name = "dgvRawSheet";
            this.dgvRawSheet.Location = new System.Drawing.Point(3, 3);
            this.dgvRawSheet.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvRawSheet.RowHeadersVisible = false;
            this.dgvRawSheet.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            
            // Create button panel for organization
            Panel pnlRawSheetButtons = new System.Windows.Forms.Panel();
            pnlRawSheetButtons.Height = 40;
            pnlRawSheetButtons.Dock = System.Windows.Forms.DockStyle.Top;
            pnlRawSheetButtons.Location = new System.Drawing.Point(3, 303);

            // Add New Row button
            this.btnAddNewRawSheet = new System.Windows.Forms.Button();
            this.btnAddNewRawSheet.Text = "Add New Row";
            this.btnAddNewRawSheet.Width = 120;
            this.btnAddNewRawSheet.Height = 25;
            this.btnAddNewRawSheet.Location = new System.Drawing.Point(10, 10);
            this.btnAddNewRawSheet.Name = "btnAddNewRawSheet";
            this.btnAddNewRawSheet.Click += new System.EventHandler(this.BtnAddNewRawSheet_Click);

            // Save button
            this.btnSaveRawSheet = new System.Windows.Forms.Button();
            this.btnSaveRawSheet.Text = "Save Raw Material";
            this.btnSaveRawSheet.Width = 150;
            this.btnSaveRawSheet.Height = 25;
            this.btnSaveRawSheet.Location = new System.Drawing.Point(140, 10);
            this.btnSaveRawSheet.Name = "btnSaveRawSheet";
            this.btnSaveRawSheet.Click += new System.EventHandler(this.BtnSaveRawSheet_Click);
            
            // Add Raw Linear Material data grid (placeholder for now)
            this.dgvRawLinear = new System.Windows.Forms.DataGridView();
            this.dgvRawLinear.AllowUserToAddRows = false;
            this.dgvRawLinear.AllowUserToDeleteRows = true;
            this.dgvRawLinear.Dock = System.Windows.Forms.DockStyle.Top;
            this.dgvRawLinear.Size = new System.Drawing.Size(950, 200);
            this.dgvRawLinear.Location = new System.Drawing.Point(3, 343);
            this.dgvRawLinear.Name = "dgvRawLinear";
            this.dgvRawLinear.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvRawLinear.RowHeadersVisible = false;
            this.dgvRawLinear.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            
            // Add controls to panels and tabs
            pnlRawSheetButtons.Controls.Add(this.btnAddNewRawSheet);
            pnlRawSheetButtons.Controls.Add(this.btnSaveRawSheet);
            
            this.tpRawMaterial.Controls.Add(this.dgvRawLinear);
            this.tpRawMaterial.Controls.Add(pnlRawSheetButtons);
            this.tpRawMaterial.Controls.Add(this.dgvRawSheet);
            
            // ... continue with existing initialization code ...
        }

        #region Raw Material Tab Variables
        private System.Windows.Forms.TabPage tpRawMaterial;
        private System.Windows.Forms.DataGridView dgvRawSheet;
        private System.Windows.Forms.DataGridView dgvRawLinear;
        private System.Windows.Forms.Button btnAddNewRawSheet;
        private System.Windows.Forms.Button btnSaveRawSheet;
        #endregion

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            // ... existing loading code ...
            
            try
            {
                // Load Raw Material data
                LoadRawSheetMaterialData();
                MessageBox.Show($"Loaded {dgvRawSheet.Rows.Count} raw material items");
            }
            catch (Exception ex)
            {
                Logger.Error("Error in LoadRawSheetMaterialData", ex);
                MessageBox.Show($"Error loading raw materials: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
            // ... continue with existing loading code ...
        }
        
        #region Raw Material Tab Methods
        
        private void LoadRawSheetMaterialData()
        {
            try
            {
                // Set up columns
                dgvRawSheet.Columns.Clear();
                dgvRawSheet.Columns.Add("PartNumber", "Part Number");
                dgvRawSheet.Columns.Add("LegacyPartNumber", "Legacy Part Number");
                dgvRawSheet.Columns.Add("Material", "Material");
                dgvRawSheet.Columns.Add("Thickness", "Thickness");
                dgvRawSheet.Columns.Add("Description", "Description");
                dgvRawSheet.Columns.Add("TotalSQInch", "Total SQ Inch");
                dgvRawSheet.Columns.Add("SheetLengthInch", "Sheet Length Inch");
                dgvRawSheet.Columns.Add("SheetHeightInch", "Sheet Height Inch");
                dgvRawSheet.Columns.Add("LastCost", "Last Cost");
                
                // Adjust column widths
                dgvRawSheet.Columns["PartNumber"].Width = 100;
                dgvRawSheet.Columns["LegacyPartNumber"].Width = 120;
                dgvRawSheet.Columns["Material"].Width = 200;
                dgvRawSheet.Columns["Thickness"].Width = 80;
                dgvRawSheet.Columns["Description"].Width = 200;
                dgvRawSheet.Columns["TotalSQInch"].Width = 80;
                dgvRawSheet.Columns["SheetLengthInch"].Width = 80;
                dgvRawSheet.Columns["SheetHeightInch"].Width = 80;
                dgvRawSheet.Columns["LastCost"].Width = 80;
                
                // Load data from RawMaterialManager
                List<RawSheetMaterial> materials = RawMaterialManager.LoadRawSheetMaterials();
                
                // Add rows to grid
                dgvRawSheet.Rows.Clear();
                foreach (var material in materials)
                {
                    int rowIndex = dgvRawSheet.Rows.Add();
                    DataGridViewRow row = dgvRawSheet.Rows[rowIndex];
                    
                    row.Cells["PartNumber"].Value = material.PartNumber;
                    row.Cells["LegacyPartNumber"].Value = material.LegacyPartNumber;
                    row.Cells["Material"].Value = material.Material;
                    row.Cells["Thickness"].Value = material.Thickness;
                    row.Cells["Description"].Value = material.Description;
                    row.Cells["TotalSQInch"].Value = material.TotalSQInch;
                    row.Cells["SheetLengthInch"].Value = material.SheetLengthInch;
                    row.Cells["SheetHeightInch"].Value = material.SheetHeightInch;
                    row.Cells["LastCost"].Value = material.LastCost;
                }
                
                Logger.Info($"Loaded {materials.Count} raw sheet material entries");
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading raw sheet material data", ex);
                MessageBox.Show($"Error loading raw sheet material data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnAddNewRawSheet_Click(object sender, EventArgs e)
        {
            try
            {
                int rowIndex = dgvRawSheet.Rows.Add();
                dgvRawSheet.FirstDisplayedScrollingRowIndex = rowIndex;
                dgvRawSheet.CurrentCell = dgvRawSheet.Rows[rowIndex].Cells[0];
                dgvRawSheet.Focus();
            }
            catch (Exception ex)
            {
                Logger.Error("Error adding new raw sheet material row", ex);
                MessageBox.Show($"Error adding new row: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnSaveRawSheet_Click(object sender, EventArgs e)
        {
            try
            {
                List<RawSheetMaterial> materials = new List<RawSheetMaterial>();
                
                foreach (DataGridViewRow row in dgvRawSheet.Rows)
                {
                    if (row.IsNewRow)
                        continue;
                        
                    // Skip rows with no part number (required field)
                    if (row.Cells["PartNumber"].Value == null || string.IsNullOrWhiteSpace(row.Cells["PartNumber"].Value.ToString()))
                        continue;
                        
                    RawSheetMaterial material = new RawSheetMaterial
                    {
                        PartNumber = row.Cells["PartNumber"].Value?.ToString(),
                        LegacyPartNumber = row.Cells["LegacyPartNumber"].Value?.ToString(),
                        Material = row.Cells["Material"].Value?.ToString(),
                        Description = row.Cells["Description"].Value?.ToString()
                    };
                    
                    // Parse numeric values safely
                    if (double.TryParse(row.Cells["Thickness"].Value?.ToString(), out double thickness))
                        material.Thickness = thickness;
                        
                    if (double.TryParse(row.Cells["TotalSQInch"].Value?.ToString(), out double totalSqInch))
                        material.TotalSQInch = totalSqInch;
                        
                    if (double.TryParse(row.Cells["SheetLengthInch"].Value?.ToString(), out double sheetLength))
                        material.SheetLengthInch = sheetLength;
                        
                    if (double.TryParse(row.Cells["SheetHeightInch"].Value?.ToString(), out double sheetHeight))
                        material.SheetHeightInch = sheetHeight;
                        
                    if (decimal.TryParse(row.Cells["LastCost"].Value?.ToString(), out decimal lastCost))
                        material.LastCost = lastCost;
                        
                    materials.Add(material);
                }
                
                // Save to file
                RawMaterialManager.SaveRawSheetMaterials(materials);
                
                MessageBox.Show($"Successfully saved {materials.Count} raw sheet material entries", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                // Reload data to ensure consistency
                LoadRawSheetMaterialData();
            }
            catch (Exception ex)
            {
                Logger.Error("Error saving raw sheet material data", ex);
                MessageBox.Show($"Error saving data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        #endregion
    }
} 