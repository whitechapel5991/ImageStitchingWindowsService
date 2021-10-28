using FileStichingLayer;
using ImageStitchingWindowsService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageStitchingWindowsServiceTopshelf
{
    internal class TopshelfService
    {
        private readonly FileService fileService;
        public TopshelfService(List<string> inputDirs, string outputDir)
        {
            fileService = new FileService(inputDirs, outputDir, new PdfStitchingService());
        }

        public void Start()
        {
            fileService.WorkThreadList.ForEach(x => x.Start());
            fileService.FileSystemWatcherList.ForEach(x => x.EnableRaisingEvents = true);
        }

        public void Stop()
        {
            fileService.FileSystemWatcherList.ForEach(x => x.EnableRaisingEvents = false);
            fileService.StopWorkEvent.Set();
            fileService.WorkThreadList.ForEach(x => x.Join());
        }
    }
}
