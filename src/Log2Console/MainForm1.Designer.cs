namespace Log2Console
{
    partial class MainForm1
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm1));
            this.log2ConsoleMainControl1 = new Log2Console.UI.Log2ConsoleMainControl();
            this.SuspendLayout();
            // 
            // log2ConsoleMainControl1
            // 
            this.log2ConsoleMainControl1.AutoSize = true;
            this.log2ConsoleMainControl1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.log2ConsoleMainControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.log2ConsoleMainControl1.Location = new System.Drawing.Point(0, 0);
            this.log2ConsoleMainControl1.Name = "log2ConsoleMainControl1";
            this.log2ConsoleMainControl1.Size = new System.Drawing.Size(1164, 658);
            this.log2ConsoleMainControl1.TabIndex = 0;
            // 
            // MainForm1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1164, 658);
            this.Controls.Add(this.log2ConsoleMainControl1);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm1";
            this.Text = "Log2Console";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private UI.Log2ConsoleMainControl log2ConsoleMainControl1;
    }
}