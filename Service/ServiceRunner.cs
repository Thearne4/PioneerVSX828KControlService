﻿using NLog;
using System;
using System.Diagnostics;
using Topshelf;

namespace Service
{

    class ServiceRunner
    {

        static void Main()
        {
            var rc = HostFactory.Run(config =>
            {
                config.Service<PioneerStatusMonitor>(service =>
                {
                    service.ConstructUsing(name => new PioneerStatusMonitor(Config.FromConfig()));
                    service.WhenStarted(psm => psm.OnStart());
                    service.WhenStopped(psm => psm.OnStop());
                    service.WhenShutdown(psm => psm.OnShutdown());
                    service.WhenPowerEvent((psm, hc, pea) => psm.OnPowerEvent(hc, pea));
                });
                config.RunAsLocalSystem();

                config.SetDescription("TheArne4's Pioneer VSX828K Control service");
                config.SetDisplayName("TheArne4's Pioneer Control Service");
                config.SetServiceName("PioneerControlService");
            });

            var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
            Environment.ExitCode = exitCode;

            NLog.LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"Exitcode={(TopshelfExitCode)exitCode}");

            if (Debugger.IsAttached && (TopshelfExitCode)exitCode != TopshelfExitCode.Ok) Console.ReadLine();

        }

    }
}
