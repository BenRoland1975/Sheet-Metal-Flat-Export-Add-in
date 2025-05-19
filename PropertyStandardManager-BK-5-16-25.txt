using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using EPDM.Interop.epdm;
using System.Diagnostics;
using System.IO;
using System.Globalization;

namespace RoesleinAddIn
{
    public class PropertyStandardManager
    {
        private ISldWorks swApp;
        private IEdmVault5 pdmVault;
        private Settings currentSettings;

        // Add counters for statistics
        private int propertiesCheckedCount = 0;
        private int propertiesAddedCount = 0;
        private int propertiesUpdatedCount = 0;
        private int propertiesSkippedCount = 0;
        
        // Material mapping statistics
        private bool isSheetMetalPart = false;
        private bool materialMappingPerformed = false;
        private string originalMaterial = "";
        private string mappedMaterial = "";
        private bool materialMappingSuccess = false;
        private bool materialIsMissing = false;
        
        // Add cut list check status properties
        private bool cutListCheckPerformed = false;
        private bool cutListFound = false;
        private bool cutListAutoUpdateEnabled = false;

        // Raw Material processing statistics
        private bool rawMaterialProcessingAttempted = false;
        private bool rawMaterialMatchFound = false;
        private bool rawMaterialPropertiesUpdated = false;
        private string rawMaterialMessage = "";
        
        public PropertyStandardManager(ISldWorks swApp, IEdmVault5 pdmVault)
        {
            this.swApp = swApp;
            this.pdmVault = pdmVault;
            // Load settings when the manager is created
            this.currentSettings = Settings.LoadSettings();
            // Load property standards separately as they are in their own file
            // We might load this list on demand in the Apply method instead, depending on usage.
            // For now, let's assume we load it here or pass it in.
            propertiesUpdatedCount = 0;
            propertiesSkippedCount = 0;
            
            // Reset material mapping stats
            isSheetMetalPart = false;
            materialMappingPerformed = false;
            originalMaterial = "";
            mappedMaterial = "";
            materialMappingSuccess = false;
            materialIsMissing = false;
            
            // Reset cut list check stats
            cutListCheckPerformed = false;
            cutListFound = false;
            cutListAutoUpdateEnabled = false;

            // Reset Raw Material stats
            rawMaterialProcessingAttempted = false;
            rawMaterialMatchFound = false;
            rawMaterialPropertiesUpdated = false;
            rawMaterialMessage = "";
        }

        /// <summary>
        /// Applies the defined property standards to the currently active SolidWorks document.
        /// </summary>
        public void ApplyStandardsToActiveDocument()
        {
            // Reset counters for new operation
            propertiesCheckedCount = 0;
            propertiesAddedCount = 0;
            propertiesUpdatedCount = 0;
            propertiesSkippedCount = 0;
            
            // Reset material mapping stats
            isSheetMetalPart = false;
            materialMappingPerformed = false;
            originalMaterial = "";
            mappedMaterial = "";
            materialMappingSuccess = false;
            materialIsMissing = false;
            
            // Reset cut list check stats
            cutListCheckPerformed = false;
            cutListFound = false;
            cutListAutoUpdateEnabled = false;
            
            // Reset Raw Material stats
            rawMaterialProcessingAttempted = false;
            rawMaterialMatchFound = false;
            rawMaterialPropertiesUpdated = false;
            rawMaterialMessage = "";
            
            ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
            if (swModel == null)
            {
                swApp.SendMsgToUser2("Please open a Part, Assembly, or Drawing file first.", (int)swMessageBoxIcon_e.swMbWarning, (int)swMessageBoxBtn_e.swMbOk);
                return;
            }

            // Load the standards (consider loading only when needed)
            List<PropertyStandardSetting> allStandards = Settings.LoadSettings().PropertyStandards;
            if (allStandards == null || allStandards.Count == 0)
            {
                swApp.SendMsgToUser2("No property standards are defined. Please configure them in the add-in settings.", (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
                return;
            }

            string filePath = swModel.GetPathName();
            if (string.IsNullOrEmpty(filePath))
            {
                swApp.SendMsgToUser2("The file needs to be saved before applying property standards.", (int)swMessageBoxIcon_e.swMbWarning, (int)swMessageBoxBtn_e.swMbOk);
                return;
            }

            // --- Determine Document Type ---
            swDocumentTypes_e docType = (swDocumentTypes_e)swModel.GetType();

            // --- Filter Standards based on Document Type ---
            List<PropertyStandardSetting> applicableStandards = FilterStandardsByDocType(allStandards, docType);

            if (applicableStandards.Count == 0)
            {
                swApp.SendMsgToUser2("No applicable property standards found for this document type.", (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
                return;
            }

            // --- PDM Check Out (Implementation needed) ---
            // bool wasCheckedOut = false; // Commented out: CS0219 - Assigned but never used yet
            // TODO: Check PDM status, check out if necessary using pdmVault
            // IEdmFile5 edmFile = pdmVault.GetFileFromPath(filePath, out _); // Example
            // if (edmFile != null && !edmFile.IsLocked) { /* ... edmFile.LockFile(...); wasCheckedOut = true; ... */ } 

            try
            {
                // First check and update material for sheet metal parts if needed
                if (docType == swDocumentTypes_e.swDocPART)
                {
                    CheckAndMapMaterial(swModel);
                }
                
                // --- Apply Properties ---
                ApplyFilteredStandards(swModel, applicableStandards);

                // Build a detailed message with statistics
                string completionMsg = $"Property standard check complete.\n" +
                                      $"Properties checked: {propertiesCheckedCount}\n" +
                                      $"Properties added: {propertiesAddedCount}\n" +
                                      $"Properties updated: {propertiesUpdatedCount}\n" +
                                      $"Properties skipped: {propertiesSkippedCount}";
                
                // Add material mapping info if it was performed
                if (materialMappingPerformed)
                {
                    completionMsg += $"\n\nSheet Metal Material Check:";
                    if (materialIsMissing)
                    {
                        completionMsg += $"\nWARNING: No material assigned to part.";
                    }
                    else if (materialMappingSuccess)
                    {
                        if (!string.IsNullOrEmpty(originalMaterial) && originalMaterial != mappedMaterial)
                        {
                            completionMsg += $"\nMaterial was successfully mapped from non-standard to standard name.";
                            completionMsg += $"\nOriginal: {originalMaterial}";
                            completionMsg += $"\nMapped to: {mappedMaterial}";
                        }
                        else
                        {
                            completionMsg += $"\nMaterial was already in the correct standard format.";
                        }
                    }
                    else
                    {
                        completionMsg += $"\nWARNING: Material '{originalMaterial}' does not match any material mapping.";
                        completionMsg += $"\nPlease update the material or add it to the Material Mappings.";
                    }
                }
                
                // Add cut list check info if it was performed
                if (cutListCheckPerformed)
                {
                    completionMsg += $"\n\nSheet Metal Cut List Check:";
                    if (cutListFound)
                    {
                        completionMsg += $"\nCut list was found and";
                        if (cutListAutoUpdateEnabled)
                        {
                            completionMsg += $" automatic update is enabled.";
                        }
                        else
                        {
                            completionMsg += $" automatic update was reviewed/set.";
                        }
                    }
                    else
                    {
                        completionMsg += $"\nNo cut list was found. Please add a cut list to this sheet metal part.";
                    }
                }

                // Add Raw Material processing info if it was attempted
                if (rawMaterialProcessingAttempted)
                {
                    completionMsg += $"\n\nRaw Material Matching:";
                    if (!string.IsNullOrEmpty(rawMaterialMessage))
                    {
                        completionMsg += $"\n{rawMaterialMessage}";
                    }
                    else if (rawMaterialMatchFound && rawMaterialPropertiesUpdated)
                    {
                        completionMsg += $"\nSuccessfully matched raw material and updated properties.";
                    }
                    else if (rawMaterialMatchFound && !rawMaterialPropertiesUpdated)
                    {
                        completionMsg += $"\nFound a raw material match, but failed to update properties.";
                    }
                    else
                    {
                        completionMsg += $"\nNo matching raw material found or processing issue occurred.";
                    }
                }
                
                swApp.SendMsgToUser2(completionMsg, (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
            }
            catch (Exception ex)
            {
                // Log error
                Logger.Error("Error applying property standards", ex);
                swApp.SendMsgToUser2($"An error occurred while applying property standards: {ex.Message}", (int)swMessageBoxIcon_e.swMbStop, (int)swMessageBoxBtn_e.swMbOk);
            }
            finally
            {
                // --- PDM Check In (Implementation needed) ---
                // TODO: Check in the file if it was checked out by this process
                // if (wasCheckedOut && edmFile != null && edmFile.IsLocked) { edmFile.UnlockFile(...); }
            }
        }

        /// <summary>
        /// Checks if the part is sheet metal and verifies/maps the material according to the Material Mappings
        /// </summary>
        private void CheckAndMapMaterial(ModelDoc2 swModel)
        {
            try
            {
                // Check if this is a sheet metal part
                bool isSheetMetal = IsSheetMetalPart(swModel);
                isSheetMetalPart = isSheetMetal;
                
                if (!isSheetMetal)
                {
                    Logger.Info("Not a sheet metal part, skipping material mapping check.");
                    return;
                }
                
                // First ensure the cut list is set up correctly
                EnsureSheetMetalCutList(swModel);
                
                materialMappingPerformed = true;
                
                // Get the current material from the model
                string currentMaterial = GetPartMaterial(swModel);
                originalMaterial = currentMaterial;
                
                if (string.IsNullOrEmpty(currentMaterial))
                {
                    Logger.Warning("Sheet metal part has no material assigned.");
                    materialIsMissing = true;
                    return;
                }
                
                // Extract the actual material name from pipe-separated format if needed
                // Format is often: "Library|MaterialName|ID"
                string normalizedMaterial = currentMaterial;
                if (currentMaterial.Contains("|"))
                {
                    string[] parts = currentMaterial.Split('|');
                    if (parts.Length >= 2)
                    {
                        // Use the middle part as the material name
                        normalizedMaterial = parts[1].Trim();
                        Logger.Info($"Extracted material name '{normalizedMaterial}' from '{currentMaterial}'");
                    }
                }
                
                // Load material mappings
                List<MaterialMapping> materialMappings = Settings.LoadMaterialMappings();
                if (materialMappings == null || materialMappings.Count == 0)
                {
                    Logger.Warning("No material mappings defined in settings.");
                    return;
                }
                
                // First check if the material is already in the DXF Output Material column
                var exactMatch = materialMappings.FirstOrDefault(m => 
                    string.Equals(m.DxfMaterial, normalizedMaterial, StringComparison.OrdinalIgnoreCase));
                    
                if (exactMatch != null)
                {
                    // Material is already in the correct format
                    Logger.Info($"Material '{normalizedMaterial}' is already in the correct output format.");
                    materialMappingSuccess = true;
                    mappedMaterial = normalizedMaterial;
                    return;
                }
                
                // Check if the material is in the SolidWorks Material column and needs mapping
                var matchToMap = materialMappings.FirstOrDefault(m => 
                    string.Equals(m.SwMaterial, normalizedMaterial, StringComparison.OrdinalIgnoreCase));
                    
                if (matchToMap != null)
                {
                    // Found a mapping, update the material
                    string targetMaterial = matchToMap.DxfMaterial;
                    mappedMaterial = targetMaterial;
                    
                    // Update the material in the model
                    bool success = SetPartMaterial(swModel, targetMaterial);
                    
                    if (success)
                    {
                        Logger.Info($"Updated material from '{currentMaterial}' to '{targetMaterial}'");
                        materialMappingSuccess = true;
                    }
                    else
                    {
                        Logger.Error($"Failed to update material from '{currentMaterial}' to '{targetMaterial}'");
                    }
                    
                    return;
                }
                
                // If we get here, no mapping was found
                Logger.Warning($"No material mapping found for '{currentMaterial}'.");
                materialMappingSuccess = false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in CheckAndMapMaterial: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the part is a sheet metal part
        /// </summary>
        private bool IsSheetMetalPart(ModelDoc2 swModel)
        {
            try
            {
                int docType = swModel.GetType();
                if (docType != (int)swDocumentTypes_e.swDocPART)
                {
                    return false;
                }
                
                // Iterate through features looking for sheet metal features
                Feature feat = swModel.FirstFeature() as Feature;
                while (feat != null)
                {
                    string typeName = feat.GetTypeName2();
                    if (typeName == "SheetMetal" || typeName == "SMBaseFlange")
                    {
                        return true;
                    }
                    feat = feat.GetNextFeature() as Feature;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking if part is sheet metal: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current material applied to the part
        /// </summary>
        private string GetPartMaterial(ModelDoc2 swModel)
        {
            try
            {
                if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    return string.Empty;
                }
                
                // Try to get material directly from the model
                return swModel.MaterialIdName;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting part material: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Sets the material for the part
        /// </summary>
        private bool SetPartMaterial(ModelDoc2 swModel, string materialName)
        {
            try
            {
                if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    Logger.Warning("Cannot set material on non-part document");
                    return false;
                }
                
                // Convert to PartDoc
                PartDoc partDoc = (PartDoc)swModel;
                
                // Path to custom material database
                const string matPropFile = @"C:\PCSVAULT\SolidWorks Settings\Material Databases 1\Roeslein Standard Materials.sldmat";
                
                // Get the current material for logging
                string originalMaterial = swModel.MaterialIdName ?? "None";
                Logger.Info($"Current material before change: '{originalMaterial}'");
                
                try
                {
                    // Use the method from the macro that works with custom materials
                    Logger.Info($"Setting material to '{materialName}' from database '{matPropFile}'");
                    
                    // This method returns void, not bool
                    partDoc.SetMaterialPropertyName(matPropFile, materialName);
                    
                    // Force a rebuild
                    swModel.ForceRebuild3(false);
                    
                    // Get the new material name
                    string newMaterial = swModel.MaterialIdName ?? "None";
                    Logger.Info($"Material after SetMaterialPropertyName: '{newMaterial}'");
                    
                    if (newMaterial.Contains(materialName))
                    {
                        Logger.Info($"Successfully changed material from '{originalMaterial}' to '{newMaterial}'");
                        return true;
                    }
                    else
                    {
                        Logger.Warning($"SetMaterialPropertyName did not change the material - value still: '{newMaterial}'");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to set material using SetMaterialPropertyName: {ex.Message}");
                }
                
                // Try fallback approaches
                try
                {
                    // Try approach 2: Using configuration specific method
                    string configName = "";
                    Configuration activeConfig = swModel.IGetActiveConfiguration();
                    if (activeConfig != null)
                    {
                        configName = activeConfig.Name;
                    }
                    
                    Logger.Info($"Attempting to set material using SetMaterialPropertyName2 with config '{configName}'");
                    
                    // Call the method with configuration name
                    partDoc.SetMaterialPropertyName2(configName, "", materialName);
                    swModel.ForceRebuild3(false);
                    
                    string newMaterial = swModel.MaterialIdName ?? "None";
                    Logger.Info($"Material after SetMaterialPropertyName2: '{newMaterial}'");
                    
                    if (newMaterial.Contains(materialName))
                    {
                        Logger.Info($"Successfully changed material to '{newMaterial}' using configuration approach");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to set material using SetMaterialPropertyName2: {ex.Message}");
                }
                
                // Last resort - try direct property change
                try
                {
                    Logger.Info($"Attempting to set material using direct MaterialIdName property");
                    swModel.MaterialIdName = materialName;
                    swModel.ForceRebuild3(false);
                    
                    string newMaterial = swModel.MaterialIdName ?? "None";
                    Logger.Info($"Material after direct property setting: '{newMaterial}'");
                    
                    if (newMaterial.Contains(materialName))
                    {
                        Logger.Info($"Successfully changed material to '{newMaterial}' using direct property");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to set material using direct property: {ex.Message}");
                }
                
                // If we get here, all approaches failed
                Logger.Error($"All methods to set material '{materialName}' failed. Current material is '{swModel.MaterialIdName}'");
                
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting part material: {ex.Message}");
                return false;
            }
        }

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
                    return new List<PropertyStandardSetting>(); // Empty list for unknown types
            }
        }

        private void ApplyFilteredStandards(ModelDoc2 swModel, List<PropertyStandardSetting> standardsToApply)
        {
            CustomPropertyManager swCustPropMgr = swModel.Extension.CustomPropertyManager[""]; // Summary properties
            // Configuration specific properties need the active configuration name
            Configuration activeConfig = swModel.IGetActiveConfiguration();
            CustomPropertyManager swConfPropMgr = null;
            if (activeConfig != null) 
            {
                 swConfPropMgr = swModel.Extension.CustomPropertyManager[activeConfig.Name];
            }

            // Determine document type for this file
            swDocumentTypes_e docType = (swDocumentTypes_e)swModel.GetType();

            foreach (var standard in standardsToApply)
            {
                propertiesCheckedCount++; // Increment the checked count
                Logger.Info($"Processing property: {standard.PropertyName}");

                // Determine if this property should be applied to this document type
                bool applyToThisDoc =
                    (docType == swDocumentTypes_e.swDocPART && standard.UseOnPartFiles)
                    || (docType == swDocumentTypes_e.swDocASSEMBLY && standard.UseOnAssemblyFiles)
                    || (docType == swDocumentTypes_e.swDocDRAWING && standard.UseOnDrawingFiles);

                if (!applyToThisDoc)
                {
                    Logger.Info($"Skipping property '{standard.PropertyName}' for this document type.");
                    propertiesSkippedCount++; // Increment skip count for non-applicable properties
                    continue;
                }

                // Set correct expressions for key properties
                string defaultExpr = standard.DefaultValueExpr;

                // Apply to Custom tab if checked
                if (standard.IsCustomProperty)
                {
                    ApplyOrUpdateProperty(swModel.Extension.CustomPropertyManager[""], standard, defaultExpr, "Custom");
                }
                // Apply to Config tab if checked
                if (standard.IsConfigSpecific)
                {
                    if (activeConfig != null)
                    {
                        ApplyOrUpdateProperty(swModel.Extension.CustomPropertyManager[activeConfig.Name], standard, defaultExpr, $"Configuration ({activeConfig.Name})");
                    }
                    else
                    {
                        Logger.Warning($"No active configuration for config-specific property '{standard.PropertyName}'. Skipping.");
                        propertiesSkippedCount++; // Increment skip count for config properties without active config
                    }
                }
            }

            // --- Set the Roeslein Standards Version property using the version from settings ---
            string standardsVersion = Settings.LoadSettings().SettingsVersion;
            if (!string.IsNullOrEmpty(standardsVersion))
            {
                swCustPropMgr.Add3("Roeslein Standards Version", (int)swCustomInfoType_e.swCustomInfoText, standardsVersion, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                if (swConfPropMgr != null)
                {
                    swConfPropMgr.Add3("Roeslein Standards Version", (int)swCustomInfoType_e.swCustomInfoText, standardsVersion, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                }
                Logger.Info($"Set 'Roeslein Standards Version' property to '{standardsVersion}' on summary and active config.");
            }
        }

        private void ApplyOrUpdateProperty(CustomPropertyManager propMgr, PropertyStandardSetting standard, string defaultExpr, string tabName)
        {
            try
            {
                // Get document type from the model - ensure we're getting the actual type
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                swDocumentTypes_e docType = (swDocumentTypes_e)swModel.GetType();
                
                // Debug logging to help diagnose document type issues
                Logger.Info($"Document type detected: {docType} for file: {swModel.GetPathName()}");
                
                // First try to get all properties to check case-sensitively
                string[] allPropertyNames = (string[])propMgr.GetNames();
                bool manualPropertyCheck = false;
                
                if (allPropertyNames != null)
                {
                    foreach (string propName in allPropertyNames)
                    {
                        // Logger.Info($"DEBUG: Found existing property in {tabName}: '{propName}'"); // Removed to reduce log spam
                        if (string.Equals(propName, standard.PropertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            manualPropertyCheck = true;
                            break;
                        }
                    }
                }
                
                // Now try the regular API call
                int getResult = propMgr.Get5(standard.PropertyName, true, out string existingValue, out string resolvedValue, out bool wasResolved);
                
                // Detailed API result debugging
                // Logger.Info($"DEBUG: Get5 API returned: code={getResult}, existingValue='{existingValue}', resolvedValue='{resolvedValue}', wasResolved={wasResolved}, manualPropertyCheck={manualPropertyCheck}");
                
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
                    Logger.Info($"Property '{standard.PropertyName}' exists and has value '{existingValue}'. Overwriting with '{valueToAdd}' because Use Default Value is checked in {tabName} tab.");
                }
                // CASE 2: Property exists AND "Use Default Value" is NOT checked - LEAVE IT ALONE
                else if (propertyExists && !standard.UseDefaultValue)
                {
                    shouldSetValue = false;
                    Logger.Info($"Property '{standard.PropertyName}' exists and has value '{existingValue}'. Not changing (Use Default Value not checked) in {tabName} tab.");
                    propertiesSkippedCount++; // Increment skip count for existing properties
                }
                // CASE 3: Property does NOT exist - ALWAYS ADD IT
                else if (!propertyExists)
                {
                    // Always add properties that don't exist, even if they're blank
                    shouldSetValue = true;
                    valueToAdd = formattedExpression;
                    Logger.Info($"Adding new property '{standard.PropertyName}' with value '{(string.IsNullOrEmpty(valueToAdd) ? "[blank]" : valueToAdd)}' to {tabName} tab.");
                }

                if (shouldSetValue)
                {
                    try
                    {
                        // For expressions in SolidWorks with quotes, we need to ensure they
                        // are passed as-is to the API without extra escaping
                        int addResult = propMgr.Add3(
                            standard.PropertyName,
                            (int)swCustomInfoType_e.swCustomInfoText,
                            valueToAdd,
                            (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

                        // Logger.Info($"DEBUG: Add3 API returned: code={addResult}");

                        if (addResult == 0)
                        {
                            Logger.Info($"Set property '{standard.PropertyName}' to '{valueToAdd}' in {tabName} tab.");
                            
                            // Update the appropriate counter
                            if (propertyExists)
                                propertiesUpdatedCount++; // Increment updated count for existing properties
                            else
                                propertiesAddedCount++;   // Increment added count for new properties
                        }
                        else
                        {
                            Logger.Warning($"Failed to set property '{standard.PropertyName}' in {tabName} tab. Result code: {addResult}");
                            propertiesSkippedCount++; // Increment skip count for failed properties
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Exception setting property '{standard.PropertyName}': {ex.Message}");
                        propertiesSkippedCount++; // Increment skip count for exception cases
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ApplyOrUpdateProperty for {standard.PropertyName}: {ex.Message}");
                propertiesSkippedCount++; // Increment skip count for exception cases
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
        
        private string GetSuffixForDocType(swDocumentTypes_e docType)
        {
            switch (docType)
            {
                case swDocumentTypes_e.swDocASSEMBLY:
                    return "@*.SLDASM";
                case swDocumentTypes_e.swDocDRAWING:
                    return "@*.SLDDRW";
                case swDocumentTypes_e.swDocPART:
                default:
                    return "@*.SLDPRT";
            }
        }

        /// <summary>
        /// Placeholder for evaluating SolidWorks property expressions like $PRPMODEL:"SW-Material".
        /// </summary>
        private string EvaluateExpression(ModelDoc2 swModel, string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return string.Empty;

            // Handle SolidWorks expressions
            if (expression.StartsWith("$"))
            {   
                // Material property handling
                if (expression.StartsWith("$PRPMODEL:\"SW-Mat") || expression.Contains("SW-Material"))
                {
                    try
                    {
                        // Try to get material directly from the model
                        string materialName = swModel.MaterialIdName;
                        
                        if (!string.IsNullOrEmpty(materialName))
                        {
                            return materialName;
                        }

                        // Fallback to MaterialPropertyValues if direct method fails
                        object[] materialPropVals = swModel.MaterialPropertyValues as object[];
                        if (materialPropVals != null && materialPropVals.Length > 0)
                        {
                            materialName = materialPropVals[0] as string;
                            if (!string.IsNullOrEmpty(materialName))
                            {
                                return materialName;
                            }
                        }

                        Logger.Warning($"Could not evaluate material expression: {expression}");
                        return string.Empty;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error evaluating material expression: {ex.Message}");
                        return string.Empty;
                    }
                }
                // Mass property handling
                else if (expression.Equals("$PRP:SW-Mass", StringComparison.OrdinalIgnoreCase))
                {
                    MassProperty swMass = swModel.Extension.CreateMassProperty();
                    if (swMass != null)
                    {   
                        return swMass.Mass.ToString();
                    }
                }
                // Sheet metal thickness handling
                else if (expression.Contains("Thickness"))
                {
                    if (swModel.GetType() == (int)swDocumentTypes_e.swDocPART)
                    {
                        Feature feat = swModel.FirstFeature() as Feature;
                        while (feat != null)
                        {
                            if (feat.GetTypeName2() == "SheetMetal")
                            {
                                var swSheetMetal = feat.GetDefinition() as SheetMetalFeatureData;
                                if (swSheetMetal != null)
                                {
                                    return swSheetMetal.Thickness.ToString();
                                }
                            }
                            feat = feat.GetNextFeature() as Feature;
                        }
                    }
                    else
                    {
                        Logger.Warning("Cannot evaluate thickness on a non-part document.");
                    }
                    return string.Empty;
                }
                
                // For any other expressions, try to evaluate using the model
                try
                {
                    // Get the active configuration name
                    string configName = "";
                    Configuration activeConfig = swModel.IGetActiveConfiguration();
                    if (activeConfig != null)
                    {
                        configName = activeConfig.Name;
                    }

                    // Try to evaluate the expression
                    string evalResult = swModel.GetCustomInfoValue(expression, configName);
                    if (!string.IsNullOrEmpty(evalResult))
                    {
                        return evalResult;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to evaluate expression {expression}: {ex.Message}");
                }
                
                Logger.Warning($"Expression evaluation for '{expression}' returned no result.");
                return expression;
            }

            return expression; // Return literal value if not an expression
        }

        private string MapMaterial(string swMaterial, string partTitle = null)
        {
            if (string.IsNullOrWhiteSpace(swMaterial)) return null;
            
            // Extract the actual material name from pipe-separated format if needed
            string normalized = swMaterial.Trim();
            if (normalized.Contains("|"))
            {
                string[] parts = normalized.Split('|');
                if (parts.Length >= 2)
                {
                    // Use the middle part as the material name
                    normalized = parts[1].Trim();
                }
            }
            
            var mappings = Settings.LoadMaterialMappings();
            var match = mappings.FirstOrDefault(m => string.Equals(m.SwMaterial.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            if (match != null && !string.IsNullOrWhiteSpace(match.DxfMaterial))
            {
                return match.DxfMaterial.Trim();
            }
            
            return null;
        }

        /// <summary>
        /// Reads cut list data and matches raw material for sheet metal parts
        /// </summary>
        private bool ProcessCutListAndRawMaterial(ModelDoc2 swModel)
        {
            rawMaterialProcessingAttempted = true;
            rawMaterialMatchFound = false;
            rawMaterialPropertiesUpdated = false;
            rawMaterialMessage = ""; // Reset message

            try
            {
                if (!isSheetMetalPart || !cutListFound)
                {
                    Logger.Info("Not a sheet metal part with a found cut list, skipping raw material processing.");
                    rawMaterialMessage = "Skipped: Not a sheet metal part or no cut list found.";
                    return false;
                }

                Logger.Info("Reading cut list data for raw material matching...");

                double boundingBoxLength = 0;
                double boundingBoxWidth = 0;
                double sheetMetalThickness = 0;
                string partMaterial = ""; // Renamed to avoid conflict

                Feature swFeat = (Feature)swModel.FirstFeature();
                Feature cutListItemFeature = null;

                // Find the SolidBodyFolder first, then look for CutListFeat under it.
                while (swFeat != null)
                {
                    if (swFeat.GetTypeName2() == "SolidBodyFolder")
                    {
                        Logger.Info($"Found SolidBodyFolder: {swFeat.Name}");
                        // Now iterate sub-features of the SolidBodyFolder to find the cut list item feature
                        Feature subFeat = swFeat.IGetFirstSubFeature() as Feature;
                        while (subFeat != null)
                        {
                            // Common type names for cut list items are "CutListFeat", "WeldMemberFeat", "SheetMetalPart" (if specific)
                            // The log line below is critical for diagnosis.
                            Logger.Info($"Checking sub-feature under SolidBodyFolder '{swFeat.Name}': Name='{subFeat.Name}', Type='{subFeat.GetTypeName2()}'");
                            if (subFeat.GetTypeName2() == "CutListFolder") // Adjust this type name if necessary based on logs
                            {
                                cutListItemFeature = subFeat;
                                Logger.Info($"Found expected CutListFolder: {cutListItemFeature.Name}");
                                break; 
                            }
                            subFeat = subFeat.IGetNextSubFeature() as Feature;
                        }
                        break; // Stop searching for SolidBodyFolder
                    }
                    swFeat = (Feature)swFeat.GetNextFeature();
                }

                if (cutListItemFeature == null)
                {
                    Logger.Warning("Could not find a 'CutListFolder' feature under SolidBodyFolder.");
                    rawMaterialMessage = "Could not find cut list item feature (e.g., CutListFolder).";
                    return false;
                }

                // Get the custom property manager for the cut list item feature
                CustomPropertyManager cutListPropMgr = cutListItemFeature.CustomPropertyManager;
                
                if (cutListPropMgr == null)
                {
                    Logger.Warning($"Could not get CustomPropertyManager for cut list item feature: {cutListItemFeature.Name}");
                    rawMaterialMessage = $"Could not access properties for cut list item: {cutListItemFeature.Name}.";
                    return false;
                }

                string resolvedValue;
                bool wasResolved;

                // Bounding Box Length
                int res = cutListPropMgr.Get5("Bounding Box Length", true, out _, out resolvedValue, out wasResolved);
                if ((res == 0 || res == 2) && wasResolved && double.TryParse(resolvedValue, out boundingBoxLength)) // Accept res == 2 if wasResolved is true
                {
                    Logger.Info($"Found Bounding Box Length: {boundingBoxLength}");
                }
                else 
                {
                    Logger.Warning($"Failed to get Bounding Box Length or parse value. Get5 result: {res}, Resolved: {wasResolved}, Value: '{resolvedValue}'");
                    boundingBoxLength = 0; // Ensure zero if not parsed
                }

                // Bounding Box Width
                res = cutListPropMgr.Get5("Bounding Box Width", true, out _, out resolvedValue, out wasResolved);
                if ((res == 0 || res == 2) && wasResolved && double.TryParse(resolvedValue, out boundingBoxWidth)) // Accept res == 2 if wasResolved is true
                {
                    Logger.Info($"Found Bounding Box Width: {boundingBoxWidth}");
                }
                 else 
                {
                    Logger.Warning($"Failed to get Bounding Box Width or parse value. Get5 result: {res}, Resolved: {wasResolved}, Value: '{resolvedValue}'");
                    boundingBoxWidth = 0; // Ensure zero if not parsed
                }

                // Sheet Metal Thickness - This is often a direct property of the sheet metal feature or a cut-list property
                // Try to get "Sheet Metal Thickness" first, as seen in user's screenshot
                res = cutListPropMgr.Get5("Sheet Metal Thickness", true, out _, out resolvedValue, out wasResolved);
                if ((res == 0 || res == 2) && wasResolved && double.TryParse(resolvedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out sheetMetalThickness))
                {
                    Logger.Info($"Found 'Sheet Metal Thickness' from cut list: {sheetMetalThickness} (assuming document units, e.g., inches)");
                }
                else 
                {
                    Logger.Warning($"Failed to get 'Sheet Metal Thickness' from cut list. Get5 result: {res}, Resolved: {wasResolved}, Value: '{resolvedValue}'. Trying part's sheet metal feature as fallback.");
                    sheetMetalThickness = 0; // Ensure zero before trying fallback
                    if (isSheetMetalPart)
                    {
                        Feature smFeat = swModel.FirstFeature() as Feature;
                        while(smFeat != null)
                        {
                            if (smFeat.GetTypeName2() == "SheetMetal")
                            {
                                SheetMetalFeatureData smData = smFeat.GetDefinition() as SheetMetalFeatureData;
                                if (smData != null)
                                {
                                    double thicknessInMeters = smData.Thickness;
                                    sheetMetalThickness = thicknessInMeters * 39.3701; // Convert meters to inches
                                    Logger.Info($"Found Sheet Metal Thickness from SM feature: {thicknessInMeters} meters, converted to {sheetMetalThickness} inches.");
                                    break;
                                }
                            }
                            smFeat = smFeat.GetNextFeature() as Feature;
                        }
                    }
                }
                
                // Material - from the part itself
                partMaterial = GetPartMaterial(swModel); // This method already logs
                if (string.IsNullOrEmpty(partMaterial)) 
                {
                    Logger.Warning("Part material is empty.");
                    // partMaterial remains empty or null
                } else {
                     Logger.Info($"Found Part Material: {partMaterial}");
                }

                // Check if all necessary data was retrieved
                if (boundingBoxLength == 0 || boundingBoxWidth == 0 || sheetMetalThickness == 0 || string.IsNullOrEmpty(partMaterial))
                {
                    Logger.Warning("Could not find all required cut list data (Length, Width, Thickness, Material).");
                    rawMaterialMessage = "Incomplete cut list data (Length, Width, Thickness, or Material missing/zero).";
                    // Log current values for debugging
                    Logger.Info($"Debug - Length: {boundingBoxLength}, Width: {boundingBoxWidth}, Thickness: {sheetMetalThickness}, Material: '{partMaterial}'");
                    return false;
                }

                // Now match with raw material table
                var rawMaterialSettingsMatch = FindRawMaterialMatch( // Renamed to avoid confusion with class RawMaterialMatch
                    partMaterial,
                    sheetMetalThickness,
                    boundingBoxLength,
                    boundingBoxWidth);

                if (rawMaterialSettingsMatch != null)
                {
                    rawMaterialMatchFound = true;
                    Logger.Info($"Found matching raw material: PN='{rawMaterialSettingsMatch.PartNumber}'");

                    // Calculate Bounding Box Area
                    double boundingBoxArea = 0;
                    if (boundingBoxLength > 0 && boundingBoxWidth > 0) // Ensure dimensions are valid
                    {
                        boundingBoxArea = boundingBoxLength * boundingBoxWidth;
                        Logger.Info($"Calculated Bounding Box Area: {boundingBoxArea} sq. inches");
                    }
                    else
                    {
                        Logger.Warning("Bounding box length or width is zero, area cannot be calculated accurately.");
                    }

                    string areaValue = boundingBoxArea.ToString("F2", CultureInfo.InvariantCulture);
                    string partNumber = rawMaterialSettingsMatch.PartNumber;
                    string unitOfMeasure = rawMaterialSettingsMatch.UnitOfMeasure ?? "EA";
                    string description = rawMaterialSettingsMatch.Description ?? "";

                    // Set properties on summary tab using corrected names/values
                    CustomPropertyManager modelPropMgr = swModel.Extension.CustomPropertyManager[""];
                    modelPropMgr.Add3("Raw Material Number", (int)swCustomInfoType_e.swCustomInfoText, partNumber, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("Unit of Measurement", (int)swCustomInfoType_e.swCustomInfoText, "SI", (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("legacy Part Number", (int)swCustomInfoType_e.swCustomInfoText, partNumber, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("Legacy Unit of Measure", (int)swCustomInfoType_e.swCustomInfoText, unitOfMeasure, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("Raw Mat Amount", (int)swCustomInfoType_e.swCustomInfoText, areaValue, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("Legacy Raw Mat Amount", (int)swCustomInfoType_e.swCustomInfoText, areaValue, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("Raw Material Description", (int)swCustomInfoType_e.swCustomInfoText, description, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    modelPropMgr.Add3("legacy Part Description", (int)swCustomInfoType_e.swCustomInfoText, description, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

                    // Set properties on all configurations using corrected names/values
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
                            configPropMgr.Add3("Legacy Unit of Measure", (int)swCustomInfoType_e.swCustomInfoText, unitOfMeasure, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("Raw Mat Amount", (int)swCustomInfoType_e.swCustomInfoText, areaValue, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("Legacy Raw Mat Amount", (int)swCustomInfoType_e.swCustomInfoText, areaValue, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("Raw Material Description", (int)swCustomInfoType_e.swCustomInfoText, description, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            configPropMgr.Add3("legacy Part Description", (int)swCustomInfoType_e.swCustomInfoText, description, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                        }
                    }

                    rawMaterialPropertiesUpdated = true;
                    rawMaterialMessage = $"Raw Material PN {partNumber} (UOM: {unitOfMeasure}) and all legacy/raw material properties applied to summary tab and all configurations.";
                    return true;
                }
                else
                {
                    rawMaterialMatchFound = false; // Explicitly set
                    Logger.Warning("No matching raw material found in table.");
                    rawMaterialMessage = $"No matching raw material found for: Mat='{partMaterial}', Thk='{sheetMetalThickness}', Size='{boundingBoxLength}x{boundingBoxWidth}'.";
                    // User message for no match is good, but handled by rawMaterialMessage now.
                    // swApp.SendMsgToUser2(...); // This can be removed if the summary message is sufficient
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ProcessCutListAndRawMaterial: {ex.Message}");
                rawMaterialMessage = $"Error during raw material processing: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Finds matching raw material from the settings table
        /// </summary>
        private RawMaterialMatch FindRawMaterialMatch(string material, double thickness, double length, double width)
        {
            try
            {
                // Get raw material data from settings
                if (this.currentSettings == null || this.currentSettings.RawMaterials == null || this.currentSettings.RawMaterials.Count == 0)
                {
                    Logger.Error("No raw materials defined in currentSettings or settings could not be loaded. Raw material matching cannot proceed.");
                    return null;
                }
                var rawMaterials = this.currentSettings.RawMaterials;

                // Define a tolerance for thickness matching
                const double thicknessTolerance = 0.005;
                Logger.Info($"Using thickness tolerance for matching: +/- {thicknessTolerance}");

                // Match material (case insensitive)
                // Ensure the 'material' parameter (from part) is normalized before comparison
                string normalizedPartMaterial = material;
                if (material.Contains("|"))
                {
                    string[] parts = material.Split('|');
                    if (parts.Length >= 2)
                    {
                        normalizedPartMaterial = parts[1].Trim();
                        Logger.Info($"FindRawMaterialMatch: Normalized part material from '{material}' to '{normalizedPartMaterial}' for matching.");
                    }
                }

                // --- DEBUG LOGGING FOR MATCHING ---
                foreach (var rm in rawMaterials)
                {
                    Logger.Info($"Candidate: Mat='{rm.Material}', Thk='{rm.Thickness}', L={rm.Length}, W={rm.Width}");
                    Logger.Info($"  Material match: {string.Equals(rm.Material.Trim(), normalizedPartMaterial, StringComparison.OrdinalIgnoreCase)}");
                    Logger.Info($"  Thickness diff: {Math.Abs(rm.Thickness - thickness)} (tolerance {thicknessTolerance})");
                    Logger.Info($"  Length ok: {rm.Length} >= {length} = {rm.Length >= length}, Width ok: {rm.Width} >= {width} = {rm.Width >= width}");
                }
                // --- END DEBUG LOGGING ---

                var matches = rawMaterials.Where(rm =>
                    string.Equals(rm.Material.Trim(), normalizedPartMaterial, StringComparison.OrdinalIgnoreCase) &&
                    Math.Abs(rm.Thickness - thickness) < thicknessTolerance &&
                    rm.Length >= length &&
                    rm.Width >= width
                ).ToList();

                if (matches.Count == 0)
                {
                    Logger.Info($"No raw material match found for: Material='{material}', Thickness='{thickness}', Length='{length}', Width='{width}' with tolerance {thicknessTolerance}.");
                    return null;
                }
                
                Logger.Info($"Found {matches.Count} potential raw material matches before ordering.");

                // If we have multiple matches, get the smallest sheet that fits our part
                var bestMatch = matches
                    .OrderBy(m => m.Length * m.Width) // Order by sheet area
                    .FirstOrDefault();

                // Convert Settings.RawMaterial to RawMaterialMatch
                if (bestMatch != null)
                {
                    return new RawMaterialMatch
                    {
                        PartNumber = bestMatch.PartNumber,
                        Material = bestMatch.Material,
                        Thickness = bestMatch.Thickness,
                        Length = bestMatch.Length,
                        Width = bestMatch.Width,
                        Description = bestMatch.Description,
                        UnitOfMeasure = bestMatch.UnitOfMeasure
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in FindRawMaterialMatch: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Internal class for raw material matching results
        /// </summary>
        private class RawMaterialMatch
        {
            public string PartNumber { get; set; }
            public string Material { get; set; }
            public double Thickness { get; set; }
            public double Length { get; set; }
            public double Width { get; set; }
            public string Description { get; set; }
            public string UnitOfMeasure { get; set; }
        }

        /// <summary>
        /// Ensures the part has a sheet metal cut list that's set to update automatically
        /// </summary>
        private bool EnsureSheetMetalCutList(ModelDoc2 swModel)
        {
            try
            {
                if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    Logger.Info("Not a part document, skipping cut list check");
                    return false;
                }

                // PartDoc partDoc = (PartDoc)swModel; // Not strictly needed here if using swModel.FeatureManager
                
                Logger.Info("Checking for sheet metal cut list...");
                
                // First, check if this is a sheet metal part at all
                // bool isSheetMetal = IsSheetMetalPart(swModel); // isSheetMetalPart class member is set by IsSheetMetalPart
                this.isSheetMetalPart = IsSheetMetalPart(swModel);
                
                if (!this.isSheetMetalPart)
                {
                    Logger.Info("Not a sheet metal part, skipping cut list check");
                    return false;
                }
                
                // Set flag to indicate check was performed
                cutListCheckPerformed = true;
                
                // Look for the SolidBodyFolder feature
                Feature swFeat = (Feature)swModel.FirstFeature();
                this.cutListFound = false; // Initialize class member
                
                while (swFeat != null)
                {
                    string typeName = swFeat.GetTypeName2();
                    
                    if (typeName == "SolidBodyFolder")
                    {
                        // Found the SolidBodyFolder
                        this.cutListFound = true;
                        Logger.Info("Found SolidBodyFolder feature");
                        
                        // Get the specific feature
                        BodyFolder swBodyFolder = swFeat.GetSpecificFeature2() as BodyFolder;
                        
                        if (swBodyFolder != null)
                        {
                            // Check current settings
                            bool isAuto = swBodyFolder.GetAutomaticCutList();
                            // cutListAutoUpdateEnabled = isAuto; // This is a class member, ensure it's set if needed for reporting
                            Logger.Info($"Automatic cut list is currently: {(isAuto ? "Enabled" : "Disabled")}");
                            
                            // Set to automatic
                            swBodyFolder.SetAutomaticCutList(true);
                            swBodyFolder.SetAutomaticUpdate(true);
                            // cutListAutoUpdateEnabled = true; // Update class member for reporting
                            Logger.Info("Set cut list to automatic update");
                        }
                        else
                        {
                            Logger.Warning("Found SolidBodyFolder but couldn't get BodyFolder interface");
                        }
                        
                        break; // Found the folder, exit loop
                    }
                    
                    // Move to next feature
                    swFeat = (Feature)swFeat.GetNextFeature();
                }
                
                if (!this.cutListFound)
                {
                    Logger.Warning("SolidBodyFolder not found. Attempting to create cut list...");
                    
                    try
                    {
                        // SolidWorks doesn't have a direct InsertCutList method on PartDoc
                        // Instead we need to use the Insert > Cut-List menu command via the API
                        PartDoc partDoc = (PartDoc)swModel; // Needed for GetBodies2

                        // First, try to select the bodies in the part
                        // Use swModel.Extension, not partDoc.Extension
                        bool selectResult = false;
                        try {
                            // Use the correct SelectionManager to select bodies
                            SelectionMgr swSelMgr = (SelectionMgr)swModel.SelectionManager;
                            
                            // Get all bodies in the part
                            Body2[] swBodies = (Body2[])partDoc.GetBodies2((int)swBodyType_e.swSolidBody, true);
                            if (swBodies != null && swBodies.Length > 0)
                            {
                                // Clear current selection
                                swModel.ClearSelection2(true);
                                
                                // Select each solid body
                                foreach (Body2 body in swBodies)
                                {
                                    swSelMgr.AddSelectionListObject(body, null);
                                }
                                
                                selectResult = true;
                                Logger.Info($"Selected {swBodies.Length} bodies for cut list creation");
                            }
                            else
                            {
                                Logger.Warning("No solid bodies found in the part");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error selecting bodies for cut list creation: {ex.Message}");
                        }
                        
                        if (selectResult)
                        {
                            // Use the Cut-List command via command manager
                            // Command ID for Insert Cut-List is 1801
                            bool cmdResult = swModel.Extension.RunCommand(1801, "");
                            
                            if (cmdResult)
                            {
                                Logger.Info("Successfully created cut list through command manager");
                                
                                // Now find it and set it to automatic
                                swModel.ForceRebuild3(false);
                                
                                // Re-scan for the SolidBodyFolder feature
                                Feature newFeat = (Feature)swModel.FirstFeature();
                                
                                while (newFeat != null)
                                {
                                    string typeName = newFeat.GetTypeName2();
                                    
                                    if (typeName == "SolidBodyFolder")
                                    {
                                        this.cutListFound = true; // Mark as found
                                        BodyFolder swBodyFolder = newFeat.GetSpecificFeature2() as BodyFolder;
                                        
                                        if (swBodyFolder != null)
                                        {
                                            swBodyFolder.SetAutomaticCutList(true);
                                            swBodyFolder.SetAutomaticUpdate(true);
                                            // cutListAutoUpdateEnabled = true; // Update class member for reporting
                                            Logger.Info("Set automatic cut list on new cut list");
                                        }
                                        
                                        break;
                                    }
                                    
                                    newFeat = (Feature)newFeat.GetNextFeature();
                                }
                            }
                            else
                            {
                                Logger.Warning("Failed to run cut list command");
                                // cutListFound remains false
                            }
                        }
                        // If selectResult is false, cutListFound remains false
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error creating cut list: {ex.Message}");
                        // cutListFound remains false
                    }
                }

                // After all attempts to find or create:
                if (this.cutListFound)
                {
                    // If a cut list exists (either pre-existing or newly created and configured)
                    // then process it for raw material.
                    // ProcessCutListAndRawMaterial itself checks for isSheetMetalPart and cutListFound.
                    // ProcessCutListAndRawMaterial(swModel); // This call is REMOVED - it's redundant
                    
                    // Update cutListAutoUpdateEnabled based on the final state if needed for reporting
                    // This requires re-fetching the BodyFolder if it was newly created or re-checking.
                    // For simplicity, the existing logging inside the find/create blocks handles immediate status.
                    // The class member cutListAutoUpdateEnabled might need more careful handling if its state
                    // post-creation is critical for the summary message and not just logged during setup.
                    // The original code set it inside the find/create blocks too.
                    // Let's assume the ApplyStandardsToActiveDocument completion message will primarily rely on `cutListFound`.
                }
                else
                {
                    if (this.isSheetMetalPart) // Only log this specific warning if we expected a cutlist
                    {
                         Logger.Warning("Sheet metal part: Cut list could not be found or created. Raw material processing skipped.");
                    }
                }
                
                // The return value of EnsureSheetMetalCutList now primarily indicates if the conditions
                // for attempting raw material processing (isSheetMetalPart and cutListFound) are met.
                // The actual success of ProcessCutListAndRawMaterial is tracked by its own flags.
                bool canAttemptRawMaterialProcessing = this.isSheetMetalPart && this.cutListFound;

                if (canAttemptRawMaterialProcessing)
                {
                    // Log that we are now actually calling it.
                    Logger.Info("EnsureSheetMetalCutList: Conditions met. Proceeding to ProcessCutListAndRawMaterial.");
                    ProcessCutListAndRawMaterial(swModel);
                    // The status flags (rawMaterialProcessingAttempted, rawMaterialMatchFound, etc.)
                    // will be set by ProcessCutListAndRawMaterial.
                }
                else
                {
                     Logger.Info("Skipping ProcessCutListAndRawMaterial due to isSheetMetalPart=false or cutListFound=false.");
                     // Ensure rawMaterialProcessingAttempted reflects this if not already false.
                     // However, ApplyStandardsToActiveDocument calls CheckAndMapMaterial, then EnsureSheetMetalCutList (which then calls ProcessCutListAndRawMaterial)
                     // So `rawMaterialProcessingAttempted` will be set by ProcessCutListAndRawMaterial itself if called.
                     // If ProcessCutListAndRawMaterial is not called, rawMaterialProcessingAttempted remains false from its reset.
                }

                return canAttemptRawMaterialProcessing; // Return if processing could be attempted.
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in EnsureSheetMetalCutList: {ex.Message}");
                return false;
            }
        }

        public void SetPdmVault(IEdmVault5 pdmVault)
        {
            this.pdmVault = pdmVault;
        }

        private Settings.RawMaterial FindRawMaterialMatch(double length, double width, double thicknessInches)
        {
            if (currentSettings.RawMaterials == null || !currentSettings.RawMaterials.Any())
            {
                this.LogMessage("RawMaterials list is empty or null. Cannot find match.");
                return null;
            }
            this.LogMessage($"Finding raw material for L={length}, W={width}, Thickness={thicknessInches} (inches). Tolerance for thickness: +/- {currentSettings.ThicknessToleranceInches}");

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
                    this.LogMessage($"Match found: CSV L={material.Length}, W={material.Width}, T={material.Thickness} with Part L={length}, W={width}, T_Cutlist={thicknessInches}. Ordered part dims: D1={dim1}, D2={dim2}. Ordered mat dims: M1={matDim1}, M2={matDim2}");
                    return material;
                }
                else if (thicknessMatch) 
                {
                    this.LogMessage($"Thickness match for PartNo {material.PartNumber} (T={material.Thickness}), but dimensions no match: Part Dims (ordered) Lrg={dim1}, Sml={dim2}. Material Dims (ordered) Lrg={matDim1}, Sml={matDim2}.");
                }
            }
            this.LogMessage("No suitable raw material found in the list for the given dimensions and thickness.");
            return null;
        }

        private void LogMessage(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[PropertyStandardManager] {DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}");
        }
    }
} 