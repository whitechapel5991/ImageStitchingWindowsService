using System.Collections.Generic;

namespace FileStichingLayer
{
    public interface IStitchingService
    {
        void Stitch(List<string> files, string outputPath);
    }
}