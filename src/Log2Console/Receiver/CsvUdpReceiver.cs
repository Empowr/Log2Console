using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Log2Console.Receiver
{
    [Serializable]
    [DisplayName("CSV UDP (IP v4 and v6)")]
    public class CsvUdpReceiver : BaseReceiver
    {
        public enum DataFormatEnums
        {
            Log4jXml,
            Flat,
            CSV
        }

        [NonSerialized]
        private Thread _worker;
        [NonSerialized]
        private UdpClient _udpClient;
        [NonSerialized]
        private IPEndPoint _remoteEndPoint;

        private bool _ipv6;
        private int _port = 7071;
        private string _address = String.Empty;
        private int _bufferSize = 10000;

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
        [DisplayName("UDP Port Number")]
        [DefaultValue(7071)]
        public int Port
        {
            get { return _port; }
            set { _port = value; }
        }

        [Category("Configuration")]
        [DisplayName("Use IPv6 Addresses")]
        [DefaultValue(false)]
        public bool IpV6
        {
            get { return _ipv6; }
            set { _ipv6 = value; }
        }

        [Category("Configuration")]
        [DisplayName("Multicast Group Address (Optional)")]
        public string Address
        {
            get { return _address; }
            set { _address = value; }
        }

        [Category("Configuration")]
        [DisplayName("Receive Buffer Size")]
        public int BufferSize
        {
            get { return _bufferSize; }
            set { _bufferSize = value; }
        }

        [Category("Configuration")]
        [DisplayName("Data Format")]
        [DefaultValue(DataFormatEnums.Log4jXml)]
        public DataFormatEnums DataFormat
        {
            get { return _dataFormat; }
            set { _dataFormat = value; }
        }

        private DataFormatEnums _dataFormat = DataFormatEnums.Log4jXml;


        #region IReceiver Members

        [Browsable(false)]
        public override string SampleClientConfig
        {
            get
            {
                return
                    "Configuration for log4net:" + Environment.NewLine +
                    "<appender name=\"UdpAppender\" type=\"log4net.Appender.UdpAppender\">" + Environment.NewLine +
                    "    <remoteAddress value=\"localhost\" />" + Environment.NewLine +
                    "    <remotePort value=\"7071\" />" + Environment.NewLine +
                    "    <layout type=\"log4net.Layout.XmlLayoutSchemaLog4j\" />" + Environment.NewLine +
                    "</appender>";
            }
        }

        public override void Initialize()
        {
            _csvUtils = new CsvUtils {Config = _csvConfig};

            if ((_worker != null) && _worker.IsAlive)
                return;

            // Init connexion here, before starting the thread, to know the status now
            _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            _udpClient = _ipv6 ? new UdpClient(_port, AddressFamily.InterNetworkV6) : new UdpClient(_port);
            _udpClient.Client.ReceiveBufferSize = _bufferSize;
            if (!String.IsNullOrEmpty(_address))
                _udpClient.JoinMulticastGroup(IPAddress.Parse(_address));

            // We need a working thread
            _worker = new Thread(Start) {IsBackground = true};
            _worker.Start();
        }

        public override void Terminate()
        {
            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient = null;

                _remoteEndPoint = null;
            }

            if ((_worker != null) && _worker.IsAlive)
                _worker.Abort();
            _worker = null;
        }

        #endregion

        private MemoryStream _memoryStream;
        private StreamReader _streamReader;
        private StreamWriter _streamWriter;

        private void Start()
        {
            while ((_udpClient != null) && (_remoteEndPoint != null))
            {
                try
                {
                    using (_memoryStream = new MemoryStream())
                    {
                        _streamReader = new StreamReader(_memoryStream);
                        _streamWriter = new StreamWriter(_memoryStream);

                        //Block until the first packet is received
                        byte[] buffer = _udpClient.Receive(ref _remoteEndPoint);

                        //Get the rest of the packets until there is a timeout
                        while (true)
                        {
                            string loggingEvent = System.Text.Encoding.UTF8.GetString(buffer);
                            _streamWriter.WriteLine(loggingEvent);

                            var asyncResult = _udpClient.BeginReceive(null, null);
                            if (!asyncResult.AsyncWaitHandle.WaitOne(1000))
                                break;

                            buffer = _udpClient.EndReceive(asyncResult, ref _remoteEndPoint);
                        }

                        _streamWriter.Flush();
                        _memoryStream.Position = 0;

                        if (Notifiable == null)
                            continue;

                        var logMsgs = _csvUtils.ReadLogStream(_streamReader);

                        // Notify the UI with the set of messages
                        Notifiable.Notify(logMsgs.ToArray());
                        Console.WriteLine("Number of Messages: {0}", logMsgs.Count);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

    }
}
