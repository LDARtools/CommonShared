using System;
using System.IO;
using System.IO.Compression;

namespace Ldartools.Common.Util
{
    public static class ZipHelper
    {
        public static void CompressFile(string fileToZip, string destinationFile, bool overwrite = true)
        {
            // Delete existing zip file if exists
            if (File.Exists(destinationFile))
            {
                if (overwrite)
                {
                    File.Delete(destinationFile);
                }
                else
                {
                    throw new Exception($"File {destinationFile} already exists!");
                }
            }

            using (ZipArchive zip = ZipFile.Open(destinationFile, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(fileToZip, Path.GetFileName(fileToZip), CompressionLevel.Optimal);
            }

            if (!File.Exists(destinationFile))
            {
                throw new Exception($"File {destinationFile} already could not be created!");
            }
        }
    }
}
