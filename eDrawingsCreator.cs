using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace RoesleinAddIn
{
    /// <summary>
    /// Represents an error that occurred during eDrawings creation
    /// </summary>
    public class eDrawingsError
    {
        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);
        public string ErrorMessage { get; set; }
        public string ErrorType { get; set; }
    }

    /// <summary>
    /// Represents a processed eDrawings file with its metadata
    /// </summary>
    public class eDrawingsProcessedFile
    {
        public string SourceFilePath { get; set; }
        public string eDrawingsFilePath { get; set; }
        public string SourceFileName => Path.GetFileName(SourceFilePath);
        public string eDrawingsFileName => Path.GetFileName(eDrawingsFilePath);
        public bool Success { get; set; }
        public string PartNumber { get; set; }
        public string Description { get; set; }
        public string Revision { get; set; }
        public string DocumentType { get; set; }
    }

    /// <summary>
    /// Creates eDrawings files from SolidWorks documents with PDM integration
    /// PDM automatically handles file detection and property inheritance
    /// </summary>
    public class eDrawingsCreator
    {
        private readonly ISldWorks _swApp;
        private readonly EPDM.Interop.epdm.IEdmVault5 _pdmVault;
        private readonly List<eDrawingsError> _errors;
        private readonly List<eDrawingsProcessedFile> _processedFiles;
        private ProgressForm _progressForm;
        private int _totalFilesToProcess;
        private int _currentFileIndex;
        private HashSet<string> _processedPartPaths;

        public eDrawingsCreator(ISldWorks swApp, EPDM.Interop.epdm.IEdmVault5 pdmVault = null)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            _pdmVault = pdmVault;
            _errors = new List<eDrawingsError>();
            _processedFiles = new List<eDrawingsProcessedFile>();
            _processedPartPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates eDrawings files based on the active document type
        /// </summary>
        public void CreateeDrawingsFromActiveDocument()
        {
            try
            {
                Logger.Info("eDrawingsCreator: Starting eDrawings creation process");
                
                // Log PDM vault status for debugging
                if (_pdmVault == null)
                {
                    Logger.Warning("eDrawingsCreator: PDM vault is null - PDM operations will not be available");
                }
                else if (!_pdmVault.IsLoggedIn)
                {
                    Logger.Warning($"eDrawingsCreator: PDM vault '{_pdmVault.Name}' is not logged in - PDM operations will not be available");
                }
                else
                {
                    Logger.Info($"eDrawingsCreator: PDM vault '{_pdmVault.Name}' is connected and logged in - PDM operations available");
                }
                
                ModelDoc2 activeDoc = _swApp.ActiveDoc as ModelDoc2;
                if (activeDoc == null)
                {
                    MessageBox.Show("No active document found.", "Create eDrawings", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string activeFilePath = activeDoc.GetPathName();
                if (string.IsNullOrEmpty(activeFilePath))
                {
                    MessageBox.Show("The active document must be saved before creating eDrawings.", 
                        "Create eDrawings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Clear previous results
                _errors.Clear();
                _processedFiles.Clear();
                
                // Initialize the set of processed part paths with case-insensitive comparison
                _processedPartPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Determine document type and process accordingly
                int docType = activeDoc.GetType();
                
                // Count total files to process for progress tracking
                _totalFilesToProcess = CountFilesToProcess(activeDoc, docType);
                _currentFileIndex = 0;

                // Show progress form
                _progressForm = new ProgressForm("Creating eDrawings Files");
                _progressForm.Show();
                _progressForm.UpdateProgress(0, "Initializing...", "");
                Application.DoEvents();

                switch (docType)
                {
                    case (int)swDocumentTypes_e.swDocPART:
                        ProcessPartDocument(activeDoc);
                        break;
                    case (int)swDocumentTypes_e.swDocASSEMBLY:
                        ProcessAssemblyDocument(activeDoc);
                        break;
                    case (int)swDocumentTypes_e.swDocDRAWING:
                        ProcessDrawingDocument(activeDoc);
                        break;
                    default:
                        AddError(activeFilePath, "Unsupported document type", "Document Type");
                        break;
                }

                // Process PDM operations for eDrawings files (non-SolidWorks files)
                ProcessPDMOperationsBatch();

                // Complete and show results
                CompleteProcessing();
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Unexpected error in main processing: {ex.Message}", ex);
                AddError("General", $"Unexpected error: {ex.Message}", "Processing Error");
                CompleteProcessing();
            }
            finally
            {
                // Ensure progress form is closed
                if (_progressForm != null && !_progressForm.IsDisposed)
                {
                    _progressForm.Invoke((MethodInvoker)delegate { _progressForm.Close(); });
                }
            }
        }

        /// <summary>
        /// Counts the total number of files that will be processed
        /// </summary>
        private int CountFilesToProcess(ModelDoc2 activeDoc, int docType)
        {
            int count = 0;
            
            switch (docType)
            {
                case (int)swDocumentTypes_e.swDocPART:
                    // Part: 1 drawing file (if found)
                    count = 1;
                    break;
                case (int)swDocumentTypes_e.swDocASSEMBLY:
                    // Assembly: 1 3D + 1 drawing + all child parts and their drawings
                    count = 2; // Assembly 3D + drawing
                    AssemblyDoc swAssembly = activeDoc as AssemblyDoc;
                    if (swAssembly != null)
                    {
                        count += CountAssemblyComponents(swAssembly);
                    }
                    break;
                case (int)swDocumentTypes_e.swDocDRAWING:
                    // Drawing: 1 drawing file + any referenced models
                    count = 1;
                    
                    // Check if the drawing references an assembly
                    try
                    {
                        DrawingDoc drawingDoc = activeDoc as DrawingDoc;
                        if (drawingDoc != null)
                        {
                            // Get the first view from the drawing
                            object firstViewObj = drawingDoc.GetFirstView();
                            SolidWorks.Interop.sldworks.View firstView = firstViewObj as SolidWorks.Interop.sldworks.View;
                            
                            // Skip the sheet view (first view is always the sheet itself)
                            if (firstView != null) 
                            {
                                firstView = firstView.GetNextView() as SolidWorks.Interop.sldworks.View;
                            }
                            
                            if (firstView != null)
                            {
                                string referencedPath = firstView.GetReferencedModelName();
                                if (!string.IsNullOrEmpty(referencedPath))
                                {
                                    string fileExtension = Path.GetExtension(referencedPath).ToLower();
                                    if (fileExtension == ".sldasm")
                                    {
                                        // Drawing references an assembly - count components too
                                        // Try to open the assembly to count components
                                        int errors = 0, warnings = 0;
                                        ModelDoc2 asmDoc = _swApp.OpenDoc6(referencedPath, (int)swDocumentTypes_e.swDocASSEMBLY, 
                                            (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);
                                            
                                        if (asmDoc != null)
                                        {
                                            count += 1; // Add 1 for assembly 3D eDrawings
                                            
                                            AssemblyDoc referencedAssembly = asmDoc as AssemblyDoc;
                                            if (referencedAssembly != null)
                                            {
                                                count += CountAssemblyComponents(referencedAssembly);
                                            }
                                            
                                            // Close the assembly
                                            _swApp.CloseDoc(asmDoc.GetTitle());
                                        }
                                    }
                                    else if (fileExtension == ".sldprt")
                                    {
                                        // Drawing references a part - add 1 for 3D eDrawings
                                        count += 1;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"eDrawingsCreator: Error counting components in referenced assembly: {ex.Message}");
                    }
                    break;
            }
            
            return Math.Max(count, 1); // Ensure at least 1 for progress calculation
        }

        /// <summary>
        /// Counts components in assembly recursively
        /// </summary>
        private int CountAssemblyComponents(AssemblyDoc swAssembly)
        {
            int count = 0;
            try
            {
                object[] components = (object[])swAssembly.GetComponents(true);
                if (components != null)
                {
                    foreach (Component2 comp in components)
                    {
                        if (comp != null && !comp.IsSuppressed() && !comp.IsVirtual)
                        {
                            ModelDoc2 compModel = (ModelDoc2)comp.GetModelDoc2();
                            if (compModel != null)
                            {
                                int compType = compModel.GetType();
                                if (compType == (int)swDocumentTypes_e.swDocPART)
                                {
                                    count += 1; // Part drawing
                                }
                                else if (compType == (int)swDocumentTypes_e.swDocASSEMBLY)
                                {
                                    count += 2; // Assembly 3D + drawing
                                    AssemblyDoc subAssembly = compModel as AssemblyDoc;
                                    if (subAssembly != null)
                                    {
                                        count += CountAssemblyComponents(subAssembly);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"eDrawingsCreator: Error counting assembly components: {ex.Message}");
            }
            return count;
        }

        /// <summary>
        /// Processes a part document
        /// </summary>
        private void ProcessPartDocument(ModelDoc2 partDoc)
        {
            string partPath = partDoc.GetPathName();
            Logger.Info($"eDrawingsCreator: Processing part document: {partPath}");
            
            // Add to processed list
            _processedPartPaths.Add(partPath);
            
            UpdateProgress($"Processing part: {Path.GetFileName(partPath)}", "Finding drawing file...");

            // Find and process the drawing for this part
            string drawingPath = FindDrawingForPart(partPath);
            if (!string.IsNullOrEmpty(drawingPath))
            {
                ProcessDrawingFile(drawingPath);
            }
            else
            {
                Logger.Warning($"eDrawingsCreator: No drawing file found for part: {partPath}");
                AddError(partPath, "Drawing file not found", "Missing Drawing");
                
                // Ask user if they want to create 3D eDrawings for the part even without a drawing
                var result = MessageBox.Show(
                    $"No drawing file was found for part '{Path.GetFileName(partPath)}'.\n\n" +
                    "Would you like to create a 3D eDrawings file for the part itself?",
                    "Drawing Not Found",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    Logger.Info($"eDrawingsCreator: User chose to create 3D eDrawings for part without drawing: {partPath}");
                    CreateeDrawings3D(partDoc);
                }
                else
                {
                    Logger.Info($"eDrawingsCreator: User chose not to create 3D eDrawings for part without drawing: {partPath}");
                }
            }
        }

        /// <summary>
        /// Processes an assembly document
        /// </summary>
        private void ProcessAssemblyDocument(ModelDoc2 assemblyDoc)
        {
            string assemblyPath = assemblyDoc.GetPathName();
            Logger.Info($"eDrawingsCreator: Processing assembly document: {assemblyPath}");
            
            // Add to processed list
            _processedPartPaths.Add(assemblyPath);
            
            UpdateProgress($"Processing assembly: {Path.GetFileName(assemblyPath)}", "Creating 3D eDrawings...");

            // 1. Create 3D eDrawings for the assembly
            CreateeDrawings3D(assemblyDoc);

            // 2. Find and process the drawing for this assembly
            string drawingPath = FindDrawingForAssembly(assemblyPath);
            if (!string.IsNullOrEmpty(drawingPath))
            {
                ProcessDrawingFile(drawingPath);
            }
            else
            {
                AddError(assemblyPath, "Assembly drawing file not found", "Missing Drawing");
            }

            // 3. Process all child components recursively
            AssemblyDoc swAssembly = assemblyDoc as AssemblyDoc;
            if (swAssembly != null)
            {
                ProcessAssemblyComponents(swAssembly);
            }
        }

        /// <summary>
        /// Processes all components in an assembly recursively
        /// </summary>
        private void ProcessAssemblyComponents(AssemblyDoc swAssembly)
        {
            try
            {
                object[] components = (object[])swAssembly.GetComponents(true);
                if (components == null) return;

                foreach (Component2 comp in components)
                {
                    if (comp == null || comp.IsSuppressed() || comp.IsVirtual) continue;

                    ModelDoc2 compModel = (ModelDoc2)comp.GetModelDoc2();
                    if (compModel == null) continue;

                    string compPath = compModel.GetPathName();
                    if (string.IsNullOrEmpty(compPath)) continue;
                    
                    // Toolbox/standard part filtering
                    if (IsToolboxOrStandardPart(compModel))
                    {
                        Logger.Info($"eDrawingsCreator: Skipping toolbox/standard part: {compPath}");
                        continue;
                    }
                    
                    // Check if this component has already been processed
                    if (_processedPartPaths.Contains(compPath))
                    {
                        Logger.Info($"eDrawingsCreator: Skipping already processed file: {compPath}");
                        continue;
                    }
                    
                    // Add to processed list before processing to prevent duplicate processing
                    _processedPartPaths.Add(compPath);

                    int compType = compModel.GetType();
                    
                    if (compType == (int)swDocumentTypes_e.swDocPART)
                    {
                        // Process part: find and create eDrawings for its drawing
                        UpdateProgress($"Processing component: {Path.GetFileName(compPath)}", "Finding drawing...");
                        
                        string drawingPath = FindDrawingForPart(compPath);
                        if (!string.IsNullOrEmpty(drawingPath))
                        {
                            ProcessDrawingFile(drawingPath);
                        }
                        else
                        {
                            AddError(compPath, "Drawing file not found for component", "Missing Drawing");
                        }
                    }
                    else if (compType == (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        // Process sub-assembly: create 3D eDrawings + drawing + recurse
                        UpdateProgress($"Processing sub-assembly: {Path.GetFileName(compPath)}", "Creating 3D eDrawings...");
                        
                        CreateeDrawings3D(compModel);
                        
                        string drawingPath = FindDrawingForAssembly(compPath);
                        if (!string.IsNullOrEmpty(drawingPath))
                        {
                            ProcessDrawingFile(drawingPath);
                        }
                        else
                        {
                            AddError(compPath, "Drawing file not found for sub-assembly", "Missing Drawing");
                        }

                        // Recurse into sub-assembly
                        AssemblyDoc subAssembly = compModel as AssemblyDoc;
                        if (subAssembly != null)
                        {
                            ProcessAssemblyComponents(subAssembly);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error processing assembly components: {ex.Message}", ex);
                AddError("Assembly Components", ex.Message, "Processing Error");
            }
        }

        /// <summary>
        /// Checks if a part is a SolidWorks Toolbox or standard library part
        /// </summary>
        private bool IsToolboxOrStandardPart(ModelDoc2 model)
        {
            if (model == null) return false;
            string path = model.GetPathName()?.ToLower() ?? "";
            // Exclude toolbox and known library folders
            if (path.Contains("\\toolbox\\") ||
                path.Contains("\\browser\\") ||
                path.Contains("\\generated parts\\") ||
                path.Contains("design library\\toolbox\\"))
                return true;
            // Optionally, check for custom property
            try
            {
                var propMgr = model.Extension.CustomPropertyManager[""];
                string val = "", resVal = "";
                bool wasRes = false, wasLinked = false;
                int ret = propMgr.Get6("IsToolboxPart", false, out val, out resVal, out wasRes, out wasLinked);
                if ((wasRes ? resVal : val).ToLower() == "yes")
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Processes a drawing document
        /// </summary>
        private void ProcessDrawingDocument(ModelDoc2 drawingDoc)
        {
            string drawingPath = drawingDoc.GetPathName();
            Logger.Info($"eDrawingsCreator: Processing drawing document: {drawingPath}");
            
            // Add to processed list
            _processedPartPaths.Add(drawingPath);
            
            UpdateProgress($"Processing drawing: {Path.GetFileName(drawingPath)}", "Creating eDrawings...");
            
            // First, create eDrawings for this drawing
            CreateeDrawingsDrawing(drawingDoc);
            
            // Check if this drawing references an assembly
            try
            {
                DrawingDoc swDrawingDoc = drawingDoc as DrawingDoc;
                if (swDrawingDoc != null)
                {
                    // Get the first view from the drawing
                    object firstViewObj = swDrawingDoc.GetFirstView();
                    SolidWorks.Interop.sldworks.View firstView = firstViewObj as SolidWorks.Interop.sldworks.View;
                    
                    // Skip the sheet view (first view is always the sheet itself)
                    if (firstView != null) 
                    {
                        firstView = firstView.GetNextView() as SolidWorks.Interop.sldworks.View;
                    }
                    
                    if (firstView != null)
                    {
                        string referencedPath = firstView.GetReferencedModelName();
                        if (!string.IsNullOrEmpty(referencedPath))
                        {
                            Logger.Info($"eDrawingsCreator: Drawing references document: {referencedPath}");
                            
                            // Check if the referenced file is an assembly
                            string fileExtension = Path.GetExtension(referencedPath).ToLower();
                            if (fileExtension == ".sldasm")
                            {
                                Logger.Info($"eDrawingsCreator: Drawing references an assembly. Processing assembly components...");
                                
                                // Save the current active document (the drawing)
                                ModelDoc2 previousActiveDoc = _swApp.ActiveDoc as ModelDoc2;
                                
                                // Try to find if the assembly is already open
                                ModelDoc2 asmDoc = null;
                                object[] openDocs = (object[])_swApp.GetDocuments();
                                if (openDocs != null)
                                {
                                    foreach (object docObj in openDocs)
                                    {
                                        ModelDoc2 doc = docObj as ModelDoc2;
                                        if (doc != null && string.Equals(doc.GetPathName(), referencedPath, StringComparison.OrdinalIgnoreCase))
                                        {
                                            asmDoc = doc;
                                            break;
                                        }
                                    }
                                }
                                bool openedHere = false;
                                int errors = 0, warnings = 0;
                                if (asmDoc == null)
                                {
                                    asmDoc = _swApp.OpenDoc6(referencedPath, (int)swDocumentTypes_e.swDocASSEMBLY, 
                                        (int)swOpenDocOptions_e.swOpenDocOptions_LoadModel, "", ref errors, ref warnings);
                                    openedHere = true;
                                }
                                // Make the assembly the active document
                                if (asmDoc != null)
                                {
                                    _swApp.ActivateDoc2(asmDoc.GetTitle(), false, 0);
                                    // Create 3D eDrawings for the assembly
                                    CreateeDrawings3D(asmDoc);
                                    // Now add to processed list
                                    _processedPartPaths.Add(referencedPath);
                                    // Process all components in the assembly
                                    AssemblyDoc swAssembly = asmDoc as AssemblyDoc;
                                    if (swAssembly != null)
                                    {
                                        ProcessAssemblyComponents(swAssembly);
                                    }
                                    // Restore the previous active document (the drawing)
                                    if (previousActiveDoc != null)
                                        _swApp.ActivateDoc2(previousActiveDoc.GetTitle(), false, 0);
                                    // Close the assembly if we opened it here
                                    if (openedHere)
                                        _swApp.CloseDoc(asmDoc.GetTitle());
                                }
                                else
                                {
                                    Logger.Warning($"eDrawingsCreator: Failed to open referenced assembly: {referencedPath}. Error: {errors}");
                                    AddError(referencedPath, $"Failed to open referenced assembly (Error: {errors})", "Open Error");
                                }
                            }
                            else if (fileExtension == ".sldprt")
                            {
                                Logger.Info($"eDrawingsCreator: Drawing references a part. Creating 3D eDrawings...");
                                
                                // The part is already processed through the drawing, but we might want to create 3D eDrawings too
                                if (!_processedPartPaths.Contains(referencedPath))
                                {
                                    _processedPartPaths.Add(referencedPath);
                                    
                                    // Open the part
                                    int errors = 0, warnings = 0;
                                    ModelDoc2 partDoc = _swApp.OpenDoc6(referencedPath, (int)swDocumentTypes_e.swDocPART, 
                                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);
                                        
                                    if (partDoc != null)
                                    {
                                        // Create 3D eDrawings
                                        CreateeDrawings3D(partDoc);
                                        
                                        // Close the part
                                        _swApp.CloseDoc(partDoc.GetTitle());
                                    }
                                    else
                                    {
                                        Logger.Warning($"eDrawingsCreator: Failed to open referenced part: {referencedPath}. Error: {errors}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error processing drawing document references: {ex.Message}", ex);
                AddError(drawingPath, $"Error processing drawing references: {ex.Message}", "Processing Error");
            }
        }

        /// <summary>
        /// Processes a drawing file by path
        /// </summary>
        private void ProcessDrawingFile(string drawingPath)
        {
            try
            {
                if (!File.Exists(drawingPath))
                {
                    AddError(drawingPath, "Drawing file does not exist", "File Not Found");
                    return;
                }
                
                // Check if this drawing has already been processed
                if (_processedPartPaths.Contains(drawingPath))
                {
                    Logger.Info($"eDrawingsCreator: Skipping already processed drawing file: {drawingPath}");
                    return;
                }
                
                // Add to processed list before processing
                _processedPartPaths.Add(drawingPath);

                UpdateProgress($"Processing drawing: {Path.GetFileName(drawingPath)}", "Opening file...");

                // Open the drawing file
                int errors = 0, warnings = 0;
                ModelDoc2 drawingDoc = _swApp.OpenDoc6(drawingPath, (int)swDocumentTypes_e.swDocDRAWING, 
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);

                if (drawingDoc != null)
                {
                    CreateeDrawingsDrawing(drawingDoc);
                    
                    // Close the drawing
                    _swApp.CloseDoc(drawingDoc.GetTitle());
                }
                else
                {
                    AddError(drawingPath, $"Failed to open drawing file (Error: {errors})", "Open Error");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error processing drawing file {drawingPath}: {ex.Message}", ex);
                AddError(drawingPath, ex.Message, "Processing Error");
            }
        }

        /// <summary>
        /// Creates a 3D eDrawings file from a part or assembly document
        /// </summary>
        private void CreateeDrawings3D(ModelDoc2 swModel)
        {
            try
            {
                string sourcePath = swModel.GetPathName();
                string fileName = Path.GetFileNameWithoutExtension(sourcePath);
                string sourceDir = Path.GetDirectoryName(sourcePath);
                string eDrawingsDir = Path.Combine(sourceDir, "eDrawings");
                
                // Ensure eDrawings directory exists
                if (!Directory.Exists(eDrawingsDir))
                {
                    Directory.CreateDirectory(eDrawingsDir);
                }

                // Get properties from source document
                string partNumber = "", description = "", revision = "";
                GetPropertiesFromDocument(swModel, out partNumber, out description, out revision);

                // Determine file extension based on document type
                string extension = swModel.GetType() == (int)swDocumentTypes_e.swDocPART ? ".eprt" : ".easm";
                
                // Use original filename (no variable replacement in filename - properties go in data card)
                string eDrawingsPath = Path.Combine(eDrawingsDir, fileName + extension);

                // Check if eDrawings file already exists and compare revisions
                if (ShouldSkipCreation(eDrawingsPath, revision))
                {
                    Logger.Info($"eDrawingsCreator: Skipping creation of {eDrawingsPath} - revision matches existing file");
                    UpdateProgress($"Skipping: {fileName + extension}", "Revision matches existing file");
                    return;
                }

                // Check if we need to check out existing file for overwrite
                bool needsCheckout = false;
                EPDM.Interop.epdm.IEdmFile5 existingEdmFile = null;
                EPDM.Interop.epdm.IEdmFolder5 existingEdmFolder = null;
                
                if (_pdmVault != null && _pdmVault.IsLoggedIn)
                {
                    existingEdmFile = _pdmVault.GetFileFromPath(eDrawingsPath, out existingEdmFolder);
                    if (existingEdmFile != null)
                    {
                        needsCheckout = true;
                        Logger.Info($"eDrawingsCreator: Existing eDrawings file found in PDM, will check out for overwrite: {eDrawingsPath}");
                    }
                }

                // Check out existing file if needed
                if (needsCheckout && existingEdmFile != null)
                {
                    try
                    {
                        if (!existingEdmFile.IsLocked)
                        {
                            Logger.Info($"eDrawingsCreator: Checking out existing eDrawings file for overwrite: {eDrawingsPath}");
                            existingEdmFile.LockFile(existingEdmFolder.ID, 0, (int)EPDM.Interop.epdm.EdmLockFlag.EdmLock_Simple);
                        }
                        else
                        {
                            Logger.Warning($"eDrawingsCreator: Existing eDrawings file is already checked out by another user: {eDrawingsPath}");
                            AddError(sourcePath, "Existing eDrawings file is checked out by another user", "PDM Conflict");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"eDrawingsCreator: Failed to check out existing eDrawings file: {ex.Message}");
                        AddError(sourcePath, $"Failed to check out existing eDrawings file: {ex.Message}", "PDM Error");
                        return;
                    }
                }

                UpdateProgress($"Creating 3D eDrawings: {fileName + extension}", "Configuring export settings...");

                // Configure eDrawings export settings
                ConfigureeDrawingsSettings();

                // Export to eDrawings
                ModelDocExtension swExtension = swModel.Extension;
                int errors = 0, warnings = 0;
                bool success = swExtension.SaveAs(eDrawingsPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, 
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);

                if (success)
                {
                    // Determine document type based on extension
                    string documentType = extension == ".eprt" ? "Part" : "Assembly";
                    
                    // Add to processed files list - PDM operations will be handled in batch
                    _processedFiles.Add(new eDrawingsProcessedFile
                    {
                        SourceFilePath = sourcePath,
                        eDrawingsFilePath = eDrawingsPath,
                        DocumentType = documentType,
                        Success = true,
                        PartNumber = partNumber,
                        Description = description,
                        Revision = revision
                    });

                    Logger.Info($"eDrawingsCreator: Successfully created 3D eDrawings: {eDrawingsPath}");
                    Logger.Info($"eDrawingsCreator: Will set PDM variables - Part Number: '{partNumber}', Description: '{description}', Revision: '{revision}'");
                }
                else
                {
                    string errorMsg = $"Failed to create 3D eDrawings (Errors: {errors}, Warnings: {warnings})";
                    Logger.Error($"eDrawingsCreator: {errorMsg} for {sourcePath}");
                    AddError(sourcePath, errorMsg, "Export Error");
                    
                    // If we checked out the file but creation failed, check it back in
                    if (needsCheckout && existingEdmFile != null && existingEdmFile.IsLocked)
                    {
                        try
                        {
                            Logger.Info($"eDrawingsCreator: Checking in eDrawings file after failed overwrite: {eDrawingsPath}");
                            existingEdmFile.UnlockFile(0, "Failed to overwrite eDrawings file");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"eDrawingsCreator: Failed to check in eDrawings file after failed overwrite: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error creating 3D eDrawings for {swModel.GetPathName()}: {ex.Message}", ex);
                AddError(swModel.GetPathName(), ex.Message, "Export Error");
            }
        }

        /// <summary>
        /// Creates a drawing eDrawings file from a drawing document
        /// </summary>
        private void CreateeDrawingsDrawing(ModelDoc2 drawingDoc)
        {
            try
            {
                string sourcePath = drawingDoc.GetPathName();
                string fileName = Path.GetFileNameWithoutExtension(sourcePath);
                string sourceDir = Path.GetDirectoryName(sourcePath);
                string eDrawingsDir = Path.Combine(sourceDir, "eDrawings");
                
                // Ensure eDrawings directory exists
                if (!Directory.Exists(eDrawingsDir))
                {
                    Directory.CreateDirectory(eDrawingsDir);
                }

                // Get properties from source document
                string partNumber = "", description = "", revision = "";
                GetPropertiesFromDocument(drawingDoc, out partNumber, out description, out revision);

                // Use original filename (no variable replacement in filename - properties go in data card)
                string eDrawingsPath = Path.Combine(eDrawingsDir, fileName + ".edrw");

                // Check if eDrawings file already exists and compare revisions
                if (ShouldSkipCreation(eDrawingsPath, revision))
                {
                    Logger.Info($"eDrawingsCreator: Skipping creation of {eDrawingsPath} - revision matches existing file");
                    UpdateProgress($"Skipping: {fileName}.edrw", "Revision matches existing file");
                    return;
                }

                // Check if we need to check out existing file for overwrite
                bool needsCheckout = false;
                EPDM.Interop.epdm.IEdmFile5 existingEdmFile = null;
                EPDM.Interop.epdm.IEdmFolder5 existingEdmFolder = null;
                
                if (_pdmVault != null && _pdmVault.IsLoggedIn)
                {
                    existingEdmFile = _pdmVault.GetFileFromPath(eDrawingsPath, out existingEdmFolder);
                    if (existingEdmFile != null)
                    {
                        needsCheckout = true;
                        Logger.Info($"eDrawingsCreator: Existing eDrawings file found in PDM, will check out for overwrite: {eDrawingsPath}");
                    }
                }

                // Check out existing file if needed
                if (needsCheckout && existingEdmFile != null)
                {
                    try
                    {
                        if (!existingEdmFile.IsLocked)
                        {
                            Logger.Info($"eDrawingsCreator: Checking out existing eDrawings file for overwrite: {eDrawingsPath}");
                            existingEdmFile.LockFile(existingEdmFolder.ID, 0, (int)EPDM.Interop.epdm.EdmLockFlag.EdmLock_Simple);
                        }
                        else
                        {
                            Logger.Warning($"eDrawingsCreator: Existing eDrawings file is already checked out by another user: {eDrawingsPath}");
                            AddError(sourcePath, "Existing eDrawings file is checked out by another user", "PDM Conflict");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"eDrawingsCreator: Failed to check out existing eDrawings file: {ex.Message}");
                        AddError(sourcePath, $"Failed to check out existing eDrawings file: {ex.Message}", "PDM Error");
                        return;
                    }
                }

                UpdateProgress($"Creating drawing eDrawings: {fileName}.edrw", "Configuring export settings...");

                // Configure eDrawings export settings
                ConfigureeDrawingsSettings();

                // Export to eDrawings
                ModelDocExtension swExtension = drawingDoc.Extension;
                int errors = 0, warnings = 0;
                bool success = swExtension.SaveAs(eDrawingsPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, 
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);

                if (success)
                {
                    // Add to processed files list - PDM operations will be handled in batch
                    _processedFiles.Add(new eDrawingsProcessedFile
                    {
                        SourceFilePath = sourcePath,
                        eDrawingsFilePath = eDrawingsPath,
                        DocumentType = "Drawing",
                        Success = true,
                        PartNumber = partNumber,
                        Description = description,
                        Revision = revision
                    });

                    Logger.Info($"eDrawingsCreator: Successfully created drawing eDrawings: {eDrawingsPath}");
                    Logger.Info($"eDrawingsCreator: Will set PDM variables - Part Number: '{partNumber}', Description: '{description}', Revision: '{revision}'");
                }
                else
                {
                    string errorMsg = $"Failed to create drawing eDrawings (Errors: {errors}, Warnings: {warnings})";
                    Logger.Error($"eDrawingsCreator: {errorMsg} for {sourcePath}");
                    AddError(sourcePath, errorMsg, "Export Error");
                    
                    // If we checked out the file but creation failed, check it back in
                    if (needsCheckout && existingEdmFile != null && existingEdmFile.IsLocked)
                    {
                        try
                        {
                            Logger.Info($"eDrawingsCreator: Checking in eDrawings file after failed overwrite: {eDrawingsPath}");
                            existingEdmFile.UnlockFile(0, "Failed to overwrite eDrawings file");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"eDrawingsCreator: Failed to check in eDrawings file after failed overwrite: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error creating drawing eDrawings for {drawingDoc.GetPathName()}: {ex.Message}", ex);
                AddError(drawingDoc.GetPathName(), ex.Message, "Export Error");
            }
        }

        /// <summary>
        /// Checks if eDrawings creation should be skipped based on revision comparison
        /// </summary>
        private bool ShouldSkipCreation(string eDrawingsPath, string sourceRevision)
        {
            try
            {
                if (_pdmVault == null || !_pdmVault.IsLoggedIn)
                {
                    Logger.Info("eDrawingsCreator: PDM vault not available, cannot check existing revisions");
                    return false; // Create the file if we can't check PDM
                }

                if (string.IsNullOrEmpty(sourceRevision))
                {
                    Logger.Info("eDrawingsCreator: Source revision is empty, will create eDrawings file");
                    return false; // Create the file if source has no revision
                }

                EPDM.Interop.epdm.IEdmFolder5 folder;
                EPDM.Interop.epdm.IEdmFile5 existingFile = _pdmVault.GetFileFromPath(eDrawingsPath, out folder);
                
                if (existingFile == null)
                {
                    Logger.Info($"eDrawingsCreator: eDrawings file does not exist in PDM, will create: {eDrawingsPath}");
                    return false; // File doesn't exist, create it
                }

                // Get the revision from the existing eDrawings file
                string existingRevision = "";
                try
                {
                    EPDM.Interop.epdm.IEdmEnumeratorVariable5 enumVar = existingFile.GetEnumeratorVariable();
                    object revisionValue = null;
                    bool success = enumVar.GetVar("Revision", "@", out revisionValue);
                    if (success && revisionValue != null)
                    {
                        existingRevision = revisionValue.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"eDrawingsCreator: Could not get revision from existing eDrawings file: {ex.Message}");
                    return false; // If we can't get the revision, create the file
                }

                Logger.Info($"eDrawingsCreator: Comparing revisions - Source: '{sourceRevision}', Existing eDrawings: '{existingRevision}'");

                // Compare revisions
                if (string.Equals(sourceRevision, existingRevision, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info($"eDrawingsCreator: Revisions match, skipping creation of {eDrawingsPath}");
                    return true; // Skip creation
                }
                else
                {
                    Logger.Info($"eDrawingsCreator: Revisions differ, will overwrite existing eDrawings file: {eDrawingsPath}");
                    return false; // Create/overwrite the file
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error checking if creation should be skipped: {ex.Message}");
                return false; // If there's an error, create the file
            }
        }

        /// <summary>
        /// Gets properties from a SolidWorks document using PDM API when available
        /// For drawing files, gets properties from the referenced part/assembly
        /// </summary>
        private void GetPropertiesFromDocument(ModelDoc2 sourceDoc, out string partNumber, out string description, out string revision)
        {
            partNumber = "";
            description = "";
            revision = "";

            try
            {
                string sourcePath = sourceDoc.GetPathName();
                ModelDoc2 propertySourceDoc = sourceDoc;
                
                // If this is a drawing, we need to get properties from the referenced part/assembly
                if (sourceDoc.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                {
                    Logger.Info($"eDrawingsCreator: Source is a drawing file, finding referenced part/assembly: {sourcePath}");
                    
                    // Get the referenced document from the drawing using the correct SolidWorks API
                    DrawingDoc drawingDoc = sourceDoc as DrawingDoc;
                    if (drawingDoc != null)
                    {
                        try
                        {
                            // Get the first view from the drawing
                            object firstViewObj = drawingDoc.GetFirstView();
                            SolidWorks.Interop.sldworks.View firstView = firstViewObj as SolidWorks.Interop.sldworks.View;
                            
                            // Skip the sheet view (first view is always the sheet itself)
                            if (firstView != null) 
                            {
                                firstView = firstView.GetNextView() as SolidWorks.Interop.sldworks.View;
                            }
                            
                            if (firstView != null)
                            {
                                string referencedPath = firstView.GetReferencedModelName();
                                if (!string.IsNullOrEmpty(referencedPath))
                                {
                                    Logger.Info($"eDrawingsCreator: Found referenced document: {referencedPath}");
                                    
                                    // Try to get properties from the referenced file via PDM
                                    if (_pdmVault != null && _pdmVault.IsLoggedIn)
                                    {
                                        try
                                        {
                                            EPDM.Interop.epdm.IEdmFolder5 refFolder;
                                            EPDM.Interop.epdm.IEdmFile5 refFile = _pdmVault.GetFileFromPath(referencedPath, out refFolder);
                                            
                                            if (refFile != null)
                                            {
                                                Logger.Info($"eDrawingsCreator: Getting properties from PDM for referenced file: {referencedPath}");
                                                
                                                // Get properties from PDM
                                                EPDM.Interop.epdm.IEdmEnumeratorVariable5 refEnumVar = refFile.GetEnumeratorVariable();
                                                if (refEnumVar != null)
                                                {
                                                    object propValue;
                                                    
                                                    // Get Part Number
                                                    bool success = refEnumVar.GetVar("Part Number", "@", out propValue);
                                                    if (success && propValue != null)
                                                    {
                                                        partNumber = propValue.ToString();
                                                    }
                                                    
                                                    // Get Description
                                                    success = refEnumVar.GetVar("Description", "@", out propValue);
                                                    if (success && propValue != null)
                                                    {
                                                        description = propValue.ToString();
                                                    }
                                                    
                                                    // Get Revision
                                                    success = refEnumVar.GetVar("Revision", "@", out propValue);
                                                    if (success && propValue != null)
                                                    {
                                                        revision = propValue.ToString();
                                                    }
                                                    
                                                    Logger.Info($"eDrawingsCreator: Retrieved properties from referenced file PDM - Part Number: '{partNumber}', Description: '{description}', Revision: '{revision}'");
                                                    return; // Successfully got properties from referenced file
                                                }
                                            }
                                            else
                                            {
                                                Logger.Warning($"eDrawingsCreator: Referenced file not found in PDM: {referencedPath}");
                                            }
                                        }
                                        catch (Exception refEx)
                                        {
                                            Logger.Warning($"eDrawingsCreator: Error getting properties from referenced file PDM: {refEx.Message}");
                                        }
                                    }
                                    
                                    // Fallback: try to open the referenced document and get properties
                                    try
                                    {
                                        Logger.Info($"eDrawingsCreator: Attempting to open referenced document for properties: {referencedPath}");
                                        int errors = 0, warnings = 0;
                                        
                                        // Determine document type from extension
                                        int docType = (int)swDocumentTypes_e.swDocPART;
                                        string ext = Path.GetExtension(referencedPath).ToLower();
                                        if (ext == ".sldasm")
                                            docType = (int)swDocumentTypes_e.swDocASSEMBLY;
                                        
                                        ModelDoc2 refDoc = _swApp.OpenDoc6(referencedPath, docType, 
                                            (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);
                                        
                                        if (refDoc != null)
                                        {
                                            propertySourceDoc = refDoc;
                                            Logger.Info($"eDrawingsCreator: Successfully opened referenced document for properties");
                                            
                                            // Get properties from the opened referenced document
                                            CustomPropertyManager refCustomPropMgr = refDoc.Extension.get_CustomPropertyManager("");
                                            partNumber = GetCustomProperty(refCustomPropMgr, "Part Number");
                                            description = GetCustomProperty(refCustomPropMgr, "Description");
                                            revision = GetCustomProperty(refCustomPropMgr, "Revision");
                                            
                                            Logger.Info($"eDrawingsCreator: Retrieved properties from opened referenced document - Part Number: '{partNumber}', Description: '{description}', Revision: '{revision}'");
                                            
                                            // Close the referenced document
                                            _swApp.CloseDoc(refDoc.GetTitle());
                                            return;
                                        }
                                        else
                                        {
                                            Logger.Warning($"eDrawingsCreator: Failed to open referenced document: {referencedPath}");
                                        }
                                    }
                                    catch (Exception openEx)
                                    {
                                        Logger.Warning($"eDrawingsCreator: Error opening referenced document: {openEx.Message}");
                                    }
                                }
                                else
                                {
                                    Logger.Warning("eDrawingsCreator: No referenced model path found in first view");
                                }
                            }
                            else
                            {
                                Logger.Warning("eDrawingsCreator: No model views found in drawing");
                            }
                        }
                        catch (Exception viewEx)
                        {
                            Logger.Warning($"eDrawingsCreator: Error getting views from drawing: {viewEx.Message}");
                        }
                    }
                }
                
                // First try to get properties from PDM if available (more reliable)
                if (_pdmVault != null && _pdmVault.IsLoggedIn && !string.IsNullOrEmpty(sourcePath))
                {
                    try
                    {
                        EPDM.Interop.epdm.IEdmFolder5 sourceFolder;
                        EPDM.Interop.epdm.IEdmFile5 sourceFile = _pdmVault.GetFileFromPath(sourcePath, out sourceFolder);
                        
                        if (sourceFile != null)
                        {
                            Logger.Info($"eDrawingsCreator: Getting properties from PDM for source file: {sourcePath}");
                            
                            // Get properties from PDM
                            EPDM.Interop.epdm.IEdmEnumeratorVariable5 sourceEnumVar = sourceFile.GetEnumeratorVariable();
                            if (sourceEnumVar != null)
                            {
                                object propValue;
                                
                                // Get Part Number
                                bool success = sourceEnumVar.GetVar("Part Number", "@", out propValue);
                                if (success && propValue != null)
                                {
                                    partNumber = propValue.ToString();
                                }
                                
                                // Get Description
                                success = sourceEnumVar.GetVar("Description", "@", out propValue);
                                if (success && propValue != null)
                                {
                                    description = propValue.ToString();
                                }
                                
                                // Get Revision
                                success = sourceEnumVar.GetVar("Revision", "@", out propValue);
                                if (success && propValue != null)
                                {
                                    revision = propValue.ToString();
                                }
                                
                                Logger.Info($"eDrawingsCreator: Retrieved properties from PDM - Part Number: '{partNumber}', Description: '{description}', Revision: '{revision}'");
                                return; // Successfully got properties from PDM
                            }
                        }
                        else
                        {
                            Logger.Info($"eDrawingsCreator: Source file not found in PDM, falling back to SolidWorks properties: {sourcePath}");
                        }
                    }
                    catch (Exception pdmEx)
                    {
                        Logger.Warning($"eDrawingsCreator: Error getting properties from PDM, falling back to SolidWorks properties: {pdmEx.Message}");
                    }
                }

                // Fallback to getting properties from SolidWorks document
                Logger.Info("eDrawingsCreator: Getting properties from SolidWorks document");
                CustomPropertyManager customPropMgr = propertySourceDoc.Extension.get_CustomPropertyManager("");
                
                partNumber = GetCustomProperty(customPropMgr, "Part Number");
                description = GetCustomProperty(customPropMgr, "Description");
                revision = GetCustomProperty(customPropMgr, "Revision");

                Logger.Info($"eDrawingsCreator: Retrieved properties from SolidWorks - Part Number: '{partNumber}', Description: '{description}', Revision: '{revision}'");
            }
            catch (Exception ex)
            {
                Logger.Warning($"eDrawingsCreator: Error getting properties from document {sourceDoc.GetPathName()}: {ex.Message}");
            }
        }

        /// <summary>
        /// Configures SolidWorks eDrawings export settings
        /// </summary>
        private void ConfigureeDrawingsSettings()
        {
            try
            {
                // Configure eDrawings export preferences
                _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swEDrawingsOkayToMeasure, true);
                _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swEDrawingsExportSTLOkay, false);
                _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swEDrawingsSaveShadedDataInDrawings, true);
                _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swEDrawingsSaveBOM, true);
                _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swEDrawingsSaveAnimationOkay, false);
            }
            catch (Exception ex)
            {
                Logger.Warning($"eDrawingsCreator: Error configuring eDrawings settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds the drawing file for a given part file
        /// </summary>
        private string FindDrawingForPart(string partPath)
        {
            try
            {
                string directory = Path.GetDirectoryName(partPath);
                string fileName = Path.GetFileNameWithoutExtension(partPath);
                
                Logger.Info($"eDrawingsCreator: Looking for drawing file for part: {fileName}");
                Logger.Info($"eDrawingsCreator: Searching in directory: {directory}");
                
                // List all files in the directory for debugging
                try
                {
                    string[] allFiles = Directory.GetFiles(directory, "*.*");
                    Logger.Info($"eDrawingsCreator: Found {allFiles.Length} files in directory:");
                    foreach (string file in allFiles)
                    {
                        string fileNameOnly = Path.GetFileName(file);
                        Logger.Info($"  - {fileNameOnly}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"eDrawingsCreator: Could not list files in directory: {ex.Message}");
                }
                
                // Try common drawing file naming patterns
                string[] possibleNames = {
                    fileName + ".slddrw",
                    fileName + ".SLDDRW",
                    fileName + ".drw",
                    fileName + ".DRW"
                };

                Logger.Info($"eDrawingsCreator: Looking for these drawing file names:");
                foreach (string name in possibleNames)
                {
                    Logger.Info($"  - {name}");
                    string drawingPath = Path.Combine(directory, name);
                    if (File.Exists(drawingPath))
                    {
                        Logger.Info($"eDrawingsCreator: Found drawing file: {drawingPath}");
                        return drawingPath;
                    }
                }

                // Also check in a "Drawings" subdirectory
                string drawingsDir = Path.Combine(directory, "Drawings");
                if (Directory.Exists(drawingsDir))
                {
                    Logger.Info($"eDrawingsCreator: Also checking in Drawings subdirectory: {drawingsDir}");
                    foreach (string name in possibleNames)
                    {
                        string drawingPath = Path.Combine(drawingsDir, name);
                        if (File.Exists(drawingPath))
                        {
                            Logger.Info($"eDrawingsCreator: Found drawing file in Drawings subdirectory: {drawingPath}");
                            return drawingPath;
                        }
                    }
                }
                
                Logger.Warning($"eDrawingsCreator: No drawing file found for part: {fileName}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"eDrawingsCreator: Error finding drawing for part {partPath}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Finds the drawing file for a given assembly file
        /// </summary>
        private string FindDrawingForAssembly(string assemblyPath)
        {
            // Use the same logic as parts - assemblies and parts follow similar naming conventions
            return FindDrawingForPart(assemblyPath);
        }

        /// <summary>
        /// Gets a custom property value from the property manager
        /// </summary>
        private string GetCustomProperty(CustomPropertyManager propMgr, string propertyName)
        {
            try
            {
                string value = "";
                string resolvedValue = "";
                bool wasResolved = false;
                
                int result = propMgr.Get5(propertyName, false, out value, out resolvedValue, out wasResolved);
                
                return wasResolved ? resolvedValue : value;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Updates the progress form
        /// </summary>
        private void UpdateProgress(string status, string detail)
        {
            if (_progressForm != null)
            {
                _currentFileIndex++;
                int percentage = (int)((double)_currentFileIndex / _totalFilesToProcess * 100);
                _progressForm.UpdateProgress(percentage, status, detail);
                Application.DoEvents();
                
                // Check for cancellation
                if (_progressForm.IsCancelled)
                {
                    throw new OperationCanceledException("User cancelled the operation");
                }
            }
        }

        /// <summary>
        /// Adds an error to the error list
        /// </summary>
        private void AddError(string filePath, string errorMessage, string errorType)
        {
            _errors.Add(new eDrawingsError
            {
                FilePath = filePath,
                ErrorMessage = errorMessage,
                ErrorType = errorType
            });
            
            Logger.Error($"eDrawingsCreator: {errorType} - {filePath}: {errorMessage}");
        }

        /// <summary>
        /// Completes the process and shows results
        /// </summary>
        private void CompleteProcessing()
        {
            try
            {
                if (_progressForm != null)
                {
                    _progressForm.Complete("eDrawings creation completed!");
                    _progressForm = null;
                }

                // Show results summary
                ShowResultsSummary();
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error completing process: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Shows a summary of the results
        /// </summary>
        private void ShowResultsSummary()
        {
            try
            {
                int successCount = _processedFiles.Count(f => f.Success);
                int errorCount = _errors.Count;
                
                string title = "eDrawings Creation Results";
                string summary = $"eDrawings Creation Summary:\r\n\r\n";
                summary += $"Files processed successfully: {successCount}\r\n";
                summary += $"Errors encountered: {errorCount}\r\n\r\n";

                if (successCount > 0)
                {
                    summary += "Successfully created eDrawings files:\r\n";
                    foreach (var file in _processedFiles.Where(f => f.Success))
                    {
                        summary += $"• {Path.GetFileName(file.eDrawingsFilePath)}\r\n";
                        if (!string.IsNullOrEmpty(file.PartNumber))
                        {
                            summary += $"  Part Number: {file.PartNumber}\r\n";
                        }
                        if (!string.IsNullOrEmpty(file.Description))
                        {
                            summary += $"  Description: {file.Description}\r\n";
                        }
                        if (!string.IsNullOrEmpty(file.Revision))
                        {
                            summary += $"  Revision: {file.Revision}\r\n";
                        }
                        summary += "\r\n";
                    }
                }

                if (errorCount > 0)
                {
                    summary += "Errors:\r\n";
                    summary += new string('=', 40) + "\r\n";
                    foreach (var error in _errors)
                    {
                        summary += $"• {error.FileName}: {error.ErrorMessage}\r\n";
                    }
                }

                // Show results in a dialog
                using (var resultForm = new Form())
                {
                    resultForm.Text = title;
                    resultForm.Width = 700;
                    resultForm.Height = 600;
                    resultForm.StartPosition = FormStartPosition.CenterScreen;
                    resultForm.MinimizeBox = false;
                    resultForm.MaximizeBox = false;
                    resultForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    
                    var textBox = new TextBox
                    {
                        Multiline = true,
                        ReadOnly = true,
                        ScrollBars = ScrollBars.Vertical,
                        Font = new System.Drawing.Font("Consolas", 9F),
                        Dock = DockStyle.Fill,
                        Text = summary
                    };
                    
                    var buttonPanel = new Panel
                    {
                        Dock = DockStyle.Bottom,
                        Height = 50
                    };
                    
                    var okButton = new Button
                    {
                        Text = "OK",
                        DialogResult = DialogResult.OK,
                        Location = new System.Drawing.Point(600, 15),
                        Size = new System.Drawing.Size(80, 25)
                    };
                    
                    buttonPanel.Controls.Add(okButton);
                    resultForm.Controls.Add(textBox);
                    resultForm.Controls.Add(buttonPanel);
                    resultForm.AcceptButton = okButton;
                    
                    resultForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error showing results summary: {ex.Message}", ex);
                MessageBox.Show($"eDrawings creation completed with {_processedFiles.Count} files processed and {_errors.Count} errors.", 
                    "eDrawings Creation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Processes PDM operations for all eDrawings files in batch
        /// Implements proper PDM variable mapping like PDM Tasks do
        /// </summary>
        private void ProcessPDMOperationsBatch()
        {
            if (_pdmVault == null || !_pdmVault.IsLoggedIn)
            {
                Logger.Info("eDrawingsCreator: PDM vault not available, eDrawings files created in filesystem only");
                return;
            }

            if (_processedFiles == null || _processedFiles.Count == 0)
            {
                Logger.Info("eDrawingsCreator: No processed files for PDM operations");
                return;
            }

            Logger.Info($"eDrawingsCreator: Processing PDM operations for {_processedFiles.Count} eDrawings files");
            Logger.Info($"eDrawingsCreator: Implementing PDM variable mapping like PDM Tasks do");
            UpdateProgress("PDM Processing...", "Adding files to PDM and setting variables");

            foreach (var processedFile in _processedFiles.Where(f => f.Success))
            {
                try
                {
                    ProcessSingleFilePDMOperations(processedFile);
                }
                catch (Exception ex)
                {
                    Logger.Error($"eDrawingsCreator: Error processing PDM operations for {processedFile.eDrawingsFilePath}: {ex.Message}", ex);
                    AddError(processedFile.eDrawingsFilePath, $"PDM operation failed: {ex.Message}", "PDM Error");
                }
            }

            Logger.Info("eDrawingsCreator: PDM processing completed - eDrawings files added to PDM with proper variable mapping");
        }

        /// <summary>
        /// Processes PDM operations for a single eDrawings file
        /// Implements proper PDM variable mapping like PDM Tasks do
        /// </summary>
        private void ProcessSingleFilePDMOperations(eDrawingsProcessedFile processedFile)
        {
            if (!File.Exists(processedFile.eDrawingsFilePath))
            {
                Logger.Warning($"eDrawingsCreator: eDrawings file not found for PDM operations: {processedFile.eDrawingsFilePath}");
                return;
            }

            Logger.Info($"eDrawingsCreator: Processing PDM operations for: {processedFile.eDrawingsFileName}");
            Logger.Info($"eDrawingsCreator: Implementing PDM variable mapping like PDM Tasks do");

            EPDM.Interop.epdm.IEdmFolder5 folder;
            EPDM.Interop.epdm.IEdmFile5 edmFile = _pdmVault.GetFileFromPath(processedFile.eDrawingsFilePath, out folder);

            // Check if file is in recycle bin and handle accordingly
            if (edmFile != null && IsFileInRecycleBin(edmFile))
            {
                Logger.Info($"eDrawingsCreator: File found in recycle bin: {processedFile.eDrawingsFileName}");
                
                // Attempt to restore from recycle bin
                if (!RestoreFileFromRecycleBin(edmFile, folder))
                {
                    Logger.Warning($"eDrawingsCreator: Could not restore file from recycle bin: {processedFile.eDrawingsFileName}");
                    Logger.Info($"eDrawingsCreator: Will treat as new file and attempt to add to PDM");
                    
                    // Treat as if the file doesn't exist in PDM - we'll add it as new
                    edmFile = null;
                }
                else
                {
                    // Successfully restored, refresh the file reference
                    edmFile = _pdmVault.GetFileFromPath(processedFile.eDrawingsFilePath, out folder);
                    Logger.Info($"eDrawingsCreator: Successfully restored file from recycle bin: {processedFile.eDrawingsFileName}");
                }
            }

            // If file doesn't exist in PDM (or was in recycle bin and couldn't be restored), add it
            if (edmFile == null)
            {
                Logger.Info($"eDrawingsCreator: Adding new eDrawings file to PDM: {processedFile.eDrawingsFileName}");
                edmFile = AddFileToPDM(processedFile.eDrawingsFilePath, folder);
                if (edmFile == null)
                {
                    Logger.Error($"eDrawingsCreator: Failed to add eDrawings file to PDM: {processedFile.eDrawingsFileName}");
                    AddError(processedFile.eDrawingsFilePath, "Failed to add file to PDM", "PDM Error");
                    return;
                }
                
                Logger.Info($"eDrawingsCreator: eDrawings file successfully added to PDM: {processedFile.eDrawingsFileName}");
            }
            else
            {
                // File already exists in PDM - check if it needs to be checked out for overwrite
                if (!edmFile.IsLocked)
                {
                    Logger.Info($"eDrawingsCreator: Existing eDrawings file found, checking out for overwrite: {processedFile.eDrawingsFileName}");
                    try
                    {
                        edmFile.LockFile(folder.ID, 0, (int)EPDM.Interop.epdm.EdmLockFlag.EdmLock_Simple);
                        Logger.Info($"eDrawingsCreator: Successfully checked out existing eDrawings file: {processedFile.eDrawingsFileName}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"eDrawingsCreator: Failed to check out existing eDrawings file: {ex.Message}");
                        AddError(processedFile.eDrawingsFilePath, $"Failed to check out existing file: {ex.Message}", "PDM Error");
                        return;
                    }
                }
            }

            // Set PDM variables (like PDM Tasks do)
            if (edmFile.IsLocked)
            {
                Logger.Info($"eDrawingsCreator: Setting PDM variables on eDrawings file (like PDM Tasks do): {processedFile.eDrawingsFileName}");
                try
                {
                    // Set variables using PDM API (like PDM Tasks do)
                    SetPDMVariables(edmFile, processedFile);
                    
                    string comment = $"eDrawings file created from {Path.GetFileName(processedFile.SourceFilePath)} via eDrawings Creator";
                    if (!string.IsNullOrEmpty(processedFile.PartNumber))
                        comment += $" - Part Number: {processedFile.PartNumber}";
                    if (!string.IsNullOrEmpty(processedFile.Revision))
                        comment += $" - Revision: {processedFile.Revision}";
                        
                    edmFile.UnlockFile(0, comment);
                    Logger.Info($"eDrawingsCreator: Successfully checked in eDrawings file with PDM variables: {processedFile.eDrawingsFileName}");
                    Logger.Info($"eDrawingsCreator: PDM variables set like PDM Task does");
                }
                catch (Exception ex)
                {
                    Logger.Error($"eDrawingsCreator: Failed to set PDM variables or check in eDrawings file: {ex.Message}");
                    AddError(processedFile.eDrawingsFilePath, $"Failed to set PDM variables or check in file: {ex.Message}", "PDM Error");
                }
            }
        }

        /// <summary>
        /// Sets PDM variables on the eDrawings file (like PDM Tasks do)
        /// </summary>
        private void SetPDMVariables(EPDM.Interop.epdm.IEdmFile5 edmFile, eDrawingsProcessedFile processedFile)
        {
            try
            {
                Logger.Info($"eDrawingsCreator: Setting PDM variables like PDM Task does for: {processedFile.eDrawingsFileName}");
                
                // Get the variable enumerator for setting variables (cast to IEdmEnumeratorVariable8 for CloseFile method)
                EPDM.Interop.epdm.IEdmEnumeratorVariable8 enumVar = edmFile.GetEnumeratorVariable() as EPDM.Interop.epdm.IEdmEnumeratorVariable8;
                if (enumVar == null)
                {
                    Logger.Warning($"eDrawingsCreator: Could not get variable enumerator for: {processedFile.eDrawingsFileName}");
                    return;
                }

                // For eDrawings files (.edrw, .eprt, .easm), use empty string for configuration instead of "@"
                // This is because eDrawings files are not SolidWorks files and don't have configurations
                string configName = "";
                Logger.Info($"eDrawingsCreator: Using configuration name '{configName}' for eDrawings file (non-SolidWorks file)");

                // Set variables like PDM Tasks do (Source -> Destination mapping)
                bool anyVariableSet = false;

                // Set Part Number
                if (!string.IsNullOrEmpty(processedFile.PartNumber))
                {
                    try
                    {
                        enumVar.SetVar("Part Number", configName, processedFile.PartNumber);
                        Logger.Info($"eDrawingsCreator: Set PDM variable 'Part Number': '{processedFile.PartNumber}' with config '{configName}'");
                        anyVariableSet = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"eDrawingsCreator: Failed to set Part Number variable: {ex.Message}");
                    }
                }

                // Set Description
                if (!string.IsNullOrEmpty(processedFile.Description))
                {
                    try
                    {
                        enumVar.SetVar("Description", configName, processedFile.Description);
                        Logger.Info($"eDrawingsCreator: Set PDM variable 'Description': '{processedFile.Description}' with config '{configName}'");
                        anyVariableSet = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"eDrawingsCreator: Failed to set Description variable: {ex.Message}");
                    }
                }

                // Set Revision
                if (!string.IsNullOrEmpty(processedFile.Revision))
                {
                    try
                    {
                        enumVar.SetVar("Revision", configName, processedFile.Revision);
                        Logger.Info($"eDrawingsCreator: Set PDM variable 'Revision': '{processedFile.Revision}' with config '{configName}'");
                        anyVariableSet = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"eDrawingsCreator: Failed to set Revision variable: {ex.Message}");
                    }
                }

                // Commit the variables to the database (like PDM Tasks do)
                if (anyVariableSet)
                {
                    try
                    {
                        enumVar.CloseFile(true);
                        Logger.Info($"eDrawingsCreator: Successfully committed PDM variables for: {processedFile.eDrawingsFileName}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"eDrawingsCreator: Failed to commit PDM variables: {ex.Message}");
                    }
                }
                else
                {
                    Logger.Warning($"eDrawingsCreator: No PDM variables were set for: {processedFile.eDrawingsFileName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error setting PDM variables: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Checks if a file is in the PDM recycle bin
        /// </summary>
        private bool IsFileInRecycleBin(EPDM.Interop.epdm.IEdmFile5 edmFile)
        {
            try
            {
                // Check if the file exists but is marked as deleted
                if (edmFile == null) return false;

                // Get file information to check if it's deleted
                EPDM.Interop.epdm.IEdmPos5 pos = edmFile.GetFirstFolderPosition();
                while (!pos.IsNull)
                {
                    EPDM.Interop.epdm.IEdmFolder5 fileFolder = edmFile.GetNextFolder(pos);
                    if (fileFolder != null)
                    {
                        // Check if the file is in a deleted state or recycle bin folder
                        // PDM typically uses special folder names or flags for deleted items
                        string folderPath = fileFolder.LocalPath;
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            // Check for common recycle bin indicators
                            if (folderPath.ToLower().Contains("$recycle") || 
                                folderPath.ToLower().Contains("deleted") ||
                                folderPath.Contains("$") && folderPath.ToLower().Contains("bin"))
                            {
                                Logger.Info($"eDrawingsCreator: File appears to be in recycle bin folder: {folderPath}");
                                return true;
                            }
                        }
                    }
                }

                // Alternative check: try to access file properties - deleted files may throw exceptions
                try
                {
                    string fileName = edmFile.Name;
                    int fileID = edmFile.ID;
                    // If we can access basic properties, file is probably not in recycle bin
                    return false;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // COM exception might indicate the file is in an inaccessible state (deleted)
                    Logger.Info($"eDrawingsCreator: File may be in recycle bin due to COM access error");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"eDrawingsCreator: Error checking if file is in recycle bin: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores a file from the PDM recycle bin
        /// </summary>
        private bool RestoreFileFromRecycleBin(EPDM.Interop.epdm.IEdmFile5 edmFile, EPDM.Interop.epdm.IEdmFolder5 targetFolder)
        {
            try
            {
                Logger.Info($"eDrawingsCreator: Attempting to restore file from recycle bin: {edmFile.Name}");
                
                // In PDM, file restoration typically requires:
                // 1. Administrative privileges
                // 2. Using the PDM Administration API
                // 3. Or using the IEdmBatchUndelete interface
                
                // For now, we'll handle this by:
                // 1. Logging the issue for manual intervention
                // 2. Attempting to delete the existing file reference so we can add a new one
                
                Logger.Warning($"eDrawingsCreator: File {edmFile.Name} is in recycle bin.");
                Logger.Warning($"eDrawingsCreator: Automatic restoration requires administrative privileges.");
                Logger.Info($"eDrawingsCreator: Will attempt to add file as new instead of restoring.");
                
                // Return false to indicate we couldn't restore, but the calling code can try to add as new
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error attempting to restore file from recycle bin: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adds a file to PDM vault
        /// </summary>
        private EPDM.Interop.epdm.IEdmFile5 AddFileToPDM(string filePath, EPDM.Interop.epdm.IEdmFolder5 folder)
        {
            try
            {
                if (folder == null)
                {
                    string folderPath = Path.GetDirectoryName(filePath);
                    folder = _pdmVault.GetFolderFromPath(folderPath);
                    if (folder == null)
                    {
                        Logger.Error($"eDrawingsCreator: Could not find PDM folder for path: {folderPath}");
                        return null;
                    }
                }

                Logger.Info($"eDrawingsCreator: Adding file to PDM folder: {folder.LocalPath}");
                
                // Add the file to PDM - AddFile returns the file ID, not the file object
                int fileID = folder.AddFile(0, filePath);
                if (fileID > 0)
                {
                    // Get the file object using GetFileFromPath after adding
                    EPDM.Interop.epdm.IEdmFolder5 tempFolder;
                    EPDM.Interop.epdm.IEdmFile5 newFile = _pdmVault.GetFileFromPath(filePath, out tempFolder);
                    if (newFile != null)
                    {
                        Logger.Info($"eDrawingsCreator: Successfully added file to PDM: {newFile.Name}");
                        return newFile;
                    }
                    else
                    {
                        Logger.Error($"eDrawingsCreator: Failed to get file object after adding to PDM: {filePath}");
                        return null;
                    }
                }
                else
                {
                    Logger.Error($"eDrawingsCreator: Failed to add file to PDM: {filePath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"eDrawingsCreator: Error adding file to PDM: {ex.Message}");
                return null;
            }
        }
    }
} 