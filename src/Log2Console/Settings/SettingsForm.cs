using System.IO;
using System.Windows.Forms;


namespace Log2Console.Settings
{
  public partial class SettingsForm : Form
  {
    public SettingsForm(UserSettings userSettings)
    {
      InitializeComponent();

      Font = UserSettings.Instance.DefaultFont ?? Font;

      // UI Settings
      UserSettings = userSettings;
    }

    public UserSettings UserSettings
    {
      get { return settingsPropertyGrid.SelectedObject as UserSettings; }
      set
      {
        settingsPropertyGrid.SelectedObject = value;
      }
    }

    private void ImportConfigBtnClick(object sender, System.EventArgs e)
    {
        if (importConfigDialog.ShowDialog() == DialogResult.OK)
        {
            var configFile = importConfigDialog.FileName;
            if (string.IsNullOrEmpty(configFile) || !File.Exists(configFile))
            {
                MessageBox.Show("Could not import configuration file", "Error");
                return;
            }
            UserSettings.Load(configFile);
            UserSettings.Instance.Save();

            MessageBox.Show("Please press OK to restart Log2Console", "Restart Required", MessageBoxButtons.OK);
            Application.Exit();
        }
    }

    private void ExportConfigBtnClick(object sender, System.EventArgs e)
    {
        if (exportConfigDialog.ShowDialog() == DialogResult.OK)
        {
            var configFile = exportConfigDialog.FileName;
            if (string.IsNullOrEmpty(configFile))
            {
                MessageBox.Show("Could not export configuration file", "Error");
                return;
            }
            UserSettings.Instance.Save(configFile);
        }
    }

  }
}