using System.Windows.Forms;

namespace Log2Console.UI
{
    public class ToolStripControl<T> : ToolStripControlHost
        where T : Control, new()
    {
        public ToolStripControl() : base(new T())
        {
        }

        public T CoreControl
        {
            get { return Control as T; }
        }
    }
}