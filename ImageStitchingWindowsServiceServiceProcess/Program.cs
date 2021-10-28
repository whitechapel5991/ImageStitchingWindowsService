using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;

namespace ImageStitchingWindowsServiceServiceProcess
{
    class Program
    {
        static void Main(string[] args)
        {
            var currentDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            var inDir1 = Path.Combine(currentDir, "in1");
            var inDir2 = Path.Combine(currentDir, "in2");
            var outDir = Path.Combine(currentDir, "out");

            ServiceBase.Run(new Service1(new List<string> { inDir1, inDir2 }, outDir));
        }
    }
}
