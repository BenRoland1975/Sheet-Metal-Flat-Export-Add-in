using System;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using EPDM.Interop.epdm;

namespace RoesleinAddIn
{
    public class StandardsSummaryForm : Form
    {
        private readonly ISldWorks swApp;
        private readonly List<string> filesNeedingMaterials;
        private readonly List<string> filesNeedingShopRoute;
        private readonly List<string> filesNotUpToDate;
        private readonly List<string> assembliesNeedingRebuild;
        private readonly EPDM.Interop.epdm.IEdmVault5 pdmVault;

        public StandardsSummaryForm(string summaryText, string title = "Standards Version Check", 
            ISldWorks solidWorksApp = null, List<string> materialFiles = null, List<string> shopRouteFiles = null, List<string> outdatedFiles = null, List<string> assembliesNeedingRebuild = null, EPDM.Interop.epdm.IEdmVault5 vault = null)
        {
            swApp = solidWorksApp;
            filesNeedingMaterials = materialFiles ?? new List<string>();
            filesNeedingShopRoute = shopRouteFiles ?? new List<string>();
            filesNotUpToDate = outdatedFiles ?? new List<string>();
            this.assembliesNeedingRebuild = assembliesNeedingRebuild ?? new List<string>();
            pdmVault = vault;

            this.Text = title;
            this.Width = 800;
            this.Height = 600;
            this.MinimumSize = new System.Drawing.Size(400, 300);
            this.StartPosition = FormStartPosition.CenterScreen;

            var resources = new ComponentResourceManager(typeof(StandardsSummaryForm));
            this.Icon = (Icon)resources.GetObject("$this.Icon");

            var textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 10),
                Text = summaryText
            };

            // Prevent text from being highlighted when the form appears
            textBox.SelectionStart = 0;
            textBox.SelectionLength = 0;

            // Create button panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10, 5, 10, 5)
            };

            // Create buttons
            var okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Height = 35,
                Width = 80,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            var openMaterialFilesButton = new Button
            {
                Text = "Open Files Needing Materials",
                Height = 35,
                Width = 200,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Enabled = filesNeedingMaterials.Count > 0 && swApp != null
            };

            var openShopRouteFilesButton = new Button
            {
                Text = "Open Files Needing Shop Route",
                Height = 35,
                Width = 200,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Enabled = filesNeedingShopRoute.Count > 0 && swApp != null
            };

            var openOutdatedFilesButton = new Button
            {
                Text = "Open Files Not Up To Date",
                Height = 35,
                Width = 180,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Enabled = filesNotUpToDate.Count > 0 && swApp != null
            };

            var updateAssemblyButton = new Button
            {
                Text = "Update Assembly",
                Height = 35,
                Width = 140,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Enabled = this.assembliesNeedingRebuild.Count > 0 && swApp != null
            };

            // Position buttons dynamically based on what's enabled
            int buttonX = 10;
            okButton.Location = new Point(buttonPanel.Width - okButton.Width - 10, 8);
            
            if (filesNeedingMaterials.Count > 0 && swApp != null)
            {
                openMaterialFilesButton.Location = new Point(buttonX, 8);
                buttonX += openMaterialFilesButton.Width + 10;
            }
            
            if (filesNeedingShopRoute.Count > 0 && swApp != null)
            {
                openShopRouteFilesButton.Location = new Point(buttonX, 8);
                buttonX += openShopRouteFilesButton.Width + 10;
            }
            
            if (filesNotUpToDate.Count > 0 && swApp != null)
            {
                openOutdatedFilesButton.Location = new Point(buttonX, 8);
                buttonX += openOutdatedFilesButton.Width + 10;
            }
            
            if (this.assembliesNeedingRebuild.Count > 0 && swApp != null)
            {
                updateAssemblyButton.Location = new Point(buttonX, 8);
                buttonX += updateAssemblyButton.Width + 10;
            }

            // Add event handlers
            openMaterialFilesButton.Click += OpenMaterialFilesButton_Click;
            openShopRouteFilesButton.Click += OpenShopRouteFilesButton_Click;
            openOutdatedFilesButton.Click += OpenOutdatedFilesButton_Click;
            updateAssemblyButton.Click += UpdateAssemblyButton_Click;

            // Add controls
            buttonPanel.Controls.Add(okButton);
            if (filesNeedingMaterials.Count > 0 && swApp != null)
                buttonPanel.Controls.Add(openMaterialFilesButton);
            if (filesNeedingShopRoute.Count > 0 && swApp != null)
                buttonPanel.Controls.Add(openShopRouteFilesButton);
            if (filesNotUpToDate.Count > 0 && swApp != null)
                buttonPanel.Controls.Add(openOutdatedFilesButton);
            if (this.assembliesNeedingRebuild.Count > 0 && swApp != null)
                buttonPanel.Controls.Add(updateAssemblyButton);

            this.Controls.Add(textBox);
            this.Controls.Add(buttonPanel);
            this.AcceptButton = okButton;
        }

        private void OpenMaterialFilesButton_Click(object sender, EventArgs e)
        {
            OpenFilesFromList(filesNeedingMaterials, "Files Needing Materials");
        }

        private void OpenShopRouteFilesButton_Click(object sender, EventArgs e)
        {
            OpenFilesFromList(filesNeedingShopRoute, "Files Needing Shop Route");
        }

        private void OpenOutdatedFilesButton_Click(object sender, EventArgs e)
        {
            OpenFilesFromList(filesNotUpToDate, "Files Not Up To Date");
        }

        private void OpenFilesFromList(List<string> fileList, string listName)
        {
            if (swApp == null || fileList == null || fileList.Count == 0)
                return;

            try
            {
                // Extract just the filenames (remove error messages)
                var filesToOpen = fileList.Select(f => {
                    // Remove error messages like " - Invalid material: 'Steel' - please assign a specific material"
                    var parts = f.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                    return parts[0].Trim();
                }).Where(f => f.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase) || 
                             f.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase))
                .ToList();

                // Debug logging to help identify issues
                System.Diagnostics.Debug.WriteLine($"[StandardsSummaryForm] Processing {listName}:");
                System.Diagnostics.Debug.WriteLine($"[StandardsSummaryForm] Original list count: {fileList.Count}");
                foreach (var item in fileList)
                {
                    System.Diagnostics.Debug.WriteLine($"[StandardsSummaryForm] Original: '{item}'");
                }
                System.Diagnostics.Debug.WriteLine($"[StandardsSummaryForm] Processed list count: {filesToOpen.Count}");
                foreach (var item in filesToOpen)
                {
                    System.Diagnostics.Debug.WriteLine($"[StandardsSummaryForm] Processed: '{item}'");
                }

                // Remove duplicates but keep track of what was removed
                var distinctFiles = filesToOpen.Distinct().ToList();
                if (distinctFiles.Count != filesToOpen.Count)
                {
                    System.Diagnostics.Debug.WriteLine($"[StandardsSummaryForm] WARNING: {filesToOpen.Count - distinctFiles.Count} duplicate file(s) were removed!");
                    var duplicates = filesToOpen.GroupBy(f => f).Where(g => g.Count() > 1).Select(g => g.Key);
                    foreach (var dup in duplicates)
                    {
                        System.Diagnostics.Debug.WriteLine($"[StandardsSummaryForm] Duplicate file: '{dup}'");
                    }
                }
                filesToOpen = distinctFiles;

                if (filesToOpen.Count == 0)
                {
                    MessageBox.Show($"No valid SolidWorks files found in {listName}.", "No Files", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Ask user for confirmation with checkout option
                string checkoutOption = pdmVault != null && pdmVault.IsLoggedIn ? 
                    "\n\n📝 Files will be checked out automatically so you can edit them immediately." : 
                    "\n\n⚠️ PDM not available - files will be opened read-only.";
                
                var result = MessageBox.Show(
                    $"Open and check out {filesToOpen.Count} file(s) from {listName}?\n\n" +
                    string.Join("\n", filesToOpen.Take(10)) + 
                    (filesToOpen.Count > 10 ? $"\n... and {filesToOpen.Count - 10} more" : "") +
                    checkoutOption,
                    "Open and Check Out Files",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    int successCount = 0;
                    int failCount = 0;
                    int checkedOutCount = 0;

                    foreach (var fileName in filesToOpen)
                    {
                        try
                        {
                            // Try to find the file in the current assembly's directory structure
                            string fullPath = FindFileInAssemblyDirectory(fileName);
                            
                            System.Diagnostics.Debug.WriteLine($"[OpenFilesFromList] Processing file: '{fileName}'");
                            System.Diagnostics.Debug.WriteLine($"[OpenFilesFromList] Found path: '{fullPath}'");
                            
                            if (!string.IsNullOrEmpty(fullPath) && System.IO.File.Exists(fullPath))
                            {
                                ModelDoc2 doc = null;
                                
                                // Check if file is already open
                                if (!IsFileAlreadyOpen(fullPath))
                                {
                                    int errors = 0, warnings = 0;
                                    int docType = GetDocumentType(fullPath);
                                    
                                    System.Diagnostics.Debug.WriteLine($"[OpenFilesFromList] Opening file: '{fullPath}' with docType: {docType}");
                                    
                                    doc = swApp.OpenDoc6(fullPath, docType, 
                                        (int)SolidWorks.Interop.swconst.swOpenDocOptions_e.swOpenDocOptions_Silent, 
                                        "", ref errors, ref warnings) as ModelDoc2;
                                    
                                    if (doc != null)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[OpenFilesFromList] Successfully opened: '{fileName}'");
                                        successCount++;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[OpenFilesFromList] Failed to open: '{fileName}' - Errors: {errors}, Warnings: {warnings}");
                                        failCount++;
                                        continue;
                                    }
                                }
                                else
                                {
                                    // File already open, just activate it
                                    System.Diagnostics.Debug.WriteLine($"[OpenFilesFromList] File already open, activating: '{fileName}'");
                                    int activateErrors = 0;
                                    swApp.ActivateDoc3(fullPath, false, (int)SolidWorks.Interop.swconst.swRebuildOnActivation_e.swDontRebuildActiveDoc, ref activateErrors);
                                    successCount++;
                                    
                                    // Get the document reference
                                    doc = swApp.ActiveDoc as ModelDoc2;
                                }
                                
                                // Try to check out the file if PDM is available
                                if (doc != null && pdmVault != null && pdmVault.IsLoggedIn)
                                {
                                    try
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[OpenFilesFromList] Attempting checkout for: '{fileName}'");
                                        if (CheckOutFile(fullPath, doc))
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[OpenFilesFromList] Successfully checked out: '{fileName}'");
                                            checkedOutCount++;
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[OpenFilesFromList] Checkout failed or not needed for: '{fileName}'");
                                        }
                                    }
                                    catch (Exception checkoutEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error checking out {fileName}: {checkoutEx.Message}");
                                        // Don't fail the whole operation if checkout fails
                                    }
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[OpenFilesFromList] File not found or path is empty: '{fileName}' -> '{fullPath}'");
                                failCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            System.Diagnostics.Debug.WriteLine($"Error opening {fileName}: {ex.Message}");
                        }
                    }

                    // Show results
                    string message = $"Opened {successCount} file(s) successfully.";
                    if (checkedOutCount > 0)
                        message += $"\n✅ Checked out {checkedOutCount} file(s) for editing.";
                    if (failCount > 0)
                        message += $"\n❌ {failCount} file(s) could not be opened.";
                    
                    if (pdmVault != null && pdmVault.IsLoggedIn && successCount > checkedOutCount)
                    {
                        int alreadyCheckedOut = successCount - checkedOutCount;
                        message += $"\n📝 {alreadyCheckedOut} file(s) were already checked out or could not be checked out.";
                    }

                    MessageBox.Show(message, "Open Files Result", 
                        MessageBoxButtons.OK, 
                        failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening files: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string FindFileInAssemblyDirectory(string fileName)
        {
            try
            {
                // Get the active document to find the base directory
                var activeDoc = swApp.ActiveDoc as ModelDoc2;
                if (activeDoc != null)
                {
                    string activeDocPath = activeDoc.GetPathName();
                    if (!string.IsNullOrEmpty(activeDocPath))
                    {
                        string baseDir = System.IO.Path.GetDirectoryName(activeDocPath);
                        
                        // Method 1: Search in the same directory first
                        string fullPath = System.IO.Path.Combine(baseDir, fileName);
                        if (System.IO.File.Exists(fullPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[FindFileInAssemblyDirectory] Found '{fileName}' in same directory: {fullPath}");
                            return fullPath;
                        }
                        
                        // Method 2: Search in subdirectories
                        var foundFiles = System.IO.Directory.GetFiles(baseDir, fileName, System.IO.SearchOption.AllDirectories);
                        if (foundFiles.Length > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[FindFileInAssemblyDirectory] Found '{fileName}' in subdirectory: {foundFiles[0]}");
                            return foundFiles[0];
                        }
                        
                        // Method 3: Search in parent directory and its subdirectories (for sibling directories)
                        string parentDir = System.IO.Path.GetDirectoryName(baseDir);
                        if (!string.IsNullOrEmpty(parentDir) && System.IO.Directory.Exists(parentDir))
                        {
                            System.Diagnostics.Debug.WriteLine($"[FindFileInAssemblyDirectory] Searching parent directory: {parentDir}");
                            foundFiles = System.IO.Directory.GetFiles(parentDir, fileName, System.IO.SearchOption.AllDirectories);
                            if (foundFiles.Length > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"[FindFileInAssemblyDirectory] Found '{fileName}' in parent/sibling directory: {foundFiles[0]}");
                                return foundFiles[0];
                            }
                        }
                        
                        // Method 4: Search up to 2 levels up (for complex directory structures)
                        string grandParentDir = System.IO.Path.GetDirectoryName(parentDir);
                        if (!string.IsNullOrEmpty(grandParentDir) && System.IO.Directory.Exists(grandParentDir))
                        {
                            System.Diagnostics.Debug.WriteLine($"[FindFileInAssemblyDirectory] Searching grandparent directory: {grandParentDir}");
                            foundFiles = System.IO.Directory.GetFiles(grandParentDir, fileName, System.IO.SearchOption.AllDirectories);
                            if (foundFiles.Length > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"[FindFileInAssemblyDirectory] Found '{fileName}' in grandparent directory tree: {foundFiles[0]}");
                                return foundFiles[0];
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[FindFileInAssemblyDirectory] File '{fileName}' not found in any searched directories");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[FindFileInAssemblyDirectory] Active document path is empty");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[FindFileInAssemblyDirectory] No active document found");
                }
                
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FindFileInAssemblyDirectory] Error searching for '{fileName}': {ex.Message}");
                return null;
            }
        }

        private bool IsFileAlreadyOpen(string filePath)
        {
            try
            {
                object[] openDocs = swApp.GetDocuments() as object[];
                if (openDocs != null)
                {
                    foreach (object docObj in openDocs)
                    {
                        var doc = docObj as ModelDoc2;
                        if (doc != null && string.Equals(doc.GetPathName(), filePath, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private int GetDocumentType(string filePath)
        {
            string ext = System.IO.Path.GetExtension(filePath).ToLower();
            switch (ext)
            {
                case ".sldprt":
                    return (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocPART;
                case ".sldasm":
                    return (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocASSEMBLY;
                case ".slddrw":
                    return (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocDRAWING;
                default:
                    return (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocPART;
            }
        }

        private bool CheckOutFile(string filePath, ModelDoc2 doc)
        {
            try
            {
                if (pdmVault == null || !pdmVault.IsLoggedIn || doc == null)
                    return false;

                // Get the file from the vault
                EPDM.Interop.epdm.IEdmFolder5 parentFolder;
                EPDM.Interop.epdm.IEdmFile5 edmFile = pdmVault.GetFileFromPath(filePath, out parentFolder);
                
                if (edmFile == null || parentFolder == null)
                    return false;

                // Check if file is already checked out
                if (edmFile.IsLocked)
                {
                    // Check if it's checked out by current user (file is not read-only)
                    bool isLockedByCurrentUser = !doc.IsOpenedReadOnly();
                    return isLockedByCurrentUser; // Return true if already checked out by us
                }

                // Get SolidWorks window handle for PDM dialogs
                int swWindowHandle = GetSolidWorksMainWindowHandle();

                // Force SolidWorks to release its file locks
                if (doc.ForceReleaseLocks() != 0)
                {
                    // Perform PDM checkout
                    edmFile.LockFile(parentFolder.ID, swWindowHandle, (int)EPDM.Interop.epdm.EdmLockFlag.EdmLock_Simple);
                    
                    // Reload the document to update its status
                    doc.ReloadOrReplace(false, filePath, true);
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CheckOutFile: {ex.Message}");
                return false;
            }
        }

        private int GetSolidWorksMainWindowHandle()
        {
            try
            {
                if (swApp != null)
                {
                    var swFrame = swApp.Frame() as SolidWorks.Interop.sldworks.IFrame;
                    if (swFrame != null)
                    {
                        return swFrame.GetHWnd();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting SW window handle: {ex.Message}");
            }
            return 0;
        }

        private void UpdateAssemblyButton_Click(object sender, EventArgs e)
        {
            UpdateAssembliesFromList(assembliesNeedingRebuild, "Assemblies Needing Rebuild");
        }

        private void UpdateAssembliesFromList(List<string> assemblyList, string listName)
        {
            if (swApp == null || assemblyList == null || assemblyList.Count == 0)
                return;

            try
            {
                // Extract just the filenames (remove any error messages)
                var assembliesToUpdate = assemblyList.Select(f => {
                    // Remove error messages if any
                    var parts = f.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                    return parts[0].Trim();
                }).Where(f => f.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

                // Debug logging
                System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Processing {listName}:");
                System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Assembly count: {assembliesToUpdate.Count}");
                foreach (var item in assembliesToUpdate)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Assembly: '{item}'");
                }

                if (assembliesToUpdate.Count == 0)
                {
                    MessageBox.Show($"No valid assembly files found in {listName}.", "No Assemblies", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Ask user for confirmation
                string assemblyText = assembliesToUpdate.Count == 1 ? "assembly" : "assemblies";
                string checkoutOption = pdmVault != null && pdmVault.IsLoggedIn ? 
                    "\n\n📝 Assemblies will be checked out, rebuilt, and checked back in automatically." : 
                    "\n\n⚠️ PDM not available - assemblies will be rebuilt but not checked in.";
                
                var result = MessageBox.Show(
                    $"Update {assembliesToUpdate.Count} {assemblyText} to reflect changes in child components?\n\n" +
                    string.Join("\n", assembliesToUpdate.Take(10).Select(f => $"• {f}")) + 
                    (assembliesToUpdate.Count > 10 ? $"\n... and {assembliesToUpdate.Count - 10} more" : "") +
                    checkoutOption,
                    "Update Assemblies",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    int successCount = 0;
                    int failCount = 0;
                    int checkedOutCount = 0;

                    foreach (var assemblyName in assembliesToUpdate)
                    {
                        try
                        {
                            // Try to find the assembly file
                            string fullPath = FindFileInAssemblyDirectory(assemblyName);
                            
                            System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Processing assembly: '{assemblyName}'");
                            System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Found path: '{fullPath}'");
                            
                            if (!string.IsNullOrEmpty(fullPath) && System.IO.File.Exists(fullPath))
                            {
                                ModelDoc2 doc = null;
                                bool wasAlreadyOpen = IsFileAlreadyOpen(fullPath);
                                
                                // Open the assembly if not already open
                                if (!wasAlreadyOpen)
                                {
                                    int errors = 0, warnings = 0;
                                    int docType = GetDocumentType(fullPath);
                                    
                                    System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Opening assembly: '{fullPath}'");
                                    
                                    doc = swApp.OpenDoc6(fullPath, docType, 
                                        (int)SolidWorks.Interop.swconst.swOpenDocOptions_e.swOpenDocOptions_Silent, 
                                        "", ref errors, ref warnings) as ModelDoc2;
                                    
                                    if (doc == null)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Failed to open assembly: '{assemblyName}' - Errors: {errors}, Warnings: {warnings}");
                                        failCount++;
                                        continue;
                                    }
                                }
                                else
                                {
                                    // Assembly already open, just activate it
                                    System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Assembly already open, activating: '{assemblyName}'");
                                    int activateErrors = 0;
                                    swApp.ActivateDoc3(fullPath, false, (int)SolidWorks.Interop.swconst.swRebuildOnActivation_e.swDontRebuildActiveDoc, ref activateErrors);
                                    doc = swApp.ActiveDoc as ModelDoc2;
                                }
                                
                                if (doc != null)
                                {
                                    bool checkedOut = false;
                                    
                                    // Try to check out the assembly if PDM is available
                                    if (pdmVault != null && pdmVault.IsLoggedIn)
                                    {
                                        try
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Attempting checkout for assembly: '{assemblyName}'");
                                            if (CheckOutFile(fullPath, doc))
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Successfully checked out assembly: '{assemblyName}'");
                                                checkedOut = true;
                                                checkedOutCount++;
                                            }
                                        }
                                        catch (Exception checkoutEx)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Error checking out assembly {assemblyName}: {checkoutEx.Message}");
                                        }
                                    }
                                    
                                    // Rebuild the assembly
                                    System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Rebuilding assembly: '{assemblyName}'");
                                    bool rebuildSuccess = RebuildAssembly(doc);
                                    
                                    if (rebuildSuccess)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Successfully rebuilt assembly: '{assemblyName}'");
                                        successCount++;
                                        
                                        // Check back in if we checked it out
                                        if (checkedOut && pdmVault != null && pdmVault.IsLoggedIn)
                                        {
                                            try
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Checking in assembly: '{assemblyName}'");
                                                CheckInAssembly(doc, $"Assembly updated to reflect changes in child components");
                                            }
                                            catch (Exception checkinEx)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Error checking in assembly {assemblyName}: {checkinEx.Message}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Failed to rebuild assembly: '{assemblyName}'");
                                        failCount++;
                                    }
                                }
                                else
                                {
                                    failCount++;
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[UpdateAssembliesFromList] Assembly not found: '{assemblyName}' -> '{fullPath}'");
                                failCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            System.Diagnostics.Debug.WriteLine($"Error updating assembly {assemblyName}: {ex.Message}");
                        }
                    }

                    // Show results
                    string message = $"Updated {successCount} {(successCount == 1 ? "assembly" : "assemblies")} successfully.";
                    if (checkedOutCount > 0)
                        message += $"\n✅ Checked out and updated {checkedOutCount} {(checkedOutCount == 1 ? "assembly" : "assemblies")}.";
                    if (failCount > 0)
                        message += $"\n❌ {failCount} {(failCount == 1 ? "assembly" : "assemblies")} could not be updated.";

                    MessageBox.Show(message, "Update Assemblies Result", 
                        MessageBoxButtons.OK, 
                        failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating assemblies: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool RebuildAssembly(ModelDoc2 assemblyDoc)
        {
            try
            {
                if (assemblyDoc == null || assemblyDoc.GetType() != (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocASSEMBLY)
                    return false;

                System.Diagnostics.Debug.WriteLine($"[RebuildAssembly] Starting rebuild for: {assemblyDoc.GetPathName()}");

                // Force a full rebuild
                bool rebuildResult = assemblyDoc.ForceRebuild3(false);
                System.Diagnostics.Debug.WriteLine($"[RebuildAssembly] ForceRebuild3 result: {rebuildResult}");

                // Save the assembly
                int saveErrors = 0, saveWarnings = 0;
                bool saveResult = assemblyDoc.Save3((int)SolidWorks.Interop.swconst.swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);
                System.Diagnostics.Debug.WriteLine($"[RebuildAssembly] Save result: {saveResult}, Errors: {saveErrors}, Warnings: {saveWarnings}");

                return rebuildResult && saveResult && saveErrors == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RebuildAssembly] Error: {ex.Message}");
                return false;
            }
        }

        private bool CheckInAssembly(ModelDoc2 assemblyDoc, string comment)
        {
            try
            {
                if (assemblyDoc == null || pdmVault == null || !pdmVault.IsLoggedIn)
                    return false;

                string filePath = assemblyDoc.GetPathName();
                if (string.IsNullOrEmpty(filePath))
                    return false;

                // Get the file from the vault
                EPDM.Interop.epdm.IEdmFolder5 parentFolder;
                EPDM.Interop.epdm.IEdmFile5 edmFile = pdmVault.GetFileFromPath(filePath, out parentFolder);
                
                if (edmFile == null || parentFolder == null || !edmFile.IsLocked)
                    return false;

                // Force SolidWorks to release its file locks
                assemblyDoc.ForceReleaseLocks();

                // Check in the file
                edmFile.UnlockFile(parentFolder.ID, comment, (int)EPDM.Interop.epdm.EdmUnlockFlag.EdmUnlock_Simple);

                // Reload the document
                assemblyDoc.ReloadOrReplace(false, filePath, true);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CheckInAssembly] Error: {ex.Message}");
                return false;
            }
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(StandardsSummaryForm));
            this.SuspendLayout();
            // 
            // StandardsSummaryForm
            // 
            this.BackgroundImage = global::RoesleinAddIn.Properties.Resources.RoesleinConnex_Small;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "StandardsSummaryForm";
            this.ResumeLayout(false);

        }
    }
} 