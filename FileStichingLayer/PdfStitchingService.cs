using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileStichingLayer
{
    public class PdfStitchingService : IStitchingService
    {
        object _lock = new object();
        public PdfStitchingService()
        {

        }

        public void Stitch(List<string> files, string outputPath)
        {
            lock (_lock)
            {
                DeleteFromListIfFileDoesntExist(ref files);
                if (files.Count == 0)
                {
                    return;
                }

                var document = new Document();
                var section = document.AddSection();

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var enc1252 = Encoding.GetEncoding(1252);

                foreach (var file in files)
                {
                    var img = section.AddImage(file);
                    img.Height = document.DefaultPageSetup.PageHeight;
                    img.Width = document.DefaultPageSetup.PageWidth;

                    //section.AddPageBreak();
                }

                var render = new PdfDocumentRenderer();
                render.Document = document;

                render.RenderDocument();
                render.Save(outputPath);

                //DeleteFiles(files);
            }
        }

        private void DeleteFiles(List<string> files)
        {
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        private void DeleteFromListIfFileDoesntExist(ref List<string> files)
        {
            foreach (var file in files)
            {
                if (!File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }
}
