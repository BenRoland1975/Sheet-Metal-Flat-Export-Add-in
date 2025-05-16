using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using EPDM.Interop.epdm; // Corrected: Was EdmLib. For PDM - USER ACTION: Ensure EPDM.Interop.epdm.dll is referenced.
using RoesleinAddIn; // Namespace for Settings, Settings.RawMaterial

// Unified namespace for all classes in this file
namespace RoesleinAddIn 
{
    public static class PdmUtils
    {
        public static string VaultName { get; set; } = "PCSVAULT"; // Example default, configure as needed

        public static bool IsPathInVaultView(IEdmVault5 vault, string path)
        {
            if (vault == null || string.IsNullOrEmpty(path))
                return false;

            try
            {
                IEdmFolder5 folder = null;
                IEdmFile5 file = vault.GetFileFromPath(path, out folder);
                
                if (file != null || folder != null) return true;


            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                if (ex.ErrorCode == unchecked((int)0x80040102) || ex.ErrorCode == unchecked((int)0x8004010B))
                {
                    return false;
                }
                Debug.WriteLine($"IsPathInVaultView COMException for path '{path}': {ex.Message} (0x{ex.ErrorCode:X})");
            }
            catch(Exception ex)
            {
                 Debug.WriteLine($"IsPathInVaultView Exception for path '{path}': {ex.Message}");
            }
            return false; 
        }
    }

    // Define PartProcessingSummary class
    public class PartProcessingSummary
    {
        public string FilePath { get; set; }
        public bool WasProcessed { get; set; }
        public bool NeedsSave { get; set; }
        public bool PdmCheckoutError { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsToolbox { get; set; }

        public PartProcessingSummary(string filePath)
        {
            FilePath = filePath;
            WasProcessed = false;
            NeedsSave = false;
            PdmCheckoutError = false;
            IsToolbox = false;
        }
    }

    // Define ComponentProcessingInfo class
    public class ComponentProcessingInfo
    {
        public string FilePath { get; set; }
        public IModelDoc2 Model { get; set; } 
        public IComponent2 Component { get; set; } 
        public Exception Error { get; set; }
        public bool IsAssembly { get; set; }
        public int Level { get; set; }


        public ComponentProcessingInfo(string filePath, IModelDoc2 model = null, IComponent2 component = null, bool isAssembly = false, int level = 0)
        {
            FilePath = filePath;
            Model = model;
            Component = component;
            IsAssembly = isAssembly;
            Level = level;
        }
    }


    public class PropertyStandardManager
    {
        private ISldWorks swApp;
        private IEdmVault5 pdmVault;
        private Settings currentSettings;
        private const string RoesleinStandardsVersionProperty = "Roeslein Standards Version";
        private const string CurrentStandardsVersion = "1.1"; 
        private const string PdmRawMaterialCsvPath = @"C:\Roeslein PDM Vault\Engineering\Master Files\Raw Material SheetMetal.csv";

        private int partsProcessedCount = 0;
        private int assembliesProcessedCount = 0;
        private int filesWrittenCount = 0;
        private int filesAlreadyUpToDateCount = 0;
        private int pdmCheckoutErrorsCount = 0;
        private int generalErrorsCount = 0;
        private List<string> successfullyProcessedFiles = new List<string>();
        private List<string> filesWithPdmCheckoutIssues = new List<string>();
        private List<string> filesWithGeneralErrors = new List<string>();
        private List<string> toolboxPartsSkipped = new List<string>();
        private List<string> filesNeedSavingList = new List<string>();

        private bool partPropertiesChanged = false;
        private bool partNeedsSave = false;
        private string currentPartMaterialStatus = "Not set";
        private string currentPartCutlistStatus = "Not processed";
        private string currentPartRawMaterialStatus = "Not processed";
        private string currentPartPdmStatus = "Not in PDM or status unknown";


        private StringBuilder logBuilder = new StringBuilder();

        public PropertyStandardManager(ISldWorks swApp, IEdmVault5 pdmVault)
        {
            this.swApp = swApp;
            this.pdmVault = pdmVault;
            if (this.pdmVault != null)
            {
                Settings.SetGlobalVaultForSettings(this.pdmVault);
            }
            this.currentSettings = Settings.LoadSettings();
            LoadRawMaterialsFromPdm();
        }

        private void LogMessage(string message)
        {
            Debug.WriteLine($"[PropertyStandardManager] {DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}");
            logBuilder.AppendLine(message);
        }

        public string GetLogMessages()
        {
            return logBuilder.ToString();
        }

        private void InitializePdmVault()
        {
            try
            {
                IEdmVault18 adminVault = new EdmVault5() as IEdmVault18; 
                if (adminVault == null) 
                {
                    adminVault = (IEdmVault18)new EdmVault5(); 
                }

                if (!adminVault.IsLoggedIn)
                {
                    string vaultName = GetDefaultVaultName(); 
                    if (!string.IsNullOrEmpty(vaultName))
                    {
                        adminVault.LoginAuto(vaultName, GetParentWindowHandleForPDM().ToInt32()); 
                        LogMessage($"Attempted PDM login for vault: {vaultName}. LoggedIn: {adminVault.IsLoggedIn}");
                    }
                    else
                    {
                        LogMessage("PDM Vault name not specified, cannot login automatically.");
                    }
                }

                if (adminVault.IsLoggedIn)
                {
                    this.pdmVault = adminVault as IEdmVault5;
                    Settings.SetGlobalVaultForSettings(this.pdmVault); 
                    LogMessage($"Successfully connected to PDM Vault: {this.pdmVault.Name}");
                    LogMessage($"PDM User: {GetCurrentPdmUserName()}");
                }
                else
                {
                    LogMessage("Failed to log into PDM Vault. PDM operations will be unavailable.");
                    this.pdmVault = null;
                }
            }
            catch (COMException ex)
            {
                LogMessage($"PDM Initialization COMException: 0x{ex.ErrorCode:X} - {ex.Message}");
                this.pdmVault = null;
            }
            catch (Exception ex)
            {
                LogMessage($"PDM Initialization Exception: {ex.Message}");
                this.pdmVault = null;
            }
        }

        private string GetDefaultVaultName()
        {
            try
            {
                IEdmVault5 tempVault = new EdmVault5();
                IEdmFolder5 folder;
                IEdmFile5 file = tempVault.GetFileFromPath(PdmRawMaterialCsvPath, out folder);
                if (tempVault != null && file != null)
                {
                    return tempVault.Name;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error getting default vault name: {ex.Message}");
            }
            return PdmUtils.VaultName; // Using PdmUtils from the same namespace
        }


        public void SetPdmVault(IEdmVault5 vault)
        {
            this.pdmVault = vault;
            Settings.SetGlobalVaultForSettings(this.pdmVault);
            if (this.pdmVault != null)
            {
                LogMessage($"PDM Vault explicitly set: {this.pdmVault.Name}");
                LogMessage($"PDM User: {GetCurrentPdmUserName()}");
                this.currentSettings = Settings.LoadSettings();
                LoadRawMaterialsFromPdm();
            }
            else
            {
                LogMessage("PDM Vault set to null.");
            }
        }

        private string GetCurrentPdmUserName()
        {
            if (pdmVault == null) return "N/A (Vault not connected)";
            try
            {
                IEdmVault7 vault7 = pdmVault as IEdmVault7;
                if (vault7 != null)
                {
                    IEdmUserMgr5 userMgr = vault7.CreateUtility(EdmUtility.EdmUtil_UserMgr) as IEdmUserMgr5;
                    if (userMgr != null)
                    {
                        IEdmUser6 user = userMgr.GetLoggedInUser() as IEdmUser6;
                        return user?.Name ?? "Unknown User";
                    }
                }
                return "Unknown User (Could not get from IEdmVault7)";
            }
            catch (Exception ex)
            {
                LogMessage($"Error getting PDM username: {ex.Message}");
                return "Error retrieving user";
            }
        }


        private void LoadRawMaterialsFromPdm()
        {
            LogMessage($"Attempting to load raw materials from PDM CSV: {PdmRawMaterialCsvPath}");
            try
            {
                if (this.currentSettings.RawMaterials == null)
                {
                    this.currentSettings.RawMaterials = new List<Settings.RawMaterial>(); // Corrected type
                }

                var pdmMaterials = Settings.LoadRawMaterialsFromPdmCsv(PdmRawMaterialCsvPath);
                if (pdmMaterials != null && pdmMaterials.Any())
                {
                    this.currentSettings.RawMaterials = pdmMaterials;
                    LogMessage($"Successfully loaded {pdmMaterials.Count} raw materials from PDM CSV.");
                }
                else
                {
                    LogMessage("No raw materials loaded from PDM CSV or an error occurred. Falling back to XML or default.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error in LoadRawMaterialsFromPdm: {ex.Message}");
                if (this.currentSettings.RawMaterials == null)
                {
                    this.currentSettings.RawMaterials = new List<Settings.RawMaterial>(); // Corrected type
                }
            }
        }


        private void ResetGlobalCountersAndLists()
        {
            partsProcessedCount = 0;
            assembliesProcessedCount = 0;
            filesWrittenCount = 0;
            filesAlreadyUpToDateCount = 0;
            pdmCheckoutErrorsCount = 0;
            generalErrorsCount = 0;
            successfullyProcessedFiles.Clear();
            filesWithPdmCheckoutIssues.Clear();
            filesWithGeneralErrors.Clear();
            toolboxPartsSkipped.Clear();
            filesNeedSavingList.Clear();
            logBuilder.Clear();
            LogMessage("Global counters and lists reset.");
        }

        private void ResetPartSpecificState()
        {
            partPropertiesChanged = false;
            partNeedsSave = false; 
            currentPartMaterialStatus = "Not set";
            currentPartCutlistStatus = "Not processed";
            currentPartRawMaterialStatus = "Not processed";
            currentPartPdmStatus = "Not in PDM or status unknown";
            LogMessage("Part-specific state variables reset.");
        }


        public string ApplyStandardsToActiveDocument()
        {
            ResetGlobalCountersAndLists();
            ModelDoc2 swModel = (ModelDoc2)swApp.ActiveDoc; // Explicit cast

            if (swModel == null)
            {
                LogMessage("No active document.");
                return "No active document to process.";
            }

            if (this.pdmVault == null)
            {
                InitializePdmVault(); 
                if (this.pdmVault == null)
                {
                    LogMessage("PDM Vault not available. Proceeding without PDM operations.");
                }
            }

            if (this.pdmVault != null) Settings.SetGlobalVaultForSettings(this.pdmVault);
            this.currentSettings = Settings.LoadSettings();
            LoadRawMaterialsFromPdm(); 


            string mainModelPath = swModel.GetPathName();
            string mainModelTitle = swModel.GetTitle();
            LogMessage($"Starting standards application for: {mainModelTitle} ({mainModelPath})");

            List<ComponentProcessingInfo> filesToProcessLater = new List<ComponentProcessingInfo>();
            List<string> assembliesToSaveLast = new List<string>(); 

            ModelDoc2 originalActiveDoc = swModel; 
            string originalActiveDocPath = mainModelPath;

            try
            {
                swDocumentTypes_e docType = (swDocumentTypes_e)swModel.GetType();

                if (docType == swDocumentTypes_e.swDocASSEMBLY)
                {
                    LogMessage($"Processing main assembly: {mainModelTitle}");
                    assembliesToSaveLast.Add(mainModelPath); 
                    ProcessAssemblyComponentsRecursively(swModel as IAssemblyDoc, filesToProcessLater, assembliesToSaveLast, 0);
                    assembliesProcessedCount++; 
                }
                else if (docType == swDocumentTypes_e.swDocPART)
                {
                    LogMessage($"Processing main part: {mainModelTitle}");
                    PartProcessingSummary summary = AttemptProcessPartImmediately(swModel, mainModelPath, null, 0, true, filesToProcessLater);
                    if (summary.NeedsSave) filesNeedSavingList.Add(mainModelPath);
                }
                else
                {
                    LogMessage($"Document type not supported for standards application: {docType}");
                    return $"Document type ({docType}) not supported.";
                }

                if (filesToProcessLater.Any())
                {
                    LogMessage($"--- Starting Deferred Processing Pass for {filesToProcessLater.Count} file(s) ---");
                    var distinctDeferredFiles = filesToProcessLater
                                                .GroupBy(f => f.FilePath)
                                                .Select(g => g.First())
                                                .ToList();
                    LogMessage($"Processing {distinctDeferredFiles.Count} distinct deferred files.");


                    foreach (var deferredInfo in distinctDeferredFiles)
                    {
                        LogMessage($"Attempting to process deferred file: {deferredInfo.FilePath}");
                        ModelDoc2 componentModel = null;
                        bool openedNew = false;
                        try
                        {
                            if (File.Exists(deferredInfo.FilePath))
                            {
                                componentModel = (ModelDoc2)swApp.GetOpenDocument(deferredInfo.FilePath);
                                if (componentModel == null)
                                {
                                    LogMessage($"Deferred file {deferredInfo.FilePath} not open, attempting to open.");
                                    int errors = 0;
                                    componentModel = (ModelDoc2)swApp.OpenDoc6(deferredInfo.FilePath,
                                                                    (int)GetDocumentTypeFromPath(deferredInfo.FilePath),
                                                                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                                                    "", ref errors, ref errors); 
                                    if (componentModel != null)
                                    {
                                        openedNew = true;
                                        LogMessage($"Successfully opened deferred file: {deferredInfo.FilePath}");
                                    }
                                    else
                                    {
                                        LogMessage($"Failed to open deferred file: {deferredInfo.FilePath}. Errors: {errors}");
                                        filesWithGeneralErrors.Add($"{deferredInfo.FilePath} (Failed to open for deferred processing)");
                                        generalErrorsCount++;
                                        continue;
                                    }
                                }
                                else
                                {
                                    LogMessage($"Deferred file {deferredInfo.FilePath} was already open.");
                                    swApp.ActivateDoc3(componentModel.GetTitle(), true, (int)swRebuildOnActivation_e.swRebuildActiveDoc, 0); // Corrected Enum
                                }

                                if (componentModel != null && componentModel.GetType() == (int)swDocumentTypes_e.swDocPART)
                                {
                                    PartProcessingSummary summary = ApplyStandardsToPartModel(componentModel, deferredInfo.FilePath, false); 
                                    if (summary.NeedsSave) filesNeedSavingList.Add(deferredInfo.FilePath);
                                    if (summary.PdmCheckoutError)
                                    {
                                        LogMessage($"PDM checkout still failed for deferred part: {deferredInfo.FilePath}");
                                    }
                                }
                                else if (componentModel != null && componentModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                                {
                                    LogMessage($"Deferred processing for assembly {deferredInfo.FilePath} - typically only parts are deferred for checkout issues.");
                                }
                            }
                            else
                            {
                                LogMessage($"Deferred file path does not exist: {deferredInfo.FilePath}");
                                filesWithGeneralErrors.Add($"{deferredInfo.FilePath} (Path not found during deferred processing)");
                                generalErrorsCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Error processing deferred file {deferredInfo.FilePath}: {ex.Message}");
                            filesWithGeneralErrors.Add($"{deferredInfo.FilePath} (Exception: {ex.Message})");
                            generalErrorsCount++;
                        }
                        finally
                        {
                            if (openedNew && componentModel != null)
                            {
                                LogMessage($"Closing document opened for deferred processing: {componentModel.GetPathName()}");
                                swApp.CloseDoc(componentModel.GetPathName());
                            }
                            if (File.Exists(originalActiveDocPath))
                            {
                                ModelDoc2 docToActivate = (ModelDoc2)swApp.ActivateDoc3(Path.GetFileName(originalActiveDocPath), true, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, 0);
                                if (docToActivate == null)
                                {
                                    LogMessage($"Warning: Could not reactivate original document {originalActiveDocPath}");
                                }
                            }
                            else
                            {
                                LogMessage($"Warning: Original document path {originalActiveDocPath} no longer exists for reactivation.");
                            }
                        }
                    }
                }


                LogMessage($"--- Saving Modified Documents ---");
                var distinctFilesToSave = filesNeedSavingList.Distinct().ToList();
                LogMessage($"Found {distinctFilesToSave.Count} unique files marked for saving.");

                foreach (string filePathToSave in distinctFilesToSave)
                {
                    if (GetDocumentTypeFromPath(filePathToSave) == swDocumentTypes_e.swDocPART)
                    {
                        SaveAndManageDocument(filePathToSave, false); 
                    }
                }
                var assembliesThatNeedSaving = distinctFilesToSave
                    .Where(f => GetDocumentTypeFromPath(f) == swDocumentTypes_e.swDocASSEMBLY)
                    .ToList();

                var assembliesToActuallySaveInOrder = assembliesToSaveLast
                                                    .Where(asmPath => assembliesThatNeedSaving.Contains(asmPath))
                                                    .Distinct() 
                                                    .ToList();

                foreach (var asmPath in assembliesThatNeedSaving)
                {
                    if (!assembliesToActuallySaveInOrder.Contains(asmPath))
                    {
                        assembliesToActuallySaveInOrder.Add(asmPath);
                    }
                }


                foreach (string asmPath in assembliesToActuallySaveInOrder.Where(p => !string.Equals(p, mainModelPath, StringComparison.OrdinalIgnoreCase)))
                {
                    SaveAndManageDocument(asmPath, true); 
                }

                if (assembliesToActuallySaveInOrder.Contains(mainModelPath) && GetDocumentTypeFromPath(mainModelPath) == swDocumentTypes_e.swDocASSEMBLY)
                {
                    SaveAndManageDocument(mainModelPath, true);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Critical error in ApplyStandardsToActiveDocument: {ex.Message}\nStackTrace: {ex.StackTrace}");
                generalErrorsCount++;
                filesWithGeneralErrors.Add($"{mainModelTitle} (Critical Error: {ex.Message})");
            }
            finally
            {
                if (File.Exists(originalActiveDocPath))
                {
                    ModelDoc2 doc = (ModelDoc2)swApp.ActivateDoc3(Path.GetFileName(originalActiveDocPath), true, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, 0);
                    if (doc == null) LogMessage($"Failed to reactivate original document: {originalActiveDocPath}");
                    else LogMessage($"Reactivated original document: {doc.GetTitle()}");
                }
                else if (originalActiveDoc != null)
                { 
                    ModelDoc2 doc = (ModelDoc2)swApp.ActivateDoc3(originalActiveDoc.GetTitle(), true, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, 0);
                    if (doc == null) LogMessage($"Failed to reactivate original document by title: {originalActiveDoc.GetTitle()} (Path was {originalActiveDocPath})");
                    else LogMessage($"Reactivated original document by title: {doc.GetTitle()}");
                }


                LogMessage("--- Standards Application Summary ---");
                LogMessage($"Total Parts Processed: {partsProcessedCount}");
                LogMessage($"Total Assemblies Traversed (excluding main if part): {assembliesProcessedCount}"); 
                LogMessage($"Files Written/Updated: {filesWrittenCount}");
                LogMessage($"Files Already Up-to-Date: {filesAlreadyUpToDateCount}");
                LogMessage($"Toolbox Parts Skipped: {toolboxPartsSkipped.Count}");
                if (toolboxPartsSkipped.Any()) LogMessage($"Toolbox Parts List: {string.Join(", ", toolboxPartsSkipped)}");
                LogMessage($"PDM Checkout Errors: {pdmCheckoutErrorsCount}");
                if (filesWithPdmCheckoutIssues.Any()) LogMessage($"Files with PDM Checkout Issues: {string.Join(", ", filesWithPdmCheckoutIssues)}");
                LogMessage($"General Errors: {generalErrorsCount}");
                if (filesWithGeneralErrors.Any()) LogMessage($"Files with General Errors: {string.Join(", ", filesWithGeneralErrors)}");
                LogMessage($"Successfully Processed & Saved (if changed): {successfullyProcessedFiles.Count}");
                if (successfullyProcessedFiles.Any()) LogMessage($"Successfully Processed List: {string.Join(", ", successfullyProcessedFiles)}");

            }

            string completionMsg =
                $"Standards application for '{mainModelTitle}' finished.\n" +
                $"Processed {partsProcessedCount} part(s), Traversed {assembliesProcessedCount} assembly(ies).\n" +
                $"{filesWrittenCount} file(s) updated.\n" +
                $"{filesAlreadyUpToDateCount} file(s) already met standards.\n" +
                $"{toolboxPartsSkipped.Count} Toolbox part(s) skipped.\n" +
                $"{pdmCheckoutErrorsCount} PDM checkout issue(s).\n" +
                $"{generalErrorsCount} general error(s).\n" +
                "Review log for details.";

            swApp.SendMsgToUser2(completionMsg, (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
            return GetLogMessages(); 
        }


        private void ProcessAssemblyComponentsRecursively(IAssemblyDoc swAssembly, List<ComponentProcessingInfo> filesToProcessLater, List<string> assembliesToSaveLast, int level)
        {
            if (level > currentSettings.MaxRecursionDepth) 
            {
                LogMessage($"Max recursion depth ({currentSettings.MaxRecursionDepth}) reached. Stopping traversal for this branch.");
                return;
            }

            LogMessage($"Processing assembly: {((IModelDoc2)swAssembly).GetTitle()} at level {level}");
            object[] vComponents = (object[])swAssembly.GetComponents(false); 

            if (vComponents == null || vComponents.Length == 0)
            {
                LogMessage("No components found in this assembly.");
                return;
            }

            foreach (object vComp in vComponents)
            {
                Component2 swComp = (Component2)vComp;
                if (swComp == null) continue;

                ModelDoc2 compModel = (ModelDoc2)swComp.GetModelDoc2(); 

                if (compModel == null)
                {
                    LogMessage($"Could not get model for component: {swComp.Name2} ({swComp.GetPathName()}) initially.");
                    if (swComp.IsSuppressed()) 
                    {
                        LogMessage($"Component {swComp.Name2} is fully suppressed (IsSuppressed() is true). Skipping.");
                        continue;
                    }
                    if (swComp.GetSuppression() == (int)swComponentSuppressionState_e.swComponentLightweight)
                    {
                        LogMessage($"Component {swComp.Name2} is lightweight (GetSuppression() is swComponentLightweight). Attempting to resolve...");
                        swComponentSuppressionState_e resolveStatus = (swComponentSuppressionState_e)swComp.SetSuppression2((int)swComponentSuppressionState_e.swComponentResolved);
                        
                        if (resolveStatus == swComponentSuppressionState_e.swComponentResolved)
                        {
                            LogMessage($"Component {swComp.Name2} resolved (or was already resolved). Status: {resolveStatus}. Attempting to get model again.");
                            compModel = (ModelDoc2)swComp.GetModelDoc2(); 
                            if (compModel == null) {
                                LogMessage($"Still unable to get model for {swComp.Name2} after attempting to resolve lightweight. Skipping.");
                                filesWithGeneralErrors.Add($"{swComp.GetPathName()} (Failed to get model after lightweight resolve)");
                                generalErrorsCount++;
                                continue;
                            }
                        } else {
                            LogMessage($"Failed to resolve lightweight component {swComp.Name2}. Resolve attempt status: {resolveStatus}. Skipping.");
                            filesWithGeneralErrors.Add($"{swComp.GetPathName()} (Failed to resolve lightweight component, status: {resolveStatus})");
                            generalErrorsCount++;
                            continue;
                        }
                    } else {
                        LogMessage($"Component {swComp.Name2} GetModelDoc2 is null. IsSuppressed: {swComp.IsSuppressed()}, GetSuppression: {(swComponentSuppressionState_e)swComp.GetSuppression()}. Skipping.");
                        filesWithGeneralErrors.Add($"{swComp.GetPathName()} (Model null, not suppressed, not resolved from lightweight state)");
                        generalErrorsCount++;
                        continue;
                    }
                }

                if (compModel == null)
                {
                    LogMessage($"Component {swComp.Name2} ({swComp.GetPathName()}) could not be resolved to a valid model after all attempts. Skipping.");
                    continue; 
                }

                string compPath = compModel.GetPathName();
                string compName = swComp.Name2;
                LogMessage($"Processing component: {compName}, Path: {compPath}, Level: {level}");


                if (swComp.IsSuppressed())
                {
                    LogMessage($"Component '{compName}' is suppressed. Skipping.");
                    continue;
                }


                swDocumentTypes_e docType = (swDocumentTypes_e)compModel.GetType();

                if (docType == swDocumentTypes_e.swDocPART)
                {
                    PartProcessingSummary summary = AttemptProcessPartImmediately(compModel, compPath, swComp, level, false, filesToProcessLater);
                    if (summary.NeedsSave) filesNeedSavingList.Add(compPath);

                }
                else if (docType == swDocumentTypes_e.swDocASSEMBLY)
                {
                    LogMessage($"Component is a sub-assembly: {compName}. Path: {compPath}");
                    if (!assembliesToSaveLast.Contains(compPath, StringComparer.OrdinalIgnoreCase))
                    {
                        assembliesToSaveLast.Add(compPath);
                    }
                    assembliesProcessedCount++; 

                    ModelDoc2 parentAssemblyDoc = (ModelDoc2)swAssembly;
                    if (string.Equals(parentAssemblyDoc.GetPathName(), compPath, StringComparison.OrdinalIgnoreCase))
                    {
                        LogMessage($"Circular reference detected: Sub-assembly {compName} ({compPath}) is the same as its parent. Skipping further recursion here.");
                        continue;
                    }

                    ProcessAssemblyComponentsRecursively((IAssemblyDoc)compModel, filesToProcessLater, assembliesToSaveLast, level + 1);
                }
            }
        }

        private PartProcessingSummary AttemptProcessPartImmediately(ModelDoc2 partModel, string partPath, Component2 componentContext, int level, bool isMainDocumentContext, List<ComponentProcessingInfo> filesToProcessLater)
        {
            string partName = Path.GetFileName(partPath);
            LogMessage($"Attempting to process part: {partName} (Path: {partPath})");
            var summary = new PartProcessingSummary(partPath);

            if (IsToolboxPart(partModel))
            {
                LogMessage($"Part '{partName}' is a Toolbox part. Skipping PDM checks and property modifications.");
                summary.IsToolbox = true;
                toolboxPartsSkipped.Add(partName);
                return summary; 
            }

            if (string.IsNullOrEmpty(partPath) || !File.Exists(partPath))
            {
                LogMessage($"Part path is invalid or file does not exist: '{partPath}'. Skipping.");
                summary.ErrorMessage = "File path invalid or does not exist.";
                filesWithGeneralErrors.Add($"{partName} (Path invalid or file missing)");
                generalErrorsCount++;
                return summary;
            }

            bool proceedWithProcessing = false;
            string lockedByAnotherUserMessage = string.Empty;


            if (pdmVault != null && PdmUtils.IsPathInVaultView(pdmVault, partPath))
            {
                LogMessage($"Part '{partName}' is in PDM. Checking status...");
                currentPartPdmStatus = $"In PDM. User: {GetCurrentPdmUserName()}";
                IEdmFile5 pdmFile = null;
                IEdmFolder5 pdmFolder = null;
                pdmFile = pdmVault.GetFileFromPath(partPath, out pdmFolder);

                if (pdmFile != null)
                {
                    if (pdmFile.IsLocked)
                    {
                        var lockedByUser = pdmFile.LockedByUser;
                        string user = lockedByUser != null ? lockedByUser.Name : "";
                        string computer = pdmFile.LockedOnComputer;
                        currentPartPdmStatus += $". Locked by: {user} on {computer}";
                        LogMessage($"PDM File '{partName}' is locked by '{user}' on '{computer}'.");

                        if (!string.Equals(user, GetCurrentPdmUserName(), StringComparison.OrdinalIgnoreCase))
                        {
                            LogMessage($"File '{partName}' locked by another user ('{user}'). Deferring processing.");
                            lockedByAnotherUserMessage = $"File locked by PDM user '{user}'.";
                            filesToProcessLater.Add(new ComponentProcessingInfo(partPath, partModel, componentContext, false, level) { Error = new Exception(lockedByAnotherUserMessage) });
                            filesWithPdmCheckoutIssues.Add($"{partName} ({lockedByAnotherUserMessage})");
                            pdmCheckoutErrorsCount++;
                            summary.PdmCheckoutError = true;
                            summary.ErrorMessage = lockedByAnotherUserMessage;
                            return summary; 
                        }
                        else
                        {
                            LogMessage($"File '{partName}' is locked by the current user ('{user}'). Proceeding.");
                            proceedWithProcessing = true; 
                        }
                    }
                    else
                    {
                        LogMessage($"File '{partName}' is not locked. Will attempt checkout if modifications are needed.");
                        proceedWithProcessing = true; 
                    }
                }
                else
                {
                    LogMessage($"Could not get PDM file object for '{partName}'. Assuming it's not under full PDM control or an issue occurred. Proceeding with caution.");
                    currentPartPdmStatus = "In PDM (Vault view), but PDM file object not retrieved.";
                    proceedWithProcessing = true; 
                }
            }
            else if (pdmVault != null)
            {
                LogMessage($"Part '{partName}' is not in a PDM vault view path according to PdmUtils.IsPathInVaultView. Path: {partPath}. Vault: {pdmVault.Name}. PDM operations will be skipped.");
                currentPartPdmStatus = "Not in PDM vault view.";
                proceedWithProcessing = true; 
            }
            else
            {
                LogMessage("PDM Vault not available. Processing part without PDM operations.");
                currentPartPdmStatus = "PDM vault not connected.";
                proceedWithProcessing = true; 
            }


            if (proceedWithProcessing)
            {
                LogMessage($"Proceeding with standards application for part: {partName}");
                ModelDoc2 previouslyActiveDoc = (ModelDoc2)swApp.ActiveDoc; // Explicit Cast
                bool needToRestoreActiveDoc = false;
                if (!isMainDocumentContext && (previouslyActiveDoc == null || previouslyActiveDoc.GetPathName() != partModel.GetPathName()))
                {
                    LogMessage($"Activating part document: {partName}");
                    int errors = 0;
                    swApp.ActivateDoc3(partModel.GetTitle(), true, (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref errors); // Corrected Enum
                    if (errors != 0) LogMessage($"Error activating document {partName}: {errors}");
                    needToRestoreActiveDoc = true;
                }


                PartProcessingSummary partSummary = ApplyStandardsToPartModel(partModel, partPath, isMainDocumentContext);
                summary.WasProcessed = partSummary.WasProcessed;
                summary.NeedsSave = partSummary.NeedsSave;
                summary.PdmCheckoutError = partSummary.PdmCheckoutError; 
                summary.ErrorMessage = partSummary.ErrorMessage;

                if (partSummary.PdmCheckoutError)
                {
                    LogMessage($"PDM checkout error occurred during ApplyStandardsToPartModel for '{partName}'. Adding to deferred list.");
                    filesToProcessLater.Add(new ComponentProcessingInfo(partPath, partModel, componentContext, false, level) { Error = new Exception(partSummary.ErrorMessage ?? "PDM Checkout Error") });
                }

                if (needToRestoreActiveDoc && previouslyActiveDoc != null && File.Exists(previouslyActiveDoc.GetPathName()))
                {
                    LogMessage($"Restoring previously active document: {previouslyActiveDoc.GetTitle()}");
                    int errors = 0;
                    swApp.ActivateDoc3(previouslyActiveDoc.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref errors);
                    if (errors != 0) LogMessage($"Error restoring active document {previouslyActiveDoc.GetTitle()}: {errors}");
                }
            }
            return summary;
        }


        private PartProcessingSummary ApplyStandardsToPartModel(ModelDoc2 swModel, string modelPath, bool isMainDocumentContext)
        {
            ResetPartSpecificState(); 
            var summary = new PartProcessingSummary(modelPath);
            string modelName = Path.GetFileName(modelPath);
            LogMessage($"--- Applying Standards to Part: {modelName} ({modelPath}) ---");

            if (swModel == null)
            {
                LogMessage($"Model for '{modelName}' is null. Cannot apply standards.");
                summary.ErrorMessage = "Model is null.";
                filesWithGeneralErrors.Add($"{modelName} (Model was null in ApplyStandardsToPartModel)");
                generalErrorsCount++;
                return summary;
            }

            ModelDoc2 originalActiveDoc = (ModelDoc2)swApp.ActiveDoc; // Explicit Cast
            bool activatedForProcessing = false;
            if (originalActiveDoc == null || !string.Equals(originalActiveDoc.GetPathName(), modelPath, StringComparison.OrdinalIgnoreCase))
            {
                LogMessage($"Activating document for processing: {modelName}");
                int errors = 0;
                ModelDoc2 activatedDoc = (ModelDoc2)swApp.ActivateDoc3(Path.GetFileName(modelPath), true, (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref errors); // Corrected Enum
                if (activatedDoc == null || errors != 0)
                {
                    LogMessage($"Failed to activate document '{modelName}'. Error code: {errors}. Properties might not be set correctly.");
                }
                else
                {
                    swModel = activatedDoc; 
                    activatedForProcessing = true;
                    LogMessage($"Document '{modelName}' activated.");
                }
            }


            bool isCheckedOutByMe = false;
            bool wasCheckedOutForThisOperation = false;
            IEdmFile5 pdmFile = null;

            if (pdmVault != null && PdmUtils.IsPathInVaultView(pdmVault, modelPath))
            {
                LogMessage($"PDM: File '{modelName}' is in vault view. Current user: {GetCurrentPdmUserName()}");
                IEdmFolder5 pdmFolder = null;
                pdmFile = pdmVault.GetFileFromPath(modelPath, out pdmFolder);

                if (pdmFile != null)
                {
                    if (pdmFile.IsLocked)
                    {
                        var lockedByUser = pdmFile.LockedByUser;
                        string user = lockedByUser != null ? lockedByUser.Name : "";
                        string computer = pdmFile.LockedOnComputer;
                        currentPartPdmStatus = $"PDM: Locked by {user} on {computer}.";
                        if (string.Equals(user, GetCurrentPdmUserName(), StringComparison.OrdinalIgnoreCase))
                        {
                            LogMessage($"PDM: File '{modelName}' is already checked out by current user ('{user}').");
                            isCheckedOutByMe = true;
                        }
                        else
                        {
                            LogMessage($"PDM ERROR: File '{modelName}' is locked by another user: '{user}'. Cannot apply standards.");
                            summary.PdmCheckoutError = true;
                            summary.ErrorMessage = $"File locked by PDM user '{user}'.";
                            filesWithPdmCheckoutIssues.Add($"{modelName} (Locked by {user})");
                            pdmCheckoutErrorsCount++;
                            if (activatedForProcessing && originalActiveDoc != null && File.Exists(originalActiveDoc.GetPathName())) swApp.ActivateDoc3(originalActiveDoc.GetTitle(), false, 0, 0);
                            return summary; 
                        }
                    }
                    else
                    {
                        LogMessage($"PDM: File '{modelName}' is not locked. Attempting checkout.");
                        try
                        {
                            pdmFile.LockFile(GetParentWindowHandleForPDM().ToInt32(), 0); 
                            isCheckedOutByMe = true;
                            wasCheckedOutForThisOperation = true;
                            currentPartPdmStatus = "PDM: Checked out successfully for modification.";
                            LogMessage($"PDM: File '{modelName}' checked out successfully by '{GetCurrentPdmUserName()}'.");
                        }
                        catch (COMException ex)
                        {
                            LogMessage($"PDM CHECKOUT FAILED for '{modelName}'. COMException: HRESULT 0x{ex.ErrorCode:X8}, Message: {ex.Message}");
                            summary.PdmCheckoutError = true;
                            summary.ErrorMessage = $"PDM Checkout failed: {ex.Message} (0x{ex.ErrorCode:X8})";
                            filesWithPdmCheckoutIssues.Add($"{modelName} (Checkout COMException: 0x{ex.ErrorCode:X8})");
                            pdmCheckoutErrorsCount++;
                            if (ex.ErrorCode == unchecked((int)0x8004020B)) 
                            {
                                LogMessage("PDM Error: File is exclusively opened by another application or PDM hook conflict (0x8004020B).");
                                summary.ErrorMessage += " File exclusively open elsewhere.";
                            }
                            if (activatedForProcessing && originalActiveDoc != null && File.Exists(originalActiveDoc.GetPathName())) swApp.ActivateDoc3(originalActiveDoc.GetTitle(), false, 0, 0);
                            return summary; 
                        }
                    }
                }
                else
                {
                    LogMessage($"PDM: Could not get IEdmFile5 object for '{modelName}', though it's in vault view. Assuming not under PDM control or error.");
                    currentPartPdmStatus = "PDM: In vault view, but file object not retrieved.";
                }
            }
            else if (pdmVault != null)
            {
                LogMessage($"PDM: File '{modelName}' is not in a PDM vault view. Path: {modelPath}. Skipping PDM checkout.");
                currentPartPdmStatus = "Not in PDM vault view.";
            }
            else
            {
                LogMessage("PDM: Vault not available. Skipping PDM checkout.");
                currentPartPdmStatus = "PDM vault not connected.";
            }


            try
            {
                CustomPropertyManager swCustPropMgr = swModel.Extension.CustomPropertyManager[""];
                string existingVersion = "";
                swCustPropMgr.Get4(RoesleinStandardsVersionProperty, false, out existingVersion, out _);

                if (existingVersion == CurrentStandardsVersion && !currentSettings.ForceApplyStandards)
                {
                    LogMessage($"Part '{modelName}' already has current standards version ({CurrentStandardsVersion}) and force apply is off. Skipping detailed checks.");
                    filesAlreadyUpToDateCount++;
                    summary.WasProcessed = true; 
                }
                else
                {
                    LogMessage(existingVersion == CurrentStandardsVersion ?
                        $"Part '{modelName}' has current version but ForceApply is ON." :
                        $"Part '{modelName}' version ('{existingVersion}') differs from current ('{CurrentStandardsVersion}'). Applying standards.");

                    ApplyMaterialStandard(swModel);
                    ApplyCutlistProperties(swModel); 

                    if (partPropertiesChanged) 
                    {
                        LogMessage($"Properties changed for '{modelName}'. Setting Roeslein Standards Version to '{CurrentStandardsVersion}'.");
                        swCustPropMgr.Add3(RoesleinStandardsVersionProperty, (int)swCustomInfoType_e.swCustomInfoText, CurrentStandardsVersion, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                        partNeedsSave = true; 
                        filesWrittenCount++;
                        successfullyProcessedFiles.Add(modelName);
                    }
                    else
                    {
                        LogMessage($"No properties were changed for '{modelName}' during standards application.");
                        if (existingVersion != CurrentStandardsVersion)
                        {
                            LogMessage($"Updating Roeslein Standards Version property for '{modelName}' to '{CurrentStandardsVersion}' as it was outdated, even if no other props changed.");
                            swCustPropMgr.Add3(RoesleinStandardsVersionProperty, (int)swCustomInfoType_e.swCustomInfoText, CurrentStandardsVersion, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            partNeedsSave = true;
                            filesWrittenCount++; 
                            successfullyProcessedFiles.Add(modelName);
                        }
                        else
                        {
                            LogMessage($"Part '{modelName}' already met standards, no changes made (including version).");
                            filesAlreadyUpToDateCount++; 
                        }
                    }
                }
                summary.WasProcessed = true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying standards to '{modelName}': {ex.Message}\nStackTrace: {ex.StackTrace}");
                summary.ErrorMessage = $"Error applying standards: {ex.Message}";
                filesWithGeneralErrors.Add($"{modelName} (ApplyStandards Error: {ex.Message})");
                generalErrorsCount++;
            }
            finally
            {
                if (wasCheckedOutForThisOperation && pdmFile != null)
                {
                    CheckInDocumentIfAppropriate(pdmFile, $"Standards applied by Roeslein Add-in. Changes: {(partPropertiesChanged ? "Yes" : "No")}", modelName);
                }
                else if (isCheckedOutByMe && !wasCheckedOutForThisOperation && pdmFile != null)
                {
                    LogMessage($"PDM: File '{modelName}' was already checked out by user. Leaving it checked out.");
                }

                if (partNeedsSave)
                {
                    summary.NeedsSave = true;
                    LogMessage($"Part '{modelName}' marked as needing save.");
                }

                partsProcessedCount++; 

                if (activatedForProcessing && originalActiveDoc != null && File.Exists(originalActiveDoc.GetPathName()))
                {
                    LogMessage($"Restoring original active document: {originalActiveDoc.GetTitle()}");
                    swApp.ActivateDoc3(originalActiveDoc.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, 0);
                }
                LogMessage($"--- Finished Applying Standards to Part: {modelName} ---");
                LogMessage($"Part Summary for {modelName}: Material: {currentPartMaterialStatus}, Cutlist: {currentPartCutlistStatus}, RawMaterial: {currentPartRawMaterialStatus}, PDM: {currentPartPdmStatus}, NeedsSave: {partNeedsSave}");

            }
            return summary;
        }


        private void ApplyMaterialStandard(ModelDoc2 swModel)
        {
            if (string.IsNullOrEmpty(currentSettings.DefaultMaterialName))
            {
                LogMessage("Default material name not set in settings. Skipping material application.");
                currentPartMaterialStatus = "Skipped (No default material in settings)";
                return;
            }

            PartDoc swPart = (PartDoc)swModel;
            string currentMaterialName = swPart.GetMaterialPropertyName2("", out string currentMaterialDB); 
            LogMessage($"Current material: '{currentMaterialName}', Database: '{currentMaterialDB}'");

            if (currentMaterialName != currentSettings.DefaultMaterialName)
            {
                swPart.SetMaterialPropertyName2("", currentSettings.DefaultMaterialDatabase, currentSettings.DefaultMaterialName);
                partPropertiesChanged = true;
                currentPartMaterialStatus = GetMaterialStatusMessage(currentSettings.DefaultMaterialName, true);
            }
            else
            {
                LogMessage($"Material is already '{currentSettings.DefaultMaterialName}'. No change needed.");
                currentPartMaterialStatus = $"Already '{currentSettings.DefaultMaterialName}'";
            }
        }


        private void ApplyCutlistProperties(ModelDoc2 swModel)
        {
            LogMessage("Attempting to apply cutlist properties...");
            Feature swFeat = (Feature)swModel.FirstFeature();
            bool propsApplied = false;
            bool isSheetMetalPart = false;

            double sheetMetalThicknessMeters = -1;

            while (swFeat != null)
            {
                if (swFeat.GetTypeName2() == "SheetMetal")
                {
                    isSheetMetalPart = true;
                    SheetMetalFeatureData swSheetMetalData = (SheetMetalFeatureData)swFeat.GetDefinition();
                    if (swSheetMetalData != null)
                    {
                        sheetMetalThicknessMeters = swSheetMetalData.Thickness; 
                        LogMessage($"SheetMetal feature found. Thickness: {sheetMetalThicknessMeters} meters.");
                    }
                    break; 
                }
                swFeat = (Feature)swFeat.GetNextFeature();
            }
            if (!isSheetMetalPart)
            {
                LogMessage("Not a sheet metal part (no 'SheetMetal' feature found). Skipping cutlist property processing for raw material.");
                currentPartCutlistStatus = "Skipped (Not a sheet metal part)";
                return;
            }


            swFeat = (Feature)swModel.FirstFeature();
            int cutListItemIndex = 0;

            while (swFeat != null)
            {
                LogMessage($"Inspecting feature: {swFeat.Name} (Type: {swFeat.GetTypeName2()})");
                if (swFeat.GetTypeName2() == "SolidBodyFolder") 
                {
                    Feature swSubFeat = (Feature)swFeat.IGetFirstSubFeature();
                    while (swSubFeat != null)
                    {
                        LogMessage($"  SubFeature: {swSubFeat.Name} (Type: {swSubFeat.GetTypeName2()})");
                        if (swSubFeat.GetTypeName2() == "CutListFolder")
                        {
                            cutListItemIndex++;
                            LogMessage($"Found CutListFolder: {swSubFeat.Name}. Attempting to process properties.");
                            CustomPropertyManager swCutListPropMgr = (CustomPropertyManager)swSubFeat.CustomPropertyManager;
                            if (swCutListPropMgr == null)
                            {
                                LogMessage($"Could not get CustomPropertyManager for CutListFolder: {swSubFeat.Name}");
                                swSubFeat = swSubFeat.IGetNextSubFeature();
                                continue;
                            }

                            string boundingBoxLengthStr = "", boundingBoxWidthStr = "", sheetMetalThicknessStr = "";
                            string valOut; 

                            int resLength = swCutListPropMgr.Get5("Bounding Box Length", true, out valOut, out boundingBoxLengthStr, out _);
                            LogMessage($"'Bounding Box Length' from cutlist: Value='{boundingBoxLengthStr}', ResolvedValue='{valOut}', Get5 result={resLength}");

                            int resWidth = swCutListPropMgr.Get5("Bounding Box Width", true, out valOut, out boundingBoxWidthStr, out _);
                            LogMessage($"'Bounding Box Width' from cutlist: Value='{boundingBoxWidthStr}', ResolvedValue='{valOut}', Get5 result={resWidth}");

                            int resThickness = swCutListPropMgr.Get5("Sheet Metal Thickness", true, out valOut, out sheetMetalThicknessStr, out _);
                            LogMessage($"'Sheet Metal Thickness' from cutlist: Value='{sheetMetalThicknessStr}', ResolvedValue='{valOut}', Get5 result={resThickness}");

                            double length = 0, width = 0, thicknessInches = 0;
                            bool lengthValid = double.TryParse(boundingBoxLengthStr, out length);
                            bool widthValid = double.TryParse(boundingBoxWidthStr, out width);
                            bool thicknessValid = double.TryParse(sheetMetalThicknessStr, out thicknessInches);

                            if (!thicknessValid && sheetMetalThicknessMeters > 0)
                            {
                                thicknessInches = sheetMetalThicknessMeters * 39.3701; 
                                LogMessage($"Using thickness from SheetMetal feature: {thicknessInches} inches (converted from {sheetMetalThicknessMeters}m).");
                                thicknessValid = true; 
                            }
                            else if (thicknessValid)
                            {
                                LogMessage($"Using thickness from cutlist property: {thicknessInches} inches.");
                            }
                            else
                            {
                                LogMessage("Sheet Metal Thickness could not be determined from cutlist or SheetMetal feature. Cannot find raw material match.");
                                currentPartCutlistStatus = GetCutlistStatusMessage("Thickness not found", false);
                                swSubFeat = (Feature)swSubFeat.IGetNextSubFeature();
                                continue; 
                            }


                            if (lengthValid && widthValid && thicknessValid)
                            {
                                LogMessage($"Parsed dimensions for {swSubFeat.Name}: Length={length}, Width={width}, Thickness={thicknessInches} (inches)");
                                Settings.RawMaterial match = FindRawMaterialMatch(length, width, thicknessInches);
                                if (match != null)
                                {
                                    LogMessage($"Found raw material match: PartNo='{match.PartNumber}', UoM='{match.UnitOfMeasure}'");
                                    CustomPropertyManager modelPropMgr = swModel.Extension.CustomPropertyManager[""]; 

                                    modelPropMgr.Add3("Raw Material Part Number", (int)swCustomInfoType_e.swCustomInfoText, match.PartNumber, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                                    modelPropMgr.Add3("Unit of Measure", (int)swCustomInfoType_e.swCustomInfoText, match.UnitOfMeasure, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

                                    LogMessage($"Set 'Raw Material Part Number' to '{match.PartNumber}' and 'Unit of Measure' to '{match.UnitOfMeasure}' on the model.");
                                    partPropertiesChanged = true;
                                    propsApplied = true;
                                    currentPartRawMaterialStatus = GetRawMaterialStatusMessage($"Set to {match.PartNumber}, UoM {match.UnitOfMeasure}", true);
                                }
                                else
                                {
                                    LogMessage("No raw material match found for the dimensions.");
                                    currentPartRawMaterialStatus = GetRawMaterialStatusMessage("No match found", false);
                                }
                            }
                            else
                            {
                                LogMessage($"Invalid dimension(s) for {swSubFeat.Name}: L={boundingBoxLengthStr}, W={boundingBoxWidthStr}, T={sheetMetalThicknessStr}. Cannot find raw material match.");
                                currentPartRawMaterialStatus = GetRawMaterialStatusMessage("Invalid dimensions from cutlist", false);
                            }
                            if (propsApplied) break; 
                        }
                        swSubFeat = (Feature)swSubFeat.IGetNextSubFeature();
                    }
                }
                if (propsApplied) break; 
                swFeat = (Feature)swFeat.GetNextFeature();
            }

            if (!propsApplied && isSheetMetalPart)
            {
                LogMessage("No cutlist properties found or processed that led to a raw material assignment, despite being a sheet metal part.");
                if (string.IsNullOrEmpty(currentPartRawMaterialStatus) || currentPartRawMaterialStatus == "Not processed")
                {
                    currentPartRawMaterialStatus = GetRawMaterialStatusMessage("No suitable cutlist item found or processed", false);
                }
            }
            currentPartCutlistStatus = propsApplied ? "Properties applied" : (isSheetMetalPart ? "No matching cutlist item or no match found" : "Skipped (Not sheet metal or no cutlist)");
        }

        private Settings.RawMaterial FindRawMaterialMatch(double length, double width, double thicknessInches)
        {
            if (currentSettings.RawMaterials == null || !currentSettings.RawMaterials.Any())
            {
                LogMessage("RawMaterials list is empty or null. Cannot find match.");
                return null;
            }
            LogMessage($"Finding raw material for L={length}, W={width}, Thickness={thicknessInches} (inches). Tolerance for thickness: +/- {currentSettings.ThicknessToleranceInches}");

            double dim1 = Math.Max(length, width); 
            double dim2 = Math.Min(length, width); 

            foreach (var material in currentSettings.RawMaterials)
            {
                double matDim1 = Math.Max(material.Length, material.Width);
                double matDim2 = Math.Min(material.Length, material.Width);

                bool thicknessMatch = Math.Abs(material.Thickness - thicknessInches) <= currentSettings.ThicknessToleranceInches;
                bool dimensionsMatch = (dim1 >= matDim1 && dim2 >= matDim2);


                if (thicknessMatch && dimensionsMatch)
                {
                    LogMessage($"Match found: CSV L={material.Length}, W={material.Width}, T={material.Thickness} with Part L={length}, W={width}, T_Cutlist={thicknessInches}. Ordered part dims: D1={dim1}, D2={dim2}. Ordered mat dims: M1={matDim1}, M2={matDim2}");
                    return material;
                }
                else if (thicknessMatch) 
                {
                    LogMessage($"Thickness match for PartNo {material.PartNumber} (T={material.Thickness}), but dimensions no match: Part Dims (ordered) Lrg={dim1}, Sml={dim2}. Material Dims (ordered) Lrg={matDim1}, Sml={matDim2}.");
                }
            }
            LogMessage("No suitable raw material found in the list for the given dimensions and thickness.");
            return null;
        }

        private void SaveAndManageDocument(string docPath, bool isAssembly)
        {
            if (string.IsNullOrEmpty(docPath) || !File.Exists(docPath))
            {
                LogMessage($"SaveAndManageDocument: Path is null, empty, or file does not exist: '{docPath}'. Skipping save.");
                return;
            }

            LogMessage($"SaveAndManageDocument: Preparing to save '{docPath}'. IsAssembly: {isAssembly}");
            ModelDoc2 swDocToSave = swApp.GetOpenDocument(docPath) as ModelDoc2;
            bool wasOpenedForSaving = false;

            if (swDocToSave == null)
            {
                LogMessage($"Document '{docPath}' not open. Attempting to open for saving.");
                int errors = 0;
                swDocToSave = swApp.OpenDoc6(docPath, isAssembly ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART,
                                            (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref errors);
                if (swDocToSave == null)
                {
                    LogMessage($"Failed to open document '{docPath}' for saving. Error code: {errors}");
                    filesWithGeneralErrors.Add($"{docPath} (Failed to open for saving)");
                    generalErrorsCount++;
                    return;
                }
                wasOpenedForSaving = true;
                LogMessage($"Document '{docPath}' opened successfully for saving.");
            }
            else
            {
                swApp.ActivateDoc3(swDocToSave.GetTitle(), true, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, 0);
            }


            try
            {
                LogMessage($"Saving document: {docPath}");
                int saveErrors = 0;
                int saveWarnings = 0;
                bool saveSuccess = swDocToSave.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);

                if (saveSuccess)
                {
                    LogMessage($"Document '{docPath}' saved successfully. Errors: {saveErrors}, Warnings: {saveWarnings}");
                    if (pdmVault != null && PdmUtils.IsPathInVaultView(pdmVault, docPath))
                    {
                        IEdmFile5 pdmFile = null;
                        IEdmFolder5 pdmFolder = null;
                        pdmFile = pdmVault.GetFileFromPath(docPath, out pdmFolder);
                        if (pdmFile != null)
                        {
                            if (pdmFile.IsLocked)
                            {
                                var lockedByUser = pdmFile.LockedByUser;
                                string user = lockedByUser != null ? lockedByUser.Name : "";
                                string computer = pdmFile.LockedOnComputer;
                                if (string.Equals(user, GetCurrentPdmUserName(), StringComparison.OrdinalIgnoreCase))
                                {
                                    LogMessage($"File '{docPath}' is locked by current user. Attempting check-in.");
                                    CheckInDocumentIfAppropriate(pdmFile, "Saved by Roeslein Add-in after standards application.", Path.GetFileName(docPath));
                                }
                                else
                                {
                                    LogMessage($"File '{docPath}' is locked by '{user}', not current user. Cannot check-in.");
                                }
                            }
                            else
                            {
                                LogMessage($"File '{docPath}' is not locked in PDM. No check-in needed or possible by this operation post-save.");
                            }
                        }
                    }
                }
                else
                {
                    LogMessage($"Failed to save document '{docPath}'. Errors: {saveErrors}, Warnings: {saveWarnings}");
                    filesWithGeneralErrors.Add($"{docPath} (Save Failed - Errors: {saveErrors})");
                    generalErrorsCount++;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Exception during save for '{docPath}': {ex.Message}");
                filesWithGeneralErrors.Add($"{docPath} (Save Exception: {ex.Message})");
                generalErrorsCount++;
            }
            finally
            {
                if (wasOpenedForSaving && swDocToSave != null)
                {
                    LogMessage($"Closing document '{docPath}' that was opened for saving.");
                    swApp.CloseDoc(docPath);
                }
            }
        }


        private void CheckInDocumentIfAppropriate(IEdmFile5 pdmFile, string comment, string docNameForLogging)
        {
            if (pdmFile == null || pdmVault == null)
            {
                LogMessage($"Check-in skipped for '{docNameForLogging}': PDM file or vault object is null.");
                return;
            }

            try
            {
                if (pdmFile.IsLocked)
                {
                    var lockedByUser = pdmFile.LockedByUser;
                    string user = lockedByUser != null ? lockedByUser.Name : "";
                    string computer = pdmFile.LockedOnComputer;

                    if (string.Equals(user, GetCurrentPdmUserName(), StringComparison.OrdinalIgnoreCase))
                    {
                        LogMessage($"Attempting to check in file '{docNameForLogging}' (Path: {pdmFile.Name}). Comment: '{comment}'");
                        pdmFile.UnlockFile(GetParentWindowHandleForPDM().ToInt32(), comment); 
                        LogMessage($"File '{docNameForLogging}' checked in successfully.");
                        currentPartPdmStatus = "PDM: Checked in.";
                    }
                    else
                    {
                        LogMessage($"File '{docNameForLogging}' is locked by another user ('{user}'). Cannot check in.");
                        currentPartPdmStatus = $"PDM: Locked by {user}, cannot check in.";
                    }
                }
                else
                {
                    LogMessage($"File '{docNameForLogging}' is not locked. No check-in required by this operation.");
                    currentPartPdmStatus = "PDM: Not locked, no check-in needed by this operation.";
                }
            }
            catch (COMException ex)
            {
                LogMessage($"PDM Check-in COMException for '{docNameForLogging}': HRESULT 0x{ex.ErrorCode:X8} - {ex.Message}");
                currentPartPdmStatus = $"PDM: Check-in failed (COMException 0x{ex.ErrorCode:X8})";
                filesWithGeneralErrors.Add($"{docNameForLogging} (PDM Check-in Error: {ex.Message})");
                generalErrorsCount++;
            }
            catch (Exception ex)
            {
                LogMessage($"PDM Check-in Exception for '{docNameForLogging}': {ex.Message}");
                currentPartPdmStatus = $"PDM: Check-in failed (Exception)";
                filesWithGeneralErrors.Add($"{docNameForLogging} (PDM Check-in Exception: {ex.Message})");
                generalErrorsCount++;
            }
        }


        private bool IsToolboxPart(IModelDoc2 model)
        {
            if (model == null) return false;
            try
            {
                ModelDocExtension ext = model.Extension; 
                object isToolboxProp = null;
                try {
                    if (ext != null)
                    {
                        CustomPropertyManager cpm = ext.CustomPropertyManager[""];
                        if (cpm != null)
                        {
                            isToolboxProp = cpm.Get("IsToolboxPart");
                        }
                    }
                } catch (Exception exProp) {
                     LogMessage($"Exception accessing 'IsToolboxPart' custom property for '{model.GetTitle()}': {exProp.Message}");
                } 
                if (isToolboxProp != null && (isToolboxProp.ToString().Equals("1") || isToolboxProp.ToString().ToLower().Equals("true"))){
                   LogMessage($"Part '{model.GetTitle()}' identified as Toolbox part by custom property 'IsToolboxPart'.");
                   return true;
                }

                string path = model.GetPathName();
                if (!string.IsNullOrEmpty(path))
                {
                    if ((path.IndexOf("Toolbox", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         path.IndexOf("SOLIDWORKS Data", StringComparison.OrdinalIgnoreCase) >= 0 || 
                         path.IndexOf("Browser", StringComparison.OrdinalIgnoreCase) >= 0) 
                         && model.IsOpenedReadOnly()) 
                    {
                        LogMessage($"Part '{model.GetTitle()}' identified as LIKELY Toolbox part due to path ('{path}') and read-only status.");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error in IsToolboxPart for '{model?.GetTitle()}': {ex.Message}");
            }
            LogMessage($"Part '{model.GetTitle()}' is NOT considered a Toolbox part by current checks.");
            return false;
        }

        private IntPtr GetParentWindowHandleForPDM()
        {
            if (this.swApp != null)
            {
                try
                {
                    SolidWorks.Interop.sldworks.IFrame swFrame = this.swApp.Frame() as SolidWorks.Interop.sldworks.IFrame;
                    if (swFrame != null)
                    {
                        return new IntPtr(swFrame.GetHWnd());
                    }
                    LogMessage("GetParentWindowHandleForPDM: swApp.Frame() could not be cast to IFrame or was null.");
                }
                catch (Exception ex)
                {
                    LogMessage("Error getting SolidWorks frame window handle: " + ex.Message + ". Falling back.");
                }
            }
            try
            {
                return Process.GetCurrentProcess().MainWindowHandle; 
            }
            catch (Exception ex)
            {
                LogMessage("Error getting current process main window handle: " + ex.Message);
                return IntPtr.Zero;
            }
        }

        private string GetMaterialStatusMessage(string materialName, bool success)
        {
            return success ? $"Material '{materialName}' applied." : $"Failed to apply material '{materialName}'.";
        }

        private string GetCutlistStatusMessage(string message, bool success) 
        {
            return success ? $"Cutlist: {message} (Success)" : $"Cutlist: {message} (Failure)";
        }

        private string GetRawMaterialStatusMessage(string message, bool success) 
        {
            return success ? $"RawMaterial: {message} (Success)" : $"RawMaterial: {message} (Failure)";
        }

        private swDocumentTypes_e GetDocumentTypeFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return swDocumentTypes_e.swDocNONE;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".sldprt": return swDocumentTypes_e.swDocPART;
                case ".sldasm": return swDocumentTypes_e.swDocASSEMBLY;
                case ".slddrw": return swDocumentTypes_e.swDocDRAWING;
                default: return swDocumentTypes_e.swDocNONE;
            }
        }
    }
}