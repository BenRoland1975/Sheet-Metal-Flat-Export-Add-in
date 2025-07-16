namespace RoesleinAddIn
{
    partial class BomExportForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBoxOracleFBDI = new System.Windows.Forms.GroupBox();
            this.radioButtonStructure = new System.Windows.Forms.RadioButton();
            this.radioButtonItems = new System.Windows.Forms.RadioButton();
            this.groupBoxBomType = new System.Windows.Forms.GroupBox();
            this.radioButtonSpareParts = new System.Windows.Forms.RadioButton();
            this.radioButtonConsolidated = new System.Windows.Forms.RadioButton();
            this.radioButtonFull = new System.Windows.Forms.RadioButton();
            this.buttonExport = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.groupBoxOracleFBDI.SuspendLayout();
            this.groupBoxBomType.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxOracleFBDI
            // 
            this.groupBoxOracleFBDI.Controls.Add(this.radioButtonStructure);
            this.groupBoxOracleFBDI.Controls.Add(this.radioButtonItems);
            this.groupBoxOracleFBDI.Location = new System.Drawing.Point(15, 16);
            this.groupBoxOracleFBDI.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBoxOracleFBDI.Name = "groupBoxOracleFBDI";
            this.groupBoxOracleFBDI.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBoxOracleFBDI.Size = new System.Drawing.Size(308, 85);
            this.groupBoxOracleFBDI.TabIndex = 0;
            this.groupBoxOracleFBDI.TabStop = false;
            this.groupBoxOracleFBDI.Text = "Oracle FBDI";
            // 
            // radioButtonStructure
            // 
            this.radioButtonStructure.AutoSize = true;
            this.radioButtonStructure.Checked = false;
            this.radioButtonStructure.Location = new System.Drawing.Point(15, 49);
            this.radioButtonStructure.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.radioButtonStructure.Name = "radioButtonStructure";
            this.radioButtonStructure.Size = new System.Drawing.Size(70, 17);
            this.radioButtonStructure.TabIndex = 1;
            this.radioButtonStructure.Text = "Structure";
            this.radioButtonStructure.UseVisualStyleBackColor = true;
            this.radioButtonStructure.CheckedChanged += new System.EventHandler(this.radioButtonStructure_CheckedChanged);
            // 
            // radioButtonItems
            // 
            this.radioButtonItems.AutoSize = true;
            this.radioButtonItems.Checked = false;
            this.radioButtonItems.Location = new System.Drawing.Point(15, 24);
            this.radioButtonItems.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.radioButtonItems.Name = "radioButtonItems";
            this.radioButtonItems.Size = new System.Drawing.Size(50, 17);
            this.radioButtonItems.TabIndex = 0;
            this.radioButtonItems.Text = "Items";
            this.radioButtonItems.UseVisualStyleBackColor = true;
            this.radioButtonItems.CheckedChanged += new System.EventHandler(this.radioButtonItems_CheckedChanged);
            // 
            // groupBoxBomType
            // 
            this.groupBoxBomType.Controls.Add(this.radioButtonSpareParts);
            this.groupBoxBomType.Controls.Add(this.radioButtonConsolidated);
            this.groupBoxBomType.Controls.Add(this.radioButtonFull);
            this.groupBoxBomType.Location = new System.Drawing.Point(15, 111);
            this.groupBoxBomType.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBoxBomType.Name = "groupBoxBomType";
            this.groupBoxBomType.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBoxBomType.Size = new System.Drawing.Size(308, 122);
            this.groupBoxBomType.TabIndex = 1;
            this.groupBoxBomType.TabStop = false;
            this.groupBoxBomType.Text = "BOM Type";
            // 
            // radioButtonSpareParts
            // 
            this.radioButtonSpareParts.AutoSize = true;
            this.radioButtonSpareParts.Location = new System.Drawing.Point(15, 73);
            this.radioButtonSpareParts.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.radioButtonSpareParts.Name = "radioButtonSpareParts";
            this.radioButtonSpareParts.Size = new System.Drawing.Size(270, 17);
            this.radioButtonSpareParts.TabIndex = 2;
            this.radioButtonSpareParts.Text = "Spare Parts BOM (Only parts marked as spare parts)";
            this.radioButtonSpareParts.UseVisualStyleBackColor = true;
            this.radioButtonSpareParts.CheckedChanged += new System.EventHandler(this.radioButtonBomType_CheckedChanged);
            // 
            // radioButtonConsolidated
            // 
            this.radioButtonConsolidated.AutoSize = true;
            this.radioButtonConsolidated.Location = new System.Drawing.Point(15, 49);
            this.radioButtonConsolidated.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.radioButtonConsolidated.Name = "radioButtonConsolidated";
            this.radioButtonConsolidated.Size = new System.Drawing.Size(261, 17);
            this.radioButtonConsolidated.TabIndex = 1;
            this.radioButtonConsolidated.Text = "Consolidated BOM (Parts grouped by part number)";
            this.radioButtonConsolidated.UseVisualStyleBackColor = true;
            this.radioButtonConsolidated.CheckedChanged += new System.EventHandler(this.radioButtonBomType_CheckedChanged);
            // 
            // radioButtonFull
            // 
            this.radioButtonFull.AutoSize = true;
            this.radioButtonFull.Checked = true;
            this.radioButtonFull.Location = new System.Drawing.Point(15, 24);
            this.radioButtonFull.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.radioButtonFull.Name = "radioButtonFull";
            this.radioButtonFull.Size = new System.Drawing.Size(198, 17);
            this.radioButtonFull.TabIndex = 0;
            this.radioButtonFull.TabStop = true;
            this.radioButtonFull.Text = "Full BOM (All parts in assembly order)";
            this.radioButtonFull.UseVisualStyleBackColor = true;
            this.radioButtonFull.CheckedChanged += new System.EventHandler(this.radioButtonBomType_CheckedChanged);
            // 
            // buttonExport
            // 
            this.buttonExport.Location = new System.Drawing.Point(188, 247);
            this.buttonExport.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonExport.Name = "buttonExport";
            this.buttonExport.Size = new System.Drawing.Size(60, 24);
            this.buttonExport.TabIndex = 2;
            this.buttonExport.Text = "Export";
            this.buttonExport.UseVisualStyleBackColor = true;
            this.buttonExport.Click += new System.EventHandler(this.buttonExport_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(264, 247);
            this.buttonCancel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(60, 24);
            this.buttonCancel.TabIndex = 3;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // BomExportForm
            // 
            this.AcceptButton = this.buttonExport;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(341, 290);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonExport);
            this.Controls.Add(this.groupBoxBomType);
            this.Controls.Add(this.groupBoxOracleFBDI);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BomExportForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Export BOM";
            this.groupBoxOracleFBDI.ResumeLayout(false);
            this.groupBoxOracleFBDI.PerformLayout();
            this.groupBoxBomType.ResumeLayout(false);
            this.groupBoxBomType.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxOracleFBDI;
        private System.Windows.Forms.RadioButton radioButtonStructure;
        private System.Windows.Forms.RadioButton radioButtonItems;
        private System.Windows.Forms.GroupBox groupBoxBomType;
        private System.Windows.Forms.RadioButton radioButtonSpareParts;
        private System.Windows.Forms.RadioButton radioButtonConsolidated;
        private System.Windows.Forms.RadioButton radioButtonFull;
        private System.Windows.Forms.Button buttonExport;
        private System.Windows.Forms.Button buttonCancel;
    }
}