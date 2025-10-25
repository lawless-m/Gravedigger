using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Gravedigger.Config;
using Gravedigger.Logging;
using Gravedigger.VSS;
using Gravedigger.Validation;

namespace Gravedigger.Engine
{
    /// <summary>
    /// Main replication engine that orchestrates shadow copy replication
    /// </summary>
    public class ReplicationEngine
    {
        private readonly ReplicationConfig _config;
        private readonly ReplicationLogger _logger;
        private readonly ShadowCopyManager _shadowCopyManager;
        private readonly FileValidator _fileValidator;

        public class ReplicationResult
        {
            public bool Success { get; set; }
            public int FilesReplicated { get; set; }
            public long BytesReplicated { get; set; }
            public TimeSpan Duration { get; set; }
            public string ErrorMessage { get; set; }
            public List<string> Warnings { get; set; }

            public ReplicationResult()
            {
                Warnings = new List<string>();
            }
        }

        public ReplicationEngine(ReplicationConfig config, ReplicationLogger logger)
        {
            _config = config;
            _logger = logger;
            _shadowCopyManager = new ShadowCopyManager(logger);
            _fileValidator = new FileValidator(logger);
        }

        /// <summary>
        /// Executes the replication process
        /// </summary>
        public ReplicationResult ExecuteReplication()
        {
            var startTime = DateTime.Now;
            var result = new ReplicationResult { Success = false };

            try
            {
                _logger.LogInformation("=== Starting Replication Process ===");
                _logger.LogInformation($"Source: {_config.DatabasePath}");
                _logger.LogInformation($"Destination: {_config.DestinationPath}");

                // Step 1: Verify VSS service is running
                if (!VerifyVssService())
                {
                    result.ErrorMessage = "VSS service is not running";
                    return result;
                }

                // Step 2: Get latest shadow copy
                var shadowCopy = _shadowCopyManager.GetLatestShadowCopy(_config.SourceVolume);
                if (shadowCopy == null)
                {
                    result.ErrorMessage = $"No shadow copy found for volume {_config.SourceVolume}";
                    _logger.LogError(result.ErrorMessage);
                    return result;
                }

                // Check shadow copy age
                var shadowAge = DateTime.Now - shadowCopy.CreationTime;
                if (shadowAge.TotalHours > _config.MaxReplicaAgeHours)
                {
                    result.Warnings.Add($"Shadow copy is {shadowAge.TotalHours:F1} hours old (max allowed: {_config.MaxReplicaAgeHours})");
                    _logger.LogWarning(result.Warnings.Last());
                }

                // Step 3: Build shadow copy path
                var shadowDbPath = _shadowCopyManager.GetShadowCopyPath(shadowCopy, _config.DatabasePath);

                // Step 4: Create destination directory with generation support
                var destPath = CreateDestinationPath();

                // Step 5: Copy files with retry logic
                var copyResult = CopyFilesWithRetry(shadowDbPath, destPath);
                result.FilesReplicated = copyResult.Item1;
                result.BytesReplicated = copyResult.Item2;

                if (result.FilesReplicated == 0)
                {
                    result.ErrorMessage = "No files were copied";
                    _logger.LogError(result.ErrorMessage);
                    return result;
                }

                // Step 6: Validate replication
                var validationResult = _fileValidator.ValidateReplication(
                    shadowDbPath,
                    destPath,
                    _config.FileExtensions);

                if (!validationResult.IsValid)
                {
                    result.ErrorMessage = "Replication validation failed";
                    result.Warnings.AddRange(validationResult.Errors);
                    _logger.LogError(result.ErrorMessage);
                    return result;
                }

                result.Warnings.AddRange(validationResult.Warnings);

                // Step 7: Cleanup old generations
                CleanupOldGenerations();

                // Success!
                result.Success = true;
                result.Duration = DateTime.Now - startTime;

                _logger.LogInformation($"=== Replication Completed Successfully ===");
                _logger.LogInformation($"Files Replicated: {result.FilesReplicated}");
                _logger.LogInformation($"Bytes Replicated: {FormatBytes(result.BytesReplicated)}");
                _logger.LogInformation($"Duration: {result.Duration.TotalSeconds:F2} seconds");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.LogCritical($"Replication failed with exception", ex);
            }
            finally
            {
                result.Duration = DateTime.Now - startTime;
            }

            return result;
        }

        /// <summary>
        /// Verifies that the VSS service is running
        /// </summary>
        private bool VerifyVssService()
        {
            _logger.LogInformation("Checking VSS service status...");

            if (!_shadowCopyManager.IsVssServiceRunning())
            {
                _logger.LogError("VSS service is not running. Please start the service and try again.");
                _logger.LogError("To start VSS service, run: net start VSS");
                return false;
            }

            _logger.LogInformation("VSS service is running");
            return true;
        }

        /// <summary>
        /// Creates destination path with generation support
        /// </summary>
        private string CreateDestinationPath()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var generationPath = Path.Combine(_config.DestinationPath, timestamp);

            _logger.LogInformation($"Creating destination directory: {generationPath}");

            if (!Directory.Exists(generationPath))
            {
                Directory.CreateDirectory(generationPath);
            }

            return generationPath;
        }

        /// <summary>
        /// Copies files with retry logic
        /// </summary>
        private Tuple<int, long> CopyFilesWithRetry(string sourceDir, string destDir)
        {
            int totalFilesCopied = 0;
            long totalBytesCopied = 0;

            foreach (var extension in _config.FileExtensions)
            {
                _logger.LogInformation($"Copying files with extension: {extension}");

                try
                {
                    if (!Directory.Exists(sourceDir))
                    {
                        _logger.LogWarning($"Source directory does not exist: {sourceDir}");
                        continue;
                    }

                    var files = Directory.GetFiles(sourceDir, extension);
                    _logger.LogInformation($"Found {files.Length} file(s) matching {extension}");

                    foreach (var sourceFile in files)
                    {
                        var fileName = Path.GetFileName(sourceFile);
                        var destFile = Path.Combine(destDir, fileName);

                        // Copy with retry
                        if (CopyFileWithRetry(sourceFile, destFile))
                        {
                            totalFilesCopied++;
                            var fileInfo = new FileInfo(destFile);
                            totalBytesCopied += fileInfo.Length;

                            _logger.LogInformation($"  Copied: {fileName} ({FormatBytes(fileInfo.Length)})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error copying {extension} files: {ex.Message}", ex);
                }
            }

            return Tuple.Create(totalFilesCopied, totalBytesCopied);
        }

        /// <summary>
        /// Copies a single file with retry logic
        /// </summary>
        private bool CopyFileWithRetry(string sourceFile, string destFile)
        {
            int attempt = 0;
            int maxAttempts = _config.RetryOnFailure ? _config.RetryAttempts + 1 : 1;

            while (attempt < maxAttempts)
            {
                try
                {
                    if (attempt > 0)
                    {
                        _logger.LogWarning($"Retry attempt {attempt} for {Path.GetFileName(sourceFile)}");
                        Thread.Sleep(_config.RetryDelayMinutes * 60 * 1000); // Convert minutes to milliseconds
                    }

                    File.Copy(sourceFile, destFile, overwrite: true);
                    return true;
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt >= maxAttempts)
                    {
                        _logger.LogError($"Failed to copy {Path.GetFileName(sourceFile)} after {attempt} attempt(s): {ex.Message}");
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Cleans up old generation directories
        /// </summary>
        private void CleanupOldGenerations()
        {
            try
            {
                _logger.LogInformation($"Cleaning up old generations (keeping {_config.RetainGenerations})...");

                if (!Directory.Exists(_config.DestinationPath))
                    return;

                var generations = Directory.GetDirectories(_config.DestinationPath)
                    .Select(d => new DirectoryInfo(d))
                    .OrderByDescending(d => d.CreationTime)
                    .ToList();

                if (generations.Count <= _config.RetainGenerations)
                {
                    _logger.LogInformation($"Only {generations.Count} generation(s) exist, no cleanup needed");
                    return;
                }

                var toDelete = generations.Skip(_config.RetainGenerations).ToList();
                foreach (var dir in toDelete)
                {
                    _logger.LogInformation($"Deleting old generation: {dir.Name}");
                    Directory.Delete(dir.FullName, recursive: true);
                }

                _logger.LogInformation($"Deleted {toDelete.Count} old generation(s)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error during generation cleanup: {ex.Message}");
            }
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
