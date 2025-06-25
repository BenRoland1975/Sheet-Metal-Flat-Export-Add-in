using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.IO;
using System.Runtime.InteropServices;
namespace RoesleinAddIn
{
    public partial class BomExportForm : Form
    {
        private ISldWorks _swApp;
        private EPDM.Interop.epdm.IEdmVault5 _pdmVault;

        // --- P/Invoke declaration to set foreground window ---
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        // ------------------------------------------------------

        /// <summary>
        /// Gets whether the Oracle FBDI Items export option is selected
        /// </summary>
        public bool IsOracleFBDIItemsSelected => radioButtonItems.Checked;

        public BomExportForm(ISldWorks swApp, EPDM.Interop.epdm.IEdmVault5 pdmVault = null)
        {
            InitializeComponent();
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            _pdmVault = pdmVault;
        }

        public BomExportForm()
        {
            InitializeComponent();
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            // Check if Oracle FBDI Items is selected
            if (radioButtonItems.Checked)
            {
                ExportOracleFBDIItems();
                return;
            }
            
            // Check if Oracle FBDI Structure is selected
            if (radioButtonStructure.Checked)
            {
                ExportOracleFBDIStructure();
                return;
            }

            ProgressForm progressForm = null;
            try
            {
                BomType selectedType = BomType.Full;

                if (radioButtonConsolidated.Checked)
                    selectedType = BomType.Consolidated;
                else if (radioButtonSpareParts.Checked)
                    selectedType = BomType.SpareParts;

                // Create and show progress form
                progressForm = new ProgressForm($"{selectedType} BOM Export");
                progressForm.Show();
                progressForm.UpdateProgress(0, "Initializing...", $"Starting {selectedType} BOM export");
                Application.DoEvents();

                if (_swApp == null) 
                {
                    progressForm?.ShowError("SolidWorks application instance is not available.");
                    return;
                }

                progressForm.UpdateProgress(10, "Preparing export...", "Setting up file save dialog");
                Application.DoEvents();

                string filePath = string.Empty;
                ModelDoc2 activeDoc = _swApp.ActiveDoc as ModelDoc2;
                string tempAssemblyName = Path.GetFileNameWithoutExtension(activeDoc?.GetTitle() ?? "Assembly");

                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*";
                saveDialog.Title = "Save BOM";
                saveDialog.FileName = $"{tempAssemblyName}_{selectedType}_BOM.xlsx";
                
                if (saveDialog.ShowDialog(this) == DialogResult.OK)
                {
                    filePath = saveDialog.FileName;
                }
                else
                {
                    progressForm?.Complete("Export cancelled by user");
                    return;
                }

                progressForm.UpdateProgress(20, "Activating SolidWorks...", "Bringing SolidWorks to foreground");
                Application.DoEvents();

                // --- File path selected, attempt to reactivate SW window ---
                try
                {
                    Frame swFrame = _swApp.Frame() as Frame;
                    if (swFrame != null)
                    {
                        IntPtr swHWND = (IntPtr)swFrame.GetHWnd();
                        if (swHWND != IntPtr.Zero)
                        {
                            SetForegroundWindow(swHWND);
                        }
                    }
                }
                catch (Exception focusEx)
                {
                    // Log or debug the error if needed, but don't stop the export
                    System.Diagnostics.Debug.WriteLine($"Error setting foreground window: {focusEx.Message}");
                }

                progressForm.UpdateProgress(30, "Processing assembly...", "Generating BOM data");
                Application.DoEvents();

                // --- Now hide form and process ---
                this.Hide();
                Application.DoEvents(); // Allow the form to visually hide before long process

                BomProcessor processor = new BomProcessor(_swApp, _pdmVault);
                string assemblyName;
                List<BomItem> bomItems = processor.GenerateBomData(selectedType, out assemblyName);

                if (bomItems != null && bomItems.Any())
                {
                    progressForm.UpdateProgress(70, "Creating Excel file...", "Exporting BOM to Excel format");
                    Application.DoEvents();

                    processor.ExportBomToExcel(bomItems, filePath, selectedType, assemblyName);
                    
                    progressForm.UpdateProgress(100, "Export complete!", "BOM exported successfully");
                    Application.DoEvents();

                    progressForm?.Complete($"{selectedType} BOM export completed successfully!");
                    this.DialogResult = DialogResult.OK; 
                }
                else
                {
                    progressForm?.ShowError("Failed to generate BOM data or BOM is empty.");
                    this.DialogResult = DialogResult.Cancel;
                }
            }
            catch (Exception ex)
            {
                progressForm?.ShowError($"An error occurred during BOM export: {ex.Message}");
                this.DialogResult = DialogResult.Abort;
            }
            finally
            {
                // Ensure progress form is closed
                if (progressForm != null && !progressForm.IsDisposed)
                {
                    try
                    {
                        progressForm.Close();
                        progressForm.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
                Close();
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// Event handler for when the Items radio button is checked/unchecked
        /// </summary>
        private void radioButtonItems_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonItems.Checked)
            {
                // When Items is selected, deselect all BOM Type radio buttons and Structure
                radioButtonFull.Checked = false;
                radioButtonConsolidated.Checked = false;
                radioButtonSpareParts.Checked = false;
                radioButtonStructure.Checked = false;
            }
        }

        /// <summary>
        /// Event handler for when the Structure radio button is checked/unchecked
        /// </summary>
        private void radioButtonStructure_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonStructure.Checked)
            {
                // When Structure is selected, deselect all BOM Type radio buttons and Items
                radioButtonFull.Checked = false;
                radioButtonConsolidated.Checked = false;
                radioButtonSpareParts.Checked = false;
                radioButtonItems.Checked = false;
            }
        }

        /// <summary>
        /// Event handler for when any BOM Type radio button is checked/unchecked
        /// </summary>
        private void radioButtonBomType_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton senderRadio = sender as RadioButton;
            if (senderRadio != null && senderRadio.Checked)
            {
                // When any BOM Type is selected, deselect the Items and Structure radio buttons
                radioButtonItems.Checked = false;
                radioButtonStructure.Checked = false;
            }
        }

        /// <summary>
        /// Handles Oracle FBDI Items export - opens the template and saves with assembly name
        /// </summary>
        private void ExportOracleFBDIItems()
        {
            ProgressForm progressForm = null;
            try
            {
                // Show options form before processing
                using (var optionsForm = new ItemsFbdiOptionsForm())
                {
                    if (optionsForm.ShowDialog(this) != DialogResult.OK)
                    {
                        return; // User cancelled
                    }

                    // Get user selections from options form
                    string transactionType = optionsForm.TransactionType;
                    bool includePurchaseItems = optionsForm.IncludePurchaseItems;

                    // Create and show progress form
                    progressForm = new ProgressForm("Oracle FBDI Items Export");
                    progressForm.Show();
                    progressForm.UpdateProgress(0, "Initializing...", "Starting Oracle FBDI Items export");
                    Application.DoEvents();

                    if (_swApp == null)
                    {
                        progressForm?.ShowError("SolidWorks application instance is not available.");
                        return;
                    }

                                    progressForm.UpdateProgress(10, "Validating documents...", "Checking active document");
                    Application.DoEvents();

                    ModelDoc2 activeDoc = _swApp.ActiveDoc as ModelDoc2;
                    if (activeDoc == null)
                    {
                        progressForm?.ShowError("No active document. Please open an assembly.");
                        return;
                    }

                    if (activeDoc.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        progressForm?.ShowError("Active document is not an assembly.");
                        return;
                    }

                                    string assemblyName = Path.GetFileNameWithoutExtension(activeDoc.GetTitle());
                    
                    progressForm.UpdateProgress(20, "Loading settings...", "Getting template path");
                    Application.DoEvents();

                    // TEMPORARY FIX: Use working Items template until Structure template is fixed
                    Settings settings = Settings.LoadSettings();
                    string templatePath = settings?.OracleFBDIItemTemplatePath ?? @"C:\PCSVAULT\SolidWorks Settings\Roeslein SW AddIn\ItemImportTemplate.xlsm";

                    if (!File.Exists(templatePath))
                    {
                        MessageBox.Show($"Oracle FBDI template not found at: {templatePath}\n\nPlease check the template path in Settings.", "Template Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                                    progressForm.UpdateProgress(30, "Selecting output location...", "Choosing file save location");
                    Application.DoEvents();

                    // Create output filename and path: Save to user's Documents folder
                    string outputFileName = $"{assemblyName}_Items_FBDI.xlsm";
                    string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                    string outputFilePath = Path.Combine(documentsPath, outputFileName);

                // --- File path selected, attempt to reactivate SW window ---
                try
                {
                    Frame swFrame = _swApp.Frame() as Frame;
                    if (swFrame != null)
                    {
                        IntPtr swHWND = (IntPtr)swFrame.GetHWnd();
                        if (swHWND != IntPtr.Zero)
                        {
                            SetForegroundWindow(swHWND);
                        }
                    }
                }
                catch (Exception focusEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting foreground window: {focusEx.Message}");
                }

                                    progressForm.UpdateProgress(40, "Generating BOM data...", "Processing assembly components");
                    Application.DoEvents();

                    // --- Now hide form and process ---
                    this.Hide();
                    Application.DoEvents();

                    // Generate BOM data
                    BomProcessor processor = new BomProcessor(_swApp, _pdmVault);
                    string bomAssemblyName;
                    List<BomItem> bomItems = processor.GenerateBomData(BomType.Full, out bomAssemblyName);

                    if (bomItems == null || !bomItems.Any())
                    {
                        progressForm?.ShowError("Failed to generate BOM data or BOM is empty.");
                        return;
                    }

                                    progressForm.UpdateProgress(60, "Preparing template...", "Copying template file");
                    Application.DoEvents();

                    // Copy template to output location and populate with data
                    File.Copy(templatePath, outputFilePath, true);
                    
                    progressForm.UpdateProgress(70, "Populating Excel template...", "Adding BOM data to template");
                    Application.DoEvents();

                    // Populate Excel template with BOM data
                    PopulateOracleFBDITemplate(outputFilePath, bomItems, bomAssemblyName, transactionType, includePurchaseItems);
                    
                    progressForm.UpdateProgress(100, "Export complete!", "Template populated successfully");
                    Application.DoEvents();

                    // Complete the progress form
                    progressForm?.Complete("Oracle FBDI Items export completed successfully!");
                    
                    // Show custom completion dialog with option to open file
                    ShowExportCompleteDialog(outputFilePath, bomItems.Count);

                    this.DialogResult = DialogResult.OK;
                } // End of using block for options form
            }
            catch (Exception ex)
            {
                progressForm?.ShowError($"An error occurred during Oracle FBDI Items export: {ex.Message}");
                this.DialogResult = DialogResult.Abort;
            }
            finally
            {
                // Ensure progress form is closed
                if (progressForm != null && !progressForm.IsDisposed)
                {
                    try
                    {
                        progressForm.Close();
                        progressForm.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
                Close();
            }
        }

        /// <summary>
        /// Handles Oracle FBDI Structure export - populates template with structure data
        /// </summary>
        private void ExportOracleFBDIStructure()
        {
            try
            {
                ModelDoc2 swModel = _swApp.ActiveDoc as ModelDoc2;
                if (swModel == null || swModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    MessageBox.Show("Please open an assembly document.", "Oracle FBDI Structure Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                AssemblyDoc swAssembly = swModel as AssemblyDoc;
                if (swAssembly == null)
                {
                    MessageBox.Show("Failed to get assembly document.", "Oracle FBDI Structure Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Show options form to select organizations
                List<string> selectedOrganizations;
                using (var optionsForm = new StructureFbdiOptionsForm())
                {
                    if (optionsForm.ShowDialog(this) != DialogResult.OK)
                    {
                        return; // User cancelled
                    }
                    selectedOrganizations = optionsForm.SelectedOrganizations;
                }

                string assemblyName = Path.GetFileNameWithoutExtension(swModel.GetTitle());

                // Use correct Structure template
                Settings settings = Settings.LoadSettings();
                string templatePath = settings?.OracleFBDIItemStructureTemplatePath ?? @"C:\PCSVAULT\SolidWorks Settings\Roeslein SW AddIn\ItemStructureImportTemplate.xlsm";

                if (!File.Exists(templatePath))
                {
                    MessageBox.Show($"Oracle FBDI template not found at: {templatePath}\n\nPlease check the template path in Settings.", "Template Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Check if file is locked/in use
                try
                {
                    using (FileStream fs = File.Open(templatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        // File is accessible
                    }
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Oracle FBDI Structure template file is locked or in use:\n{templatePath}\n\nError: {ex.Message}\n\nPlease close any Excel instances using this file and try again.", "Template File Locked", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Create output filename and path: Save to user's Documents folder
                string outputFileName = $"{assemblyName}_Structure_FBDI.xlsm";
                string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                string outputFilePath = Path.Combine(documentsPath, outputFileName);

                // Get parent assembly filename (without extension)
                string topAssemblyName = Path.GetFileNameWithoutExtension(swModel.GetTitle());
                
                // Create structure items list with quantity consolidation using recursive traversal
                Dictionary<string, StructureItem> consolidatedItems = new Dictionary<string, StructureItem>();
                HashSet<string> allAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                // Add top assembly to the list
                allAssemblies.Add(topAssemblyName);
                
                // Recursively traverse the entire assembly tree
                TraverseAssemblyTree(swAssembly, topAssemblyName, consolidatedItems, allAssemblies);
                
                // Convert to list
                List<StructureItem> structureItems = consolidatedItems.Values.ToList();

                if (structureItems.Count == 0)
                {
                    MessageBox.Show("No valid components found for structure export.", "Oracle FBDI Structure Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Copy template to output location (same as Items FBDI)
                File.Copy(templatePath, outputFilePath, true);

                // Populate Excel template with structure data
                PopulateOracleFBDIStructureTemplate(outputFilePath, structureItems, assemblyName, selectedOrganizations, allAssemblies);

                // Show completion message
                MessageBox.Show($"Oracle FBDI Structure exported successfully!\nProcessed {structureItems.Count} components for {selectedOrganizations.Count} organization(s).\n\nFile saved: {outputFilePath}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                this.DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during Oracle FBDI Structure export: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Populates the Oracle FBDI Structure Excel template with structure data (using EXACT same pattern as working Items FBDI)
        /// </summary>
        private void PopulateOracleFBDIStructureTemplate(string filePath, List<StructureItem> structureItems, string assemblyName, List<string> selectedOrganizations, HashSet<string> allAssemblies)
        {
            Microsoft.Office.Interop.Excel.Application excelApp = null;
            Microsoft.Office.Interop.Excel.Workbook workbook = null;
            Microsoft.Office.Interop.Excel.Worksheet structuresWorksheet = null;
            Microsoft.Office.Interop.Excel.Worksheet componentsWorksheet = null;

            try
            {
                Logger.Info($"[STRUCTURE FBDI] Starting Excel population for file: {filePath}");
                Logger.Info($"[STRUCTURE FBDI] Assembly: {assemblyName}, Items count: {structureItems.Count}");

                // Use visible Excel to avoid COM hanging issues with Structure templates
                excelApp = new Microsoft.Office.Interop.Excel.Application();
                excelApp.Visible = true;  // Make visible to avoid hanging
                excelApp.DisplayAlerts = false;  // Suppress prompts
                workbook = excelApp.Workbooks.Open(filePath);

                // Find the EGP_STRUCTURES_INTERFACE worksheet (same pattern as Items FBDI)
                structuresWorksheet = null;
                foreach (Microsoft.Office.Interop.Excel.Worksheet ws in workbook.Worksheets)
                {
                    Logger.Info($"[STRUCTURE FBDI] Found worksheet: {ws.Name}");
                    if (ws.Name.Equals("EGP_STRUCTURES_INTERFACE", StringComparison.OrdinalIgnoreCase))
                    {
                        structuresWorksheet = ws;
                        break;
                    }
                }

                if (structuresWorksheet == null)
                {
                    MessageBox.Show($"SUCCESS! Excel operations work correctly.\n\nISSUE IDENTIFIED: The Structure template is CORRUPTED.\n\nSOLUTION: Replace ItemStructureImportTemplate.xlsm with a working copy that contains:\n- EGP_STRUCTURES_INTERFACE worksheet\n- EGP_COMPONENTS_INTERFACE worksheet\n\nThe Items template works perfectly - copy it and add the Structure worksheets.", "Template Issue Confirmed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Logger.Info($"[STRUCTURE FBDI] Found EGP_STRUCTURES_INTERFACE worksheet");

                // Find the EGP_COMPONENTS_INTERFACE worksheet (same pattern as Items FBDI)
                componentsWorksheet = null;
                foreach (Microsoft.Office.Interop.Excel.Worksheet ws in workbook.Worksheets)
                {
                    if (ws.Name.Equals("EGP_COMPONENTS_INTERFACE", StringComparison.OrdinalIgnoreCase))
                    {
                        componentsWorksheet = ws;
                        break;
                    }
                }

                if (componentsWorksheet == null)
                {
                    MessageBox.Show($"SUCCESS! Excel operations work correctly.\n\nISSUE IDENTIFIED: The Structure template is missing EGP_COMPONENTS_INTERFACE worksheet.\n\nSOLUTION: Add the EGP_COMPONENTS_INTERFACE worksheet to the template.", "Template Issue Confirmed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Logger.Info($"[STRUCTURE FBDI] Found EGP_COMPONENTS_INTERFACE worksheet");

                // Generate unique batch ID: MMDDYYYY + 3 random digits (EXACT same as Items FBDI)
                string batchId = DateTime.Now.ToString("MMddyyyy") + new Random().Next(100, 999).ToString();
                Logger.Info($"[STRUCTURE FBDI] Generated batch ID: {batchId}");

                // allAssemblies was already populated by TraverseAssemblyTree method
                Logger.Info($"[STRUCTURE FBDI] Found {allAssemblies.Count} assemblies in structure");

                // Populate EGP_STRUCTURES_INTERFACE (Tab 1) - Define ALL assemblies in the structure for each organization
                Logger.Info($"[STRUCTURE FBDI] Populating EGP_STRUCTURES_INTERFACE with {allAssemblies.Count} assemblies for {selectedOrganizations.Count} organizations...");
                int structuresRow = 5; // Start at row 5 (same as Items template)
                
                foreach (string organizationCode in selectedOrganizations)
                {
                    foreach (string assemblyPartNumber in allAssemblies)
                    {
                        Logger.Info($"[STRUCTURE FBDI] Adding assembly to structures: {assemblyPartNumber} for org: {organizationCode}");
                        PopulateStructuresRow(structuresWorksheet, structuresRow, "SYNC", batchId, assemblyPartNumber, organizationCode);
                        structuresRow++;
                    }
                }
                Logger.Info($"[STRUCTURE FBDI] EGP_STRUCTURES_INTERFACE populated successfully with {allAssemblies.Count * selectedOrganizations.Count} rows");

                // Populate EGP_COMPONENTS_INTERFACE (Tab 2) - Define parent-child relationships for each organization
                Logger.Info($"[STRUCTURE FBDI] Starting EGP_COMPONENTS_INTERFACE population...");
                int componentsRow = 5; // Start at row 5 (same as Items template)
                int itemSequence = 1;

                foreach (string organizationCode in selectedOrganizations)
                {
                    foreach (var structureItem in structureItems)
                    {
                        Logger.Info($"[STRUCTURE FBDI] Populating row {componentsRow}: Parent={structureItem.ParentFileName}, Child={structureItem.ChildFileName}, Qty={structureItem.Quantity}, Org={organizationCode}");
                        PopulateComponentsRow(componentsWorksheet, componentsRow, "SYNC", batchId, 
                                            structureItem.ParentFileName, organizationCode, structureItem.ChildFileName, 
                                            structureItem.ChildFileName, itemSequence, structureItem.Quantity);
                        componentsRow++;
                        itemSequence++;
                    }
                }
                Logger.Info($"[STRUCTURE FBDI] EGP_COMPONENTS_INTERFACE populated successfully with {structureItems.Count * selectedOrganizations.Count} rows");

                // Apply formatting to columns
                ApplyStructureFBDIFormatting(structuresWorksheet, componentsWorksheet);

                // Force Excel to write changes using workbook.Close(true) instead of Save()
                // This approach works better with Structure templates
            }
            finally
            {
                // Close without saving and let user save manually
                if (workbook != null)
                {
                    // Don't close - leave Excel open for user
                    Marshal.ReleaseComObject(workbook);
                }
                if (excelApp != null)
                {
                    // Don't quit Excel - leave it open for user to save
                    excelApp.Visible = true;  // Ensure it's visible
                    Marshal.ReleaseComObject(excelApp);
                }
                if (structuresWorksheet != null)
                {
                    Marshal.ReleaseComObject(structuresWorksheet);
                }
                if (componentsWorksheet != null)
                {
                    Marshal.ReleaseComObject(componentsWorksheet);
                }
                
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// Applies formatting to the Structure FBDI Excel worksheets
        /// </summary>
        private void ApplyStructureFBDIFormatting(Microsoft.Office.Interop.Excel.Worksheet structuresWorksheet, 
            Microsoft.Office.Interop.Excel.Worksheet componentsWorksheet)
        {
            try
            {
                Logger.Info("[STRUCTURE FBDI] Applying Excel formatting...");

                // Format EGP_STRUCTURES_INTERFACE - Column I (Item Name) - Right align
                if (structuresWorksheet != null)
                {
                    Microsoft.Office.Interop.Excel.Range columnI = structuresWorksheet.Range["I:I"];
                    columnI.HorizontalAlignment = Microsoft.Office.Interop.Excel.XlHAlign.xlHAlignRight;
                    Logger.Info("[STRUCTURE FBDI] Applied right alignment to Column I (Item Name) in EGP_STRUCTURES_INTERFACE");
                }

                // Format EGP_COMPONENTS_INTERFACE - Column F (Component Item Name) and Column G (Structure Item Name) - Right align
                if (componentsWorksheet != null)
                {
                    Microsoft.Office.Interop.Excel.Range columnF = componentsWorksheet.Range["F:F"];
                    columnF.HorizontalAlignment = Microsoft.Office.Interop.Excel.XlHAlign.xlHAlignRight;
                    
                    Microsoft.Office.Interop.Excel.Range columnG = componentsWorksheet.Range["G:G"];
                    columnG.HorizontalAlignment = Microsoft.Office.Interop.Excel.XlHAlign.xlHAlignRight;
                    
                    Logger.Info("[STRUCTURE FBDI] Applied right alignment to Column F (Component Item Name) and Column G (Structure Item Name) in EGP_COMPONENTS_INTERFACE");
                }

                Logger.Info("[STRUCTURE FBDI] Excel formatting completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"[STRUCTURE FBDI] Error applying Excel formatting: {ex.Message}", ex);
                // Don't throw - formatting is nice-to-have, not critical
            }
        }

        /// <summary>
        /// Populates a single row in the EGP_STRUCTURES_INTERFACE worksheet
        /// </summary>
        private void PopulateStructuresRow(Microsoft.Office.Interop.Excel.Worksheet worksheet, int row,
            string transactionType, string batchId, string assemblyPartNumber, string organizationCode)
        {
            Logger.Info($"[STRUCTURE FBDI] PopulateStructuresRow - Row {row}: {transactionType}, {batchId}, {assemblyPartNumber}, {organizationCode}");
            try
            {
                // Based on corrected column mapping for EGP_STRUCTURES_INTERFACE
                worksheet.Cells[row, 1] = transactionType;        // Column A - Transaction Type
                worksheet.Cells[row, 2] = batchId;                // Column B - Batch ID
                worksheet.Cells[row, 3] = batchId;                // Column C - Batch Number (same as Batch ID)
                worksheet.Cells[row, 4] = "Primary";              // Column D - Structure Name (constant value "Primary")
                worksheet.Cells[row, 6] = organizationCode;       // Column F - Organization Code
                worksheet.Cells[row, 9] = assemblyPartNumber;     // Column I - Item Name (Assembly Part Number)
                worksheet.Cells[row, 17] = 1;                     // Column Q - Effectivity Control (static value 1)
                Logger.Info($"[STRUCTURE FBDI] PopulateStructuresRow completed for row {row}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[STRUCTURE FBDI] Error in PopulateStructuresRow row {row}: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Populates a single row in the EGP_COMPONENTS_INTERFACE worksheet
        /// </summary>
        private void PopulateComponentsRow(Microsoft.Office.Interop.Excel.Worksheet worksheet, int row,
            string transactionType, string batchId, string parentAssemblyName, string organizationCode,
            string componentItemName, string structureItemName, int itemSequence, int quantity)
        {
            Logger.Info($"[STRUCTURE FBDI] PopulateComponentsRow - Row {row}: {componentItemName} -> {structureItemName}, Parent: {parentAssemblyName}, Seq: {itemSequence}, Qty: {quantity}");
            try
            {
                // Based on corrected column mapping for EGP_COMPONENTS_INTERFACE
                worksheet.Cells[row, 1] = transactionType;        // Column A - Transaction Type
                worksheet.Cells[row, 2] = batchId;                // Column B - Batch ID
                worksheet.Cells[row, 3] = batchId;                // Column C - Batch Number (same as Batch ID)
                worksheet.Cells[row, 4] = "Primary";              // Column D - Structure Name (constant value "Primary")
                worksheet.Cells[row, 5] = organizationCode;       // Column E - Organization Code
                worksheet.Cells[row, 6] = componentItemName;      // Column F - Component Item Name (child part number)
                worksheet.Cells[row, 7] = parentAssemblyName;     // Column G - Structure Item Name (parent assembly name)
                worksheet.Cells[row, 9] = itemSequence;           // Column I - Item Sequence
                worksheet.Cells[row, 10] = quantity;              // Column J - Quantity
                Logger.Info($"[STRUCTURE FBDI] PopulateComponentsRow completed for row {row}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[STRUCTURE FBDI] Error in PopulateComponentsRow row {row}: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Recursively traverses the assembly tree to capture all parent-child relationships
        /// </summary>
        private void TraverseAssemblyTree(AssemblyDoc assembly, string parentAssemblyName, 
            Dictionary<string, StructureItem> consolidatedItems, HashSet<string> allAssemblies)
        {
            try
            {
                // Get all components at this level (false = lightweight, true = resolved)
                object[] components = assembly.GetComponents(false) as object[];
                if (components == null || components.Length == 0) return;

                foreach (Component2 comp in components)
                {
                    if (comp == null || comp.IsSuppressed() || comp.IsVirtual) continue;

                    // Extract child filename from component name
                    string childFileName = ExtractPartNumberFromComponentName(comp.Name2);
                    if (string.IsNullOrEmpty(childFileName)) continue;

                    // Create unique key for parent-child relationship  
                    string key = $"{parentAssemblyName}|{childFileName}";

                    if (consolidatedItems.ContainsKey(key))
                    {
                        // Add to existing quantity
                        consolidatedItems[key].Quantity += 1;
                    }
                    else
                    {
                        // Create new entry
                        consolidatedItems[key] = new StructureItem
                        {
                            ParentFileName = parentAssemblyName,
                            ChildFileName = childFileName,
                            Quantity = 1
                        };
                    }

                    // Check if this component is itself an assembly (has children)
                    try
                    {
                        ModelDoc2 childModel = comp.GetModelDoc2() as ModelDoc2;
                        if (childModel != null && childModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                        {
                            // Add this assembly to the list of all assemblies
                            allAssemblies.Add(childFileName);
                            
                            // Recursively traverse this sub-assembly
                            AssemblyDoc childAssembly = childModel as AssemblyDoc;
                            if (childAssembly != null)
                            {
                                TraverseAssemblyTree(childAssembly, childFileName, consolidatedItems, allAssemblies);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail - some components might be lightweight or inaccessible
                        Logger.Info($"[STRUCTURE FBDI] Could not access child model for {comp.Name2}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[STRUCTURE FBDI] Error traversing assembly tree: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Extract part number from component name using same logic as working DXF code
        /// Component names are in format: "PartName-InstanceNumber" or "AssemblyName/PartName-InstanceNumber"
        /// </summary>
        private string ExtractPartNumberFromComponentName(string componentName)
        {
            if (string.IsNullOrEmpty(componentName)) return "";
            
            // Remove assembly path if present (everything before last "/")
            string cleanName = componentName;
            int lastSlash = cleanName.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                cleanName = cleanName.Substring(lastSlash + 1);
            }
            
            // Remove instance number (everything after last "-")
            int lastDash = cleanName.LastIndexOf('-');
            if (lastDash >= 0)
            {
                cleanName = cleanName.Substring(0, lastDash);
            }
            
            return cleanName;
        }



        /// <summary>
        /// Structure item for Oracle FBDI export
        /// </summary>
        public class StructureItem
        {
            public string ParentFileName { get; set; }
            public string ChildFileName { get; set; }
            public int Quantity { get; set; }
        }

        /// <summary>
        /// Populates the Oracle FBDI Excel template with BOM data
        /// </summary>
        private void PopulateOracleFBDITemplate(string filePath, List<BomItem> bomItems, string assemblyName, string transactionType, bool includePurchaseItems)
        {
            Microsoft.Office.Interop.Excel.Application excelApp = null;
            Microsoft.Office.Interop.Excel.Workbook workbook = null;
            Microsoft.Office.Interop.Excel.Worksheet worksheet = null;

            try
            {
                excelApp = new Microsoft.Office.Interop.Excel.Application();
                excelApp.Visible = false;
                workbook = excelApp.Workbooks.Open(filePath);

                // Find the "EGP_SYSTEM_ITEMS_INTERFACE" worksheet
                worksheet = null;
                foreach (Microsoft.Office.Interop.Excel.Worksheet ws in workbook.Worksheets)
                {
                    if (ws.Name.Equals("EGP_SYSTEM_ITEMS_INTERFACE", StringComparison.OrdinalIgnoreCase))
                    {
                        worksheet = ws;
                        break;
                    }
                }

                if (worksheet == null)
                {
                    throw new Exception("Worksheet 'EGP_SYSTEM_ITEMS_INTERFACE' not found in the template.");
                }

                // Generate unique batch ID: MMDDYYYY + 3 random digits
                string batchId = DateTime.Now.ToString("MMddyyyy") + new Random().Next(100, 999).ToString();

                // Add top assembly as first item
                ModelDoc2 activeDoc = _swApp.ActiveDoc as ModelDoc2;
                string topAssemblyPartNumber = GetTopAssemblyPartNumber(activeDoc);
                string topAssemblyDescription = GetTopAssemblyDescription(activeDoc);
                
                int currentRow = 9; // Start at row 9

                // Determine top assembly template name
                string topAssemblyTemplateName;
                if (IsTopAssemblyRaIndustrialBuy(activeDoc))
                {
                    topAssemblyTemplateName = "Roeslein Buy/Spare Part Template";
                }
                else
                {
                    topAssemblyTemplateName = "Roeslein Subassembly Template";
                }

                // Check if top assembly should be included based on purchase items setting
                bool includeTopAssembly = true;
                if (!includePurchaseItems && IsTopAssemblyRaIndustrialBuy(activeDoc))
                {
                    includeTopAssembly = false;
                    System.Diagnostics.Debug.WriteLine($"Oracle FBDI: Excluding top assembly '{topAssemblyPartNumber}' - is RA Industrial Buy and purchase items are excluded");
                }

                // Insert top assembly first (if not excluded)
                if (includeTopAssembly)
                {
                    PopulateExcelRow(worksheet, currentRow, transactionType, batchId, topAssemblyPartNumber, 
                                   "ROESLEIN_MASTER", topAssemblyDescription, topAssemblyTemplateName, "PIMDH", 
                                   "Roeslein Item Class", "Each", "Production", "Active");
                    currentRow++;
                }

                // Track unique part numbers to avoid duplicates (Oracle Items only need to be added once)
                var processedPartNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                // Insert all BOM items (only first occurrence of each part number)
                foreach (var bomItem in bomItems)
                {
                    // Skip if we've already processed this part number
                    if (processedPartNumbers.Contains(bomItem.PartNumber))
                    {
                        System.Diagnostics.Debug.WriteLine($"Oracle FBDI: Skipping duplicate part number '{bomItem.PartNumber}' - already processed");
                        continue;
                    }
                    
                    // Check if this item should be excluded based on purchase items setting
                    if (!includePurchaseItems && bomItem.IsRaIndustrialBuy)
                    {
                        System.Diagnostics.Debug.WriteLine($"Oracle FBDI: Excluding part number '{bomItem.PartNumber}' - is RA Industrial Buy and purchase items are excluded");
                        continue;
                    }
                    
                    // Add to processed set
                    processedPartNumbers.Add(bomItem.PartNumber);
                    
                    // Priority order: RA Industrial Buy → Assembly/Part
                    string templateName;
                    if (bomItem.IsRaIndustrialBuy)
                    {
                        templateName = "Roeslein Buy/Spare Part Template";
                    }
                    else
                    {
                        templateName = bomItem.IsAssembly ? "Roeslein Subassembly Template" : "Roeslein Part Template";
                    }
                    
                    PopulateExcelRow(worksheet, currentRow, transactionType, batchId, bomItem.PartNumber,
                                   "ROESLEIN_MASTER", bomItem.Description, templateName, "PIMDH",
                                   "Roeslein Item Class", "Each", "Production", "Active");
                    currentRow++;
                    
                    System.Diagnostics.Debug.WriteLine($"Oracle FBDI: Added part number '{bomItem.PartNumber}' with template '{templateName}' and transaction type '{transactionType}'");
                }

                // Format Column E (ITEM_NUMBER) as TEXT and Right-aligned
                Microsoft.Office.Interop.Excel.Range columnE = worksheet.Range["E:E"];
                columnE.NumberFormat = "@"; // Format as TEXT
                columnE.HorizontalAlignment = Microsoft.Office.Interop.Excel.XlHAlign.xlHAlignRight; // Right-aligned

                // Save and close
                workbook.Save();
            }
            finally
            {
                // Clean up COM objects
                if (workbook != null)
                {
                    workbook.Close(false);
                    Marshal.ReleaseComObject(workbook);
                }
                if (excelApp != null)
                {
                    excelApp.Quit();
                    Marshal.ReleaseComObject(excelApp);
                }
                if (worksheet != null)
                {
                    Marshal.ReleaseComObject(worksheet);
                }
                
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// Gets the part number of the top assembly
        /// </summary>
        private string GetTopAssemblyPartNumber(ModelDoc2 doc)
        {
            if (doc == null) return "UNKNOWN_ASSEMBLY";
            
            string configName = doc.ConfigurationManager.ActiveConfiguration.Name;
            
            // Try to get Part Number from custom properties
            CustomPropertyManager custPropMgr = doc.Extension.CustomPropertyManager[configName];
            if (custPropMgr != null)
            {
                custPropMgr.Get6("Part Number", false, out _, out string partNumber, out _, out _);
                if (!string.IsNullOrWhiteSpace(partNumber))
                    return partNumber;
                    
                custPropMgr.Get6("PartNo", false, out _, out partNumber, out _, out _);
                if (!string.IsNullOrWhiteSpace(partNumber))
                    return partNumber;
            }
            
            // Fallback to filename
            return Path.GetFileNameWithoutExtension(doc.GetTitle());
        }

        /// <summary>
        /// Gets the description of the top assembly
        /// </summary>
        private string GetTopAssemblyDescription(ModelDoc2 doc)
        {
            if (doc == null) return "";
            
            string configName = doc.ConfigurationManager.ActiveConfiguration.Name;
            
            // Try to get Description from custom properties
            CustomPropertyManager custPropMgr = doc.Extension.CustomPropertyManager[configName];
            if (custPropMgr != null)
            {
                custPropMgr.Get6("Description", false, out _, out string description, out _, out _);
                if (!string.IsNullOrWhiteSpace(description))
                    return description;
            }
            
            return ""; // Return empty if no description found
        }

        /// <summary>
        /// Checks if the top assembly meets RA Industrial Buy criteria
        /// </summary>
        private bool IsTopAssemblyRaIndustrialBuy(ModelDoc2 doc)
        {
            if (doc == null) return false;

            string partNumber = GetTopAssemblyPartNumber(doc);
            if (string.IsNullOrEmpty(partNumber)) return false;

            // Check condition 1: Part number format XXX-XXX (exactly 3 digits, dash, 3 digits)
            bool isCorrectFormat = System.Text.RegularExpressions.Regex.IsMatch(partNumber, @"^\d{3}-\d{3}$");
            
            // Check condition 2: File property "RA Industrial Buy" = "1" or "True"
            bool hasRaIndustrialBuyProperty = false;
            try
            {
                // Check file-level properties only
                CustomPropertyManager swCustPropMgr = doc.Extension.CustomPropertyManager[""];
                if (swCustPropMgr != null)
                {
                    swCustPropMgr.Get6("RA Industrial Buy", false, out _, out string propertyValue, out _, out _);
                    hasRaIndustrialBuyProperty = "1".Equals(propertyValue, StringComparison.OrdinalIgnoreCase) || 
                                               "True".Equals(propertyValue, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsTopAssemblyRaIndustrialBuy: Error checking RA Industrial Buy property: {ex.Message}");
            }

            return isCorrectFormat || hasRaIndustrialBuyProperty;
        }

        /// <summary>
        /// Shows a custom completion dialog with option to open the exported file
        /// </summary>
        private void ShowExportCompleteDialog(string filePath, int itemCount)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "Oracle FBDI Items Export Complete";
                dialog.Size = new Size(450, 250);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ShowIcon = false;

                // Message label
                Label messageLabel = new Label();
                messageLabel.Text = $"Oracle FBDI Items template populated and saved as:\n{filePath}\n\nPopulated {itemCount} items from the BOM into the template.";
                messageLabel.Location = new Point(20, 20);
                messageLabel.Size = new Size(400, 120);
                messageLabel.AutoSize = false;
                dialog.Controls.Add(messageLabel);

                // Open File button
                Button openFileButton = new Button();
                openFileButton.Text = "Open File";
                openFileButton.Location = new Point(260, 160);
                openFileButton.Size = new Size(80, 30);
                openFileButton.Click += (s, e) => {
                    try
                    {
                        System.Diagnostics.Process.Start(filePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not open file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                dialog.Controls.Add(openFileButton);

                // OK button
                Button okButton = new Button();
                okButton.Text = "OK";
                okButton.Location = new Point(350, 160);
                okButton.Size = new Size(60, 30);
                okButton.DialogResult = DialogResult.OK;
                dialog.AcceptButton = okButton;
                dialog.Controls.Add(okButton);

                dialog.ShowDialog(this);
            }
        }

        /// <summary>
        /// Populates a single row in the Excel worksheet with the specified values (used for Items FBDI)
        /// </summary>
        private void PopulateExcelRow(Microsoft.Office.Interop.Excel.Worksheet worksheet, int row,
            string transactionType, string batchId, string itemNumber, string organizationCode,
            string description, string templateName, string sourceSystemCode, string itemCatalogGroupName,
            string primaryUomName, string currentPhaseCode, string inventoryItemStatusCode)
        {
            worksheet.Cells[row, 2] = transactionType;        // Column B
            worksheet.Cells[row, 3] = batchId;                // Column C
            worksheet.Cells[row, 5] = itemNumber;             // Column E
            worksheet.Cells[row, 7] = organizationCode;       // Column G
            worksheet.Cells[row, 8] = description;            // Column H
            worksheet.Cells[row, 9] = templateName;           // Column I
            worksheet.Cells[row, 10] = sourceSystemCode;      // Column J
            worksheet.Cells[row, 13] = itemCatalogGroupName;  // Column M
            worksheet.Cells[row, 14] = primaryUomName;        // Column N
            worksheet.Cells[row, 15] = currentPhaseCode;      // Column O
            worksheet.Cells[row, 16] = inventoryItemStatusCode; // Column P
            
            // Add new default values for additional columns
            worksheet.Cells[row, 121] = 2;                    // Column DQ (PREPROCESSING_LEAD_TIME)
            worksheet.Cells[row, 127] = 2;                    // Column DW (POSTPROCESSING_LEAD_TIME)
            worksheet.Cells[row, 387] = "PROJECT_AND_TASK";   // Column NW (HARD_PEGGING_LEVEL)
            worksheet.Cells[row, 388] = "Y";                  // Column NX (COMN_SUPPLY_PRJ_DEMAND_FLAG)
        }
    }
}
