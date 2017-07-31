using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Windows.Forms;
using Log2Console.Log;
using Log2Console.Settings;

namespace Log2Console.Receiver
{   
    /// <summary>
    /// This receiver watch a given file, like a 'tail' program, with one log event by line.
    /// Ideally the log events should use the log4j XML Schema layout.
    /// </summary>
    [Serializable]
    [DisplayName("CSV Log File")]
    public class CsvFileReceiver : BaseReceiver
    {
        [NonSerialized]
        private FileSystemWatcher _fileWatcher;
        [NonSerialized]
        private StreamReader _fileReader;
        [NonSerialized]
        private string _filename;

        private string _fileToWatch = String.Empty;        
        private bool _showFromBeginning;
        private string _loggerName;

        [NonSerialized]
        private CsvUtils _csvUtils = new CsvUtils();

        private CsvConfiguration _csvConfig = new CsvConfiguration();

        [Category("Configuration")]
        [DisplayName("CSV Configuration")]
        [Browsable(true)]
        public CsvConfiguration CsvConfig
        {
            get { return _csvConfig; }
            set { _csvConfig = value; if (_csvUtils != null) _csvUtils.Config = value; }
        }

        [Category("Configuration")]
        [DisplayName("File to Watch")]
        [Editor(typeof(FileSelectorTypeEditor), typeof(UITypeEditor))]
        public string FileToWatch
        {
            get { return _fileToWatch; }
            set
            {
                if (String.Compare(_fileToWatch, value, true) == 0)
                    return;

                _fileToWatch = value;

                Restart();
            }
        }
        
        [Category("Configuration")]
        [DisplayName("Show from Beginning")]
        [Description("Show file contents from the beginning (not just newly appended lines)")]
        [DefaultValue(false)]
        public bool ShowFromBeginning
        {
            get { return _showFromBeginning; }
            set { _showFromBeginning = value; }
        }                       

        [Category("Behavior")]
        [DisplayName("Logger Name")]
        [Description("Append the given Name to the Logger Name. If left empty, the filename will be used.")]
        public string LoggerName
        {
            get { return _loggerName; }
            set
            {
                _loggerName = value;

                ComputeFullLoggerName();
            }
        }

            #region IReceiver Members

        [Browsable(false)]
        public override string SampleClientConfig
        {
            get
            {
                return
@"<target name=""CsvLog"" 
        xsi:type=""File"" 
        fileName=""${basedir}/Logs/log.csv""
        archiveFileName=""${basedir}/Logs/Archives/log_{#}_${date:format=yyyy-MM-d_HH}.txt""
        archiveEvery=""Hour""
        archiveNumbering=""Sequence""
        maxArchiveFiles=""30000""
        concurrentWrites=""true""
        keepFileOpen=""false""            
        >
    <layout xsi:type=""CSVLayout"">
    <column name =""sequence"" layout =""${counter}"" />
    <column name=""time"" layout=""${date:format=yyyy/MM/dd HH\:mm\:ss.fff}"" />
    <column name=""level"" layout=""${level}""/>
    <column name=""thread"" layout=""${threadid}""/>
    <column name=""class"" layout =""${callsite:className=true:methodName=false:fileName=false:includeSourcePath=false}"" />
    <column name=""method"" layout =""${callsite:className=false:methodName=true:fileName=false:includeSourcePath=false}"" />
    <column name=""message"" layout=""${message}"" />
    <column name=""exception"" layout=""${exception:format=Message,Type,StackTrace}"" />
    <column name=""file"" layout =""${callsite:className=false:methodName=false:fileName=true:includeSourcePath=true}"" />
    </layout>
</target>";
            }
        }

        public override void Initialize()
        {
            _csvUtils = new CsvUtils {Config = _csvConfig};

            if (String.IsNullOrEmpty(_fileToWatch))
                return;

            _fileReader =
                new StreamReader(new FileStream(_fileToWatch, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

            string path = Path.GetDirectoryName(_fileToWatch);
            _filename = Path.GetFileName(_fileToWatch);
            _fileWatcher = new FileSystemWatcher(path, _filename)
                               {NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size};
            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.EnableRaisingEvents = true;

            ComputeFullLoggerName();

            if (_csvConfig.ReadHeaderFromFile)
                _csvUtils.AutoConfigureHeader(_fileReader);

            if (!_showFromBeginning)
            {
                _fileReader.BaseStream.Seek(0, SeekOrigin.End);
                _fileReader.DiscardBufferedData();
            }
        }

        

        public override void Terminate()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Changed -= OnFileChanged;
                _fileWatcher = null;
            }

            if (_fileReader != null)
                _fileReader.Close();
            _fileReader = null;

        }
        
        public override void Attach(ILogMessageNotifiable notifiable)
        {
            base.Attach(notifiable);
            
            if (_showFromBeginning)
                ReadFile();
        }

        #endregion


        private void Restart()
        {
            Terminate();
            Initialize();
        }

        private void ComputeFullLoggerName()
        {
            DisplayName = String.IsNullOrEmpty(_loggerName)
                              ? String.Empty
                              : String.Format("Log File [{0}]", _loggerName);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
                return;

            ReadFile();
        }

        private void ReadFile()
        {
            if ((_fileReader == null))
                return;

            if (_fileReader.BaseStream.Position > _fileReader.BaseStream.Length)
            {
                _fileReader.BaseStream.Seek(0, SeekOrigin.Begin);
                _fileReader.DiscardBufferedData();
            }

            var logMsgs = _csvUtils.ReadLogStream(_fileReader);

            // Notify the UI with the set of messages
            Notifiable.Notify(logMsgs.ToArray());
        }

                       
    }
}
