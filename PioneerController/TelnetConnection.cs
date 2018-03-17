using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LogLevel = NLog.LogLevel;

namespace PioneerController
{
    class TelnetConnection : IDisposable
    {
        private readonly TcpClient _socket;
        private Thread _readThread;
        private CancellationToken _readTaskCancellationToken = new CancellationToken();
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public int ReadTimeOutMs { get; set; } = 100;

        public byte[] MessagePrefix { get; set; } = { 0xD };
        public byte[] MessageSuffix { get; set; } = { 0xD, 0xA };

        public bool Connected => _socket?.Connected == true;

        public DateTime? LastSendTime { get; private set; } = null;
        public DateTime? LastReceiveTime { get; private set; } = null;

        public event EventHandler<ReceivedData> DataReceived;
        public event EventHandler Disconnected;

        public TelnetConnection(string hostname, int port, bool startRead = false)
        {
            _socket = new TcpClient(hostname, port);

            Logger.Info($"Created new Telnet Connection (hostname:{hostname} / port:{port} / Connected:{Connected}");

            if (startRead) StartReading();
        }

        public void StartReading()
        {
            Logger.Debug("Starting Reading");
            if (_readThread == null) _readThread = new Thread(ReadFromSocket);
            if (_readThread.ThreadState != ThreadState.Running) _readThread.Start();
        }

        private void ReadFromSocket()
        {
            try
            {
                while (Connected)
                {
                    if (_socket.Available > 0)
                    {
                        List<byte> receivedBytes = new List<byte>();
                        while (_socket.Available > 0)
                        {
                            int readByte = _socket.GetStream().ReadByte();
                            if (readByte == -1) break;
                            receivedBytes.Add((byte)readByte);
                        }
                        LastReceiveTime = DateTime.Now;
                        OnDataReceived(new ReceivedData(receivedBytes.ToArray()));
                    }
                    else
                    {
                        Thread.Sleep(ReadTimeOutMs);
                    }
                }
            }
            catch (ThreadAbortException) { } //Don't do anything
            catch (Exception ex) { Logger.Log(LogLevel.Error, ex, "Error during read of Telnet socket"); }
            finally
            {
                if (!Connected) OnDisconnected();
                Logger.Debug("Reading Stopped");
            }

        }

        public void Write(string cmd, bool noPrefixAndSuffix = false)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(cmd);
            Write(buffer, noPrefixAndSuffix);
        }
        public void Write(byte[] buffer, bool noPrefixAndSuffix = false)
        {
            if (!Connected) return;

            var bufferWithPreAndPost = MessagePrefix.Concat(buffer).Concat(MessageSuffix).ToArray();

            _socket.GetStream().Write(noPrefixAndSuffix ? buffer : bufferWithPreAndPost, 0, buffer.Length);
            LastSendTime = DateTime.Now;
        }

        public void Dispose()
        {
            _socket?.Dispose();
            _readThread.Abort();
            _readThread.Join();
        }

        protected virtual void OnDisconnected()
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        protected virtual void OnDataReceived(ReceivedData e)
        {
            DataReceived?.Invoke(this, e);
        }
    }
}
