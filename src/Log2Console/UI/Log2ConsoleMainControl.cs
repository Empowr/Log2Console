using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.WindowsAPICodePack.Taskbar;

using ControlExtenders;

using Log2Console.Log;
using Log2Console.Receiver;
using Log2Console.Settings;
using Log2Console.UI;

using Timer = System.Threading.Timer;

// Configure log4net using the .config file
[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Log2Console.UI
{
    using System.ComponentModel;
    using System.Reflection;

    public partial class Log2ConsoleMainControl : UserControl, ILogMessageNotifiable
    {
        private Form _parrentForm;

        private bool _firstStartup;
    private bool _isWin7orLater;
    private  WindowRestorer _windowRestorer;

    private readonly DockExtender _dockExtender;
    private readonly IFloaty _logDetailsPanelFloaty;
    private readonly IFloaty _loggersPanelFloaty;

    private string _msgDetailText = String.Empty;
    private LoggerItem _lastHighlightedLogger;
    private LoggerItem _lastHighlightedLogMsgs;
    private bool _ignoreEvents;
    private bool _pauseLog;

    private Timer _taskbarProgressTimer;
    private const int _taskbarProgressTimerPeriod = 2000;
    private bool _addedLogMessage;
    private ThumbnailToolBarButton _pauseWinbarBtn;
    private ThumbnailToolBarButton _autoScrollWinbarBtn;
    private ThumbnailToolBarButton _clearAllWinbarBtn;

    private Queue<LogMessage> _eventQueue;
    private Timer _logMsgTimer;

    delegate void NotifyLogMsgCallback(LogMessage logMsg);
    delegate void NotifyLogMsgsCallback(LogMessage[] logMsgs);

    // Specific event handler on minimized action
    public event EventHandler Minimized;


    public Log2ConsoleMainControl()
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
       
    }
       
        public void Initialize(Form parrentForm = null)
        {
            _parrentForm = parrentForm;

            _firstStartup = !UserSettings.Load();

            Font = UserSettings.Instance.DefaultFont ?? Font;

            // Windows 7 CodePack (Taskbar icons and progress)
            _isWin7orLater = false; //TaskbarManager.IsPlatformSupported;

            if (_isWin7orLater)
            {
                try
                {
                    // Taskbar Progress
                    TaskbarManager.Instance.ApplicationId = Text;
                    _taskbarProgressTimer = new Timer(OnTaskbarProgressTimer, null, _taskbarProgressTimerPeriod, _taskbarProgressTimerPeriod);

                    // Pause Btn
                    _pauseWinbarBtn = new ThumbnailToolBarButton(Icon.FromHandle(((Bitmap)pauseBtn.Image).GetHicon()), pauseBtn.ToolTipText);
                    _pauseWinbarBtn.Click += pauseBtn_Click;

                    // Auto Scroll Btn
                    _autoScrollWinbarBtn =
                        new ThumbnailToolBarButton(Icon.FromHandle(((Bitmap)autoLogToggleBtn.Image).GetHicon()), autoLogToggleBtn.ToolTipText);
                    _autoScrollWinbarBtn.Click += autoLogToggleBtn_Click;

                    // Clear All Btn
                    _clearAllWinbarBtn =
                        new ThumbnailToolBarButton(Icon.FromHandle(((Bitmap)clearLoggersBtn.Image).GetHicon()), clearLoggersBtn.ToolTipText);
                    _clearAllWinbarBtn.Click += clearAll_Click;

                    // Add Btns
                    TaskbarManager.Instance.ThumbnailToolBars.AddButtons(Handle, _pauseWinbarBtn, _autoScrollWinbarBtn, _clearAllWinbarBtn);
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
            foreach (IReceiver receiver in UserSettings.Instance.Receivers)
                InitializeReceiver(receiver);

            // Start the timer to process event logs in batch mode
            _logMsgTimer = new Timer(OnLogMessageTimer, null, 1000, 100);

            if (_firstStartup)
            {
                // Initialize default layout
                if (parrentForm != null)
                {
                    UserSettings.Instance.Layout.Set(parrentForm.DesktopBounds, parrentForm.WindowState, logDetailPanel, loggerPanel);
                }

                // Force panel to visible
                UserSettings.Instance.Layout.ShowLogDetailView = true;
                UserSettings.Instance.Layout.ShowLoggerTree = true;
                UserSettings.Instance.DefaultFont = Environment.OSVersion.Version.Major >= 6 ? new Font("Segoe UI", 9F) : new Font("Tahoma", 8.25F);
            }

            if (parrentForm != null)
            {
                _windowRestorer = new WindowRestorer(parrentForm, UserSettings.Instance.Layout.WindowPosition,
                    UserSettings.Instance.Layout.WindowState);
            }

            if (parrentForm != null)
            {
                parrentForm.Shown += OnShown;
                parrentForm.Closing += OnFormClosing;
            }

            //logListView.VirtualMode = true;
            //logListView.VirtualListSize = size;
            //logListView.RetrieveVirtualItem += new RetrieveVirtualItemEventHandler(listView1_RetrieveVirtualItem);
            //logListView.CacheVirtualItems += new CacheVirtualItemsEventHandler(listView1_CacheVirtualItems);
            //logListView.SearchForVirtualItem += new SearchForVirtualItemEventHandler(listView1_SearchForVirtualItem);

            //var items = new ListViewItem.ListViewSubItem[UserSettings.Instance.ColumnConfiguration.Length];
            //for (int i = 0; i < items.Length; i++)
            //{
            //    items[i] = new ListViewItem.ListViewSubItem();
            //}
            //for (int i = 0; i < size; i++)
            //{

            //    myCache[i] = new ListViewItem(items, 0);
            //}
        }

        private const int size = 1000;
        private ListViewItem[] myCache = new ListViewItem[size];
        private int firstItem;

        //The basic VirtualMode function.  Dynamically returns a ListViewItem
        //with the required properties; in this case, the square of the index.
        void listView1_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            //Caching is not required but improves performance on large sets.
            //To leave out caching, don't connect the CacheVirtualItems event 
            //and make sure myCache is null.

            //check to see if the requested item is currently in the cache
            if (myCache != null && e.ItemIndex >= firstItem && e.ItemIndex < firstItem + myCache.Length)
            {
                //A cache hit, so get the ListViewItem from the cache instead of making a new one.
                e.Item = myCache[e.ItemIndex - firstItem];
            }
            else
            {
                //A cache miss, so create a new ListViewItem and pass it back.
                int x = e.ItemIndex * e.ItemIndex;
                e.Item = new ListViewItem(x.ToString());
            }
        }

        //Manages the cache.  ListView calls this when it might need a 
        //cache refresh.
        void listView1_CacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            //We've gotten a request to refresh the cache.
            //First check if it's really neccesary.
            if (myCache != null && e.StartIndex >= firstItem && e.EndIndex <= firstItem + myCache.Length)
            {
                //If the newly requested cache is a subset of the old cache, 
                //no need to rebuild everything, so do nothing.
                return;
            }

            //Now we need to rebuild the cache.
            firstItem = e.StartIndex;
            int length = e.EndIndex - e.StartIndex + 1; //indexes are inclusive
            myCache = new ListViewItem[length];

            //Fill the cache with the appropriate ListViewItems.
            int x = 0;
            for (int i = 0; i < length; i++)
            {
                x = (i + firstItem) * (i + firstItem);
                myCache[i] = new ListViewItem(x.ToString());
            }

        }

        //This event handler enables search functionality, and is called
        //for every search request when in Virtual mode.
        void listView1_SearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
        {
            //We've gotten a search request.
            //In this example, finding the item is easy since it's
            //just the square of its index.  We'll take the square root
            //and round.
            double x = 0;
            if (Double.TryParse(e.Text, out x)) //check if this is a valid search
            {
                x = Math.Sqrt(x);
                x = Math.Round(x);
                e.Index = (int)x;

            }
            //If e.Index is not set, the search returns null.
            //Note that this only handles simple searches over the entire
            //list, ignoring any other settings.  Handling Direction, StartIndex,
            //and the other properties of SearchForVirtualItemEventArgs is up
            //to this handler.
        }


        /// <summary>
        /// Catch on minimize event
        /// @author : Asbjørn Ulsberg -=|=- asbjornu@hotmail.com
        /// </summary>
        /// <param name="msg"></param>
        protected override void WndProc(ref Message msg)
    {
      const int WM_SIZE = 0x0005;
      const int SIZE_MINIMIZED = 1;

      if ((msg.Msg == WM_SIZE)
          && ((int)msg.WParam == SIZE_MINIMIZED)
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

    protected void OnShown(object sender, EventArgs eventArgs)
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

    protected void OnFormClosing(object sender, CancelEventArgs cancelEventArgs)
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

        if ((UserSettings.Instance.Layout.LogListViewColumnsWidths == null) ||
            (UserSettings.Instance.Layout.LogListViewColumnsWidths.Length != logListView.Columns.Count))
        {
          UserSettings.Instance.Layout.LogListViewColumnsWidths = new int[logListView.Columns.Count];
        }

        for (int i = 0; i < logListView.Columns.Count; i++)
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
            if (_parrentForm != null)
            {
                _parrentForm.Opacity = (double)UserSettings.Instance.Transparency / 100;
                _parrentForm.ShowInTaskbar = !UserSettings.Instance.HideTaskbarIcon;

                _parrentForm.TopMost = UserSettings.Instance.AlwaysOnTop;
            }
            pinOnTopBtn.Checked = UserSettings.Instance.AlwaysOnTop;
            autoLogToggleBtn.Checked = UserSettings.Instance.AutoScrollToLastLog;

            logListView.Font = UserSettings.Instance.LogListFont;
            logDetailTextBox.Font = UserSettings.Instance.LogDetailFont;
            loggerTreeView.Font = UserSettings.Instance.LoggerTreeFont;

            logListView.BackColor = UserSettings.Instance.LogListBackColor;

            LogLevels.Instance.LogLevelInfos[(int)LogLevel.Trace].Color = UserSettings.Instance.TraceLevelColor;
            LogLevels.Instance.LogLevelInfos[(int)LogLevel.Debug].Color = UserSettings.Instance.DebugLevelColor;
            LogLevels.Instance.LogLevelInfos[(int)LogLevel.Info].Color = UserSettings.Instance.InfoLevelColor;
            LogLevels.Instance.LogLevelInfos[(int)LogLevel.Warn].Color = UserSettings.Instance.WarnLevelColor;
            LogLevels.Instance.LogLevelInfos[(int)LogLevel.Error].Color = UserSettings.Instance.ErrorLevelColor;
            LogLevels.Instance.LogLevelInfos[(int)LogLevel.Fatal].Color = UserSettings.Instance.FatalLevelColor;

            levelComboBox.SelectedIndex = (int)UserSettings.Instance.LogLevelInfo.Level;

            if (logListView.ShowGroups != UserSettings.Instance.GroupLogMessages)
            {
                if (noCheck)
                {
                    logListView.ShowGroups = UserSettings.Instance.GroupLogMessages;
                }
                else
                {
                    DialogResult res = MessageBox.Show(
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

            //See if the Columns Changed
            bool columnsChanged = false;

            if (logListView.Columns.Count != UserSettings.Instance.ColumnConfiguration.Length)
                columnsChanged = true;
            else
                for (int i = 0; i < UserSettings.Instance.ColumnConfiguration.Length; i++)
                {
                    if (!UserSettings.Instance.ColumnConfiguration[i].Name.Equals(logListView.Columns[i].Text))
                    {
                        columnsChanged = true;
                        break;
                    }
                }

            if (columnsChanged)
            {
                logListView.Columns.Clear();
                foreach (var column in UserSettings.Instance.ColumnConfiguration)
                {
                    logListView.Columns.Add(column.Name);
                }
            }

            // Layout
            if (noCheck)
            {
                if (_parrentForm != null)
                {
                    _parrentForm.DesktopBounds = UserSettings.Instance.Layout.WindowPosition;
                    _parrentForm.WindowState = UserSettings.Instance.Layout.WindowState;
                }

                ShowDetailsPanel(UserSettings.Instance.Layout.ShowLogDetailView);
                logDetailPanel.Size = UserSettings.Instance.Layout.LogDetailViewSize;

                ShowLoggersPanel(UserSettings.Instance.Layout.ShowLoggerTree);
                loggerPanel.Size = UserSettings.Instance.Layout.LoggerTreeSize;

                if (UserSettings.Instance.Layout.LogListViewColumnsWidths != null)
                {
                    for (int i = 0; i < UserSettings.Instance.Layout.LogListViewColumnsWidths.Length; i++)
                    {
                        if (i < logListView.Columns.Count)
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
        catch { }

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
        if (_parrentForm != null)
        {
            _parrentForm.Close();
        }
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
      UserSettings copy = UserSettings.Instance.Clone();
      SettingsForm form = new SettingsForm(copy);
      if (form.ShowDialog(this) != DialogResult.OK)
        return;

      UserSettings.Instance = copy;
      UserSettings.Instance.Save();

      ApplySettings(false);
    }

    private void ShowReceiversForm()
    {
      ReceiversForm form = new ReceiversForm(UserSettings.Instance.Receivers);
      if (form.ShowDialog(this) != DialogResult.OK)
        return;

      foreach (IReceiver receiver in form.RemovedReceivers)
      {
        TerminateReceiver(receiver);
        UserSettings.Instance.Receivers.Remove(receiver);
      }

      foreach (IReceiver receiver in form.AddedReceivers)
      {
        UserSettings.Instance.Receivers.Add(receiver);
        InitializeReceiver(receiver);
      }

      UserSettings.Instance.Save();
    }


    private void ShowAboutForm()
    {
      AboutForm aboutBox = new AboutForm();
      aboutBox.ShowDialog(this);
    }

    private void RestoreWindow()
    {
      // Make the form visible and activate it. We need to bring the form
      // the front so the user will see it. Otherwise the user would have
      // to find it in the task bar and click on it.

      Visible = true;
        if (_parrentForm != null)
        {
            _parrentForm.Activate();
        }
        BringToFront();

        if (_parrentForm != null && _parrentForm.WindowState == FormWindowState.Minimized)
            _parrentForm.WindowState = _windowRestorer.WindowState;
    }

    #region ILogMessageNotifiable Members

    /// <summary>
    /// Transforms the notification into an asynchronous call.
    /// The actual method called to add log messages is 'AddLogMessages'.
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
    /// Transforms the notification into an asynchronous call.
    /// The actual method called to add a log message is 'AddLogMessage'.
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

    /// <summary>
    /// Adds a new log message, synchronously.
    /// </summary>
    private void AddLogMessages(IEnumerable<LogMessage> logMsgs)
    {
      if (_pauseLog)
        return;

      logListView.BeginUpdate();
      loggerTreeView.BeginUpdate();
      var temp = logListView.ListViewItemSorter;
      logListView.ListViewItemSorter = null;

      foreach (LogMessage msg in logMsgs)
        AddLogMessage(msg);

      logListView.ListViewItemSorter = temp;
      loggerTreeView.EndUpdate();
      logListView.EndUpdate();
      
    }

    /// <summary>
    /// Adds a new log message, synchronously.
    /// </summary>
    private void AddLogMessage(LogMessage logMsg)
    {
	    try
	    {
	      if (_pauseLog)
	        return;
	
	      RemovedLogMsgsHighlight();
	
	      _addedLogMessage = true;
	
	      LogManager.Instance.ProcessLogMessage(logMsg);
	
	      if (!Visible && UserSettings.Instance.NotifyNewLogWhenHidden)
	        ShowBalloonTip("A new message has been received...");
	    }
        catch (Exception ex)
        {
        }
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
          Invoke(d, new object[] { messages });
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
        logDetailTextBox.Text = string.Empty;
        _msgDetailText = String.Empty;
        PopulateExceptions(null);
        OpenSourceFile(null, 0);
      }
      else
      {
        StringBuilder sb = new StringBuilder();

        sb.Append("CallSiteClass: ");
        sb.AppendLine(logMsgItem.Message.CallSiteClass);
        sb.Append("CallSiteMethod: ");
        sb.AppendLine(logMsgItem.Message.CallSiteMethod);
        sb.Append("File: ");
        sb.AppendLine(logMsgItem.Message.SourceFileName);
        sb.Append("Line: ");
        sb.AppendLine(logMsgItem.Message.SourceFileLineNr.ToString());

        if (UserSettings.Instance.ShowMsgDetailsProperties)
        {
          // Append properties
          foreach (KeyValuePair<string, string> kvp in logMsgItem.Message.Properties)
            sb.AppendFormat("{0} = {1}{2}", kvp.Key, kvp.Value, Environment.NewLine);
        }

        // Append message
        sb.AppendLine(logMsgItem.Message.Message.Replace("\n", "\r\n"));

        // Append exception
        tbExceptions.Text = string.Empty;
        if (UserSettings.Instance.ShowMsgDetailsException &&
            !String.IsNullOrEmpty(logMsgItem.Message.ExceptionString))
        {
            //sb.AppendLine(logMsgItem.Message.ExceptionString);            
            if (!string.IsNullOrEmpty(logMsgItem.Message.ExceptionString))
            {
                PopulateExceptions(logMsgItem.Message.ExceptionString);
            }
        }

        _msgDetailText = sb.ToString();

        logDetailTextBox.ForeColor = logMsgItem.Message.Level.Color;
        logDetailTextBox.Text = _msgDetailText;

        OpenSourceFile(logMsgItem.Message.SourceFileName, logMsgItem.Message.SourceFileLineNr);
      }
    }

    private void OpenSourceFile(string fileName, uint line)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            textEditorSourceCode.Visible = false;
            lbFileName.Text = string.Empty;
            return;
        }

        textEditorSourceCode.Visible = true;
        try
        {
            //If the file cannot be found, try to locate it using the source code mapping configuration
            var mappedFile = TryToLocateSourceFile(fileName);
            if (string.IsNullOrEmpty(mappedFile))
            {
                textEditorSourceCode.Visible = false;
                lbFileName.Text = string.Format("Original file: {0} not found...", fileName);
                return;
            }

            if (!File.Exists(mappedFile))
            {
                textEditorSourceCode.Visible = false;
                lbFileName.Text = string.Format("Mapped file: {0} not found...", fileName);
                return;
            }                
            
            if (line > 1)
                line--;
            textEditorSourceCode.LoadFile(mappedFile);
            textEditorSourceCode.ActiveTextAreaControl.TextArea.Caret.Line = (int) line;
            textEditorSourceCode.ActiveTextAreaControl.TextArea.Caret.UpdateCaretPosition();
            lbFileName.Text = mappedFile + ":" + line;
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format("Message: {0}, Stack Trace: {1}", ex.Message, ex.StackTrace), "Error opening source file");
        }
    }

    private string TryToLocateSourceFile(string file)
    {
        //First see if there is a mapping for the file to a different location
        if (UserSettings.Instance.SourceLocationMapConfiguration != null)
            foreach (var sourceMap in UserSettings.Instance.SourceLocationMapConfiguration)
            {
                if(file.StartsWith(sourceMap.LogSource))
                {
                    file = sourceMap.LocalSource + file.Remove(0, sourceMap.LogSource.Length);                   
                    return file;
                }
            }

        //If not, then see if the original file exists
        return File.Exists(file) ? file : null;
    }

      private void PopulateExceptions(string exceptions)
    {
        if(string.IsNullOrEmpty(exceptions))
        {
            tbExceptions.Text = string.Empty;
            return;                
        }

        string[] lines = exceptions.Split(new[] {"\r\n", "\n"}, StringSplitOptions.None);
        foreach (var line in lines)
        {
            if (!ParseCSharpStackTraceLine(line))
            {
                //No supported exception stack traces is detected
                tbExceptions.SelectedText = line;
            }
            //else if (Add other Parsers Here...)

            tbExceptions.SelectedText = "\r\n";  
        }              
    }

    private bool ParseCSharpStackTraceLine(string line)
    {
        bool stackTraceFileDetected = false;

        //Detect a C Sharp File                
        int endOfFileIndex = line.ToLower().LastIndexOf(".cs");
        if (endOfFileIndex != -1)
        {
            var leftTruncatedFile = line.Substring(0, endOfFileIndex + 3);
            int startOfFileIndex = leftTruncatedFile.LastIndexOf(":") - 1;
            if (startOfFileIndex >= 0)
            {
                string fileName = leftTruncatedFile.Substring(startOfFileIndex, leftTruncatedFile.Length - startOfFileIndex);

                const string lineSignature = ":line ";
                int lineIndex = line.ToLower().LastIndexOf(lineSignature);
                if (lineIndex != -1)
                {
                    int lineSignatureLength = lineSignature.Length;
                    var lineNrString = line.Substring(lineIndex + lineSignatureLength,
                                                        line.Length - lineIndex - lineSignatureLength);
                    lineNrString = lineNrString.TrimEnd(new[] { ',' });
                    if (!string.IsNullOrEmpty(lineNrString))
                    {
                        uint parsedLineNr;
                        if (uint.TryParse(lineNrString, out parsedLineNr))
                        {
                            int fileLine = (int)parsedLineNr;
                            stackTraceFileDetected = true;

                            tbExceptions.SelectedText = line.Substring(0, startOfFileIndex - 1) + " ";
                            tbExceptions.InsertLink(string.Format("{0} line:{1}",
                                                            fileName, fileLine));
                        }
                    }
                }
            }
        }

        return stackTraceFileDetected; 
    }

    private void logDetailTextBox_TextChanged(object sender, EventArgs e)
    {
      // Disable Edition without making it Read Only (better rendering...), see above
      logDetailTextBox.Text = _msgDetailText;
    }

    private void clearBtn_Click(object sender, EventArgs e)
    {
        try
        {
            ClearLogMessages();
            UserSettings.Instance.Receivers.ForEach(receiver => receiver.Clear());
        }
        catch (Exception ex)
        {
            var message = $"Error clearing all receivers: {ex.Message}";
            Console.WriteLine(message);
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
      if (String.IsNullOrEmpty(logDetailTextBox.Text))
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
        if (_parrentForm != null && !_parrentForm.ShowInTaskbar)
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
        if (_parrentForm != null)
        {
            _parrentForm.TopMost = pinOnTopBtn.Checked;
        }
    }

    private static void ZoomControlFont(Control ctrl, bool zoomIn)
    {
      // Limit to a minimum size
      float newSize = Math.Max(0.5f, ctrl.Font.SizeInPoints + (zoomIn ? +1 : -1));
      ctrl.Font = new Font(ctrl.Font.FontFamily, newSize);
    }


    private void deleteLoggerTreeMenuItem_Click(object sender, EventArgs e)
    {
      LoggerItem logger = (LoggerItem)loggerTreeView.SelectedNode.Tag;

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
          logListView.BeginUpdate();
          loggerTreeView.BeginUpdate();

                UserSettings.Instance.LogLevelInfo =
            LogUtils.GetLogLevelInfo((LogLevel)levelComboBox.SelectedIndex);
        LogManager.Instance.UpdateLogLevel();

          logListView.Sorting = SortOrder.Ascending;
          logListView.Sort();
                logListView.EndUpdate();
          loggerTreeView.EndUpdate();

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

      pauseBtn.Image = _pauseLog ? Properties.Resources.Go16 : Properties.Resources.Pause16;
      pauseBtn.Checked = _pauseLog;

      if (_isWin7orLater)
      {
        _pauseWinbarBtn.Icon = Icon.FromHandle(((Bitmap)pauseBtn.Image).GetHicon());

        TaskbarManager.Instance.SetOverlayIcon(
            _pauseLog ? Icon.FromHandle(Properties.Resources.Pause16.GetHicon()) : null, String.Empty);
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
      UserSettings.Instance.AutoScrollToLastLog = !UserSettings.Instance.AutoScrollToLastLog;

      autoLogToggleBtn.Checked = UserSettings.Instance.AutoScrollToLastLog;
    }

    private void clearAll_Click(object sender, EventArgs e)
    {
      ClearAll();
    }


    /// <summary>
    /// Quick and dirty implementation of an export function...
    /// </summary>
    private void saveBtn_Click(object sender, EventArgs e)
    {
      SaveFileDialog dlg = new SaveFileDialog();
      if (dlg.ShowDialog(this) == DialogResult.Cancel)
        return;

      using (StreamWriter sw = new StreamWriter(dlg.FileName))
      {
        using (TextWriter ssw = TextWriter.Synchronized(sw))
        {
          foreach (ListViewItem lvi in logListView.Items)
          {
            string line =
                String.Format("{0}\t{1}\t{2}\t{3}\t{4}",
                    lvi.SubItems[0].Text, lvi.SubItems[1].Text, lvi.SubItems[2].Text, lvi.SubItems[3].Text, lvi.SubItems[4].Text);
            ssw.WriteLine(line);
          }
        }
      }
    }


    private void btnOpenFileInVS_Click(object sender, EventArgs e)
    {
        try
        {
            var processInfo = new ProcessStartInfo("devenv",
                                                   string.Format("/edit \"{0}\" /command \"Edit.Goto {1}\"",
                                                                 textEditorSourceCode.FileName, 0));
            var process = Process.Start(processInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error opening file in Visual Studio");
        }
    }

    private void TbExceptionsLinkClicked(object sender, LinkClickedEventArgs e)
    {
        string exception = e.LinkText;
        if (exception != null)
        {
            var exceptionPair = exception.Split(new[] {" line:"}, StringSplitOptions.None);
            if (exceptionPair.Length == 2)
            {
                int lineNr=0;
                int.TryParse(exceptionPair[1], out lineNr);

                OpenSourceFile(exceptionPair[0], (uint) lineNr);
                tabControlDetail.SelectedTab = tabSource;
            }                                   
        }
    }

    private void quickLoadBtn_Click(object sender, EventArgs e)
    {
        if (openFileDialog1.ShowDialog() == DialogResult.OK)
        {
            if (!File.Exists(openFileDialog1.FileName))
            {
                MessageBox.Show(string.Format("File: {0} does not exists", openFileDialog1.FileName),
                                "Error Opening Log File");
                return;
            }

            var fileReceivers = new List<IReceiver>();
            foreach (var receiver in UserSettings.Instance.Receivers)
            {
                if (receiver is CsvFileReceiver)
                    fileReceivers.Add(receiver);
            }

            var form = new ReceiversForm(fileReceivers, true);
            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            foreach (IReceiver receiver in form.AddedReceivers)
            {
                UserSettings.Instance.Receivers.Add(receiver);
                InitializeReceiver(receiver);
            }

            UserSettings.Instance.Save();

            var fileReceiver = form.SelectedReceiver as CsvFileReceiver;
            if (fileReceiver == null)
                return;

            fileReceiver.ShowFromBeginning = true;
            fileReceiver.FileToWatch = openFileDialog1.FileName;
            fileReceiver.Attach(this);

            /*
        var fileReceiver = new CsvFileReceiver();

        fileReceiver.FileToWatch = openFileDialog1.FileName;
        fileReceiver.ReadHeaderFromFile = true;
        fileReceiver.ShowFromBeginning = true;
    
        fileReceiver.Initialize();
        fileReceiver.Attach(this);
        */
        }
    }

        private async void queryBtn_Click(object sender, EventArgs e)
        {
            var queryRunners = UserSettings.Instance.Receivers.OfType<IQueryRunner>();
            var defaultQueryRunner = queryRunners.FirstOrDefault();
            if (defaultQueryRunner == null)
            {
                MessageBox.Show("No Query Runner Receivers Configured. Please ad a relevant receiver.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                receiversBtn_Click(sender, e);
                return;
            }

            try
            {
                await Task.Run(()=> defaultQueryRunner.RunQuery());
            }
            catch (Exception ex)
            {
                var message = $"Error running query: {ex.Message}";
                Console.WriteLine(message);
                MessageBox.Show(message, "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
