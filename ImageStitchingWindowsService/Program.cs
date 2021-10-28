using NLog.Config;
using NLog.Targets;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using Topshelf;
using System.Collections.Generic;
using ImageStitchingWindowsServiceTopshelf;

namespace ImageStitchingWindowsService
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var currentDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                var inDir1 = Path.Combine(currentDir, "in1");
                var inDir2 = Path.Combine(currentDir, "in2");
                var outDir = Path.Combine(currentDir, "out");

                var conf = new LoggingConfiguration();
                var fileTarget = new FileTarget()
                {
                    Name = "Default",
                    FileName = Path.Combine(currentDir, "log.txt"),
                    Layout = "${date} ${message} ${onexception:inner=${exception:format=toString}}"
                };
                conf.AddTarget(fileTarget);
                conf.AddRuleForAllLevels(fileTarget);

                var logFactory = new LogFactory(conf);

                HostFactory.Run(
                    hostConf => {
                        hostConf.Service<TopshelfService>(
                            s =>
                            {
                                s.ConstructUsing(() => new TopshelfService(new List<string> { inDir1, inDir2 }, outDir));
                                s.WhenStarted(serv => serv.Start());
                                s.WhenStopped(serv => serv.Stop());
                            }
                            ).UseNLog(logFactory);
                        hostConf.EnableServiceRecovery(
                            r => r.RestartService(1).RestartService(1));
                        hostConf.RunAsNetworkService();
                    });
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Application", ex.ToString(), EventLogEntryType.Error);
            }
        }
    }
}
