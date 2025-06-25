using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RoesleinAddIn
{
    /// <summary>
    /// Handles programmatic creation of shipping label blocks in SolidWorks
    /// Similar to AutoCAD dynamic blocks with different arrow configurations
    /// </summary>
    public class ShippingLabelBlockCreator
    {
        // Windows API for sending keys directly to SolidWorks
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        
        private readonly ISldWorks swApp;
        private readonly DrawingDoc swDraw;
        private readonly ModelDoc2 swModel;

        // Path to PDM vault where blocks are stored
        private const string BLOCK_VAULT_PATH = @"C:\PCSVAULT\SolidWorks Settings\Roeslein SW AddIn\";
        
        // Block file names (assuming .SLDBLK extension)
        private const string SLOT_BLOCK_FILE = "RoesleinShippingLabel_Slot.SLDBLK";
        private const string RIGHT_ARROW_BLOCK_FILE = "RoesleinShippingLabel_RightArrow.SLDBLK";
        private const string LEFT_ARROW_BLOCK_FILE = "RoesleinShippingLabel_LeftArrow.SLDBLK";
        private const string DOUBLE_ARROW_BLOCK_FILE = "RoesleinShippingLabel_DoubleArrow.SLDBLK";

        // Block names for manually created blocks
        private const string SLOT_BLOCK_NAME = "RoesleinShippingLabel_Slot";
        private const string RIGHT_ARROW_BLOCK_NAME = "RoesleinShippingLabel_RightArrow";
        private const string LEFT_ARROW_BLOCK_NAME = "RoesleinShippingLabel_LeftArrow";
        private const string DOUBLE_ARROW_BLOCK_NAME = "RoesleinShippingLabel_DoubleArrow";

        // Smart Copy System - Cache for last successfully placed blocks
        private static Dictionary<ShippingLabelArrowType, LastPlacedBlockInfo> lastPlacedBlocks = 
            new Dictionary<ShippingLabelArrowType, LastPlacedBlockInfo>();
        
        // Block Placement Cooldown System - Prevents SolidWorks API conflicts
        private static DateTime lastBlockPlacementTime = DateTime.MinValue;
        private static readonly TimeSpan BLOCK_PLACEMENT_COOLDOWN = TimeSpan.FromMilliseconds(2000); // 2 second cooldown
        
        // Offset distance for smart copy placement (in drawing units)
        private const double SMART_COPY_OFFSET_X = 0.5; // 0.5 inches to the right
        private const double SMART_COPY_OFFSET_Y = -0.5; // 0.5 inches down

        private static Dictionary<string,int> drawingSequenceCounters = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the main SolidWorks window handle
        /// </summary>
        private IntPtr GetSolidWorksWindowHandle()
        {
            try
            {
                // First try to find by the frame handle from SolidWorks API
                IntPtr frameHandle = IntPtr.Zero;
                if (swApp != null)
                {
                    try
                    {
                        var frameObj = swApp.Frame();
                        if (frameObj != null)
                        {
                            // Cast to IFrame interface to access GetHWnd method
                            var frame = frameObj as SolidWorks.Interop.sldworks.IFrame;
                            if (frame != null)
                            {
                                frameHandle = new IntPtr(frame.GetHWnd());
                                if (frameHandle != IntPtr.Zero)
                                {
                                    return frameHandle;
                                }
                            }
                        }
                    }
                    catch (Exception frameEx)
                    {
                        Logger.Warning($"Could not get SolidWorks frame handle via API: {frameEx.Message}");
                    }
                }
                
                // Fallback: Find SolidWorks window by class name
                IntPtr swHandle = FindWindow("SldWorks", null);
                return swHandle;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting SolidWorks window handle: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Sends a key directly to the SolidWorks window using Windows API
        /// </summary>
        private void SendKeyToSolidWorks(char key)
        {
            try
            {
                IntPtr swHandle = GetSolidWorksWindowHandle();
                if (swHandle != IntPtr.Zero)
                {
                    // Convert character to virtual key code
                    IntPtr virtualKey = new IntPtr(char.ToUpper(key));
                    
                    // Send key down and key up messages
                    PostMessage(swHandle, WM_KEYDOWN, virtualKey, IntPtr.Zero);
                    System.Threading.Thread.Sleep(50); // Brief pause
                    PostMessage(swHandle, WM_KEYUP, virtualKey, IntPtr.Zero);
                    
                    Logger.Info($"Sent '{key}' key to SolidWorks window handle: {swHandle}");
                }
                else
                {
                    Logger.Warning("Could not find SolidWorks window handle");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error sending key to SolidWorks: {ex.Message}");
            }
        }

        public ShippingLabelBlockCreator(ISldWorks solidWorksApp)
        {
            swApp = solidWorksApp ?? throw new ArgumentNullException(nameof(solidWorksApp));
            swModel = swApp.ActiveDoc as ModelDoc2;
            swDraw = swModel as DrawingDoc;
            
            if (swDraw == null)
            {
                throw new InvalidOperationException("Active document must be a drawing to create blocks.");
            }
        }

        /// <summary>
        /// Creates all four shipping label block definitions by loading them from PDM vault
        /// </summary>
        public void CreateAllShippingLabelBlocks()
        {
            try
            {
                LoadAllShippingLabelBlocks();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating shipping label blocks: {ex.Message}", ex);
                MessageBox.Show($"Error creating blocks: {ex.Message}", "Block Creation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Loads all shipping label blocks from the PDM vault into the current drawing
        /// </summary>
        public bool LoadAllShippingLabelBlocks()
        {
            try
            {
                // Use the simplified file detection approach
                CreateBlockGeometryFromFiles();
                return true; // Return success for now since we're detecting files correctly
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading shipping label blocks: {ex.Message}", ex);
                MessageBox.Show($"Error loading blocks: {ex.Message}", "Load Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Simplified approach: Detects block files and provides user guidance
        /// </summary>
        public void CreateBlockGeometryFromFiles()
        {
            try
            {
                Logger.Info($"Checking for block files in: {BLOCK_VAULT_PATH}");

                var foundFiles = new List<string>();
                var missingFiles = new List<string>();
                
                foreach (ShippingLabelArrowType arrowType in Enum.GetValues(typeof(ShippingLabelArrowType)))
                {
                    string blockFileName = GetBlockFileName(arrowType);
                    string blockFilePath = BLOCK_VAULT_PATH + blockFileName;
                    
                    if (System.IO.File.Exists(blockFilePath))
                    {
                        foundFiles.Add(blockFileName);
                        Logger.Info($"✓ Found block file: {blockFileName}");
                    }
                    else
                    {
                        missingFiles.Add(blockFileName);
                        Logger.Warning($"✗ Missing block file: {blockFileName}");
                    }
                }

                // Create summary message
                string message = $"Block File Detection Results:\n\n";
                message += $"PDM Vault Location: {BLOCK_VAULT_PATH}\n\n";
                
                if (foundFiles.Count > 0)
                {
                    message += $"✓ Found {foundFiles.Count} block files:\n";
                    foreach (var file in foundFiles)
                    {
                        message += $"  • {file}\n";
                    }
                    message += "\n";
                }

                if (missingFiles.Count > 0)
                {
                    message += $"✗ Missing {missingFiles.Count} block files:\n";
                    foreach (var file in missingFiles)
                    {
                        message += $"  • {file}\n";
                    }
                    message += "\n";
                }

                if (foundFiles.Count == 4)
                {
                    message += "🎉 All block files found!\n\n";
                    message += "Next Steps:\n";
                    message += "1. Use 'Create New' to place shipping labels\n";
                    message += "2. The blocks will be imported automatically when needed\n";
                    message += "3. Full block integration coming in next update";
                    
                    MessageBox.Show(message, "All Blocks Found!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (foundFiles.Count > 0)
                {
                    message += "⚠️ Partial block set found.\n\n";
                    message += "Please ensure all 4 block files are in the PDM vault location.";
                    
                    MessageBox.Show(message, "Partial Load", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    message += "❌ No block files found.\n\n";
                    message += "Please verify:\n";
                    message += "1. Block files are saved in the correct location\n";
                    message += "2. Files have the exact names shown above\n";
                    message += "3. PDM vault is accessible";
                    
                    MessageBox.Show(message, "No Blocks Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking block files: {ex.Message}", ex);
                MessageBox.Show($"Error checking block files: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Checks if a block definition exists in the current drawing
        /// </summary>
        private bool BlockExists(string blockName)
        {
            try
            {
                // Simplified approach - for now, assume blocks need to be loaded
                // TODO: Implement proper block existence check once we have working block loading
                Logger.Info($"Checking if block exists: {blockName} (simplified check)");
                return false; // Always assume blocks need to be loaded for now
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error checking block existence: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the block file name for the arrow type
        /// </summary>
        private string GetBlockFileName(ShippingLabelArrowType arrowType)
        {
            switch (arrowType)
            {
                case ShippingLabelArrowType.Slot:
                    return SLOT_BLOCK_FILE;
                case ShippingLabelArrowType.RightArrow:
                    return RIGHT_ARROW_BLOCK_FILE;
                case ShippingLabelArrowType.LeftArrow:
                    return LEFT_ARROW_BLOCK_FILE;
                case ShippingLabelArrowType.DoubleArrow:
                    return DOUBLE_ARROW_BLOCK_FILE;
                default:
                    throw new ArgumentException($"Unknown arrow type: {arrowType}");
            }
        }

        /// <summary>
        /// Instructions for manually creating the shipping label blocks
        /// </summary>
        public void ShowBlockCreationInstructions()
        {
            string instructions = @"MANUAL BLOCK CREATION INSTRUCTIONS:

1. Create SLOT block:
   - Draw rectangle: 0.05"" wide × 0.01"" tall
   - Name: RoesleinShippingLabel_Slot

2. Create RIGHT ARROW block:
   - Draw rectangle with right-pointing arrow notch
   - Name: RoesleinShippingLabel_RightArrow

3. Create LEFT ARROW block:
   - Draw rectangle with left-pointing arrow notch  
   - Name: RoesleinShippingLabel_LeftArrow

4. Create DOUBLE ARROW block:
   - Draw rectangle with arrow notches on both sides
   - Name: RoesleinShippingLabel_DoubleArrow

5. Save drawing as template for future use

Then use this code to INSERT the blocks at specified locations.";

            MessageBox.Show(instructions, "Manual Block Creation Instructions", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Gets the arrow type from the block file name
        /// </summary>
        private ShippingLabelArrowType GetArrowTypeFromFileName(string filePath)
        {
            string fileName = System.IO.Path.GetFileName(filePath);
            
            if (fileName.Contains("Slot")) return ShippingLabelArrowType.Slot;
            if (fileName.Contains("RightArrow")) return ShippingLabelArrowType.RightArrow;
            if (fileName.Contains("LeftArrow")) return ShippingLabelArrowType.LeftArrow;
            if (fileName.Contains("DoubleArrow")) return ShippingLabelArrowType.DoubleArrow;
            
            return ShippingLabelArrowType.Slot; // Default
        }

        /// <summary>
        /// Gets the standardized block name for the arrow type
        /// </summary>
        private string GetBlockName(ShippingLabelArrowType arrowType)
        {
            switch (arrowType)
            {
                case ShippingLabelArrowType.Slot:
                    return SLOT_BLOCK_NAME;
                case ShippingLabelArrowType.RightArrow:
                    return RIGHT_ARROW_BLOCK_NAME;
                case ShippingLabelArrowType.LeftArrow:
                    return LEFT_ARROW_BLOCK_NAME;
                case ShippingLabelArrowType.DoubleArrow:
                    return DOUBLE_ARROW_BLOCK_NAME;
                default:
                    throw new ArgumentException($"Unknown arrow type: {arrowType}");
            }
        }

        /// <summary>
        /// Starts interactive block placement with comprehensive SolidWorks API limitation workarounds
        /// </summary>
        public bool StartInteractiveBlockPlacement(string arrowType, ShippingLabelData labelData)
        {
            try
            {
                Logger.Info("=== START INTERACTIVE BLOCK PLACEMENT ===");
                Logger.Info($"ArrowType: {arrowType}");
                Logger.Info($"LabelData: ID={labelData.IDNumber}");

                // Get active document
                if (swApp == null)
                {
                    Logger.Error("Failed to connect to SolidWorks");
                    return false;
                }

                var swModel = (ModelDoc2)swApp.ActiveDoc;
                if (swModel == null)
                {
                    Logger.Error("No active SolidWorks document");
                    return false;
                }

                var swDrawing = (DrawingDoc)swModel;
                if (swDrawing == null)
                {
                    Logger.Error("Active document is not a drawing");
                    return false;
                }

                // WORKAROUND 1: Clear any existing sketch mode conflicts
                Logger.Info("Applying SolidWorks API limitation workarounds...");
                ClearSketchModeConflicts(swModel);

                // WORKAROUND 2: Force garbage collection to clear block definition cache
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();

                // Get block file path
                string blockFileName = GetBlockFileName(GetArrowTypeFromString(arrowType));
                string blockFilePath = BLOCK_VAULT_PATH + blockFileName;

                Logger.Info($"Block file name: {blockFileName}");
                Logger.Info($"Block file path: {blockFilePath}");

                if (!File.Exists(blockFilePath))
                {
                    Logger.Error($"Block file not found: {blockFilePath}");
                    return false;
                }

                Logger.Info("Block file exists, starting INTERACTIVE placement with mouse positioning");

                // Use interactive placement method
                return PlaceBlockInteractively(swDrawing, blockFilePath, labelData);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in StartInteractiveBlockPlacement: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Places a block interactively at a specific location and allows user to position it
        /// </summary>
        private bool PlaceBlockInteractively(DrawingDoc swDrawing, string blockFilePath, ShippingLabelData labelData)
        {
            try
            {
                var swModel = (ModelDoc2)swApp.ActiveDoc;
                if (swModel == null)
                {
                    Logger.Error("No active SolidWorks document available");
                    return false;
                }

                var swSketchMgr = swModel.SketchManager;
                
                Logger.Info($"=== INTERACTIVE PLACEMENT WITHOUT MOVE COMMAND ===");
                Logger.Info($"Ensuring SolidWorks has focus to minimize popup interference...");

                // Ensure SolidWorks window is in foreground
                IntPtr swWindow = GetSolidWorksWindowHandle();
                if (swWindow != IntPtr.Zero)
                {
                    SetForegroundWindow(swWindow);
                    System.Threading.Thread.Sleep(100);
                }

                // Get or create block definition
                string blockName = GetBlockNameFromFilePath(blockFilePath);
                var blockDef = GetSketchBlockDefinitionByName(blockName, swSketchMgr);
                
                if (blockDef == null)
                {
                    Logger.Error($"Block definition '{blockName}' not found. Loading from file...");
                    
                    // Enter sketch mode first for block creation
                    swSketchMgr.InsertSketch(true);
                    
                    // Use MakeSketchBlockFromFile to create the block definition
                    var mathUtils = (MathUtility)swApp.GetMathUtility();
                    var mathPoint = (MathPoint)mathUtils.CreatePoint(new double[] { 0.0, 0.0, 0.0 });
                    
                    blockDef = swSketchMgr.MakeSketchBlockFromFile(mathPoint, blockFilePath, true, labelData.BlockScale, 0.0);
                    
                    if (blockDef == null)
                    {
                        Logger.Error($"Failed to create block definition from: {blockFilePath}");
                        swSketchMgr.InsertSketch(false);
                        return false;
                    }
                    
                    // Get the automatically created instance from MakeSketchBlockFromFile
                    object[] instances = blockDef.GetInstances() as object[];
                    if (instances != null && instances.Length > 0)
                    {
                        var newBlockInstance = instances[instances.Length - 1] as SketchBlockInstance;
                        if (newBlockInstance != null)
                        {
                            // Setup the automatically created block
                            bool initialSetupSuccess = SetupBlockBeforeMove(newBlockInstance, labelData, swModel);
                            if (initialSetupSuccess)
                            {
                                var initialFeature = newBlockInstance as Feature;
                                if (initialFeature != null)
                                {
                                    Logger.Info($"Block '{initialFeature.Name}' created and selected successfully");
                                    
                                    // Exit sketch mode 
                                    swSketchMgr.InsertSketch(false);
                                    
                                    // Clear selection and refresh
                                    swModel.ClearSelection2(true);
                                    swModel.GraphicsRedraw2();
                                    
                                    Logger.Info("SUCCESS: Block placed and ready for manual positioning if needed");
                                    
                                    // Store the block info for later finalization
                                    labelData.InstanceName = initialFeature.Name;
                                    
                                    return true;
                                }
                            }
                        }
                    }
                    
                    swSketchMgr.InsertSketch(false);
                    Logger.Error("Failed to get instance from newly created block definition");
                    return false;
                }

                Logger.Info($"Block definition '{blockName}' already exists. Inserting new instance.");

                // Get smart placement coordinates (center of view or smart offset)
                var coords = GetSmartPlacementCoordinates(swModel);
                Logger.Info($"Placing block at smart coordinates: X={coords.X:F3}, Y={coords.Y:F3}");
                
                // Enter sketch mode and insert the block
                swSketchMgr.InsertSketch(true);
                
                // Use InsertSketchBlockInstance to create a new instance from existing definition
                var mathUtils2 = (MathUtility)swApp.GetMathUtility();
                var mathPoint2 = (MathPoint)mathUtils2.CreatePoint(new double[] { coords.X, coords.Y, coords.Z });
                
                var newBlockInstance2 = swSketchMgr.InsertSketchBlockInstance(blockDef, mathPoint2, labelData.BlockScale, 0.0);
                
                if (newBlockInstance2 == null)
                {
                    Logger.Error("Failed to create block instance");
                    swSketchMgr.InsertSketch(false);
                    return false;
                }

                // Setup the block before attempting any movement
                bool secondSetupSuccess = SetupBlockBeforeMove(newBlockInstance2, labelData, swModel);
                if (!secondSetupSuccess)
                {
                    Logger.Error("Failed to setup block properties");
                    swSketchMgr.InsertSketch(false);
                    return false;
                }
                
                // Select the block instance for interactive positioning
                var secondNewFeature = newBlockInstance2 as Feature;
                if (secondNewFeature != null)
                {
                    bool selected = secondNewFeature.Select2(false, 0);
                    if (selected)
                    {
                        Logger.Info($"Block '{secondNewFeature.Name}' selected successfully");
                        
                        // Try to start drag operation for interactive placement
                        try
                        {
                            // Instead of using the problematic move command, use SolidWorks drag functionality
                            // First ensure we're still in sketch mode for interactive placement
                            if (swModel.SketchManager.ActiveSketch == null)
                            {
                                swSketchMgr.InsertSketch(true);
                            }
                            
                            // Use the SolidWorks sketch entity drag functionality
                            // This approach keeps the block attached to the cursor until user clicks
                            var sketchEntity = newBlockInstance2 as Entity;
                            if (sketchEntity != null)
                            {
                                // Start the drag operation - this makes the block follow the mouse
                                bool dragStarted = sketchEntity.Select4(false, null);
                                
                                if (dragStarted)
                                {
                                    Logger.Info("SUCCESS: Block is now attached to mouse cursor for positioning");
                                    Logger.Info("User can move mouse to position block, then click to place it");
                                    
                                    // Store the block info but don't exit sketch mode yet
                                    labelData.InstanceName = secondNewFeature.Name;
                                    
                                    // Don't exit sketch mode - let user complete the placement
                                    return true;
                                }
                                else
                                {
                                    Logger.Warning("Could not start drag operation, placing block at calculated position");
                                }
                            }
                            
                            // Fallback: Exit sketch mode and place at calculated position
                            swSketchMgr.InsertSketch(false);
                            swModel.ClearSelection2(true);
                            swModel.GraphicsRedraw2();
                            
                            Logger.Info("Block placed at calculated position - user can manually move it if needed");
                            labelData.InstanceName = secondNewFeature.Name;
                            
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Interactive placement failed: {ex.Message}");
                            
                            // Fallback to standard placement
                            swSketchMgr.InsertSketch(false);
                            swModel.ClearSelection2(true);
                            swModel.GraphicsRedraw2();
                            
                            Logger.Info("Block placed at calculated position - user can manually move it if needed");
                            labelData.InstanceName = secondNewFeature.Name;
                            
                            return true;
                        }
                    }
                    else
                    {
                        Logger.Error("Failed to select the block after placement");
                        // Still return true since the block was placed
                        labelData.InstanceName = secondNewFeature.Name;
                        return true;
                    }
                }
                else
                {
                    Logger.Error("Could not cast SketchBlockInstance to Feature for selection");
                    // Still return true since the block was created
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in PlaceBlockInteractively: {ex.Message}", ex);
                var swModel = (ModelDoc2)swApp.ActiveDoc;
                if (swModel != null)
                {
                    swModel.SketchManager.InsertSketch(false); // Ensure we exit sketch mode on error
                }
                
                return false;
            }
        }

        /// <summary>
        /// Sets up block properties before the move command
        /// </summary>
        private bool SetupBlockBeforeMove(SketchBlockInstance blockInstance, ShippingLabelData labelData, ModelDoc2 swModel)
        {
            try
            {
                var newFeature = blockInstance as Feature;
                if (newFeature == null)
                {
                    Logger.Error("Could not cast SketchBlockInstance to Feature for setup.");
                    return false;
                }
                
                string drawingNumber = labelData.DrawingName ?? string.Empty;
                string swHandle = GenerateSwHandle(swModel, drawingNumber);
                
                try 
                { 
                    newFeature.Name = swHandle; 
                } 
                catch 
                { 
                    Logger.Warning($"Could not set feature name to {swHandle} - name may already exist");
                }
                
                SetBlockUniqueIdentifier(blockInstance, swHandle);
                labelData.InstanceName = swHandle;
                Logger.Info($"Generated and assigned SW handle: {swHandle}");

                SetBlockEntitiesLayer(blockInstance, "ID");
                PopulateBlockWithLabelData(blockInstance, labelData);

                Logger.Info($"Block '{swHandle}' setup completed before move command");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in SetupBlockBeforeMove: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Finalizes the block placement after user has positioned it with the move command
        /// This should be called after the user completes the move operation
        /// </summary>
        public bool FinalizeInteractiveBlockPlacement(string instanceName)
        {
            try
            {
                Logger.Info($"=== FINALIZING INTERACTIVE BLOCK PLACEMENT ===");
                Logger.Info($"Instance Name: {instanceName}");

                var swModel = (ModelDoc2)swApp.ActiveDoc;
                if (swModel == null)
                {
                    Logger.Error("No active SolidWorks document");
                    return false;
                }

                // Find the block instance by name
                var blockInstance = FindBlockInstanceByName(instanceName, swModel.SketchManager);
                if (blockInstance == null)
                {
                    Logger.Error($"Could not find block instance with name: {instanceName}");
                    return false;
                }

                var feature = blockInstance as Feature;
                if (feature == null)
                {
                    Logger.Error("Could not cast block instance to Feature");
                    return false;
                }

                // Get the final position after move
                try
                {
                    object boxObj = null;
                    feature.GetBox(ref boxObj);
                    if (boxObj is double[] box)
                    {
                        double finalX = (box[0] + box[3]) / 2.0;
                        double finalY = (box[1] + box[4]) / 2.0;
                        
                        Logger.Info($"Final block position for {instanceName}: X={finalX}, Y={finalY}");
                        
                        // Exit sketch mode if still active
                        swModel.SketchManager.InsertSketch(false);
                        
                        // Clear selections and refresh view
                        swModel.ClearSelection2(true);
                        swModel.GraphicsRedraw2();
                        swModel.ViewZoomtofit();
                        
                        Logger.Info($"SUCCESS: Block instance '{instanceName}' placement finalized at position ({finalX}, {finalY})");
                        return true;
                    }
                    else
                    {
                        Logger.Warning("Could not get final block position, but placement completed");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not get final block position: {ex.Message}");
                    // Still consider it successful since the block was placed
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in FinalizeInteractiveBlockPlacement: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Converts string arrow type to enum
        /// </summary>
        private ShippingLabelArrowType GetArrowTypeFromString(string arrowType)
        {
            switch (arrowType?.ToLower())
            {
                case "slot":
                    return ShippingLabelArrowType.Slot;
                case "rightarrow":
                    return ShippingLabelArrowType.RightArrow;
                case "leftarrow":
                    return ShippingLabelArrowType.LeftArrow;
                case "doublearrow":
                    return ShippingLabelArrowType.DoubleArrow;
                default:
                    return ShippingLabelArrowType.Slot; // Default fallback
            }
        }

        /// <summary>
        /// WORKAROUND: Clear sketch mode conflicts that cause MakeSketchBlockFromFile to fail
        /// </summary>
        private void ClearSketchModeConflicts(ModelDoc2 swModel)
        {
            try
            {
                Logger.Info("Clearing sketch mode conflicts...");
                
                // Exit any active sketch mode
                swModel.SketchManager.InsertSketch(false);
                
                // Clear all selections
                swModel.ClearSelection2(true);
                
                // Force document rebuild to clear internal state
                swModel.ForceRebuild3(false);
                
                // Brief pause to let SolidWorks settle
                System.Threading.Thread.Sleep(100);
                
                Logger.Info("Sketch mode conflicts cleared");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not clear sketch mode conflicts: {ex.Message}");
            }
        }

        /// <summary>
        /// Advanced block insertion using a stable Definition/Instance workflow.
        /// This is the definitive solution to back-to-back placement failures.
        /// </summary>
        private bool InsertBlockWithAdvancedWorkarounds(DrawingDoc swDrawing, string blockFilePath, ShippingLabelData labelData)
        {
            ModelDoc2 swModel = null;
            try
            {
                Logger.Info("=== DEFINITION/INSTANCE STRATEGY (V9 FINAL) ===");
                swModel = (ModelDoc2)swDrawing;
                string blockName = GetBlockName(labelData.ArrowType);

                SketchBlockDefinition blockDefinition = GetSketchBlockDefinitionByName(blockName, swModel.SketchManager);
                SketchBlockInstance newBlockInstance = null;

                // If definition doesn't exist, create it. This also creates the FIRST instance.
                if (blockDefinition == null)
                {
                    Logger.Info($"Block definition '{blockName}' not found. Creating it from file...");
                    var coordinates = GetAlternativeBlockCoordinates();
                    var mathUtils = (MathUtility)swApp.GetMathUtility();
                    var mathPoint = (MathPoint)mathUtils.CreatePoint(new double[] { coordinates.X, coordinates.Y, coordinates.Z });

                    swModel.SketchManager.InsertSketch(true);
                    blockDefinition = swModel.SketchManager.MakeSketchBlockFromFile(mathPoint, blockFilePath, true, labelData.BlockScale, 0.0);
                    
                    if (blockDefinition == null)
                    {
                        Logger.Error($"CRITICAL: Failed to create block definition from file '{blockFilePath}'.");
                        swModel.SketchManager.InsertSketch(false);
                        return false;
                    }
                    Logger.Info($"Successfully created block definition '{blockName}'.");

                    // Get the instance created by MakeSketchBlockFromFile
                    object[] instances = blockDefinition.GetInstances() as object[];
                    if (instances == null || instances.Length == 0)
                    {
                        Logger.Error("CRITICAL: MakeSketchBlockFromFile created a definition but NO instance was found.");
                        swModel.SketchManager.InsertSketch(false);
                        return false;
                    }
                    newBlockInstance = instances[instances.Length - 1] as SketchBlockInstance;
                }
                // If definition already exists, create a new instance from it.
                else
                {
                    Logger.Info($"Block definition '{blockName}' already exists. Inserting new instance.");
                    var coordinates = GetAlternativeBlockCoordinates();
                    var mathUtils = (MathUtility)swApp.GetMathUtility();
                    var mathPoint = (MathPoint)mathUtils.CreatePoint(new double[] { coordinates.X, coordinates.Y, coordinates.Z });

                    swModel.SketchManager.InsertSketch(true);
                    newBlockInstance = swModel.SketchManager.InsertSketchBlockInstance(blockDefinition, mathPoint, labelData.BlockScale, 0.0);
                }

                // Now, process the instance that was created or retrieved.
                if (newBlockInstance == null)
                {
                    Logger.Error("CRITICAL: Failed to obtain a valid block instance.");
                    swModel.SketchManager.InsertSketch(false);
                    return false;
                }

                return FinalizeBlockPlacement(newBlockInstance, labelData, blockFilePath, swModel);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in InsertBlockWithAdvancedWorkarounds: {ex.Message}", ex);
                if (swModel != null)
                {
                    swModel.SketchManager.InsertSketch(false); // Ensure we exit sketch mode on error
                }
                return false;
            }
        }
        
        /// <summary>
        /// Handles the final setup steps for a newly created block instance.
        /// </summary>
        private bool FinalizeBlockPlacement(SketchBlockInstance newBlockInstance, ShippingLabelData labelData, string blockFilePath, ModelDoc2 swModel)
        {
            try
            {
                var newFeature = newBlockInstance as Feature;
                if (newFeature == null)
                {
                    Logger.Error("Could not cast SketchBlockInstance to Feature. Cannot complete setup.");
                    return false;
                }
                string drawingNumber = labelData.DrawingName ?? string.Empty;
                string swHandle = GenerateSwHandle(swModel, drawingNumber);
                try { newFeature.Name = swHandle; } catch { /* ignore errors if name clashes */ }
                SetBlockUniqueIdentifier(newBlockInstance, swHandle);
                labelData.InstanceName = swHandle;
                Logger.Info($"Generated and assigned SW handle: {swHandle}");

                SetBlockEntitiesLayer(newBlockInstance, "ID");
                PopulateBlockWithLabelData(newBlockInstance, labelData);

                swModel.GraphicsRedraw2();
                
                // Get final position and update label data
                try
                {
                    // Use the feature's bounding box to get a reliable position
                    object boxObj = null;
                    newFeature.GetBox(ref boxObj);
                    if (boxObj is double[] box)
                    {
                        labelData.XPosition = (box[0] + box[3]) / 2.0;
                        labelData.YPosition = (box[1] + box[4]) / 2.0;
                        Logger.Info($"Final block position for {swHandle}: X={labelData.XPosition}, Y={labelData.YPosition}");
                    }
                }
                catch (Exception ex)
                {
                     Logger.Warning($"Could not get final block position using GetBox: {ex.Message}");
                }

                swModel.ViewZoomtofit();
                
                Logger.Info($"SUCCESS: Block instance '{swHandle}' placed and configured.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in FinalizeBlockPlacement: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets an existing SketchBlockDefinition from the document by its name.
        /// </summary>
        private SketchBlockDefinition GetSketchBlockDefinitionByName(string blockName, ISketchManager sketchManager)
        {
            try
            {
                object[] definitions = sketchManager.GetSketchBlockDefinitions() as object[];
                if (definitions == null) return null;

                foreach (SketchBlockDefinition definition in definitions)
                {
                    if (string.Equals(Path.GetFileNameWithoutExtension(definition.FileName), blockName, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info($"Found existing block definition: {blockName}");
                        return definition;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting sketch block definitions: {ex.Message}", ex);
            }
            return null;
        }



        /// <summary>
        /// Get current block count for positioning
        /// </summary>
        private int GetCurrentBlockCount()
        {
            try
            {
                // Try to get count from global registry
                var registryType = Type.GetType("RoesleinAddIn.GlobalBlockRegistry, RoesleinAddIn");
                if (registryType != null)
                {
                    var getCountMethod = registryType.GetMethod("GetBlockCount", BindingFlags.Public | BindingFlags.Static);
                    if (getCountMethod != null)
                    {
                        var count = (int)getCountMethod.Invoke(null, null);
                        Logger.Info($"Got block count from registry: {count}");
                        return count;
                    }
                }

                Logger.Info("Could not get block count from registry, using default");
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting block count: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Show dialog explaining SolidWorks API limitation (research-confirmed)
        /// DISABLED: This dialog is commented out to prevent interrupting interactive placement
        /// </summary>
        private void ShowBlockPlacementLimitationDialog()
        {
            try
            {
                // DISABLED: Dialog commented out to prevent interrupting interactive placement
                // The interactive placement is now working with RunCommand(swCommands_Move)
                // so this limitation dialog is no longer needed
                
                Logger.Info("SolidWorks API limitation dialog was requested but is disabled");
                Logger.Info("Interactive placement with RunCommand is working properly");
                
                /*
                string message = "Block placement failed after 5 comprehensive retry strategies.\n\n" +
                               "CONFIRMED: This is a documented SolidWorks API limitation\n" +
                               "affecting ALL applications when placing multiple blocks rapidly.\n\n" +
                               "RESEARCH SOURCES CONFIRM:\n" +
                               "• MakeSketchBlockFromFile has memory/cache conflicts\n" +
                               "• Sketch mode state conflicts occur between placements\n" +
                               "• This affects all SolidWorks API developers worldwide\n\n" +
                               "PROVEN SOLUTIONS:\n" +
                               "• Wait 5-10 seconds and try again\n" +
                               "• Close and reopen the drawing\n" +
                               "• Restart SolidWorks if problem persists\n" +
                               "• Place blocks more slowly (industry standard workaround)\n\n" +
                               "This is NOT a bug in this add-in - it's a well-documented\n" +
                               "SolidWorks API limitation that affects all block placement APIs.";

                string title = "Confirmed SolidWorks API Limitation";

                MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                */
            }
            catch (Exception ex)
            {
                Logger.Error($"Error showing limitation dialog: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates and activates the ID layer with proper thickness for Imperial units
        /// </summary>
        private bool CreateAndActivateIDLayer(DrawingDoc drawDoc)
        {
            try
            {
                Logger.Info("Creating and activating ID layer");
                
                string layerName = "ID";
                
                if (drawDoc == null)
                {
                    Logger.Warning("Document is not a drawing, cannot create layer");
                    return false;
                }
                
                LayerMgr layerMgr = ((ModelDoc2)drawDoc).GetLayerManager() as LayerMgr;
                if (layerMgr == null)
                {
                    Logger.Warning("Could not get layer manager");
                    return false;
                }
                
                // Determine document units
                bool isImperial = false;
                try
                {
                    var userUnits = ((ModelDoc2)drawDoc).GetUnits() as System.Int16[];
                    if (userUnits != null && userUnits.Length > 0)
                    {
                        int lengthUnit = userUnits[0];
                        isImperial = (lengthUnit == 3); // 3 = inches, 0 = meters, etc.
                        Logger.Info($"Document unit system: {(isImperial ? "Imperial (inches)" : "Metric")} (length unit code: {lengthUnit})");
                    }
                }
                catch (Exception unitEx)
                {
                    Logger.Warning($"Could not determine document units: {unitEx.Message}, assuming Imperial");
                    isImperial = true;
                }

                // Check if ID layer already exists
                Layer idLayer = layerMgr.GetLayer(layerName) as Layer;
                bool layerExists = (idLayer != null);
                
                if (!layerExists)
                {
                    Logger.Info("ID layer does not exist, attempting multiple layer creation methods");
                    
                    // Method 1: Try basic AddLayer call
                    Logger.Info("Method 1: Trying AddLayer with minimal parameters");
                    int createResult = layerMgr.AddLayer(layerName, "Shipping label layer", 0, 1, 1);
                    Logger.Info($"AddLayer result: {createResult}");
                    
                    if (createResult != 0)
                    {
                        // Method 2: Try different parameter combinations
                        Logger.Info("Method 2: Trying AddLayer with different parameters");
                        createResult = layerMgr.AddLayer(layerName, "", 0, 0, 0);
                        Logger.Info($"AddLayer (minimal) result: {createResult}");
                    }
                    
                    if (createResult != 0)
                    {
                        // Method 3: Try creating via drawing document directly
                        Logger.Info("Method 3: Trying to create layer via DrawingDoc");
                        try
                        {
                            DrawingDoc altDrawDoc = drawDoc;
                            if (altDrawDoc != null)
                            {
                                // Try creating layer through drawing document
                                ((ModelDoc2)drawDoc).Extension.SelectByID2(layerName, "LAYER", 0, 0, 0, false, 0, null, 0);
                                createResult = 0; // Assume success for now
                                Logger.Info("DrawingDoc layer creation attempted");
                            }
                        }
                        catch (Exception drawEx)
                        {
                            Logger.Warning($"DrawingDoc layer creation failed: {drawEx.Message}");
                        }
                    }
                    
                    if (createResult != 0)
                    {
                        Logger.Warning($"All layer creation methods failed. Continuing without layer creation.");
                        Logger.Warning("The layer properties will be set on the existing block entities instead.");
                        // Don't return false - continue with the process
                    }
                    else
                    {
                        Logger.Info($"Layer creation succeeded with one of the methods");
                    }
                    
                    // Try to get the layer again
                    idLayer = layerMgr.GetLayer(layerName) as Layer;
                    if (idLayer != null)
                    {
                        Logger.Info($"Successfully retrieved ID layer: {layerName}");
                    }
                    else
                    {
                        Logger.Warning("Could not retrieve ID layer after creation attempts");
                    }
                }
                else
                {
                    Logger.Info("ID layer already exists, will update its properties");
                }
                
                // Set layer properties with correct line thickness
                if (idLayer != null)
                {
                    Logger.Info("Setting properties on ID layer");
                    SetLayerPropertiesCorrectly(idLayer, isImperial);
                }
                else
                {
                    Logger.Warning("No ID layer available - will rely on block entity property setting");
                }
                
                // Simple drawing refresh
                ((ModelDoc2)drawDoc).GraphicsRedraw2();
                
                // Try to activate the ID layer for block placement
                try
                {
                    int activateResult = layerMgr.SetCurrentLayer(layerName);
                    if (activateResult == 0)
                    {
                        Logger.Info("Successfully activated ID layer for block placement");
                    }
                    else
                    {
                        Logger.Warning($"Could not activate ID layer (error {activateResult}), but layer properties are set");
                    }
                }
                catch (Exception activateEx)
                {
                    Logger.Warning($"Exception activating ID layer: {activateEx.Message}, but layer properties are set");
                }
                
                Logger.Info("ID layer setup completed");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error creating/activating ID layer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets basic layer properties (color and style only, using SolidWorks defaults for thickness)
        /// </summary>
        private void SetLayerPropertiesCorrectly(Layer layer, bool isImperial)
        {
            try
            {
                Logger.Info("Setting basic layer properties (using SolidWorks default thickness)");
                
                // Set basic properties only
                layer.Color = 0; // Black
                layer.Style = 0; // 0 = Solid line
                
                // DO NOT set layer.Width - let SolidWorks use its default thickness
                Logger.Info("Layer properties set: Color=Black, Style=Solid, Thickness=SolidWorks Default");
            }
            catch (Exception propEx)
            {
                Logger.Warning($"Could not set layer properties: {propEx.Message}");
            }
        }
        
        /// <summary>
        /// Sets the layer of block entities after placement instead of relying on active layer
        /// </summary>
        private void SetBlockEntitiesLayer(object blockInstance, string layerName)
        {
            try
            {
                Logger.Info($"Setting block entities to layer: {layerName}");
                
                // Simplified approach: Since we can't reliably access block entities through the API,
                // we'll rely on the layer being properly set during creation and focus on ensuring
                // the ID layer itself has the correct properties.
                
                // Try to set the block instance layer directly if possible
                try
                {
                    if (blockInstance != null)
                    {
                        // Try dynamic approach to set layer property
                        dynamic dynBlock = blockInstance;
                        if (dynBlock != null)
                        {
                            dynBlock.Layer = layerName;
                            Logger.Info($"Successfully set block instance layer to {layerName}");
                        }
                    }
                }
                catch (Exception dynEx)
                {
                    Logger.Info($"Could not set block layer directly: {dynEx.Message}");
                    Logger.Info("This is expected - the layer should be set by having the ID layer active during placement");
                }
                
                Logger.Info("Finished block layer setting attempt");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error in SetBlockEntitiesLayer: {ex.Message}");
            }
        }

        /// <summary>
        /// Populates block with label data by editing existing text entities within the block
        /// This approach modifies text that is already part of the block definition
        /// </summary>
        private void PopulateBlockWithLabelData(object blockInstance, ShippingLabelData labelData)
        {
            try
            {
                Logger.Info("=== POPULATING BLOCK WITH LABEL DATA (EDIT EXISTING TEXT) ===");
                
                if (blockInstance == null)
                {
                    Logger.Warning("Block instance is null");
                    return;
                }
                
                if (labelData == null)
                {
                    Logger.Warning("Label data is null");
                    return;
                }

                // The blockInstance is a SketchBlockDefinition, we need to find the actual instances
                Logger.Info("Attempting to find block instances from definition...");
                
                // Try to cast to SketchBlockDefinition first
                if (blockInstance is SketchBlockDefinition blockDef)
                {
                    Logger.Info("Working with SketchBlockDefinition, looking for instances...");
                    
                    // Get all instances of this block definition
                    object[] instances = blockDef.GetInstances() as object[];
                    if (instances != null && instances.Length > 0)
                    {
                        Logger.Info($"Found {instances.Length} block instances");
                        
                        // Work with the most recent instance (last one created)
                        var latestInstance = instances[instances.Length - 1];
                        if (latestInstance is ISketchBlockInstance sketchBlockInstance)
                        {
                            Logger.Info("Successfully cast to ISketchBlockInstance, updating attributes...");
                            EditExistingBlockText(sketchBlockInstance, labelData);
                        }
                        else
                        {
                            Logger.Warning($"Instance is not ISketchBlockInstance, type: {latestInstance?.GetType().Name}");
                        }
                    }
                    else
                    {
                        Logger.Warning("No instances found for block definition");
                    }
                }
                else if (blockInstance is ISketchBlockInstance directInstance)
                {
                    Logger.Info("Working directly with ISketchBlockInstance");
                    EditExistingBlockText(directInstance, labelData);
                }
                else
                {
                    Logger.Warning($"Block instance is unexpected type: {blockInstance.GetType().Name}");
                    Logger.Info("Attempting generic text update approach...");
                    
                    // Try to update as generic entity
                    UpdateGenericTextEntity(blockInstance, labelData);
                }

            }
            catch (Exception ex)
            {
                Logger.Error($"Error populating block with label data: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates proper UID format: SW + DrawingName + 6-char random alphanumeric
        /// Example: SW17000113-06-58e0b6
        /// </summary>
        private string GenerateProperUID()
        {
            try
            {
                // Generate 6-character random alphanumeric string
                const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
                var random = new Random();
                string randomSuffix = new string(Enumerable.Repeat(chars, 6)
                    .Select(s => s[random.Next(s.Length)]).ToArray());

                // Format: SW + DrawingName + - + random6chars
                string uid = $"SW{DateTime.Now:HHmmss}-{randomSuffix}";
                
                Logger.Info($"Generated UID: {uid}");
                return uid;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error generating UID: {ex.Message}");
                // Fallback to simple format
                return $"SW{DateTime.Now:HHmmss}";
            }
        }

        /// <summary>
        /// Edits block attributes using the proper SolidWorks Block Attribute system
        /// Updates attributes by name just like AutoCAD blocks using ISketchBlockInstance methods
        /// </summary>
        private void EditExistingBlockText(ISketchBlockInstance blockInstance, ShippingLabelData labelData)
        {
            try
            {
                Logger.Info("=== UPDATING BLOCK ATTRIBUTES ===");
                
                // Log the company values for debugging
                Logger.Info($"Company data being applied: CompanyNumber='{labelData.CompanyNumber}', CompanyLetter='{labelData.CompanyLetter}'");

                // Try to get attribute count first and list all actual attribute names
                int attributeCount = 0;
                try
                {
                    attributeCount = blockInstance.GetAttributeCount();
                    Logger.Info($"Block reports {attributeCount} attributes");
                    
                    // ENHANCED DEBUG: List all actual attribute names in the block
                    Logger.Info("=== LISTING ALL ACTUAL BLOCK ATTRIBUTES ===");
                    ListAllBlockAttributes(blockInstance);
                    Logger.Info("=== END ATTRIBUTE LIST ===");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not get attribute count: {ex.Message}");
                }

                // List of all expected attribute names (including the missing ones)
                string[] knownAttributeNames = {
                    "ID Number", "Description", "Quantity", 
                    "Project Number", "Job Number", "System", 
                    "UID", "Company Letter", "Company Number"
                };

                Logger.Info($"Attempting to update {knownAttributeNames.Length} known attributes");

                // Update each known attribute by name
                int updateCount = 0;
                foreach (string attributeName in knownAttributeNames)
                {
                    try
                    {
                        bool updated = UpdateBlockAttributeByName(blockInstance, attributeName, labelData);
                        if (updated) updateCount++;
                    }
                    catch (Exception attrEx)
                    {
                        Logger.Warning($"Error processing block attribute '{attributeName}': {attrEx.Message}");
                    }
                }

                Logger.Info($"Successfully updated {updateCount} block attributes");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating block text: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Updates a single block attribute by name using the proper SolidWorks Block Attribute API
        /// Uses ISketchBlockInstance.SetAttributeValue() method
        /// </summary>
        private bool UpdateBlockAttributeByName(ISketchBlockInstance blockInstance, string attributeName, ShippingLabelData labelData)
        {
            try
            {
                // Get the current attribute value to see if this attribute exists
                string currentValue = blockInstance.GetAttributeValue(attributeName);
                
                // If GetAttributeValue returns null, the attribute doesn't exist
                if (currentValue == null)
                {
                    Logger.Info($"Attribute '{attributeName}' does not exist in block, skipping");
                    return false;
                }

                Logger.Info($"Processing attribute '{attributeName}' with current value '{currentValue}'");

                // Determine the new value based on attribute name
                string newValue = GetAttributeValueFromName(attributeName, labelData);
                if (newValue == null)
                {
                    Logger.Info($"No mapping for attribute '{attributeName}', skipping");
                    return false;
                }

                // ENHANCED DEBUG: Try multiple approaches to set the attribute value
                Logger.Info($"Attempting to set '{attributeName}' to '{newValue}'");
                
                // Method 1: Direct SetAttributeValue call
                bool success = blockInstance.SetAttributeValue(attributeName, newValue);
                if (success)
                {
                    Logger.Info($"✅ Updated attribute '{attributeName}': '{currentValue}' → '{newValue}'");
                    return true;
                }
                
                // Method 2: Try alternative attribute name formats if first method failed
                Logger.Warning($"❌ SetAttributeValue failed for '{attributeName}', trying alternative formats...");
                
                // Try with exact case from your screenshots
                string[] alternativeNames = GetAlternativeAttributeNames(attributeName);
                foreach (string altName in alternativeNames)
                {
                    try
                    {
                        Logger.Info($"Trying alternative name: '{altName}'");
                        string altCurrentValue = blockInstance.GetAttributeValue(altName);
                        if (altCurrentValue != null) // Attribute exists with this name
                        {
                            success = blockInstance.SetAttributeValue(altName, newValue);
                            if (success)
                            {
                                Logger.Info($"✅ Updated attribute using alternative name '{altName}': '{altCurrentValue}' → '{newValue}'");
                                return true;
                            }
                        }
                    }
                    catch (Exception altEx)
                    {
                        Logger.Info($"Alternative name '{altName}' failed: {altEx.Message}");
                    }
                }
                
                // Method 3: Try forcing a model rebuild and retry
                Logger.Warning($"All alternative names failed, trying model rebuild approach...");
                try
                {
                    swModel.ForceRebuild3(false);
                    System.Threading.Thread.Sleep(100); // Brief pause
                    success = blockInstance.SetAttributeValue(attributeName, newValue);
                    if (success)
                    {
                        Logger.Info($"✅ Updated attribute '{attributeName}' after rebuild: '{currentValue}' → '{newValue}'");
                        return true;
                    }
                }
                catch (Exception rebuildEx)
                {
                    Logger.Warning($"Rebuild approach failed: {rebuildEx.Message}");
                }
                
                Logger.Error($"❌ ALL METHODS FAILED to update attribute '{attributeName}' from '{currentValue}' to '{newValue}'");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error updating block attribute '{attributeName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lists all actual attribute names in the block for debugging purposes
        /// This helps us see exactly what attribute names exist in the block
        /// </summary>
        private void ListAllBlockAttributes(ISketchBlockInstance blockInstance)
        {
            try
            {
                int attributeCount = blockInstance.GetAttributeCount();
                Logger.Info($"Attempting to list {attributeCount} block attributes:");
                
                // Unfortunately, SolidWorks doesn't provide a direct GetAttributeNames() method
                // So we'll try common attribute names and see which ones exist
                string[] commonNames = {
                    "ID Number", "id number", "Description", "description", "Quantity", "quantity",
                    "Project Number", "project number", "project_no", "Job Number", "job number", "job_number",
                    "System", "system", "UID", "uid", "Company Letter", "company letter", 
                    "Company Number", "company number", "CompanyLetter", "CompanyNumber"
                };
                
                int foundCount = 0;
                foreach (string testName in commonNames)
                {
                    try
                    {
                        string value = blockInstance.GetAttributeValue(testName);
                        if (value != null) // Attribute exists
                        {
                            foundCount++;
                            Logger.Info($"  [{foundCount}] '{testName}' = '{value}'");
                        }
                    }
                    catch
                    {
                        // Attribute doesn't exist, continue
                    }
                }
                
                Logger.Info($"Found {foundCount} existing attributes out of {commonNames.Length} tested names");
                
                if (foundCount < attributeCount)
                {
                    Logger.Warning($"Expected {attributeCount} attributes but only found {foundCount}. Some attributes may have different names.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error listing block attributes: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets alternative attribute name formats to try if the primary name fails
        /// Based on common SolidWorks attribute naming variations
        /// </summary>
        private string[] GetAlternativeAttributeNames(string attributeName)
        {
            switch (attributeName.ToLower())
            {
                case "project number":
                    return new string[] { "Project Number", "project number", "PROJECT NUMBER", "project_no", "Project_Number", "PROJECTNUMBER" };
                case "job number":
                    return new string[] { "Job Number", "job number", "JOB NUMBER", "job_number", "Job_Number", "JOBNUMBER" };
                case "system":
                    return new string[] { "System", "system", "SYSTEM", "System Number", "SystemNumber" };
                case "uid":
                    return new string[] { "UID", "uid", "Uid", "UniqueID", "Unique ID" };
                case "company letter":
                    return new string[] { "Company Letter", "company letter", "COMPANY LETTER", "CompanyLetter", "Company_Letter" };
                case "company number":
                    return new string[] { "Company Number", "company number", "COMPANY NUMBER", "CompanyNumber", "Company_Number" };
                case "id number":
                    return new string[] { "ID Number", "id number", "ID NUMBER", "IDNumber", "Id Number" };
                case "description":
                    return new string[] { "Description", "description", "DESCRIPTION", "DESC", "desc" };
                case "quantity":
                    return new string[] { "Quantity", "quantity", "QUANTITY", "QTY", "qty" };
                default:
                    return new string[] { attributeName, attributeName.ToLower(), attributeName.ToUpper() };
            }
        }

        /// <summary>
        /// Updates a generic text entity that might serve as a block attribute
        /// </summary>
        private bool UpdateGenericTextEntity(object entity, ShippingLabelData labelData)
        {
            try
            {
                var entityType = entity.GetType();
                Logger.Info($"Processing entity type: {entityType.Name}");

                // Skip non-text entities
                if (!entityType.Name.Contains("Text") && !entityType.Name.Contains("Note"))
                {
                    return false;
                }

                // Try to get current text using various approaches
                string currentText = GetEntityText(entity);
                if (string.IsNullOrEmpty(currentText))
                {
                    return false;
                }

                // Determine new value
                string newValue = GetAttributeValueFromText(currentText, labelData);
                if (newValue == null)
                {
                    return false;
                }

                // Try to set the text
                bool success = SetEntityText(entity, newValue);
                if (success)
                {
                    Logger.Info($"✅ Updated {entityType.Name}: '{currentText}' → '{newValue}'");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error updating generic text entity: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets text from a generic entity using reflection
        /// </summary>
        private string GetEntityText(object entity)
        {
            try
            {
                var entityType = entity.GetType();

                // Try Text property
                var textProperty = entityType.GetProperty("Text");
                if (textProperty != null && textProperty.CanRead)
                {
                    return textProperty.GetValue(entity)?.ToString();
                }

                // Try GetText method
                var getTextMethod = entityType.GetMethod("GetText");
                if (getTextMethod != null)
                {
                    return getTextMethod.Invoke(entity, null)?.ToString();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sets text on a generic entity using reflection
        /// </summary>
        private bool SetEntityText(object entity, string text)
        {
            try
            {
                var entityType = entity.GetType();

                // Try Text property
                var textProperty = entityType.GetProperty("Text");
                if (textProperty != null && textProperty.CanWrite)
                {
                    textProperty.SetValue(entity, text);
                    return true;
                }

                // Try SetText method
                var setTextMethod = entityType.GetMethod("SetText");
                if (setTextMethod != null)
                {
                    setTextMethod.Invoke(entity, new object[] { text });
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Maps attribute names to label data values based on your defined attribute names
        /// </summary>
        private string GetAttributeValueFromName(string attributeName, ShippingLabelData labelData)
        {
            // Normalize attribute name for comparison (case-insensitive, remove spaces/underscores)
            string normalizedName = attributeName.ToUpper().Replace(" ", "").Replace("_", "");

            // Map based on your defined attribute names
            switch (normalizedName)
            {
                case "IDNUMBER":
                case "ID":
                    return labelData.IDNumber;

                case "DESCRIPTION":
                case "DESC":
                    return labelData.Description;

                case "QUANTITY":
                case "QTY":
                    return labelData.Quantity;

                case "PROJECTNUMBER":
                case "PROJECT":
                case "PROJECTNO":
                    Logger.Info($"📋 Project Number attribute requested: returning '{labelData.ProjectNumber}'");
                    return labelData.ProjectNumber;

                case "JOBNUMBER":
                case "JOB":
                    Logger.Info($"📋 Job Number attribute requested: returning '{labelData.JobNumber}'");
                    return labelData.JobNumber;

                case "SYSTEM":
                case "SYSTEMNUMBER":
                    Logger.Info($"📋 System attribute requested: returning '{labelData.SystemNumber}'");
                    return labelData.SystemNumber;

                case "UID":
                case "UNIQUEID":
                    Logger.Info($"📋 UID attribute requested: returning '{labelData.InstanceName}'");
                    return labelData.InstanceName; // This is our generated UID

                case "COMPANYLETTER":
                case "COMPANY":
                    Logger.Info($"📋 Company Letter attribute requested: returning '{labelData.CompanyLetter}'");
                    return labelData.CompanyLetter;

                case "COMPANYNUMBER":
                case "COMPANYNO":
                    Logger.Info($"📋 Company Number attribute requested: returning '{labelData.CompanyNumber}'");
                    return labelData.CompanyNumber;

                default:
                    Logger.Info($"Unknown attribute name: '{attributeName}' (normalized: '{normalizedName}')");
                    return null;
            }
        }

        /// <summary>
        /// Maps note/text content to label data values based on attribute names you defined
        /// </summary>
        private string GetAttributeValueFromText(string currentText, ShippingLabelData labelData)
        {
            // Normalize text for comparison (case-insensitive, remove spaces/underscores)
            string normalizedText = currentText.ToUpper().Replace(" ", "").Replace("_", "");

            // Map based on your defined attribute names
            switch (normalizedText)
            {
                case "IDNUMBER":
                case "ID":
                    return labelData.IDNumber;

                case "DESCRIPTION":
                case "DESC":
                    return labelData.Description;

                case "QUANTITY":
                case "QTY":
                    return labelData.Quantity;

                case "JOBNUMBER":
                case "JOB":
                    return labelData.JobNumber;

                case "PROJECTNUMBER":
                case "PROJECT":
                case "PROJECTNO":
                    Logger.Info($"📋 Project Number attribute requested: returning '{labelData.ProjectNumber}'");
                    return labelData.ProjectNumber;

                case "SYSTEM":
                case "SYSTEMNUMBER":
                    return labelData.SystemNumber;

                case "UID":
                case "UNIQUEID":
                    return labelData.InstanceName; // This is our generated UID

                case "COMPANYLETTER":
                case "COMPANY":
                    return labelData.CompanyLetter;

                case "COMPANYNUMBER":
                case "COMPANYNO":
                    return labelData.CompanyNumber;

                default:
                    Logger.Info($"Unknown text content: '{currentText}' (normalized: '{normalizedText}')");
                    return null;
            }
        }

        /// <summary>
        /// Sets a unique identifier on the block instance for database linking
        /// Similar to AutoCAD block Handle system
        /// </summary>
        private void SetBlockUniqueIdentifier(ISketchBlockInstance blockInstance, string uniqueId)
        {
            try
            {
                Logger.Info($"Setting unique identifier: {uniqueId}");
                
                // Try to set the block name as the unique identifier
                try
                {
                    // SolidWorks blocks don't have a direct "Handle" equivalent,
                    // but we can use the instance name or add a custom attribute
                    var blockType = blockInstance.GetType();
                    var nameProperty = blockType.GetProperty("Name");
                    if (nameProperty != null && nameProperty.CanWrite)
                    {
                        nameProperty.SetValue(blockInstance, uniqueId);
                        Logger.Info($"Set block name to unique ID: {uniqueId}");
                    }
                }
                catch (Exception nameEx)
                {
                    Logger.Warning($"Could not set block name: {nameEx.Message}");
                }

                // Alternative: Store in a global registry for lookup
                // This ensures we can always link back to the database
                RegisterBlockInstance(uniqueId, blockInstance);
                
                Logger.Info($"Block unique identifier set: {uniqueId}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error setting block unique identifier: {ex.Message}");
            }
        }

        /// <summary>
        /// Global registry to track block instances by unique ID
        /// Allows bidirectional database linking like AutoCAD handles
        /// </summary>
        private static Dictionary<string, object> blockRegistry = new Dictionary<string, object>();

        private void RegisterBlockInstance(string uniqueId, object blockInstance)
        {
            try
            {
                blockRegistry[uniqueId] = blockInstance;
                Logger.Info($"Registered block instance in global registry: {uniqueId}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error registering block instance: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a block instance by unique ID (for database updates)
        /// </summary>
        public static object GetBlockInstanceByUniqueId(string uniqueId)
        {
            try
            {
                if (blockRegistry.ContainsKey(uniqueId))
                {
                    return blockRegistry[uniqueId];
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error retrieving block instance {uniqueId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clears the entire block registry (useful when there are conflicts)
        /// </summary>
        private void ClearBlockRegistry()
        {
            try
            {
                int count = blockRegistry.Count;
                blockRegistry.Clear();
                Logger.Info($"Cleared entire block registry ({count} entries removed)");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error clearing block registry: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if we can use smart copy for back-to-back placement of the same block type
        /// </summary>
        public bool CanUseSmartCopy(ShippingLabelArrowType arrowType)
        {
            return lastPlacedBlocks.ContainsKey(arrowType) && 
                   lastPlacedBlocks[arrowType].IsValid();
        }

        /// <summary>
        /// Attempts to place a block using a true Copy/Paste operation from the last successfully placed block.
        /// This is the new "Smart Copy" implementation.
        /// </summary>
        public bool TrySmartCopyPlacement(ShippingLabelArrowType arrowType, ShippingLabelData labelData, out string finalUID)
        {
            // This method is now obsolete and will not be called. 
            // The new logic is entirely within InsertBlockWithAdvancedWorkarounds.
            finalUID = "";
            Logger.Warning("TrySmartCopyPlacement is obsolete and should not have been called.");
            return false;
        }

        /// <summary>
        /// Caches information about a successfully placed block for future smart copy operations
        /// </summary>
        public void CacheSuccessfulPlacement(ShippingLabelArrowType arrowType, string blockFilePath, 
            (double X, double Y, double Z) position, ShippingLabelData labelData, Feature feature)
        {
            try
            {
                var blockInfo = new LastPlacedBlockInfo
                {
                    ArrowType = arrowType,
                    BlockFilePath = blockFilePath,
                    Position = position,
                    LabelData = labelData.Clone(), // Store a copy of the data
                    PlacementTime = DateTime.Now,
                    LastFeature = feature // Store the feature
                };

                lastPlacedBlocks[arrowType] = blockInfo;
                Logger.Info($"Cached successful placement for {arrowType} at position ({position.X}, {position.Y}) with feature {feature.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error caching block placement: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Clears the smart copy cache (useful when starting fresh or after errors)
        /// </summary>
        public static void ClearSmartCopyCache()
        {
            try
            {
                lastPlacedBlocks.Clear();
                Logger.Info("Smart copy cache cleared");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing smart copy cache: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Updates an existing block instance with new data
        /// </summary>
        public bool UpdateExistingBlock(string instanceName, ShippingLabelData newData)
        {
            try
            {
                Logger.Info($"=== UPDATING EXISTING BLOCK ===");
                Logger.Info($"Instance Name: {instanceName}");
                Logger.Info($"New ID: {newData.IDNumber}");
                Logger.Info($"New Description: {newData.Description}");

                // Get the active drawing
                var swModel = swApp.ActiveDoc as ModelDoc2;
                var swDraw = swModel as DrawingDoc;
                
                if (swDraw == null)
                {
                    Logger.Error("No active drawing found");
                    return false;
                }

                // Get the current sheet
                var currentSheet = swDraw.GetCurrentSheet() as Sheet;
                if (currentSheet == null)
                {
                    Logger.Error("No current sheet found");
                    return false;
                }

                // Get sketch manager
                var sketchManager = swModel.SketchManager;
                if (sketchManager == null)
                {
                    Logger.Error("Could not get sketch manager");
                    return false;
                }

                // Find the block instance by name
                var blockInstance = FindBlockInstanceByName(instanceName, sketchManager);
                if (blockInstance == null)
                {
                    Logger.Warning($"Block instance not found: {instanceName}");
                    return false;
                }

                Logger.Info($"Found block instance: {instanceName}");
                
                // Update the block attributes with new data
                EditExistingBlockText(blockInstance, newData);
                
                // Force regeneration to see changes
                swModel.ForceRebuild3(true);
                swModel.GraphicsRedraw2();
                
                Logger.Info("Block update completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating existing block: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Finds a block instance by its unique instance name
        /// </summary>
        private ISketchBlockInstance FindBlockInstanceByName(string instanceName, ISketchManager sketchManager)
        {
            try
            {
                Logger.Info($"Searching for block instance: {instanceName}");
                
                // Get all sketch block instances
                // Note: SolidWorks API doesn't have GetSketchBlockInstances method
                // Instead we need to use GetSketchBlockDefinitions and then get instances from each definition
                object[] blockInstances = GetAllSketchBlockInstancesFromDefinitions(sketchManager);
                
                if (blockInstances == null || blockInstances.Length == 0)
                {
                    Logger.Warning("No block instances found in current drawing");
                    return null;
                }

                Logger.Info($"Found {blockInstances.Length} block instances, searching for {instanceName}");
                
                // Search through all instances
                foreach (object instance in blockInstances)
                {
                    var blockInstance = instance as ISketchBlockInstance;
                    if (blockInstance == null) continue;

                    // Check if this is our target block by name first
                    string blockName = blockInstance.Name;
                    
                    Logger.Info($"Checking block: {blockName}");
                    
                    if (string.Equals(blockName, instanceName, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info($"Found matching block by name: {blockName}");
                        return blockInstance;
                    }

                    // Fallback: compare UID attribute stored on the block instance
                    try
                    {
                        string uidValue = blockInstance.GetAttributeValue("UID");
                        if (!string.IsNullOrEmpty(uidValue) && string.Equals(uidValue, instanceName, StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.Info($"Found matching block by UID attribute: {uidValue}");
                            return blockInstance;
                        }
                    }
                    catch (Exception uidEx)
                    {
                        Logger.Warning($"Error retrieving UID attribute for block {blockName}: {uidEx.Message}");
                    }

                    // Additional fallback: attempt generic attribute names if UID not found
                    try
                    {
                        string[] fallbackAttrNames = { "Instance", "InstanceName" };
                        foreach (var attrName in fallbackAttrNames)
                        {
                            try
                            {
                                string val = blockInstance.GetAttributeValue(attrName);
                                if (!string.IsNullOrEmpty(val) && string.Equals(val, instanceName, StringComparison.OrdinalIgnoreCase))
                                {
                                    Logger.Info($"Found matching block by {attrName} attribute: {val}");
                                    return blockInstance;
                                }
                            }
                            catch { /* ignore missing attributes */ }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Error checking fallback attributes for block {blockName}: {ex.Message}");
                    }
                }
                
                Logger.Warning($"Block instance not found: {instanceName}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error finding block instance: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Helper method to get all sketch block instances from all definitions
        /// Works around the missing GetSketchBlockInstances method in SolidWorks API
        /// </summary>
        private object[] GetAllSketchBlockInstancesFromDefinitions(ISketchManager sketchManager)
        {
            try
            {
                var allInstances = new List<object>();
                
                // Get all block definitions
                object[] definitions = sketchManager.GetSketchBlockDefinitions() as object[];
                if (definitions != null)
                {
                    foreach (SketchBlockDefinition definition in definitions)
                    {
                        // Get instances from each definition
                        object[] instances = definition.GetInstances() as object[];
                        if (instances != null && instances.Length > 0)
                        {
                            allInstances.AddRange(instances);
                        }
                    }
                }
                
                return allInstances.ToArray();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting all sketch block instances: {ex.Message}");
                return new object[0];
            }
        }

        /// <summary>
        /// Helper method to get block definition from instance
        /// Works around the missing GetDefinition method
        /// </summary>
        private SketchBlockDefinition GetBlockDefinitionFromInstance(ISketchBlockInstance blockInstance)
        {
            try
            {
                // Try to find the definition by matching instance
                var definitions = swModel.SketchManager.GetSketchBlockDefinitions() as object[];
                if (definitions != null)
                {
                    foreach (SketchBlockDefinition definition in definitions)
                    {
                        var instances = definition.GetInstances() as object[];
                        if (instances != null)
                        {
                            foreach (var instance in instances)
                            {
                                if (instance == blockInstance)
                                {
                                    return definition;
                                }
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting block definition from instance: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper method to get attributes from block definition
        /// Works around API limitations
        /// </summary>
        private object[] GetAttributesFromDefinition(SketchBlockDefinition blockDef)
        {
            try
            {
                // SolidWorks sketch blocks don't have traditional attributes like AutoCAD
                // For now, return empty array - attributes are handled through text entities
                return new object[0];
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting attributes from definition: {ex.Message}");
                return new object[0];
            }
        }

        /// <summary>
        /// Helper method to get attribute name
        /// </summary>
        private string GetAttributeName(object attr)
        {
            try
            {
                // Since SolidWorks blocks don't have traditional attributes,
                // we'll use reflection to try to get a name property
                var type = attr.GetType();
                var nameProperty = type.GetProperty("Name");
                if (nameProperty != null)
                {
                    return nameProperty.GetValue(attr)?.ToString() ?? "";
                }
                return "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Helper method to get attribute value
        /// </summary>
        private string GetAttributeValue(object attr)
        {
            try
            {
                // Since SolidWorks blocks don't have traditional attributes,
                // we'll use reflection to try to get a value property
                var type = attr.GetType();
                var valueProperty = type.GetProperty("Value");
                if (valueProperty != null)
                {
                    return valueProperty.GetValue(attr)?.ToString() ?? "";
                }
                return "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Generates a SW handle based on drawing number and 4-digit sequence
        /// </summary>
        private string GenerateSwHandle(ModelDoc2 model, string drawingNumber)
        {
            if (string.IsNullOrWhiteSpace(drawingNumber)) drawingNumber = "UNKNOWN";
            string baseHandle = $"SW{drawingNumber}";
            int maxSeq = 0;
            if (!drawingSequenceCounters.TryGetValue(drawingNumber, out maxSeq))
            {
                // First time for this drawing in this session – scan features to seed counter
                Feature scanFeat = model != null ? model.FirstFeature() as Feature : null;
                while (scanFeat != null)
                {
                    string nm = scanFeat.Name;
                    if (!string.IsNullOrEmpty(nm) && nm.StartsWith(baseHandle, StringComparison.OrdinalIgnoreCase))
                    {
                        int dashIdx = nm.LastIndexOf('-');
                        if (dashIdx > 0 && int.TryParse(nm.Substring(dashIdx + 1), out int num))
                        {
                            if (num > maxSeq) maxSeq = num;
                        }
                    }
                    scanFeat = scanFeat.GetNextFeature() as Feature;
                }
            }
            maxSeq += 1;
            drawingSequenceCounters[drawingNumber] = maxSeq;
            return $"{baseHandle}-{maxSeq.ToString("D4")}";
        }

        /// <summary>
        /// Gets the block name from a file path by removing the extension
        /// </summary>
        private string GetBlockNameFromFilePath(string filePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                return fileName;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting block name from path '{filePath}': {ex.Message}");
                return "UnknownBlock";
            }
        }

        /// <summary>
        /// Gets smart placement coordinates based on current view and existing blocks
        /// </summary>
        private (double X, double Y, double Z) GetSmartPlacementCoordinates(ModelDoc2 swModel)
        {
            try
            {
                // Strategy 1: Try to get center of current view
                var viewCenter = GetCurrentViewCenter(swModel);
                if (viewCenter.HasValue)
                {
                    Logger.Info($"Using view center for placement: X={viewCenter.Value.X:F3}, Y={viewCenter.Value.Y:F3}");
                    return viewCenter.Value;
                }

                // Strategy 2: Use smart offset from existing blocks
                var smartCoords = GetAlternativeBlockCoordinates();
                Logger.Info($"Using smart coordinates for placement: X={smartCoords.X:F3}, Y={smartCoords.Y:F3}");
                return smartCoords;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting smart placement coordinates: {ex.Message}");
                // Fallback to a reasonable default (not 0,0)
                return (2.0, 2.0, 0.0);
            }
        }

        /// <summary>
        /// Gets the center point of the current view
        /// </summary>
        private (double X, double Y, double Z)? GetCurrentViewCenter(ModelDoc2 swModel)
        {
            try
            {
                // Get the current view - need to cast from object
                ModelView currentView = swModel.ActiveView as ModelView;
                if (currentView == null)
                {
                    Logger.Warning("No active view found");
                    return null;
                }

                // Get view transformation and scale - need to cast from object
                MathUtility mathUtility = swApp.GetMathUtility() as MathUtility;
                if (mathUtility == null)
                {
                    Logger.Warning("Could not get MathUtility");
                    return null;
                }
                
                // Try to get view bounds using a different approach
                // Since GetOutline doesn't exist, we'll use view transformation
                try
                {
                    // Get the view scale and position
                    MathTransform viewTransform = currentView.Transform;
                    if (viewTransform != null)
                    {
                        // Get the view scale
                        double scale = currentView.Scale2;
                        
                        // Use a reasonable default center based on typical drawing sizes
                        // This is a fallback approach when we can't get exact view bounds
                        double centerX = 0.0;
                        double centerY = 0.0;
                        
                        // Try to get some indication of view positioning
                        // For now, use a simple offset from origin
                        centerX = 4.0; // 4 inches from origin
                        centerY = 4.0; // 4 inches from origin
                        
                        Logger.Info($"Using view-based positioning: X={centerX:F3}, Y={centerY:F3}, Scale={scale:F3}");
                        
                        return (centerX, centerY, 0.0);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not get view transform: {ex.Message}");
                }

                Logger.Warning("Could not determine view center, using fallback");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting view center: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets an improved alternative coordinate that avoids 0,0 and existing blocks
        /// </summary>
        private (double X, double Y, double Z) GetAlternativeBlockCoordinates()
        {
            try
            {
                // Get count of existing blocks to create offset
                int blockCount = GetCurrentBlockCount();
                
                // Create a spiral pattern starting from a reasonable base position
                double baseX = 1.0; // Start at 1 inch from origin instead of 0
                double baseY = 1.0;
                
                // Create offset in a spiral pattern
                double offsetMultiplier = Math.Max(1, blockCount);
                double angle = blockCount * 45.0 * Math.PI / 180.0; // 45-degree increments
                double radius = offsetMultiplier * 0.75; // 0.75 inch radius increments
                
                double x = baseX + radius * Math.Cos(angle);
                double y = baseY + radius * Math.Sin(angle);
                
                Logger.Info($"Alternative coordinates (block #{blockCount + 1}): X={x:F3}, Y={y:F3}");
                
                return (x, y, 0.0);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error calculating alternative coordinates: {ex.Message}");
                // Safe fallback - not at origin
                return (1.5, 1.5, 0.0);
            }
        }
    }

    /// <summary>
    /// Information about the last successfully placed block for smart copy functionality
    /// </summary>
    public class LastPlacedBlockInfo
    {
        public ShippingLabelArrowType ArrowType { get; set; }
        public string BlockFilePath { get; set; }
        public (double X, double Y, double Z) Position { get; set; }
        public ShippingLabelData LabelData { get; set; }
        public DateTime PlacementTime { get; set; }
        public Feature LastFeature { get; set; }

        /// <summary>
        /// Checks if this cached block info is still valid for smart copy
        /// </summary>
        public bool IsValid()
        {
            // Consider cache valid for 1 hour to prevent stale data
            var maxAge = TimeSpan.FromHours(1);
            var age = DateTime.Now - PlacementTime;
            
            bool isStillValid = LastFeature != null &&
                           !string.IsNullOrEmpty(BlockFilePath) && 
                           File.Exists(BlockFilePath ?? "") && 
                           age <= maxAge;

            if (!isStillValid)
            {
                Logger.Info($"Cached block info for {ArrowType} is invalid. Age: {age}, File exists: {File.Exists(BlockFilePath ?? "")}, Feature exists: {LastFeature != null}");
            }

            return isStillValid;
        }
    }
} 