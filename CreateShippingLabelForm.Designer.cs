namespace RoesleinAddIn
{
    partial class CreateShippingLabelForm
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
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
                // Clean up preview image resources
                picPreview?.Image?.Dispose();
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
            this.lblCompanyNumber = new System.Windows.Forms.Label();
            this.cmbCompanyNumber = new System.Windows.Forms.ComboBox();
            this.lblCompanyLetter = new System.Windows.Forms.Label();
            this.txtCompanyLetter = new System.Windows.Forms.TextBox();
            this.lblArrowType = new System.Windows.Forms.Label();
            this.cmbArrowType = new System.Windows.Forms.ComboBox();
            this.lblProjectNumber = new System.Windows.Forms.Label();
            this.txtProjectNumber = new System.Windows.Forms.TextBox();
            this.lblJobNumber = new System.Windows.Forms.Label();
            this.txtJobNumber = new System.Windows.Forms.TextBox();
            this.lblSystemNumber = new System.Windows.Forms.Label();
            this.txtSystemNumber = new System.Windows.Forms.TextBox();
            this.lblIDNumber = new System.Windows.Forms.Label();
            this.txtIDNumber = new System.Windows.Forms.TextBox();
            this.lblDescription = new System.Windows.Forms.Label();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.lblQuantity = new System.Windows.Forms.Label();
            this.txtQuantity = new System.Windows.Forms.TextBox();
            this.lblBlockScale = new System.Windows.Forms.Label();
            this.txtBlockScale = new System.Windows.Forms.TextBox();
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.btnPlaceOnDrawing = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.SuspendLayout();
            // 
            // lblCompanyNumber
            // 
            this.lblCompanyNumber.AutoSize = true;
            this.lblCompanyNumber.Location = new System.Drawing.Point(18, 22);
            this.lblCompanyNumber.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblCompanyNumber.Name = "lblCompanyNumber";
            this.lblCompanyNumber.Size = new System.Drawing.Size(140, 20);
            this.lblCompanyNumber.TabIndex = 0;
            this.lblCompanyNumber.Text = "Company Number:";
            // 
            // cmbCompanyNumber
            // 
            this.cmbCompanyNumber.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCompanyNumber.FormattingEnabled = true;
            this.cmbCompanyNumber.Location = new System.Drawing.Point(172, 18);
            this.cmbCompanyNumber.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cmbCompanyNumber.Name = "cmbCompanyNumber";
            this.cmbCompanyNumber.Size = new System.Drawing.Size(298, 28);
            this.cmbCompanyNumber.TabIndex = 1;
            // 
            // lblCompanyLetter
            // 
            this.lblCompanyLetter.AutoSize = true;
            this.lblCompanyLetter.Location = new System.Drawing.Point(18, 68);
            this.lblCompanyLetter.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblCompanyLetter.Name = "lblCompanyLetter";
            this.lblCompanyLetter.Size = new System.Drawing.Size(126, 20);
            this.lblCompanyLetter.TabIndex = 2;
            this.lblCompanyLetter.Text = "Company Letter:";
            // 
            // txtCompanyLetter
            // 
            this.txtCompanyLetter.BackColor = System.Drawing.Color.White;
            this.txtCompanyLetter.Location = new System.Drawing.Point(172, 62);
            this.txtCompanyLetter.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtCompanyLetter.MaxLength = 1;
            this.txtCompanyLetter.Name = "txtCompanyLetter";
            this.txtCompanyLetter.ReadOnly = true;
            this.txtCompanyLetter.Size = new System.Drawing.Size(73, 26);
            this.txtCompanyLetter.TabIndex = 3;
            this.txtCompanyLetter.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblArrowType
            // 
            this.lblArrowType.AutoSize = true;
            this.lblArrowType.Location = new System.Drawing.Point(18, 112);
            this.lblArrowType.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblArrowType.Name = "lblArrowType";
            this.lblArrowType.Size = new System.Drawing.Size(92, 20);
            this.lblArrowType.TabIndex = 4;
            this.lblArrowType.Text = "Arrow Type:";
            // 
            // cmbArrowType
            // 
            this.cmbArrowType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbArrowType.FormattingEnabled = true;
            this.cmbArrowType.Location = new System.Drawing.Point(172, 108);
            this.cmbArrowType.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cmbArrowType.Name = "cmbArrowType";
            this.cmbArrowType.Size = new System.Drawing.Size(298, 28);
            this.cmbArrowType.TabIndex = 5;
            // 
            // lblProjectNumber
            // 
            this.lblProjectNumber.AutoSize = true;
            this.lblProjectNumber.Location = new System.Drawing.Point(18, 158);
            this.lblProjectNumber.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblProjectNumber.Name = "lblProjectNumber";
            this.lblProjectNumber.Size = new System.Drawing.Size(122, 20);
            this.lblProjectNumber.TabIndex = 6;
            this.lblProjectNumber.Text = "Project Number:";
            // 
            // txtProjectNumber
            // 
            this.txtProjectNumber.Location = new System.Drawing.Point(172, 153);
            this.txtProjectNumber.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtProjectNumber.Name = "txtProjectNumber";
            this.txtProjectNumber.Size = new System.Drawing.Size(298, 26);
            this.txtProjectNumber.TabIndex = 7;
            // 
            // lblJobNumber
            // 
            this.lblJobNumber.AutoSize = true;
            this.lblJobNumber.Location = new System.Drawing.Point(18, 202);
            this.lblJobNumber.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblJobNumber.Name = "lblJobNumber";
            this.lblJobNumber.Size = new System.Drawing.Size(99, 20);
            this.lblJobNumber.TabIndex = 8;
            this.lblJobNumber.Text = "Job Number:";
            // 
            // txtJobNumber
            // 
            this.txtJobNumber.Location = new System.Drawing.Point(172, 198);
            this.txtJobNumber.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtJobNumber.Name = "txtJobNumber";
            this.txtJobNumber.Size = new System.Drawing.Size(298, 26);
            this.txtJobNumber.TabIndex = 9;
            // 
            // lblSystemNumber
            // 
            this.lblSystemNumber.AutoSize = true;
            this.lblSystemNumber.Location = new System.Drawing.Point(18, 248);
            this.lblSystemNumber.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblSystemNumber.Name = "lblSystemNumber";
            this.lblSystemNumber.Size = new System.Drawing.Size(126, 20);
            this.lblSystemNumber.TabIndex = 10;
            this.lblSystemNumber.Text = "System Number:";
            // 
            // txtSystemNumber
            // 
            this.txtSystemNumber.Location = new System.Drawing.Point(172, 243);
            this.txtSystemNumber.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtSystemNumber.Name = "txtSystemNumber";
            this.txtSystemNumber.Size = new System.Drawing.Size(298, 26);
            this.txtSystemNumber.TabIndex = 11;
            // 
            // lblIDNumber
            // 
            this.lblIDNumber.AutoSize = true;
            this.lblIDNumber.Location = new System.Drawing.Point(18, 292);
            this.lblIDNumber.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblIDNumber.Name = "lblIDNumber";
            this.lblIDNumber.Size = new System.Drawing.Size(90, 20);
            this.lblIDNumber.TabIndex = 12;
            this.lblIDNumber.Text = "ID Number:";
            // 
            // txtIDNumber
            // 
            this.txtIDNumber.Location = new System.Drawing.Point(172, 288);
            this.txtIDNumber.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtIDNumber.Name = "txtIDNumber";
            this.txtIDNumber.Size = new System.Drawing.Size(298, 26);
            this.txtIDNumber.TabIndex = 13;
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Location = new System.Drawing.Point(18, 338);
            this.lblDescription.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(93, 20);
            this.lblDescription.TabIndex = 14;
            this.lblDescription.Text = "Description:";
            // 
            // txtDescription
            // 
            this.txtDescription.Location = new System.Drawing.Point(172, 333);
            this.txtDescription.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.Size = new System.Drawing.Size(298, 44);
            this.txtDescription.TabIndex = 15;
            // 
            // lblQuantity
            // 
            this.lblQuantity.AutoSize = true;
            this.lblQuantity.Location = new System.Drawing.Point(18, 397);
            this.lblQuantity.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblQuantity.Name = "lblQuantity";
            this.lblQuantity.Size = new System.Drawing.Size(72, 20);
            this.lblQuantity.TabIndex = 16;
            this.lblQuantity.Text = "Quantity:";
            // 
            // txtQuantity
            // 
            this.txtQuantity.Location = new System.Drawing.Point(172, 393);
            this.txtQuantity.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtQuantity.Name = "txtQuantity";
            this.txtQuantity.Size = new System.Drawing.Size(148, 26);
            this.txtQuantity.TabIndex = 17;
            // 
            // lblBlockScale
            // 
            this.lblBlockScale.AutoSize = true;
            this.lblBlockScale.Location = new System.Drawing.Point(18, 442);
            this.lblBlockScale.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblBlockScale.Name = "lblBlockScale";
            this.lblBlockScale.Size = new System.Drawing.Size(93, 20);
            this.lblBlockScale.TabIndex = 18;
            this.lblBlockScale.Text = "Block Scale:";
            // 
            // txtBlockScale
            // 
            this.txtBlockScale.Location = new System.Drawing.Point(172, 438);
            this.txtBlockScale.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtBlockScale.Name = "txtBlockScale";
            this.txtBlockScale.Size = new System.Drawing.Size(148, 26);
            this.txtBlockScale.TabIndex = 19;
            // 
            // picPreview
            // 
            this.picPreview.BackColor = System.Drawing.Color.White;
            this.picPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picPreview.Location = new System.Drawing.Point(546, 22);
            this.picPreview.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.picPreview.Name = "picPreview";
            this.picPreview.Size = new System.Drawing.Size(337, 340);
            this.picPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.picPreview.TabIndex = 18;
            this.picPreview.TabStop = false;
            // 
            // btnPlaceOnDrawing
            // 
            this.btnPlaceOnDrawing.Location = new System.Drawing.Point(410, 481);
            this.btnPlaceOnDrawing.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnPlaceOnDrawing.Name = "btnPlaceOnDrawing";
            this.btnPlaceOnDrawing.Size = new System.Drawing.Size(225, 45);
            this.btnPlaceOnDrawing.TabIndex = 20;
            this.btnPlaceOnDrawing.Text = "Place Block";
            this.btnPlaceOnDrawing.UseVisualStyleBackColor = true;
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(680, 481);
            this.btnClose.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(225, 45);
            this.btnClose.TabIndex = 21;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            // 
            // CreateShippingLabelForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(928, 564);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnPlaceOnDrawing);
            this.Controls.Add(this.picPreview);
            this.Controls.Add(this.txtBlockScale);
            this.Controls.Add(this.lblBlockScale);
            this.Controls.Add(this.txtQuantity);
            this.Controls.Add(this.lblQuantity);
            this.Controls.Add(this.txtDescription);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.txtIDNumber);
            this.Controls.Add(this.lblIDNumber);
            this.Controls.Add(this.txtSystemNumber);
            this.Controls.Add(this.lblSystemNumber);
            this.Controls.Add(this.txtJobNumber);
            this.Controls.Add(this.lblJobNumber);
            this.Controls.Add(this.txtProjectNumber);
            this.Controls.Add(this.lblProjectNumber);
            this.Controls.Add(this.cmbArrowType);
            this.Controls.Add(this.lblArrowType);
            this.Controls.Add(this.txtCompanyLetter);
            this.Controls.Add(this.lblCompanyLetter);
            this.Controls.Add(this.cmbCompanyNumber);
            this.Controls.Add(this.lblCompanyNumber);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(950, 620);
            this.Name = "CreateShippingLabelForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Create Shipping Label";
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblCompanyNumber;
        private System.Windows.Forms.ComboBox cmbCompanyNumber;
        private System.Windows.Forms.Label lblCompanyLetter;
        private System.Windows.Forms.TextBox txtCompanyLetter;
        private System.Windows.Forms.Label lblArrowType;
        private System.Windows.Forms.ComboBox cmbArrowType;
        private System.Windows.Forms.Label lblProjectNumber;
        private System.Windows.Forms.TextBox txtProjectNumber;
        private System.Windows.Forms.Label lblJobNumber;
        private System.Windows.Forms.TextBox txtJobNumber;
        private System.Windows.Forms.Label lblSystemNumber;
        private System.Windows.Forms.TextBox txtSystemNumber;
        private System.Windows.Forms.Label lblIDNumber;
        private System.Windows.Forms.TextBox txtIDNumber;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.Label lblQuantity;
        private System.Windows.Forms.TextBox txtQuantity;
        private System.Windows.Forms.Label lblBlockScale;
        private System.Windows.Forms.TextBox txtBlockScale;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.Button btnPlaceOnDrawing;
        private System.Windows.Forms.Button btnClose;
    }
} 