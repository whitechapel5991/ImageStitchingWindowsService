using FileStichingLayer;
using ImageStitchingWindowsService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ImageStitchingWindowsServiceServiceProcess
{
    partial class Service1 : ServiceBase
    {
        private readonly FileService fileService;

        public Service1(List<string> inputDirs, string outputDir)
        {
            InitializeComponent();
            fileService = new FileService(inputDirs, outputDir, new PdfStitchingService());
        }

        protected override void OnStart(string[] args)
        {
            fileService.WorkThreadList.ForEach(x => x.Start());
            fileService.FileSystemWatcherList.ForEach(x => x.EnableRaisingEvents = true);
        }

        protected override void OnStop()
        {
            fileService.FileSystemWatcherList.ForEach(x => x.EnableRaisingEvents = false);
            fileService.StopWorkEvent.Set();
            fileService.WorkThreadList.ForEach(x => x.Join());
        }
    }
}
