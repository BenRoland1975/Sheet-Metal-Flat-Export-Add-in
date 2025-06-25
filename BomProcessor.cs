using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Excel = Microsoft.Office.Interop.Excel;
using System.Runtime.InteropServices;
using System.Linq;
using System.Diagnostics;

namespace RoesleinAddIn
{
    public enum BomType
    {
        Full,
        Consolidated,
        SpareParts
    }

    public class BomItem
    {
        public string ItemNumber { get; set; } = "";
        public string PartNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public string Revision { get; set; } = "";
        public int Quantity { get; set; } = 1; // Changed to int for easier aggregation
        public string ComponentPath { get; set; } = ""; // For debugging or advanced linking
        public bool IsSparePart { get; set; } = false; // For spare parts BOM
        public int IndentLevel { get; set; } = 0; // For Full BOM hierarchy
        public bool IsAssembly { get; set; } = false; // Flag to indicate if the item represents an assembly
        public bool IsRaIndustrialBuy { get; set; } = false; // Flag for RA Industrial Buy template
    }

    public class BomProcessor
    {
        private readonly ISldWorks _swApp;
        private readonly EPDM.Interop.epdm.IEdmVault5 _pdmVault; // Add PDM vault support
        // Assuming you have a Settings class and Logger, you'd uncomment and use them.
        // private Settings _settings; 
        // private Logger _logger; // Or your specific logger instance

        public BomProcessor(ISldWorks swApp, EPDM.Interop.epdm.IEdmVault5 pdmVault = null /*, Logger logger, Settings settings */)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            _pdmVault = pdmVault;
            // _logger = logger;
            // _settings = settings;
            // Example: Logger?.Info("BomProcessor initialized.");
            Debug.WriteLine("BomProcessor initialized.");
        }

        public List<BomItem> GenerateBomData(BomType bomType, out string assemblyName)
        {
            assemblyName = "UnknownAssembly";
            ModelDoc2 swModel = _swApp.ActiveDoc as ModelDoc2;
            
            if (swModel == null)
            {
                MessageBox.Show("No active document. Please open an assembly.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return new List<BomItem>();
            }
            
            if (swModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                MessageBox.Show("Active document is not an assembly.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return new List<BomItem>();
            }
            
            assemblyName = Path.GetFileNameWithoutExtension(swModel.GetTitle());
            AssemblyDoc swAssembly = swModel as AssemblyDoc;
            Configuration activeConfig = swModel.ConfigurationManager.ActiveConfiguration;
            string activeConfigName = activeConfig.Name;

            Debug.WriteLine($"Processing BOM for assembly: {swModel.GetPathName()}, Configuration: {activeConfigName}");
            // Logger?.Info($"Processing BOM for assembly: {swModel.GetPathName()}, Configuration: {activeConfigName}");

                switch (bomType)
                {
                    case BomType.Full:
                    return CreateFullBom(swAssembly, activeConfig);
                    case BomType.Consolidated:
                    return CreateConsolidatedBom(swAssembly, activeConfig, false);
                    case BomType.SpareParts:
                    // Spare parts BOM is a consolidated BOM, filtered by the "Mark as Spare Part" property.
                    return CreateConsolidatedBom(swAssembly, activeConfig, true); 
                default:
                    Debug.WriteLine($"Unsupported BOM type: {bomType}");
                    // Logger?.Error($"Unsupported BOM type: {bomType}");
                    throw new ArgumentOutOfRangeException(nameof(bomType), "Unsupported BOM type.");
            }
        }

        private List<BomItem> CreateFullBom(AssemblyDoc swAssembly, Configuration swConfig)
        {
            var bomItems = new List<BomItem>();
            Component2 rootComponent = swConfig.GetRootComponent3(true); // Using GetRootComponent3 for better handling of hidden/suppressed components at root level

            if (rootComponent != null)
            {
                Debug.WriteLine($"Root component: {rootComponent.Name2} for configuration: {swConfig.Name}");
                object childrenVariant = rootComponent.GetChildren();
                if (childrenVariant != null && ((object[])childrenVariant).Length > 0)
                {
                    ProcessAndAddChildrenRecursive(
                        childrenVariant as object[], // Pass the children of the root
                        "",                         // Parent BOM number for top-level items is empty
                        0,                          // Indent level for top-level items
                        bomItems,                   // The list to populate
                        swConfig.Name,
                        true                        // Yes, these are top-level children
                    );
                }
                else
                {
                    Debug.WriteLine($"Root component {rootComponent.Name2} has no children or GetChildren() returned null/empty.");
                }
            }
            else
            {
                 Debug.WriteLine("Root component is null for the active configuration.");
                 // Logger?.Warning("Root component is null for the active configuration.");
            }
            return bomItems;
        }

        // Helper to recursively collect effective children, dissolving phantoms
        private void AddEffectiveChildren(Component2 component, string configName, List<Component2> effectiveChildrenList)
        {
            if (component == null) return;
            
            // Check if component should be excluded based on SW properties (e.g., "Exclude from BOM")
            // or if it's suppressed in the context of the current configuration.
            if (IsComponentSuppressedOrExcluded(component, configName))
            {
                Debug.WriteLine($"Component {component.Name2} is suppressed or excluded from BOM in config {configName}.");
                return;
            }

            ModelDoc2 model = component.GetModelDoc2() as ModelDoc2;
            if (model == null) {
                Debug.WriteLine($"Skipping component {component.Name2} in AddEffectiveChildren as its ModelDoc2 is null.");
                // Check if it's lightweight as a possible reason for GetModelDoc2 returning null initially.
                // Accessing GetModelDoc2() itself should trigger resolution if it's lightweight and resolvable.
                // If it's still null here, it means it couldn't be resolved or the file is missing.
                if (component.GetSuppression() == (int)swComponentSuppressionState_e.swComponentLightweight) { 
                    Debug.WriteLine($"Component {component.Name2} is lightweight and its ModelDoc2 could not be obtained (likely failed to resolve).");
                } else {
                    Debug.WriteLine($"Component {component.Name2} is not lightweight (Suppression State: {component.GetSuppression()}) but ModelDoc2 is null (file might be missing, or other access issue).");
                }
                return;
            }

            if (model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY && IsPhantomAssembly(component, configName))
            {
                Debug.WriteLine($"Phantom Assembly in AddEffectiveChildren: {component.Name2} (Config: {component.ReferencedConfiguration}). Promoting its children.");
                object[] grandchildren = component.GetChildren() as object[];
                if (grandchildren != null)
                {
                    foreach (object gcObj in grandchildren)
                    {
                        AddEffectiveChildren(gcObj as Component2, configName, effectiveChildrenList); // Recurse for nested phantoms
                    }
                }
            }
            else
            {
                effectiveChildrenList.Add(component);
            }
        }

        private void ProcessAndAddChildrenRecursive(
            object[] childComponentObjects,  // Raw children from GetChildren() of the parent
            string parentBomNumber,          // BOM number of the parent of these children
            int currentIndentLevel,
            List<BomItem> bomItemsList,      // The final list to populate
            string activeConfigName,         // Name of the *top-level* assembly's active configuration
            bool areTheseChildrenTopLevel    // True if these children are direct children of the root
        )
        {
            if (childComponentObjects == null || childComponentObjects.Length == 0) return;

            List<Component2> effectiveChildren = new List<Component2>();
            foreach (object childObj in childComponentObjects)
            {
                AddEffectiveChildren(childObj as Component2, activeConfigName, effectiveChildren);
            }
            
            if (effectiveChildren.Count == 0) return;

            // DEBUG: Log effective children order
            Logger.DebugLog($"[BOM_ORDER_DEBUG] ParentBomNumber: '{parentBomNumber ?? "TOP_LEVEL"}', Indent: {currentIndentLevel}, Effective Children (Name2 <Config>): " + 
                            string.Join("; ", effectiveChildren.Select(c => $"{c.Name2} <{c.ReferencedConfiguration}>")));

            var groupedChildren = new Dictionary<string, (Component2 representative, ModelDoc2 model, int quantity, string description, string revision, string path)>();
            var orderedKeys = new List<string>(); 

            foreach (Component2 effectiveChildComp in effectiveChildren)
            {
                // effectiveChildComp is already confirmed non-null, non-suppressed/excluded, and non-phantom by AddEffectiveChildren
                ModelDoc2 childModel = effectiveChildComp.GetModelDoc2() as ModelDoc2;
                if (childModel == null) {
                     Debug.WriteLine($"Unexpected null ModelDoc2 for effective child {effectiveChildComp.Name2}. Skipping. (This component might be lightweight and failed to resolve, or its file is missing/inaccessible)");
                    continue; 
                }

                // Use the component's specific configuration if it's different from the assembly's active one.
                string componentConfigName = effectiveChildComp.ReferencedConfiguration;
                if (string.IsNullOrEmpty(componentConfigName))
                {
                    componentConfigName = activeConfigName; // Fallback, though ReferencedConfiguration should be reliable
                }
                
                string partNumber = GetPartNumber(effectiveChildComp, childModel, componentConfigName); // Pass component's own config
                string description = GetPropertyValue(childModel, componentConfigName, "Description");
                string revision = GetPropertyValue(childModel, componentConfigName, "Revision");
                string path = childModel.GetPathName();

                // Ensure partNumber is not empty, as it's the key for grouping.
                if (string.IsNullOrEmpty(partNumber))
                {
                    Debug.WriteLine($"Warning: Component '{effectiveChildComp.Name2}' (Path: {path}) has an empty or null PartNumber. It will be grouped under an 'UNKNOWN_PARTNUMBER' key or skipped if not handled.");
                    // Option: use a placeholder key or skip. For now, let's use a placeholder to still list it.
                    partNumber = $"MISSING_PN_{effectiveChildComp.Name2}_{path}"; 
                }
                
                // Unique key for grouping can be just part number if configurations don't alter BOM-relevant properties.
                // If different configurations of the same part number should be separate lines, key needs to include config.
                // For now, PDM-like often means part number is king for grouping.
                string uniqueKey = partNumber; 

                if (groupedChildren.TryGetValue(uniqueKey, out var group))
                {
                    group.quantity++;
                    groupedChildren[uniqueKey] = group;
                }
                else
                {
                    groupedChildren.Add(uniqueKey, (effectiveChildComp, childModel, 1, description, revision, path));
                    orderedKeys.Add(uniqueKey); 
                }
            }
            
            int siblingCounterForBomNumbering = 0;

            // DEBUG: Log ordered keys
            Logger.DebugLog($"[BOM_ORDER_DEBUG] ParentBomNumber: '{parentBomNumber ?? "TOP_LEVEL"}', Indent: {currentIndentLevel}, Ordered PartNumber Keys for BOM: " + 
                            string.Join("; ", orderedKeys));

            foreach (string key in orderedKeys)
            {
                var group = groupedChildren[key];
                Component2 representativeComponent = group.representative;
                ModelDoc2 representativeModel = group.model; // This is the model of the representative instance
                int quantityForThisGroup = group.quantity;

                siblingCounterForBomNumbering++;
                string currentItemBomNumber;
                if (areTheseChildrenTopLevel)
                    currentItemBomNumber = siblingCounterForBomNumbering.ToString();
                else
                    currentItemBomNumber = $"{parentBomNumber}.{siblingCounterForBomNumbering}";

                bool isAssembly = representativeModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY;

                var bomItem = new BomItem
                {
                    ItemNumber = currentItemBomNumber,
                    PartNumber = key, // Use the key which is the part number used for grouping
                    Description = group.description,
                    Revision = group.revision,
                    Quantity = quantityForThisGroup,
                    ComponentPath = group.path, // Path of the representative instance
                    IndentLevel = currentIndentLevel,
                    IsAssembly = isAssembly, // Set the flag
                    IsRaIndustrialBuy = IsRaIndustrialBuy(representativeComponent, representativeModel, key) // Check RA Industrial Buy criteria
                };
                bomItemsList.Add(bomItem);

                // If this representative component is an Assembly, recurse for its children.
                // It must be non-phantom here because phantoms were dissolved by AddEffectiveChildren.
                if (isAssembly) // Use the flag we just determined
                {
                    object[] childrenOfThisSubAssembly = representativeComponent.GetChildren() as object[];
                    if (childrenOfThisSubAssembly != null && childrenOfThisSubAssembly.Length > 0)
                    {
                        ProcessAndAddChildrenRecursive(
                            childrenOfThisSubAssembly,
                            currentItemBomNumber,       // Parent BOM number for the next level is this item's BOM number
                            currentIndentLevel + 1,
                            bomItemsList,
                            activeConfigName,           // Pass the top-level assembly's active config name consistently
                            false                       // Subsequent levels are not top-level
                        );
                    }
                }
            }
        }

        private List<BomItem> CreateConsolidatedBom(AssemblyDoc swAssembly, Configuration swConfig, bool filterForSpareParts)
        {
            var allItemsFlatList = new List<BomItem>();
            Component2 rootComponent = swConfig.GetRootComponent() as Component2;

            if (rootComponent != null)
            {
                // Initial traversal to get all individual parts with their properties
                TraverseForConsolidation(rootComponent, allItemsFlatList, swConfig.Name);
            }
            
            var consolidatedMap = new Dictionary<string, BomItem>(StringComparer.OrdinalIgnoreCase);
            // Use a list to maintain the order of first appearance for consolidation
            var orderedPartNumbers = new List<string>(); 

            foreach (var itemDetails in allItemsFlatList)
            {
                if (string.IsNullOrEmpty(itemDetails.PartNumber))
                {
                    Debug.WriteLine($"Skipping item with no PartNumber during consolidation: {itemDetails.ComponentPath}");
                    continue;
                }

                // If filtering for spare parts, and this item is not marked as a spare part, skip it.
                if (filterForSpareParts && !itemDetails.IsSparePart)
                {
                    continue; 
                }

                if (consolidatedMap.TryGetValue(itemDetails.PartNumber, out BomItem existingItem))
                {
                    // Part number already exists, sum quantities
                    existingItem.Quantity += itemDetails.Quantity; // itemDetails.Quantity is always 1 from traversal
                }
                else
                {
                    // New part number, add it to map and tracking list
                    // For consolidated BOM, ItemNumber is not used/cleared. Description/Revision from first instance.
                    BomItem newItemForConsolidated = new BomItem
                    {
                        PartNumber = itemDetails.PartNumber,
                        Description = itemDetails.Description,
                        Revision = itemDetails.Revision,
                        Quantity = itemDetails.Quantity, // Starts at 1
                        ComponentPath = itemDetails.ComponentPath, // Path of first instance encountered
                        IsSparePart = itemDetails.IsSparePart, // Carry over the flag
                        IsRaIndustrialBuy = itemDetails.IsRaIndustrialBuy // Carry over the RA Industrial Buy flag
                    };
                    consolidatedMap.Add(itemDetails.PartNumber, newItemForConsolidated);
                    orderedPartNumbers.Add(itemDetails.PartNumber); 
                }
            }
            
            // Return the consolidated items in the order they were first encountered
            return orderedPartNumbers.Select(pn => consolidatedMap[pn]).ToList();
        }

        private void TraverseForConsolidation(Component2 swComp, List<BomItem> allItems, string configName)
        {
            if (swComp == null || IsComponentSuppressedOrExcluded(swComp, configName))
            {
                return;
            }

            ModelDoc2 compModel = swComp.GetModelDoc2() as ModelDoc2;
            if (compModel == null) {
                Logger.DebugLog($"TraverseForConsolidation: Could not get ModelDoc2 for {swComp.Name2}. Might be lightweight/unresolved. Skipping component.");
                return;
            }
            
            string compNameForLog = swComp.Name2; 
            string compPathForLog = compModel.GetPathName();
            Logger.DebugLog($"TraverseForConsolidation: Processing Component '{compNameForLog}' (Path: '{compPathForLog}')");

            if (swComp.IsVirtual && string.IsNullOrEmpty(compModel.GetPathName())) {
                 Logger.DebugLog($"  [CONSOL_DEBUG] '{compNameForLog}' is virtual with no path. Properties will be checked.");
            }

            bool isPhantom = IsPhantomAssembly(swComp, configName);

            if (compModel.GetType() == (int)swDocumentTypes_e.swDocPART ||
               (compModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY && !isPhantom))
            {
                string partNumber = GetPartNumber(swComp, compModel, configName);
                Logger.DebugLog($"  [CONSOL_DEBUG] '{compNameForLog}': Raw PartNumber from GetPartNumber is '{partNumber}'");

                if (string.IsNullOrWhiteSpace(partNumber))
                {
                    Logger.DebugLog($"  [CONSOL_DEBUG] '{compNameForLog}' (Path: '{compPathForLog}'): PartNumber is NULL or WHITESPACE. Component will be SKIPPED for consolidated/spare parts BOM.");
                }
                else
                {
                    var bomItemInstance = new BomItem
                    {
                        PartNumber = partNumber,
                        Description = GetPropertyValue(compModel, configName, "Description"),
                        Revision = GetPropertyValue(compModel, configName, "Revision"),
                        Quantity = 1, // Each instance found contributes 1 to the initial list
                        ComponentPath = compModel.GetPathName(),
                        IsSparePart = "1".Equals(GetPropertyValue(compModel, configName, "Spare Part"), StringComparison.OrdinalIgnoreCase),
                        IsRaIndustrialBuy = IsRaIndustrialBuy(swComp, compModel, partNumber) // Check RA Industrial Buy criteria
                    };
                    allItems.Add(bomItemInstance);
                }
            }

            // If it's an assembly (phantom or not), recurse through its children.
            // Children of phantom assemblies are treated as direct children of the phantom's parent for traversal purposes.
            if (compModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                object childrenVariant = swComp.GetChildren();
                if (childrenVariant != null)
                {
                    object[] children = (object[])childrenVariant;
                    foreach (object childObj in children)
                    {
                        Component2 childComp = (Component2)childObj;
                        TraverseForConsolidation(childComp, allItems, configName);
                    }
                }
            }
        }
        
        private bool IsComponentSuppressedOrExcluded(Component2 swComp, string configName)
        {
            if (swComp == null) return true;

            // Check if component is suppressed in the context of the active assembly configuration
            if (swComp.IsSuppressed()) 
            {
                 Debug.WriteLine($"Component {swComp.Name2} is suppressed in current assembly context.");
                return true;
            }
            
            // Check the component's "Exclude from bill of materials" checkbox property (instance specific)
            if (swComp.ExcludeFromBOM)
            {
                Debug.WriteLine($"Component {swComp.Name2} instance is set to 'ExcludeFromBOM'.");
                return true;
            }
            
            // For sub-assemblies, check their own configuration's BOM display setting
            ModelDoc2 compModel = swComp.GetModelDoc2() as ModelDoc2;
            if (compModel != null && compModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY) {
                string compRefConfigName = swComp.ReferencedConfiguration;
                // If ReferencedConfiguration is empty, component uses its own active config or one matching parent assembly's config name.
                if (string.IsNullOrEmpty(compRefConfigName)) {
                     compRefConfigName = compModel.ConfigurationManager.ActiveConfiguration.Name;
                     Debug.WriteLine($"IsComponentSuppressedOrExcluded: {swComp.Name2} has no ref config, using its active: {compRefConfigName}");
                }

                Configuration compConfig = compModel.GetConfigurationByName(compRefConfigName) as Configuration;
                if (compConfig != null) {
                    // If a sub-assembly's configuration is set to "Hide" in BOM, it (and its children, effectively) are excluded.
                    if (compConfig.ChildComponentDisplayInBOM == (int)swChildComponentInBOMOption_e.swChildComponent_Hide) {
                        Debug.WriteLine($"Sub-assembly {swComp.Name2} (config: {compRefConfigName}) is set to 'Hide' in BOM (ChildComponentDisplayInBOM). Excluding.");
                        return true; 
                    }
                } else {
                     Debug.WriteLine($"IsComponentSuppressedOrExcluded: Could not find config '{compRefConfigName}' for {swComp.Name2} to check ChildComponentDisplayInBOM.");
                }
            }
            return false;
        }

        private bool IsPhantomAssembly(Component2 swComp, string owningAssemblyConfigName)
        {
            if (swComp == null) return false;
            
            ModelDoc2 compModel = swComp.GetModelDoc2() as ModelDoc2;
            if (compModel != null && compModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                // The "Phantom" or "Promote" behavior is a property of the sub-assembly's *own* configuration that is active in the parent assembly.
                string compRefConfigName = swComp.ReferencedConfiguration; 
                if (string.IsNullOrEmpty(compRefConfigName)) {
                    // If not explicitly specified, it might be using its own active configuration, or the one SolidWorks resolved.
                    // It's safer to get the configuration that SolidWorks considers active for this component instance.
                    compRefConfigName = compModel.ConfigurationManager.ActiveConfiguration.Name; // Fallback, might not be the one used in parent assembly.
                                                                                               // Better: Get the specific config instance if possible.
                    Debug.WriteLine($"IsPhantomAssembly: {swComp.Name2} has no specific ReferencedConfiguration. Checking its own active config: {compRefConfigName} for phantom status.");
                }

                Configuration compConfig = compModel.GetConfigurationByName(compRefConfigName) as Configuration;
                if (compConfig != null)
                {
                    if (compConfig.ChildComponentDisplayInBOM == (int)swChildComponentInBOMOption_e.swChildComponent_Promote)
                    {
                        Debug.WriteLine($"Component {swComp.Name2} (using its config: {compConfig.Name}) is a Phantom/Promote assembly.");
                        return true;
                    }
                } else {
                    Debug.WriteLine($"IsPhantomAssembly: Could not find configuration '{compRefConfigName}' for component {swComp.Name2} to check ChildComponentDisplayInBOM. Assuming not phantom.");
                }
            }
            return false;
        }

        private string GetPartNumber(Component2 swComp, ModelDoc2 compModel, string activeAssemblyConfigName)
        {
            if (compModel == null) {
                Logger.DebugLog($"GetPartNumber: compModel is null for swComp: {swComp.Name2}. Returning instance name as fallback.");
                return swComp.Name2; 
            }

            string originalCompNameForLog = swComp.Name2; 
            string compPathForLog = compModel.GetPathName();
            Logger.DebugLog($"GetPartNumber START for Component: '{originalCompNameForLog}' (Path: '{compPathForLog}')");

            // --- Determine Configuration to Use --- 
            string configNameToUse = swComp.ReferencedConfiguration;
            if (string.IsNullOrEmpty(configNameToUse))
            {
                Configuration potentialMatchingConfig = compModel.GetConfigurationByName(activeAssemblyConfigName) as Configuration;
                if (potentialMatchingConfig != null) {
                    configNameToUse = activeAssemblyConfigName;
                    Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': No ref config, using matching parent assembly config name: {configNameToUse}");
                } else {
                    configNameToUse = compModel.ConfigurationManager.ActiveConfiguration.Name;
                    Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': No ref config and no matching parent. Using its own active: {configNameToUse}");
                }
            }
            
            Configuration compConfig = compModel.GetConfigurationByName(configNameToUse) as Configuration;
            if (compConfig == null) { 
                Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': Failed to get config '{configNameToUse}'. Trying component's active config.");
                compConfig = compModel.ConfigurationManager.ActiveConfiguration; 
                 if (compConfig == null) {
                     Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': CRITICAL - Failed to get any config from '{compPathForLog}'. Cannot determine part number. Fallback to filename.");
                     return Path.GetFileNameWithoutExtension(compModel.GetPathName()); 
                 }
                 configNameToUse = compConfig.Name; 
                 Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': Now using its own active config: {configNameToUse}");
            }
            Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': Determined config context for PN: '{configNameToUse}'");

            // --- Get Part Number based on Priority --- 
            string bomPartNo = "";

            // Priority 1: Custom Properties ("PartNo" or "PartNumber")
            bomPartNo = GetPropertyValue(compModel, configNameToUse, "PartNo");
            if (!string.IsNullOrWhiteSpace(bomPartNo)) {
                 Logger.DebugLog($"  [PN_PRIORITY] '{originalCompNameForLog}': Using custom property 'PartNo'. PN='{bomPartNo}'");
            } else {
                bomPartNo = GetPropertyValue(compModel, configNameToUse, "Part Number");
                if (!string.IsNullOrWhiteSpace(bomPartNo)) {
                     Logger.DebugLog($"  [PN_PRIORITY] '{originalCompNameForLog}': Using custom property 'Part Number'. PN='{bomPartNo}'");
                }
            }

            // Priority 2: Configuration BOM Setting (if no custom prop found)
            if (string.IsNullOrWhiteSpace(bomPartNo)) 
            {
                Logger.DebugLog($"  [PN_PRIORITY] '{originalCompNameForLog}': No suitable custom property found. Checking config BOM settings for '{configNameToUse}'.");
                int partNoOption = compConfig.BOMPartNoSource; 
                switch(partNoOption)
                {
                    case 0: // swConfigurationBomOption_DocumentName
                        bomPartNo = Path.GetFileNameWithoutExtension(compModel.GetPathName());
                        Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': Config '{configNameToUse}' BOMSource=DocumentName. PN='{bomPartNo}'");
                        break;
                    case 1: // swConfigurationBomOption_ConfigurationName
                        // CRITICAL FIX: Only use config name if it's NOT "Default"
                        if (!"Default".Equals(configNameToUse, StringComparison.OrdinalIgnoreCase)) 
                        {
                           bomPartNo = configNameToUse; 
                           Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': Config '{configNameToUse}' BOMSource=ConfigurationName. PN='{bomPartNo}'");
                        } else {
                           Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': Config '{configNameToUse}' BOMSource=ConfigurationName, BUT name is 'Default'. Ignoring and proceeding to fallback.");
                           // bomPartNo remains empty/null here
                        }
                        break;
                    case 2: // swConfigurationBomOptions_UserSpecifiedName
                        bomPartNo = compConfig.AlternateName; 
                        Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': Config '{configNameToUse}' BOMSource=UserSpecifiedName. PN='{bomPartNo}'");
                        break;
                    default: 
                         Logger.DebugLog($"  [PN_DEBUG] '{originalCompNameForLog}': Config '{configNameToUse}' Unknown BOMPartNoSource option {partNoOption}. Proceeding to fallback.");
                         break;
                }
            }

            // Priority 3: Filename Fallback (if still no valid PN)
            if (string.IsNullOrWhiteSpace(bomPartNo)) {
                bomPartNo = Path.GetFileNameWithoutExtension(compModel.GetPathName());
                 Logger.DebugLog($"  [PN_PRIORITY] '{originalCompNameForLog}': PartNumber is still blank after checking custom props and config settings. Final fallback to filename. PN='{bomPartNo}'");
            }

            Logger.DebugLog($"GetPartNumber END for Component: '{originalCompNameForLog}'. Final PN='{bomPartNo}'");
            return bomPartNo;
        }

        private bool IsRaIndustrialBuy(Component2 swComp, ModelDoc2 compModel, string partNumber)
        {
            if (compModel == null || string.IsNullOrEmpty(partNumber)) return false;

            // Check condition 1: Part number format XXX-XXX (exactly 3 digits, dash, 3 digits)
            bool isCorrectFormat = System.Text.RegularExpressions.Regex.IsMatch(partNumber, @"^\d{3}-\d{3}$");
            
            // Check condition 2: File property "RA Industrial Buy" = "1" or "True"
            bool hasRaIndustrialBuyProperty = false;
            try
            {
                // Check file-level properties only (as requested)
                CustomPropertyManager swCustPropMgr = compModel.Extension.CustomPropertyManager[""];
                if (swCustPropMgr != null)
                {
                    swCustPropMgr.Get6("RA Industrial Buy", false, out _, out string propertyValue, out _, out _);
                    hasRaIndustrialBuyProperty = "1".Equals(propertyValue, StringComparison.OrdinalIgnoreCase) || 
                                               "True".Equals(propertyValue, StringComparison.OrdinalIgnoreCase);
                    
                    Logger.DebugLog($"IsRaIndustrialBuy: Component '{swComp.Name2}' - PartNumber='{partNumber}', FormatMatch={isCorrectFormat}, PropertyValue='{propertyValue}', PropertyMatch={hasRaIndustrialBuyProperty}");
                }
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"IsRaIndustrialBuy: Error checking RA Industrial Buy property for '{swComp.Name2}': {ex.Message}");
            }

            bool result = isCorrectFormat || hasRaIndustrialBuyProperty;
            Logger.DebugLog($"IsRaIndustrialBuy: Component '{swComp.Name2}' - Final Result = {result}");
            return result;
        }

        private string GetPropertyValue(ModelDoc2 swModelDoc, string configName, string propertyName)
        {
            if (swModelDoc == null || string.IsNullOrEmpty(propertyName)) return "";

            string resolvedValOut = "";
            CustomPropertyManager swCustPropMgr = null;

            // Priority 1: Try file-level properties first
            swCustPropMgr = swModelDoc.Extension.CustomPropertyManager[""]; 
            if (swCustPropMgr != null)
            {
                swCustPropMgr.Get6(propertyName, false, out _, out resolvedValOut, out _, out _);
                if (!string.IsNullOrWhiteSpace(resolvedValOut))
                {
                    Logger.DebugLog($"GetPropertyValue: Found '{propertyName}' in FILE-LEVEL properties of '{swModelDoc.GetPathName()}' -> Result='{resolvedValOut}'");
                    return resolvedValOut;
                }
                else
                {
                    Logger.DebugLog($"GetPropertyValue: Property '{propertyName}' not found or empty in FILE-LEVEL properties of '{swModelDoc.GetPathName()}'");
                }
            }

            // Priority 2: Fall back to configuration-specific properties if file-level is empty/missing
            if (!string.IsNullOrEmpty(configName))
            {
                Configuration testConfig = swModelDoc.GetConfigurationByName(configName) as Configuration;
                if (testConfig != null) 
                {
                    swCustPropMgr = swModelDoc.Extension.CustomPropertyManager[configName];
                    if (swCustPropMgr != null)
                    {
                        swCustPropMgr.Get6(propertyName, false, out _, out resolvedValOut, out _, out _);
                        if (!string.IsNullOrWhiteSpace(resolvedValOut))
                        {
                            Logger.DebugLog($"GetPropertyValue: Found '{propertyName}' in CONFIG-SPECIFIC properties ('{configName}') of '{swModelDoc.GetPathName()}' -> Result='{resolvedValOut}'");
                            return resolvedValOut;
                        }
                        else
                        {
                            Logger.DebugLog($"GetPropertyValue: Property '{propertyName}' not found or empty in CONFIG-SPECIFIC properties ('{configName}') of '{swModelDoc.GetPathName()}'");
            }
                    }
                    else
                    {
                        Logger.DebugLog($"GetPropertyValue: Could not get CustomPropertyManager for config '{configName}' in '{swModelDoc.GetPathName()}'");
                    }
                } 
                else 
                {
                    Logger.DebugLog($"GetPropertyValue: Config '{configName}' not found in '{swModelDoc.GetPathName()}'. File-level properties already checked.");
                }
            }
            else
            {
                Logger.DebugLog($"GetPropertyValue: No specific config name provided. File-level properties already checked for '{propertyName}' in '{swModelDoc.GetPathName()}'");
            }
            
            Logger.DebugLog($"GetPropertyValue: Property '{propertyName}' not found in either file-level or config-specific properties for '{swModelDoc.GetPathName()}'. Returning empty string.");
            return ""; 
        }

        private string GetAssemblyThumbnail(ModelDoc2 swModelDoc, string tempFolder)
        {
            if (swModelDoc == null) return null;

            try
            {
                // Create a temporary file for the thumbnail
                string thumbnailPath = Path.Combine(tempFolder, "assembly_thumbnail.png");
                
                // Get the thumbnail using SaveAs3 with PNG format
                int status = swModelDoc.SaveAs3(thumbnailPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, 
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy);

                if (status == 0) // Success
                {
                    return thumbnailPath;
                }
                else
                {
                    Debug.WriteLine($"Failed to save thumbnail. Status: {status}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting assembly thumbnail: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Exports BOM data to an existing Excel template file, supporting PDM vault operations
        /// </summary>
        /// <param name="bomItems">The BOM data to export</param>
        /// <param name="templateFilePath">Path to the template file in PDM vault or local filesystem</param>
        /// <param name="outputFilePath">Path where the populated file should be saved</param>
        /// <param name="bomType">Type of BOM being exported</param>
        /// <param name="assemblyName">Name of the assembly</param>
        /// <param name="worksheetName">Name of the worksheet to populate (optional, uses first sheet if not specified)</param>
        /// <param name="startRow">Row number to start inserting BOM data (1-based)</param>
        /// <param name="startColumn">Column number to start inserting BOM data (1-based)</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ExportBomToExistingExcelTemplate(
            List<BomItem> bomItems, 
            string templateFilePath, 
            string outputFilePath, 
            BomType bomType, 
            string assemblyName,
            string worksheetName = null,
            int startRow = 1,
            int startColumn = 1)
        {
            if (bomItems == null || !bomItems.Any())
            {
                MessageBox.Show("No BOM data available to export.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Debug.WriteLine("ExportBomToExistingExcelTemplate: No BOM items to export.");
                return false;
            }

            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;
            string localTemplateFile = null;
            bool outputCheckedOut = false;
            EPDM.Interop.epdm.IEdmFile5 templateEdmFile = null;
            EPDM.Interop.epdm.IEdmFolder5 templateEdmFolder = null;
            EPDM.Interop.epdm.IEdmFile5 outputEdmFile = null;
            EPDM.Interop.epdm.IEdmFolder5 outputEdmFolder = null;

            try
            {
                // Step 1: Handle PDM vault operations for template file
                if (_pdmVault != null && _pdmVault.IsLoggedIn)
                {
                    Debug.WriteLine($"PDM: Attempting to retrieve template from vault: {templateFilePath}");
                    
                    // Get template file from vault
                    templateEdmFile = _pdmVault.GetFileFromPath(templateFilePath, out templateEdmFolder);
                    if (templateEdmFile != null)
                    {
                        Debug.WriteLine($"PDM: Template file found in vault: {templateFilePath}");
                        
                        // Get local copy of template (this will download latest version if needed)
                        int swWindowHandle = GetSolidWorksMainWindowHandle();
                        localTemplateFile = templateEdmFile.GetLocalPath(templateEdmFolder.ID);
                        
                        if (string.IsNullOrEmpty(localTemplateFile) || !File.Exists(localTemplateFile))
                        {
                            Debug.WriteLine($"PDM: Getting local copy of template file");
                            templateEdmFile.GetFileCopy(swWindowHandle, 0); // 0 = latest version
                            localTemplateFile = templateEdmFile.GetLocalPath(templateEdmFolder.ID);
                        }

                        if (string.IsNullOrEmpty(localTemplateFile) || !File.Exists(localTemplateFile))
                        {
                            throw new Exception($"Could not get local copy of template file from PDM vault: {templateFilePath}");
                        }

                        Debug.WriteLine($"PDM: Using local template file: {localTemplateFile}");
                    }
                    else
                    {
                        Debug.WriteLine($"PDM: Template file not found in vault, using local path: {templateFilePath}");
                        localTemplateFile = templateFilePath;
                    }

                    // Handle output file checkout if it already exists in vault
                    outputEdmFile = _pdmVault.GetFileFromPath(outputFilePath, out outputEdmFolder);
                    if (outputEdmFile != null)
                    {
                        Debug.WriteLine($"PDM: Output file exists in vault, checking out: {outputFilePath}");
                        
                        if (!outputEdmFile.IsLocked)
                        {
                            outputEdmFile.LockFile(outputEdmFolder.ID, GetSolidWorksMainWindowHandle(), (int)EPDM.Interop.epdm.EdmLockFlag.EdmLock_Simple);
                            outputCheckedOut = true;
                            Debug.WriteLine($"PDM: Successfully checked out output file: {outputFilePath}");
                        }
                        else
                        {
                            Debug.WriteLine($"PDM: Output file is already locked by another user: {outputFilePath}");
                            // You might want to handle this differently based on your workflow
                            MessageBox.Show($"Output file '{Path.GetFileName(outputFilePath)}' is checked out by another user.", 
                                          "PDM Checkout Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return false;
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("PDM: No vault connection, using local template file");
                    localTemplateFile = templateFilePath;
                }

                // Step 2: Open Excel application and workbook
                excelApp = new Excel.Application();
                excelApp.Visible = false;
                
                if (!File.Exists(localTemplateFile))
                {
                    throw new FileNotFoundException($"Template file not found: {localTemplateFile}");
                }

                Debug.WriteLine($"Opening Excel template: {localTemplateFile}");
                workbook = excelApp.Workbooks.Open(localTemplateFile);

                // Step 3: Get the worksheet to work with
                if (!string.IsNullOrEmpty(worksheetName))
                {
                    try
                    {
                        worksheet = workbook.Sheets[worksheetName] as Excel.Worksheet;
                    }
                    catch
                    {
                        Debug.WriteLine($"Worksheet '{worksheetName}' not found, using first worksheet");
                        worksheet = workbook.Sheets[1] as Excel.Worksheet;
                    }
                }
                else
                {
                    worksheet = workbook.Sheets[1] as Excel.Worksheet;
                }

                Debug.WriteLine($"Using worksheet: {worksheet.Name}");

                // Step 4: Clear existing data if needed (optional - you might want to make this configurable)
                // Excel.Range usedRange = worksheet.UsedRange;
                // if (usedRange != null && startRow <= usedRange.Rows.Count)
                // {
                //     Excel.Range clearRange = worksheet.Range[worksheet.Cells[startRow, startColumn], usedRange];
                //     clearRange.Clear();
                // }

                // Step 5: Insert BOM data
                int currentRow = startRow;
                int currentCol = startColumn;

                // Insert headers based on BOM type
                if (bomType == BomType.Full)
                {
                    worksheet.Cells[currentRow, currentCol] = "Item Number";
                    worksheet.Cells[currentRow, currentCol + 1] = "Part Number";
                    worksheet.Cells[currentRow, currentCol + 2] = "Description";
                    worksheet.Cells[currentRow, currentCol + 3] = "Revision";
                    worksheet.Cells[currentRow, currentCol + 4] = "Quantity";
                }
                else // Consolidated or Spare Parts
                {
                    worksheet.Cells[currentRow, currentCol] = "Part Number";
                    worksheet.Cells[currentRow, currentCol + 1] = "Description";
                    worksheet.Cells[currentRow, currentCol + 2] = "Revision";
                    worksheet.Cells[currentRow, currentCol + 3] = "Quantity";
                }
                currentRow++;

                // Insert BOM data rows
                foreach (var item in bomItems)
                {
                    currentCol = startColumn;
                    
                    if (bomType == BomType.Full)
                    {
                        worksheet.Cells[currentRow, currentCol++] = item.ItemNumber;
                    }
                    
                    worksheet.Cells[currentRow, currentCol++] = item.PartNumber;
                    worksheet.Cells[currentRow, currentCol++] = item.Description;
                    worksheet.Cells[currentRow, currentCol++] = item.Revision;
                    worksheet.Cells[currentRow, currentCol++] = item.Quantity;
                    
                    currentRow++;
                }

                // Step 6: Save the populated workbook
                Debug.WriteLine($"Saving populated workbook to: {outputFilePath}");
                
                // Ensure output directory exists
                string outputDirectory = Path.GetDirectoryName(outputFilePath);
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                workbook.SaveAs(outputFilePath, Excel.XlFileFormat.xlOpenXMLWorkbook, Type.Missing, Type.Missing,
                               false, false, Excel.XlSaveAsAccessMode.xlNoChange,
                               Excel.XlSaveConflictResolution.xlUserResolution, true,
                               Type.Missing, Type.Missing, Type.Missing);

                // Step 7: Handle PDM check-in operations
                if (_pdmVault != null && _pdmVault.IsLoggedIn)
                {
                    // Add new file to vault if it doesn't exist
                    if (outputEdmFile == null && outputEdmFolder != null)
                    {
                        Debug.WriteLine($"PDM: Adding new file to vault: {outputFilePath}");
                        outputEdmFolder.AddFile(GetSolidWorksMainWindowHandle(), outputFilePath, 
                                              $"BOM export for {assemblyName} - {bomType} BOM");
                        
                        // Get the newly added file reference
                        outputEdmFile = _pdmVault.GetFileFromPath(outputFilePath, out outputEdmFolder);
                    }
                    
                    // Check in the file if we checked it out
                    if (outputCheckedOut && outputEdmFile != null)
                    {
                        Debug.WriteLine($"PDM: Checking in output file: {outputFilePath}");
                        outputEdmFile.UnlockFile(GetSolidWorksMainWindowHandle(), 
                                               $"Updated BOM export for {assemblyName} - {bomType} BOM");
                    }
                }

                Debug.WriteLine($"BOM exported successfully to existing template: {outputFilePath}");
                MessageBox.Show($"BOM exported successfully to:\n{outputFilePath}\n\nUsing template: {Path.GetFileName(localTemplateFile)}", 
                               "Roeslein Add-in - Export Complete", 
                               MessageBoxButtons.OK, 
                               MessageBoxIcon.Information);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting BOM to existing Excel template: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"An error occurred while exporting the BOM to Excel template: {ex.Message}", 
                               "Roeslein Add-in - Excel Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Handle cleanup on error - undo checkouts if needed
                try
                {
                    if (outputCheckedOut && outputEdmFile != null)
                    {
                        Debug.WriteLine("PDM: Undoing checkout due to error");
                        outputEdmFile.UndoLockFile(GetSolidWorksMainWindowHandle(), true);
                    }
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"Error during PDM cleanup: {cleanupEx.Message}");
                }

                return false;
            }
            finally
            {
                // Clean up COM objects
                try 
                {
                    if (workbook != null)
                    {
                        workbook.Close(false);
                    }
                    if (excelApp != null)
                    {
                        excelApp.Quit();
                    }
                } 
                catch (Exception exFin) 
                {
                    Debug.WriteLine($"Exception during Excel cleanup: {exFin.Message}");
                }

                ReleaseComObject(worksheet);
                ReleaseComObject(workbook);
                ReleaseComObject(excelApp);
                
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// Get the SolidWorks main window handle for PDM operations
        /// </summary>
        private int GetSolidWorksMainWindowHandle()
        {
            try
            {
                if (_swApp != null)
                {
                    Frame swFrame = _swApp.Frame() as Frame;
                    if (swFrame != null)
                    {
                        return swFrame.GetHWnd();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting SW window handle: {ex.Message}");
            }
            Debug.WriteLine("Could not get SolidWorks main window handle, returning 0.");
            return 0;
        }

        public void ExportBomToExcel(List<BomItem> bomItems, string filePath, BomType bomType, string assemblyName)
        {
            if (bomItems == null || !bomItems.Any())
            {
                MessageBox.Show("No BOM data available to export.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Debug.WriteLine("ExportBomToExcel: No BOM items to export.");
                return;
            }

            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;
            string tempFolder = Path.Combine(Path.GetTempPath(), "RoesleinBOM");
            
            try
            {
                // Create temp folder if it doesn't exist
                if (!Directory.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                }

                excelApp = new Excel.Application();
                excelApp.Visible = false;
                workbook = excelApp.Workbooks.Add();
                worksheet = workbook.Sheets[1] as Excel.Worksheet;
                worksheet.Name = SanitizeSheetName($"{assemblyName}_{bomType}_BOM");

                // --- INSERT NEW ANCHOR COLUMN ---
                string anchorColumnLetter = "D"; // Default for Full BOM
                string descriptionColumnLetter = "C"; // Default for Full BOM
                if (bomType != BomType.Full)
                {
                    anchorColumnLetter = "C"; // For Consolidated/Spare, new anchor is C
                    descriptionColumnLetter = "B"; // Description is in B
                }
                Excel.Range colToInsertBefore = (Excel.Range)worksheet.Columns[anchorColumnLetter + ":" + anchorColumnLetter];
                colToInsertBefore.Insert(Excel.XlInsertShiftDirection.xlShiftToRight);
                // -------------------------------------------

                // Get the active assembly document
                ModelDoc2 swModel = _swApp.ActiveDoc as ModelDoc2;
                if (swModel == null)
                {
                    throw new Exception("No active document found.");
                }

                // Get assembly details
                string partNumber = GetPropertyValue(swModel, swModel.ConfigurationManager.ActiveConfiguration.Name, "Part Number");
                string description = GetPropertyValue(swModel, swModel.ConfigurationManager.ActiveConfiguration.Name, "Description");
                string state = GetPropertyValue(swModel, swModel.ConfigurationManager.ActiveConfiguration.Name, "State");
                string revision = GetPropertyValue(swModel, swModel.ConfigurationManager.ActiveConfiguration.Name, "Revision");

                // Determine the last column for BOM data (accounting for new anchor column)
                int lastBomDataColumn;
                if (bomType == BomType.Full) // A=Item, B=PN, C=Desc, D=Anchor, E=Rev, F=Qty
                {
                    lastBomDataColumn = 6; 
                }
                else // Consolidated/Spare: A=PN, B=Desc, C=Anchor, D=Rev, E=Qty
                {
                    lastBomDataColumn = 5; 
                }

                // Get thumbnail
                string thumbnailPath = GetAssemblyThumbnail(swModel, tempFolder); // Re-enabled

                int currentRow = 1;

                // Title Row
                worksheet.Cells[currentRow, 1] = $"{assemblyName} - {bomType} BOM";
                Excel.Range titleRange = worksheet.Range[worksheet.Cells[currentRow, 1], worksheet.Cells[currentRow, lastBomDataColumn]];
                titleRange.Merge();
                titleRange.Font.Bold = true;
                titleRange.Font.Size = 14;
                titleRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                currentRow++;

                // Assembly Details Section
                worksheet.Cells[currentRow, 1] = "Part Number:";
                worksheet.Cells[currentRow, 2] = partNumber;
                currentRow++;

                worksheet.Cells[currentRow, 1] = "Description:";
                worksheet.Cells[currentRow, 2] = description;
                // Merge B3 and C3 for Description value
                Excel.Range descMergeRange = worksheet.Range[worksheet.Cells[currentRow, 2], worksheet.Cells[currentRow, 3]];
                descMergeRange.Merge();
                descMergeRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft;
                currentRow++;

                worksheet.Cells[currentRow, 1] = "State:";
                worksheet.Cells[currentRow, 2] = state;
                currentRow++;

                worksheet.Cells[currentRow, 1] = "Revision:";
                worksheet.Cells[currentRow, 2] = revision;
                currentRow++;

                // --- Re-enabled Thumbnail Logic with Basic Coordinates ---
                // Add thumbnail if available
                if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
                {
                    float thumbnailWidth = 150f; // Desired width of the thumbnail
                    float thumbnailHeight = 100f; // Desired height of the thumbnail

                    // --- Anchor Thumbnail's Top-Left to Anchor Column, Row 2 ---
                    string anchorCellAddress = anchorColumnLetter + "2"; 
                    float imageLeft;
                    float imageTop;
                    try
                    {
                        Excel.Range anchorCell = worksheet.Range[anchorCellAddress];
                        imageLeft = (float)anchorCell.Left; // Align image Left to D2.Left
                        imageTop = (float)anchorCell.Top;   // Align image Top to D2.Top
                        
                        Logger.DebugLog($"[THUMBNAIL_POS_DEBUG] Anchoring Top-Left of image to Top-Left of cell {anchorCellAddress}. " + 
                                        $"Calculated imageLeft={imageLeft}, imageTop={imageTop}");
                    }
                    catch (Exception ex)
                    {
                        Logger.DebugLog($"[THUMBNAIL_POS_DEBUG] Error getting anchor cell {anchorCellAddress}. Defaulting to 10,10. Error: {ex.Message}");
                        imageLeft = 10f; // Fallback Left position
                        imageTop = 10f;  // Fallback Top position
                    }
                    
                    Excel.Shape thumbnailShape = worksheet.Shapes.AddPicture(
                        thumbnailPath,
                        Microsoft.Office.Core.MsoTriState.msoFalse,
                        Microsoft.Office.Core.MsoTriState.msoTrue,
                        imageLeft,
                        imageTop, 
                        thumbnailWidth, 
                        thumbnailHeight 
                    );

                    // DEBUG: Log the actual position assigned by Excel
                    try {
                        float actualLeft = thumbnailShape.Left;
                        float actualTop = thumbnailShape.Top;
                        Logger.DebugLog($"[THUMBNAIL_POS_DEBUG] Set Left={imageLeft}, Top={imageTop}. Excel reported Left={actualLeft}, Top={actualTop}");
                    } catch (Exception ex) {
                        Logger.DebugLog($"[THUMBNAIL_POS_DEBUG] Error reading back thumbnail position: {ex.Message}");
                    }

                }
                // --------------------------------------------------------

                currentRow++; // Add spacing before date

                // Date Row
                worksheet.Cells[currentRow, 1] = $"Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                Excel.Range dateRange = worksheet.Range[worksheet.Cells[currentRow, 1], worksheet.Cells[currentRow, lastBomDataColumn]];
                dateRange.Merge();
                currentRow++;
                currentRow++; // Add spacing before BOM data

                // Column Headers
                int headerRow = currentRow; 
                int headerCol = 1;
                string currentDescHeaderColLetter = "";

                if (bomType == BomType.Full)
                {
                    worksheet.Cells[currentRow, headerCol++] = "Item Number"; // Col A
                    worksheet.Cells[currentRow, headerCol++] = "Part Number";   // Col B
                    currentDescHeaderColLetter = "C";
                    worksheet.Cells[currentRow, headerCol++] = "Description";   // Col C 
                }
                else // Consolidated or Spare Parts
                {
                    worksheet.Cells[currentRow, headerCol++] = "Part Number";   // Col A
                    currentDescHeaderColLetter = "B";
                    worksheet.Cells[currentRow, headerCol++] = "Description";   // Col B
                }
                
                // Merge Description Header with new anchor column header
                Excel.Range descHeaderCell = worksheet.Range[currentDescHeaderColLetter + headerRow.ToString()];
                Excel.Range anchorHeaderCell = (Excel.Range)descHeaderCell.Offset[0, 1]; // The column to its right (new anchor column)
                Excel.Range headerRangeToMerge = worksheet.Range[descHeaderCell, anchorHeaderCell];
                headerRangeToMerge.Merge();
                headerRangeToMerge.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter; 
                headerCol++; // Account for the anchor column that was just merged into description

                worksheet.Cells[currentRow, headerCol++] = "Revision";      
                worksheet.Cells[currentRow, headerCol++] = "Quantity";      
                
                Excel.Range headerRowRange = worksheet.Range[worksheet.Cells[currentRow, 1], worksheet.Cells[currentRow, headerCol - 1]];
                headerRowRange.Font.Bold = true;
                headerRowRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                headerRowRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                currentRow++;

                // Data Rows
                foreach(var item in bomItems)
                {
                    int dataCol = 1;
                    Excel.Range itemNumberCell = null;
                    string currentDescDataColLetter = "";

                    if (bomType == BomType.Full)
                    {
                        itemNumberCell = worksheet.Cells[currentRow, dataCol] as Excel.Range; // Col A
                        itemNumberCell.Value2 = item.ItemNumber;
                        dataCol++;
                        
                        Excel.Range pnCell = worksheet.Cells[currentRow, dataCol] as Excel.Range; // Col B
                        pnCell.Value2 = item.PartNumber; 
                        pnCell.IndentLevel = item.IndentLevel;
                        dataCol++;
                        currentDescDataColLetter = "C";
                    }
                    else { // For Consolidated/Spare Parts
                        Excel.Range pnCell = worksheet.Cells[currentRow, dataCol] as Excel.Range; // Col A
                        pnCell.Value2 = item.PartNumber;
                        dataCol++;
                        currentDescDataColLetter = "B";
                    }
                    
                    Excel.Range descCell = worksheet.Range[currentDescDataColLetter + currentRow.ToString()];
                    descCell.Value2 = item.Description;
                    
                    // Merge Description cell with new anchor cell 
                    Excel.Range anchorDataCell = (Excel.Range)descCell.Offset[0, 1]; // New Anchor Col
                    Excel.Range rangeToMerge = worksheet.Range[descCell, anchorDataCell];
                    rangeToMerge.Merge();
                    rangeToMerge.HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft; 
                    dataCol++; // Advance past original description column letter
                    dataCol++; // Advance past new anchor column (which is now merged into description)
                    
                    worksheet.Cells[currentRow, dataCol++] = item.Revision;    
                    worksheet.Cells[currentRow, dataCol++] = item.Quantity;    

                    // Apply bold formatting to Item Number if it's a Full BOM and represents an Assembly
                    if (itemNumberCell != null && item.IsAssembly)
                    {
                        itemNumberCell.Font.Bold = true;
                    }

                    currentRow++;
                }

                // Format the worksheet
                // worksheet.Columns.AutoFit(); // Remove or comment out general AutoFit for more control

                // --- Set specific column widths ---
                Excel.Range descColForWidth = (Excel.Range)worksheet.Columns[descriptionColumnLetter + ":" + descriptionColumnLetter];
                descColForWidth.ColumnWidth = 82;
                Excel.Range anchorColForWidth = (Excel.Range)worksheet.Columns[anchorColumnLetter + ":" + anchorColumnLetter];
                anchorColForWidth.ColumnWidth = 11; 
                // ----------------------------------

                // Align Part Number column (original B for Full, A for others) to the left
                string partNumberColumn = (bomType == BomType.Full) ? "B:B" : "A:A";
                worksheet.Columns[partNumberColumn].HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft;
                worksheet.Columns[partNumberColumn].ColumnWidth = 20;

                // Format Item Number Column (Column A for Full BOM) as TEXT and Right-aligned
                if (bomType == BomType.Full)
                {
                    Excel.Range columnA = worksheet.Range["A:A"];
                    columnA.NumberFormat = "@"; // Format as TEXT
                    columnA.HorizontalAlignment = Excel.XlHAlign.xlHAlignRight; // Right-aligned
                    columnA.ColumnWidth = 12.5; // Set Column A width
                }
                else // For Consolidated/SpareParts, Column A is Part Number
                {
                    Excel.Range columnA = worksheet.Range["A:A"];
                    columnA.ColumnWidth = 12.5; // Set Column A width (used for Part Number here)
                    // For Consolidated/Spare, Part Number is in A, Description in B.
                    // The general Part Number column (A for these types) is already handled by the partNumberColumn logic below for alignment.
                    // Cell B2 specifically contains the main assembly's Part Number value in the header for these types.
                    Excel.Range headerPartNumberCell = worksheet.Cells[2, "B"] as Excel.Range; // Cell B2
                    headerPartNumberCell.HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft;
                }

                // --- Page Setup for Printing ---
                // Define Print Area: From cell A1 to the last cell of the BOM data
                // currentRow currently points to the row *after* the last data row
                string lastCellAddress = $"{GetExcelColumnName(lastBomDataColumn)}{currentRow - 1}";
                worksheet.PageSetup.PrintArea = $"A1:{lastCellAddress}";

                // Fit all columns to one page wide
                worksheet.PageSetup.Zoom = false; // Must be false to use FitToPagesWide/Tall
                worksheet.PageSetup.FitToPagesWide = 1;
                worksheet.PageSetup.FitToPagesTall = false; // Allow multiple pages tall if necessary
                // Optional: Set orientation if desired (e.g., Landscape)
                // worksheet.PageSetup.Orientation = Excel.XlPageOrientation.xlLandscape;
                // --------------------------------

                worksheet.Activate();

                // Save the workbook
                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook, Type.Missing, Type.Missing,
                                false, false, Excel.XlSaveAsAccessMode.xlNoChange,
                                Excel.XlSaveConflictResolution.xlUserResolution, true,
                                Type.Missing, Type.Missing, Type.Missing);
                
                Debug.WriteLine($"BOM exported successfully to: {filePath}");
                MessageBox.Show($"BOM exported successfully to:\n{filePath}\n\nYou can open the file from this location.", 
                                "Roeslein Add-in - Export Complete", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting BOM to Excel: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"An error occurred while exporting the BOM to Excel: {ex.Message}", "Roeslein Add-in - Excel Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Clean up temp files
                try
                {
                    if (Directory.Exists(tempFolder))
                    {
                        Directory.Delete(tempFolder, true);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error cleaning up temp folder: {ex.Message}");
                }

                // Release COM objects
                try 
                {
                    if (workbook != null)
                    {
                        workbook.Close(false);
                    }
                    if (excelApp != null)
                    {
                        excelApp.Quit();
                    }
                } 
                catch (Exception exFin) 
                {
                    Debug.WriteLine($"Exception during Excel cleanup: {exFin.Message}");
                }

                ReleaseComObject(worksheet);
                ReleaseComObject(workbook);
                ReleaseComObject(excelApp);
                
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        private string SanitizeSheetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Sheet1";
            // Invalid characters for Excel sheet names: / \ ? * [ ] :
            string invalidCharsRegex = @"[\\/\?\*\[\]:]";
            string sanitized = System.Text.RegularExpressions.Regex.Replace(name, invalidCharsRegex, "_"); // Replace invalid chars with underscore
            // Sheet name cannot exceed 31 characters
            return sanitized.Length > 31 ? sanitized.Substring(0, 31) : sanitized;
        }

        private string GetExcelColumnName(int columnNumber)
        {
            string columnName = "";
            while (columnNumber > 0)
            {
                int modulo = (columnNumber - 1) % 26;
                columnName = Convert.ToChar(65 + modulo) + columnName;
                columnNumber = (columnNumber - modulo) / 26;
            }
            return columnName;
        }

        private void ReleaseComObject(object obj)
        {
            try
            {
                if (obj != null && Marshal.IsComObject(obj))
                {
                    Marshal.ReleaseComObject(obj);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReleaseComObject error for object of type {obj?.GetType().Name}: {ex.Message}");
            }
            finally
            {
                obj = null; // Help GC by nulling the reference
            }
        }
    }
}