using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Log2Console.Log;

namespace Log2Console.Receiver
{
  [Serializable]
  [DisplayName("CSV TCP (IP v4 and v6)")]
  public class CsvTcpReceiver : BaseReceiver
  {
    #region Port Property


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

    int _port = 4505;
    [Category("Configuration")]
    [DisplayName("TCP Port Number")]
    [DefaultValue(4505)]
    public int Port
    {
      get { return _port; }
      set { _port = value; }
    }

    #endregion

    #region IpV6 Property

    bool _ipv6;
    [Category("Configuration")]
    [DisplayName("Use IPv6 Addresses")]
    [DefaultValue(false)]
    public bool IpV6
    {
      get { return _ipv6; }
      set { _ipv6 = value; }
    }

    private int _bufferSize = 10000;
    [Category("Configuration")]
    [DisplayName("Receive Buffer Size")]
    [DefaultValue(10000)]
    public int BufferSize
    {
        get { return _bufferSize; }
        set { _bufferSize = value; }
    }

    #endregion

    #region IReceiver Members

    [Browsable(false)]
    public override string SampleClientConfig
    {
      get
      {
        return
            "Configuration for NLog:" + Environment.NewLine +
            "<target name=\"TcpOutlet\" xsi:type=\"NLogViewer\" address=\"tcp://localhost:4505\"/>";
      }
    }

    [NonSerialized]
    Socket _socket;

    public override void Initialize()
    {
      _csvUtils = new CsvUtils { Config = _csvConfig };

      if (_socket != null) return;

      _socket = new Socket(_ipv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
      _socket.ExclusiveAddressUse = true;
      _socket.Bind(new IPEndPoint(_ipv6 ? IPAddress.IPv6Any : IPAddress.Any, _port));
      _socket.Listen(100);
      _socket.ReceiveBufferSize = _bufferSize;

      var args = new SocketAsyncEventArgs();
      args.Completed += AcceptAsyncCompleted;

      _socket.AcceptAsync(args);
    }

    void AcceptAsyncCompleted(object sender, SocketAsyncEventArgs e)
    {
      if (_socket == null || e.SocketError != SocketError.Success) return;

      new Thread(Start) { IsBackground = true }.Start(e.AcceptSocket);

      e.AcceptSocket = null;
      _socket.AcceptAsync(e);
    }

    void Start(object newSocket)
    {
        int count = 0;
        bool readHeader = true;
        try
        {           
            using (var socket = (Socket) newSocket)
            using (var ns = new NetworkStream(socket, FileAccess.Read, false))
            using (var streamReader = new StreamReader(ns))
                while (_socket != null)
                {
                    if (_csvConfig.ReadHeaderFromFile && readHeader)
                    {
                        try
                        {
                            _csvUtils.AutoConfigureHeader(streamReader);
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine("Error Reading Header {0}", ex.Message);
                        }
                        finally
                        {
                            readHeader = false;
                        }
                    }

                    var logMsgs = _csvUtils.ReadLogStream(streamReader);
                    if(logMsgs.Count == 0)
                        return;

                    // Notify the UI with the set of messages
                    Notifiable.Notify(logMsgs.ToArray());
                    count += logMsgs.Count;
                }
        }
        catch (IOException)
        {
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            Console.WriteLine("Number of Messages: {0}", count); 
        }
    }

    public override void Terminate()
    {
      if (_socket == null) return;

      _socket.Close();
      _socket = null;
    }

    #endregion
  }
}
