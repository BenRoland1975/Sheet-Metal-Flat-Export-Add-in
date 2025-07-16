using System;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using EPDM.Interop.epdm;

namespace RoesleinAddIn
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            try
            {
                // For testing the settings form in standalone mode, pass null for SolidWorks and PDM instances
                Application.Run(new SettingsFormV2(null, null));
                
                // For testing just the raw materials functionality
                // Application.Run(new TestRawMaterials());
                
                // For using SettingsFormV2 after it's fixed
                // Application.Run(new SettingsFormV2());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unhandled error: {ex.Message}\n\n{ex.StackTrace}", "Application Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 