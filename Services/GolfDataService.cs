using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GolfClubSelectionApp.Services
{
    public class GolfDataService
    {
        private readonly string _courseFilePath;
        private readonly string _clubFilePath;
        private readonly string _exportFolderPath;
        private readonly string _importFolderPath;
        private readonly string _archiveFolderPath;
        private readonly string _backupFolderPath;

        private const string ExportFolderName = "KukiGolf";
        private const string CourseFileName = "GolfCourseData";
        private const string ClubFileName = "ClubAndDistance";

        public GolfDataService()
        {
            _courseFilePath = Path.Combine(FileSystem.AppDataDirectory, CourseFileName + ".txt");
            _clubFilePath = Path.Combine(FileSystem.AppDataDirectory, ClubFileName + ".txt");

            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AppData", ExportFolderName);

            _exportFolderPath = Path.Combine(basePath, "Exports");
            _importFolderPath = Path.Combine(basePath, "Imports");
            _archiveFolderPath = Path.Combine(basePath, "Archive");
            _backupFolderPath = Path.Combine(FileSystem.AppDataDirectory, "Backups");

            Directory.CreateDirectory(_exportFolderPath);
            Directory.CreateDirectory(_importFolderPath);
            Directory.CreateDirectory(_archiveFolderPath);
            Directory.CreateDirectory(_backupFolderPath);
        }

        public async Task ExportDatabaseAsync()
        {
            await ExportFileAsync(_courseFilePath, CourseFileName);
            await ExportFileAsync(_clubFilePath, ClubFileName);
        }

        private async Task ExportFileAsync(string localFilePath, string fileName)
        {
            if (!File.Exists(localFilePath))
                return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string baseFileName = $"DB_{timestamp}_{fileName}";
            string tempFilePath = Path.Combine(_exportFolderPath, $"{baseFileName}.tmp");
            string finalFilePath = Path.Combine(_exportFolderPath, $"{baseFileName}.txt");
            string readyFilePath = Path.Combine(_exportFolderPath, $"{baseFileName}.ready");
            string latestFilePath = Path.Combine(_exportFolderPath, $"LATEST_{fileName}.txt");

            await Task.Run(() => File.Copy(localFilePath, tempFilePath, overwrite: true));
            await Task.Run(() =>
            {
                if (File.Exists(finalFilePath))
                    File.Delete(finalFilePath);
                File.Move(tempFilePath, finalFilePath);
            });
            await Task.Run(() => File.WriteAllText(readyFilePath, timestamp));
            await Task.Run(() => File.WriteAllText(latestFilePath, $"{baseFileName}.txt"));
        }

        public async Task<bool> ImportDatabaseAsync()
        {
            bool courseImported = await ImportFileAsync(_courseFilePath, CourseFileName);
            bool clubImported = await ImportFileAsync(_clubFilePath, ClubFileName);
            return courseImported || clubImported;
        }

        private async Task<bool> ImportFileAsync(string localFilePath, string fileName)
        {
            string latestPointer = Path.Combine(_exportFolderPath, $"LATEST_{fileName}.txt");
            string? fileToImport = null;

            if (File.Exists(latestPointer))
            {
                string latestFileName = (await Task.Run(() => File.ReadAllText(latestPointer))).Trim();
                string readyFilePath = Path.Combine(_exportFolderPath, latestFileName.Replace(".txt", ".ready"));
                if (File.Exists(readyFilePath) && File.Exists(Path.Combine(_exportFolderPath, latestFileName)))
                    fileToImport = latestFileName;
            }

            if (fileToImport == null)
            {
                var readyFiles = Directory.GetFiles(_exportFolderPath, $"DB_*_{fileName}.ready")
                    .OrderByDescending(f => f)
                    .ToList();
                if (readyFiles.Any())
                {
                    string readyFile = readyFiles.First();
                    string txtFile = readyFile.Replace(".ready", ".txt");
                    if (File.Exists(txtFile))
                        fileToImport = Path.GetFileName(txtFile);
                }
            }

            if (fileToImport == null)
                return false;

            string sourceFilePath = Path.Combine(_exportFolderPath, fileToImport);

            if (!await WaitForFileStabilityAsync(sourceFilePath))
                return false;

            if (File.Exists(localFilePath))
            {
                string backupFileName = $"LocalBackup_{DateTime.Now:yyyy-MM-dd_HHmmss}_{fileName}";
                string backupFilePath = Path.Combine(_backupFolderPath, backupFileName);
                await Task.Run(() => File.Copy(localFilePath, backupFilePath, overwrite: true));
            }

            string tempImportPath = localFilePath + ".importing.tmp";
            await Task.Run(() => File.Copy(sourceFilePath, tempImportPath, overwrite: true));
            if (File.Exists(localFilePath))
                File.Move(localFilePath, localFilePath + ".old", overwrite: true);
            File.Move(tempImportPath, localFilePath, overwrite: true);

            return true;
        }

        private async Task<bool> WaitForFileStabilityAsync(string filePath)
        {
            int maxRetries = 30;
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                if (!File.Exists(filePath))
                {
                    await Task.Delay(2000);
                    retryCount++;
                    continue;
                }
                long size1 = new FileInfo(filePath).Length;
                await Task.Delay(500);
                long size2 = new FileInfo(filePath).Length;
                if (size1 == size2 && size1 > 0)
                    return true;
                await Task.Delay(2000);
                retryCount++;
            }
            return false;
        }
    }
}
