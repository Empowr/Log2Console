using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Log2Console.Log;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LogLevel = Log2Console.Log.LogLevel;

namespace Log2Console.Receiver
{
    [Serializable]
    [DisplayName("Elastic Receiver")]
    public class ElasticReceiver : BaseReceiver, IQueryRunner
    {
        [NonSerialized] private Thread _worker;

        [NonSerialized]
        private ElasticClient client;

        [NonSerialized]
        private int count;

        [NonSerialized]
        private DateTime _newestItem;

        [Category("Debugging")]
        [DisplayName("Log Query")]
        [Description("Add a log entry defining the Elastic Query that used")]
        [DefaultValue(false)]
        public bool LogQuery { get; set; }

        [Category("Server")]
        [DisplayName("Node URI")]
        [DefaultValue("http://localhost:9200")]
        public string NodeUri { get; set; }

        [Category("Configuration")]
        [DisplayName("Index")]
        [DefaultValue("logstash-*")]
        public string Index { get; set; } = "logstash-*";

        [Category("Configuration")]
        [DisplayName("Size")]
        [DefaultValue(500)]
        public int Size { get; set; } = 500;

        [Category("Continuous Mode")]
        [DisplayName("Refresh Period")]
        [DefaultValue(5000)]
        public int RefreshPeriod { get; set; } = 5000;

        [Category("Continuous Mode")]
        [DisplayName("Run Query Continuously")]
        [DefaultValue(false)]
        public bool RunQueryContinuously { get; set; } = false;

        [Category("Configuration")]
        [DisplayName("Search Type")]
        [DefaultValue("logevent")]
        public string SearchType { get; set; } = "logevent";

        [Category("Configuration")]
        [DisplayName("RawQuery")]
        public string RawQuery { get; set; }

        [Category("Time Range")]
        [DisplayName("For Recent TimeSpan")]
        public string ForRecentTime { get; set; }

        [Category("Time Range")]
        [DisplayName("Oldest Time")]
        public string OldestTime { get; set; }

        [Category("Time Range")]
        [DisplayName("Newest Time")]
        public string NewestTime { get; set; }

        [XmlIgnore]
        [NonSerialized]
        private HashSet<string> _items1 = new HashSet<string>();

        public ElasticReceiver()
        {
            _items1 = new HashSet<string>();
        }

        public override void Clear()
        {
            count = 0;
            _newestItem = DateTime.MinValue;
            _items1.Clear();
        }

        private void Start()
        {
            while (_worker.IsAlive)
            {
                try
                {
                    RunQueryImplementation(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error running Elastic Query: {ex}");
                }
                Thread.Sleep(RefreshPeriod);
            }
        }

        #region IReceiver Members

        [Browsable(false)]
        public override string SampleClientConfig => "";

        protected void RunQueryImplementation(bool fromWorkerThread = false)
        {
            var oldestItems = new List<DateTime> {DateTime.MinValue};

            if (DateTime.TryParse(OldestTime, DateTimeFormatInfo.CurrentInfo, DateTimeStyles.AssumeLocal, out var oldestTime))
                oldestItems.Add(oldestTime);
            
            if (TimeSpan.TryParse(ForRecentTime, out var recentTimeSpan1))
            {
                oldestItems.Add(DateTime.Now.Subtract(recentTimeSpan1));
            }

            if (fromWorkerThread)
                oldestItems.Add(_newestItem);

            var oldestItem = oldestItems.Max();

            var newestItems = new List<DateTime> {DateTime.Now};
            if(DateTime.TryParse(NewestTime, DateTimeFormatInfo.CurrentInfo, DateTimeStyles.AssumeLocal, out var newestTime))
                newestItems.Add(newestTime);

            var newestItem = newestItems.Min();

            var useFilter = oldestItem != DateTime.MinValue || newestItem != DateTime.MaxValue;

            var query = new SearchDescriptor<JObject>()
                    .When(!string.IsNullOrEmpty(Index), s => s.Index(Index), s => s.Index(Indices.AllIndices))
                    .When(!string.IsNullOrEmpty(SearchType), s => s.Type(SearchType), s => s.AllTypes())
                    .Index(new IndexName[] {Index})
                    //.When(TimeSpan.TryParse(ForRecentTime, out var recentTimeSpan), s => s
                    //    .Query(descriptor => descriptor.DateRange(
                    //        queryDescriptor => queryDescriptor
                    //            .Field("@timestamp")
                    //            .GreaterThan(DateMath.Now.Subtract(recentTimeSpan))
                    //            .LessThan(DateMath.Now))))
                    .When(useFilter, s => s
                        .Query(descriptor => descriptor.DateRange(
                            queryDescriptor => queryDescriptor
                                .Field("@timestamp")
                                .GreaterThan(oldestItem)
                                .LessThan(newestItem))))
                    .When(!string.IsNullOrEmpty(RawQuery), s => s
                        .Query(q => q.Raw(RawQuery)))
                    .When(Size > 0, s => s.Size(Size))
                    //.MatchAll()
                    .Sort(sort => sort
                        .Descending("@timestamp"))
                ;

            var search = client.Search<JObject>(query);

            if (LogQuery && !fromWorkerThread)
            {
                var stream = new System.IO.MemoryStream();
                client.Serializer.Serialize(query, stream);
                var jsonQuery = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                var logMsg = new[]{new LogMessage
                {
                    Level = LogUtils.GetLogLevelInfo(LogLevel.Info),
                    TimeStamp = DateTime.Now,
                    LoggerName = "ElasticReceiver",
                    CallSiteClass = "ElasticReceiver",
                    Message = $"Invoked Elastic Query:\n{search.ApiCall.Uri}\n{jsonQuery}"

                }};
                Notifiable.Notify(logMsg);
            }

            //var queryString = JsonConvert.SerializeObject(query);

            if (search == null) return;

            foreach (var item in search.HitsMetaData.Hits)
            {
                if (Notifiable == null)
                    continue;
                
                if (_items1.Contains(item.Id)) continue;
                _items1.Add(item.Id);

                var logMsg = Parse(item);
                if (logMsg.TimeStamp > _newestItem)
                    _newestItem = logMsg.TimeStamp;
                Notifiable.Notify(logMsg);
            }
        }

        public void RunQuery()
        {
            RunQueryImplementation(false);
        }

        public override void Initialize()
        {
            if (_worker != null && _worker.IsAlive)
                return;

            _items1 = new HashSet<string>();
            _newestItem = DateTime.MinValue;

            // Init connexion here, before starting the thread, to know the status now
            var node = new Uri(NodeUri);
            var settings = new ConnectionSettings(node);
            client = new ElasticClient(settings);
            settings.DefaultIndex(Index);

            // We need a working thread
            if (!RunQueryContinuously) return;
            _worker = new Thread(Start) {IsBackground = true};
            _worker.Start();
        }

        public override void Terminate()
        {
            if (_worker?.IsAlive == true)
                _worker?.Abort();
            _worker = null;
        }

        #endregion

        public static LogMessage Parse(IHit<JObject> item)
        {
            try
            {
                var log = new LogMessage();

                var properties =
                    new Dictionary<string, string>
                    {
                        {"ID", item.Id},
                        {"Index", item.Index},
                        {"Document Type", item.Type}
                    };

                foreach (var field in item.Source)
                {
                    switch (field.Key)
                    {
                        case "@timestamp":
                            log.TimeStamp = field.Value.Value<DateTime>().ToLocalTime();  
                            break;
                        case "Message":
                            log.Message = field.Value.Value<string>();
                            break;
                        case "Class":
                            log.LoggerName = field.Value.Value<string>();
                            log.CallSiteClass = field.Value.Value<string>();
                            break;
                        case "Method":
                            log.CallSiteMethod = field.Value.Value<string>();
                            break;
                        case "File":
                            log.SourceFileName = field.Value.Value<string>();
                            break;
                        case "level":
                            log.Level = LogLevels.Instance[field.Value.Value<string>()];
                            break;
                        case "Thread":
                            log.ThreadName = field.Value.Value<string>(); ;
                            break;
                        case "Exceptions":
                            log.ExceptionString = field.Value.Value<string>();
                            break;
                        case "exception":
                            var className = field.Value["ClassName"].Value<string>();
                            var message = field.Value["Message"].Value<string>();
                            var stackTrace = field.Value["StackTraceString"].Value<string>();
                            log.ExceptionString = $"{className} {message}\n{stackTrace}";
                            break;
                        
                        default:
                            try
                            {
                                properties.Add(field.Key, field.Value.ToString()); //Value<string>());
                            }
                            catch (Exception ex)
                            {
                            }
                            
                            break;

                    }
                }                
                log.Properties = properties;

                return log;
            }
            catch (Exception ex)
            {
                return new LogMessage
                {
                    Message = "Error in Elastic Receiver",
                    ExceptionString = ex.ToString()
                };
            }            
        }        
    }

    public static class SearchExtensions
    {
        public static SearchDescriptor<T> When<T>(this SearchDescriptor<T> search,
            bool predicate,
            Func<SearchDescriptor<T>, SearchDescriptor<T>> action, Func<SearchDescriptor<T>, SearchDescriptor<T>> actionElse = null) where T : class
        {
            return predicate ? action(search) : actionElse?.Invoke(search) ?? search;
        }
    }
}