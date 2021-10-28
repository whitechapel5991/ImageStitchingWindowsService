using FileStichingLayer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ImageStitchingWindowsService
{
    public class FileService
    {
        const int FileAppearanceTimeoutMs = 10000;
        private readonly List<string> inputDirList;
        private readonly string outputDir;
        private readonly string tempDir;
        private readonly Dictionary<string, AutoResetEvent> fileEventDictionary = new Dictionary<string, AutoResetEvent>();
        private readonly IStitchingService stitchingService;

        public ManualResetEvent StopWorkEvent { get; private set; }
        public List<Thread> WorkThreadList { get; private set; } = new List<Thread>();
        public List<FileSystemWatcher> FileSystemWatcherList = new List<FileSystemWatcher>();

        public FileService(List<string> inputDirs, string outputDir, IStitchingService stitchingService)
        {
            this.inputDirList = inputDirs;
            this.outputDir = outputDir;
            this.tempDir = Path.Combine(this.outputDir, "temp");
            this.stitchingService = stitchingService;
            StopWorkEvent = new ManualResetEvent(false);

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            foreach (var dirPAth in inputDirList)
            {
                var dir = dirPAth;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var watcher = new FileSystemWatcher(dir);
                watcher.Created += OnCreatedFile;
                FileSystemWatcherList.Add(watcher);
                WorkThreadList.Add(new Thread(async () =>
                {
                    await ServiceMainMethod(dir);
                }));
                fileEventDictionary.Add(dir, new AutoResetEvent(false));
            }
        }

        private async Task ServiceMainMethod(string dir)
        {
            var waitHandles = new WaitHandle[] { StopWorkEvent };
            waitHandles.ToList().AddRange(fileEventDictionary.Values.ToArray());
            do
            {
                var cancellationTokenSourceForObservingNewFilesInTheInputFolders = new CancellationTokenSource();
                cancellationTokenSourceForObservingNewFilesInTheInputFolders.CancelAfter(TimeSpan.FromMilliseconds(FileAppearanceTimeoutMs));
                var listenDirectoryTask = DirListenerMethod(dir, cancellationTokenSourceForObservingNewFilesInTheInputFolders.Token);
                await listenDirectoryTask;

                
                var groupDictionary = GroupingFiles(tempDir);
                foreach (var group in groupDictionary)
                {
                    Dictionary<string, int> groupNumbers = new Dictionary<string, int>();
                    foreach (var file in group.Value)
                    {
                        groupNumbers.Add(file, GetNumber(file));
                    }

                    groupNumbers = groupNumbers.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
                    var count = groupNumbers.Count;
                    int sequenceIndex = 0;
                    if (count == 1)
{
                        stitchingService.Stitch(new List<string> 
                        {
                            groupNumbers.Keys.ElementAt(0)
                        }, Path.Combine(outputDir,$"{DateTime.Now.ToString("dd_MM_yyyy_HH_mm")}_{group.Key}_{groupNumbers.Values.ElementAt(0)}.pdf"));
                    }
                    else
                    {
                        for (int i = 1; i < count; i++)
                        {
                            //if (i == count - 1)
                            //{
                            //    break;
                            //}
                            var previousIndex = groupNumbers.Values.ElementAt(i - 1);
                            var currentIndex = groupNumbers.Values.ElementAt(i);
                            if (previousIndex + 1 != currentIndex)
                            {
                                List<string> files = new List<string>();
                                for (int j = sequenceIndex; j < i; j++)
                                {
                                    files.Add(groupNumbers.Keys.ElementAt(j));
                                }
                                string postfix = files.Count > 1 ? 
                                    $"{groupNumbers.Values.ElementAt(sequenceIndex)}_{groupNumbers.Values.ElementAt(i - 1)}" : 
                                    $"{groupNumbers.Values.ElementAt(sequenceIndex)}";
                                stitchingService.Stitch(files, Path.Combine(outputDir, $"{DateTime.Now.ToString("dd_MM_yyyy_HH_mm")}_{group.Key}_{postfix}.pdf"));
                                sequenceIndex = i;
                            }
                            
                            if (i == count - 1)
                            {
                                List<string> files = new List<string>();
                                for (int j = sequenceIndex; j <= i; j++)
                                {
                                    files.Add(groupNumbers.Keys.ElementAt(j));
                                }
                                string postfix = files.Count > 1 ?
                                    $"{groupNumbers.Values.ElementAt(sequenceIndex)}_{groupNumbers.Values.ElementAt(i)}" :
                                    $"{groupNumbers.Values.ElementAt(sequenceIndex)}";
                                stitchingService.Stitch(files, Path.Combine(outputDir, $"{DateTime.Now.ToString("dd_MM_yyyy_HH_mm")}_{group.Key}_{postfix}.pdf"));
                                sequenceIndex = i;
                            }
                        }
                    }
                }
            }
            while (WaitHandle.WaitAny(waitHandles, 1000) != 0);
        }

        private Dictionary<string, List<string>> GroupingFiles(string dir)
        {
            Dictionary<string, List<string>> prefixDictionary = new Dictionary<string, List<string>>();

            var files = Directory.EnumerateFiles(dir).ToList();
            for (int i = 0; i < files.Count(); i++)
            {
                var currentPrefix = GetPrefix(files[i]);
                if (prefixDictionary.ContainsKey(currentPrefix))
                {
                    prefixDictionary[currentPrefix].Add(files[i]);
                }
                else
                {
                    prefixDictionary.Add(currentPrefix, new List<string>() { files[i] });
                }
            }

            return prefixDictionary;
        }

        private string GetPrefix(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return fileName.Split('_')[0];
        }

        private int GetNumber(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return Convert.ToInt32(fileName.Split('_', '.')[1]);
        }

        private void OnCreatedFile(object sender, FileSystemEventArgs e)
        {
            var fileEvent = fileEventDictionary.GetValueOrDefault(((FileSystemWatcher)sender).Path);
            fileEvent.Set();
        }

        private Task DirListenerMethod(string inputDir, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var file in Directory.EnumerateFiles(inputDir))
                {
                    if (StopWorkEvent.WaitOne(TimeSpan.Zero) || cancellationToken.IsCancellationRequested)
                        return Task.CompletedTask;

                    var inFile = file;
                    var fileName = Path.GetFileName(file);
                    var outFile = Path.Combine(tempDir, fileName);

                    if (IsMatchPatern(fileName))
                    {
                        if (TryOpen(inFile, 3))
                            System.IO.File.Move(inFile, outFile);
                    }
                }
            }

            return Task.CompletedTask;
        }

        private bool IsMatchPatern(string fileName)
        {
            var pattern = @"(\w)+_(\d)+\.(png|jpeg|jpg)";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            return regex.IsMatch(fileName);
        }

        private bool TryOpen(string fileName, int tryCount)
        {
            for (int i = 0; i < tryCount; i++)
            {
                try
                {
                    var file = System.IO.File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None);
                    file.Close();

                    return true;
                }
                catch (IOException)
                {
                    Thread.Sleep(5000);
                }
            }

            return false;
        }
    }
}
