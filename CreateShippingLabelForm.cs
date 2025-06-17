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
        
        // Form controls
        private ComboBox cmbCompanyNumber;
        private TextBox txtCompanyLetter;
        private ComboBox cmbArrowType;
        private TextBox txtProjectNumber;
        private TextBox txtJobNumber;
        private TextBox txtSystemNumber;
        private TextBox txtIDNumber;
        private TextBox txtDescription;
        private TextBox txtQuantity;
        private Button btnPlaceOnDrawing;
        private Button btnClose;
        private PictureBox picPreview;
        
        // Static memory variables to remember the last entered values across form instances
        private static string lastJobNumber = "";
        private static string lastSystemNumber = "";
        private static string lastIDNumber = "";
        private static string lastDescription = "";
        private static string lastQuantity = "1";
        private static string lastProjectNumber = ""; // Project number field
        private static int lastCompanyNumber = 10; // Default to Pride Conveyance
        
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
        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // Form properties
            this.Text = "Create New Shipping Label";
            this.Size = new Size(600, 380); // Slightly taller to avoid control clipping
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            
            // Ensure proper tab navigation for modeless form
            this.KeyPreview = true;  // Enable key preview to handle tab navigation
            this.TabStop = false;    // Form itself doesn't need tab focus
            
            this.ResumeLayout(false);
        }

        private void SetupForm()
        {
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(10)
            };

            // Set column styles
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); // Left panel
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); // Right panel

            // Set row styles
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 80)); // Main content takes most of the space
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); // Button panel (extra space below)

            // Create left panel for input fields
            var inputPanel = CreateInputPanel();
            mainPanel.Controls.Add(inputPanel, 0, 0);

            // Create right panel for preview
            var previewPanel = CreatePreviewPanel();
            mainPanel.Controls.Add(previewPanel, 1, 0);

            // Create button panel
            var buttonPanel = CreateButtonPanel();
            mainPanel.SetColumnSpan(buttonPanel, 2);
            mainPanel.Controls.Add(buttonPanel, 0, 1);

            this.Controls.Add(mainPanel);
        }

        private Panel CreateInputPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, TabStop = false };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(5),
                TabStop = false  // Don't let layout panel interfere with tab navigation
            };

            // Set column styles
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Set row styles for form fields (now 9 rows)
            for (int i = 0; i < 9; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            int row = 0;

            // Company Number (First)
            layout.Controls.Add(new Label { Text = "Company Number:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, TabStop = false }, 0, row);
            cmbCompanyNumber = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, TabIndex = 0 };
            cmbCompanyNumber.SelectedIndexChanged += CmbCompanyNumber_SelectedIndexChanged;
            layout.Controls.Add(cmbCompanyNumber, 1, row++);

            // Company Letter (Second)
            layout.Controls.Add(new Label { Text = "Company Letter:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, TabStop = false }, 0, row);
            txtCompanyLetter = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = SystemColors.Control, TabStop = false };
            layout.Controls.Add(txtCompanyLetter, 1, row++);

            // Arrow Type
            layout.Controls.Add(new Label { Text = "Arrow Type:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, TabStop = false }, 0, row);
            cmbArrowType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, TabIndex = 1 };
            cmbArrowType.SelectedIndexChanged += CmbArrowType_SelectedIndexChanged;
            layout.Controls.Add(cmbArrowType, 1, row++);

            // Project Number
            layout.Controls.Add(new Label { Text = "Project Number:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, TabStop = false }, 0, row);
            txtProjectNumber = new TextBox { Dock = DockStyle.Fill, Text = lastProjectNumber, TabIndex = 2 };
            txtProjectNumber.TextChanged += TxtProjectNumber_TextChanged;
            layout.Controls.Add(txtProjectNumber, 1, row++);

            // Job Number
            layout.Controls.Add(new Label { Text = "Job Number:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, TabStop = false }, 0, row);
            txtJobNumber = new TextBox { Dock = DockStyle.Fill, Text = lastJobNumber, TabIndex = 3 };
            txtJobNumber.TextChanged += TxtJobNumber_TextChanged;
            layout.Controls.Add(txtJobNumber, 1, row++);

            // System Number
            layout.Controls.Add(new Label { Text = "System Number:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, TabStop = false }, 0, row);
            txtSystemNumber = new TextBox { Dock = DockStyle.Fill, Text = lastSystemNumber, TabIndex = 4 };
            txtSystemNumber.TextChanged += TxtSystemNumber_TextChanged;
            layout.Controls.Add(txtSystemNumber, 1, row++);

            // ID Number
            layout.Controls.Add(new Label { Text = "ID Number:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, TabStop = false }, 0, row);
            txtIDNumber = new TextBox { Dock = DockStyle.Fill, Text = lastIDNumber, TabIndex = 5 };
            txtIDNumber.TextChanged += TxtIDNumber_TextChanged;
            layout.Controls.Add(txtIDNumber, 1, row++);

            // Description with autocomplete
            layout.Controls.Add(new Label { Text = "Description:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, TabStop = false }, 0, row);
            txtDescription = new TextBox { Dock = DockStyle.Fill, Text = lastDescription, TabIndex = 6 };
            txtDescription.TextChanged += TxtDescription_TextChanged;
            SetupDescriptionAutoComplete(); // Setup autocomplete functionality
            layout.Controls.Add(txtDescription, 1, row++);

            // Quantity
            layout.Controls.Add(new Label { Text = "Quantity:", TextAlign = ContentAlignment.MiddleRight, AutoSize = true, TabStop = false }, 0, row);
            txtQuantity = new TextBox { Dock = DockStyle.Fill, Text = lastQuantity, TabIndex = 7 };
            txtQuantity.TextChanged += TxtQuantity_TextChanged;
            layout.Controls.Add(txtQuantity, 1, row++);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreatePreviewPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, TabStop = false };
            panel.Controls.Add(new Label 
            { 
                Text = "Preview", 
                Dock = DockStyle.Top, 
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                TabStop = false
            });

            picPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                TabStop = false
            };
            
            panel.Controls.Add(picPreview);
            return panel;
        }

        private Panel CreateButtonPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 60,
                MinimumSize = new Size(0, 60),
                Margin = new Padding(0, 0, 0, 20), // 20px bottom space inside 80px row
                TabStop = false
            };

            // Use TableLayoutPanel for more reliable button positioning
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Height = 60,
                MinimumSize = new Size(0, 60),
                TabStop = false
            };

            // Set column styles: [spacer][Place Block][Close]
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Spacer
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // Place Block button
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // Close button

            btnPlaceOnDrawing = new Button
            {
                Text = isEditMode ? "Update Block" : "Place Block",
                Size = new Size(120, 30),
                UseVisualStyleBackColor = true,
                Anchor = AnchorStyles.Top,
                Margin = new Padding(0, 5, 0, 0), // shift down from row top slightly (~5px)
                TabIndex = 8
            };
            btnPlaceOnDrawing.Click += BtnStartPlacement_Click;

            btnClose = new Button
            {
                Text = "Close",
                Size = new Size(75, 30),
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = true,
                Anchor = AnchorStyles.Top,
                Margin = new Padding(0, 5, 0, 0),
                TabIndex = 9
            };
            btnClose.Click += BtnClose_Click;

            // Add buttons to the layout
            layout.Controls.Add(new Panel() { TabStop = false }, 0, 0); // Spacer panel
            layout.Controls.Add(btnPlaceOnDrawing, 1, 0);
            layout.Controls.Add(btnClose, 2, 0);

            panel.Controls.Add(layout);
            return panel;
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
            // Memory values are already loaded in the TextBox constructors
            // Just need to set the company number and update the letter
            SetCompanyNumber(lastCompanyNumber);
            
            // Initialize description autocomplete for this drawing
            InitializeDescriptionHistory();
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

            return true;
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

        #region Dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                picPreview?.Image?.Dispose();
            }
            base.Dispose(disposing);
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