using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;

namespace TestApp
{
	/// <summary>
	/// Zusammenfassung für Form1.
	/// </summary>
	public class Form1 : System.Windows.Forms.Form
	{
		private RichTextBoxLinks.RichTextBoxEx richTextBoxEx1;
		/// <summary>
		/// Erforderliche Designervariable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public Form1()
		{
			//
			// Erforderlich für die Windows Form-Designerunterstützung
			//
			InitializeComponent();

			//
			// TODO: Fügen Sie den Konstruktorcode nach dem Aufruf von InitializeComponent hinzu
			//
		}

		/// <summary>
		/// Die verwendeten Ressourcen bereinigen.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Vom Windows Form-Designer generierter Code
		/// <summary>
		/// Erforderliche Methode für die Designerunterstützung. 
		/// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
		/// </summary>
		private void InitializeComponent()
		{
			this.richTextBoxEx1 = new RichTextBoxLinks.RichTextBoxEx();
			this.SuspendLayout();
			// 
			// richTextBoxEx1
			// 
			this.richTextBoxEx1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
				| System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right)));
			this.richTextBoxEx1.Location = new System.Drawing.Point(8, 8);
			this.richTextBoxEx1.Name = "richTextBoxEx1";
			this.richTextBoxEx1.Size = new System.Drawing.Size(328, 152);
			this.richTextBoxEx1.TabIndex = 0;
			this.richTextBoxEx1.Text = "";
			this.richTextBoxEx1.LinkClicked += new System.Windows.Forms.LinkClickedEventHandler(this.richTextBoxEx1_LinkClicked);
			// 
			// Form1
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(344, 166);
			this.Controls.Add(this.richTextBoxEx1);
			this.Name = "Form1";
			this.Text = "Form1";
			this.Load += new System.EventHandler(this.Form1_Load);
			this.ResumeLayout(false);

		}
		#endregion

		/// <summary>
		/// Der Haupteinstiegspunkt für die Anwendung.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			Application.Run(new Form1());
		}

		private void Form1_Load(object sender, System.EventArgs e)
		{
            richTextBoxEx1.SelectedText = "With this extended RichTextBox you're able to insert";
            richTextBoxEx1.SelectedText = "your own arbitrary links in the text: ";
            
			richTextBoxEx1.InsertLink("Link with arbitrary text");
            
			richTextBoxEx1.SelectedText = ".\n\nYou are not limited to the standard protocols any more,";
			richTextBoxEx1.SelectedText = "but you can still use them, of course: ";
			richTextBoxEx1.InsertLink("http://www.codeproject.com");
			richTextBoxEx1.SelectedText = "\n\nThe new links fire the LinkClicked event, just like the standard";
			richTextBoxEx1.SelectedText = "links do when AutoDetectUrls is set.\n\n";
			richTextBoxEx1.SelectedText = "Managing hyperlinks independent of link text is possible as well: ";
			richTextBoxEx1.InsertLink("Link text", "Hyperlink text");
		}

		private void richTextBoxEx1_LinkClicked(object sender, System.Windows.Forms.LinkClickedEventArgs e)
		{
			MessageBox.Show("A link has been clicked.\nThe link text is '"+e.LinkText+"'");
		}
	}
}
