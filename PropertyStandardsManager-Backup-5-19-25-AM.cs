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
    }

    /// <summary>
    /// Manages applying property standards to SolidWorks files, including PDM checkout.
    /// </summary>
    public class PropertyStandardManager
    {
        private ISldWorks swApp;
        private EPDM.Interop.epdm.IEdmVault5 pdmVault;
        private string logFilePath;

        public PropertyStandardManager(EPDM.Interop.epdm.IEdmVault5 vault, ISldWorks application, string debugLogFilePath = null)
        {
            swApp = application;
            pdmVault = vault;
            
            logFilePath = debugLogFilePath;
            LogMessage("PropertyStandardManager initialized" + (pdmVault != null ? " with PDM Vault connection." : " without PDM Vault connection."));
        }
        
        // Add logging functionality
        private void LogMessage(string message)
        {
            try
            {
                // First output to debug for immediate developer visibility
                Debug.WriteLine($"[PropertyStandardManager] {message}");
                
                // Then use the proper Logger class to ensure logging settings are respected
                Logger.DebugLog(message);
                
                // Additional logging to custom log file if path was specifically provided
                if (!string.IsNullOrEmpty(logFilePath))
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                    File.AppendAllText(logFilePath, logEntry + System.Environment.NewLine);
                }
            }
            catch
            {
                // Silent fail for logging
            }
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
        /// <returns>True if standards were successfully applied (or file was already compliant/not in PDM), false otherwise.</returns>
        public bool ApplyStandardsToActiveDocument(ModelDoc2 swModel, string currentStandardsVersion, bool silentMode = false)
        {
            if (swModel == null)
            {
                LogMessage("ApplyStandardsToActiveDocument: No active SolidWorks model provided.");
                if (!silentMode) MessageBox.Show("No active document.", "Property Standards", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string filePath = swModel.GetPathName();
            if (string.IsNullOrEmpty(filePath))
            {
                LogMessage("ApplyStandardsToActiveDocument: File has not been saved yet. Cannot perform PDM operations or apply standards that require a saved file.");
                if (!silentMode) MessageBox.Show("The file must be saved before applying property standards.", "Property Standards", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
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
                                // Use simpler approach to determine if file is locked by current user
                                bool isLockedByCurrentUser = false;
                                
                                // First check if this file is already open in SolidWorks
                                // If it's open and not read-only, we have it checked out
                                isLockedByCurrentUser = !swModel.IsOpenedReadOnly();
                                LogMessage($"PDM: File is locked. Appears to be locked by current user (based on read-only status): {isLockedByCurrentUser}");
                                
                                if (isLockedByCurrentUser)
                                {
                                    LogMessage($"PDM: File appears to be checked out by the current user. Proceeding with write access.");
                                    canWriteToFile = true;
                                }
                                else
                                {
                                    LogMessage($"PDM: File is checked out by another user. Cannot apply standards.");
                                    if (!silentMode) MessageBox.Show($@"File '{Path.GetFileName(filePath)}' is checked out by another user.
Cannot apply standards.", "PDM Checkout Conflict", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                    return false;
                                }
                            }
                            else
                            {
                                LogMessage("PDM: File is not checked out. Attempting to check out...");
                                int swWindowHandle = GetSolidWorksMainWindowHandle();
                                try
                                {
                                    // Using the standard EPDM API checkout method
                                    // First, close the document in SolidWorks to avoid sharing violations
                                    bool wasOpen = false;
                                    string docPath = swModel.GetPathName();
                                    
                                    // Check if this is the file we're trying to check out
                                    if (string.Equals(docPath, filePath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        wasOpen = true;
                                        LogMessage("PDM: Closing document to avoid sharing violation during checkout");
                                        swApp.CloseDoc(docPath); // Use CloseDoc instead of model.Close()
                                    }
                                    
                                    // Perform the checkout operation
                                    edmFile.LockFile(parentFolder.ID, swWindowHandle, (int)EdmLockFlag.EdmLock_Simple);
                                    wasCheckedOutByThisProcess = true;
                                    canWriteToFile = true;
                                    
                                    // Reopen the file if it was previously open
                                    if (wasOpen)
                                    {
                                        LogMessage("PDM: Reopening document after checkout");
                                        int docType = (int)swDocumentTypes_e.swDocPART; // Default to part
                                        
                                        // Try to determine document type from extension
                                        string ext = Path.GetExtension(filePath).ToLower();
                                        if (ext == ".sldasm") docType = (int)swDocumentTypes_e.swDocASSEMBLY;
                                        else if (ext == ".slddrw") docType = (int)swDocumentTypes_e.swDocDRAWING;
                                        
                                        int errors = 0;
                                        int warnings = 0;
                                        swModel = swApp.OpenDoc6(filePath, docType, 
                                                     (int)swOpenDocOptions_e.swOpenDocOptions_Silent, 
                                                     "", ref errors, ref warnings);
                                        
                                        if (swModel == null)
                                        {
                                            LogMessage($"PDM: Failed to reopen document after checkout. Errors: {errors}, Warnings: {warnings}");
                                            if (!silentMode) MessageBox.Show("File was checked out but could not be reopened.", 
                                                           "Document Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                            return false;
                                        }
                                    }
                                    
                                    LogMessage("PDM: File checked out successfully.");
                                    if (!silentMode) MessageBox.Show($"File '{Path.GetFileName(filePath)}' checked out successfully.", "PDM Checkout", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                catch (System.Runtime.InteropServices.COMException comEx)
                                {
                                    LogMessage($"PDM: Checkout FAILED. COMException: HRESULT=0x{comEx.ErrorCode:X}, Msg={comEx.Message}");
                                    if (!silentMode) MessageBox.Show($@"Failed to check out file '{Path.GetFileName(filePath)}' from PDM.

Error: {comEx.Message}

Please ensure you have permissions and the file is not locked by another process.", "PDM Checkout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return false;
                                }
                                catch (Exception ex)
                                {
                                    LogMessage($"PDM: Checkout FAILED. Exception: {ex.Message}");
                                    if (!silentMode) MessageBox.Show($@"Failed to check out file '{Path.GetFileName(filePath)}' from PDM.

Error: {ex.Message}", "PDM Checkout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            LogMessage("PDM: 'Roeslein Standards Version' property is up-to-date. No checkout needed for version update.");
                            if (edmFile.IsLocked)
                            {
                                // Simplify this approach as well - just check if SolidWorks shows the file as read-only
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

            // Get the current property value before doing the check - using a different name to avoid conflict
            string currentPropertyVersion = "";
            try
            {
                // Using Get6 (consistent with other parts of code) instead of Get to avoid bool-to-string conversion issues
                string tempVal = "";
                string tempResolvedVal = "";
                bool tempWasResolved = false;
                bool tempLinkToProperty = false;
                swModel.Extension.CustomPropertyManager[""].Get6("Roeslein Standards Version", false, 
                    out tempVal, out tempResolvedVal, out tempWasResolved, out tempLinkToProperty);
                currentPropertyVersion = tempResolvedVal;
                LogMessage($"Retrieved 'Roeslein Standards Version' property: '{currentPropertyVersion}'");
            }
            catch
            {
                // If any error occurs, assume the property doesn't exist
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
                return false;
            }

            bool standardsAppliedSuccessfully = false;
            if (canWriteToFile)
            {
                try
                {
                    LogMessage("=== STARTING PROPERTY APPLICATION ===");
                    
                    // Create a backup version of the filename for saving
                    string backupFilePath = filePath + ".backup";
                    bool backupCreated = false;
                    
                    try
                    {
                        // Save a backup copy of the file in case something goes wrong
                        File.Copy(filePath, backupFilePath, true);
                        backupCreated = true;
                        LogMessage($"Created backup at {backupFilePath}");
                    }
                    catch (Exception backupEx)
                    {
                        LogMessage($"WARNING: Could not create backup: {backupEx.Message}");
                    }
                    
                    // Use the SolidWorks API to get property manager
                    LogMessage("Getting CustomPropertyManager...");
                    CustomPropertyManager swCustPropMgr = swModel.Extension.CustomPropertyManager[""];
                    
                    if (swCustPropMgr == null)
                    {
                        LogMessage("ERROR: CustomPropertyManager is null!");
                        standardsAppliedSuccessfully = false;
                    }
                    else
                    {
                        // DIRECT: Set the property using BOTH available methods to ensure it works
                        LogMessage($"Setting property 'Roeslein Standards Version' to '{currentStandardsVersion}'");
                        
                        // First try Add3 method
                        int addPropResult = swCustPropMgr.Add3("Roeslein Standards Version",
                                               (int)swCustomInfoType_e.swCustomInfoText,
                                               currentStandardsVersion,
                                               (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                        
                        LogMessage($"Add3 result: {addPropResult}");
                        
                        // For insurance, also try Set2 method
                        int setPropResult = swCustPropMgr.Set2("Roeslein Standards Version", currentStandardsVersion);
                        LogMessage($"Set2 result: {setPropResult}");
                        
                        // Verify the property was set
                        string valOut = "";
                        string resolvedValOut = "";
                        bool wasResolved = false;
                        bool linkToProperty = false;
                        
                        swCustPropMgr.Get6("Roeslein Standards Version", false, out valOut, out resolvedValOut, out wasResolved, out linkToProperty);
                        LogMessage($"Property verification - Value: '{valOut}', Resolved: '{resolvedValOut}'");
                        
                        if (resolvedValOut == currentStandardsVersion)
                        {
                            LogMessage("Property set successfully!");
                            standardsAppliedSuccessfully = true;
                        }
                        else 
                        {
                            LogMessage($"FAILED to set property correctly. Expected '{currentStandardsVersion}' but got '{resolvedValOut}'");
                            standardsAppliedSuccessfully = false;
                        }
                    }
                    
                    // Force save the document with all changes
                    if (standardsAppliedSuccessfully)
                    {
                        LogMessage("Forcing save...");
                        int saveErrors = 0;
                        int saveWarnings = 0;
                        
                        // Ensure we're on the right document
                        swApp.ActivateDoc3(filePath, false, 0, ref saveErrors);
                        
                        // Explicitly force the save
                        bool saveResult = swModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, 
                                         ref saveErrors, ref saveWarnings) as bool? ?? false;
                        
                        LogMessage($"Save result: {saveResult}, Errors: {saveErrors}, Warnings: {saveWarnings}");
                        
                        if (!saveResult || saveErrors != 0)
                        {
                            standardsAppliedSuccessfully = false;
                            LogMessage("Save failed - standards will not be considered applied");
                        }
                    }
                    
                    // Cleanup backup if successful
                    if (standardsAppliedSuccessfully && backupCreated)
                    {
                        try
                        {
                            File.Delete(backupFilePath);
                            LogMessage("Backup file deleted");
                        }
                        catch
                        {
                            LogMessage("Note: Could not delete backup file");
                        }
                    }
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
                // Safe way to get property value
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
                    // If property can't be accessed, assume it doesn't exist
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

            if (wasCheckedOutByThisProcess && edmFile != null && parentFolder != null && pdmVault != null && pdmVault.IsLoggedIn)
            {
                // Declare the file path variable at this scope level so it's available in catch blocks
                string filePathToCheckIn = swModel.GetPathName();
                string fileName = Path.GetFileName(filePathToCheckIn);
                
                try
                {
                    string comment = standardsAppliedSuccessfully ? $"Applied Roeslein Standards Version: {currentStandardsVersion}." : "Attempted to apply Roeslein Standards; see logs for details.";
                    
                    // Final verification to make sure properties were saved
                    LogMessage("=== FINAL VERIFICATION BEFORE CHECK-IN ===");
                    bool propertiesVerified = false;
                    
                    try
                    {
                        // Get the final property value to verify it worked
                        CustomPropertyManager swCustPropMgr = swModel.Extension.CustomPropertyManager[""];
                        string valOut = "";
                        string resolvedValOut = "";
                        bool wasResolved = false;
                        bool linkToProperty = false;
                        
                        swCustPropMgr.Get6("Roeslein Standards Version", false, out valOut, out resolvedValOut, out wasResolved, out linkToProperty);
                        LogMessage($"Final property check - Value: '{valOut}', Resolved: '{resolvedValOut}'");
                        
                        propertiesVerified = (resolvedValOut == currentStandardsVersion);
                        
                        // If not right, try one more time to set the property
                        if (!propertiesVerified)
                        {
                            LogMessage("WARNING: Property not set correctly - trying one more time");
                            int setPropResult = swCustPropMgr.Set2("Roeslein Standards Version", currentStandardsVersion);
                            LogMessage($"Final Set2 attempt result: {setPropResult}");
                            
                            // Check again
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
                        
                        // Force one final save to be sure
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
                    
                    // CRITICAL: Before checking in, ensure the file is saved and released
                    LogMessage("=== STARTING PDM CHECK-IN PROCESS ===");
                    LogMessage("Preparing file for check-in by saving and closing...");
                    
                    // Get the file path before closing
                    LogMessage($"File to check in: '{filePathToCheckIn}'");
                    
                    // Close the document to release its handles 
                    LogMessage("Closing document to release file handles for check-in...");
                    swApp.CloseDoc(filePathToCheckIn);
                    
                    // Give Windows a moment to fully release file handles
                    LogMessage("Waiting for file handles to be released...");
                    System.Threading.Thread.Sleep(1000);

                    int swWindowHandle = GetSolidWorksMainWindowHandle();
                    LogMessage($"Checking in file '{fileName}' with comment: '{comment}'");
                    edmFile.UnlockFile(swWindowHandle, comment);
                    LogMessage($"PDM: File '{fileName}' checked back in successfully");
                    
                    // Reopen the document after check-in
                    LogMessage("Reopening document after check-in...");
                    int docType = (int)swDocumentTypes_e.swDocPART;
                    string ext = Path.GetExtension(filePathToCheckIn).ToLower();
                    if (ext == ".sldasm") docType = (int)swDocumentTypes_e.swDocASSEMBLY;
                    else if (ext == ".slddrw") docType = (int)swDocumentTypes_e.swDocDRAWING;
                    
                    int errors = 0;
                    int warnings = 0;
                    ModelDoc2 reopenedModel = swApp.OpenDoc6(filePathToCheckIn, docType, 
                                         (int)swOpenDocOptions_e.swOpenDocOptions_Silent, 
                                         "", ref errors, ref warnings);
                    
                    if (reopenedModel == null)
                    {
                        LogMessage($"Warning: Could not reopen document after check-in. Errors: {errors}");
                        if (!silentMode) MessageBox.Show($"File was successfully checked in, but could not be reopened automatically.",
                                       "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        // Verify if the properties persisted
                        try 
                        {
                            CustomPropertyManager finalPropMgr = reopenedModel.Extension.CustomPropertyManager[""];
                            string finalVal = "";
                            string finalResolvedVal = "";
                            bool finalResolved = false;
                            bool finalLink = false;
                            
                            finalPropMgr.Get6("Roeslein Standards Version", false, out finalVal, out finalResolvedVal, out finalResolved, out finalLink);
                            LogMessage($"Post-reopen property check - Value: '{finalVal}', Resolved: '{finalResolvedVal}'");
                            
                            if (finalResolvedVal == currentStandardsVersion)
                            {
                                LogMessage("SUCCESS! Property persisted through PDM check-in/check-out!");
                            }
                            else
                            {
                                LogMessage("WARNING: Property did not persist after PDM check-in/check-out.");
                            }
                        }
                        catch (Exception propEx)
                        {
                            LogMessage($"Error checking properties after reopen: {propEx.Message}");
                        }
                    }
                    
                    LogMessage("=== PDM CHECK-IN PROCESS COMPLETED ===");
                    
                    if (propertiesVerified) 
                    {
                        if (!silentMode) MessageBox.Show($"File '{fileName}' successfully checked in with Roeslein Standards Version {currentStandardsVersion}.", 
                                       "PDM Check-in Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        if (!silentMode) MessageBox.Show($"File '{fileName}' was checked in, but there may have been issues setting the Roeslein Standards Version property.",
                                       "PDM Check-in Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (System.Runtime.InteropServices.COMException comEx)
                {
                    LogMessage($"PDM: Failed to check file back in. COMException: HRESULT=0x{comEx.ErrorCode:X}, Msg={comEx.Message}");
                    if (!silentMode) MessageBox.Show($"Failed to check file '{fileName}' back into PDM: {comEx.Message}", "PDM Check-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    LogMessage($"PDM: Failed to check file back in: {ex.Message}");
                    if (!silentMode) MessageBox.Show($"Failed to check file '{fileName}' back into PDM: {ex.Message}", "PDM Check-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            
            if (standardsAppliedSuccessfully)
            {
                LogMessage($"ApplyStandardsToActiveDocument completed successfully for '{filePath}'.");
            }
            else
            {
                LogMessage($"ApplyStandardsToActiveDocument failed or partially completed for '{filePath}'.");
            }
            return standardsAppliedSuccessfully;
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
                string tempVal = "";
                string tempResolvedVal = "";
                bool tempWasResolved = false;
                bool tempLinkToProperty = false;
                
                propMgr.Get6("Roeslein Standards Version", false, 
                    out tempVal, out tempResolvedVal, out tempWasResolved, out tempLinkToProperty);
                
                return tempResolvedVal;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Process an assembly file and apply standards to all its components
        /// </summary>
        public void ProcessAssemblyForStandards(ModelDoc2 assemblyDoc, string requiredStandardsVersion, bool silentMode = false)
        {
            if (assemblyDoc == null || assemblyDoc.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                LogMessage("ProcessAssemblyForStandards: Not an assembly document.");
                if (!silentMode) MessageBox.Show("The active document is not an assembly.", "Standards Application", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LogMessage($"Beginning standards batch processing for assembly: {assemblyDoc.GetPathName()}");
            
            // Step 1: Scan assembly and collect all files needing standards
            List<FileCheckoutInfo> filesToCheckout = new List<FileCheckoutInfo>();
            
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
                
                // Get the root component
                Component2 rootComp = activeConfig.GetRootComponent3(true);
                if (rootComp == null)
                {
                    LogMessage("Unable to get root component from assembly.");
                    return;
                }
                
                // Get all components - need to cast to object[]
                object[] allComponents = (object[])rootComp.GetChildren();
                if (allComponents == null || allComponents.Length == 0)
                {
                    LogMessage("No components found in assembly.");
                    if (!silentMode) MessageBox.Show("No components found in this assembly.", "Standards Application", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                LogMessage($"Found {allComponents.Length} components in assembly.");
                
                // Process each component
                foreach (object obj in allComponents)
                {
                    Component2 comp = obj as Component2;
                    if (comp == null) continue;
                    
                    // Skip virtual components - IsVirtual is a property, not a method
                    if (comp.IsVirtual) continue;
                    
                    // Need to cast to ModelDoc2
                    ModelDoc2 compDoc = comp.GetModelDoc2() as ModelDoc2;
                    if (compDoc == null) continue;
                    
                    // We only care about part files
                    if (compDoc.GetType() != (int)swDocumentTypes_e.swDocPART) continue;
                    
                    string filePath = compDoc.GetPathName();
                    LogMessage($"Examining component: {filePath}");
                    
                    // Skip if file path is empty
                    if (string.IsNullOrEmpty(filePath)) continue;
                    
                    if (pdmVault == null || !pdmVault.IsLoggedIn)
                    {
                        LogMessage("PDM vault not connected or not logged in. Skipping PDM operations.");
                        continue;
                    }
                    
                    // Skip if not in PDM vault
                    string vaultName = pdmVault.GetVaultNameFromPath(filePath);
                    if (string.IsNullOrEmpty(vaultName))
                    {
                        LogMessage($"File not in PDM vault: {filePath}");
                        continue;
                    }
                    
                    // Get PDM file
                    EPDM.Interop.epdm.IEdmFile5 edmFile = pdmVault.GetFileFromPath(filePath, out EPDM.Interop.epdm.IEdmFolder5 folder);
                    if (edmFile == null || folder == null)
                    {
                        LogMessage($"Could not get PDM file or folder for: {filePath}");
                        continue;
                    }
                    
                    // Check if standards need to be applied
                    string currentVersion = GetCurrentStandardsVersion(compDoc);
                    bool needsStandards = string.IsNullOrEmpty(currentVersion) || 
                                         currentVersion != requiredStandardsVersion;
                    
                    if (!needsStandards)
                    {
                        LogMessage($"Standards already up to date for: {filePath}");
                        continue;
                    }
                    
                    // Check if locked by someone else
                    bool isLockedByOther = edmFile.IsLocked && !IsLockedByCurrentUser(edmFile);
                    
                    if (isLockedByOther)
                    {
                        LogMessage($"File is locked by another user: {filePath}");
                        continue;
                    }
                    
                    // Add to checkout list
                    filesToCheckout.Add(new FileCheckoutInfo 
                    { 
                        File = edmFile, 
                        Folder = folder, 
                        Path = filePath,
                        IsAlreadyCheckedOut = edmFile.IsLocked && IsLockedByCurrentUser(edmFile),
                        Document = compDoc
                    });
                    
                    LogMessage($"Added to checkout list: {filePath}, Already checked out: {(edmFile.IsLocked && IsLockedByCurrentUser(edmFile))}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error scanning assembly components: {ex.Message}\n{ex.StackTrace}");
                if (!silentMode) MessageBox.Show($"Error scanning assembly components: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            // Step 2: Show dialog with list of files to check out
            if (filesToCheckout.Count > 0)
            {
                StringBuilder message = new StringBuilder();
                message.AppendLine($"The following {filesToCheckout.Count} file(s) need to be checked out to apply Roeslein standards v{requiredStandardsVersion}:");
                message.AppendLine();
                
                foreach (var fileInfo in filesToCheckout)
                {
                    string status = fileInfo.IsAlreadyCheckedOut ? " (already checked out)" : "";
                    message.AppendLine($"• {fileInfo.FileName}{status}");
                }
                
                message.AppendLine();
                message.AppendLine("Proceed with checkout and apply standards?");
                
                DialogResult result = MessageBox.Show(
                    message.ToString(),
                    "Batch Checkout Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                    
                if (result == DialogResult.Yes)
                {
                    // Step 3: Check out all files
                    List<FileCheckoutInfo> filesToProcess = new List<FileCheckoutInfo>();
                    
                    foreach (var fileInfo in filesToCheckout)
                    {
                        try
                        {
                            if (!fileInfo.IsAlreadyCheckedOut)
                            {
                                LogMessage($"Checking out: {fileInfo.Path}");
                                fileInfo.File.LockFile(fileInfo.Folder.ID, 0, 
                                    (int)EdmLockFlag.EdmLock_Simple);
                                fileInfo.IsAlreadyCheckedOut = true;
                            }
                            
                            filesToProcess.Add(fileInfo);
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Failed to check out {fileInfo.Path}: {ex.Message}");
                            if (!silentMode) MessageBox.Show($"Failed to check out file: {fileInfo.FileName}\n\nError: {ex.Message}", 
                                "Checkout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    
                    // Step 4: Apply standards to each file
                    int successCount = 0;
                    foreach (var fileInfo in filesToProcess)
                    {
                        try
                        {
                            LogMessage($"Applying standards to: {fileInfo.Path}");
                            
                            bool success = ApplyStandardsToActiveDocument(fileInfo.Document, requiredStandardsVersion, true);
                            fileInfo.StandardsApplied = success;
                            
                            if (success)
                            {
                                successCount++;
                                LogMessage($"Successfully applied standards to: {fileInfo.Path}");
                            }
                            else
                            {
                                LogMessage($"Failed to apply standards to: {fileInfo.Path}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Error applying standards to {fileInfo.Path}: {ex.Message}");
                            fileInfo.StandardsApplied = false;
                        }
                    }
                    
                    // Step 5: Check in all files with appropriate comments
                    int checkinCount = 0;
                    foreach (var fileInfo in filesToProcess)
                    {
                        // Only check in files that we checked out (not ones that were already checked out)
                        if (!fileInfo.IsAlreadyCheckedOut || fileInfo.File == null) continue;
                        
                        try
                        {
                            string comment = fileInfo.StandardsApplied ?
                                $"Standards v{requiredStandardsVersion} applied by Roeslein Connex" :
                                "Standards application attempted by Roeslein Connex but failed";
                                
                            LogMessage($"Checking in: {fileInfo.Path} with comment: {comment}");
                            fileInfo.File.UnlockFile(0, comment);
                            checkinCount++;
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Failed to check in {fileInfo.Path}: {ex.Message}");
                            if (!silentMode) MessageBox.Show($"Failed to check in file: {fileInfo.FileName}\n\nError: {ex.Message}", 
                                "Check-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    
                    // Show summary
                    if (!silentMode)
                    {
                        MessageBox.Show(
                            $"Standards application complete.\n\n" +
                            $"Files processed: {filesToProcess.Count}\n" +
                            $"Standards applied successfully: {successCount}\n" +
                            $"Files checked back in: {checkinCount}",
                            "Standards Application Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
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
                if (!silentMode) MessageBox.Show("No files in this assembly need standards applied or all files requiring standards are locked by other users.", 
                    "Standards Application", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
} 