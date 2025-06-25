using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;

namespace RoesleinAddIn
{
    /// <summary>
    /// Dialog form for creating new shipping labels
    /// </summary>
    public partial class CreateShippingLabelForm : Form
    {


        #region Private Members
        private readonly ISldWorks swApp;
        private ShippingLabelData labelData;
        private ShippingLabelBlockCreator blockCreator;
        
        // Form controls are declared in the Designer file
        
        // Static memory variables to remember the last entered values across form instances
        private static string lastJobNumber = "";
        private static string lastSystemNumber = "";
        private static string lastIDNumber = "";
        private static string lastDescription = "";
        private static string lastQuantity = "1";
        private static string lastProjectNumber = ""; // Project number field
        private static int lastCompanyNumber = 10; // Default to Pride Conveyance
        private static string lastBlockScale = "1.0"; // Default to 1.0 (100% scale)
        
        // Description history for autocomplete functionality (per drawing)
        private static Dictionary<string, HashSet<string>> descriptionHistory = new Dictionary<string, HashSet<string>>();
        private AutoCompleteStringCollection autoCompleteSource;
        private List<string> currentDrawingDescriptions;
        
        // Track successful placement for auto-increment behavior
        private bool lastSuccessfulPlacement = false;
        
        // Edit mode support
        private bool isEditMode = false;
        private ShippingLabelData originalData = null;
        #endregion

        #region Properties
        public ShippingLabelData LabelData => labelData;
        #endregion

        #region Constructor
        public CreateShippingLabelForm(ISldWorks solidWorksApp)
        {
            swApp = solidWorksApp ?? throw new ArgumentNullException(nameof(solidWorksApp));
            labelData = new ShippingLabelData();
            isEditMode = false;
            
            try
            {
                blockCreator = new ShippingLabelBlockCreator(swApp);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not initialize block creator: {ex.Message}");
            }
            
            InitializeComponent();
            SetupForm();
            PopulateCompanyNumbers();
            PopulateArrowTypes();
            LoadMemoryValues();
            
            // Set initial focus to the first meaningful input field
            this.Load += (s, e) => { 
                cmbCompanyNumber.Focus(); 
            };
            
            // Add key event handling for proper tab navigation
            this.KeyDown += CreateShippingLabelForm_KeyDown;
        }
        
        // Constructor for edit mode
        public CreateShippingLabelForm(ISldWorks solidWorksApp, ShippingLabelData existingData)
        {
            swApp = solidWorksApp ?? throw new ArgumentNullException(nameof(solidWorksApp));
            originalData = existingData ?? throw new ArgumentNullException(nameof(existingData));
            labelData = existingData.Clone(); // Make a copy for editing
            isEditMode = true;
            
            try
            {
                blockCreator = new ShippingLabelBlockCreator(swApp);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not initialize block creator: {ex.Message}");
            }
            
            InitializeComponent();
            SetupForm(); // Create the form controls first
            SetupFormForEdit(); // Then setup for edit mode
            PopulateCompanyNumbers();
            PopulateArrowTypes();
            InitializeDescriptionHistory(); // Initialize autocomplete for edit mode too
            LoadExistingData(); // Load existing data instead of memory values
            
            // Set initial focus to the first meaningful input field
            this.Load += (s, e) => { 
                cmbCompanyNumber.Focus(); 
            };
            
            // Add key event handling for proper tab navigation
            this.KeyDown += CreateShippingLabelForm_KeyDown;
        }
        #endregion

        #region Form Setup

        private void SetupForm()
        {
            // Set dynamic form title
            this.Text = isEditMode ? "Edit Shipping Label" : "Create New Shipping Label";
            
            // Set form properties for 4K displays - bigger initial size to prevent cutoff
            this.Size = new Size(Math.Max(750, (int)(750 * this.DeviceDpi / 96.0)), 
                                Math.Max(520, (int)(520 * this.DeviceDpi / 96.0)));

            // Setup event handlers for controls that are now in the designer
            SetupEventHandlers();
            }

        private void SetupEventHandlers()
        {
            // Setup combo box event handlers
            cmbCompanyNumber.SelectedIndexChanged += CmbCompanyNumber_SelectedIndexChanged;
            cmbArrowType.SelectedIndexChanged += CmbArrowType_SelectedIndexChanged;

            // Setup text box event handlers
            txtProjectNumber.TextChanged += TxtProjectNumber_TextChanged;
            txtJobNumber.TextChanged += TxtJobNumber_TextChanged;
            txtSystemNumber.TextChanged += TxtSystemNumber_TextChanged;
            txtIDNumber.TextChanged += TxtIDNumber_TextChanged;
            txtDescription.TextChanged += TxtDescription_TextChanged;
            txtQuantity.TextChanged += TxtQuantity_TextChanged;
            txtBlockScale.TextChanged += TxtBlockScale_TextChanged;

            // Setup button event handlers
            btnPlaceOnDrawing.Click += BtnStartPlacement_Click;
            btnClose.Click += BtnClose_Click;
        }



        private void PopulateArrowTypes()
        {
            cmbArrowType.Items.Clear();
            cmbArrowType.Items.Add(new ArrowTypeItem(ShippingLabelArrowType.Slot, "Slot (Rectangular)"));
            cmbArrowType.Items.Add(new ArrowTypeItem(ShippingLabelArrowType.RightArrow, "Right Arrow (Drive)"));
            cmbArrowType.Items.Add(new ArrowTypeItem(ShippingLabelArrowType.LeftArrow, "Left Arrow"));
            cmbArrowType.Items.Add(new ArrowTypeItem(ShippingLabelArrowType.DoubleArrow, "Double Arrow (Intermediate)"));
            
            cmbArrowType.SelectedIndex = 0; // Default to Slot
            UpdatePreview(); // Show the default preview immediately
        }

        private void PopulateCompanyNumbers()
        {
            // Check if the combo box is initialized
            if (cmbCompanyNumber == null)
            {
                Logger.Warning("cmbCompanyNumber is null, cannot populate company numbers");
                return;
            }

            try
        {
            cmbCompanyNumber.Items.Clear();
            cmbCompanyNumber.Items.Add(new CompanyItem(2, "Roeslein Modular Fabrication, LLC"));
            cmbCompanyNumber.Items.Add(new CompanyItem(5, "Roeslein ShangHai"));
            cmbCompanyNumber.Items.Add(new CompanyItem(10, "Pride Conveyance"));
            cmbCompanyNumber.Items.Add(new CompanyItem(11, "Roeslein Poland"));
            cmbCompanyNumber.Items.Add(new CompanyItem(12, "Roeslein Brasil"));
            
            // Set default to Pride Conveyance (10)
            SetCompanyNumber(lastCompanyNumber);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error populating company numbers: {ex.Message}", ex);
            }
        }

        private void LoadMemoryValues()
        {
            // Set the button text based on edit mode
            btnPlaceOnDrawing.Text = isEditMode ? "Update Block" : "Place Block";
            
            // Set initial values from memory/defaults
            txtProjectNumber.Text = lastProjectNumber;
            txtJobNumber.Text = lastJobNumber;
            txtSystemNumber.Text = lastSystemNumber;
            txtIDNumber.Text = lastIDNumber;
            txtDescription.Text = lastDescription;
            txtQuantity.Text = lastQuantity;
            txtBlockScale.Text = lastBlockScale;
            
            // Setup autocomplete for description
            SetupDescriptionAutoComplete();
            
            // Try to get the last placed label data and populate from it
            var lastLabelData = GetLastPlacedLabelData();
            if (lastLabelData != null)
            {
                Logger.Info($"Found last placed label: {lastLabelData.IDNumber}, auto-populating form");
                
                // Populate from last label data
                txtProjectNumber.Text = lastLabelData.ProjectNumber ?? "";
                txtJobNumber.Text = lastLabelData.JobNumber ?? "";
                txtSystemNumber.Text = lastLabelData.SystemNumber ?? "";
                txtDescription.Text = lastLabelData.Description ?? "";
                txtQuantity.Text = lastLabelData.Quantity ?? "1";
                
                // Set company number
                if (int.TryParse(lastLabelData.CompanyNumber, out int companyNum))
                {
                    SetCompanyNumber(companyNum);
                }
                else
                {
                    SetCompanyNumber(lastCompanyNumber);
                }
                
                // Increment the ID number for the new label
                string incrementedID = GetIncrementedIDNumber(lastLabelData.IDNumber);
                txtIDNumber.Text = incrementedID;
                Logger.Info($"Auto-incremented ID from '{lastLabelData.IDNumber}' to '{incrementedID}'");
                
                // Update static memory variables for future use
                lastProjectNumber = txtProjectNumber.Text;
                lastJobNumber = txtJobNumber.Text;
                lastSystemNumber = txtSystemNumber.Text;
                lastIDNumber = incrementedID;
                lastDescription = txtDescription.Text;
                lastQuantity = txtQuantity.Text;
                lastBlockScale = txtBlockScale.Text;
                lastCompanyNumber = companyNum;
            }
            else
            {
                Logger.Info("No previous labels found, using default memory values");
                // Fall back to previous memory values behavior
                SetCompanyNumber(lastCompanyNumber);
            }
            
            // Initialize description autocomplete for this drawing
            InitializeDescriptionHistory();
        }

        /// <summary>
        /// Gets the last placed label data from the current drawing to auto-populate the form
        /// </summary>
        private ShippingLabelData GetLastPlacedLabelData()
        {
            try
            {
                // Get access to the shipping label manager's data
                var managerForm = ShippingLabelManagerForm.GetInstance();
                if (managerForm == null)
                {
                    Logger.Info("ShippingLabelManager instance not available for data lookup");
                    return null;
                }

                var allLabels = managerForm.GetAllShippingLabels();
                if (allLabels == null || !allLabels.Any())
                {
                    Logger.Info("No existing shipping labels found in drawing");
                    return null;
                }

                // Sort by ID number to get the highest/last one
                var sortedLabels = allLabels
                    .Where(label => !string.IsNullOrWhiteSpace(label.IDNumber))
                    .OrderByDescending(label => ParseIDNumberForSorting(label.IDNumber))
                    .ThenByDescending(label => label.IDNumber) // Secondary sort by string comparison
                    .ToList();

                if (sortedLabels.Any())
                {
                    var lastLabel = sortedLabels.First();
                    Logger.Info($"Found {allLabels.Count()} total labels, highest ID: '{lastLabel.IDNumber}'");
                    return lastLabel;
                }

                Logger.Info("No labels with valid ID numbers found");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting last placed label data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses an ID number for proper numeric sorting (handles formats like "1.7.1-05")
        /// </summary>
        private double ParseIDNumberForSorting(string idNumber)
        {
            if (string.IsNullOrWhiteSpace(idNumber))
                return 0;

            try
            {
                // Handle format like "1.7.1-05" by extracting the last numeric part
                int lastSeparatorIndex = Math.Max(idNumber.LastIndexOf('-'), idNumber.LastIndexOf('.'));
                if (lastSeparatorIndex > 0 && lastSeparatorIndex < idNumber.Length - 1)
                {
                    string lastPart = idNumber.Substring(lastSeparatorIndex + 1);
                    if (double.TryParse(lastPart, out double numValue))
                    {
                        return numValue;
                    }
                }

                // Fallback: try to parse the whole string as a number
                if (double.TryParse(idNumber.Replace("-", "").Replace(".", ""), out double wholeValue))
                {
                    return wholeValue;
                }

                return 0; // Default for non-numeric IDs
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Increments an ID number string (e.g., "1.7.1-05" becomes "1.7.1-06")
        /// </summary>
        private string GetIncrementedIDNumber(string currentID)
        {
            if (string.IsNullOrWhiteSpace(currentID))
                return "1.7.1-01"; // Default starting ID

            try
            {
                // Find the last separator (either - or .)
                int lastSeparatorIndex = Math.Max(currentID.LastIndexOf('-'), currentID.LastIndexOf('.'));
                
                if (lastSeparatorIndex > 0 && lastSeparatorIndex < currentID.Length - 1)
                {
                    string prefix = currentID.Substring(0, lastSeparatorIndex + 1);
                    string numberPart = currentID.Substring(lastSeparatorIndex + 1);
                    
                    if (int.TryParse(numberPart, out int currentNumber))
                    {
                        // Increment the number, preserving leading zeros
                        int newNumber = currentNumber + 1;
                        string incrementedPart = newNumber.ToString().PadLeft(numberPart.Length, '0');
                        return prefix + incrementedPart;
                    }
                }
                
                // Fallback: just append "-01" if we can't parse the format
                return currentID + "-01";
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error incrementing ID number '{currentID}': {ex.Message}");
                return currentID + "-01"; // Safe fallback
            }
        }

        /// <summary>
        /// Sets up autocomplete functionality for the description field
        /// </summary>
        private void SetupDescriptionAutoComplete()
        {
            try
            {
                if (txtDescription == null)
                    return;

                // Initialize the autocomplete collection
                autoCompleteSource = new AutoCompleteStringCollection();
                
                // Set up autocomplete properties
                txtDescription.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                txtDescription.AutoCompleteSource = AutoCompleteSource.CustomSource;
                txtDescription.AutoCompleteCustomSource = autoCompleteSource;

                Logger.Info("Description autocomplete setup completed");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error setting up description autocomplete: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes description history for the current drawing
        /// </summary>
        private void InitializeDescriptionHistory()
        {
            try
            {
                string currentDrawingName = GetCurrentDrawingName();
                if (string.IsNullOrEmpty(currentDrawingName))
                {
                    Logger.Warning("Could not get current drawing name for description history");
                    return;
                }

                // Initialize history for this drawing if it doesn't exist
                if (!descriptionHistory.ContainsKey(currentDrawingName))
                {
                    descriptionHistory[currentDrawingName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                // Load existing descriptions from the current drawing's shipping labels
                LoadExistingDescriptionsFromDrawing(currentDrawingName);

                currentDrawingDescriptions = descriptionHistory[currentDrawingName].ToList();
                
                // Update autocomplete source
                UpdateAutoCompleteSource();

                Logger.Info($"Initialized description history for drawing: {currentDrawingName} with {currentDrawingDescriptions.Count} descriptions");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error initializing description history: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads existing descriptions from shipping labels in the current drawing
        /// </summary>
        private void LoadExistingDescriptionsFromDrawing(string drawingName)
        {
            try
            {
                // Get access to the shipping label manager's data
                var managerForm = ShippingLabelManagerForm.GetInstance();
                if (managerForm == null)
                {
                    Logger.Info("ShippingLabelManager instance not available for description loading");
                    return;
                }

                var allLabels = managerForm.GetAllShippingLabels();
                if (allLabels == null || !allLabels.Any())
                {
                    Logger.Info("No existing shipping labels found for description loading");
                    return;
                }

                // Add all unique descriptions from existing labels to the history
                var drawingHistory = descriptionHistory[drawingName];
                int addedCount = 0;

                foreach (var label in allLabels)
                {
                    if (!string.IsNullOrWhiteSpace(label.Description))
                    {
                        string trimmedDescription = label.Description.Trim();
                        if (drawingHistory.Add(trimmedDescription))
                        {
                            addedCount++;
                        }
                    }
                }

                Logger.Info($"Loaded {addedCount} existing descriptions from {allLabels.Count()} shipping labels");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error loading existing descriptions from drawing: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the autocomplete source with current drawing descriptions
        /// </summary>
        private void UpdateAutoCompleteSource()
        {
            try
            {
                if (autoCompleteSource == null || currentDrawingDescriptions == null)
                    return;

                autoCompleteSource.Clear();
                
                // Add all descriptions from current drawing
                foreach (string description in currentDrawingDescriptions.OrderBy(d => d))
                {
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        autoCompleteSource.Add(description);
                    }
                }

                Logger.Info($"Updated autocomplete source with {autoCompleteSource.Count} descriptions");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error updating autocomplete source: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a description to the history for the current drawing
        /// </summary>
        private void AddDescriptionToHistory(string description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(description))
                    return;

                string currentDrawingName = GetCurrentDrawingName();
                if (string.IsNullOrEmpty(currentDrawingName))
                    return;

                // Ensure history exists for this drawing
                if (!descriptionHistory.ContainsKey(currentDrawingName))
                {
                    descriptionHistory[currentDrawingName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                // Add to history (HashSet prevents duplicates)
                bool wasAdded = descriptionHistory[currentDrawingName].Add(description);
                
                if (wasAdded)
                {
                    // Update current list and autocomplete source
                    currentDrawingDescriptions = descriptionHistory[currentDrawingName].ToList();
                    UpdateAutoCompleteSource();
                    
                    Logger.Info($"Added new description to history: '{description}'");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error adding description to history: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current drawing name for history tracking
        /// </summary>
        private string GetCurrentDrawingName()
        {
            try
            {
                if (swApp == null)
                    return "";

                var swModel = swApp.ActiveDoc as ModelDoc2;
                if (swModel == null)
                    return "";

                string title = swModel.GetTitle();
                
                // Remove file extension if present
                if (title.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase))
                {
                    title = title.Substring(0, title.Length - 7);
                }

                return title;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting current drawing name: {ex.Message}");
                return "";
            }
        }
        
        private void SetupFormForEdit()
        {
            // Change form title for edit mode
            this.Text = "Edit Shipping Label";
        }
        
        private void LoadExistingData()
        {
            try
            {
                Logger.Info($"Loading existing data for edit mode: {originalData.InstanceName}");
                
                // Set company number
                if (int.TryParse(originalData.CompanyNumber, out int companyNum))
                {
                    SetCompanyNumber(companyNum);
                }
                
                // Set arrow type
                for (int i = 0; i < cmbArrowType.Items.Count; i++)
                {
                    if (cmbArrowType.Items[i] is ArrowTypeItem item && item.ArrowType == originalData.ArrowType)
                    {
                        cmbArrowType.SelectedIndex = i;
                        break;
                    }
                }
                
                // Set text fields
                txtProjectNumber.Text = originalData.ProjectNumber ?? "";
                txtJobNumber.Text = originalData.JobNumber ?? "";
                txtSystemNumber.Text = originalData.SystemNumber ?? "";
                txtIDNumber.Text = originalData.IDNumber ?? "";
                txtDescription.Text = originalData.Description ?? "";
                txtQuantity.Text = originalData.Quantity ?? "";
                txtBlockScale.Text = "1.0"; // Default scale for existing blocks
                
                Logger.Info("Successfully loaded existing data into form");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading existing data: {ex.Message}", ex);
                MessageBox.Show($"Error loading existing data: {ex.Message}", "Load Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetCompanyNumber(int companyNumber)
        {
            try
            {
                if (cmbCompanyNumber == null || cmbCompanyNumber.Items == null)
                {
                    Logger.Warning("cmbCompanyNumber or its Items collection is null");
                    return;
                }

            foreach (CompanyItem item in cmbCompanyNumber.Items)
            {
                if (item.CompanyNumber == companyNumber)
                {
                    cmbCompanyNumber.SelectedItem = item;
                    UpdateCompanyLetter(companyNumber);
                    break;
                }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting company number: {ex.Message}", ex);
            }
        }

        private void UpdateCompanyLetter(int companyNumber)
        {
            string letter;
            switch (companyNumber)
            {
                case 2:
                    letter = "R";
                    break;
                case 5:
                    letter = "C";
                    break;
                case 10:
                    letter = "H";
                    break;
                case 11:
                    letter = "P";
                    break;
                case 12:
                    letter = "B";
                    break;
                default:
                    letter = "";
                    break;
            }
            txtCompanyLetter.Text = letter;
        }

        private string GetNextSequenceID(string currentID)
        {
            if (string.IsNullOrEmpty(currentID))
                return "";

            // Find the last occurrence of '-' or '.'
            int lastSeparatorIndex = Math.Max(currentID.LastIndexOf('-'), currentID.LastIndexOf('.'));
            
            if (lastSeparatorIndex == -1)
            {
                // No separator found, sequence the entire string
                return SequenceString(currentID);
            }

            // Split into prefix and suffix
            string prefix = currentID.Substring(0, lastSeparatorIndex + 1);
            string suffix = currentID.Substring(lastSeparatorIndex + 1);
            
            // Sequence the suffix
            string newSuffix = SequenceString(suffix);
            
            return prefix + newSuffix;
        }

        private string SequenceString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "01";

            // Work backwards through the string to find the last sequence of digits or letters
            int i = input.Length - 1;
            
            // Find the last character that can be sequenced
            while (i >= 0 && !char.IsLetterOrDigit(input[i]))
                i--;
            
            if (i < 0)
                return input + "01";

            // Determine if we're dealing with letters or numbers
            bool isLetter = char.IsLetter(input[i]);
            
            if (isLetter)
            {
                // Handle letter sequencing
                return SequenceLetters(input, i);
            }
            else
            {
                // Handle number sequencing
                return SequenceNumbers(input, i);
            }
        }

        private string SequenceLetters(string input, int lastLetterIndex)
        {
            char lastChar = input[lastLetterIndex];
            
            if (lastChar == 'Z')
            {
                // Z -> AA (or similar pattern)
                string prefix = input.Substring(0, lastLetterIndex);
                return prefix + "AA";
            }
            else if (lastChar == 'z')
            {
                // z -> aa (lowercase)
                string prefix = input.Substring(0, lastLetterIndex);
                return prefix + "aa";
            }
            else
            {
                // Increment the letter
                char nextChar = (char)(lastChar + 1);
                return input.Substring(0, lastLetterIndex) + nextChar + input.Substring(lastLetterIndex + 1);
            }
        }

        private string SequenceNumbers(string input, int lastDigitIndex)
        {
            // Find the start of the number sequence
            int startIndex = lastDigitIndex;
            while (startIndex > 0 && char.IsDigit(input[startIndex - 1]))
                startIndex--;

            // Extract the number part
            string numberPart = input.Substring(startIndex, lastDigitIndex - startIndex + 1);
            string prefix = input.Substring(0, startIndex);
            string suffix = input.Substring(lastDigitIndex + 1);

            // Parse and increment the number
            if (int.TryParse(numberPart, out int number))
            {
                number++;
                // Preserve leading zeros
                string newNumberPart = number.ToString().PadLeft(numberPart.Length, '0');
                return prefix + newNumberPart + suffix;
            }

            return input;
        }
        #endregion

        #region Event Handlers
        private void CmbCompanyNumber_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbCompanyNumber.SelectedItem is CompanyItem item)
            {
                lastCompanyNumber = item.CompanyNumber;
                UpdateCompanyLetter(item.CompanyNumber);
            }
        }

        private void CmbArrowType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void TxtProjectNumber_TextChanged(object sender, EventArgs e)
        {
            lastProjectNumber = txtProjectNumber.Text;
        }

        private void TxtJobNumber_TextChanged(object sender, EventArgs e)
        {
            lastJobNumber = txtJobNumber.Text;
        }

        private void TxtSystemNumber_TextChanged(object sender, EventArgs e)
        {
            lastSystemNumber = txtSystemNumber.Text;
        }

        private void TxtIDNumber_TextChanged(object sender, EventArgs e)
        {
            lastIDNumber = txtIDNumber.Text;
        }

        private void TxtDescription_TextChanged(object sender, EventArgs e)
        {
            // Keep the description persistent (don't clear it)
            lastDescription = txtDescription.Text;
        }

        private void TxtQuantity_TextChanged(object sender, EventArgs e)
        {
            lastQuantity = txtQuantity.Text;
        }

        private void TxtBlockScale_TextChanged(object sender, EventArgs e)
        {
            lastBlockScale = txtBlockScale.Text;
        }

        private void BtnStartPlacement_Click(object sender, EventArgs e)
        {
            if (ValidateInput())
            {
                PopulateLabelData();
                
                if (isEditMode)
                {
                    // Edit mode: Update existing block
                    UpdateExistingBlock();
                }
                else
                {
                    // Create mode: Start the block placement
                    StartInteractivePlacement();
                    
                    // If successful, auto-increment ID and prepare for next label
                    if (lastSuccessfulPlacement)
                    {
                        // Add description to history (keep it persistent)
                        if (!string.IsNullOrWhiteSpace(txtDescription.Text))
                        {
                            AddDescriptionToHistory(txtDescription.Text.Trim());
                        }
                        
                        // Auto-sequence the ID number for next time
                        string nextID = GetNextSequenceID(txtIDNumber.Text);
                        if (!string.IsNullOrEmpty(nextID))
                        {
                            lastIDNumber = nextID;
                            txtIDNumber.Text = nextID;
                        }
                        
                        // Keep description persistent - DO NOT clear it
                        // User requested to keep description history and not clear it
                        
                        // Keep form open and ready for next label
                        // Don't steal focus from SolidWorks - let user interact with both
                        txtIDNumber.Focus();
                        txtIDNumber.SelectAll();
                    }
                }
            }
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void CreateShippingLabelForm_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Tab and Arrow key navigation
            if (e.KeyCode == Keys.Tab || e.KeyCode == Keys.Down)
            {
                // Move to next control
                this.SelectNextControl(this.ActiveControl, !e.Shift, true, true, true);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                // Move to previous control
                this.SelectNextControl(this.ActiveControl, false, true, true, true);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Prevent form from being closed by external events (like SolidWorks interactions)
            // Only allow closure if user explicitly clicked Close or system shutdown
            if (e.CloseReason != CloseReason.UserClosing && 
                e.CloseReason != CloseReason.WindowsShutDown && 
                e.CloseReason != CloseReason.ApplicationExitCall)
            {
                Logger.Warning($"Preventing form closure due to reason: {e.CloseReason}");
                e.Cancel = true;
                
                // If form was hidden, make sure it's visible and re-establish ownership
                if (!this.Visible)
                {
                    ReestablishSolidWorksOwnership();
                }
                return;
            }
            
            Logger.Info($"Allowing form closure due to reason: {e.CloseReason}");
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Re-establishes SolidWorks as the owner of this form to keep it associated in taskbar
        /// </summary>
        public void ReestablishSolidWorksOwnership()
        {
            try
            {
                IntPtr swMainWindow = new IntPtr(swApp.IFrameObject().GetHWnd());
                if (swMainWindow != IntPtr.Zero)
                {
                    // Create a temporary wrapper for the SolidWorks window handle
                    var swOwner = new SolidWorksOwnerWrapper(swMainWindow);
                    if (!this.Visible)
                    {
                        this.Show(swOwner);
                    }
                    Logger.Info("Re-established SolidWorks ownership for create form");
                }
                else
                {
                    if (!this.Visible)
                    {
                        this.Show();
                    }
                    Logger.Warning("Could not get SolidWorks window handle for ownership");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to re-establish SolidWorks ownership: {ex.Message}");
                if (!this.Visible)
                {
                    this.Show();
                }
            }
        }
        #endregion

        #region Methods
        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtIDNumber.Text))
            {
                MessageBox.Show("ID Number is required.", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtIDNumber.Focus();
                return false;
            }

            // Validate block scale
            if (!TryParseBlockScale(txtBlockScale.Text, out double scale))
            {
                MessageBox.Show("Block Scale must be a valid positive number (e.g., 1.0, 0.5, 2.0).", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtBlockScale.Focus();
                return false;
            }

            if (scale <= 0)
            {
                MessageBox.Show("Block Scale must be greater than 0.", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtBlockScale.Focus();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tries to parse the block scale text into a valid double value
        /// </summary>
        private bool TryParseBlockScale(string scaleText, out double scale)
        {
            scale = 1.0;
            
            if (string.IsNullOrWhiteSpace(scaleText))
            {
                return true; // Default to 1.0
            }
            
            return double.TryParse(scaleText.Trim(), out scale);
        }

        /// <summary>
        /// Gets the block scale factor from the form input
        /// </summary>
        private double GetBlockScale()
        {
            if (TryParseBlockScale(txtBlockScale.Text, out double scale))
            {
                return scale;
            }
            return 1.0; // Default fallback
        }

        private void PopulateLabelData()
        {
            var selectedArrowItem = cmbArrowType.SelectedItem as ArrowTypeItem;
            var selectedCompanyItem = cmbCompanyNumber.SelectedItem as CompanyItem;
            
            labelData.ArrowType = selectedArrowItem?.ArrowType ?? ShippingLabelArrowType.Slot;
            labelData.ProjectNumber = txtProjectNumber.Text.Trim();
            labelData.JobNumber = txtJobNumber.Text.Trim();
            labelData.SystemNumber = txtSystemNumber.Text.Trim();
            labelData.IDNumber = txtIDNumber.Text.Trim();
            labelData.Description = txtDescription.Text.Trim();
            labelData.Quantity = txtQuantity.Text.Trim();
            
            // Add company information (these will be stored as hidden attributes)
            labelData.CompanyNumber = selectedCompanyItem?.CompanyNumber.ToString() ?? "10";
            labelData.CompanyLetter = txtCompanyLetter.Text.Trim();
            
            // Add block scale
            labelData.BlockScale = GetBlockScale();
            
            // Generate unique instance name to prevent duplicates
            var guidPart = Guid.NewGuid().ToString("N").Substring(0, 8);
            labelData.InstanceName = $"RoesleinShipping_{labelData.ArrowType}_{DateTime.Now:yyyyMMddHHmmss}_{guidPart}";
            
            // Populate drawing and sheet information
            var swModel = swApp.ActiveDoc as ModelDoc2;
            if (swModel != null)
            {
                labelData.DrawingName = System.IO.Path.GetFileNameWithoutExtension(swModel.GetPathName());
                
                var swDraw = swModel as DrawingDoc;
                if (swDraw != null)
                {
                    var currentSheet = swDraw.GetCurrentSheet() as Sheet;
                    labelData.SheetName = currentSheet?.GetName() ?? "Unknown";
                }
            }
            
            // Set default values for other fields to prevent null/empty issues
            if (string.IsNullOrEmpty(labelData.JobNumber)) labelData.JobNumber = "TBD";
            if (string.IsNullOrEmpty(labelData.SystemNumber)) labelData.SystemNumber = "TBD";
            if (string.IsNullOrEmpty(labelData.Description)) labelData.Description = "Shipping Label";
            if (string.IsNullOrEmpty(labelData.Quantity)) labelData.Quantity = "1";
            if (string.IsNullOrEmpty(labelData.DrawingName)) labelData.DrawingName = "Unknown";
            if (string.IsNullOrEmpty(labelData.SheetName)) labelData.SheetName = "Sheet1";
            
            // Initialize other properties to prevent null reference issues
            labelData.HasLeader = false;
            labelData.ComponentName = "";
            labelData.PartFileName = "";
            labelData.MaterialName = "";
            labelData.PartNumber = "";
            labelData.PartDescription = "";
            labelData.Mass = "";
            labelData.Volume = "";
            labelData.ConfigurationName = "";
            labelData.XPosition = 0.0;
            labelData.YPosition = 0.0;
            
            Logger.Info($"PopulateLabelData completed:");
            Logger.Info($"  InstanceName: {labelData.InstanceName}");
            Logger.Info($"  ArrowType: {labelData.ArrowType}");
            Logger.Info($"  ProjectNumber: {labelData.ProjectNumber}");
            Logger.Info($"  JobNumber: {labelData.JobNumber}");
            Logger.Info($"  SystemNumber: {labelData.SystemNumber}");
            Logger.Info($"  IDNumber: {labelData.IDNumber}");
            Logger.Info($"  Description: {labelData.Description}");
            Logger.Info($"  Quantity: {labelData.Quantity}");
            Logger.Info($"  CompanyNumber: {labelData.CompanyNumber}");
            Logger.Info($"  CompanyLetter: {labelData.CompanyLetter}");
            Logger.Info($"  DrawingName: {labelData.DrawingName}");
            Logger.Info($"  SheetName: {labelData.SheetName}");
        }

        private void StartInteractivePlacement()
        {
            try
            {
                if (blockCreator == null)
                {
                    MessageBox.Show("Block creator not available.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lastSuccessfulPlacement = false;
                    return;
                }

                Logger.Info($"=== STARTING BLOCK PLACEMENT ===");
                Logger.Info($"Arrow Type: {labelData.ArrowType}");
                Logger.Info($"ID Number: {labelData.IDNumber}");

                // Hide this form so user can see the drawing clearly
                this.Hide();

                // Start the block placement
                bool success = blockCreator.StartInteractiveBlockPlacement(labelData.ArrowType.ToString(), labelData);

                if (success)
                {
                    lastSuccessfulPlacement = true;
                    
                    // DON'T show popup - it interferes with interactive placement!
                    // The block is now attached to the mouse cursor and the popup would steal focus
                    
                    // Show the form but don't steal focus from SolidWorks
                    this.Show();
                    
                    // Restore tab functionality by setting focus to a control
                    this.BeginInvoke(new Action(() => {
                        if (this.Visible && !this.IsDisposed)
                        {
                            txtIDNumber.Focus();
                        }
                    }));
                }
                else
                {
                    lastSuccessfulPlacement = false;
                    // Show the form again if placement failed
                    this.Show();
                    
                    // Restore tab functionality
                    this.BeginInvoke(new Action(() => {
                        if (this.Visible && !this.IsDisposed)
                        {
                            txtIDNumber.Focus();
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during block placement: {ex.Message}", ex);
                lastSuccessfulPlacement = false;
                
                // Make sure form is visible again on error
                this.Show();
                
                MessageBox.Show($"Error during placement: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateExistingBlock()
        {
            try
            {
                Logger.Info($"=== UPDATING EXISTING BLOCK ===");
                Logger.Info($"Original Instance: {originalData.InstanceName}");
                Logger.Info($"Updating with: ID={labelData.IDNumber}, Description={labelData.Description}");

                if (blockCreator == null)
                {
                    MessageBox.Show("Block creator not available.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Use the original instance name to find and update the block
                labelData.InstanceName = originalData.InstanceName;
                
                // Find the existing block using the block creator
                bool updateSuccess = blockCreator.UpdateExistingBlock(originalData.InstanceName, labelData);

                if (updateSuccess)
                {
                    // Add description to history when successfully updated
                    if (!string.IsNullOrWhiteSpace(labelData.Description))
                    {
                        AddDescriptionToHistory(labelData.Description.Trim());
                    }
                    
                    MessageBox.Show("Block updated successfully!", "Update Complete", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Close the edit form after successful update
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Failed to update the block. The block may not exist in the current drawing.", 
                        "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating existing block: {ex.Message}", ex);
                MessageBox.Show($"Error updating block: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdatePreview()
        {
            // TODO: Create a simple preview image of the selected arrow type
            // For now, just update the preview panel with text
            var selectedItem = cmbArrowType.SelectedItem as ArrowTypeItem;
            if (selectedItem != null)
            {
                // Create a simple preview bitmap
                var preview = CreatePreviewImage(selectedItem.ArrowType);
                picPreview.Image?.Dispose();
                picPreview.Image = preview;
            }
        }

        private Bitmap CreatePreviewImage(ShippingLabelArrowType arrowType)
        {
            var bitmap = new Bitmap(200, 100);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                var pen = new Pen(Color.Black, 1);
                var brush = new SolidBrush(Color.LightGray);

                // Draw EXACT representation matching user's screenshots
                switch (arrowType)
                {
                    case ShippingLabelArrowType.Slot:
                        // Slot: TRUE oval/racetrack shape like user's screenshots
                        DrawSlotPreview(g, pen, brush);
                        break;

                    case ShippingLabelArrowType.RightArrow:
                        DrawRightArrowPreview(g, pen, brush);
                        break;

                    case ShippingLabelArrowType.LeftArrow:
                        DrawLeftArrowPreview(g, pen, brush);
                        break;

                    case ShippingLabelArrowType.DoubleArrow:
                        DrawDoubleArrowPreview(g, pen, brush);
                        break;
                }

                // Draw sample text
                using (var font = new Font("Arial", 8))
                {
                    g.DrawString("ID#", font, Brushes.Black, new PointF(90, 47));
                }

                pen.Dispose();
                brush.Dispose();
            }

            return bitmap;
        }

        private void DrawSlotPreview(Graphics g, Pen pen, Brush brush)
        {
            // EXACTLY like user's screenshot - oval/racetrack shape
            // User's slot is a rounded rectangle (capsule shape)
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            
            // Create a rounded rectangle (racetrack/oval shape)
            int x = 60, y = 42, width = 80, height = 16;
            int radius = height / 2; // Half height for perfect oval ends
            
            // Add the rounded rectangle path
            path.AddArc(x, y, radius * 2, height, 90, 180); // Left semicircle
            path.AddLine(x + radius, y, x + width - radius, y); // Top line
            path.AddArc(x + width - radius * 2, y, radius * 2, height, 270, 180); // Right semicircle  
            path.AddLine(x + width - radius, y + height, x + radius, y + height); // Bottom line
            path.CloseFigure();
            
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
            
            path.Dispose();
        }

        private void DrawNormalizedPolygon(Graphics g, Pen pen, Brush brush, PointF[] normalizedPoints, RectangleF destRect)
        {
            int count = normalizedPoints.Length;
            var scaled = new PointF[count];
            for (int i = 0; i < count; i++)
            {
                var p = normalizedPoints[i];
                scaled[i] = new PointF(destRect.Left + p.X * destRect.Width,
                                       destRect.Top + p.Y * destRect.Height);
            }
            g.FillPolygon(brush, scaled);
            g.DrawPolygon(pen, scaled);
        }

        // Normalised point sets extracted from SVG resources -----------------
        private static readonly PointF[] RightArrowNorm =
        {
            new PointF(0.000f, 0.2275f),
            new PointF(0.8262f, 0.2275f),
            new PointF(0.8262f, 0.0000f),
            new PointF(1.0000f, 0.5000f),
            new PointF(0.8262f, 1.0000f),
            new PointF(0.8262f, 0.7720f),
            new PointF(0.000f, 0.7720f)
        };

        private static readonly PointF[] LeftArrowNorm = RightArrowNorm
            .Select(p => new PointF(1 - p.X, p.Y))
            .Reverse()  // Preserve clockwise winding
            .ToArray();

        private static readonly PointF[] DoubleArrowNorm =
        {
            // Extracted from DOUBLEARROW.svg and normalised
            new PointF(0.0000f, 0.5000f),   // Left tip
            new PointF(0.1524f, 0.0000f),
            new PointF(0.1524f, 0.2385f),
            new PointF(0.8474f, 0.2385f),
            new PointF(0.8474f, 0.0000f),
            new PointF(1.0000f, 0.5000f),   // Right tip
            new PointF(0.8474f, 1.0000f),
            new PointF(0.8474f, 0.7620f),
            new PointF(0.1524f, 0.7620f),
            new PointF(0.1524f, 1.0000f)
        };
        // --------------------------------------------------------------------

        private RectangleF GetArrowDestRect(RectangleF canvas, float aspectRatio)
        {
            const float MaxHeight = 40f;                // px
            const float Margin = 20f;                   // px
            float height = MaxHeight;
            float width = height * aspectRatio;

            // Constrain to canvas width
            float maxWidth = canvas.Width - 2 * Margin;
            if (width > maxWidth)
            {
                width = maxWidth;
                height = width / aspectRatio;
            }

            float x = (canvas.Width - width) / 2f;
            float y = (canvas.Height - height) / 2f;
            return new RectangleF(x, y, width, height);
        }

        private void DrawRightArrowPreview(Graphics g, Pen pen, Brush brush)
        {
            var dest = GetArrowDestRect(g.VisibleClipBounds, 4.094f);
            DrawNormalizedPolygon(g, pen, brush, RightArrowNorm, dest);
        }

        private void DrawLeftArrowPreview(Graphics g, Pen pen, Brush brush)
        {
            var dest = GetArrowDestRect(g.VisibleClipBounds, 4.094f);
            DrawNormalizedPolygon(g, pen, brush, LeftArrowNorm, dest);
        }

        private void DrawDoubleArrowPreview(Graphics g, Pen pen, Brush brush)
        {
            var dest = GetArrowDestRect(g.VisibleClipBounds, 3.305f);
            DrawNormalizedPolygon(g, pen, brush, DoubleArrowNorm, dest);
        }
        #endregion

        #region Helper Classes
        private class ArrowTypeItem
        {
            public ShippingLabelArrowType ArrowType { get; }
            public string DisplayName { get; }

            public ArrowTypeItem(ShippingLabelArrowType arrowType, string displayName)
            {
                ArrowType = arrowType;
                DisplayName = displayName;
            }

            public override string ToString() => DisplayName;
        }

        private class CompanyItem
        {
            public int CompanyNumber { get; }
            public string CompanyName { get; }

            public CompanyItem(int companyNumber, string companyName)
            {
                CompanyNumber = companyNumber;
                CompanyName = companyName;
            }

            public override string ToString() => $"{CompanyNumber}, {CompanyName}";
        }
        #endregion
    }

    /// <summary>
    /// Helper class to wrap SolidWorks window handle for form ownership
    /// </summary>
    public class SolidWorksOwnerWrapper : System.Windows.Forms.NativeWindow, IWin32Window
    {
        public new IntPtr Handle { get; private set; }

        public SolidWorksOwnerWrapper(IntPtr solidWorksHandle)
        {
            Handle = solidWorksHandle;
        }
    }
} 