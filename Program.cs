using System;
using System.Windows.Forms;

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
                // Use the original SettingsForm until we can fix SettingsFormV2
                Application.Run(new SettingsForm());
                
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