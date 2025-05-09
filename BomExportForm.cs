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
using System.IO;
using System.Runtime.InteropServices;

namespace RoesleinAddIn
{
    public partial class BomExportForm : Form
    {
        private ISldWorks _swApp;

        // --- P/Invoke declaration to set foreground window ---
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        // ------------------------------------------------------

        public BomExportForm(ISldWorks swApp)
        {
            InitializeComponent();
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        public BomExportForm()
        {
            InitializeComponent();
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            BomType selectedType = BomType.Full;

            if (radioButtonConsolidated.Checked)
                selectedType = BomType.Consolidated;
            else if (radioButtonSpareParts.Checked)
                selectedType = BomType.SpareParts;

            if (_swApp == null) 
            {
                MessageBox.Show("SolidWorks application instance is not available in the form.", "Roeslein Add-in - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.Abort;
                Close();
                return;
            }

            string filePath = string.Empty;
            ModelDoc2 activeDoc = _swApp.ActiveDoc as ModelDoc2;
            string tempAssemblyName = Path.GetFileNameWithoutExtension(activeDoc?.GetTitle() ?? "Assembly");

            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*";
            saveDialog.Title = "Save BOM";
            saveDialog.FileName = $"{tempAssemblyName}_{selectedType}_BOM.xlsx";
            
            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                filePath = saveDialog.FileName;
            }
            else
            {
                this.DialogResult = DialogResult.Cancel;
                return;
            }

            // --- File path selected, attempt to reactivate SW window ---
            try
            {
                Frame swFrame = _swApp.Frame() as Frame;
                if (swFrame != null)
                {
                    IntPtr swHWND = (IntPtr)swFrame.GetHWnd();
                    if (swHWND != IntPtr.Zero)
                    {
                        SetForegroundWindow(swHWND);
                    }
                }
            }
            catch (Exception focusEx)
            {
                // Log or debug the error if needed, but don't stop the export
                System.Diagnostics.Debug.WriteLine($"Error setting foreground window: {focusEx.Message}");
            }

            // --- Now hide form and process ---
            this.Hide();
            Application.DoEvents(); // Allow the form to visually hide before long process

            try
            {
                BomProcessor processor = new BomProcessor(_swApp);
                string assemblyName;
                List<BomItem> bomItems = processor.GenerateBomData(selectedType, out assemblyName);

                if (bomItems != null && bomItems.Any())
                {
                    processor.ExportBomToExcel(bomItems, filePath, selectedType, assemblyName);
                    this.DialogResult = DialogResult.OK; 
                }
                else
                {
                    MessageBox.Show("Failed to generate BOM data or BOM is empty.", "Roeslein Add-in - Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.Cancel;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during BOM export: {ex.Message}", "Roeslein Add-in - Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.Abort;
            }
            finally
            {
                Close();
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
