namespace Log2Console.Settings
{
    partial class SettingsForm
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
            this.cancelBtn = new System.Windows.Forms.Button();
            this.okBtn = new System.Windows.Forms.Button();
            this.settingsPropertyGrid = new System.Windows.Forms.PropertyGrid();
            this.importConfigBtn = new System.Windows.Forms.Button();
            this.exportConfigBtn = new System.Windows.Forms.Button();
            this.importConfigDialog = new System.Windows.Forms.OpenFileDialog();
            this.exportConfigDialog = new System.Windows.Forms.SaveFileDialog();
            this.SuspendLayout();
            // 
            // cancelBtn
            // 
            this.cancelBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelBtn.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelBtn.Location = new System.Drawing.Point(473, 515);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(75, 23);
            this.cancelBtn.TabIndex = 0;
            this.cancelBtn.Text = "Cancel";
            this.cancelBtn.UseVisualStyleBackColor = true;
            // 
            // okBtn
            // 
            this.okBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okBtn.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okBtn.Location = new System.Drawing.Point(392, 515);
            this.okBtn.Name = "okBtn";
            this.okBtn.Size = new System.Drawing.Size(75, 23);
            this.okBtn.TabIndex = 0;
            this.okBtn.Text = "OK";
            this.okBtn.UseVisualStyleBackColor = true;
            // 
            // settingsPropertyGrid
            // 
            this.settingsPropertyGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.settingsPropertyGrid.Location = new System.Drawing.Point(12, 12);
            this.settingsPropertyGrid.Name = "settingsPropertyGrid";
            this.settingsPropertyGrid.Size = new System.Drawing.Size(536, 497);
            this.settingsPropertyGrid.TabIndex = 1;
            this.settingsPropertyGrid.ToolbarVisible = false;
            // 
            // importConfigBtn
            // 
            this.importConfigBtn.Location = new System.Drawing.Point(12, 515);
            this.importConfigBtn.Name = "importConfigBtn";
            this.importConfigBtn.Size = new System.Drawing.Size(86, 23);
            this.importConfigBtn.TabIndex = 2;
            this.importConfigBtn.Text = "Import Config";
            this.importConfigBtn.UseVisualStyleBackColor = true;
            this.importConfigBtn.Click += new System.EventHandler(this.ImportConfigBtnClick);
            // 
            // exportConfigBtn
            // 
            this.exportConfigBtn.Location = new System.Drawing.Point(104, 515);
            this.exportConfigBtn.Name = "exportConfigBtn";
            this.exportConfigBtn.Size = new System.Drawing.Size(86, 23);
            this.exportConfigBtn.TabIndex = 3;
            this.exportConfigBtn.Text = "Export Config";
            this.exportConfigBtn.UseVisualStyleBackColor = true;
            this.exportConfigBtn.Click += new System.EventHandler(this.ExportConfigBtnClick);
            // 
            // importConfigDialog
            // 
            this.importConfigDialog.DefaultExt = "*.log2";
            this.importConfigDialog.FileName = "openFileDialog1";
            this.importConfigDialog.Filter = "Log2Console Configuration|*.log2|All files|*.*";
            this.importConfigDialog.Title = "Import Configuration";
            // 
            // exportConfigDialog
            // 
            this.exportConfigDialog.DefaultExt = "*.log2";
            this.exportConfigDialog.FileName = "Config.log2";
            this.exportConfigDialog.Filter = "Log2Console Configuration|*.log2|All files|*.*";
            this.exportConfigDialog.Title = "Import Configuration";
            // 
            // SettingsForm
            // 
            this.AcceptButton = this.okBtn;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.cancelBtn;
            this.ClientSize = new System.Drawing.Size(560, 550);
            this.Controls.Add(this.exportConfigBtn);
            this.Controls.Add(this.importConfigBtn);
            this.Controls.Add(this.settingsPropertyGrid);
            this.Controls.Add(this.okBtn);
            this.Controls.Add(this.cancelBtn);
            this.Name = "SettingsForm";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.Text = "Log2Console Settings";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.Button okBtn;
        private System.Windows.Forms.PropertyGrid settingsPropertyGrid;
        private System.Windows.Forms.Button importConfigBtn;
        private System.Windows.Forms.Button exportConfigBtn;
        private System.Windows.Forms.OpenFileDialog importConfigDialog;
        private System.Windows.Forms.SaveFileDialog exportConfigDialog;
    }
}