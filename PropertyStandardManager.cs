using SolidWorks.Interop.sldworks;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using SolidWorks.Interop.swconst;
using EPDM.Interop.epdm;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using RoesleinAddIn;

namespace RoesleinAddIn
{
    /// <summary>
    /// Helper class to track files that need to be checked out for standards application
    /// </summary>
    public class FileCheckoutInfo
    {
        public EPDM.Interop.epdm.IEdmFile5 File { get; set; }
        public EPDM.Interop.epdm.IEdmFolder5 Folder { get; set; }
        public string Path { get; set; }
        public string FileName => System.IO.Path.GetFileName(Path);
        public bool IsAlreadyCheckedOut { get; set; }
        public bool StandardsApplied { get; set; }
        public ModelDoc2 Document { get; set; }
        public bool CheckedOutByThisProcess { get; set; } // New property to track checkouts we performed
        public bool HasAssociatedDrawing { get; set; } // New property to track if file has an associated drawing
    }

    /// <summary>
    /// Manages applying property standards to SolidWorks files, including PDM checkout.
    /// </summary>
    public class PropertyStandardManager
    {
        private ISldWorks swApp;
        private EPDM.Interop.epdm.IEdmVault5 pdmVault;
        private string logFilePath;
        private PDMFileManager pdmFileManager; // Add new field

        public PropertyStandardManager(EPDM.Interop.epdm.IEdmVault5 vault, ISldWorks application, string debugLogFilePath = null)
        {
            swApp = application;
            pdmVault = vault;
            logFilePath = debugLogFilePath;
            var settings = Settings.LoadSettings();
            if (settings != null && !string.IsNullOrWhiteSpace(settings.LogFilePath))
            {
                Logger.SetMainLogFilePath(settings.LogFilePath);
                Logger.Instance.SetDebugLogPath(settings.DebugLogFilePath);
            }
            LogMessage("PropertyStandardManager initialized" + (pdmVault != null ? " with PDM Vault connection." : " without PDM Vault connection."));
            
            // Initialize the PDM file manager
            pdmFileManager = new PDMFileManager(vault, application, debugLogFilePath);
        }
        
        // Add logging functionality
        private void LogMessage(string message)
        {
            Logger.Log($"[PropertyStandardManager] {message}");
        }

        // Get the SolidWorks main window handle
        private int GetSolidWorksMainWindowHandle()
        {
            try
            {
                if (swApp != null)
                {
                    IFrame swFrame = swApp.Frame() as IFrame;
                    if (swFrame != null)
                    {
                        return swFrame.GetHWnd();
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error getting SW window handle: {ex.Message}");
            }
            LogMessage("Could not get SolidWorks main window handle, returning 0.");
            return 0;
        }

        /// <summary>
        /// Applies property standards to the active document.
        /// Checks out the file from PDM if necessary and if the "Roeslein Standards Version" property is missing or outdated.
        /// </summary>
        /// <param name="swModel">The SolidWorks model document.</param>
        /// <param name="currentStandardsVersion">The current version string of the Roeslein Standards.</param>
        /// <param name="silentMode">If true, suppresses UI messages (not fully implemented for PDM interactions yet).</param>
        /// <param name="settings">The settings object to use for standards application.</param>
        /// <param name="deferCheckIn">If true, skips the check-in logic for the document.</param>
        /// <returns>StandardsApplicationResult with status and checkout info.</returns>
        public StandardsApplicationResult ApplyStandardsToActiveDocument(ModelDoc2 swModel, string currentStandardsVersion, bool silentMode = false, Settings settings = null, bool deferCheckIn = false)
        {
            LogMessage("ApplyStandardsToActiveDocument called");
            if (swModel == null)
            {
                LogMessage("ApplyStandardsToActiveDocument: No active SolidWorks model provided.");
                if (!silentMode) MessageBox.Show("No active document.", "Property Standards", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return new StandardsApplicationResult { StandardsApplied = false, CheckedOutByThisProcess = false };
            }

            string filePath = swModel.GetPathName();
            if (string.IsNullOrEmpty(filePath))
            {
                LogMessage("ApplyStandardsToActiveDocument: File has not been saved yet. Cannot perform PDM operations or apply standards that require a saved file.");
                if (!silentMode) MessageBox.Show("The file must be saved before applying property standards.", "Property Standards", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return new StandardsApplicationResult { StandardsApplied = false, CheckedOutByThisProcess = false };
            }

            LogMessage($"ApplyStandardsToActiveDocument started for: '{filePath}'. Current Standards Version: '{currentStandardsVersion}'");

            bool wasCheckedOutByThisProcess = false;
            bool canWriteToFile = !swModel.IsOpenedReadOnly();
            EPDM.Interop.epdm.IEdmFile5 edmFile = null;
            EPDM.Interop.epdm.IEdmFolder5 parentFolder = null;

            if (pdmVault != null && pdmVault.IsLoggedIn)
            {
                LogMessage($"PDM: Vault '{pdmVault.Name}' is logged in. Checking file status for '{filePath}'.");
                try
                {
                    edmFile = pdmVault.GetFileFromPath(filePath, out parentFolder);

                    if (edmFile != null && parentFolder != null)
                    {
                        LogMessage($"PDM: File '{filePath}' found in vault. ID: {edmFile.ID}, Parent Folder ID: {parentFolder.ID}");

                        CustomPropertyManager swCustPropMgr = swModel.Extension.CustomPropertyManager[""];
                        string existingStandardsVersion = "";
                        string valOut = "";
                        string resolvedValOut = "";
                        bool wasResolved;
                        bool linkToProperty;
                        swCustPropMgr.Get6("Roeslein Standards Version", false, out valOut, out resolvedValOut, out wasResolved, out linkToProperty);
                        existingStandardsVersion = resolvedValOut;

                        LogMessage($"PDM: Existing 'Roeslein Standards Version' property value: '{existingStandardsVersion}'. Required version: '{currentStandardsVersion}'.");

                        if (string.IsNullOrWhiteSpace(existingStandardsVersion) || existingStandardsVersion != currentStandardsVersion)
                        {
                            LogMessage("PDM: Standards version mismatch or property missing. Checkout may be required.");

                            if (edmFile.IsLocked)
                            {
                                bool isLockedByCurrentUser = !swModel.IsOpenedReadOnly();
                                LogMessage($"PDM: File is locked. Appears to be locked by current user (based on read-only status): {isLockedByCurrentUser}");
                                if (isLockedByCurrentUser)
                                {
                                    LogMessage($"PDM: File appears to be checked out by the current user. Proceeding with write access.");
                                    canWriteToFile = true;
                                }
                                else
                                {
                                    LogMessage($"PDM: File is checked out by another user. Cannot apply standards.");
                                    if (!silentMode) MessageBox.Show($@"File '{Path.GetFileName(filePath)}' is checked out by another user.\nCannot apply standards.", "PDM Checkout Conflict", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                    return new StandardsApplicationResult { StandardsApplied = false, CheckedOutByThisProcess = false };
                                }
                            }
                            else
                            {
                                LogMessage("PDM: File is not checked out. Attempting to check out...");
                                
                                // Use the PDM File Manager to check out the file
                                bool checkoutResult = pdmFileManager.CheckOutOpenFile(swModel, silentMode);
                                
                                if (checkoutResult)
                                {
                                    LogMessage($"PDM: File '{filePath}' checked out successfully using PDMFileManager.");
                                    wasCheckedOutByThisProcess = true;
                                    canWriteToFile = true;
                                    
                                    // Display a checkout success message if not in silent mode
                                    if (!silentMode) 
                                    {
                                        MessageBox.Show($"File '{Path.GetFileName(filePath)}' checked out successfully.", 
                                            "PDM Checkout", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    }
                                }
                                else
                                {
                                    LogMessage($"PDM: Failed to check out file '{filePath}'.");
                                    return new StandardsApplicationResult { StandardsApplied = false, CheckedOutByThisProcess = false };
                                }
                            }
                        }
                        else
                        {
                            LogMessage("PDM: 'Roeslein Standards Version' property is up-to-date. No checkout needed for version update.");
                            if (edmFile.IsLocked)
                            {
                                bool isLockedByCurrentUser = !swModel.IsOpenedReadOnly();
                                if(isLockedByCurrentUser)
                                {
                                   canWriteToFile = true;
                                   LogMessage("PDM: File already checked out by current user.");
                                } else {
                                   LogMessage($"PDM: File locked by another user. Assuming read-only.");
                                   canWriteToFile = false;
                                }
                            } else {
                                LogMessage("PDM: Standards up-to-date, file not locked. Assuming no write needed for version property.");
                                canWriteToFile = !swModel.IsOpenedReadOnly();
                            }
                        }
                    }
                    else
                    {
                        LogMessage($"PDM: File '{filePath}' not found in the current PDM vault view or vault connection issue. Proceeding as non-PDM file.");
                        canWriteToFile = !swModel.IsOpenedReadOnly();
                    }
                }
                catch (System.Runtime.InteropServices.COMException comEx)
                {
                    LogMessage($"PDM: Error during PDM operations for '{filePath}'. COMException: HRESULT=0x{comEx.ErrorCode:X}, Msg={comEx.Message}. Proceeding as if file is not in PDM or not writable.");
                    if (!silentMode) MessageBox.Show($"Error communicating with PDM: {comEx.Message}", "PDM Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    canWriteToFile = !swModel.IsOpenedReadOnly();
                }
                catch (Exception ex)
                {
                    LogMessage($"PDM: Error during PDM operations for '{filePath}'. Exception: {ex.Message}. Proceeding as if file is not in PDM or not writable.");
                    if (!silentMode) MessageBox.Show($"Error during PDM operations: {ex.Message}", "PDM Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    canWriteToFile = !swModel.IsOpenedReadOnly();
                }
            }
            else
            {
                LogMessage("PDM: No PDM vault connected or not logged in. Proceeding as non-PDM file.");
                canWriteToFile = !swModel.IsOpenedReadOnly();
            }

            string currentPropertyVersion = "";
            try
            {
                string tempVal = "";
                string tempResolvedVal = "";
                bool tempWasResolved = false;
                bool tempLinkToProperty = false;
                swModel.Extension.CustomPropertyManager[""]
                    .Get6("Roeslein Standards Version", false, out tempVal, out tempResolvedVal, out tempWasResolved, out tempLinkToProperty);
                currentPropertyVersion = tempResolvedVal;
                LogMessage($"Retrieved 'Roeslein Standards Version' property: '{currentPropertyVersion}'");
            }
            catch
            {
                currentPropertyVersion = "";
                LogMessage("Could not get existing 'Roeslein Standards Version' property. It may not exist.");
            }

            if (!canWriteToFile && (string.IsNullOrWhiteSpace(currentPropertyVersion) || currentPropertyVersion != currentStandardsVersion))
            {
                LogMessage("File is not writable and standards need update/application. Cannot proceed.");
                if (!silentMode) MessageBox.Show($@"File '{Path.GetFileName(filePath)}' is read-only and standards need to be applied/updated. Cannot proceed.", "File Read-Only", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                if (wasCheckedOutByThisProcess && edmFile != null && parentFolder != null)
                {
                    try
                    {
                        edmFile.UnlockFile(0, "Automatic check-in after failed standards application due to read-only state.");
                        LogMessage("PDM: File checked back in after failing to apply standards to read-only file.");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"PDM: Failed to check file back in after error: {ex.Message}");
                    }
                }
                return new StandardsApplicationResult { StandardsApplied = false, CheckedOutByThisProcess = wasCheckedOutByThisProcess };
            }

            bool standardsAppliedSuccessfully = false;
            if (canWriteToFile)
            {
                try
                {
                    LogMessage("=== STARTING PROPERTY APPLICATION ===");
                    standardsAppliedSuccessfully = ApplyAllPropertyStandards(swModel, currentStandardsVersion, silentMode, settings);
                    LogMessage("=== PROPERTY APPLICATION COMPLETED ===");
                }
                catch (Exception ex)
                {
                    LogMessage($"ERROR applying standards or saving file: {ex.Message}\\nStackTrace: {ex.StackTrace}");
                    if (!silentMode) MessageBox.Show($"An error occurred while applying standards or saving the file: {ex.Message}", "Standards Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    standardsAppliedSuccessfully = false;
                }
            }
            else
            {
                string finalVersionCheck = "";
                try
                {
                    string tempValOut = "";
                    string tempResolvedValOut = "";
                    bool tempWasResolved = false;
                    bool tempLinkToProperty = false;
                    swModel.Extension.CustomPropertyManager[""].Get6("Roeslein Standards Version", false, out tempValOut, out tempResolvedValOut, out tempWasResolved, out tempLinkToProperty);
                    finalVersionCheck = tempResolvedValOut;
                }
                catch
                {
                    finalVersionCheck = "";
                    LogMessage("Error accessing 'Roeslein Standards Version' property when checking final state.");
                }

                if (finalVersionCheck == currentStandardsVersion) {
                    LogMessage("File is read-only or no PDM action taken, but standards are already up-to-date.");
                    standardsAppliedSuccessfully = true;
                } else {
                    LogMessage("File is read-only and standards need update. This state should have been caught earlier.");
                    standardsAppliedSuccessfully = false;
                }
            }

            if (!deferCheckIn && wasCheckedOutByThisProcess && edmFile != null && parentFolder != null && pdmVault != null && pdmVault.IsLoggedIn)
            {
                string filePathToCheckIn = swModel.GetPathName();
                string fileName = Path.GetFileName(filePathToCheckIn);
                
                try
                {
                    string comment = standardsAppliedSuccessfully ?
                        $"Applied Roeslein Standards Version: {currentStandardsVersion}." : "Attempted to apply Roeslein Standards; see logs for details.";
                    
                    LogMessage("=== FINAL VERIFICATION BEFORE CHECK-IN ===");
                    bool propertiesVerified = false;
                    
                    try
                    {
                        CustomPropertyManager swCustPropMgr = swModel.Extension.CustomPropertyManager[""];
                        string valOut = "";
                        string resolvedValOut = "";
                        bool wasResolved = false;
                        bool linkToProperty = false;
                        
                        swCustPropMgr.Get6("Roeslein Standards Version", false, out valOut, out resolvedValOut, out wasResolved, out linkToProperty);
                        LogMessage($"Final property check - Value: '{valOut}', Resolved: '{resolvedValOut}'");
                        
                        propertiesVerified = (resolvedValOut == currentStandardsVersion);
                        
                        if (!propertiesVerified)
                        {
                            LogMessage("WARNING: Property not set correctly - trying one more time");
                            int setPropResult = swCustPropMgr.Set2("Roeslein Standards Version", currentStandardsVersion);
                            LogMessage($"Final Set2 attempt result: {setPropResult}");
                            
                            swCustPropMgr.Get6("Roeslein Standards Version", false, out valOut, out resolvedValOut, out wasResolved, out linkToProperty);
                            LogMessage($"Re-check - Value: '{valOut}', Resolved: '{resolvedValOut}'");
                            
                            propertiesVerified = (resolvedValOut == currentStandardsVersion);
                        }
                        
                        if (propertiesVerified)
                        {
                            LogMessage("Properties verified successfully!");
                        }
                        else
                        {
                            LogMessage("WARNING: Property not set correctly, but proceeding with check-in");
                        }
                        
                        int saveErrors = 0;
                        int saveWarnings = 0;
                        bool finalSaveResult = swModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, 
                                               ref saveErrors, ref saveWarnings) as bool? ?? false;
                        
                        LogMessage($"Final save result: {finalSaveResult}, Errors: {saveErrors}");
                    }
                    catch (Exception propEx)
                    {
                        LogMessage($"Error during final property verification: {propEx.Message}");
                    }
                    
                    LogMessage("=== STARTING PDM CHECK-IN PROCESS ===");
                    // Use the PDMFileManager to check in the file
                    bool checkinResult = pdmFileManager.CheckInOpenFile(swModel, comment, false);
                    
                    if (checkinResult) 
                    {
                        LogMessage("=== PDM CHECK-IN PROCESS COMPLETED SUCCESSFULLY ===");
                        
                        if (propertiesVerified) 
                        {
                            if (!silentMode)
                            {
                                MessageBox.Show(
                                    $"Standards application complete.\n\n" +
                                    $"File processed: {filePath}",
                                    "Standards Application Complete",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                        }
                        else
                        {
                            if (!silentMode) MessageBox.Show($"File '{fileName}' was checked in, but there may have been issues setting the Roeslein Standards Version property.",
                                           "PDM Check-in Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        LogMessage("=== PDM CHECK-IN PROCESS FAILED ===");
                        if (!silentMode) MessageBox.Show($"Failed to check file '{fileName}' back into PDM.", "PDM Check-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error during final check-in process: {ex.Message}");
                    if (!silentMode) MessageBox.Show($"Error during final check-in process: {ex.Message}", "PDM Check-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            
            if (standardsAppliedSuccessfully)
            {
                LogMessage($"ApplyStandardsToActiveDocument completed successfully for '{filePath}'.");
                
                // Add message to inform user when standards are already up-to-date and no action was taken
                if (!wasCheckedOutByThisProcess && !silentMode)
                {
                    if (currentPropertyVersion == currentStandardsVersion)
                    {
                        MessageBox.Show($"File '{Path.GetFileName(filePath)}' already has the current standards version ({currentStandardsVersion}) applied.\n\nNo changes were needed.", 
                            "Standards Already Up-to-Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else if (edmFile != null && edmFile.IsLocked && !swModel.IsOpenedReadOnly())
                    {
                        // If file was already checked out by user and standards were applied
                        MessageBox.Show(
                            $"Standards version {currentStandardsVersion} applied to file '{Path.GetFileName(filePath)}'.\n\n" +
                            $"File was already checked out by you, so it was not checked back in.",
                            "Standards Application Complete", 
                            MessageBoxButtons.OK, 
                            MessageBoxIcon.Information);
                    }
                }
            }
            else
            {
                LogMessage($"ApplyStandardsToActiveDocument failed or partially completed for '{filePath}'.");
            }
            return new StandardsApplicationResult { StandardsApplied = standardsAppliedSuccessfully, CheckedOutByThisProcess = wasCheckedOutByThisProcess };
        }

        private bool ApplyAllPropertyStandards(ModelDoc2 swModel, string currentStandardsVersion, bool silentMode, Settings settings)
        {
            LogMessage("ApplyAllPropertyStandards called");
            var allStandards = Settings.CurrentPropertyStandards;
            LogMessage($"Loaded {allStandards?.Count ?? 0} property standards from in-memory cache.");
            if (allStandards == null || allStandards.Count == 0)
            {
                LogMessage("No property standards are defined in in-memory cache. Aborting property application.");
                if (!silentMode) swApp.SendMsgToUser2("No property standards are defined. Please configure them in the add-in settings.", (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
                return false;
            }

            swDocumentTypes_e docType = (swDocumentTypes_e)swModel.GetType();
            var applicableStandards = FilterStandardsByDocType(allStandards, docType);
            LogMessage($"Found {applicableStandards.Count} applicable standards for doc type {docType}.");
            if (applicableStandards.Count == 0)
            {
                LogMessage($"No applicable property standards found for document type {docType}. Aborting property application.");
                if (!silentMode) swApp.SendMsgToUser2("No applicable property standards found for this document type.", (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
                return false;
            }

            if (docType == swDocumentTypes_e.swDocPART)
            {
                CheckAndMapMaterial(swModel, settings);
            }

            ApplyFilteredStandards(swModel, applicableStandards, currentStandardsVersion, settings);

            return true;
        }

        /// <summary>
        /// Check if file is locked by current user - using a very simplified approach
        /// </summary>
        private bool IsLockedByCurrentUser(EPDM.Interop.epdm.IEdmFile5 file)
        {
            if (!file.IsLocked) return false;
            
            try
            {
                // Return as a function of UserName - check if the current logged in user matches the file lock
                LogMessage("We cannot determine file checkout ownership in the current PDM API version.");
                
                // For now, assume the file isn't locked by the current user
                // This will result in a conservative approach - the UI will show locked files as 
                // being locked by other users unless the document is open and not read-only
                
                // Just use a simple flag to indicate we don't know
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"Error in IsLockedByCurrentUser: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Helper method to get document by path
        /// </summary>
        private ModelDoc2 GetDocumentByPath(string path)
        {
            if (swApp == null) return null;
            
            object[] openDocs = swApp.GetDocuments() as object[];
            if (openDocs == null) return null;
            
            foreach (object docObj in openDocs)
            {
                ModelDoc2 doc = docObj as ModelDoc2;
                if (doc != null && string.Equals(doc.GetPathName(), path, StringComparison.OrdinalIgnoreCase))
                {
                    return doc;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Get the current standards version from a document
        /// </summary>
        private string GetCurrentStandardsVersion(ModelDoc2 doc)
        {
            if (doc == null) return string.Empty;
            try
            {
                CustomPropertyManager propMgr = doc.Extension.CustomPropertyManager[""];
                string[] propNames = propMgr.GetNames() as string[] ?? new string[0];
                LogMessage($"[GetCurrentStandardsVersion] Custom tab property names for {doc.GetPathName()}: {string.Join(", ", propNames)}");
                if (!propNames.Any(n => n.Equals("Roeslein Standards Version", StringComparison.OrdinalIgnoreCase)))
                {
                    LogMessage($"[GetCurrentStandardsVersion] Property not found in summary tab for {doc.GetPathName()}");
                    return string.Empty;
                }
                string tempVal = "";
                string tempResolvedVal = "";
                bool tempWasResolved = false;
                bool tempLinkToProperty = false;
                propMgr.Get6("Roeslein Standards Version", false, out tempVal, out tempResolvedVal, out tempWasResolved, out tempLinkToProperty);
                LogMessage($"[GetCurrentStandardsVersion] Found property in summary tab: Value='{tempVal}', Resolved='{tempResolvedVal}'");
                return tempResolvedVal;
            }
            catch (Exception ex)
            {
                LogMessage($"[GetCurrentStandardsVersion] Exception: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Recursively scans all components (including subassemblies) for standards compliance
        /// </summary>
        private void ScanComponentsForStandards(
            Component2 comp,
            string requiredVersion,
            List<FileCheckoutInfo> filesWithStandardsUpToDate,
            List<FileCheckoutInfo> filesNotUpToDate,
            List<string> skippedFiles)
        {
            try
            {
                ModelDoc2 compDoc = comp.GetModelDoc2() as ModelDoc2;
                if (compDoc == null)
                    return;

                int docType = compDoc.GetType();
                if (docType == (int)swDocumentTypes_e.swDocPART)
                {
                    bool isSheetMetal = IsSheetMetalPart(compDoc);
                    if (isSheetMetal)
                    {
                        string filePath = compDoc.GetPathName();
                        string partVersion = GetCurrentStandardsVersion(compDoc);
                        if (string.IsNullOrWhiteSpace(partVersion) || partVersion != requiredVersion)
                        {
                            LogMessage($"[Recursive] Component '{comp.Name2}' is NOT up to date. Found: '{partVersion}', Required: '{requiredVersion}'");
                            filesNotUpToDate.Add(new FileCheckoutInfo { Path = filePath, File = null, Folder = null, StandardsApplied = false, Document = compDoc });
                        }
                        else
                        {
                            LogMessage($"[Recursive] Component '{comp.Name2}' is up to date. Version: '{partVersion}'");
                            filesWithStandardsUpToDate.Add(new FileCheckoutInfo { Path = filePath, File = null, Folder = null, StandardsApplied = true, Document = compDoc });
                        }
                    }
                }
                else if (docType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    // Check the subassembly itself (not just the root)
                    string subAsmPath = compDoc.GetPathName();
                    string subAsmVersion = GetCurrentStandardsVersion(compDoc);
                    if (string.IsNullOrWhiteSpace(subAsmVersion) || subAsmVersion != requiredVersion)
                    {
                        LogMessage($"[Recursive] Subassembly '{comp.Name2}' is NOT up to date. Found: '{subAsmVersion}', Required: '{requiredVersion}'");
                        filesNotUpToDate.Add(new FileCheckoutInfo { Path = subAsmPath, File = null, Folder = null, StandardsApplied = false, Document = compDoc });
                    }
                    else
                    {
                        LogMessage($"[Recursive] Subassembly '{comp.Name2}' is up to date. Version: '{subAsmVersion}'");
                        filesWithStandardsUpToDate.Add(new FileCheckoutInfo { Path = subAsmPath, File = null, Folder = null, StandardsApplied = true, Document = compDoc });
                    }
                    // Then recurse into children
                    object[] children = comp.GetChildren() as object[];
                    if (children != null)
                    {
                        foreach (Component2 child in children)
                        {
                            ScanComponentsForStandards(child, requiredVersion, filesWithStandardsUpToDate, filesNotUpToDate, skippedFiles);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"[Recursive] Error scanning component: {ex.Message}");
                skippedFiles.Add($"{comp.Name2} - error: {ex.Message}");
            }
        }

        /// <summary>
        /// Process an assembly file and apply standards to all its components (recursive)
        /// </summary>
        public void ProcessAssemblyForStandards(ModelDoc2 assemblyDoc, string requiredStandardsVersion, bool silentMode = false)
        {
            Settings.LoadSettings();
            var settings = Settings.LoadSettings();
            if (assemblyDoc == null || assemblyDoc.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                LogMessage("ProcessAssemblyForStandards: Not an assembly document.");
                if (!silentMode) MessageBox.Show("The active document is not an assembly.", "Standards Application", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LogMessage($"=== BEGIN ASSEMBLY STANDARDS PROCESSING ===");
            LogMessage($"Assembly path: {assemblyDoc.GetPathName()}");
            LogMessage($"Required standards version: {requiredStandardsVersion}");

            // NEW: List to track files missing drawings
            List<string> filesMissingDrawings = new List<string>();
            // Check if the assembly itself has a drawing
            string assemblyPath = assemblyDoc.GetPathName();
            LogMessage($"======= CHECKING FOR DRAWING OF ASSEMBLY: {assemblyPath} =======");
            if (!HasAssociatedDrawing(assemblyPath))
            {
                filesMissingDrawings.Add(System.IO.Path.GetFileName(assemblyPath));
                LogMessage($"*** ASSEMBLY FILE DOES NOT HAVE AN ASSOCIATED DRAWING: {assemblyPath} ***");
            }
            else
            {
                LogMessage($"√√√ ASSEMBLY FILE HAS AN ASSOCIATED DRAWING: {assemblyPath} √√√");
            }

            // NEW: Apply property standards to the assembly file itself before processing components
            List<string> summaryMessages = new List<string>();
            var topLevelResult = ApplyStandardsToActiveDocument(assemblyDoc, requiredStandardsVersion, silentMode, settings, true);
            bool topLevelCheckedOutByThisProcess = topLevelResult.CheckedOutByThisProcess;

            // Step 1: Recursively scan assembly and collect all files needing standards
            List<FileCheckoutInfo> filesToCheckout = new List<FileCheckoutInfo>();
            List<FileCheckoutInfo> filesAlreadyCheckedOut = new List<FileCheckoutInfo>();
            List<FileCheckoutInfo> filesWithStandardsUpToDate = new List<FileCheckoutInfo>();
            List<FileCheckoutInfo> filesNotUpToDate = new List<FileCheckoutInfo>();
            List<string> skippedFiles = new List<string>();

            StringBuilder combinedSummary = new StringBuilder();
            try
            {
                // Get the assembly's configuration manager and active configuration
                ConfigurationManager configMgr = assemblyDoc.ConfigurationManager;
                if (configMgr == null)
                {
                    LogMessage("Unable to get configuration manager from assembly.");
                    return;
                }
                
                Configuration activeConfig = configMgr.ActiveConfiguration;
                if (activeConfig == null)
                {
                    LogMessage("Unable to get active configuration from assembly.");
                    return;
                }
                
                LogMessage($"Current assembly configuration: {activeConfig.Name}");
                
                // Get the root component
                Component2 rootComp = activeConfig.GetRootComponent3(true);
                if (rootComp == null)
                {
                    LogMessage("Unable to get root component from assembly.");
                    return;
                }

                // RECURSIVE: Scan all components (including subassemblies)
                ScanComponentsForStandards(rootComp, requiredStandardsVersion, filesWithStandardsUpToDate, filesNotUpToDate, skippedFiles);

                // Also check the top-level assembly itself for standards (if not already included)
                string topLevelVersion = GetCurrentStandardsVersion(assemblyDoc);
                if (string.IsNullOrWhiteSpace(topLevelVersion) || topLevelVersion != requiredStandardsVersion)
                {
                    LogMessage($"[Recursive] Top-level assembly is NOT up to date. Found: '{topLevelVersion}', Required: '{requiredStandardsVersion}'");
                    filesNotUpToDate.Insert(0, new FileCheckoutInfo { Path = assemblyDoc.GetPathName(), File = null, Folder = null, StandardsApplied = false, Document = assemblyDoc });
                }
                else
                {
                    LogMessage($"[Recursive] Top-level assembly is up to date. Version: '{topLevelVersion}'");
                    filesWithStandardsUpToDate.Insert(0, new FileCheckoutInfo { Path = assemblyDoc.GetPathName(), File = null, Folder = null, StandardsApplied = true, Document = assemblyDoc });
                }

                // BATCH MODE: Check out all files that need update (not already checked out)
                List<FileCheckoutInfo> filesCheckedOutByThisProcess = new List<FileCheckoutInfo>();
                foreach (var fileInfo in filesNotUpToDate)
                {
                    try
                    {
                        // Attempt to check out (suppress popups)
                        bool checkedOut = pdmFileManager.CheckOutOpenFile(fileInfo.Document, true);
                        if (checkedOut)
                        {
                            fileInfo.CheckedOutByThisProcess = true;
                            filesCheckedOutByThisProcess.Add(fileInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Batch checkout failed for {fileInfo.Path}: {ex.Message}");
                        skippedFiles.Add($"{fileInfo.Path} - checkout failed: {ex.Message}");
                    }
                }

                // Apply standards to all files that need update (suppress popups)
                List<string> updatedFiles = new List<string>();
                foreach (var fileInfo in filesNotUpToDate)
                {
                    try
                    {
                        var result = ApplyStandardsToActiveDocument(fileInfo.Document, requiredStandardsVersion, true, settings);
                        if (result.StandardsApplied)
                        {
                            updatedFiles.Add(System.IO.Path.GetFileName(fileInfo.Path));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Batch apply failed for {fileInfo.Path}: {ex.Message}");
                        skippedFiles.Add($"{fileInfo.Path} - apply failed: {ex.Message}");
                    }
                }

                // Batch check-in all files checked out by this process
                foreach (var fileInfo in filesCheckedOutByThisProcess)
                {
                    try
                    {
                        string comment = $"Applied Roeslein Standards Version: {requiredStandardsVersion}.";
                        pdmFileManager.CheckInOpenFile(fileInfo.Document, comment, true);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Batch check-in failed for {fileInfo.Path}: {ex.Message}");
                        skippedFiles.Add($"{fileInfo.Path} - check-in failed: {ex.Message}");
                    }
                }

                // Show single summary popup
                int upToDate = filesWithStandardsUpToDate.Count;
                int notUpToDate = filesNotUpToDate.Count;
                var summaryBuilder = new StringBuilder();
                summaryBuilder.AppendLine("Assembly Standards Check Complete:");
                summaryBuilder.AppendLine($"Files Up to Date: {upToDate}");
                summaryBuilder.AppendLine($"Not Up To Date: {notUpToDate}");
                if (notUpToDate > 0)
                {
                    summaryBuilder.AppendLine();
                    summaryBuilder.AppendLine("Parts Not Up To Date:");
                    foreach (var f in filesNotUpToDate)
                        summaryBuilder.AppendLine($"- {System.IO.Path.GetFileName(f.Path)}");
                }
                if (updatedFiles.Count > 0)
                {
                    summaryBuilder.AppendLine();
                    summaryBuilder.AppendLine("Files Updated:");
                    foreach (var fname in updatedFiles)
                        summaryBuilder.AppendLine($"- {fname}");
                }
                if (skippedFiles.Count > 0)
                {
                    summaryBuilder.AppendLine();
                    summaryBuilder.AppendLine("Files Skipped (Errors):");
                    foreach (var fname in skippedFiles)
                        summaryBuilder.AppendLine($"- {fname}");
                }
                combinedSummary.Append(summaryBuilder.ToString());
            }
            catch (Exception ex)
            {
                string compName = "<unknown>";
                try { compName = (assemblyDoc as ModelDoc2)?.GetPathName() ?? "<unknown>"; } catch {}
                string msg = $"Error scanning assembly: {compName} - {ex.Message}";
                LogMessage(msg);
                skippedFiles.Add(msg);
            }

            LogMessage($"=== ASSEMBLY SCANNING COMPLETE ===");
            LogMessage($"Files with standards up to date: {filesWithStandardsUpToDate.Count}");
            LogMessage($"Files not up to date: {filesNotUpToDate.Count}");
            LogMessage($"Skipped files: {skippedFiles.Count}");

            // Step 2: Show dialog with list of files to check out
            if (filesToCheckout.Count > 0 || filesAlreadyCheckedOut.Count > 0)
            {
                StringBuilder message = new StringBuilder();
                int totalFilesNeedingStandards = filesToCheckout.Count + filesAlreadyCheckedOut.Count;
                
                message.AppendLine($"The following {totalFilesNeedingStandards} file(s) need to have Roeslein standards v{requiredStandardsVersion} applied:");
                message.AppendLine();
                
                foreach (var fileInfo in filesToCheckout)
                {
                    message.AppendLine($"• {fileInfo.FileName} (needs checkout)");
                }
                
                foreach (var fileInfo in filesAlreadyCheckedOut)
                {
                    message.AppendLine($"• {fileInfo.FileName} (already checked out)");
                }
                
                message.AppendLine();
                message.AppendLine("Proceed with standards application?");
                
                DialogResult result = MessageBox.Show(
                    message.ToString(),
                    "Standards Application",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                    
                if (result == DialogResult.Yes)
                {
                    // Step 3: Check out all files
                    List<FileCheckoutInfo> filesToProcess = new List<FileCheckoutInfo>();
                    
                    // Add files already checked out by user
                    filesToProcess.AddRange(filesAlreadyCheckedOut);
                    
                    // Process regular checkout files
                    foreach (var fileInfo in filesToCheckout)
                    {
                        try
                        {
                            if (!fileInfo.IsAlreadyCheckedOut)
                            {
                                // Use the PDMFileManager to check out the component
                                bool checkoutResult = pdmFileManager.CheckOutComponent(fileInfo.Document, true);
                                
                                if (checkoutResult)
                                {
                                    LogMessage($"PDM: Component '{fileInfo.Path}' checked out successfully using PDMFileManager.");
                                    fileInfo.IsAlreadyCheckedOut = true;
                                    fileInfo.CheckedOutByThisProcess = true; // Mark that we checked it out
                                    filesToProcess.Add(fileInfo);
                                }
                                else
                                {
                                    LogMessage($"PDM: Failed to check out component '{fileInfo.Path}'.");
                                    skippedFiles.Add($"{fileInfo.FileName} - checkout failed");
                                }
                            }
                            else
                            {
                                filesToProcess.Add(fileInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Failed to check out {fileInfo.Path}: {ex.Message}");
                            if (!silentMode) MessageBox.Show($"Failed to check out file: {fileInfo.FileName}\n\nError: {ex.Message}", "Checkout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            skippedFiles.Add($"{fileInfo.FileName} - error: {ex.Message}");
                        }
                    }
                    
                    // Step 4: Apply standards to each file
                    int successCount = 0;
                    foreach (var fileInfo in filesToProcess)
                    {
                        try
                        {
                            LogMessage($"Applying standards to: {fileInfo.Path}");
                            
                            var childResult = ApplyStandardsToActiveDocument(fileInfo.Document, requiredStandardsVersion, true, settings);
                            fileInfo.StandardsApplied = childResult.StandardsApplied;
                            
                            if (childResult.StandardsApplied)
                            {
                                successCount++;
                                LogMessage($"Successfully applied standards to: {fileInfo.Path}");
                            }
                            else
                            {
                                LogMessage($"Failed to apply standards to: {fileInfo.Path}");
                                skippedFiles.Add($"{fileInfo.FileName} - standards application failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Error applying standards to {fileInfo.Path}: {ex.Message}");
                            fileInfo.StandardsApplied = false;
                            skippedFiles.Add($"{fileInfo.FileName} - error during standards application: {ex.Message}");
                        }
                    }
                    
                    // Step 5: Check in all files with appropriate comments
                    int checkinCount = 0;
                    foreach (var fileInfo in filesToProcess)
                    {
                        // Check in the files that WE checked out (not ones that were already checked out by user)
                        if (fileInfo.File == null || !fileInfo.CheckedOutByThisProcess) continue;
                        
                        try
                        {
                            string comment = fileInfo.StandardsApplied ?
                                $"Standards v{requiredStandardsVersion} applied by Roeslein Connex" :
                                "Standards application attempted by Roeslein Connex but failed";
                                
                            LogMessage($"Checking in: {fileInfo.Path} with comment: {comment}");
                            
                            // Use the PDMFileManager to check in the component
                            bool checkinResult = pdmFileManager.CheckInOpenFile(fileInfo.Document, comment, true);
                            
                            if (checkinResult)
                            {
                                LogMessage($"Successfully checked in component: {fileInfo.Path}");
                                checkinCount++;
                            }
                            else
                            {
                                LogMessage($"Failed to check in component: {fileInfo.Path}");
                                if (!silentMode) MessageBox.Show($"Failed to check in file: {fileInfo.FileName}", 
                                    "Check-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                skippedFiles.Add($"{fileInfo.FileName} - check-in failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Failed to check in {fileInfo.Path}: {ex.Message}");
                            if (!silentMode) MessageBox.Show($"Failed to check in file: {fileInfo.FileName}\n\nError: {ex.Message}", 
                                "Check-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            skippedFiles.Add($"{fileInfo.FileName} - error during check-in: {ex.Message}");
                        }
                    }
                    
                    // Show summary
                    if (!silentMode)
                    {
                        // Count files that were already checked out
                        int alreadyCheckedOutCount = filesAlreadyCheckedOut.Count;
                        
                        StringBuilder summary = new StringBuilder();
                        summary.AppendLine($"Standards application complete.\n");
                        summary.AppendLine($"Files processed: {filesToProcess.Count}");
                        summary.AppendLine($"Standards applied successfully: {successCount}");
                        summary.AppendLine($"Files already checked out by you: {alreadyCheckedOutCount}");
                        summary.AppendLine($"Files checked back in: {checkinCount}");
                            
                        if (filesWithStandardsUpToDate.Count > 0)
                        {
                            summary.AppendLine($"Files already up to date: {filesWithStandardsUpToDate.Count}");
                        }
                        
                        // Add information about files missing drawings
                        if (filesMissingDrawings.Count > 0)
                        {
                            summary.AppendLine();
                            summary.AppendLine($"===== FILES MISSING DRAWINGS ({filesMissingDrawings.Count}) =====");
                            foreach (string missingDrawingFile in filesMissingDrawings)
                            {
                                summary.AppendLine($"• {missingDrawingFile}");
                            }
                        }
                        else
                        {
                            summary.AppendLine();
                            summary.AppendLine("All files have associated drawings.");
                        }
                            
                        combinedSummary.AppendLine();
                        combinedSummary.Append(summary.ToString());
                    }
                }
                else
                {
                    LogMessage("User cancelled batch standards application.");
                }
            }
            else
            {
                LogMessage("No files found that need standards applied.");
                StringBuilder message = new StringBuilder();
                
                if (filesWithStandardsUpToDate.Count > 0 && skippedFiles.Count == 0)
                {
                    message.AppendLine($"All {filesWithStandardsUpToDate.Count} component files already have standards v{requiredStandardsVersion} applied.");
                    message.AppendLine("No changes were needed.");
                }
                else if (skippedFiles.Count > 0 && filesWithStandardsUpToDate.Count == 0)
                {
                    message.AppendLine("All files requiring standards are checked out by other users.");
                }
                else if (filesWithStandardsUpToDate.Count > 0 && skippedFiles.Count > 0)
                {
                    message.AppendLine($"{filesWithStandardsUpToDate.Count} files already have current standards.");
                    message.AppendLine($"{skippedFiles.Count} files are checked out by other users.");
                }
                else
                {
                    message.AppendLine("No files in this assembly need standards applied or all files requiring standards are checked out by other users.");
                }
                
                // Add information about files missing drawings
                if (filesMissingDrawings.Count > 0)
                {
                    message.AppendLine();
                    message.AppendLine($"===== FILES MISSING DRAWINGS ({filesMissingDrawings.Count}) =====");
                    foreach (string missingDrawingFile in filesMissingDrawings)
                    {
                        message.AppendLine($"• {missingDrawingFile}");
                    }
                }
                else
                {
                    message.AppendLine();
                    message.AppendLine("All files have associated drawings.");
                }
                
                if (!silentMode) combinedSummary.AppendLine(message.ToString());
            }

            // Save the top-level assembly
            int saveErrors = 0, saveWarnings = 0;
            assemblyDoc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);

            // Get the file from the vault before attempting check-in
            EPDM.Interop.epdm.IEdmFolder5 parentFolder;
            EPDM.Interop.epdm.IEdmFile5 topLevelEdmFile = pdmVault.GetFileFromPath(assemblyDoc.GetPathName(), out parentFolder);
            
            if (topLevelCheckedOutByThisProcess && topLevelEdmFile != null && parentFolder != null && topLevelEdmFile.IsLocked)
            {
                // Only attempt check-in if the file is actually checked out by this process
                string topLevelComment = $"Top-level assembly checked in after all children. Standards v{requiredStandardsVersion} applied.";
                bool topLevelCheckin = pdmFileManager.CheckInOpenFile(assemblyDoc, topLevelComment, silentMode);
                if (!topLevelCheckin && !silentMode) {
                    MessageBox.Show("Failed to check in the top-level assembly. Please check manually.", "Check-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                LogMessage("Top-level assembly was not checked out by this process. Skipping check-in.");
            }

            // At the very end, show only one popup if not silent
            if (!silentMode)
            {
                new StandardsSummaryForm(combinedSummary.ToString(), "Standards Application").ShowDialog();
            }
        }

        /// <summary>
        /// Filters property standards by document type.
        /// </summary>
        private List<PropertyStandardSetting> FilterStandardsByDocType(List<PropertyStandardSetting> allStandards, swDocumentTypes_e docType)
        {
            switch (docType)
            {
                case swDocumentTypes_e.swDocPART:
                    return allStandards.Where(s => s.UseOnPartFiles).ToList();
                case swDocumentTypes_e.swDocASSEMBLY:
                    return allStandards.Where(s => s.UseOnAssemblyFiles).ToList();
                case swDocumentTypes_e.swDocDRAWING:
                    return allStandards.Where(s => s.UseOnDrawingFiles).ToList();
                default:
                    return new List<PropertyStandardSetting>();
            }
        }

        /// <summary>
        /// Checks if the part is sheet metal and verifies/maps the material according to the Material Mappings
        /// </summary>
        private void CheckAndMapMaterial(ModelDoc2 swModel, Settings settings)
        {
            try
            {
                // Check if this is a sheet metal part
                bool isSheetMetal = IsSheetMetalPart(swModel);
                if (!isSheetMetal)
                {
                    LogMessage("Not a sheet metal part, skipping material mapping check.");
                    return;
                }
                // Ensure cut list exists and is set to automatic
                EnsureSheetMetalCutList(swModel, settings);
                // Material mapping logic (from backup)
                string currentMaterial = GetPartMaterial(swModel);
                if (string.IsNullOrEmpty(currentMaterial))
                {
                    LogMessage("Sheet metal part has no material assigned.");
                    return;
                }
                string normalizedMaterial = currentMaterial;
                if (currentMaterial.Contains("|"))
                {
                    string[] parts = currentMaterial.Split('|');
                    if (parts.Length >= 2)
                        normalizedMaterial = parts[1].Trim();
                }
                var materialMappings = settings.MaterialMappings;
                if (materialMappings == null || materialMappings.Count == 0)
                {
                    LogMessage("No material mappings defined in settings.");
                    return;
                }
                var exactMatch = materialMappings.FirstOrDefault(m => string.Equals(m.DxfMaterial, normalizedMaterial, StringComparison.OrdinalIgnoreCase));
                if (exactMatch != null)
                {
                    LogMessage($"Material '{normalizedMaterial}' is already in the correct output format.");
                    return;
                }
                var matchToMap = materialMappings.FirstOrDefault(m => string.Equals(m.SwMaterial, normalizedMaterial, StringComparison.OrdinalIgnoreCase));
                if (matchToMap != null)
                {
                    string targetMaterial = matchToMap.DxfMaterial;
                    bool success = SetPartMaterial(swModel, targetMaterial);
                    if (success)
                        LogMessage($"Updated material from '{currentMaterial}' to '{targetMaterial}'");
                    else
                        LogMessage($"Failed to update material from '{currentMaterial}' to '{targetMaterial}'");
                    return;
                }
                LogMessage($"No material mapping found for '{currentMaterial}'.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error in CheckAndMapMaterial: {ex.Message}");
            }
        }

        private bool IsSheetMetalPart(ModelDoc2 swModel)
        {
            try
            {
                int docType = swModel.GetType();
                if (docType != (int)swDocumentTypes_e.swDocPART)
                    return false;
                Feature feat = swModel.FirstFeature() as Feature;
                while (feat != null)
                {
                    string typeName = feat.GetTypeName2();
                    if (typeName == "SheetMetal" || typeName == "SMBaseFlange")
                        return true;
                    feat = feat.GetNextFeature() as Feature;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"Error checking if part is sheet metal: {ex.Message}");
                return false;
            }
        }

        private string GetPartMaterial(ModelDoc2 swModel)
        {
            try
            {
                if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                    return string.Empty;
                return swModel.MaterialIdName;
            }
            catch (Exception ex)
            {
                LogMessage($"Error getting part material: {ex.Message}");
                return string.Empty;
            }
        }

        private bool SetPartMaterial(ModelDoc2 swModel, string materialName)
        {
            try
            {
                if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    LogMessage("Cannot set material on non-part document");
                    return false;
                }
                PartDoc partDoc = (PartDoc)swModel;
                const string matPropFile = @"C:\PCSVAULT\SolidWorks Settings\Material Databases 1\Roeslein Standard Materials.sldmat";
                string originalMaterial = swModel.MaterialIdName ?? "None";
                LogMessage($"Current material before change: '{originalMaterial}'");
                try
                {
                    LogMessage($"Setting material to '{materialName}' from database '{matPropFile}'");
                    partDoc.SetMaterialPropertyName(matPropFile, materialName);
                    swModel.ForceRebuild3(false);
                    string newMaterial = swModel.MaterialIdName ?? "None";
                    LogMessage($"Material after SetMaterialPropertyName: '{newMaterial}'");
                    if (newMaterial.Contains(materialName))
                        return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"Exception in SetPartMaterial: {ex.Message}");
                }
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"Error in SetPartMaterial: {ex.Message}");
                return false;
            }
        }

        private bool EnsureSheetMetalCutList(ModelDoc2 swModel, Settings settings)
        {
            try
            {
                if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    LogMessage("Not a part document, skipping cut list check");
                    return false;
                }
                LogMessage("Checking for sheet metal cut list...");
                bool isSheetMetal = IsSheetMetalPart(swModel);
                if (!isSheetMetal)
                {
                    LogMessage("Not a sheet metal part, skipping cut list check");
                    return false;
                }
                Feature swFeat = (Feature)swModel.FirstFeature();
                bool cutListFound = false;
                while (swFeat != null)
                {
                    string typeName = swFeat.GetTypeName2();
                    if (typeName == "SolidBodyFolder")
                    {
                        cutListFound = true;
                        LogMessage("Found SolidBodyFolder feature");
                        BodyFolder swBodyFolder = swFeat.GetSpecificFeature2() as BodyFolder;
                        if (swBodyFolder != null)
                        {
                            bool isAuto = swBodyFolder.GetAutomaticCutList();
                            LogMessage($"Automatic cut list is currently: {(isAuto ? "Enabled" : "Disabled")}");
                            swBodyFolder.SetAutomaticCutList(true);
                            swBodyFolder.SetAutomaticUpdate(true);
                            LogMessage("Set cut list to automatic update");
                        }
                        else
                        {
                            LogMessage("Found SolidBodyFolder but couldn't get BodyFolder interface");
                        }
                        break;
                    }
                    swFeat = (Feature)swFeat.GetNextFeature();
                }
                if (!cutListFound)
                {
                    LogMessage("SolidBodyFolder not found. Attempting to create cut list...");
                    try
                    {
                        PartDoc partDoc = (PartDoc)swModel;
                        bool selectResult = false;
                        try {
                            SelectionMgr swSelMgr = (SelectionMgr)swModel.SelectionManager;
                            Body2[] swBodies = (Body2[])partDoc.GetBodies2((int)swBodyType_e.swSolidBody, true);
                            if (swBodies != null && swBodies.Length > 0)
                            {
                                swModel.ClearSelection2(true);
                                foreach (Body2 body in swBodies)
                                {
                                    swSelMgr.AddSelectionListObject(body, null);
                                }
                                selectResult = true;
                                LogMessage($"Selected {swBodies.Length} bodies for cut list creation");
                            }
                            else
                            {
                                LogMessage("No solid bodies found in the part");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Error selecting bodies for cut list creation: {ex.Message}");
                        }
                        if (selectResult)
                        {
                            bool cmdResult = swModel.Extension.RunCommand(1801, "");
                            if (cmdResult)
                            {
                                LogMessage("Successfully created cut list through command manager");
                                swModel.ForceRebuild3(false);
                                Feature newFeat = (Feature)swModel.FirstFeature();
                                while (newFeat != null)
                                {
                                    string typeName = newFeat.GetTypeName2();
                                    if (typeName == "SolidBodyFolder")
                                    {
                                        cutListFound = true;
                                        BodyFolder swBodyFolder = newFeat.GetSpecificFeature2() as BodyFolder;
                                        if (swBodyFolder != null)
                                        {
                                            swBodyFolder.SetAutomaticCutList(true);
                                            swBodyFolder.SetAutomaticUpdate(true);
                                            LogMessage("Set automatic cut list on new cut list");
                                        }
                                        break;
                                    }
                                    newFeat = (Feature)newFeat.GetNextFeature();
                                }
                            }
                            else
                            {
                                LogMessage("Failed to run cut list command");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error creating cut list: {ex.Message}");
                    }
                }
                return cutListFound;
            }
            catch (Exception ex)
            {
                LogMessage($"Error in EnsureSheetMetalCutList: {ex.Message}");
                return false;
            }
        }

        private bool ProcessCutListAndRawMaterial(ModelDoc2 swModel, Settings settings)
        {
            LogMessage("ProcessCutListAndRawMaterial called");
            try
            {
                // Defensive: Ensure settings and RawMaterials are loaded
                if (settings == null || settings.RawMaterials == null || settings.RawMaterials.Count == 0)
                {
                    LogMessage("RawMaterials not loaded, attempting to reload settings...");
                    settings = Settings.LoadSettings();
                    if (settings == null)
                    {
                        LogMessage("Settings.LoadSettings() returned null. Aborting raw material processing.");
                        return false;
                    }
                    if (settings.RawMaterials == null)
                    {
                        LogMessage("settings.RawMaterials is null after reload. Aborting raw material processing.");
                        return false;
                    }
                    if (settings.RawMaterials.Count == 0)
                    {
                        LogMessage("settings.RawMaterials is empty after reload. Aborting raw material processing.");
                        return false;
                    }
                    LogMessage($"Reloaded settings. RawMaterials count: {settings.RawMaterials.Count}");
                }
                // Ensure cut list exists and is set to automatic
                bool cutListFound = EnsureSheetMetalCutList(swModel, settings);
                swModel.ForceRebuild3(false); // Force rebuild to ensure cut list and bounding box data are up to date
                LogMessage($"After EnsureSheetMetalCutList and ForceRebuild3, cut list found: {cutListFound}");
                if (!IsSheetMetalPart(swModel) || !cutListFound)
                {
                    LogMessage("Not a sheet metal part with a found cut list, skipping raw material processing.");
                    return false;
                }
                LogMessage("Reading cut list data for raw material matching...");
                double boundingBoxLength = 0;
                double boundingBoxWidth = 0;
                double sheetMetalThickness = 0;
                string partMaterial = GetPartMaterial(swModel);
                Feature swFeat = (Feature)swModel.FirstFeature();
                Feature cutListItemFeature = null;
                while (swFeat != null)
                {
                    if (swFeat.GetTypeName2() == "SolidBodyFolder")
                    {
                        Feature subFeat = swFeat.IGetFirstSubFeature() as Feature;
                        while (subFeat != null)
                        {
                            if (subFeat.GetTypeName2() == "CutListFolder")
                            {
                                cutListItemFeature = subFeat;
                                break;
                            }
                            subFeat = subFeat.IGetNextSubFeature() as Feature;
                        }
                        break;
                    }
                    swFeat = (Feature)swFeat.GetNextFeature();
                }
                if (cutListItemFeature == null)
                {
                    LogMessage("Could not find a 'CutListFolder' feature under SolidBodyFolder.");
                    return false;
                }
                CustomPropertyManager cutListPropMgr = cutListItemFeature.CustomPropertyManager;
                if (cutListPropMgr == null)
                {
                    LogMessage($"Could not get CustomPropertyManager for cut list item feature: {cutListItemFeature.Name}");
                    return false;
                }
                string resolvedValue;
                bool wasResolved;
                int res = cutListPropMgr.Get5("Bounding Box Length", true, out _, out resolvedValue, out wasResolved);
                if ((res == 0 || res == 2) && wasResolved && double.TryParse(resolvedValue, out boundingBoxLength))
                {
                    LogMessage($"Found Bounding Box Length: {boundingBoxLength}");
                }
                else
                {
                    LogMessage($"Failed to get Bounding Box Length or parse value. Get5 result: {res}, Resolved: {wasResolved}, Value: '{resolvedValue}'");
                    boundingBoxLength = 0;
                }
                res = cutListPropMgr.Get5("Bounding Box Width", true, out _, out resolvedValue, out wasResolved);
                if ((res == 0 || res == 2) && wasResolved && double.TryParse(resolvedValue, out boundingBoxWidth))
                {
                    LogMessage($"Found Bounding Box Width: {boundingBoxWidth}");
                }
                else
                {
                    LogMessage($"Failed to get Bounding Box Width or parse value. Get5 result: {res}, Resolved: {wasResolved}, Value: '{resolvedValue}'");
                    boundingBoxWidth = 0;
                }
                res = cutListPropMgr.Get5("Sheet Metal Thickness", true, out _, out resolvedValue, out wasResolved);
                if ((res == 0 || res == 2) && wasResolved && double.TryParse(resolvedValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out sheetMetalThickness))
                {
                    LogMessage($"Found 'Sheet Metal Thickness' from cut list: {sheetMetalThickness} (assuming document units, e.g., inches)");
                }
                else
                {
                    LogMessage($"Failed to get 'Sheet Metal Thickness' from cut list. Get5 result: {res}, Resolved: {wasResolved}, Value: '{resolvedValue}'. Trying part's sheet metal feature as fallback.");
                    sheetMetalThickness = 0;
                    if (IsSheetMetalPart(swModel))
                    {
                        Feature smFeat = swModel.FirstFeature() as Feature;
                        while (smFeat != null)
                        {
                            if (smFeat.GetTypeName2() == "SheetMetal")
                            {
                                SheetMetalFeatureData smData = smFeat.GetDefinition() as SheetMetalFeatureData;
                                if (smData != null)
                                {
                                    double thicknessInMeters = smData.Thickness;
                                    sheetMetalThickness = thicknessInMeters * 39.3701;
                                    LogMessage($"Found Sheet Metal Thickness from SM feature: {thicknessInMeters} meters, converted to {sheetMetalThickness} inches.");
                                    break;
                                }
                            }
                            smFeat = smFeat.GetNextFeature() as Feature;
                        }
                    }
                }
                partMaterial = GetPartMaterial(swModel);
                if (string.IsNullOrEmpty(partMaterial))
                {
                    LogMessage("Part material is empty.");
                }
                else
                {
                    LogMessage($"Found Part Material: {partMaterial}");
                }
                if (boundingBoxLength == 0 || boundingBoxWidth == 0 || sheetMetalThickness == 0 || string.IsNullOrEmpty(partMaterial))
                {
                    LogMessage("Could not find all required cut list data (Length, Width, Thickness, Material).");
                    LogMessage($"Debug - Length: {boundingBoxLength}, Width: {boundingBoxWidth}, Thickness: {sheetMetalThickness}, Material: '{partMaterial}'");
                    return false;
                }
                // Now match with raw material table
                var rawMaterials = settings.RawMaterials;
                const double thicknessTolerance = 0.005;
                string normalizedPartMaterial = partMaterial;
                if (partMaterial.Contains("|"))
                {
                    string[] parts = partMaterial.Split('|');
                    if (parts.Length >= 2)
                        normalizedPartMaterial = parts[1].Trim();
                }
                var matches = rawMaterials.Where(rm =>
                    string.Equals(rm.Material.Trim(), normalizedPartMaterial, StringComparison.OrdinalIgnoreCase) &&
                    Math.Abs(rm.Thickness - sheetMetalThickness) < thicknessTolerance &&
                    rm.Length >= boundingBoxLength &&
                    rm.Width >= boundingBoxWidth
                ).ToList();
                var bestMatch = matches.OrderBy(m => m.Length * m.Width).FirstOrDefault();
                if (bestMatch != null)
                {
                    double boundingBoxArea = boundingBoxLength * boundingBoxWidth;
                    string areaValue = boundingBoxArea.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    string partNumber = bestMatch.PartNumber;
                    string unitOfMeasure = bestMatch.UnitOfMeasure ?? "SI";
                    string description = bestMatch.Description ?? "";
                    CustomPropertyManager modelPropMgr = swModel.Extension.CustomPropertyManager[""];
                    modelPropMgr.Add3("Raw Material Number", (int)swCustomInfoType_e.swCustomInfoText, partNumber, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("Unit of Measurement", (int)swCustomInfoType_e.swCustomInfoText, "SI", (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("legacy Part Number", (int)swCustomInfoType_e.swCustomInfoText, partNumber, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("Legacy Unit of Measure", (int)swCustomInfoType_e.swCustomInfoText, "SI", (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("Raw Mat Amount", (int)swCustomInfoType_e.swCustomInfoText, areaValue, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("Raw Material Description", (int)swCustomInfoType_e.swCustomInfoText, description, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("legacy Part Description", (int)swCustomInfoType_e.swCustomInfoText, description, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("Legacy Raw Mat Amount", (int)swCustomInfoType_e.swCustomInfoText, areaValue, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

                    // Add the same values to all configurations
                    string[] configNames = (string[])swModel.GetConfigurationNames();
                    if (configNames != null)
                    {
                        foreach (string configName in configNames)
                        {
                            if (string.IsNullOrEmpty(configName)) continue;
                            CustomPropertyManager configPropMgr = swModel.Extension.CustomPropertyManager[configName];
                            configPropMgr.Add3("Raw Material Number", (int)swCustomInfoType_e.swCustomInfoText, partNumber, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("Unit of Measurement", (int)swCustomInfoType_e.swCustomInfoText, "SI", (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("legacy Part Number", (int)swCustomInfoType_e.swCustomInfoText, partNumber, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("Legacy Unit of Measure", (int)swCustomInfoType_e.swCustomInfoText, "SI", (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("Raw Mat Amount", (int)swCustomInfoType_e.swCustomInfoText, areaValue, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("Raw Material Description", (int)swCustomInfoType_e.swCustomInfoText, description, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("legacy Part Description", (int)swCustomInfoType_e.swCustomInfoText, description, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("Legacy Raw Mat Amount", (int)swCustomInfoType_e.swCustomInfoText, areaValue, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                        }
                    }
                    LogMessage($"Raw Material PN {partNumber} (UOM: SI) and all legacy/raw material properties applied to summary tab and all configurations.");

                    // --- NEW: Loop through ALL CutListFolder features and update properties ---
                    Feature clSwFeat = (Feature)swModel.FirstFeature();
                    int cutListCount = 0;
                    while (clSwFeat != null)
                    {
                        if (clSwFeat.GetTypeName2() == "SolidBodyFolder")
                        {
                            Feature subFeat = clSwFeat.IGetFirstSubFeature() as Feature;
                            while (subFeat != null)
                            {
                                if (subFeat.GetTypeName2() == "CutListFolder")
                                {
                                    cutListCount++;
                                    CustomPropertyManager clPropMgr = subFeat.CustomPropertyManager;
                                    // Use file property values if available, otherwise fallback
                                    string fileMaterial = null;
                                    string fileThickness = null;
                                    string val, resolvedVal;
                                    bool clWasResolved;
                                    int resMat = swModel.Extension.CustomPropertyManager[""]
                                        .Get5("Material", true, out val, out resolvedVal, out clWasResolved);
                                    if ((resMat == 0 || resMat == 2) && clWasResolved && !string.IsNullOrEmpty(resolvedVal))
                                        fileMaterial = resolvedVal;
                                    int resThk = swModel.Extension.CustomPropertyManager[""]
                                        .Get5("Sheet Metal Thickness", true, out val, out resolvedVal, out clWasResolved);
                                    if ((resThk == 0 || resThk == 2) && clWasResolved && !string.IsNullOrEmpty(resolvedVal))
                                        fileThickness = resolvedVal;
                                    string materialValue = !string.IsNullOrEmpty(fileMaterial) ? fileMaterial : partMaterial;
                                    string thicknessValue = !string.IsNullOrEmpty(fileThickness) ? fileThickness : sheetMetalThickness.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                                    string descValue = $"Sheet, {materialValue}, {thicknessValue} Thick, {boundingBoxLength:F3} X {boundingBoxWidth:F3}";
                                    clPropMgr.Add3("Raw Material Number", (int)swCustomInfoType_e.swCustomInfoText, partNumber, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                                    clPropMgr.Add3("Description", (int)swCustomInfoType_e.swCustomInfoText, descValue, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                                    LogMessage($"[CutList] Updated {subFeat.Name}: Raw Material Number={partNumber}, Description={descValue}");
                                }
                                subFeat = subFeat.IGetNextSubFeature() as Feature;
                            }
                        }
                        clSwFeat = (Feature)clSwFeat.GetNextFeature();
                    }
                    LogMessage($"Processed {cutListCount} cut list items for Raw Material Number and Description.");
                    return true;
                }
                else
                {
                    LogMessage("No matching raw material found in table.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error in ProcessCutListAndRawMaterial: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies the filtered property standards to the model, and sets the Roeslein Standards Version property LAST.
        /// </summary>
        private void ApplyFilteredStandards(ModelDoc2 swModel, List<PropertyStandardSetting> standardsToApply, string currentStandardsVersion, Settings settings)
        {
            var allStandards = Settings.CurrentPropertyStandards;
            LogMessage($"Loaded {allStandards?.Count ?? 0} property standards from in-memory cache.");
            if (allStandards == null || allStandards.Count == 0)
            {
                LogMessage("No property standards are defined in in-memory cache. Aborting property application.");
                swApp.SendMsgToUser2("No property standards are defined. Please configure them in the add-in settings.", (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
                return;
            }

            swDocumentTypes_e docType = (swDocumentTypes_e)swModel.GetType();
            var applicableStandards = FilterStandardsByDocType(allStandards, docType);
            LogMessage($"Found {applicableStandards.Count} applicable standards for doc type {docType}.");
            if (applicableStandards.Count == 0)
            {
                LogMessage($"No applicable property standards found for document type {docType}. Aborting property application.");
                swApp.SendMsgToUser2("No applicable property standards found for this document type.", (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
                return;
            }

            if (docType == swDocumentTypes_e.swDocPART)
            {
                CheckAndMapMaterial(swModel, settings);
            }

            // 1. Apply all properties except 'Roeslein Standards Version'
            foreach (var standard in standardsToApply)
            {
                if (string.Equals(standard.PropertyName, "Roeslein Standards Version", StringComparison.OrdinalIgnoreCase))
                    continue; // Skip for now
                // Apply to Custom tab if checked
                if (standard.IsCustomProperty)
                {
                    ApplyOrUpdateProperty(swModel.Extension.CustomPropertyManager[""], standard, standard.DefaultValueExpr, "Custom", settings);
                }
                // Apply to ALL Config tabs if checked
                if (standard.IsConfigSpecific && swModel.GetConfigurationNames() != null)
                {
                    foreach (string configName in (string[])swModel.GetConfigurationNames())
                    {
                        if (!string.IsNullOrEmpty(configName))
                            ApplyOrUpdateProperty(swModel.Extension.CustomPropertyManager[configName], standard, standard.DefaultValueExpr, $"Configuration ({configName})", settings);
                    }
                }
            }
            
            // 2. Run cut list and raw material logic if part
            if (docType == swDocumentTypes_e.swDocPART)
            {
                LogMessage("Running CheckAndMapMaterial...");
                CheckAndMapMaterial(swModel, settings);
                LogMessage("Running EnsureSheetMetalCutList...");
                bool cutListResult = EnsureSheetMetalCutList(swModel, settings);
                if (cutListResult)
                {
                    LogMessage("Cut list found or created and set to automatic update.");
                    LogMessage("Running ProcessCutListAndRawMaterial...");
                    bool rawMatResult = ProcessCutListAndRawMaterial(swModel, settings);
                    if (rawMatResult)
                    {
                        LogMessage("Raw material properties successfully applied.");
                    }
                    else
                    {
                        LogMessage("No matching raw material found or applied.");
                    }
                }
                else
                {
                    LogMessage("Cut list was missing and could not be created. Skipping raw material processing.");
                }
            }
            
            // 3. Set document display properties - NEW STEP!
            SetDocumentDisplayProperties(swModel);
            
            // 4. Set 'Roeslein Standards Version' LAST in both Custom and Config tabs (for ALL file types)
            try
            {
                swModel.Extension.CustomPropertyManager[""].Add3("Roeslein Standards Version", (int)swCustomInfoType_e.swCustomInfoText, currentStandardsVersion, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                if (swModel.GetConfigurationNames() != null)
                {
                    foreach (string configName in (string[])swModel.GetConfigurationNames())
                    {
                        if (!string.IsNullOrEmpty(configName))
                            swModel.Extension.CustomPropertyManager[configName].Add3("Roeslein Standards Version", (int)swCustomInfoType_e.swCustomInfoText, currentStandardsVersion, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    }
                }
                LogMessage($"Set 'Roeslein Standards Version' property to '{currentStandardsVersion}' on summary and all configs.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting 'Roeslein Standards Version': {ex.Message}");
            }
        }

        /// <summary>
        /// Adds or updates a property in the given property manager, using the correct logic for default values and property existence.
        /// </summary>
        private void ApplyOrUpdateProperty(CustomPropertyManager propMgr, PropertyStandardSetting standard, string defaultExpr, string tabName, Settings settings)
        {
            try
            {
                // Get document type from the model - ensure we're getting the actual type
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                swDocumentTypes_e docType = (swDocumentTypes_e)swModel.GetType();
                
                // First try to get all properties to check case-sensitively
                string[] allPropertyNames = (string[])propMgr.GetNames();
                bool manualPropertyCheck = false;
                
                if (allPropertyNames != null)
                {
                    foreach (string propName in allPropertyNames)
                    {
                        if (string.Equals(propName, standard.PropertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            manualPropertyCheck = true;
                            break;
                        }
                    }
                }
                
                // Now try the regular API call
                int getResult = propMgr.Get5(standard.PropertyName, true, out string existingValue, out string resolvedValue, out bool wasResolved);
                
                // According to SolidWorks API docs - 0 means property exists, 1 means it doesn't
                bool propertyExists = (getResult == 0 || manualPropertyCheck);
                bool shouldSetValue = false;
                string valueToAdd = string.Empty;

                // Use expressions from settings form if they're properly formatted, 
                // or format them correctly if they need to be adjusted
                string formattedExpression = FormatPropertyExpression(standard.PropertyName, defaultExpr, docType);

                // CASE 1: Property exists AND "Use Default Value" is checked - UPDATE it
                if (propertyExists && standard.UseDefaultValue)
                {
                    shouldSetValue = true;
                    valueToAdd = formattedExpression;
                    LogMessage($"Property '{standard.PropertyName}' exists and has value '{existingValue}'. Overwriting with '{valueToAdd}' because Use Default Value is checked in {tabName} tab.");
                }
                // CASE 2: Property exists AND "Use Default Value" is NOT checked - LEAVE IT ALONE
                else if (propertyExists && !standard.UseDefaultValue)
                {
                    shouldSetValue = false;
                    LogMessage($"Property '{standard.PropertyName}' exists and has value '{existingValue}'. Not changing (Use Default Value not checked) in {tabName} tab.");
                }
                // CASE 3: Property does NOT exist - ALWAYS ADD IT
                else if (!propertyExists)
                {
                    // Always add properties that don't exist, even if they're blank
                    shouldSetValue = true;
                    valueToAdd = formattedExpression;
                    LogMessage($"Adding new property '{standard.PropertyName}' with value '{(string.IsNullOrEmpty(valueToAdd) ? "[blank]" : valueToAdd)}' to {tabName} tab.");
                }

                if (shouldSetValue)
                {
                    try
                    {
                        int addResult = propMgr.Add3(
                            standard.PropertyName,
                            (int)swCustomInfoType_e.swCustomInfoText,
                            valueToAdd,
                            (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

                        if (addResult == 0)
                        {
                            LogMessage($"Set property '{standard.PropertyName}' to '{valueToAdd}' in {tabName} tab.");
                        }
                        else
                        {
                            LogMessage($"Failed to set property '{standard.PropertyName}' in {tabName} tab. Result code: {addResult}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Exception setting property '{standard.PropertyName}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error in ApplyOrUpdateProperty for {standard.PropertyName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Formats property expressions correctly based on property name and document type
        /// </summary>
        private string FormatPropertyExpression(string propertyName, string defaultExpr, swDocumentTypes_e docType)
        {
            // If defaultExpr is empty, return empty
            if (string.IsNullOrEmpty(defaultExpr))
                return defaultExpr;

            // Get the model to extract the filename if needed
            ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
            string filename = System.IO.Path.GetFileNameWithoutExtension(swModel.GetPathName());
            
            // Get the correct file extension based on document type
            string suffix;
            switch (docType)
            {
                case swDocumentTypes_e.swDocASSEMBLY:
                    // For assemblies, use the specific filename instead of wildcard
                    suffix = "@" + filename + ".SLDASM";
                    break;
                case swDocumentTypes_e.swDocDRAWING:
                    suffix = "@*.SLDDRW";
                    break;
                case swDocumentTypes_e.swDocPART:
                default:
                    suffix = "@*.SLDPRT";
                    break;
            }
            
            // Create the correct expressions for special properties WITH quotes
            // SolidWorks needs the quotes for the expressions to work
            switch (propertyName.ToLower())
            {
                case "weight":
                    if (defaultExpr.Contains("SW-Mass") || defaultExpr.Contains("$PRP:"))
                        return "\"SW-Mass" + suffix + "\"";
                    break;
                case "material":
                    if (defaultExpr.Contains("SW-Material") || defaultExpr.Contains("$PRPMODEL:"))
                        return "\"SW-Material" + suffix + "\"";
                    break;
                case "sheet metal thickness":
                case "thickness":
                    if (defaultExpr.Contains("Thickness") || defaultExpr.Contains("$PRPSHEET:"))
                        return "\"Thickness" + suffix + "\"";
                    break;
            }
            
            // For other expressions, ensure they have quotes if needed
            // If the expression already has quotes, use as is
            if (defaultExpr.StartsWith("\"") && defaultExpr.EndsWith("\""))
                return defaultExpr;
            
            if (defaultExpr.StartsWith("'") && defaultExpr.EndsWith("'"))
            {
                defaultExpr = defaultExpr.Substring(1, defaultExpr.Length - 2);
                return "\"" + defaultExpr + "\"";
            }
            
            // If it doesn't have quotes and seems to be an expression (contains @ or $),
            // add quotes around it
            if (defaultExpr.Contains("@") || defaultExpr.Contains("$"))
                return "\"" + defaultExpr + "\"";
                
            return defaultExpr;
        }

        /// <summary>
        /// Sets document display properties as per Roeslein standards
        /// </summary>
        private void SetDocumentDisplayProperties(ModelDoc2 swModel)
        {
            try
            {
                LogMessage("Setting document display properties...");
                
                // Set shaded and draft quality HLR/HLV resolution (use document method, not app-wide)
                int shadedDraftQualityInt = 25;
                swModel.SetTessellationQuality(shadedDraftQualityInt);
                LogMessage($"SetTessellationQuality (shaded/draft) to: {shadedDraftQualityInt}");

                // Set wireframe and high quality HLR/HLV resolution (use document extension method)
                int wireframeQualityInt = 35;
                swModel.Extension.SetUserPreferenceInteger(
                    (int)SolidWorks.Interop.swconst.swUserPreferenceIntegerValue_e.swImageQualityWireframeValue,
                    (int)SolidWorks.Interop.swconst.swUserPreferenceOption_e.swDetailingNoOptionSpecified,
                    wireframeQualityInt);
                LogMessage($"SetUserPreferenceInteger (wireframe/high quality) to: {wireframeQualityInt}");

                // (Optional) Remove or comment out the old swApp.SetUserPreferenceDoubleValue calls
                // swApp.SetUserPreferenceDoubleValue(73, shadedResolution);
                // swApp.SetUserPreferenceDoubleValue(72, wireframeResolution);

                // Enable "Use isometric, zoom to fit view for document preview"
                LogMessage("Enabling 'Use isometric, zoom to fit view for document preview'");
                // swDisplayNewDocIsometricView = 235
                swApp.SetUserPreferenceToggle(235, true);
                
                // Set the view to isometric and zoom to fit
                try
                {
                    LogMessage("Setting view to isometric and zoom to fit");
                    
                    // Set to isometric view - use the standard view enum
                    swModel.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
                    
                    // Zoom to fit
                    swModel.ViewZoomtofit2();
                    
                    LogMessage("View set to isometric and zoomed to fit");
                }
                catch (Exception viewEx)
                {
                    LogMessage($"Error setting view orientation: {viewEx.Message}. Continuing with other settings.");
                }
                
                // Save the document to persist the display settings
                try
                {
                    int saveErrors = 0;
                    int saveWarnings = 0;
                    bool saveResult = swModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings) as bool? ?? false;
                    
                    if (saveResult)
                    {
                        LogMessage("Document saved with new display properties");
                    }
                    else
                    {
                        LogMessage($"Failed to save document with new display properties. Errors: {saveErrors}, Warnings: {saveWarnings}");
                    }
                }
                catch (Exception saveEx)
                {
                    LogMessage($"Error saving document: {saveEx.Message}");
                }
                
                LogMessage("Document display properties set successfully");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting document display properties: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a file has an associated drawing in PDM
        /// </summary>
        /// <param name="filePath">Full path to the file to check</param>
        /// <returns>True if a drawing exists, false otherwise</returns>
        private bool HasAssociatedDrawing(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;
            
            LogMessage($"=== CHECKING FOR ASSOCIATED DRAWING: {filePath} ===");
            
            try
            {
                // Check by file naming convention - look for file with same name but .SLDDRW extension
                string fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(filePath);
                string directoryPath = System.IO.Path.GetDirectoryName(filePath);
                
                LogMessage($"Looking for drawing file with name: {fileNameWithoutExt}.SLDDRW");
                
                // Try with same name in the same folder
                string potentialDrawingPath = System.IO.Path.Combine(directoryPath, fileNameWithoutExt + ".SLDDRW");
                LogMessage($"Checking path: {potentialDrawingPath}");
                if (System.IO.File.Exists(potentialDrawingPath))
                {
                    LogMessage($"Found associated drawing at: {potentialDrawingPath}");
                    return true;
                }
                
                // Check if there's a drawing subfolder in the same directory
                string drawingsFolder = System.IO.Path.Combine(directoryPath, "Drawings");
                LogMessage($"Checking Drawings subfolder: {drawingsFolder}");
                if (System.IO.Directory.Exists(drawingsFolder))
                {
                    potentialDrawingPath = System.IO.Path.Combine(drawingsFolder, fileNameWithoutExt + ".SLDDRW");
                    LogMessage($"Checking path: {potentialDrawingPath}");
                    if (System.IO.File.Exists(potentialDrawingPath))
                    {
                        LogMessage($"Found associated drawing in Drawings subfolder: {potentialDrawingPath}");
                        return true;
                    }
                }
                
                // Check for drawing in parent folder (for PDM structures where drawings are in a parent folder)
                if (pdmVault != null && pdmVault.IsLoggedIn)
                {
                    try
                    {
                        // Go up one level to look for a Drawings folder
                        string parentDir = System.IO.Directory.GetParent(directoryPath)?.FullName;
                        if (!string.IsNullOrEmpty(parentDir))
                        {
                            string parentDrawingsFolder = System.IO.Path.Combine(parentDir, "Drawings");
                            LogMessage($"Checking parent Drawings folder: {parentDrawingsFolder}");
                            if (System.IO.Directory.Exists(parentDrawingsFolder))
                            {
                                potentialDrawingPath = System.IO.Path.Combine(parentDrawingsFolder, fileNameWithoutExt + ".SLDDRW");
                                LogMessage($"Checking path: {potentialDrawingPath}");
                                if (System.IO.File.Exists(potentialDrawingPath))
                                {
                                    LogMessage($"Found associated drawing in parent Drawings folder: {potentialDrawingPath}");
                                    return true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error checking parent folder for drawings: {ex.Message}");
                    }
                }
                
                // Check for common drawing naming conventions (DR-prefix)
                try
                {
                    // Some companies use "DR-" prefix for drawings
                    string drPrefixName = "DR-" + fileNameWithoutExt + ".SLDDRW";
                    LogMessage($"Checking for DR-prefix naming convention: {drPrefixName}");
                    
                    // Check in the same folder
                    potentialDrawingPath = System.IO.Path.Combine(directoryPath, drPrefixName);
                    LogMessage($"Checking path: {potentialDrawingPath}");
                    if (System.IO.File.Exists(potentialDrawingPath))
                    {
                        LogMessage($"Found associated drawing with DR prefix: {potentialDrawingPath}");
                        return true;
                    }
                    
                    // Check in Drawings subfolder
                    string drawingsFolderPath = System.IO.Path.Combine(directoryPath, "Drawings");
                    if (System.IO.Directory.Exists(drawingsFolderPath))
                    {
                        potentialDrawingPath = System.IO.Path.Combine(drawingsFolderPath, drPrefixName);
                        LogMessage($"Checking path: {potentialDrawingPath}");
                        if (System.IO.File.Exists(potentialDrawingPath))
                        {
                            LogMessage($"Found associated drawing with DR prefix in Drawings subfolder: {potentialDrawingPath}");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error checking for DR-prefix drawing naming convention: {ex.Message}");
                }
                
                // No associated drawing found through simple file path checks
                LogMessage($"*** NO ASSOCIATED DRAWING FOUND FOR: {filePath} ***");
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"Error in HasAssociatedDrawing: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies property standards to a drawing and its referenced model (part or assembly), with PDM logic.
        /// </summary>
        public void ProcessDrawingForStandards(ModelDoc2 drawingDoc, string requiredStandardsVersion, bool silentMode = false)
        {
            Settings.LoadSettings();
            var settings = Settings.LoadSettings();
            if (drawingDoc == null || drawingDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                LogMessage("ProcessDrawingForStandards: Not a drawing document.");
                if (!silentMode) MessageBox.Show("The active document is not a drawing.", "Standards Application", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LogMessage($"=== BEGIN DRAWING STANDARDS PROCESSING ===");
            LogMessage($"Drawing path: {drawingDoc.GetPathName()}");
            LogMessage($"Required standards version: {requiredStandardsVersion}");

            List<string> summaryMessages = new List<string>();
            List<string> errors = new List<string>();

            // 1. Apply standards to the drawing itself
            var drawingResult = ApplyStandardsToActiveDocument(drawingDoc, requiredStandardsVersion, true, settings);
            if (drawingResult.StandardsApplied)
                summaryMessages.Add($"Drawing: {System.IO.Path.GetFileName(drawingDoc.GetPathName())} updated.");
            else
                errors.Add($"Drawing: {System.IO.Path.GetFileName(drawingDoc.GetPathName())} not updated.");

            // 2. Find referenced model from first view
            try
            {
                DrawingDoc swDrawing = drawingDoc as DrawingDoc;
                object firstViewObj = swDrawing?.GetFirstView();
                SolidWorks.Interop.sldworks.View firstView = firstViewObj as SolidWorks.Interop.sldworks.View;
                // Skip the sheet view (first view is always the sheet itself)
                if (firstView != null) firstView = firstView.GetNextView() as SolidWorks.Interop.sldworks.View;
                if (firstView == null)
                {
                    errors.Add("No model views found in drawing.");
                }
                else
                {
                    string refModelPath = firstView.GetReferencedModelName();
                    if (string.IsNullOrEmpty(refModelPath) || !File.Exists(refModelPath))
                    {
                        errors.Add($"Referenced model not found: {refModelPath ?? "(null)"}");
                    }
                    else
                    {
                        LogMessage($"Referenced model path: {refModelPath}");
                        // Try to get the model from open docs, otherwise open it
                        ModelDoc2 refModel = GetDocumentByPath(refModelPath);
                        bool openedHere = false;
                        if (refModel == null)
                        {
                            int errorsOpen = 0, warningsOpen = 0;
                            int docType = (int)swDocumentTypes_e.swDocPART;
                            string ext = Path.GetExtension(refModelPath).ToLower();
                            if (ext == ".sldasm") docType = (int)swDocumentTypes_e.swDocASSEMBLY;
                            else if (ext == ".sldprt") docType = (int)swDocumentTypes_e.swDocPART;
                            refModel = swApp.OpenDoc6(refModelPath, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errorsOpen, ref warningsOpen);
                            openedHere = refModel != null;
                        }
                        if (refModel != null)
                        {
                            if (refModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                            {
                                // If the referenced model is an assembly, process the whole assembly tree
                                ProcessAssemblyForStandards(refModel, requiredStandardsVersion, true);
                                summaryMessages.Add($"Assembly: {System.IO.Path.GetFileName(refModelPath)} processed (all children checked).");
                            }
                            else
                            {
                                var modelResult = ApplyStandardsToActiveDocument(refModel, requiredStandardsVersion, true, settings);
                                if (modelResult.StandardsApplied)
                                    summaryMessages.Add($"Model: {System.IO.Path.GetFileName(refModelPath)} updated.");
                                else
                                    errors.Add($"Model: {System.IO.Path.GetFileName(refModelPath)} not updated.");
                            }
                            // Optionally close the model if we opened it
                            // if (openedHere) swApp.CloseDoc(refModel.GetTitle());
                        }
                        else
                        {
                            errors.Add($"Failed to open referenced model: {refModelPath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error processing referenced model: {ex.Message}");
                LogMessage($"Error processing referenced model: {ex.Message}");
            }

            // 3. Show summary
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Drawing Standards Application Complete:");
            foreach (var msg in summaryMessages) summary.AppendLine("  " + msg);
            if (errors.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Errors:");
                foreach (var err in errors) summary.AppendLine("  " + err);
            }
            if (!silentMode)
            {
                new StandardsSummaryForm(summary.ToString(), "Standards Application").ShowDialog();
            }
        }
    }

    /// <summary>
    /// Helper class for PDM file operations that can be reused across the add-in
    /// </summary>
    public class PDMFileManager
    {
        private readonly ISldWorks swApp;
        private readonly EPDM.Interop.epdm.IEdmVault5 pdmVault;
        private readonly string logFilePath;
        
        // Define the enum values we need
        private const int SW_RELOAD_REPLACE_SUCCESS = 0; // swReloadReplaceStatus_Success
        private const int SW_RELOAD_REPLACE_FAILED = 1;  // swReloadReplaceStatus_Failed

        public PDMFileManager(EPDM.Interop.epdm.IEdmVault5 vault, ISldWorks application, string logFilePath = null)
        {
            swApp = application;
            pdmVault = vault;
            this.logFilePath = logFilePath;
            LogMessage("PDMFileManager initialized" + (pdmVault != null ? " with PDM Vault connection." : " without PDM Vault connection."));
        }

        /// <summary>
        /// Logs a message to debug output and file if enabled
        /// </summary>
        private void LogMessage(string message)
        {
            Logger.Log($"[PDMFileManager] {message}");
        }

        /// <summary>
        /// Get the SolidWorks main window handle for PDM operations
        /// </summary>
        private int GetSolidWorksMainWindowHandle()
        {
            try
            {
                if (swApp != null)
                {
                    IFrame swFrame = swApp.Frame() as IFrame;
                    if (swFrame != null)
                    {
                        return swFrame.GetHWnd();
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error getting SW window handle: {ex.Message}");
            }
            LogMessage("Could not get SolidWorks main window handle, returning 0.");
            return 0;
        }

        /// <summary>
        /// Check out a file that is currently open in SolidWorks
        /// </summary>
        /// <param name="swModel">The SolidWorks model to check out</param>
        /// <param name="silentMode">If true, suppresses UI messages</param>
        /// <returns>True if checkout was successful</returns>
        public bool CheckOutOpenFile(ModelDoc2 swModel, bool silentMode = false)
        {
            if (swModel == null)
            {
                LogMessage("CheckOutOpenFile: No active SolidWorks model provided.");
                if (!silentMode) MessageBox.Show("No active document.", "PDM Checkout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string filePath = swModel.GetPathName();
            if (string.IsNullOrEmpty(filePath))
            {
                LogMessage("CheckOutOpenFile: File has not been saved yet. Cannot perform PDM operations.");
                if (!silentMode) MessageBox.Show("The file must be saved before checking out from PDM.", "PDM Checkout", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (pdmVault == null || !pdmVault.IsLoggedIn)
            {
                LogMessage("CheckOutOpenFile: PDM vault not connected or not logged in.");
                if (!silentMode) MessageBox.Show("PDM vault not connected or not logged in.", "PDM Checkout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            LogMessage($"CheckOutOpenFile: Starting checkout process for '{filePath}'");

            try
            {
                // Get the file from the vault
                EPDM.Interop.epdm.IEdmFolder5 parentFolder;
                EPDM.Interop.epdm.IEdmFile5 edmFile = pdmVault.GetFileFromPath(filePath, out parentFolder);
                if (edmFile == null || parentFolder == null)
                {
                    LogMessage($"CheckOutOpenFile: File '{filePath}' not found in the current PDM vault view.");
                    if (!silentMode) MessageBox.Show($"File '{Path.GetFileName(filePath)}' not found in the PDM vault.", "PDM Checkout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Check if file is already checked out
                if (edmFile.IsLocked)
                {
                    bool isLockedByCurrentUser = !swModel.IsOpenedReadOnly();
                    if (isLockedByCurrentUser)
                    {
                        LogMessage($"CheckOutOpenFile: File '{filePath}' is already checked out by current user.");
                        // Only show message if explicitly checking out (not as part of standards application)
                        return true; // Return success but don't show message here
                    }
                    else
                    {
                        LogMessage($"CheckOutOpenFile: File '{filePath}' is checked out by another user.");
                        if (!silentMode) MessageBox.Show($"File '{Path.GetFileName(filePath)}' is checked out by another user.", "PDM Checkout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }

                // Prepare to check out - get main window handle for PDM dialogs
                int swWindowHandle = GetSolidWorksMainWindowHandle();

                // 1. Force SolidWorks to release its file locks
                LogMessage($"CheckOutOpenFile: Calling ForceReleaseLocks on '{filePath}'");
                if (swModel.ForceReleaseLocks() != 0)
                {
                    // 2. Perform PDM checkout
                    LogMessage($"CheckOutOpenFile: ForceReleaseLocks successful. Now checking out file.");
                    edmFile.LockFile(parentFolder.ID, swWindowHandle, (int)EdmLockFlag.EdmLock_Simple);
                    LogMessage($"CheckOutOpenFile: File '{filePath}' checked out successfully.");

                    // 3. Reload the document to update its status
                    LogMessage($"CheckOutOpenFile: Reloading document after checkout");
                    int reloadResult = swModel.ReloadOrReplace(false, filePath, true);
                    
                    if (reloadResult == SW_RELOAD_REPLACE_SUCCESS)
                    {
                        LogMessage($"CheckOutOpenFile: Document reload successful");
                        // Don't show individual success message here - let calling code handle that
                        return true;
                    }
                    else
                    {
                        LogMessage($"CheckOutOpenFile: WARNING - Document reload failed with status {reloadResult}");
                        // Still return true since checkout was successful even if reload had issues
                        if (!silentMode) MessageBox.Show($"File '{Path.GetFileName(filePath)}' was checked out, but there was an issue reloading the file. You may need to close and reopen it.", "PDM Checkout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return true;
                    }
                }
                else
                {
                    LogMessage($"CheckOutOpenFile: ForceReleaseLocks failed. Cannot proceed with checkout.");
                    if (!silentMode) MessageBox.Show($"Could not release SolidWorks locks on file '{Path.GetFileName(filePath)}'. Checkout aborted.", "PDM Checkout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                LogMessage($"CheckOutOpenFile: COM Exception during checkout: HRESULT=0x{comEx.ErrorCode:X}, Msg={comEx.Message}");
                if (!silentMode) MessageBox.Show($"Error checking out file: {comEx.Message}", "PDM Checkout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"CheckOutOpenFile: Exception during checkout: {ex.Message}");
                if (!silentMode) MessageBox.Show($"Error checking out file: {ex.Message}", "PDM Checkout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Check out a component file that's open within an assembly
        /// </summary>
        /// <param name="componentDoc">The component's ModelDoc2</param>
        /// <param name="silentMode">If true, suppresses UI messages</param>
        /// <returns>True if checkout was successful</returns>
        public bool CheckOutComponent(ModelDoc2 componentDoc, bool silentMode = false)
        {
            if (componentDoc == null)
            {
                LogMessage("CheckOutComponent: No component document provided.");
                return false;
            }

            return CheckOutOpenFile(componentDoc, silentMode);
        }

        /// <summary>
        /// Check in a file that is currently open in SolidWorks
        /// </summary>
        /// <param name="swModel">The SolidWorks model to check in</param>
        /// <param name="comment">The check-in comment</param>
        /// <param name="silentMode">If true, suppresses UI messages</param>
        /// <returns>True if checkin was successful</returns>
        public bool CheckInOpenFile(ModelDoc2 swModel, string comment, bool silentMode = false)
        {
            if (swModel == null)
            {
                LogMessage("CheckInOpenFile: No active SolidWorks model provided.");
                if (!silentMode) MessageBox.Show("No active document.", "PDM Check-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string filePath = swModel.GetPathName();
            if (string.IsNullOrEmpty(filePath))
            {
                LogMessage("CheckInOpenFile: File has not been saved yet. Cannot perform PDM operations.");
                if (!silentMode) MessageBox.Show("The file must be saved before checking in to PDM.", "PDM Check-in", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (pdmVault == null || !pdmVault.IsLoggedIn)
            {
                LogMessage("CheckInOpenFile: PDM vault not connected or not logged in.");
                if (!silentMode) MessageBox.Show("PDM vault not connected or not logged in.", "PDM Check-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            LogMessage($"CheckInOpenFile: Starting check-in process for '{filePath}'");

            try
            {
                // Get the file from the vault
                EPDM.Interop.epdm.IEdmFolder5 parentFolder;
                EPDM.Interop.epdm.IEdmFile5 edmFile = pdmVault.GetFileFromPath(filePath, out parentFolder);
                if (edmFile == null || parentFolder == null)
                {
                    LogMessage($"CheckInOpenFile: File '{filePath}' not found in the current PDM vault view.");
                    if (!silentMode) MessageBox.Show($"File '{Path.GetFileName(filePath)}' not found in the PDM vault.", "PDM Check-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Check if file is checked out
                if (!edmFile.IsLocked)
                {
                    LogMessage($"CheckInOpenFile: File '{filePath}' is not checked out.");
                    if (!silentMode) MessageBox.Show($"File '{Path.GetFileName(filePath)}' is not checked out.", "PDM Check-in", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                // Save the file before checking in
                LogMessage($"CheckInOpenFile: Saving file before check-in");
                int saveErrors = 0;
                int saveWarnings = 0;
                if (!(swModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings) as bool? ?? false))
                {
                    LogMessage($"CheckInOpenFile: Failed to save file. Errors: {saveErrors}, Warnings: {saveWarnings}");
                    if (!silentMode) MessageBox.Show($"Failed to save file before check-in. Please save the file manually and try again.", "PDM Check-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Prepare to check in - get main window handle for PDM dialogs
                int swWindowHandle = GetSolidWorksMainWindowHandle();

                // 1. Force SolidWorks to release its file locks
                LogMessage($"CheckInOpenFile: Calling ForceReleaseLocks on '{filePath}'");
                if (swModel.ForceReleaseLocks() != 0)
                {
                    // 2. Perform PDM check-in
                    LogMessage($"CheckInOpenFile: ForceReleaseLocks successful. Now checking in file with comment: '{comment}'");
                    edmFile.UnlockFile(swWindowHandle, comment);
                    LogMessage($"CheckInOpenFile: File '{filePath}' checked in successfully.");

                    // 3. Reload the document to update its status
                    LogMessage($"CheckInOpenFile: Reloading document after check-in");
                    int reloadResult = swModel.ReloadOrReplace(false, filePath, true);
                    
                    if (reloadResult == SW_RELOAD_REPLACE_SUCCESS)
                    {
                        LogMessage($"CheckInOpenFile: Document reload successful");
                        if (!silentMode) MessageBox.Show($"File '{Path.GetFileName(filePath)}' checked in successfully.", "PDM Check-in", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return true;
                    }
                    else
                    {
                        LogMessage($"CheckInOpenFile: WARNING - Document reload failed with status {reloadResult}");
                        // Still return true since check-in was successful even if reload had issues
                        if (!silentMode) MessageBox.Show($"File '{Path.GetFileName(filePath)}' was checked in, but there was an issue reloading the file. You may need to close and reopen it.", "PDM Check-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return true;
                    }
                }
                else
                {
                    LogMessage($"CheckInOpenFile: ForceReleaseLocks failed. Cannot proceed with check-in.");
                    if (!silentMode) MessageBox.Show($"Could not release SolidWorks locks on file '{Path.GetFileName(filePath)}'. Check-in aborted.", "PDM Check-in", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                LogMessage($"CheckInOpenFile: COM Exception during check-in: HRESULT=0x{comEx.ErrorCode:X}, Msg={comEx.Message}");
                if (!silentMode) MessageBox.Show($"Error checking in file: {comEx.Message}", "PDM Check-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"CheckInOpenFile: Exception during check-in: {ex.Message}");
                if (!silentMode) MessageBox.Show($"Error checking in file: {ex.Message}", "PDM Check-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }

    /// <summary>
    /// Result object for standards application
    /// </summary>
    public class StandardsApplicationResult
    {
        public bool StandardsApplied { get; set; }
        public bool CheckedOutByThisProcess { get; set; }
    }
} 