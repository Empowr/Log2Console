using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ControlExtenders;
using log4net.Config;
using Log2Console.Log;
using Log2Console.Properties;
using Log2Console.Receiver;
using Log2Console.Settings;
using Log2Console.UI;
using Microsoft.WindowsAPICodePack.Taskbar;
using Timer = System.Threading.Timer;

// Configure log4net using the .config file

[assembly: XmlConfigurator(Watch = true)]

namespace Log2Console
{
    public partial class MainForm : Form, ILogMessageNotifiable
    {
        private const int _taskbarProgressTimerPeriod = 2000;
        private readonly ThumbnailToolbarButton _autoScrollWinbarBtn;
        private readonly ThumbnailToolbarButton _clearAllWinbarBtn;
        private readonly DockExtender _dockExtender;
        private readonly Queue<LogMessage> _eventQueue;
        private readonly bool _firstStartup;
        private readonly bool _isWin7orLater;
        private readonly IFloaty _logDetailsPanelFloaty;
        private readonly IFloaty _loggersPanelFloaty;
        private readonly ThumbnailToolbarButton _pauseWinbarBtn;
        private readonly WindowRestorer _windowRestorer;
        private bool _addedLogMessage;
        private bool _ignoreEvents;
        private LoggerItem _lastHighlightedLogger;
        private LoggerItem _lastHighlightedLogMsgs;
        private Timer _logMsgTimer;
        private string _msgDetailText = string.Empty;
        private bool _pauseLog;
        private Timer _taskbarProgressTimer;

        public MainForm()
        {
            InitializeComponent();

            appNotifyIcon.Text = AboutForm.AssemblyTitle;

            levelComboBox.SelectedIndex = 0;

            Minimized += OnMinimized;


            // Init Log Manager Singleton
            LogManager.Instance.Initialize(new TreeViewLoggerView(loggerTreeView), logListView);


            _dockExtender = new DockExtender(this);

            // Dockable Log Detail View
            _logDetailsPanelFloaty = _dockExtender.Attach(logDetailPanel, logDetailToolStrip, logDetailSplitter);
            _logDetailsPanelFloaty.DontHideHandle = true;
            _logDetailsPanelFloaty.Docking += OnFloatyDocking;

            // Dockable Logger Tree
            _loggersPanelFloaty = _dockExtender.Attach(loggerPanel, loggersToolStrip, loggerSplitter);
            _loggersPanelFloaty.DontHideHandle = true;
            _loggersPanelFloaty.Docking += OnFloatyDocking;

            // Settings
            _firstStartup = !UserSettings.Load();
            if (_firstStartup)
            {
                // Initialize default layout
                UserSettings.Instance.Layout.Set(DesktopBounds, WindowState, logDetailPanel, loggerPanel);

                // Force panel to visible
                UserSettings.Instance.Layout.ShowLogDetailView = true;
                UserSettings.Instance.Layout.ShowLoggerTree = true;
                UserSettings.Instance.DefaultFont = Environment.OSVersion.Version.Major >= 6
                    ? new Font("Segoe UI", 9F)
                    : new Font("Tahoma", 8.25F);
            }

            Font = UserSettings.Instance.DefaultFont ?? Font;

            _windowRestorer = new WindowRestorer(this, UserSettings.Instance.Layout.WindowPosition,
                UserSettings.Instance.Layout.WindowState);

            // Windows 7 CodePack (Taskbar icons and progress)
            _isWin7orLater = TaskbarManager.IsPlatformSupported;

            if (_isWin7orLater)
            {
                try
                {
                    // Taskbar Progress
                    TaskbarManager.Instance.ApplicationId = Text;
                    _taskbarProgressTimer = new Timer(OnTaskbarProgressTimer, null, _taskbarProgressTimerPeriod,
                        _taskbarProgressTimerPeriod);

                    // Pause Btn
                    _pauseWinbarBtn = new ThumbnailToolbarButton(Icon.FromHandle(((Bitmap) pauseBtn.Image).GetHicon()),
                        pauseBtn.ToolTipText);
                    _pauseWinbarBtn.Click += pauseBtn_Click;

                    // Auto Scroll Btn
                    _autoScrollWinbarBtn =
                        new ThumbnailToolbarButton(Icon.FromHandle(((Bitmap) autoLogToggleBtn.Image).GetHicon()),
                            autoLogToggleBtn.ToolTipText);
                    _autoScrollWinbarBtn.Click += autoLogToggleBtn_Click;

                    // Clear All Btn
                    _clearAllWinbarBtn =
                        new ThumbnailToolbarButton(Icon.FromHandle(((Bitmap) clearLoggersBtn.Image).GetHicon()),
                            clearLoggersBtn.ToolTipText);
                    _clearAllWinbarBtn.Click += clearAll_Click;

                    // Add Btns
                    TaskbarManager.Instance.ThumbnailToolbars.AddButtons(Handle, _pauseWinbarBtn, _autoScrollWinbarBtn,
                        _clearAllWinbarBtn);
                }
                catch (Exception)
                {
                    // Not running on Win 7?
                    _isWin7orLater = false;
                }
            }

            ApplySettings(true);

            _eventQueue = new Queue<LogMessage>();

            // Initialize Receivers
            foreach (var receiver in UserSettings.Instance.Receivers)
                InitializeReceiver(receiver);

            // Start the timer to process event logs in batch mode
            _logMsgTimer = new Timer(OnLogMessageTimer, null, 1000, 100);
        }

        // Specific event handler on minimized action
        public event EventHandler Minimized;

        /// <summary>
        ///     Catch on minimize event
        ///     @author : Asbj�rn Ulsberg -=|=- asbjornu@hotmail.com
        /// </summary>
        /// <param name="msg"></param>
        protected override void WndProc(ref Message msg)
        {
            const int WM_SIZE = 0x0005;
            const int SIZE_MINIMIZED = 1;

            if ((msg.Msg == WM_SIZE)
                && ((int) msg.WParam == SIZE_MINIMIZED)
                && (Minimized != null))
            {
                Minimized(this, EventArgs.Empty);
            }

            base.WndProc(ref msg);
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);

            if (_windowRestorer != null)
                _windowRestorer.TrackWindow();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (_windowRestorer != null)
                _windowRestorer.TrackWindow();
        }

        protected override void OnShown(EventArgs e)
        {
            if (_firstStartup)
            {
                MessageBox.Show(
                    this,
                    @"Welcome to Log2Console! You must configure some Receivers in order to use the tool.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

                ShowReceiversForm();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (_logMsgTimer != null)
                {
                    _logMsgTimer.Dispose();
                    _logMsgTimer = null;
                }

                if (_taskbarProgressTimer != null)
                {
                    _taskbarProgressTimer.Dispose();
                    _taskbarProgressTimer = null;
                }

                if (UserSettings.Instance.Layout.LogListViewColumnsWidths == null)
                {
                    UserSettings.Instance.Layout.LogListViewColumnsWidths = new int[logListView.Columns.Count];
                }

                for (var i = 0; i < logListView.Columns.Count; i++)
                {
                    UserSettings.Instance.Layout.LogListViewColumnsWidths[i] = logListView.Columns[i].Width;
                }

                UserSettings.Instance.Layout.Set(
                    _windowRestorer.WindowPosition, _windowRestorer.WindowState, logDetailPanel, loggerPanel);

                UserSettings.Instance.Save();
                UserSettings.Instance.Close();
            }
            catch (Exception)
            {
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            // Display Version
            versionLabel.Text = AboutForm.AssemblyTitle + @" v" + AboutForm.AssemblyVersion;

            DoubleBuffered = true;
            base.OnLoad(e);
        }

        private void OnFloatyDocking(object sender, EventArgs e)
        {
            // make sure the ZOrder remains intact
            logListView.BringToFront();
            BringToFront();
        }

        private void ApplySettings(bool noCheck)
        {
            Opacity = (double) UserSettings.Instance.Transparency/100;
            ShowInTaskbar = !UserSettings.Instance.HideTaskbarIcon;

            TopMost = UserSettings.Instance.AlwaysOnTop;
            pinOnTopBtn.Checked = UserSettings.Instance.AlwaysOnTop;
            autoLogToggleBtn.Checked = UserSettings.Instance.AutoScrollToLastLog;

            logListView.Font = UserSettings.Instance.LogListFont;
            logDetailTextBox.Font = UserSettings.Instance.LogDetailFont;
            loggerTreeView.Font = UserSettings.Instance.LoggerTreeFont;

            logListView.BackColor = UserSettings.Instance.LogListBackColor;

            LogLevels.Instance.LogLevelInfos[(int) LogLevel.Trace].Color = UserSettings.Instance.TraceLevelColor;
            LogLevels.Instance.LogLevelInfos[(int) LogLevel.Debug].Color = UserSettings.Instance.DebugLevelColor;
            LogLevels.Instance.LogLevelInfos[(int) LogLevel.Info].Color = UserSettings.Instance.InfoLevelColor;
            LogLevels.Instance.LogLevelInfos[(int) LogLevel.Warn].Color = UserSettings.Instance.WarnLevelColor;
            LogLevels.Instance.LogLevelInfos[(int) LogLevel.Error].Color = UserSettings.Instance.ErrorLevelColor;
            LogLevels.Instance.LogLevelInfos[(int) LogLevel.Fatal].Color = UserSettings.Instance.FatalLevelColor;

            levelComboBox.SelectedIndex = (int) UserSettings.Instance.LogLevelInfo.Level;

            if (logListView.ShowGroups != UserSettings.Instance.GroupLogMessages)
            {
                if (noCheck)
                {
                    logListView.ShowGroups = UserSettings.Instance.GroupLogMessages;
                }
                else
                {
                    var res = MessageBox.Show(
                        this,
                        @"You changed the Message Grouping setting, the Log Message List must be cleared, OK?",
                        Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

                    if (res == DialogResult.OK)
                    {
                        ClearAll();
                        logListView.ShowGroups = UserSettings.Instance.GroupLogMessages;
                    }
                    else
                    {
                        UserSettings.Instance.GroupLogMessages = !UserSettings.Instance.GroupLogMessages;
                    }
                }
            }

            // Layout
            if (noCheck)
            {
                DesktopBounds = UserSettings.Instance.Layout.WindowPosition;
                WindowState = UserSettings.Instance.Layout.WindowState;

                ShowDetailsPanel(UserSettings.Instance.Layout.ShowLogDetailView);
                logDetailPanel.Size = UserSettings.Instance.Layout.LogDetailViewSize;

                ShowLoggersPanel(UserSettings.Instance.Layout.ShowLoggerTree);
                loggerPanel.Size = UserSettings.Instance.Layout.LoggerTreeSize;

                if (UserSettings.Instance.Layout.LogListViewColumnsWidths != null)
                {
                    for (var i = 0; i < UserSettings.Instance.Layout.LogListViewColumnsWidths.Length; i++)
                    {
                        logListView.Columns[i].Width = UserSettings.Instance.Layout.LogListViewColumnsWidths[i];
                    }
                }
            }
        }

        private void InitializeReceiver(IReceiver receiver)
        {
            try
            {
                receiver.Initialize();
                receiver.Attach(this);

                //LogManager.Instance.SetRootLoggerName(String.Format("Root [{0}]", receiver));
            }
            catch (Exception ex)
            {
                try
                {
                    receiver.Terminate();
                }
                catch
                {
                }

                ShowErrorBox("Failed to Initialize Receiver: " + ex.Message);
            }
        }

        private void TerminateReceiver(IReceiver receiver)
        {
            try
            {
                receiver.Detach();
                receiver.Terminate();
            }
            catch (Exception ex)
            {
                ShowErrorBox("Failed to Terminate Receiver: " + ex.Message);
            }
        }

        private void Quit()
        {
            Close();
        }

        private void ClearLogMessages()
        {
            SetLogMessageDetail(null);
            LogManager.Instance.ClearLogMessages();
        }

        private void ClearLoggers()
        {
            SetLogMessageDetail(null);
            LogManager.Instance.ClearAll();
        }

        private void ClearAll()
        {
            ClearLogMessages();
            ClearLoggers();
        }

        protected void ShowBalloonTip(string msg)
        {
            appNotifyIcon.BalloonTipTitle = AboutForm.AssemblyTitle;
            appNotifyIcon.BalloonTipText = msg;
            appNotifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            appNotifyIcon.ShowBalloonTip(3000);
        }

        private void ShowErrorBox(string msg)
        {
            MessageBox.Show(this, msg, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowSettingsForm()
        {
            // Make a copy of the settings in case the user cancels.
            var copy = UserSettings.Instance.Clone();
            var form = new SettingsForm(copy);
            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            UserSettings.Instance = copy;
            UserSettings.Instance.Save();

            ApplySettings(false);
        }

        private void ShowReceiversForm()
        {
            var form = new ReceiversForm(UserSettings.Instance.Receivers);
            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            foreach (var receiver in form.RemovedReceivers)
            {
                TerminateReceiver(receiver);
                UserSettings.Instance.Receivers.Remove(receiver);
            }

            foreach (var receiver in form.AddedReceivers)
            {
                UserSettings.Instance.Receivers.Add(receiver);
                InitializeReceiver(receiver);
            }

            UserSettings.Instance.Save();
        }

        private void ShowAboutForm()
        {
            var aboutBox = new AboutForm();
            aboutBox.ShowDialog(this);
        }

        private void RestoreWindow()
        {
            // Make the form visible and activate it. We need to bring the form
            // the front so the user will see it. Otherwise the user would have
            // to find it in the task bar and click on it.

            Visible = true;
            Activate();
            BringToFront();

            if (WindowState == FormWindowState.Minimized)
                WindowState = _windowRestorer.WindowState;
        }

        /// <summary>
        ///     Adds a new log message, synchronously.
        /// </summary>
        private void AddLogMessages(IEnumerable<LogMessage> logMsgs)
        {
            if (_pauseLog)
                return;

            logListView.BeginUpdate();

            foreach (var msg in logMsgs)
                AddLogMessage(msg);

            logListView.EndUpdate();
        }

        /// <summary>
        ///     Adds a new log message, synchronously.
        /// </summary>
        private void AddLogMessage(LogMessage logMsg)
        {
            if (_pauseLog)
                return;

            RemovedLogMsgsHighlight();

            _addedLogMessage = true;

            LogManager.Instance.ProcessLogMessage(logMsg);

            if (!Visible && UserSettings.Instance.NotifyNewLogWhenHidden)
                ShowBalloonTip("A new message has been received...");
        }

        private void OnLogMessageTimer(object sender)
        {
            LogMessage[] messages;

            lock (_eventQueue)
            {
                // Do a local copy to minimize the lock
                messages = _eventQueue.ToArray();
                _eventQueue.Clear();
            }

            // Process logs if any
            if (messages.Length > 0)
            {
                // InvokeRequired required compares the thread ID of the
                // calling thread to the thread ID of the creating thread.
                // If these threads are different, it returns true.
                if (logListView.InvokeRequired)
                {
                    NotifyLogMsgsCallback d = AddLogMessages;
                    Invoke(d, new object[] {messages});
                }
                else
                {
                    AddLogMessages(messages);
                }
            }
        }

        private void OnTaskbarProgressTimer(object o)
        {
            if (_isWin7orLater)
            {
                TaskbarManager.Instance.SetProgressState(_addedLogMessage
                    ? TaskbarProgressBarState.Indeterminate
                    : TaskbarProgressBarState.NoProgress);
            }
            _addedLogMessage = false;
        }

        private void quitBtn_Click(object sender, EventArgs e)
        {
            try
            {
                Quit();
            }
            catch (Exception)
            {
                Environment.Exit(0);
            }
        }

        private void logListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            RemovedLoggerHighlight();

            LogMessageItem logMsgItem = null;
            if (logListView.SelectedItems.Count > 0)
                logMsgItem = logListView.SelectedItems[0].Tag as LogMessageItem;

            SetLogMessageDetail(logMsgItem);

            // Highlight Logger in the Tree View
            if ((logMsgItem != null) && (UserSettings.Instance.HighlightLogger))
            {
                logMsgItem.Parent.Highlight = true;
                _lastHighlightedLogger = logMsgItem.Parent;
            }
        }

        private void SetLogMessageDetail(LogMessageItem logMsgItem)
        {
            // Store the text to avoid editing without settings the control
            // as readonly... kind of ugly trick...

            if (logMsgItem == null)
            {
                _msgDetailText = string.Empty;
            }
            else
            {
                var sb = new StringBuilder();

                if (UserSettings.Instance.ShowMsgDetailsProperties)
                {
                    // Append properties
                    foreach (var kvp in logMsgItem.Message.Properties)
                        sb.AppendFormat("{0} = {1}{2}", kvp.Key, kvp.Value, Environment.NewLine);
                }

                // Append message
                sb.AppendLine(logMsgItem.Message.Message.Replace("\n", "\r\n"));

                // Append exception
                if (UserSettings.Instance.ShowMsgDetailsException &&
                    !string.IsNullOrEmpty(logMsgItem.Message.ExceptionString))
                {
                    sb.AppendLine(logMsgItem.Message.ExceptionString);
                }

                _msgDetailText = sb.ToString();

                logDetailTextBox.ForeColor = logMsgItem.Message.Level.Color;
            }

            logDetailTextBox.Text = _msgDetailText;
        }

        private void logDetailTextBox_TextChanged(object sender, EventArgs e)
        {
            // Disable Edition without making it Read Only (better rendering...), see above
            logDetailTextBox.Text = _msgDetailText;
        }

        private void clearBtn_Click(object sender, EventArgs e)
        {
            ClearLogMessages();
        }

        private void closeLoggersPanelBtn_Click(object sender, EventArgs e)
        {
            ShowLoggersPanel(false);
        }

        private void loggersPanelToggleBtn_Click(object sender, EventArgs e)
        {
            // Toggle check state
            ShowLoggersPanel(!loggersPanelToggleBtn.Checked);
        }

        private void ShowLoggersPanel(bool show)
        {
            loggersPanelToggleBtn.Checked = show;

            if (show)
                _dockExtender.Show(loggerPanel);
            else
                _dockExtender.Hide(loggerPanel);
        }

        private void clearLoggersBtn_Click(object sender, EventArgs e)
        {
            ClearLoggers();
        }

        private void closeLogDetailPanelBtn_Click(object sender, EventArgs e)
        {
            ShowDetailsPanel(false);
        }

        private void logDetailsPanelToggleBtn_Click(object sender, EventArgs e)
        {
            // Toggle check state
            ShowDetailsPanel(!logDetailsPanelToggleBtn.Checked);
        }

        private void ShowDetailsPanel(bool show)
        {
            logDetailsPanelToggleBtn.Checked = show;

            if (show)
                _dockExtender.Show(logDetailPanel);
            else
                _dockExtender.Hide(logDetailPanel);
        }

        private void copyLogDetailBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(logDetailTextBox.Text))
                return;

            Clipboard.SetText(logDetailTextBox.Text);
        }

        private void aboutBtn_Click(object sender, EventArgs e)
        {
            ShowAboutForm();
        }

        private void settingsBtn_Click(object sender, EventArgs e)
        {
            ShowSettingsForm();
        }

        private void receiversBtn_Click(object sender, EventArgs e)
        {
            ShowReceiversForm();
        }

        private void appNotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            RestoreWindow();
        }

        private void OnMinimized(object sender, EventArgs e)
        {
            if (!ShowInTaskbar)
                Visible = false;
        }

        private void restoreTrayMenuItem_Click(object sender, EventArgs e)
        {
            RestoreWindow();
        }

        private void settingsTrayMenuItem_Click(object sender, EventArgs e)
        {
            ShowSettingsForm();
        }

        private void aboutTrayMenuItem_Click(object sender, EventArgs e)
        {
            ShowAboutForm();
        }

        private void exitTrayMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Quit();
            }
            catch (Exception)
            {
                Environment.Exit(0);
            }
        }

        private void searchTextBox_TextChanged(object sender, EventArgs e)
        {
            using (new AutoWaitCursor())
            {
                LogManager.Instance.SearchText(searchTextBox.Text);
            }
        }

        private void zoomOutLogListBtn_Click(object sender, EventArgs e)
        {
            ZoomControlFont(logListView, false);
        }

        private void zoomInLogListBtn_Click(object sender, EventArgs e)
        {
            ZoomControlFont(logListView, true);
        }

        private void zoomOutLogDetailsBtn_Click(object sender, EventArgs e)
        {
            ZoomControlFont(logDetailTextBox, false);
        }

        private void zoomInLogDetailsBtn_Click(object sender, EventArgs e)
        {
            ZoomControlFont(logDetailTextBox, true);
        }

        private void pinOnTopBtn_Click(object sender, EventArgs e)
        {
            // Toggle check state
            pinOnTopBtn.Checked = !pinOnTopBtn.Checked;

            // Save and apply setting
            UserSettings.Instance.AlwaysOnTop = pinOnTopBtn.Checked;
            TopMost = pinOnTopBtn.Checked;
        }

        private static void ZoomControlFont(Control ctrl, bool zoomIn)
        {
            // Limit to a minimum size
            var newSize = Math.Max(0.5f, ctrl.Font.SizeInPoints + (zoomIn ? +1 : -1));
            ctrl.Font = new Font(ctrl.Font.FontFamily, newSize);
        }

        private void deleteLoggerTreeMenuItem_Click(object sender, EventArgs e)
        {
            var logger = (LoggerItem) loggerTreeView.SelectedNode.Tag;

            if (logger != null)
            {
                logger.Remove();
            }
        }

        private void deleteAllLoggerTreeMenuItem_Click(object sender, EventArgs e)
        {
            ClearAll();
        }

        private void loggerTreeView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Select the clicked node
                loggerTreeView.SelectedNode = loggerTreeView.GetNodeAt(e.X, e.Y);

                deleteLoggerTreeMenuItem.Enabled = (loggerTreeView.SelectedNode != null);

                loggerTreeContextMenuStrip.Show(loggerTreeView, e.Location);
            }
        }

        private void loggerTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            // If we are suppose to ignore events right now, then just return.
            if (_ignoreEvents)
                return;

            // Set a flag to ignore future events while processing this event. We have
            // to do this because it may be possbile that this event gets fired again
            // during a recursive call. To avoid more processing than necessary, we should
            // set a flag and clear it when we're done.
            _ignoreEvents = true;

            using (new AutoWaitCursor())
            {
                try
                {
                    // Enable/disable the logger item that is represented by the checked node.
                    (e.Node.Tag as LoggerItem).Enabled = e.Node.Checked;
                }
                finally
                {
                    _ignoreEvents = false;
                }
            }
        }

        private void levelComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!IsHandleCreated)
                return;

            using (new AutoWaitCursor())
            {
                UserSettings.Instance.LogLevelInfo =
                    LogUtils.GetLogLevelInfo((LogLevel) levelComboBox.SelectedIndex);
                LogManager.Instance.UpdateLogLevel();
            }
        }

        private void loggerTreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if ((e.Node == null) || ((e.Node.Tag as LoggerItem) == null))
                return;

            if (UserSettings.Instance.HighlightLogMessages)
            {
                _lastHighlightedLogMsgs = e.Node.Tag as LoggerItem;
                _lastHighlightedLogMsgs.HighlightLogMessages = true;
            }
        }

        private void loggerTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            RemovedLogMsgsHighlight();
        }

        private void RemovedLogMsgsHighlight()
        {
            if (_lastHighlightedLogMsgs != null)
            {
                _lastHighlightedLogMsgs.HighlightLogMessages = false;
                _lastHighlightedLogMsgs = null;
            }
        }

        private void RemovedLoggerHighlight()
        {
            if (_lastHighlightedLogger != null)
            {
                _lastHighlightedLogger.Highlight = false;
                _lastHighlightedLogger = null;
            }
        }

        private void pauseBtn_Click(object sender, EventArgs e)
        {
            _pauseLog = !_pauseLog;

            pauseBtn.Image = _pauseLog ? Resources.Go16 : Resources.Pause16;
            pauseBtn.Checked = _pauseLog;

            if (_isWin7orLater)
            {
                _pauseWinbarBtn.Icon = Icon.FromHandle(((Bitmap) pauseBtn.Image).GetHicon());

                TaskbarManager.Instance.SetOverlayIcon(
                    _pauseLog ? Icon.FromHandle(Resources.Pause16.GetHicon()) : null, string.Empty);
            }
        }

        private void goToFirstLogBtn_Click(object sender, EventArgs e)
        {
            if (logListView.Items.Count == 0)
                return;

            logListView.Items[0].EnsureVisible();
        }

        private void goToLastLogBtn_Click(object sender, EventArgs e)
        {
            if (logListView.Items.Count == 0)
                return;

            logListView.Items[logListView.Items.Count - 1].EnsureVisible();
        }

        private void autoLogToggleBtn_Click(object sender, EventArgs e)
        {
            autoLogToggleBtn.Checked = !autoLogToggleBtn.Checked;
            UserSettings.Instance.AutoScrollToLastLog = autoLogToggleBtn.Checked;
        }

        private void clearAll_Click(object sender, EventArgs e)
        {
            ClearAll();
        }

        /// <summary>
        ///     Quick and dirty implementation of an export function...
        /// </summary>
        private void saveBtn_Click(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog();
            if (dlg.ShowDialog(this) == DialogResult.Cancel)
                return;

            using (var sw = new StreamWriter(dlg.FileName))
            {
                using (var ssw = TextWriter.Synchronized(sw))
                {
                    foreach (ListViewItem lvi in logListView.Items)
                    {
                        var line =
                            string.Format("{0}\t{1}\t{2}\t{3}\t{4}",
                                lvi.SubItems[0].Text, lvi.SubItems[1].Text, lvi.SubItems[2].Text, lvi.SubItems[3].Text,
                                lvi.SubItems[4].Text);
                        ssw.WriteLine(line);
                    }
                }
            }
        }

        private delegate void NotifyLogMsgCallback(LogMessage logMsg);

        private delegate void NotifyLogMsgsCallback(LogMessage[] logMsgs);

        #region ILogMessageNotifiable Members

        /// <summary>
        ///     Transforms the notification into an asynchronous call.
        ///     The actual method called to add log messages is 'AddLogMessages'.
        /// </summary>
        public void Notify(LogMessage[] logMsgs)
        {
            //// InvokeRequired required compares the thread ID of the
            //// calling thread to the thread ID of the creating thread.
            //// If these threads are different, it returns true.
            //if (logListView.InvokeRequired)
            //{
            //    NotifyLogMsgsCallback d = AddLogMessages;
            //    Invoke(d, new object[] { logMsgs });
            //}
            //else
            //{
            //    AddLogMessages(logMsgs);
            //}

            lock (_eventQueue)
            {
                foreach (var logMessage in logMsgs)
                {
                    _eventQueue.Enqueue(logMessage);
                }
            }
        }

        /// <summary>
        ///     Transforms the notification into an asynchronous call.
        ///     The actual method called to add a log message is 'AddLogMessage'.
        /// </summary>
        public void Notify(LogMessage logMsg)
        {
            //// InvokeRequired required compares the thread ID of the
            //// calling thread to the thread ID of the creating thread.
            //// If these threads are different, it returns true.
            //if (logListView.InvokeRequired)
            //{
            //    NotifyLogMsgCallback d = AddLogMessage;
            //    Invoke(d, new object[] { logMsg });
            //}
            //else
            //{
            //    AddLogMessage(logMsg);
            //}

            lock (_eventQueue)
            {
                _eventQueue.Enqueue(logMsg);
            }
        }

        #endregion
    }
}