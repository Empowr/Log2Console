using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Log2Console
{
    using Log2Console.Settings;

    public partial class MainForm1 : Form
    {
        public MainForm1()
        {
            InitializeComponent();

           log2ConsoleMainControl1.Initialize(this);
        }
    }
}
