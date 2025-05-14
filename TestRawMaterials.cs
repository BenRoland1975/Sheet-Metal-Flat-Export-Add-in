using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RoesleinAddIn
{
    public class TestRawMaterials : Form
    {
        private DataGridView dgvRawSheet;
        private Button btnLoad;
        private Button btnSave;

        public TestRawMaterials()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Raw Materials Test";
            this.Size = new System.Drawing.Size(1200, 600);

            // Create and configure DataGridView
            this.dgvRawSheet = new DataGridView();
            this.dgvRawSheet.AllowUserToAddRows = false;
            this.dgvRawSheet.AllowUserToDeleteRows = true;
            this.dgvRawSheet.Dock = DockStyle.Top;
            this.dgvRawSheet.Height = 500;
            this.dgvRawSheet.Name = "dgvRawSheet";
            this.dgvRawSheet.Location = new System.Drawing.Point(12, 12);
            this.dgvRawSheet.Size = new System.Drawing.Size(1160, 500);

            // Configure columns
            this.dgvRawSheet.Columns.Add("PartNumber", "Part Number");
            this.dgvRawSheet.Columns.Add("LegacyPartNumber", "Legacy Part Number");
            this.dgvRawSheet.Columns.Add("Material", "Material");
            this.dgvRawSheet.Columns.Add("Thickness", "Thickness");
            this.dgvRawSheet.Columns.Add("Description", "Description");
            this.dgvRawSheet.Columns.Add("TotalSQInch", "Total SQ Inch");
            this.dgvRawSheet.Columns.Add("SheetLengthInch", "Sheet Length Inch");
            this.dgvRawSheet.Columns.Add("SheetHeightInch", "Sheet Height Inch");
            this.dgvRawSheet.Columns.Add("LastCost", "Last Cost");

            // Set column widths
            this.dgvRawSheet.Columns["PartNumber"].Width = 100;
            this.dgvRawSheet.Columns["LegacyPartNumber"].Width = 120;
            this.dgvRawSheet.Columns["Material"].Width = 200;
            this.dgvRawSheet.Columns["Thickness"].Width = 80;
            this.dgvRawSheet.Columns["Description"].Width = 200;
            this.dgvRawSheet.Columns["TotalSQInch"].Width = 80;
            this.dgvRawSheet.Columns["SheetLengthInch"].Width = 80;
            this.dgvRawSheet.Columns["SheetHeightInch"].Width = 80;
            this.dgvRawSheet.Columns["LastCost"].Width = 80;

            // Create buttons
            this.btnLoad = new Button();
            this.btnLoad.Text = "Load Data";
            this.btnLoad.Location = new System.Drawing.Point(12, 520);
            this.btnLoad.Size = new System.Drawing.Size(100, 30);
            this.btnLoad.Click += BtnLoad_Click;

            this.btnSave = new Button();
            this.btnSave.Text = "Save Data";
            this.btnSave.Location = new System.Drawing.Point(122, 520);
            this.btnSave.Size = new System.Drawing.Size(100, 30);
            this.btnSave.Click += BtnSave_Click;

            // Add controls to form
            this.Controls.Add(this.dgvRawSheet);
            this.Controls.Add(this.btnLoad);
            this.Controls.Add(this.btnSave);
        }

        private void BtnLoad_Click(object sender, EventArgs e)
        {
            try
            {
                // Load data from settings
                List<RawSheetMaterial> materials = RawMaterialManager.LoadRawSheetMaterials();

                if (materials == null || materials.Count == 0)
                {
                    MessageBox.Show("No materials found in settings. Loading default materials.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    materials = RawMaterialManager.GetDefaultRawSheetMaterials();
                }

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

                MessageBox.Show($"Loaded {materials.Count} raw sheet material entries", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading materials: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving materials: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Main entry point for testing
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TestRawMaterials());
        }
    }
} 