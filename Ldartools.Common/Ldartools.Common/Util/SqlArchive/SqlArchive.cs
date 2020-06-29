using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;

namespace Ldartools.Common.Util.SqlArchive
{
    public class SqlArchive : IDisposable
    {
        private readonly string _filename;
        private readonly SqlFileStreamMode _mode;
        private readonly string _folder;
        private readonly HashSet<ManafestEntry> _manafest = new HashSet<ManafestEntry>(new ManafestEntryEqualityComparer());

        public SqlArchive(string filename, SqlFileStreamMode mode, string tempFolder = null)
        {
            _filename = filename;
            _mode = mode;
            tempFolder = Path.Combine(tempFolder ?? Path.GetTempPath(), "SqlFileStream");

            //create the temp directory if not there
            var di = new DirectoryInfo(tempFolder);
            if (!di.Exists)
            {
                di.Create();
            }

            _folder = Path.Combine(tempFolder, Guid.NewGuid().ToString());

            var di2 = new DirectoryInfo(_folder);
            if (!di2.Exists)
            {
                di2.Create();
            }
        }

        public void Pack()
        {
            //check to make sure all the stream have been written to
            if (_manafest.Any(m => m.Schema == null)) throw new Exception("Not all streams have been written to.");

            var manafestArray = _manafest.ToArray();
            var manafestFilePath = Path.Combine(_folder, @"_manafest");
            var ser = new JsonSerializer();
            using (var filestream = File.Open(manafestFilePath, FileMode.Create))
            {
                using (var compressionStream = new GZipStream(filestream, CompressionMode.Compress))
                {
                    using (var sw = new StreamWriter(compressionStream))
                    using (var writer = new JsonTextWriter(sw))
                    {
                       ser.Serialize(writer, manafestArray);
                    }
                }
            }

            if (File.Exists(_filename))
            {
                File.Delete(_filename);
            }
            ZipFile.CreateFromDirectory(_folder, _filename, CompressionLevel.NoCompression, false);
        }


        public IEnumerable<ManafestEntry> UnPack()
        {
            if (_mode != SqlFileStreamMode.Read) throw new Exception("File not open for read.");
            ZipFile.ExtractToDirectory(_filename, _folder);

            var manafestFilePath = Path.Combine(_folder, @"_manafest");
            var ser = new JsonSerializer();
            using (var filestream = File.Open(manafestFilePath, FileMode.Open))
            {
                using (var compressionStream = new GZipStream(filestream, CompressionMode.Decompress))
                {
                    using (var rw = new StreamReader(compressionStream))
                    using (var reader = new JsonTextReader(rw))
                    {
                        var manafestArray = ser.Deserialize<ManafestEntry[]>(reader);
                        foreach (var entry in manafestArray)
                        {
                            entry.FilePath = Path.Combine(_folder, entry.ResultSetName);
                            _manafest.Add(entry);
                        }
                    }
                }
            }

            return _manafest.AsEnumerable();
        }

        public void Dispose()
        {
            Directory.Delete(_folder, true);
        }

        public SqlFileStream CreateStream(string resultSetName)
        {
            if(_mode != SqlFileStreamMode.Write) throw new Exception("File not open for write.");
            var fileName = Path.Combine(_folder, resultSetName);
            var entry = new ManafestEntry{ FilePath = fileName, ResultSetName = resultSetName};
            var stream = new SqlFileStream(fileName, SqlFileStreamMode.Write, entry);
            _manafest.Add(entry);
            return stream;
        }

    }
}
