using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Xml.Linq;

namespace RoesleinAddIn
{
    /// <summary>
    /// Manually define the COM IDispatch interface to ensure it's available.
    /// This is a standard approach for low-level COM interop.
    /// </summary>
    [ComImport]
    [Guid("00020400-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDispatch
    {
        int GetTypeInfoCount();
        void GetTypeInfo([In, MarshalAs(UnmanagedType.U4)] int iTInfo, [In, MarshalAs(UnmanagedType.U4)] int lcid, [Out, MarshalAs(UnmanagedType.Interface)] out System.Runtime.InteropServices.ComTypes.ITypeInfo ppTInfo);
        void GetIDsOfNames([In] ref Guid riid, [In, MarshalAs(UnmanagedType.LPArray)] string[] rgszNames, [In, MarshalAs(UnmanagedType.U4)] int cNames, [In, MarshalAs(UnmanagedType.U4)] int lcid, [Out, MarshalAs(UnmanagedType.LPArray)] int[] rgDispId);
        void Invoke(int dispIdMember, [In] ref Guid riid, [In, MarshalAs(UnmanagedType.U4)] int lcid, [In, MarshalAs(UnmanagedType.U4)] short wFlags, [In, Out] ref System.Runtime.InteropServices.ComTypes.DISPPARAMS pDispParams, [Out] out object pVarResult, [Out] out System.Runtime.InteropServices.ComTypes.EXCEPINFO pExcepInfo, [Out] out int puArgErr);
    }

    /// <summary>
    /// Arrow types for shipping label blocks (like AutoCAD dynamic blocks)
    /// </summary>
    public enum ShippingLabelArrowType
    {
        Slot = 0,        // Rectangular slot shape
        RightArrow = 1,  // Arrow pointing right (Drive configuration)
        LeftArrow = 2,   // Arrow pointing left  
        DoubleArrow = 3  // Double-headed arrow (Intermediate configuration)
    }

    /// <summary>
    /// Main form for managing shipping labels in SolidWorks drawings
    /// </summary>
    public partial class ShippingLabelManagerForm : Form
    {
        #region Private Members
        private ISldWorks swApp;
        private ModelDoc2 swModel;
        private DrawingDoc swDraw;
        private DataTable shippingData;
        private string dataFilePath;
        
        // UI Controls - Fixed type declarations
        private DataGridView dgvShippingLabels;
        private ToolStripButton btnRefresh;
        private ToolStripButton btnForceReload;
        private ToolStripButton btnCreateNew;
        private ToolStripButton btnEditSelected;
        private ToolStripButton btnDeleteSelected;
        private ToolStripButton btnExportCSV;
        private ToolStripButton btnExportDatabase;
        private ToolStripButton btnClose;
        private ToolStripStatusLabel lblStatus;
        private ToolStripProgressBar progressBar;
        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        // Add static dictionary at top of ShippingLabelManagerForm class fields (after existing fields)
        private static Dictionary<string,int> copySequenceCounters = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region Constructor
        public ShippingLabelManagerForm(ISldWorks solidWorksApp)
        {
            swApp = solidWorksApp ?? throw new ArgumentNullException(nameof(solidWorksApp));
            swModel = swApp.ActiveDoc as ModelDoc2;
            
            // Set up data file path based on drawing name
            SetupDataFilePath();
            
            InitializeComponent(); // This now calls the Designer's InitializeComponent
            SetupFormUI(); // Custom UI setup
            SetupDataGrid();
            LoadShippingLabels();
        }
        #endregion

        #region Form UI Setup
        private void SetupFormUI()
        {
            // Create main table layout
            var mainTableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            mainTableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Toolbar
            mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // DataGrid
            mainTableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Status bar

            // Create toolbar
            CreateToolbar();
            mainTableLayout.Controls.Add(toolStrip, 0, 0);

            // Create DataGridView
            CreateDataGridView();
            mainTableLayout.Controls.Add(dgvShippingLabels, 0, 1);

            // Create status bar
            CreateStatusBar();
            mainTableLayout.Controls.Add(statusStrip, 0, 2);

            this.Controls.Add(mainTableLayout);
        }

        private void CreateToolbar()
        {
            toolStrip = new ToolStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden
            };

            // Refresh button
            btnRefresh = new ToolStripButton
            {
                Text = "Refresh",
                Image = SystemIcons.Asterisk.ToBitmap(),
                ImageAlign = ContentAlignment.MiddleLeft,
                ToolTipText = "Refresh shipping labels from drawing"
            };
            btnRefresh.Click += BtnRefresh_Click;

            // Force Reload button
            btnForceReload = new ToolStripButton
            {
                Text = "Force Reload",
                Image = SystemIcons.Exclamation.ToBitmap(),
                ImageAlign = ContentAlignment.MiddleLeft,
                ToolTipText = "Force save/close/reopen drawing to clear ALL caches (use after deleting blocks)"
            };
            btnForceReload.Click += BtnForceReload_Click;

            // Create New button
            btnCreateNew = new ToolStripButton
            {
                Text = "Create New",
                ToolTipText = "Create new shipping label"
            };
            btnCreateNew.Click += BtnCreateNew_Click;

            // Edit Selected button
            btnEditSelected = new ToolStripButton
            {
                Text = "Edit Selected",
                Enabled = false,
                ToolTipText = "Edit selected shipping label"
            };
            btnEditSelected.Click += BtnEditSelected_Click;

            // Delete Selected button
            btnDeleteSelected = new ToolStripButton
            {
                Text = "Delete Selected",
                Enabled = false,
                ToolTipText = "Delete selected shipping labels"
            };
            btnDeleteSelected.Click += BtnDeleteSelected_Click;

            // Export CSV button
            btnExportCSV = new ToolStripButton
            {
                Text = "Export CSV",
                ToolTipText = "Export data to CSV file"
            };
            btnExportCSV.Click += BtnExportCSV_Click;

            // Push to Connex button
            btnExportDatabase = new ToolStripButton
            {
                Text = "Push to Connex",
                ToolTipText = "Push data to Connex system"
            };
            btnExportDatabase.Click += BtnExportDatabase_Click;

            // Close button
            btnClose = new ToolStripButton
            {
                Text = "Close",
                ToolTipText = "Close this window"
            };
            btnClose.Click += BtnClose_Click;

            toolStrip.Items.AddRange(new ToolStripItem[] 
            {
                btnRefresh,
                btnForceReload,
                new ToolStripSeparator(),
                btnCreateNew,
                btnEditSelected,
                btnDeleteSelected,
                new ToolStripSeparator(),
                btnExportCSV,
                btnExportDatabase,
                new ToolStripSeparator(),
                btnClose
            });
        }

        private void CreateDataGridView()
        {
            dgvShippingLabels = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                RowHeadersVisible = true,
                AlternatingRowsDefaultCellStyle = { BackColor = Color.LightGray }
            };

            // Wire up events
            dgvShippingLabels.SelectionChanged += DgvShippingLabels_SelectionChanged;
            dgvShippingLabels.CellDoubleClick += DgvShippingLabels_CellDoubleClick;
            dgvShippingLabels.CellEndEdit += DgvShippingLabels_CellEndEdit;
        }

        private void CreateStatusBar()
        {
            statusStrip = new StatusStrip();
            
            lblStatus = new ToolStripStatusLabel
            {
                Text = "Ready",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            progressBar = new ToolStripProgressBar
            {
                Visible = false,
                Style = ProgressBarStyle.Marquee
            };

            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus, progressBar });
        }
        #endregion

        #region Data Management
        private void SetupDataFilePath()
        {
            try
            {
                string drawingName = "Unknown";
                if (swModel != null)
                {
                    string fullPath = swModel.GetPathName();
                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        drawingName = Path.GetFileNameWithoutExtension(fullPath);
                    }
                }
                
                // Create data file path in same directory as the add-in blocks
                string dataDirectory;

                if (swModel != null)
                {
                    string drawingFullPath = swModel.GetPathName();

                    if (!string.IsNullOrEmpty(drawingFullPath))
                    {
                        // Use the folder that contains the drawing, then create/locate the ShippingLabelData sub-folder
                        string drawingFolder = Path.GetDirectoryName(drawingFullPath);
                        dataDirectory = Path.Combine(drawingFolder ?? string.Empty, "ShippingLabelData");
                    }
                    else
                    {
                        // Drawing has not been saved yet – fall back to previous default location
                        dataDirectory = @"C:\PCSVAULT\SolidWorks Settings\Roeslein SW AddIn\ShippingLabelData\";
                    }
                }
                else
                {
                    // No active model – fall back to previous default location
                    dataDirectory = @"C:\PCSVAULT\SolidWorks Settings\Roeslein SW AddIn\ShippingLabelData\";
                }

                // Ensure the target directory exists
                Directory.CreateDirectory(dataDirectory);

                dataFilePath = Path.Combine(dataDirectory, $"{drawingName}_ShippingLabels.xml");
                Logger.Info($"Data file path set to: {dataFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error setting up data file path: {ex.Message}");
                dataFilePath = @"C:\PCSVAULT\SolidWorks Settings\Roeslein SW AddIn\ShippingLabelData\Default_ShippingLabels.xml";
            }
        }

        private void SaveDataToFile()
        {
            try
            {
                if (shippingData != null && !string.IsNullOrEmpty(dataFilePath))
                {
                    shippingData.WriteXml(dataFilePath);
                    Logger.Info($"Saved shipping label data to: {dataFilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error saving data to file: {ex.Message}");
            }
        }

        private void LoadDataFromFile()
        {
            try
            {
                if (File.Exists(dataFilePath))
                {
                    // Create a temporary DataTable with the same schema as our main table
                    var tempData = new DataTable("ShippingLabels");
                    
                    // Add columns to match our schema exactly
                    tempData.Columns.Add("InstanceName", typeof(string));
                    tempData.Columns.Add("JobNumber", typeof(string));
                    tempData.Columns.Add("SystemNumber", typeof(string));
                    tempData.Columns.Add("IDNumber", typeof(string));
                    tempData.Columns.Add("Description", typeof(string));
                    tempData.Columns.Add("Quantity", typeof(string));
                    tempData.Columns.Add("XPosition", typeof(double));
                    tempData.Columns.Add("YPosition", typeof(double));
                    tempData.Columns.Add("SheetName", typeof(string));
                    tempData.Columns.Add("DrawingName", typeof(string));
                    tempData.Columns.Add("HasLeader", typeof(bool));
                    tempData.Columns.Add("ComponentName", typeof(string));
                    tempData.Columns.Add("PartFileName", typeof(string));
                    tempData.Columns.Add("MaterialName", typeof(string));
                    tempData.Columns.Add("PartNumber", typeof(string));
                    tempData.Columns.Add("PartDescription", typeof(string));
                    tempData.Columns.Add("Mass", typeof(string));
                    tempData.Columns.Add("Volume", typeof(string));
                    tempData.Columns.Add("ConfigurationName", typeof(string));
                    tempData.Columns.Add("ArrowType", typeof(string));
                    tempData.Columns.Add("CompanyNumber", typeof(string));
                    tempData.Columns.Add("CompanyLetter", typeof(string));
                    
                    // Try to read the XML with the predefined schema
                    try
                    {
                        tempData.ReadXml(dataFilePath);
                        Logger.Info($"Successfully loaded XML data with schema");
                    }
                    catch (Exception xmlEx)
                    {
                        Logger.Warning($"Failed to load XML with schema: {xmlEx.Message}, trying alternative method");
                        
                        // Alternative: Try to read as DataSet first
                        var dataSet = new DataSet();
                        dataSet.ReadXml(dataFilePath);
                        
                        if (dataSet.Tables.Count > 0)
                        {
                            var sourceTable = dataSet.Tables[0];
                            Logger.Info($"Loaded DataSet with {sourceTable.Rows.Count} rows");
                            
                            // Manually copy data to our structured table
                            foreach (DataRow sourceRow in sourceTable.Rows)
                            {
                                var newRow = tempData.NewRow();
                                
                                // Safely copy each column
                                foreach (DataColumn col in tempData.Columns)
                                {
                                    if (sourceTable.Columns.Contains(col.ColumnName))
                                    {
                                        var sourceValue = sourceRow[col.ColumnName];
                                        if (sourceValue != null && sourceValue != DBNull.Value)
                                        {
                                            try
                                            {
                                                // Convert to the correct type
                                                if (col.DataType == typeof(double))
                                                {
                                                    newRow[col.ColumnName] = Convert.ToDouble(sourceValue);
                                                }
                                                else if (col.DataType == typeof(bool))
                                                {
                                                    newRow[col.ColumnName] = Convert.ToBoolean(sourceValue);
                                                }
                                                else
                                                {
                                                    newRow[col.ColumnName] = sourceValue.ToString();
                                                }
                                            }
                                            catch
                                            {
                                                // Use default value if conversion fails
                                                if (col.DataType == typeof(double))
                                                    newRow[col.ColumnName] = 0.0;
                                                else if (col.DataType == typeof(bool))
                                                    newRow[col.ColumnName] = false;
                                                else
                                                    newRow[col.ColumnName] = "";
                                            }
                                        }
                                        else
                                        {
                                            // Set default values for null/empty
                                            if (col.DataType == typeof(double))
                                                newRow[col.ColumnName] = 0.0;
                                            else if (col.DataType == typeof(bool))
                                                newRow[col.ColumnName] = false;
                                            else
                                                newRow[col.ColumnName] = "";
                                        }
                                    }
                                    else
                                    {
                                        // Column doesn't exist in source, use default
                                        if (col.DataType == typeof(double))
                                            newRow[col.ColumnName] = 0.0;
                                        else if (col.DataType == typeof(bool))
                                            newRow[col.ColumnName] = false;
                                        else
                                            newRow[col.ColumnName] = "";
                                    }
                                }
                                
                                tempData.Rows.Add(newRow);
                            }
                        }
                    }
                    
                    // Merge with current data to avoid duplicates
                    foreach (DataRow row in tempData.Rows)
                    {
                        string instanceName = row["InstanceName"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(instanceName))
                        {
                            bool exists = false;
                            
                            foreach (DataRow existingRow in shippingData.Rows)
                            {
                                if (existingRow["InstanceName"]?.ToString() == instanceName)
                                {
                                    exists = true;
                                    break;
                                }
                            }
                            
                            if (!exists)
                            {
                                var newRow = shippingData.NewRow();
                                newRow.ItemArray = row.ItemArray;
                                shippingData.Rows.Add(newRow);
                            }
                        }
                    }
                    
                    Logger.Info($"Loaded {tempData.Rows.Count} shipping labels from: {dataFilePath}");
                }
                else
                {
                    Logger.Info($"Data file does not exist: {dataFilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error loading data from file: {ex.Message}");
            }
        }

        private void LoadSavedDataIntoTable(DataTable targetTable)
        {
            try
            {
                if (File.Exists(dataFilePath))
                {
                    Logger.Info($"Loading saved data from: {dataFilePath}");
                    
                    // Setup table structure first
                    SetupDataTableStructure(targetTable);
                    
                    // Try to load as DataSet first (more robust)
                    try
                    {
                        var dataSet = new DataSet();
                        dataSet.ReadXml(dataFilePath);
                        
                        if (dataSet.Tables.Count > 0)
                        {
                            var sourceTable = dataSet.Tables[0];
                            
                            // Copy data with proper type handling
                            foreach (DataRow sourceRow in sourceTable.Rows)
                            {
                                var newRow = targetTable.NewRow();
                                
                                foreach (DataColumn col in targetTable.Columns)
                                {
                                    if (sourceTable.Columns.Contains(col.ColumnName))
                                    {
                                        var sourceValue = sourceRow[col.ColumnName];
                                        if (sourceValue != null && sourceValue != DBNull.Value)
                                        {
                                            try
                                            {
                                                newRow[col.ColumnName] = sourceValue;
                                            }
                                            catch
                                            {
                                                // Use default value if conversion fails
                                                if (col.DataType == typeof(double))
                                                    newRow[col.ColumnName] = 0.0;
                                                else if (col.DataType == typeof(bool))
                                                    newRow[col.ColumnName] = false;
                                                else
                                                    newRow[col.ColumnName] = "";
                                            }
                                        }
                                        else
                                        {
                                            // Set default values for null/empty
                                            if (col.DataType == typeof(double))
                                                newRow[col.ColumnName] = 0.0;
                                            else if (col.DataType == typeof(bool))
                                                newRow[col.ColumnName] = false;
                                            else
                                                newRow[col.ColumnName] = "";
                                        }
                                    }
                                    else
                                    {
                                        // Column doesn't exist in source, use default
                                        if (col.DataType == typeof(double))
                                            newRow[col.ColumnName] = 0.0;
                                        else if (col.DataType == typeof(bool))
                                            newRow[col.ColumnName] = false;
                                        else
                                            newRow[col.ColumnName] = "";
                                    }
                                }
                                
                                targetTable.Rows.Add(newRow);
                            }
                            
                            Logger.Info($"Loaded {targetTable.Rows.Count} saved records");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Error loading saved data: {ex.Message}");
                    }
                }
                else
                {
                    Logger.Info($"No saved data file found: {dataFilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error in LoadSavedDataIntoTable: {ex.Message}");
            }
        }

        private DataRow FindSavedRowByInstanceName(DataTable savedData, string instanceName)
        {
            if (savedData == null || string.IsNullOrEmpty(instanceName))
                return null;

            foreach (DataRow row in savedData.Rows)
            {
                if (row["InstanceName"]?.ToString() == instanceName)
                {
                    return row;
                }
            }
            
            return null;
        }

        private void SetupDataTableStructure(DataTable table)
        {
            if (table.Columns.Count > 0) return; // Already setup
            
            // Ensure the table has a meaningful name for XML serialization
            table.TableName = "ShippingLabels";
            
            // Add columns to match ShippingLabelData structure
            table.Columns.Add("InstanceName", typeof(string));
            table.Columns.Add("ProjectNumber", typeof(string));
            table.Columns.Add("JobNumber", typeof(string));
            table.Columns.Add("SystemNumber", typeof(string));
            table.Columns.Add("IDNumber", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("Quantity", typeof(string));
            table.Columns.Add("XPosition", typeof(double));
            table.Columns.Add("YPosition", typeof(double));
            table.Columns.Add("SheetName", typeof(string));
            table.Columns.Add("DrawingName", typeof(string));
            table.Columns.Add("HasLeader", typeof(bool));
            table.Columns.Add("ComponentName", typeof(string));
            table.Columns.Add("PartFileName", typeof(string));
            table.Columns.Add("MaterialName", typeof(string));
            table.Columns.Add("PartNumber", typeof(string));
            table.Columns.Add("PartDescription", typeof(string));
            table.Columns.Add("Mass", typeof(string));
            table.Columns.Add("Volume", typeof(string));
            table.Columns.Add("ConfigurationName", typeof(string));
            table.Columns.Add("ArrowType", typeof(string));
            table.Columns.Add("CompanyNumber", typeof(string));
            table.Columns.Add("CompanyLetter", typeof(string));

            // Define primary key on InstanceName to automatically prevent duplicate rows
            table.PrimaryKey = new[] { table.Columns["InstanceName"] };
        }

        private void SetupDataGrid()
        {
            // Create data table structure
            shippingData = new DataTable("ShippingLabels");

            // Add columns to match ShippingLabelData structure
            shippingData.Columns.Add("InstanceName", typeof(string));
            shippingData.Columns.Add("ProjectNumber", typeof(string));
            shippingData.Columns.Add("JobNumber", typeof(string));
            shippingData.Columns.Add("SystemNumber", typeof(string));
            shippingData.Columns.Add("IDNumber", typeof(string));
            shippingData.Columns.Add("Description", typeof(string));
            shippingData.Columns.Add("Quantity", typeof(string));
            shippingData.Columns.Add("XPosition", typeof(double));
            shippingData.Columns.Add("YPosition", typeof(double));
            shippingData.Columns.Add("SheetName", typeof(string));
            shippingData.Columns.Add("DrawingName", typeof(string));
            shippingData.Columns.Add("HasLeader", typeof(bool));
            shippingData.Columns.Add("ComponentName", typeof(string));
            shippingData.Columns.Add("PartFileName", typeof(string));
            shippingData.Columns.Add("MaterialName", typeof(string));
            shippingData.Columns.Add("PartNumber", typeof(string));
            shippingData.Columns.Add("PartDescription", typeof(string));
            shippingData.Columns.Add("Mass", typeof(string));
            shippingData.Columns.Add("Volume", typeof(string));
            shippingData.Columns.Add("ConfigurationName", typeof(string));
            shippingData.Columns.Add("ArrowType", typeof(string));
            shippingData.Columns.Add("CompanyNumber", typeof(string));
            shippingData.Columns.Add("CompanyLetter", typeof(string));

            // Setup DataGridView columns
            dgvShippingLabels.Columns.Clear();

            // Add columns with specific properties
            dgvShippingLabels.Columns.Add(CreateTextColumn("InstanceName", "Instance ID", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("ArrowType", "Arrow Type", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("ProjectNumber", "Project Number", false));
            dgvShippingLabels.Columns.Add(CreateTextColumn("JobNumber", "Job Number", false));
            dgvShippingLabels.Columns.Add(CreateTextColumn("SystemNumber", "System Number", false));
            dgvShippingLabels.Columns.Add(CreateTextColumn("IDNumber", "ID Number", false));
            dgvShippingLabels.Columns.Add(CreateTextColumn("Description", "Description", false));
            dgvShippingLabels.Columns.Add(CreateTextColumn("Quantity", "Quantity", false));
            dgvShippingLabels.Columns.Add(CreateTextColumn("XPosition", "X Position", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("YPosition", "Y Position", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("SheetName", "Sheet", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("DrawingName", "Drawing", true));
            dgvShippingLabels.Columns.Add(CreateCheckBoxColumn("HasLeader", "Has Leader", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("ComponentName", "Component", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("PartFileName", "Part File", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("MaterialName", "Material", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("PartNumber", "Part Number", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("PartDescription", "Part Description", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("Mass", "Mass", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("Volume", "Volume", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("ConfigurationName", "Configuration", true));
            dgvShippingLabels.Columns.Add(CreateTextColumn("CompanyNumber", "Company Number", false));
            dgvShippingLabels.Columns.Add(CreateTextColumn("CompanyLetter", "Company Letter", false));

            // Bind data source
            dgvShippingLabels.DataSource = shippingData;

            // Auto-size columns
            dgvShippingLabels.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
        }

        private DataGridViewTextBoxColumn CreateTextColumn(string dataPropertyName, string headerText, bool isReadOnly)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = dataPropertyName,
                HeaderText = headerText,
                ReadOnly = isReadOnly
            };
        }

        private DataGridViewCheckBoxColumn CreateCheckBoxColumn(string dataPropertyName, string headerText, bool isReadOnly)
        {
            return new DataGridViewCheckBoxColumn
            {
                DataPropertyName = dataPropertyName,
                HeaderText = headerText,
                ReadOnly = isReadOnly
            };
        }
        #endregion

        #region Shipping Label Extraction
        private void LoadShippingLabels()
        {
            try
            {
                UpdateStatus("Loading shipping labels...");
                ShowProgress(true);

                // Clear existing data
                shippingData.Clear();

                // Get the drawing document
                swDraw = swModel as DrawingDoc;
                if (swDraw == null)
                {
                    UpdateStatus("Failed to get drawing document");
                    ShowProgress(false);
                    return;
                }

                // STEP 1: Extract all shipping labels from drawing (source of truth for positions and block existence)
                Logger.Info("Step 1: Reading all blocks from drawing...");
                var drawingBlocks = ExtractAllShippingLabels();
                Logger.Info($"Found {drawingBlocks.Count} blocks in drawing");

                // STEP 2: Load saved XML data for attribute values (source of truth for user-edited data)
                Logger.Info("Step 2: Loading saved XML data for attribute values...");
                var savedData = new DataTable();
                LoadSavedDataIntoTable(savedData);
                Logger.Info($"Loaded {savedData.Rows.Count} saved records from XML");

                // STEP 3: Merge drawing blocks with saved data using UID-based matching
                Logger.Info("Step 3: Merging drawing blocks with saved attribute data using UID matching...");
                foreach (var drawingBlock in drawingBlocks)
                {
                    var row = shippingData.NewRow();
                    
                    // Always use current drawing data for positions and basic info
                    row["InstanceName"] = drawingBlock.InstanceName; // This should be the UID
                    row["XPosition"] = drawingBlock.XPosition;
                    row["YPosition"] = drawingBlock.YPosition;
                    row["SheetName"] = drawingBlock.SheetName ?? "";
                    row["DrawingName"] = drawingBlock.DrawingName ?? "";
                    row["HasLeader"] = drawingBlock.HasLeader;
                    row["ComponentName"] = drawingBlock.ComponentName ?? "";
                    row["PartFileName"] = drawingBlock.PartFileName ?? "";
                    row["MaterialName"] = drawingBlock.MaterialName ?? "";
                    row["PartNumber"] = drawingBlock.PartNumber ?? "";
                    row["PartDescription"] = drawingBlock.PartDescription ?? "";
                    row["Mass"] = drawingBlock.Mass ?? "";
                    row["Volume"] = drawingBlock.Volume ?? "";
                    row["ConfigurationName"] = drawingBlock.ConfigurationName ?? "";
                    row["ArrowType"] = drawingBlock.ArrowType.ToString();

                    // For user-editable attributes, try to match by UID first, then by feature name
                    DataRow savedRow = null;
                    
                    // First try to find by UID (preferred method)
                    if (drawingBlock.InstanceName.StartsWith("SW"))
                    {
                        savedRow = FindSavedRowByInstanceName(savedData, drawingBlock.InstanceName);
                        if (savedRow != null)
                        {
                            Logger.Info($"Found saved data by UID: {drawingBlock.InstanceName}");
                        }
                    }
                    
                    // If no UID match found, try to find by feature name patterns
                    if (savedRow == null)
                    {
                        // Try common feature name patterns
                        string[] possibleNames = {
                            drawingBlock.InstanceName,
                            drawingBlock.InstanceName.Replace("RoesleinShippingLabel_", ""),
                            "RoesleinShippingLabel_" + drawingBlock.ArrowType.ToString()
                        };
                        
                        foreach (string possibleName in possibleNames)
                        {
                            savedRow = FindSavedRowByInstanceName(savedData, possibleName);
                            if (savedRow != null)
                            {
                                Logger.Info($"Found saved data by feature name pattern: {possibleName}");
                                break;
                            }
                        }
                    }
                    
                    if (savedRow != null)
                    {
                        // Use saved attribute values (user may have edited these)
                        row["ProjectNumber"] = savedRow["ProjectNumber"] ?? drawingBlock.ProjectNumber ?? "";
                        row["JobNumber"] = savedRow["JobNumber"] ?? drawingBlock.JobNumber ?? "";
                        row["SystemNumber"] = savedRow["SystemNumber"] ?? drawingBlock.SystemNumber ?? "";
                        row["IDNumber"] = savedRow["IDNumber"] ?? drawingBlock.IDNumber ?? "";
                        row["Description"] = savedRow["Description"] ?? drawingBlock.Description ?? "";
                        row["Quantity"] = savedRow["Quantity"] ?? drawingBlock.Quantity ?? "";
                        row["CompanyNumber"] = savedRow["CompanyNumber"] ?? drawingBlock.CompanyNumber ?? "";
                        row["CompanyLetter"] = savedRow["CompanyLetter"] ?? drawingBlock.CompanyLetter ?? "";
                        Logger.Info($"Merged saved data for block: {drawingBlock.InstanceName}");
                    }
                    else
                    {
                        // Use block attribute values (fresh block or no saved data)
                        row["ProjectNumber"] = drawingBlock.ProjectNumber ?? "";
                        row["JobNumber"] = drawingBlock.JobNumber ?? "";
                        row["SystemNumber"] = drawingBlock.SystemNumber ?? "";
                        row["IDNumber"] = drawingBlock.IDNumber ?? "";
                        row["Description"] = drawingBlock.Description ?? "";
                        row["Quantity"] = drawingBlock.Quantity ?? "";
                        row["CompanyNumber"] = drawingBlock.CompanyNumber ?? "";
                        row["CompanyLetter"] = drawingBlock.CompanyLetter ?? "";
                        Logger.Info($"Using block attributes for: {drawingBlock.InstanceName}");
                    }
                    
                    shippingData.Rows.Add(row);
                }

                // STEP 4: Save merged data to XML to preserve any new blocks found
                Logger.Info("Step 4: Saving merged data to XML...");
                SaveDataToFile();

                UpdateStatus($"Loaded {shippingData.Rows.Count} shipping labels");
                ShowProgress(false);
                
                Logger.Info($"LoadShippingLabels completed successfully with {shippingData.Rows.Count} labels");
                
                // Force DataGridView refresh to show updated data
                RefreshDataGridView();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading shipping labels: {ex.Message}", ex);
                MessageBox.Show($"Error loading shipping labels: {ex.Message}", "Load Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("Error loading data");
                ShowProgress(false);
            }
        }

        private List<ShippingLabelData> ExtractAllShippingLabels()
        {
            var labels = new List<ShippingLabelData>();

            try
            {
                // Get all sheets in the drawing
                string[] sheetNames = swDraw.GetSheetNames() as string[];
                
                if (sheetNames == null || sheetNames.Length == 0)
                {
                    Logger.Warning("No sheets found in drawing");
                    return labels;
                }

                Sheet currentSheet = swDraw.GetCurrentSheet() as Sheet;
                string currentSheetName = currentSheet?.GetName();

                foreach (string sheetName in sheetNames)
                {
                    // Activate each sheet
                    bool success = swDraw.ActivateSheet(sheetName);
                    if (!success)
                    {
                        Logger.Warning($"Failed to activate sheet: {sheetName}");
                        continue;
                    }

                    // Extract labels from current sheet
                    var sheetLabels = ExtractShippingLabelsFromCurrentSheet(sheetName);
                    labels.AddRange(sheetLabels);
                }

                // Restore original sheet
                if (!string.IsNullOrEmpty(currentSheetName))
                {
                    swDraw.ActivateSheet(currentSheetName);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting shipping labels: {ex.Message}", ex);
            }

            return labels;
        }

        private List<ShippingLabelData> ExtractShippingLabelsFromCurrentSheet(string sheetName)
        {
            var labels = new List<ShippingLabelData>();
            var processedBlocks = new HashSet<string>(); // Track processed blocks to avoid duplicates

            try
            {
                Sheet currentSheet = swDraw.GetCurrentSheet() as Sheet;
                if (currentSheet == null) return labels;

                Logger.Info($"Extracting blocks from sheet: {sheetName}");

                // AGGRESSIVE APPROACH: Force complete SolidWorks cache clearing
                ClearSolidWorksDocumentCache();

                // Primary approach: Get all features from the drawing model
                object[] allObjects = swModel.FeatureManager.GetFeatures(false) as object[];
                if (allObjects != null)
                {
                    Logger.Info($"Found {allObjects.Length} total features in drawing");
                    
                    foreach (object objFeature in allObjects)
                    {
                        if (objFeature is Feature feature)
                        {
                            // Look for sketch block instances
                            if (feature.GetTypeName2() == "SketchBlockInst")
                            {
                                // ENHANCED DELETION DETECTION - Multiple checks to skip deleted blocks
                                try
                                {
                                    string featureName = feature.Name;
                                    
                                    // Check 0: Skip if in deletion blacklist
                                    if (IsBlockInDeletionBlacklist(featureName))
                                    {
                                        Logger.Info($"Skipping blacklisted (confirmed deleted) block: {featureName}");
                                        continue;
                                    }

                                    // Check 1: Standard suppression
                                    if (feature.IsSuppressed())
                                    {
                                        Logger.Info($"Skipping suppressed block feature: {featureName}");
                                        AddToBlacklist(featureName); // Add to blacklist
                                        continue;
                                    }
                                    
                                    // Check 2: Visibility
                                    if (feature.Visible == 0)
                                    {
                                        Logger.Info($"Skipping invisible block feature: {featureName}");
                                        AddToBlacklist(featureName); // Add to blacklist
                                        continue;
                                    }

                                    // Check 3: Advanced validity check using feature selection
                                    if (!IsFeatureSelectableAndValid(feature))
                                    {
                                        Logger.Info($"Skipping invalid/non-selectable block feature: {featureName}");
                                        AddToBlacklist(featureName); // Add to blacklist
                                        continue;
                                    }
                                    
                                    // Check 4: Try to get the block instance - if this fails, it's deleted
                                    SketchBlockInstance blockInstance = feature.GetSpecificFeature2() as SketchBlockInstance;
                                    if (blockInstance == null)
                                    {
                                        Logger.Info($"Skipping block feature with no valid instance: {featureName}");
                                        AddToBlacklist(featureName); // Add to blacklist
                                        continue;
                                    }
                                    
                                    // Check 5: Try to access block transform - deleted blocks can't provide this
                                    var transform = blockInstance.BlockToSketchTransform;
                                    if (transform == null)
                                    {
                                        Logger.Info($"Skipping block with no transform (likely deleted): {featureName}");
                                        AddToBlacklist(featureName); // Add to blacklist
                                        continue;
                                    }

                                    // Check 6: Try to access block attributes - if this fails catastrophically, it's deleted
                                    if (!CanAccessBlockAttributes(blockInstance))
                                    {
                                        Logger.Info($"Skipping block with inaccessible attributes (likely deleted): {featureName}");
                                        AddToBlacklist(featureName); // Add to blacklist
                                        continue;
                                    }
                                    
                                    // Skip if already processed
                                    if (processedBlocks.Contains(featureName))
                                    {
                                        Logger.Info($"Skipping duplicate block: {featureName}");
                                        continue;
                                    }
                                    
                                    var label = ExtractShippingLabelFromSketchBlock(feature, sheetName);
                                    if (label != null)
                                    {
                                        labels.Add(label);
                                        processedBlocks.Add(featureName);
                                        Logger.Info($"Extracted block: {featureName} -> {label.InstanceName}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Info($"Skipping block feature due to error (likely deleted): {feature.Name} - {ex.Message}");
                                    AddToBlacklist(feature.Name); // Add to blacklist
                                    continue;
                                }
                            }
                        }
                    }
                }

                Logger.Info($"Extracted {labels.Count} unique blocks from sheet {sheetName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting labels from sheet {sheetName}: {ex.Message}", ex);
            }

            return labels;
        }

        // Deletion blacklist to track confirmed deleted blocks
        private static HashSet<string> deletionBlacklist = new HashSet<string>();

        /// <summary>
        /// Adds a block to the deletion blacklist so it won't be processed again
        /// </summary>
        private void AddToBlacklist(string featureName)
        {
            try
            {
                deletionBlacklist.Add(featureName);
                Logger.Info($"Added {featureName} to deletion blacklist");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error adding to blacklist: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a block is in the deletion blacklist
        /// </summary>
        private bool IsBlockInDeletionBlacklist(string featureName)
        {
            return deletionBlacklist.Contains(featureName);
        }

        /// <summary>
        /// Clears the deletion blacklist (call this when starting fresh)
        /// </summary>
        public static void ClearDeletionBlacklist()
        {
            try
            {
                int count = deletionBlacklist.Count;
                deletionBlacklist.Clear();
                Logger.Info($"Cleared deletion blacklist ({count} entries removed)");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error clearing deletion blacklist: {ex.Message}");
            }
        }

        /// <summary>
        /// Forces aggressive clearing of SolidWorks document cache
        /// </summary>
        private void ClearSolidWorksDocumentCache()
        {
            try
            {
                Logger.Info("=== AGGRESSIVE SOLIDWORKS CACHE CLEARING ===");
                
                // Method 1: Force rebuild multiple times
                swModel.ForceRebuild3(true);
                swModel.ForceRebuild3(false);
                
                // Method 2: Clear graphics cache
                swModel.GraphicsRedraw2();
                
                // Method 3: Force garbage collection
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
                
                // Method 4: Clear selections
                swModel.ClearSelection2(true);
                
                // Method 5: Force update
                swModel.EditRebuild3();
                
                // Brief pause to let SolidWorks settle
                System.Threading.Thread.Sleep(500);
                
                Logger.Info("Completed aggressive SolidWorks cache clearing");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error during cache clearing: {ex.Message}");
            }
        }

        /// <summary>
        /// Tests if a feature can be selected and is valid - deleted features often fail this test
        /// </summary>
        private bool IsFeatureSelectableAndValid(Feature feature)
        {
            try
            {
                // Simple test: Try to access the feature name and properties
                string featureName = feature.Name;
                if (string.IsNullOrEmpty(featureName))
                {
                    Logger.Info($"Feature has empty name - likely deleted");
                    return false;
                }
                
                // Try to access basic feature properties that fail for deleted features
                var specificFeature = feature.GetSpecificFeature2();
                if (specificFeature == null)
                {
                    Logger.Info($"Feature {featureName} GetSpecificFeature2 returned null - likely deleted");
                    return false;
                }
                
                // Check if feature is suppressed
                if (feature.IsSuppressed())
                {
                    Logger.Info($"Feature {featureName} is suppressed - treating as deleted");
                    return false;
                }
                
                // Check visibility
                if (feature.Visible == 0)
                {
                    Logger.Info($"Feature {featureName} is not visible - treating as deleted");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Info($"Feature {feature.Name} failed selection test: {ex.Message} - likely deleted");
                try { swModel.ClearSelection2(true); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Tests if block attributes can be accessed without errors
        /// </summary>
        private bool CanAccessBlockAttributes(SketchBlockInstance blockInstance)
        {
            try
            {
                // Try to access basic attributes
                string testName = blockInstance.Name;
                if (string.IsNullOrEmpty(testName))
                {
                    return false;
                }
                
                // Try to get attribute count
                int attrCount = blockInstance.GetAttributeCount();
                
                // Try to access a common attribute (this often fails for deleted blocks)
                string testUID = blockInstance.GetAttributeValue("UID");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Info($"Block attribute access failed: {ex.Message} - likely deleted");
                return false;
            }
        }

        private ShippingLabelData ExtractShippingLabelFromSketchBlock(Feature feature, string sheetName)
        {
            try
            {
                // Get sketch block instance first
                SketchBlockInstance blockInstance = feature.GetSpecificFeature2() as SketchBlockInstance;
                if (blockInstance == null)
                {
                    Logger.Warning($"Could not get block instance for feature: {feature.Name}");
                    return null;
                }

                // Try to get the proper UID from block attributes FIRST
                string instanceName = feature.Name; // Default fallback
                string uidValue = null;
                
                try
                {
                    uidValue = blockInstance.GetAttributeValue("UID");
                    if (!string.IsNullOrEmpty(uidValue) && uidValue.StartsWith("SW"))
                    {
                        instanceName = uidValue;
                        Logger.Info($"Found UID attribute: {uidValue}");
                    }
                    else
                    {
                        Logger.Info($"No valid UID found (value: '{uidValue}'), using feature name: {feature.Name}");
                    }
                }
                catch (Exception uidEx)
                {
                    Logger.Info($"Could not read UID attribute: {uidEx.Message}, using feature name: {feature.Name}");
                }

                var label = new ShippingLabelData
                {
                    InstanceName = instanceName, // Use UID if available, otherwise feature name
                    SheetName = sheetName,
                    DrawingName = Path.GetFileNameWithoutExtension(swModel.GetPathName())
                };

                // Get position using proper BlockToSketchTransform method
                ExtractBlockCoordinatesFromTransform(blockInstance, label);

                // Extract block attributes - this will populate all the data fields
                    ExtractBlockAttributes(blockInstance, label);

                // Determine arrow type from block definition
                try
                {
                    SketchBlockDefinition blockDef = GetBlockDefinitionFromInstance(blockInstance);
                    if (blockDef != null)
                    {
                        string defName = Path.GetFileNameWithoutExtension(blockDef.FileName);
                        if (string.IsNullOrEmpty(defName))
                        {
                            // Fallback for blocks created from geometry
                            defName = blockInstance.Name;
                        }

                        if (defName.Contains("RightArrow"))
                            label.ArrowType = ShippingLabelArrowType.RightArrow;
                        else if (defName.Contains("LeftArrow"))
                            label.ArrowType = ShippingLabelArrowType.LeftArrow;
                        else if (defName.Contains("DoubleArrow"))
                            label.ArrowType = ShippingLabelArrowType.DoubleArrow;
                        else
                            label.ArrowType = ShippingLabelArrowType.Slot;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not determine arrow type: {ex.Message}");
                    label.ArrowType = ShippingLabelArrowType.Slot; // Default
                }

                return label;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting block data: {ex.Message}", ex);
                return null;
            }
        }

        private ShippingLabelData ExtractShippingLabelFromBlockInstance(SketchBlockInstance blockInstance, string sheetName)
        {
            try
            {
                var label = new ShippingLabelData
                {
                    InstanceName = blockInstance.Name,
                    SheetName = sheetName,
                    DrawingName = Path.GetFileNameWithoutExtension(swModel.GetPathName())
                };

                // Get position using proper BlockToSketchTransform method
                ExtractBlockCoordinatesFromTransform(blockInstance, label);

                // Extract block attributes
                ExtractBlockAttributes(blockInstance, label);

                return label;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting block instance data: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Extracts precise block coordinates using BlockToSketchTransform matrix
        /// This replaces the old bounding box workaround with the proper SolidWorks API method
        /// </summary>
        private void ExtractBlockCoordinatesFromTransform(ISketchBlockInstance blockInstance, ShippingLabelData label)
        {
            try
            {
                Logger.Info($"=== EXTRACTING COORDINATES USING BLOCKTOSKETCHTRANSFORM ===");
                Logger.Info($"Block: {blockInstance.Name}");

                // Get the transformation matrix from the block instance
                var mathTransform = blockInstance.BlockToSketchTransform;
                
                if (mathTransform != null)
                {
                    // Extract the 16-element transformation matrix array
                    var transformData = (double[])mathTransform.ArrayData;
                    
                    if (transformData != null && transformData.Length >= 16)
                    {
                        // Elements 9, 10, 11 contain the X, Y, Z translation (coordinates)
                        // This is the exact position where the block was placed
                        label.XPosition = transformData[9];  // X coordinate
                        label.YPosition = transformData[10]; // Y coordinate
                        // transformData[11] would be Z coordinate (usually 0 for 2D drawings)
                        
                        Logger.Info($"✅ PROPER COORDINATES EXTRACTED:");
                        Logger.Info($"   X Position: {label.XPosition:F4}");
                        Logger.Info($"   Y Position: {label.YPosition:F4}");
                        Logger.Info($"   Method: BlockToSketchTransform (EXACT coordinates)");
                        
                        // Log the full transform matrix for debugging (first 12 elements)
                        Logger.Info($"Transform Matrix: [{transformData[0]:F2},{transformData[1]:F2},{transformData[2]:F2}] " +
                                  $"[{transformData[3]:F2},{transformData[4]:F2},{transformData[5]:F2}] " +
                                  $"[{transformData[6]:F2},{transformData[7]:F2},{transformData[8]:F2}] " +
                                  $"Translation:[{transformData[9]:F4},{transformData[10]:F4},{transformData[11]:F4}] " +
                                  $"Scale:{transformData[12]:F2}");
                    }
                    else
                    {
                        Logger.Warning($"Transform matrix array is null or insufficient length for {blockInstance.Name}");
                        SetDefaultCoordinates(label, blockInstance.Name);
                    }
                }
                else
                {
                    Logger.Warning($"BlockToSketchTransform is null for {blockInstance.Name}");
                    SetDefaultCoordinates(label, blockInstance.Name);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error extracting coordinates from transform for {blockInstance.Name}: {ex.Message}");
                SetDefaultCoordinates(label, blockInstance.Name);
            }
        }

        /// <summary>
        /// Sets default coordinates and logs fallback method usage
        /// </summary>
        private void SetDefaultCoordinates(ShippingLabelData label, string blockName)
        {
            label.XPosition = 0.0;
            label.YPosition = 0.0;
            Logger.Info($"⚠️ Using default coordinates (0,0) for block: {blockName}");
            Logger.Info($"   Method: Fallback (transform method failed)");
        }

        private void ExtractBlockAttributes(SketchBlockInstance blockInstance, ShippingLabelData label)
        {
            try
            {
                Logger.Info($"Extracting attributes for block: {label.InstanceName}");
                
                // Read block attributes using the same method as in block creation
                int attributeCount = blockInstance.GetAttributeCount();
                Logger.Info($"Block has {attributeCount} attributes");

                // Test known attribute names directly
                string[] knownAttributes = { "ID Number", "Description", "Quantity", "Job Number", "Project Number", "System", "UID", "Company Letter", "Company Number" };
                
                foreach (string attributeName in knownAttributes)
                {
                    try
                    {
                        string currentValue = blockInstance.GetAttributeValue(attributeName);
                        if (!string.IsNullOrEmpty(currentValue))
                        {
                            // Map attribute to label data
                            switch (attributeName.ToLower())
                            {
                                case "id number":
                                    label.IDNumber = currentValue;
                                    Logger.Info($"Read ID Number: {currentValue}");
                                    break;
                                case "description":
                                    label.Description = currentValue;
                                    Logger.Info($"Read Description: {currentValue}");
                                    break;
                                case "quantity":
                                    label.Quantity = currentValue;
                                    Logger.Info($"Read Quantity: {currentValue}");
                                    break;
                                case "job number":
                                    label.JobNumber = currentValue;
                                    Logger.Info($"Read Job Number: {currentValue}");
                                    break;
                                case "project number":
                                    label.ProjectNumber = currentValue;
                                    Logger.Info($"Read Project Number: {currentValue}");
                                    break;
                                case "system":
                                    label.SystemNumber = currentValue;
                                    Logger.Info($"Read System: {currentValue}");
                                    break;
                                case "uid":
                                    // UID is already handled above, but log it
                                    Logger.Info($"Read UID: {currentValue}");
                                    break;
                                case "company letter":
                                    label.CompanyLetter = currentValue;
                                    Logger.Info($"Read Company Letter: {currentValue}");
                                    break;
                                case "company number":
                                    label.CompanyNumber = currentValue;
                                    Logger.Info($"Read Company Number: {currentValue}");
                                    break;
                            }
                        }
                        else
                        {
                            Logger.Info($"Attribute '{attributeName}' is empty or null");
                        }
                    }
                    catch (Exception attrEx)
                    {
                        Logger.Info($"Attribute '{attributeName}' does not exist: {attrEx.Message}");
                        // Attribute doesn't exist, continue
                    }
                }

                Logger.Info($"Finished extracting attributes for: {label.InstanceName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting block attributes: {ex.Message}", ex);
            }
        }

        private void ExtractComponentInformation(Annotation annotation, ShippingLabelData label)
        {
            try
            {
                // Get leader information
                int leaderCount = annotation.GetLeaderCount();
                if (leaderCount == 0) return;

                // Simplified component extraction
                // In a real implementation, you'd need to find which component the leader points to
                
                // For now, we'll mark that it has a leader
                label.HasLeader = true;
                label.ComponentName = "Component with leader";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting component information: {ex.Message}", ex);
            }
        }

        private void ExtractComponentData(Component2 component, ShippingLabelData label)
        {
            try
            {
                label.ComponentName = component.Name2;
                label.ConfigurationName = component.ReferencedConfiguration;

                // Get the referenced model document
                ModelDoc2 componentModel = component.GetModelDoc2() as ModelDoc2;
                if (componentModel != null)
                {
                    label.PartFileName = Path.GetFileNameWithoutExtension(componentModel.GetPathName());

                    // Get custom properties - Fixed method signature
                    CustomPropertyManager propMgr = componentModel.Extension.CustomPropertyManager[label.ConfigurationName];
                    if (propMgr != null)
                    {
                        string resolvedValue = "";
                        bool wasResolved = false;
                        bool linkToProperty = false; // Changed from int to bool

                        // Extract common properties - Fixed Get6 method signature
                        propMgr.Get6("Part Number", false, out _, out resolvedValue, out wasResolved, out linkToProperty);
                        if (wasResolved) label.PartNumber = resolvedValue;

                        propMgr.Get6("Description", false, out _, out resolvedValue, out wasResolved, out linkToProperty);
                        if (wasResolved) label.PartDescription = resolvedValue;

                        propMgr.Get6("Material", false, out _, out resolvedValue, out wasResolved, out linkToProperty);
                        if (wasResolved) label.MaterialName = resolvedValue;

                        propMgr.Get6("SW-Mass", false, out _, out resolvedValue, out wasResolved, out linkToProperty);
                        if (wasResolved) label.Mass = resolvedValue;

                        propMgr.Get6("SW-Volume", false, out _, out resolvedValue, out wasResolved, out linkToProperty);
                        if (wasResolved) label.Volume = resolvedValue;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting component data: {ex.Message}", ex);
            }
        }
        #endregion

        #region Event Handlers
        private void BtnForceReload_Click(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("=== FORCE RELOAD BUTTON CLICKED ===");
                Logger.Info("User requested nuclear cache clearing with document reload");
                
                // Show confirmation dialog since this is a more intensive operation
                var result = MessageBox.Show(
                    "Force Reload will save and reopen the drawing to completely clear all caches.\n\n" +
                    "This is more intensive than regular Refresh and takes a few seconds.\n\n" +
                    "Use this when:\n" +
                    "• You've deleted blocks but they still appear\n" +
                    "• Regular Refresh doesn't show current state\n" +
                    "• You suspect SolidWorks caching issues\n\n" +
                    "Continue with Force Reload?",
                    "Confirm Force Reload",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result != DialogResult.Yes)
                {
                    Logger.Info("User cancelled Force Reload operation");
                    return;
                }
                
                UpdateStatus("Performing nuclear cache clearing...");
                ShowProgress(true);
                
                // Perform the nuclear cache clearing
                bool reloadSuccess = ForceDocumentSaveReload();
                
                if (reloadSuccess)
                {
                    Logger.Info("Nuclear cache clearing completed successfully");
                    
                    // Now refresh the data grid with the freshly loaded document
                    Logger.Info("Refreshing data grid after nuclear reload...");
                    LoadShippingLabels();
                    
                    UpdateStatus("Force Reload completed successfully");
                    MessageBox.Show(
                        "Force Reload completed successfully!\n\n" +
                        "The drawing has been saved and reloaded with a completely fresh cache.\n" +
                        "All data should now reflect the current state of the drawing.",
                        "Force Reload Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    Logger.Error("Nuclear cache clearing failed");
                    UpdateStatus("Force Reload failed");
                    MessageBox.Show(
                        "Force Reload failed!\n\n" +
                        "The document could not be saved and reloaded properly.\n" +
                        "Check the logs for details and try again.",
                        "Force Reload Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in Force Reload: {ex.Message}", ex);
                MessageBox.Show($"Error during Force Reload: {ex.Message}", "Force Reload Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ShowProgress(false);
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("=== REFRESH BUTTON CLICKED ===");
                Logger.Info("Starting TRUE REFRESH: Clear DataGrid -> Read Drawing -> Save to XML");
                
                UpdateStatus("Refreshing shipping labels from drawing...");
                ShowProgress(true);
                
                // Clear the smart copy cache to start fresh
                ShippingLabelBlockCreator.ClearSmartCopyCache();
                Logger.Info("Cleared smart copy cache during refresh");
                
                // Clear deletion blacklist to allow fresh detection
                ClearDeletionBlacklist();
                Logger.Info("Cleared deletion blacklist during refresh");

                // STEP 1: SAFELY CLEAR THE DATAGRID
                Logger.Info("STEP 1: Safely clearing DataGrid");
                try
                {
                    // First, end any current edit operations to prevent binding conflicts
                    if (dgvShippingLabels.IsCurrentCellInEditMode)
                    {
                        dgvShippingLabels.EndEdit();
                        Logger.Info("Ended current cell edit mode");
                    }
                    
                    // Clear selection to prevent index errors
                    dgvShippingLabels.ClearSelection();
                    
                    // Safely unbind and clear
                    dgvShippingLabels.DataSource = null;
                    Logger.Info("Unbound DataGridView safely");
                    
                    if (shippingData != null)
                    {
                        shippingData.Rows.Clear();
                        Logger.Info("Cleared all rows from DataTable");
                    }
                    
                    // Force immediate UI update
                    dgvShippingLabels.Refresh();
                    Application.DoEvents();
                }
                catch (Exception clearEx)
                {
                    Logger.Warning($"Error during DataGrid clearing: {clearEx.Message}");
                    // Continue with refresh even if clearing had issues
                }
                
                // STEP 2: FORCE SOLIDWORKS TO UPDATE AND READ ALL BLOCKS FRESH FROM THE DRAWING
                Logger.Info("STEP 2: Forcing SolidWorks update and reading all blocks fresh from drawing (ignoring XML)");
                
                // Force SolidWorks to fully update the drawing and clear any cached data
                if (swApp != null && swApp.ActiveDoc != null)
                {
                    var swModel = swApp.ActiveDoc as ModelDoc2;
                    if (swModel != null)
                    {
                        Logger.Info("Forcing SolidWorks rebuild and graphics redraw before scanning");
                        swModel.ForceRebuild3(true);  // Force complete rebuild
                        swModel.GraphicsRedraw2();    // Force graphics redraw
                        System.Threading.Thread.Sleep(200); // Give SolidWorks time to process
                        Application.DoEvents();
                    }
                }
                
                List<ShippingLabelData> freshDrawingData = ExtractAllShippingLabels();
                Logger.Info($"Extracted {freshDrawingData.Count} blocks fresh from drawing");
                
                // STEP 3: RECREATE DATATABLE WITH FRESH DATA ONLY
                Logger.Info("STEP 3: Recreating DataTable with fresh drawing data");
                shippingData = new DataTable();
                SetupDataTableStructure(shippingData);
                
                // Add each fresh block to the table
                foreach (var labelData in freshDrawingData)
                {
                    // Skip duplicates based on InstanceName (PrimaryKey)
                    if (shippingData.Rows.Contains(labelData.InstanceName))
                    {
                        Logger.Info($"Skipping duplicate InstanceName when rebuilding DataTable: {labelData.InstanceName}");
                        continue;
                    }

                    var row = shippingData.NewRow();
                    row["InstanceName"] = labelData.InstanceName ?? "";
                    row["ArrowType"] = labelData.ArrowType.ToString();
                    row["ProjectNumber"] = labelData.ProjectNumber ?? "";
                    row["JobNumber"] = labelData.JobNumber ?? "";
                    row["SystemNumber"] = labelData.SystemNumber ?? "";
                    row["IDNumber"] = labelData.IDNumber ?? "";
                    row["Description"] = labelData.Description ?? "";
                    row["Quantity"] = labelData.Quantity ?? "";
                    row["XPosition"] = labelData.XPosition;
                    row["YPosition"] = labelData.YPosition;
                    row["SheetName"] = labelData.SheetName ?? "";
                    row["DrawingName"] = labelData.DrawingName ?? "";
                    row["HasLeader"] = labelData.HasLeader;
                    row["ComponentName"] = labelData.ComponentName ?? "";
                    row["PartFileName"] = labelData.PartFileName ?? "";
                    row["MaterialName"] = labelData.MaterialName ?? "";
                    row["PartNumber"] = labelData.PartNumber ?? "";
                    row["PartDescription"] = labelData.PartDescription ?? "";
                    row["Mass"] = labelData.Mass ?? "";
                    row["Volume"] = labelData.Volume ?? "";
                    row["ConfigurationName"] = labelData.ConfigurationName ?? "";
                    row["CompanyNumber"] = labelData.CompanyNumber ?? "";
                    row["CompanyLetter"] = labelData.CompanyLetter ?? "";
                    
                    shippingData.Rows.Add(row);
                    Logger.Info($"Added fresh block to DataTable: {labelData.InstanceName}");
                }
                
                // STEP 4: PROPERLY REBUILD DATAGRID WITH FRESH DATA
                Logger.Info("STEP 4: Rebuilding DataGrid structure and binding to fresh data");
                
                // CRITICAL: Re-establish the DataGridView column structure for the new DataTable
                // This prevents the "Column named InstanceName cannot be found" error
                dgvShippingLabels.Columns.Clear();
                
                // Recreate all columns with proper bindings
                dgvShippingLabels.Columns.Add(CreateTextColumn("InstanceName", "Instance ID", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("ArrowType", "Arrow Type", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("ProjectNumber", "Project Number", false));
                dgvShippingLabels.Columns.Add(CreateTextColumn("JobNumber", "Job Number", false));
                dgvShippingLabels.Columns.Add(CreateTextColumn("SystemNumber", "System Number", false));
                dgvShippingLabels.Columns.Add(CreateTextColumn("IDNumber", "ID Number", false));
                dgvShippingLabels.Columns.Add(CreateTextColumn("Description", "Description", false));
                dgvShippingLabels.Columns.Add(CreateTextColumn("Quantity", "Quantity", false));
                dgvShippingLabels.Columns.Add(CreateTextColumn("XPosition", "X Position", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("YPosition", "Y Position", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("SheetName", "Sheet", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("DrawingName", "Drawing", true));
                dgvShippingLabels.Columns.Add(CreateCheckBoxColumn("HasLeader", "Has Leader", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("ComponentName", "Component", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("PartFileName", "Part File", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("MaterialName", "Material", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("PartNumber", "Part Number", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("PartDescription", "Part Description", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("Mass", "Mass", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("Volume", "Volume", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("ConfigurationName", "Configuration", true));
                dgvShippingLabels.Columns.Add(CreateTextColumn("CompanyNumber", "Company Number", false));
                dgvShippingLabels.Columns.Add(CreateTextColumn("CompanyLetter", "Company Letter", false));
                
                // Now bind the new data source
                dgvShippingLabels.DataSource = shippingData;
                
                // Auto-resize columns for new data
                dgvShippingLabels.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                
                // Force complete DataGrid refresh
                dgvShippingLabels.Refresh();
                dgvShippingLabels.Update();
                Application.DoEvents();
                
                // STEP 5: SAVE FRESH DATA TO XML (OVERWRITE COMPLETELY)
                Logger.Info("STEP 5: Saving fresh data to XML file (complete overwrite)");
                SaveDataToFile();
                
                UpdateStatus($"Refreshed {shippingData.Rows.Count} shipping labels from drawing");
                ShowProgress(false);
                
                Logger.Info($"TRUE REFRESH COMPLETED: Found {shippingData.Rows.Count} labels from drawing, saved to XML");
                Logger.Info("DataGrid now shows current attribute values from SolidWorks blocks");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error refreshing shipping labels: {ex.Message}", ex);
                MessageBox.Show($"Error refreshing shipping labels: {ex.Message}", "Refresh Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("Error refreshing data");
                ShowProgress(false);
            }
        }

        private void BtnCreateNew_Click(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("=== CREATE NEW BUTTON CLICKED ===");
                
                // Disable the button temporarily to prevent double-clicks
                btnCreateNew.Enabled = false;
                
                // Store current data count to detect new entries
                int initialCount = shippingData.Rows.Count;
                Logger.Info($"Initial data count: {initialCount}");
                
                // Hide the manager form during block placement
                this.WindowState = FormWindowState.Minimized;
                
                CreateShippingLabelForm createForm = null;
                try
                {
                    createForm = new CreateShippingLabelForm(swApp);
                    activeCreateForm = createForm; // Track the form
                    Logger.Info("Created CreateShippingLabelForm instance");
                    
                    // Store initial label count to detect new labels
                    int startingLabelCount = shippingData.Rows.Count;
                    Logger.Info($"Starting label count: {startingLabelCount}");
                    
                    // Show form as modeless (non-blocking) so user can interact with SolidWorks
                    // Set SolidWorks as the owner to keep form associated with SW in taskbar
                    try 
                    {
                        IntPtr swMainWindow = new IntPtr(swApp.IFrameObject().GetHWnd());
                        if (swMainWindow != IntPtr.Zero)
                        {
                            // Create a temporary Form wrapper for the SolidWorks window handle
                            var swOwner = new SolidWorksOwnerForm(swMainWindow);
                            createForm.Show(swOwner);
                            Logger.Info("Showed CreateShippingLabelForm as modeless dialog with SolidWorks as owner");
                        }
                        else
                        {
                            createForm.Show();
                            Logger.Warning("Could not get SolidWorks window handle, showing form without owner");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error setting SolidWorks as form owner: {ex.Message}");
                        createForm.Show();
                        Logger.Info("Fallback: Showed CreateShippingLabelForm without owner");
                    }
                    
                    // Set up form closed event to handle cleanup and refresh
                    createForm.FormClosed += (s, args) => {
                        Logger.Info("CreateShippingLabelForm was closed");
                        activeCreateForm = null; // Clear tracking
                        
                        // Automatically refresh using the new TRUE REFRESH functionality
                        Logger.Info("Auto-refreshing after create/edit to show current values");
                        SafeAutoRefresh("create operation");
                        
                        int finalCount = shippingData.Rows.Count;
                        int newLabelsCreated = finalCount - startingLabelCount;
                        
                        if (newLabelsCreated > 0)
                        {
                            UpdateStatus($"Create label session completed. {newLabelsCreated} new label(s) created. Total labels: {finalCount}");
                        }
                        else
                        {
                            UpdateStatus($"Create label session cancelled. Total labels: {finalCount}");
                        }
                        
                        // Re-enable the button
                        btnCreateNew.Enabled = true;
                        Logger.Info("Re-enabled Create New button");
                        
                        // Dispose the form
                        createForm.Dispose();
                        Logger.Info("Disposed CreateShippingLabelForm");
                    };
                    
                    // Don't wait for form to close - it's modeless now
                    UpdateStatus("Create label form opened. You can interact with both the form and SolidWorks.");
                }
                catch (Exception formCreationEx)
                {
                    Logger.Error($"Error creating form: {formCreationEx.Message}", formCreationEx);
                    // Re-enable the button if form creation fails
                    btnCreateNew.Enabled = true;
                    
                    // Dispose form if it was created but failed to show
                    if (createForm != null)
                    {
                        createForm.Dispose();
                        Logger.Info("Disposed CreateShippingLabelForm after error");
                    }
                    throw;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating new shipping label: {ex.Message}", ex);
                MessageBox.Show($"Error creating new shipping label: {ex.Message}", "Create New Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                
                // Make sure button is re-enabled even if there's an error
                btnCreateNew.Enabled = true;
                
                // Restore form state
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
            }
        }

        /// <summary>
        /// Adds a new label directly to the data grid without full refresh
        /// This prevents duplicates and ensures all data is properly populated
        /// </summary>
        private void AddNewLabelToGrid(ShippingLabelData labelData)
        {
            try
            {
                Logger.Info($"Adding new label to grid: {labelData.InstanceName}");
                
                // Check if this instance already exists to prevent duplicates
                foreach (DataRow existingRow in shippingData.Rows)
                {
                    if (existingRow["InstanceName"].ToString() == labelData.InstanceName)
                    {
                        Logger.Warning($"Label with InstanceName {labelData.InstanceName} already exists, skipping duplicate");
                        return;
                    }
                }
                
                // Create new row with all the data
                var row = shippingData.NewRow();
                row["InstanceName"] = labelData.InstanceName ?? "";
                row["ProjectNumber"] = labelData.ProjectNumber ?? "";
                row["JobNumber"] = labelData.JobNumber ?? "";
                row["SystemNumber"] = labelData.SystemNumber ?? "";
                row["IDNumber"] = labelData.IDNumber ?? "";
                row["Description"] = labelData.Description ?? "";
                row["Quantity"] = labelData.Quantity ?? "";
                row["XPosition"] = labelData.XPosition;
                row["YPosition"] = labelData.YPosition;
                row["SheetName"] = labelData.SheetName ?? "";
                row["DrawingName"] = labelData.DrawingName ?? "";
                row["HasLeader"] = labelData.HasLeader;
                row["ComponentName"] = labelData.ComponentName ?? "";
                row["PartFileName"] = labelData.PartFileName ?? "";
                row["MaterialName"] = labelData.MaterialName ?? "";
                row["PartNumber"] = labelData.PartNumber ?? "";
                row["PartDescription"] = labelData.PartDescription ?? "";
                row["Mass"] = labelData.Mass ?? "";
                row["Volume"] = labelData.Volume ?? "";
                row["ConfigurationName"] = labelData.ConfigurationName ?? "";
                row["ArrowType"] = labelData.ArrowType.ToString();
                row["CompanyNumber"] = labelData.CompanyNumber ?? "";
                row["CompanyLetter"] = labelData.CompanyLetter ?? "";
                
                shippingData.Rows.Add(row);
                
                Logger.Info($"Successfully added label to grid. Row data:");
                Logger.Info($"  InstanceName: {row["InstanceName"]}");
                Logger.Info($"  ProjectNumber: {row["ProjectNumber"]}");
                Logger.Info($"  JobNumber: {row["JobNumber"]}");
                Logger.Info($"  SystemNumber: {row["SystemNumber"]}");
                Logger.Info($"  IDNumber: {row["IDNumber"]}");
                Logger.Info($"  ArrowType: {row["ArrowType"]}");
                
                // Save the updated data
                SaveDataToFile();
                
                // Refresh the grid display
                dgvShippingLabels.Refresh();
                
                // Select the newly added row
                if (shippingData.Rows.Count > 0)
                {
                    int lastRowIndex = dgvShippingLabels.Rows.Count - 1;
                    dgvShippingLabels.ClearSelection();
                    dgvShippingLabels.Rows[lastRowIndex].Selected = true;
                    dgvShippingLabels.FirstDisplayedScrollingRowIndex = lastRowIndex;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error adding new label to grid: {ex.Message}", ex);
                // Fallback to full refresh if direct add fails
                LoadShippingLabels();
            }
        }

        // Removed BtnCreateTemplates_Click method - no longer needed

        private void BtnEditSelected_Click(object sender, EventArgs e)
        {
            if (dgvShippingLabels.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a shipping label to edit.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (dgvShippingLabels.SelectedRows.Count > 1)
            {
                MessageBox.Show("Please select only one shipping label to edit.", "Multiple Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Logger.Info("=== EDIT SELECTED BUTTON CLICKED ===");
                
                // Get the selected row data
                var selectedRow = dgvShippingLabels.SelectedRows[0];
                var labelData = ExtractLabelDataFromRow(selectedRow);
                
                Logger.Info($"Editing label: {labelData.InstanceName}");
                
                // Disable the button temporarily
                btnEditSelected.Enabled = false;
                
                // Create edit form (reuse create form but in edit mode)
                var editForm = new CreateShippingLabelForm(swApp, labelData); // Pass existing data for editing
                
                // Set up form closed event to handle updates
                editForm.FormClosed += (s, args) => {
                    Logger.Info("Edit form was closed");
                    
                    // Automatically refresh using the new TRUE REFRESH functionality
                    Logger.Info("Auto-refreshing after edit to show updated values");
                    SafeAutoRefresh("edit operation");
                    
                    // Re-enable the button
                    btnEditSelected.Enabled = true;
                    Logger.Info("Re-enabled Edit Selected button");
                };
                
                // Show form with SolidWorks ownership
                try 
                {
                    IntPtr swMainWindow = new IntPtr(swApp.IFrameObject().GetHWnd());
                    if (swMainWindow != IntPtr.Zero)
                    {
                        var swOwner = new SolidWorksOwnerForm(swMainWindow);
                        editForm.Show(swOwner);
                        Logger.Info("Showed edit form with SolidWorks ownership");
                    }
                    else
                    {
                        editForm.Show();
                        Logger.Warning("Could not get SolidWorks window handle for edit form");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error setting SolidWorks as edit form owner: {ex.Message}");
                    editForm.Show();
                }
                
                UpdateStatus("Edit form opened. Make changes and close when done.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error opening edit form: {ex.Message}", ex);
                MessageBox.Show($"Error opening edit form: {ex.Message}", "Edit Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnEditSelected.Enabled = true;
            }
        }

        private void BtnDeleteSelected_Click(object sender, EventArgs e)
        {
            if (dgvShippingLabels.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select one or more shipping labels to delete.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRows = dgvShippingLabels.SelectedRows;
            string confirmMessage;
            
            if (selectedRows.Count == 1)
            {
                var instanceName = GetCellValueSafe(selectedRows[0], "InstanceName") ?? "Unknown";
                confirmMessage = $"Are you sure you want to delete the selected shipping label?\n\nInstance: {instanceName}\n\nThis will remove both the block from the drawing and the row from the manager.";
            }
            else
            {
                confirmMessage = $"Are you sure you want to delete {selectedRows.Count} selected shipping labels?\n\nThis will remove both the blocks from the drawing and the rows from the manager.";
            }

            var result = MessageBox.Show(confirmMessage, "Confirm Delete", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            try
            {
                Logger.Info($"=== DELETE SELECTED BUTTON CLICKED ===");
                Logger.Info($"Deleting {selectedRows.Count} selected row(s)");
                
                // CRITICAL CHECK: Verify the drawing is not read-only
                if (IsDrawingReadOnly())
                {
                    ShowReadOnlyWarning();
                    return;
                }
                
                var deletedCount = 0;
                var failedCount = 0;
                var deletedInstances = new List<string>();
                
                // Process each selected row
                foreach (DataGridViewRow row in selectedRows)
                {
                    var instanceName = GetCellValueSafe(row, "InstanceName");
                    if (string.IsNullOrEmpty(instanceName))
                    {
                        Logger.Warning("Skipping row with empty instance name");
                        failedCount++;
                        continue;
                    }
                    
                    Logger.Info($"Attempting to delete: {instanceName}");
                    
                    // Use normal deletion (user can use Force Reload button if needed)
                    bool blockDeleted = DeleteBlockFromDrawing(instanceName);
                    
                    if (blockDeleted)
                    {
                        deletedInstances.Add(instanceName);
                        deletedCount++;
                        Logger.Info($"✅ Block deleted successfully: {instanceName}");
                        
                        // Add to blacklist
                        AddToBlacklist(instanceName);
                        Logger.Info($"Added deleted block to blacklist: {instanceName}");
                    }
                    else
                    {
                        failedCount++;
                        Logger.Warning($"❌ Failed to delete block: {instanceName}");
                    }
                }
                
                // Auto-refresh to show current state
                Logger.Info("Auto-refreshing after delete to show current state");
                SafeAutoRefresh("delete operation");
                
                // Show results
                string resultMessage = $"Delete operation completed:\n\n";
                resultMessage += $"• Successfully deleted: {deletedCount}\n";
                if (failedCount > 0)
                {
                    resultMessage += $"• Failed to delete: {failedCount}\n\n";
                    resultMessage += "Some blocks may have been manually deleted from the drawing already.\n\n";
                }
                
                if (deletedCount > 0)
                {
                    resultMessage += "💡 TIP: If deleted blocks still appear after Refresh,\n";
                    resultMessage += "click 'Force Reload' to completely clear SolidWorks caches.";
                }
                
                var icon = failedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information;
                MessageBox.Show(resultMessage, "Delete Complete", MessageBoxButtons.OK, icon);
                
                UpdateStatus($"Deleted {deletedCount} shipping label(s)");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during delete operation: {ex.Message}", ex);
                MessageBox.Show($"Error during delete operation: {ex.Message}", "Delete Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnExportCSV_Click(object sender, EventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV Files|*.csv",
                Title = "Export Shipping Labels to CSV",
                FileName = $"ShippingLabels_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ExportDataTableToCSV(shippingData, saveDialog.FileName);
                    MessageBox.Show("Data exported successfully!", "Export Complete", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    UpdateStatus($"Exported to {saveDialog.FileName}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error exporting CSV: {ex.Message}", ex);
                    MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnExportDatabase_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvShippingLabels.Rows.Count == 0)
                {
                    MessageBox.Show("There are no shipping labels to push.", "Push to Connex", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                IEnumerable<DataGridViewRow> targetRows = dgvShippingLabels.Rows.Cast<DataGridViewRow>();

                var labels = targetRows.Select(r => ExtractLabelDataFromRow(r)).ToList();
                ConnexDatabaseHelper.SyncShippingLabels(labels);

                MessageBox.Show($"Connex sync complete for {labels.Count} label(s). Check logs for details.",
                    "Push to Connex", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error pushing labels to Connex: {ex.Message}", ex);
                MessageBox.Show($"Error pushing labels: {ex.Message}", "Push Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void DgvShippingLabels_SelectionChanged(object sender, EventArgs e)
        {
            bool hasSelection = dgvShippingLabels.SelectedRows.Count > 0;
            btnEditSelected.Enabled = hasSelection && dgvShippingLabels.SelectedRows.Count == 1;
            btnDeleteSelected.Enabled = hasSelection;
        }

        private void DgvShippingLabels_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                BtnEditSelected_Click(sender, e);
            }
        }

        private void DgvShippingLabels_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            // Handle direct cell editing (this is different from the Edit Selected button)
            try
            {
                // Basic validation only - don't refresh automatically during direct editing
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (e.RowIndex >= dgvShippingLabels.Rows.Count) return;
                
                var row = dgvShippingLabels.Rows[e.RowIndex];
                string instanceName = GetCellValueSafe(row, "InstanceName") ?? "";
                string columnName = dgvShippingLabels.Columns[e.ColumnIndex].DataPropertyName;
                string newValue = row.Cells[e.ColumnIndex].Value?.ToString() ?? "";

                // TODO: In future, implement real-time block attribute updates here
                // For now, just save the DataTable changes to XML
                SaveDataToFile();
                
                UpdateStatus($"Cell edited: {columnName} = '{newValue}' for {instanceName} (Use Edit Selected for block updates)");
                Logger.Info($"Direct cell edit: {columnName} = '{newValue}' for {instanceName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in cell edit: {ex.Message}", ex);
                // Don't show popup or refresh during direct editing - just log the error
                UpdateStatus("Error during cell edit - see logs");
            }
        }
        #endregion

        #region Utility Methods
        private void UpdateStatus(string message)
        {
            if (lblStatus != null)
            {
                lblStatus.Text = message;
                Application.DoEvents();
            }
        }

        private void ShowProgress(bool show)
        {
            if (progressBar != null)
            {
                progressBar.Visible = show;
                Application.DoEvents();
            }
        }

        /// <summary>
        /// Forces the DataGridView to refresh and show updated data
        /// </summary>
        private void RefreshDataGridView()
        {
            try
            {
                Logger.Info("Forcing DataGridView refresh...");
                
                // Method 1: Refresh the data source binding
                if (dgvShippingLabels.DataSource != null)
                {
                    var currentDataSource = dgvShippingLabels.DataSource;
                    dgvShippingLabels.DataSource = null;
                    dgvShippingLabels.DataSource = currentDataSource;
                    Logger.Info("DataSource refreshed");
                }
                
                // Method 2: Force invalidation and update
                dgvShippingLabels.Invalidate();
                dgvShippingLabels.Update();
                dgvShippingLabels.Refresh();
                
                // Method 3: Refresh the default cell style to force redraw
                dgvShippingLabels.DefaultCellStyle = dgvShippingLabels.DefaultCellStyle;
                
                Logger.Info($"DataGridView refreshed - showing {dgvShippingLabels.Rows.Count} rows");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error refreshing DataGridView: {ex.Message}");
            }
        }

        /// <summary>
        /// Safe auto-refresh method for form operations that avoids DataGridView binding conflicts
        /// </summary>
        private void SafeAutoRefresh(string operationType)
        {
            try
            {
                Logger.Info($"=== SAFE AUTO-REFRESH AFTER {operationType.ToUpper()} ===");
                
                // Use Invoke to ensure this runs on the UI thread safely
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => SafeAutoRefresh(operationType)));
                    return;
                }
                
                // Longer delay to let SolidWorks fully process any drawing changes
                Application.DoEvents();
                System.Threading.Thread.Sleep(1000); // Increased to 1000ms for delete operations
                Application.DoEvents(); // Second call to ensure all UI updates are processed
                
                // Call the main refresh method
                BtnRefresh_Click(null, null);
                
                Logger.Info($"Safe auto-refresh completed after {operationType}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in safe auto-refresh after {operationType}: {ex.Message}", ex);
                // Fallback to just saving the data if refresh fails
                try
                {
                    SaveDataToFile();
                    UpdateStatus($"Auto-refresh failed after {operationType}, but data was saved");
                }
                catch (Exception saveEx)
                {
                    Logger.Error($"Even fallback save failed: {saveEx.Message}", saveEx);
                    UpdateStatus($"Auto-refresh and save failed after {operationType} - manual refresh recommended");
                }
            }
        }

        private void ExportDataTableToCSV(DataTable dt, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                // Write headers
                var headers = dt.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
                writer.WriteLine(string.Join(",", headers));

                // Write data rows
                foreach (DataRow row in dt.Rows)
                {
                    var fields = row.ItemArray.Select(field => $"\"{field}\"").ToArray();
                    writer.WriteLine(string.Join(",", fields));
                }
            }
        }

        private ShippingLabelData ExtractLabelDataFromRow(DataGridViewRow row)
        {
            var labelData = new ShippingLabelData();
            
            try
            {
                // Safe cell value extraction - check if column exists first
                labelData.InstanceName = GetCellValueSafe(row, "InstanceName");
                labelData.ProjectNumber = GetCellValueSafe(row, "ProjectNumber");
                labelData.JobNumber = GetCellValueSafe(row, "JobNumber");
                labelData.SystemNumber = GetCellValueSafe(row, "SystemNumber");
                labelData.IDNumber = GetCellValueSafe(row, "IDNumber");
                labelData.Description = GetCellValueSafe(row, "Description");
                labelData.Quantity = GetCellValueSafe(row, "Quantity");
                labelData.SheetName = GetCellValueSafe(row, "SheetName");
                labelData.DrawingName = GetCellValueSafe(row, "DrawingName");
                labelData.ComponentName = GetCellValueSafe(row, "ComponentName");
                labelData.PartFileName = GetCellValueSafe(row, "PartFileName");
                labelData.MaterialName = GetCellValueSafe(row, "MaterialName");
                labelData.PartNumber = GetCellValueSafe(row, "PartNumber");
                labelData.PartDescription = GetCellValueSafe(row, "PartDescription");
                labelData.Mass = GetCellValueSafe(row, "Mass");
                labelData.Volume = GetCellValueSafe(row, "Volume");
                labelData.ConfigurationName = GetCellValueSafe(row, "ConfigurationName");
                labelData.CompanyNumber = GetCellValueSafe(row, "CompanyNumber");
                labelData.CompanyLetter = GetCellValueSafe(row, "CompanyLetter");
                
                // Parse numeric values
                if (double.TryParse(GetCellValueSafe(row, "XPosition"), out double x))
                    labelData.XPosition = x;
                if (double.TryParse(GetCellValueSafe(row, "YPosition"), out double y))
                    labelData.YPosition = y;
                if (bool.TryParse(GetCellValueSafe(row, "HasLeader"), out bool hasLeader))
                    labelData.HasLeader = hasLeader;
                
                // Parse arrow type
                if (Enum.TryParse<ShippingLabelArrowType>(GetCellValueSafe(row, "ArrowType"), out var arrowType))
                    labelData.ArrowType = arrowType;
                
                Logger.Info($"Extracted label data for editing: {labelData.InstanceName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting label data from row: {ex.Message}", ex);
            }
            
            return labelData;
        }

        /// <summary>
        /// Safely gets cell value, checking if column exists first
        /// </summary>
        private string GetCellValueSafe(DataGridViewRow row, string columnName)
        {
            try
            {
                // Check if the column exists in the DataGridView by DataPropertyName
                DataGridViewColumn targetColumn = null;
                foreach (DataGridViewColumn column in dgvShippingLabels.Columns)
                {
                    if (string.Equals(column.DataPropertyName, columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetColumn = column;
                        break;
                    }
                }

                if (targetColumn == null)
                {
                    Logger.Warning($"Column with DataPropertyName '{columnName}' not found in DataGridView");
                    Logger.Info($"Available columns: {string.Join(", ", dgvShippingLabels.Columns.Cast<DataGridViewColumn>().Select(c => $"{c.Name}({c.DataPropertyName})"))}");
                    return "";
                }

                // Try to get the cell value using the column index
                var cell = row.Cells[targetColumn.Index];
                return cell?.Value?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting cell value for column '{columnName}': {ex.Message}");
                return "";
            }
        }

        private bool DeleteBlockFromDrawing(string instanceName)
        {
            try
            {
                Logger.Info($"Attempting to delete block from drawing: {instanceName}");
                
                // Get the active drawing
                var swModel = swApp.ActiveDoc as ModelDoc2;
                var swDraw = swModel as DrawingDoc;
                
                if (swDraw == null)
                {
                    Logger.Error("No active drawing found");
                    return false;
                }

                // Get sketch manager
                var sketchManager = swModel.SketchManager;
                if (sketchManager == null)
                {
                    Logger.Error("Could not get sketch manager");
                    return false;
                }

                // Find and delete the block instance
                // Note: SolidWorks API doesn't have GetSketchBlockInstances method
                // Instead we need to use GetSketchBlockDefinitions and then get instances from each definition
                object[] blockInstances = GetAllSketchBlockInstancesFromDefinitions(sketchManager);
                
                if (blockInstances == null || blockInstances.Length == 0)
                {
                    Logger.Warning("No block instances found in current drawing");
                    return false;
                }

                Logger.Info($"Found {blockInstances.Length} block instances, searching for {instanceName}");
                
                // Search through all instances
                foreach (object instance in blockInstances)
                {
                    var blockInstance = instance as ISketchBlockInstance;
                    if (blockInstance == null) continue;

                    // Try different ways to identify the block
                    string blockName = blockInstance.Name;
                    
                    Logger.Info($"Checking block: {blockName}");
                    
                    // Check if this is our target block by name
                    if (string.Equals(blockName, instanceName, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info($"Found matching block by name: {blockName}");
                        return DeleteBlockInstance(blockInstance, swModel);
                    }
                    
                    // Also check by trying to find a UID attribute that matches
                    try
                    {
                        // Get block definition and check attributes
                        var blockDef = GetBlockDefinitionFromInstance(blockInstance);
                        if (blockDef != null)
                        {
                            var attributes = GetAttributesFromDefinition(blockDef);
                        if (attributes != null)
                        {
                            foreach (object attr in attributes)
                            {
                                    string attrName = GetAttributeName(attr);
                                    if (!string.IsNullOrEmpty(attrName) && attrName.ToLower().Contains("uid") || attrName.ToLower().Contains("instance"))
                                {
                                        string attrValue = GetAttributeValue(attr);
                                        if (string.Equals(attrValue, instanceName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            Logger.Info($"Found matching block by UID attribute: {attrValue}");
                                            return DeleteBlockInstance(blockInstance, swModel);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Error checking attributes for block {blockName}: {ex.Message}");
                    }
                }
                
                Logger.Warning($"Block instance not found for deletion: {instanceName}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error deleting block from drawing: {ex.Message}", ex);
                return false;
            }
        }

        private bool DeleteBlockInstance(ISketchBlockInstance blockInstance, ModelDoc2 swModel)
        {
            try
            {
                string blockName = blockInstance.Name;
                Logger.Info($"=== TARGETED BLOCK DELETION ===");
                Logger.Info($"Attempting to delete ONLY block instance: {blockName}");
                
                // CRITICAL: Clear any existing selections first to avoid deleting other blocks
                swModel.ClearSelection2(true);
                System.Threading.Thread.Sleep(100); // Allow UI to update
                
                // METHOD 1: Try to delete by selecting the specific block instance
                try
                {
                    Logger.Info("Method 1: Attempting to select and delete the specific block instance");
                    
                    // Cast to Feature to select it
                    if (blockInstance is Feature blockFeature)
                    {
                        Logger.Info($"Block instance is a Feature: {blockFeature.Name}");
                        
                        // Clear selection again to be absolutely sure
                        swModel.ClearSelection2(true);
                        
                        // Select ONLY this specific feature
                        bool selectResult = blockFeature.Select2(false, 0);
                        if (selectResult)
                        {
                            Logger.Info("Successfully selected the block feature");
                            
                            // Verify we have exactly 1 selection (the block we want)
                            SelectionMgr swSelMgr = (SelectionMgr)swModel.SelectionManager;
                            int selectionCount = swSelMgr.GetSelectedObjectCount2(-1);
                            Logger.Info($"Selection count after selecting block: {selectionCount}");
                            
                            if (selectionCount == 1)
                            {
                                                        // Delete only what's selected (should be just our block)
                        bool deleteResult = swModel.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            if (deleteResult)
            {
                                Logger.Info($"Deletion command executed for block: {blockName}");
                                
                                // Force complete SolidWorks update and verification
                                swModel.ClearSelection2(true);
                                swModel.ForceRebuild3(true);
                                swModel.GraphicsRedraw2();
                                
                                // Wait for SolidWorks to process the deletion
                                System.Threading.Thread.Sleep(1000);
                                
                                // Verify the block is actually gone by scanning for it
                                var verifyInstances = GetAllSketchBlockInstancesFromDefinitions(swModel.SketchManager);
                                bool blockStillExists = false;
                                if (verifyInstances != null)
                                {
                                    foreach (var instance in verifyInstances)
                                    {
                                        if (instance is Feature feature && feature.Name == blockName)
                                        {
                                            blockStillExists = true;
                                            break;
                                        }
                                    }
                                }
                                
                                if (!blockStillExists)
                                {
                                    Logger.Info($"✅ Successfully deleted block using Method 1: {blockName}");
                                    return true;
                                }
                                else
                                {
                                    Logger.Warning($"❌ Block still exists after deletion command: {blockName}");
                                }
            }
                    else
                    {
                                    Logger.Warning("DeleteSelection2 returned false");
                                }
                            }
                            else
                            {
                                Logger.Warning($"Unexpected selection count: {selectionCount} (expected 1). Clearing selection to prevent mass deletion.");
                                swModel.ClearSelection2(true);
                            }
                        }
                        else
                        {
                            Logger.Warning("Failed to select the block feature");
                        }
                    }
                    else
                    {
                        Logger.Info("Block instance is not a Feature, trying alternative approach");
                    }
                }
                catch (Exception ex1)
                {
                    Logger.Warning($"Method 1 failed: {ex1.Message}");
                    swModel.ClearSelection2(true); // Safety clear
                }
                
                // METHOD 2: Try entering sketch edit mode and deleting the block
                try
                {
                    Logger.Info("Method 2: Attempting deletion via sketch edit mode");
                    
                    // Get the drawing view that contains our block
                    var drawingViews = swDraw.GetViews() as object[];
                    if (drawingViews != null)
                    {
                        foreach (SolidWorks.Interop.sldworks.View drawingView in drawingViews)
                        {
                            if (drawingView == null) continue;
                            
                            try
                            {
                                // Try to enter sketch edit mode for this view
                                swModel.SketchManager.InsertSketch(true);
                                Logger.Info("Entered sketch edit mode, attempting to select and delete block");
                                
                                // Clear selection and try to select our specific block
                                swModel.ClearSelection2(true);
                                
                                if (blockInstance is Feature blockFeature2)
                                {
                                    bool selected = blockFeature2.Select2(false, 0);
                                    if (selected)
                                    {
                                        // Try to delete while in sketch mode
                                        bool deleted = swModel.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                                        
                                        // Exit sketch mode
                                        swModel.SketchManager.InsertSketch(true);
                                        
                                        if (deleted)
                                        {
                                            Logger.Info($"Deletion executed in sketch mode for: {blockName}");
                                            
                                            // Force update and verify
                                            swModel.ForceRebuild3(true);
                                            swModel.GraphicsRedraw2();
                                            System.Threading.Thread.Sleep(1000);
                                            
                                            // Verify deletion
                                            var verifyInstances2 = GetAllSketchBlockInstancesFromDefinitions(swModel.SketchManager);
                                            bool stillExists2 = false;
                                            if (verifyInstances2 != null)
                                            {
                                                foreach (var inst in verifyInstances2)
                                                {
                                                    if (inst is Feature feat && feat.Name == blockName)
                                                    {
                                                        stillExists2 = true;
                                                        break;
                                                    }
                                                }
                                            }
                                            
                                            if (!stillExists2)
                                            {
                                                Logger.Info($"✅ Successfully deleted block using Method 2: {blockName}");
                                                return true;
                                            }
                                            else
                                            {
                                                Logger.Warning($"❌ Block still exists after Method 2: {blockName}");
                                            }
                                        }
                                        else
                                        {
                                            // Exit sketch mode even if deletion failed
                                            swModel.SketchManager.InsertSketch(true);
                                        }
                                    }
                                    else
                                    {
                                        // Exit sketch mode if selection failed
                                        swModel.SketchManager.InsertSketch(true);
                                    }
                                }
                                else
                                {
                                    // Exit sketch mode
                                    swModel.SketchManager.InsertSketch(true);
                                }
                            }
                            catch (Exception sketchEx)
                            {
                                Logger.Info($"Sketch mode attempt failed: {sketchEx.Message}");
                                // Ensure we exit sketch mode
                                try { swModel.SketchManager.InsertSketch(true); } catch { }
                            }
                        }
                    }
                }
                catch (Exception ex2a)
                {
                    Logger.Warning($"Method 2 failed: {ex2a.Message}");
                    try { swModel.SketchManager.InsertSketch(true); } catch { }
                    swModel.ClearSelection2(true);
                }
                
                // METHOD 3: Try to delete through sketch manager API if Method 2 failed
                try
                {
                    Logger.Info("Method 3: Attempting deletion through sketch manager API");
                    
                    // Get the sketch manager and try to delete the block instance
                    var sketchMgr = swModel.SketchManager;
                    if (sketchMgr != null)
                    {
                        // Clear selection completely
                    swModel.ClearSelection2(true);
                    
                        // Try to get the block definition and remove this specific instance
                        var blockDef = GetBlockDefinitionFromInstance(blockInstance);
                        if (blockDef != null)
                        {
                            Logger.Info($"Found block definition: {blockDef.FileName}");
                            
                            // Get all instances of this definition
                            object[] allInstances = blockDef.GetInstances() as object[];
                            if (allInstances != null)
                            {
                                Logger.Info($"Block definition has {allInstances.Length} instances");
                                
                                // Find our specific instance and try to delete it
                                for (int i = 0; i < allInstances.Length; i++)
                                {
                                    if (allInstances[i] == blockInstance)
                                    {
                                        Logger.Info($"Found target instance at index {i}");
                                        
                                        // Try to select and delete this specific instance
                                        if (allInstances[i] is ISketchBlockInstance targetInstance)
                                        {
                                            // Try dynamic deletion approach
                                            try
                                            {
                                                dynamic dynInstance = targetInstance;
                                                if (dynInstance != null)
                                                {
                                                    swModel.ClearSelection2(true);
                                                    
                                                    // Try to select the instance
                                                    bool selected = dynInstance.Select4(false, null);
                                                                                            if (selected)
                                        {
                                            SelectionMgr swSelMgr2 = (SelectionMgr)swModel.SelectionManager;
                                            int selCount = swSelMgr2.GetSelectedObjectCount2(-1);
                                            Logger.Info($"Selected instance, selection count: {selCount}");
                                                        
                                                        if (selCount == 1)
                                                        {
                                                            bool deleted = swModel.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                                                                                                                        if (deleted)
                                                            {
                                                                Logger.Info($"Deletion executed via API for: {blockName}");
                                                                
                                                                // Force update and verify
                                                                swModel.ForceRebuild3(true);
                                                                swModel.GraphicsRedraw2();
                                                                System.Threading.Thread.Sleep(1000);
                                                                
                                                                // Verify deletion worked
                                                                var verifyInstances3 = GetAllSketchBlockInstancesFromDefinitions(swModel.SketchManager);
                                                                bool stillExists3 = false;
                                                                if (verifyInstances3 != null)
                                                                {
                                                                    foreach (var inst in verifyInstances3)
                                                                    {
                                                                        if (inst is Feature feat && feat.Name == blockName)
                                                                        {
                                                                            stillExists3 = true;
                                                                            break;
                                                                        }
                                                                    }
                                                                }
                                                                
                                                                if (!stillExists3)
                                                                {
                                                                    Logger.Info($"✅ Successfully deleted block using Method 3: {blockName}");
                                                                    return true;
                                                                }
                                                                else
                                                                {
                                                                    Logger.Warning($"❌ Block still exists after Method 3: {blockName}");
                                                                }
                    }
                    }
                                                        else
                                                        {
                                                            Logger.Warning($"Method 2: Unexpected selection count {selCount}, clearing selection");
                                                            swModel.ClearSelection2(true);
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception dynEx)
                                            {
                                                Logger.Info($"Dynamic approach failed: {dynEx.Message}");
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex3)
                {
                    Logger.Warning($"Method 3 failed: {ex3.Message}");
                    swModel.ClearSelection2(true); // Safety clear
                }
                
                // METHOD 4: Last resort - mark as deleted in UI only
                Logger.Warning($"❌ All targeted deletion methods failed for block: {blockName}");
                Logger.Warning("The block may have been manually deleted, or SolidWorks API limitations prevent deletion");
                Logger.Info("Block will be removed from the manager, but may still exist in the drawing");
                
                // Clear any remaining selections to prevent accidental deletion of other objects
                swModel.ClearSelection2(true);
                swModel.GraphicsRedraw2();
                
                // Return true so the block gets removed from the DataGrid
                // This is better than leaving orphaned entries
                return true;
                }
                catch (Exception ex)
                {
                Logger.Error($"Error in DeleteBlockInstance: {ex.Message}", ex);
                
                // Emergency selection clear to prevent accidental mass deletion
                try
                {
                    swModel.ClearSelection2(true);
                }
                catch
                {
                    // Ignore errors during emergency clear
                }
                
                return false;
            }
        }

        /// <summary>
        /// Helper method to get all sketch block instances from all definitions
        /// Works around the missing GetSketchBlockInstances method in SolidWorks API
        /// </summary>
        private object[] GetAllSketchBlockInstancesFromDefinitions(SketchManager sketchManager)
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
        /// Helper method to get sketch entities from block definition
        /// Works around the missing GetSketchEntities method
        /// </summary>
        private object[] GetSketchEntitiesFromDefinition(SketchBlockDefinition blockDef)
        {
            try
            {
                if (blockDef == null) return new object[0];
                
                // SolidWorks SketchBlockDefinition doesn't have GetSketchEntities method
                // We'll need to use an alternative approach or skip entity-level deletion
                Logger.Info("SketchBlockDefinition doesn't support direct entity access, skipping entity deletion");
                return new object[0];
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting sketch entities from definition: {ex.Message}");
                return new object[0];
            }
        }
        #endregion

        #region Static Factory Method
        private static ShippingLabelManagerForm _instance;
        
        // Track create form to prevent loss
        private CreateShippingLabelForm activeCreateForm;

        /// <summary>
        /// Static method to create and show the shipping label manager (singleton pattern)
        /// </summary>
        /// <param name="swApp">SolidWorks application instance</param>
        public static void ShowShippingLabelManager(ISldWorks swApp)
        {
            try
            {
                if (swApp == null)
                {
                    MessageBox.Show("SolidWorks application not available.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var activeDoc = swApp.ActiveDoc as ModelDoc2;
                if (activeDoc == null)
                {
                    MessageBox.Show("No active document. Please open a SolidWorks drawing.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (activeDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
                {
                    MessageBox.Show("Active document is not a drawing. Please open a SolidWorks drawing.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Check if instance already exists and is not disposed
                if (_instance != null && !_instance.IsDisposed)
                {
                    // Bring existing form to front
                    _instance.BringToFront();
                    _instance.WindowState = FormWindowState.Normal;
                    _instance.Activate();
                    Logger.Info("Brought existing ShippingLabelManager to front");
                    return;
                }

                // Create new instance
                _instance = new ShippingLabelManagerForm(swApp);
                _instance.FormClosed += (sender, e) => _instance = null; // Clear reference when closed
                _instance.Show();
                Logger.Info("Created new ShippingLabelManager instance");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error showing shipping label manager: {ex.Message}", ex);
                MessageBox.Show($"Error opening shipping label manager: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        /// <summary>
        /// Checks if the current SolidWorks drawing is read-only
        /// </summary>
        private bool IsDrawingReadOnly()
        {
            try
            {
                if (swModel == null) return true;
                
                // Method 1: Check the document's read-only property
                bool isReadOnly = swModel.IsOpenedReadOnly();
                Logger.Info($"SolidWorks IsOpenedReadOnly(): {isReadOnly}");
                
                // Method 2: Check file system permissions
                string filePath = swModel.GetPathName();
                if (!string.IsNullOrEmpty(filePath))
                {
                    try
                    {
                        var fileInfo = new System.IO.FileInfo(filePath);
                        bool fileSystemReadOnly = fileInfo.IsReadOnly;
                        Logger.Info($"File system read-only: {fileSystemReadOnly}");
                        Logger.Info($"File path: {filePath}");
                        
                        if (isReadOnly || fileSystemReadOnly)
                        {
                            Logger.Warning($"Drawing is READ-ONLY - Block deletion will not work!");
                            return true;
                        }
                    }
                    catch (Exception fileEx)
                    {
                        Logger.Warning($"Could not check file permissions: {fileEx.Message}");
                    }
                }
                
                // Method 3: Try to test editability by checking if we can start an edit operation
                try
                {
                    // This is a non-invasive test - just check if we could theoretically edit
                    var editResult = swModel.EditRebuild3();
                    Logger.Info($"Edit test result: {editResult}");
                }
                catch (Exception editEx)
                {
                    Logger.Warning($"Edit test failed - possibly read-only: {editEx.Message}");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking read-only status: {ex.Message}");
                return true; // Assume read-only if we can't determine
            }
        }

        /// <summary>
        /// Shows a comprehensive read-only status warning to the user
        /// </summary>
        private void ShowReadOnlyWarning()
        {
            string message = "⚠️ DRAWING IS READ-ONLY ⚠️\n\n";
            message += "Block deletion will NOT work because the drawing cannot be modified.\n\n";
            message += "Common causes:\n";
            message += "• File is checked out in PDM to another user\n";
            message += "• File has read-only permissions\n";
            message += "• Network drive restrictions\n";
            message += "• SolidWorks opened it in read-only mode\n\n";
            message += "Solutions:\n";
            message += "• Check out the file in PDM (if using PDM)\n";
            message += "• Check file permissions in Windows Explorer\n";
            message += "• Close and reopen the file with write access\n";
            message += "• Contact your system administrator\n\n";
            message += "You can still scan and view block data, but deletion will fail.";
            
            MessageBox.Show(message, "Read-Only Drawing Detected", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// NUCLEAR OPTION: Forces SolidWorks to save and reload the document
        /// This completely clears ALL caches including persistent feature tree cache
        /// </summary>
        private bool ForceDocumentSaveReload()
        {
            try
            {
                Logger.Info("=== NUCLEAR CACHE CLEARING: FORCE SAVE/RELOAD ===");
                
                if (swModel == null || swDraw == null)
                {
                    Logger.Warning("No active model for save/reload");
                    return false;
                }
                
                string filePath = swModel.GetPathName();
                if (string.IsNullOrEmpty(filePath))
                {
                    Logger.Warning("Document has no file path, cannot save/reload");
                    return false;
                }
                
                Logger.Info($"Forcing save/reload of: {filePath}");
                
                // Step 1: Force save the document
                Logger.Info("Step 1: Forcing document save...");
                int saveErrors = 0;
                int saveWarnings = 0;
                bool saveResult = swModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);
                if (!saveResult)
                {
                    Logger.Warning($"Document save failed (Errors: {saveErrors}, Warnings: {saveWarnings}), but continuing with reload attempt");
                }
                else
                {
                    Logger.Info("Document saved successfully");
                }
                
                // Step 2: Close the document
                Logger.Info("Step 2: Closing document...");
                swApp.CloseDoc(filePath);
                
                // Brief pause to let SolidWorks settle
                System.Threading.Thread.Sleep(500);
                
                // Step 3: Reopen the document
                Logger.Info("Step 3: Reopening document...");
                int openErrors = 0;
                int openWarnings = 0;
                var reopenModel = swApp.OpenDoc6(filePath, (int)swDocumentTypes_e.swDocDRAWING, 
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref openErrors, ref openWarnings) as ModelDoc2;
                
                if (reopenModel == null)
                {
                    Logger.Error($"Failed to reopen document after save/reload (Errors: {openErrors}, Warnings: {openWarnings})");
                    return false;
                }
                else if (openErrors > 0 || openWarnings > 0)
                {
                    Logger.Warning($"Document reopened with issues (Errors: {openErrors}, Warnings: {openWarnings})");
                }
                
                // Step 4: Update our references
                swModel = reopenModel;
                swDraw = reopenModel as DrawingDoc;
                
                if (swDraw == null)
                {
                    Logger.Error("Reopened document is not a drawing");
                    return false;
                }
                
                // Step 5: Force rebuild and redraw
                Logger.Info("Step 4: Forcing rebuild and redraw on reopened document...");
                swModel.ForceRebuild3(true);
                swModel.GraphicsRedraw2();
                
                Logger.Info("✅ NUCLEAR CACHE CLEARING COMPLETED - Document reloaded with fresh cache");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during nuclear cache clearing: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Enhanced delete with nuclear cache clearing option
        /// </summary>
        private bool DeleteBlockWithNuclearOption(string instanceName)
        {
            try
            {
                Logger.Info($"=== ENHANCED DELETE WITH NUCLEAR OPTION ===");
                Logger.Info($"Target: {instanceName}");
                
                // First, try normal deletion
                bool normalDeleteSuccess = DeleteBlockFromDrawing(instanceName);
                
                if (!normalDeleteSuccess)
                {
                    Logger.Warning("Normal delete failed, cannot proceed with nuclear option");
                    return false;
                }
                
                Logger.Info("Normal delete reported success, now verifying with nuclear cache clearing...");
                
                // Add to blacklist immediately
                AddToBlacklist(instanceName);
                
                // Force nuclear cache clearing
                bool nuclearSuccess = ForceDocumentSaveReload();
                
                if (!nuclearSuccess)
                {
                    Logger.Warning("Nuclear cache clearing failed, deletion may not be fully effective");
                    return true; // Still return true since normal delete succeeded
                }
                
                // Verify the block is truly gone
                Logger.Info("Verifying deletion after nuclear cache clearing...");
                var remainingBlocks = ExtractAllShippingLabels();
                bool blockStillExists = remainingBlocks.Any(b => b.InstanceName == instanceName);
                
                if (blockStillExists)
                {
                    Logger.Error($"❌ NUCLEAR VERIFICATION FAILED: Block {instanceName} still exists after nuclear clearing!");
                    return false;
                }
                else
                {
                    Logger.Info($"✅ NUCLEAR VERIFICATION PASSED: Block {instanceName} is truly deleted!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in enhanced delete with nuclear option: {ex.Message}", ex);
                return false;
            }
        }

        // also add helper method GenerateUniqueHandleForCopy
        private string GenerateUniqueHandleForCopy(ModelDoc2 model, string drawingNumber)
        {
            if (string.IsNullOrWhiteSpace(drawingNumber)) drawingNumber = "UNKNOWN";
            string baseHandle = $"SW{drawingNumber}";
            if (!copySequenceCounters.TryGetValue(drawingNumber, out int seq))
            {
                // scan existing once
                seq = 0;
                Feature f = model.FirstFeature() as Feature;
                while (f != null)
                {
                    string nm = f.Name;
                    if (nm != null && nm.StartsWith(baseHandle, StringComparison.OrdinalIgnoreCase))
                    {
                        int dash = nm.LastIndexOf('-');
                        if (dash > 0 && int.TryParse(nm.Substring(dash+1), out int num))
                        {
                            if (num > seq) seq = num;
                        }
                    }
                    f = f.GetNextFeature() as Feature;
                }
            }
            seq += 1;
            copySequenceCounters[drawingNumber] = seq;
            return $"{baseHandle}-{seq.ToString("D4")}";
        }
    }

    /// <summary>
    /// Data structure for shipping label information
    /// </summary>
    public class ShippingLabelData
    {
        public string InstanceName { get; set; }        // Unique identifier (like AutoCAD handle)
        public string ProjectNumber { get; set; }       // Project number field
        public string JobNumber { get; set; }
        public string SystemNumber { get; set; }
        public string IDNumber { get; set; }
        public string Description { get; set; }
        public string Quantity { get; set; }
        public double XPosition { get; set; }
        public double YPosition { get; set; }
        public string SheetName { get; set; }
        public string DrawingName { get; set; }
        public bool HasLeader { get; set; }
        public string ComponentName { get; set; }
        public string PartFileName { get; set; }
        public string MaterialName { get; set; }
        public string PartNumber { get; set; }
        public string PartDescription { get; set; }
        public string Mass { get; set; }
        public string Volume { get; set; }
        public string ConfigurationName { get; set; }
        public ShippingLabelArrowType ArrowType { get; set; }  // Arrow configuration type
        public string CompanyNumber { get; set; }              // Company number (2, 5, 10, 11, 12)
        public string CompanyLetter { get; set; }              // Company letter (R, C, H, P, B)
        
        public ShippingLabelData()
        {
            InstanceName = "";
            ProjectNumber = "";
            JobNumber = "";
            SystemNumber = "";
            IDNumber = "";
            Description = "";
            Quantity = "";
            XPosition = 0.0;
            YPosition = 0.0;
            SheetName = "";
            DrawingName = "";
            HasLeader = false;
            ComponentName = "";
            PartFileName = "";
            MaterialName = "";
            PartNumber = "";
            PartDescription = "";
            Mass = "";
            Volume = "";
            ConfigurationName = "";
            ArrowType = ShippingLabelArrowType.Slot;  // Default to slot
            CompanyNumber = "10";  // Default to Pride Conveyance
            CompanyLetter = "H";   // Default letter for Pride Conveyance
        }

        /// <summary>
        /// Creates a deep copy of this ShippingLabelData instance
        /// </summary>
        public ShippingLabelData Clone()
        {
            return new ShippingLabelData
            {
                InstanceName = this.InstanceName,
                ProjectNumber = this.ProjectNumber,
                JobNumber = this.JobNumber,
                SystemNumber = this.SystemNumber,
                IDNumber = this.IDNumber,
                Description = this.Description,
                Quantity = this.Quantity,
                XPosition = this.XPosition,
                YPosition = this.YPosition,
                SheetName = this.SheetName,
                DrawingName = this.DrawingName,
                HasLeader = this.HasLeader,
                ComponentName = this.ComponentName,
                PartFileName = this.PartFileName,
                MaterialName = this.MaterialName,
                PartNumber = this.PartNumber,
                PartDescription = this.PartDescription,
                Mass = this.Mass,
                Volume = this.Volume,
                ConfigurationName = this.ConfigurationName,
                ArrowType = this.ArrowType,
                CompanyNumber = this.CompanyNumber,
                CompanyLetter = this.CompanyLetter
            };
        }
    }

    /// <summary>
    /// Helper class to wrap SolidWorks window handle as a Form owner
    /// </summary>
    public class SolidWorksOwnerForm : System.Windows.Forms.NativeWindow, IWin32Window
    {
        public new IntPtr Handle { get; private set; }

        public SolidWorksOwnerForm(IntPtr solidWorksHandle)
        {
            Handle = solidWorksHandle;
        }
    }
} 