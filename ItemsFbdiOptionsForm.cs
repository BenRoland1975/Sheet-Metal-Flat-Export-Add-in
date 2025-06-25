using System;
using System.Drawing;
using System.Windows.Forms;

namespace RoesleinAddIn
{
    public partial class ItemsFbdiOptionsForm : Form
    {
        public string TransactionType { get; private set; } = "CREATE";
        public bool IncludePurchaseItems { get; private set; } = true;

        public ItemsFbdiOptionsForm()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            TransactionType = rbCreate.Checked ? "CREATE" : "UPDATE";
            IncludePurchaseItems = chkIncludePurchaseItems.Checked;
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