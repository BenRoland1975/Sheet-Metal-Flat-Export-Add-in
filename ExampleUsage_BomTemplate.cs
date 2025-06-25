using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;

namespace RoesleinAddIn
{
    /// <summary>
    /// Example class showing how to use the new BOM template export functionality
    /// </summary>
    public class ExampleUsage_BomTemplate
    {
        private ISldWorks _swApp;
        private EPDM.Interop.epdm.IEdmVault5 _pdmVault;

        public ExampleUsage_BomTemplate(ISldWorks swApp, EPDM.Interop.epdm.IEdmVault5 pdmVault)
        {
            _swApp = swApp;
            _pdmVault = pdmVault;
        }

        /// <summary>
        /// Example: Export BOM to Oracle Fusion FBDI template stored in PDM vault
        /// </summary>
        public void ExportBomToOracleFusionTemplate()
        {
            try
            {
                // Path to your FBDI template in PDM vault
                string templatePath = @"\\VaultName\Templates\Oracle_Fusion_BOM_FBDI_Template.xlsx";
                
                // Create BOM processor with PDM vault support
                BomProcessor processor = new BomProcessor(_swApp, _pdmVault);
                
                // Generate BOM data
                string assemblyName;
                List<BomItem> bomItems = processor.GenerateBomData(BomType.Consolidated, out assemblyName);
                
                if (bomItems != null && bomItems.Any())
                {
                    // Define output path (can be local or PDM vault path)
                    string outputPath = $@"\\VaultName\BOM_Exports\{assemblyName}_Oracle_BOM_{DateTime.Now:yyyyMMdd}.xlsx";
                    
                    // Export to template
                    // - templatePath: Path to FBDI template in PDM vault
                    // - outputPath: Where to save the populated file
                    // - BomType.Consolidated: Type of BOM
                    // - assemblyName: Name of the assembly
                    // - "BOM_DATA": Name of worksheet to populate (adjust to match your template)
                    // - 2: Starting row (row 1 might have headers)
                    // - 1: Starting column (column A)
                    bool success = processor.ExportBomToExistingExcelTemplate(
                        bomItems,
                        templatePath,
                        outputPath,
                        BomType.Consolidated,
                        assemblyName,
                        "BOM_DATA",  // Worksheet name in your FBDI template
                        2,           // Start at row 2 (assuming row 1 has headers)
                        1            // Start at column A
                    );
                    
                    if (success)
                    {
                        MessageBox.Show($"BOM successfully exported to Oracle Fusion template:\n{outputPath}", 
                                       "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show("No BOM data available to export.", "Warning", 
                                   MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting BOM to Oracle template: {ex.Message}", 
                               "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Example: Export BOM using the form with template support
        /// </summary>
        public void ExportBomUsingFormWithTemplate()
        {
            try
            {
                // Create BOM export form with PDM vault support
                BomExportForm bomForm = new BomExportForm(_swApp, _pdmVault);
                
                // Option 1: Use standard export (creates new Excel file)
                if (bomForm.ShowDialog() == DialogResult.OK)
                {
                    // Standard export completed
                }
                
                // Option 2: Use template export (populates existing template)
                string templatePath = @"\\VaultName\Templates\Oracle_Fusion_BOM_FBDI_Template.xlsx";
                bool templateSuccess = bomForm.ExportToTemplate(
                    templatePath,
                    "BOM_DATA",  // Worksheet name
                    2,           // Start row
                    1            // Start column
                );
                
                if (templateSuccess)
                {
                    MessageBox.Show("Template export completed successfully!", "Success", 
                                   MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in template export: {ex.Message}", "Error", 
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Example: Batch export multiple assemblies to the same template
        /// </summary>
        public void BatchExportToTemplate()
        {
            try
            {
                string templatePath = @"\\VaultName\Templates\Oracle_Fusion_BOM_FBDI_Template.xlsx";
                BomProcessor processor = new BomProcessor(_swApp, _pdmVault);
                
                // Assuming you have a list of assembly paths or documents to process
                var assemblyPaths = new List<string>
                {
                    @"\\VaultName\Assemblies\Assembly1.SLDASM",
                    @"\\VaultName\Assemblies\Assembly2.SLDASM",
                    @"\\VaultName\Assemblies\Assembly3.SLDASM"
                };
                
                foreach (string assemblyPath in assemblyPaths)
                {
                    // Open the assembly (you'd need to implement this logic)
                    // ModelDoc2 doc = _swApp.OpenDoc6(assemblyPath, ...);
                    
                    // Generate BOM data
                    string assemblyName;
                    List<BomItem> bomItems = processor.GenerateBomData(BomType.Consolidated, out assemblyName);
                    
                    if (bomItems != null && bomItems.Any())
                    {
                        string outputPath = $@"\\VaultName\BOM_Exports\{assemblyName}_Oracle_BOM.xlsx";
                        
                        processor.ExportBomToExistingExcelTemplate(
                            bomItems,
                            templatePath,
                            outputPath,
                            BomType.Consolidated,
                            assemblyName,
                            "BOM_DATA",
                            2,
                            1
                        );
                    }
                    
                    // Close the assembly
                    // _swApp.CloseDoc(doc.GetTitle());
                }
                
                MessageBox.Show("Batch export completed!", "Batch Export", 
                               MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in batch export: {ex.Message}", "Batch Export Error", 
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 