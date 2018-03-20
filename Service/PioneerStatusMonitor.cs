using NLog;
using PioneerController;
using System;
using System.Linq;
using System.Timers;
using Topshelf;

namespace Service
{
    class PioneerStatusMonitor : IDisposable
    {
        private readonly Config _config;
        private readonly PioneerAmp _pioneerController;
        private readonly Timer _keepAliveTimer;

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
            LogManager.GetCurrentClassLogger().Log(LogLevel.Debug, "KeepAlive Elapsed");
            var sendTime = DateTime.Now;
            do
            {
                System.Threading.Thread.Sleep(100);
            } while (!(sendTime.AddMilliseconds(new[] { _keepAliveTimer.Interval, 10000 }.Min()) > DateTime.Now || (_pioneerController.LastReceiveTime.GetValueOrDefault(DateTime.MinValue) > sendTime && _pioneerController.PowerOn != null)));

            if (_pioneerController.PowerOn.HasValue)
                _pioneerController.SetPowerState(_pioneerController.PowerOn.Value);
            else
                _pioneerController.RequestPowerState();
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
            LogManager.GetCurrentClassLogger().Log(LogLevel.Warn, $"Unhandled Power Event (eventcode:{pea.EventCode})");
            return true;
        }
    }
}
