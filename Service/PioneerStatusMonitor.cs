using PioneerController;
using System;
using System.Linq;
using System.Timers;
using Topshelf;

namespace Service
{
    class PioneerStatusMonitor : IDisposable
    {
        private PioneerAmp _pioneerController;
        private readonly object _restartLock = new object();
        private readonly Config _config;
        private readonly Timer _keepAliveTimer;

        private readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public PioneerStatusMonitor(Config config)
        {
            _config = config;
            _config.FillBlanksWithDefault();

            _pioneerController = new PioneerAmp(_config.IpAddress, _config.Port);

            _keepAliveTimer = new Timer(_config.KeepAliveTime ?? 1800000) { AutoReset = true };
            _keepAliveTimer.Elapsed += KeepAliveTimerElapsed;

            if (_config.KeepAlive == true) _keepAliveTimer.Start();
        }

        private void KeepAliveTimerElapsed(object sender, ElapsedEventArgs e)
        {
            KeepAlive();
        }

        private void KeepAlive()
        {
            _logger.Log(NLog.LogLevel.Debug, "KeepAlive Elapsed");

            lock (_restartLock)
                if (_pioneerController?.Port != null && _pioneerController.FirstUnconfirmedSendTime != null && _pioneerController.FirstUnconfirmedSendTime.Value.AddSeconds(10) < DateTime.Now)
                {
                    _logger.Debug("suspect not receiving anything. Resetting Device...");
                    var hostname = _pioneerController.Hostname;
                    var port = _pioneerController.Port.Value;
                    _pioneerController.Dispose();
                    _pioneerController = new PioneerAmp(hostname, port);
                    OnStart();
                }

            var sendTime = DateTime.Now;
            do
            {
                System.Threading.Thread.Sleep(100);
            } while (!(sendTime.AddMilliseconds(new[] { _keepAliveTimer.Interval, 10000 }.Min()) > DateTime.Now || (_pioneerController?.LastReceiveTime.GetValueOrDefault(DateTime.MinValue) > sendTime && _pioneerController.PowerOn != null)));

            if (_pioneerController?.PowerOn != null)
                _pioneerController.SetPowerState(_pioneerController.PowerOn.Value);
            else
                _pioneerController?.RequestPowerState();

        }

        public void Dispose()
        {
            _pioneerController.Dispose();
            _keepAliveTimer?.Dispose();
        }

        public void OnStart()
        {
            if (_config.KeepAlive == true && !_keepAliveTimer.Enabled) _keepAliveTimer.Start();
            if (_config.TurnOnOnStart == true) _pioneerController.SetPowerState(true);
            if (_config.TurnOffOnStart == true) _pioneerController.SetPowerState(false);
        }

        public void OnStop()
        {
            _keepAliveTimer.Stop();
            if (_config.TurnOnOnStop == true) _pioneerController.SetPowerState(true);
            if (_config.TurnOffOnStop == true) _pioneerController.SetPowerState(false);
        }

        public void OnShutdown()
        {
            if (_config.TurnOnOnShutdown == true) _pioneerController.SetPowerState(true);
            if (_config.TurnOffOnShutdown == true) _pioneerController.SetPowerState(false);
        }

        public bool OnPowerEvent(HostControl hc, PowerEventArguments pea)
        {
            _logger.Log(NLog.LogLevel.Warn, $"Unhandled Power Event (eventcode:{pea.EventCode})");
            return true;
        }

        public void OnPause()
        {
            if (_config.TurnOnOnPause==true) _pioneerController.SetPowerState(true);
            if (_config.TurnOffOnPause==true) _pioneerController.SetPowerState(false);
        }

        public void OnContinue()
        {
            if(_config.TurnOnOnContinue==true) _pioneerController.SetPowerState(true);
            if(_config.TurnOffOnContinue==true) _pioneerController.SetPowerState(false);
        }
    }
}
