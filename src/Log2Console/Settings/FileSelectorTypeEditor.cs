using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Windows.Forms;

namespace Log2Console.Settings
{
    /// <summary>
    /// Customer UITypeEditor that pops up a
    /// file selector dialog
    /// </summary>
    public class FileSelectorTypeEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return context.Instance == null ? base.GetEditStyle(context) : UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (context.Instance == null)
                if (value != null) return value;

            var dlg = new OpenFileDialog
                          {
                              Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                              CheckFileExists = true,
                              Title = "Select LOG File"
                          };

            var filename = (string)value;
            if (!File.Exists(filename))
                filename = null;
            dlg.FileName = filename;

            using (dlg)
            {
                var res = dlg.ShowDialog();
                if (res == DialogResult.OK)
                {
                    filename = dlg.FileName;
                }
            }
            return filename;
        }
    }
}
