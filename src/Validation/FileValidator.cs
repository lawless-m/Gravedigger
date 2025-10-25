using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Gravedigger.Logging;

namespace Gravedigger.Validation
{
    /// <summary>
    /// Validates replicated files for integrity and consistency
    /// </summary>
    public class FileValidator
    {
        private readonly ReplicationLogger _logger;

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; }
            public List<string> Warnings { get; set; }
            public int FilesValidated { get; set; }
            public long TotalBytes { get; set; }

            public ValidationResult()
            {
                Errors = new List<string>();
                Warnings = new List<string>();
                IsValid = true;
            }
        }

        public class FileInfo
        {
            public string FilePath { get; set; }
            public long FileSize { get; set; }
            public string Checksum { get; set; }
            public DateTime LastWriteTime { get; set; }
        }

        public FileValidator(ReplicationLogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates that all expected files were copied
        /// </summary>
        public ValidationResult ValidateReplication(
            string sourcePath,
            string destPath,
            List<string> expectedExtensions)
        {
            var result = new ValidationResult();

            _logger.LogInformation("Starting replication validation...");

            try
            {
                // Check if destination directory exists
                if (!Directory.Exists(destPath))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Destination directory does not exist: {destPath}");
                    return result;
                }

                // Get source files
                var sourceFiles = GetFilesByExtensions(sourcePath, expectedExtensions);
                if (sourceFiles.Count == 0)
                {
                    result.Warnings.Add($"No files found in source directory: {sourcePath}");
                    _logger.LogWarning($"No files found in source directory matching extensions: {string.Join(", ", expectedExtensions)}");
                }

                // Get destination files
                var destFiles = GetFilesByExtensions(destPath, expectedExtensions);

                // Compare file counts
                if (sourceFiles.Count != destFiles.Count)
                {
                    result.Warnings.Add($"File count mismatch: Source={sourceFiles.Count}, Destination={destFiles.Count}");
                    _logger.LogWarning($"File count mismatch: Source has {sourceFiles.Count} files, Destination has {destFiles.Count} files");
                }

                // Validate each source file has a corresponding destination file
                foreach (var sourceFile in sourceFiles)
                {
                    var fileName = Path.GetFileName(sourceFile);
                    var destFile = Path.Combine(destPath, fileName);

                    if (!File.Exists(destFile))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Missing file in destination: {fileName}");
                        continue;
                    }

                    // Compare file sizes
                    var sourceInfo = new System.IO.FileInfo(sourceFile);
                    var destInfo = new System.IO.FileInfo(destFile);

                    if (sourceInfo.Length != destInfo.Length)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"File size mismatch for {fileName}: Source={sourceInfo.Length}, Dest={destInfo.Length}");
                    }
                    else
                    {
                        result.FilesValidated++;
                        result.TotalBytes += destInfo.Length;
                    }
                }

                _logger.LogInformation($"Validation complete: {result.FilesValidated} files validated, {FormatBytes(result.TotalBytes)} total");

                if (result.Errors.Any())
                {
                    _logger.LogError($"Validation failed with {result.Errors.Count} error(s)");
                    foreach (var error in result.Errors)
                    {
                        _logger.LogError($"  - {error}");
                    }
                }

                if (result.Warnings.Any())
                {
                    foreach (var warning in result.Warnings)
                    {
                        _logger.LogWarning($"  - {warning}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Validation exception: {ex.Message}");
                _logger.LogError($"Validation failed with exception", ex);
            }

            return result;
        }

        /// <summary>
        /// Calculates checksums for files (optional, more thorough validation)
        /// </summary>
        public FileInfo GetFileInfo(string filePath)
        {
            var fileInfo = new System.IO.FileInfo(filePath);

            return new FileInfo
            {
                FilePath = filePath,
                FileSize = fileInfo.Length,
                LastWriteTime = fileInfo.LastWriteTime,
                Checksum = CalculateChecksum(filePath)
            };
        }

        /// <summary>
        /// Calculates MD5 checksum for a file
        /// </summary>
        private string CalculateChecksum(string filePath)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not calculate checksum for {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all files matching the specified extensions
        /// </summary>
        private List<string> GetFilesByExtensions(string directory, List<string> extensions)
        {
            var files = new List<string>();

            if (!Directory.Exists(directory))
            {
                _logger.LogWarning($"Directory does not exist: {directory}");
                return files;
            }

            foreach (var ext in extensions)
            {
                try
                {
                    var matchingFiles = Directory.GetFiles(directory, ext);
                    files.AddRange(matchingFiles);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error getting files with extension {ext}: {ex.Message}");
                }
            }

            return files;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
