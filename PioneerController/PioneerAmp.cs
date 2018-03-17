using PioneerController.Annotations;
using PioneerController.Enums;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace PioneerController
{
    public class PioneerAmp : INotifyPropertyChanged, IDisposable
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private TelnetConnection _connection;

        private bool? _powerOn;
        private int? _volumePioneer;
        private bool? _mute;
        private InputSource? _inputSource;
        private ListeningMode? _listeningModeSet;
        private ListeningMode? _listeningMode;
        private int? _bass;
        private int? _treble;

        public DateTime? LastSendTime => _connection?.LastSendTime;
        public DateTime? LastReceiveTime => _connection?.LastReceiveTime;
        public DateTime? LastConfirmationReceived { get; private set; } = null;

        public bool Connecting { get; private set; }

        /// <summary>
        /// The Powerstate of the Pioneer Amp
        /// </summary>
        public bool? PowerOn
        {
            get => _powerOn;
            private set { _powerOn = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// The Volume in dB of the Pioneer Amp
        /// </summary>
        public float? VolumeDb => (VolumePioneer - 161) / 2;

        /// <summary>
        /// The volume in the format read on the Pioneer Amp
        /// </summary>
        public float? VolumeOnDevice => (VolumePioneer - 1) / 2;

        /// <summary>
        /// The volume in pioneers volume format
        /// </summary>
        public int? VolumePioneer
        {
            get => _volumePioneer;
            private set { _volumePioneer = value; OnPropertyChanged(); OnPropertyChanged(nameof(VolumeDb)); }
        }

        /// <summary>
        /// The Mute state of the Pioneer Amp
        /// </summary>
        public bool? Mute
        {
            get => _mute;
            private set { _mute = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// The currently selected source of the Pioneer Amp
        /// </summary>
        public InputSource? InputSource
        {
            get => _inputSource;
            private set { _inputSource = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// The current listening mode set of the Pioneer Amp
        /// </summary>
        public ListeningMode? ListeningModeSet
        {
            get => _listeningModeSet;
            private set { _listeningModeSet = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// The current listening mode of the Pioneer Amp
        /// </summary>
        public ListeningMode? ListeningMode
        {
            get => _listeningMode;
            private set { _listeningMode = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// The current bass setting of the Pioneer Amp
        /// </summary>
        public int? Bass
        {
            get => _bass;
            private set { _bass = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// The current treble setting of the Pioneer Amp
        /// </summary>
        public int? Treble
        {
            get => _treble;
            private set { _treble = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public PioneerAmp(IPAddress ipAddress, int port) : this(ipAddress.ToString(), port) { }
        public PioneerAmp(string hostname, int port)
        {
            _connection = InitAndValidateConnection(hostname, port);// new TelnetConnection(hostname, port);

            CreateHandles(hostname, port);

            _connection.StartReading();
        }

        private TelnetConnection InitAndValidateConnection(string hostname, int port)
        {
            TelnetConnection connection;
            do
            {
                Connecting = true;
                connection = new TelnetConnection(hostname, port);
            } while (!connection.Connected);

            Connecting = false;
            return connection;
        }

        private void CreateHandles(string hostname, int port)
        {
            _connection.DataReceived += DataReceived;
            _connection.Disconnected += (s, e) =>
            {
                Logger.Info("Disconnected Received, Reconnecting");
                _connection.Dispose();
                _connection = InitAndValidateConnection(hostname, port);
                CreateHandles(hostname, port);
                _connection.StartReading();
            };
        }

        private void DataReceived(object sender, ReceivedData e)
        {
            if (e.Data.Length == 0) Logger.Debug("Device may be disconnected while reporting connected on tcp port");
            else ProcessReceivedData(e.Data);
        }

        private void ProcessReceivedData(byte[] data)
        {
            var stringData = Encoding.ASCII.GetString(data).Trim();

            Logger.Debug($"Received data from device {stringData}");

            foreach (var cmd in stringData.Split((char)0xA).Select((s) => s.Trim()))
            {
                if (string.IsNullOrEmpty(cmd)) continue;

                if (cmd == "R") LastConfirmationReceived = DateTime.Now;
                else if (cmd.StartsWith("PWR") && cmd.Length == 4)
                {
                    if (cmd[3] == '0') PowerOn = true;
                    else if (cmd[3] == '1' || cmd[3] == '2') PowerOn = false;
                    else PowerOn = null;
                }
                else if (cmd.StartsWith("VOL") && cmd.Length == 6)
                {
                    if (int.TryParse(cmd.Substring(3), out int vol)) VolumePioneer = vol;
                    else VolumePioneer = null;
                }
                else if (cmd.StartsWith("MUT") && cmd.Length == 4)
                {
                    if (cmd[3] == '0') Mute = true;
                    else if (cmd[3] == '1') Mute = false;
                    else Mute = null;
                }
                else if (cmd.StartsWith("FN") && cmd.Length == 4)
                {
                    if (int.TryParse(cmd.Substring(2), out int source)) InputSource = (InputSource)source;
                    else InputSource = null;
                }
                else if (cmd.StartsWith("SR"))//Need additional length info
                {
                    if (int.TryParse(cmd.Substring(2), out int listeningMdS)) ListeningModeSet = (ListeningMode)listeningMdS;
                    else InputSource = null;
                }
                else if (cmd.StartsWith("LM"))//Need additional length info
                {
                    if (int.TryParse(cmd.Substring(2), out int listeningMd)) ListeningMode = (ListeningMode)listeningMd;
                    else InputSource = null;
                }
                else if (cmd.StartsWith("BA"))//Need additional length info
                {
                    if (int.TryParse(cmd.Substring(3), out int bass)) Bass = bass - 6;
                    else Bass = null;
                }
                else if (cmd.StartsWith("TR"))//Need additional length info
                {
                    if (int.TryParse(cmd.Substring(3), out int treble)) Treble = treble - 6;
                    else Treble = null;
                }
                else
                {
                    Logger.Info("Unhandled command received: " + cmd);
                }
            }
        }

        private void SafeSend(string cmd, [CallerMemberName] string methodName = null)
        {
            if (_connection?.Connected != true) throw new AmpDisconnectedException();

            Logger.Debug($"Using SafeSend from {methodName} with value {cmd}");

            _connection.Write(cmd);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void WakeUpCpu()
        {
            SafeSend(string.Empty);
            System.Threading.Thread.Sleep(100);//Allow time for amp cpu to wake up
        }

        public void RequestPowerState() { SafeSend("?P"); }
        public void SetPowerState(bool on) { SafeSend(on ? "PO" : "PF"); }

        public void RequestVolumeLevel() { SafeSend("?V"); }
        public void ChangeVolume(bool up) { SafeSend(up ? "VU" : "VD"); }

        public void SetVolume(float db)
        {
            if (db < -80 || db > 12) throw new ArgumentOutOfRangeException(nameof(db), db, "Volume in db must be between -80 dB and 12 dB");

            int pioneerVolume = (int)(db * 2 + 161);
            SetVolume(pioneerVolume);
        }
        public void SetVolume(int pioneerVolume)
        {
            if (pioneerVolume < 0 || pioneerVolume > 185) throw new ArgumentOutOfRangeException(nameof(pioneerVolume), pioneerVolume, "Pioneer volume must be between 0 and 185");

            SafeSend(pioneerVolume.ToString().PadLeft(3, '0') + "VL");
        }

        public void RequestMuteState() { SafeSend("?M"); }
        public void SetMute(bool on) { SafeSend(on ? "MO" : "MF"); }

        public void RequestFunctionMode() { SafeSend("?F"); }

        public void RequestListeningMode() { SafeSend("?S"); } //Untested!

        public void RequestPlayingListeningMode() { SafeSend("?L"); } //Untested!

        public void RequestToneStatus() { SafeSend("?TO"); } //Untested!
        public void SetTone(bool on) { SafeSend(on ? "TO" : "TF"); } //Untested!

        public void SendCommand(string command) { SafeSend(command); }

        public void Dispose()
        {
            _connection?.Dispose();
        }

    }
}
