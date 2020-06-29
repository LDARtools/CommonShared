using System;
using System.IO;

namespace Ldartools.Common.IO
{
    public interface IFileManager
    {
        bool FileExists(string filePath);
        bool DirectoryExists(string directoryPath);
        void CreateFile(string filePath);
        void CreateDirectory(string directoryPath);
        void AppendToFile(string filePath, string contents);
        string InternalBackupDirectory { get; }
        string ExternalBackupDirectory { get; }
        void CopyFile(string sourcePath, string destinationPath, bool overwrite = true);
		string DataDirectory { get; set; }
        string LogDirectory { get; set; }

        string[] GetFiles(string path);

        void WriteAllBytes(string filePath, byte[] contents);
        byte[] ReadAllBytes(string filePath);
        void WriteAllText(string filePath, string text);
        string ReadAllText(string filePath);
        void DeleteFile(string filePath);

        string MasterLocalDatabasePath { get; }
        string InternalDatabaseDirectory { get; }

        void BackupAllBytes(byte[] contents, string filename, BackupFileLocations backupFileLocations = BackupFileLocations.InternalStorage | BackupFileLocations.ExternalStorage);
        void BackupAllText(string text, string filename, BackupFileLocations backupFileLocations);
        void BackupFile(string sourcePath, string filename = null, BackupFileLocations backupFileLocations = BackupFileLocations.InternalStorage | BackupFileLocations.ExternalStorage, bool overwrite = true, bool compress = true);

        void CleanBackupLocations(Func<string, bool> selector, BackupFileLocations backupFileLocations = BackupFileLocations.InternalStorage | BackupFileLocations.ExternalStorage);

        Stream OpenWrite(string filePath);
        Stream OpenRead(string filePath);

        long GetStorageUsedBytes(string path);
        long GetStorageAvailableBytes(string path);
        long GetStorageTotalBytes(string path);
        float GetStorageUsedPercentage(string path);
        string GetFileHash(string path);
    }

    [Flags]
    public enum BackupFileLocations
    {
        ExternalStorage=1,
        InternalStorage=2
    }
}