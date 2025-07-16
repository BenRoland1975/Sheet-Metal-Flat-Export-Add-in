using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RoesleinAddIn
{
    public partial class StructureFbdiOptionsForm : Form
    {
        public List<string> SelectedOrganizations { get; private set; }

        public StructureFbdiOptionsForm()
        {
            InitializeComponent();
            SelectedOrganizations = new List<string>();
            
            // Set default selection to RB Randolph
            checkBoxRBRandolph.Checked = true;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Collect selected organizations
            SelectedOrganizations.Clear();
            
            if (checkBoxRoesleinMaster.Checked)
                SelectedOrganizations.Add("ROESLEIN_MASTER");
                
            if (checkBoxRBKaskaskia.Checked)
                SelectedOrganizations.Add("RMFKK");
                
            if (checkBoxRBRandolph.Checked)
                SelectedOrganizations.Add("RMF401");
                
            if (checkBoxLifeCycleKaskaskia.Checked)
                SelectedOrganizations.Add("LCYKSK");

            // Validate at least one is selected
            if (SelectedOrganizations.Count == 0)
            {
                MessageBox.Show("Please select at least one organization.", "Selection Required", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
} 